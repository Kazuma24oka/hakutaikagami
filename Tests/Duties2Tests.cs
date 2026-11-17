using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S09E（2026-11-17）：併用の表と代わり、書状の道と開封、命令の競合、子の形成、先読み、役以外の技能、後見、代理と追認、国人連合、配下の責任、婚姻と養子、鞍替え、忠誠、身代金、兵糧の見聞。
[Trait("Tier","Heavy")]
public sealed class Duties2Tests
{
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.Duties2).ToArray()};
        return old;
    }
    static void Month(ContractWorld w,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;NationalStrategy.Apply(w);}
    static StrategyState Step(ContractWorld w,StrategyState s,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;w.Strategy=s;return Duties2.Month(w,s,NationalStrategy.Owners(w,s));}
    static string Me(ContractWorld w)=>w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction!;
    static string[] Npc(ContractWorld w)=>w.Strategy!.Houses!.Select(h=>h.House.Value).Where(h=>h!=Me(w)&&NationalStrategy.Owners(w,w.Strategy).ContainsValue(h)).ToArray();
    static StrategyState Capture(ContractWorld w,StrategyState s,EntityId person,string from,string captor,string id)
    {
        var owners=NationalStrategy.Owners(w,s);var site=owners.First(x=>x.Value==captor).Key;long t=w.ElapsedSeconds;w.CommitSequence++;
        var c=new StrategyCampaign(new(id),new(from),new(captor),new(site),t,1,"border");
        return s with{Campaigns=[..s.Campaigns,c],Outcomes=[..s.Outcomes,new(c.Id,t,w.CommitSequence,false){Capture=new(person,new(from),new(captor),HouseSuccession.Head(w,new(captor)),new(site),t)}]};
    }

    [Fact]
    public void CapturedOfficeHoldersAreReplacedAndRestoredWithCompensation()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var h=w.Strategy!.Houses!.First(x=>Npc(w).Contains(x.House.Value)&&x.Posts.Length>=3);var post=h.Posts[1];
        var captor=Npc(w).First(x=>x!=h.House.Value);
        var s=Capture(w,w.Strategy,post.Person,h.House.Value,captor,"campaign:cap-post");
        w.Strategy=s;Assert.True(BattleCaptivity.HeldAt(w,post.Person,w.ElapsedSeconds,w.CommitSequence+1));
        Assert.False(Duties2.Concurrency[("捕らわれ","家の役")]);Assert.True(Duties2.Concurrency[("出陣中","家の役")]);
        var s2=Step(w,s,1560,7);
        var sus=s2.Duties!.Single(e=>e.Kind=="suspend"&&e.Person==post.Person);
        Assert.DoesNotContain(s2.Houses!.Single(x=>x.House==h.House).Posts,p=>p.Person==post.Person);
        // 身代金が払われて放たれれば、元の役に戻り、代わりに補償。
        var s3=s2 with{Houses=s2.Houses!.Select(x=>x.House==h.House?x with{Coin=100000}:x).ToArray()};
        var s4=Step(w,s3,1560,8);
        Assert.Contains(s4.Duties!,e=>e.Kind=="ransom"&&e.Person==post.Person&&e.Amount>0);
        w.Strategy=s4;Assert.False(BattleCaptivity.HeldAt(w,post.Person,w.ElapsedSeconds,w.CommitSequence+1));
        var s5=Step(w,s4,1560,9);
        Assert.Contains(s5.Duties!,e=>e.Kind=="resume"&&e.Person==post.Person&&e.Detail==sus.Id);
        Assert.Contains(s5.Houses!.Single(x=>x.House==h.House).Posts,p=>p.Person==post.Person);
    }

    [Fact]
    public void LettersAvoidEnemiesGetCongestedAndElderOpensForAnAbsentLord()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var npc=Npc(w);var a=npc[0];var b=npc[^1];
        string ha=w.NewGame!.Start.Factions.Single(f=>f.Id==a).Home,hb=w.NewGame.Start.Factions.Single(f=>f.Id==b).Home;
        var plain=Duties2.Route(w,w.Strategy!,a,ha,hb,0,w.ElapsedSeconds);var busy=Duties2.Route(w,w.Strategy!,a,ha,hb,6,w.ElapsedSeconds);
        Assert.Equal(plain.Days+3,busy.Days);Assert.True(plain.Days>=Domestic2.Days(ha,hb));
        // 隣の家すべてと戦っていれば、敵の領地を通れず、遠回りか失って継ぎ直す。
        var owners=NationalStrategy.Owners(w,w.Strategy);
        var foes=owners.Where(x=>x.Value==a).SelectMany(x=>NationalStrategy.Neighbors(x.Key)).Select(n=>owners.GetValueOrDefault(n)).Where(o=>o!=null&&o!=a).Distinct().ToArray();
        var wars=foes.Select((f,i)=>new StrategyCampaign(new($"campaign:foe{i}"),new(a),new(f!),new(owners.First(x=>x.Value==f).Key),w.ElapsedSeconds,1,"border")).ToArray();
        var blocked=Duties2.Route(w,w.Strategy! with{Campaigns=[..w.Strategy.Campaigns,..wars]},a,ha,hb,0,w.ElapsedSeconds);
        Assert.True(blocked.Lost||blocked.Detour||blocked.Days>=plain.Days);
        // 当主が本拠にいなければ家老が開封する。
        var head=HouseSuccession.Head(w,new(a));Assert.Equal(head,Duties2.Opener(w,a,w.ElapsedSeconds));
        w.Locations=w.Locations.Select(l=>l.Actor==head?l with{Kind=LocationKind.AtSite,Site=new(hb),Route=null}:l).ToArray();
        var elder=Domestic2.House(w,new(a))!.Posts.FirstOrDefault(p=>p.Post==Domestic2.Elder)?.Person;
        if(elder!=null)Assert.Equal(elder.Value,Duties2.Opener(w,a,w.ElapsedSeconds));
    }

    [Fact]
    public void ConflictingOrdersChildrenStrongAIAndGuardiansFollowTheirRules()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var owners=NationalStrategy.Owners(w,w.Strategy);
        var vassal=owners.Values.Where(v=>v!=null&&v!=Me(w)).Distinct().First(v=>NationalStrategy.DbOverlord(w,v!) is {} o&&owners.ContainsValue(o));var lord=NationalStrategy.DbOverlord(w,vassal!)!;
        long prev=NationalStrategy.MonthStart(w,1560,6);
        StrategyCampaign C(string id,string att){var t=owners.First(x=>x.Value!=null&&x.Value!=att&&x.Value!=lord&&x.Value!=vassal);return new(new(id),new(att),new(t.Value!),new(t.Key),prev,1,"border");}
        var s=Step(w,w.Strategy! with{Campaigns=[..w.Strategy.Campaigns,C("campaign:own",vassal!),C("campaign:lord",lord)]},1560,7);
        Assert.Contains(s.Duties!,e=>e.Kind=="order"&&e.Site=="campaign:own"&&e.Other!.Value.Value==lord);
        // 子の形成：未成年の子は1月に興味が育ち、元服で志が定まる。
        var kid=w.NewGame!.Start.People.First(p=>p.Faction!=null&&p.Faction!=Me(w)&&p.BirthParents is {Length:>0});
        w.Characters=w.Characters.Select(c=>c.Id.Value==kid.Id?c with{Life=LifeState.Minor}:c).ToArray();
        var s2=Step(w,s,1561,1);Assert.Contains(s2.Duties!,e=>e.Kind=="growth"&&e.Person!.Value.Value==kid.Id);
        var gen=new PersonEvent("person:genpuku/test","genpuku",new(kid.Id),new(kid.Faction!),NationalStrategy.MonthStart(w,1561,2),1,"元服");
        var s3=Step(w,s2 with{Persons=[..s2.Persons??[],gen]},1561,2);
        Assert.Contains(s3.Persons!,e=>e.Kind=="aspiration-formed"&&e.Person.Value==kid.Id);
        w.Strategy=s3;Assert.Equal("養育と経験から定まった志",Persons2.Aspiration(w,new(kid.Id))!.Basis);
        // 先読み：手強さが「強い」なら、相手の同盟の家の援軍を見込む。
        var power=NationalStrategy.Powers(w,owners);var x=Npc(w)[0];var y=Npc(w)[1];long t0=w.ElapsedSeconds;
        var ally=new StrategyPact(new("pact:look"),new(x),new(y),"alliance",NationalStrategy.MonthStart(w,1561,2),t0+365*86400L,1,"test");
        var sa=s3 with{Pacts=[..s3.Pacts??[],ally]};
        Assert.Equal(power.GetValueOrDefault(x,50),Duties2.Expected(w,sa,x,power));
        w.NewGame=w.NewGame with{Judgment=w.NewGame.Judgment! with{Challenge=NpcChallenge.Strong}};
        Assert.Equal(power.GetValueOrDefault(x,50)+power.GetValueOrDefault(y)/2,Duties2.Expected(w,sa,x,power));
        // 後見：当主が15歳未満なら家老が後見人、15歳で帳簿を返す。
        var h=s3.Houses!.First(z=>Npc(w).Contains(z.House.Value)&&z.Posts.Any(p=>p.Post==Domestic2.Elder));var hd=HouseSuccession.Head(w,h.House);
        w.NewGame=w.NewGame with{Start=w.NewGame.Start with{People=w.NewGame.Start.People.Select(p=>p.Id==hd.Value?p with{BirthYear=1551}:p).ToArray()}};
        var s4=Step(w,s3,1561,3);Assert.Contains(s4.Duties!,e=>e.Kind=="guardian"&&e.House==h.House&&e.Person2==hd);
        w.NewGame=w.NewGame with{Start=w.NewGame.Start with{People=w.NewGame.Start.People.Select(p=>p.Id==hd.Value?p with{BirthYear=1540}:p).ToArray()}};
        var s5=Step(w,s4,1561,4);Assert.Contains(s5.Duties!,e=>e.Kind=="guardian-end"&&e.House==h.House);
    }

    [Fact]
    public void ProxiesAreRatifiedCoalitionsCouncilAndIncidentsAreAnswered()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var npc=Npc(w);var a=npc[0];var head=HouseSuccession.Head(w,new(a));
        var elder=Domestic2.House(w,new(a))!.Posts.FirstOrDefault(p=>p.Post==Domestic2.Elder)?.Person;
        long prev=NationalStrategy.MonthStart(w,1560,6);
        var pol=new DomesticEvent("domestic:policy/proxy-test","policy",null,new(a),head,prev,1,0,"方針：商");
        var away=w.NewGame!.Start.Factions.Single(f=>f.Id==npc[^1]).Home;var home=w.NewGame.Start.Factions.Single(f=>f.Id==a).Home;
        w.Locations=w.Locations.Select(l=>l.Actor==head?l with{Kind=LocationKind.AtSite,Site=new(away),Route=null}:l).ToArray();
        var s=Step(w,w.Strategy! with{Domestic=[..w.Strategy.Domestic??[],pol]},1560,7);
        if(elder!=null)
        {
            Assert.Contains(s.Duties!,e=>e.Kind=="proxy"&&e.Detail==pol.Id&&e.Person==elder);
            w.Locations=w.Locations.Select(l=>l.Actor==head?l with{Kind=LocationKind.AtSite,Site=new(home),Route=null}:l).ToArray();
            var s2=Step(w,s,1560,8);Assert.Contains(s2.Duties!,e=>e.Kind is "ratify" or "overturn"&&e.Person==head);
        }
        // 国人連合：合議で半数以上が賛成すれば共同で出陣し、反対の家は負担を払う。
        var owners=NationalStrategy.Owners(w,w.Strategy);var power=NationalStrategy.Powers(w,owners);
        var target=npc.OrderByDescending(x=>power.GetValueOrDefault(x)).First();
        var members=owners.Where(x=>x.Value==target).SelectMany(x=>NationalStrategy.Neighbors(x.Key)).Select(n=>owners.GetValueOrDefault(n)).Where(o=>o!=null&&o!=target&&o!=Me(w)).Distinct().Take(3).Select(o=>new EntityId(o!)).ToArray();
        var co=new StrategyCoalition(new("coalition:test"),new(target),members,NationalStrategy.MonthStart(w,1560,6),1);
        var s3=Step(w,w.Strategy! with{Coalitions=[co]},1560,10);
        var council=s3.Duties!.Last(e=>e.Kind=="council"&&e.House!.Value.Value==target);
        if(council.Amount*2>=members.Length)Assert.Contains(s3.Campaigns,c=>c.Id.Value.Contains("/coalition/")&&c.Defender.Value==target);
        // 配下の放火が見つかれば、抗議と当主の答え。
        var caught=new DomesticEvent("domestic:caught/test","caught",owners.First(x=>x.Value==npc[0]).Key,new(npc[0]),new(npc[1]),NationalStrategy.MonthStart(w,1560,11),1,0,"放火を突き止めた");
        var s4=Step(w,s3 with{Domestic=[..s3.Domestic??[],caught]},1560,11);
        Assert.Contains(s4.Duties!,e=>e.Kind=="incident"&&e.House!.Value.Value==npc[0]&&e.Other!.Value.Value==npc[1]);
    }

    [Fact]
    public void MarriagesAdoptionsDefectionsLoyaltyAndSupplySightingsAreRecorded()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var npc=Npc(w);var a=npc[0];var b=npc[1];long now=NationalStrategy.MonthStart(w,1560,7);
        var man=new EntityId(w.NewGame!.Start.People.First(p=>p.Faction==a).Id);var woman=new EntityId(w.NewGame.Start.People.First(p=>p.Faction==b).Id);
        var marr=new StrategyMarriage(new("pact:old"),man,woman,NationalStrategy.MonthStart(w,1560,1),1);
        var site=NationalStrategy.Owners(w,w.Strategy).First(x=>x.Value==b).Key;
        var war=new StrategyCampaign(new("campaign:marwar"),new(a),new(b),new(site),now,1,"border");
        var adopt=new PoliticsEvent("politics:adopt/test2","adopt",null,new(a),new(b),man,now,1,0,"養子");
        var s=Step(w,w.Strategy! with{Marriages=[..w.Strategy.Marriages??[],marr],Campaigns=[..w.Strategy.Campaigns,war],Politics=[..w.Strategy.Politics??[],adopt]},1560,7);
        Assert.Contains(s.Duties!,e=>e.Kind=="marriage-check"&&e.Person==man&&e.Amount==0);
        Assert.Contains(s.Duties!,e=>e.Kind=="adopt-consent"&&e.Person==man);
        // 鞍替え：離反は通知として記録し、届いた月に効く。
        var power=NationalStrategy.Powers(w,NationalStrategy.Owners(w,s));var pending=(s.Duties??[]).ToList();
        Assert.True(Duties2.Defer(w,pending,a,b,now,w.CommitSequence,power));
        var notice=pending.Single(e=>e.Kind=="defect-notice");Assert.True(notice.Arrives>notice.At);
        var d=WorldCalendar.At(w.EpochDate,notice.Arrives!.Value).AddMonths(1);
        var s2=Step(w,s with{Duties=pending.ToArray()},d.Year,d.Month);
        Assert.Contains(s2.Defections!,x=>x.House.Value==a&&x.From.Value==b);
        Assert.False(Duties2.Defer(Without(w),[],a,b,now,1,power));
        // 恩と忠誠：一族は高く、給付を削った家では下がる。
        w.Strategy=s2;int loyal=Duties2.Loyalty(w,s2,man,a);Assert.InRange(loyal,0,100);
        var cut=s2 with{Houses=s2.Houses!.Select(h=>h.House.Value==a?h with{StipendCut=20}:h).ToArray()};Assert.True(Duties2.Loyalty(w,cut,man,a)<=loyal);
        Duties2.Validate(w,s2,null);
    }
}
