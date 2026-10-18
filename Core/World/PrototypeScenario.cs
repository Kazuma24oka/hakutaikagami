using System.Text.Json;
using HakutaiKagami.Core.Characters;
using HakutaiKagami.Core.Castles;
using HakutaiKagami.Core.Commands;

namespace HakutaiKagami.Core.World;

public static class PrototypeScenario
{
    public static WorldState Load(string json)
    {
        var definition = JsonSerializer.Deserialize<ScenarioDefinition>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new ArgumentException("Missing scenario.");
        if (definition.Characters.Length != 3 || definition.Castles.Length != 1 || definition.Organizations.Length != 1)
            throw new ArgumentException("This foundation fixture requires 3 characters, 1 castle and 1 organization.");
        string castle = definition.Castles[0].Id, org = definition.Organizations[0].Id;
        var characters = definition.Characters.Select((c, i) => new Character(c, org, new[] { "城主", "奉行", "家臣" }[i], castle)).ToArray();
        return new WorldState(definition, characters, [new Castle(definition.Castles[0], characters[0].Id, org)],
            [new Authority(characters[0].Id, castle, "inspect", "試作の明示的閲覧委任", 0, long.MaxValue)],
            [new ResourceStock(org, castle, 100, 50)]);
    }
}
