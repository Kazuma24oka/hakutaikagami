using HakutaiKagami.Core.World;
using HakutaiKagami.Core.Commands;
using HakutaiKagami.Core.Events;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class FoundationTests
{
    private static string Json => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prototype.json"));
    private static WorldState Create() => PrototypeScenario.Load(Json);
    [Fact] public void InitializesThreeCharactersOneCastleAndResources()
    {
        var w = Create(); Assert.Equal(3, w.Characters.Count); Assert.Single(w.Castles);
        Assert.Equal(0, w.Tick); Assert.Empty(w.Events.Pending);
        Assert.Equal(100, Assert.Single(w.Resources).Rice); w.Validate();
    }
    [Fact] public void CharacterAndCastleIdsAreUnique()
    {
        var w = Create(); Assert.Equal(3, w.Definition.Characters.Select(c => c.Id).Distinct().Count());
        Assert.Single(w.Definition.Castles.Select(c => c.Id).Distinct());
    }
    [Fact] public void DuplicateCharacterDefinitionIsRejected() =>
        Assert.Throws<ArgumentException>(() => PrototypeScenario.Load(Json.Replace("character-002", "character-001")));
    [Fact] public void DuplicateCastleDefinitionIsRejected()
    {
        var w = Create(); var d = w.Definition with { Castles = [w.Definition.Castles[0], w.Definition.Castles[0]] };
        Assert.Throws<ArgumentException>(() => new WorldState(d, w.Characters.Values, w.Castles.Values, w.Authorities, w.Resources));
    }
    [Fact] public void AllReferencesResolve()
    {
        var w = Create(); Assert.All(w.Characters.Values, c => Assert.True(w.Castles.ContainsKey(c.CastleId)));
        Assert.All(w.Castles.Values, c => Assert.True(w.Characters.ContainsKey(c.LordId)));
    }
    [Fact] public void BrokenCharacterReferenceIsRejected()
    {
        var w = Create(); w.Characters.Values.First().CastleId = "missing";
        Assert.Throws<ArgumentException>(w.Validate);
    }
    [Fact] public void BrokenLordReferenceIsRejected()
    {
        var w = Create(); w.Castles.Values.Single().LordId = "missing";
        Assert.Throws<ArgumentException>(w.Validate);
    }
    [Fact] public void IdentitySurvivesAffiliationAndLordChanges()
    {
        var w = Create(); var c = w.Characters.Values.First(); var id = c.Id;
        c.OrganizationId = "future-organization"; c.Role = "浪人"; Assert.Equal(id, c.Id);
        var castle = w.Castles.Values.Single(); var castleId = castle.Id;
        castle.LordId = w.Characters.Values.Last().Id; Assert.Equal(castleId, castle.Id);
    }
    [Fact] public void SameSeedReproducesSequenceAndWorldIsIndependent()
    {
        var a = Create(); var b = Create();
        Assert.Equal(20261018UL, a.Definition.Seed);
        Assert.Equal(Enumerable.Range(0, 100).Select(_ => a.Random.Next()).ToArray(), Enumerable.Range(0, 100).Select(_ => b.Random.Next()).ToArray());
        a.Characters.Values.First().Role = "変更"; Assert.Equal("城主", b.Characters.Values.First().Role);
    }
    [Fact] public void DifferentSeedsProduceDifferentSequences() => Assert.NotEqual(new SeededRandom(1).Next(), new SeededRandom(2).Next());
    [Fact] public void AuthorityChecksActorTargetActionAndLifetime()
    {
        var w = Create(); var a = Assert.Single(w.Authorities);
        var request = new CommandRequest("request-001", a.ActorId, a.TargetCastleId, a.ActionId, 0);
        Assert.True(a.Allows(request, 0)); Assert.False(a.Allows(request with { ActorId = "character-002" }, 0));
        Assert.False(a.Allows(request with { TargetCastleId = "other" }, 0));
        Assert.False(a.Allows(request with { ActionId = "tax" }, 0));
        Assert.False(a.Allows(request, -1)); Assert.False(a.Allows(request, long.MaxValue));
    }
    [Fact] public void EventIdentityAndCausalityArePreserved()
    {
        var scheduler = new EventScheduler();
        scheduler.Schedule(new ScheduledEvent("arrival", 1, []));
        scheduler.Schedule(new ScheduledEvent("response", 1, ["arrival"]));
        Assert.Throws<InvalidOperationException>(() => scheduler.Schedule(new ScheduledEvent("arrival", 1, [])));
        Assert.Empty(scheduler.Ready(0, new HashSet<string>()));
        Assert.Equal("arrival", Assert.Single(scheduler.Ready(1, new HashSet<string>())).Id);
        Assert.Equal("response", Assert.Single(scheduler.Ready(1, new HashSet<string> { "arrival" })).Id);
    }
    [Fact] public void CoreHasNoGodotAssemblyDependency() =>
        Assert.DoesNotContain(typeof(WorldState).Assembly.GetReferencedAssemblies(), a => a.Name!.Contains("Godot"));
}
