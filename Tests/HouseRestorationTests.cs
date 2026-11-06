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
        var pledge = new CourtInput(new("request:rice"), CourtAction.PledgeRestorationRice, proposer, house, site,
            Amount: 1, Reference: answer.Id);
        if (choice == CourtAction.ReserveRestoration)
        {
            Assert.Throws<InvalidDataException>(() => HouseholdCourt.Execute(world, pledge, peer));
            return;
        }
        HouseholdCourt.Execute(world, pledge, peer);
        Assert.Equal(0, HouseSalary.PrivateRice(world, proposer));
        world = ContractJson.Copy(world);
        world.CommitSequence++;
        var delivery = pledge with { Id = new("request:deliver"), Action = CourtAction.HandOverRestorationRice, Reference = pledge.Id };
        Assert.Throws<InvalidDataException>(() => HouseholdCourt.Execute(world, delivery, peer));
        // この局所テストの開始残高は、既存の徴収米受領記録から用意する。
        var receipt = new CourtInput(new("request:prior-rice"), CourtAction.ReceiveLevyShare, peer, house, site, Amount: 2);
        world.Court = world.Court! with { Events = [..world.Court.Events, new(receipt, peer, site, world.ElapsedSeconds, world.CommitSequence, false)] };
        world.CommitSequence++;
        HouseholdCourt.Execute(world, delivery, peer);
        Assert.Equal(1, HouseSalary.PrivateRice(world, proposer));
        Assert.Equal(1, HouseSalary.PrivateRice(world, peer));
        world.CommitSequence++;
        Assert.Throws<InvalidDataException>(() => HouseholdCourt.Execute(world, delivery with { Id = new("request:repeat-rice") }, peer));
    }
}
