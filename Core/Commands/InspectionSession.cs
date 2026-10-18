using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Messaging;
using HakutaiKagami.Core.World;

namespace HakutaiKagami.Core.Commands;

public enum InspectionOutcome { Completed, Denied, Cancelled }
public sealed record InspectionResult(string RequestId, string ActorId, InspectionOutcome Outcome, string CastleName);
public sealed class InspectionSnapshot
{
    public int Version { get; set; } = 1;
    public long Tick { get; set; }
    public bool Paused { get; set; }
    public string[] Characters { get; set; } = [];
    public string ClerkId { get; set; } = "";
    public string CastleId { get; set; } = "";
    public string CastleName { get; set; } = "";
    public Authority[] Grants { get; set; } = [];
    public MessageState Messaging { get; set; } = new();
    public string[] Applied { get; set; } = [];
    public InspectionResult[] Results { get; set; } = [];
}

// A second domain adapter: read-only castle inspection, with no tax/resource/cancellation model dependency.
public sealed class InspectionSession
{
    private InspectionSnapshot state;
    private InspectionSession(InspectionSnapshot state) { Validate(state); this.state = SnapshotCodec.Copy(state); }
    public long Tick => state.Tick;
    public int AppliedCount => state.Applied.Length;
    public int PendingCount => MessageFlow.Pending(state.Messaging);
    public IReadOnlyList<MessageEnvelope> Messages => Array.AsReadOnly(state.Messaging.Messages.ToArray());
    public IReadOnlyList<MessageAttempt> Attempts => Array.AsReadOnly(state.Messaging.Attempts.ToArray());
    public static InspectionSession Create(WorldState world) => new(new InspectionSnapshot {
        Tick = world.Tick, Characters = world.Characters.Keys.ToArray(), ClerkId = world.Definition.Characters[1].Id,
        CastleId = world.Definition.Castles.Single().Id, CastleName = world.Definition.Castles.Single().Name,
        Grants = world.Authorities.ToArray() });
    private void Change(Action<InspectionSnapshot> action, Action? beforeCommit = null) => state = SessionTransaction.Apply(state, action, Validate, beforeCommit);
    public void Submit(string id, string actor, int delay = 1, long? deliverBy = null) => Change(s =>
    {
        if (!PermissionPolicy.Allows(s.Grants, actor, s.CastleId, "inspect", s.Tick)) throw new UnauthorizedAccessException("No inspection grant.");
        MessageFlow.Send(s.Messaging, new(id + "/request", id, "inspect-castle", actor, s.ClerkId, s.CastleId, s.CastleId, s.Tick, deliverBy), s.Tick, delay);
    });
    public void Cancel(string requestId, string actor, int delay = 1) => Change(s =>
    {
        var original = s.Messaging.Messages.Single(m => m.Id == requestId + "/request");
        if (original.SenderId != actor || !PermissionPolicy.Allows(s.Grants, actor, s.CastleId, "inspect", s.Tick)) throw new UnauthorizedAccessException("Cannot withdraw this inspection.");
        MessageFlow.Send(s.Messaging, new(requestId + "/cancel", requestId, "cancel-inspection", actor, s.ClerkId, requestId, s.CastleId, s.Tick), s.Tick, delay);
    });
    public void Revoke(string actor) => Change(s => s.Grants = s.Grants.Where(g => g.ActorId != actor).ToArray());
    public void SetPaused(bool paused) => Change(s => s.Paused = paused);
    public void Fail(string message) => Change(s => MessageFlow.Fail(s.Messaging, message));
    public void ReportFailure(string message, string reportId) => Change(s => MessageFlow.ReportFailure(s.Messaging, reportId, MessageFlow.Latest(s.Messaging, message).Id, s.Characters[2], s.Tick));
    public void Retry(string message, string sender) => Change(s => MessageFlow.Retry(s.Messaging, message, sender, s.Tick));
    public void ScheduleLate(string message, int number, int delay) => Change(s => MessageFlow.ScheduleLate(s.Messaging, message, number, s.Tick, delay));
    public void GrantProxy(ProxyPermission permission) => Change(s => s.Messaging.Proxies = [.. s.Messaging.Proxies, permission]);
    public void Route(string message, string receiver) => Change(s => MessageFlow.RouteTo(s.Messaging, message, receiver));
    public void Forward(string message, string agent, string forwardId) => Change(s => MessageFlow.Forward(s.Messaging, message, agent, forwardId, s.Tick));
    public InspectionResult? ResultFor(string requestId, string actor) => MessageFlow.Knows(state.Messaging, requestId + "/result", actor)
        ? state.Results.Single(r => r.RequestId == requestId) : null;
    public void AdvanceOneTick(Action? beforeCommit = null)
    {
        if (state.Paused) return;
        Change(s =>
        {
            s.Tick = checked(s.Tick + 1); MessageFlow.Advance(s.Messaging, s.Tick);
            foreach (var request in s.Messaging.Messages.Where(m => m.Kind == "inspect-castle").ToArray())
            {
                bool withdrawn = MessageFlow.Knows(s.Messaging, request.CorrelationId + "/cancel", s.ClerkId);
                if (!withdrawn && !MessageFlow.Knows(s.Messaging, request.Id, s.ClerkId)) continue;
                var applied = s.Applied;
                if (!EffectLedger.Mark(ref applied, request.CorrelationId + "/inspection")) continue;
                s.Applied = applied;
                var outcome = !PermissionPolicy.Allows(s.Grants, request.SenderId, request.Scope, "inspect", s.Tick) ? InspectionOutcome.Denied :
                    withdrawn ? InspectionOutcome.Cancelled : InspectionOutcome.Completed;
                var result = new InspectionResult(request.CorrelationId, request.SenderId, outcome, outcome == InspectionOutcome.Completed ? s.CastleName : "");
                s.Results = [.. s.Results, result];
                MessageFlow.Send(s.Messaging, new(request.CorrelationId + "/result", request.CorrelationId, "inspection-result", s.ClerkId,
                    request.SenderId, SnapshotCodec.Write(result), request.Scope, s.Tick), s.Tick);
            }
        }, beforeCommit);
    }
    public string SaveJson() => SnapshotCodec.Write(state);
    public void SaveFile(string path) => SnapshotCodec.SaveFile(path, SaveJson());
    public static InspectionSession LoadJson(string json)
    {
        try { return new(SnapshotCodec.Read<InspectionSnapshot>(json)); }
        catch (Exception error) when (error is not InvalidDataException) { throw new InvalidDataException("Invalid inspection save.", error); }
    }
    public static InspectionSession LoadFile(string path) => LoadJson(File.ReadAllText(path));
    private static void Validate(InspectionSnapshot s)
    {
        static void Check(bool ok, string message) { if (!ok) throw new InvalidDataException(message); }
        Check(s.Version == 1 && s.Tick >= 0 && s.Characters.Length >= 3 && s.Characters.Distinct().Count() == s.Characters.Length &&
            s.Characters.Contains(s.ClerkId) && !string.IsNullOrWhiteSpace(s.CastleId) && !string.IsNullOrWhiteSpace(s.CastleName), "Invalid inspection context.");
        MessageFlow.Validate(s.Messaging, s.Tick, s.Characters.ToHashSet(StringComparer.Ordinal));
        Check(s.Grants.All(g => s.Characters.Contains(g.ActorId) && g.TargetCastleId == s.CastleId && g.ValidUntil > g.ValidFrom && !string.IsNullOrWhiteSpace(g.Basis)), "Invalid inspection authority.");
        Check(s.Applied.Distinct().Count() == s.Applied.Length && s.Results.Select(r => r.RequestId).Distinct().Count() == s.Results.Length && s.Applied.Length == s.Results.Length, "Duplicate inspection effect.");
        foreach (var r in s.Results)
            Check(Enum.IsDefined(r.Outcome) && s.Applied.Contains(r.RequestId + "/inspection") &&
                s.Messaging.Messages.Any(m => m.Id == r.RequestId + "/request" && m.SenderId == r.ActorId) &&
                s.Messaging.Messages.Any(m => m.Id == r.RequestId + "/result" && m.Body == SnapshotCodec.Write(r)) &&
                (r.Outcome == InspectionOutcome.Completed ? r.CastleName == s.CastleName : r.CastleName == ""), "Inspection result/message mismatch.");
    }
}
