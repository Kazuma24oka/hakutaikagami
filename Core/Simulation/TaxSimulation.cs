using System.Text.Json;
using System.Text.Json.Serialization;
using HakutaiKagami.Core.Characters;
using HakutaiKagami.Core.Castles;
using HakutaiKagami.Core.Commands;
using HakutaiKagami.Core.World;

namespace HakutaiKagami.Core.Simulation;

public enum TaxPhase { Dispatched, Received, Reserved, Completed, Partial, Refused, Failed, Cancelled }
public enum DeliveryKind { Order, Report }
public enum TaxEventKind { DeliverOrder, Decide, Collect, DeliverReport, DeliverLateOrder, DeliverLateReport }
public sealed record TaxOrder(string RequestId, string CaseId, string IssuerId, string ExecutorId,
    string CastleId, int Requested, int Reserved, int Collected, TaxPhase Phase, bool Reported, string Reason, int? DecisionRoll);
public sealed record TaxDelivery(string Id, string RequestId, DeliveryKind Kind, string SenderId, string RecipientId,
    long DueAt, bool Delivered, int Attempt = 1, bool Lost = false, bool FailOnArrival = false, bool Cancelled = false,
    long? LateDueAt = null, bool ArrivedLate = false)
{
    [JsonIgnore] public string MessageId => RequestId + (Kind == DeliveryKind.Order ? "/letter" : "/report");
}
public sealed record DeliveryFailureNotice(string DeliveryId, string ObserverId, long LearnedAt, bool LegacyImported = false);
public sealed record FailureReportDelivery(string Id, string DeliveryId, string ReporterId, string RecipientId,
    int Copy, long SentAt, long DueAt, bool Delivered = false, bool Lost = false, bool FailOnArrival = false);
public sealed record TaxKnowledge(string Id, string CharacterId, string RequestId, DeliveryKind Kind,
    int Amount, string Content, long LearnedAt);
public sealed record TaxEvent(string Id, string RequestId, TaxEventKind Kind, long DueAt, string[] DependsOn, int Attempt = 1);
public sealed record RiceAccount(string Id, string OwnerId, string CustodyCastleId, int Balance);
public sealed record CharacterSnapshot(string Id, string OrganizationId, string Role, string CastleId);
public sealed record CastleSnapshot(string Id, string LordId, string ControllerOrganizationId);
public sealed record WorldSnapshot(ScenarioDefinition Definition, CharacterSnapshot[] Characters,
    CastleSnapshot[] Castles, Authority[] Authorities, ResourceStock[] Resources, long Tick, ulong RandomState)
{
    public static WorldSnapshot Capture(WorldState world) => new(world.Definition,
        world.Characters.Values.Select(c => new CharacterSnapshot(c.Id, c.OrganizationId, c.Role, c.CastleId)).ToArray(),
        world.Castles.Values.Select(c => new CastleSnapshot(c.Id, c.LordId, c.ControllerOrganizationId)).ToArray(),
        world.Authorities.ToArray(), world.Resources.ToArray(), world.Tick, world.Random.State);
    public WorldState Restore() => new(Definition,
        Characters.Select(c => new Character(Definition.Characters.Single(d => d.Id == c.Id), c.OrganizationId, c.Role, c.CastleId)),
        Castles.Select(c => new Castle(Definition.Castles.Single(d => d.Id == c.Id), c.LordId, c.ControllerOrganizationId)), Authorities, Resources, Tick, RandomState);
}

// Persisted DTO is a boundary type. The simulation never exposes its live mutable DTO.
public sealed class TaxSnapshot
{
    public int Version { get; set; } = 3;
    public int? MigrationOrigin { get; set; }
    public WorldSnapshot World { get; set; } = null!;
    public long Tick { get; set; }
    public bool Paused { get; set; }
    public ulong RandomState { get; set; }
    public int AcceptChancePercent { get; set; } = 100;
    public int InitialRice { get; set; }
    public int InitialSourceRice { get; set; }
    public int InitialTreasuryRice { get; set; }
    public RiceAccount[] Accounts { get; set; } = [];
    public Authority[] Grants { get; set; } = [];
    public TaxOrder[] Orders { get; set; } = [];
    public TaxDelivery[] Deliveries { get; set; } = [];
    public TaxKnowledge[] Knowledge { get; set; } = [];
    public TaxEvent[] Pending { get; set; } = [];
    public string[] Applied { get; set; } = [];
    public DeliveryFailureNotice[] FailureNotices { get; set; } = [];
    public FailureReportDelivery[] FailureReports { get; set; } = [];
}

/// <summary>One-thread prototype transaction boundary: clone, validate, publish. No wall-clock or Godot dependency.</summary>
public sealed class TaxSimulation
{
    public const int MaxDeliveryAttempts = 3; // Prototype cap, including the original attempt.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };
    private TaxSnapshot state;
    private TaxSimulation(TaxSnapshot state) { Validate(state); this.state = Clone(state); }
    public long Tick => state.Tick;
    public bool Paused => state.Paused;
    public int SourceRice => state.Accounts.Single(a => a.Id == "village").Balance;
    public int TreasuryRice => state.Accounts.Single(a => a.Id == "treasury").Balance;
    public int ReservedRice => state.Orders.Sum(o => o.Reserved);
    public int PendingCount => state.Pending.Length + state.FailureReports.Count(r => !r.Delivered && !r.Lost);
    public int AppliedCount => state.Applied.Length;
    public IReadOnlyList<TaxOrder> Orders => Array.AsReadOnly(state.Orders.ToArray());
    public IReadOnlyList<TaxDelivery> Deliveries => Array.AsReadOnly(state.Deliveries.ToArray());
    public IReadOnlyList<FailureReportDelivery> FailureReports => Array.AsReadOnly(state.FailureReports.ToArray());
    public bool FailureKnown(string deliveryId) => state.FailureNotices.Any(n => n.DeliveryId == deliveryId);
    public IReadOnlyList<TaxKnowledge> KnowledgeFor(string characterId) =>
        Array.AsReadOnly(state.Knowledge.Where(k => k.CharacterId == characterId).ToArray());
    public WorldState RestoreWorld()
    {
        var snapshot = Clone(state).World;
        var source = state.Accounts.Single(a => a.Id == "village");
        return (snapshot with { Tick = state.Tick, RandomState = state.RandomState,
            Resources = [snapshot.Resources.Single() with { Rice = TreasuryRice },
                new ResourceStock(source.OwnerId, source.CustodyCastleId, source.Balance, 0)] }).Restore();
    }

    public static TaxSimulation Create(WorldState world, int sourceRice = 60, int acceptChancePercent = 100)
    {
        world.Validate();
        if (sourceRice < 0 || acceptChancePercent is < 0 or > 100) throw new ArgumentOutOfRangeException();
        var characters = world.Definition.Characters;
        var castle = world.Definition.Castles.Single().Id;
        var owner = world.Definition.Organizations[0].Id;
        var snapshot = WorldSnapshot.Capture(world);
        // A fixture-only village organization makes transfer of ownership explicit.
        snapshot = snapshot with { Definition = snapshot.Definition with {
            Organizations = [.. snapshot.Definition.Organizations, new("org-fixture-village", "試作村落")]
        } };
        return new TaxSimulation(new TaxSnapshot
        {
            World = snapshot, Tick = world.Tick, RandomState = world.Random.State, AcceptChancePercent = acceptChancePercent,
            InitialRice = checked(sourceRice + world.Resources.Single().Rice),
            InitialSourceRice = sourceRice, InitialTreasuryRice = world.Resources.Single().Rice,
            Accounts = [new("village", "org-fixture-village", castle, sourceRice),
                new("treasury", owner, castle, world.Resources.Single().Rice)],
            Grants = [new(characters[0].Id, castle, "tax-order", "試作の徴税命令委任", 0, long.MaxValue),
                new(characters[1].Id, castle, "collect-tax", "試作の徴収担当委任", 0, long.MaxValue)]
        });
    }

    public void Submit(string requestId, string issuerId, string castleId, int amount)
    {
        if (string.IsNullOrWhiteSpace(requestId) || amount <= 0) throw new ArgumentException("Positive amount and request ID required.");
        var existing = state.Orders.SingleOrDefault(o => o.RequestId == requestId);
        if (existing != null)
        {
            if (existing.IssuerId != issuerId || existing.CastleId != castleId || existing.Requested != amount)
                throw new InvalidOperationException("Request ID reused with different payload.");
            return; // Retry retains logical identity, including after load.
        }
        Mutate(s =>
        {
            if (!s.World.Castles.Any(c => c.Id == castleId) || !Authorized(s, issuerId, castleId, "tax-order"))
                throw new UnauthorizedAccessException("No active grant for this actor/target/action.");
            var order = new TaxOrder(requestId, requestId + "/case", issuerId, s.World.Definition.Characters[1].Id,
                castleId, amount, 0, 0, TaxPhase.Dispatched, false, "", null);
            s.Orders = [.. s.Orders, order];
            s.Deliveries = [.. s.Deliveries, new(requestId + "/letter", requestId, DeliveryKind.Order,
                issuerId, order.ExecutorId, checked(s.Tick + 1), false)];
            Enqueue(s, order, TaxEventKind.DeliverOrder, s.Tick + 1, []);
        });
    }

    public void SetPaused(bool paused) => Mutate(s => s.Paused = paused);
    public void Revoke(string actorId, string actionId) => Mutate(s =>
        s.Grants = s.Grants.Where(g => g.ActorId != actorId || g.ActionId != actionId).ToArray());
    // Fault injection is an explicit test input; it does not inform either character of the future outcome.
    public void FailNextDelivery(string requestId, DeliveryKind kind) => Mutate(s =>
    {
        var delivery = s.Deliveries.Single(d => d.RequestId == requestId && d.Kind == kind && !d.Delivered && !d.Lost && !d.Cancelled);
        s.Deliveries = s.Deliveries.Select(d => d.Id == delivery.Id ? d with { FailOnArrival = true } : d).ToArray();
    });
    // The fixture supplies a courier's observation; the resulting report must travel before the sender knows.
    public void DispatchFailureReport(string deliveryId, string reporterId, int delay = 1) => Mutate(s =>
    {
        var delivery = s.Deliveries.Single(d => d.Id == deliveryId);
        if (!delivery.Lost || delay < 1) throw new InvalidOperationException("A report requires an observed failed attempt and positive delay.");
        if (reporterId != s.World.Definition.Characters[2].Id) throw new UnauthorizedAccessException("Only the fixture courier reports this observation.");
        int copy = s.FailureReports.Count(r => r.DeliveryId == deliveryId) + 1;
        if (copy > MaxDeliveryAttempts) throw new InvalidOperationException("Failure report copy limit reached.");
        s.FailureReports = [.. s.FailureReports, new(deliveryId + "/failure-report/" + copy,
            deliveryId, reporterId, delivery.SenderId, copy, s.Tick, checked(s.Tick + delay))];
    });
    public void FailFailureReport(string reportId) => Mutate(s =>
    {
        var report = s.FailureReports.Single(r => r.Id == reportId);
        if (report.Delivered || report.Lost) throw new InvalidOperationException("Report has already finished its delivery attempt.");
        s.FailureReports = s.FailureReports.Select(r => r.Id == reportId ? r with { FailOnArrival = true } : r).ToArray();
    });
    public void ScheduleLateArrival(string deliveryId, int delay) => Mutate(s =>
    {
        var delivery = s.Deliveries.Single(d => d.Id == deliveryId);
        if (!delivery.Lost || delivery.Delivered || delivery.Cancelled || delivery.LateDueAt != null || delay < 1)
            throw new InvalidOperationException("Only a failed, unreceived, active original can arrive late once.");
        var order = s.Orders.Single(o => o.RequestId == delivery.RequestId);
        long due = checked(s.Tick + delay);
        s.Deliveries = s.Deliveries.Select(d => d.Id == deliveryId ? d with { LateDueAt = due } : d).ToArray();
        var originalKind = delivery.Kind == DeliveryKind.Order ? TaxEventKind.DeliverOrder : TaxEventKind.DeliverReport;
        var lateKind = delivery.Kind == DeliveryKind.Order ? TaxEventKind.DeliverLateOrder : TaxEventKind.DeliverLateReport;
        Enqueue(s, order, lateKind, due, [EventId(order, originalKind, delivery.Attempt)], delivery.Attempt);
    });
    public void RetryDelivery(string requestId, DeliveryKind kind, string senderId) => Mutate(s =>
    {
        var order = s.Orders.Single(o => o.RequestId == requestId);
        var history = s.Deliveries.Where(d => d.RequestId == requestId && d.Kind == kind).OrderBy(d => d.Attempt).ToArray();
        var previous = history.Last();
        if (previous.SenderId != senderId || !previous.Lost || previous.Cancelled || history.Any(d => d.Delivered) ||
            !s.FailureNotices.Any(n => n.DeliveryId == previous.Id && n.ObserverId == senderId) ||
            previous.Attempt >= MaxDeliveryAttempts)
            throw new InvalidOperationException("Retry requires a known failed latest attempt within the limit.");
        if (kind == DeliveryKind.Order)
        {
            if (order.Phase != TaxPhase.Dispatched) throw new InvalidOperationException("This order cannot be resent.");
            if (!Authorized(s, senderId, order.CastleId, "tax-order")) throw new UnauthorizedAccessException("Sender grant expired before retry.");
        }
        else if (order.Reported) throw new InvalidOperationException("Report already received.");
        var attempt = checked(previous.Attempt + 1);
        var delivery = previous with { Id = previous.MessageId + "/attempt/" + attempt, Attempt = attempt,
            DueAt = checked(s.Tick + 1), Delivered = false, Lost = false, FailOnArrival = false, Cancelled = false,
            LateDueAt = null, ArrivedLate = false };
        s.Deliveries = [.. s.Deliveries, delivery];
        var eventKind = kind == DeliveryKind.Order ? TaxEventKind.DeliverOrder : TaxEventKind.DeliverReport;
        Enqueue(s, order, eventKind, delivery.DueAt, [EventId(order, eventKind, previous.Attempt)], attempt);
    });
    public void Cancel(string requestId) => Mutate(s =>
    {
        var order = s.Orders.Single(o => o.RequestId == requestId);
        if (order.Phase is not (TaxPhase.Dispatched or TaxPhase.Received or TaxPhase.Reserved))
            throw new InvalidOperationException("Committed outcome cannot be cancelled.");
        s.Pending = s.Pending.Where(e => e.RequestId != requestId).ToArray();
        s.Deliveries = s.Deliveries.Select(d => d.RequestId == requestId && !d.Delivered && d.Kind == DeliveryKind.Order ? d with { Cancelled = true } : d).ToArray();
        order = order with { Reserved = 0, Phase = TaxPhase.Cancelled, Reason = "発行者が取消" };
        Replace(s, order);
        QueueReport(s, order, []);
    });

    public void AdvanceOneTick(Action? beforeCommit = null)
    {
        if (state.Paused) return;
        Mutate(s =>
        {
            s.Tick = checked(s.Tick + 1);
            var due = s.Pending.Where(e => e.DueAt <= s.Tick).ToArray();
            // No implicit priority for resource conflicts: this fixture rejects conflicting decisions together.
            var decisions = due.Where(e => e.Kind == TaxEventKind.Decide).ToArray();
            bool conflict = decisions.Length > 1;
            foreach (var e in due.OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                if (!e.DependsOn.All(s.Applied.Contains)) throw new InvalidOperationException("Unresolved causal dependency.");
                Process(s, e, conflict && e.Kind == TaxEventKind.Decide);
                s.Pending = s.Pending.Where(p => p.Id != e.Id).ToArray();
                s.Applied = [.. s.Applied, e.Id];
            }
            foreach (var report in s.FailureReports.Where(r => !r.Delivered && !r.Lost && r.DueAt <= s.Tick).ToArray())
            {
                s.FailureReports = s.FailureReports.Select(r => r.Id == report.Id ? r with {
                    Delivered = !r.FailOnArrival, Lost = r.FailOnArrival } : r).ToArray();
                if (!report.FailOnArrival && !s.FailureNotices.Any(n => n.DeliveryId == report.DeliveryId))
                    s.FailureNotices = [.. s.FailureNotices, new(report.DeliveryId, report.RecipientId, s.Tick)];
                s.Applied = [.. s.Applied, report.Id + "/event"];
            }
        }, beforeCommit);
    }

    public string SaveJson() => JsonSerializer.Serialize(state, JsonOptions);
    public static TaxSimulation LoadJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("Version", out var version) || !version.TryGetInt32(out int number) || number is not (1 or 2 or 3))
                throw new InvalidDataException("Missing or unsupported save version.");
            var loaded = JsonSerializer.Deserialize<TaxSnapshot>(json, JsonOptions) ?? throw new InvalidDataException("Empty save.");
            if (number < 3)
            {
                if (loaded.MigrationOrigin != null || loaded.FailureReports.Length != 0 || loaded.Deliveries.Any(d => d.LateDueAt != null || d.ArrivedLate) ||
                    loaded.Pending.Any(e => e.Kind is TaxEventKind.DeliverLateOrder or TaxEventKind.DeliverLateReport))
                    throw new InvalidDataException("V3 delivery data cannot be disguised as an older save.");
                if (number == 1 && (loaded.FailureNotices.Length != 0 || loaded.Deliveries.Any(d => d.Attempt != 1 || d.Lost || d.FailOnArrival || d.Cancelled) || loaded.Pending.Any(e => e.Attempt != 1)))
                    throw new InvalidDataException("Retry data cannot be disguised as a v1 save.");
                loaded.Version = 3;
                loaded.MigrationOrigin = number;
                loaded.FailureNotices = loaded.FailureNotices.Select(n => n with { LegacyImported = true }).ToArray();
                loaded.Deliveries = loaded.Deliveries.Select(d => d.Kind == DeliveryKind.Order && !d.Delivered &&
                    loaded.Orders.Any(o => o.RequestId == d.RequestId && o.Phase == TaxPhase.Cancelled) ? d with { Cancelled = true } : d).ToArray();
            }
            return new TaxSimulation(loaded);
        }
        catch (Exception e) when (e is not InvalidDataException)
        { throw new InvalidDataException("Invalid or unsupported tax prototype save.", e); }
    }
    public void SaveFile(string path)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, SaveJson());
        // Same-volume replacement: failure preserves the previous destination and leaves the temp for diagnosis.
        File.Move(temp, full, overwrite: true);
    }
    public static TaxSimulation LoadFile(string path) => LoadJson(File.ReadAllText(path));

    private static TaxSnapshot Clone(TaxSnapshot s) => JsonSerializer.Deserialize<TaxSnapshot>(JsonSerializer.Serialize(s, JsonOptions), JsonOptions)!;
    private void Mutate(Action<TaxSnapshot> change, Action? beforeCommit = null)
    {
        var next = Clone(state);
        change(next);
        Validate(next);
        beforeCommit?.Invoke(); // Failure injection verifies no partial state becomes visible.
        state = next;
    }
    private static bool Authorized(TaxSnapshot s, string actor, string castle, string action) =>
        s.Grants.Any(g => g.Allows(new("validation", actor, castle, action, s.Tick), s.Tick));
    private static string EventId(TaxOrder o, TaxEventKind kind, int attempt = 1) =>
        o.RequestId + "/event/" + kind + (attempt == 1 ? "" : "/attempt/" + attempt);
    private static void Enqueue(TaxSnapshot s, TaxOrder o, TaxEventKind kind, long dueAt, string[] dependencies, int attempt = 1) =>
        s.Pending = [.. s.Pending, new(EventId(o, kind, attempt), o.RequestId, kind, dueAt, dependencies, attempt)];
    private static void Replace(TaxSnapshot s, TaxOrder o) => s.Orders = s.Orders.Select(x => x.RequestId == o.RequestId ? o : x).ToArray();
    private static void QueueReport(TaxSnapshot s, TaxOrder o, string[] dependencies)
    {
        s.Deliveries = [.. s.Deliveries, new(o.RequestId + "/report", o.RequestId, DeliveryKind.Report,
            o.ExecutorId, o.IssuerId, checked(s.Tick + 1), false)];
        Enqueue(s, o, TaxEventKind.DeliverReport, s.Tick + 1, dependencies);
    }
    private static void Process(TaxSnapshot s, TaxEvent e, bool resourceConflict)
    {
        var o = s.Orders.Single(o => o.RequestId == e.RequestId);
        if (e.Kind is TaxEventKind.DeliverOrder or TaxEventKind.DeliverReport or TaxEventKind.DeliverLateOrder or TaxEventKind.DeliverLateReport)
        {
            bool late = e.Kind is TaxEventKind.DeliverLateOrder or TaxEventKind.DeliverLateReport;
            var kind = e.Kind is TaxEventKind.DeliverOrder or TaxEventKind.DeliverLateOrder ? DeliveryKind.Order : DeliveryKind.Report;
            var delivery = s.Deliveries.Single(d => d.RequestId == o.RequestId && d.Kind == kind && d.Attempt == e.Attempt);
            if (!late && delivery.FailOnArrival)
            {
                s.Deliveries = s.Deliveries.Select(d => d.Id == delivery.Id ? d with { Lost = true } : d).ToArray();
                return; // A failed attempt completes, but no recipient knowledge, RNG draw or logical effect occurs.
            }
            s.Deliveries = s.Deliveries.Select(d => d.Id == delivery.Id ? d with { Delivered = true, ArrivedLate = late } : d).ToArray();
            // A physical copy is received, but the stable logical message is recognized only once.
            if (s.Knowledge.Any(k => k.RequestId == o.RequestId && k.Kind == kind)) return;
            if (late) e = e with { Kind = kind == DeliveryKind.Order ? TaxEventKind.DeliverOrder : TaxEventKind.DeliverReport };
        }
        switch (e.Kind)
        {
            case TaxEventKind.DeliverOrder:
                s.Knowledge = [.. s.Knowledge, new(o.RequestId + "/knowledge/order", o.ExecutorId, o.RequestId,
                    DeliveryKind.Order, o.Requested, "徴税要求を本人が受領", s.Tick)];
                Replace(s, o with { Phase = TaxPhase.Received });
                Enqueue(s, o, TaxEventKind.Decide, s.Tick + 1, [e.Id]);
                break;
            case TaxEventKind.Decide:
                var known = s.Knowledge.Single(k => k.CharacterId == o.ExecutorId && k.RequestId == o.RequestId && k.Kind == DeliveryKind.Order);
                var random = new SeededRandom(s.RandomState);
                var roll = (int)(random.Next() % 100);
                s.RandomState = random.State;
                o = o with { DecisionRoll = roll };
                if (!Authorized(s, o.IssuerId, o.CastleId, "tax-order") || !Authorized(s, o.ExecutorId, o.CastleId, "collect-tax"))
                    o = o with { Phase = TaxPhase.Refused, Reason = "権限が失効" };
                else if (roll >= s.AcceptChancePercent)
                    o = o with { Phase = TaxPhase.Refused, Reason = "受領した命令をNPCが拒否" };
                else if (resourceConflict)
                    o = o with { Phase = TaxPhase.Failed, Reason = "同時刻の競合。制度別裁定未実装のため安全に拒否" };
                else
                {
                    int available = s.Accounts.Single(a => a.Id == "village").Balance - s.Orders.Sum(x => x.Reserved);
                    int reserved = Math.Min(known.Amount, available);
                    o = reserved > 0 ? o with { Phase = TaxPhase.Reserved, Reserved = reserved }
                        : o with { Phase = TaxPhase.Failed, Reason = "徴収可能な米がない" };
                }
                Replace(s, o);
                if (o.Phase == TaxPhase.Reserved) Enqueue(s, o, TaxEventKind.Collect, s.Tick + 1, [e.Id]);
                else QueueReport(s, o, [e.Id]);
                break;
            case TaxEventKind.Collect:
                if (!Authorized(s, o.IssuerId, o.CastleId, "tax-order") || !Authorized(s, o.ExecutorId, o.CastleId, "collect-tax"))
                    o = o with { Phase = TaxPhase.Failed, Reserved = 0, Reason = "確定前に権限が失効" };
                else
                {
                    var amount = o.Reserved;
                    s.Accounts = s.Accounts.Select(a => a.Id == "village" ? a with { Balance = checked(a.Balance - amount) }
                        : a with { Balance = checked(a.Balance + amount) }).ToArray();
                    o = o with { Reserved = 0, Collected = amount, Phase = amount == o.Requested ? TaxPhase.Completed : TaxPhase.Partial,
                        Reason = amount == o.Requested ? "全量を徴収" : "在庫不足により部分達成" };
                }
                Replace(s, o);
                QueueReport(s, o, [e.Id]);
                break;
            case TaxEventKind.DeliverReport:
                s.Knowledge = [.. s.Knowledge, new(o.RequestId + "/knowledge/report", o.IssuerId, o.RequestId,
                    DeliveryKind.Report, o.Collected, o.Reason, s.Tick)];
                Replace(s, o with { Reported = true });
                break;
            default: throw new InvalidDataException("Unknown event kind.");
        }
    }

    private static void Validate(TaxSnapshot s)
    {
        static void Check(bool ok, string message) { if (!ok) throw new InvalidDataException(message); }
        Check(s.Version == 3 && s.MigrationOrigin is null or 1 or 2 && s.Tick >= 0 && s.InitialRice >= 0 && s.AcceptChancePercent is >= 0 and <= 100, "Unsupported version or invalid clock/policy.");
        var world = s.World.Restore();
        Check(s.Accounts.Length == 2 && s.Accounts.Select(a => a.Id).ToHashSet().SetEquals(["village", "treasury"]), "Invalid accounts.");
        Check(s.Accounts.All(a => a.Balance >= 0 && world.Castles.ContainsKey(a.CustodyCastleId) &&
            world.Definition.Organizations.Any(o => o.Id == a.OwnerId)), "Invalid owner, custody or balance.");
        Check(s.Accounts.Sum(a => (long)a.Balance) == s.InitialRice, "Rice conservation violated.");
        Check(s.InitialSourceRice >= 0 && s.InitialTreasuryRice >= 0 &&
            (long)s.InitialSourceRice + s.InitialTreasuryRice == s.InitialRice, "Invalid initial accounts.");
        Check(s.Orders.Select(o => o.RequestId).Distinct().Count() == s.Orders.Length &&
            s.Orders.Select(o => o.CaseId).Distinct().Count() == s.Orders.Length, "Duplicate request/case.");
        Check(s.Orders.All(o => !string.IsNullOrWhiteSpace(o.RequestId) && o.CaseId == o.RequestId + "/case" &&
            world.Characters.ContainsKey(o.IssuerId) && world.Characters.ContainsKey(o.ExecutorId) && world.Castles.ContainsKey(o.CastleId) &&
            o.Requested > 0 && o.Reserved >= 0 && o.Collected >= 0 && o.Collected <= o.Requested &&
            (long)o.Reserved + o.Collected <= o.Requested && Enum.IsDefined(o.Phase) &&
            (o.Phase == TaxPhase.Reserved ? o.Reserved > 0 && o.Collected == 0 : o.Reserved == 0) &&
            (o.Phase is TaxPhase.Completed or TaxPhase.Partial ? o.Collected > 0 : o.Collected == 0) &&
            (o.Phase != TaxPhase.Completed || o.Collected == o.Requested) &&
            (o.Phase != TaxPhase.Partial || o.Collected < o.Requested) &&
            (!o.Reported || o.Phase is TaxPhase.Completed or TaxPhase.Partial or TaxPhase.Refused or TaxPhase.Failed or TaxPhase.Cancelled)), "Invalid order state.");
        Check(s.Orders.Sum(o => (long)o.Reserved) <= s.Accounts.Single(a => a.Id == "village").Balance, "Double reservation.");
        long collected = s.Orders.Sum(o => (long)o.Collected);
        Check(s.Accounts.Single(a => a.Id == "village").Balance == s.InitialSourceRice - collected &&
            s.Accounts.Single(a => a.Id == "treasury").Balance == s.InitialTreasuryRice + collected, "Account/effect mismatch.");
        var allIds = s.Pending.Select(e => e.Id).Concat(s.Applied).ToArray();
        Check(allIds.Distinct().Count() == allIds.Length, "Duplicate pending/applied event.");
        Check(s.Pending.All(e => Enum.IsDefined(e.Kind) && e.DueAt > s.Tick &&
            s.Orders.Any(o => o.RequestId == e.RequestId && e.Id == EventId(o, e.Kind, e.Attempt)) &&
            (e.Kind is TaxEventKind.DeliverOrder or TaxEventKind.DeliverReport ? s.Deliveries.Any(d => d.RequestId == e.RequestId &&
                d.Kind == (e.Kind == TaxEventKind.DeliverOrder ? DeliveryKind.Order : DeliveryKind.Report) && d.Attempt == e.Attempt &&
                !d.Delivered && !d.Lost && !d.Cancelled && d.DueAt == e.DueAt) :
             e.Kind is TaxEventKind.DeliverLateOrder or TaxEventKind.DeliverLateReport ? s.Deliveries.Any(d => d.RequestId == e.RequestId &&
                d.Kind == (e.Kind == TaxEventKind.DeliverLateOrder ? DeliveryKind.Order : DeliveryKind.Report) && d.Attempt == e.Attempt &&
                d.Lost && !d.Delivered && !d.Cancelled && d.LateDueAt == e.DueAt) : e.Attempt == 1) &&
            e.DependsOn.All(s.Applied.Contains)), "Invalid pending event or causal dependency.");
        Check(s.Applied.All(id => s.FailureReports.Any(r => id == r.Id + "/event") || s.Orders.Any(o => new[] { TaxEventKind.Decide, TaxEventKind.Collect }.Any(kind => EventId(o, kind) == id) ||
            s.Deliveries.Any(d => d.RequestId == o.RequestId && (EventId(o, d.Kind == DeliveryKind.Order ? TaxEventKind.DeliverOrder : TaxEventKind.DeliverReport, d.Attempt) == id ||
                d.ArrivedLate && EventId(o, d.Kind == DeliveryKind.Order ? TaxEventKind.DeliverLateOrder : TaxEventKind.DeliverLateReport, d.Attempt) == id)))), "Unknown applied event.");
        Check(s.Deliveries.Select(d => d.Id).Distinct().Count() == s.Deliveries.Length && s.Knowledge.Select(k => k.Id).Distinct().Count() == s.Knowledge.Length, "Duplicate delivery/knowledge.");
        Check(s.Deliveries.All(d => Enum.IsDefined(d.Kind) && s.Orders.Any(o => o.RequestId == d.RequestId) &&
            world.Characters.ContainsKey(d.SenderId) && world.Characters.ContainsKey(d.RecipientId) && d.DueAt >= 0 &&
            d.Attempt is >= 1 and <= MaxDeliveryAttempts && !(d.Delivered && d.Cancelled) &&
            (d.ArrivedLate ? d.Delivered && d.Lost && d.LateDueAt != null && d.LateDueAt <= s.Tick : !(d.Delivered && (d.Lost || d.FailOnArrival))) &&
            (!d.Delivered && !d.Lost || d.DueAt <= s.Tick) &&
            d.Id == d.MessageId + (d.Attempt == 1 ? "" : "/attempt/" + d.Attempt)), "Invalid delivery.");
        Check(s.FailureNotices.Select(n => n.DeliveryId).Distinct().Count() == s.FailureNotices.Length &&
            s.FailureNotices.All(n => n.LearnedAt >= 0 && n.LearnedAt <= s.Tick && s.Deliveries.Any(d =>
                d.Id == n.DeliveryId && d.Lost && d.SenderId == n.ObserverId && d.DueAt <= n.LearnedAt) &&
                (n.LegacyImported ? s.MigrationOrigin is 1 or 2 : s.FailureReports.Any(r => r.DeliveryId == n.DeliveryId && r.Delivered && r.RecipientId == n.ObserverId && r.DueAt == n.LearnedAt))), "Loss recognition without a received failure notice.");
        Check(s.FailureReports.Select(r => r.Id).Distinct().Count() == s.FailureReports.Length &&
            s.FailureReports.All(r => r.Copy is >= 1 and <= MaxDeliveryAttempts && r.Id == r.DeliveryId + "/failure-report/" + r.Copy &&
                r.ReporterId == world.Definition.Characters[2].Id && r.SentAt >= 0 && r.SentAt <= s.Tick && r.DueAt > r.SentAt &&
                s.Deliveries.Any(d => d.Id == r.DeliveryId && d.Lost && d.SenderId == r.RecipientId && d.DueAt <= r.SentAt) &&
                !(r.Delivered && (r.Lost || r.FailOnArrival)) && (r.Delivered || r.Lost ? r.DueAt <= s.Tick && s.Applied.Contains(r.Id + "/event") :
                    r.DueAt > s.Tick && !s.Applied.Contains(r.Id + "/event")) &&
                (!r.Delivered || s.FailureNotices.Any(n => n.DeliveryId == r.DeliveryId && n.ObserverId == r.RecipientId))), "Invalid failure report delivery.");
        foreach (var reports in s.FailureReports.GroupBy(r => r.DeliveryId))
            Check(reports.OrderBy(r => r.Copy).Select(r => r.Copy).SequenceEqual(Enumerable.Range(1, reports.Count())), "Failure report copy sequence broken.");
        foreach (var group in s.Deliveries.GroupBy(d => d.MessageId))
        {
            var history = group.OrderBy(d => d.Attempt).ToArray();
            Check(history.Select(d => d.Attempt).SequenceEqual(Enumerable.Range(1, history.Length)), "Invalid attempt sequence.");
            for (int i = 0; i < history.Length; i++)
            {
                var d = history[i]; var order = s.Orders.Single(o => o.RequestId == d.RequestId);
                Check(d.SenderId == (d.Kind == DeliveryKind.Order ? order.IssuerId : order.ExecutorId) &&
                    d.RecipientId == (d.Kind == DeliveryKind.Order ? order.ExecutorId : order.IssuerId), "Delivery endpoint mismatch.");
                if (i > 0) Check(history[i - 1].Lost && s.FailureNotices.Any(n => n.DeliveryId == history[i - 1].Id && n.LearnedAt < d.DueAt), "Retry before known failure.");
                var eventId = EventId(order, d.Kind == DeliveryKind.Order ? TaxEventKind.DeliverOrder : TaxEventKind.DeliverReport, d.Attempt);
                Check(d.Delivered || d.Lost ? s.Applied.Contains(eventId) : d.Cancelled || s.Pending.Any(e => e.Id == eventId), "Delivery/event mismatch.");
                if (d.LateDueAt != null)
                {
                    var lateId = EventId(order, d.Kind == DeliveryKind.Order ? TaxEventKind.DeliverLateOrder : TaxEventKind.DeliverLateReport, d.Attempt);
                    Check(d.Lost && d.LateDueAt > d.DueAt && (d.ArrivedLate ? s.Applied.Contains(lateId) :
                        d.Cancelled || d.LateDueAt > s.Tick && s.Pending.Any(e => e.Id == lateId)), "Late arrival/event mismatch.");
                }
            }
        }
        Check(s.Knowledge.GroupBy(k => (k.RequestId, k.Kind)).All(g => g.Count() == 1) &&
            s.Knowledge.All(k => Enum.IsDefined(k.Kind) && k.LearnedAt <= s.Tick && k.LearnedAt >= 0 &&
            k.Id == k.RequestId + (k.Kind == DeliveryKind.Order ? "/knowledge/order" : "/knowledge/report") &&
            s.Deliveries.Any(d => d.RequestId == k.RequestId && d.Kind == k.Kind && d.RecipientId == k.CharacterId && d.Delivered &&
                (d.ArrivedLate ? d.LateDueAt : d.DueAt) <= k.LearnedAt)), "Knowledge without unique logical reception.");
        Check(s.Grants.All(g => world.Characters.ContainsKey(g.ActorId) && world.Castles.ContainsKey(g.TargetCastleId) &&
            !string.IsNullOrWhiteSpace(g.Basis) && g.ValidFrom >= 0 && g.ValidUntil > g.ValidFrom), "Invalid authority reference.");
        foreach (var o in s.Orders)
        {
            var work = s.Pending.Where(e => e.RequestId == o.RequestId && e.Kind is TaxEventKind.Decide or TaxEventKind.Collect).ToArray();
            Check(o.Phase == TaxPhase.Received ? work.Length == 1 && work[0].Kind == TaxEventKind.Decide :
                o.Phase == TaxPhase.Reserved ? work.Length == 1 && work[0].Kind == TaxEventKind.Collect : work.Length == 0, "Order/work phase mismatch.");
            if (o.Phase == TaxPhase.Dispatched || (!o.Reported && o.Phase is not (TaxPhase.Received or TaxPhase.Reserved)))
            {
                var kind = o.Phase == TaxPhase.Dispatched ? DeliveryKind.Order : DeliveryKind.Report;
                Check(s.Deliveries.Any(d => d.RequestId == o.RequestId && d.Kind == kind && !d.Delivered && !d.Cancelled), "No pending or failed logical delivery.");
            }
            Check((o.Phase == TaxPhase.Dispatched) == !s.Knowledge.Any(k => k.RequestId == o.RequestId && k.Kind == DeliveryKind.Order) ||
                o.Phase == TaxPhase.Cancelled, "Order knowledge/phase mismatch.");
            Check(o.Reported == s.Knowledge.Any(k => k.RequestId == o.RequestId && k.Kind == DeliveryKind.Report), "Report knowledge mismatch.");
            Check(s.Applied.Contains(EventId(o, TaxEventKind.Decide)) == (o.DecisionRoll != null) &&
                (o.DecisionRoll == null || o.DecisionRoll is >= 0 and < 100), "Decision/recorded random draw mismatch.");
            Check((o.Phase is TaxPhase.Completed or TaxPhase.Partial) == s.Applied.Contains(EventId(o, TaxEventKind.Collect)) ||
                (o.Phase == TaxPhase.Failed && s.Applied.Contains(EventId(o, TaxEventKind.Collect))), "Committed effect/event mismatch.");
            Check(o.Reported == s.Deliveries.Any(d => d.RequestId == o.RequestId && d.Kind == DeliveryKind.Report && d.Delivered), "Delivered report mismatch.");
            Check(s.Knowledge.Where(k => k.RequestId == o.RequestId).All(k =>
                k.Kind == DeliveryKind.Order ? k.Amount == o.Requested && k.CharacterId == o.ExecutorId :
                    k.Amount == o.Collected && k.CharacterId == o.IssuerId && k.Content == o.Reason), "Knowledge payload mismatch.");
        }
    }
}
