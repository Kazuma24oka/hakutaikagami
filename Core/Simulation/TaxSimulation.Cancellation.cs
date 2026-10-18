using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Simulation;

public enum CancellationStatus { InTransit, AwaitingEffect, Applied, AlreadyStopped, TooLate, Unauthorized, Lost, DeliveryRejected }
public sealed record CancellationCommand(string Id, string RequestId, string SenderId, string RecipientId,
    long SentAt, long DueAt, long EffectiveAt, CancellationStatus Status = CancellationStatus.InTransit,
    bool Delivered = false, bool Lost = false, bool FailOnArrival = false, long? LateDueAt = null,
    long? ReceivedAt = null, long? ResolvedAt = null, long? PrincipalLearnedAt = null, bool LegacyOutcome = false);

public sealed partial class TaxSimulation
{
    public IReadOnlyList<CancellationCommand> Cancellations => Array.AsReadOnly(state.Cancellations.ToArray());
    private static bool IsActive(TaxOrder o) => o.Phase is TaxPhase.Dispatched or TaxPhase.Received or TaxPhase.Reserved;

    public void DispatchCancellation(string id, string requestId, string senderId, int delay = 1, long? effectiveAt = null)
    {
        var previous = state.Cancellations.SingleOrDefault(c => c.Id == id);
        if (previous != null)
        {
            if (previous.RequestId != requestId || previous.SenderId != senderId || previous.DueAt - previous.SentAt != delay ||
                previous.EffectiveAt != (effectiveAt ?? previous.DueAt)) throw new InvalidOperationException("Cancellation ID payload conflict.");
            return;
        }
        Mutate(s =>
        {
            if (string.IsNullOrWhiteSpace(id) || delay < 1) throw new ArgumentException("Cancellation ID and positive delivery delay required.");
            var order = s.Orders.Single(o => o.RequestId == requestId);
            if (senderId != order.IssuerId || !Authorized(s, senderId, order.CastleId, "cancel-tax"))
                throw new UnauthorizedAccessException("No cancellation grant for this issuer and target.");
            long due = checked(s.Tick + delay), effective = effectiveAt ?? due;
            if (effective < due) throw new ArgumentException("Announced effect cannot precede planned receipt.");
            s.Cancellations = [.. s.Cancellations, new(id, requestId, senderId, order.ExecutorId, s.Tick, due, effective)];
            MessageFlow.Send(s.Messaging, new(id + "/command", id, "cancel-command", senderId, order.ExecutorId,
                requestId, order.CastleId, s.Tick), s.Tick, delay);
        });
    }
    public void FailCancellation(string id) => Mutate(s =>
    {
        var c = s.Cancellations.Single(c => c.Id == id);
        if (c.Status != CancellationStatus.InTransit) throw new InvalidOperationException("Cancellation is no longer in transit.");
        s.Cancellations = s.Cancellations.Select(c => c.Id == id ? c with { FailOnArrival = true } : c).ToArray();
        MessageFlow.Fail(s.Messaging, id + "/command");
    });
    public void ScheduleLateCancellation(string id, int delay) => Mutate(s =>
    {
        var c = s.Cancellations.Single(c => c.Id == id);
        if (c.Status != CancellationStatus.Lost || c.LateDueAt != null || delay < 1) throw new InvalidOperationException("Only a lost cancellation can be scheduled late once.");
        s.Cancellations = s.Cancellations.Select(c => c.Id == id ? c with { LateDueAt = checked(s.Tick + delay) } : c).ToArray();
        MessageFlow.ScheduleLate(s.Messaging, id + "/command", 1, s.Tick, delay);
    });

    // Trial policy: inclusive completion deadline, then effective cancellations, then ordinary arrivals/work.
    private static void ApplyDeadlinesAndCancellations(TaxSnapshot s)
    {
        foreach (var order in s.Orders.Where(o => IsActive(o) && o.ExecuteBy < s.Tick).ToArray())
        {
            StopUncommitted(s, order, TaxPhase.Expired, "履行期限を超過");
            s.Applied = [.. s.Applied, order.RequestId + "/deadline"];
        }
        var activeAtCancellationBoundary = s.Orders.Where(IsActive).Select(o => o.RequestId).ToHashSet(StringComparer.Ordinal);
        foreach (var original in s.Cancellations.ToArray())
        {
            var c = original;
            var receipt = s.Messaging.Receipts.SingleOrDefault(r => r.MessageId == c.Id + "/command");
            var latest = MessageFlow.Latest(s.Messaging, c.Id + "/command");
            if (!c.Delivered && receipt != null)
            {
                c = c with { Delivered = true, ReceivedAt = receipt.ReceivedAt, Status = CancellationStatus.AwaitingEffect };
                s.Applied = [.. s.Applied, c.Id + "/received"];
            }
            else if (!c.Delivered && latest.Status is AttemptStatus.Lost or AttemptStatus.Rejected)
                c = c with { Status = latest.Status == AttemptStatus.Lost ? CancellationStatus.Lost : CancellationStatus.DeliveryRejected };
            if (!c.Lost && s.Messaging.Attempts.Any(a => a.MessageId == c.Id + "/command" && a.FailureAt != null))
            {
                c = c with { Lost = true }; s.Applied = [.. s.Applied, c.Id + "/lost"];
            }
            c = c with { PrincipalLearnedAt = MessageFlow.LearnedAt(s.Messaging, c.Id + "/command", c.RecipientId) };
            if (c.Status == CancellationStatus.AwaitingEffect && c.PrincipalLearnedAt != null && c.EffectiveAt <= s.Tick)
            {
                var order = s.Orders.Single(o => o.RequestId == c.RequestId);
                var result = !Authorized(s, c.SenderId, order.CastleId, "cancel-tax") ? CancellationStatus.Unauthorized :
                    IsActive(order) || order.Phase == TaxPhase.Cancelled && order.StoppedAt == s.Tick && activeAtCancellationBoundary.Contains(order.RequestId) ? CancellationStatus.Applied :
                    order.Collected > 0 ? CancellationStatus.TooLate : CancellationStatus.AlreadyStopped;
                if (result == CancellationStatus.Applied && IsActive(order)) StopUncommitted(s, order, TaxPhase.Cancelled, "到達した取消命令が有効化");
                c = c with { Status = result, ResolvedAt = s.Tick };
                s.Applied = [.. s.Applied, c.Id + "/effective"];
                SendCancellationReply(s, c);
            }
            s.Cancellations = s.Cancellations.Select(x => x.Id == c.Id ? c : x).ToArray();
        }
    }
    private static void StopUncommitted(TaxSnapshot s, TaxOrder order, TaxPhase phase, string reason)
    {
        // Physical arrivals remain in flight and can be recorded later, but they cannot restart this order.
        s.Pending = s.Pending.Where(e => e.RequestId != order.RequestId || e.Kind is not (TaxEventKind.Decide or TaxEventKind.Collect)).ToArray();
        var stopped = order with { Phase = phase, Reserved = 0, StoppedAt = s.Tick, Reason = reason };
        Replace(s, stopped);
        QueueReport(s, stopped, []);
    }
    private static bool IsDeadlineOrCancellationMarker(TaxSnapshot s, string id) =>
        s.Orders.Any(o => o.Phase == TaxPhase.Expired && id == o.RequestId + "/deadline") ||
        s.Cancellations.Any(c => id == c.Id + "/received" || id == c.Id + "/effective" || id == c.Id + "/lost");

    private static void ValidateDeadlinesAndCancellations(TaxSnapshot s)
    {
        static void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        foreach (var o in s.Orders)
        {
            Check(o.ExecuteBy is null or >= 0 && (o.ExecuteBy == null || !IsActive(o) || o.ExecuteBy >= s.Tick), "Active order past completion deadline.");
            Check(o.StoppedAt == null || o.Phase is TaxPhase.Cancelled or TaxPhase.Expired && o.StoppedAt >= 0 && o.StoppedAt <= s.Tick, "Invalid stop time.");
            Check(o.Phase != TaxPhase.Expired || o.ExecuteBy != null && o.StoppedAt == o.ExecuteBy + 1 && s.Applied.Contains(o.RequestId + "/deadline"), "Expiry evidence missing.");
            Check(o.CollectedAt == null ? o.Collected == 0 || s.MigrationOrigin != null && o.ExecuteBy == null :
                o.Collected > 0 && o.CollectedAt >= 0 && o.CollectedAt <= s.Tick && (o.ExecuteBy == null || o.CollectedAt <= o.ExecuteBy), "Collection committed outside its deadline.");
        }
        Check(s.Cancellations.Select(c => c.Id).Distinct().Count() == s.Cancellations.Length, "Duplicate cancellation ID.");
        foreach (var c in s.Cancellations)
        {
            var order = s.Orders.Single(o => o.RequestId == c.RequestId);
            Check(!string.IsNullOrWhiteSpace(c.Id) && Enum.IsDefined(c.Status) && c.SenderId == order.IssuerId && c.RecipientId == order.ExecutorId &&
                c.SentAt >= 0 && c.SentAt <= s.Tick && c.DueAt > c.SentAt && c.EffectiveAt >= c.DueAt, "Invalid cancellation identity/time.");
            var receipt = s.Messaging.Receipts.SingleOrDefault(r => r.MessageId == c.Id + "/command");
            var latest = MessageFlow.Latest(s.Messaging, c.Id + "/command");
            Check(c.Delivered == (c.ReceivedAt != null) && c.Delivered == (receipt != null) && c.ReceivedAt == receipt?.ReceivedAt &&
                c.Delivered == s.Applied.Contains(c.Id + "/received") && c.PrincipalLearnedAt == MessageFlow.LearnedAt(s.Messaging, c.Id + "/command", c.RecipientId), "Cancellation receipt/recognition mismatch.");
            Check(c.Lost == s.Applied.Contains(c.Id + "/lost") && c.Lost == s.Messaging.Attempts.Any(a => a.MessageId == c.Id + "/command" && a.FailureAt != null) &&
                (c.LateDueAt == null || c.Lost && c.LateDueAt > c.DueAt), "Cancellation loss/late mismatch.");
            bool resolved = c.Status is CancellationStatus.Applied or CancellationStatus.AlreadyStopped or CancellationStatus.TooLate or CancellationStatus.Unauthorized;
            Check(resolved == (c.ResolvedAt != null) && resolved == s.Applied.Contains(c.Id + "/effective") &&
                (!resolved || c.Delivered && c.PrincipalLearnedAt != null && c.ResolvedAt == Math.Max(c.EffectiveAt, c.PrincipalLearnedAt.Value) && c.ResolvedAt <= s.Tick), "Cancellation effect mismatch.");
            Check(c.Status != CancellationStatus.InTransit || !c.Delivered && latest.Status == AttemptStatus.InTransit, "Stale cancellation transit.");
            Check(c.Status != CancellationStatus.AwaitingEffect || c.Delivered && (c.PrincipalLearnedAt == null || c.EffectiveAt > s.Tick), "Stale pending effect.");
            Check(c.Status != CancellationStatus.Lost || c.Lost && !c.Delivered && latest.Status == AttemptStatus.Lost, "Stale lost cancellation.");
            Check(c.Status != CancellationStatus.DeliveryRejected || !c.Delivered && latest.Status == AttemptStatus.Rejected, "Stale rejected cancellation.");
            Check(c.Status != CancellationStatus.Applied || order.Phase == TaxPhase.Cancelled && order.StoppedAt == c.ResolvedAt, "Applied cancellation did not stop order.");
            Check(c.Status != CancellationStatus.TooLate || order.Collected > 0, "Too-late outcome without committed collection.");
            Check(!c.LegacyOutcome || s.MigrationOrigin != null && resolved, "Invalid imported outcome.");
            Check(!resolved || c.LegacyOutcome || s.Messaging.Messages.Any(m => m.Id == c.Id + "/reply" && m.Body == c.Status.ToString()), "Missing cancellation reply.");
        }
    }
}
