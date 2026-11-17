using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S09C（2026-11-17）：約束の条項と条件の変更、本人への申し入れ、多者の和睦と官位の願い、制裁の履行、従属の条項と違反、婚約と輿入れと養子、独立の伝達、派閥、辞去と転籍、捕虜の交換、偵察と流言。
[Trait("Tier","Heavy")]
public sealed class Politics2Tests
{
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.Politics2).ToArray()};
        return old;
    }
    static void Month(ContractWorld w,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;NationalStrategy.Apply(w);}
    static StrategyState Step(ContractWorld w,StrategyState s,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;return Politics2.Month(w,s,NationalStrategy.Owners(w,s));}
    static string Me(ContractWorld w)=>w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction!;
    static string[] Npc(ContractWorld w)=>w.Strategy!.Houses!.Select(h=>h.House.Value).Where(h=>h!=Me(w)&&NationalStrategy.Owners(w,w.Strategy).ContainsValue(h)).ToArray();

    [Fact]
    public void AlliancesCarryClausesAndAStrongPartnerAsksForVassalage()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var owners=NationalStrategy.Owners(w,w.Strategy);var power=NationalStrategy.Powers(w,owners);
        // 境を接し、力が3倍以上違う二家の同盟。
        var pair=owners.Where(x=>x.Value!=null&&x.Value!=Me(w)).SelectMany(x=>NationalStrategy.Neighbors(x.Key).Select(n=>(A:x.Value!,B:owners.GetValueOrDefault(n)))).Where(p=>p.B!=null&&p.B!=p.A&&p.B!=Me(w)&&power.GetValueOrDefault(p.A)>=3*power.GetValueOrDefault(p.B!)).First();
        long at=NationalStrategy.MonthStart(w,1560,7);
        var pact=new StrategyPact(new("pact:test-terms"),new(pair.A),new(pair.B!),"alliance",at,at+2*365*86400L,1,"test");
        var s=Step(w,w.Strategy! with{Pacts=[..w.Strategy.Pacts??[],pact]},1560,7);
        Assert.Equal(Politics2.Clauses.Length,s.Terms!.Count(t=>t.Pact==pact.Id));
        Assert.Equal(at+365*86400L,s.Terms!.Single(t=>t.Pact==pact.Id&&t.Clause=="共同の出陣").Until);
        var rev=s.Politics!.Single(e=>e.Kind=="revise"&&e.House!.Value.Value==pair.A&&e.Other!.Value.Value==pair.B);
        Assert.Equal(1,rev.Amount);Assert.NotNull(s.Pacts!.Single(p=>p.Id==pact.Id).Broken);
        Assert.Contains(s.Politics!,e=>e.Kind=="vassal"&&e.House!.Value.Value==pair.B);
    }

    [Fact]
    public void ThePlayerAnswersAnAllianceProposalAndTheShogunCallsForPeace()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var other=Npc(w)[0];long now=w.ElapsedSeconds;
        var prop=new PoliticsEvent("politics:proposal/test","proposal",null,new(other),new(Me(w)),null,now,w.CommitSequence,0,"同盟の申し入れ"){Arrives=now};
        w.Strategy=w.Strategy! with{Politics=[prop]};
        Assert.Single(Politics2.Proposals(w,w.PlayerControl));
        w.CommitSequence++;Politics2.Answer(w,w.PlayerControl,prop.Id,true);
        var made=w.Strategy!.Pacts!.Single(p=>p.Basis=="proposal:"+prop.Id);Assert.Equal("alliance",made.Kind);Assert.True(made.From<=w.ElapsedSeconds);
        Assert.Empty(Politics2.Proposals(w,w.PlayerControl));
        Assert.Throws<InvalidDataException>(()=>Politics2.Answer(w,w.PlayerControl,prop.Id,false));
        // 三家以上が互いに攻め合っていれば、将軍が和睦を呼びかけ、受けた家どうしが和睦する。
        var houses=Npc(w).Take(3).ToArray();long t0=NationalStrategy.MonthStart(w,1560,6);
        StrategyCampaign C(string id,string a,string b){var site=NationalStrategy.Owners(w,w.Strategy).First(x=>x.Value==b).Key;return new(new(id),new(a),new(b),new(site),t0,1,"border");}
        var cs=new[]{C("campaign:w1",houses[0],houses[1]),C("campaign:w2",houses[1],houses[2]),C("campaign:w3",houses[2],houses[0])};
        var s=Step(w,w.Strategy with{Campaigns=[..w.Strategy.Campaigns,..cs]},1560,7);
        Assert.Contains(s.Politics!,e=>e.Kind=="peace-call");
        var answers=s.Politics!.Where(e=>e.Kind=="peace-answer").ToArray();Assert.True(answers.Length>=3);Assert.All(answers,a=>Assert.True(a.Arrives>=a.At));
    }

    [Fact]
    public void HousesAskForCourtRankSanctionBetrayersAndVassalsPayTribute()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var h=w.Strategy!.Houses!.First(x=>Npc(w).Contains(x.House.Value));var head=HouseSuccession.Head(w,h.House);
        var ask=new PoliticsEvent("politics:rank-ask/test","rank-ask",null,h.House,null,head,w.ElapsedSeconds,w.CommitSequence,300,"官位を願った"){Arrives=w.ElapsedSeconds+86400};
        var s=Step(w,w.Strategy with{Politics=[ask]},1560,7);
        Assert.Contains(s.Honors!,x=>x.Person==head&&x.Kind=="従五位下");
        // 裏切り：裏切られた家の同盟の家が、裏切った家を制裁し、隊商を通さず銭が減る。
        var npc=Npc(w);var victim=npc[0];var betrayer=npc[1];var friend=npc[2];long t=NationalStrategy.MonthStart(w,1560,8);
        var site=NationalStrategy.Owners(w,s).First(x=>x.Value==victim).Key;
        var bc=new StrategyCampaign(new("campaign:betray"),new(betrayer),new(victim),new(site),t,1,"betrayal");
        var ally=new StrategyPact(new("pact:friend"),new(friend),new(victim),"alliance",NationalStrategy.MonthStart(w,1560,6),t+365*86400L,1,"test");
        long coin=s.Houses!.Single(x=>x.House.Value==betrayer).Coin;
        w.Strategy=s;var s2=Step(w,s with{Campaigns=[..s.Campaigns,bc],Pacts=[..s.Pacts??[],ally]},1560,8);
        Assert.Contains(s2.Politics!,e=>e.Kind=="sanction"&&e.House!.Value.Value==friend&&e.Other!.Value.Value==betrayer);
        w.Strategy=s2;Assert.True(Politics2.Sanctioning(w,friend,betrayer,w.ElapsedSeconds));Assert.False(Politics2.Sanctioning(Without(w),friend,betrayer,w.ElapsedSeconds));
        Assert.True(s2.Houses!.Single(x=>x.House.Value==betrayer).Coin<coin||coin==0);
        // 従属：1月に従う家が主へ銭を納める。
        var s3=Step(w,s2,1561,1);
        Assert.Contains(s3.Politics!,e=>e.Kind=="tribute"&&NationalStrategy.DbOverlord(w,e.House!.Value.Value)==e.Other!.Value.Value);
    }

    [Fact]
    public void BetrothalsBecomeMarriagesOnArrivalAndHeirlessHousesAdoptFromStrongAllies()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var npc=Npc(w);long t=NationalStrategy.MonthStart(w,1560,6);
        bool Adult(SetupPerson p)=>w.Characters.Single(c=>c.Id.Value==p.Id).Life==LifeState.Adult;
        var woman=w.NewGame!.Start.People.First(p=>p.Faction!=null&&npc.Contains(p.Faction)&&NationalStart.DbPerson(p.Id)?.Sex=="女"&&Adult(p));var b=woman.Faction!;
        var man=w.NewGame.Start.People.First(p=>p.Faction!=null&&p.Faction!=b&&npc.Contains(p.Faction)&&NationalStart.DbPerson(p.Id)?.Sex=="男"&&Adult(p));var a=man.Faction!;
        var pact=new StrategyPact(new("pact:wed"),new(a),new(b),"alliance",t,t+3*365*86400L,1,"test");
        var bet=new PoliticsEvent("politics:betroth/test","betroth",null,new(a),new(b),new(man.Id),t,1,0,"婚約"){Person2=new(woman.Id)};
        var s=Step(w,w.Strategy! with{Pacts=[..w.Strategy.Pacts??[],pact],Politics=[bet]},1560,7);
        var wed=s.Politics!.Single(e=>e.Kind=="wed"&&e.Person==new EntityId(man.Id));Assert.True(wed.Arrives>wed.At);
        Assert.DoesNotContain(s.Marriages??[],m=>m.Man.Value==man.Id&&m.Woman.Value==woman.Id);
        w.Strategy=s;var arrive=WorldCalendar.At(w.EpochDate,wed.Arrives!.Value).AddMonths(1);
        var s2=Step(w,s,arrive.Year,arrive.Month);
        Assert.Contains(s2.Marriages!,m=>m.Man.Value==man.Id&&m.Woman.Value==woman.Id&&m.Pact==pact.Id);
        // 養子：成人の後継の無い老いた当主の家は、強い同盟の家の当主の次男以下を養子に迎え、その人が後継になる。
        var adopt=new PoliticsEvent("politics:adopt/test","adopt",null,new(a),new(b),new(man.Id),t,1,0,"養子");
        w.Strategy=s2 with{Politics=[..s2.Politics!,adopt]};
        Assert.Equal(new EntityId(man.Id),Politics2.AdoptedHeir(w,new(a)));Assert.Null(Politics2.AdoptedHeir(Without(w),new(a)));
    }

    [Fact]
    public void IndependenceReachesCorpsCommandersAndFactionsShiftWithRumors()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var corps=w.Strategy!.Corps!.First(c=>!c.Kin);var former=corps.House;
        var founder=new EntityId(w.NewGame!.Start.People.First(p=>p.Faction==former.Value&&p.Id!=corps.Commander.Value&&p.Id!=HouseSuccession.Head(w,former).Value).Id);
        long t=NationalStrategy.MonthStart(w,1560,7);
        w.Declarations=[new(new("pact:indep"),founder,former,new("org:independent/test"),new(NationalStrategy.Owners(w,w.Strategy).First(x=>x.Value==former.Value).Key),t-86400,1)];
        var s=Step(w,w.Strategy,1560,7);
        var al=s.Politics!.Single(e=>e.Kind=="allegiance"&&e.Person==corps.Commander);Assert.True(al.Arrives>=al.At);
        // 派閥：毎月の記録は一門・譜代と外様の合計が100。流言は相手の外様の支持を動かす。
        Assert.All(s.Factions!,f=>Assert.Equal(100,f.Kin+f.Outsiders));
        Politics2.Validate(w,s,null);
        Assert.Equal(100,Politics2.DefectPerMille(Without(w),former.Value));
    }

    [Fact]
    public void CaptivesAreExchangedScoutingIsImperfectAndUnhappyRetainersResign()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var npc=Npc(w);
        // 見立ては真の力と違い得るが、±20%に収まる。
        var est=Politics2.Seen(w,npc[0],npc[1],1000);Assert.InRange(est,800,1200);Assert.Equal(1000,Politics2.Seen(Without(w),npc[0],npc[1],1000));
        // 捕虜の交換：互いの家の者を捕らえている二家は、一人ずつ交換し、交換された人は捕らわれでなくなる。
        {
            var x=npc[0];var y=npc[1];long t=w.ElapsedSeconds;var owners=NationalStrategy.Owners(w,w.Strategy);
            var px=new EntityId(w.NewGame!.Start.People.First(p=>p.Faction==x&&p.Id!=HouseSuccession.Head(w,new(x)).Value).Id);
            var py=new EntityId(w.NewGame.Start.People.First(p=>p.Faction==y&&p.Id!=HouseSuccession.Head(w,new(y)).Value).Id);
            string sx=owners.First(o=>o.Value==x).Key,sy=owners.First(o=>o.Value==y).Key;
            var c1=new StrategyCampaign(new("campaign:cap1"),new(x),new(y),new(sy),t,1,"border");var c2=new StrategyCampaign(new("campaign:cap2"),new(y),new(x),new(sx),t,1,"border");
            var hy=HouseSuccession.Head(w,new(y));var hx=HouseSuccession.Head(w,new(x));
            w.CommitSequence++;
            w.Strategy=w.Strategy with{Campaigns=[..w.Strategy.Campaigns,c1,c2],Outcomes=[..w.Strategy.Outcomes,new(c1.Id,t,w.CommitSequence,false){Capture=new(px,new(x),new(y),hy,new(sy),t)},new(c2.Id,t,w.CommitSequence,false){Capture=new(py,new(y),new(x),hx,new(sx),t)}]};
            Assert.True(BattleCaptivity.HeldAt(w,px,w.ElapsedSeconds,w.CommitSequence+1));
            var ex=Step(w,w.Strategy,1560,7);
            var e=ex.Politics!.Single(e=>e.Kind=="exchange");Assert.True(e.Person==px&&e.Person2==py||e.Person==py&&e.Person2==px);
            var keep=w.Strategy;w.Strategy=ex;
            Assert.False(BattleCaptivity.HeldAt(w,px,w.ElapsedSeconds,w.CommitSequence+1));Assert.False(BattleCaptivity.HeldAt(w,py,w.ElapsedSeconds,w.CommitSequence+1));
            w.Strategy=keep;
        }
        // 給付を削った家の家臣は辞去しうる（2年続ければ一人は出る）。
        var h=w.Strategy!.Houses!.Where(x=>npc.Contains(x.House.Value)).OrderByDescending(x=>w.NewGame!.Start.People.Count(p=>p.Faction==x.House.Value)).First();
        var s=w.Strategy with{Houses=w.Strategy.Houses!.Select(x=>x.House==h.House?x with{StipendCut=40}:x).ToArray()};
        for(int m=7;m<=12&&!(s.Politics??[]).Any(e=>e.Kind=="resign");m++)s=Step(w,s with{Houses=s.Houses!.Select(x=>x.House==h.House?x with{StipendCut=40}:x).ToArray()},1560,m);
        for(int y=1561;y<=1562&&!(s.Politics??[]).Any(e=>e.Kind=="resign");y++)for(int m=1;m<=12&&!(s.Politics??[]).Any(e=>e.Kind=="resign");m++)s=Step(w,s with{Houses=s.Houses!.Select(x=>x.House==h.House?x with{StipendCut=40}:x).ToArray()},y,m);
        var r=(s.Politics??[]).FirstOrDefault(e=>e.Kind=="resign");Assert.NotNull(r);
        w.Strategy=s;Assert.Null(Persons2.Recruits(w)[r!.Person!.Value.Value]);
    }
}
