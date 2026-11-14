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
// 離反を主家へ知らせる書状（DefectionLetters、2026-11-14）：離れた家の当主から主家の当主へ、封をした書状が実際に運ばれる。
public sealed class DefectionLetterTests
{
    [Fact]
    public void TheDefectingHeadSendsASealedLetterToTheFormerOverlord()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(NationalSystems.On(w,"DefectionLetters"));
        var player=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var pair=w.NewGame.Start.Factions.Select(f=>(House:f.Id,Overlord:NationalStrategy.DbOverlord(f.Id))).First(x=>x.Overlord!=null&&x.House!=player&&x.Overlord!=player&&w.NewGame.Start.Factions.Any(f=>f.Id==x.Overlord));
        w.CommitSequence++;
        NationalStrategy.DefectionLetter(w,new(new(pair.House),new(pair.Overlord!),w.ElapsedSeconds,w.CommitSequence));
        var m=Assert.Single(w.Messaging.Messages,m=>m.CorrelationId=="defection:"+pair.House);
        Assert.Equal(HouseSuccession.Head(w,new(pair.House)).Value,m.SenderId);
        Assert.Equal(HouseSuccession.Head(w,new(pair.Overlord!)).Value,m.RecipientId);
        Assert.Contains(w.SealedMessages,id=>id.Value==m.Id);
        Assert.Contains(w.Messaging.Attempts,a=>a.MessageId==m.Id); // 書状は運ばれる
    }
}
// 独立した家の城（StrategyIndependence、2026-11-14）：家の戦略では元の家の城に数えない。
public sealed class StrategyIndependenceTests
{
    [Fact]
    public void ACastleHeldByADeclaredIndependentHouseIsNoLongerTheFormerHouses()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(NationalSystems.On(w,"StrategyIndependence"));
        w.Strategy??=new(1,NationalStrategy.Policy,[],[]);
        var castle=w.NewGame!.Start.Castles.First(c=>c.Faction!=null&&w.NewGame.Start.Factions.Single(f=>f.Id==c.Faction).Home!=c.Id);
        var house=new EntityId("org:test/independent");
        Assert.Equal(castle.Faction,NationalStrategy.Owners(w,w.Strategy)[castle.Id]);
        w.Declarations=[new(new("pact:test"),new("person:test"),new(castle.Faction!),house,new(castle.Id),0,0)];
        w.LandRights=[..w.LandRights,new(new("right:test/independent"),house,new(castle.Id),"control",new("pact:test"),0,long.MaxValue,0)];
        Assert.Equal(house.Value,NationalStrategy.Owners(w,w.Strategy)[castle.Id]); // 独立した家のもの（IndependentHouses）
    }
}
// 再興の挙兵への後援（RestorationSupport、2026-11-14）：落とした家に一年以内に城を奪われた家は、兵力の半分で後援する。
public sealed class RestorationSupportTests
{
    [Fact]
    public void HousesThatLostCastlesToTheConquerorSupportTheUprising()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(NationalSystems.On(w,"RestorationSupport"));
        var f=w.NewGame!.Start.Factions.OrderBy(x=>x.Id,StringComparer.Ordinal).Take(3).ToArray();
        var (fallen,conqueror,victim)=(f[0],f[1],f[2]);
        var victimCastle=w.NewGame.Start.Castles.First(c=>c.Faction==victim.Id&&c.Id!=victim.Home);
        w.ElapsedSeconds=60*86400;
        var earlier=new StrategyCampaign(new("campaign:national/test/earlier"),new(conqueror.Id),new(victim.Id),new(victimCastle.Id),0,1,"border");
        var uprising=new StrategyCampaign(new("campaign:national/test/uprising"),new(fallen.Id),new(conqueror.Id),new(conqueror.Home),30*86400,2,NationalStrategy.Restoration);
        var s=new StrategyState(1,NationalStrategy.Policy,[earlier,uprising],[new(earlier.Id,30*86400,2,true)]);
        w.Strategy=s;
        var outcome=NationalStrategy.Settle(w,s,s.Outcomes.ToList(),uprising);
        Assert.Equal([new EntityId(victim.Id)],outcome.Supporters);
        // 一覧を持たない世界では後援しない。
        w.NewGame=w.NewGame with{NationalSystems=w.NewGame.NationalSystems!.Where(x=>x!="RestorationSupport").ToArray()};
        Assert.Null(NationalStrategy.Settle(w,s,s.Outcomes.ToList(),uprising).Supporters);
    }
}
// 落城した城の帳簿（ConquestDomains、2026-11-14）：その時点の持ち主の家の当主が扱い、前の持ち主の当主は扱えない。
public sealed class ConquestDomainsTests
{
    [Fact]
    public void TheConquerorsHeadManagesTheFallenCastlesDomain()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(NationalSystems.On(w,"ConquestDomains"));
        var player=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var site=MaterialMarkets.Sites(w).First(x=>RegionalDomains.Owner(w,x) is {} o&&o.Value!=player&&w.NewGame.Start.Factions.Single(f=>f.Id==o.Value).Home!=x.Value);
        var loser=RegionalDomains.Owner(w,site)!.Value;
        var winner=w.NewGame.Start.Factions.First(f=>f.Id!=loser.Value&&f.Id!=player).Id;
        var c=new StrategyCampaign(new("campaign:national/test/conquest"),new(winner),loser,site,0,1,"border");
        w.Strategy=new(1,NationalStrategy.Policy,[c],[new(c.Id,0,2,true)]);w.CommitSequence=3;w.ElapsedSeconds=60;
        var newHead=HouseSuccession.Head(w,new(winner));var oldHead=HouseSuccession.Head(w,loser);
        Assert.Equal(newHead,RegionalDomains.Head(w,site));
        Assert.True(RegionalDomains.Authority(w,newHead,site,60,4));
        Assert.False(RegionalDomains.Authority(w,oldHead,site,60,4));
        // 村・商い・代理行政も今の持ち主の家を読む。
        Assert.Equal(new EntityId(winner),RegionalDomains.CurrentOwner(w,site,60,4));
        Assert.Null(RegionalDomains.Conquered(w,site,0,0)); // 落ちる前
        w.NewGame=w.NewGame with{NationalSystems=w.NewGame.NationalSystems!.Where(x=>x!="ConquestDomains").ToArray()};
        Assert.Equal(loser,RegionalDomains.CurrentOwner(w,site,60,4)); // 一覧を持たない世界は開始時の持ち主
    }
}
// 独立した家の城（IndependentHouses、2026-11-15）：独立した家のものとして数え、守る兵は元の家の城一つ分。宣言の後に落ちた城は落とした家のもの。
public sealed class IndependentHouseTests
{
    [Fact]
    public void AnIndependentHouseHoldsItsCastleAndCanLoseIt()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(NationalSystems.On(w,"IndependentHouses"));
        w.Strategy??=new(1,NationalStrategy.Policy,[],[]);
        var castle=w.NewGame!.Start.Castles.First(c=>c.Faction!=null&&w.NewGame.Start.Factions.Single(f=>f.Id==c.Faction).Home!=c.Id&&NationalStrategy.Cap(c.Faction!)>0);
        var former=castle.Faction!;var house=new EntityId("org:test/independent");
        var founder=w.NewGame.Start.People.First(p=>p.Faction==former&&p.Id!=w.NewGame.Start.Factions.Single(f=>f.Id==former).Head).Id;
        w.Declarations=[new(new("pact:test"),new(founder),new(former),house,new(castle.Id),0,0)];
        w.LandRights=[..w.LandRights,new(new("right:test/independent"),house,new(castle.Id),"control",new("pact:test"),0,long.MaxValue,0)];
        var owners=NationalStrategy.Owners(w,w.Strategy);
        Assert.Equal(house.Value,owners[castle.Id]);
        int start=w.NewGame.Start.Castles.Count(c=>c.Faction==former);
        Assert.Equal(Math.Max(50L,NationalStrategy.Cap(former)/start),NationalStrategy.Powers(w,owners)[house.Value]);
        // 宣言の後に出陣で落ちた城は、落とした家のもの。
        var attacker=w.NewGame.Start.Factions.First(f=>f.Id!=former).Id;
        var c=new StrategyCampaign(new("campaign:national/test/indep"),new(attacker),house,new(castle.Id),60,1,"border");
        w.Strategy=new(1,NationalStrategy.Policy,[c],[new(c.Id,60,2,true)]);w.ElapsedSeconds=120;
        Assert.Equal(attacker,NationalStrategy.Owners(w,w.Strategy)[castle.Id]);
        // 一覧を持たない世界は今までどおり誰の城にも数えない。
        w.Strategy=new(1,NationalStrategy.Policy,[],[]);
        w.NewGame=w.NewGame with{NationalSystems=w.NewGame.NationalSystems!.Where(x=>x!="IndependentHouses").ToArray()};
        Assert.Null(NationalStrategy.Owners(w,w.Strategy)[castle.Id]);
    }
}
