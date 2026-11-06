using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class HouseScaleTests
{
    [Fact]
    public void ThreeCastlesEnableDeclarationAndSecondSeatButDoNotNotifyInstantly()
    {
        var head=NewGameWorldTests.Officer(role:StartTemplate.Daimyo);
        var first=NewGameWorldTests.Officer("created:first",StartTemplate.Retainer,lord:head.Id);
        var second=first with{Id="created:second"};
        EntityId owner=new("org:scale-test"),site=new(head.Site);
        var setup=NewGameWorldTests.Setup([head,first,second],[new(head.Id,owner.Value,[head.Site,"site:kofu","site:odawara"],false)]);
        var w=NewGameWorld.Create(setup.World,setup.Registered,head.Id,42,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy);
        var s=new WorldSession(w);
        CommandResult Nominate(string id,string person)=>s.ManageCourt(new(new(id),CourtAction.Nominate,new(person),owner,site),s.Revision);
        Assert.Equal(ResultCode.Accepted,Nominate("request:first",first.Id).Code);
        Assert.Equal(ResultCode.NotAvailable,Nominate("request:early",second.Id).Code);
        var declaration=new CourtInput(new("request:declare"),CourtAction.DeclareScale,s.PlayerControl,owner,site,Scale:CourtScale.Daimyo);
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(declaration,s.Revision).Code);
        var sent=s.DiagnosticSnapshot();
        Assert.Equal(CourtScale.Local,HouseScaleNotices.Known(sent,new(first.Id),owner));
        Assert.Equal(CourtScale.Daimyo,HouseScaleNotices.Known(sent,s.PlayerControl,owner));
        Assert.Equal(ResultCode.Accepted,Nominate("request:second",second.Id).Code);
        Assert.Equal(2,s.DiagnosticSnapshot().Offices.Count(o=>o.Kind=="house-steward" && o.Until==long.MaxValue));
        s.SetPaused(true);
        var dir=Directory.CreateTempSubdirectory("hakutai-scale-");
        try
        {
            var store=new GenerationSaveStore(dir.FullName,w.Definitions);
            Assert.True(store.Save(s,"declared",w.WorldId).Published);
            var host=new WorldSessionHost(new WorldSession(w));
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("declared"),host.Current,host.Current.Revision));
            NewGameWorldTests.At(host.Current,120);NewGameWorldTests.At(s,120);
            var after=host.Current.DiagnosticSnapshot();
            Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(after));
            Assert.Equal(CourtScale.Daimyo,HouseScaleNotices.Known(after,new(first.Id),owner));
            Assert.Equal(CourtScale.Local,HouseScaleNotices.Known(after,new("person:nobunaga"),owner));
        }
        finally{dir.Delete(true);}
    }
}
