using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S09A（2026-11-17）：志の推定と兵科適性、家の目標と計画、断る条件、自発登用と役の目標、実務の技能、隠居、元服、各年代の改名の台帳。
[Trait("Tier","Heavy")]
public sealed class Persons2Tests
{
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.Persons2).ToArray()};
        return old;
    }
    static void Month(ContractWorld w,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;NationalStrategy.Apply(w);}

    [Fact]
    public void AspirationsAreEstimatedFromStandingAndAptitudesFromSkills()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var heads=w.NewGame!.Start.Factions.Select(f=>new EntityId(f.Head)).ToArray();
        Assert.All(heads.Take(20),h=>Assert.Contains(Persons2.Aspiration(w,h)!.Value,new[]{"天下","家の存続"}));
        Assert.Contains("推定",Persons2.Aspiration(w,heads[0])!.Basis);
        var retainer=w.NewGame.Start.People.First(p=>p.Faction!=null&&!w.NewGame.Start.Factions.Any(f=>f.Head==p.Id)&&NationalStart.DbPerson(p.Id)!=null);
        Assert.Contains(Persons2.Aspiration(w,new(retainer.Id))!.Value,Persons2.Aspirations);
        Assert.Matches("^槍[ABC]　弓[ABC]　騎馬[ABC]　鉄砲[ABC]$",Persons2.Aptitude(w,new(retainer.Id)));
        Assert.Null(Persons2.Aspiration(Without(w),heads[0]));
    }

    [Fact]
    public void HousesHoldGoalsWithPlansAndRefuseThoseWhoBetrayedThem()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var goals=w.Strategy!.Goals!;Assert.True(goals.Length>20);
        Assert.All(goals,g=>Assert.Contains(g.Goal,new[]{Persons2.Tenka,Persons2.Unify,Persons2.Survive,Persons2.Serve}));
        Assert.Contains(goals,g=>g.Province!=null);
        var me=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;Assert.DoesNotContain(goals,g=>g.House.Value==me);
        // 計画の国の城が先に選ばれ、家の存続を目指す家は3倍の差がなければ攻めない。
        var planned=goals.First(g=>g.Province!=null);
        (string Site,string? Owner)[] targets=[("site:x-other",null),("site:x-plan",null)];
        var owners=NationalStrategy.Owners(w,w.Strategy);
        var inPlan=owners.First(x=>x.Value!=null&&x.Value!=planned.House.Value&&CentralStanding.ProvinceOf(new(x.Key))==planned.Province);
        var other=owners.First(x=>x.Value!=null&&x.Value!=planned.House.Value&&CentralStanding.ProvinceOf(new(x.Key))!=planned.Province);
        var pick=Persons2.Choose(w,planned.House.Value,[(other.Key,other.Value),(inPlan.Key,inPlan.Value)],_=>200);
        Assert.Equal(inPlan.Key,pick!.Value.Site);
        var survive=w.Strategy with{Goals=[..w.Strategy.Goals!,planned with{Goal=Persons2.Survive}]};
        var sw=ContractJson.Read<ContractWorld>(ContractJson.Write(w));sw.Strategy=survive;
        Assert.Null(Persons2.Choose(sw,planned.House.Value,[(inPlan.Key,inPlan.Value)],_=>200));
        // 裏切り：約束を破って攻めた家との同盟を、裏切られた家は10年断る。理由は知らせが届いた人だけが知る。
        long now=NationalStrategy.MonthStart(w,1560,7);
        var a=new EntityId(goals[0].House.Value);var b=new EntityId(goals[1].House.Value);
        var site=owners.First(x=>x.Value==b.Value).Key;
        var pact=new StrategyPact(new("pact:test"),a,b,"alliance",NationalStrategy.MonthStart(w,1560,6),now+365*86400L,1,"test"){Broken=NationalStrategy.MonthStart(w,1560,6)};
        var c=new StrategyCampaign(new("campaign:betray"),a,b,new(site),NationalStrategy.MonthStart(w,1560,6),1,"betrayal");
        w.Strategy=w.Strategy with{Pacts=[..w.Strategy.Pacts??[],pact],Campaigns=[..w.Strategy.Campaigns,c]};
        Month(w,1560,7);
        Assert.Contains(w.Strategy!.Refusals!,r=>r.House==b&&r.Other==a&&r.Reason.Contains("約束を破られた"));
        Assert.True(Persons2.Refuses(w,a.Value,b.Value,w.ElapsedSeconds));
        Assert.False(Persons2.Refuses(w,a.Value,b.Value,w.ElapsedSeconds+11*365*86400L));
        Assert.Contains(Persons2.Heard(w,w.PlayerControl,w.ElapsedSeconds+365*86400L),r=>r.Text.Contains("断っている"));
        Assert.False(Persons2.Refuses(Without(w),a.Value,b.Value,w.ElapsedSeconds));
    }

    [Fact]
    public void HousesRecruitMasterlessMenAndReviewTheirOfficers()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var me=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var owners=NationalStrategy.Owners(w,w.Strategy);
        // 人手の足りない家（家中の成人が、国の数×2＋6 に足りない家）。
        var h=w.Strategy!.Houses!.First(x=>x.House.Value!=me&&FamilyBirth.People(w).Count(p=>p.Faction==x.House.Value&&w.Characters.Single(c=>c.Id.Value==p.Id).Life==LifeState.Adult)<owners.Where(o=>o.Value==x.House.Value).Select(o=>CentralStanding.ProvinceOf(new(o.Key))).Distinct().Count()*2+6);
        var site=owners.First(x=>x.Value==h.House.Value).Key;
        // 領地に主のない成人を一人置く。
        var ronin=w.NewGame.Start.People.First(p=>p.Faction!=null&&p.Faction!=h.House.Value&&p.Faction!=me&&!w.NewGame.Start.Factions.Any(f=>f.Head==p.Id)&&w.Characters.Single(c=>c.Id.Value==p.Id).Life==LifeState.Adult);
        w.NewGame=w.NewGame with{Start=w.NewGame.Start with{People=w.NewGame.Start.People.Select(p=>p.Id==ronin.Id?p with{Faction=null,Lord=null,Site=site}:p).ToArray()}};
        Month(w,1560,7);
        Assert.Contains(w.Strategy!.Persons!,e=>e.Kind=="recruit"&&e.Person.Value==ronin.Id&&e.House==h.House);
        Assert.Equal(h.House.Value,Persons2.Recruits(w)[ronin.Id]);
        // 勘定奉行は、1年務めて借銀が残れば役を解かれる。
        var tr=Domestic2.House(w,h.House)!.Posts.FirstOrDefault(p=>p.Post==Domestic2.Treasurer);
        if(tr!=null)
        {
            w.Strategy=w.Strategy with{Houses=w.Strategy.Houses!.Select(x=>x.House==h.House?x with{Debt=5000,Posts=x.Posts.Select(p=>p==tr?p with{Since=p.Since-2*365*86400L}:p).ToArray()}:x).ToArray()};
            Month(w,1561,1);
            Assert.Contains(w.Strategy!.Persons!,e=>e.Kind=="review"&&e.Person==tr.Person);
        }
    }

    [Fact]
    public void SkillsGrowWithServiceAndTeachingAndFadeWithAge()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var h=w.Strategy!.Houses!.First(x=>x.Posts.Any(p=>p.Post==Domestic2.WorksMagistrate));
        var wm=h.Posts.First(p=>p.Post==Domestic2.WorksMagistrate);
        int start=CharacterDevelopment.StartLevel(w,wm.Person,DevelopmentSkill.Construction);
        int born=w.NewGame!.Start.People.Single(p=>p.Id==wm.Person.Value).BirthYear??1520;
        long later=wm.Since+5*365*86400L;int age=WorldCalendar.At(w.EpochDate,later).Year-born;
        int expect=Math.Clamp(start+10-(age>55?(age-55)*2:0),0,100);
        Assert.Equal(expect,Persons2.Skill(w,wm.Person,DevelopmentSkill.Construction,later));
        Assert.Equal(start,Persons2.Skill(Without(w),wm.Person,DevelopmentSkill.Construction,later));
        // 年を取ると衰える。
        var old=w.NewGame.Start.People.First(p=>p.BirthYear is int y&&1560-y>=50&&CharacterDevelopment.StartLevel(w,new(p.Id),DevelopmentSkill.Tactics)>=20);
        long at70=NationalStrategy.MonthStart(w,old.BirthYear!.Value+70,1)-0;
        Assert.True(Persons2.Skill(w,new(old.Id),DevelopmentSkill.Tactics,at70)<CharacterDevelopment.StartLevel(w,new(old.Id),DevelopmentSkill.Tactics));
        // 当主の戦術は合戦の力に効く。
        var c=new StrategyCampaign(new("campaign:tac"),h.House,h.House,new("site:x"),w.ElapsedSeconds,1,"border");
        var (a,d)=Persons2.Effective(w,w.Strategy,c,1000,1000);Assert.Equal(a,d);
    }

    [Fact]
    public void OldLordsRetireAndChildrenComeOfAgeRecallingTheirWishes()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        // 開始の世界は成人だけなので、一人の生年を1546年にして、1561年1月に元服させる（ゲームの中で生まれた子と同じ道）。
        var me=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var youth=w.NewGame.Start.People.First(p=>p.Faction!=null&&p.Faction!=me&&p.BirthParents is {Length:>0}&&!w.NewGame.Start.Factions.Any(f=>f.Head==p.Id));
        w.NewGame=w.NewGame with{Start=w.NewGame.Start with{People=w.NewGame.Start.People.Select(p=>p.Id==youth.Id?p with{BirthYear=1546}:p).ToArray()}};
        for(int y=1561;y<=1564;y++)Month(w,y,1);
        var retire=w.Strategy!.Persons?.FirstOrDefault(e=>e.Kind=="retire");
        Assert.NotNull(retire);
        var row=w.Strategy!.Successions!.Single(x=>x.Retired==true&&x.Previous==retire!.Person);
        Assert.Equal(LifeState.Adult,w.Characters.Single(c=>c.Id==row.Previous).Life);
        Assert.Equal(row.Heir,HouseSuccession.Head(w,row.House));
        Assert.Contains("隠居",CentralStanding.KnownTitles(w,w.PlayerControl,row.Previous));
        Assert.Contains(w.Strategy.Persons!,e=>e.Kind=="genpuku"&&e.Person.Value==youth.Id&&e.Text.Contains("元服"));
        Persons2.Validate(w,w.Strategy,null);
    }

    [Fact]
    public void The1600StartGetsItsOwnNameChangeLedger()
    {
        var head=NationalStart.Scenario(NationalStart.RevenueEdition,NationalSystems.ForYear(1600)).Factions[0].Head;
        var w=NationalStart.Create(head,year:1600);
        Assert.NotNull(w.NationalNames);Assert.True(NationalNameHistory.Enabled(w));
        var old=Without(w);Assert.False(NationalNameHistory.Enabled(old));
    }
}
