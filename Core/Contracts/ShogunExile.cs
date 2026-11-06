namespace HakutaiKagami.Core.Contracts;

public static class ShogunExile
{
    public const string Route="shogun-exile-by-land-v1";
    internal static EntityId Trip(CourtEvent order)=>HouseholdCourt.Id("leave-kyoto",order.Input.Id.Value);
    internal static IEnumerable<CourtEvent> Orders(ContractWorld w)=>HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.ExpelShogun);
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {
        if(place!=CentralCourtCatalog.Kyoto || i.Site==place || i.Reference!=null || actor==i.Person ||
            SupremeOffice.ShogunAt(w,at,rev-1)!=i.Person || !SupremeOffice.ControlsKyoto(w,actor,i.Owner,at,rev-1) ||
            !TransportWork.At(w,actor,place,at,rev,true) || !TransportWork.At(w,i.Person,place,at,rev,true) ||
            Orders(w).Any(e=>e.Input.Person==i.Person && e.At<=at && e.Revision<rev))
            throw new InvalidDataException("京を掌握した当主が、京にいる現将軍に一度だけ退去先を示せます");
    }
    internal static bool Departed(ContractWorld w,EntityId incumbent,long at,long revision)=>
        Orders(w).Where(e=>e.Input.Person==incumbent && e.Revision<=revision && e.At<=at).Any(e=>
            w.Requests.Any(r=>r.Id==Trip(e) && r.CommitSequence<=revision) &&
            w.Tasks.Any(t=>t.RequestId==Trip(e) && t.StartedAt is {} start && start<=at));
    internal static bool IsJourney(ContractWorld w,Intent i)=>i.RoutePolicy==Route && i.Action==ActionKind.Move && i.Actor==i.Executor &&
        Orders(w).Any(e=>e.Input.Person==i.Actor && Trip(e)==i.RequestId && e.Input.Site==i.DestinationSite && e.At<=i.EffectiveAt);
    internal static Intent Move(ContractWorld w,CourtEvent e)=>TravelWorld.ActorMove(w,Trip(e).Value,e.Input.Person,NewGameWorld.Node(e.Input.Site.Value),true,Route);
    internal static void ValidateDeparture(ContractWorld w,Intent i)
    {
        if(!IsJourney(w,i) || !TransportWork.At(w,i.Actor,CentralCourtCatalog.Kyoto,w.ElapsedSeconds,w.CommitSequence,true) ||
            Assassination.DeadAt(w,i.Actor,w.ElapsedSeconds,w.CommitSequence) || i!=Move(w,Orders(w).Single(e=>Trip(e)==i.RequestId)))
            throw new InvalidDataException("有効な追放命令と京からの実経路を確認してください");
    }
}
public sealed partial class WorldSession
{
    internal void StartExileJourneys()
    {
        foreach(var e in ShogunExile.Orders(state).ToArray()){
            if(state.Requests.Any(r=>r.Id==ShogunExile.Trip(e)) || SocialWorld.Site(state,e.Input.Person)!=CentralCourtCatalog.Kyoto)continue;
            try{Submit(ShogunExile.Move(state,e),state.CommitSequence);}
            catch(InvalidDataException){/* 通行可能な経路と本人の空きを待つ。 */}
        }
    }
}