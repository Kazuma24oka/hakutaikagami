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

    internal static bool IsEscort(CourtAction action) => action is CourtAction.OfferRestorationEscort
        or CourtAction.AcceptRestorationEscort or CourtAction.DeclineRestorationEscort;

    internal static void CheckEscort(ContractWorld world, CourtInput input, EntityId actor, EntityId site, long at, long revision)
    {
        var history = HouseholdCourt.Events(world).Where(e => e.Revision < revision);
        var source = history.SingleOrDefault(e => e.Input.Id == input.Reference);
        if (source == null || source.Place != site || site != input.Site || input.When <= at ||
            !TransportWork.At(world, actor, site, at, revision, true) ||
            !TransportWork.At(world, input.Person, site, at, revision, true) ||
            Assassination.DeadAt(world, input.Person, at, revision))
            throw new InvalidDataException("期限内に両者が集合先で原本を確かめてください");
        if (input.Action == CourtAction.OfferRestorationEscort)
        {
            if (source.Input.Action != CourtAction.SupportRestoration || source.Actor != actor ||
                source.Input.Person != input.Person || source.Input.Owner == input.Owner ||
                input.When - at > 7 * 86400L ||
                FactionStances.ResolveAuthority(world, actor, input.Owner, at, revision - 1) == null ||
                history.Any(e => e.Input.Action == input.Action && e.Input.Reference == input.Reference && e.Input.When > at))
                throw new InvalidDataException("支持を表明した別家の代表が、一週間以内の現地警護を申し出てください");
            return;
        }
        if (source.Input.Action != CourtAction.OfferRestorationEscort || source.Input.Person != actor ||
            source.Actor != input.Person || source.Input.Owner != input.Owner || source.Input.When != input.When ||
            history.Any(e => IsEscort(e.Input.Action) && e.Input.Reference == input.Reference))
            throw new InvalidDataException("宛先本人が未回答の同一条件を確認してください");
    }

    internal static bool IsRice(CourtAction action) =>
        action is CourtAction.PledgeRestorationRice or CourtAction.HandOverRestorationRice;

    internal static long RiceTransferred(ContractWorld world, EntityId person, long at, long revision)
    {
        return HouseholdCourt.Events(world).Where(e => e.Input.Action == CourtAction.HandOverRestorationRice &&
            e.At <= at && e.Revision <= revision).Sum(e =>
                e.Actor == person ? -e.Input.Amount : e.Input.Person == person ? e.Input.Amount : 0L);
    }

    internal static void CheckRice(ContractWorld world, CourtInput input, EntityId actor, EntityId site, long at, long revision)
    {
        bool handover = input.Action == CourtAction.HandOverRestorationRice;
        var sourceAction = handover ? CourtAction.PledgeRestorationRice : CourtAction.SupportRestoration;
        var previous = HouseholdCourt.Events(world).Where(e => e.Revision < revision);
        var source = previous.SingleOrDefault(e => e.Input.Id == input.Reference && e.Input.Action == sourceAction);
        if (source == null || source.Actor != actor || source.Input.Person != input.Person ||
            source.Input.Owner != input.Owner || source.Place != site || site != input.Site ||
            !TransportWork.At(world, actor, site, at, revision, true) ||
            !TransportWork.At(world, input.Person, site, at, revision, true) ||
            Assassination.DeadAt(world, input.Person, at, revision) || input.Amount <= 0 ||
            previous.Any(e => e.Input.Action == input.Action && e.Input.Reference == input.Reference))
            throw new InvalidDataException("本人と支援相手が集まった旧領で未履行の原本と数量を確認してください");
        if (handover && (input.Amount != source.Input.Amount ||
            HouseSalary.PrivateRice(world, actor, at, revision - 1) < input.Amount))
            throw new InvalidDataException("約束した数量の私有米を持参してください");
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
        foreach (var source in history.Where(e => e.At < world.ElapsedSeconds &&
            e.Input.Action is CourtAction.SupportRestoration or CourtAction.OfferRestorationEscort))
        {
            bool proposing = source.Input.Action == CourtAction.SupportRestoration;
            var agent = proposing ? source.Actor : source.Input.Person;
            var other = proposing ? source.Input.Person : source.Actor;
            if (agent == world.PlayerControl || SocialWorld.Site(world, agent) != source.Place ||
                SocialWorld.Site(world, other) != source.Place ||
                history.Any(e => IsEscort(e.Input.Action) && e.Input.Reference == source.Input.Id))
                continue;

            EntityId sponsor;
            CourtAction action;
            long until;
            if (proposing)
            {
                var owner = DefectionPacts.HouseAt(world, agent, world.ElapsedSeconds, world.CommitSequence);
                if (owner == null || owner == source.Input.Owner ||
                    FactionStances.ResolveAuthority(world, agent, owner.Value, world.ElapsedSeconds, world.CommitSequence) == null)
                    continue;
                sponsor = owner.Value;
                action = CourtAction.OfferRestorationEscort;
                until = world.ElapsedSeconds + 7 * 86400L;
            }
            else
            {
                until = source.Input.When;
                if (world.ElapsedSeconds >= until) continue;
                sponsor = source.Input.Owner;
                var assessment = CharacterRelations.DiagnosticScores(world, agent, other);
                action = assessment.Trust > assessment.Hostility ? CourtAction.AcceptRestorationEscort : CourtAction.DeclineRestorationEscort;
            }
            var terms = new CourtInput(HouseholdCourt.Id("request", source.Input.Id.Value + "/escort"),
                action, other, sponsor, source.Place, Reference: source.Input.Id, When: until);
            TransactionalWorld.TryApply(world, copy => HouseholdCourt.Execute(copy, terms, agent));
        }
        foreach (var source in history.Where(e => e.Actor != world.PlayerControl && e.At < world.ElapsedSeconds &&
            e.Input.Action is CourtAction.SupportRestoration or CourtAction.PledgeRestorationRice))
        {
            var pledge = source.Input.Action == CourtAction.SupportRestoration;
            var action = pledge ? CourtAction.PledgeRestorationRice : CourtAction.HandOverRestorationRice;
            long amount = pledge ? 1 : source.Input.Amount;
            if (history.Any(e => e.Input.Reference == source.Input.Id && e.Input.Action == action) ||
                SocialWorld.Site(world, source.Actor) != source.Place ||
                SocialWorld.Site(world, source.Input.Person) != source.Place ||
                HouseSalary.PrivateRice(world, source.Actor) < (pledge ? 2 : amount))
                continue;
            var supply = new CourtInput(HouseholdCourt.Id("request", source.Input.Id.Value + "/rice"),
                action, source.Input.Person, source.Input.Owner, source.Place, Amount: amount, Reference: source.Input.Id);
            TransactionalWorld.TryApply(world, copy => HouseholdCourt.Execute(copy, supply, source.Actor));
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
