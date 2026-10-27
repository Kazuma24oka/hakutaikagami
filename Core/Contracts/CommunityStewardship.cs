using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public enum StewardAction { Assign,Operate,Report,ResendReport,Revoke }
public sealed record StewardInput(EntityId Id,StewardAction Action,EntityId Site,EntityId? Other=null,EntityId? Grant=null,EconomyAction? Operation=null,long Amount=0,int Days=30,EntityId? ReturnAddress=null,string? Report=null);
public sealed record StewardEntry(StewardInput Input,long At,long Revision);
public sealed record StewardGrant(EntityId Id,EntityId Principal,EntityId Agent,EntityId Owner,EntityId Site,EntityId Address,long From,long Until,long Revision);
public sealed record StewardObservation(EntityId Site,long ObservedAt,CommunityAccounts Accounts,string Statement) { [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public RegionalObservation? Regional {get;init;} }
public sealed record StewardPaper(string Id,EntityId Grant,string Kind,EntityId Sender,EntityId Recipient,EntityId Address,long At,long Revision,StewardObservation? Observation,string? Original);
public sealed record StewardReceipt(string Message,long At,long Revision);
public sealed record StewardState(int Schema,string Policy,StewardEntry[] Entries,StewardGrant[] Grants,StewardPaper[] Papers,StewardReceipt[] Receipts);
public sealed record StewardCard(EntityId Id,EntityId Principal,EntityId Agent,EntityId Site,long Until,string Status,bool CanOperate,bool CanRevoke);
public sealed record StewardView(EntityId? Site,EntityId[] People,StewardCard[] Grants,StewardPaper[] Reports);
public static class CommunityStewardship
{
    public const string Policy="local-bounded-stewardship-physical-reports-revocation-v7",ReportKind="steward-report",RevokeKind="steward-revoke",AckKind="steward-ack";
    const long Day=86400;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string text){if(!ok)throw new InvalidDataException(text);}
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or MaterialMarkets.Policy or RegionalDomains.Policy or HouseLogistics.Policy or HouseFreightReceiving.Policy;
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Stewardship?.Entries.Any(e=>e.Input.Id==id)==true;
    static bool Before(long at,long rev,long t,long r)=>at<t||at==t&&rev<r;
    internal static bool Local(ContractWorld w,EntityId actor,EntityId site,long at,long rev)=>w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult)&&CommunityEconomy.Local(w,actor,site,at,rev);
    static string Hash(string s)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(s));
    internal static EntityId Effect(EntityId id)=>new("request:steward-effect/"+Hash(id.Value));
    static bool Bound(ContractWorld w,StewardGrant g,long at,long rev)=>w.ServiceBonds.Any(b=>b.Subject==g.Agent&&b.Target==g.Principal&&b.From<=at&&(at<b.Until||at==b.Until&&rev<b.Revision));
    static bool Stopped(ContractWorld w,StewardGrant g,long at,long rev)=>w.Stewardship!.Receipts.Any(r=>Before(r.At,r.Revision,at,rev)&&w.Stewardship.Papers.Any(p=>p.Id==r.Message&&p.Grant==g.Id&&p.Kind==RevokeKind));
    internal static bool Active(ContractWorld w,StewardGrant g,long at,long rev)=>g.From<=at&&at<g.Until&&Before(g.From,g.Revision,at,rev)&&Bound(w,g,at,rev)&&!Stopped(w,g,at,rev);
    internal static StewardGrant? GrantAt(ContractWorld w,EntityId site,long at,long rev)=>Enabled(w)?w.Stewardship!.Grants.LastOrDefault(g=>g.Site==site&&Active(w,g,at,rev)):null;
    internal static EntityId? Delegate(ContractWorld w,EntityId site,long at,long rev)=>GrantAt(w,site,at,rev)?.Agent;
    static bool SameMonth(ContractWorld w,long a,long b){var epoch=DateOnly.Parse(w.EpochDate);var x=epoch.AddDays((int)(a/Day));var y=epoch.AddDays((int)(b/Day));return x.Year==y.Year&&x.Month==y.Month;}
    internal static long Budget(ContractWorld w,EntityId actor,EntityId site,EconomyAction action,long at,long rev)
    {
        var g=GrantAt(w,site,at,rev);if(g==null||g.Agent!=actor)return long.MaxValue;
        long cap=action==EconomyAction.RelieveVillage?500:action==EconomyAction.CollectLevy?1000:200;
        long used=w.Communities!.Orders.Where(o=>o.Actor==actor&&o.Input.Site==site&&o.Input.Action==action&&Before(o.At,o.Revision,at,rev)&&SameMonth(w,o.At,at)).Sum(o=>o.Input.Amount);
        return Math.Max(0,cap-used);
    }
    internal static FactionStanceAuthority? Authority(ContractWorld w,EntityId actor,EntityId owner,EntityId site,long at,long rev)
    {
        if(!Enabled(w))return null;var g=GrantAt(w,site,at,rev);if(g==null||g.Agent!=actor||g.Owner!=owner)return null;
        var source=FactionStances.ResolveAuthority(w,g.Principal,owner,at,Math.Min(rev,w.CommitSequence));if(source==null)return null;
        var member=w.Memberships.LastOrDefault(m=>m.Subject==actor&&m.Target==owner&&m.From<=at&&at<m.Until);if(member==null)return null;
        return source with{Issuer=actor,Office=g.Id,Membership=member.Id};
    }
    internal static bool WaterAuthorized(ContractWorld w,EntityId effect,EntityId site,long at,long rev)=>Enabled(w)&&w.Stewardship!.Entries.Any(e=>e.At==at&&e.Revision==rev&&e.Input.Action==StewardAction.Operate&&e.Input.Operation==EconomyAction.ImproveWater&&e.Input.Site==site&&Effect(e.Input.Id)==effect);
    static StewardGrant MakeGrant(ContractWorld w,StewardEntry e)
    {
        var i=e.Input;Need(i.Other!=null&&i.Other!=w.PlayerControl&&i.Days>=1&&i.Days<=30&&i.ReturnAddress!=null&&w.Definitions.Sites.Any(s=>s.Id==i.ReturnAddress)&&CommunityEconomy.SitesFor(w).Contains(i.Site),"委任先・期限・返報先を指定してください");
        var f=w.NewGame!.Start.Factions.Single(f=>f.Id==w.NewGame.Start.Castles.Single(c=>c.Id==i.Site.Value).Faction);var head=new EntityId(f.Head);var agent=head==w.PlayerControl?i.Other!.Value:w.PlayerControl;
        Need(head==w.PlayerControl||head==i.Other,"当主と直属家臣が現地で合意してください");
        var g=new StewardGrant(i.Id,head,agent,new(f.Id),i.Site,i.ReturnAddress!.Value,e.At,checked(e.At+i.Days*Day),e.Revision);
        Need(Local(w,head,i.Site,e.At,e.Revision)&&Local(w,agent,i.Site,e.At,e.Revision)&&Bound(w,g,e.At,e.Revision)&&FactionStances.ResolveAuthority(w,head,g.Owner,e.At,e.Revision)!=null,"当事者の現地所在・直属関係・家の権限を確認できません");return g;
    }
    static StewardObservation Observe(ContractWorld w,StewardGrant g,long at,long rev,string statement)=>new(g.Site,at,CommunityEconomy.AccountAt(w,g.Site,at,rev),statement){Regional=RegionalDomains.Observation(w,g.Site,g.Agent,at,rev)};
    static StewardPaper Paper(ContractWorld w,StewardEntry e)
    {
        var i=e.Input;var g=w.Stewardship!.Grants.Single(g=>g.Id==i.Grant);string id="message:steward/"+Hash(i.Id.Value);
        if(i.Action==StewardAction.Revoke)return new(id,g.Id,RevokeKind,g.Principal,g.Agent,g.Site,e.At,e.Revision,null,null);
        if(i.Action==StewardAction.ResendReport){var old=w.Stewardship.Papers.Single(p=>p.Id==i.Report);return new(id,g.Id,ReportKind,g.Agent,g.Principal,g.Address,e.At,e.Revision,old.Observation,old.Id);}
        return new(id,g.Id,ReportKind,g.Agent,g.Principal,g.Address,e.At,e.Revision,Observe(w,g,e.At,e.Revision,"代理官が現地で確認した台帳"),null);
    }
    static MessageEnvelope Envelope(StewardPaper p)=>new(p.Id,p.Grant.Value,p.Kind,p.Sender.Value,p.Recipient.Value,System.Text.Encoding.UTF8.GetString(ContractJson.Write(p)),p.Address.Value,p.At);
    static void Send(ContractWorld w,StewardPaper p){w.Stewardship=w.Stewardship! with{Papers=[..w.Stewardship!.Papers,p]};MessageFlow.Send(w.Messaging,Envelope(p),p.At,1);w.SealedMessages=[..w.SealedMessages,new(p.Id)];}
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Stewardship=new(1,Policy,[],[],[],[]);}
    internal static void Apply(ContractWorld w,StewardInput i)
    {
        var e=new StewardEntry(i,w.ElapsedSeconds,w.CommitSequence);w.Stewardship=w.Stewardship! with{Entries=[..w.Stewardship!.Entries,e]};
        if(i.Action==StewardAction.Assign){var g=MakeGrant(w,e);Need(!w.Stewardship.Grants.Any(q=>q.Site==g.Site&&q.Until>e.At&&!Stopped(w,q,e.At,e.Revision)),"先の委任を終了してから合意してください");w.Stewardship=w.Stewardship with{Grants=[..w.Stewardship.Grants,g]};}
        else{var g=w.Stewardship.Grants.Single(q=>q.Id==i.Grant);Need(i.Site==g.Site,"委任先の地域が違います");
            if(i.Action==StewardAction.Operate){Need(g.Agent==w.PlayerControl&&Active(w,g,e.At,e.Revision)&&i.Operation is {}&&i.Amount>0&&i.Amount<=Budget(w,g.Agent,g.Site,i.Operation.Value,e.At,e.Revision),"代理官の処分枠・期限・数量を超えています");CommunityEconomy.ApplyFor(w,new(Effect(i.Id),i.Site,i.Operation!.Value,i.Amount),g.Agent);}
            else if(i.Action==StewardAction.Revoke){Need(g.Principal==w.PlayerControl&&e.At<g.Until&&!w.Stewardship.Entries.Any(x=>x.Revision<e.Revision&&x.Input.Action==StewardAction.Revoke&&x.Input.Grant==g.Id),"取消原本を既に発送済みか、委任期限後です");Send(w,Paper(w,e));}
            else{Need(g.Agent==w.PlayerControl&&Local(w,g.Agent,g.Site,e.At,e.Revision),"現地の代理官本人が報告してください");if(i.Action==StewardAction.Report)Need(Active(w,g,e.At,e.Revision),"有効な委任がありません");else Need(w.Stewardship.Papers.Count(p=>p.Grant==g.Id&&p.Observation==w.Stewardship.Papers.Single(q=>q.Id==i.Report).Observation)<3&&i.Action==StewardAction.ResendReport&&w.Stewardship.Papers.Any(p=>p.Id==i.Report&&p.Grant==g.Id&&p.Kind==ReportKind&&p.Sender==g.Agent),"同じ報告原本を指定してください");Send(w,Paper(w,e));}
        }
    }
    internal static void Tick(ContractWorld w)
    {
        if(!Enabled(w))return;
        foreach(var p in w.Stewardship!.Papers.Where(p=>p.Kind==RevokeKind).ToArray()){
            if(w.Stewardship.Receipts.Any(r=>r.Message==p.Id)||!w.Messaging.Attempts.Any(a=>a.MessageId==p.Id&&a.ReceiverId==p.Recipient.Value&&a.Status==AttemptStatus.Delivered))continue;
            w.Stewardship=w.Stewardship with{Receipts=[..w.Stewardship.Receipts,new(p.Id,w.ElapsedSeconds,w.CommitSequence)]};
            var g=w.Stewardship.Grants.Single(g=>g.Id==p.Grant);Send(w,new("message:steward-ack/"+Hash(p.Id),g.Id,AckKind,g.Agent,g.Principal,g.Address,w.ElapsedSeconds,w.CommitSequence,null,p.Id));
        }
        foreach(var old in w.Stewardship.Papers.Where(p=>p.Kind==ReportKind&&p.Sender!=w.PlayerControl).ToArray()){
            var failure=KnownFailure(w,old);if(failure==null||w.ElapsedSeconds<RetryAt(w,old,failure)||w.Stewardship.Papers.Count(p=>p.Grant==old.Grant&&p.Observation==old.Observation)>=3)continue;
            var g=w.Stewardship.Grants.Single(g=>g.Id==old.Grant);if(!Local(w,g.Agent,g.Site,w.ElapsedSeconds,w.CommitSequence+1))continue;string id="message:steward-retry/"+Hash(old.Id+"/"+failure.Id);if(w.Stewardship.Papers.Any(p=>p.Id==id))continue;
            Send(w,new(id,g.Id,ReportKind,g.Agent,g.Principal,g.Address,w.ElapsedSeconds,w.CommitSequence,old.Observation,old.Id));
        }
        foreach(var d in w.CommunityAdministration!.Decisions.ToArray()){
            var g=GrantAt(w,d.Site,d.At,d.Revision);if(g==null||g.Agent!=d.Actor||d.Action==null)continue;string id="message:steward-npc/"+Hash(d.Id.Value);if(w.Stewardship.Papers.Any(p=>p.Id==id))continue;
            Send(w,new(id,g.Id,ReportKind,g.Agent,g.Principal,g.Address,d.At,d.Revision,Observe(w,g,d.At,d.Revision+1,d.Reason),d.Id.Value));
        }
    }
    static MessageEnvelope? KnownFailure(ContractWorld w,StewardPaper p)=>w.Messaging.Messages.FirstOrDefault(m=>m.Kind=="delivery-failure"&&m.RecipientId==p.Sender.Value&&w.Messaging.Attempts.Any(a=>a.Id==m.Body&&a.MessageId==p.Id)&&MessageFlow.Knows(w.Messaging,m.Id,p.Sender.Value));
    static long RetryAt(ContractWorld w,StewardPaper p,MessageEnvelope failure)=>checked(MessageFlow.LearnedAt(w.Messaging,failure.Id,p.Sender.Value)!.Value+Day);
    internal static long? NextRetryBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;return w.Stewardship!.Papers.Where(p=>p.Kind==ReportKind&&p.Sender!=w.PlayerControl).Select(p=>(Paper:p,Failure:KnownFailure(w,p))).Where(x=>x.Failure!=null&&!w.Stewardship.Papers.Any(p=>p.Id=="message:steward-retry/"+Hash(x.Paper.Id+"/"+x.Failure.Id))&&w.Stewardship.Papers.Count(p=>p.Grant==x.Paper.Grant&&p.Observation==x.Paper.Observation)<3).Select(x=>RetryAt(w,x.Paper,x.Failure!)).Where(at=>at>w.ElapsedSeconds&&at<=target).Order().Select(at=>(long?)at).FirstOrDefault();
    }
    public static string? Address(ContractWorld w,MessageEnvelope m)=>w.Stewardship?.Papers.SingleOrDefault(p=>p.Id==m.Id) is {} p?NewGameWorld.Node(p.Address.Value):null;
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.Stewardship==null,"旧世界へ代理行政を追加できません");return;}Need(w.Stewardship is {Schema:1,Policy:Policy,Entries:not null,Grants:not null,Papers:not null,Receipts:not null},"代理行政の原本がありません");var s=w.Stewardship;long prior=0,at=0;var ids=new HashSet<EntityId>();
        foreach(var e in s.Entries){var i=e.Input;Need(e.At>=at&&e.At<=w.ElapsedSeconds&&e.Revision>prior&&e.Revision<=w.CommitSequence&&EntityId.IsValid(i.Id.Value)&&ids.Add(i.Id)&&Enum.IsDefined(i.Action)&&!FactionStances.OtherIdUsed(w,i.Id,includeStewardship:false)&&!FactionStances.OwnIdUsed(w,i.Id),"代理行政の入力原本が不正です");at=e.At;prior=e.Revision;
            if(i.Action==StewardAction.Assign){var g=MakeGrant(w,e);Need(s.Grants.Count(q=>q.Id==i.Id&&q==g)==1&&!s.Grants.Any(q=>q.Revision<e.Revision&&q.Site==g.Site&&q.Until>e.At&&!Stopped(w,q,e.At,e.Revision)),"委任原本または期間の重複が不正です");}
            else{Need(s.Grants.SingleOrDefault(g=>g.Id==i.Grant) is {} grant&&grant.Revision<e.Revision&&grant.Site==i.Site,"委任原本がありません");var g=s.Grants.Single(g=>g.Id==i.Grant);
                if(i.Action==StewardAction.Operate){Need(g.Agent==w.PlayerControl&&Active(w,g,e.At,e.Revision)&&i.Operation is {} op&&Enum.IsDefined(op)&&i.Amount>0&&i.Amount<=Budget(w,g.Agent,g.Site,i.Operation.Value,e.At,e.Revision),"委任枠を超えた処分です");Need(w.Communities!.Orders.Any(o=>o.Input.Id==Effect(i.Id)&&o.Input.Site==i.Site&&o.Actor==g.Agent&&o.At==e.At&&o.Revision==e.Revision&&o.Input.Action==i.Operation&&o.Input.Amount==i.Amount),"現物の履行がありません");}
                else{Need(s.Papers.Contains(Paper(w,e)),"発送原本と封書が一致しません");if(i.Action==StewardAction.Revoke)Need(g.Principal==w.PlayerControl&&e.At<g.Until&&s.Entries.Count(x=>x.Input.Action==StewardAction.Revoke&&x.Input.Grant==g.Id)==1,"当主の取消原本が不正です");else Need(g.Agent==w.PlayerControl&&Local(w,g.Agent,g.Site,e.At,e.Revision)&&(i.Action!=StewardAction.Report||Active(w,g,e.At,e.Revision))&&(i.Action!=StewardAction.ResendReport||s.Papers.Any(old=>old.Id==i.Report&&old.Kind==ReportKind&&old.Grant==g.Id&&old.Sender==g.Agent&&Before(old.At,old.Revision,e.At,e.Revision))),"代理官の現地観測がありません");}
            }
        }
        Need(s.Grants.Length==s.Entries.Count(e=>e.Input.Action==StewardAction.Assign)&&s.Papers.Select(p=>p.Id).Distinct().Count()==s.Papers.Length&&s.Receipts.Select(r=>r.Message).Distinct().Count()==s.Receipts.Length,"委任・封書・受領が重複しています");
        foreach(var p in s.Papers){if(p.Kind==ReportKind)Need(s.Papers.Count(q=>q.Grant==p.Grant&&q.Observation==p.Observation&&(Before(q.At,q.Revision,p.At,p.Revision)||q.Id==p.Id))<=3,"同じ観測の発送は三通までです");Need(p.Kind is ReportKind or RevokeKind or AckKind&&p.At<=w.ElapsedSeconds&&p.Revision<=w.CommitSequence&&w.Messaging.Messages.Contains(Envelope(p))&&w.SealedMessages.Contains(new(p.Id)),"実際の封書原本がありません");if(p.Id.StartsWith("message:steward-npc/",StringComparison.Ordinal)){var d=w.CommunityAdministration!.Decisions.Single(d=>d.Id.Value==p.Original);var g=GrantAt(w,d.Site,d.At,d.Revision);Need(g!=null&&g.Agent==d.Actor&&p.Id=="message:steward-npc/"+Hash(d.Id.Value)&&p.Grant==g.Id&&p.Sender==g.Agent&&p.Recipient==g.Principal&&p.Address==g.Address&&p.At==d.At&&p.Revision==d.Revision&&p.Kind==ReportKind&&p.Observation==Observe(w,g,d.At,d.Revision+1,d.Reason),"NPC報告が当時の観測と違います");}else if(p.Id.StartsWith("message:steward-retry/",StringComparison.Ordinal)){var old=s.Papers.Single(q=>q.Id==p.Original&&q.Kind==ReportKind);var failure=KnownFailure(w,old);Need(failure!=null&&p.Id=="message:steward-retry/"+Hash(old.Id+"/"+failure.Id)&&p.At>=RetryAt(w,old,failure)&&p.Grant==old.Grant&&p.Kind==ReportKind&&p.Sender==old.Sender&&p.Recipient==old.Recipient&&p.Address==old.Address&&p.Observation==old.Observation&&Before(old.At,old.Revision,p.At,p.Revision)&&Local(w,p.Sender,p.Observation!.Site,p.At,p.Revision)&&s.Papers.Count(q=>q.Grant==p.Grant&&q.Observation==p.Observation&&(Before(q.At,q.Revision,p.At,p.Revision)||q.Id==p.Id))<=3,"再送が既知の不達・同じ観測原本に一致しません");}else if(p.Kind==AckKind){var receipt=s.Receipts.Single(r=>r.Message==p.Original);var source=s.Papers.Single(q=>q.Id==receipt.Message);var g=s.Grants.Single(g=>g.Id==source.Grant);Need(p.Id=="message:steward-ack/"+Hash(source.Id)&&p.Grant==g.Id&&p.Sender==g.Agent&&p.Recipient==g.Principal&&p.Address==g.Address&&p.At==receipt.At&&p.Revision==receipt.Revision&&p.Observation==null,"取消返報が受領と一致しません");}else Need(s.Entries.Any(e=>e.Input.Action is StewardAction.Report or StewardAction.ResendReport or StewardAction.Revoke&&"message:steward/"+Hash(e.Input.Id.Value)==p.Id),"封書に発送操作がありません");}
        foreach(var r in s.Receipts){var p=s.Papers.Single(p=>p.Id==r.Message&&p.Kind==RevokeKind);Need(r.At>=p.At&&r.At<=w.ElapsedSeconds&&r.Revision>=p.Revision&&r.Revision<=w.CommitSequence&&w.Messaging.Attempts.Any(a=>a.MessageId==r.Message&&a.ReceiverId==p.Recipient.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt==r.At),"取消の実受領がありません");}
        Need(w.Messaging.Messages.Count(m=>m.Kind is ReportKind or RevokeKind or AckKind)==s.Papers.Length,"代理行政の封書に根拠がありません");
    }
    internal static StewardView? View(ContractWorld w)
    {
        if(!Enabled(w))return null;var site=w.Locations.Single(l=>l.Actor==w.PlayerControl).Site;bool local=site!=null&&CommunityEconomy.SitesFor(w).Contains(site.Value)&&Local(w,w.PlayerControl,site.Value,w.ElapsedSeconds,w.CommitSequence+1);
        var people=local?w.NewGame!.Start.People.Select(p=>new EntityId(p.Id)).Where(p=>p!=w.PlayerControl&&Local(w,p,site!.Value,w.ElapsedSeconds,w.CommitSequence+1)&&w.ServiceBonds.Any(b=>(b.Subject==p&&b.Target==w.PlayerControl||b.Subject==w.PlayerControl&&b.Target==p)&&b.From<=w.ElapsedSeconds&&w.ElapsedSeconds<b.Until)).ToArray():[];
        var s=w.Stewardship!;var cards=s.Grants.Select(g=>{bool ownAgent=g.Agent==w.PlayerControl;bool revoke=s.Entries.Any(e=>e.Input.Grant==g.Id&&e.Input.Action==StewardAction.Revoke);bool known=s.Papers.Any(p=>p.Grant==g.Id&&p.Kind==AckKind&&MessageFlow.Knows(w.Messaging,p.Id,w.PlayerControl.Value));return new StewardCard(g.Id,g.Principal,g.Agent,g.Site,g.Until,known?"取消受領の返報を確認":w.ElapsedSeconds>=g.Until?"合意した期限を終了":revoke?"取消を発送・返報は未確認":"期限付きの現地委任",ownAgent&&local&&site==g.Site&&Active(w,g,w.ElapsedSeconds,w.CommitSequence+1),!ownAgent&&!revoke&&w.ElapsedSeconds<g.Until);}).ToArray();
        return new(local?site:null,people,cards,s.Papers.Where(p=>p.Kind==ReportKind&&(p.Sender==w.PlayerControl||MessageFlow.Knows(w.Messaging,p.Id,w.PlayerControl.Value))).ToArray());
    }
}
public sealed partial class WorldSession
{
    public StewardView? StewardshipView(){lock(gate)return CommunityStewardship.View(state);}
    public CommandResult ManageStewardship(StewardInput input,EntityId actor,long revision)
    {lock(gate){if(actor!=state.PlayerControl||!CommunityStewardship.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の委任操作ではありません");var old=state.Stewardship!.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"操作済みです");if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の原本で使用済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"委任を読み直してください");try{Change(w=>CommunityStewardship.Apply(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"合意・現地履行・封書を記録しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}}
}
