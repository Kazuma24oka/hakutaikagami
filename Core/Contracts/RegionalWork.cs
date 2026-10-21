using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public sealed record RegionalRole(EntityId Person,string Node,string Position,string Source,string CheckedOn);
// A new profile. Historical standing is evidence; a consensual inspection permit is gameplay setup.
public static class RegionalWork
{
    public const string Ruleset="regional-peace-work-v1";
    public const string Contract="three-local-voluntary-inspections-fixed-scope-address-work4-sealed-report1-v1";
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset||NpcWork.Enabled(w);
    public static RegionalRole[] Roles=>[
        new(new("person:nobunaga"),"castle","清須を拠点とする領主（支配領域は未収録）","https://www.city.kiyosu.aichi.jp/kiyosu_brand/jinbutsu_den.html","2026-10-21"),
        new(new("person:shingen"),"kofu","甲斐の国主（具体的な権限・領域は別定義）","https://www.pref.yamanashi.jp/sabo/114_019.html","2026-10-21"),
        new(new("person:ujiyasu"),"odawara","1559年に氏政へ家督譲渡後・御本城様（現役当主としない）","https://www.city.odawara.kanagawa.jp/encycl/neohojo5/009/","2026-10-21")];
    public static ContractWorld Create()
    {
        var w=OfficerLoop.Create();w.WorldId=new("world:"+Ruleset);
        var basis=new EntityId("basis:regional-voluntary-inspection");
        w.Definitions=w.Definitions with{Id=new("scenario:"+Ruleset),Ruleset=Ruleset,AuthorityBases=[..w.Definitions.AuthorityBases,new(basis,"試作用の任意点検の合意。史実の任命/徴税/軍事/土地権ではない")]};
        foreach(var role in Roles)foreach(var actor in new[]{role.Person,w.PlayerControl})
            w.Grants=[..w.Grants,new(new("grant:regional-work/"+role.Node+"/"+actor.Value.Replace(':','/')),role.Person,actor,RegionalCampaign.Site(role.Node),[ActionKind.Inspect],basis,0,long.MaxValue,0,[],false,0)];
        w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};ContractValidation.Validate(w);return w;
    }
    public static TaskKind Kind(ContractWorld w,EntityId issuer)=>(RetainerStart.Enabled(w)||CreatedCampaign.Enabled(w))&&issuer==RetainerStart.Lord?TaskKind.BindingOrder:TaskKind.VoluntaryRequest;
    public static Intent Build(ContractWorld w,string id,EntityId issuer,int due=60)
    {
        if(!Enabled(w)||!EntityId.IsValid(id)||due is <1 or >3600)throw new InvalidDataException("Local work profile required");
        var role=Roles.SingleOrDefault(r=>r.Person==issuer)??throw new InvalidDataException("Unknown local dispatcher");
        long end=checked(w.ElapsedSeconds+due);
        return new(new(id),issuer,w.PlayerControl,RegionalCampaign.Site(role.Node),ActionKind.Inspect,null,null,0,null,
            new(DeadlineCriterion.Completion,end,false,null,end),w.ElapsedSeconds,1,4,1,true,Kind(w,issuer));
    }
    public static void ValidateOrder(ContractWorld w,Intent i)
    {
        long due=(i.Deadline.At??-1)-w.ElapsedSeconds;
        if(due is <1 or >3600||i!=Build(w,i.RequestId.Value,i.Actor,(int)due))throw new InvalidDataException("Invalid local work fields");
        bool At(EntityId actor)=>w.Locations.Any(l=>l.Actor==actor&&l.Kind==LocationKind.AtSite&&l.Site==i.Scope);
        if(FieldPresence.PlayerDeployed(w)||!At(i.Actor)||!At(i.Executor)||OfficerLoop.Busy(w,i.Executor)||
            !WorldSession.Permits(w,i.Actor,i,w.ElapsedSeconds)||!WorldSession.Permits(w,i.Executor,i,w.ElapsedSeconds))throw new InvalidDataException("Local work unavailable");
    }
    // Dispatcher-only availability. Never include private willingness in a visible explanation.
    public static bool CanDispatch(ContractWorld w,EntityId issuer)
    {try{ValidateOrder(w,Build(w,"request:regional-work-preview",issuer));return true;}catch(InvalidDataException){return false;}}
    public static EntityId[] NearbyDispatchers(WorldSession s)=>s.GetNearbyActors(s.DiagnosticSnapshot().PlayerControl).Select(a=>a.Actor).Where(id=>Roles.Any(r=>r.Person==id)).ToArray();
    public static string? Address(ContractWorld w,MessageEnvelope message)
    {
        if(!Enabled(w))return null;
        var task=w.Tasks.SingleOrDefault(t=>t.Action==ActionKind.Inspect&&Roles.Any(r=>r.Person==t.Issuer)&&
            (WorldSession.Child("message",t.Id,"order").Value==message.Id||WorldSession.Child("message",t.Id,"report").Value==message.Id));
        return task==null?null:RegionalCampaign.Destination(w,task.Scope);
    }
    public static IncomingMission.Card[] Inbox(WorldSession s)
    {
        var v=s.GetActorView(s.DiagnosticSnapshot().PlayerControl);var w=s.DiagnosticSnapshot();
        return v.Messages.Where(m=>w.Messaging.Messages.Single(x=>x.Id==m.Id.Value).Kind=="contract-order"&&w.Tasks.Any(t=>t.Action==ActionKind.Inspect&&WorldSession.Child("message",t.Id,"order")==m.Id)).Select(m=>{
            if(m.KnownBody==null)return new IncomingMission.Card(m.Id,null,"現地からの書状・未開封（内容未確認）",m.Sealed,false);
            var task=w.Tasks.Single(t=>t.Id.Value==m.KnownBody);var person=w.Definitions.Characters.Single(p=>p.Id==task.Issuer);
            string site=w.Definitions.Sites.Single(p=>p.Id==task.Scope).Name;
            return new IncomingMission.Card(m.Id,task.Id,person.Name+(task.Kind==TaskKind.BindingOrder?"からの点検命令：":"からの任意依頼：")+site+"の点検 / "+v.Tasks.Single(t=>t.Id==task.Id).KnownStatus,false,task.Agreement==AgreementPhase.AwaitingAssent);
        }).ToArray();
    }
    public static void Validate(ContractWorld w)
    {
        foreach(var task in w.Tasks.Where(t=>t.Action==ActionKind.Inspect)){
            var request=w.Requests.Single(r=>r.Id==task.RequestId);var i=request.Intent;
            if(!Roles.Any(r=>r.Person==i.Actor&&RegionalCampaign.Site(r.Node)==i.Scope)||i.Executor!=w.PlayerControl||i.Kind!=Kind(w,i.Actor)||i.Action!=ActionKind.Inspect||i.Amount!=0||i.SourceLot!=null||i.DestinationLot!=null||i.DestinationSite!=null||i.WorkSeconds!=4||i.DeliverySeconds!=1||i.ReportSeconds!=1||!i.Important||i.TargetTask!=null||i.EffectiveAt!=request.CreatedAt||i.Deadline.Criterion!=DeadlineCriterion.Completion||i.Deadline.FromOpening||i.Deadline.DurationSeconds!=null||i.Deadline.At!=i.Deadline.AbsoluteLatestAt||i.Deadline.AbsoluteLatestAt-request.CreatedAt is <1 or >3600)
                throw new InvalidDataException("Regional work lacks a valid local contract");
        }
        foreach(var g in w.Grants.Where(g=>Roles.Any(r=>r.Person==g.Agent))){if(g.Actions.Length!=1||g.Actions[0]!=ActionKind.Inspect||g.Limit!=0||g.Assets.Length!=0||g.Redelegate||!Roles.Any(r=>r.Person==g.Agent&&RegionalCampaign.Site(r.Node)==g.Scope))throw new InvalidDataException("Historical dispatcher granted unregistered coercive rights");}
        foreach(var c in w.Couriers!.Missions){var message=w.Messaging.Messages.Single(m=>m.Id==w.Messaging.Attempts.Single(a=>a.Id==c.Attempt).MessageId);var address=Address(w,message);if(address!=null&&c.Destination!=address)throw new InvalidDataException("Local work left its fixed dispatch address");}
    }
}
