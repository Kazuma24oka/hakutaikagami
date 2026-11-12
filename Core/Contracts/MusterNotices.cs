namespace HakutaiKagami.Core.Contracts;

public static class MusterNotices
{
    public const string AssemblyRoute = "received-muster-personal-land-v1";

    static EntityId TravelId(EntityId order) => HouseholdCourt.Id("march",order.Value);

    internal static bool IsJourney(ContractWorld w, Intent intent)
    {
        if (intent.RoutePolicy != AssemblyRoute || intent.Action != ActionKind.Move ||
            intent.Actor == w.PlayerControl || intent.Executor != intent.Actor) return false;
        long revision = w.Requests.SingleOrDefault(r => r.Id == intent.RequestId)?.CommitSequence ?? w.CommitSequence+1;
        return Reservations(w,intent.Actor,intent.EffectiveAt,revision).Any(e => TravelId(e.Input.Id)==intent.RequestId &&
            e.Input.Site==intent.DestinationSite && DefectionPacts.HouseAt(w,intent.Actor,intent.EffectiveAt,revision-1)==e.Input.Owner);
    }

    internal static void ValidateDeparture(ContractWorld w, Intent intent)
    {
        if (!IsJourney(w,intent) || intent.DestinationSite is not {} site ||
            NpcArmies.CommanderBusyAt(w,intent.Actor,w.ElapsedSeconds,w.CommitSequence) ||
            intent != TravelWorld.ActorMove(w,intent.RequestId.Value,intent.Actor,NewGameWorld.Node(site.Value),true,AssemblyRoute))
            throw new InvalidDataException("本人が受領した陣触れの集合地と現在の経路・任務を確認してください");
    }

    internal static bool Handles(CourtAction action) => action is CourtAction.SendMusterNotice or CourtAction.AttendMuster;

    public static CourtEvent[] Reservations(ContractWorld w, EntityId recipient, long at, long rev)
    {using var _ccRev=ControlContext.At(rev);
        var events = HouseholdCourt.Events(w);
        return events.Where(e => e.Input.Action == CourtAction.SendMusterNotice && e.Input.Person == recipient &&
            e.Revision < rev && e.At <= at && HouseholdCourt.Received(w, e.Input.Id, recipient, at, rev) &&
            !events.Any(r => r.Input.Action == CourtAction.AttendMuster && r.Input.Reference == e.Input.Id &&
                r.Revision < rev && r.At <= at)).ToArray();
    }

    internal static void Assemble(ContractWorld w)
    {
        var orders = HouseholdCourt.Events(w).Where(e => e.Input.Action == CourtAction.SendMusterNotice &&
            e.Input.Person != w.PlayerControl).ToArray();
        foreach (var order in orders)
        {
            var person = order.Input.Person;
            if (SocialWorld.Site(w, person) is not {} currentSite ||
                !Reservations(w, person, w.ElapsedSeconds, w.CommitSequence+1).Any(r => r.Input.Id == order.Input.Id) ||
                HostageCustody.HeldAt(w, person, w.ElapsedSeconds, w.CommitSequence) ||
                Assassination.DeadAt(w, person, w.ElapsedSeconds, w.CommitSequence)) continue;
            if (w.ElapsedSeconds < order.Input.When)
            {
                Prepare(w, order, person, currentSite);
                continue;
            }
            if (currentSite != order.Input.Site) continue;
            long count = Math.Min(order.Input.Amount, PrivateRecruitment.Soldiers(w, person, order.Input.Site, w.ElapsedSeconds, w.CommitSequence+1));
            var arrival = new CourtInput(HouseholdCourt.Id("arrival", order.Input.Id.Value), CourtAction.AttendMuster,
                person, order.Input.Owner, order.Input.Site, Amount: count, Reference: order.Input.Id);
            TransactionalWorld.TryApply(w, copy => HouseholdCourt.Execute(copy, arrival, person));
        }
    }

    static void Prepare(ContractWorld w, CourtEvent order, EntityId person, EntityId site)
    {
        long at = w.ElapsedSeconds, rev = w.CommitSequence+1;
        long days = (order.Input.When-at)/86400+1;
        if (days > 30 || !MilitaryEconomy.Enabled(w) || !CommunityEconomy.SitesFor(w).Contains(site) ||
            DefectionPacts.HouseAt(w, person, at, rev-1) != order.Input.Owner) return;
        long stillNeeded = order.Input.Amount-PrivateRecruitment.Strength(w,person,order.Input.When,rev);
        long affordable = Math.Min(stillNeeded, HouseSalary.PrivateRice(w,person)/days);
        if (affordable <= 0) return;
        CourtAction action;
        if (PrivateRecruitment.CanLevy(w,person,order.Input.Owner,site,at,rev))
        {
            var village = CommunityEconomy.AccountAt(w,site,at,rev);
            affordable=Math.Min(affordable,village.Workforce-village.Reserved);
            action=CourtAction.RaiseVillageSoldiers;
        }
        else
        {
            if (!MaterialMarkets.Enabled(w) || !MaterialMarkets.Sites(w).Contains(site)) return;
            affordable=Math.Min(affordable,PrivateRecruitment.FreeHires(w,site,at,rev));
            affordable=Math.Min(affordable,TransportWork.WalletBefore(w,person,at,rev)/(PrivateRecruitment.DailyWage*days));
            action=CourtAction.HireMarketSoldiers;
        }
        if (affordable <= 0) return;
        var input=new CourtInput(HouseholdCourt.Id("muster-recruit",order.Input.Id.Value+"/"+rev),action,
            person,order.Input.Owner,site,Amount:affordable,When:checked(at+days*86400));
        TransactionalWorld.TryApply(w,copy=>HouseholdCourt.Execute(copy,input,person));
    }

    public static CourtEvent? Conflict(ContractWorld w, EntityId actor, long earliestFinish) =>
        Reservations(w,actor,w.ElapsedSeconds,w.CommitSequence+1).Where(e=>e.Input.When<earliestFinish)
            .OrderBy(e=>e.Input.When).FirstOrDefault();

    internal static void Check(ContractWorld w, CourtInput i, EntityId actor, EntityId place, long at, long rev)
    {using var _ccRev=ControlContext.At(rev);
        if (i.Action == CourtAction.SendMusterNotice)
        {
            if (actor == i.Person || i.Reference != null || i.Amount is < 1 or > 100 || i.When <= at ||
                HouseSuccession.Head(w, i.Owner, at, rev-1) != actor ||
                DefectionPacts.HouseAt(w, i.Person, at, rev-1) != i.Owner ||
                !w.LandRights.Any(r => r.Kind == "control" && r.Subject == i.Owner && r.Target == i.Site &&
                    r.From <= at && at < r.Until && (r.From < at || r.Revision < rev)))
                throw new InvalidDataException("当主から家臣へ、支配する集合地・将来の期日・軍役の人数を指定してください");
            if (HouseholdCourt.Events(w).Any(e => e.Input.Action == i.Action && e.Input.Owner == i.Owner &&
                e.Input.Person == i.Person && e.Input.When == i.When && e.Revision < rev))
                throw new InvalidDataException("同じ家臣・期日の陣触れは発令済みです");
            return;
        }
        var order = Reservations(w, actor, at, rev).SingleOrDefault(e => e.Input.Id == i.Reference);
        if (order == null || order.Input.Owner != i.Owner || i.Person != actor || order.Input.Site != place || i.Site != place ||
            at < order.Input.When || !TransportWork.At(w, actor, place, at, rev, true) ||
            DefectionPacts.HouseAt(w,actor,at,rev-1) != i.Owner ||
            i.Amount != Math.Min(order.Input.Amount, PrivateRecruitment.Soldiers(w,actor,place,at,rev)))
            throw new InvalidDataException("本人宛ての陣触れを受け取り、集合期日以降に現地の自前の兵を確認してください");
    }
}

public sealed partial class WorldSession
{
    internal void StartMusterJourneys()
    {
        if (!HouseholdCourt.Events(state).Any(e=>e.Input.Action==CourtAction.SendMusterNotice)) return;
        foreach (var order in HouseholdCourt.Events(state).Where(e=>e.Input.Action==CourtAction.SendMusterNotice))
        {
            var actor=order.Input.Person;
            if (actor==state.PlayerControl || SocialWorld.Site(state,actor) is not {} place || place==order.Input.Site ||
                !MusterNotices.Reservations(state,actor,state.ElapsedSeconds,state.CommitSequence+1).Contains(order) ||
                HostageCustody.HeldAt(state,actor,state.ElapsedSeconds,state.CommitSequence) ||
                Assassination.DeadAt(state,actor,state.ElapsedSeconds,state.CommitSequence)) continue;
            var id=HouseholdCourt.Id("march",order.Input.Id.Value);
            if (state.Requests.Any(r=>r.Id==id) || state.Tasks.Any(t=>t.Executor==actor &&
                t.Execution is not (ExecutionPhase.Completed or ExecutionPhase.Failed or ExecutionPhase.Stopped))) continue;
            try
            {
                var intent=TravelWorld.ActorMove(state,id.Value,actor,NewGameWorld.Node(order.Input.Site.Value),true,MusterNotices.AssemblyRoute);
                Submit(intent,state.CommitSequence);
            }
            catch (InvalidDataException) { /* 現在の既知道では出発できない。次の判断で再確認する。 */ }
        }
    }
}
