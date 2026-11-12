using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
[Trait("Tier","Heavy")]
public sealed class NationalDistrictRevenueTests
{
    [Fact]
    public void NationalPublicMoneyCanBuyFiniteLocalGoodsAndRestoreWithoutDuplication()
    {
        var scenario=NationalStart.Scenario();
        var head=scenario.Factions.Select(f=>f.Head).Single(h=>scenario.People.Single(p=>p.Id==h).Name.Contains("信長"));
        var world=NewGameWorld.Create(scenario,[],head,116,NewGameWorld.CompactPersonalAuthority,
            historicalCatalog:NationalStart.RevenueEdition,networkPolicy:WorldNetwork.CastlePolicy,
            districtControlPolicy:NationalDistrictControl.Policy,developmentPolicy:CharacterDevelopment.Policy,
            nationalPersonDataPolicy:NationalPersonEstimates.Policy,economyPolicy:RegionalDomains.Policy,
            workPolicy:WorkFollowup.Policy,inspectionPolicy:LocalInspectionFindings.Policy,demandPolicy:LocalWorkDemand.Policy);
        var s=new WorldSession(world);var site=s.DiagnosticSnapshot().Locations.Single(l=>l.Actor==s.PlayerControl).Site!.Value;
        Assert.Equal(0,s.HouseTreasuryView()!.HouseCash);
        var privateBefore=NewGameWork.Balance(s.DiagnosticSnapshot(),s.PlayerControl);
        s.SetPaused(false);s.AdvanceTo(31*86400,100000);
        Assert.Equal(31*86400,s.Seconds);
        var prior=s.DiagnosticSnapshot();var view=s.HouseTreasuryView()!;
        Assert.True(view.HouseCash>0);
        var id=new EntityId("request:national-revenue/purchase");
        var input=new TreasuryInput(id,TreasuryAction.Buy,site,"wood",1);
        var result=s.ManageHouseTreasury(input,s.PlayerControl,s.Revision);
        Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        Assert.Equal(view.HouseCash-4,s.HouseTreasuryView()!.HouseCash);
        Assert.Equal(privateBefore,NewGameWork.Balance(s.DiagnosticSnapshot(),s.PlayerControl));
        Assert.Equal(ResultCode.AlreadyAccepted,s.ManageHouseTreasury(input,s.PlayerControl,s.Revision).Code);
        s.SetPaused(true);
        var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"national-revenue-"+Guid.NewGuid().ToString("N")),prior.Definitions);
        Assert.True(store.Save(s,"public-money",prior.WorldId).Published);
        var host=new WorldSessionHost(new WorldSession(TravelWorld.Create()));
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("public-money"),host.Current,host.Current.Revision));
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        Assert.Equal(s.HouseTreasuryView(),host.Current.HouseTreasuryView());
        foreach(var session in new[]{s,host.Current}){session.SetPaused(false);session.AdvanceTo(31*86400+1,100000);}
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        var a=s.DiagnosticSnapshot();var b=host.Current.DiagnosticSnapshot();
        Assert.Equal(NationalDistrictRevenue.Income(a,site,60*86400,a.CommitSequence),NationalDistrictRevenue.Income(b,site,60*86400,b.CommitSequence));
    }
}
