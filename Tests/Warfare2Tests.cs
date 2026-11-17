using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S05（2026-11-17）：遠方の合戦の天候と地形・NPCの開城・負傷兵・捕縛からの逃亡・落城の勲功と知行。
[Trait("Tier","Heavy")]
public sealed class Warfare2Tests
{
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.Warfare2).ToArray()};
        return old;
    }
    // 一番強い家が、隣の城を持つ一番弱い家（操作する人物の家でない）を攻める出陣を、1月の初めに置く。
    static (ContractWorld W,StrategyCampaign C) Setup(ContractWorld w)
    {
        var owners=NationalStrategy.Owners(w,null);var power=NationalStrategy.Powers(w,owners);var me=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var pick=owners.Where(x=>x.Value!=null&&x.Value!=me).SelectMany(x=>NationalStrategy.Neighbors(x.Key).Where(n=>owners.GetValueOrDefault(n) is {} o&&o!=x.Value&&o!=me).Select(n=>(A:x.Value!,D:owners[n]!,Site:n)))
            .OrderByDescending(t=>power.GetValueOrDefault(t.A,50)*100/Math.Max(1,power.GetValueOrDefault(t.D,50))).ThenBy(t=>t.Site,StringComparer.Ordinal).First();
        long t0=NationalStrategy.MonthStart(w,1560,1);
        var c=new StrategyCampaign(new("campaign:test-war2"),new(pick.A),new(pick.D),new(pick.Site),t0,1,"border");
        w.Strategy=new StrategyState(1,NationalStrategy.Policy,[c],[]);
        return (w,c);
    }
    static void Month(ContractWorld w,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;NationalStrategy.Apply(w);}

    [Fact]
    public void CastleTypesStrengthenTheDefenseAndRainWeakensTheAttack()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var sites=CastleRoutes.Catalog.Nodes.Select(n=>new EntityId("site:"+n.Id)).ToArray();
        Assert.Contains(sites,s=>Warfare2.CastleType(s)=="山城"&&Warfare2.DefensePercent(s)==150);
        Assert.Contains(sites,s=>Warfare2.CastleType(s)=="平城"&&Warfare2.DefensePercent(s)==100);
        var site=sites.First();
        for(long d=0;d<60;d++){long at=d*86400;int p=Warfare2.AttackPercent(w,site,at);Assert.Equal(BattlefieldRules.Weather(w,site,at) is BattleWeather.Rain or BattleWeather.Snow?80:100,p);}
    }

    [Fact]
    public void AWeakDefenderWithoutHelpSurrendersAndTheLeaderGetsMeritAndTheCastle()
    {
        var (w,c)=Setup(NationalStart.Create(NationalStartTests.Nobunaga()));
        var old=Without(w);
        Month(w,1560,2);
        var o=w.Strategy!.Outcomes.Single(x=>x.Campaign==c.Id);
        Assert.True(o.Captured);Assert.True(o.Surrendered);Assert.Null(o.Battle);
        var merit=w.Strategy.Merits!.Single(m=>m.Campaign==c.Id&&m.Kind=="落城");
        Assert.Equal(c.Attacker,merit.House);
        Assert.Equal(merit.Person,Warfare2.FiefOf(w,c.Target)!.Holder);
        var head=HouseSuccession.Head(w,c.Attacker);
        Assert.Contains(Warfare2.KnownMerits(w,head),m=>m.Campaign==c.Id);
        Month(old,1560,2);
        var oo=old.Strategy!.Outcomes.Single(x=>x.Campaign==c.Id);
        Assert.Null(oo.Surrendered);Assert.Null(old.Strategy.Merits);
    }

    [Fact]
    public void WoundedSoldiersReturnInSixtyDaysAndTheDeadInOneHundredEighty()
    {
        long D=86400,R=180*D;
        Assert.Equal(100,Warfare2.Left(100,0,R));
        Assert.Equal(50*150/180+50*30/60,Warfare2.Left(100,30*D,R));
        Assert.Equal(50*119/180,Warfare2.Left(100,61*D,R));
        Assert.Equal(0,Warfare2.Left(100,180*D,R));
    }

    [Fact]
    public void CaptivesHeldByNpcsSometimesEscapeAndTheCaptivityEnds()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var me=w.PlayerControl;var houses=w.NewGame!.Start.Factions.Where(f=>f.Head!=me.Value).Take(2).ToArray();
        // 一つ目の家の家臣たちを、二つ目の家が捕らえている（捕縛の記録を出陣の決着に置く）。
        var captives=w.NewGame.Start.People.Where(p=>p.Faction==houses[0].Id&&p.Lord!=null).Take(60).Select(p=>new EntityId(p.Id)).ToArray();
        var holder=new EntityId(houses[1].Head);var site=new EntityId(houses[1].Home);
        long t0=NationalStrategy.MonthStart(w,1560,1);
        var camp=new StrategyCampaign(new("campaign:test-cap"),new(houses[0].Id),new(houses[1].Id),site,t0,1,"border");
        var outcomes=captives.Select((p,i)=>new StrategyOutcome(camp.Id,t0,1,false){Capture=new BattleCapture(p,new(houses[0].Id),new(houses[1].Id),holder,site,t0)}).ToArray();
        w.Strategy=new StrategyState(1,NationalStrategy.Policy,[camp],outcomes);
        w.ElapsedSeconds=t0+86400*20;w.CommitSequence=3;
        Assert.All(captives,p=>Assert.True(BattleCaptivity.HeldAt(w,p,w.ElapsedSeconds,w.CommitSequence+1)));
        var old=Without(w);
        for(int m=2;m<=7;m++)Month(w,1560,m);
        var esc=w.Strategy!.Escapes??[];
        Assert.NotEmpty(esc);Assert.True(esc.Length<captives.Length);
        Assert.All(esc,e=>Assert.False(BattleCaptivity.HeldAt(w,e.Person,w.ElapsedSeconds,w.CommitSequence+1)));
        Assert.Contains(Warfare2.ChronicleRows(w,HouseSuccession.Head(w,new(houses[0].Id))),r=>r.Kind=="逃亡");
        for(int m=2;m<=7;m++)Month(old,1560,m);
        Assert.Null(old.Strategy!.Escapes);
    }
}
