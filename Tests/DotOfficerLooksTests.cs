using System.Text.Json;
using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class DotOfficerLooksTests
{
    // 書き出しの一覧（Tools/DotOfficerBaker/looks.json と bake.py の決まり）から、作られる絵の名前を作る。
    static (HashSet<string> Keys,string[] Armors) Baked()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir!=null&&!File.Exists(Path.Combine(dir.FullName,"Tools","DotOfficerBaker","looks.json")))dir=dir.Parent;
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir!.FullName,"Tools","DotOfficerBaker","looks.json")));
        var armors=doc.RootElement.GetProperty("armors").EnumerateArray().Select(a=>a.GetString()!).ToArray();var keys=new HashSet<string>();
        foreach(var l in doc.RootElement.GetProperty("looks").EnumerateArray())
        {
            string id=l.GetProperty("id").GetString()!;
            if(id.StartsWith("generic/")||id.StartsWith("soldier/ashigaru"))foreach(var a in armors)keys.Add($"{id}/war/{a}");else keys.Add(id+"/war");
            if(l.TryGetProperty("peace",out _))keys.Add(id+"/peace");
        }
        return (keys,armors);
    }
    static ContractWorld World(string edition)=>NpcContinuingTests.Start() is var w&&edition==w.NewGame!.HistoricalCatalog?w:
        NewGameWorld.Create(NewGameWorld.Scenario(edition),[],"person:nobunaga",7,historicalCatalog:edition);

    [Fact]public void ArmorColorsMatchTheBakedLibrary()=>Assert.Equal(Baked().Armors,DotOfficerLooks.Armors);

    [Theory][InlineData(HistoricalStartCatalog.ImagawaEdition)][InlineData(CentralCourtCatalog.RegentEdition)][InlineData(HistoricalHouseholdCatalog.Id)]
    public void EveryPersonHasBakedPeaceAndWarLooks(string edition)
    {
        var keys=Baked().Keys;var w=World(edition);
        foreach(var p in FamilyBirth.People(w))foreach(bool war in new[]{false,true})
        {
            var look=DotOfficerLooks.For(w,new EntityId(p.Id),war);
            Assert.True(keys.Contains(look.Key),p.Id+" → "+look.Key);Assert.Equal(war,look.Key.Contains("/war"));
        }
        foreach(var house in w.NewGame!.Start.Factions.Select(f=>(string?)f.Id).Append(null))foreach(var weapon in new[]{"yari","teppo","yumi"})
            Assert.Contains(DotOfficerLooks.Soldier(house,weapon),keys);
        Assert.Contains(DotOfficerLooks.WarriorMonk,keys);
    }

    [Fact]public void FamousHousesCourtiersMonksRetainersAndRoninAreDistinguished()
    {
        var w=World(HistoricalStartCatalog.ImagawaEdition);string Key(string id,bool war)=>DotOfficerLooks.For(w,new EntityId(id),war).Key;
        Assert.Equal("famous/nobunaga/war",Key("person:nobunaga",true));Assert.Equal("famous/shingen/peace",Key("person:shingen",false));
        Assert.Equal(DotTier.Courtier,DotOfficerLooks.For(w,new("person:konoe-sakihisa"),false).Tier);
        Assert.Equal(DotTier.Monk,DotOfficerLooks.For(w,new("person:temple-representative/site/sakai"),true).Tier);
        Assert.Equal(DotTier.Samurai,DotOfficerLooks.For(w,new("person:takeda-nobushige"),true).Tier);
        Assert.Equal(DotTier.Lord,DotOfficerLooks.For(w,new("person:imagawa-yoshimoto"),true).Tier);
        Assert.Equal("紫",DotOfficerLooks.For(w,new("person:imagawa-ujizane"),true).Armor);
        Assert.Equal(DotTier.Ronin,DotOfficerLooks.TierOf(new("created:x","浪人","site:kofu",null,null,true,false),false));
        // 有名武将専用の一式・前立ては、名前の一致でだけ使う。
        Assert.Equal("famous/ieyasu",DotOfficerLooks.For("created:y","松平元康",null,false,null,20,true).Look);
        Assert.StartsWith("generic/",DotOfficerLooks.For("created:z","松平某",null,false,null,20,true).Look);
    }

    [Fact]public void AgeChangesTheBandAndLooksNeverChangeTheWorld()
    {
        Assert.Equal("young",DotOfficerLooks.Band(18));Assert.Equal("prime",DotOfficerLooks.Band(null));Assert.Equal("old",DotOfficerLooks.Band(61));
        var w=World(HistoricalStartCatalog.ImagawaEdition);var before=ContractJson.Write(w);
        foreach(var p in FamilyBirth.People(w))DotOfficerLooks.For(w,new EntityId(p.Id),true);
        Assert.Equal(before,ContractJson.Write(w));
    }
}
