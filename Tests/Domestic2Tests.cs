using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S07B（2026-11-17）：全国の家の内政（方針と専門職・家計・生産と職人・災害・徴募と逃散・一揆と檄・寺社・破壊の計略・幕府職・口入れ・本人の家の指示）。
[Trait("Tier","Heavy")]
public sealed class Domestic2Tests
{
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.Domestic2).ToArray()};
        return old;
    }
    static void Month(ContractWorld w,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;NationalStrategy.Apply(w);}
    static ContractWorld Copy(ContractWorld w)=>ContractJson.Read<ContractWorld>(ContractJson.Write(w));

    [Fact]
    public void HousesSetPoliciesAppointSpecialistsAndKeepABudget()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());var old=Without(w);
        Month(w,1560,6);
        var houses=w.Strategy!.Houses!;Assert.True(houses.Length>20);
        Assert.All(houses,h=>Assert.Contains(h.Policy,new[]{Domestic2.Farming,Domestic2.Trade,Domestic2.Arms,Domestic2.Building,Domestic2.Thrift}));
        var npc=houses.Where(h=>HouseSuccession.Head(w,h.House)!=w.PlayerControl&&h.Posts.Length>=4).ToArray();Assert.NotEmpty(npc);
        foreach(var h in npc)
        {
            Assert.All(h.Posts,p=>Assert.Equal(h.House.Value,w.NewGame!.Start.People.Single(x=>x.Id==p.Person.Value).Faction));
            Assert.Equal(h.Posts.Length,h.Posts.Select(p=>p.Person).Distinct().Count());
            Assert.True(h.Posts.Any(p=>p.Post==Domestic2.Intendant&&p.Place!=null),string.Join(",",h.Posts.Select(p=>p.Post+"/"+p.Place))+" "+h.House.Value);
        }
        // 年貢の布告は、本拠から遠い拠点ほど遅れて効く。
        var decree=w.Strategy.Domestic!.First(e=>e.Kind=="decree"&&e.Amount!=50);
        var mine=NationalStrategy.Owners(w,w.Strategy).Where(x=>x.Value==decree.House!.Value.Value).Select(x=>x.Key).ToArray();
        var far=mine.MaxBy(x=>Domestic2.Days(w.NewGame!.Start.Factions.Single(f=>f.Id==decree.House!.Value.Value).Home,x))!;
        Assert.Equal(50,Domestic2.RateAt(w,far,decree.House!.Value.Value,decree.At));
        Assert.Equal((int)decree.Amount,Domestic2.RateAt(w,far,decree.House!.Value.Value,decree.At+400*86400L));
        // 10月の年貢で蔵米が入る。
        foreach(var m in new[]{7,8,9,10})Month(w,1560,m);
        Assert.Contains(w.Strategy.Houses!,h=>h.Rice>0);
        // 本人の家の記録（家系・歴代・帳面）。
        var mineHouse=new EntityId(w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction!);
        var record=Domestic2.FamilyRecord(w,w.PlayerControl,mineHouse);
        Assert.Contains(record,r=>r.StartsWith("歴代",StringComparison.Ordinal));Assert.Contains(record,r=>r.StartsWith("方針",StringComparison.Ordinal));
        Domestic2.Validate(w,w.Strategy,null);
        Month(old,1560,6);Assert.Null(old.Strategy!.Houses);Assert.Null(old.Strategy.Domestic);
    }

    [Fact]
    public void CraftsmenAppearByHistoryModeAndGunsNeedIronAndSaltpeter()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Month(w,1560,6);
        var crafts=Domestic2.Crafts(w);
        Assert.Contains(crafts,c=>c.Name=="国友の鉄砲鍛冶");Assert.Contains(crafts,c=>c.Name=="穴太衆");
        Assert.DoesNotContain(crafts,c=>c.Id=="sado");
        // 仮想の世界では、開始の年の後の職人は現れない（史実尊重では年表どおり現れる）。
        var fict=Copy(w);fict.NewGame=fict.NewGame! with{Judgment=fict.NewGame.Judgment! with{History=HistoryMode.Fictional}};fict.Strategy=fict.Strategy! with{Crafts=null};
        Month(fict,1601,6);Assert.DoesNotContain(Domestic2.Crafts(fict),c=>c.Id=="sado");
        var hist=Copy(w);hist.NewGame=hist.NewGame! with{Judgment=hist.NewGame.Judgment! with{History=HistoryMode.Historical}};hist.Strategy=hist.Strategy! with{Crafts=null};
        Month(hist,1601,6);Assert.Contains(Domestic2.Crafts(hist),c=>c.Id=="sado");
        // 鉄砲鍛冶の拠点を持つ家は鉄砲を作り、硝石の入る家だけが火薬を作る。
        for(int m=7;m<=9;m++)Month(w,1560,m);
        var owners=NationalStrategy.Owners(w,w.Strategy);
        var gunHouses=crafts.Where(c=>c.Kind=="gun").Select(c=>owners.GetValueOrDefault(c.Site)).Where(x=>x!=null).Distinct().ToArray();
        Assert.NotEmpty(gunHouses);
        Assert.All(gunHouses,h=>Assert.True(Domestic2.House(w,new(h!))!.Goods.Guns>0));
        Assert.All(w.Strategy!.Houses!.Where(h=>!gunHouses.Contains(h.House.Value)),h=>Assert.Equal(0,h.Goods.Guns));
        Assert.All(w.Strategy.Houses!.Where(h=>h.Goods.Powder>0),h=>Assert.Contains(h.House.Value,gunHouses));
        Assert.Contains(w.Strategy.Houses!,h=>h.Goods.Horses>0);Assert.Contains(w.Strategy.Houses!,h=>h.Goods.Gold+h.Goods.Silver>0);
    }

    [Fact]
    public void DisastersLeaveDamageThatMagistratesRepairAndTheNewsTravels()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        DomesticEvent? hit=null;
        for(int y=1560;y<=1562&&hit==null;y++)for(int m=y==1560?6:1;m<=12&&hit==null;m++){Month(w,y,m);hit=Domestic2.Events(w).FirstOrDefault(e=>e.Kind=="disaster");}
        Assert.NotNull(hit);
        var damaged=w.Strategy!.Sites!.Where(x=>x.Agriculture+x.Commerce+x.Sections>0).ToArray();Assert.NotEmpty(damaged);
        var date=WorldCalendar.At(w.EpochDate,w.ElapsedSeconds);
        for(int i=1;i<=4;i++){var d=date.AddMonths(i);Month(w,d.Year,d.Month);}
        Assert.Contains(Domestic2.Events(w),e=>e.Kind=="repair");
        // 被害の知らせは、本人のところへ道のりに応じて遅れて届く。
        var rumor=Domestic2.Heard(w,w.PlayerControl,w.ElapsedSeconds).FirstOrDefault(r=>r.Id==hit!.Id);
        Assert.NotNull(rumor);Assert.True(rumor!.LearnedAt>hit!.At);
        Assert.DoesNotContain(Domestic2.Heard(w,w.PlayerControl,hit.At),r=>r.Id==hit.Id);
    }

    [Fact]
    public void SoldiersAreLeviedFromVillagesAndComeHomeAfterTheBattle()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var owners=NationalStrategy.Owners(w,null);var me=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var pick=owners.Where(x=>x.Value!=null&&x.Value!=me).SelectMany(x=>NationalStrategy.Neighbors(x.Key).Where(n=>owners.GetValueOrDefault(n) is {} o&&o!=x.Value&&o!=me).Select(n=>(A:x.Value!,D:owners[n]!,Site:n))).OrderBy(t=>t.Site,StringComparer.Ordinal).First();
        long t0=NationalStrategy.MonthStart(w,1560,5);
        var c=new StrategyCampaign(new("campaign:test-levy"),new(pick.A),new(pick.D),new(pick.Site),t0,1,"border");
        w.Strategy=new StrategyState(1,NationalStrategy.Policy,[c],[]);
        Month(w,1560,6);
        var levied=w.Strategy!.Sites!.Where(x=>x.LevyCampaign==c.Id.Value).ToArray();
        Assert.NotEmpty(levied);var after=NationalStrategy.Owners(w,w.Strategy);Assert.All(levied,x=>Assert.Equal(pick.A,after[x.Site]));
        Assert.All(levied,x=>Assert.InRange(x.Levied,1,Domestic2.Population(w,x.Site)/2*3/10));
        Assert.Contains(w.Strategy.Outcomes,o=>o.Campaign==c.Id);
        Month(w,1560,7);
        Assert.DoesNotContain(w.Strategy!.Sites??[],x=>x.LevyCampaign==c.Id.Value);
    }

    [Fact]
    public void AngryVillagesFleeRiseAndAreSettledByTheLordOrAProxy()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var owners=NationalStrategy.Owners(w,w.Strategy);var me=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var site=owners.Where(x=>x.Value!=null&&x.Value!=me&&x.Key.StartsWith("site:c-",StringComparison.Ordinal)&&NationalStrategy.Neighbors(x.Key).Length>0).OrderBy(x=>x.Key,StringComparer.Ordinal).First();
        var neighbor=NationalStrategy.Neighbors(site.Key).First();
        // 不信が重い村は一月に一段ずつ進む（隣が逃散していなければ連合にはならない）。
        w.Strategy=w.Strategy! with{Sites=[..(w.Strategy.Sites??[]).Where(x=>x.Site!=site.Key&&x.Site!=neighbor),new(site.Key,40,0,0,2,0,null,0,null,0,12,null,0),new(neighbor,0,0,0,3,0,null,0,null,0,12,null,0)]};
        Month(w,1560,7);
        var d=Domestic2.Site(w,site.Key);Assert.Equal(3,d.Unrest);Assert.True(d.Fled>0);Assert.NotNull(d.FledTo);
        Assert.Contains(Domestic2.Events(w),e=>e.Kind=="unrest"&&e.Site==site.Key&&e.Amount==3);
        // 逃散の拠点で田畑が荒れていれば、飢えて亡くなる者が出て人口が減る。
        Assert.True(d.Lost>0);Assert.Contains(Domestic2.Events(w),e=>e.Kind=="famine"&&e.Site==site.Key);
        // 隣も逃散のままなら、連合に進む。
        w.Strategy=w.Strategy! with{Sites=[..w.Strategy.Sites!.Where(x=>x.Site!=neighbor),Domestic2.Site(w,neighbor) with{Unrest=3,Distrust=12,Ikki=null}]};
        Month(w,1560,8);
        // 連合に進むと、領主（または代わりの郡代・家老）が救済・鎮圧・減免のどれかで応じる。
        Assert.Contains(Domestic2.Events(w),e=>e.Site==site.Key&&e.Kind is "negotiate" or "suppress"||e.Kind=="decree"&&e.House?.Value==site.Value&&e.At==w.ElapsedSeconds);
        // 本願寺の檄（1570年9月）：届いた門徒の拠点が自分で応じるかを決める。
        var hist=Copy(w);hist.NewGame=hist.NewGame! with{Judgment=hist.NewGame.Judgment! with{History=HistoryMode.Historical}};Month(hist,1570,9);
        var call=Domestic2.Events(hist).Single(e=>e.Kind=="call");
        Month(hist,1570,10);Month(hist,1570,11);
        var answers=Domestic2.Events(hist).Where(e=>e.Kind=="answer").ToArray();Assert.NotEmpty(answers);
        Assert.All(answers,a=>Assert.True(Territory2.Profile(hist,new(a.Site!))!.Monto>0));
        Assert.All(answers,a=>Assert.True(a.At>=call.At+Domestic2.Days(call.Site!,a.Site!)*86400L));
        Assert.Contains(answers,a=>a.Amount==0);
    }

    [Fact]
    public void TemplesResistInfringementInStagesAndArmedTemplesJoinBattles()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        Assert.Contains(Domestic2.Temples(w),t=>t.Temple=="延暦寺"&&t.Lord!=null&&t.Relation>=0);
        var t=Domestic2.Temples(w).First(t=>t.Temple=="延暦寺");
        // 領主が楽市（商の方針）を取ると、権益を侵された寺は段階を進める。
        var lord=t.Lord!.Value;
        w.Strategy=w.Strategy! with{Houses=w.Strategy.Houses!.Select(h=>h.House==lord?h with{Policy=Domestic2.Trade}:h).ToArray(),Temples=w.Strategy.Temples!.Select(x=>x.Temple=="延暦寺"?x with{Relation=0}:x).ToArray()};
        for(int y=1567;y<=1567;y++)for(int m=1;m<=12;m++){if(HouseSuccession.Head(w,lord)!=w.PlayerControl)w.Strategy=w.Strategy! with{Houses=w.Strategy.Houses!.Select(h=>h.House==lord?h with{Policy=Domestic2.Trade}:h).ToArray()};Month(w,y,m);}
        var stages=Domestic2.Events(w).Where(e=>e.Kind=="temple"&&e.Text.StartsWith("延暦寺",StringComparison.Ordinal)).Select(e=>e.Amount).ToArray();
        Assert.NotEmpty(stages);Assert.Equal(1,stages[0]);Assert.All(stages.Zip(stages.Skip(1)),p=>Assert.True(p.Second==p.First+1||p.Second<=p.First));
        Assert.Contains(Domestic2.Events(w),e=>e.Kind=="rakuichi"&&e.House==lord);
        // 関係の良い武装した大寺社は、その国の合戦で領主に僧兵を出す。
        var site=t.Site;var friend=Copy(w);friend.Strategy=friend.Strategy! with{Temples=friend.Strategy.Temples!.Select(x=>x.Temple=="延暦寺"?x with{Lord=lord,Relation=5,Stage=0,Ally=null}:x).ToArray()};
        var c=new StrategyCampaign(new("campaign:test-temple"),new("faction:test-attacker"),lord,new(site),friend.ElapsedSeconds,1,"border");
        var (a,d)=Domestic2.Effective(friend,friend.Strategy,c,1000,1000);Assert.Equal(1000,a);Assert.True(d>=1800);
        var hostile=Copy(w);hostile.Strategy=hostile.Strategy! with{Temples=hostile.Strategy.Temples!.Select(x=>x.Temple=="延暦寺"?x with{Lord=lord,Relation=0,Stage=5,Ally=null}:x).ToArray()};
        var c2=c with{Attacker=lord,Defender=new("faction:test-defender")};
        var (a2,d2)=Domestic2.Effective(hostile,hostile.Strategy,c2,1000,1000);Assert.True(a2>=1000);Assert.True(d2>=1800);
    }

    [Fact]
    public void ScarceRiceIsSharedAndIndivisibleItemsAreAwardedByLot()
    {
        var (paid,shortfall)=Domestic2.Allocate(90,[60,30,0]);Assert.Equal([60L,30L,0L],paid);Assert.Equal(0,shortfall);
        (paid,shortfall)=Domestic2.Allocate(60,[60,30]);Assert.Equal([40L,20L],paid);Assert.Equal(30,shortfall);
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var pick=Domestic2.Award(w,"horse-1",["a","b","c"]);Assert.Equal(pick,Domestic2.Award(w,"horse-1",["c","b","a"]));
    }

    [Fact]
    public void TheShogunatePostsStartFromTheRecordsAndTandaiMediateTheirRegion()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var holders=Domestic2.InitialHolders(w);
        Assert.Contains(Domestic2.Kanrei,holders.Keys);Assert.Contains(Domestic2.KyushuTandai,holders.Keys);
        Assert.StartsWith("大友",w.NewGame!.Start.Factions.Single(f=>f.Id==holders[Domestic2.KyushuTandai]).Name,StringComparison.Ordinal);
        var tandai=holders[Domestic2.KyushuTandai];
        var target=NationalStrategy.Owners(w,null).First(x=>x.Value!=null&&x.Value!=tandai&&CentralStanding.ProvinceOf(new(x.Key)) is "豊前" or "筑前" or "肥前");
        var c=new StrategyCampaign(new("campaign:test-tandai"),new(tandai),new(target.Value!),new(target.Key),0,1,"border");
        Month(w,1560,6);
        var (a,_)=Domestic2.Effective(w,w.Strategy!,c,1000,1000);Assert.True(a>=1100);
        Assert.Contains(Domestic2.Authority(w,tandai,w.ElapsedSeconds),r=>r.StartsWith(Domestic2.KyushuTandai,StringComparison.Ordinal));
        Assert.Contains(Domestic2.Duties.Keys,k=>k==Domestic2.Intendant);
        Assert.All(new[]{Domestic2.Elder,Domestic2.WorksMagistrate,Domestic2.Treasurer,Domestic2.Inspector,Domestic2.Envoy,Domestic2.TempleMagistrate,Domestic2.Deputy,Domestic2.Mandokoro,Domestic2.Samurai},k=>Assert.True(Domestic2.Duties.ContainsKey(k)));
        Assert.Empty(Domestic2.InitialHolders(NationalStart.Create(NationalStart.Scenario(NationalStart.RevenueEdition,NationalSystems.ForYear(1600)).Factions[0].Head,year:1600)));
    }

    [Fact]
    public void HousesReformAndLordsGiveNamesToTheirMostMeritoriousRetainers()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        // 勲功を2度立てた家臣（当主の名の一字か姓を受けていない人）を一人用意する。
        var me=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var house=w.Strategy!.Houses!.First(h=>h.House.Value!=me&&h.Posts.Length>=3&&NationalStart.DbPerson(HouseSuccession.Head(w,h.House).Value)?.Imina is {Length:>=2}).House;
        var retainer=w.Strategy.Houses!.Single(h=>h.House==house).Posts[0].Person;
        w.Strategy=w.Strategy with{Merits=[new(new("campaign:a"),retainer,house,"落城",w.ElapsedSeconds,1),new(new("campaign:b"),retainer,house,"首級",w.ElapsedSeconds,1)]};
        for(int m=7;m<=12;m++)Month(w,1560,m);
        var gift=w.Strategy!.Honors!.Single(h=>h.Person==retainer&&h.Kind is "偏諱" or "与姓");
        Assert.Equal(house,gift.House);Assert.Equal(HouseSuccession.Head(w,house),gift.Shogun);
        Month(w,1561,1);Month(w,1562,1);
        var reforms=Domestic2.Events(w).Where(e=>e.Kind=="reform").ToArray();Assert.NotEmpty(reforms);
        Assert.All(reforms.GroupBy(e=>e.House),g=>{var names=g.Select(e=>e.Text.Split('：')[^1]).ToArray();if(names.Contains(Domestic2.Kenchi))Assert.Contains(Domestic2.Sashidashi,names);});
        Assert.Contains(Domestic2.FamilyRecord(w,w.PlayerControl,reforms[0].House!.Value),r=>r.StartsWith("改革",StringComparison.Ordinal));
        // 奉公衆：将軍家の成人で将軍でない人。
        var shogunHouse=NationalStart.FactionId(NationalStrategy.Shogun);
        var bakushin=FamilyBirth.People(w).FirstOrDefault(p=>p.Faction==shogunHouse&&w.Characters.Single(c=>c.Id.Value==p.Id).Life==LifeState.Adult&&SupremeOffice.ShogunAt(w,w.ElapsedSeconds,w.CommitSequence)?.Value!=p.Id);
        if(bakushin!=null)Assert.Contains(Domestic2.Hokoshu,CentralStanding.KnownTitles(w,w.PlayerControl,new(bakushin.Id)));
    }

    [Fact]
    public void ThePlayerHeadDecidesTheHousePolicyDecreeAndReliefAndSeesTheJobBoard()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var session=new WorldSession(Copy(w));
        var r=session.DecideDomestic("policy",Domestic2.Trade,0,session.Revision);Assert.Equal(ResultCode.Accepted,r.Code);
        r=session.DecideDomestic("decree",null,40,session.Revision);Assert.Equal(ResultCode.Accepted,r.Code);
        Assert.Equal(ResultCode.NotAvailable,session.DecideDomestic("decree",null,90,session.Revision).Code);
        var state=session.DiagnosticSnapshot();var house=new EntityId(state.NewGame!.Start.People.Single(p=>p.Id==state.PlayerControl.Value).Faction!);
        var h=Domestic2.House(state,house)!;Assert.Equal(Domestic2.Trade,h.Policy);Assert.Equal(40,h.TaxRate);
        Assert.Contains(Domestic2.Events(state),e=>e.Kind=="decree"&&e.House==house&&e.Amount==40);
        // 口入れ：本人のいる拠点の実在の人物の仕事がまとまって並ぶ。
        var board=Domestic2.Board(state,state.PlayerControl);
        Assert.All(board,b=>Assert.Equal(SocialWorld.Site(state,state.PlayerControl),SocialWorld.Site(state,b.Issuer)));
        Assert.Empty(Domestic2.Board(Without(state),state.PlayerControl));
        var old=Without(w);Assert.Throws<InvalidDataException>(()=>Domestic2.Decide(old,old.PlayerControl,"policy",Domestic2.Trade,0));
    }
}
