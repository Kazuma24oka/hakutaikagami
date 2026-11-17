using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S07（2026-11-17）：拠点ごとの人口・実名の寺社と門徒・家中職・拠点の値の初期データ・全国の賊の巣。
[Trait("Tier","Heavy")]
public sealed class Territory2Tests
{
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.Territory2).ToArray()};
        return old;
    }

    [Fact]
    public void CastleSitesTakeTheirVillagePopulationFromTheDistrictEstimate()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());var old=Without(w);
        var castleSites=CommunityEconomy.SitesFor(w).Where(s=>s.Value.StartsWith("site:c-",StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(castleSites);
        var pops=castleSites.Select(s=>Territory2.Population(w,s)).ToArray();
        Assert.All(pops,p=>Assert.InRange(p,50,2000));
        Assert.True(pops.Distinct().Count()>10,"拠点ごとに人口が違う");
        Assert.Equal(CommunityEconomy.Population,Territory2.Population(w,new("site:castle")));
        Assert.All(castleSites,s=>Assert.Equal(CommunityEconomy.Population,Territory2.Population(old,s)));
        var s0=castleSites.First();
        Assert.Equal(Territory2.Population(w,s0)/2,Territory2.Workers(w,s0));
        Assert.Equal(CommunityEconomy.InitialVillage*Territory2.Population(w,s0)/CommunityEconomy.Population,Territory2.InitialVillage(w,s0));
        // 村の米の収支は人口に合わせても保存則のまま（帳簿を作り直して検証が通る）。
        var acc=CommunityEconomy.AccountAt(w,s0,w.ElapsedSeconds,w.CommitSequence+1);
        Assert.Equal(Territory2.Population(w,s0),acc.Population);
        _=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
    }

    [Fact]
    public void RealTemplesStandAtTheNearestCastleSitesAndShinshuProvincesHaveFollowers()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(Territory2.TempleCount>=20);
        Assert.Equal(HakutaiDbSupplement.Current.Temples.Count(t=>t.LatitudeE7!=null),w.Definitions.Sites.Sum(x=>Territory2.TemplesAt(w,x.Id).Length));
        var sites=w.Definitions.Sites.Select(s=>s.Id).ToArray();
        var temple=sites.First(s=>Territory2.TempleAt(w,s)?.Name=="延暦寺");
        Assert.Equal("延暦寺",SocialWorld.Temple(w,temple).Name);
        Assert.NotEqual("延暦寺",SocialWorld.Temple(Without(w),temple).Name);
        var castle=CommunityEconomy.SitesFor(w).First(s=>s.Value.StartsWith("site:c-",StringComparison.Ordinal)&&Territory2.Profile(w,s)!.Monto>0);
        var p=Territory2.Profile(w,castle)!;
        Assert.Equal(p.Population*3/10,p.Monto);
        Assert.InRange(p.Agriculture,0,100);Assert.InRange(p.Sections,0,4);Assert.Contains("推定",p.Basis);
        Assert.Null(Territory2.Profile(Without(w),castle));
    }

    [Fact]
    public void UnownedCastlesAreBanditCampsThatMakeTheRoadDangerous()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var camps=Territory2.CampSites(w);
        Assert.InRange(camps.Length,10,200);
        Assert.All(camps.Where(c=>c.StartsWith("site:c-",StringComparison.Ordinal)),c=>{Assert.Equal("山城",Warfare2.CastleType(new(c)));Assert.DoesNotContain(w.NewGame!.Start.Factions,f=>f.Home==c);});
        camps=camps.Where(c=>c.StartsWith("site:c-",StringComparison.Ordinal)).ToArray();
        Assert.True(BanditEncounter.Dangerous(w,new(camps[0])));
        Assert.False(BanditEncounter.Dangerous(Without(w),new(camps[0])));
        var p=Territory2.Profile(w,new(camps[0]))!;Assert.Equal(40,p.Security);
    }

    [Fact]
    public void HousePostsTakenByTheStartYearShowInTheNames()
    {
        // 年の分かる家中職の記録は少ない（1560年以前は1件）。1600年の開始で、その年までに就いた人を確かめる。
        var head=NationalStart.Scenario(NationalStart.RevenueEdition,NationalSystems.ForYear(1600)).Factions[0].Head;
        var w=NationalStart.Create(head,year:1600);
        var person=w.NewGame!.Start.People.Select(p=>new EntityId(p.Id)).FirstOrDefault(p=>NationalStart.DbPerson(p.Value) is {} db&&HakutaiDbSupplement.Person(db.Id)?.HousePosts?.Any(h=>h.From is <=1600)==true);
        Assert.NotEqual(default,person);
        var post=HakutaiDbSupplement.Person(NationalStart.DbPerson(person.Value)!.Id)!.HousePosts!.First(h=>h.From is <=1600).Post;
        Assert.Contains(CentralStanding.KnownTitles(w,w.PlayerControl,person),t=>t.EndsWith(post,StringComparison.Ordinal));
        Assert.DoesNotContain(CentralStanding.KnownTitles(Without(w),w.PlayerControl,person),t=>t.EndsWith(post,StringComparison.Ordinal)&&!CentralTitles.Held1560(w,person).Any(h=>h.Office==t));
    }
}
