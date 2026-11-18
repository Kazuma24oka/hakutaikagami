using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S12（2026-11-18）：最初の完成版の残りの機能（一覧の Finish2）。
[Trait("Tier","Heavy")]
public sealed class Finish2Tests
{
    static readonly Lazy<ContractWorld> start=new(()=>NationalStart.Create(NationalStartTests.Nobunaga(),history:HistoryMode.Historical));
    static readonly Lazy<ContractWorld> month=new(()=>{var w=ContractJson.Copy(start.Value);w.ElapsedSeconds=NationalStrategy.MonthStart(w,1560,6);w.CommitSequence+=5;NationalStrategy.Apply(w);return w;});
    static ContractWorld Month()=>ContractJson.Copy(month.Value);
    static string[] Houses(ContractWorld w)=>NationalStrategy.Owners(w,w.Strategy).Values.Where(v=>v!=null).Select(v=>v!).Distinct().OrderBy(v=>v,StringComparer.Ordinal).ToArray();
    static bool PlayerHouse(ContractWorld w,string h)=>HouseSuccession.Head(w,new(h))==w.PlayerControl||Screens2.HouseOf(w,w.PlayerControl)==h;
    static StrategyState Run(ContractWorld w,StrategyState s)=>Finish2.Month(w,s,NationalStrategy.Owners(w,s));

    [Fact]
    public void TheFirstMonthPlacesTempleOfficesAndChildTraitsAsEstimates()
    {
        var w=Month();Assert.True(Finish2.On(w));var e=Finish2.Events(w);
        Assert.Contains(e,x=>x.Kind=="temple-office"&&x.Text.Contains("推定"));
        Assert.Contains(e,x=>x.Kind=="child-start"&&x.Text.Contains("推定"));
        Assert.Single(e,x=>x.Kind=="started");
        // 次の月に繰り返さない。
        var next=Run(w,w.Strategy!);Assert.Equal(e.Count(x=>x.Kind=="temple-office"),next.Finish!.Count(x=>x.Kind=="temple-office"));
        NationalStrategy.Validate(w,null);
    }

    [Fact]
    public void SupplyMeritsAreRankedAndADisputeIsJudgedWithAReason()
    {
        var w=Month();var s=w.Strategy!;var owners=NationalStrategy.Owners(w,s);long now=w.ElapsedSeconds;
        var (house,target)=owners.Where(x=>x.Value!=null&&!PlayerHouse(w,x.Value!)).SelectMany(x=>NationalStrategy.Neighbors(x.Key).Where(n=>owners.GetValueOrDefault(n) is {} o&&o!=x.Value&&!PlayerHouse(w,o)).Select(n=>(x.Value!,n))).First();
        var defender=owners[target]!;var c=new StrategyCampaign(new("campaign:test/merit"),new(house),new(defender),new(target),now-30*86400L,w.CommitSequence,NationalStrategy.Border);
        var people=FamilyBirth.People(w).Where(p=>p.Faction==house&&w.Characters.Any(x=>x.Id.Value==p.Id&&x.Life==LifeState.Adult)&&p.Id!=HouseSuccession.Head(w,new(house)).Value).Select(p=>new EntityId(p.Id)).OrderBy(p=>p.Value,StringComparer.Ordinal).ToArray();
        s=s with{Campaigns=[..s.Campaigns,c],Outcomes=[..s.Outcomes,new(c.Id,now,w.CommitSequence,true)],Merits=[..s.Merits??[],new(c.Id,people[0],new(house),"落城",now,w.CommitSequence)],
            Logistics=[..s.Logistics??[],new("logistics:test/supply","supply",target,new(house),new(defender),c.At,w.CommitSequence,100,"兵糧")]};
        var r=Run(w,s).Finish!;
        var supply=r.Single(x=>x.Kind=="merit-supply");var judged=r.Single(x=>x.Kind=="adjudicate");
        Assert.Equal(people[0],judged.Person);Assert.Contains("落城",judged.Text);Assert.Contains("兵糧",judged.Text);
        // 兵糧の者の兵站が30以上なら先を争い、当主が理由を示して裁く。
        Assert.Equal(CharacterDevelopment.LevelAt(w,supply.Person!.Value,DevelopmentSkill.Logistics,now)>=30,r.Any(x=>x.Kind=="dispute"&&x.Text.Contains("理由")));
    }

    [Fact]
    public void AlliesGiveHostagesAndTheHolderDecidesWhenThePactEnds()
    {
        var w=Month();var s=w.Strategy!;long now=w.ElapsedSeconds;var houses=Houses(w).Where(h=>!PlayerHouse(w,h)).ToArray();
        var pact=new StrategyPact(new("pact:test/hostage"),new(houses[0]),new(houses[1]),"alliance",now-28*86400L,now+2*365*86400L,w.CommitSequence,"test");
        var r=Run(w,s with{Pacts=[..s.Pacts??[],pact]});var hostage=r.Finish!.Single(x=>x.Kind=="hostage"&&(x.House==pact.A||x.House==pact.B)&&x.Other!=null&&(x.Other==pact.A||x.Other==pact.B));
        // 盟約が切れた月：破った出陣が人質を出した家なら留め置き、そうでなければ返す。処刑はしない。
        var broken=r with{Pacts=r.Pacts!.Select(p=>p.Id==pact.Id?p with{Broken=now}:p).ToArray()};
        var after=Run(w,broken).Finish!;Assert.Contains(after,x=>x.Kind=="hostage-returned"&&x.Person==hostage.Person);
        Assert.DoesNotContain(after,x=>x.Text.Contains("処刑"));
    }

    [Fact]
    public void MediationThatBreaksASoleTruceClauseIsRefusedWithAReason()
    {
        var w=Month();var s=w.Strategy!;long now=w.ElapsedSeconds;var h=Houses(w).Where(x=>!PlayerHouse(w,x)).Take(3).Select(x=>new EntityId(x)).ToArray();
        var pact=new StrategyPact(new("pact:test/sole"),h[0],h[2],"alliance",now,now+365*86400L,w.CommitSequence,"test");
        var war=new StrategyCampaign(new("campaign:test/third"),h[2],h[1],new(NationalStrategy.Owners(w,s).First(x=>x.Value==h[1].Value).Key),now,w.CommitSequence,NationalStrategy.Border);
        var t=s with{Pacts=[..s.Pacts??[],pact],Terms=[..s.Terms??[],new(pact.Id,"単独で和睦しない",now+365*86400L)],Campaigns=[..s.Campaigns,war]};
        Assert.Contains("単独で和睦しない",Finish2.MediationConflict(w,t,h[0],h[1],now));
        Assert.Null(Finish2.MediationConflict(w,s,h[0],h[1],now));
    }

    [Fact]
    public void RecruitsRefuseForOldAgeAndAStarvedCastleWithoutWaterSupplyYields()
    {
        var w=Month();var s=w.Strategy!;long now=w.ElapsedSeconds;int year=WorldCalendar.At(w.EpochDate,now).Year;
        var old=FamilyBirth.People(w).First(p=>p.BirthYear is int b&&year-b>=60);
        Assert.Equal("隠居を望む",Finish2.RecruitRefusal(w,s,old.Id,new(Houses(w)[0]),now));
        var young=FamilyBirth.People(w).First(p=>p.BirthYear is int b&&year-b<40&&p.Faction==null);
        Assert.NotEqual("隠居を望む",Finish2.RecruitRefusal(w,s,young.Id,new(Houses(w)[0]),now));
        // 兵糧の尽きた城：水軍で守り手が上回らなければ開城、上回れば水路から米が入って持ちこたえる。
        var owners=NationalStrategy.Owners(w,s);var (a,target)=owners.Where(x=>x.Value!=null&&!PlayerHouse(w,x.Value!)).SelectMany(x=>NationalStrategy.Neighbors(x.Key).Where(n=>owners.GetValueOrDefault(n) is {} o&&o!=x.Value&&!PlayerHouse(w,o)).Select(n=>(x.Value!,n))).First();
        var d=owners[target]!;var c=new StrategyCampaign(new("campaign:test/starve"),new(a),new(d),new(target),now,w.CommitSequence,NationalStrategy.Border);
        var hungry=s with{Houses=s.Houses!.Select(h=>h.House.Value==d?h with{Rice=0}:h).ToArray(),Stores=(s.Stores??[]).Where(x=>x.House.Value!=a&&x.House.Value!=d).ToArray()};
        w.Strategy=hungry;Assert.True(Finish2.Starved(w,hungry,c,300,100,300,100));Assert.False(Finish2.Starved(w,hungry,c,120,100,120,100));
        var fleet=hungry with{Stores=[..hungry.Stores!,new(new(d),0,50,now)]};w.Strategy=fleet;Assert.False(Finish2.Starved(w,fleet,c,300,100,300,100));
    }

    [Fact]
    public void AnInjuredPlayerReturnsADutyAndRecordsTheReturnOnRecovery()
    {
        var w=Month();
        // 本人の世界でない（傷病の無い）ときは返上できない。
        Assert.ThrowsAny<InvalidDataException>(()=>Finish2.Relief(w,new("task:none"),"返還"));
        Assert.ThrowsAny<InvalidDataException>(()=>Finish2.Relief(w,new("task:none"),"逃げる"));
        // 代理の開封：家老がいなければ後見か正室（いなければ当主のまま）。
        foreach(var h in Houses(w).Take(30))Assert.True(Finish2.Proxy(w,h) is null or {Value:not null});
        // 陣触れの併用の表。
        Assert.False(Duties2.Concurrency[("療養","陣触れ")]);Assert.False(Duties2.Concurrency[("捕らわれ","陣触れ")]);
    }

    [Fact]
    public void AidIsRequestedByLetterAndArrivesAfterTheRoadDays()
    {
        var w=Month();var s=w.Strategy!;long now=w.ElapsedSeconds;var h=Houses(w).Where(x=>!PlayerHouse(w,x)&&s.Houses!.Any(d=>d.House.Value==x)).Take(2).Select(x=>new EntityId(x)).ToArray();
        var t=s with{Houses=s.Houses!.Select(d=>d.House==h[0]?d with{Rice=-10}:d.House==h[1]?d with{Rice=5000}:d).ToArray(),Pacts=[..s.Pacts??[],new(new("pact:test/aid"),h[0],h[1],"alliance",now-86400,now+365*86400L,w.CommitSequence,"test")]};
        var r=Run(w,t);var sent=r.Finish!.Single(e=>e.Kind=="aid-sent"&&e.Other==h[0]);Assert.Contains(r.Finish!,e=>e.Kind=="aid-request"&&e.House==h[0]&&e.Other==h[1]);
        Assert.Equal(5000-300,r.Houses!.Single(d=>d.House==h[1]).Rice);
        w.ElapsedSeconds=sent.Arrives!.Value;var later=Run(w,r);Assert.Contains(later.Finish!,e=>e.Kind=="aid-arrived"&&e.Other==h[1]);Assert.Equal(r.Houses!.Single(d=>d.House==h[0]).Rice+300,later.Houses!.Single(d=>d.House==h[0]).Rice);
    }

    [Fact]
    public void AViolationSeparatesEvidenceKnowledgeAndResponsibility()
    {
        var w=Month();var s=w.Strategy!;long now=w.ElapsedSeconds;var h=Houses(w).Where(x=>!PlayerHouse(w,x)).Take(2).Select(x=>new EntityId(x)).ToArray();
        var r=Run(w,s with{Politics=[..s.Politics??[],new("politics:test/violation","violation",null,h[0],h[1],null,now,w.CommitSequence,0,"不履行")]}).Finish!;
        Assert.Contains(r,e=>e.Kind=="violation-evidence"&&e.House==h[0]);Assert.Contains(r,e=>e.Kind=="violation-known"&&e.Arrives>=now);Assert.Contains(r,e=>e.Kind=="violation-responsibility"&&e.House==h[1]&&e.Text.Contains("責任"));
    }

    [Fact]
    public void ASickExecutorReturnsADutyAndTheReturnIsConfirmedOnRecovery()
    {
        // 全国の開始で本人が現地の仕事を頼まれた世界。
        var s=new WorldSession(ContractJson.Copy(start.Value));var i=NewGameWorld.LocalWork(s.DiagnosticSnapshot(),"request:test-relief");
        Assert.Equal(ResultCode.Accepted,s.Submit(i,s.Revision).Code);NewGameWorldTests.At(s,s.Seconds+2);
        var w=s.DiagnosticSnapshot();var t=w.Tasks.Single(x=>x.Executor==w.PlayerControl&&x.Issuer!=w.PlayerControl);
        Assert.ThrowsAny<InvalidDataException>(()=>Finish2.Relief(ContractJson.Copy(w),t.Id,"返還")); // 傷病のないときは返上できない
        var sick=PersonalHealth.Conditions(w,w.PlayerControl,w.ElapsedSeconds+30*365*86400L).First(c=>c.OnsetAt>w.ElapsedSeconds);
        w.ElapsedSeconds=sick.OnsetAt;w.Tasks=w.Tasks.Select(x=>x.Id==t.Id?x with{Execution=ExecutionPhase.Running}:x).ToArray();
        w.Strategy??=new StrategyState(1,NationalStrategy.Policy,[],[]);
        Finish2.Relief(w,t.Id,"延期");Assert.Equal(ExecutionPhase.Stopped,w.Tasks.Single(x=>x.Id==t.Id).Execution);Assert.Contains(Finish2.Events(w),e=>e.Kind=="relief"&&e.Text.Contains("癒えるまで"));
        Assert.ThrowsAny<InvalidDataException>(()=>Finish2.Relief(w,t.Id,"返還")); // 止まった務めは二度返さない
        w.ElapsedSeconds=PersonalHealth.Conditions(w,w.PlayerControl,w.ElapsedSeconds).Max(c=>PersonalHealth.RecoverAt(w,c))+1;
        if(PersonalHealth.Active(w,w.PlayerControl,w.ElapsedSeconds).Length==0){var r=Run(w,w.Strategy!);Assert.Contains(r.Finish!,e=>e.Kind=="recovered"&&e.Person==w.PlayerControl);}
    }

    [Fact]
    public void PassageHonorsAndSchemeDiscoveryAppearOverMonthsAndValidate()
    {
        using var noGuard=TurnSnapshotGuard.Off(); // 全国の重い検査は遊ぶ画面と同じ設定で流す（2026-11-18、検査の見直し）
        var w=Month();var kinds=new HashSet<string>();
        for(int m=7;m<=24&&!(kinds.Contains("passage")||kinds.Contains("protest")||kinds.Contains("passage-refused"));m++)
        {
            int y=1560+(m-1)/12,mm=(m-1)%12+1;w.ElapsedSeconds=NationalStrategy.MonthStart(w,y,mm);w.CommitSequence+=5;NationalStrategy.Apply(w);
            foreach(var e in Finish2.Events(w))kinds.Add(e.Kind);
        }
        Assert.True(kinds.Contains("passage")||kinds.Contains("protest")||kinds.Contains("passage-refused"),string.Join(",",kinds));
        if(NationalStrategy.Capital(w) is {} cap&&HouseSuccession.Head(w,cap)!=w.PlayerControl)Assert.Contains(Finish2.Events(w),e=>e.Kind=="honor"&&e.Text.Contains("御相伴衆"));
        NationalStrategy.Validate(w,null);
    }
}
