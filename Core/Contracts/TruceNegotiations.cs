using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public enum TruceAction { Propose, Accept, Decline, Counter, Confirm }
public sealed record TruceInput(EntityId Id,TruceAction Action,EntityId Owner,EntityId Target,EntityId? Source=null,int Days=0);
public sealed record TruceTerms(EntityId Origin,int Version,EntityId? Parent,EntityId A,EntityId B,long From,long Until,long ReplyBy);
public sealed record TrucePaper(TruceInput Input,TruceTerms Terms,EntityId Author,EntityId Recipient,EntityId Address,long At,long Revision,FactionStanceAuthority Authority,string Message);
public sealed record TruceBody(TruceAction Action,TruceTerms Terms);
public sealed record TruceFormation(EntityId Acceptance,TruceTerms Terms,long At,long Revision);
public sealed record TruceFulfillment(EntityId Acceptance,EntityId Actor,long At,long Revision,FactionStanceInput? Withdrawal);
public sealed record TruceState(int Schema,string Contract,TrucePaper[] Papers,FactionNoticeOpening[] Openings,TruceFormation[] Formations,TruceFulfillment[] Fulfillments);
public sealed record TruceCard(EntityId Paper,EntityId Other,string Stage,int Version,long Until,bool CanAccept,bool CanDecline);
// Bilateral papers are versioned consent, not an instantaneous global peace switch.
public static class TruceNegotiations
{
    public const string Policy="head-versioned-received-bilateral-nonaggression-own-knowledge-v1",Kind="truce-negotiation-paper";
    public const long Day=86400;public const int MaximumNpcDays=30;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.TrucePolicy==Policy;
    static TruceState State(ContractWorld w)=>w.Truces??new(1,Policy,[],[],[],[]);
    static bool Before(long t,long r,long at,long rev)=>t<at||t==at&&r<=rev;
    static string Hash(string s)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(s));
    static string MessageId(EntityId id)=>"message:truce/"+Hash(id.Value);
    static EntityId AutoId(string action,TrucePaper p)=>new("request:truce-"+action+"/"+Hash(p.Input.Id.Value));
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>State(w).Papers.Any(p=>p.Input.Id==id);
    static bool Opened(ContractWorld w,TrucePaper p,EntityId actor,long at,long rev)=>State(w).Openings.Any(o=>o.Message==p.Message&&o.Actor==actor&&Before(o.At,o.Revision,at,rev));
    static TrucePaper[] PapersAt(ContractWorld w,long at,long rev)=>State(w).Papers.Where(p=>Before(p.At,p.Revision,at,rev)).ToArray();
    static TrucePaper Prepare(ContractWorld w,TruceInput i,EntityId actor,long at,long rev)
    {
        Need(Enabled(w)&&EntityId.IsValid(i.Id.Value)&&Enum.IsDefined(i.Action)&&i.Owner!=i.Target,"交渉の当事者・入力が不正です");var auth=FactionStances.ResolveAuthority(w,actor,i.Owner,at,rev);Need(auth!=null,"当事者の締結権者ではありません");var target=w.NewGame!.Start.Factions.SingleOrDefault(f=>f.Id==i.Target.Value);Need(target!=null,"相手の当事者が不明です");TruceTerms terms;
        if(i.Action==TruceAction.Propose){Need(actor==w.PlayerControl,"NPCからの新規交渉開始はこの版の対象外です");Need(i.Source==null&&i.Days is >=1 and <=90,"不戦期間は1〜90日で提示してください");terms=new(i.Id,1,null,i.Owner,i.Target,at,checked(at+i.Days*Day),checked(at+Math.Min(7,i.Days)*Day));}
        else{
            var source=PapersAt(w,at,rev).SingleOrDefault(p=>p.Input.Id==i.Source);Need(source!=null&&source.Recipient==actor&&source.Input.Owner==i.Target&&source.Input.Target==i.Owner&&Opened(w,source,actor,at,rev),"本人に到達して開封した同一原本が必要です");
            Need(!PapersAt(w,at,rev).Any(p=>p.Input.Id!=i.Id&&p.Input.Source==source.Input.Id&&p.Author==actor),"この版には既に返答しています");
            if(i.Action==TruceAction.Confirm){Need(source.Input.Action==TruceAction.Accept&&Forms(w,at,rev).Any(f=>f.Acceptance==source.Input.Id),"成立を確認した承諾の原本が必要です");Need(i.Days==0,"確認書で条件は変えません");terms=source.Terms;}
            else{Need(source.Input.Action is TruceAction.Propose or TruceAction.Counter,"提案または対案へ返答してください");Need(at<=source.Terms.ReplyBy&&at<source.Terms.Until,"返答期限を過ぎています");
                if(i.Action==TruceAction.Counter){Need(actor!=w.PlayerControl&&source.Input.Action==TruceAction.Propose&&i.Days==MaximumNpcDays&&source.Terms.Until-source.Terms.From>MaximumNpcDays*Day,"相手の調整方針による対案ではありません");terms=new(source.Terms.Origin,2,source.Input.Id,source.Terms.A,source.Terms.B,at,checked(at+MaximumNpcDays*Day),source.Terms.ReplyBy);}
                else{Need(i.Days==0&&i.Action is TruceAction.Accept or TruceAction.Decline,"返答で本文を勝手に変更できません");if(actor!=w.PlayerControl)Need(i.Action==TruceAction.Accept&&source.Terms.Until-source.Terms.From<=MaximumNpcDays*Day,"NPCの条件上限と返答が一致しません");terms=source.Terms;}
            }
        }
        if(i.Action==TruceAction.Confirm){var source=State(w).Papers.Single(p=>p.Input.Id==i.Source);Need(i.Id==AutoId("confirm",source),"成立確認のIDが原本と違います");}else if(actor!=w.PlayerControl){var source=State(w).Papers.Single(p=>p.Input.Id==i.Source);Need(i.Id==AutoId(i.Action==TruceAction.Counter?"counter":"accept",source),"NPCの返答原本IDが不正です");}
        return new(i,terms,actor,new(target.Head),new(target.Home),at,rev,auth,MessageId(i.Id));
    }
    static MessageEnvelope Envelope(TrucePaper p)=>new(p.Message,p.Input.Id.Value,Kind,p.Author.Value,p.Recipient.Value,System.Text.Encoding.UTF8.GetString(ContractJson.Write(new TruceBody(p.Input.Action,p.Terms))),p.Address.Value,p.At);
    internal static void Dispatch(ContractWorld w,TruceInput input,EntityId actor)
    {Need(!OwnIdUsed(w,input.Id)&&!FactionStances.OtherIdUsed(w,input.Id,includeTruces:false)&&!FactionStances.OwnIdUsed(w,input.Id),"別の操作に使ったIDです");var p=Prepare(w,input,actor,w.ElapsedSeconds,w.CommitSequence);w.Truces=State(w) with{Papers=[..State(w).Papers,p]};MessageFlow.Send(w.Messaging,Envelope(p),p.At,1);w.SealedMessages=[..w.SealedMessages,new(p.Message)];}
    public static string? Address(ContractWorld w,MessageEnvelope m)=>m.Kind==Kind&&State(w).Papers.SingleOrDefault(p=>p.Message==m.Id) is {} p?NewGameWorld.Node(p.Address.Value):null;
    internal static void RecordOpening(ContractWorld w,EntityId actor,string message)
    {var s=State(w);if(!Enabled(w)||s.Openings.Any(o=>o.Message==message)||!s.Papers.Any(p=>p.Message==message&&p.Recipient==actor))return;Need(w.Messaging.Attempts.Any(a=>a.MessageId==message&&a.ReceiverId==actor.Value&&a.Status==AttemptStatus.Delivered)&&MessageFlow.Knows(w.Messaging,message,actor.Value),"到達・本人開封が必要です");w.Truces=s with{Openings=[..s.Openings,new(message,actor,w.ElapsedSeconds,w.CommitSequence)]};}
    static TruceFormation[] Forms(ContractWorld w,long at,long rev,bool discover=false)
    {
        var papers=PapersAt(w,at,rev);var result=new List<TruceFormation>();foreach(var p in papers.Where(p=>p.Input.Action==TruceAction.Accept)){
            var source=papers.SingleOrDefault(s=>s.Input.Id==p.Input.Source);if(source==null||source.Terms!=p.Terms)continue;
            var delivery=w.Messaging.Attempts.Where(a=>a.MessageId==p.Message&&a.ReceiverId==p.Recipient.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=at).OrderBy(a=>a.FinishedAt).FirstOrDefault();if(delivery?.FinishedAt is not {} arrived||arrived>p.Terms.ReplyBy||arrived>=p.Terms.Until)continue;
            var stored=State(w).Formations.SingleOrDefault(f=>f.Acceptance==p.Input.Id);if(stored==null&&!discover)continue;long revision=stored?.Revision??w.CommitSequence;if(arrived==at&&revision>rev)continue;result.Add(new(p.Input.Id,p.Terms,arrived,revision));
        }return result.OrderBy(f=>f.At).ThenBy(f=>f.Acceptance.Value,StringComparer.Ordinal).ToArray();
    }
    public static TruceFormation[] Known(ContractWorld w,EntityId actor,long? at=null,long? revision=null)
    {long time=at??w.ElapsedSeconds,rev=revision??w.CommitSequence;Need(time>=0&&time<=w.ElapsedSeconds&&rev>=0&&rev<=w.CommitSequence,"未確定の交渉知識です");return Forms(w,time,rev).Where(f=>{var p=State(w).Papers.Single(p=>p.Input.Id==f.Acceptance);return Opened(w,p,actor,time,rev)||State(w).Papers.Any(c=>c.Input.Action==TruceAction.Confirm&&c.Input.Source==p.Input.Id&&Opened(w,c,actor,time,rev));}).ToArray();}
    internal static void Update(ContractWorld w)
    {
        if(!Enabled(w))return;foreach(var p in State(w).Papers){if(p.Recipient==w.PlayerControl||State(w).Openings.Any(o=>o.Message==p.Message)||!w.Messaging.Attempts.Any(a=>a.MessageId==p.Message&&a.ReceiverId==p.Recipient.Value&&a.Status==AttemptStatus.Delivered))continue;if(!MessageFlow.Knows(w.Messaging,p.Message,p.Recipient.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(p.Message,p.Recipient.Value,w.ElapsedSeconds,p.Message)];RecordOpening(w,p.Recipient,p.Message);}
        w.Truces=State(w) with{Formations=Forms(w,w.ElapsedSeconds,w.CommitSequence,true)};
    }
    static (EntityId Actor,TruceInput? Reply,TruceFormation? Fulfill)? Next(ContractWorld w,bool allowReplies)
    {
        foreach(var f in State(w).Formations){var acceptance=State(w).Papers.Single(p=>p.Input.Id==f.Acceptance);foreach(var actor in new[]{acceptance.Author,acceptance.Recipient})if(Known(w,actor).Any(k=>k.Acceptance==f.Acceptance)&&w.ElapsedSeconds<Math.Min(f.Terms.Until,long.MaxValue)&&!State(w).Fulfillments.Any(q=>q.Acceptance==f.Acceptance&&q.Actor==actor))return(actor,null,f);}
        if(allowReplies)foreach(var p in State(w).Papers.Where(p=>p.Recipient!=w.PlayerControl&&p.Input.Action is TruceAction.Propose or TruceAction.Counter).OrderBy(p=>p.At).ThenBy(p=>p.Input.Id.Value,StringComparer.Ordinal))if(Opened(w,p,p.Recipient,w.ElapsedSeconds,w.CommitSequence)&&w.ElapsedSeconds<=p.Terms.ReplyBy&&w.ElapsedSeconds<p.Terms.Until&&!State(w).Papers.Any(q=>q.Input.Source==p.Input.Id&&q.Author==p.Recipient)){
            bool counter=p.Terms.Until-p.Terms.From>MaximumNpcDays*Day;return(p.Recipient,new(AutoId(counter?"counter":"accept",p),counter?TruceAction.Counter:TruceAction.Accept,p.Input.Target,p.Input.Owner,p.Input.Id,counter?MaximumNpcDays:0),null);
        }return null;
    }
    internal static void TakeTurns(WorldSession session)
    {for(int n=0;n<32;n++){var w=session.DiagnosticSnapshot();var next=Next(w,!w.Paused);if(next==null)return;session.ActOnTruce(next.Value.Actor,next.Value.Reply,next.Value.Fulfill);}}
    internal static void ApplyAutomatic(ContractWorld w,EntityId actor,TruceInput? reply,TruceFormation? f)
    {
        var prior=w.ForkTransportLocation();prior.CommitSequence=w.CommitSequence-1;var expected=Next(prior,!w.Paused);Need(expected!=null&&expected.Value.Actor==actor&&expected.Value.Reply==reply&&expected.Value.Fulfill==f,"受領・開封した条件からの処理ではありません");
        if(reply!=null){Dispatch(w,reply,actor);return;}Need(f!=null,"成立原本がありません");var p=State(w).Papers.Single(p=>p.Input.Id==f.Acceptance);EntityId owner=actor==p.Author?p.Input.Owner:p.Input.Target,target=actor==p.Author?p.Input.Target:p.Input.Owner;
        if(actor==p.Recipient)Dispatch(w,new(AutoId("confirm",p),TruceAction.Confirm,owner,target,p.Input.Id),actor);
        var claim=FactionStances.OwnAt(w,actor,w.ElapsedSeconds,w.CommitSequence).SingleOrDefault(c=>c.FromOwner==owner&&c.ToOwner==target&&c.EffectiveFrom<=w.ElapsedSeconds&&w.ElapsedSeconds<c.Until);FactionStanceInput? decision=null;
        if(claim!=null){decision=new(new("decision:truce-end/"+Hash(f.Acceptance.Value+"/"+actor.Value)),FactionStanceAction.WithdrawOwnDeclaration,owner,target,w.ElapsedSeconds,0,claim.Declaration);FactionStances.Apply(w,decision,actor);FactionNotices.Dispatch(w,new(new("request:truce-end-notice/"+Hash(decision.Id.Value)),claim.Declaration),actor);}
        w.Truces=State(w) with{Fulfillments=[..State(w).Fulfillments,new(f.Acceptance,actor,w.ElapsedSeconds,w.CommitSequence,decision)]};
    }
    public static bool WillNotInitiate(ContractWorld w,EntityId owner,EntityId target,long at,long revision)
    {if(!Enabled(w))return false;var head=w.NewGame!.Start.Factions.SingleOrDefault(f=>f.Id==owner.Value)?.Head;if(head==null)return false;return Known(w,new(head),at,revision).Any(f=>Math.Max(f.At,f.Terms.From)<=at&&at<f.Terms.Until&&(f.Terms.A==owner&&f.Terms.B==target||f.Terms.B==owner&&f.Terms.A==target));}
    internal static long? NextBoundary(ContractWorld w,long target)
    {if(!Enabled(w))return null;var times=State(w).Papers.SelectMany(p=>new[]{p.Terms.ReplyBy+1,p.Terms.Until}).ToList();if(Next(w,true)!=null)times.Add(w.ElapsedSeconds+1);return times.Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();}
    internal static IEnumerable<(long At,long Revision)> ExternalInputs(ContractWorld w)=>State(w).Papers.Select(p=>(p.At,p.Revision)).Concat(State(w).Openings.Where(o=>o.Actor==w.PlayerControl).Select(o=>(o.At,o.Revision))).Concat(State(w).Fulfillments.Select(f=>(f.At,f.Revision)));
    internal static IEnumerable<long> Changes(ContractWorld w)=>State(w).Papers.SelectMany(p=>new[]{p.At,p.Terms.From,p.Terms.Until,p.Terms.ReplyBy}).Concat(State(w).Openings.Select(o=>o.At)).Concat(State(w).Formations.Select(f=>f.At));
    internal static int BoundaryCost(ContractWorld w)=>!Enabled(w)?0:2*State(w).Papers.Select(p=>p.Recipient).Distinct().Count();
    internal static void Rewind(ContractWorld w,long at,long rev){if(w.Truces is {} s)w.Truces=s with{Papers=s.Papers.Where(p=>Before(p.At,p.Revision,at,rev)).ToArray(),Openings=s.Openings.Where(o=>Before(o.At,o.Revision,at,rev)).ToArray(),Formations=s.Formations.Where(f=>Before(f.At,f.Revision,at,rev)).ToArray(),Fulfillments=s.Fulfillments.Where(f=>Before(f.At,f.Revision,at,rev)).ToArray()};}
    public static TruceCard[] View(ContractWorld w)
    {
        if(!Enabled(w))return [];var actor=w.PlayerControl;var papers=State(w).Papers.Where(p=>p.Author==actor||Opened(w,p,actor,w.ElapsedSeconds,w.CommitSequence)).ToArray();return papers.Where(p=>p.Input.Action is TruceAction.Propose or TruceAction.Counter).GroupBy(p=>p.Terms.Origin).Select(g=>g.OrderByDescending(p=>p.Terms.Version).First()).Select(p=>{
            bool incoming=p.Recipient==actor,answered=State(w).Papers.Any(r=>r.Input.Source==p.Input.Id&&(r.Author==actor||Opened(w,r,actor,w.ElapsedSeconds,w.CommitSequence)));bool respond=incoming&&!answered&&w.ElapsedSeconds<=p.Terms.ReplyBy&&w.ElapsedSeconds<p.Terms.Until;bool formed=Known(w,actor).Any(f=>f.Terms==p.Terms);string stage=formed?w.ElapsedSeconds<p.Terms.Until?"成立を確認・不戦期間中":"成立を確認・期間終了":w.ElapsedSeconds>p.Terms.ReplyBy?"返答期限経過":answered?"返答済み・相手の到達は別途確認":incoming?"相手の条件を開封・返答可能":"提示発送・返答待ち";return new TruceCard(p.Input.Id,incoming?p.Input.Owner:p.Input.Target,stage,p.Terms.Version,p.Terms.Until,respond,respond);}).ToArray();
    }
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.Truces==null,"旧世界へ交渉を混在できません");return;}var s=State(w);Need(w.Truces!=null&&s.Schema==1&&s.Contract==Policy&&s.Papers!=null&&s.Openings!=null&&s.Formations!=null&&s.Fulfillments!=null,"交渉原本がありません");Need(s.Papers.Select(p=>p.Input.Id).Distinct().Count()==s.Papers.Length&&s.Openings.Select(o=>o.Message).Distinct().Count()==s.Openings.Length,"交渉・開封が重複しています");
        Need(s.Papers.Zip(s.Papers.Skip(1)).All(pair=>pair.First.At<=pair.Second.At&&pair.First.Revision<pair.Second.Revision),"交渉原本の順序が不正です");foreach(var p in s.Papers){Need(p.At>=0&&p.At<=w.ElapsedSeconds&&p.Revision>0&&p.Revision<=w.CommitSequence&&p==Prepare(w,p.Input,p.Author,p.At,p.Revision),"交渉の版・当事者・権限・原本が違います");Need(!FactionStances.OtherIdUsed(w,p.Input.Id,includeTruces:false)&&!FactionStances.OwnIdUsed(w,p.Input.Id),"交渉IDが他の操作と重複しています");Need(ContractJson.CanonicalHash(w.Messaging.Messages.Single(m=>m.Id==p.Message))==ContractJson.CanonicalHash(Envelope(p))&&w.SealedMessages.Contains(new(p.Message)),"交渉原文・封印が違います");foreach(var c in w.Couriers!.Missions.Where(c=>w.Messaging.Attempts.Any(a=>a.Id==c.Attempt&&a.MessageId==p.Message)))Need(c.Destination==NewGameWorld.Node(p.Address.Value),"相手の現在地へ宛先をすり替えています");}
        foreach(var o in s.Openings){var p=s.Papers.Single(p=>p.Message==o.Message);Need(p.Recipient==o.Actor&&o.At>=p.At&&o.At<=w.ElapsedSeconds&&o.Revision>p.Revision&&o.Revision<=w.CommitSequence&&MessageFlow.LearnedAt(w.Messaging,o.Message,o.Actor.Value)==o.At&&w.Messaging.Attempts.Any(a=>a.MessageId==o.Message&&a.ReceiverId==o.Actor.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=o.At),"交渉の到達・本人開封が一致しません");}
        foreach(var p in s.Papers)Need(MessageFlow.Knows(w.Messaging,p.Message,p.Recipient.Value)==s.Openings.Any(o=>o.Message==p.Message),"開封前に条件を知っています");Need(w.Messaging.Messages.Count(m=>m.Kind==Kind)==s.Papers.Length&&ContractJson.CanonicalHash(s.Formations)==ContractJson.CanonicalHash(Forms(w,w.ElapsedSeconds,w.CommitSequence,true)),"成立の到達・版が一致しません");
        foreach(var f in s.Formations){var p=s.Papers.Single(p=>p.Input.Id==f.Acceptance);Need(f.Revision>p.Revision&&f.Revision<=w.CommitSequence&&f.At>=p.At,"成立の順序が不正です");}Need(s.Fulfillments.Select(f=>(f.Acceptance,f.Actor)).Distinct().Count()==s.Fulfillments.Length,"不戦履行が重複しています");foreach(var f in s.Fulfillments){var form=Known(w,f.Actor,f.At,f.Revision).SingleOrDefault(k=>k.Acceptance==f.Acceptance);Need(form!=null&&f.At>=form.At&&f.At<form.Terms.Until&&f.Revision>0&&f.Revision<=w.CommitSequence,"未認識の成立から履行しています");var acceptance=s.Papers.Single(p=>p.Input.Id==f.Acceptance);EntityId owner=f.Actor==acceptance.Author?acceptance.Input.Owner:acceptance.Input.Target,target=f.Actor==acceptance.Author?acceptance.Input.Target:acceptance.Input.Owner;var claim=FactionStances.OwnAt(w,f.Actor,f.At,f.Revision-1).SingleOrDefault(c=>c.FromOwner==owner&&c.ToOwner==target&&c.EffectiveFrom<=f.At&&f.At<c.Until);FactionStanceInput? expected=claim==null?null:new(new("decision:truce-end/"+Hash(f.Acceptance.Value+"/"+f.Actor.Value)),FactionStanceAction.WithdrawOwnDeclaration,owner,target,f.At,0,claim.Declaration);Need(f.Withdrawal==expected,"自家の原判断と撤回の対象が違います");if(f.Actor==acceptance.Recipient)Need(s.Papers.Any(c=>c.Input.Action==TruceAction.Confirm&&c.Input.Id==AutoId("confirm",acceptance)&&c.Input.Source==f.Acceptance&&c.Author==f.Actor&&c.At==f.At&&c.Revision==f.Revision),"相手への成立確認の原本がありません");if(f.Withdrawal is {} d)Need(w.FactionStances?.Decisions.Any(q=>q.Input==d&&q.Authority.Issuer==f.Actor&&q.At==f.At&&q.Revision==f.Revision)==true,"自家の撤回原本がありません");}
    }
}
