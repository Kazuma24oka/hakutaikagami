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
    [Fact]
    public void EscortRequiresCurrentSponsorAuthorityAndIdenticalUnexpiredTerms()
    {
        var leader = NewGameWorldTests.Officer(role: StartTemplate.Daimyo);
        var applicant = NewGameWorldTests.Officer("created:claimant");
        EntityId sponsor = new("org:sponsor"), oldHouse = new("org:old"), site = new(leader.Site);
        EntityId lord = new(leader.Id), heir = new(applicant.Id);
        var setup = NewGameWorldTests.Setup([leader, applicant], [new(leader.Id, sponsor.Value, [leader.Site], false)]);
        var w = NewGameWorld.Create(setup.World, setup.Registered, applicant.Id, 42,
            inspectionPolicy: LocalInspectionFindings.Policy, workPolicy: WorkFollowup.Policy, economyPolicy: HouseSuccession.Policy);
        var support = new CourtInput(new("request:personal"), CourtAction.SupportRestoration, heir, oldHouse, site);
        w.CommitSequence++;
        w.Court = new(1, HouseholdCourt.Policy, [new(support, lord, site, 0, w.CommitSequence, false)]);
        w.CommitSequence++;
        var offer = new CourtInput(new("request:escort"), CourtAction.OfferRestorationEscort, heir, sponsor, site,
            Reference: support.Id, When: 86400);
        Assert.Throws<InvalidDataException>(() => HouseholdCourt.Execute(w, offer, heir));
        HouseholdCourt.Execute(w, offer, lord);
        w = ContractJson.Copy(w);
        w.CommitSequence++;
        var accept = new CourtInput(new("request:agree"), CourtAction.AcceptRestorationEscort, lord, sponsor, site,
            Reference: offer.Id, When: offer.When);
        Assert.Throws<InvalidDataException>(() => HouseholdCourt.Execute(w, accept with { When = offer.When + 1 }, heir));
        var expired = ContractJson.Copy(w);
        expired.ElapsedSeconds = offer.When;
        Assert.Throws<InvalidDataException>(() => HouseholdCourt.Execute(expired, accept, heir));
        HouseholdCourt.Execute(w, accept, heir);
        Assert.Empty(NpcArmies.Musters(w));
    }
}
