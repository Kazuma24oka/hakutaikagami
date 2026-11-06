namespace HakutaiKagami.Core.Contracts;

public static class RestorationEscort
{
    internal static EntityId MusterId(EntityId agreement) => HouseholdCourt.Id("decision", "restoration/" + agreement.Value);

    public static void Muster(ContractWorld world, EntityId agreementId)
    {
        var agreement = HouseholdCourt.Events(world).SingleOrDefault(e => e.Input.Id == agreementId &&
            e.Input.Action == CourtAction.AcceptRestorationEscort);
        if (agreement == null || world.ElapsedSeconds >= agreement.Input.When)
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
        var snapshot = session.DiagnosticSnapshot();
        foreach (var agreement in HouseholdCourt.Events(snapshot).Where(e => e.Input.Action == CourtAction.AcceptRestorationEscort))
        {
            var world = session.DiagnosticSnapshot();
            var commander = agreement.Input.Person;
            if (commander == world.PlayerControl ||
                world.Tasks.Any(t => t.Executor == commander && t.Execution is not
                    (ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed))) continue;
            var id = MusterId(agreement.Input.Id);
            var muster = NpcArmies.Musters(world).SingleOrDefault(m => m.Id == id);
            var location = world.Locations.SingleOrDefault(l => l.Actor == commander && l.Kind == LocationKind.AtSite)?.Site;
            if (location == null) continue;
            if (muster == null)
            {
                var authority = FactionStances.ResolveAuthority(world, commander, agreement.Input.Owner, world.ElapsedSeconds, world.CommitSequence);
                if (world.ElapsedSeconds < agreement.Input.When && authority?.Home == location &&
                    !NpcArmies.HoldsGarrison(world, agreement.Input.Owner, location.Value))
                    session.TryMusterRestorationEscort(agreement.Input.Id);
                continue;
            }
            if (world.NpcArmies!.Returns.Any(r => r.Muster == id)) continue;
            bool recall = world.ElapsedSeconds >= agreement.Input.When ||
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
