using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class ImagawaStartTests
{
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