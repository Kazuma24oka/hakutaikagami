using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class SupremeOfficeTests
{
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