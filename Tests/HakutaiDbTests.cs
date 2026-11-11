using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class HakutaiDbTests
{
    static byte[] Raw()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir!=null&&!File.Exists(Path.Combine(dir.FullName,"Core","Data","hakutai-db-1560-v2.json")))dir=dir.Parent;
        return File.ReadAllBytes(Path.Combine(dir!.FullName,"Core","Data","hakutai-db-1560-v2.json"));
    }
    static HakutaiDbEdition Broken(Action<JsonObject> edit){var o=JsonNode.Parse(Raw())!.AsObject();edit(o);return HakutaiDb.Parse(Encoding.UTF8.GetBytes(o.ToJsonString()));}

    [Fact]public void EditionIsPinnedToOneSengokuDbVersionAndCoversTheCountry()
    {
        Assert.Equal("SengokuDB",HakutaiDb.Source.Repository);Assert.Matches("^[0-9a-f]{40}$",HakutaiDb.Source.Commit);
        Assert.Equal(1560,HakutaiDb.WorldYear);
        Assert.True(HakutaiDb.PeopleCount>4000);Assert.True(HakutaiDb.Houses.Count>280);
        var provinces=HakutaiDb.PublicGeography().Select(c=>c.Province).Where(p=>p!=null).Distinct().Count();
        Assert.True(provinces>=60,"国の数 "+provinces);
    }

    [Fact]public void ExistingStartPeopleLinkToTheSamePeopleAndHouses()
    {
        var nobunaga=HakutaiDb.ForGame("person:nobunaga")!;
        Assert.Equal("織田信長",nobunaga.Name);Assert.Equal("織田信長",nobunaga.StartName);Assert.Equal("G-000131",nobunaga.House);Assert.Equal("清洲城",nobunaga.CastleName);Assert.Equal(1534,nobunaga.Birth);
        Assert.Equal(nobunaga.Id,HakutaiDb.House(HakutaiDb.GameLinks["org:oda"])!.Head);
        Assert.Equal("今川館",HakutaiDb.ForGame("person:imagawa-yoshimoto")!.CastleName);
        Assert.Equal("小田原城",HakutaiDb.Castle(HakutaiDb.House(HakutaiDb.GameLinks["org:hojo"])!.Home!)!.Name);
        Assert.Equal("武田信玄",HakutaiDb.ForGame("person:shingen")!.Name);
        // 既存の開始版の人物の生年は、DBに年がある人と食い違わない。
        foreach(var edition in new[]{HistoricalStartCatalog.ImagawaEdition,CentralCourtCatalog.RegentEdition,HistoricalHouseholdCatalog.Id})
            foreach(var p in NewGameWorld.Scenario(edition).People.Where(p=>p.BirthYear!=null))
                if(HakutaiDb.ForGame(p.Id) is {Birth: int born})Assert.True(born==p.BirthYear,$"{p.Id} 開始版 {p.BirthYear} / DB {born}");
    }

    [Fact]public void HeadsBelongToTheirHouseAndStayAtTheHomeCastle()
    {
        foreach(var h in HakutaiDb.Houses.Where(h=>h.Head!=null))
        {
            var head=HakutaiDb.Person(h.Head!)!;Assert.Equal(h.Id,head.House);
            if(h.Home!=null)Assert.Equal(h.Home,head.Castle);
        }
        Assert.Equal("後北条",HakutaiDb.TopHouse(HakutaiDb.GameLinks["org:hojo"]).Name);
        var vassal=HakutaiDb.Houses.First(h=>h.Overlord!=null);Assert.Null(HakutaiDb.TopHouse(vassal.Id).Overlord);
    }

    [Fact]public void EveryEstimateKeepsHowItWasDecided()
    {
        foreach(var id in HakutaiDb.Houses.SelectMany(h=>HakutaiDb.Members(h.Id)).Select(p=>p.Id))
        {
            var p=HakutaiDb.Person(id)!;Assert.False(string.IsNullOrWhiteSpace(p.HouseBasis));
            if(p.Castle!=null)Assert.False(string.IsNullOrWhiteSpace(p.CastleBasis));
            if(p.Superior!=null)Assert.False(string.IsNullOrWhiteSpace(p.SuperiorBasis));
        }
        Assert.Contains(HakutaiDb.Houses.SelectMany(h=>HakutaiDb.Members(h.Id)),p=>p.HouseBasis.Contains("推定"));
    }

    [Fact]public void GeographyIsPublicButOwnershipIsTheYearSituation()
    {
        Assert.Equal(HakutaiDbKnowledge.PublicGeography,HakutaiDb.KnowledgeOf(nameof(HakutaiDbCastle.LatitudeE7)));
        Assert.Equal(HakutaiDbKnowledge.YearSituation,HakutaiDb.KnowledgeOf(nameof(HakutaiDbCastle.Owner)));
        Assert.Equal(HakutaiDbKnowledge.YearSituation,HakutaiDb.KnowledgeOf(nameof(HakutaiDbCastle.Holder)));
        Assert.DoesNotContain(typeof(HakutaiDbPlace).GetProperties(),p=>p.Name is "Owner" or "Holder");
        var kiyosu=HakutaiDb.Situation(HakutaiDb.ForGame("person:nobunaga")!.Castle!)!;
        Assert.Equal(1560,kiyosu.Year);Assert.Equal("G-000131",kiyosu.Owner);
    }

    [Fact]public void BrokenEditionsAreRejected()
    {
        var ok=HakutaiDb.Parse(Raw());HakutaiDb.Validate(ok);
        Assert.Throws<InvalidDataException>(()=>HakutaiDb.Validate(Broken(o=>o["Source"]!["Commit"]="main")));
        Assert.Throws<InvalidDataException>(()=>HakutaiDb.Validate(Broken(o=>o["People"]![0]!["Superior"]="P-999999")));
        Assert.Throws<InvalidDataException>(()=>HakutaiDb.Validate(Broken(o=>o["People"]![0]!["Death"]=1550)));
        Assert.Throws<InvalidDataException>(()=>HakutaiDb.Validate(Broken(o=>o["Castles"]![0]!["LatitudeE7"]=100)));
        Assert.Throws<InvalidDataException>(()=>HakutaiDb.Validate(Broken(o=>o["Houses"]![0]!["Head"]="P-005235")));
        Assert.ThrowsAny<JsonException>(()=>Broken(o=>o["People"]![0]!["Ability"]=50));
    }

    [Fact]public void EveryDbPersonHasBakedPeaceAndWarLooks()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir!=null&&!File.Exists(Path.Combine(dir.FullName,"Tools","DotOfficerBaker","looks.json")))dir=dir.Parent;
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir!.FullName,"Tools","DotOfficerBaker","looks.json")));
        var armors=doc.RootElement.GetProperty("armors").EnumerateArray().Select(a=>a.GetString()!).ToArray();var keys=new HashSet<string>();
        foreach(var l in doc.RootElement.GetProperty("looks").EnumerateArray()){string id=l.GetProperty("id").GetString()!;if(id.StartsWith("generic/"))foreach(var a in armors)keys.Add($"{id}/war/{a}");else keys.Add(id+"/war");if(l.TryGetProperty("peace",out _))keys.Add(id+"/peace");}
        var tiers=new Dictionary<DotTier,int>();
        foreach(var h in HakutaiDb.Houses)foreach(var p in HakutaiDb.Members(h.Id))foreach(bool war in new[]{false,true})
        {
            var look=DotOfficerLooks.For(p,war);Assert.True(keys.Contains(look.Key),p.Id+" → "+look.Key);
            if(look.Tier is {} t&&war)tiers[t]=tiers.GetValueOrDefault(t)+1;
        }
        Assert.Equal("famous/nobunaga/war",DotOfficerLooks.For(HakutaiDb.ForGame("person:nobunaga")!,true).Key);
        Assert.Equal("famous/ieyasu/peace",DotOfficerLooks.For(HakutaiDb.Named("徳川家康").Single(),false).Key);
        Assert.Equal("generic/monk",DotOfficerLooks.For(HakutaiDb.Person(HakutaiDb.House(HakutaiDb.GameLinks["org:ishiyama-honganji"])!.Head!)!,false).Look[..12]);
        Assert.Equal(DotTier.Courtier,DotOfficerLooks.For(HakutaiDb.ForGame("person:yamashina-tokitsugu")!,false).Tier);
        Assert.True(tiers[DotTier.Lord]>200&&tiers[DotTier.Samurai]>3000,string.Join(",",tiers));
        Assert.Equal("黒漆",DotOfficerLooks.For(HakutaiDb.Named("柴田勝家").Single(),true).Armor);
    }

    [Fact]public void PeopleAppearUnderTheNameTheyUsedIn1560()
    {
        var ieyasu=HakutaiDb.Named("徳川家康").Single();
        Assert.Equal("松平元康",ieyasu.StartName);Assert.Equal("松平元康",HakutaiDb.NameAt(ieyasu,1560));
        Assert.Contains(HakutaiDb.LaterRenames(ieyasu),n=>n.Name=="徳川家康"&&n.From>1560);
        Assert.Equal("長尾景虎",HakutaiDb.Named("上杉謙信").Single().StartName);
        Assert.Equal("木下秀吉",HakutaiDb.Named("豊臣秀吉").Single().StartName);
        Assert.Equal("織田信長",HakutaiDb.ForGame("person:nobunaga")!.StartName);
        // 別人の名前で登場させない
        var starts=HakutaiDb.Houses.SelectMany(h=>HakutaiDb.Members(h.Id)).Where(p=>p.StartName!=p.Name).ToArray();
        Assert.All(starts,p=>Assert.DoesNotContain(HakutaiDb.Named(p.StartName),q=>q.Id!=p.Id));
    }

    [Fact]public void DistrictsCarryOwnerAndEstimatedKokudakaAndUnknownCastlesBelongToTheDistrictRuler()
    {
        Assert.True(HakutaiDb.Districts.Count>600);Assert.True(HakutaiDb.Districts.Count(d=>d.Owner!=null)>580);
        var oda=HakutaiDb.House(HakutaiDb.GameLinks["org:oda"])!;Assert.InRange(oda.Kokudaka,400000,800000);
        Assert.All(HakutaiDb.Districts.Where(d=>d.Kokudaka>0),d=>Assert.Contains("推定",d.KokudakaBasis));
        Assert.Contains(HakutaiDb.PublicGeography(),c=>HakutaiDb.Castle(c.Id)!.OwnerBasis=="郡の支配者の城（城主不明）");
    }

    [Fact]public void RosterFollowsIncomeKeepsFamousPeopleAndFillsWithMobs()
    {
        var oda=HakutaiDbStart.Roster(HakutaiDb.GameLinks["org:oda"]);var house=HakutaiDb.House(oda.House)!;
        Assert.Equal(HakutaiDbStart.Quota(house),oda.Officers.Length);Assert.True(oda.DbMembers>oda.Quota);Assert.Equal(0,oda.Mobs);
        Assert.Equal("織田信長",oda.Officers[0].Name);
        foreach(var name in new[]{"柴田勝家","丹羽長秀","佐久間信盛"})Assert.Contains(oda.Officers,o=>o.Name==name);
        Assert.Equal(house.Kokudaka*250/10000,oda.SoldierCap);
        var small=HakutaiDb.Houses.Where(h=>!h.Religious&&HakutaiDbStart.Quota(h)>HakutaiDb.Members(h.Id).Count).OrderBy(h=>h.Id,StringComparer.Ordinal).First();
        var r=HakutaiDbStart.Roster(small.Id);Assert.True(r.Mobs>0);Assert.Equal(r.Quota,r.Officers.Length);
        Assert.All(r.Officers.Where(o=>o.Person==null),o=>{Assert.Equal(Historicity.Fictional,o.Historicity);Assert.StartsWith("mob:",o.Id);});
        var mobNames=r.Officers.Where(o=>o.Person==null).Select(o=>o.Name).ToArray();Assert.Equal(mobNames.Length,mobNames.Distinct().Count());Assert.DoesNotContain(r.Officers.Where(o=>o.Person!=null),o=>mobNames.Contains(o.Name));
        Assert.Equal(r,HakutaiDbStart.Roster(small.Id) with{Officers=r.Officers});Assert.Equal(r.Officers,HakutaiDbStart.Roster(small.Id).Officers);
        Assert.Equal("〇",HakutaiDbStart.Mark(HakutaiDbStart.Of(HakutaiDb.ForGame("person:nobunaga")!)));
        Assert.Equal("×",HakutaiDbStart.Mark(Historicity.Fictional));
        var all=HakutaiDb.Houses.Select(h=>HakutaiDbStart.Roster(h.Id)).ToArray();
        Assert.All(all,x=>Assert.True(x.Officers.Length>=Math.Min(3,x.Quota)));
    }
}
