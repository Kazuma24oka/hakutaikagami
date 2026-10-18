using HakutaiKagami.Core.Commands;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Simulation;

public sealed partial class TaxSimulation
{
    public IReadOnlyList<MessageEnvelope> Messages => Array.AsReadOnly(state.Messaging.Messages.ToArray());
    public IReadOnlyList<MessageAttempt> MessageAttempts => Array.AsReadOnly(state.Messaging.Attempts.ToArray());
    public IReadOnlyList<FormalReceipt> FormalReceipts => Array.AsReadOnly(state.Messaging.Receipts.ToArray());
    public bool KnowsMessage(string messageId, string characterId) => MessageFlow.Knows(state.Messaging, messageId, characterId);
    public CancellationStatus? CancellationResultFor(string id, string person) =>
        KnowsMessage(id + "/reply", person) ? Enum.Parse<CancellationStatus>(state.Messaging.Messages.Single(m => m.Id == id + "/reply").Body) : null;
    public void SetProxyPermission(ProxyPermission permission) => Mutate(s =>
    {
        if (s.Messaging.Proxies.Any(p => p.Id == permission.Id)) throw new InvalidOperationException("Duplicate proxy grant ID.");
        s.Messaging.Proxies = [.. s.Messaging.Proxies, permission];
    });
    public void RevokeProxy(string id) => Mutate(s => s.Messaging.Proxies = s.Messaging.Proxies.Select(p => p.Id == id ? p with { ValidUntil = Math.Max(p.ValidFrom, s.Tick) } : p).ToArray());
    public void RouteMessage(string message, string receiver) => Mutate(s => MessageFlow.RouteTo(s.Messaging, message, receiver));
    public void OpenProxyMessage(string message, string agent) => Mutate(s => MessageFlow.OpenAsProxy(s.Messaging, message, agent, s.Tick));
    public void ForwardMessage(string message, string agent, string reportId, int delay = 1) => Mutate(s => MessageFlow.Forward(s.Messaging, message, agent, reportId, s.Tick, delay));
    public void FailMessage(string message) => Mutate(s => MessageFlow.Fail(s.Messaging, message));
    public void ReportMessageFailure(string attemptId, string reportId, string courier = "character-003", int delay = 1) => Mutate(s =>
    {
        if (courier != s.World.Definition.Characters[2].Id) throw new UnauthorizedAccessException("Fixture observer required.");
        MessageFlow.ReportFailure(s.Messaging, reportId, attemptId, courier, s.Tick, delay);
    });
    public void RetryCancellation(string id, string sender) => Mutate(s =>
    {
        var c = s.Cancellations.Single(c => c.Id == id); var order = s.Orders.Single(o => o.RequestId == c.RequestId);
        if (!Authorized(s, sender, order.CastleId, "cancel-tax")) throw new UnauthorizedAccessException("Cancellation grant expired before resend.");
        MessageFlow.Retry(s.Messaging, id + "/command", sender, s.Tick);
        s.Cancellations = s.Cancellations.Select(c => c.Id == id ? c with { Status = CancellationStatus.InTransit } : c).ToArray();
    });
    public void RetryCancellationReply(string id, string responder) => Mutate(s =>
    {
        var c = s.Cancellations.Single(c => c.Id == id);
        if (responder != c.RecipientId || c.ResolvedAt == null) throw new UnauthorizedAccessException("No resolved response for this responder.");
        if (!s.Messaging.Messages.Any(m => m.Id == id + "/reply")) SendCancellationReply(s, c);
        else MessageFlow.Retry(s.Messaging, id + "/reply", responder, s.Tick);
    });
    public void ScheduleLateMessage(string messageId, int attempt, int delay) => Mutate(s => MessageFlow.ScheduleLate(s.Messaging, messageId, attempt, s.Tick, delay));
    public void SetRole(string characterId, string role) => Mutate(s =>
    {
        if (!s.World.Characters.Any(c => c.Id == characterId) || string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Unknown character or empty role.");
        s.World = s.World with { Characters = s.World.Characters.Select(c => c.Id == characterId ? c with { Role = role } : c).ToArray() };
    });
    public void GrantAuthority(Authority grant) => Mutate(s => s.Grants = [.. s.Grants, grant]);
    private static void SendCancellationReply(TaxSnapshot s, CancellationCommand c) => MessageFlow.Send(s.Messaging,
        new(c.Id + "/reply", c.Id, "cancel-result", c.RecipientId, c.SenderId, c.Status.ToString(),
            s.Orders.Single(o => o.RequestId == c.RequestId).CastleId, s.Tick), s.Tick);

    private static void ImportCancellationTransport(TaxSnapshot s)
    {
        foreach (var old in s.Cancellations.ToArray())
        {
            var id = old.Id + "/command";
            var scope = s.Orders.Single(o => o.RequestId == old.RequestId).CastleId;
            s.Messaging.Messages = [.. s.Messaging.Messages, new(id, old.Id, "cancel-command", old.SenderId, old.RecipientId, old.RequestId, scope, old.SentAt)];
            var status = old.Delivered ? AttemptStatus.Delivered : old.Lost ? AttemptStatus.Lost : AttemptStatus.InTransit;
            s.Messaging.Attempts = [.. s.Messaging.Attempts, new(id + "/attempt/1", id, 1, old.SentAt, old.DueAt, old.RecipientId,
                status, old.FailOnArrival, old.Delivered ? old.ReceivedAt : old.Lost ? old.DueAt : null,
                old.Lost ? old.DueAt : null, old.LateDueAt, old.Delivered && old.Lost)];
            if (old.Delivered)
            {
                s.Messaging.Receipts = [.. s.Messaging.Receipts, new(id, old.RecipientId, old.RecipientId, old.ReceivedAt!.Value, false)];
                s.Messaging.Knowledge = [.. s.Messaging.Knowledge, new(id, old.RecipientId, old.ReceivedAt.Value, id)];
            }
            s.Cancellations = s.Cancellations.Select(c => c.Id == old.Id ? c with { PrincipalLearnedAt = old.ReceivedAt, LegacyOutcome = old.ResolvedAt != null } : c).ToArray();
        }
    }
}
