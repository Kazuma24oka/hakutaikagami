using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class ImagawaStartTests
{
    [Fact]
    public void RegentEditionRetainsItsOriginalScenario()
    {
        var scenario = NewGameWorld.Scenario(CentralCourtCatalog.RegentEdition);
        Assert.Equal("4be334853afe5184b54ffaaaa434d05c2d025c0d4c29896c47f849f63e92204e", ContractJson.CanonicalHash(scenario));
    }
}