namespace HakutaiKagami.Core.Contracts;

// 石高は出典付きの持分、収入率はゲーム用試作。年10石あたり1文を月境界で計上。
public static class NationalDistrictRevenue
{
    public const string Policy="national-district-cash-v1";
    public const int AnnualKokuPerMon=10;
    public static bool Enabled(ContractWorld w)=>w.NewGame?.HistoricalCatalog==NationalStart.RevenueEdition;
    public static long InitialCash(ContractWorld w,EntityId site)=>Enabled(w)?0:RegionalDomains.HasHouse(w,site)?500:0;
    public static EntityId[] Sites(ContractWorld w)=>w.NewGame!.Start.Factions
        .Select(f=>w.NewGame.Start.Castles.Where(c=>c.Faction==f.Id).OrderBy(c=>c.Id,StringComparer.Ordinal)
            .OrderByDescending(c=>c.Id==w.NewGame.Start.People.Single(p=>p.Id==f.Head).Site).FirstOrDefault())
        .Where(c=>c!=null).Select(c=>new EntityId(c!.Id)).Distinct().OrderBy(s=>s.Value,StringComparer.Ordinal).ToArray();
    public static long Income(ContractWorld w,EntityId site,long at,long revision)
    {
        if(!Enabled(w)||at<0)return 0;
        var castle=w.NewGame!.Start.Castles.SingleOrDefault(c=>c.Id==site.Value);
        if(castle?.Faction is not {} faction||!Sites(w).Contains(site))return 0;
        var dbHouse=NationalStart.DbHouse(faction);if(dbHouse==null)return 0;
        long koku=w.DistrictControl!.Districts.SelectMany(d=>d.Shares).Where(s=>s.House==dbHouse).Sum(s=>s.Kokudaka);
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
