using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S09D（2026-11-17）：水利の争いの裁定と普請、寺社への願いとキリスト教、惣国と徳政、客将と任命の再承認、国司・京官、幕府への申請と奉行人、中央の仲裁。
[Trait("Tier","Heavy")]
public sealed class Cases2Tests
{
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.Cases2).ToArray()};
        return old;
    }
    static void Month(ContractWorld w,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;NationalStrategy.Apply(w);}
    static StrategyState Step(ContractWorld w,StrategyState s,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;w.Strategy=s;return Cases2.Month(w,s,NationalStrategy.Owners(w,s));}
    static string Me(ContractWorld w)=>w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction!;
    static StrategyState Rich(StrategyState s)=>s with{Houses=s.Houses!.Select(h=>h with{Rice=Math.Max(h.Rice,10000),Coin=Math.Max(h.Coin,10000)}).ToArray()};

    [Fact]
    public void WaterDisputesAreSurveyedRuledAndOnlyTakeEffectWhenTheWorksAreDone()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var owners=NationalStrategy.Owners(w,w.Strategy);
        var prov=owners.Where(x=>x.Value!=null&&x.Value!=Me(w)).Select(x=>CentralStanding.ProvinceOf(new(x.Key))).First(p=>p!=null&&owners.Count(o=>CentralStanding.ProvinceOf(new(o.Key))==p&&o.Value!=null&&o.Value!=Me(w))>=4)!;
        long aug=NationalStrategy.MonthStart(w,1560,8);
        var harvest=new DomesticEvent("domestic:harvest/test","harvest",prov,null,null,aug,w.CommitSequence,60,"凶作");
        var s=Step(w,Rich(w.Strategy! with{Domestic=[..w.Strategy.Domestic??[],harvest]}),1560,8);
        var dispute=s.Cases!.Single(e=>e.Kind=="water");Assert.Equal(prov,CentralStanding.ProvinceOf(new(dispute.Site!)));
        var s2=Step(w,s,1560,9);
        var ruling=s2.Cases!.SingleOrDefault(e=>e.Kind is "ruling" or "accord"&&e.Detail==dispute.Id);
        if(owners[dispute.Site!]==Me(w)){Assert.Null(ruling);return;}
        Assert.NotNull(ruling);Assert.Equal(dispute.House==dispute.Other?"ruling":"accord",ruling!.Kind);Assert.True(ruling.Arrives>ruling.At);
        Assert.DoesNotContain(s2.Cases!,e=>e.Kind=="water-done");
        var s3=Step(w,s2,1560,10);var s4=Step(w,s3,1560,11);
        Assert.Contains(s4.Cases!,e=>e.Kind=="water-done"&&e.Site==dispute.Site);
    }

    [Fact]
    public void ThePlayerAsksTemplesAndTradeHousesAllowMissionaries()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var t=Domestic2.Temples(w).First(x=>x.Lord!=null&&x.Lord.Value.Value!=Me(w));
        Cases2.AskTemple(w,w.PlayerControl,t.Temple,"寄進");
        Assert.Throws<InvalidDataException>(()=>Cases2.AskTemple(w,w.PlayerControl,t.Temple,"安堵"));
        Cases2.AskTemple(w,w.PlayerControl,t.Temple,"仲裁");
        var asks=Cases2.Events(w).Where(e=>e.Kind=="temple-case"&&e.Site==t.Temple).ToArray();Assert.Equal(2,asks.Length);
        Assert.Equal(1,asks[0].Amount);Assert.Equal(t.Relation+1>=4?1:0,asks[1].Amount);
        Assert.Throws<InvalidDataException>(()=>Cases2.AskTemple(Without(w),w.PlayerControl,t.Temple,"寄進"));
        // 南蛮の港の国を持つ商の家は布教を許し、その国の寺社との関係が下がる。
        var owners=NationalStrategy.Owners(w,w.Strategy);
        var port=owners.First(x=>x.Value!=null&&x.Value!=Me(w)&&CentralStanding.ProvinceOf(new(x.Key)) is "肥前" or "豊後" or "薩摩");
        var s=Step(w,w.Strategy! with{Houses=w.Strategy.Houses!.Select(h=>h.House.Value==port.Value?h with{Policy=Domestic2.Trade}:h).ToArray()},1560,7);
        Assert.Contains(s.Cases!,e=>e.Kind=="mission"&&e.House!.Value.Value==port.Value);
        w.Strategy=s;Assert.True(Cases2.Mission(w,port.Value!));
    }

    [Fact]
    public void IkkiCanRuleAProvinceAndIndebtedLordsMayIssueTokusei()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var owners=NationalStrategy.Owners(w,w.Strategy);
        var prov=owners.Where(x=>x.Value!=null&&x.Value!=Me(w)).GroupBy(x=>CentralStanding.ProvinceOf(new(x.Key))).First(g=>g.Key!=null&&g.Count()>=2);
        long old=NationalStrategy.MonthStart(w,1559,1);
        var sites=prov.Select(x=>new DomesticSite(x.Key,0,0,0,5,0,null,0,null,0,0,"村",0)).ToArray();
        var ikki=prov.Select(x=>new DomesticEvent("domestic:ikki/"+x.Key,"ikki",x.Key,new(x.Value!),null,old,0,5,"一揆")).ToArray();
        var s=Step(w,w.Strategy! with{Sites=[..(w.Strategy.Sites??[]).Where(x=>!prov.Any(p=>p.Key==x.Site)),..sites],Domestic=[..w.Strategy.Domestic??[],..ikki]},1560,7);
        Assert.Contains(s.Cases!,e=>e.Kind=="sokoku"&&e.Site==prov.Key);
        w.Strategy=s;Assert.True(Cases2.Sokoku(w,prov.Key!,w.ElapsedSeconds));Assert.False(Cases2.Sokoku(Without(w),prov.Key!,w.ElapsedSeconds));
        // 徳政：借銀のある家の拠点で今月一揆が起きると、借銀が年貢の半分を超える家は徳政を出す。
        var lord=prov.First().Value!;long now=NationalStrategy.MonthStart(w,1560,8);
        var fresh=new DomesticEvent("domestic:ikki/fresh","ikki",prov.First().Key,new(lord),null,now,w.CommitSequence,5,"一揆");
        var s2=Step(w,s with{Houses=s.Houses!.Select(h=>h.House.Value==lord?h with{Debt=10_000_000}:h).ToArray(),Domestic=[..s.Domestic!,fresh]},1560,8);
        Assert.Contains(s2.Cases!,e=>e.Kind=="tokusei"&&e.House!.Value.Value==lord&&e.Text.Contains("棒引き"));
        Assert.Equal(0,s2.Houses!.Single(h=>h.House.Value==lord).Debt);
    }

    [Fact]
    public void StrongAlliesLendGuestGeneralsAndNewHeadsReconfirmOffices()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var owners=NationalStrategy.Owners(w,w.Strategy);var power=NationalStrategy.Powers(w,owners);
        var npc=w.Strategy!.Houses!.Select(h=>h.House.Value).Where(h=>h!=Me(w)&&owners.ContainsValue(h)).ToArray();
        var strong=npc.OrderByDescending(h=>power.GetValueOrDefault(h)).First();var weak=npc.Where(h=>power.GetValueOrDefault(h)*2<=power.GetValueOrDefault(strong)).OrderBy(h=>h,StringComparer.Ordinal).First();
        long t=NationalStrategy.MonthStart(w,1561,3);var site=owners.First(x=>x.Value==weak).Key;var foe=npc.First(h=>h!=weak&&h!=strong);
        var pact=new StrategyPact(new("pact:guest"),new(strong),new(weak),"alliance",t,t+3*365*86400L,1,"test");
        var war=new StrategyCampaign(new("campaign:guest-war"),new(foe),new(weak),new(site),t,1,"border");
        var s=Step(w,w.Strategy with{Pacts=[..w.Strategy.Pacts??[],pact],Campaigns=[..w.Strategy.Campaigns,war]},1561,4);
        var g=s.Cases!.Single(e=>e.Kind=="guest"&&e.House!.Value.Value==weak);
        w.Strategy=s;Assert.Equal(g.Person,Cases2.Guest(w,weak,g.Site,w.ElapsedSeconds));Assert.Null(Cases2.Guest(w,weak,g.Site,g.Arrives!.Value+86400));
        // 家督の後、家督を争った人の役を解く。
        var h=s.Houses!.First(x=>x.House.Value==weak);var rival=h.Posts.FirstOrDefault()?.Person;
        if(rival!=null)
        {
            var heir=new EntityId(w.NewGame!.Start.People.First(p=>p.Faction==weak&&p.Id!=rival.Value.Value).Id);
            long now=NationalStrategy.MonthStart(w,1561,6);
            var succ=new StrategySuccession(new(weak),HouseSuccession.Head(w,new(weak)),heir,now,w.CommitSequence+5);
            var dispute=new StrategyDispute(new(weak),succ.Previous,heir,[heir,rival.Value],"家中の筆頭格",now,succ.Revision);
            var s2=Step(w,s with{Successions=[..s.Successions??[],succ],Disputes=[..s.Disputes??[],dispute]},1561,6);
            Assert.DoesNotContain(s2.Houses!.Single(x=>x.House.Value==weak).Posts,p=>p.Person==rival);
            Assert.Contains(s2.Cases!,e=>e.Kind=="reconfirm"&&e.House!.Value.Value==weak);
        }
    }

    [Fact]
    public void TheShogunMediatesForLosingHousesAndAnswersThePlayersPetition()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var owners=NationalStrategy.Owners(w,w.Strategy);var power=NationalStrategy.Powers(w,owners);
        var loser=w.Strategy!.Houses!.First(h=>h.House.Value!=Me(w)&&h.Posts.Any(p=>p.Post==Domestic2.Envoy)&&owners.Count(x=>x.Value==h.House.Value)>=3).House;
        var winner=w.Strategy.Houses!.Select(h=>h.House.Value).First(h=>h!=loser.Value&&h!=Me(w)&&power.GetValueOrDefault(h)<3*power.GetValueOrDefault(loser.Value)&&owners.ContainsValue(h));
        var sites=owners.Where(x=>x.Value==loser.Value).Select(x=>x.Key).Take(2).ToArray();long t=NationalStrategy.MonthStart(w,1560,6);
        var c1=new StrategyCampaign(new("campaign:lose1"),new(winner),loser,new(sites[0]),t,1,"border");var c2=c1 with{Id=new("campaign:lose2"),Target=new(sites[1])};
        var siege=new SiegeProgress(owners.First(x=>x.Value==loser.Value&&!sites.Contains(x.Key)).Key,new(winner),1,4,t);
        var s=Step(w,w.Strategy with{Campaigns=[..w.Strategy.Campaigns,c1,c2],Outcomes=[..w.Strategy.Outcomes,new(c1.Id,t+86400,1,true),new(c2.Id,t+86400,1,true)],Sieges=[siege]},1560,7);
        var m=s.Cases!.Single(e=>e.Kind=="mediation"&&e.House==loser);Assert.Equal(1,m.Amount);
        Assert.Contains(s.Pacts!,p=>p.Basis=="mediation:shogun"&&(p.A==loser||p.B==loser));
        Assert.DoesNotContain(s.Sieges??[],x=>x.Site==siege.Site);
        // 本人の幕府への申請：京に届いた後に決まり、城の数が足りれば栄典を許す。
        w.Strategy=s;Cases2.AskShogunate(w,w.PlayerControl,"御供衆");
        Assert.Throws<InvalidDataException>(()=>Cases2.AskShogunate(w,w.PlayerControl,"御供衆"));
        var ask=Cases2.Events(w).Single(e=>e.Kind=="shogunate-ask");Assert.True(ask.Arrives>ask.At);
        var d=WorldCalendar.At(w.EpochDate,ask.Arrives!.Value).AddMonths(1);
        var s2=Step(w,w.Strategy!,d.Year,d.Month);
        var ans=s2.Cases!.Single(e=>e.Kind=="shogunate-answer");Assert.Equal(owners.Count(x=>x.Value==Me(w))>=2?1:0,ans.Amount);
        if(ans.Amount==1)Assert.Contains(s2.Honors!,x=>x.Person==w.PlayerControl&&x.Kind=="御供衆");
        Assert.NotEmpty(Cases2.Bugyonin(w));
    }

    [Fact]
    public void ProvincialTitlesStrengthenAttacksOnThatProvince()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,6);
        var owners=NationalStrategy.Owners(w,w.Strategy);
        var holder=w.NewGame!.Start.Factions.Select(f=>(f,head:HouseSuccession.Head(w,new(f.Id)))).Select(x=>(x.f,x.head,prov:CentralTitles.Held1560(w,x.head).Select(h=>CentralTitles.Titles.FirstOrDefault(t=>t.Name==h.Office)).FirstOrDefault(t=>t?.Kind=="kokushi")?.Province))
            .FirstOrDefault(x=>x.prov!=null&&owners.Any(o=>o.Value!=null&&o.Value!=x.f.Id&&CentralStanding.ProvinceOf(new(o.Key))==x.prov));
        Assert.NotNull(holder.prov);
        var target=owners.First(o=>o.Value!=null&&o.Value!=holder.f.Id&&CentralStanding.ProvinceOf(new(o.Key))==holder.prov);
        var c=new StrategyCampaign(new("campaign:title"),new(holder.f.Id),new(target.Value!),new(target.Key),w.ElapsedSeconds,1,"border");
        Assert.True(Cases2.ProvinceTitle(w,holder.head,holder.prov));
        var (a,_)=Cases2.Effective(w,w.Strategy!,c,1000,1000);Assert.True(a>=1050);
        var (oa,_)=Cases2.Effective(Without(w),w.Strategy!,c,1000,1000);Assert.Equal(1000,oa);
    }
}
