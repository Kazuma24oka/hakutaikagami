namespace HakutaiKagami.Core.Contracts;
public sealed record TransportWorldReceipt(EntityId Operation,long Revision);
public sealed record TransportJob(TransportHandoverState Handover,long CreatedRevision,TransportWorldReceipt[] Receipts);
public sealed record TransportWorkState(int Schema,string Policy,TransportJob[] Jobs);
public static class TransportWork
{
    public const string Policy="personal-purse100-fee20-local-custody-v1";
    public const long Amount=100,Fee=20;
    static void Need(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static EntityId Cargo(EntityId job)=>new("lot:transport-cargo/"+job.Value.Replace(':','/'));
    public static EntityId Delivered(EntityId job)=>new("lot:transport-delivered/"+job.Value.Replace(':','/'));
    public static EntityId Debt(EntityId job)=>new("debt:transport-fee/"+job.Value.Replace(':','/'));
    public static readonly EntityId Basis=NewGameWorld.Basis;
    static bool IsDebt(DebtState d)=>d.Id.Value.StartsWith("debt:transport-fee/",StringComparison.Ordinal);
    public static bool IsLot(ResourceLot l)=>l.Id.Value.StartsWith("lot:transport-",StringComparison.Ordinal);
    static bool Before(long t,long rev,long at,long cut)=>t<at||t==at&&rev<cut;
    static bool At(ContractWorld w,EntityId actor,EntityId site,long at,long revision)
    {
        if(actor==w.PlayerControl&&w.NewMilitary?.Requests.LastOrDefault(q=>Before(q.At,q.Revision,at,revision)&&q.Input.Action is NewMilitaryAction.Deploy or NewMilitaryAction.Enter)?.Input.Action==NewMilitaryAction.Deploy)return false;
        var journeys=w.Branches!.Journeys.Where(j=>j.Actor==actor&&w.Requests.Any(q=>q.Id==w.Tasks.Single(t=>t.Id==j.Task).RequestId&&Before(q.CreatedAt,q.CommitSequence,at,revision))).ToArray();
        if(journeys.Any(j=>j.Legs[0].StartAt<=at&&w.Tasks.Single(t=>t.Id==j.Task) is var t&&(t.FinishedAt==null||t.FinishedAt>at||t.FinishedAt==at&&t.Revision>=revision)))return false;
        var history=w.ForkTransportLocation();history.Branches=w.Branches with{Journeys=journeys};
        var target=BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==site).Position;
        return RegionalCampaign.RoadActorPointAt(history,actor,at)==target;
    }
    public static TransportTerms? OfferAt(ContractWorld w,EntityId id)
    {
        if(!NewGameWork.Enabled(w)||w.TransportWork?.Jobs.Any(j=>TransportHandover.Validate(j.Handover).Phase is not(HandoverPhase.Paid or HandoverPhase.Cancelled or HandoverPhase.Returned))==true)return null;
        var location=w.Locations.Single(l=>l.Actor==w.PlayerControl);if(location.Kind!=LocationKind.AtSite)return null;var site=location.Site!.Value;
        var issuer=RegionalCampaign.Nearby(w,w.PlayerControl).Where(p=>p.Actor!=w.PlayerControl&&!Jobs(w).Any(j=>j.Handover.Terms.Issuer==p.Actor&&j.Handover.Terms.Amount==WorkProgression.Amount(w,p.Actor))).FirstOrDefault(p=>{
            var sender=w.NewGame!.Start.People.Single(s=>s.Id==p.Actor.Value);
            return w.NewGame.Start.People.Any(d=>d.Id!=w.PlayerControl.Value&&d.Id!=sender.Id&&d.Site!=site.Value&&d.Faction!=null&&d.Faction==sender.Faction&&w.Characters.Any(c=>c.Id.Value==d.Id&&c.Life==LifeState.Adult));});
        // Avoid dereferencing absent opt-in state. Candidate positions come from the
        // declared initial address book, never another person's hidden live GPS.
        if(issuer==null)return null;
        var sender=w.NewGame!.Start.People.Single(p=>p.Id==issuer.Actor.Value);
        var destination=w.NewGame.Start.People.Where(p=>p.Id!=w.PlayerControl.Value&&p.Id!=sender.Id&&p.Site!=site.Value&&p.Faction!=null&&p.Faction==sender.Faction&&w.Characters.Any(c=>c.Id.Value==p.Id&&c.Life==LifeState.Adult)).OrderBy(p=>p.Site,StringComparer.Ordinal).ThenBy(p=>p.Id,StringComparer.Ordinal).FirstOrDefault();
        if(destination==null)return null;
        return new(id,issuer.Actor,w.PlayerControl,new(destination.Id),site,new(destination.Site),issuer.Actor,NewGameWork.Wallet(issuer.Actor),"money","mon",WorkProgression.Amount(w,issuer.Actor),WorkProgression.Fee(w,issuer.Actor),w.ElapsedSeconds,w.ElapsedSeconds+86400,w.ElapsedSeconds+30*86400);
    }
    public static TransportJob[] Jobs(ContractWorld w)=>w.TransportWork?.Jobs??[];
    public static long Net(ContractWorld w,EntityId lot,long at=long.MaxValue,long revision=long.MaxValue)
    {
        long n=0;foreach(var j in Jobs(w))foreach(var e in j.Handover.Entries){var t=j.Handover.Terms;long rev=j.Receipts.Single(p=>p.Operation==e.Input.Id).Revision;if(!Before(e.Input.At,rev,at,revision))continue;
            void Move(EntityId from,EntityId to,long amount){if(lot==from)n=checked(n-amount);if(lot==to)n=checked(n+amount);}
            switch(e.Input.Action){case HandoverAction.Collect:Move(t.SourceLot,Cargo(t.Id),t.Amount);break;case HandoverAction.Deliver:Move(Cargo(t.Id),Delivered(t.Id),t.Amount);break;case HandoverAction.Return:Move(Cargo(t.Id),t.SourceLot,t.Amount);break;case HandoverAction.Pay:Move(NewGameWork.Wallet(t.Issuer),NewGameWork.Wallet(t.Carrier),t.Fee);break;}}
        return n;
    }
    internal static long WalletBefore(ContractWorld w,EntityId actor,long at,long revision)
    {
        var id=NewGameWork.Wallet(actor);long n=NewGameWork.InitialCoins+Net(w,id,at,revision)+VoyageExpense.Net(w,id,at,revision);
        foreach(var p in w.WorkCycle!.Payments.Where(p=>Before(p.At,p.Revision,at,revision))){var t=w.Tasks.Single(t=>t.Id==p.Task);if(t.Issuer==actor)n-=p.Amount;if(t.Executor==actor)n+=p.Amount;}
        return n;
    }
    internal static void Apply(ContractWorld w,EntityId job,HandoverInput input)
    {
        var j=Jobs(w).Single(x=>x.Handover.Terms.Id==job);CheckInput(w,j,input,w.CommitSequence);var decision=TransportHandover.Apply(j.Handover,input);Need(!decision.Repeated,"Already committed receipt");
        w.TransportWork=w.TransportWork! with{Jobs=Jobs(w).Select(x=>x==j?x with{Handover=decision.State,Receipts=[..x.Receipts,new(input.Id,w.CommitSequence)]}:x).ToArray()};Sync(w);NewGameWork.Sync(w);
    }
    internal static void Create(ContractWorld w,TransportTerms terms)
    {
        Need(terms==OfferAt(w,terms.Id),"Transport offer changed");Need(WalletBefore(w,terms.Issuer,w.ElapsedSeconds,w.CommitSequence)>=terms.Amount,"Cargo cannot be prepared");
        w.TravelDecisions??=[];w.TrafficStops??=[];w.TransportWork??=new(1,Policy,[]);w.TransportWork=w.TransportWork with{Jobs=[..Jobs(w),new(TransportHandover.Offer(terms),w.CommitSequence,[])]};
        Sync(w);
    }
    static void CheckInput(ContractWorld w,TransportJob j,HandoverInput i,long revision)
    {
        var t=j.Handover.Terms;Need(i.At<=w.ElapsedSeconds&&At(w,i.Actor,i.Site,i.At,revision)&&At(w,i.Counterparty,i.Site,i.At,revision),"Actual local parties are required");
        Need(i.At==w.ElapsedSeconds||revision<=w.CommitSequence,"Future receipt");
        var expected=i.Action is HandoverAction.Collect or HandoverAction.Deliver or HandoverAction.Pay or HandoverAction.Return?new EntityId("transfer:transport/"+i.Id.Value.Replace(':','/')):(EntityId?)null;
        Need(i.Transaction==expected,"Unregistered physical transfer identity");
        if(i.Action==HandoverAction.Collect)Need(WalletBefore(w,t.Issuer,i.At,revision)>=t.Amount,"Insufficient cargo");
        if(i.Action==HandoverAction.Pay)Need(WalletBefore(w,t.Issuer,i.At,revision)>=t.Fee,"Unpaid transport debt remains");
    }
    internal static void Sync(ContractWorld w)
    {
        w.ResourceLots=[..w.ResourceLots.Where(l=>!IsLot(l)),..Jobs(w).SelectMany(j=>{var t=j.Handover.Terms;return new[]{new ResourceLot(Cargo(t.Id),t.Resource,t.Owner,t.Carrier,t.Source,t.Unit,Net(w,Cargo(t.Id)),0,0,t.Carrier),new ResourceLot(Delivered(t.Id),t.Resource,t.Owner,t.Recipient,t.Destination,t.Unit,Net(w,Delivered(t.Id)),0,0)};})];
        w.Debts=[..w.Debts.Where(d=>!IsDebt(d)),..Jobs(w).Where(j=>j.Handover.Entries.Any(e=>e.Input.Action==HandoverAction.Report)).Select(j=>new DebtState(Debt(j.Handover.Terms.Id),j.Handover.Terms.Issuer,j.Handover.Terms.Carrier,"mon",TransportHandover.Validate(j.Handover).FeeOwed,Basis))];
    }
    internal static void Rewind(ContractWorld w,long at,long revision)
    {
        if(w.TransportWork==null)return;
        var jobs=Jobs(w).Where(j=>j.Handover.Terms.OfferedAt<at||j.Handover.Terms.OfferedAt==at&&j.CreatedRevision<=revision).Select(j=>{
            var receipts=j.Receipts.Where(p=>p.Revision<=revision&&j.Handover.Entries.Any(e=>e.Input.Id==p.Operation&&e.Input.At<=at)).ToArray();var ids=receipts.Select(p=>p.Operation).ToHashSet();return j with{Receipts=receipts,Handover=j.Handover with{Entries=j.Handover.Entries.Where(e=>ids.Contains(e.Input.Id)).ToArray()}};}).ToArray();
        w.TransportWork=jobs.Length==0?null:w.TransportWork with{Jobs=jobs};if(jobs.Length==0&&!VoyageExpense.Enabled(w)){w.TravelDecisions=null;w.TrafficStops=null;}Sync(w);
    }
    public static void Validate(ContractWorld w)
    {
        if(w.TransportWork==null){Need(!w.ResourceLots.Any(IsLot)&&!w.Debts.Any(d=>IsDebt(d)),"Orphan transport stock");return;}
        var s=w.TransportWork;Need(NewGameWork.Enabled(w)&&s.Schema==1&&s.Policy==Policy&&s.Jobs.Length>0,"Unexpected transport policy");Need(s.Jobs.Select(j=>j.Handover.Terms.Id).Distinct().Count()==s.Jobs.Length,"Duplicate transport job");
        Need(s.Jobs.Select(j=>(j.Handover.Terms.Issuer,j.Handover.Terms.Amount)).Distinct().Count()==s.Jobs.Length,"Duplicate local dispatch demand");
        var operations=new HashSet<EntityId>();var revisions=new HashSet<long>();
        foreach(var j in s.Jobs){var t=j.Handover.Terms;TransportHandover.Validate(j.Handover);Need(t.Carrier==w.PlayerControl&&t.SourceLot==NewGameWork.Wallet(t.Issuer)&&t.Amount==WorkProgression.Amount(w,t.Issuer,t.OfferedAt,j.CreatedRevision)&&t.Fee==WorkProgression.Fee(w,t.Issuer,t.OfferedAt,j.CreatedRevision)&&t.Unit=="mon"&&t.Resource=="money"&&t.AcceptBy==t.OfferedAt+86400&&t.DeliverBy==t.OfferedAt+30*86400&&j.CreatedRevision>0&&j.CreatedRevision<=w.CommitSequence&&t.OfferedAt<=w.ElapsedSeconds,"Unregistered transport terms");Need(At(w,t.Issuer,t.Source,t.OfferedAt,j.CreatedRevision)&&At(w,t.Carrier,t.Source,t.OfferedAt,j.CreatedRevision),"Offer was not local");
            Need(w.NewGame!.Start.People.Any(p=>p.Id==t.Recipient.Value&&p.Site==t.Destination.Value&&p.Faction!=null&&p.Faction==w.NewGame.Start.People.Single(p=>p.Id==t.Issuer.Value).Faction),"Recipient lacks original appointment address");
            Need(revisions.Add(j.CreatedRevision)&&j.Receipts.Length==j.Handover.Entries.Length,"Invalid receipt timeline");long previous=j.CreatedRevision;
            foreach(var e in j.Handover.Entries){var p=j.Receipts.Single(p=>p.Operation==e.Input.Id);Need(operations.Add(e.Input.Id)&&revisions.Add(p.Revision)&&p.Revision>previous&&p.Revision<=w.CommitSequence,"Duplicate or reversed operation sequence");previous=p.Revision;CheckInput(w,j,e.Input,p.Revision);}
        }
        var copy=ContractJson.Copy(w);Sync(copy);Need(ContractJson.CanonicalHash(w.ResourceLots.Where(IsLot).ToArray())==ContractJson.CanonicalHash(copy.ResourceLots.Where(IsLot).ToArray())&&ContractJson.CanonicalHash(w.Debts.Where(d=>IsDebt(d)).ToArray())==ContractJson.CanonicalHash(copy.Debts.Where(d=>IsDebt(d)).ToArray()),"Transport stock/debt differs from receipts");
    }
}
