using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record RecoveryArrival(long At,RoutePoint RecipientPosition);
public sealed record RecoveredCourier(string Attempt,long FoundAt,long Revision,FieldPoint Origin,FieldPoint Address,FieldLeg[] Path,long DueAt,RecoveryArrival? Arrival);
// Explicitly found original, local land recovery only. Not an actor's remote locate/delay command.
public static class CourierRecovery
{
    public const string Contract="registered-local-land-original-recovery-from-failed-courier-to-known-address-v1";
    public static bool Registered(ContractWorld w,string attempt)=>(CareerMilitary.Enabled(w)||TruceConfirmations.Enabled(w)||NewGameMilitary.LedgerStates(w).Any(s=>s.RemoteSuccession!=null))&&w.CourierRecoveries?.Any(r=>r.Attempt==attempt)==true;
    static bool Supported(ContractWorld w,MessageEnvelope m)=>NewGameMilitary.LedgerStates(w).Any(s=>s.RemoteSuccession?.Papers.Any(p=>p.Id==m.Id&&p.Kind==RemoteMilitaryKind.Offer)==true)||w.TruceConfirmations?.Papers.Any(p=>p.Id==m.Id&&(p.Kind is TruceConfirmationKind.Confirmation or TruceConfirmationKind.RepresentativeInquiry))==true||CareerMilitary.Enabled(w)&&(w.Military!.Requests.Any(q=>q.Message==m.Id)||w.Military.Events.Any(e=>e.Message==m.Id));
    public static RoutePoint? Position(ContractWorld w,string attempt,long at){var r=w.CourierRecoveries?.SingleOrDefault(r=>r.Attempt==attempt&&r.FoundAt<=at);return r==null?null:FieldWorld.ToWorld(RegionalGround.At(r.Path,r.Origin,at));}
    public static bool Moving(ContractWorld w,long at)=>w.CourierRecoveries?.Any(r=>r.FoundAt<=at&&r.Path.Any(l=>l.EndAt>at))==true;
    internal static void Start(ContractWorld w,string message,int number)
    {
        var m=w.Messaging.Messages.Single(m=>m.Id==message);var a=w.Messaging.Attempts.Single(a=>a.MessageId==message&&a.Number==number);
        if(!Supported(w,m)||a.Status!=AttemptStatus.Lost||a.FailureAt is not {} failure||a.LateDueAt!=null||Registered(w,a.Id))throw new InvalidDataException("Only one explicit local recovery of a lost military original is supported");
        var c=w.Couriers!.Missions.Single(c=>c.Attempt==a.Id);var from=FieldWorld.FromWorld(RegionalCampaign.OriginalPosition(w,c,failure));var goal=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>n.Id==c.Destination).Position);
        if(RegionalGround.Distance(from,goal)>80000)throw new InvalidDataException("Recovery outside the local land scope");
        var path=from==goal?Array.Empty<FieldLeg>():RegionalGround.Plan(from,goal,w.ElapsedSeconds,courier:true,scenery:RegionalScenery.Enabled(w),atlas:ContinuousWorld.Enabled(w)||NewGameWorld.Enabled(w));long due=Math.Max(w.ElapsedSeconds+1,path.LastOrDefault()?.EndAt??w.ElapsedSeconds+1);
        MessageFlow.ScheduleLate(w.Messaging,message,number,w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));w.CourierRecoveries=[..w.CourierRecoveries??[],new(a.Id,w.ElapsedSeconds,w.CommitSequence,from,goal,path,due,null)];
    }
    internal static void BeforeMessages(ContractWorld w)
    {
        foreach(var r in (w.CourierRecoveries??[]).Where(r=>r.DueAt==w.ElapsedSeconds&&r.Arrival==null).ToArray()){
            var a=w.Messaging.Attempts.Single(a=>a.Id==r.Attempt);var p=RegionalCampaign.ActorPoint(w,new(a.ReceiverId));w.CourierRecoveries=w.CourierRecoveries!.Select(x=>x.Attempt==r.Attempt?x with{Arrival=new(w.ElapsedSeconds,p)}:x).ToArray();
            if(RegionalGround.Distance(FieldWorld.FromWorld(p),r.Address)>1500)w.Messaging.Attempts=w.Messaging.Attempts.Select(x=>x.Id==r.Attempt?x with{Status=AttemptStatus.Rejected,FinishedAt=w.ElapsedSeconds}:x).ToArray();
        }
    }
    public static CourierView? LocalView(ContractWorld w,CourierMission c,EntityId observer)
    {
        var r=w.CourierRecoveries?.SingleOrDefault(r=>r.Attempt==c.Attempt&&r.FoundAt<=w.ElapsedSeconds&&w.ElapsedSeconds<r.DueAt);if(r==null)return null;
        var p=Position(w,c.Attempt,w.ElapsedSeconds)!;var own=RegionalCampaign.ActorPoint(w,observer);if(Math.Abs(p.EastMm-own.EastMm)>80000||Math.Abs(p.NorthMm-own.NorthMm)>80000)return null;
        var a=w.Messaging.Attempts.Single(a=>a.Id==c.Attempt);var m=w.Messaging.Messages.Single(m=>m.Id==a.MessageId);var next=Position(w,c.Attempt,w.ElapsedSeconds+1)!;bool informed=m.SenderId==observer.Value||MessageFlow.Knows(w.Messaging,m.Id,observer.Value);string opaque=ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(c.Attempt));
        return new(informed?c.Attempt:"courier:seen/"+opaque,informed?m.Id:"message:unread/"+opaque,informed?c.Destination:"届け先未確認",informed?a.SentAt:null,informed?c.Appointment:null,p,r.Path.Any(l=>l.EndAt>w.ElapsedSeconds)?"移動を目撃":"待機を目撃",new(next.EastMm-p.EastMm,next.NorthMm-p.NorthMm),informed?0:checked(w.ElapsedSeconds*2000),next,RegionalGround.Distance(FieldWorld.FromWorld(p),FieldWorld.FromWorld(next)));
    }
    public static void Validate(ContractWorld w)
    {
        if(w.CourierRecoveries==null)return;if(!(CareerMilitary.Enabled(w)||TruceConfirmations.Enabled(w)||NewGameMilitary.LedgerStates(w).Any(s=>s.RemoteSuccession!=null))||w.CourierRecoveries.Select(r=>r.Attempt).Distinct().Count()!=w.CourierRecoveries.Length)throw new InvalidDataException("Unexpected/repeated original recovery");
        foreach(var r in w.CourierRecoveries){var a=w.Messaging.Attempts.Single(a=>a.Id==r.Attempt);var m=w.Messaging.Messages.Single(m=>m.Id==a.MessageId);var c=w.Couriers!.Missions.Single(c=>c.Attempt==r.Attempt);
            if(!Supported(w,m)||a.FailureAt is not {} failure||r.FoundAt<failure||r.FoundAt>w.ElapsedSeconds||r.Revision<1||r.Revision>w.CommitSequence||r.Origin!=FieldWorld.FromWorld(RegionalCampaign.OriginalPosition(w,c,failure))||r.Address!=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>n.Id==c.Destination).Position)||RegionalGround.Distance(r.Origin,r.Address)>80000)throw new InvalidDataException("Recovery origin/address lacks its failed physical courier");
            var path=r.Origin==r.Address?Array.Empty<FieldLeg>():RegionalGround.Plan(r.Origin,r.Address,r.FoundAt,courier:true,scenery:RegionalScenery.Enabled(w),atlas:ContinuousWorld.Enabled(w)||NewGameWorld.Enabled(w));long due=Math.Max(r.FoundAt+1,path.LastOrDefault()?.EndAt??r.FoundAt+1);
            if(ContractJson.CanonicalHash(path)!=ContractJson.CanonicalHash(r.Path)||r.DueAt!=due||a.LateDueAt!=due)throw new InvalidDataException("Late copy lacks registered land path and travel time");
            if(w.ElapsedSeconds<due){if(r.Arrival!=null||a.Status!=AttemptStatus.Lost)throw new InvalidDataException("Premature recovery outcome");}
            else{if(r.Arrival?.At!=due||a.FinishedAt!=due)throw new InvalidDataException("Recovery lacks local arrival witness");bool present=RegionalGround.Distance(FieldWorld.FromWorld(r.Arrival!.RecipientPosition),r.Address)<=1500;bool timely=m.DeliverBy==null||due<=m.DeliverBy;if(present&&timely?a.Status!=AttemptStatus.Delivered||!a.ArrivedLate:a.Status!=AttemptStatus.Rejected||a.ArrivedLate)throw new InvalidDataException("Recovery receipt differs from physical presence/deadline");}
        }
    }
}
public sealed partial class WorldSession
{
    // Simulation fixture event, never exposed as a player locate command or ordinary actor view.
    public void DiagnosticRecoverLocalCourier(string message,int number){lock(gate)Change(w=>CourierRecovery.Start(w,message,number));}
}
