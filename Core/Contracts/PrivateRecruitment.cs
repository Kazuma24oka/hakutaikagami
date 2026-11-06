namespace HakutaiKagami.Core.Contracts;

// 募兵時に本人の米を日数分だけ渡す。村を離れる期間だけ労働と収穫から差し引く。
public static class PrivateRecruitment
{
    public const int MarketCompanySize = 20;
    public const int DailyWage = 4;
    internal static bool Contract(CourtAction action) =>
        action is CourtAction.RaiseVillageSoldiers or CourtAction.HireMarketSoldiers;

    static IEnumerable<CourtEvent> Contracts(ContractWorld w) =>
        HouseholdCourt.Events(w).Where(e => Contract(e.Input.Action));

    internal static long Wages(CourtEvent e) => checked(DailyWage * e.Input.Amount * ((e.Input.When-e.At)/86400));

    internal static IEnumerable<CourtEvent> Levies(ContractWorld w) =>
        HouseholdCourt.Events(w).Where(e => e.Input.Action == CourtAction.RaiseVillageSoldiers);

    public static int Soldiers(ContractWorld w, EntityId commander, EntityId site, long at, long rev) =>
        (int)Contracts(w).Where(e => e.Actor == commander && e.Place == site && e.Revision < rev &&
            e.At <= at && at < e.Input.When).Sum(e => e.Input.Amount);

    internal static int Labor(ContractWorld w, EntityId site, long at, long rev) =>
        (int)Levies(w).Where(e => e.Place == site && e.Revision < rev && e.At <= at && at < e.Input.When).Sum(e => e.Input.Amount);

    internal static long LaborSeconds(ContractWorld w, EntityId site, long from, long until, long rev) =>
        Levies(w).Where(e => e.Place == site && e.Revision < rev).Sum(e =>
            checked(e.Input.Amount * Math.Max(0, Math.Min(until, e.Input.When) - Math.Max(from, e.At))));

    internal static long Food(ContractWorld w, EntityId person, long at, long rev) =>
        Contracts(w).Where(e => e.Actor == person && e.Revision <= rev && e.At <= at)
            .Sum(e => checked(e.Input.Amount * ((e.Input.When - e.At) / 86400)));

    internal static void Check(ContractWorld w, CourtInput input, EntityId actor, EntityId place, long at, long rev)
    {
        if (!MilitaryEconomy.Enabled(w) || !CommunityEconomy.SitesFor(w).Contains(place) || input.Person != actor ||
            input.Site != place || !TransportWork.At(w, actor, place, at, rev, true) ||
            input.Action == CourtAction.RaiseVillageSoldiers && DefectionPacts.HouseAt(w, actor, at, rev-1) != input.Owner ||
            !w.LandRights.Any(r => r.Kind == "control" && r.Subject == input.Owner && r.Target == place &&
                r.From <= at && at < r.Until && (r.From < at || r.Revision < rev)))
            throw new InvalidDataException("所属する家が支配する現地で募兵してください");
        long duration = checked(input.When - at);
        if (input.Amount is < 1 or > 100 || duration is < 86400 or > 2592000 || duration % 86400 != 0)
            throw new InvalidDataException("人数と1〜30日間の従軍期間を指定してください");
        if (input.Action == CourtAction.HireMarketSoldiers)
        {
            long alreadyHired = Contracts(w).Where(e => e.Input.Action == CourtAction.HireMarketSoldiers &&
                e.Place == place && e.Revision < rev && e.At <= at && at < e.Input.When).Sum(e => e.Input.Amount);
            long wages = checked(input.Amount * DailyWage * (duration / 86400));
            if (!MaterialMarkets.Enabled(w) || !MaterialMarkets.Sites(w).Contains(place) ||
                MarketCompanySize-alreadyHired < input.Amount || TransportWork.WalletBefore(w, actor, at, rev) < wages)
                throw new InvalidDataException("現地の市で空いている雇い兵と、契約期間分の私財を確認してください");
        }
        else
        {
            var village = CommunityEconomy.AccountAt(w, place, at, rev);
            if (village.Workforce - village.Reserved < input.Amount)
                throw new InvalidDataException("村の働き手が足りません");
        }
        // 扶持の受領原本は版未満、家中の受渡し原本は版以下で照会する。
        long privateRice = HouseSalary.PrivateRice(w, actor, at, rev-1) +
            HouseSalary.Received(w, actor, at, rev) - HouseSalary.Received(w, actor, at, rev-1);
        if (privateRice < input.Amount * (duration / 86400))
            throw new InvalidDataException("村に残る働き手と、人数・日数分の本人の扶持米が必要です");
    }
}
