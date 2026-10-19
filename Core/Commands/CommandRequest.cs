namespace HakutaiKagami.Core.Commands;

public sealed record CommandRequest(string RequestId, string ActorId, string TargetCastleId, string ActionId, long RequestedAt);
// A role title alone confers no permission. Grants have explicit actor, target, basis and lifetime.
public sealed record Authority(string ActorId, string TargetCastleId, string ActionId, string Basis, long ValidFrom, long ValidUntil)
{
    public bool Allows(CommandRequest request, long now) =>
        !string.IsNullOrWhiteSpace(Basis) && ActorId == request.ActorId &&
        TargetCastleId == request.TargetCastleId && ActionId == request.ActionId &&
        now >= ValidFrom && now < ValidUntil;
}
