namespace HakutaiKagami.Core.Contracts;

public static class RestorationEscort
{
    public const string HomeRoute = "restoration-escort-gather-at-home-v1";

    static EntityId HomeJourneyId(EntityId agreement) => HouseholdCourt.Id("request", agreement.Value + "/to-garrison");

    static CourtEvent? HomeAgreement(ContractWorld world, Intent intent)
    {
        if (intent.RoutePolicy != HomeRoute || intent.Action != ActionKind.Move ||
            intent.Actor != intent.Executor || intent.Actor == world.PlayerControl) return null;
        return HouseholdCourt.Events(world).SingleOrDefault(e => e.Input.Action == CourtAction.AcceptRestorationEscort &&
            e.Input.Person == intent.Actor && e.At <= intent.EffectiveAt && intent.EffectiveAt < e.Input.When &&
            HomeJourneyId(e.Input.Id) == intent.RequestId);
    }

    public static bool IsHomeJourney(ContractWorld world, Intent intent)
    {
        var agreement = HomeAgreement(world, intent);
        if (agreement == null) return false;
        var request = world.Requests.SingleOrDefault(r => r.Id == intent.RequestId);
        var revision = request == null ? world.CommitSequence : request.CommitSequence - 1;
        var authority = FactionStances.ResolveAuthority(world, intent.Actor, agreement.Input.Owner, intent.EffectiveAt, revision);
        return authority != null && agreement.Revision <= revision && !HouseRestoration.EscortReleased(world, agreement.Input.Id, revision + 1) && intent.DestinationSite == authority.Home;
    }

    public static Intent PlanHomeJourney(ContractWorld world, CourtEvent agreement)
    {
        var authority = FactionStances.ResolveAuthority(world, agreement.Input.Person, agreement.Input.Owner,
            world.ElapsedSeconds, world.CommitSequence) ?? throw new InvalidDataException("後援家の代表権がありません");
        var intent = TravelWorld.ActorMove(world, HomeJourneyId(agreement.Input.Id).Value, agreement.Input.Person,
            NewGameWorld.Node(authority.Home.Value), true, HomeRoute);
        ValidateDeparture(world, intent);
        return intent;
    }

    internal static void ValidateDeparture(ContractWorld world, Intent intent)
    {
        if (!IsHomeJourney(world, intent) || intent.DestinationSite is not {} home ||
            NpcArmies.CommanderBusyAt(world, intent.Actor, world.ElapsedSeconds, world.CommitSequence) ||
            intent != TravelWorld.ActorMove(world, intent.RequestId.Value, intent.Actor, NewGameWorld.Node(home.Value), true, HomeRoute))
            throw new InvalidDataException("警護の合意と当主の帰郷経路が一致しません");
    }

    internal static EntityId MusterId(EntityId agreement) => HouseholdCourt.Id("decision", "restoration/" + agreement.Value);

    public static void Muster(ContractWorld world, EntityId agreementId)
    {
        var agreement = HouseholdCourt.Events(world).SingleOrDefault(e => e.Input.Id == agreementId &&
            e.Input.Action == CourtAction.AcceptRestorationEscort);
        if (agreement == null || world.ElapsedSeconds >= agreement.Input.When ||
            HouseRestoration.EscortReleased(world, agreementId, world.CommitSequence + 1))
            throw new InvalidDataException("期限内の警護合意が必要です");
        NpcArmies.Muster(world, MusterId(agreementId), agreement.Input.Person, agreement.Input.Owner);
    }

    public static int WitnessedAtAssembly(ContractWorld world, EntityId observer, CourtEvent agreement)
    {
        if (agreement.Input.Action != CourtAction.AcceptRestorationEscort ||
            (agreement.Actor != observer && agreement.Input.Person != observer) ||
            SocialWorld.Site(world, observer) != agreement.Place) return 0;
        var muster = NpcArmies.Musters(world).SingleOrDefault(m => m.Id == MusterId(agreement.Input.Id));
        if (muster == null || !world.Locations.Any(l => l.Actor == muster.Commander &&
            l.Kind == LocationKind.AtSite && l.Site == agreement.Place)) return 0;
        return NpcArmies.Sights(world, observer).SingleOrDefault(s => s.Commander == muster.Commander &&
            s.Owner == muster.Owner)?.Soldiers ?? 0;
    }

    // 署名のある原本から後援家の駐屯軍を一意に引く。別の援軍や攻撃軍には指示しない。
    internal static void TakeTurns(WorldSession session)
    {
        if (!session.HasRestorationEscort()) return;
        var snapshot = session.TurnSnapshot();
        foreach (var agreement in HouseholdCourt.Events(snapshot).Where(e => e.Input.Action == CourtAction.AcceptRestorationEscort))
        {
            var world = session.TurnSnapshot();
            var commander = agreement.Input.Person;
            if (commander == world.PlayerControl ||
                world.Tasks.Any(t => t.Executor == commander && t.Execution is not
                    (ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed))) continue;
            var id = MusterId(agreement.Input.Id);
            var muster = NpcArmies.Musters(world).SingleOrDefault(m => m.Id == id);
            var location = world.Locations.SingleOrDefault(l => l.Actor == commander && l.Kind == LocationKind.AtSite)?.Site;
            if (location == null) continue;
            bool released = HouseRestoration.EscortReleased(world, agreement.Input.Id, world.CommitSequence + 1);
            if (muster == null)
            {
                var authority = FactionStances.ResolveAuthority(world, commander, agreement.Input.Owner, world.ElapsedSeconds, world.CommitSequence);
                if (released || world.ElapsedSeconds >= agreement.Input.When || authority == null ||
                    NpcArmies.CommanderBusyAt(world, commander, world.ElapsedSeconds, world.CommitSequence) ||
                    NpcArmies.HoldsGarrison(world, agreement.Input.Owner, authority.Home)) continue;
                if (authority.Home == location)
                    session.TryMusterRestorationEscort(agreement.Input.Id);
                else
                {
                    var journeyId = HomeJourneyId(agreement.Input.Id);
                    if (world.Requests.Any(r => r.Id == journeyId)) continue;
                    try
                    {
                        var journey = PlanHomeJourney(world, agreement);
                        session.Submit(journey, session.Revision);
                    }
                    catch (InvalidDataException) { /* 既知の陸路が開くまで待つ。 */ }
                }
                continue;
            }
            if (world.NpcArmies!.Returns.Any(r => r.Muster == id)) continue;
            bool recall = released || world.ElapsedSeconds >= agreement.Input.When ||
                NpcArmies.Consumed(world, muster) >= NpcArmies.CarryCapacity(world, muster) ||
                FactionStances.ResolveAuthority(world, commander, muster.Owner, world.ElapsedSeconds, world.CommitSequence) == null;
            var destination = recall ? muster.Site : agreement.Place;
            if (location == destination)
            {
                if (recall) session.ReturnNpcArmy(id);
                continue;
            }
            var tripId = HouseholdCourt.Id("request", agreement.Input.Id.Value + (recall ? "/return" : "/guard"));
            if (world.Requests.Any(r => r.Id == tripId)) continue;
            try
            {
                var trip = NpcArmies.Move(world, id, tripId.Value, NewGameWorld.Node(destination.Value));
                session.Submit(trip, session.Revision);
            }
            catch (InvalidDataException) { /* 現在の既知道路で経路を作れない場合は現地待機。 */ }
        }
    }
}

public sealed partial class WorldSession
{
    internal bool HasRestorationEscort()
    {
        lock (gate) return NpcArmies.Enabled(state) && HouseholdCourt.Events(state)
            .Any(e => e.Input.Action == CourtAction.AcceptRestorationEscort);
    }

    internal bool TryMusterRestorationEscort(EntityId agreementId)
    {
        lock (gate)
        {
            try
            {
                Change(w => RestorationEscort.Muster(w, agreementId));
                return true;
            }
            catch (InvalidDataException) { return false; }
        }
    }
}
