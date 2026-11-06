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
    [Theory]
    [InlineData(CourtAction.SupportRestoration)]
    [InlineData(CourtAction.ReserveRestoration)]
    public void PersonalReplyPreservesServiceAndCannotBeRepeated(CourtAction choice)
    {
        var head = NewGameWorldTests.Officer(role: StartTemplate.Daimyo);
        var officer = NewGameWorldTests.Officer("created:former", StartTemplate.Retainer, lord: head.Id);
        EntityId house = new("org:former"), site = new(head.Site), proposer = new(officer.Id), peer = new(head.Id);
        var setup = NewGameWorldTests.Setup([head, officer], [new(head.Id, house.Value, [head.Site], false)]);
        var world = NewGameWorld.Create(setup.World, setup.Registered, officer.Id, 42,
            inspectionPolicy: LocalInspectionFindings.Policy, workPolicy: WorkFollowup.Policy, economyPolicy: HouseSuccession.Policy);
        world.ElapsedSeconds = 100;
        world.LandRights = world.LandRights.Select(r => r.Subject == house && r.Target == site ? r with { Until = 50 } : r).ToArray();
        var bonds = ContractJson.CanonicalHash(world.ServiceBonds);
        var offer = new CourtInput(new("request:support"), CourtAction.SeekRestorationSupport, peer, house, site);
        world.CommitSequence++;
        HouseholdCourt.Execute(world, offer, proposer);
        world = ContractJson.Copy(world);
        var answer = new CourtInput(new("request:answer"), choice, proposer, house, site, Reference: offer.Id);
        world.CommitSequence++;
        HouseholdCourt.Execute(world, answer, peer);
        Assert.Equal(bonds, ContractJson.CanonicalHash(world.ServiceBonds));
        world.CommitSequence++;
        Assert.Throws<InvalidDataException>(() => HouseholdCourt.Execute(world, answer with { Id = new("request:duplicate") }, peer));
    }
}
