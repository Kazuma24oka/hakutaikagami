using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class SupremeOfficeTests
{
    [Fact]
    public void CandidateAnswersTheSponsorAfterRestoreWithoutTakingIncumbentsOffice()
    {
        var site=CentralCourtCatalog.Kyoto;
        var sponsor=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);
        var house=new EntityId("org:kyoto-sponsor");
        var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var request=new WorldSetupRequest([sponsor],[new(sponsor.Id,house.Value,[site.Value],false)]);
        var scenario=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var game=new WorldSession(NewGameWorld.Create(scenario,[sponsor],sponsor.Id,historicalCatalog:HistoricalStartCatalog.Current,
            workPolicy:WorkFollowup.Policy,inspectionPolicy:LocalInspectionFindings.Policy));
        var candidate=CentralCourtCatalog.ShogunBroker;
        var offer=new CourtInput(new("request:sponsor"),CourtAction.OfferShogunSupport,candidate,house,site);
        var result=game.ManageCourt(offer,game.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(game.DiagnosticSnapshot())));
        foreach(var run in new[]{game,restored}){
            NewGameWorldTests.At(run,run.Seconds+2);
            var w=run.DiagnosticSnapshot();
            Assert.Single(w.Court!.Events,e=>e.Input.Action==CourtAction.AcceptShogunSupport && e.Actor==candidate && e.Input.Reference==offer.Id);
            Assert.Equal(CentralCourtCatalog.Shogun,SupremeOffice.ShogunAt(w,run.Seconds,run.Revision));
            Assert.Equal(ResultCode.NotAvailable,run.ManageCourt(offer with{Id=new("request:duplicate-offer")},run.Revision).Code);
        }
        Assert.Equal(ContractJson.Write(game.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
        var expel=new CourtInput(new("request:expel"),CourtAction.ExpelShogun,CentralCourtCatalog.Shogun,house,TempleMilitary.Site);
        var ordered=game.ManageCourt(expel,game.Revision);Assert.True(ordered.Code==ResultCode.Accepted,ordered.VisibleReason);
        Assert.Equal(CentralCourtCatalog.Shogun,SupremeOffice.ShogunAt(game.DiagnosticSnapshot(),game.Seconds,game.Revision));
        NewGameWorldTests.At(game,game.Seconds+1);NewGameWorldTests.At(game,game.Seconds+2);
        var moving=game.DiagnosticSnapshot();
        var trip=Assert.Single(moving.Branches!.Journeys,j=>j.Actor==CentralCourtCatalog.Shogun);
        Assert.Null(SupremeOffice.ShogunAt(moving,game.Seconds,game.Revision));
        var travellingSave=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(moving)));
        foreach(var run in new[]{game,travellingSave}){
            NewGameWorldTests.At(run,trip.Legs[^1].EndAt+1);
            Assert.Equal(TempleMilitary.Site,SocialWorld.Site(run.DiagnosticSnapshot(),CentralCourtCatalog.Shogun));
            NewGameWorldTests.At(run,run.Seconds+2);
            var sent=run.DiagnosticSnapshot();
            var letter=Assert.Single(sent.Messaging.Messages,m=>m.Kind==ExileLetters.Kind);
            var mission=sent.Couriers!.Missions.Single(m=>sent.Messaging.Attempts.Any(a=>a.Id==m.Attempt && a.MessageId==letter.Id));
            Assert.Equal("kyoto",mission.Destination);
            Assert.Equal(ResultCode.NotAvailable,run.Open(run.PlayerControl,new(letter.Id),run.Revision).Code);
            NewGameWorldTests.At(run,mission.ReadyAt+1);
            Assert.Equal(ResultCode.Accepted,run.Open(run.PlayerControl,new(letter.Id),run.Revision).Code);
            var receipt=run.DiagnosticSnapshot();
            Assert.Null(SupremeOffice.ShogunAt(receipt,run.Seconds,run.Revision));
            Assert.Contains(run.GetActorView(run.PlayerControl).Messages,m=>m.Id.Value==letter.Id && m.KnownBody==letter.Body);
            _=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(receipt)));
        }
        Assert.Equal(ContractJson.Write(game.DiagnosticSnapshot()),ContractJson.Write(travellingSave.DiagnosticSnapshot()));
    }
    [Fact]
    public void IncumbentCanResignOnceAndVacancySurvivesSave()
    {
        var actor=CentralCourtCatalog.Shogun;
        var scenario=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var game=new WorldSession(NewGameWorld.Create(scenario,[],actor.Value,historicalCatalog:HistoricalStartCatalog.Current,
            workPolicy:WorkFollowup.Policy,inspectionPolicy:LocalInspectionFindings.Policy));
        var before=game.Revision;
        var input=new CourtInput(new("request:resign-shogun"),CourtAction.ResignShogun,actor,SupremeOffice.Bakufu,CentralCourtCatalog.Kyoto);
        var result=game.ManageCourt(input,game.Revision);
        Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(game.DiagnosticSnapshot())));
        var state=restored.DiagnosticSnapshot();
        Assert.Equal(actor,SupremeOffice.ShogunAt(state,game.Seconds,before));
        Assert.Null(SupremeOffice.ShogunAt(state,game.Seconds,restored.Revision));
        Assert.Equal(ResultCode.AlreadyAccepted,restored.ManageCourt(input,restored.Revision).Code);
        Assert.Equal(ResultCode.NotAvailable,restored.ManageCourt(input with{Id=new("request:again")},restored.Revision).Code);
    }
}