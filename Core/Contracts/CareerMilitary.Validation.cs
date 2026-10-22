using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public static partial class CareerMilitary
{
    internal static bool OtherIdUsed(ContractWorld w,EntityId id)=>w.Career!.Requests.Any(q=>q.Input.Id==id)||w.Requests.Any(q=>q.Id==id)||w.Region!.Commands.Any(q=>q.Request.Id==id)||w.Field!.Orders.Any(q=>q.Request.Id==id)||w.TravelDecisions!.Any(q=>q.Request.Id==id)||w.Supply!.Moves.Any(q=>q.Id==id)||w.Engagement!.Reports.Any(q=>q.Id==id);
    internal static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Military?.Requests.Any(q=>q.Input.Id==id)==true||NewGameService.OwnIdUsed(w,id);
    public static void ValidateArmy(ContractWorld w)
    {
        if(!w.Definitions.Armies.Contains(new(CreatedCampaign.Army,RetainerStart.House,Lord))||w.Atlas!.Anchor!="castle"||w.Region!.PerForce!=3||w.Region.Hostile||w.Region.Losses.Length!=0||w.Field!.Orders.Length!=0)throw new InvalidDataException("Placement-only military profile differs");
        bool custody=Custody(w);var commander=custody?w.PlayerControl:Lord;var ids=FieldWorld.UnitIds.ToHashSet();
        if(w.Units.Where(u=>u.Commander==w.PlayerControl).Any(u=>!ids.Contains(u.Id))||ids.Any(id=>!w.Units.Any(u=>u.Id==id&&u.Commander==commander&&u.Army==CreatedCampaign.Army&&u.Organization==RetainerStart.House)||!w.Region.Units.Any(u=>u.Id==id&&u.Commander==commander&&u.Side==0))||w.Region.Units.Where(u=>ids.Contains(u.Id)).Sum(u=>u.InitialSoldiers)!=100)throw new InvalidDataException("Borrowed force ownership/command differs");
        for(int i=0;i<FieldWorld.UnitIds.Length;i++)if(w.Region.Units.Single(u=>u.Id==FieldWorld.UnitIds[i]).Home!=ContinuousWorld.Point("castle",i*10000,0))throw new InvalidDataException("Loan reserve home moved");
        var pickup=Event(w,MilitaryEffect.PickedUp,w.ElapsedSeconds,w.CommitSequence);var end=Event(w,MilitaryEffect.Returned,w.ElapsedSeconds,w.CommitSequence);
        CommandMandate[] expected=pickup==null?[]:[new(CreatedCampaign.Mandate,Lord,w.PlayerControl,CreatedCampaign.Army,FieldWorld.UnitIds,[RegionalAction.Move,RegionalAction.Hold,RegionalAction.Retreat],CreatedCampaign.Basis,pickup.At,end?.At??long.MaxValue)];
        if(ContractJson.CanonicalHash(w.CommandMandates)!=ContractJson.CanonicalHash(expected))throw new InvalidDataException("Command rights differ from explicit local custody periods");
        if(w.Region.Commands.Any(c=>c.Request.Actor==w.PlayerControl&&(c.Request.Action==RegionalAction.Engage||!CustodyAt(w,c.IssuedAt)))||w.Supply!.Sorties.Any(s=>!CustodyAt(w,s.From))||w.Supply.Moves.Any(m=>!CustodyAt(w,m.At))||w.Engagement!.Reports.Any(r=>!CustodyAt(w,r.At)))throw new InvalidDataException("Military action outside custody");
    }
    public static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){if(w.Military!=null)throw new InvalidDataException("Unexpected military module");return;}
        var s=w.Military??throw new InvalidDataException("Missing military module");
        if(s.Schema!=1||w.Career!.Terms.DismissAfterReport||!w.Career.Terms.Recruits||w.Evidence.Any(e=>e.Id.Value.StartsWith("evidence:created-command/")||e.Id.Value.StartsWith("evidence:created-retainer/")))throw new InvalidDataException("Military start inherited unearned service or command");
        if(s.Requests.Select(q=>q.Input.Id).Distinct().Count()!=s.Requests.Length||s.Requests.GroupBy(q=>q.Input.Action).Any(g=>g.Count()>1)||s.Events.Where(e=>e.Effect!=MilitaryEffect.StatusSent).GroupBy(e=>e.Effect).Any(g=>g.Count()>1)||s.Events.Where(e=>e.Effect==MilitaryEffect.StatusSent).GroupBy(e=>e.Source).Any(g=>g.Count()>1)||s.Openings.Select(o=>o.Message).Distinct().Count()!=s.Openings.Length)throw new InvalidDataException("Repeated military effect/business");
        if(w.Messaging.Messages.Count(m=>m.Kind=="military-request")!=s.Requests.Count(q=>q.Message!=null)||w.Messaging.Messages.Count(m=>m.Kind is "military-notice" or "military-status")!=s.Events.Length)throw new InvalidDataException("Military transport ledger incomplete");
        ValidateRetries(w);
        bool Received(string message,long at)=>MessageFlow.LearnedAt(w.Messaging,message,Lord.Value) is {} t&&t<=at;
        foreach(var o in s.Openings)if(o.At<0||o.At>w.ElapsedSeconds||o.Revision<1||o.Revision>w.CommitSequence||!s.Events.Any(e=>e.Message==o.Message&&e.Revision<=o.Revision)||MessageFlow.LearnedAt(w.Messaging,o.Message,w.PlayerControl.Value)!=o.At||!w.Messaging.Attempts.Any(a=>a.MessageId==o.Message&&a.ReceiverId==w.PlayerControl.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=o.At))throw new InvalidDataException("Military opening has no physical notice");
        foreach(var q in s.Requests){var i=q.Input;if(!EntityId.IsValid(i.Id.Value)||!Enum.IsDefined(i.Action)||q.At<0||q.At>w.ElapsedSeconds||q.Revision<1||q.Revision>w.CommitSequence||OtherIdUsed(w,i.Id))throw new InvalidDataException("Invalid military business ID/time");
            var before=View(w,w.PlayerControl,q.At,q.Revision-1);bool eligible=i.Action switch{MilitaryAction.Request=>before.CanRequest,MilitaryAction.Accept=>before.CanAccept,MilitaryAction.Pickup=>before.CanPickup,MilitaryAction.Return=>before.CanReturn,_=>false};if(!eligible)throw new InvalidDataException("Military decision lacks known conditions");
            if(i.Action is MilitaryAction.Request or MilitaryAction.Pickup){if(!NpcWork.Inputs(w,Lord,q.At,q.Revision-1).LocalWitness||RegionalCampaign.RoadActorPointAt(w,w.PlayerControl,q.At)!=BranchRoutes.GraphNodes(w).Single(n=>n.Id=="castle").Position)throw new InvalidDataException("Military local action has no audience");}
            if(i.Action==MilitaryAction.Pickup){if(q.Message!=null||!s.Events.Any(e=>e.Effect==MilitaryEffect.PickedUp&&e.Source==i.Id.Value&&e.At==q.At&&e.Revision==q.Revision))throw new InvalidDataException("Local pickup is not a distant letter");continue;}
            var m=w.Messaging.Messages.Single(x=>x.Id==q.Message);long? deadline=i.Action==MilitaryAction.Accept?s.Events.Single(e=>e.Effect==MilitaryEffect.Offered).At+OfferSeconds-1:null;
            if(q.Message!=RequestMessage(i.Id)||m.Kind!="military-request"||m.CorrelationId!=i.Id.Value||m.SenderId!=w.PlayerControl.Value||m.RecipientId!=Lord.Value||m.Scope!=ContractFixture.Castle.Value||m.CreatedAt!=q.At||m.DeliverBy!=deadline||m.Body!=Text(i)||w.SealedMessages.Any(x=>x.Value==m.Id))throw new InvalidDataException("Military request differs from immutable sender/body/time");
        }
        for(int n=0;n<s.Events.Length;n++){var e=s.Events[n];if(!Enum.IsDefined(e.Effect)||e.At<0||e.At>w.ElapsedSeconds||e.Revision<1||e.Revision>w.CommitSequence||n>0&&(e.At<s.Events[n-1].At||e.Revision<s.Events[n-1].Revision))throw new InvalidDataException("Military event order differs");
            if(e.Effect==MilitaryEffect.StatusSent){ValidateStatus(w,e,n);continue;}
            var source=s.Requests.SingleOrDefault(q=>q.Message==e.Source||q.Input.Action==MilitaryAction.Pickup&&q.Input.Id.Value==e.Source);var offer=Event(w,MilitaryEffect.Offered,e.At,e.Revision-1);var permission=Event(w,MilitaryEffect.Permission,e.At,e.Revision-1);
            if(e.Effect!=MilitaryEffect.PickedUp&&(source!=null?!Received(e.Source,e.At):e.Effect!=MilitaryEffect.Expired||offer?.Message!=e.Source))throw new InvalidDataException("Military effect lacks received source");
            bool active=Career.ActiveAt(w,Lord,e.At,e.Revision-1);
            switch(e.Effect){
                case MilitaryEffect.Offered:if(source?.Input.Action!=MilitaryAction.Request||!active||!Career.WorkReportsKnown(w,out _,e.At,e.Revision-1))throw new InvalidDataException("Offer without service/application");break;
                case MilitaryEffect.Refused:if(source?.Input.Action!=MilitaryAction.Request||active&&Career.WorkReportsKnown(w,out _,e.At,e.Revision-1))throw new InvalidDataException("Refusal without application");break;
                case MilitaryEffect.Permission:if(source?.Input.Action!=MilitaryAction.Accept||offer==null||e.At>=offer.At+OfferSeconds||!active)throw new InvalidDataException("Permission without timely assent/service");break;
                case MilitaryEffect.PickedUp:if(source?.Input.Action!=MilitaryAction.Pickup||permission==null||!active||source.Revision!=e.Revision)throw new InvalidDataException("Pickup without received permission");break;
                case MilitaryEffect.Expired:if(offer==null||e.At<offer.At+OfferSeconds||permission!=null)throw new InvalidDataException("Premature expiry");break;
                case MilitaryEffect.Waiting:case MilitaryEffect.Returned:if(source?.Input.Action!=MilitaryAction.Return||!CustodyAt(w,e.At,e.Revision-1))throw new InvalidDataException("Handover without custody/request");if(e.Effect==MilitaryEffect.Returned&&(!w.Engagement!.Reports.Any(r=>r.At>=Math.Max(s.Events.Single(x=>x.Effect==MilitaryEffect.PickedUp).At,w.Supply!.Sorties.Where(t=>t.Until<=e.At).LastOrDefault()?.Until??0)&&Received(r.Message,e.At))||w.Supply!.Moves.Where(m=>m.At<=e.At).Sum(m=>m.Loading?m.Amount:-m.Amount)-w.Supply.Uses.Where(u=>u.Until<=e.At).Sum(u=>u.Consumed)!=0||w.Supply.Sorties.Any(t=>t.From<=e.At&&(t.Until==null||t.Until>e.At))||RegionalCampaign.RoadActorPointAt(w,w.PlayerControl,e.At)!=BranchRoutes.GraphNodes(w).Single(n=>n.Id=="castle").Position||s.Events.Single(x=>x.Effect==MilitaryEffect.PickedUp).At>=e.At))throw new InvalidDataException("Handover lacks received report/supply return");break;
            }
            var m=w.Messaging.Messages.Single(x=>x.Id==e.Message);var notice=new MilitaryNotice(e.Effect,e.Source,e.At,e.Effect==MilitaryEffect.Offered?e.At+OfferSeconds:null);
            if(e.Message!=NoticeMessage(e.Effect)||m.Kind!="military-notice"||m.SenderId!=Lord.Value||m.RecipientId!=w.PlayerControl.Value||m.CreatedAt!=e.At||m.Scope!=ContractFixture.Castle.Value||m.Body!=Text(notice)||m.CorrelationId!=m.Id||m.DeliverBy!=null||!w.SealedMessages.Any(x=>x.Value==m.Id))throw new InvalidDataException("Military notice differs from immutable effect");
        }
        ValidateArmy(w);
    }
}
