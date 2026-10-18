namespace HakutaiKagami.Core.Messaging;

public enum AttemptStatus { InTransit, Lost, Rejected, Delivered }
public sealed record MessageEnvelope(string Id, string CorrelationId, string Kind, string SenderId, string RecipientId,
    string Body, string Scope, long CreatedAt, long? DeliverBy = null);
public sealed record MessageAttempt(string Id, string MessageId, int Number, long SentAt, long DueAt, string ReceiverId,
    AttemptStatus Status = AttemptStatus.InTransit, bool FailOnArrival = false, long? FinishedAt = null,
    long? FailureAt = null, long? LateDueAt = null, bool ArrivedLate = false);
public sealed record FormalReceipt(string MessageId, string PrincipalId, string ReceiverId, long ReceivedAt, bool ByProxy);
public sealed record PersonalKnowledge(string MessageId, string CharacterId, long LearnedAt, string EvidenceMessageId);
public sealed record ProxyPermission(string Id, string PrincipalId, string AgentId, string Scope, string Kind,
    string Basis, long ValidFrom, long ValidUntil, bool Receive, bool Open);
public sealed class MessageState
{
    public MessageEnvelope[] Messages { get; set; } = [];
    public MessageAttempt[] Attempts { get; set; } = [];
    public FormalReceipt[] Receipts { get; set; } = [];
    public PersonalKnowledge[] Knowledge { get; set; } = [];
    public ProxyPermission[] Proxies { get; set; } = [];
}

// No tax, castle, cancellation or renderer dependency. Adapters own authorization and business effects.
public static class MessageFlow
{
    public const int MaxAttempts = 3;
    public static bool Knows(MessageState s, string message, string character) => s.Knowledge.Any(k => k.MessageId == message && k.CharacterId == character);
    public static long? LearnedAt(MessageState s, string message, string character) => s.Knowledge.SingleOrDefault(k => k.MessageId == message && k.CharacterId == character)?.LearnedAt;
    public static int Pending(MessageState s) => s.Attempts.Count(a => a.Status == AttemptStatus.InTransit || a.Status == AttemptStatus.Lost && a.LateDueAt != null);
    public static MessageAttempt Latest(MessageState s, string id) => s.Attempts.Where(a => a.MessageId == id).MaxBy(a => a.Number) ?? throw new InvalidOperationException("No message attempt.");
    public static void Send(MessageState s, MessageEnvelope message, long tick, int delay = 1)
    {
        var old = s.Messages.SingleOrDefault(m => m.Id == message.Id);
        if (old != null)
        {
            if (old with { CreatedAt = message.CreatedAt } != message) throw new InvalidOperationException("Logical message ID payload conflict.");
            return;
        }
        if (delay < 1 || message.CreatedAt != tick) throw new ArgumentException("Positive delay and current creation time required.");
        s.Messages = [.. s.Messages, message];
        s.Attempts = [.. s.Attempts, new(message.Id + "/attempt/1", message.Id, 1, tick, checked(tick + delay), message.RecipientId)];
    }
    public static void RouteTo(MessageState s, string messageId, string receiver)
    {
        var attempt = Latest(s, messageId);
        if (attempt.Status != AttemptStatus.InTransit) throw new InvalidOperationException("Route applies only to an in-flight attempt.");
        s.Attempts = s.Attempts.Select(a => a.Id == attempt.Id ? a with { ReceiverId = receiver } : a).ToArray();
    }
    public static void Fail(MessageState s, string messageId)
    {
        var attempt = Latest(s, messageId);
        if (attempt.Status != AttemptStatus.InTransit) throw new InvalidOperationException("Attempt is not in transit.");
        s.Attempts = s.Attempts.Select(a => a.Id == attempt.Id ? a with { FailOnArrival = true } : a).ToArray();
    }
    public static void ReportFailure(MessageState s, string reportId, string attemptId, string courier, long tick, int delay = 1)
    {
        var attempt = s.Attempts.Single(a => a.Id == attemptId);
        if (attempt.FailureAt == null || attempt.FailureAt > tick) throw new InvalidOperationException("No observed delivery failure.");
        var message = s.Messages.Single(m => m.Id == attempt.MessageId);
        if (s.Messages.Count(m => m.Kind == "delivery-failure" && m.Body == attemptId) >= MaxAttempts && !s.Messages.Any(m => m.Id == reportId))
            throw new InvalidOperationException("Failure report copy limit reached.");
        Send(s, new(reportId, message.CorrelationId, "delivery-failure", courier, message.SenderId, attemptId, message.Scope, tick), tick, delay);
    }
    public static bool FailureKnown(MessageState s, string attemptId, string sender) =>
        s.Messages.Any(m => m.Kind == "delivery-failure" && m.Body == attemptId && m.RecipientId == sender && Knows(s, m.Id, sender));
    public static void Retry(MessageState s, string messageId, string sender, long tick, int delay = 1)
    {
        var message = s.Messages.Single(m => m.Id == messageId); var previous = Latest(s, messageId);
        if (message.SenderId != sender || previous.Status is not (AttemptStatus.Lost or AttemptStatus.Rejected) ||
            !FailureKnown(s, previous.Id, sender) || previous.Number >= MaxAttempts || s.Receipts.Any(r => r.MessageId == messageId) || delay < 1 || message.DeliverBy < tick + delay)
            throw new InvalidOperationException("Retry requires a known failed latest attempt, no receipt, and a valid deadline.");
        int next = previous.Number + 1;
        s.Attempts = [.. s.Attempts, new(message.Id + "/attempt/" + next, message.Id, next, tick, checked(tick + delay), message.RecipientId)];
    }
    public static void ScheduleLate(MessageState s, string messageId, int number, long tick, int delay)
    {
        var attempt = s.Attempts.Single(a => a.MessageId == messageId && a.Number == number);
        if (attempt.Status != AttemptStatus.Lost || attempt.LateDueAt != null || delay < 1) throw new InvalidOperationException("Only one late arrival for a lost attempt.");
        s.Attempts = s.Attempts.Select(a => a.Id == attempt.Id ? a with { LateDueAt = checked(tick + delay) } : a).ToArray();
    }
    private static bool Permits(MessageState s, MessageEnvelope m, string agent, long tick, bool open) => s.Proxies.Any(p =>
        p.PrincipalId == m.RecipientId && p.AgentId == agent && p.Scope == m.Scope && p.Kind == m.Kind &&
        !string.IsNullOrWhiteSpace(p.Basis) && p.ValidFrom <= tick && tick < p.ValidUntil && (open ? p.Open : p.Receive));
    private static void Learn(MessageState s, string message, string character, long tick, string evidence)
    { if (!Knows(s, message, character)) s.Knowledge = [.. s.Knowledge, new(message, character, tick, evidence)]; }
    public static void OpenAsProxy(MessageState s, string messageId, string agent, long tick)
    {
        var m = s.Messages.Single(m => m.Id == messageId);
        if (!s.Attempts.Any(a => a.MessageId == messageId && a.ReceiverId == agent && a.Status == AttemptStatus.Delivered) || !Permits(s, m, agent, tick, true))
            throw new UnauthorizedAccessException("No current opening permission for the received message.");
        Learn(s, messageId, agent, tick, messageId);
    }
    public static void Forward(MessageState s, string messageId, string agent, string forwardId, long tick, int delay = 1)
    {
        var original = s.Messages.Single(m => m.Id == messageId);
        if (agent == original.RecipientId || !Knows(s, messageId, agent)) throw new UnauthorizedAccessException("Agent has not learned the content.");
        Send(s, new(forwardId, original.CorrelationId, "forward", agent, original.RecipientId, messageId, original.Scope, tick), tick, delay);
    }
    public static void Advance(MessageState s, long tick)
    {
        foreach (var original in s.Attempts.Where(a => a.Status == AttemptStatus.InTransit && a.DueAt <= tick ||
            a.Status == AttemptStatus.Lost && a.LateDueAt <= tick).OrderBy(a => a.Id, StringComparer.Ordinal).ToArray())
        {
            var message = s.Messages.Single(m => m.Id == original.MessageId); bool late = original.Status == AttemptStatus.Lost;
            var attempt = original;
            if (!late && attempt.FailOnArrival) attempt = attempt with { Status = AttemptStatus.Lost, FinishedAt = tick, FailureAt = tick };
            else if (message.DeliverBy < tick || attempt.ReceiverId != message.RecipientId && !Permits(s, message, attempt.ReceiverId, tick, false))
                attempt = attempt with { Status = AttemptStatus.Rejected, FinishedAt = tick, FailureAt = attempt.FailureAt ?? tick };
            else
            {
                attempt = attempt with { Status = AttemptStatus.Delivered, FinishedAt = tick, ArrivedLate = late };
                if (!s.Receipts.Any(r => r.MessageId == message.Id)) s.Receipts = [.. s.Receipts, new(message.Id, message.RecipientId, attempt.ReceiverId, tick, attempt.ReceiverId != message.RecipientId)];
                bool canRead = attempt.ReceiverId == message.RecipientId || Permits(s, message, attempt.ReceiverId, tick, true);
                if (canRead)
                {
                    Learn(s, message.Id, attempt.ReceiverId, tick, message.Id);
                    if (message.Kind == "forward") Learn(s, message.Body, attempt.ReceiverId, tick, message.Id);
                }
            }
            s.Attempts = s.Attempts.Select(a => a.Id == attempt.Id ? attempt : a).ToArray();
        }
    }
    public static void Validate(MessageState s, long tick, IReadOnlySet<string> characters)
    {
        static void Check(bool ok, string text) { if (!ok) throw new InvalidDataException(text); }
        Check(s.Messages.Select(m => m.Id).Distinct().Count() == s.Messages.Length && s.Attempts.Select(a => a.Id).Distinct().Count() == s.Attempts.Length, "Duplicate message/attempt ID.");
        Check(s.Messages.All(m => !string.IsNullOrWhiteSpace(m.Id) && !string.IsNullOrWhiteSpace(m.Kind) && !string.IsNullOrWhiteSpace(m.Scope) &&
            characters.Contains(m.SenderId) && characters.Contains(m.RecipientId) && m.CreatedAt >= 0 && m.CreatedAt <= tick && (m.DeliverBy == null || m.DeliverBy >= m.CreatedAt)), "Invalid message identity/time.");
        Check(s.Proxies.Select(p => p.Id).Distinct().Count() == s.Proxies.Length && s.Proxies.All(p => characters.Contains(p.PrincipalId) && characters.Contains(p.AgentId) &&
            p.PrincipalId != p.AgentId && p.ValidFrom >= 0 && p.ValidUntil >= p.ValidFrom && !string.IsNullOrWhiteSpace(p.Basis)), "Invalid proxy grant.");
        foreach (var m in s.Messages)
        {
            var attempts = s.Attempts.Where(a => a.MessageId == m.Id).OrderBy(a => a.Number).ToArray();
            Check(attempts.Length is >= 1 and <= MaxAttempts && attempts.Select(a => a.Number).SequenceEqual(Enumerable.Range(1, attempts.Length)), "Invalid attempt sequence.");
            Check(attempts[0].SentAt == m.CreatedAt, "Initial attempt creation mismatch.");
            foreach (var a in attempts)
            {
                Check(a.Id == m.Id + "/attempt/" + a.Number && characters.Contains(a.ReceiverId) && Enum.IsDefined(a.Status) && a.SentAt >= m.CreatedAt && a.SentAt <= tick && a.DueAt > a.SentAt &&
                    (a.Status == AttemptStatus.InTransit ? a.DueAt > tick && a.FinishedAt == null : a.FinishedAt != null && a.FinishedAt <= tick && a.FinishedAt >= a.DueAt) &&
                    (a.LateDueAt == null || a.FailureAt != null && a.LateDueAt > a.FailureAt) &&
                    (a.Status != AttemptStatus.Lost || a.FailureAt != null && (a.LateDueAt == null || a.LateDueAt > tick)) &&
                    (a.FailureAt == null || a.FailureAt >= a.DueAt && a.FailureAt <= tick) &&
                    (!a.ArrivedLate || a.Status == AttemptStatus.Delivered && a.FinishedAt == a.LateDueAt), "Invalid physical attempt.");
                if (a.Number > 1)
                {
                    var previous = attempts[a.Number - 2];
                    Check(previous.FailureAt <= a.SentAt && s.Messages.Any(f => f.Kind == "delivery-failure" && f.Body == previous.Id &&
                        f.RecipientId == m.SenderId && s.Knowledge.Any(k => k.MessageId == f.Id && k.CharacterId == m.SenderId && k.LearnedAt <= a.SentAt)), "Retry before personal failure recognition.");
                }
            }
        }
        Check(s.Attempts.All(a => s.Messages.Any(m => m.Id == a.MessageId)), "Unknown attempt parent.");
        Check(s.Receipts.Select(r => r.MessageId).Distinct().Count() == s.Receipts.Length && s.Receipts.All(r => s.Messages.Any(m => m.Id == r.MessageId && m.RecipientId == r.PrincipalId) &&
            r.ByProxy == (r.ReceiverId != r.PrincipalId) && s.Attempts.Any(a => a.MessageId == r.MessageId && a.ReceiverId == r.ReceiverId && a.Status == AttemptStatus.Delivered && a.FinishedAt == r.ReceivedAt)), "Invalid formal receipt.");
        Check(s.Knowledge.Select(k => (k.MessageId, k.CharacterId)).Distinct().Count() == s.Knowledge.Length && s.Knowledge.All(k => k.LearnedAt >= 0 && k.LearnedAt <= tick &&
            s.Messages.Any(m => m.Id == k.MessageId) && s.Attempts.Any(a => a.MessageId == k.EvidenceMessageId && a.ReceiverId == k.CharacterId && a.Status == AttemptStatus.Delivered && a.FinishedAt <= k.LearnedAt) &&
            (k.EvidenceMessageId == k.MessageId || s.Messages.Any(m => m.Id == k.EvidenceMessageId && m.Kind == "forward" && m.Body == k.MessageId && m.RecipientId == k.CharacterId))), "Knowledge without personal reception.");
        foreach (var m in s.Messages.Where(m => m.Kind == "forward"))
            Check(s.Messages.Any(o => o.Id == m.Body && o.RecipientId == m.RecipientId) && s.Knowledge.Any(k => k.MessageId == m.Body && k.CharacterId == m.SenderId && k.LearnedAt <= m.CreatedAt), "Forwarded unknown content.");
        foreach (var m in s.Messages.Where(m => m.Kind == "delivery-failure"))
            Check(s.Attempts.Any(a => a.Id == m.Body && a.FailureAt <= m.CreatedAt && s.Messages.Any(o => o.Id == a.MessageId && o.SenderId == m.RecipientId)), "Failure report without observed failure.");
    }
}
