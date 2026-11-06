using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class CentralMediationTests
{
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
    [Theory][InlineData(false)][InlineData(true)]
    public void PetitionRequiresMeetingAndBrokerAnswersOnceAfterRestore(bool gift)
    {
        var broker=CentralCourtCatalog.CourtBroker;var site=CentralCourtCatalog.Kyoto;var owner=new EntityId("org:court-test");
        var applicant=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);
        var raw=NewGameWorld.Scenario(CentralCourtCatalog.Id);
        var request=new WorldSetupRequest([applicant],[new(applicant.Id,owner.Value,[site.Value],false)]);
        var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var s=new WorldSession(NewGameWorld.Create(setup,[applicant],applicant.Id,historicalCatalog:CentralCourtCatalog.Id,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy));
        var ask=new CourtInput(new("request:petition"),CourtAction.AskCourtIntroduction,broker,owner,site);
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(ask,s.Revision).Code);
        var visit=s.ManageSocial(new(new("request:visit-courtier"),SocialWorldAction.Visit,broker),s.Revision);
        Assert.True(visit.Code==ResultCode.Accepted,visit.VisibleReason);
        if(gift){
            var offered=s.ManageSocial(new(new("request:court-gift"),SocialWorldAction.DispatchGift,broker,Amount:100),s.Revision);
            Assert.True(offered.Code==ResultCode.Accepted,offered.VisibleReason);NewGameWorldTests.At(s,s.Seconds+3);
        }
        var petition=s.ManageCourt(ask,s.Revision);Assert.True(petition.Code==ResultCode.Accepted,petition.VisibleReason);
        var saved=s.DiagnosticSnapshot();Assert.DoesNotContain(saved.Court!.Events,e=>e.Input.Reference==ask.Id);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(saved)));
        foreach(var run in new[]{s,restored}){
            NewGameWorldTests.At(run,run.Seconds+2);var w=run.DiagnosticSnapshot();
            var reply=Assert.Single(w.Court!.Events,e=>e.Input.Reference==ask.Id);
            Assert.Equal(gift?CourtAction.AcceptCourtIntroduction:CourtAction.DeclineCourtIntroduction,reply.Input.Action);
            Assert.Equal(broker,reply.Actor);Assert.Equal(run.PlayerControl,reply.Input.Person);
            Assert.Equal(ResultCode.AlreadyAccepted,run.ManageCourt(ask,run.Revision).Code);
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
}
