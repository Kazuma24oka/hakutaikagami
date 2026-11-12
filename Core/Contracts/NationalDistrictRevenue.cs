namespace HakutaiKagami.Core.Contracts;

// 石高は出典付きの持分、収入率はゲーム用試作。年10石あたり1文を月境界で計上。
public static class NationalDistrictRevenue
{
    public const string Policy="national-district-cash-v1";
    public const int AnnualKokuPerMon=10;
    public static bool Enabled(ContractWorld w)=>w.NewGame?.HistoricalCatalog==NationalStart.RevenueEdition;
    public static long InitialCash(ContractWorld w,EntityId site)=>Enabled(w)?0:RegionalDomains.HasHouse(w,site)?500:0;
    sealed record Collection(EntityId Site,string Faction,long Koku);
    sealed record Index(Collection[] Collections,Dictionary<EntityId,Collection> BySite);
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NewGameState,Index> indexes=new();
    static Index For(ContractWorld w)=>indexes.GetValue(w.NewGame!,data=>
    {
        var people=data.Start.People.ToDictionary(p=>p.Id,StringComparer.Ordinal);
        var castles=data.Start.Castles.Where(c=>c.Faction!=null).GroupBy(c=>c.Faction!).ToDictionary(g=>g.Key,g=>g.OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray(),StringComparer.Ordinal);
        var koku=w.DistrictControl!.Districts.SelectMany(d=>d.Shares).GroupBy(s=>s.House).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.Kokudaka),StringComparer.Ordinal);
        var rows=new List<Collection>();
        foreach(var f in data.Start.Factions)
        {
            if(!castles.TryGetValue(f.Id,out var owned))continue;
            var site=owned.FirstOrDefault(c=>c.Id==people[f.Head].Site)??owned[0];
            rows.Add(new(new(site.Id),f.Id,NationalStart.DbHouse(f.Id) is {} db?koku.GetValueOrDefault(db):0));
        }
        var sorted=rows.OrderBy(r=>r.Site.Value,StringComparer.Ordinal).ToArray();
        return new(sorted,sorted.ToDictionary(r=>r.Site));
    });
    public static EntityId[] Sites(ContractWorld w)=>For(w).Collections.Select(r=>r.Site).ToArray();
    public static long Income(ContractWorld w,EntityId site,long at,long revision)
    {
        if(!Enabled(w)||at<0)return 0;
        if(!For(w).BySite.TryGetValue(site,out var collection))return 0;
        string faction=collection.Faction;long koku=collection.Koku;
        var epoch=DateOnly.Parse(w.EpochDate);var end=epoch.AddDays(checked((int)(at/86400)));
        var due=new DateOnly(epoch.Year,epoch.Month,1).AddMonths(1);long result=0;int period=1;
        for(;due<=end;due=due.AddMonths(1),period++)
        {
            long boundary=checked((long)(due.DayNumber-epoch.DayNumber)*86400);
            if(NationalStrategy.Owner(w,site,boundary,revision)?.Value!=faction)continue;
            result=checked(result+checked(koku*period)/120-checked(koku*(period-1))/120);
        }
        return result;
    }
}
