using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record EngagementTerms(EntityId Principal,EntityId Agent,EntityId EnemyOrganization,long From,long Until,EntityId Basis);
public sealed record OwnUnitReport(EntityId Unit,int Soldiers,int Lost,int Morale);
public sealed record EngagementReport(EntityId Id,EntityId Sender,EntityId Recipient,long At,OwnUnitReport[] Units,string Message);
public sealed record EngagementState(EngagementTerms Terms,EngagementReport[] Reports);
public static class EngagementMission
{
    public const string Ruleset="created-retainer-engagement-v1",Contract="explicit-synthetic-enemy-contact-loss-own-report-physical-v1",Kind="campaign-field-report";
    public static readonly EntityId EnemyOrganization=new("org:region-enemy"),Basis=new("basis:created-engagement");
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    public static ContractWorld Create(string name="作成武将",ulong seed=20261020,long until=long.MaxValue){var w=CreatedCampaign.Create(name,seed,until);w.WorldId=new("world:"+Ruleset);w.Definitions=w.Definitions with{Id=new("scenario:"+Ruleset),Ruleset=Ruleset,AuthorityBases=[..w.Definitions.AuthorityBases,new(Basis,"架空演習の検証敵との交戦任務。史実の開戦ではない")]};w.Engagement=new(new(RetainerStart.Lord,PersonalStart.Person,EnemyOrganization,0,until,Basis),[]);w.CommandMandates![0]=w.CommandMandates[0] with{Actions=[RegionalAction.Move,RegionalAction.Hold,RegionalAction.Retreat,RegionalAction.Engage]};w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};ContractValidation.Validate(w);return w;}
    public static bool CanEngage(ContractWorld w,RegionalRequest r,long at)=>Enabled(w)&&w.Engagement is {} e&&e.Terms.Agent==r.Actor&&r.Recipient==r.Actor&&e.Terms.From<=at&&at<e.Terms.Until&&w.Region!.Units.Where(u=>u.Side!=0).All(u=>w.Units.Single(x=>x.Id==u.Id).Organization==e.Terms.EnemyOrganization);
    public static OwnUnitReport[] Snapshot(ContractWorld w,long at)=>w.Region!.Units.Where(u=>u.Commander==w.PlayerControl).OrderBy(u=>u.Id.Value,StringComparer.Ordinal).Select(u=>{int lost=w.Region.Losses.Where(l=>l.Unit==u.Id&&l.At<=at).Sum(l=>l.Soldiers);int morale=u.InitialMorale-w.Region.Losses.Where(l=>l.Unit==u.Id&&l.At<=at).Sum(l=>l.Morale);return new OwnUnitReport(u.Id,u.InitialSoldiers-lost,lost,morale);}).ToArray();
    public static string Body(EngagementReport r)=>System.Text.Encoding.UTF8.GetString(ContractJson.Write(r.Units));
    public static EngagementReport[] View(WorldSession session,EntityId observer){var w=session.DiagnosticSnapshot();return w.Engagement?.Reports.Where(r=>observer==r.Sender||observer==r.Recipient&&MessageFlow.Knows(w.Messaging,r.Message,observer.Value)).ToArray()??[];}
    public static void Validate(ContractWorld w){if(!Enabled(w))return;var e=w.Engagement??throw new InvalidDataException("Missing engagement mission");var t=e.Terms;
        if(t.Principal!=RetainerStart.Lord||t.Agent!=PersonalStart.Person||t.EnemyOrganization!=EnemyOrganization||t.Basis!=Basis||t.From!=0||t.Until<=0||t.Until!=w.CommandMandates![0].Until||!w.Definitions.AuthorityBases.Any(b=>b.Id==Basis))throw new InvalidDataException("Invalid explicit engagement terms");
        if(e.Reports.Select(r=>r.Id).Distinct().Count()!=e.Reports.Length)throw new InvalidDataException("Duplicate loss report");
        foreach(var report in e.Reports){var m=w.Messaging.Messages.SingleOrDefault(m=>m.Id==report.Message);if(!EntityId.IsValid(report.Id.Value)||report.Sender!=t.Agent||report.Recipient!=t.Principal||report.At<0||report.At>w.ElapsedSeconds||!report.Units.SequenceEqual(Snapshot(w,report.At))||m==null||m.Kind!=Kind||m.SenderId!=report.Sender.Value||m.RecipientId!=report.Recipient.Value||m.CreatedAt!=report.At||m.Body!=Body(report)||m.CorrelationId!=report.Id.Value)throw new InvalidDataException("Report differs from dispatched own-unit loss ledger");}
        if(w.Messaging.Messages.Count(m=>m.Kind==Kind)!=e.Reports.Length)throw new InvalidDataException("Unbound field report");
    }
}
