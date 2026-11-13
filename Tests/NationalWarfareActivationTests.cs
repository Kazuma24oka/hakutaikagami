using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class NationalWarfareActivationTests
{
    [Fact]public void NationalStartInitializesTheOwnedArmyAndSparseClashLedgers()
    {
        var raw=NationalStart.Scenario();var person=raw.People.First(p=>p.Name.Contains("信長",StringComparison.Ordinal)).Id;
        var w=NationalStart.Create(person);
        Assert.True(NpcArmies.Enabled(w));Assert.True(WorldClashes.Enabled(w));Assert.True(WorldClashes.Sparse(w));
        Assert.True(OfficerBattleHealth.Enabled(w));Assert.True(BattlefieldRules.Enabled(w));Assert.True(CastleSiege.Enabled(w));Assert.True(CastleWorks.Enabled(w));Assert.True(HostageCustody.Enabled(w));Assert.True(VoyageExpense.Enabled(w));
        Assert.NotNull(w.NpcArmies);Assert.NotNull(w.WorldClashes);Assert.NotNull(w.Custody);Assert.True(FieldPresence.Enabled(w));Assert.True(FieldSupply.Enabled(w));Assert.True(FieldWorld.Enabled(w));Assert.Null(w.Field);Assert.Null(w.Presence);Assert.Null(w.Supply);Assert.False(EngagementMission.Enabled(w));
        Assert.NotNull(w.MilitaryEconomy);Assert.NotNull(w.HouseFreight);Assert.NotNull(w.FreightOperations);Assert.NotNull(w.ArmyRoad);Assert.NotNull(w.CampConvoys);
        Assert.Equal(RegionalCampaign.RoadActorPointAt(w,w.PlayerControl,0),RegionalCampaign.ActorPointAt(w,w.PlayerControl,0));
        var bytes=ContractJson.Write(w);var restored=ContractJson.Read<ContractWorld>(bytes);
        new WorldSession(restored);Assert.Equal(bytes,ContractJson.Write(restored));
    }
    [Fact]public void AStoredNationalWorldWithoutTheListKeepsAllPreviousSwitches()
    {
        var raw=NationalStart.Scenario(NationalStart.PersonEdition);var me=raw.People.First(p=>p.Name.Contains("信長",StringComparison.Ordinal)).Id;
        var w=NewGameWorld.Create(raw,[],me,20261113,NewGameWorld.CompactPersonalAuthority,NationalStart.PersonEdition,networkPolicy:WorldNetwork.CastlePolicy,developmentPolicy:CharacterDevelopment.Policy,nationalPersonDataPolicy:NationalPersonEstimates.Policy,districtControlPolicy:NationalDistrictControl.Policy);
        Assert.Null(w.NewGame!.NationalSystems);Assert.False(OfficerBattleHealth.Enabled(w));Assert.False(NpcArmies.Enabled(w));Assert.False(VoyageExpense.Enabled(w));
        Assert.Null(w.NpcArmies);Assert.Null(w.WorldClashes);Assert.Null(w.Custody);
        var bytes=ContractJson.Write(w);Assert.Equal(bytes,ContractJson.Write(new WorldSession(ContractJson.Read<ContractWorld>(bytes)).DiagnosticSnapshot()));
    }
    [Fact]public void RecordingNationalNpcDecisionsDoesNotChangeMoneyFoodOrTheFuture()
    {
        var person=NationalStart.Scenario().People.First(p=>p.Name.Contains("信長",StringComparison.Ordinal)).Id;
        var initial=NationalStart.Create(person);byte[] Run(bool slow)
        {
            WorldSession.SlowNpcDecisionRecord=slow;
            try
            {
                var s=new WorldSession(initial);var outside=s.DiagnosticSnapshot();outside.NewGame!.Start.People[0]=outside.NewGame.Start.People[0] with{Name="外部で変更"};s.SetPaused(false);s.AdvanceTo(1,100000);
                Assert.Equal(1,s.Seconds);var after=s.DiagnosticSnapshot();Assert.NotEmpty(after.NpcWork!.Decisions);Assert.DoesNotContain(after.NewGame!.Start.People,p=>p.Name=="外部で変更");return ContractJson.Write(after);
            }
            finally{WorldSession.SlowNpcDecisionRecord=false;}
        }
        Assert.Equal(Run(true),Run(false));
    }
    [Fact]public void OpeningCommunitiesRejectExtraRiceAndRefreshAfterAnActualNationalMuster()
    {
        var scenario=NationalStart.Scenario();var me=scenario.People.First(p=>p.Name.Contains("信長",StringComparison.Ordinal)).Id;
        var w=NationalStart.Create(me);CommunityEconomy.Tick(w);
        foreach(var account in w.Communities!.Accounts.Take(2))
            Assert.Equal(account,CommunityEconomy.AccountAt(w,account.Site,0,w.CommitSequence+1));
        var forged=ContractJson.Copy(w);forged.Communities!.Accounts[0]=forged.Communities.Accounts[0] with{HouseRice=999999};
        Assert.Throws<InvalidDataException>(()=>new WorldSession(forged));
        var head=scenario.Factions.First(f=>f.Head!=me&&scenario.People.Single(p=>p.Id==f.Head) is {} p&&w.Communities.Accounts.Any(a=>a.Site.Value==p.Site)&&RegionalDomains.Owner(w,new(p.Site))?.Value==f.Id);
        var site=new EntityId(scenario.People.Single(p=>p.Id==head.Head).Site);
        var before=w.Communities.Accounts.Single(a=>a.Site==site);w.CommitSequence++;
        NpcArmies.Muster(w,new("decision:national-opening-muster"),new(head.Head),new(head.Id));
        var after=w.Communities!.Accounts.Single(a=>a.Site==site);
        Assert.Equal(20,after.MilitaryWorkers);Assert.True(after.HouseRice<before.HouseRice);
        Assert.Equal(before.Workforce-20,after.Workforce);
        var bytes=ContractJson.Write(w);Assert.Equal(bytes,ContractJson.Write(new WorldSession(ContractJson.Read<ContractWorld>(bytes)).DiagnosticSnapshot()));
    }
}
