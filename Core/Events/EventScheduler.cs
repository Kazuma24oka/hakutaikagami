namespace HakutaiKagami.Core.Events;

public sealed record ScheduledEvent(string Id, long EffectiveAt, IReadOnlyList<string> DependsOn);
// Foundation only: no effects are applied here. Ambiguous conflicts must be resolved by future policy.
public sealed class EventScheduler
{
    private readonly Dictionary<string, ScheduledEvent> pending = new(StringComparer.Ordinal);
    public IReadOnlyCollection<ScheduledEvent> Pending => pending.Values.ToArray();
    public void Schedule(ScheduledEvent item)
    {
        if (string.IsNullOrWhiteSpace(item.Id) || item.EffectiveAt < 0 || item.DependsOn.Contains(item.Id))
            throw new ArgumentException("Invalid event identity, time or self dependency.");
        if (!pending.TryAdd(item.Id, item with { DependsOn = item.DependsOn.ToArray() }))
            throw new InvalidOperationException("Duplicate logical event ID.");
    }
    public IReadOnlyList<ScheduledEvent> Ready(long now, IReadOnlySet<string> applied) =>
        pending.Values.Where(e => e.EffectiveAt <= now && !applied.Contains(e.Id) && e.DependsOn.All(applied.Contains))
            .OrderBy(e => e.EffectiveAt).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
}
