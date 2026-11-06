using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class CentralMediationTests
{
    [Fact]
    public void ShogunAndHisAttendantAreSeparatePeopleWithNoAutomaticKyotoTerritory()
    {
        var edition=HistoricalStartCatalog.Current;
        var scenario=NewGameWorld.Scenario(edition);
        var prior=NewGameWorld.Scenario(CentralCourtCatalog.Id);
        Assert.Equal(prior.Castles,scenario.Castles);
        Assert.DoesNotContain(prior.People,p=>p.Id==CentralCourtCatalog.Shogun.Value);
        var w=NewGameWorld.Create(scenario,[],CentralCourtCatalog.ShogunBroker.Value,historicalCatalog:edition);
        Assert.Contains(w.ServiceBonds,b=>b.Subject==CentralCourtCatalog.ShogunBroker && b.Target==CentralCourtCatalog.Shogun);
        Assert.DoesNotContain(w.LandRights,r=>r.Subject==CentralCourtCatalog.Shogun);
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(new WorldSession(w).DiagnosticSnapshot()));
    }

    [Fact]
    public void KyotoCourtierCanEnterTheWorldWithoutOwningKyotoOrAnArmy()
    {
        var prior=NewGameWorld.Scenario(HistoricalHouseholdCatalog.Id);
        var scenario=NewGameWorld.Scenario(CentralCourtCatalog.Id);
        var id=CentralCourtCatalog.CourtBroker;
        Assert.DoesNotContain(prior.People,p=>p.Id==id.Value);
        var person=Assert.Single(scenario.People,p=>p.Id==id.Value);
        Assert.Equal(1507,person.BirthYear);Assert.Equal(CentralCourtCatalog.Kyoto.Value,person.Site);
        Assert.Equal(prior.Castles,scenario.Castles);Assert.Equal(prior.Factions,scenario.Factions);
        var state=NewGameWorld.Create(scenario,[],id.Value,historicalCatalog:CentralCourtCatalog.Id);
        Assert.DoesNotContain(state.LandRights,right=>right.Subject==id);
        Assert.DoesNotContain(state.Units,u=>u.Commander==id);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(state)));
        Assert.Equal(id,restored.PlayerControl);
        Assert.Equal(ContractJson.Write(state),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
    [Fact]
    public void ShogunMustReadRecommendationAndApplicantMustOpenSeparateAppointment()
    {
        var broker=CentralCourtCatalog.ShogunBroker;var site=CentralCourtCatalog.Kyoto;var kofu=new EntityId("site:kofu");
        var owner=new EntityId("org:shugo-test");var applicant=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);
        var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var request=new WorldSetupRequest([applicant],[new(applicant.Id,owner.Value,[site.Value,kofu.Value],false)]);
        var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var s=new WorldSession(NewGameWorld.Create(setup,[applicant],applicant.Id,historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy));
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        Good(s.ManageSocial(new(new("request:meet-fujitaka"),SocialWorldAction.Visit,broker),s.Revision));
        Good(s.ManageSocial(new(new("request:fujitaka-gift"),SocialWorldAction.DispatchGift,broker,Amount:100),s.Revision));
        NewGameWorldTests.At(s,s.Seconds+3);
        var input=new CourtInput(new("request:kai-shugo"),CourtAction.AskShugo,broker,owner,kofu);
        Good(s.ManageCourt(input,s.Revision));NewGameWorldTests.At(s,s.Seconds+1);
        var w=s.DiagnosticSnapshot();
        Assert.Single(w.Messaging.Messages,m=>m.Kind==ShogunateAppointments.Petition && m.RecipientId==CentralCourtCatalog.Shogun.Value);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        foreach(var run in new[]{s,restored}){
            NewGameWorldTests.At(run,run.Seconds+6);var end=run.DiagnosticSnapshot();
            var record=Assert.Single(end.Court!.Events,e=>e.Input.Action==CourtAction.AppointShugo);
            Assert.Equal(CentralCourtCatalog.Shogun,record.Actor);
            Assert.Empty(ShogunateAppointments.KnownOffices(end,run.PlayerControl));
            var letter=Assert.Single(end.Messaging.Messages,m=>m.Kind==ShogunateAppointments.Reply);
            Good(run.Open(run.PlayerControl,new(letter.Id),run.Revision));
            Assert.Equal("甲斐守護",Assert.Single(ShogunateAppointments.KnownOffices(run.DiagnosticSnapshot(),run.PlayerControl)));
            Assert.Null(CentralRank.KnownRank(run.DiagnosticSnapshot(),run.PlayerControl));
            Assert.Equal(ContractJson.Write(w.LandRights),ContractJson.Write(run.DiagnosticSnapshot().LandRights));
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }

    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(true,true)][InlineData(false,true)]
    public void PetitionRequiresMeetingAndBrokerAnswersOnceAfterRestore(bool gift,bool currentEdition)
    {
        var broker=CentralCourtCatalog.CourtBroker;var site=CentralCourtCatalog.Kyoto;var owner=new EntityId("org:court-test");
        var applicant=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);
        var edition=currentEdition?HistoricalStartCatalog.Current:CentralCourtCatalog.Id;
        var raw=NewGameWorld.Scenario(edition);
        var request=new WorldSetupRequest([applicant],[new(applicant.Id,owner.Value,[site.Value],false)]);
        var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var s=new WorldSession(NewGameWorld.Create(setup,[applicant],applicant.Id,historicalCatalog:edition,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy));
        var ask=new CourtInput(new("request:petition"),CourtAction.AskCourtIntroduction,broker,owner,site);
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(ask,s.Revision).Code);
        var visit=s.ManageSocial(new(new("request:visit-courtier"),SocialWorldAction.Visit,broker),s.Revision);
        Assert.True(visit.Code==ResultCode.Accepted,visit.VisibleReason);
        if(gift){
            var offered=s.ManageSocial(new(new("request:court-gift"),SocialWorldAction.DispatchGift,broker,Amount:100),s.Revision);
            Assert.True(offered.Code==ResultCode.Accepted,offered.VisibleReason);NewGameWorldTests.At(s,s.Seconds+3);
        }
        var expectedBalance=NewGameWork.Balance(s.DiagnosticSnapshot(),s.PlayerControl)-(currentEdition?CentralRank.Offering:0);
        var petition=s.ManageCourt(ask,s.Revision);Assert.True(petition.Code==ResultCode.Accepted,petition.VisibleReason);
        Assert.Equal(expectedBalance,NewGameWork.Balance(s.DiagnosticSnapshot(),s.PlayerControl));
        var saved=s.DiagnosticSnapshot();Assert.DoesNotContain(saved.Court!.Events,e=>e.Input.Reference==ask.Id);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(saved)));
        foreach(var run in new[]{s,restored}){
            NewGameWorldTests.At(run,run.Seconds+2);var w=run.DiagnosticSnapshot();
            var reply=Assert.Single(w.Court!.Events,e=>e.Input.Reference==ask.Id);
            Assert.Equal(gift?CourtAction.AcceptCourtIntroduction:CourtAction.DeclineCourtIntroduction,reply.Input.Action);
            Assert.Equal(broker,reply.Actor);Assert.Equal(run.PlayerControl,reply.Input.Person);
            Assert.Equal(ResultCode.AlreadyAccepted,run.ManageCourt(ask,run.Revision).Code);
            Assert.Equal(expectedBalance,NewGameWork.Balance(run.DiagnosticSnapshot(),run.PlayerControl));
        }
        if(gift){
            foreach(var run in new[]{s,restored}){
                NewGameWorldTests.At(run,run.Seconds+2);
                var before=run.DiagnosticSnapshot();
                var filed=Assert.Single(before.Court!.Events,e=>e.Input.Action==CourtAction.FileRankPetition);
                var date=WorldCalendar.At(before.EpochDate,filed.At);
                var delay=currentEdition?(long)(date.AddMonths(2)-date).TotalSeconds:86400;
                NewGameWorldTests.At(run,filed.At+delay-1);
                Assert.DoesNotContain(run.DiagnosticSnapshot().Court!.Events,e=>e.Input.Action==CourtAction.IssueCourtRank);
                NewGameWorldTests.At(run,filed.At+delay);
                var issued=run.DiagnosticSnapshot();
                Assert.Single(issued.Court!.Events,e=>e.Input.Action==CourtAction.IssueCourtRank);
                Assert.Null(CentralRank.KnownRank(issued,run.PlayerControl));
                var letter=Assert.Single(issued.Messaging.Messages,m=>m.Kind==CentralRank.ReplyKind);
                NewGameWorldTests.At(run,run.Seconds+3);
                Assert.Null(CentralRank.KnownRank(run.DiagnosticSnapshot(),run.PlayerControl));
                var read=run.Open(run.PlayerControl,new(letter.Id),run.Revision);
                Assert.True(read.Code==ResultCode.Accepted,read.VisibleReason);
                Assert.Equal(CentralRank.RankName,CentralRank.KnownRank(run.DiagnosticSnapshot(),run.PlayerControl));
                Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(run.DiagnosticSnapshot().LandRights));
            }
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
}
