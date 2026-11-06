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

    static SetupScenario? BeforePlacement(ContractWorld world) =>
        world.NewGame is {} start && start.Registered.Any(r => r.Role == StartTemplate.Daimyo)
            ? NewGameWorld.Scenario(start.HistoricalCatalog) : null;

    internal static bool NamesFormerHouse(ContractWorld world, CourtInput input)
    {
        if (input.Action is not (CourtAction.SeekRestorationSupport or CourtAction.SupportRestoration or
            CourtAction.ReserveRestoration or CourtAction.PledgeRestorationRice or CourtAction.HandOverRestorationRice or
            CourtAction.RecognizeHouseContinuity or CourtAction.RecordRecoveredSeat)) return false;
        return BeforePlacement(world)?.Factions.Any(f => f.Id == input.Owner.Value) == true &&
            !world.NewGame!.Start.Factions.Any(f => f.Id == input.Owner.Value);
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

    public static EntityId? SuccessorOrganization(ContractWorld world, EntityId original, long at, long revision)
    {
        var recognition = HouseholdCourt.Events(world).LastOrDefault(e => e.Input.Action == CourtAction.RecognizeHouseContinuity &&
            e.Input.Owner == original && e.At <= at && e.Revision < revision);
        return recognition == null ? null : world.Declarations?.Single(d => d.Pact == recognition.Input.Reference).House;
    }

    public static bool RepresentsFoundation(ContractWorld world, IndependenceDeclaration declaration, EntityId actor, long at, long revision)
    {
        if (declaration.Revision > revision || declaration.At > at ||
            Assassination.DeadAt(world, actor, at, revision) || HostageCustody.HeldAt(world, actor, at, revision) ||
            HouseSuccession.Head(world, declaration.House, at, revision) != actor ||
            DefectionPacts.HouseAt(world, actor, at, revision) != declaration.House) return false;
        // 独立時の創設原本は従来の当主任命状と別。実際に引き渡された拠点だけを対象とする。
        return world.LandRights.Any(r => r.Subject == declaration.House && r.Kind == "control" &&
            r.Target == declaration.Site && r.From <= at && (r.From < at || r.Revision <= revision) &&
            (at < r.Until || at == r.Until && revision < r.Revision)) &&
            !CastleAdministration.Blocked(world, declaration.Site, at, revision);
    }

    public static IEnumerable<CourtInput> ContinuityApplications(ContractWorld world, EntityId actor)
    {
        if (SocialWorld.Site(world, actor) is not {} site) yield break;
        var houses = HouseholdCourt.Events(world).Where(e => e.Input.Action == CourtAction.SeekRestorationSupport &&
            e.Actor == actor && e.Place == site).Select(e => e.Input.Owner).Distinct();
        foreach (var house in houses)
        foreach (var declaration in (world.Declarations ?? []).Where(d => d.Site == site && d.House != house))
        {
            var id = HouseholdCourt.Id("request", $"restoration-recognition/{house.Value}/{declaration.Pact.Value}");
            var input = new CourtInput(id, CourtAction.RecognizeHouseContinuity, actor, house, site, Reference: declaration.Pact);
            if (CanRecognize(world, input, actor, site, world.ElapsedSeconds, world.CommitSequence + 1))
                yield return input;
        }
    }

    internal static void CheckContinuity(ContractWorld world, CourtInput input, EntityId actor, EntityId place, long at, long revision)
    {
        if (!CanRecognize(world, input, actor, place, at, revision))
            throw new InvalidDataException("旧家の当主・継承記録と現地の新しい家の代表権を同じ人物について照合してください");
    }

    static bool CanRecognize(ContractWorld world, CourtInput input, EntityId actor, EntityId place, long at, long revision)
    {
        var declaration = world.Declarations?.SingleOrDefault(d => d.Pact == input.Reference && d.At <= at && d.Revision < revision);
        if (declaration == null || declaration.House == input.Owner || declaration.Site != place || input.Site != place ||
            actor != input.Person || !TransportWork.At(world, actor, place, at, revision, true) ||
            (NamesFormerHouse(world, input) ? BeforePlacement(world)!.Factions.Single(f => f.Id == input.Owner.Value).Head != actor.Value :
                HouseSuccession.Head(world, input.Owner, at, revision - 1) != actor) ||
            !RepresentsFoundation(world, declaration, actor, at, revision - 1) ||
            world.LandRights.Any(r => r.Subject == input.Owner && r.Kind == "control" && r.From <= at && (r.From < at || r.Until != long.MaxValue || r.Revision < revision) &&
                (at < r.Until || at == r.Until && revision < r.Revision)) ||
            !HouseholdCourt.Events(world).Any(e => e.Input.Action == CourtAction.SeekRestorationSupport &&
                e.Actor == actor && e.Input.Owner == input.Owner && e.Place == place && e.Revision < revision) ||
            HouseholdCourt.Events(world).Any(e => e.Input.Action == CourtAction.RecognizeHouseContinuity && e.Revision < revision &&
                (e.Input.Owner == input.Owner || e.Input.Reference == input.Reference)))
            return false;
        return true;
    }

    // 本丸を取った最新の原本と、今そこを守る実兵を両方照合する。
    public static EntityId? SecuredKeep(ContractWorld world, EntityId observer, EntityId house, EntityId site)
    {
        if (SocialWorld.Site(world, observer) != site) return null;
        return OccupationSource(world, SuccessorOrganization(world, house, world.ElapsedSeconds, world.CommitSequence + 1) ?? house, site, world.ElapsedSeconds, world.CommitSequence + 1);
    }

    static EntityId? OccupationSource(ContractWorld world, EntityId house, EntityId site, long at, long revision)
    {
        var handovers = CastleHandover.Jobs(world, at, revision)
            .Where(j => j.Terms.Site == site && j.Occupied != null)
            .Select(j => (j.Occupied!.Revision, Owner: j.Terms.Occupier, Defender: j.Terms.Owner, j.Occupied.Input.Id));
        var captures = CastleSiege.Orders(world).Where(o => CastleSiege.Before(o, at, revision) &&
            o.Input.Site == site && o.Outcome == "本丸を占有")
            .Select(o => (o.Revision, Owner: o.Attacker, o.Defender, o.Input.Id));
        var latest = handovers.Concat(captures).OrderByDescending(o => o.Revision).FirstOrDefault();
        if (latest.Owner != house || CastleHandover.Forces(world, house, site, at, revision) <= 0 ||
            CastleHandover.Forces(world, latest.Defender, site, at, revision) > 0) return null;
        return latest.Id;
    }

    public static long UnpaidSalary(SalaryTerm original, long at) =>
        checked(HouseSalary.Due(original, at) - original.Paid);

    internal static bool CanUseRestoredStore(ContractWorld world, EntityId original, EntityId oldHouse,
        EntityId payer, EntityId employee, EntityId site, long at, long revision, long requiredRice = 0)
    {
        var successor = SuccessorOrganization(world, oldHouse, at, revision);
        if (successor == null || RegionalDomains.Owner(world, site) != oldHouse ||
            CastleAdministration.Owner(world, site, at, revision) != successor) return false;
        var foundation = world.Declarations?.SingleOrDefault(d => d.House == successor);
        return foundation != null && foundation.Site == site &&
            RepresentsFoundation(world, foundation, payer, at, revision - 1) &&
            HouseholdCourt.Events(world).Any(e => e.Input.Action == CourtAction.PresentRestorationArrears &&
                e.Input.Reference == original && e.Input.Owner == oldHouse && e.Actor == employee &&
                e.Input.Person == payer && e.At <= at && e.Revision < revision &&
                RemainingClaim(world, e, revision) >= requiredRice);
    }

    private static long RemainingClaim(ContractWorld world, CourtEvent claim, long beforeRevision)
    {
        var entries = world.Salary!.Entries;
        var paid = entries.Where(e => e.Revision < beforeRevision &&
            (e.At > claim.At || e.At == claim.At && e.Revision > claim.Revision) &&
            e.Input.Contract == claim.Input.Reference && e.Input.Action == SalaryAction.Receive)
            .Sum(e => entries.Single(p => p.Input.Id == e.Input.Preparation).Input.Amount);
        return claim.Input.Amount - paid;
    }

    public static SalaryInput? SettlementForClaim(ContractWorld world, EntityId claimId)
    {
        var claim = HouseholdCourt.Events(world).SingleOrDefault(e => e.Input.Id == claimId &&
            e.Input.Action == CourtAction.PresentRestorationArrears);
        var term = world.Salary?.Terms.SingleOrDefault(t => t.Id == claim?.Input.Reference);
        if (claim == null || term == null || term.Owner != claim.Input.Owner || term.Employee != claim.Actor ||
            (world.PlayerControl != claim.Actor && world.PlayerControl != claim.Input.Person)) return null;
        long at = world.ElapsedSeconds, revision = world.CommitSequence;
        var payer = claim.Input.Person;
        var authorized = !HouseSuccession.Enabled(world) || HouseSuccession.Head(world, term.Owner, at, revision) == term.Employer
            ? term.Employer : HouseSuccession.Acknowledged(world, term.Id, term.Owner, at, revision + 1)?.Representative;
        if (authorized != payer || SocialWorld.Site(world, payer) != term.Site ||
            SocialWorld.Site(world, term.Employee) != term.Site) return null;
        var originalStore = FactionStances.ResolveAuthority(world, payer, term.Owner, at, revision) != null &&
            world.LandRights.Any(r => r.Subject == term.Owner && r.Target == term.Site && r.Kind == "control" &&
                r.From <= at && at < r.Until);
        if (!originalStore && !CanUseRestoredStore(world, term.Id, term.Owner, payer, term.Employee, term.Site, at, revision + 1))
            return null;
        long outstanding = Math.Min(UnpaidSalary(term, at), RemainingClaim(world, claim, revision + 1));
        if (outstanding <= 0) return null;
        var prepared = HouseSalary.Prepared(world.Salary!.Entries, term.Id, at, revision + 1);
        var other = term.Employer == world.PlayerControl ? term.Employee : term.Employer;
        var id = HouseholdCourt.Id("request", $"settle/{claimId.Value}/{revision}");
        if (prepared != null && prepared.Input.Amount > outstanding) return null;
        if (prepared != null)
            return new(id, SalaryAction.Receive, other, term.Site, term.Id, Preparation: prepared.Input.Id);
        long available = CommunityEconomy.AccountAt(world, term.Site, at, revision + 1).HouseRice -
            HouseSalary.Reserved(world, term.Site, at, revision + 1);
        long amount = Math.Min(available, outstanding);
        return amount > 0 ? new(id, SalaryAction.Prepare, other, term.Site, term.Id, amount) : null;
    }

    internal static void CheckArrears(ContractWorld world, CourtInput input, EntityId creditor, EntityId place, long at, long revision)
    {
        var prior = HouseholdCourt.Events(world).Where(e => e.Revision < revision);
        if (input.Site != place || input.Reference == null || input.Person == creditor ||
            !TransportWork.At(world, creditor, place, at, revision, true) ||
            !TransportWork.At(world, input.Person, place, at, revision, true) ||
            Assassination.DeadAt(world, input.Person, at, revision) ||
            !prior.Any(e => e.Input.Action == CourtAction.SeekRestorationSupport && e.Input.Owner == input.Owner &&
                e.Actor == input.Person && e.Input.Person == creditor))
            throw new InvalidDataException("再興について直接相談を受けた相手に、現地で原本を示してください");
        var term = HouseSalary.SnapshotAt(world, at, revision - 1).Terms.SingleOrDefault(t => t.Id == input.Reference);
        if (term == null || term.Employee != creditor || term.Owner != input.Owner ||
            input.Amount <= 0 || input.Amount != UnpaidSalary(term, at) ||
            prior.Any(e => e.Input.Action == input.Action && e.Input.Reference == input.Reference &&
                e.Actor == creditor && e.Input.Person == input.Person && e.Input.Amount == input.Amount))
            throw new InvalidDataException("自分の給付原本について、既に受け取った米を引いた残額を確認してください");
    }

    internal static void CheckRecoveredSeat(ContractWorld world, CourtInput input, EntityId actor, EntityId place, long at, long revision)
    {
        var past = HouseholdCourt.Events(world).Where(e => e.Revision < revision);
        if (input.Site != place || input.Person != actor || input.Reference == null ||
            !TransportWork.At(world, actor, place, at, revision, true) ||
            !past.Any(e => e.Input.Action == CourtAction.SeekRestorationSupport && e.Actor == actor &&
                e.Input.Owner == input.Owner && e.Place == place) ||
            past.Any(e => e.Input.Action == input.Action && e.Actor == actor && e.Input.Reference == input.Reference) ||
            OccupationSource(world, SuccessorOrganization(world, input.Owner, at, revision) ?? input.Owner, place, at, revision) != input.Reference)
            throw new InvalidDataException("旧領の本丸引渡しと現地の守備を、再興を呼びかけた本人が確認してください");
    }

    internal static bool IsEscort(CourtAction action) => action is CourtAction.OfferRestorationEscort
        or CourtAction.AcceptRestorationEscort or CourtAction.DeclineRestorationEscort or CourtAction.ReleaseRestorationEscort;

    internal static void CheckEscort(ContractWorld world, CourtInput input, EntityId actor, EntityId site, long at, long revision)
    {
        var history = HouseholdCourt.Events(world).Where(e => e.Revision < revision);
        var source = history.SingleOrDefault(e => e.Input.Id == input.Reference);
        if (input.Action == CourtAction.ReleaseRestorationEscort)
        {
            if (source == null || source.Input.Action != CourtAction.AcceptRestorationEscort ||
                source.Actor != actor || source.Input.Person != input.Person || source.Input.Owner != input.Owner ||
                source.Input.When != input.When || at >= input.When || site != input.Site ||
                !TransportWork.At(world, actor, site, at, revision, true) ||
                !TransportWork.At(world, input.Person, site, at, revision, true) ||
                Assassination.DeadAt(world, input.Person, at, revision) ||
                EscortReleased(world, source.Input.Id, revision))
                throw new InvalidDataException("警護期間中に依頼者本人が後援家の指揮者と会い、元の合意を示してください");
            return;
        }
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

    internal static bool EscortReleased(ContractWorld world, EntityId agreement, long beforeRevision) =>
        HouseholdCourt.Events(world).Any(e => e.Revision < beforeRevision &&
            e.Input.Action == CourtAction.ReleaseRestorationEscort && e.Input.Reference == agreement);

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

    internal static void SeekLocalSupport(ContractWorld world, long previousTime)
    {
        if (previousTime / 86400 == world.ElapsedSeconds / 86400 || !NewGameWork.Enabled(world) ||
            !CharacterRelations.Enabled(world) || !NewGameNpcRoutine.Enabled(world) ||
            (!world.LandRights.Any(r => r.Kind == "control" && r.Until <= world.ElapsedSeconds) && BeforePlacement(world) == null)) return;
        var history = HouseholdCourt.Events(world);
        foreach (var person in world.Characters.Where(c => c.Life == LifeState.Adult && c.Id != world.PlayerControl)
            .OrderBy(c => c.Id.Value, StringComparer.Ordinal))
        {
            var actor = person.Id;
            if (Assassination.DeadAt(world, actor, world.ElapsedSeconds, world.CommitSequence) ||
                HouseRestoration.ObserveFormerSeat(world, actor).FirstOrDefault() is not {} seen) continue;
            var peer = world.Characters.Where(c => c.Id != actor && c.Life == LifeState.Adult &&
                SocialWorld.Site(world, c.Id) == seen.Site &&
                !Assassination.DeadAt(world, c.Id, world.ElapsedSeconds, world.CommitSequence) &&
                !history.Any(e => e.Actor == actor && e.Input.Person == c.Id && e.Place == seen.Site &&
                    e.Input.Owner == seen.House && e.Input.Action == CourtAction.SeekRestorationSupport))
                .Select(c => (c.Id, Opinion: CharacterRelations.DiagnosticScores(world, actor, c.Id)))
                .Where(c => c.Opinion.Trust > c.Opinion.Hostility)
                .OrderByDescending(c => c.Opinion.Trust - c.Opinion.Hostility)
                .ThenBy(c => c.Id.Value, StringComparer.Ordinal).FirstOrDefault();
            if (peer.Id.Value == null) continue;
            var key = string.Join("/", actor.Value, seen.House.Value, seen.Site.Value, peer.Id.Value);
            var ask = new CourtInput(HouseholdCourt.Id("request", "restore-support/" + key),
                CourtAction.SeekRestorationSupport, peer.Id, seen.House, seen.Site);
            TransactionalWorld.TryApply(world, copy => HouseholdCourt.Execute(copy, ask, actor));
        }
    }

    internal static void AnswerLocalRequests(ContractWorld world)
    {
        var history = HouseholdCourt.Events(world);
        // 旧所属だけを根拠に、NPCの当主を一括して切り替えない。
        foreach (var candidate in history.Where(e => e.Input.Action == CourtAction.SeekRestorationSupport &&
            e.Actor != world.PlayerControl && e.At < world.ElapsedSeconds).Select(e => e.Actor).Distinct())
        {
            foreach (var application in ContinuityApplications(world, candidate).ToArray())
                TransactionalWorld.TryApply(world, copy => HouseholdCourt.Execute(copy, application, candidate));
        }
        foreach (var ask in history.Where(e => e.Input.Action == CourtAction.SeekRestorationSupport &&
            e.Input.Person != world.PlayerControl && e.At < world.ElapsedSeconds))
        {
            var creditor = ask.Input.Person;
            var place = SocialWorld.Site(world, creditor);
            if (place == null || SocialWorld.Site(world, ask.Actor) != place) continue;
            foreach (var term in (world.Salary?.Terms ?? []).Where(t => t.Employee == creditor && t.Owner == ask.Input.Owner))
            {
                var amount = UnpaidSalary(term, world.ElapsedSeconds);
                if (amount <= 0 || HouseholdCourt.Events(world).Any(e => e.Input.Action == CourtAction.PresentRestorationArrears &&
                    e.Input.Reference == term.Id && e.Input.Person == ask.Actor && e.Input.Amount == amount)) continue;
                var claim = new CourtInput(HouseholdCourt.Id("request", $"restoration-claim/{term.Id.Value}/{ask.Actor.Value}/{amount}"),
                    CourtAction.PresentRestorationArrears, ask.Actor, term.Owner, place.Value, Amount: amount, Reference: term.Id);
                TransactionalWorld.TryApply(world, copy => HouseholdCourt.Execute(copy, claim, creditor));
            }
        }
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
        var person = FamilyBirth.People(world).SingleOrDefault(p => p.Id == observer.Value);
        if (person == null || FamilyKin.LifeAt(world.NewGame!, person, now) != LifeState.Adult) return [];
        var parents = (person.BirthParents ?? []).Select(id => new EntityId(id)).ToHashSet();
        var adoption = world.Adoption?.Entries ?? [];
        foreach (var implementation in adoption.Where(e => e.Input.Action == AdoptionAction.Implement &&
            e.At <= now && e.Revision < revision))
        {
            var terms = adoption.Single(e => e.Input.Id == implementation.Input.Original).Terms!;
            if (terms.Child == observer) parents.Add(terms.Parent);
        }
        var service = world.Memberships.Where(r => r.Kind == "membership" &&
            (r.Subject == observer || parents.Contains(r.Subject))).ToArray();
        var birth = world.Births?.SingleOrDefault(b => b.Child == observer);
        foreach (var house in service.Select(row => row.Target).Distinct())
        {
            var rights = world.LandRights.Where(row =>
                row.Subject == house && row.Target == site && row.Kind == "control").ToArray();
            if (rights.Any(row => row.From <= now && (now < row.Until || now == row.Until && revision < row.Revision)))
                continue;

            var witnessed = rights.Any(right => service.Any(member =>
            {
                if (member.Target != house) return false;
                var since = Math.Max(Math.Max(member.From, right.From), birth?.At ?? 0);
                var until = Math.Min(member.Until, right.Until);
                if (since >= until || until > now ||
                    PersonalCompanions.SiteAt(world, observer, since, revision) != site) return false;
                return member.Subject == observer || HouseSuccession.Factions(world, since, revision)
                    .Any(f => f.Id == house.Value && f.Head == member.Subject.Value);
            }));
            if (witnessed)
                observations.Add(new(house, site, now));
        }
        if (BeforePlacement(world) is {} original)
        {
            var oldSeat = original.Castles.SingleOrDefault(c => c.Id == site.Value);
            var oldHouse = original.Factions.SingleOrDefault(f => f.Id == oldSeat?.Faction);
            var resident = original.People.SingleOrDefault(p => p.Id == observer.Value);
            if (oldHouse != null && !world.NewGame!.Start.Factions.Any(f => f.Id == oldHouse.Id) &&
                (resident?.Site == site.Value && resident.Faction == oldHouse.Id ||
                 world.NewGame.Start.People.Any(p => p.Id == observer.Value && p.Site == site.Value) && parents.Contains(new EntityId(oldHouse.Head)) &&
                 (person.BirthParents ?? []).Contains(oldHouse.Head)))
                observations.Add(new(new(oldHouse.Id), site, now));
        }
        return observations.OrderBy(row => row.House.Value, StringComparer.Ordinal).ToArray();
    }
}
