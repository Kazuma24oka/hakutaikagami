using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// 遠方の合戦の損害を家の兵力に残す（StrategyLosses、2026-11-14）：失った兵は180日かけて戻り、守る側の損害は守る家と援軍で 2:1 に分ける。
public sealed class StrategyLossesTests
{
    [Fact]
    public void BattleLossesWeakenTheHousesAndRecoverOverHalfAYear()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(NationalSystems.On(w,"StrategyLosses"));
        var f=w.NewGame!.Start.Factions.OrderBy(x=>x.Id,StringComparer.Ordinal).Take(3).Select(x=>x.Id).ToArray();
        var c=new StrategyCampaign(new("campaign:national/test/loss"),new(f[0]),new(f[1]),new(w.NewGame.Start.Factions.Single(x=>x.Id==f[1]).Home),0,1,"border");
        var battle=new StrategyBattle(1000,900,100,100,600,300,120,false,true);
        w.Strategy=new(1,NationalStrategy.Policy,[c],[new(c.Id,86400,2,true){Allies=[new(f[2])],Battle=battle}]);
        long At(long day){w.ElapsedSeconds=86400+day*86400;var l=NationalStrategy.Losses(w);return l.GetValueOrDefault(f[0]);}
        Assert.Equal(600,At(0));
        Assert.Equal(300,At(90));
        Assert.Equal(0,At(180));
        w.ElapsedSeconds=86400;var now=NationalStrategy.Losses(w);
        Assert.Equal(200,now[f[1]]); // 守る家：300×2/3
        Assert.Equal(100,now[f[2]]); // 援軍：300×1/3
        // 一覧を持たない世界（旧保存）では損害を残さない。
        w.NewGame=w.NewGame with{NationalSystems=w.NewGame.NationalSystems!.Where(x=>x!="StrategyLosses").ToArray()};
        Assert.Empty(NationalStrategy.Losses(w));
    }
}
