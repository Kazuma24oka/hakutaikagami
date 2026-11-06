using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class HouseConditionTests
{
    [Fact]
    public void LocalControlAndReadBooksAreDistinctFromTheExistenceOfASurvey()
    {
        var s=LandSurveyTests.Start();
        EntityId owner=new("org:oda"),site=new("site:castle");
        HouseConditionView Report()=>HouseCondition.Observe(s.DiagnosticSnapshot(),s.PlayerControl,owner)!;
        Assert.True(Report().ControlsLocalSite);
        Assert.Null(HouseCondition.Observe(s.DiagnosticSnapshot(),s.PlayerControl,new("org:takeda")));
        var order=new SurveyInput(new("request:house-survey"),SurveyAction.Measurement,owner,site);
        Assert.Equal(ResultCode.Accepted,s.ManageLandSurvey(order,s.Revision).Code);
        NewGameWorldTests.At(s,7*86400);
        Assert.Empty(Report().KnownSurveys);
        Assert.Equal(ResultCode.Accepted,s.ManageLandSurvey(order with{Id=new("request:read-survey"),Action=SurveyAction.ReadBook,Reference=order.Id},s.Revision).Code);
        var known=Assert.Single(Report().KnownSurveys);
        Assert.Equal(site,known.Site);Assert.True(known.Book.Measured);
        var restored=new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot()));
        Assert.Equal(ContractJson.Write(Report()),ContractJson.Write(HouseCondition.Observe(restored.DiagnosticSnapshot(),restored.PlayerControl,owner)!));
    }
}
