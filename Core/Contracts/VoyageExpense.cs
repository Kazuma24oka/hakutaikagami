namespace HakutaiKagami.Core.Contracts;
public sealed record VoyageExpenseRules(int Schema,string Policy,long FarePerPassage);
public sealed record VoyagePayment(EntityId Id,EntityId Task,EntityId Payer,int LegIndex,string Edge,long StartAt,long At,long Revision,long Amount,EntityId Payee);
public sealed record VoyageRefusal(EntityId Task,int LegIndex,string Edge,long At,long Revision,long Required,long Available);
public sealed record VoyageExpenseState(int Schema,string Policy,VoyagePayment[] Payments,VoyageRefusal[] Refusals);
public static class VoyageExpense
{
    public const string Policy="private-sea-fare-at-boarding-external-account-v1",WalkOnly="personal-walk-only-v1";
    public static VoyageExpenseRules Standard=>new(1,Policy,10);
    static void Need(bool b,string why){if(!b)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.VoyageRules!=null;
    public static void ValidateRules(VoyageExpenseRules? r){if(r!=null)Need(r.Schema==1&&r.Policy==Policy&&r.FarePerPassage is >=1 and <=1000,"Unknown voyage fare rules");}
    public static string[] PlanningExclusions(ContractWorld w,EntityId actor,bool walkOnly)=>BranchRoutes.KnownBlocked(w,actor).Concat(walkOnly?BranchRoutes.GraphEdges(w).Where(e=>NationalRoutes.Sea(e.Id)).Select(e=>e.Id):[]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    public static string[] PlanningExclusions(ContractWorld w,TaskState task,IEnumerable<string>? known=null)
    {
        bool walk=w.Requests.Single(q=>q.Id==task.RequestId).Intent.RoutePolicy is WalkOnly or NpcArmies.LandRoute or PersonalCompanions.LandRoute or NpcFreight.LandRoute or ArmyRoadMarch.LandRoute or RoadConvoy.LandRoute;
        return (known??BranchRoutes.KnownBlocked(w,task.Executor)).Concat(walk?BranchRoutes.GraphEdges(w).Where(e=>NationalRoutes.Sea(e.Id)).Select(e=>e.Id):[]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
    static bool Before(long at,long rev,long time,long cut)=>at<time||at==time&&rev<cut;
    public static long Net(ContractWorld w,EntityId lot,long at=long.MaxValue,long revision=long.MaxValue)=>lot==NewGameWork.Wallet(w.PlayerControl)?-(w.VoyageExpenses?.Payments.Where(p=>Before(p.At,p.Revision,at,revision)).Sum(p=>p.Amount)??0):0;
    static EntityId PaymentId(EntityId task,int leg)=>new("payment:voyage/"+task.Value.Replace(':','/')+"/"+leg);
    static EntityId Operator(string edge)=>new("account:voyage-operator/"+edge);
    internal static bool Board(ContractWorld w,TaskState task,int index,JourneyLeg leg)
    {
        if(!Enabled(w)||task.Executor!=w.PlayerControl)return true;
        if(w.VoyageExpenses?.Payments.Any(p=>p.Id==PaymentId(task.Id,index))==true)return true;
        Need(NationalRoutes.Sea(leg.Edge)&&w.ElapsedSeconds==TravelWorld.Departure(w,leg.Edge,leg.StartAt),"Fare only at actual boarding");
        w.VoyageExpenses??=new(1,Policy,[],[]);var s=w.VoyageExpenses;long price=w.NewGame!.VoyageRules!.FarePerPassage,available=NewGameWork.Balance(w,w.PlayerControl);
        if(available<price){w.VoyageExpenses=s with{Refusals=[..s.Refusals,new(task.Id,index,leg.Edge,w.ElapsedSeconds,w.CommitSequence,price,available)]};return false;}
        w.VoyageExpenses=s with{Payments=[..s.Payments,new(PaymentId(task.Id,index),task.Id,w.PlayerControl,index,leg.Edge,leg.StartAt,w.ElapsedSeconds,w.CommitSequence,price,Operator(leg.Edge))]};NewGameWork.Sync(w);return true;
    }
    public static void ValidateStop(ContractWorld w,TrafficStop stop)
    {
        Need(Enabled(w),"Fare stop has no rules");var refusal=w.VoyageExpenses?.Refusals.SingleOrDefault(x=>x.Task==stop.Task)??throw new InvalidDataException("Fare refusal lacks local receipt");var t=w.Tasks.Single(t=>t.Id==stop.Task);var j=w.Branches!.Journeys.Single(j=>j.Task==stop.Task);Need(refusal.LegIndex>=0&&refusal.LegIndex<j.Legs.Length,"Fare stop leg missing");var l=j.Legs[refusal.LegIndex];
        Need(stop.Actor==w.PlayerControl&&t.Executor==stop.Actor&&t.Execution==ExecutionPhase.Stopped&&t.InternalReason=="fare-insufficient"&&t.FinishedAt==stop.At&&stop.At==refusal.At&&stop.At<=w.ElapsedSeconds&&stop.Edge==l.Edge&&stop.Node==l.From&&NationalRoutes.Sea(l.Edge)&&stop.At==TravelWorld.Departure(w,l.Edge,l.StartAt)&&!RegionalCampaign.BlockedAt(w,l.Edge,stop.At),"Fare stop lacks actual port boundary");
        Need(refusal.Required==w.NewGame!.VoyageRules!.FarePerPassage&&refusal.Available==TransportWork.WalletBefore(w,w.PlayerControl,refusal.At,refusal.Revision)&&refusal.Available<refusal.Required&&refusal.Available>=0&&refusal.Revision>0&&refusal.Revision<=w.CommitSequence,"Fare refusal differs from personal funds");
    }
    internal static void Rewind(ContractWorld w,long at,long rev)
    {
        if(w.VoyageExpenses==null)return;var s=w.VoyageExpenses;
        var payments=s.Payments.Where(p=>p.At<at||p.At==at&&p.Revision<=rev).ToArray();var refusals=s.Refusals.Where(p=>p.At<at||p.At==at&&p.Revision<=rev).ToArray();
        w.VoyageExpenses=payments.Length+refusals.Length==0?null:s with{Payments=payments,Refusals=refusals};
        if(w.TrafficStops!=null)w.TrafficStops=w.TrafficStops.Where(s=>s.Reason!="fare-insufficient"||refusals.Any(p=>p.Task==s.Task)).ToArray();
    }
    public static void Validate(ContractWorld w)
    {
        ValidateRules(w.NewGame?.VoyageRules);var s=w.VoyageExpenses;
        Need(w.Requests.All(q=>q.Intent.RoutePolicy==null||q.Intent.RoutePolicy==RoadConvoy.LandRoute&&RoadConvoy.IsTrip(w,q.Intent)||q.Intent.RoutePolicy==ArmyRoadMarch.LandRoute&&ArmyRoadMarch.IsTrip(w,q.Intent)||q.Intent.RoutePolicy==NpcFreight.LandRoute&&NpcFreight.Enabled(w)||q.Intent.RoutePolicy==PersonalCompanions.LandRoute&&(PersonalCompanions.IsTask(w,q.Intent)||HostageCustody.IsReturnTask(w,q.Intent))||q.Intent.RoutePolicy==NpcArmies.LandRoute&&NpcArmies.IsMarch(w,q.Intent)||Enabled(w)),"Route choice requires pinned start rules");
        if(!Enabled(w)){Need(s==null,"Unexpected voyage expenses");return;}Need(NewGameWork.Enabled(w),"Fare needs explicit private wallets");
        if(s!=null)Need(s.Schema==1&&s.Policy==Policy&&s.Payments.Length+s.Refusals.Length>0,"Unknown voyage ledger");
        var payments=s?.Payments??[];var refusals=s?.Refusals??[];Need(payments.Select(p=>p.Id).Distinct().Count()==payments.Length&&refusals.Select(p=>p.Task).Distinct().Count()==refusals.Length,"Duplicate fare receipt");
        var timeline=payments.Select(p=>(p.At,p.Revision)).Concat(refusals.Select(p=>(p.At,p.Revision))).OrderBy(p=>p.At).ToArray();Need(timeline.Select(p=>p.Revision).Distinct().Count()==timeline.Length&&timeline.Zip(timeline.Skip(1),(a,b)=>a.At<b.At&&a.Revision<b.Revision).All(x=>x),"Fare commit order differs from time");
        foreach(var p in payments){var task=w.Tasks.Single(t=>t.Id==p.Task);var journey=w.Branches!.Journeys.Single(j=>j.Task==p.Task);Need(p.LegIndex>=0&&p.LegIndex<journey.Legs.Length,"Missing paid sea leg");var l=journey.Legs[p.LegIndex];Need(task.Action==ActionKind.Move&&task.Executor==w.PlayerControl&&task.Issuer==w.PlayerControl&&p.Payer==w.PlayerControl&&p.Id==PaymentId(task.Id,p.LegIndex)&&p.Edge==l.Edge&&p.StartAt==l.StartAt&&p.Payee==Operator(l.Edge)&&NationalRoutes.Sea(l.Edge)&&p.At==TravelWorld.Departure(w,l.Edge,l.StartAt)&&p.At<=w.ElapsedSeconds&&p.Amount==w.NewGame!.VoyageRules!.FarePerPassage&&p.Revision>w.Requests.Single(q=>q.Id==task.RequestId).CommitSequence&&p.Revision<=w.CommitSequence&&!RegionalCampaign.BlockedAt(w,p.Edge,p.At)&&TransportWork.WalletBefore(w,p.Payer,p.At,p.Revision)>=p.Amount,"Invalid actual fare payment");}
        foreach(var j in w.Branches!.Journeys.Where(j=>j.Actor==w.PlayerControl)){var t=w.Tasks.Single(t=>t.Id==j.Task);var input=w.Requests.Single(q=>q.Id==t.RequestId).Intent;if(input.RoutePolicy==WalkOnly)Need(j.Legs.All(l=>!NationalRoutes.Sea(l.Edge)),"Walk-only route entered sea");for(int n=0;n<j.Legs.Length;n++){var l=j.Legs[n];if(!NationalRoutes.Sea(l.Edge))continue;var dep=TravelWorld.Departure(w,l.Edge,l.StartAt);bool boarded=dep<=w.ElapsedSeconds&&(t.FinishedAt==null||t.FinishedAt>dep);Need(payments.Any(p=>p.Id==PaymentId(j.Task,n))==boarded,"Boarded sea leg and paid receipt differ");}}
        foreach(var f in refusals){var stop=w.TrafficStops?.SingleOrDefault(s=>s.Task==f.Task&&s.Reason=="fare-insufficient")??throw new InvalidDataException("Refusal did not stop at port");ValidateStop(w,stop);Need(!payments.Any(p=>p.Task==f.Task&&p.LegIndex==f.LegIndex),"Refused boarding was charged");}
        Need(w.ResourceLots.Where(l=>l.Kind=="money"&&l.Unit=="mon").Sum(l=>l.InitialQuantity)==checked(w.ResourceLots.Where(l=>l.Kind=="money"&&l.Unit=="mon").Sum(l=>l.Quantity)+payments.Sum(p=>p.Amount)-w.ResourceLots.Where(l=>l.Kind=="money"&&l.Unit=="mon").Sum(l=>RegionalDomains.Net(w,l.Id)+RegionalTrade.Net(w,l.Id)+PrivateStanddown.Net(w,l.Id)+PrivateInformation.Net(w,l.Id)+PersonalCompanions.Net(w,l.Id)+TransportWork.Net(w,l.Id)+ArmyRoadMarch.Net(w,l.Id)+PersonalLife.Net(w,l.Id))),"Private money and operator receipts are not conserved");
    }
}
