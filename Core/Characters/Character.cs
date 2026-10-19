namespace HakutaiKagami.Core.Characters;

public sealed record CharacterDefinition(string Id, string Name);
// Identity belongs to the definition; mutable affiliation and office never generate a new ID.
public sealed class Character(CharacterDefinition definition, string organizationId, string role, string castleId)
{
    public string Id => definition.Id;
    public string OrganizationId { get; set; } = organizationId;
    public string Role { get; set; } = role;
    public string CastleId { get; set; } = castleId;
}
