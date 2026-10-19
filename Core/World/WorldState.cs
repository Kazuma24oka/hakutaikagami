using HakutaiKagami.Core.Characters;
using HakutaiKagami.Core.Castles;
using HakutaiKagami.Core.Commands;
using HakutaiKagami.Core.Events;

namespace HakutaiKagami.Core.World;

public sealed record OrganizationDefinition(string Id, string Name);
public sealed record ScenarioDefinition(int SchemaVersion, ulong Seed, OrganizationDefinition[] Organizations,
    CharacterDefinition[] Characters, CastleDefinition[] Castles);

public sealed class WorldState
{
    public ScenarioDefinition Definition { get; }
    public long Tick { get; } // A session supplies the common tick; no wall-clock policy lives here.
    public SeededRandom Random { get; }
    public IReadOnlyDictionary<string, Character> Characters { get; }
    public IReadOnlyDictionary<string, Castle> Castles { get; }
    public IReadOnlyList<Authority> Authorities { get; }
    public IReadOnlyList<ResourceStock> Resources { get; }
    public EventScheduler Events { get; } = new();

    public WorldState(ScenarioDefinition definition, IEnumerable<Character> characters, IEnumerable<Castle> castles,
        IEnumerable<Authority> authorities, IEnumerable<ResourceStock> resources, long tick = 0, ulong? randomState = null)
    {
        Definition = definition;
        Tick = tick;
        Random = new SeededRandom(randomState ?? definition.Seed);
        Characters = characters.ToDictionary(c => c.Id, StringComparer.Ordinal);
        Castles = castles.ToDictionary(c => c.Id, StringComparer.Ordinal);
        Authorities = Array.AsReadOnly(authorities.ToArray());
        Resources = Array.AsReadOnly(resources.ToArray());
        Validate();
    }

    public void Validate()
    {
        static void Check(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
        var characterIds = Definition.Characters.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var castleIds = Definition.Castles.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var orgIds = Definition.Organizations.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        Check(Tick >= 0, "Negative world tick.");
        Check(Definition.SchemaVersion == 1, "Unsupported scenario version.");
        Check(characterIds.Count == Definition.Characters.Length && castleIds.Count == Definition.Castles.Length &&
            orgIds.Count == Definition.Organizations.Length, "Duplicate definition ID.");
        Check(!characterIds.Concat(castleIds).Concat(orgIds).Any(string.IsNullOrWhiteSpace), "Empty ID.");
        Check(characterIds.SetEquals(Characters.Keys) && castleIds.SetEquals(Castles.Keys), "Definition/state mismatch.");
        foreach (var c in Characters.Values)
            Check(orgIds.Contains(c.OrganizationId) && Castles.ContainsKey(c.CastleId), "Broken character reference.");
        foreach (var c in Castles.Values)
            Check(Characters.ContainsKey(c.LordId) && orgIds.Contains(c.ControllerOrganizationId), "Broken castle reference.");
        foreach (var a in Authorities)
            Check(Characters.ContainsKey(a.ActorId) && Castles.ContainsKey(a.TargetCastleId) &&
                !string.IsNullOrWhiteSpace(a.Basis) && a.ValidFrom >= 0 && a.ValidUntil > a.ValidFrom, "Invalid authority.");
        foreach (var r in Resources)
            Check(orgIds.Contains(r.OwnerOrganizationId) && Castles.ContainsKey(r.CustodyCastleId) &&
                r.Rice >= 0 && r.Gold >= 0, "Invalid resource stock.");
    }
}
