using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class HouseRestorationTests
{
    [Fact]
    public void LossIsVisibleOnlyWhereTheObserverPersonallyServed()
    {
        var head = NewGameWorldTests.Officer(role: StartTemplate.Daimyo);
        var former = NewGameWorldTests.Officer("created:former", StartTemplate.Retainer, lord: head.Id);
        EntityId house = new("org:former"), person = new(former.Id), site = new(head.Site);
        var setup = NewGameWorldTests.Setup([head, former], [new(head.Id, house.Value, [head.Site], false)]);
        var world = NewGameWorld.Create(setup.World, setup.Registered, head.Id, 42);
        Assert.Empty(HouseRestoration.ObserveFormerSeat(world, person));
        world.ElapsedSeconds = 100;
        world.LandRights = world.LandRights.Select(row => row.Subject == house && row.Target == site
            ? row with { Until = 50 } : row).ToArray();
        var seen = Assert.Single(HouseRestoration.ObserveFormerSeat(world, person));
        Assert.Equal(new FormerSeatObservation(house, site, 100), seen);
        world.Memberships = world.Memberships.Where(row => row.Subject != person).ToArray();
        Assert.Empty(HouseRestoration.ObserveFormerSeat(world, person));
    }
}
