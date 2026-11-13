using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

// 遠方の合戦の簡略計算（W4）：実際の合戦と同じ接触の規則を部隊単位でまとめて計算する。
public sealed class StrategyBattleTests
{
    [Fact]
    public void LargerForcesBreakTheEnemyAndEqualForcesBreakTogether()
    {
        var strong=StrategyBattles.Simulate(6000,2000,85,85);
        Assert.True(strong.DefenderBroke);Assert.False(strong.AttackerBroke);Assert.True(StrategyBattles.Falls(strong));
        var weak=StrategyBattles.Simulate(2000,6000,85,85);
        Assert.True(weak.AttackerBroke);Assert.False(weak.DefenderBroke);Assert.False(StrategyBattles.Falls(weak));
        var even=StrategyBattles.Simulate(3000,3000,80,80);
        Assert.True(even.AttackerBroke&&even.DefenderBroke);Assert.False(StrategyBattles.Falls(even));
        Assert.Equal(even.AttackerLost,even.DefenderLost);
    }
    [Fact]
    public void HigherMoraleHoldsLongerAndTheResultIsDeterministic()
    {
        var b=StrategyBattles.Simulate(3000,3000,100,70);
        Assert.True(b.DefenderBroke);Assert.False(b.AttackerBroke);
        Assert.Equal(b,StrategyBattles.Simulate(3000,3000,100,70));
        Assert.All(new[]{b.AttackerLost,b.DefenderLost},n=>Assert.InRange(n,1,3000));
        Assert.Throws<InvalidDataException>(()=>StrategyBattles.Simulate(0,100,80,80));
    }
}
