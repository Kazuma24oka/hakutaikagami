using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
[Trait("Tier","Heavy")]public sealed class CastleSiegeTests
{
 internal static readonly EntityId Home=new("site:kyoto"),Site=new("site:akutagawa"),Owner=new("created:siege-house"),Defender=new("created:keep-house");
 internal static void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
 internal static WorldSession Start(bool march=true,bool npcArmy=false){var lord=NewGameWorldTests.Officer("created:siege-lord",StartTemplate.Daimyo,Home.Value);var other=NewGameWorldTests.Officer("created:keep-lord",StartTemplate.Daimyo,Site.Value);var setup=NewGameWorldTests.Setup([lord,other],[new(lord.Id,Owner.Value,[Home.Value],false),new(other.Id,Defender.Value,[Site.Value],false)]);var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,lord.Id,42,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,voyageRules:VoyageExpense.Standard,worldArmyPolicy:npcArmy?NpcArmies.CompactCombatPolicy:null,economyPolicy:HouseSuccession.Policy,territoryPolicy:CastleSiege.Policy,battlefieldPolicy:BattlefieldRules.Policy));Good(s.DecideFactionStance(new(new("decision:siege-hostile"),FactionStanceAction.DeclareHostile,Owner,Defender,0,86400*30),s.PlayerControl,s.Revision));Good(s.DecideNewMilitary(new(new("request:siege-duty"),NewMilitaryAction.Request){AuthorityPolicy=NewGameMilitary.AuthorityContract,LedgerPolicy=NewGameMilitary.LedgerIdentityContract},s.PlayerControl,s.Revision));NewGameWorldTests.At(s,2);BattlefieldRulesTests.Open(s);BattlefieldRulesTests.Military(s,NewMilitaryAction.Accept);NewGameWorldTests.At(s,4);BattlefieldRulesTests.Open(s);BattlefieldRulesTests.Military(s,NewMilitaryAction.Pickup);Good(s.ManageMilitaryRice(new(new("request:siege-food"),s.MilitaryEconomyView()!.Depot,120),s.PlayerControl,s.Revision));BattlefieldRulesTests.Military(s,NewMilitaryAction.Load,NewGameMilitary.Capacity);if(march){Good(s.ManageArmyRoad(new(new("request:siege-march"),ArmyRoadAction.March,Site),s.PlayerControl,s.Revision));NewGameWorldTests.At(s,7);var j=s.DiagnosticSnapshot().Branches!.Journeys.Last();NewGameWorldTests.At(s,j.Legs[^1].EndAt+2);BattlefieldRulesTests.Military(s,NewMilitaryAction.Deploy);}return s;}
 static SiegeInput Input(WorldSession s,SiegeAction action,EntityId? root=null,CastleSection? district=null)=>new(new("request:siege/"+s.Revision),action,Site,root,district);
 [Fact]public void AssaultNeedsRealArrivalAndOrderedDistrict(){var s=Start(false);Assert.Equal(ResultCode.NotAvailable,s.ManageSiege(Input(s,SiegeAction.Begin),s.Revision).Code);}
 [Fact]public void StormConsumesFieldFoodAndRestoresMidAssaultThenHandsOverGateAndKeep(){var s=Start();var begin=Input(s,SiegeAction.Begin);Good(s.ManageSiege(begin,s.Revision));Assert.Equal(ResultCode.NotAvailable,s.ManageSiege(Input(s,SiegeAction.Assault,begin.Id,CastleSection.Keep),s.Revision).Code);var outer=Input(s,SiegeAction.Assault,begin.Id,CastleSection.OuterMoat);Good(s.ManageSiege(outer,s.Revision));Assert.Equal(ResultCode.AlreadyAccepted,s.ManageSiege(outer,s.Revision).Code);long end=s.Seconds+60,food=NewGameMilitary.View(s.DiagnosticSnapshot()).Carry;NewGameWorldTests.At(s,end-30);s.SetPaused(true);var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"siege-check-"+Guid.NewGuid().ToString("N")),w.Definitions);store.Save(s,"manual",w.WorldId);var host=new WorldSessionHost(new WorldSession(w));Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("manual"),host.Current,host.Current.Revision));foreach(var x in new[]{s,host.Current}){NewGameWorldTests.At(x,end);Assert.Contains(x.DiagnosticSnapshot().Sieges!.Orders,o=>o.Outcome=="区画を確保");Assert.True(NewGameMilitary.View(x.DiagnosticSnapshot()).Carry<food);Good(x.ManageSiege(Input(x,SiegeAction.Parley,begin.Id),x.Revision));Good(x.ManageSiege(Input(x,SiegeAction.DeliverGate,begin.Id),x.Revision));Good(x.ManageSiege(Input(x,SiegeAction.DeliverKeep,begin.Id),x.Revision));}Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));Assert.Equal("本丸を占有",s.DiagnosticSnapshot().Sieges!.Orders.Last().Outcome);Assert.Equal(Owner,CastleSiege.OccupierAt(s.DiagnosticSnapshot(),Site,s.Seconds,s.Revision+1));Assert.Equal(ResultCode.NotAvailable,s.ManageSiege(Input(s,SiegeAction.Begin),s.Revision).Code);}
 [Fact] public void SupplyUnloadsAtArrivalOnceAfterRestoringAnUnderwayShip(){
  EntityId source=new("site:sakai"),target=new("site:sumoto");
  var lord=NewGameWorldTests.Officer("created:port-lord",StartTemplate.Daimyo,source.Value);
  var setup=NewGameWorldTests.Setup([lord],[new(lord.Id,"created:port-house",[source.Value,target.Value],false)]);
  var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,lord.Id,42,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy,territoryPolicy:CastleSiege.Policy));
  Good(s.StartPublicWork(new(new("request:port-work"),source,WorksKind.Port),s.Revision));NewGameWorldTests.At(s,7*86400);
  Good(s.StartPublicWork(new(new("request:ship-build"),source,WorksKind.TradeShip),s.Revision));NewGameWorldTests.At(s,21*86400);
  var ship=Assert.Single(PublicWorks.Ships(s.DiagnosticSnapshot()),x=>x.Port==source);Good(s.MaintainPublicShip(new("request:care"),ship.Id,s.Revision));
  var send=new FleetInput(new("request:send"),FleetAction.SendSupply,ship.Id,source,target,15);
  Good(s.ManageSiegeFleet(send,s.Revision));var root=Assert.Single(s.DiagnosticSnapshot().Sieges!.Fleets);
  NewGameWorldTests.At(s,root.Arrive-1);s.SetPaused(true);var w=s.DiagnosticSnapshot();
  var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"fleet-check-"+Guid.NewGuid().ToString("N")),w.Definitions);
  store.Save(s,"manual",w.WorldId);var host=new WorldSessionHost(new WorldSession(w));Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("manual"),host.Current,host.Current.Revision));
  foreach(var x in new[]{s,host.Current}){NewGameWorldTests.At(x,root.Arrive);var delivered=Assert.Single(x.DiagnosticSnapshot().Sieges!.Fleets,q=>q.Input.Action==FleetAction.Unload);Assert.Equal(root.Arrive,delivered.At);Assert.Equal(15,SiegeFleet.CastleRiceAt(x.DiagnosticSnapshot(),target,x.Seconds,x.Revision+1));NewGameWorldTests.At(x,root.Home);Assert.Single(x.DiagnosticSnapshot().Sieges!.Fleets,q=>q.Input.Action==FleetAction.Dock);}
  Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
 }
 [Fact] public void HostileWarshipBlocksOnlyWhileOnStationAndSupplyWaitSurvivesLoad()
 {
     EntityId sakai=new("site:sakai"), sumoto=new("site:sumoto"), shozui=new("site:shozui");
     var trader=NewGameWorldTests.Officer("created:port-lord",StartTemplate.Daimyo,sakai.Value);
     var opponent=NewGameWorldTests.Officer("created:sea-lord",StartTemplate.Daimyo,shozui.Value);
     EntityId traderHouse=new("created:port-house"), opposingHouse=new("created:sea-house");
     var setup=NewGameWorldTests.Setup([trader,opponent],[new(trader.Id,traderHouse.Value,[sakai.Value,sumoto.Value],false),new(opponent.Id,opposingHouse.Value,[shozui.Value],false)]);
     var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,trader.Id,42,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy,territoryPolicy:CastleSiege.Policy));
     var npcDecision=new Action<ContractWorld>(w=>FactionStances.Apply(w,new(new("decision:naval-hostility"),FactionStanceAction.DeclareHostile,opposingHouse,traderHouse,0,90*86400),new(opponent.Id)));
     typeof(WorldSession).GetMethod("Change",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(s,[npcDecision,null]);
     Good(s.StartPublicWork(new(new("request:trade-port"),sakai,WorksKind.Port),s.Revision));
     NewGameWorldTests.At(s,7*86400);
     Good(s.StartPublicWork(new(new("request:trade-ship"),sakai,WorksKind.TradeShip),s.Revision));
     NewGameWorldTests.At(s,23*86400);
     var ship=Assert.Single(PublicWorks.Ships(s.DiagnosticSnapshot()),q=>q.Port==sakai);
     Good(s.MaintainPublicShip(new("request:trade-care"),ship.Id,s.Revision));
     var trace=s.DiagnosticSnapshot();
     Assert.True(trace.Sieges?.Fleets.Length>0,string.Join(" / ",trace.PublicWorks!.Orders.Select(q=>q.Input.Site+" "+q.Input.Kind+" "+q.At/86400))+" 保守 "+trace.PublicWorks.Care.Length);
     var warship=Assert.Single(s.DiagnosticSnapshot().Sieges?.Fleets??[],q=>q.Input.Action==FleetAction.StationWarship);
     Assert.False(SiegeFleet.Blocked(s.DiagnosticSnapshot(),sumoto,traderHouse,s.Seconds,s.Revision+1));
     Good(s.ManageSiegeFleet(new(new("request:deliver"),FleetAction.SendSupply,ship.Id,sakai,sumoto,15),s.Revision));
     var supply=Assert.Single(s.DiagnosticSnapshot().Sieges!.Fleets,q=>q.Input.Action==FleetAction.SendSupply);
     NewGameWorldTests.At(s,supply.Arrive);
     Assert.True(SiegeFleet.Blocked(s.DiagnosticSnapshot(),sumoto,traderHouse,s.Seconds,s.Revision+1));
     Assert.Equal(0,SiegeFleet.CastleRiceAt(s.DiagnosticSnapshot(),sumoto,s.Seconds,s.Revision+1));
     s.SetPaused(true);var w=s.DiagnosticSnapshot();
     var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"fleet-wait-"+Guid.NewGuid().ToString("N")),w.Definitions);
     store.Save(s,"manual",w.WorldId);var host=new WorldSessionHost(new WorldSession(w));
     Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("manual"),host.Current,host.Current.Revision));
     foreach(var branch in new[]{s,host.Current})
     {
         NewGameWorldTests.At(branch,warship.Leave);
         var final=branch.DiagnosticSnapshot();
         Assert.False(SiegeFleet.Blocked(final,sumoto,traderHouse,branch.Seconds,branch.Revision+1));
         Assert.Equal(warship.Leave,Assert.Single(final.Sieges!.Fleets,q=>q.Input.Action==FleetAction.Unload).At);
         Assert.Equal(15,SiegeFleet.CastleRiceAt(final,sumoto,branch.Seconds,branch.Revision+1));
     }
     Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
 }
 [Fact] public void LocalMusterAfterGatePreventsKeepHandover()
 {
     var session=Start(npcArmy:true);
     var begin=Input(session,SiegeAction.Begin);
     Good(session.ManageSiege(begin,session.Revision));
     Good(session.ManageSiege(Input(session,SiegeAction.Assault,begin.Id,CastleSection.OuterMoat),session.Revision));
     NewGameWorldTests.At(session,session.Seconds+60);
     Good(session.ManageSiege(Input(session,SiegeAction.Parley,begin.Id),session.Revision));
     Good(session.ManageSiege(Input(session,SiegeAction.DeliverGate,begin.Id),session.Revision));
     var muster=new Action<ContractWorld>(w=>NpcArmies.Muster(w,new("decision:castle-defender-muster"),new("created:keep-lord"),Defender));
     typeof(WorldSession).GetMethod("Change",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(session,[muster,null]);
     var w=session.DiagnosticSnapshot();
     Assert.True(CastleHandover.Forces(w,Defender,Site,session.Seconds,session.Revision+1)>0);
     var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
     foreach(var branch in new[]{session,restored})
     {
         Assert.Equal(ResultCode.NotAvailable,branch.ManageSiege(Input(branch,SiegeAction.DeliverKeep,begin.Id),branch.Revision).Code);
         Assert.Equal("城門を受領",branch.DiagnosticSnapshot().Sieges!.Orders.Last().Outcome);
         Assert.Null(CastleSiege.OccupierAt(branch.DiagnosticSnapshot(),Site,branch.Seconds,branch.Revision+1));
     }
 }
 [Fact] public void StormAllDistrictsAndRejectMissingFinalResolution()
 {
     var session=Start();
     var root=Input(session,SiegeAction.Begin);
     Good(session.ManageSiege(root,session.Revision));
     foreach(var district in Enum.GetValues<CastleSection>())
     {
         for(int attempt=0;attempt<5;attempt++)
         {
             Good(session.ManageSiege(Input(session,SiegeAction.Assault,root.Id,district),session.Revision));
             NewGameWorldTests.At(session,session.Seconds+60);
             if(session.DiagnosticSnapshot().Sieges!.Orders.Last().Outcome!="区画が損傷")break;
         }
     }
     var w=session.DiagnosticSnapshot();
     Assert.Equal(Owner,CastleSiege.OccupierAt(w,Site,session.Seconds,session.Revision+1));
     w.Sieges=w.Sieges! with { Orders=w.Sieges.Orders[..^1] };
     Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
 }
}
