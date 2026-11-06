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

    [Fact]
    public void ConsultationSurvivesTransitAndDoesNotConveyProperty()
    {
        var s=HouseholdCourtTests.Start();
        var w=s.DiagnosticSnapshot();
        var input=new CourtInput(new("request:market-consultation"),CourtAction.ConsultReform,
            HouseholdCourtTests.Retainer,HouseholdCourtTests.Owner,HouseholdCourtTests.Site){Reform=ReformKind.OpenMarket};
        var result=s.ManageCourt(input,s.Revision);
        Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        Assert.Equal(ResultCode.AlreadyAccepted,s.ManageCourt(input,s.Revision).Code);
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(input with{Id=new("request:another-consultation")},s.Revision).Code);
        s.SetPaused(true);
        var dir=Directory.CreateTempSubdirectory("hakutai-consultation-");
        try
        {
            var store=new GenerationSaveStore(dir.FullName,w.Definitions);
            Assert.True(store.Save(s,"sent",w.WorldId).Published);
            var host=new WorldSessionHost(new WorldSession(w));
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("sent"),host.Current,host.Current.Revision));
            NewGameWorldTests.At(host.Current,120);
            NewGameWorldTests.At(s,120);
            var after=host.Current.DiagnosticSnapshot();
            Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(after));
            Assert.Single(after.Messaging.Messages,m=>m.Id==HouseReforms.Letter(input,HouseReforms.Answer));
            Assert.False(HouseReforms.AnswerKnown(after,after.Court!.Events.Single(e=>e.Input.Id==input.Id),s.PlayerControl));
            Assert.Equal(ContractJson.Write(w.LandRights),ContractJson.Write(after.LandRights));
            var defer=input with{Action=CourtAction.DeferReform,Reform=null,Id=new("request:defer"),Reference=input.Id};
            Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(defer,s.Revision).Code);
            Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(HouseReforms.Letter(input,HouseReforms.Answer)),s.Revision).Code);
            var decision=s.ManageCourt(defer,s.Revision);
            Assert.True(decision.Code==ResultCode.Accepted,decision.VisibleReason);
            Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(defer with{Id=new("request:duplicate-defer")},s.Revision).Code);
            s.SetPaused(true);
            Assert.True(store.Save(s,"deferred",w.WorldId).Published);
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("deferred"),host.Current,host.Current.Revision));
            Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        }
        finally{dir.Delete(true);}
    }
}
