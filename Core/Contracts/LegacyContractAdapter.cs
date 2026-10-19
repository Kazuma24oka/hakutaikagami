using HakutaiKagami.Core.Simulation;

namespace HakutaiKagami.Core.Contracts;

public sealed record LegacyRelation(string CharacterId, string OrganizationId, string Role, string CastleId);
public sealed record LegacyAuthorityRow(string Actor,string Target,string Action,string Basis,long FromTick,long UntilTick);
public sealed record LegacyTaxRow(string Id,string Issuer,string Executor,string Castle,int Requested,int Reserved,int Collected,string Phase,long? ExecuteByTick);
public sealed record LegacyArmyRow(string Id,string Organization,string Commander);
public sealed record LegacyUnitRow(string Id,string Army,string Commander,int CellX,int CellZ,int Soldiers,string Phase,string? ActiveOrder);
public sealed record LegacyMilitaryOrderRow(string Id,string Issuer,string Recipient,string Unit,string Action,string Status,long SentTick,long EffectiveTick,long? ExecuteByTick);
public sealed record LegacyMilitaryProjection(string MapId,string Rules,long LegacyTick,LegacyArmyRow[] Armies,
    LegacyUnitRow[] Units,LegacyMilitaryOrderRow[] Orders,int PersonalIntelRows,int SharedTransportMessages);
public sealed record LegacyCompatibilityReport(string TimeDomain, int Version, long LegacyTick,
    LegacyRelation[] Characters, int Grants, int Orders, int AppliedEffects, int Messages, ResultCode ProductImport,
    string Explanation,LegacyAuthorityRow[] Authorities,LegacyTaxRow[] TaxOrders,LegacyMilitaryProjection? Military);
public static class LegacyContractAdapter
{
    // Deliberately a read-only projection, never a lossy migration or second source of truth.
    public static LegacyCompatibilityReport Inspect(TaxSimulation legacy)
    {
        var s = HakutaiKagami.Core.Common.SnapshotCodec.Read<TaxSnapshot>(legacy.SaveJson());
        var military=s.Officers?.Military;
        var projection=military==null?null:new LegacyMilitaryProjection(military.MapId,military.Rules,military.Tick,
            military.Armies.Select(a=>new LegacyArmyRow(a.Id,a.OrganizationId,a.CommanderId)).ToArray(),
            military.Units.Select(u=>new LegacyUnitRow(u.Id,u.ArmyId,u.CommanderId,u.Cell.X,u.Cell.Z,u.Soldiers,u.Phase.ToString(),u.ActiveOrder)).ToArray(),
            military.Orders.Select(o=>new LegacyMilitaryOrderRow(o.Id,o.IssuerId,o.RecipientId,o.UnitId,o.Action.ToString(),o.Status.ToString(),o.SentAt,o.EffectiveAt,o.ExecuteBy)).ToArray(),
            military.Intel.Length,s.Messaging.Messages.Count(m=>m.Kind.StartsWith("military-",StringComparison.Ordinal)));
        return new("LegacyPrototypeTicks",s.Version,s.Tick,
            s.World.Characters.Select(c=>new LegacyRelation(c.Id,c.OrganizationId,c.Role,c.CastleId)).ToArray(),
            s.Grants.Length,s.Orders.Length,s.Applied.Length,s.Messaging.Messages.Length,ResultCode.MigrationRequired,
            "No registered seconds/geography mapping. Continue in the legacy entry point; source was not modified.",
            s.Grants.Select(g=>new LegacyAuthorityRow(g.ActorId,g.TargetCastleId,g.ActionId,g.Basis,g.ValidFrom,g.ValidUntil)).ToArray(),
            s.Orders.Select(o=>new LegacyTaxRow(o.RequestId,o.IssuerId,o.ExecutorId,o.CastleId,o.Requested,o.Reserved,o.Collected,o.Phase.ToString(),o.ExecuteBy)).ToArray(),projection);
    }
}
