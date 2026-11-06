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
        w.CommitSequence++;
        var release = new CourtInput(new("request:release"), CourtAction.ReleaseRestorationEscort, lord, sponsor, site,
            Reference: accept.Id, When: accept.When);
        Assert.Throws<InvalidDataException>(() => HouseholdCourt.Execute(w, release, lord));
        Assert.Throws<InvalidDataException>(() => HouseholdCourt.Execute(w, release with { Site = new("site:kofu") }, heir));
        var balances = (HouseSalary.PrivateRice(w, heir), HouseSalary.PrivateRice(w, lord));
        HouseholdCourt.Execute(w, release, heir);
        var afterRelease = ContractJson.Copy(w);
        afterRelease.CommitSequence++;
        Assert.Throws<InvalidDataException>(() => RestorationEscort.Muster(afterRelease, accept.Id));
        Assert.Throws<InvalidDataException>(() => HouseholdCourt.Execute(afterRelease, release with { Id = new("request:release-twice") }, heir));
        Assert.Equal(balances, (HouseSalary.PrivateRice(afterRelease, heir), HouseSalary.PrivateRice(afterRelease, lord)));

    }
    [Fact]
    public void EscortMustersOnlyOnceUnderSponsorCommandAndReturnsItsOwnGarrison()
    {
        var setup = NewGameWorldTests.Setup([]);
        var world = NewGameWorld.Create(setup.World, [], "person:shingen", worldArmyPolicy: NpcArmies.Policy);
        EntityId sponsor = new("org:oda"), commander = new("person:nobunaga"), site = new("site:castle");
        var accept = new CourtInput(new("request:escort-accepted"), CourtAction.AcceptRestorationEscort,
            commander, sponsor, site, When: 3600);
        world.CommitSequence++;
        world.Court = new(1, HouseholdCourt.Policy, [new(accept, world.PlayerControl, site, 0, world.CommitSequence, false)]);
        world.CommitSequence++;
        RestorationEscort.Muster(world, accept.Id);
        var muster = Assert.Single(NpcArmies.Musters(world));
        Assert.Equal((commander, sponsor, site), (muster.Commander, muster.Owner, muster.Site));
        Assert.Equal(100, world.Units.Where(u => u.Army == Assert.Single(NpcArmies.Armies(world)).Id).Sum(u => u.Soldiers));
        Assert.Throws<InvalidDataException>(() => RestorationEscort.Muster(world, accept.Id));
        world = ContractJson.Copy(world);
        world.CommitSequence++;
        world.ElapsedSeconds = accept.When;
        Assert.Throws<InvalidDataException>(() => RestorationEscort.Muster(world, accept.Id));
        NpcArmies.Return(world, muster.Id);
        Assert.Equal(muster.Id, Assert.Single(world.NpcArmies!.Returns).Muster);
    }
    [Fact]
    public void EscortHomeJourneyIsBoundToTheHeadHomeAndOriginalAgreement()
    {
        var setup = NewGameWorldTests.Setup([]);
        var w = NewGameWorld.Create(setup.World, [], "person:shingen", worldArmyPolicy: NpcArmies.Policy);
        EntityId commander = new("person:nobunaga"), site = new("site:fork");
        var agreement = new CourtInput(new("request:escort-away"), CourtAction.AcceptRestorationEscort,
            commander, new("org:oda"), site, When: 86400);
        w.CommitSequence++;
        var original = new CourtEvent(agreement, w.PlayerControl, site, 0, w.CommitSequence, false);
        w.Court = new(1, HouseholdCourt.Policy, [original]);
        w.Locations = w.Locations.Select(l => l.Actor == commander ? l with { Site = site } : l).ToArray();
        var journey = RestorationEscort.PlanHomeJourney(w, original);
        Assert.Equal(new EntityId("site:castle"), journey.DestinationSite);
        Assert.Equal(commander, journey.Executor);
        Assert.True(journey.WorkSeconds > 0);
        Assert.False(RestorationEscort.IsHomeJourney(w, journey with { DestinationSite = site }));
        Assert.False(RestorationEscort.IsHomeJourney(w, journey with { RequestId = new("request:another") }));
        w.ElapsedSeconds = agreement.When;
        Assert.Throws<InvalidDataException>(() => RestorationEscort.PlanHomeJourney(w, original));
    }
    [Theory]
    [InlineData(true, "site:castle", true)]
    [InlineData(false, "site:castle", false)]
    [InlineData(true, "site:kofu", false)]
    public void ChildCanUsePersonallyWitnessedSeatOfParentWhoWasHead(bool childOfHead, string start, bool eligible)
    {
        var head = NewGameWorldTests.Officer(role: StartTemplate.Daimyo) with { BirthYear = 1490 };
        var retainer = NewGameWorldTests.Officer("created:retainer", StartTemplate.Retainer, lord: head.Id) with { BirthYear = 1490 };
        var child = NewGameWorldTests.Officer("created:child", site: start) with { BirthParents = [childOfHead ? head.Id : retainer.Id] };
        EntityId house = new("org:lost"), site = new(head.Site), observer = new(child.Id);
        var setup = NewGameWorldTests.Setup([head, retainer, child], [new(head.Id, house.Value, [head.Site], false)]);
        var world = NewGameWorld.Create(setup.World, setup.Registered, child.Id, 42);
        world.ElapsedSeconds = 100;
        world.LandRights = world.LandRights.Select(r => r.Subject == house && r.Target == site ? r with { Until = 50 } : r).ToArray();
        world.Locations = world.Locations.Select(l => l.Actor == observer ? l with { Site = site } : l).ToArray();
        Assert.Equal(eligible, HouseRestoration.ObserveFormerSeat(world, observer).Any(r => r.House == house));
        Assert.DoesNotContain(world.Memberships, m => m.Subject == observer && m.Target == house);
    }
}
