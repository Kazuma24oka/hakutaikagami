namespace HakutaiKagami.Core.Contracts;

// S07（2026-11-17）：拠点のデータ。一覧（NationalSystems）に Territory2 のある新規の全国開始だけ。値はすべて推定（決め方を Basis に持つ）。
// ・人口：城の拠点の村の人口は、百代鏡DBの郡の人口の推定を、その郡の経済の拠点の数で分けた100分の1（50〜2000人）。働き手は半分、村の米と収穫の基準は人口に比例。
// ・寺社：百代鏡DBの寺社54か所を、位置の一番近い城の網の拠点に置く。その拠点の寺社の代表は実名・宗派の寺社になる。
// ・門徒：浄土真宗の寺社のある国の城の拠点は、村の人口の3割を門徒とみなす。
// ・開発の4軸・民心・治安・城の区画：郡の石高・町（地方の版の拠点）・職人の町・東国の牧・賊の巣・城の種類から決める。
// ・賊の巣：全国の開始では城の網の拠点にすべて持ち主がいるので、どの家の本拠でもない山城のうち、拠点の ID の固定のくじで16に1つ。
public sealed record SiteProfile(EntityId Site,int Population,int Agriculture,int Commerce,int Workshop,int Pasture,int Morale,int Security,int Monto,int Sections,string Basis);

public static class Territory2
{
    public const string Name=NationalSystems.Territory2;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name)&&NationalStrategy.Enabled(w);

    static readonly Lazy<Dictionary<string,string>> castleOfSite=new(()=>CastleRoutes.Catalog.Nodes.ToDictionary(n=>"site:"+n.Id,n=>n.Castle,StringComparer.Ordinal));
    static readonly Lazy<Dictionary<string,long>> districtPopulation=new(()=>HakutaiDbSupplement.Current.DistrictPopulation.ToDictionary(d=>d.Id,d=>d.Population,StringComparer.Ordinal));
    static string? DistrictOf(EntityId site)=>castleOfSite.Value.TryGetValue(site.Value,out var c)&&HakutaiDb.Castle(c) is {District:{} d,Province:{} p}?p+"::"+d:null;

    // 経済の拠点の人口（地方の版の拠点・一覧の無い世界は前のまま200）。
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NewGameState,Dictionary<string,int>> populations=new();
    public static int Population(ContractWorld w,EntityId site)
    {
        if(!On(w))return CommunityEconomy.Population;
        var map=populations.GetValue(w.NewGame!,_=>
        {
            var sites=CommunityEconomy.SitesFor(w).Select(s=>(Site:s,District:DistrictOf(s))).ToArray();
            var count=sites.Where(x=>x.District!=null).GroupBy(x=>x.District!).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal);
            return sites.Where(x=>x.District!=null&&districtPopulation.Value.ContainsKey(x.District!)).ToDictionary(x=>x.Site.Value,x=>(int)Math.Clamp(districtPopulation.Value[x.District!]/count[x.District!]/100,50,2000),StringComparer.Ordinal);
        });
        return map.TryGetValue(site.Value,out var p)?p:CommunityEconomy.Population;
    }
    public static int Workers(ContractWorld w,EntityId site)=>Population(w,site)/2;
    public static long InitialVillage(ContractWorld w,EntityId site)=>CommunityEconomy.InitialVillage*Population(w,site)/CommunityEconomy.Population;
    public static long BaseHarvest(ContractWorld w,EntityId site)=>CommunityEconomy.BaseHarvest*Population(w,site)/CommunityEconomy.Population;

    // 寺社：一番近い城の網の拠点。
    static readonly Lazy<Dictionary<string,HakutaiDbTemple>> templeAt=new(()=>
    {
        var nodes=CastleRoutes.Catalog.Nodes.ToArray();var map=new Dictionary<string,HakutaiDbTemple>(StringComparer.Ordinal);
        foreach(var t in HakutaiDbSupplement.Current.Temples.Where(t=>t.LatitudeE7!=null).OrderBy(t=>t.Id,StringComparer.Ordinal))
        {
            var near=nodes.MinBy(n=>{double dx=(n.LongitudeE7-t.LongitudeE7!.Value)*0.82,dy=n.LatitudeE7-t.LatitudeE7!.Value;return dx*dx+dy*dy;})!;
            map.TryAdd("site:"+near.Id,t);
        }
        return map;
    });
    // 一つの拠点に近い寺社が二つ以上あれば、代表は ID の順で先の寺社（ほかの寺社も TemplesAt で引ける）。
    public static HakutaiDbTemple? TempleAt(ContractWorld w,EntityId site)=>On(w)?templeAt.Value.GetValueOrDefault(site.Value):null;
    static readonly Lazy<ILookup<string,HakutaiDbTemple>> templesAt=new(()=>
    {
        var nodes=CastleRoutes.Catalog.Nodes.ToArray();
        return HakutaiDbSupplement.Current.Temples.Where(t=>t.LatitudeE7!=null).OrderBy(t=>t.Id,StringComparer.Ordinal)
            .ToLookup(t=>"site:"+nodes.MinBy(n=>{double dx=(n.LongitudeE7-t.LongitudeE7!.Value)*0.82,dy=n.LatitudeE7-t.LatitudeE7!.Value;return dx*dx+dy*dy;})!.Id,StringComparer.Ordinal);
    });
    public static HakutaiDbTemple[] TemplesAt(ContractWorld w,EntityId site)=>On(w)?templesAt.Value[site.Value].ToArray():[];
    public static int TempleCount=>templeAt.Value.Count;

    // 賊の巣。
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NewGameState,HashSet<string>> camps=new();
    public static bool IsCamp(ContractWorld w,EntityId site)=>BanditEncounter.Camps.Contains(site)||On(w)&&camps.GetValue(w.NewGame!,data=>
        data.Start.Castles.Where(c=>castleOfSite.Value.ContainsKey(c.Id)&&!data.Start.Factions.Any(f=>f.Home==c.Id)&&Warfare2.CastleType(new(c.Id))=="山城"&&ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes("bandit-camp/"+c.Id))[0] is '0').Select(c=>c.Id).ToHashSet(StringComparer.Ordinal)).Contains(site.Value);
    public static string[] CampSites(ContractWorld w)=>On(w)?w.NewGame!.Start.Castles.Select(c=>c.Id).Where(id=>IsCamp(w,new(id))).ToArray():[];

    static readonly HashSet<string> East=new(["甲斐","信濃","陸奥","出羽","上野","下野","武蔵","相模","常陸","下総","上総","安房"],StringComparer.Ordinal);
    public static SiteProfile? Profile(ContractWorld w,EntityId site)
    {
        if(!On(w))return null;
        int pop=Population(w,site);var province=CentralStanding.ProvinceOf(site);
        long koku=DistrictOf(site) is {} d&&HakutaiDb.District(d) is {} dist?dist.Kokudaka:0;
        int agriculture=(int)Math.Clamp(koku/1000,0,100);
        int commerce=MaterialMarkets.Sites(w).Contains(site)&&!castleOfSite.Value.ContainsKey(site.Value)?60:20;
        int workshop=site.Value is "site:sakai" or "site:kofu" or "site:kyoto"?40:10;
        int pasture=province!=null&&East.Contains(province)?30:10;
        bool camp=IsCamp(w,site);
        bool nearCamp=NationalStrategy.Neighbors(site.Value).Any(n=>IsCamp(w,new(n)));
        int security=camp?40:nearCamp?70:90;
        bool shin=province!=null&&HakutaiDbSupplement.Current.Temples.Any(t=>t.Religion=="浄土真宗"&&HakutaiDb.Kuni(t.Province)==province);
        int monto=shin?pop*3/10:0;
        int sections=Warfare2.CastleType(site) switch{"山城"=>4,"平山城"=>3,"平城"=>2,_=>0};
        return new(site,pop,agriculture,commerce,workshop,pasture,50,security,monto,sections,"郡の石高・人口の推定・城の種類・寺社・賊の巣からの推定");
    }
}
