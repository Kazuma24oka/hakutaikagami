namespace HakutaiKagami.Core.Castles;

public sealed record CastleDefinition(string Id, string Name);
// Office, effective control and ownership are separate; this prototype does not transfer them.
public sealed class Castle(CastleDefinition definition, string lordId, string controllerId)
{
    public string Id => definition.Id;
    public string LordId { get; set; } = lordId;
    public string ControllerOrganizationId { get; set; } = controllerId;
}
public sealed record ResourceStock(string OwnerOrganizationId, string CustodyCastleId, int Rice, int Gold);
