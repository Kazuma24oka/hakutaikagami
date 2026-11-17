using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S08（2026-11-17）：名物の目録と移り変わり・探物、水軍と海を渡る出陣、陣への補給と消耗、交易船、隊商と道中の引継ぎ。
[Trait("Tier","Heavy")]
public sealed class Logistics2Tests
{
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.Logistics2).ToArray()};
        return old;
    }
    static void Month(ContractWorld w,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;NationalStrategy.Apply(w);}
    static void Put(ContractWorld w,EntityId actor,EntityId site)=>w.Locations=w.Locations.Select(l=>l.Actor==actor?l with{Kind=LocationKind.AtSite,Site=site,Route=null}:l).ToArray();
    static string House(ContractWorld w,string name)=>w.NewGame!.Start.Factions.First(f=>f.Name==name).Id;

    [Fact]
    public void TreasuresStartWithTheirHoldersAreTakenWithCastlesAndSoldByMerchants()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var start=Logistics2.Treasures(w);
        var dojigiri=start.Single(t=>t.Name=="童子切安綱");Assert.Equal(House(w,"足利将軍家"),dojigiri.House!.Value.Value);Assert.Contains("要確認",dojigiri.Basis);
        Assert.Contains(start,t=>t.ForSale&&t.Site=="site:sakai");Assert.Contains(start,t=>t.ForSale&&t.Site=="site:hakata");
        Assert.Equal(start.Length,start.Select(t=>t.Id).Distinct().Count());
        Assert.Empty(Logistics2.Treasures(Without(w)));
        // 名物を置いた城が落ちれば、落とした家の物になり、履歴が残る。
        Month(w,1560,6);
        var owners=NationalStrategy.Owners(w,w.Strategy);
        var keep=Logistics2.Treasures(w).First(t=>t.House!=null&&!t.ForSale&&NationalStrategy.Neighbors(t.Site).Any(n=>owners.GetValueOrDefault(n) is {} o&&o!=t.House!.Value.Value));
        var attacker=NationalStrategy.Neighbors(keep.Site).Select(n=>owners.GetValueOrDefault(n)).First(o=>o!=null&&o!=keep.House!.Value.Value)!;
        var c=new StrategyCampaign(new("campaign:take-treasure"),new(attacker),keep.House!.Value,new(keep.Site),NationalStrategy.MonthStart(w,1560,6),w.CommitSequence,"border");
        long at=NationalStrategy.MonthStart(w,1560,7);w.ElapsedSeconds=at;
        var s=w.Strategy! with{Campaigns=[..w.Strategy.Campaigns,c],Outcomes=[..w.Strategy.Outcomes,new(c.Id,at,w.CommitSequence,true)]};
        s=Logistics2.Month(w,s,NationalStrategy.Owners(w,s));
        var taken=s.Treasures!.Single(t=>t.Id==keep.Id);Assert.Equal(attacker,taken.House!.Value.Value);Assert.Contains("落城",taken.History[^1]);
        // 堺・博多の売り物は、銭のある家が買う。
        var rich=Logistics2.Treasures(w).Where(t=>t.ForSale).ToArray();
        w.Strategy=w.Strategy! with{Houses=w.Strategy.Houses!.Select(h=>h with{Coin=100000}).ToArray()};
        for(int y=1561;y<=1566;y++)Month(w,y,1);
        Assert.Contains(Logistics2.Treasures(w),t=>rich.Any(r=>r.Id==t.Id)&&!t.ForSale&&t.House!=null&&t.History.Length>=2);
    }

    [Fact]
    public void ThePlayerFindsATreasureForAMerchantAndIsPaidOnReturn()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var market=MaterialMarkets.Sites(w).First(x=>x.Value=="site:sakai");
        var me=w.PlayerControl;Put(w,me,market);
        var issuer=w.Characters.First(c=>c.Id!=me&&c.Life==LifeState.Adult&&w.Locations.Any(l=>l.Actor==c.Id&&l.Kind==LocationKind.AtSite)).Id;Put(w,issuer,market);
        var offers=Logistics2.Offers(w,me);Assert.NotEmpty(offers);
        Assert.Contains(Domestic2.Board(w,me),b=>b.Kind.StartsWith("探物",StringComparison.Ordinal));
        var pick=offers.First(o=>!o.Treasure.ForSale);
        Logistics2.Work(w,me,"accept",pick.Treasure.Id);
        var job=Logistics2.Jobs(w).Single(j=>j.Worker==me);Assert.Equal("引受中",job.Phase);
        Assert.Throws<InvalidDataException>(()=>Logistics2.Work(w,me,"find",job.Id));
        Put(w,me,new(pick.Treasure.Site));w.CommitSequence++;Logistics2.Work(w,me,"find",job.Id);
        Assert.Throws<InvalidDataException>(()=>Logistics2.Work(w,me,"settle",job.Id));
        Put(w,me,market);Put(w,pick.Issuer,market);w.CommitSequence++;w.ElapsedSeconds+=3600;Logistics2.Work(w,me,"settle",job.Id);
        Assert.Equal("精算済み",Logistics2.Jobs(w).Single(j=>j.Id==job.Id).Phase);
        Assert.Equal(pick.Fee,Logistics2.Net(w,NewGameWork.Wallet(me),long.MaxValue,long.MaxValue));
        // 売り物の探物：預り金で買って、依頼主の物にする（預り金と代金は差し引きなし、礼だけが残る）。
        var buy=Logistics2.Offers(w,me).FirstOrDefault(o=>o.Treasure.ForSale);
        Assert.NotNull(buy.Treasure);
        {
            w.ElapsedSeconds+=3600;w.CommitSequence++;Logistics2.Work(w,me,"accept",buy.Treasure.Id);
            var j2=Logistics2.Jobs(w).Single(j=>j.Worker==me&&j.Phase=="引受中");Assert.True(j2.Buy);Assert.Equal(buy.Treasure.Price,j2.Advance);
            Put(w,me,new(buy.Treasure.Site));w.ElapsedSeconds+=3600;w.CommitSequence++;Logistics2.Work(w,me,"find",j2.Id);
            var bought=Logistics2.Treasures(w).Single(t=>t.Id==buy.Treasure.Id);Assert.False(bought.ForSale);Assert.Equal(buy.Issuer,bought.Person);
            Put(w,me,market);Put(w,buy.Issuer,market);w.ElapsedSeconds+=3600;w.CommitSequence++;Logistics2.Work(w,me,"settle",j2.Id);
            Assert.Equal(pick.Fee+buy.Fee,Logistics2.Net(w,NewGameWork.Wallet(me),long.MaxValue,long.MaxValue));
        }
    }

    [Fact]
    public void CrossingTheSeaNeedsAFleetAndShortSuppliesWeakenTheAttack()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var owners=NationalStrategy.Owners(w,w.Strategy);
        // 海に面した城の無い家が、別の島の城を攻めると、水軍が無いので渡れない。
        var landlocked=w.Strategy!.Houses!.Select(h=>h.House.Value).First(h=>owners.Any(x=>x.Value==h)&&!owners.Any(x=>x.Value==h&&Logistics2.Coastal(x.Key))&&Logistics2.Fleet(w,h,owners)==0);
        var target=owners.First(x=>x.Value!=null&&x.Value!=landlocked&&CentralStanding.ProvinceOf(new(x.Key)) is "阿波" or "讃岐" or "伊予" or "土佐" or "筑前" or "豊後");
        var c=new StrategyCampaign(new("campaign:cross"),new(landlocked),new(target.Value!),new(target.Key),w.ElapsedSeconds,1,"border");
        Assert.True(Logistics2.Crossing(w,c));
        var (a,_)=Logistics2.Effective(w,w.Strategy,c,1000,1000);Assert.True(a<=300);
        var (oa,_)=Logistics2.Effective(Without(w),w.Strategy,c,1000,1000);Assert.Equal(1000,oa);
        // 蔵米が無ければ兵糧を持ち出せず、攻めの力が落ちる。守る城の兵糧が尽きれば守りが弱る。
        var land=owners.First(x=>x.Value!=null&&NationalStrategy.Neighbors(x.Key).Any(n=>owners.GetValueOrDefault(n) is {} o&&o!=x.Value));
        var foe=NationalStrategy.Neighbors(land.Key).Select(n=>owners.GetValueOrDefault(n)).First(o=>o!=null&&o!=land.Value)!;
        var c2=new StrategyCampaign(new("campaign:supply"),new(foe),new(land.Value!),new(land.Key),w.ElapsedSeconds,1,"border");
        var hungry=w.Strategy with{Houses=w.Strategy.Houses!.Select(h=>h.House.Value==foe||h.House.Value==land.Value?h with{Rice=0}:h).ToArray()};
        var line=Logistics2.Supply(w,hungry,c2,1000,1000);Assert.Equal(0,line.Delivered);Assert.Equal(0,line.DefenderStock);
        var (a2,d2)=Logistics2.Effective(w,hungry,c2,1000,1000);Assert.True(a2<=600);Assert.True(d2<=700);
        var fed=w.Strategy with{Houses=w.Strategy.Houses!.Select(h=>h with{Rice=10_000_000}).ToArray()};
        var full=Logistics2.Supply(w,fed,c2,1000,1000);Assert.True(full.Delivered>0);Assert.True(full.DefenderStock>=full.DefenderNeed);
    }

    [Fact]
    public void TradeShipsSailAndReturnAndCaravansCarryGoodsAcrossBorders()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        // 品の余る家を作る（軍馬と銭）。
        Month(w,1560,6);
        w.Strategy=w.Strategy! with{Houses=w.Strategy.Houses!.Select((h,i)=>i%5==0?h with{Goods=h.Goods with{Horses=2000,Gold=200},Coin=5000}:h with{Coin=Math.Max(h.Coin,1000)}).ToArray()};
        for(int m=7;m<=12;m++)Month(w,1560,m);
        var ev=Logistics2.Events(w);
        Assert.Contains(ev,e=>e.Kind=="voyage");Assert.Contains(ev,e=>e.Kind is "return" or "wreck" or "plunder");
        Assert.All(ev.Where(e=>e.Kind=="voyage"),e=>Assert.True(e.Arrives>e.At));
        var caravan=ev.First(e=>e.Kind=="caravan");Assert.Contains("軍馬=",caravan.Cargo);
        Assert.Contains(ev,e=>e.Kind=="delivered"&&e.House==caravan.Other&&e.At>=caravan.Arrives);
        Assert.True(Logistics2.Store(w,caravan.House!.Value)!.Fleet>=0);
        Logistics2.Validate(w,w.Strategy!,null);
    }

    [Fact]
    public void HorsesEatFodderAndIronBecomesGear()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var h=w.Strategy!.Houses!.First(x=>x.Goods.Horses>0);
        w.Strategy=w.Strategy with{Houses=w.Strategy.Houses!.Select(x=>x.House==h.House?x with{Rice=0,Goods=x.Goods with{Horses=1000,Iron=50}}:x).ToArray()};
        long gear=Logistics2.Store(w,h.House)?.Gear??0;
        Month(w,1560,7);
        var after=Domestic2.House(w,h.House)!;Assert.True(after.Goods.Horses<1000+200);
        Assert.Contains(Logistics2.Events(w),e=>e.Kind=="fodder"&&e.House==h.House);
        Assert.True(Logistics2.Store(w,h.House)!.Gear>gear);
        var old=Without(w);old.Strategy=old.Strategy! with{Logistics=null,Treasures=null,Stores=null};Month(old,1560,8);Assert.Null(old.Strategy!.Logistics);Assert.Null(old.Strategy.Treasures);
    }
}
