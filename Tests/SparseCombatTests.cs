using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class SparseCombatTests
{
    static void At(WorldSession s,long at)=>NewGameWorldTests.At(s,at);
    static void Open(WorldSession s){foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())s.Open(s.PlayerControl,m.Id,s.Revision);}
    static WorldSession Waiting(long food)
    {
        var s=FactionNoticeTests.Start(declare:false);
        void Send(NewMilitaryAction action,long amount=0){var q=new NewMilitaryInput(new("request:sparse/"+s.Revision+"/"+action),action,amount){AuthorityPolicy=action==NewMilitaryAction.Request?NewGameMilitary.AuthorityContract:null,LedgerPolicy=action==NewMilitaryAction.Request?NewGameMilitary.LedgerIdentityContract:null};Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(q,s.PlayerControl,s.Revision).Code);}
        Send(NewMilitaryAction.Request);At(s,2);Open(s);Send(NewMilitaryAction.Accept);At(s,4);Open(s);Send(NewMilitaryAction.Pickup);Send(NewMilitaryAction.Load,food);Send(NewMilitaryAction.Deploy);return s;
    }
    [Theory][InlineData(1)][InlineData(1000000)]
    public void WaitingDoesNotGrowEmptyCombatOrExhaustedFoodRows(long food)
    {
        var s=Waiting(food);At(s,s.Seconds+1);long bytes=ContractJson.Write(s.DiagnosticSnapshot()).LongLength;At(s,s.Seconds+200);var w=s.DiagnosticSnapshot();Assert.Empty(w.WorldClashes!.Steps);Assert.Single(w.NewMilitary!.Uses);Assert.True(ContractJson.Write(w).LongLength-bytes<256);Assert.Equal(Math.Max(0,food-20100),NewGameMilitary.View(w).Carry);new WorldSession(w);
    }
    [Fact]public void ActualOldV2SaveRemainsByteExactWithItsOriginalClockJournal()
    {
        var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","world-clash-4d8ac2f-active.json"));Assert.Equal("4b5593729cb968782ec16eec70f03d8975ea3f2749be17edc4ec28346b917c97",ContractJson.Hash(bytes));var w=ContractJson.Read<ContractWorld>(bytes);Assert.Equal(NpcArmies.CombatPolicy,w.NewGame!.WorldArmyPolicy);Assert.Equal(WorldClashes.Policy,w.WorldClashes!.Contract);new WorldSession(w);Assert.Equal(bytes,ContractJson.Write(w));
    }
    [Fact]public void OpeningPermissionAfterAClockStepCannotRetroactivelyTurnDefenseIntoMission()
    {
        var s=NpcArmyTests.AtActualContact(permitted:true,sparse:true,openPermission:false);At(s,s.Seconds+1);var before=s.DiagnosticSnapshot();var ids=NewGameMilitary.UnitIdsFor(before).ToHashSet();Assert.DoesNotContain(before.WorldClashes!.Steps[^1].Result.Strikes,h=>ids.Contains(h.Attacker)&&h.Basis==EngagementBasis.Mission);var paper=before.NewMilitary!.Events.Single(e=>e.Effect==MilitaryEffect.EngagementPermission).Message;Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(paper),s.Revision).Code);Assert.Equal(ContractJson.Write(before.WorldClashes),ContractJson.Write(s.DiagnosticSnapshot().WorldClashes));new WorldSession(s.DiagnosticSnapshot());At(s,s.Seconds+1);Assert.Contains(s.DiagnosticSnapshot().WorldClashes!.Steps[^1].Result.Strikes,h=>ids.Contains(h.Attacker)&&h.Basis==EngagementBasis.Mission);new WorldSession(s.DiagnosticSnapshot());
    }
    [Fact]public void OpeningCancellationAfterAClockStepKeepsPriorDamageAndOnlyEndsFutureMissionAuthority()
    {
        var s=NpcArmyTests.AtActualContact(permitted:true,sparse:true);At(s,s.Seconds+1);Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(new(new("request:sparse-cancel"),NewMilitaryAction.CancelEngagement){EngagementOriginal=new("request:contact-engagement")},s.PlayerControl,s.Revision).Code);At(s,s.Seconds+2);var w=s.DiagnosticSnapshot();var ids=NewGameMilitary.UnitIdsFor(w).ToHashSet();var old=ContractJson.Write(w.WorldClashes);var notice=w.NewMilitary!.Events.Single(e=>e.Effect==MilitaryEffect.EngagementCancelled).Message;Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(notice),s.Revision).Code);Assert.Equal(old,ContractJson.Write(s.DiagnosticSnapshot().WorldClashes));At(s,s.Seconds+1);var result=s.DiagnosticSnapshot().WorldClashes!.Steps[^1].Result;Assert.DoesNotContain(result.Strikes,h=>ids.Contains(h.Attacker)&&h.Basis==EngagementBasis.Mission);Assert.Contains(result.Strikes,h=>ids.Contains(h.Attacker)&&h.Basis==EngagementBasis.ImmediateDefense);new WorldSession(s.DiagnosticSnapshot());
    }
    [Fact]public void RoadContactBeforeTheEndpointIsIncludedInSparseReplay()
    {
        var s=NpcArmyTests.AtActualContact(sparse:true,arrivalOffset:-7);var w=s.DiagnosticSnapshot();long arrival=w.Branches!.Journeys.Single(j=>j.Actor==new EntityId("person:nobunaga")).Legs[^1].EndAt;Assert.True(s.Seconds<arrival);At(s,s.Seconds+2);w=s.DiagnosticSnapshot();Assert.Contains(w.WorldClashes!.Steps,t=>t.At<arrival&&t.Result.Strikes.Length>0);new WorldSession(w);
    }
    [Fact]public void SparseJournalContainsOnlyActualStrikesAndExactRestoredFuture()
    {
        var s=NpcArmyTests.AtActualContact(sparse:true);At(s,s.Seconds+2);var w=s.DiagnosticSnapshot();Assert.All(w.WorldClashes!.Steps,q=>Assert.NotEmpty(q.Result.Strikes));var copy=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));foreach(var x in new[]{s,copy})At(x,w.ElapsedSeconds+2);Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(copy.DiagnosticSnapshot()));
    }
    [Fact]public void EmptyStepCannotBeInsertedIntoTheSparseClockJournal()
    {
        var s=Waiting(100000);At(s,s.Seconds+2);var w=s.DiagnosticSnapshot();w.WorldClashes=w.WorldClashes! with{Steps=[new(w.NewMilitary!.Terms.Army,w.ElapsedSeconds-1,w.ElapsedSeconds,w.CommitSequence,new(w.ElapsedSeconds,[],[]))]};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Fact]public void RemovingARealHitCannotBeHiddenByNormalizingVisibleRosters()
    {
        var s=NpcArmyTests.AtActualContact(sparse:true);At(s,s.Seconds+2);var w=s.DiagnosticSnapshot();w.WorldClashes=w.WorldClashes! with{Steps=w.WorldClashes.Steps[1..]};var own=NewGameMilitary.UnitIdsFor(w);
        w.Units=w.Units.Select(u=>u with{Soldiers=own.Contains(u.Id)?NewGameMilitary.StrengthAt(w,Array.IndexOf(own,u.Id),w.ElapsedSeconds):(Array.IndexOf(NewGameMilitary.LedgerIdentity(u.Organization,w.NpcArmies!.Musters.Single(m=>m.Owner==u.Organization).Site).Units,u.Id)==0?40:30)-WorldClashes.SoldiersLost(w,u.Id,w.ElapsedSeconds)}).ToArray();w.ResourceLots=w.ResourceLots.Select(l=>NpcArmies.IsLot(w,l)?l with{Quantity=l.InitialQuantity+NpcArmies.Net(w,l.Id)}:l).ToArray();Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
}
