namespace HakutaiKagami.Core.Contracts;

public sealed record FormerSeatObservation(EntityId House, EntityId Site, long ObservedAt);

public static class HouseRestoration
{
    // 以前本人が仕えていた時に現地で見た支配と、現在地で確かめられる喪失だけを扱う。
    public static FormerSeatObservation[] ObserveFormerSeat(ContractWorld world, EntityId observer)
    {
        if (world.NewGame == null || SocialWorld.Site(world, observer) is not {} site)
            return [];

        return ObserveAt(world, observer, site, world.ElapsedSeconds, world.CommitSequence + 1);
    }

    internal static bool IsSupport(CourtAction action) => action is CourtAction.SeekRestorationSupport
        or CourtAction.SupportRestoration or CourtAction.ReserveRestoration;

    internal static void CheckSupport(ContractWorld w, CourtInput input, EntityId actor, EntityId site, long at, long revision)
    {
        if (input.Site != site || actor == input.Person ||
            !TransportWork.At(w, actor, site, at, revision, true) ||
            !TransportWork.At(w, input.Person, site, at, revision, true) ||
            !FamilyBirth.People(w).Any(p => p.Id == input.Person.Value && FamilyKin.LifeAt(w.NewGame!, p, at) == LifeState.Adult) ||
            Assassination.DeadAt(w, input.Person, at, revision))
            throw new InvalidDataException("現在地で会える成人を指定してください");
        var prior = HouseholdCourt.Events(w).Where(e => e.Revision < revision);
        if (input.Action == CourtAction.SeekRestorationSupport)
        {
            if (input.Reference != null || !ObserveAt(w, actor, site, at, revision).Any(o => o.House == input.Owner) ||
                prior.Any(e => e.Input.Action == input.Action && e.Input.Owner == input.Owner &&
                    e.Actor == actor && e.Input.Person == input.Person && e.Place == site))
                throw new InvalidDataException("本人が確認した旧領について、一度だけ相談できます");
            return;
        }
        if (!prior.Any(e => e.Input.Id == input.Reference && e.Input.Action == CourtAction.SeekRestorationSupport &&
            e.Input.Owner == input.Owner && e.Input.Person == actor && e.Actor == input.Person && e.Place == site) ||
            prior.Any(e => IsSupport(e.Input.Action) && e.Input.Reference == input.Reference))
            throw new InvalidDataException("自分に届いた未回答の相談を選んでください");
    }

    internal static void AnswerLocalRequests(ContractWorld world)
    {
        var history = HouseholdCourt.Events(world);
        foreach (var ask in history.Where(e => e.Input.Action == CourtAction.SeekRestorationSupport &&
            e.Input.Person != world.PlayerControl && e.At < world.ElapsedSeconds))
        {
            var person = ask.Input.Person;
            if (history.Any(e => e.Input.Reference == ask.Input.Id && IsSupport(e.Input.Action)) ||
                SocialWorld.Site(world, person) != ask.Place || SocialWorld.Site(world, ask.Actor) != ask.Place)
                continue;
            var supports = CharacterRelations.Enabled(world) &&
                CharacterRelations.DiagnosticScores(world, person, ask.Actor) is var view && view.Trust > view.Hostility;
            var reply = new CourtInput(HouseholdCourt.Id("request", ask.Input.Id.Value + "/support"),
                supports ? CourtAction.SupportRestoration : CourtAction.ReserveRestoration,
                ask.Actor, ask.Input.Owner, ask.Place, Reference: ask.Input.Id);
            TransactionalWorld.TryApply(world, copy => HouseholdCourt.Execute(copy, reply, person));
        }
    }

    static FormerSeatObservation[] ObserveAt(ContractWorld world, EntityId observer, EntityId site, long now, long revision)
    {
        var observations = new List<FormerSeatObservation>();
        var service = world.Memberships.Where(row => row.Subject == observer && row.Kind == "membership");
        foreach (var house in service.Select(row => row.Target).Distinct())
        {
            var rights = world.LandRights.Where(row =>
                row.Subject == house && row.Target == site && row.Kind == "control").ToArray();
            if (rights.Any(row => row.From <= now && (now < row.Until || now == row.Until && revision < row.Revision)))
                continue;

            var witnessed = rights.Any(right => service.Any(member =>
            {
                var since = Math.Max(member.From, right.From);
                var until = Math.Min(member.Until, right.Until);
                return member.Target == house && since < until && until <= now &&
                    PersonalCompanions.SiteAt(world, observer, since, revision) == site;
            }));
            if (witnessed)
                observations.Add(new(house, site, now));
        }
        return observations.OrderBy(row => row.House.Value, StringComparer.Ordinal).ToArray();
    }
}
