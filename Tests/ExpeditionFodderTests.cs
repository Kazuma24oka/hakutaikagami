using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class ExpeditionFodderTests
{
    static WorldSession Start(bool enabled,int feed=10)
    {
        var p=NewGameWorldTests.Officer(role:StartTemplate.Retainer,site:"site:kofu",lord:"person:shingen");var setup=NewGameWorldTests.Setup([p]);
        var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,p.Id,battlefieldPolicy:enabled?BattlefieldRules.ExpeditionPolicy:BattlefieldRules.Policy));
        BattlefieldRulesTests.Pickup(s);var unit=NewGameMilitary.UnitIdsFor(s.DiagnosticSnapshot())[0];
        BattlefieldRulesTests.Good(s.ManageBattlefield(new(new("request:horses"),BattlefieldAction.Equip,unit,TroopKind.Cavalry,"horse",10),s.Revision));
        BattlefieldRulesTests.Good(s.ManageBattlefield(new(new("request:feed"),BattlefieldAction.Load,unit,Good:"fodder",Amount:feed),s.Revision));
        BattlefieldRulesTests.Military(s,NewMilitaryAction.Load,10*MilitaryEconomy.PersonSecondsPerKg);
        BattlefieldRulesTests.Military(s,NewMilitaryAction.Deploy);return s;
    }
    // 既存の軍務の積分処理で野外待機区間を作る。毎秒の同じ世界複写はこの受渡し検査には不要。
    static ContractWorld Camp(ContractWorld source,long seconds)
    {
        var w=ContractJson.Copy(source);long before=w.ElapsedSeconds;w.ElapsedSeconds+=seconds;w.CommitSequence++;
        HakutaiKagami.Core.Messaging.MessageFlow.Advance(w.Messaging,w.ElapsedSeconds);
        NewGameMilitary.Tick(w,before);NewGameMilitary.Sync(w);return new WorldSession(w).DiagnosticSnapshot();
    }
    [Fact]public void ActualRoadMarchAndNestedFieldCampConsumeOneStockAndLoadFromDisk()
    {
        var s=ArmyRoadTests.Start(BattlefieldRules.ExpeditionPolicy);var w=s.DiagnosticSnapshot();var unit=NewGameMilitary.UnitIdsFor(w)[0];
        BattlefieldRulesTests.Good(s.ManageBattlefield(new(new("request:road-horses"),BattlefieldAction.Equip,unit,TroopKind.Cavalry,"horse",10),s.Revision));
        BattlefieldRulesTests.Good(s.ManageBattlefield(new(new("request:road-feed"),BattlefieldAction.Load,unit,Good:"fodder",Amount:10),s.Revision));
        BattlefieldRulesTests.Good(s.ManageMilitaryRice(new(new("request:road-rice"),NewGameMilitary.DepotFor(w),120),s.PlayerControl,s.Revision));
        BattlefieldRulesTests.Military(s,NewMilitaryAction.Load,NewGameMilitary.Capacity);w=s.DiagnosticSnapshot();
        var target=ArmyRoadTests.Near(s);
        BattlefieldRulesTests.Good(s.ManageArmyRoad(new(new("request:feed-march"),ArmyRoadAction.March,target),s.PlayerControl,s.Revision));
        NewGameWorldTests.At(s,s.Seconds+2);var journey=s.DiagnosticSnapshot().Branches!.Journeys.Last();
        NewGameWorldTests.At(s,journey.Legs[^1].EndAt+1);BattlefieldRulesTests.Military(s,NewMilitaryAction.Deploy);
        var camp=Camp(s.DiagnosticSnapshot(),3600);long began=ArmyRoadMarch.AwayWindows(camp,camp.NewMilitary!.Terms.Army).Single().From;
        long used=Math.Min(10,(camp.ElapsedSeconds-began)*10/ExpeditionFodder.HorseSecondsPerUnit);
        Assert.True(used>0);Assert.Equal(used,ExpeditionFodder.Spent(camp,unit));Assert.Equal(10-used,BattlefieldRules.Carried(camp,unit,"fodder"));
        var saved=new WorldSession(camp);saved.SetPaused(true);var snapshot=saved.DiagnosticSnapshot();
        var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"fodder-"+Guid.NewGuid().ToString("N")),snapshot.Definitions);
        Assert.True(store.Save(saved,"camp",snapshot.WorldId).Published);var host=new WorldSessionHost(new WorldSession(TravelWorld.Create()));
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("camp"),host.Current,host.Current.Revision));
        Assert.Equal(ContractJson.Write(snapshot),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        Assert.Equal(ContractJson.Write(Camp(snapshot,300)),ContractJson.Write(Camp(host.Current.DiagnosticSnapshot(),300)));
    }
    [Fact]public void MissingFeedDoesNotBecomeNegativeOrEatLaterLoadsRetroactively()
    {
        var hungry=Camp(Start(true,1).DiagnosticSnapshot(),3600);var unit=NewGameMilitary.UnitIdsFor(hungry)[0];
        Assert.Equal(0,BattlefieldRules.Carried(hungry,unit,"fodder"));Assert.Equal(1,ExpeditionFodder.Spent(hungry,unit));
        var s=new WorldSession(hungry);BattlefieldRulesTests.Military(s,NewMilitaryAction.Enter);
        var load=new BattlefieldInput(new("request:refeed"),BattlefieldAction.Load,unit,Good:"fodder",Amount:2);
        BattlefieldRulesTests.Good(s.ManageBattlefield(load,s.Revision));
        Assert.Equal(ResultCode.AlreadyAccepted,s.ManageBattlefield(load,s.Revision).Code);
        Assert.Equal(2,BattlefieldRules.Carried(s.DiagnosticSnapshot(),unit,"fodder"));
        BattlefieldRulesTests.Military(s,NewMilitaryAction.Deploy);var after=Camp(s.DiagnosticSnapshot(),3600);
        Assert.Equal(0,BattlefieldRules.Carried(after,unit,"fodder"));Assert.Equal(3,ExpeditionFodder.Spent(after,unit));
        var terms=after.NewMilitary!.Terms;Assert.Equal(97,BattlefieldRules.Depot(after,terms.Owner,terms.Site,"fodder"));
    }
    [Fact]public void FeedIsConsumedOnceAcrossElapsedTimeAndRecoveryAndTheOldWorldKeepsItsRules()
    {
        var w=Start(true).DiagnosticSnapshot();var unit=NewGameMilitary.UnitIdsFor(w)[0];
        var hour=Camp(w,3600);Assert.Equal(8,BattlefieldRules.Carried(hour,unit,"fodder"));
        var restored=ContractJson.Read<ContractWorld>(ContractJson.Write(hour));
        var after=Camp(hour,3600);var recovered=Camp(restored,3600);
        Assert.Equal(ContractJson.Write(after),ContractJson.Write(recovered));
        Assert.Equal(5,BattlefieldRules.Carried(after,unit,"fodder"));
        Assert.Equal(5,ExpeditionFodder.Spent(after,unit));
        var old=Camp(Start(false).DiagnosticSnapshot(),7200);var oldUnit=NewGameMilitary.UnitIdsFor(old)[0];
        Assert.Equal(10,BattlefieldRules.Carried(old,oldUnit,"fodder"));
        var bytes=ContractJson.Write(old);Assert.Equal(bytes,ContractJson.Write(new WorldSession(ContractJson.Read<ContractWorld>(bytes)).DiagnosticSnapshot()));
    }
}
