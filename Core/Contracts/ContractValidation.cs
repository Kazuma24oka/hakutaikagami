using System.Globalization;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

public static class ContractValidation
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidDataException(message); }
    private static HashSet<EntityId> Keys<T>(IEnumerable<T> values, Func<T, EntityId> id, string name)
    {
        var list = values.Select(id).ToArray();
        Check(list.All(i => EntityId.IsValid(i.Value)) && list.Distinct().Count() == list.Length, "Invalid/duplicate " + name);
        return list.ToHashSet();
    }
    public static void Validate(ContractWorld s)
    {
        try { ValidateCore(s); if(NpcWork.Enabled(s))NpcWork.Validate(s); else Check(s.NpcWork==null,"Unexpected NPC work module"); if(RegionalWork.Enabled(s))RegionalWork.Validate(s); if(PersonalStart.Enabled(s))PersonalStart.Validate(s); if(RetainerStart.Enabled(s))RetainerStart.Validate(s); if(AssessmentNotice.Enabled(s))AssessmentNotice.Validate(s); }
        catch (Exception e) when (e is not InvalidDataException)
        { throw new InvalidDataException("Malformed contract state.", e); }
    }
    private static void ValidateCore(ContractWorld s)
    {
        Check((s.Schema == 1 || s.Schema == 2 && BranchRoutes.Enabled(s)) && s.TimeDomain == "GameSecondsV1" && EntityId.IsValid(s.WorldId.Value), "Unknown world format/time domain.");
        Check(ContinuousWorld.Enabled(s)==(s.Atlas!=null),"Terrain atlas requires its ruleset");if(ContinuousWorld.Enabled(s))ContinuousWorld.Validate(s);
        Check(RegionalBattle.Enabled(s)==(s.Region!=null),"Regional module requires its ruleset.");if(RegionalBattle.Enabled(s))RegionalBattle.Validate(s);
        Check(FieldWorld.Enabled(s)==(s.Field!=null),"Field module requires its ruleset.");if(FieldWorld.Enabled(s))FieldWorld.Validate(s);
        Check(TrafficWorld.Enabled(s)==(s.TrafficStops!=null),"Traffic stops require their own ruleset/module.");
        Check(TravelControl.Enabled(s)==(s.TravelDecisions!=null),"Personal decisions require their own ruleset/module.");
        Check(s.ElapsedSeconds >= 0 && s.CommitSequence >= 0 && DateOnly.TryParseExact(s.EpochDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _), "Invalid world clock.");
        var defs = s.Definitions;
        Check(s.Schema==2 ? s.Branches!=null : s.Branches==null,"Graph module/schema mismatch");
        if(BranchRoutes.Enabled(s))BranchRoutes.Validate(s);
        if(MovementControl.Enabled(s))MovementControl.Validate(s);
        if(RegionalCampaign.Enabled(s))RegionalCampaign.Validate(s);else Check(s.Couriers==null,"Unexpected courier module");
        Check(!s.Tasks.Any(t=>t.Action==ActionKind.Redirect)||MovementControl.Enabled(s),"Redirect requires registered rules");
        if(RouteContract.Enabled(s))RouteContract.Validate(s);
        Check(EntityId.IsValid(defs.Id.Value) && !string.IsNullOrWhiteSpace(defs.Version) && (defs.Ruleset == "common-core-fixture-v1" || defs.Ruleset == RouteContract.Ruleset || defs.Ruleset == RegionalRoute.Ruleset || defs.Ruleset == RegionalBranch.Ruleset || defs.Ruleset == RegionalControl.Ruleset || defs.Ruleset == RegionalCampaign.Ruleset || defs.Ruleset == NpcWork.Ruleset || defs.Ruleset == PersonalStart.Ruleset || defs.Ruleset == RetainerStart.Ruleset || defs.Ruleset == RegionalWork.Ruleset || defs.Ruleset == AssessmentNotice.Ruleset || defs.Ruleset == MissionReview.Ruleset || defs.Ruleset == IncomingMission.Ruleset || defs.Ruleset == MissionEntry.Ruleset || defs.Ruleset == OfficerLoop.Ruleset || defs.Ruleset == ContinuousWorld.Ruleset || defs.Ruleset == RegionalScenery.Ruleset || defs.Ruleset == RegionalBattle.Ruleset || defs.Ruleset == FieldWorld.Ruleset || defs.Ruleset == TrafficWorld.Ruleset || defs.Ruleset == TravelControl.Ruleset || defs.Ruleset == TravelWorld.Ruleset || defs.Ruleset == NationalRoutes.Ruleset || defs.Ruleset == HistoricalRoads.Ruleset || defs.Ruleset == RiverRoads.Ruleset || defs.Ruleset == BranchRoutes.Ruleset || defs.Ruleset == MovementControl.Ruleset) && defs.MaxAttempts == 3,
            "Unsupported definition/ruleset; no silent production mapping.");
        var people = Keys(defs.Characters, c => c.Id, "character definitions");
        var orgs = Keys(defs.Organizations, o => o.Id, "organizations");
        var sites = Keys(defs.Sites, o => o.Id, "sites");
        var bases = Keys(defs.AuthorityBases,b=>b.Id,"authority bases");
        var armies = Keys(defs.Armies,a=>a.Id,"armies");
        Check(defs.Armies.All(a=>orgs.Contains(a.Organization)&&people.Contains(a.Commander)),"Broken army definition reference.");
        Check(defs.Characters.All(c => !string.IsNullOrWhiteSpace(c.Name)) && defs.Organizations.All(o => !string.IsNullOrWhiteSpace(o.Name)), "Missing name.");
        Check(defs.Sites.All(c => !string.IsNullOrWhiteSpace(c.Name) && c.LatitudeE7 is >= -900000000 and <= 900000000 &&
            c.LongitudeE7 is >= -1800000000 and <= 1800000000), "Invalid geographic anchor.");
        Check(Keys(s.Characters, c => c.Id, "characters").SetEquals(people), "Definition/state mismatch.");
        Check(people.Contains(s.PlayerControl) && s.Characters.Single(c => c.Id == s.PlayerControl).Life == LifeState.Adult,
            "Invalid playable character.");
        Check(s.Characters.All(c => Enum.IsDefined(c.Life) && EntityId.IsValid(c.AppearanceId.Value) && c.Revision >= 0), "Invalid character.");
        var entities = people.Concat(orgs).Concat(sites).ToHashSet();
        Check(entities.Count == people.Count + orgs.Count + sites.Count, "Cross-kind identity collision.");
        foreach (var relations in new[] { s.Memberships, s.ServiceBonds, s.Offices, s.LandRights })
        {
            Keys(relations, x => x.Id, "relations");
            foreach (var x in relations) Check(entities.Contains(x.Subject) && entities.Contains(x.Target) && x.Subject != x.Target &&
                bases.Contains(x.Basis) && x.From >= 0 && x.Until > x.From && x.Revision >= 0 && !string.IsNullOrWhiteSpace(x.Kind), "Invalid relation.");
        }
        Check(s.Memberships.All(x=>people.Contains(x.Subject)&&orgs.Contains(x.Target)&&x.Kind=="membership"),"Membership is not a person/organization relation.");
        Check(s.ServiceBonds.All(x=>people.Contains(x.Subject)&&people.Contains(x.Target)&&x.Kind=="service"),"Service is not a person/person relation.");
        Check(s.Offices.All(x=>people.Contains(x.Subject)&&sites.Contains(x.Target)),"Office is not a person/site assignment.");
        Check(s.LandRights.All(x=>entities.Contains(x.Subject)&&sites.Contains(x.Target)&&new[]{"control","ownership","occupation","revenue"}.Contains(x.Kind)),"Invalid land right category.");
        foreach (var edge in s.ServiceBonds.Where(x => x.From <= s.ElapsedSeconds && s.ElapsedSeconds < x.Until))
        {
            var seen = new HashSet<EntityId> { edge.Subject }; var queue = new Queue<EntityId>(); queue.Enqueue(edge.Target);
            while (queue.TryDequeue(out var id))
            {
                Check(id != edge.Subject, "Service cycle."); if (!seen.Add(id)) continue;
                foreach (var next in s.ServiceBonds.Where(x => x.Subject == id && x.From <= s.ElapsedSeconds && s.ElapsedSeconds < x.Until)) queue.Enqueue(next.Target);
            }
        }
        Keys(s.Grants, x => x.Id, "grants");
        Check(s.Grants.All(g => entities.Contains(g.Principal) && people.Contains(g.Agent) && sites.Contains(g.Scope) && bases.Contains(g.Basis) &&
            g.From >= 0 && g.Until > g.From && g.Limit >= 0 && g.Actions.Length > 0 && g.Actions.Distinct().Count() == g.Actions.Length &&
            g.Actions.All(a => Enum.IsDefined(a)) && g.Assets.Distinct().Count() == g.Assets.Length &&
            g.Assets.All(a => s.ResourceLots.Any(l => l.Id == a && l.Owner == g.Principal)) && g.Revision >= 0), "Invalid grant.");
        Check(Keys(s.Locations, x => x.Actor, "locations").SetEquals(people), "Every character needs exactly one physical location.");
        foreach (var l in s.Locations)
        {
            Check(Enum.IsDefined(l.Kind) && l.Revision >= 0 && l.ProgressMm >= 0 && l.SegmentIndex >= 0, "Invalid location.");
            Check(l.Kind switch {
                LocationKind.AtSite => l.Site is { } site && sites.Contains(site) && l.Route == null && l.ProgressMm == 0 && l.SegmentIndex == 0 && l.LatitudeE7 == null && l.LongitudeE7 == null && l.HeightMm == null && l.TerrainRevision == null,
                LocationKind.Traveling => l.Route is { } route && EntityId.IsValid(route.Value) && l.Site == null && l.LatitudeE7 == null && l.LongitudeE7 == null && l.HeightMm == null && l.TerrainRevision != null,
                LocationKind.AtPosition => l.Site == null && l.Route == null && l.LatitudeE7 is >= -900000000 and <= 900000000 && l.LongitudeE7 is >= -1800000000 and <= 1800000000 && l.HeightMm != null && !string.IsNullOrWhiteSpace(l.TerrainRevision),
                _ => false }, "Location union fields disagree.");
        }
        Keys(s.Units, x => x.Id, "units");
        Check(s.Units.All(u => people.Contains(u.Commander) && orgs.Contains(u.Organization) && armies.Contains(u.Army) && u.Soldiers >= 0 && u.Revision >= 0), "Invalid unit.");
        var lots = Keys(s.ResourceLots, x => x.Id, "resource lots");
        Check(s.ResourceLots.All(l => entities.Contains(l.Owner) && entities.Contains(l.Holder) && sites.Contains(l.Site) &&
            l.Quantity >= 0 && l.InitialQuantity >= 0 && l.Revision >= 0 && !string.IsNullOrWhiteSpace(l.Kind) && !string.IsNullOrWhiteSpace(l.Unit)), "Invalid resource lot.");
        var requests = Keys(s.Requests, r => r.Id, "requests"); var tasks = Keys(s.Tasks, t => t.Id, "tasks");
        foreach (var r in s.Requests)
        {
            Check(r.Id == r.Intent.RequestId && r.BodyHash == ContractJson.CanonicalHash(r.Intent) && r.CreatedAt >= 0 && r.CreatedAt <= s.ElapsedSeconds &&
                r.CommitSequence > 0 && r.CommitSequence <= s.CommitSequence, "Request payload/time/receipt mismatch.");
            ValidateIntentShape(r.Intent, people, sites, lots);
        }
        foreach (var t in s.Tasks)
        {
            Check(requests.Contains(t.RequestId) && people.Contains(t.Issuer) && people.Contains(t.Executor) && sites.Contains(t.Scope) &&
                t.Revision >= 0 && t.Progress >= 0 && Enum.IsDefined(t.Action) && Enum.IsDefined(t.Kind) && Enum.IsDefined(t.Agreement) &&
                Enum.IsDefined(t.Execution) && Enum.IsDefined(t.Report) && Enum.IsDefined(t.Assessment) && t.AbsoluteLatestAt >= 0 &&
                (t.DeadlineAt == null || t.DeadlineAt >= 0 && t.DeadlineAt <= t.AbsoluteLatestAt) && t.EffectiveAt >= 0 &&
                (t.StartedAt == null || t.StartedAt >= 0 && t.StartedAt <= s.ElapsedSeconds) &&
                (t.FinishedAt == null || t.FinishedAt >= 0 && t.FinishedAt <= s.ElapsedSeconds) &&
                (t.Execution is not (ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed) || t.FinishedAt != null), "Invalid task.");
            var i = s.Requests.Single(r => r.Id == t.RequestId).Intent;
            if(i.Action is ActionKind.Cancel or ActionKind.Redirect)Check(i.TargetTask is {} target&&tasks.Contains(target)&&s.Tasks.Single(x=>x.Id==target).Scope==i.Scope&&
                s.Tasks.Single(x=>x.Id==target).Executor==i.Executor,"Cancellation targets wrong task/recipient/scope.");
            Check(t.Issuer == i.Actor && t.Executor == i.Executor && t.Scope == i.Scope && t.Action == i.Action && t.Kind == i.Kind && t.AbsoluteLatestAt == i.Deadline.AbsoluteLatestAt, "Task identity mismatch.");
            var r=s.Requests.Single(r=>r.Id==t.RequestId);var mid=WorldSession.Child("message",t.Id,"order");
            Check(s.Messaging.Messages.Any(m=>m.Id==mid.Value&&m.CorrelationId==t.RequestId.Value&&m.SenderId==t.Issuer.Value&&
                m.RecipientId==t.Executor.Value&&m.Scope==t.Scope.Value&&m.Kind=="contract-order"&&m.Body==t.Id.Value&&m.CreatedAt==r.CreatedAt),"Order envelope disagrees with immutable request.");
            Check(s.SealedMessages.Contains(mid)==i.Important,"Important-letter policy lost.");
            Check(t.StartedAt==null||t.FinishedAt==null||t.StartedAt<=t.FinishedAt,"Task finishes before start.");
            if(t.Execution==ExecutionPhase.Completed&&t.Action!=ActionKind.Cancel)
                Check(s.EffectLedger.Count(e=>e.BusinessKey==t.Id.Value+"/finish/1")==1,"Completed task without exactly one committed effect.");
            if(t.Execution is ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed)
                Check(s.Outbox.Count(o=>o.Task==t.Id)==1&&!s.PendingEvents.Any(e=>e.Task==t.Id),"Terminal task missing report or still scheduled.");
        }
        Check(s.Tasks.Length == s.Requests.Length, "One task per accepted request.");
        Keys(s.Actions, a => a.Id, "actions");
        Check(s.Actions.All(a => tasks.Contains(a.Task) && people.Contains(a.Actor) && Enum.IsDefined(a.Phase) && a.StartedAt >= 0 && a.StartedAt <= s.ElapsedSeconds && a.Until >= a.StartedAt && a.Progress >= 0 && a.Channel == "main"), "Invalid action.");
        Check(s.Actions.All(a=>a.Actor==s.Tasks.Single(t=>t.Id==a.Task).Executor&&a.Progress<=a.Until-a.StartedAt&&
            (a.Phase!=ExecutionPhase.Running||s.Tasks.Single(t=>t.Id==a.Task).Execution==ExecutionPhase.Running)),"Action/task phase mismatch.");
        Check(s.Tasks.Where(t=>t.Execution==ExecutionPhase.Running).All(t=>s.Actions.Count(a=>a.Task==t.Id&&a.Phase==ExecutionPhase.Running)==1),"Running task lacks action occupancy.");
        Check(!s.Actions.Where(a => a.Phase == ExecutionPhase.Running).GroupBy(a => a.Actor).Any(g => g.Count() > 1), "Overlapping main actions.");
        Keys(s.Reservations, x => x.Id, "reservations");
        foreach (var v in s.Reservations) Check(lots.Contains(v.Lot) && tasks.Contains(v.Task) && v.Amount > 0 && v.Consumed >= 0 && v.Consumed <= v.Amount && Enum.IsDefined(v.Phase) &&
            (v.Phase != ReservationPhase.Active || v.Consumed == 0) && (v.Phase != ReservationPhase.Consumed || v.Consumed == v.Amount), "Invalid reservation.");
        foreach (var lot in s.ResourceLots) Check(s.Reservations.Where(v => v.Lot == lot.Id && v.Phase is ReservationPhase.Active or ReservationPhase.PartiallyConsumed)
            .Aggregate(0L, (sum, v) => checked(sum + v.Amount - v.Consumed)) <= lot.Quantity, "Over-reservation.");
        var effects = Keys(s.EffectLedger, e => e.Id, "effects");
        Check(s.EffectLedger.Select(e => e.BusinessKey).Distinct(StringComparer.Ordinal).Count() == s.EffectLedger.Length && s.EffectLedger.All(e => tasks.Contains(e.Task) && e.At >= 0 && e.At <= s.ElapsedSeconds &&
            IsHash(e.BodyHash) && !string.IsNullOrWhiteSpace(e.BusinessKey) && !string.IsNullOrWhiteSpace(e.Result)), "Effect key/hash/time invalid.");
        Keys(s.Transfers, x => x.Id, "transfers");
        Check(s.Transfers.All(x => effects.Contains(x.Effect) && lots.Contains(x.FromLot) && lots.Contains(x.ToLot) && x.FromLot != x.ToLot && x.Amount > 0 && x.At >= 0 && x.At <= s.ElapsedSeconds &&
            s.ResourceLots.Single(l => l.Id == x.FromLot).Unit == s.ResourceLots.Single(l => l.Id == x.ToLot).Unit &&
            s.ResourceLots.Single(l => l.Id == x.FromLot).Kind == s.ResourceLots.Single(l => l.Id == x.ToLot).Kind), "Invalid transfer.");
        foreach (var lot in s.ResourceLots)
        {
            var expected = s.Transfers.Where(x => x.ToLot == lot.Id).Aggregate(lot.InitialQuantity, (sum, x) => checked(sum + x.Amount));
            expected = s.Transfers.Where(x => x.FromLot == lot.Id).Aggregate(expected, (sum, x) => checked(sum - x.Amount));
            Check(expected == lot.Quantity, "Resource conservation failure.");
        }
        Keys(s.Debts, x => x.Id, "debts");
        Check(s.Debts.All(x => entities.Contains(x.Debtor) && entities.Contains(x.Creditor) && x.Debtor != x.Creditor && x.Outstanding >= 0 && bases.Contains(x.Basis) && !string.IsNullOrWhiteSpace(x.Unit)), "Invalid debt.");
        MessageFlow.Validate(s.Messaging, s.ElapsedSeconds, people.Select(x => x.Value).ToHashSet(StringComparer.Ordinal));
        Check(s.Messaging.Messages.All(m => EntityId.IsValid(m.Id) && EntityId.IsValid(m.CorrelationId) && EntityId.IsValid(m.Scope)), "Invalid message IDs.");
        Check(s.SealedMessages.Distinct().Count() == s.SealedMessages.Length && s.SealedMessages.All(id => s.Messaging.Messages.Any(m => m.Id == id.Value)), "Unknown sealed message.");
        var evidence = Keys(s.Evidence, x => x.Id, "evidence");
        foreach (var e in s.Evidence)
        {
            Check(people.Contains(e.Observer) && (entities.Contains(e.Subject) || tasks.Contains(e.Subject)) && e.RecordedAt >= 0 && e.RecordedAt <= s.ElapsedSeconds &&
                (e.ObservedAt == null || e.ObservedAt >= 0 && e.ObservedAt <= e.RecordedAt) && !string.IsNullOrWhiteSpace(e.Claim) &&
                (e.Supersedes == null || evidence.Contains(e.Supersedes.Value) && e.Supersedes != e.Id), "Invalid evidence.");
            if (e.Message is { } mid) Check(s.Messaging.Messages.Any(m => m.Id == mid.Value) && MessageFlow.Knows(s.Messaging, mid.Value, e.Observer.Value), "Evidence lacks message reception.");
        }
        Check(s.Knowledge.Select(k => (k.Character, k.Evidence)).Distinct().Count() == s.Knowledge.Length, "Duplicate personal knowledge.");
        Check(s.Knowledge.All(k => people.Contains(k.Character) && evidence.Contains(k.Evidence) && k.LearnedAt >= s.Evidence.Single(e => e.Id == k.Evidence).RecordedAt &&
            k.LearnedAt <= s.ElapsedSeconds && s.Evidence.Single(e => e.Id == k.Evidence).Observer == k.Character), "Knowledge without admissible evidence.");
        var pending = Keys(s.PendingEvents, e => e.Id, "events");
        Check(s.PendingEvents.All(e => tasks.Contains(e.Task) && Enum.IsDefined(e.Kind) && e.At > s.ElapsedSeconds &&
            e.DependsOn.All(d => effects.Contains(d) || pending.Contains(d)) && !effects.Contains(e.Id)), "Invalid event time/dependency.");
        foreach(var e in s.PendingEvents)
        {
            var t=s.Tasks.Single(t=>t.Id==e.Task);
            Check(e.Id==WorldSession.Child("event",t.Id,e.Kind.ToString().ToLowerInvariant()),"Event ID does not match logical step.");
            Check(e.Kind switch{
                EventKind.Finish=>t.Execution==ExecutionPhase.Running&&s.Actions.Any(a=>a.Task==t.Id&&a.Phase==ExecutionPhase.Running&&a.Until==e.At),
                EventKind.Execute=>t.Execution==ExecutionPhase.Waiting&&t.Agreement==AgreementPhase.Active&&t.EffectiveAt==e.At,
                EventKind.Cancel=>(t.Action is ActionKind.Cancel or ActionKind.Redirect)&&t.Agreement==AgreementPhase.Active&&t.EffectiveAt==e.At,
                _=>false},"Scheduled step contradicts task phase.");
            Check(e.DependsOn.All(d=>!pending.Contains(d)||s.PendingEvents.Single(x=>x.Id==d).At<=e.At),"Dependency is later than its consequence.");
        }
        CheckAcyclic(s.PendingEvents);
        Keys(s.Outbox, e => e.Id, "outbox");
        Check(s.Outbox.All(e => tasks.Contains(e.Task) && people.Contains(e.Sender) && people.Contains(e.Recipient) && e.At >= 0 && e.At <= s.ElapsedSeconds && e.Delay > 0 &&
            (!e.Sent || s.Messaging.Messages.Any(m => m.Id == e.Id.Value && m.Body == e.Body && m.CreatedAt == e.At))), "Invalid transactional outbox.");
        Keys(s.Compensations, x => x.Id, "compensations");
        Check(s.Compensations.All(c => effects.Contains(c.OriginalEffect) && c.Outstanding >= 0 && Enum.IsDefined(c.Phase) &&
            (c.Phase != CompensationPhase.Settled || c.Outstanding == 0)), "Invalid compensation.");
        Check(s.RandomStreams.Select(x => x.Purpose).Distinct(StringComparer.Ordinal).Count() == s.RandomStreams.Length && s.RandomStreams.All(x => !string.IsNullOrWhiteSpace(x.Purpose) && x.DrawCount >= 0), "Invalid random streams.");
        Keys(s.RandomDraws, x => x.Id, "draws");
        foreach (var stream in s.RandomStreams)
        {
            var draws = s.RandomDraws.Where(x => x.Purpose == stream.Purpose).ToArray();
            Check(draws.Length == stream.DrawCount, "Random draw count mismatch.");
            Check(draws.Length==0?stream.State==ContractRandom.InitialState(s.Seed,stream.Purpose):draws[0].Before==ContractRandom.InitialState(s.Seed,stream.Purpose),"Random chain does not match stored seed.");
            for (var n = 0; n < draws.Length; n++)
            {
                var x = draws[n]; var after = unchecked(x.Before * 6364136223846793005UL + 1442695040888963407UL);
                Check(x.After == after && x.Result == (uint)(after >> 32) && x.At >= 0 && x.At <= s.ElapsedSeconds && (n == 0 || x.Before == draws[n-1].After), "Random chain mismatch.");
            }
            Check(draws.Length == 0 || stream.State == draws[^1].After, "Random end-state mismatch.");
        }
        Check(s.RandomDraws.All(x => s.RandomStreams.Any(r => r.Purpose == x.Purpose)), "Orphan random draw.");
        Check(s.IdCounters.All(k => System.Text.RegularExpressions.Regex.IsMatch(k.Key, "^[a-z][a-z0-9-]*$") && k.Value >= 0), "Invalid allocator.");
        foreach(var (name,ids) in new[]{("task",s.Tasks.Select(t=>t.Id)),("chronicle",s.Chronicle.Select(c=>c.Id)),("conflict",s.ConflictDecisions.Select(c=>c.Id))})
        {
            foreach(var id in ids)
            {
                var tail=id.Value.StartsWith(name+":",StringComparison.Ordinal)?id.Value[(name.Length+1)..]:"";
                Check(long.TryParse(tail,System.Globalization.NumberStyles.None,CultureInfo.InvariantCulture,out var n)&&n>0&&s.IdCounters.GetValueOrDefault(name)>=n,"Allocator would reuse a live ID.");
            }
        }
        Keys(s.ConflictDecisions, x => x.Id, "conflict decisions");
        Check(s.ConflictDecisions.All(x => x.Inputs.All(tasks.Contains) && x.At >= 0 && x.At <= s.ElapsedSeconds && !string.IsNullOrWhiteSpace(x.Rule)), "Invalid conflict decision.");
        Keys(s.Chronicle, x => x.Id, "chronicle");
        Check(s.Chronicle.All(x => people.Contains(x.Actor) && x.At >= 0 && x.At <= s.ElapsedSeconds && !string.IsNullOrWhiteSpace(x.Text)), "Invalid chronicle.");
    }
    public static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static void ValidateIntentShape(Intent i, IReadOnlySet<EntityId> people, IReadOnlySet<EntityId> sites, IReadOnlySet<EntityId> lots)
    {
        Check(EntityId.IsValid(i.RequestId.Value) && people.Contains(i.Actor) && people.Contains(i.Executor) && sites.Contains(i.Scope) && Enum.IsDefined(i.Action) && Enum.IsDefined(i.Kind) &&
            i.Amount >= 0 && i.DeliverySeconds > 0 && i.WorkSeconds > 0 && i.ReportSeconds > 0 && i.EffectiveAt >= 0 &&
            Enum.IsDefined(i.Deadline.Criterion) && i.Deadline.AbsoluteLatestAt >= 0 &&
            (i.Deadline.At == null || i.Deadline.At >= 0 && i.Deadline.At <= i.Deadline.AbsoluteLatestAt) &&
            (!i.Deadline.FromOpening || i.Important && i.Deadline.DurationSeconds > 0 && i.Deadline.Criterion == DeadlineCriterion.Completion) &&
            (i.SourceLot == null || lots.Contains(i.SourceLot.Value)) && (i.DestinationLot == null || lots.Contains(i.DestinationLot.Value)) &&
            (i.DestinationSite == null || sites.Contains(i.DestinationSite.Value)), "Invalid intent.");
        Check(i.Action switch {
            ActionKind.Transfer => i.Amount > 0 && i.SourceLot != null && i.DestinationLot != null && i.SourceLot != i.DestinationLot && i.DestinationSite == null && i.TargetTask == null,
            ActionKind.Move => i.Amount == 0 && i.SourceLot == null && i.DestinationLot == null && i.DestinationSite != null && i.TargetTask == null,
            ActionKind.Inspect => i.Amount == 0 && i.SourceLot == null && i.DestinationLot == null && i.DestinationSite == null && i.TargetTask == null,
            ActionKind.Redirect => i.Amount == 0 && i.SourceLot == null && i.DestinationLot == null && i.DestinationSite != null && i.TargetTask != null,
            ActionKind.Cancel => i.Amount == 0 && i.SourceLot == null && i.DestinationLot == null && i.DestinationSite == null && i.TargetTask != null,
            _ => false }, "Action payload mismatch.");
    }
    public static void CheckAcyclic(PendingEvent[] events)
    {
        var pending=events.ToDictionary(e=>e.Id);var degree=events.ToDictionary(e=>e.Id,e=>e.DependsOn.Count(pending.ContainsKey));
        var children=events.SelectMany(e=>e.DependsOn.Where(pending.ContainsKey).Select(d=>(Parent:d,Child:e.Id))).ToLookup(x=>x.Parent,x=>x.Child);
        var ready=new Queue<EntityId>(degree.Where(x=>x.Value==0).Select(x=>x.Key));int count=0;
        while(ready.TryDequeue(out var id)){count++;foreach(var child in children[id])if(--degree[child]==0)ready.Enqueue(child);}
        Check(count==events.Length,"Event dependency cycle.");
    }
}
