using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class ImagawaStartTests
{
    [Theory]
    [InlineData("person:imagawa-yoshimoto")]
    [InlineData("person:imagawa-ujizane")]
    public void PlacementKeepsBothPeopleSelectableAndOnlyAssignsSunpu(string head)
    {
        var facts=ImagawaPersonnel.Read();
        var prior=NewGameWorld.Scenario(CentralCourtCatalog.RegentEdition);
        var placement=facts.StartingPlacement(prior,new(head));
        var request=new WorldSetupRequest([],[]);
        var confirmed=NewGameSetup.Confirm(placement,request,NewGameSetup.Preview(placement,request));
        var restored=ContractJson.Read<SetupScenario>(ContractJson.Write(confirmed));
        Assert.Equal(ContractJson.Write(confirmed),ContractJson.Write(restored));
        Assert.Equal("site:sunpu",Assert.Single(restored.Castles,c=>c.Faction==facts.Faction.Value).Id);

        foreach(var person in facts.People){
            var candidate=restored.People.Single(p=>p.Id==person.Id.Value);
            Assert.True(NewGameWorld.CanPlay(restored,candidate));
            Assert.Equal("site:sunpu",candidate.Site);
            Assert.Equal(facts.Faction.Value,candidate.Faction);
            Assert.Equal(person.Id.Value==head?null:head,candidate.Lord);
        }
        Assert.Equal(head,restored.Factions.Single(f=>f.Id==facts.Faction.Value).Head);
    }
    [Theory]
    [InlineData("person:imagawa-yoshimoto")]
    [InlineData("person:imagawa-ujizane")]
    public void InitialHeirAndAuthoritySurviveRestore(string player)
    {
        var scenario = NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var request = new WorldSetupRequest([], []);
        var confirmed = NewGameSetup.Confirm(scenario, request, NewGameSetup.Preview(scenario, request));
        var world = NewGameWorld.Create(confirmed, [], player, historicalCatalog: HistoricalStartCatalog.Current);
        var bytes = ContractJson.Write(world);
        var restored = ContractJson.Read<ContractWorld>(bytes);
        ContractValidation.Validate(restored);
        Assert.Equal(bytes, ContractJson.Write(restored));
        var paper = Assert.Single(restored.HeirNominations!);
        Assert.Equal("person:imagawa-ujizane", paper.Heir.Value);
        Assert.Equal("person:imagawa-yoshimoto", paper.Head.Value);
        Assert.NotNull(FactionStances.ResolveAuthority(restored, paper.Head, paper.House, 0, restored.CommitSequence));
        Assert.Null(FactionStances.ResolveAuthority(restored, paper.Heir, paper.House, 0, restored.CommitSequence));
    }

    [Fact]
    public void DocumentedFamilySharesAnExistingHomeCandidate()
    {
        var facts = ImagawaPersonnel.Read();
        Assert.Equal(1560, facts.WorldYear);
        Assert.Contains(NationalRoutes.Nodes, n=>n.Id==facts.HomeCandidate);
        var father = Assert.Single(facts.People, p=>p.Father==null);
        var son = Assert.Single(facts.People, p=>p.Father==father.Id);
        Assert.Equal(("今川義元",1519), (father.Name,father.BirthYear));
        Assert.Equal(("今川氏真",1538), (son.Name,son.BirthYear));
        var sources=facts.Sources.ToDictionary(s=>s.Id);
        foreach(var id in facts.People.SelectMany(p=>p.SourceIds).Append(facts.HomeSource))
            Assert.Equal("https",new Uri(sources[id].Url).Scheme);
    }
    [Fact]
    public void RegentEditionRetainsItsOriginalScenario()
    {
        var scenario = NewGameWorld.Scenario(CentralCourtCatalog.RegentEdition);
        Assert.Equal("4be334853afe5184b54ffaaaa434d05c2d025c0d4c29896c47f849f63e92204e", ContractJson.CanonicalHash(scenario));
    }
}