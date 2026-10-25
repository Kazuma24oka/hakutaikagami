using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class NpcArmyTests
{
    static readonly EntityId Oda=new("org:oda"),Nobunaga=new("person:nobunaga"),MusterId=new("decision:npc-army/oda"),Site=new("site:castle");
    static ContractWorld Initial(bool policy=true)
    {
        var setup=NewGameWorldTests.Setup([]);return NewGameWorld.Create(setup.World,[],"person:shingen",worldArmyPolicy:policy?NpcArmies.Policy:null);
    }
    static ContractWorld Raised()
    {
        var w=Initial();w.CommitSequence++;NpcArmies.Muster(w,MusterId,Nobunaga,Oda);return w;
    }
    static WorldSession March()
    {
        var s=new WorldSession(Raised());var i=NpcArmies.Move(s.DiagnosticSnapshot(),MusterId,"request:npc-army-out","fork");var r=s.Submit(i,s.Revision);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);return s;
    }
    [Fact]public void LegacyInitialStateHasNoArmyPolicyOrSectionAndBytesRoundTrip()
    {
        var w=Initial(false);var b=ContractJson.Write(w);Assert.DoesNotContain("WorldArmyPolicy",System.Text.Encoding.UTF8.GetString(b));Assert.DoesNotContain("NpcArmies",System.Text.Encoding.UTF8.GetString(b));Assert.Equal(b,ContractJson.Write(ContractJson.Read<ContractWorld>(b)));new WorldSession(w);
    }
    [Fact]public void OwnedRealGarrisonHasStableIdsAndConservesInitialFood()
    {
        var w=Raised();new WorldSession(w);var b=NewGameMilitary.LedgerIdentity(Oda,Site);Assert.Equal(b.Army,Assert.Single(NpcArmies.Armies(w)).Id);Assert.Equal(100,w.Units.Sum(u=>u.Soldiers));Assert.Equal(b.Units,w.Units.Select(u=>u.Id));Assert.Equal(NewGameMilitary.InitialStock,w.ResourceLots.Sum(l=>l.Quantity));Assert.All(w.Units,u=>{Assert.Equal(Oda,u.Organization);Assert.Equal(Nobunaga,u.Commander);});Assert.Empty(FactionStances.Truth(w,0));Assert.Empty(w.Messaging.Messages);
    }
    [Fact]public void SameGarrisonCannotBeMaterializedTwice()
    {
        var w=Raised();w.CommitSequence++;var before=ContractJson.CanonicalHash(w);Assert.Throws<InvalidDataException>(()=>NpcArmies.Muster(w,new("decision:duplicate"),Nobunaga,Oda));Assert.Equal(before,ContractJson.CanonicalHash(w));
    }
    [Theory][InlineData("policy")][InlineData("player")][InlineData("house")][InlineData("away")]
    public void MusterRequiresOptInActualNpcHeadAndPhysicalHome(string why)
    {
        var w=Initial(why!="policy");w.CommitSequence++;
        if(why=="away")w.Locations=w.Locations.Select(l=>l.Actor==Nobunaga?l with{Site=new("site:fork")}:l).ToArray();
        Assert.Throws<InvalidDataException>(()=>NpcArmies.Muster(w,MusterId,why=="player"?w.PlayerControl:Nobunaga,why=="house"?new("org:hojo"):why=="player"?new("org:takeda"):Oda));
    }
    [Fact]public void OutboundIsAnActualCommonRequestCourierTaskAndJourney()
    {
        var s=March();var w=s.DiagnosticSnapshot();Assert.Single(w.Requests);Assert.Single(w.Tasks);Assert.Empty(w.Branches!.Journeys);Assert.Equal(0,NpcArmies.Consumed(w,w.NpcArmies!.Musters[0]));
        NewGameWorldTests.At(s,3);w=s.DiagnosticSnapshot();var j=Assert.Single(w.Branches!.Journeys);Assert.Equal(Nobunaga,j.Actor);Assert.All(j.Legs,l=>Assert.False(NationalRoutes.Sea(l.Edge)));Assert.Equal(w.Requests[0].Intent,w.NpcArmies!.Marches[0].Input);Assert.NotEqual(BranchRoutes.GraphNodes(w).Single(n=>n.Id=="castle").Position,NpcArmies.Position(w,NpcArmies.Armies(w)[0].Id,3));Assert.Equal(RegionalCampaign.ActorPointAt(w,Nobunaga,3),NpcArmies.Position(w,NpcArmies.Armies(w)[0].Id,3));
        Assert.Equal(NewGameMilitary.InitialStock-NpcArmies.Consumed(w,w.NpcArmies.Musters[0]),w.ResourceLots.Sum(l=>l.Quantity));
    }
    [Fact]public void UnregisteredNpcCannotMoveAndCannotChangeAnAcceptedMarch()
    {
        var s=March();var w=s.DiagnosticSnapshot();var i=w.Requests[0].Intent;Assert.Equal(ResultCode.AlreadyAccepted,s.Submit(i,0).Code);Assert.Equal(ResultCode.IdPayloadConflict,s.Submit(i with{WorkSeconds=i.WorkSeconds+1},s.Revision).Code);Assert.Equal(ResultCode.InvalidInput,s.Submit(i with{RequestId=new("request:other-npc"),Actor=new("person:ujiyasu"),Executor=new("person:ujiyasu")},s.Revision).Code);
    }
    [Theory][InlineData("soldiers")][InlineData("food")][InlineData("authority")][InlineData("march")][InlineData("policy")]
    public void ForgedRosterFoodAuthorityMarchOrPolicyCannotLoad(string why)
    {
        var s=March();NewGameWorldTests.At(s,3);var w=s.DiagnosticSnapshot();
        if(why=="soldiers")w.Units[0]=w.Units[0] with{Soldiers=100};
        if(why=="food")w.ResourceLots[0]=w.ResourceLots[0] with{Quantity=w.ResourceLots[0].Quantity+1};
        if(why=="authority"){var m=w.NpcArmies!.Musters[0];w.NpcArmies.Musters[0]=m with{Authority=m.Authority with{Office=new("office:forged")}};}
        if(why=="march")w.NpcArmies=w.NpcArmies! with{Marches=[]};
        if(why=="policy")w.NpcArmies=null;
        Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(3)][InlineData(8)]
    public void GenerationRecoveryPreservesWorldCommanderArmyPositionAndFuture(int at)
    {
        var s=March();NewGameWorldTests.At(s,at);s.SetPaused(true);var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"npc-army-"+Guid.NewGuid().ToString("N")),w.Definitions);Assert.True(store.Save(s,"case",w.WorldId).Published);var host=new WorldSessionHost(new WorldSession(Initial()));Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("case"),host.Current,host.Current.Revision));Assert.Equal(ContractJson.Write(w),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        foreach(var v in new[]{s,host.Current})NewGameWorldTests.At(v,20);Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));var army=NpcArmies.Armies(w)[0].Id;Assert.Equal(NpcArmies.Position(s.DiagnosticSnapshot(),army,20),NpcArmies.Position(host.Current.DiagnosticSnapshot(),army,20));
    }
    [Fact]public void NewCampaignProfilePinsPolicyWithoutChangingDefaultWorlds()
    {
        var store=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"npc-army-start-"+Guid.NewGuid().ToString("N")));var setup=NewGameWorldTests.Setup([]);var p=store.New(setup.World,[],"person:shingen",42,worldArmyPolicy:NpcArmies.Policy);Assert.Equal(NpcArmies.Policy,store.Read(p.Instance).WorldArmyPolicy);Assert.Empty(NewCampaignStore.Build(p).NpcArmies!.Musters);
    }
    static void Arrive(WorldSession s)
    {
        NewGameWorldTests.At(s,s.Seconds+2);var w=s.DiagnosticSnapshot();var j=w.Branches!.Journeys.Last();NewGameWorldTests.At(s,j.Legs[^1].EndAt+2);
    }
    [Fact]public void ActualRoundTripReturnsOnlyRemainingFoodAndNeverResetsTroops()
    {
        var s=March();Arrive(s);var i=NpcArmies.Move(s.DiagnosticSnapshot(),MusterId,"request:npc-army-home","castle");Assert.Equal(ResultCode.Accepted,s.Submit(i,s.Revision).Code);Arrive(s);var w=s.DiagnosticSnapshot();w.CommitSequence++;NpcArmies.Return(w,MusterId);s=new(w);
        var m=w.NpcArmies!.Musters[0];Assert.Equal(0,w.ResourceLots.Single(l=>l.Id==NewGameMilitary.LedgerIdentity(Oda,Site).Carry).Quantity);Assert.Equal(NewGameMilitary.InitialStock-NpcArmies.Consumed(w,m),w.ResourceLots.Sum(l=>l.Quantity));var food=ContractJson.Write(w.ResourceLots);NewGameWorldTests.At(s,s.Seconds+100);Assert.Equal(food,ContractJson.Write(s.DiagnosticSnapshot().ResourceLots));Assert.Equal(100,s.DiagnosticSnapshot().Units.Sum(u=>u.Soldiers));Assert.False(NpcArmies.CommanderBusyAt(s.DiagnosticSnapshot(),Nobunaga,s.Seconds,s.Revision));Assert.Throws<InvalidDataException>(()=>NpcArmies.Move(s.DiagnosticSnapshot(),MusterId,"request:reset","fork"));
    }
    [Fact]public void RemoteReturnCannotTeleportArmyOrRefundFood()
    {
        var s=March();NewGameWorldTests.At(s,5);var w=s.DiagnosticSnapshot();w.CommitSequence++;var before=ContractJson.Write(w);Assert.Throws<InvalidDataException>(()=>NpcArmies.Return(w,MusterId));Assert.Equal(before,ContractJson.Write(w));
    }
    [Fact]public void UnknownClosureDoesNotLeakIntoNpcInitialPlan()
    {
        var a=Raised();var b=ContractJson.Copy(a);var edge=BranchRoutes.Plan("castle","fork",[],w:a)[0];b.Branches=b.Branches! with{BlockedEdges=[edge]};b.Couriers=b.Couriers! with{InitialBlocked=[edge]};
        Assert.Equal(NpcArmies.Move(a,MusterId,"request:private","fork"),NpcArmies.Move(b,MusterId,"request:private","fork"));Assert.Empty(BranchRoutes.KnownBlocked(b,Nobunaga));
    }
    [Fact]public void ArmyAndPersonalInspectionCannotUseCommanderSimultaneously()
    {
        var setup=NewGameWorldTests.Setup([]);var w=NewGameWorld.Create(setup.World,[],"person:shingen",npcRoutinePolicy:NewGameNpcRoutine.Policy,worldArmyPolicy:NpcArmies.Policy);w.CommitSequence++;NpcArmies.Muster(w,MusterId,Nobunaga,Oda);var s=new WorldSession(w);NewGameWorldTests.At(s,8);Assert.DoesNotContain(s.DiagnosticSnapshot().Requests,r=>r.Intent.Executor==Nobunaga);
    }
    [Fact]public void OtherHousePlayerLedgerAndNpcRosterCoexistThroughPickupAndRecovery()
    {
        var s=new WorldSession(Raised());var q=new NewMilitaryInput(new("request:own-military"),NewMilitaryAction.Request){AuthorityPolicy=NewGameMilitary.AuthorityContract,LedgerPolicy=NewGameMilitary.LedgerIdentityContract};Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(q,s.PlayerControl,s.Revision).Code);NewGameWorldTests.At(s,2);foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())s.Open(s.PlayerControl,m.Id,s.Revision);
        Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(new(new("request:own-accept"),NewMilitaryAction.Accept),s.PlayerControl,s.Revision).Code);NewGameWorldTests.At(s,4);foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())s.Open(s.PlayerControl,m.Id,s.Revision);
        Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(new(new("request:own-pickup"),NewMilitaryAction.Pickup),s.PlayerControl,s.Revision).Code);var w=s.DiagnosticSnapshot();Assert.Equal(200,w.Units.Sum(u=>u.Soldiers));Assert.Equal(6,w.Units.Length);new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));Assert.All(w.Units.Where(u=>u.Organization==Oda),u=>Assert.Equal(Nobunaga,u.Commander));
    }
    [Fact]public void PoliticalDecisionCannotReuseMusterId()
    {
        var s=new WorldSession(Raised());Assert.Equal(ResultCode.IdPayloadConflict,s.ChangeTravel(new(MusterId,s.PlayerControl,new("task:unknown"),new("site:fork")),s.Revision).Code);Assert.Equal(ResultCode.IdPayloadConflict,s.DecideFactionStance(new(MusterId,FactionStanceAction.DeclareHostile,new("org:takeda"),Oda,0,20),s.PlayerControl,s.Revision).Code);
    }
    [Fact]public void AcceptedPreviousSaveStillRoundTripsByteForByte()
    {
        var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","engagement-613d135-accepted.json"));var w=ContractJson.Read<ContractWorld>(bytes);new WorldSession(w);Assert.Equal(bytes,ContractJson.Write(w));Assert.Null(w.NpcArmies);Assert.Null(w.NewGame!.WorldArmyPolicy);
    }

    static WorldSession PoliticalRaid(bool hostile=true,bool combat=false)
    {
        var p=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:"site:fork");var setup=NewGameWorldTests.Setup([p],[new(p.Id,"org:created-raid",[p.Site],false)]);var w=NewGameWorld.Create(setup.World,setup.Registered,p.Id,worldArmyPolicy:combat?NpcArmies.CombatPolicy:NpcArmies.Policy);w.CommitSequence++;
        if(hostile)FactionStances.Apply(w,new(new("decision:oda-raiding-intent"),FactionStanceAction.DeclareHostile,Oda,new("org:created-raid"),0,1000000),Nobunaga);return new(w);
    }
    static void FinishRaid(WorldSession s)
    {
        for(int n=0;n<6;n++){NewGameWorldTests.At(s,s.Seconds+2);var w=s.DiagnosticSnapshot();if(w.NpcArmies!.Returns.Length>0)return;var j=w.Branches!.Journeys.Last();NewGameWorldTests.At(s,Math.Max(s.Seconds,j.Legs[^1].EndAt+32));}Assert.Fail("NPC did not complete its bounded sortie");
    }
    [Fact]public void NoDeclaredEnemyMeansNoAutonomousWarOrArmySpawn()
    {
        var s=PoliticalRaid(false);NewGameWorldTests.At(s,200);Assert.Empty(s.DiagnosticSnapshot().NpcArmies!.Musters);Assert.Empty(s.DiagnosticSnapshot().Units);
    }
    [Fact]public void NpcKnownOwnHostilityDrivesActualOutHoldReturnWithoutCapturingCastle()
    {
        var s=PoliticalRaid();var before=s.DiagnosticSnapshot();FinishRaid(s);var w=s.DiagnosticSnapshot();var m=Assert.Single(w.NpcArmies!.Musters);Assert.Equal(Nobunaga,m.Commander);Assert.Equal(2,w.NpcArmies.Marches.Length);Assert.Single(w.NpcArmies.Returns);Assert.Equal(new EntityId("site:fork"),m.Objective!.TargetSite);Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(w.LandRights));Assert.Equal(ContractJson.Write(before.Knowledge.Where(k=>k.Character==s.PlayerControl).ToArray()),ContractJson.Write(w.Knowledge.Where(k=>k.Character==s.PlayerControl).ToArray()));Assert.Empty(FactionStances.Known(w,s.PlayerControl));Assert.Equal(NewGameMilitary.InitialStock-NpcArmies.Consumed(w,m),w.ResourceLots.Sum(l=>l.Quantity));
    }
    [Theory][InlineData(1)][InlineData(8)][InlineData(30)][InlineData(60)]
    public void AutonomousRaidRecoveryPreservesSameDecisionsFoodAndLocations(int at)
    {
        var s=PoliticalRaid();NewGameWorldTests.At(s,at);s.SetPaused(true);var w=s.DiagnosticSnapshot();var dir=Path.Combine(Path.GetTempPath(),"npc-ai-recovery-"+Guid.NewGuid().ToString("N"));var store=new GenerationSaveStore(dir,w.Definitions);Assert.True(store.Save(s,"case",w.WorldId).Published);var host=new WorldSessionHost(PoliticalRaid());Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("case"),host.Current,host.Current.Revision));foreach(var x in new[]{s,host.Current})FinishRaid(x);Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
    }

    [Fact]public void ExistingNpcGarrisonCannotAlsoBeLentToThePlayerRetainer()
    {
        var p=NewGameWorldTests.Officer(role:StartTemplate.Retainer,lord:Nobunaga.Value);var setup=NewGameWorldTests.Setup([p]);var w=NewGameWorld.Create(setup.World,setup.Registered,p.Id,worldArmyPolicy:NpcArmies.Policy);w.CommitSequence++;NpcArmies.Muster(w,MusterId,Nobunaga,Oda);var s=new WorldSession(w);var before=ContractJson.Write(w);
        var q=new NewMilitaryInput(new("request:duplicate-loan"),NewMilitaryAction.Request){AuthorityPolicy=NewGameMilitary.AuthorityContract,LedgerPolicy=NewGameMilitary.LedgerIdentityContract};Assert.Equal(ResultCode.NotAvailable,s.DecideNewMilitary(q,s.PlayerControl,s.Revision).Code);Assert.Equal(before,ContractJson.Write(s.DiagnosticSnapshot()));
    }
    [Fact]public void UnknownBlockedDepartureIsDiscoveredAndDetouredWithoutLeakingOrFoodReset()
    {
        var w=PoliticalRaid().DiagnosticSnapshot();var edge=BranchRoutes.Plan("castle","fork",[],w:w)[0];w.Branches=w.Branches! with{BlockedEdges=[edge]};w.Couriers=w.Couriers! with{InitialBlocked=[edge]};var s=new WorldSession(w);FinishRaid(s);w=s.DiagnosticSnapshot();var m=Assert.Single(w.NpcArmies!.Musters);Assert.Equal(2,w.NpcArmies.Marches.Length);Assert.Single(w.NpcArmies.Returns);Assert.Equal(RegionalCampaign.Site("castle"),w.Locations.Single(l=>l.Actor==Nobunaga).Site);Assert.All(w.Branches!.Journeys.Where(j=>j.Actor==Nobunaga).SelectMany(j=>j.Legs),l=>Assert.NotEqual(edge,l.Edge));Assert.Contains(edge,BranchRoutes.KnownBlocked(w,Nobunaga));Assert.Empty(BranchRoutes.KnownBlocked(w,s.PlayerControl));Assert.Equal(NewGameMilitary.InitialStock-NpcArmies.Consumed(w,m),w.ResourceLots.Sum(l=>l.Quantity));
    }
    static WorldSession AtActualContact(bool permitted=false)
    {
        var s=PoliticalRaid(combat:true);NewGameWorldTests.At(s,3);var outLeg=s.DiagnosticSnapshot().Branches!.Journeys.Single(j=>j.Actor==Nobunaga);NewGameWorldTests.At(s,outLeg.Legs[^1].EndAt+1);
        var q=new NewMilitaryInput(new("request:contact-army"),NewMilitaryAction.Request){AuthorityPolicy=NewGameMilitary.AuthorityContract,LedgerPolicy=NewGameMilitary.LedgerIdentityContract};Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(q,s.PlayerControl,s.Revision).Code);
        NewGameWorldTests.At(s,s.Seconds+2);foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())s.Open(s.PlayerControl,m.Id,s.Revision);
        Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(new(new("request:contact-accept"),NewMilitaryAction.Accept),s.PlayerControl,s.Revision).Code);NewGameWorldTests.At(s,s.Seconds+2);foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())s.Open(s.PlayerControl,m.Id,s.Revision);
        foreach(var action in new[]{NewMilitaryAction.Pickup,NewMilitaryAction.Load})Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(new(new("request:contact-"+action),action,action==NewMilitaryAction.Load?100000:0),s.PlayerControl,s.Revision).Code);
        if(permitted){Assert.Equal(ResultCode.Accepted,s.DecideFactionStance(new(new("decision:own-defense"),FactionStanceAction.DeclareHostile,new("org:created-raid"),Oda,s.Seconds,s.Seconds+60),s.PlayerControl,s.Revision).Code);var engagement=new NewMilitaryInput(new("request:contact-engagement"),NewMilitaryAction.RequestEngagement){Engagement=new(NewGameMilitary.EngagementTaskContract,Oda,s.Seconds,s.Seconds+60)};Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(engagement,s.PlayerControl,s.Revision).Code);NewGameWorldTests.At(s,s.Seconds+2);foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())s.Open(s.PlayerControl,m.Id,s.Revision);}
        Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(new(new("request:contact-deploy"),NewMilitaryAction.Deploy),s.PlayerControl,s.Revision).Code);return s;
    }
    [Fact]public void ActualRosterAndPositionFeedScopedContactWithoutCreatingAnExerciseEnemy()
    {
        var s=AtActualContact();var w=s.DiagnosticSnapshot();var before=ContractJson.Write(w);var snapshot=WorldEngagement.Snapshot(w);var contact=WorldEngagement.Resolve(w);Assert.Equal(6,snapshot.Units.Length);Assert.NotEmpty(contact.Strikes);Assert.Contains(contact.Strikes,h=>h.Basis==EngagementBasis.Mission);Assert.Contains(contact.Strikes,h=>h.Basis==EngagementBasis.ImmediateDefense);Assert.NotEmpty(contact.Losses);Assert.All(snapshot.Units,u=>Assert.Contains(u.Unit,w.Units));Assert.DoesNotContain(snapshot.Units,u=>NewGameMilitary.ExerciseIds.Contains(u.Unit.Id));Assert.Equal(before,ContractJson.Write(w));
    }
    [Fact]public void DistantRealArmiesAndFriendlyTroopsDoNotProduceContactDamage()
    {
        var s=new WorldSession(Raised());Assert.Empty(WorldEngagement.Resolve(s.DiagnosticSnapshot()).Losses);
        var c=AtActualContact();var w=c.DiagnosticSnapshot();w.CommitSequence++;FactionStances.Apply(w,new(new("request:contact-withdraw"),FactionStanceAction.WithdrawOwnDeclaration,Oda,new("org:created-raid"),w.ElapsedSeconds,0,new("decision:oda-raiding-intent")),Nobunaga);Assert.Empty(WorldEngagement.Resolve(w).Strikes);Assert.Empty(WorldEngagement.Resolve(w).Losses);
    }

    [Fact]public void ContactActuallyReducesOwnedSoldiersAndLaterFoodConsumption()
    {
        var s=AtActualContact();var initial=s.DiagnosticSnapshot();NewGameWorldTests.At(s,s.Seconds+2);var w=s.DiagnosticSnapshot();Assert.Equal(2,w.WorldClashes!.Steps.Length);Assert.NotEmpty(WorldClashes.Losses(w,w.ElapsedSeconds));Assert.True(w.Units.Where(u=>u.Organization==Oda).Sum(u=>u.Soldiers)<100);Assert.True(NewGameMilitary.TotalStrength(w,w.ElapsedSeconds)<100);Assert.Equal(6,w.Units.Length);Assert.Equal(ContractJson.Write(initial.Definitions),ContractJson.Write(w.Definitions));Assert.True(w.NewMilitary!.Uses[^1].Soldiers<100);new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(4)]
    public void ActualClashRecoveryKeepsTheSameLossesFoodAndNextSecond(int seconds)
    {
        var s=AtActualContact();NewGameWorldTests.At(s,s.Seconds+seconds);s.SetPaused(true);var w=s.DiagnosticSnapshot();ExportProjection(w,seconds,"before");var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"actual-clash-"+Guid.NewGuid().ToString("N")),w.Definitions);Assert.True(store.Save(s,"case",w.WorldId).Published);var host=new WorldSessionHost(PoliticalRaid());Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("case"),host.Current,host.Current.Revision));Assert.Equal(ContractJson.Write(w),ContractJson.Write(host.Current.DiagnosticSnapshot()));ExportProjection(host.Current.DiagnosticSnapshot(),seconds,"restored");foreach(var x in new[]{s,host.Current})NewGameWorldTests.At(x,w.ElapsedSeconds+2);Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
    }
    [Theory][InlineData("missing")][InlineData("duplicate")][InlineData("damage")][InlineData("target")]
    public void MissingDuplicatedOrChangedCombatEffectsCannotBeLoaded(string change)
    {
        var s=AtActualContact();NewGameWorldTests.At(s,s.Seconds+2);var w=s.DiagnosticSnapshot();var steps=w.WorldClashes!.Steps;
        if(change=="missing")w.WorldClashes=w.WorldClashes with{Steps=steps[1..]};
        if(change=="duplicate")w.WorldClashes=w.WorldClashes with{Steps=[..steps,steps[^1]]};
        if(change=="damage"){var q=steps[0];var losses=q.Result.Losses.ToArray();losses[0]=losses[0] with{Soldiers=0};steps[0]=q with{Result=q.Result with{Losses=losses}};}
        if(change=="target"){var q=steps[0];var strikes=q.Result.Strikes.ToArray();strikes[0]=strikes[0] with{Target=strikes[0].Attacker};steps[0]=q with{Result=q.Result with{Strikes=strikes}};}
        Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }

    [Fact]public void RealLossesSurviveRetreatReturnUnloadingReportAndCommandHandback()
    {
        var s=AtActualContact();NewGameWorldTests.At(s,s.Seconds+2);int remaining=NewGameMilitary.TotalStrength(s.DiagnosticSnapshot(),s.Seconds);
        void Send(NewMilitaryAction action,long amount=0){var q=new NewMilitaryInput(new("request:actual-return/"+action),action,amount);var result=s.DecideNewMilitary(q,s.PlayerControl,s.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);}
        Send(NewMilitaryAction.Recall);var end=s.DiagnosticSnapshot().NewMilitary!.Orders.Last().Routes.SelectMany(r=>r).Select(l=>l.EndAt).DefaultIfEmpty(s.Seconds).Max();NewGameWorldTests.At(s,end);Send(NewMilitaryAction.Enter);Send(NewMilitaryAction.Unload,NewGameMilitary.View(s.DiagnosticSnapshot()).Carry);Send(NewMilitaryAction.Report);Send(NewMilitaryAction.Return);NewGameWorldTests.At(s,s.Seconds+3);foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())s.Open(s.PlayerControl,m.Id,s.Revision);
        var w=s.DiagnosticSnapshot();Assert.Equal(remaining,w.NewMilitary!.Reports.Last().Soldiers);Assert.Equal(0,NewGameMilitary.View(w).Carry);Assert.False(NewGameMilitary.CustodyAt(w,w.ElapsedSeconds,w.CommitSequence));Assert.Equal(remaining,NewGameMilitary.TotalStrength(w,w.ElapsedSeconds));Assert.All(w.Units.Where(u=>u.Organization!=Oda),u=>Assert.Equal(s.PlayerControl,u.Commander));Assert.NotEmpty(WorldClashes.Losses(w,w.ElapsedSeconds));
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));foreach(var x in new[]{s,restored})FinishRaid(x);Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));Assert.Equal(remaining,NewGameMilitary.TotalStrength(s.DiagnosticSnapshot(),s.Seconds));
    }

    [Fact]public void ActuallyOpenedScopedPermissionBecomesTheSourceOfRealWorldDamage()
    {
        var s=AtActualContact(true);NewGameWorldTests.At(s,s.Seconds+1);var w=s.DiagnosticSnapshot();var own=w.NewMilitary!.Terms.Owner;var ids=w.Units.Where(u=>u.Organization==own).Select(u=>u.Id).ToHashSet();Assert.Contains(w.WorldClashes!.Steps[0].Result.Strikes,h=>ids.Contains(h.Attacker)&&h.Basis==EngagementBasis.Mission&&h.Original==new EntityId("request:contact-engagement"));Assert.True(w.Units.Where(u=>u.Organization==Oda).Sum(u=>u.Soldiers)<100);new WorldSession(w);
    }

    [Fact]public void ActualPriorNpcV1ProfileRebuildsItsOriginalWorldWithoutAdoptingCombat()
    {
        var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","npc-army-4a0f0a4-start.json"));Assert.Equal("a9a17a93ea1a7f58574c7975f7093c1ba910ee87c8eec7586391b1023ce6cd4c",ContractJson.Hash(bytes));var p=ContractJson.Read<NewCampaignProfile>(bytes);Assert.Equal(NpcArmies.Policy,p.WorldArmyPolicy);var w=NewCampaignStore.Build(p);Assert.Null(w.WorldClashes);Assert.Equal("94aa624ca100e7bdd5e87f22de37fd3df1091f02934b00307b998075bfc70359",ContractJson.CanonicalHash(w));Assert.Equal(bytes,ContractJson.Write(p));
    }

    static void ExportProjection(ContractWorld world,int phase,string variant)
    {
        var dir=Environment.GetEnvironmentVariable("HAKUTAI_WORLD_CLASH_QA_DIR");if(string.IsNullOrEmpty(dir))return;Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"clash-"+phase+"."+variant+".json"),ContractJson.Write(world));
    }
    [Fact]public void NpcArmyDisplayNeedsCurrentLocalSightAndDoesNotMutateTheWorld()
    {
        var remote=PoliticalRaid(combat:true);NewGameWorldTests.At(remote,3);Assert.Empty(NpcArmies.Sights(remote.DiagnosticSnapshot(),remote.PlayerControl));var s=AtActualContact();NewGameWorldTests.At(s,s.Seconds+1);var w=s.DiagnosticSnapshot();var before=ContractJson.Write(w);var sight=Assert.Single(NpcArmies.Sights(w,s.PlayerControl));Assert.Equal(Nobunaga,sight.Commander);Assert.True(sight.Soldiers<100);Assert.True(sight.Attacking);Assert.Equal(RegionalCampaign.ActorPointAt(w,Nobunaga,w.ElapsedSeconds),sight.Position);Assert.Equal(before,ContractJson.Write(w));
    }

}
