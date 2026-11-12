using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
[Flags]public enum TreatyClause { None=0,NonAggression=1,Passage=2,Supply=4,Defense=8 }
public enum TreatyAction { Propose, Accept, Decline, Counter, Confirm }
public sealed record SubmissionConditions(bool GuaranteeLands,int MusterMen,long TributeRice,bool RestrictDiplomacy,EntityId? Hostage)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EntityId? Precedent {get;init;}
}
public sealed record TreatyInput(EntityId Id,TreatyAction Action,EntityId Owner,EntityId Target,EntityId? Source=null,int Days=0,TreatyClause Clauses=TreatyClause.None,int NoticeDays=0)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CouncilDepartureCopy? DepartureCopy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CouncilBallot? Ballot {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CouncilCharter? Charter {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SubmissionConditions? Submission {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? AttachHouseScale{get;init;}
}
public sealed record TreatyTerms(EntityId Origin,int Version,EntityId? Parent,EntityId A,EntityId B,long From,long Until,long ReplyBy,TreatyClause Clauses,int NoticeSeconds)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CouncilDepartureCopy? DepartureCopy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CouncilBallot? Ballot {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CouncilCharter? Charter {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SubmissionConditions? Submission {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CourtScale? DeclaredScale{get;init;}
}
public sealed record TreatyPaper(TreatyInput Input,TreatyTerms Terms,EntityId Author,EntityId Recipient,EntityId Address,long At,long Revision,FactionStanceAuthority Authority,string Message);
public sealed record TreatyBody(TreatyAction Action,TreatyTerms Terms);
public sealed record TreatyFormation(EntityId Acceptance,TreatyTerms Terms,long At,long Revision);
public sealed record TreatyRecognition(EntityId Acceptance,EntityId Actor,long At,long Revision);
public sealed record TreatyState(int Schema,string Contract,TreatyPaper[] Papers,FactionNoticeOpening[] Openings,TreatyFormation[] Formations,TreatyRecognition[] Recognitions){[System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public TreatySupplyState? Supply{get;init;}[System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public TreatyDefenseState? Defense{get;init;}public TreatyOperationArrival[] OperationArrivals{get;init;}=[];public TreatyOperationPaper[] Operations{get;init;}=[];public FactionNoticeOpening[] OperationOpenings{get;init;}=[];public FactionNoticeOpening[] FailureOpenings{get;init;}=[];};
public sealed record TreatyCard(EntityId Paper,EntityId Other,string Stage,int Version,long Until,bool CanAccept,bool CanDecline);
// Bilateral papers are versioned consent, not an instantaneous global peace switch.
public static partial class TreatyNegotiations
{
    public const string Policy="head-versioned-multiple-clauses-physical-treaty-v1",Kind="treaty-negotiation-paper";
    public const long Day=86400;public const int MaximumNpcDays=30;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.TrucePolicy==TruceNegotiations.Policy;
    internal static int ReplyDuration(ContractWorld w,TreatyPaper paper,long at,long rev)
    {
        if(paper.Terms.DeclaredScale is not {} declared || !paper.Terms.Clauses.HasFlag(TreatyClause.Defense))
            return MaximumNpcDays;
        var own=HouseScaleNotices.Known(w,paper.Recipient,paper.Input.Target,at,rev);
        var gap=(int)own-(int)declared;
        if(gap<2)return MaximumNpcDays;
        var trust=CharacterRelations.Enabled(w)?CharacterRelations.DiagnosticScores(w,paper.Recipient,paper.Author,at,rev).Trust:0;
        return trust>=50?MaximumNpcDays:Math.Max(7,MaximumNpcDays-7*gap);
    }
    static TreatyAction SubmissionReply(ContractWorld w,TreatyPaper paper,long at,long revision)
    {
        var conditions=paper.Terms.Submission!;
        int trust=CharacterRelations.Enabled(w)
            ? CharacterRelations.DiagnosticScores(w,paper.Recipient,paper.Author,at,revision).Trust : 0;
        int score=(conditions.GuaranteeLands?3:0)+Math.Clamp(trust/20,-3,3);
        if(paper.Terms.DeclaredScale is {} declared)
            score+=Math.Clamp((int)declared-(int)HouseScaleNotices.Known(w,paper.Recipient,paper.Input.Target,at,revision),-2,2);
        // 返答当時の宛先で当主自身が見られた部隊だけを数える。
        if(w.Branches!=null && (w.NpcArmies!=null || w.NewMilitary!=null) &&
            BranchRoutes.GraphNodes(w).Any(n=>RegionalCampaign.Site(n.Id)==paper.Address) &&
            TransportWork.At(w,paper.Recipient,paper.Address,at,revision)){
            int approaching=CastleHandover.Forces(w,paper.Input.Owner,paper.Address,at,revision);
            int defenders=CastleHandover.Forces(w,paper.Input.Target,paper.Address,at,revision);
            if(approaching>defenders)score+=Math.Min(4,1+(approaching-defenders)/50);
        }
        if(conditions.Precedent!=null)score++;
        if(KokujinCouncil.KnowsSeparateDeparture(w,paper.Recipient,paper.Input.Owner,at,revision))score-=3;
        // 自家が発した敵対宣言を根拠に、相手の防衛上の大義を認める。
        if(FactionStances.OwnAt(w,paper.Recipient,at,revision).Any(c=>c.FromOwner==paper.Input.Target &&
            c.ToOwner==paper.Input.Owner && c.EffectiveFrom<=at && at<c.Until))score++;
        if(PeaceMediation.KnowsRefusal(w,paper.Recipient,paper.Input.Owner,paper.Input.Target,at,revision))score--;
        if(conditions.RestrictDiplomacy)score-=2;
        if(conditions.Hostage!=null)score-=3;
        score-=conditions.MusterMen>100?4:conditions.MusterMen>0?1:0;
        score-=conditions.TributeRice>500?4:conditions.TributeRice>0?1:0;
        return score>0?TreatyAction.Accept:TreatyAction.Decline;
    }
    static TreatyAction BallotReply(ContractWorld w,TreatyPaper request,long at,long rev)
    {
        var original=State(w).Papers.Single(p=>p.Input.Id==request.Terms.Ballot!.Demand);
        return SubmissionReply(w,original with{Recipient=request.Recipient,Address=request.Address,
            Input=original.Input with{Target=request.Input.Target}},at,rev);
    }
    static TreatyInput? NpcSubmission(ContractWorld w,EntityId head,EntityId owner,EntityId target,long at,long rev)
    {
        var claim=FactionStances.OwnAt(w,head,at,rev).LastOrDefault(c=>c.FromOwner==owner && c.ToOwner==target &&
            c.EffectiveFrom<=at && at<c.Until);
        if(claim==null || RepresentativeKnowledge.Known(w,head,target,at,rev)==null)return null;
        var id=HouseholdCourt.Id("submission-demand",claim.Declaration.Value);
        return new TreatyInput(id,TreatyAction.Propose,owner,target,Days:30,NoticeDays:1)
            {Submission=new(true,100,0,false,null),AttachHouseScale=true};
    }
    static TreatyState State(ContractWorld w)=>w.Treaties??new(1,Policy,[],[],[],[]);
    static bool Before(long t,long r,long at,long rev)=>t<at||t==at&&r<=rev;
    static string Hash(string s)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(s));
    static string MessageId(EntityId id)=>"message:treaty/"+Hash(id.Value);
    static EntityId AutoId(string action,TreatyPaper p)=>new("request:treaty-"+action+"/"+Hash(p.Input.Id.Value));
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>State(w).Papers.Any(p=>p.Input.Id==id)||State(w).Operations.Any(p=>p.Input.Id==id);
    static bool Opened(ContractWorld w,TreatyPaper p,EntityId actor,long at,long rev)=>State(w).Openings.Any(o=>o.Message==p.Message&&o.Actor==actor&&Before(o.At,o.Revision,at,rev));
    static TreatyPaper[] PapersAt(ContractWorld w,long at,long rev)=>State(w).Papers.Where(p=>Before(p.At,p.Revision,at,rev)).ToArray();
    static TreatyPaper Prepare(ContractWorld w,TreatyInput i,EntityId actor,long at,long rev)
    {
        Need(Enabled(w)&&EntityId.IsValid(i.Id.Value)&&Enum.IsDefined(i.Action)&&i.Owner!=i.Target,"交渉の当事者・入力が不正です");var auth=FactionStances.ResolveAuthority(w,actor,i.Owner,at,rev);Need(auth!=null,"当事者の締結権者ではありません");var target=RepresentativeKnowledge.Known(w,actor,i.Target,at,Math.Min(rev,w.CommitSequence));Need(target!=null,"相手の当事者が不明です");TreatyTerms terms;
        if(i.DepartureCopy is {} copy){
            var notice=State(w).Operations.SingleOrDefault(p=>p.Input.Id==copy.Original && p.Kind==TreatyOperation.Terminate);
            Need(notice!=null && notice.Intent.Evidence!=null && notice.Recipient==actor && notice.Input.Target==i.Owner &&
                notice.Input.Owner==copy.DepartingHouse && notice.Terms.Charter==copy.Charter && OperationOpened(w,notice,actor,at,rev-1) &&
                i.Action==TreatyAction.Propose && i.Ballot==null && i.Submission==null && i.Charter==null &&
                i.Clauses==TreatyClause.None && i.Target!=copy.DepartingHouse &&
                KokujinCouncil.KnownMembers(w,actor,copy.Charter,at,rev-1).Contains(i.Target),"本人開封済みの離脱原本を加盟家にだけ転送できます");
            Need(!PapersAt(w,at,rev-1).Any(p=>p.Input.DepartureCopy==copy && p.Input.Target==i.Target),"同じ家へ離脱を通知済みです");
        }
        if(i.Ballot is {} ballot){
            var incoming=PapersAt(w,at,rev-1).SingleOrDefault(p=>p.Input.Id==ballot.Demand);
            Need(incoming?.Terms.Submission!=null && incoming.Terms.Ballot==null && incoming.Recipient==actor &&
                incoming.Terms.Submission==ballot.Conditions && incoming.Terms.A==ballot.DemandingHouse && incoming.Input.Target==i.Owner && Opened(w,incoming,actor,at,rev-1) && at<incoming.Terms.ReplyBy,
                "本人が開封した有効な勧告だけを合議に付せます");
            Need(i.Action==TreatyAction.Propose && i.Charter==null && i.Submission==null && i.Clauses==TreatyClause.None &&
                i.Target!=incoming.Input.Owner && KokujinCouncil.KnownMembers(w,actor,ballot.Charter,at,rev-1).Contains(i.Target),
                "契状で加盟を確認した別の家にだけ照会できます");
            var consultations=PapersAt(w,at,rev-1).Where(p=>p.Input.Ballot?.Demand==ballot.Demand).ToArray();
            Need(consultations.All(p=>p.Input.Ballot==ballot && p.Input.Target!=i.Target) &&
                !PapersAt(w,at,rev-1).Any(p=>p.Input.Source==ballot.Demand),"同じ家への重複照会・返答後の合議はできません");
        }
        if(i.Charter is {} charter){
            Need(i.Action==TreatyAction.Propose && i.Submission==null && i.Clauses==TreatyClause.None &&
                Enum.IsDefined(charter.Rule) && i.Days is >=1 and <=MaximumNpcDays,"契状は独立した原本として30日以内の期間と議決規則を記してください");
            if(charter.Origin!=i.Id){
                var founding=PapersAt(w,at,rev-1).SingleOrDefault(p=>p.Input.Id==charter.Origin);
                Need(founding!=null && founding.Input.Owner==i.Owner && founding.Terms.Charter==charter &&
                    at<founding.Terms.Until && checked(at+i.Days*Day)<=founding.Terms.Until,"呼びかけた家・議決規則・契状の期間が最初の原本と一致しません");
            }
            Need(!PapersAt(w,at,rev-1).Any(p=>p.Input.Action==TreatyAction.Propose && p.Input.Charter==charter &&
                p.Input.Target==i.Target && at<p.Terms.Until),"同じ家宛ての加盟書状がすでにあります");
        }
        if(i.Submission is {} demand){
            Need(i.Action==TreatyAction.Propose,"従属条件は勧告の原本に記します");
            Need(demand.MusterMen>=0 && demand.TributeRice>=0 && (demand.MusterMen>0 || demand.TributeRice>0 || demand.RestrictDiplomacy || demand.Hostage!=null || demand.GuaranteeLands),"勧告に有効な条件が必要です");
            if(demand.Precedent is {} example)
                Need(Known(w,actor,at,Math.Min(w.CommitSequence,rev-1)).Any(f=>f.Acceptance==example && f.Terms.A==i.Owner &&
                    f.Terms.B!=i.Target && f.Terms.Submission!=null && at<f.Terms.Until &&
                    !KnowsEnding(w,actor,f.Acceptance,at,rev-1)),"先例には当主が成立を確認した別家への勧告を指定してください");
            if(demand.Hostage is {} hostage)
                Need(w.NewGame!.Start.People.Any(person=>person.Id==hostage.Value && person.Faction==i.Target.Value),"人質の候補は公知の開始名簿にある相手方の人物を指定してください");
        }
        if(i.Action==TreatyAction.Propose){Need(actor==w.PlayerControl || i.Ballot!=null || i.DepartureCopy!=null || i==NpcSubmission(w,actor,i.Owner,i.Target,at,rev-1),"勧告を発する当主の既知の開戦根拠がありません");Need(i.Source==null&&i.Days is >=1 and <=90,"不戦期間は1〜90日で提示してください");Need((i.Ballot!=null||i.DepartureCopy!=null||i.Charter!=null||i.Submission!=null||i.Clauses!=TreatyClause.None)&&((int)i.Clauses&~15)==0&&i.NoticeDays is >=1 and <=7,"独立条項と1〜7日の解除通知期間を指定してください");terms=new(i.Id,1,null,i.Owner,i.Target,at,checked(at+i.Days*Day),checked(at+Math.Min(7,i.Days)*Day),i.Clauses,checked(i.NoticeDays*(int)Day)){DepartureCopy=i.DepartureCopy,Ballot=i.Ballot,Charter=i.Charter,Submission=i.Submission,DeclaredScale=i.AttachHouseScale==true?HouseScaleNotices.Known(w,actor,i.Owner,at,rev):null};}
        else{
            var source=PapersAt(w,at,rev).SingleOrDefault(p=>p.Input.Id==i.Source);Need(source!=null&&source.Terms.DepartureCopy==null&&source.Recipient==actor&&source.Input.Owner==i.Target&&source.Input.Target==i.Owner&&Opened(w,source,actor,at,rev),"本人に到達して開封した同一原本が必要です");
            Need(!PapersAt(w,at,rev).Any(p=>p.Input.Id!=i.Id&&p.Input.Source==source.Input.Id&&p.Author==actor),"この版には既に返答しています");
            if(source.Terms.Submission!=null && KokujinCouncil.Tally(w,actor,source.Input.Id,at,rev-1) is {} count)
                Need(count.Verdict==i.Action,"到着して開封した票が契状の議決条件を満たすまで連合を代表して返答できません");
            if(i.Action==TreatyAction.Confirm){Need(source.Input.Action==TreatyAction.Accept&&Forms(w,at,rev).Any(f=>f.Acceptance==source.Input.Id),"成立を確認した承諾の原本が必要です");Need(i.Days==0,"確認書で条件は変えません");terms=source.Terms;}
            else{Need(source.Input.Action is TreatyAction.Propose or TreatyAction.Counter,"提案または対案へ返答してください");Need(at<=source.Terms.ReplyBy&&at<source.Terms.Until,"返答期限を過ぎています");
                int limit=ReplyDuration(w,source,at,rev);
                if(i.Action==TreatyAction.Counter){Need(source.Terms.Ballot==null&&source.Terms.Submission==null&&actor!=w.PlayerControl&&source.Input.Action==TreatyAction.Propose&&i.Days==limit&&source.Terms.Until-source.Terms.From>limit*Day,"相手の調整方針による対案ではありません");terms=new(source.Terms.Origin,2,source.Input.Id,source.Terms.A,source.Terms.B,at,checked(at+limit*Day),source.Terms.ReplyBy,source.Terms.Clauses,source.Terms.NoticeSeconds){Submission=source.Terms.Submission,DeclaredScale=source.Terms.DeclaredScale};}
                else{Need(i.Clauses==TreatyClause.None&&i.NoticeDays==0,"返答で条項と解除期間は変えません");Need(i.Days==0&&i.Action is TreatyAction.Accept or TreatyAction.Decline,"返答で本文を勝手に変更できません");if(actor!=w.PlayerControl)Need(source.Terms.Ballot!=null ? i.Action==BallotReply(w,source,at,rev) : source.Terms.Submission!=null ? i.Action==(KokujinCouncil.Tally(w,actor,source.Input.Id,at,rev-1)?.Verdict??SubmissionReply(w,source,at,rev)) : i.Action==TreatyAction.Accept&&source.Terms.Until-source.Terms.From<=limit*Day,"NPCの条件上限と返答が一致しません");terms=source.Terms;}
            }
        }
        if(i.Action==TreatyAction.Confirm){var source=State(w).Papers.Single(p=>p.Input.Id==i.Source);Need(actor==w.PlayerControl||i.Id==AutoId("confirm",source),"成立確認のIDが原本と違います");}else if(actor!=w.PlayerControl && i.Action!=TreatyAction.Propose){var source=State(w).Papers.Single(p=>p.Input.Id==i.Source);Need(i.Id==AutoId(i.Action==TreatyAction.Counter?"counter":i.Action==TreatyAction.Decline?"decline":"accept",source),"NPCの返答原本IDが不正です");}
        return new(i,terms,actor,target.Person,target.Home,at,rev,auth,MessageId(i.Id));
    }
    static MessageEnvelope Envelope(TreatyPaper p)=>new(p.Message,p.Input.Id.Value,Kind,p.Author.Value,p.Recipient.Value,System.Text.Encoding.UTF8.GetString(ContractJson.Write(new TreatyBody(p.Input.Action,p.Terms))),p.Address.Value,p.At);
    internal static void Dispatch(ContractWorld w,TreatyInput input,EntityId actor)
    {Need(!OwnIdUsed(w,input.Id)&&!FactionStances.OtherIdUsed(w,input.Id,includeTreaties:false)&&!FactionStances.OwnIdUsed(w,input.Id),"別の操作に使ったIDです");var p=Prepare(w,input,actor,w.ElapsedSeconds,w.CommitSequence);w.Treaties=State(w) with{Papers=[..State(w).Papers,p]};MessageFlow.Send(w.Messaging,Envelope(p),p.At,1);w.SealedMessages=[..w.SealedMessages,new(p.Message)];}
    public static string? Address(ContractWorld w,MessageEnvelope m)=>m.Kind==Kind&&State(w).Papers.SingleOrDefault(p=>p.Message==m.Id) is {} p?NewGameWorld.Node(p.Address.Value):null;
    internal static void RecordOpening(ContractWorld w,EntityId actor,string message)
    {var s=State(w);if(!Enabled(w)||s.Openings.Any(o=>o.Message==message)||!s.Papers.Any(p=>p.Message==message&&p.Recipient==actor))return;Need(w.Messaging.Attempts.Any(a=>a.MessageId==message&&a.ReceiverId==actor.Value&&a.Status==AttemptStatus.Delivered)&&MessageFlow.Knows(w.Messaging,message,actor.Value),"到達・本人開封が必要です");w.Treaties=s with{Openings=[..s.Openings,new(message,actor,w.ElapsedSeconds,w.CommitSequence)]};}
    static TreatyFormation[] Forms(ContractWorld w,long at,long rev,bool discover=false)
    {
        var papers=PapersAt(w,at,rev);var result=new List<TreatyFormation>();foreach(var p in papers.Where(p=>p.Input.Action==TreatyAction.Accept)){
            var source=papers.SingleOrDefault(s=>s.Input.Id==p.Input.Source);if(source==null||source.Terms!=p.Terms||source.Terms.Ballot!=null)continue;
            var delivery=w.Messaging.Attempts.Where(a=>a.MessageId==p.Message&&a.ReceiverId==p.Recipient.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=at).OrderBy(a=>a.FinishedAt).FirstOrDefault();if(delivery?.FinishedAt is not {} arrived||arrived>p.Terms.ReplyBy||arrived>=p.Terms.Until)continue;
            var stored=State(w).Formations.SingleOrDefault(f=>f.Acceptance==p.Input.Id);if(stored==null&&!discover)continue;long revision=stored?.Revision??w.CommitSequence;if(FactionStances.ResolveAuthority(w,p.Recipient,p.Input.Target,arrived,revision)==null)continue;if(arrived==at&&revision>rev)continue;result.Add(new(p.Input.Id,p.Terms,arrived,revision));
        }return result.OrderBy(f=>f.At).ThenBy(f=>f.Acceptance.Value,StringComparer.Ordinal).ToArray();
    }
    public static TreatyFormation[] Known(ContractWorld w,EntityId actor,long? at=null,long? revision=null)
    {long time=at??w.ElapsedSeconds,rev=revision??w.CommitSequence;Need(time>=0&&time<=w.ElapsedSeconds&&rev>=0&&rev<=w.CommitSequence,"未確定の交渉知識です");return Forms(w,time,rev).Where(f=>{var p=State(w).Papers.Single(p=>p.Input.Id==f.Acceptance);return Opened(w,p,actor,time,rev)||State(w).Papers.Any(c=>c.Input.Action==TreatyAction.Confirm&&c.Input.Source==p.Input.Id&&Opened(w,c,actor,time,rev));}).ToArray();}
    internal static void Update(ContractWorld w)
    {
        if(!Enabled(w)||w.Treaties==null)return;foreach(var p in State(w).Papers){if(p.Recipient==w.PlayerControl||State(w).Openings.Any(o=>o.Message==p.Message)||!w.Messaging.Attempts.Any(a=>a.MessageId==p.Message&&a.ReceiverId==p.Recipient.Value&&a.Status==AttemptStatus.Delivered))continue;if(!MessageFlow.Knows(w.Messaging,p.Message,p.Recipient.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(p.Message,p.Recipient.Value,w.ElapsedSeconds,p.Message)];RecordOpening(w,p.Recipient,p.Message);}
        w.Treaties=State(w) with{Formations=Forms(w,w.ElapsedSeconds,w.CommitSequence,true)};UpdateOperations(w);
    }
    static (EntityId Actor,TreatyInput? Reply,TreatyFormation? Fulfill)? Next(ContractWorld w,bool allowReplies)
    {
        foreach(var f in State(w).Formations){var acceptance=State(w).Papers.Single(p=>p.Input.Id==f.Acceptance);foreach(var actor in new[]{acceptance.Author,acceptance.Recipient}.Where(a=>a!=w.PlayerControl))if(FactionStances.ResolveAuthority(w,actor,actor==acceptance.Author?acceptance.Input.Owner:acceptance.Input.Target,w.ElapsedSeconds,w.CommitSequence)!=null&&Known(w,actor).Any(k=>k.Acceptance==f.Acceptance)&&w.ElapsedSeconds<Math.Min(f.Terms.Until,long.MaxValue)&&!State(w).Recognitions.Any(q=>q.Acceptance==f.Acceptance&&q.Actor==actor))return(actor,null,f);}
        if(allowReplies)foreach(var p in State(w).Papers.Where(p=>p.Terms.DepartureCopy==null&&p.Recipient!=w.PlayerControl&&p.Input.Action is TreatyAction.Propose or TreatyAction.Counter).OrderBy(p=>p.At).ThenBy(p=>p.Input.Id.Value,StringComparer.Ordinal))if(FactionStances.ResolveAuthority(w,p.Recipient,p.Input.Target,w.ElapsedSeconds,w.CommitSequence)!=null&&Opened(w,p,p.Recipient,w.ElapsedSeconds,w.CommitSequence)&&w.ElapsedSeconds<=p.Terms.ReplyBy&&w.ElapsedSeconds<p.Terms.Until&&!State(w).Papers.Any(q=>q.Input.Source==p.Input.Id&&q.Author==p.Recipient)){
            if(p.Terms.Ballot!=null){
                var vote=BallotReply(w,p,w.ElapsedSeconds,w.CommitSequence);
                return(p.Recipient,new(AutoId(vote==TreatyAction.Accept?"accept":"decline",p),vote,p.Input.Target,p.Input.Owner,p.Input.Id),null);
            }
            if(p.Terms.Submission!=null){
                var answer=SubmissionReply(w,p,w.ElapsedSeconds,w.CommitSequence);
                var past=State(w).Papers.Where(q=>q.Input.Ballot?.Demand==p.Input.Id && q.Author==p.Recipient).ToArray();
                var charter=past.FirstOrDefault()?.Input.Ballot?.Charter ??
                    Known(w,p.Recipient).Where(f=>f.Terms.A==p.Input.Target && f.Terms.Charter!=null &&
                        w.ElapsedSeconds<f.Terms.Until && !KnowsEnding(w,p.Recipient,f.Acceptance,w.ElapsedSeconds,w.CommitSequence))
                    .Select(f=>f.Terms.Charter).FirstOrDefault();
                if(charter!=null){
                    var members=KokujinCouncil.KnownMembers(w,p.Recipient,charter,w.ElapsedSeconds,w.CommitSequence);
                    var missing=members.Where(h=>h!=p.Input.Target && h!=p.Input.Owner &&
                        !past.Any(q=>q.Input.Target==h)).ToArray();
                    if(missing.Length>0){
                        var ballot=past.FirstOrDefault()?.Input.Ballot??new(charter,p.Input.Id,p.Input.Owner,p.Terms.Submission,answer==TreatyAction.Accept);
                        var id=HouseholdCourt.Id("council-vote",p.Input.Id.Value+"/"+missing[0].Value);
                        return(p.Recipient,new TreatyInput(id,TreatyAction.Propose,p.Input.Target,missing[0],Days:7,NoticeDays:1){Ballot=ballot},null);
                    }
                    if(past.Length>0){
                        var verdict=KokujinCouncil.Tally(w,p.Recipient,p.Input.Id,w.ElapsedSeconds,w.CommitSequence)?.Verdict;
                        if(verdict==null)continue;
                        answer=verdict.Value;
                    }
                }
                return(p.Recipient,new(AutoId(answer==TreatyAction.Accept?"accept":"decline",p),answer,p.Input.Target,p.Input.Owner,p.Input.Id),null);
            }
            int limit=ReplyDuration(w,p,w.ElapsedSeconds,w.CommitSequence);bool counter=p.Terms.Until-p.Terms.From>limit*Day;return(p.Recipient,new(AutoId(counter?"counter":"accept",p),counter?TreatyAction.Counter:TreatyAction.Accept,p.Input.Target,p.Input.Owner,p.Input.Id,counter?limit:0),null);
        }
        if(allowReplies)foreach(var notice in State(w).Operations.Where(p=>p.Kind==TreatyOperation.Terminate && p.Intent.Evidence!=null &&
            p.Recipient!=w.PlayerControl && p.Terms.Charter!=null && OperationOpened(w,p,p.Recipient,w.ElapsedSeconds,w.CommitSequence))){
            if(FactionStances.ResolveAuthority(w,notice.Recipient,notice.Input.Target,w.ElapsedSeconds,w.CommitSequence)==null)continue;
            foreach(var copy in DepartureCopies(w,notice,notice.Recipient,w.ElapsedSeconds,w.CommitSequence))
                return(notice.Recipient,copy,null);
        }
        if(allowReplies && Enabled(w) && w.FactionStances is {Decisions.Length:>0}){
            foreach(var declaration in w.FactionStances.Decisions.Where(d=>d.Input.Action==FactionStanceAction.DeclareHostile &&
                d.Authority.Issuer!=w.PlayerControl && d.Input.EffectiveAt<=w.ElapsedSeconds && w.ElapsedSeconds<d.Input.Until)){
                var head=declaration.Authority.Issuer;
                if(FactionStances.ResolveAuthority(w,head,declaration.Input.Owner,w.ElapsedSeconds,w.CommitSequence)==null)continue;
                var offer=NpcSubmission(w,head,declaration.Input.Owner,declaration.Input.Target,w.ElapsedSeconds,w.CommitSequence);
                if(offer!=null && !OwnIdUsed(w,offer.Id))return(head,offer,null);
            }
        }
        return null;
    }
    public static IEnumerable<TreatyInput> DepartureCopies(ContractWorld w,TreatyOperationPaper notice,EntityId actor,long at,long revision)
    {
        if(notice.Terms.Charter is not {} charter || notice.Kind!=TreatyOperation.Terminate || notice.Intent.Evidence==null ||
            notice.Recipient!=actor || !OperationOpened(w,notice,actor,at,revision)) yield break;
        var copy=new CouncilDepartureCopy(notice.Root,charter,notice.Input.Owner);
        foreach(var house in KokujinCouncil.KnownMembers(w,actor,charter,at,revision).Where(h=>h!=notice.Input.Owner && h!=notice.Input.Target)){
            var id=HouseholdCourt.Id("council-departure-copy",notice.Root.Value+"/"+house.Value);
            if(!OwnIdUsed(w,id))yield return new TreatyInput(id,TreatyAction.Propose,notice.Input.Target,house,Days:7,NoticeDays:1){DepartureCopy=copy};
        }
    }
    internal static void TakeTurns(WorldSession session)
    {for(int n=0;n<32;n++){var w=session.TurnSnapshot();var next=Next(w,!w.Paused);if(next==null){TakeOperationTurns(session);return;}session.ActOnTreaty(next.Value.Actor,next.Value.Reply,next.Value.Fulfill);}}
    internal static void ApplyAutomatic(ContractWorld w,EntityId actor,TreatyInput? reply,TreatyFormation? f)
    {
        var prior=w.ForkTransportLocation();prior.CommitSequence=w.CommitSequence-1;var expected=Next(prior,!w.Paused);Need(expected!=null&&expected.Value.Actor==actor&&expected.Value.Reply==reply&&expected.Value.Fulfill==f,"受領・開封した条件からの処理ではありません");
        if(reply!=null){Dispatch(w,reply,actor);return;}Need(f!=null,"成立原本がありません");var p=State(w).Papers.Single(p=>p.Input.Id==f.Acceptance);EntityId owner=actor==p.Author?p.Input.Owner:p.Input.Target,target=actor==p.Author?p.Input.Target:p.Input.Owner;
        if(actor==p.Recipient)Dispatch(w,new(AutoId("confirm",p),TreatyAction.Confirm,owner,target,p.Input.Id),actor);
        w.Treaties=State(w) with{Recognitions=[..State(w).Recognitions,new(f.Acceptance,actor,w.ElapsedSeconds,w.CommitSequence)]};
    }
    public static bool WillNotInitiate(ContractWorld w,EntityId owner,EntityId target,long at,long revision)
    {if(!Enabled(w))return false;var head=HouseSuccession.Factions(w,at,revision).SingleOrDefault(f=>f.Id==owner.Value)?.Head;if(head==null)return false;return Known(w,new(head),at,revision).Any(f=>ClauseUsable(w,new(head),f.Acceptance,TreatyClause.NonAggression,at,revision)&&Math.Max(f.At,f.Terms.From)<=at&&at<f.Terms.Until&&(f.Terms.A==owner&&f.Terms.B==target||f.Terms.B==owner&&f.Terms.A==target));}
    internal static long? NextBoundary(ContractWorld w,long target)
    {if(!Enabled(w))return null;var times=State(w).Papers.SelectMany(p=>new[]{p.Terms.ReplyBy+1,p.Terms.Until}).Concat(OperationChanges(w)).Concat(TreatySupply.Changes(w)).Concat(TreatyDefense.Changes(w)).ToList();if(Next(w,true)!=null||NextOperation(w)!=null)times.Add(w.ElapsedSeconds+1);return times.Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();}
    internal static IEnumerable<(long At,long Revision)> ExternalInputs(ContractWorld w)=>State(w).Papers.Select(p=>(p.At,p.Revision)).Concat(State(w).Openings.Where(o=>o.Actor==w.PlayerControl).Select(o=>(o.At,o.Revision))).Concat(State(w).Recognitions.Select(f=>(f.At,f.Revision))).Concat(OperationInputs(w)).Concat(TreatySupply.Inputs(w)).Concat(TreatyDefense.Inputs(w));
    internal static IEnumerable<long> Changes(ContractWorld w)=>State(w).Papers.SelectMany(p=>new[]{p.At,p.Terms.From,p.Terms.Until,p.Terms.ReplyBy}).Concat(State(w).Openings.Select(o=>o.At)).Concat(State(w).Formations.Select(f=>f.At)).Concat(OperationChanges(w)).Concat(TreatySupply.Changes(w)).Concat(TreatyDefense.Changes(w));
    internal static int BoundaryCost(ContractWorld w)=>!Enabled(w)?0:2*State(w).Papers.Select(p=>p.Recipient).Distinct().Count()+Operations(w).Select(p=>p.Recipient).Distinct().Count()*2;
    internal static void Rewind(ContractWorld w,long at,long rev){TreatySupply.Rewind(w,at,rev);TreatyDefense.Rewind(w,at,rev);RewindOperations(w,at,rev);if(w.Treaties is {} s)w.Treaties=s with{Papers=s.Papers.Where(p=>Before(p.At,p.Revision,at,rev)).ToArray(),Openings=s.Openings.Where(o=>Before(o.At,o.Revision,at,rev)).ToArray(),Formations=s.Formations.Where(f=>Before(f.At,f.Revision,at,rev)).ToArray(),Recognitions=s.Recognitions.Where(f=>Before(f.At,f.Revision,at,rev)).ToArray()};}
    public static TreatyCard[] View(ContractWorld w)
    {
        if(!Enabled(w))return [];var actor=w.PlayerControl;var papers=State(w).Papers.Where(p=>p.Author==actor||Opened(w,p,actor,w.ElapsedSeconds,w.CommitSequence)).ToArray();return papers.Where(p=>p.Input.Action is TreatyAction.Propose or TreatyAction.Counter).GroupBy(p=>p.Terms.Origin).Select(g=>g.OrderByDescending(p=>p.Terms.Version).First()).Select(p=>{
            bool incoming=p.Recipient==actor,answered=State(w).Papers.Any(r=>r.Input.Source==p.Input.Id&&(r.Author==actor||Opened(w,r,actor,w.ElapsedSeconds,w.CommitSequence)));bool respond=p.Terms.DepartureCopy==null&&incoming&&!answered&&w.ElapsedSeconds<=p.Terms.ReplyBy&&w.ElapsedSeconds<p.Terms.Until;bool formed=Known(w,actor).Any(f=>f.Terms==p.Terms);bool ended=State(w).Formations.Where(f=>f.Terms==p.Terms).Any(f=>KnowsEnding(w,actor,f.Acceptance,w.ElapsedSeconds,w.CommitSequence));string stage=ended?"解除通知の効力を本人が確認":formed?w.ElapsedSeconds<p.Terms.Until?"成立を確認・条約期間中":"成立を確認・期間終了":w.ElapsedSeconds>p.Terms.ReplyBy?"返答期限経過":answered?"返答済み・相手の到達は別途確認":incoming?"相手の条件を開封・返答可能":"提示発送・返答待ち";return new TreatyCard(p.Input.Id,incoming?p.Input.Owner:p.Input.Target,stage,p.Terms.Version,p.Terms.Until,respond,respond);}).ToArray();
    }
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.Treaties==null,"旧世界へ交渉を混在できません");return;}var s=State(w);if(w.Treaties==null)return;Need(s.Schema==1&&s.Contract==Policy&&s.Papers!=null&&s.Openings!=null&&s.Formations!=null&&s.Recognitions!=null,"交渉原本がありません");Need(s.Papers.Select(p=>p.Input.Id).Distinct().Count()==s.Papers.Length&&s.Openings.Select(o=>o.Message).Distinct().Count()==s.Openings.Length,"交渉・開封が重複しています");
        Need(s.Papers.Zip(s.Papers.Skip(1)).All(pair=>pair.First.At<=pair.Second.At&&pair.First.Revision<pair.Second.Revision),"交渉原本の順序が不正です");foreach(var p in s.Papers){Need(p.At>=0&&p.At<=w.ElapsedSeconds&&p.Revision>0&&p.Revision<=w.CommitSequence&&p==Prepare(w,p.Input,p.Author,p.At,p.Revision),"交渉の版・当事者・権限・原本が違います");Need(!FactionStances.OtherIdUsed(w,p.Input.Id,includeTreaties:false)&&!FactionStances.OwnIdUsed(w,p.Input.Id),"交渉IDが他の操作と重複しています");Need(ContractJson.CanonicalHash(w.Messaging.Messages.Single(m=>m.Id==p.Message))==ContractJson.CanonicalHash(Envelope(p))&&w.SealedMessages.Contains(new(p.Message)),"交渉原文・封印が違います");foreach(var c in w.Couriers!.Missions.Where(c=>w.Messaging.Attempts.Any(a=>a.Id==c.Attempt&&a.MessageId==p.Message)))Need(c.Destination==NewGameWorld.Node(p.Address.Value),"相手の現在地へ宛先をすり替えています");}
        foreach(var o in s.Openings){var p=s.Papers.Single(p=>p.Message==o.Message);Need(p.Recipient==o.Actor&&o.At>=p.At&&o.At<=w.ElapsedSeconds&&o.Revision>p.Revision&&o.Revision<=w.CommitSequence&&MessageFlow.LearnedAt(w.Messaging,o.Message,o.Actor.Value)==o.At&&w.Messaging.Attempts.Any(a=>a.MessageId==o.Message&&a.ReceiverId==o.Actor.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=o.At),"交渉の到達・本人開封が一致しません");}
        foreach(var p in s.Papers)Need(MessageFlow.Knows(w.Messaging,p.Message,p.Recipient.Value)==s.Openings.Any(o=>o.Message==p.Message),"開封前に条件を知っています");Need(w.Messaging.Messages.Count(m=>m.Kind==Kind)==s.Papers.Length&&ContractJson.CanonicalHash(s.Formations)==ContractJson.CanonicalHash(Forms(w,w.ElapsedSeconds,w.CommitSequence,true)),"成立の到達・版が一致しません");
        foreach(var f in s.Formations){var p=s.Papers.Single(p=>p.Input.Id==f.Acceptance);Need(f.Revision>p.Revision&&f.Revision<=w.CommitSequence&&f.At>=p.At,"成立の順序が不正です");}Need(s.Recognitions.Select(f=>(f.Acceptance,f.Actor)).Distinct().Count()==s.Recognitions.Length,"条約の成立認識が重複しています");foreach(var f in s.Recognitions){var form=Known(w,f.Actor,f.At,f.Revision).SingleOrDefault(k=>k.Acceptance==f.Acceptance);Need(form!=null&&f.At>=form.At&&f.At<form.Terms.Until&&f.Revision>0&&f.Revision<=w.CommitSequence,"成立を本人が認識していません");var acceptance=s.Papers.Single(p=>p.Input.Id==f.Acceptance);EntityId owner=f.Actor==acceptance.Author?acceptance.Input.Owner:acceptance.Input.Target,target=f.Actor==acceptance.Author?acceptance.Input.Target:acceptance.Input.Owner;if(f.Actor==acceptance.Recipient)Need(s.Papers.Any(c=>c.Input.Action==TreatyAction.Confirm&&c.Input.Id==AutoId("confirm",acceptance)&&c.Input.Source==f.Acceptance&&c.Author==f.Actor&&c.At==f.At&&c.Revision==f.Revision),"相手への成立確認の原本がありません");}
        ValidateOperations(w);
    }
}
