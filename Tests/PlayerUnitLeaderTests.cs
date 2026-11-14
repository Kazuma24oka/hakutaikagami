using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// 本人の軍の部隊を家臣に任せる（PlayerUnitLeaders、2026-11-15）：出陣で選んだ家臣が2番目の部隊を率い、率いる間は野にいてほかの用をしない。
public sealed class PlayerUnitLeaderTests
{
    [Fact]
    public void ARetainerChosenAtDeploymentLeadsTheSecondUnitInTheField()
    {
        var s=NpcArmyTests.AtActualContact(permitted:true,sparse:true,capture:true,aide:true);
        var w=s.DiagnosticSnapshot();EntityId aide=new("created:raid-aide");
        var units=w.Units.Where(u=>u.Army==w.NewMilitary!.Terms.Army).OrderBy(u=>u.Id.Value,StringComparer.Ordinal).ToArray();
        Assert.Equal(s.PlayerControl,w.Units.Single(u=>u.Id==NewGameMilitary.UnitIdsFor(w)[0]).Commander);
        Assert.Equal(aide,w.Units.Single(u=>u.Id==NewGameMilitary.UnitIdsFor(w)[1]).Commander);
        Assert.Equal(s.PlayerControl,w.Units.Single(u=>u.Id==NewGameMilitary.UnitIdsFor(w)[2]).Commander);
        Assert.Equal([aide],NewGameMilitary.UnitLeadersAt(w,w.ElapsedSeconds,w.CommitSequence+1));
        Assert.Null(PersonalCompanions.PhysicalSiteAt(w,aide,w.ElapsedSeconds,w.CommitSequence+1)); // 軍と一緒に野にいる
        Assert.True(NpcArmies.CommanderBusyAt(w,aide,w.ElapsedSeconds,w.CommitSequence));
        Assert.Empty(NewGameMilitary.UnitLeaderCandidates(w));
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(restored.DiagnosticSnapshot()));
        NewGameWorldTests.At(s,s.Seconds+60);
        Assert.Equal(aide,s.DiagnosticSnapshot().Units.Single(u=>u.Id==NewGameMilitary.UnitIdsFor(w)[1]).Commander);
    }
    [Fact]
    public void TheRetainerLeadingABrokenUnitCanBeCaptured()
    {
        var s=NpcArmyTests.AtActualContact(permitted:true,sparse:true,capture:true,aide:true);
        var w=s.DiagnosticSnapshot();EntityId aide=new("created:raid-aide");
        var mine=NewGameMilitary.UnitIdsFor(w)[1];var m=w.NpcArmies!.Musters.Single();var enemy=NewGameMilitary.LedgerIdentity(m.Owner,m.Site).Units[0];
        int Roll(long day)=>System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"battle-capture-v1/{w.Seed}/{aide.Value}/{day}"))[0]%100;
        long day=w.ElapsedSeconds/86400;while(Roll(day)>=35)day++;long at=Math.Max(w.ElapsedSeconds,day*86400);
        var result=new EngagementResult(at,[new(enemy,mine,EngagementBasis.ImmediateDefense,null)],[new(at,mine,5,100)]);
        var c=Assert.Single(BattleCaptivity.Resolve(w,result)!,x=>x.Person==aide);
        Assert.Equal(m.Owner,c.CaptorOwner);Assert.Equal(w.NewMilitary!.Terms.Owner,c.FromOwner);
        // 崩れなかった部隊の家臣は捕らえない。
        Assert.DoesNotContain(BattleCaptivity.Resolve(w,new(at,[new(enemy,mine,EngagementBasis.ImmediateDefense,null)],[new(at,mine,1,10)]))??[],x=>x.Person==aide);
    }
}
