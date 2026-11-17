using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S09B（2026-11-17）：作戦線と優先度、兵種の編成と募兵、軍団と一族の分隊、競合する命令と現場の裁量、区画の攻城と開城の条件、救援の到着、逃げた人の帰還、追跡補給。
[Trait("Tier","Heavy")]
public sealed class Armies2Tests
{
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.Armies2).ToArray()};
        return old;
    }
    static void Month(ContractWorld w,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;NationalStrategy.Apply(w);}
    static ContractWorld Copy(ContractWorld w)=>ContractJson.Read<ContractWorld>(ContractJson.Write(w));
    static (ContractWorld W,StrategyCampaign C) Pair(ContractWorld w,Func<string,bool>? site=null)
    {
        var owners=NationalStrategy.Owners(w,w.Strategy);var me=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var pick=owners.Where(x=>x.Value!=null&&x.Value!=me&&(site==null||site(x.Key))).SelectMany(x=>NationalStrategy.Neighbors(x.Key).Where(n=>owners.GetValueOrDefault(n) is {} o&&o!=x.Value&&o!=me).Select(n=>(A:owners[n]!,D:x.Value!,Site:x.Key))).OrderBy(t=>t.Site,StringComparer.Ordinal).First();
        return (w,new StrategyCampaign(new("campaign:test-army"),new(pick.A),new(pick.D),new(pick.Site),w.ElapsedSeconds,1,"border"));
    }

    [Fact]
    public void BigHousesFormCorpsLedByKinAndOpenASecondLine()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var corps=w.Strategy!.Corps!;Assert.NotEmpty(corps);
        var owners=NationalStrategy.Owners(w,w.Strategy);
        Assert.All(corps,c=>{Assert.True(owners.Count(x=>x.Value==c.House.Value)>=15);Assert.Equal(c.House.Value,w.NewGame!.Start.People.Single(p=>p.Id==c.Commander.Value).Faction);
            Assert.NotEqual(CentralStanding.ProvinceOf(new(w.NewGame.Start.Factions.Single(f=>f.Id==c.House.Value).Home)),c.Province);});
        Assert.Contains(corps,c=>c.Kin);
        // 二の線：天下か国の統一を目指す城10以上の家は、主の線と別の国の、4割の兵でも1.5倍の相手を的にする。
        var big=w.Strategy.Goals!.GroupBy(g=>g.House).Select(g=>g.Last()).First(g=>g.Goal is Persons2.Tenka or Persons2.Unify&&owners.Count(x=>x.Value==g.House.Value)>=10);
        var own=owners.Where(x=>x.Value==big.House.Value).Select(x=>x.Key).ToArray();
        var targets=own.SelectMany(NationalStrategy.Neighbors).Distinct().Select(t=>(Site:t,Owner:owners.GetValueOrDefault(t))).Where(t=>t.Owner!=null&&t.Owner!=big.House.Value).ToArray();
        var first=targets[0];
        var second=Armies2.SecondLine(w,big.House.Value,first.Site,targets,_=>500,own.Length,w.ElapsedSeconds);
        if(targets.Any(t=>CentralStanding.ProvinceOf(new(t.Site))!=CentralStanding.ProvinceOf(new(first.Site))))
        {Assert.NotNull(second);Assert.NotEqual(CentralStanding.ProvinceOf(new(first.Site)),CentralStanding.ProvinceOf(new(second!.Value.Site)));}
        Assert.Null(Armies2.SecondLine(w,big.House.Value,first.Site,targets,_=>300,own.Length,w.ElapsedSeconds));
        Assert.Null(Armies2.SecondLine(Without(w),big.House.Value,first.Site,targets,_=>500,own.Length,w.ElapsedSeconds));
        // 線の割合は兵を分ける。
        var (_,c0)=Pair(w);var (a,_)=Armies2.Effective(w,w.Strategy,c0 with{Share=40},1000,1000);var (a6,_)=Armies2.Effective(w,w.Strategy,c0 with{Share=60},1000,1000);Assert.True(a<a6);
        Assert.True(Armies2.Withdraws(w,c0 with{Line=2,Commander=corps[0].Commander},500,1000));Assert.False(Armies2.Withdraws(w,c0 with{Line=1},500,1000));
    }

    [Fact]
    public void EquipmentShapesTheArmyAndMercenariesAreHiredAndFlee()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var (_,c)=Pair(w,s=>Warfare2.CastleType(new(s)) is "山城" or "平山城");
        var with=w.Strategy! with{Houses=w.Strategy.Houses!.Select(h=>h.House==c.Attacker?h with{Goods=h.Goods with{Guns=300}}:h.House==c.Defender?h with{Goods=h.Goods with{Guns=0,Horses=0}}:h).ToArray()};
        var none=w.Strategy with{Houses=w.Strategy.Houses!.Select(h=>h.House==c.Attacker||h.House==c.Defender?h with{Goods=h.Goods with{Guns=0,Horses=0}}:h).ToArray()};
        Assert.Equal(30,Armies2.Composition(w,with,c.Attacker,1000).Guns);
        var (ag,_)=Armies2.Effective(w,with,c,1000,1000);var (an,_)=Armies2.Effective(w,none,c,1000,1000);Assert.True(ag>an);
        // 募兵：軍備の方針で銭のある家は雇い兵を雇い、負けると逃げる。
        w.Strategy=w.Strategy with{Houses=w.Strategy.Houses!.Select(h=>h.House==c.Attacker?h with{Policy=Domestic2.Arms,Coin=5000}:h).ToArray()};
        Month(w,1560,7);
        var hire=w.Strategy!.Armies!.Single(e=>e.Kind=="hire"&&e.House==c.Attacker);Assert.True(hire.Amount>=100&&hire.Amount%100==0);Assert.True(hire.Until>w.ElapsedSeconds);
        var (ah,_)=Armies2.Effective(w,w.Strategy,c with{At=w.ElapsedSeconds},1000,1000);var (a0,_)=Armies2.Effective(Without(w),w.Strategy,c with{At=w.ElapsedSeconds},1000,1000);Assert.True(ah>a0);
        long now=NationalStrategy.MonthStart(w,1560,8);var lost=c with{Id=new("campaign:lost"),At=w.ElapsedSeconds};
        w.Strategy=w.Strategy with{Campaigns=[..w.Strategy.Campaigns,lost],Outcomes=[..w.Strategy.Outcomes,new(lost.Id,now,w.CommitSequence,false)]};
        w.ElapsedSeconds=now;w.Strategy=Armies2.Month(w,w.Strategy,NationalStrategy.Owners(w,w.Strategy));
        Assert.Contains(w.Strategy.Armies!,e=>e.Kind=="desert"&&e.House==c.Attacker);
    }

    [Fact]
    public void MountainCastlesFallSectionBySectionAndOfferTermsWhenHalfIsTaken()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var (_,c)=Pair(w,s=>Warfare2.CastleType(new(s))=="山城");
        long now=NationalStrategy.MonthStart(w,1560,7);c=c with{At=w.ElapsedSeconds};
        var battle=new StrategyBattle(1000,800,50,50,100,300,3600,false,false);
        w.Strategy=w.Strategy! with{Campaigns=[..w.Strategy.Campaigns,c],Outcomes=[..w.Strategy.Outcomes,new(c.Id,now,w.CommitSequence,false){Battle=battle}]};
        w.ElapsedSeconds=now;var s=Armies2.Month(w,w.Strategy,NationalStrategy.Owners(w,w.Strategy));
        var sg=s.Sieges!.Single(x=>x.Site==c.Target.Value);Assert.Equal(1,sg.Taken);Assert.Equal(4,sg.Sections);
        w.Strategy=s;var (_,d1)=Armies2.Effective(w,s,c,1000,1000);Assert.True(d1<1000);
        Assert.False(Armies2.Yields(w,c));
        w.Strategy=s with{Sieges=[sg with{Taken=2}]};Assert.True(Armies2.Yields(w,c));
        var c2=c with{Id=new("campaign:again")};
        w.Strategy=w.Strategy with{Campaigns=[..w.Strategy.Campaigns,c2],Outcomes=[..w.Strategy.Outcomes,new(c2.Id,now,w.CommitSequence,false){Battle=battle}]};
        var s2=Armies2.Month(w,w.Strategy,NationalStrategy.Owners(w,w.Strategy));Assert.Contains(s2.Armies!,e=>e.Kind=="terms"&&e.Site==c.Target.Value);
        // 落ちれば攻城の記録は消える。
        var c3=c with{Id=new("campaign:fall")};
        w.Strategy=s2 with{Campaigns=[..s2.Campaigns,c3],Outcomes=[..s2.Outcomes,new(c3.Id,now,w.CommitSequence,true)]};
        var s3=Armies2.Month(w,w.Strategy,NationalStrategy.Owners(w,w.Strategy));Assert.DoesNotContain(s3.Sieges??[],x=>x.Site==c.Target.Value);
    }

    [Fact]
    public void ReliefFromFarAwayIsLateAndConflictingOrdersAreRecorded()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var (_,c)=Pair(w,s=>Warfare2.CastleType(new(s))!="山城");
        var far=w.NewGame!.Start.Factions.OrderByDescending(f=>Domestic2.Days(f.Home,c.Target.Value)).First();
        var near=w.NewGame.Start.Factions.OrderBy(f=>Domestic2.Days(f.Home,c.Target.Value)).First();
        Assert.True(Armies2.Late(w,c,new(far.Id)));Assert.False(Armies2.Late(w,c,new(near.Id)));Assert.False(Armies2.Late(Without(w),c,new(far.Id)));
        // 攻める家に従う家が、守る家と長い約束を結んでいれば、約束を守って加わらない。
        var owners=NationalStrategy.Owners(w,w.Strategy);
        var vassal=owners.Values.Where(v=>v!=null).Distinct().FirstOrDefault(v=>NationalStrategy.DbOverlord(w,v!) is {} o&&owners.ContainsValue(o));
        Assert.NotNull(vassal);var lord=NationalStrategy.DbOverlord(w,vassal!)!;
        var target=owners.First(x=>x.Value!=null&&x.Value!=lord&&x.Value!=vassal);
        long now=NationalStrategy.MonthStart(w,1560,7);
        var cc=new StrategyCampaign(new("campaign:conflict"),new(lord),new(target.Value!),new(target.Key),now,w.CommitSequence,"border");
        var pact=new StrategyPact(new("pact:conflict"),new(vassal!),new(target.Value!),"alliance",NationalStrategy.MonthStart(w,1560,6),now+3*365*86400L,1,"test");
        w.ElapsedSeconds=now;
        var s=Armies2.Month(w,w.Strategy! with{Campaigns=[..w.Strategy.Campaigns,cc],Pacts=[..w.Strategy.Pacts??[],pact]},owners);
        var ev=s.Armies!.Single(e=>e.Kind=="conflict"&&e.Site==cc.Id.Value);Assert.Equal(1,ev.Amount);Assert.Contains("約束を守って",ev.Text);
    }

    [Fact]
    public void EscapedPeopleWalkHomeAndWithdrawalsAreRecorded()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var p=w.NewGame!.Start.People.First(x=>x.Faction!=null&&w.NewGame.Start.Factions.Any(f=>f.Id==x.Faction&&Domestic2.Days(f.Home,"site:c-l501394")>5));
        long now=NationalStrategy.MonthStart(w,1560,7);
        var esc=new StrategyEscape(new(p.Id),new(w.NewGame.Start.Factions[0].Id),new("site:c-l501394"),now-86400,1);
        w.ElapsedSeconds=now;var s=Armies2.Month(w,w.Strategy! with{Escapes=[esc]},NationalStrategy.Owners(w,w.Strategy));
        var back=s.Armies!.Single(e=>e.Kind=="return"&&e.Person==esc.Person);
        Assert.Equal(esc.At+back.Amount*86400L,back.Until);Assert.True(back.Amount>5);
        w.Strategy=s;Assert.DoesNotContain(Armies2.Heard(w,w.PlayerControl,back.Until!.Value-1),r=>r.Id==back.Id);
        // 二の線の軍団長が退いた合戦（戦わず、落ちない）は記録に残る。
        var (_,c)=Pair(w);var cm=new EntityId(w.NewGame.Start.People.First(x=>x.Faction==c.Attacker.Value).Id);
        var line2=c with{Id=new("campaign:line2"),Line=2,Share=40,Commander=cm,At=w.ElapsedSeconds};
        long next=NationalStrategy.MonthStart(w,1560,8);w.ElapsedSeconds=next;
        var s2=Armies2.Month(w,w.Strategy with{Campaigns=[..w.Strategy.Campaigns,line2],Outcomes=[..w.Strategy.Outcomes,new(line2.Id,next,w.CommitSequence,false)]},NationalStrategy.Owners(w,w.Strategy));
        Assert.Contains(s2.Armies!,e=>e.Kind=="withdraw"&&e.Person==cm);
        // 追跡補給は実際の軍勢だけ。
        Assert.Null(Armies2.TrackingDays(w,c));
        Armies2.Validate(w,s2,null);
    }

    [Fact]
    public void OldWorldsKeepOneLineAndNoCorps()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());var old=Without(w);
        Month(old,1560,6);Month(old,1560,7);
        Assert.Null(old.Strategy!.Corps);Assert.Null(old.Strategy.Armies);Assert.DoesNotContain(old.Strategy.Campaigns,c=>c.Line!=null);
        Month(w,1560,6);Month(w,1560,7);
        Assert.All(w.Strategy!.Campaigns.Where(c=>c.Line==2),c=>{Assert.Equal(40,c.Share);Assert.Contains(w.Strategy.Campaigns,x=>x.Attacker==c.Attacker&&x.At==c.At&&x.Line==1&&x.Share==60);});
    }
}
