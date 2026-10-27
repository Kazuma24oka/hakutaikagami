namespace HakutaiKagami.Core.Contracts;
public sealed record DelegatedFreightCard(EntityId Job,EntityId Carrier,EntityId Home,EntityId Source,string? Commodity,long Quantity,long Advance,string Status,HouseFreightReceipt? Receipt){[System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public FreightObservation? Report {get;init;}}
public static class NpcFreight
{
    public const string Policy="local-consenting-retainer-physical-house-freight-v13",LandRoute="house-freight-own-land-trip-v1";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    internal static bool IsTrip(ContractWorld w,Intent i)=>Enabled(w)&&i.RoutePolicy==LandRoute&&w.Requests.Any(r=>r.Intent==i)&&Jobs(w,w.Requests.Single(r=>r.Intent==i).CreatedAt,w.Requests.Single(r=>r.Intent==i).CommitSequence).Any(j=>j.Terms.Carrier==i.Actor&&!HouseLogistics.Closed(j));
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or FreightOperations.Policy or FieldConvoy.Policy;
    static bool Before(HouseFreightEntry e,long at,long rev)=>e.At<at||e.At==at&&e.Revision<rev;
    internal static HouseFreightJob[] Jobs(ContractWorld w,long at,long rev)=>HouseLogistics.FreightJobs(w,at,rev).Where(j=>j.Terms.Carrier!=w.PlayerControl).ToArray();
    internal static bool Busy(ContractWorld w,EntityId actor,long at,long rev)=>Enabled(w)&&Jobs(w,at,rev).Any(j=>j.Terms.Carrier==actor&&!HouseLogistics.Closed(j));
    static bool TaskBusy(ContractWorld w,EntityId actor,long at,long rev)=>w.Requests.Any(r=>r.Intent.Executor==actor&&r.CreatedAt<=at&&r.CommitSequence<rev&&w.Tasks.Any(t=>t.RequestId==r.Id&&(t.FinishedAt==null||t.FinishedAt>at)));
    internal static void CheckCarrier(ContractWorld w,HouseFreightInput i,EntityId actor,EntityId head,EntityId site,long at,long rev)
    {
        if(actor==w.PlayerControl){Need(i.Carrier==null,"本人の運搬は運搬者指定を省略してください");return;}
        Need(Enabled(w)&&i.Carrier==actor&&head==w.PlayerControl&&w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult&&c.WillObey)&&w.ServiceBonds.Any(b=>b.Subject==actor&&b.Target==head&&b.From<=at&&at<b.Until),"現地の協力意思を持つ成人直属家臣と当主が合意してください");
        Need(!FieldConvoy.Busy(w,actor,at,rev)&&!TaskBusy(w,actor,at,rev)&&!PersonalCompanions.Occupied(w,actor,at,rev)&&!NpcArmies.CommanderBusyAt(w,actor,at,rev),"家臣は別の用件を先に終えてください");
    }
    static EntityId Id(ContractWorld w,HouseFreightJob j,string action)
    {
        string root="request:npc-freight/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(j.Terms.Id.Value))+"/"+action;var id=new EntityId(root);int n=0;while(FactionStances.OtherIdUsed(w,id)||FactionStances.OwnIdUsed(w,id))id=new(root+"/n"+(++n));return id;
    }
    static bool Local(ContractWorld w,EntityId actor,EntityId site,long at,long rev)=>TransportWork.At(w,actor,site,at,rev,stableCompletion:true);
    static string Goal(ContractWorld w,HouseFreightJob j)=>RegionalCampaign.Destination(w,j.Phase==HouseFreightPhase.Outbound?j.Terms.Source:j.Terms.Home);
    internal static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;
        // At most one minute between decisions while an assigned carrier is idle. Travel itself uses common boundaries.
        if(!Jobs(w,w.ElapsedSeconds,w.CommitSequence+1).Any(j=>!HouseLogistics.Closed(j)&&!TaskBusy(w,j.Terms.Carrier,w.ElapsedSeconds,w.CommitSequence+1)))return null;
        long next=checked((w.ElapsedSeconds/60+1)*60);return next<=target?next:null;
    }
    internal static void TakeTurns(WorldSession session)
    {
        var w=session.DiagnosticSnapshot();if(!Enabled(w)||w.Paused||FreightOperations.Enabled(w)&&w.ElapsedSeconds%60!=0)return;
        foreach(var job in Jobs(w,w.ElapsedSeconds,w.CommitSequence+1).Where(j=>!HouseLogistics.Closed(j)))
        {
            w=session.DiagnosticSnapshot();var j=Jobs(w,w.ElapsedSeconds,w.CommitSequence+1).Single(q=>q.Terms.Id==job.Terms.Id);var t=j.Terms;
            if(TaskBusy(w,t.Carrier,w.ElapsedSeconds,w.CommitSequence+1))continue;
            if(Local(w,t.Carrier,t.Home,w.ElapsedSeconds,w.CommitSequence+1)&&j.Phase!=HouseFreightPhase.Outbound){session.RunFreightStep(new(Id(w,j,"receive"),HouseFreightAction.Receive,t.Id),t.Carrier);continue;}
            if(j.Phase==HouseFreightPhase.Outbound&&Local(w,t.Carrier,t.Source,w.ElapsedSeconds,w.CommitSequence+1))
            {
                if(!session.RunFreightStep(new(Id(w,j,"buy"),HouseFreightAction.Buy,t.Id),t.Carrier))session.RunFreightStep(new(Id(w,j,"withdraw"),HouseFreightAction.Withdraw,t.Id),t.Carrier);
                continue;
            }
            try{var move=TravelWorld.ActorMove(w,Id(w,j,"travel").Value,t.Carrier,Goal(w,j),true,LandRoute);session.Submit(move,w.CommitSequence);}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException){/* No legal route: preserve entrusted assets and wait. */}
        }
    }
    internal static void ValidatePending(ContractWorld w,Intent i)
    {
        Need(Enabled(w)&&i.Actor==i.Executor&&i.Actor!=w.PlayerControl,"家臣運搬の本人移動ではありません");var j=Jobs(w,w.ElapsedSeconds,w.CommitSequence+1).SingleOrDefault(j=>j.Terms.Carrier==i.Actor&&!HouseLogistics.Closed(j));Need(j!=null,"有効な運搬合意がありません");
        Need(i==TravelWorld.ActorMove(w,i.RequestId.Value,i.Actor,Goal(w,j),true,LandRoute),"合意した仕入先または帰還先への陸路ではありません");
    }
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.HouseFreight?.Entries.All(e=>e.Input.Carrier==null)==true||w.HouseFreight==null,"旧版へ家臣運搬を追加できません");return;}
        foreach(var e in w.HouseFreight!.Entries.Where(e=>e.Actor!=w.PlayerControl))
        {
            var t=e.Terms??HouseLogistics.FreightTerms(w,e.Input.Job!.Value);Need(t.Principal==w.PlayerControl&&w.HouseFreight.Entries.Any(d=>d.Terms?.Id==t.Id&&d.Input.Carrier==t.Carrier),"本人が現地で合意した運搬原本がありません");
        }
        foreach(var r in w.Requests.Where(r=>r.Intent.RoutePolicy==LandRoute))
        {
            var j=Jobs(w,r.CreatedAt,r.CommitSequence).SingleOrDefault(j=>j.Terms.Carrier==r.Intent.Actor&&!HouseLogistics.Closed(j));Need(j!=null&&r.Intent.Actor==r.Intent.Executor&&r.Intent.DestinationSite==(j.Phase==HouseFreightPhase.Outbound?j.Terms.Source:j.Terms.Home),"運搬経路が当時の合意と一致しません");
        }
    }
    public static EntityId[] Candidates(ContractWorld w,EntityId home)=>!Enabled(w)||RegionalDomains.Head(w,home)!=w.PlayerControl?[]:w.Characters.Where(c=>c.Id!=w.PlayerControl&&c.Life==LifeState.Adult&&c.WillObey&&Local(w,c.Id,home,w.ElapsedSeconds,w.CommitSequence+1)&&w.ServiceBonds.Any(b=>b.Subject==c.Id&&b.Target==w.PlayerControl&&b.From<=w.ElapsedSeconds&&w.ElapsedSeconds<b.Until)&&!FieldConvoy.Busy(w,c.Id,w.ElapsedSeconds,w.CommitSequence+1)&&!Busy(w,c.Id,w.ElapsedSeconds,w.CommitSequence+1)&&!TaskBusy(w,c.Id,w.ElapsedSeconds,w.CommitSequence+1)&&!PersonalCompanions.Occupied(w,c.Id,w.ElapsedSeconds,w.CommitSequence+1)&&!NpcArmies.CommanderBusyAt(w,c.Id,w.ElapsedSeconds,w.CommitSequence+1)).Select(c=>c.Id).OrderBy(c=>c.Value,StringComparer.Ordinal).ToArray();
    public static bool CanRecallAtMeeting(ContractWorld w,EntityId job){var j=Jobs(w,w.ElapsedSeconds,w.CommitSequence+1).SingleOrDefault(j=>j.Terms.Id==job&&!HouseLogistics.Closed(j));return j!=null&&w.Locations.Single(l=>l.Actor==w.PlayerControl).Site is {} site&&Local(w,w.PlayerControl,site,w.ElapsedSeconds,w.CommitSequence+1)&&Local(w,j.Terms.Carrier,site,w.ElapsedSeconds,w.CommitSequence+1);}
    public static DelegatedFreightCard[] View(ContractWorld w)
    {
        if(!Enabled(w))return [];return Jobs(w,w.ElapsedSeconds,w.CommitSequence+1).Where(j=>j.Terms.Principal==w.PlayerControl).Select(j=>{bool receipt=j.Receipt is {} got&&(Local(w,w.PlayerControl,j.Terms.Home,got.At,got.Revision+1)||Local(w,w.PlayerControl,j.Terms.Home,w.ElapsedSeconds,w.CommitSequence+1)||w.Branches!.Journeys.Any(q=>q.Actor==w.PlayerControl&&q.Legs[^1].To==RegionalCampaign.Destination(w,j.Terms.Home)&&w.Tasks.Any(t=>t.Id==q.Task&&t.Execution==ExecutionPhase.Completed&&t.FinishedAt>=got.At&&t.FinishedAt<=w.ElapsedSeconds)));return new DelegatedFreightCard(j.Terms.Id,j.Terms.Carrier,j.Terms.Home,j.Terms.Source,j.Terms.Commodity,j.Terms.Quantity,j.Terms.Advance,receipt?"帰還・現地精算済み":FreightOperations.KnownStatus(w,j.Terms.Id,"帰還報告待ち（現在の荷・購入状況は未確認）"),receipt?j.Receipt:null){Report=FreightOperations.KnownObservation(w,j.Terms.Id)};}).ToArray();
    }
}
public static partial class HouseLogistics
{
    public static HouseFreightJob[] FreightJobs(ContractWorld w,long at,long rev)=>Jobs(w,at,rev);
    internal static HouseFreightTerms FreightTerms(ContractWorld w,EntityId id)=>Terms(w,id);
}
public sealed partial class WorldSession
{
    public DelegatedFreightCard[] DelegatedFreightView(){lock(gate)return NpcFreight.View(state);}
    internal bool RunFreightStep(HouseFreightInput input,EntityId carrier){lock(gate){try{Change(w=>HouseLogistics.ApplyFor(w,input,carrier));return true;}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return false;}}}
}
