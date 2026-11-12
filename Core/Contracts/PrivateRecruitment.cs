namespace HakutaiKagami.Core.Contracts;

// 募兵時に本人の米を日数分だけ渡す。村を離れる期間だけ労働と収穫から差し引く。
public static partial class PrivateRecruitment
{
    public const int MarketCompanySize = 20;
    public const int DailyWage = 4;
    internal static bool Contract(CourtAction action) =>
        action is CourtAction.RaiseVillageSoldiers or CourtAction.HireMarketSoldiers;

    static IEnumerable<CourtEvent> Contracts(ContractWorld w) =>
        HouseholdCourt.Events(w).Where(e => Contract(e.Input.Action));

    internal static long FreeHires(ContractWorld w, EntityId site, long at, long rev) =>
        MarketCompanySize - Contracts(w).Where(e => e.Input.Action == CourtAction.HireMarketSoldiers &&
            e.Place == site && e.Revision < rev && e.At <= at && at < PaidUntil(w,e,at,rev)).Sum(e => e.Input.Amount-Released(w,e,at,rev));

    internal static long Wages(CourtEvent e) => checked(DailyWage * e.Input.Amount * ((e.Input.When-e.At)/86400));

    internal static IEnumerable<CourtEvent> Levies(ContractWorld w) =>
        HouseholdCourt.Events(w).Where(e => e.Input.Action == CourtAction.RaiseVillageSoldiers);

    // 従軍中の本人の兵。徴募元は労働台帳に残し、参集地の人数は指揮者の実所在で数える。
    internal static EntityId CompanyId(CourtEvent recruitment) =>
        new("unit:private-company/" + ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(recruitment.Input.Id.Value)));

    static IEnumerable<CourtEvent> ActiveCompanies(ContractWorld w, EntityId commander, long at, long rev) =>
        Contracts(w).Where(e => e.Actor == commander && e.Revision < rev && e.At <= at && at < PaidUntil(w,e,at,rev));

    static int Available(ContractWorld w, CourtEvent recruitment, long at, long rev)
    {using var _ccRev=ControlContext.At(rev);
        var unit = CompanyId(recruitment);
        var losses = (w.WorldClashes?.Steps ?? []).Where(s => s.At <= at && s.Revision < rev)
            .SelectMany(s => s.Result.Losses).Where(l => l.Unit == unit).Sum(l => l.Soldiers);
        return checked((int)recruitment.Input.Amount - Released(w,recruitment,at,rev) - losses + BattlefieldRules.Recovered(w, unit, at, rev));
    }

    internal static int Strength(ContractWorld w, EntityId commander, long at, long rev) =>
        ActiveCompanies(w, commander, at, rev).Sum(e => Available(w, e, at, rev));

    internal static IEnumerable<EngagementUnit> Accompany(ContractWorld w, EntityId captain, EngagementUnit wing)
    {
        long at = w.ElapsedSeconds, rev = w.CommitSequence + 1;
        foreach (var company in ActiveCompanies(w, captain, at, rev))
        {
            var id = CompanyId(company);
            int morale = Math.Max(0, 100 - WorldClashes.MoraleLost(w, id, at));
            var state = new UnitState(id, new("army:" + id.Value.Substring(5)), captain,
                wing.Unit.Organization, Available(w, company, at, rev), 0);
            yield return new(state, wing.Position, wing.Height, morale,
                morale <= 25 ? "routed" : wing.Phase, EngagementIntent.SelfDefense, wing.WillFight);
        }
    }

    public static int Soldiers(ContractWorld w, EntityId commander, EntityId site, long at, long rev) =>
        PersonalCompanions.SiteAt(w,commander,at,rev) == site ? Strength(w,commander,at,rev) : 0;

    public sealed record CompanyReport(EntityId Unit,int Fit,int Dead,int Wounded,int Deserted,bool VillageLevy);

    public static IEnumerable<CompanyReport> CompanyReports(ContractWorld w, EntityId captain, long at, long rev)
    {using var _ccRev=ControlContext.At(rev);
        foreach(var company in ActiveCompanies(w,captain,at,rev)){
            var id=CompanyId(company);
            var losses=BattlefieldRules.Casualties(w,id,at,rev);
            yield return new(id,Available(w,company,at,rev),losses.Dead,losses.Wounded,losses.Deserted,
                company.Input.Action==CourtAction.RaiseVillageSoldiers);
        }
    }

    internal static int ServingLevies(ContractWorld w, EntityId site,long at,long rev) =>
        Levies(w).Where(e=>e.Place==site && e.At<=at && at<PaidUntil(w,e,at,rev) && e.Revision<rev)
            .Sum(e=>Available(w,e,at,rev));

    internal static bool PrivateUnit(EntityId unit) => unit.Value.StartsWith("unit:private-company/",StringComparison.Ordinal);

    internal static EntityId? VillageOf(ContractWorld w, EntityId unit) =>
        PrivateUnit(unit) ? Levies(w).FirstOrDefault(e => CompanyId(e) == unit)?.Place : null;

    static IEnumerable<(CourtEvent Levy, BattlefieldCasualties Loss, long At)> VillageLosses(
        ContractWorld w, EntityId site, long at, long rev)
    {using var _ccRev=ControlContext.At(rev);
        if (w.WorldClashes is not { Steps.Length: >0 } clashes) yield break;
        var levies=Levies(w).Where(e=>e.Place==site && e.At<=at && e.Revision<rev).ToDictionary(CompanyId);
        if (levies.Count==0) yield break;
        foreach (var step in clashes.Steps.Where(s=>s.At<=at && s.Revision<rev))
            foreach (var loss in step.Result.Casualties??[])
                if (levies.TryGetValue(loss.Unit,out var levy)) yield return (levy,loss,step.At);
    }

    internal static int VillageDeaths(ContractWorld w, EntityId site, long at, long rev) =>
        VillageLosses(w,site,at,rev).Sum(row=>row.Loss.Dead);

    static IEnumerable<(long At,int Change)> LaborChanges(ContractWorld w, EntityId site,long until,long rev)
    {using var _ccRev=ControlContext.At(rev);
        foreach(var levy in Levies(w).Where(e=>e.Place==site && e.At<=until && e.Revision<rev)){
            int count=checked((int)levy.Input.Amount);
            yield return (levy.At,count);
            foreach(var payment in Payments(w,levy,until,rev)) yield return (payment.At,-checked((int)payment.Input.Amount));
            yield return (PaidUntil(w,levy,until,rev),-count+Released(w,levy,until,rev));
        }
        foreach(var (levy,loss,_) in VillageLosses(w,site,until,rev)){
            // 契約中は全員を動員済みとしている。満了時には実際に働ける人数だけを戻す。
            yield return (PaidUntil(w,levy,until,rev),loss.Dead+loss.Wounded+loss.Deserted);
            if(loss.ReturnAt is {} arrival) yield return (arrival,-loss.Deserted);
        }
    }

    internal static int Labor(ContractWorld w, EntityId site, long at, long rev) =>
        LaborChanges(w,site,at,rev).Where(e=>e.At<=at).Sum(e=>e.Change);

    internal static long LaborSeconds(ContractWorld w, EntityId site, long from, long until, long rev) =>
        LaborChanges(w,site,until,rev).Where(e=>e.At<until).Sum(e=>checked(e.Change*(until-Math.Max(from,e.At))));

    public static bool CanLevy(ContractWorld w, EntityId actor, EntityId owner, EntityId place, long at, long rev)
    {using var _ccRev=ControlContext.At(rev);
        if (DefectionPacts.HouseAt(w, actor, at, rev-1) != owner) return false;
        return HouseSuccession.Head(w, owner, at, rev-1) == actor ||
            w.Offices.Any(o => o.Kind == "castellan" && o.Subject == actor && o.Target == place &&
                o.From <= at && at < o.Until && (o.From < at || o.Revision < rev)) ||
            HouseReforms.LevyGrants(w).Any(g => g.Input.Person == actor && g.Input.Owner == owner &&
                g.Place == place && g.Revision < rev && g.At <= at && HouseReforms.LevyEnd(w, g.Input.Id) >= rev);
    }

    internal static long Food(ContractWorld w, EntityId person, long at, long rev) =>
        Contracts(w).Where(e => e.Actor == person && e.Revision <= rev && e.At <= at)
            .Sum(e => checked(e.Input.Amount * ((e.Input.When - e.At) / 86400))) + RenewalCost(w,person,at,rev,false);

    internal static void Check(ContractWorld w, CourtInput input, EntityId actor, EntityId place, long at, long rev)
    {using var _ccRev=ControlContext.At(rev);
        if (!MilitaryEconomy.Enabled(w) || !CommunityEconomy.SitesFor(w).Contains(place) || input.Person != actor ||
            input.Site != place || !TransportWork.At(w, actor, place, at, rev, true) ||
            input.Action == CourtAction.RaiseVillageSoldiers && !CanLevy(w, actor, input.Owner, place, at, rev) ||
            !w.LandRights.Any(r => r.Kind == "control" && r.Subject == input.Owner && r.Target == place &&
                r.From <= at && at < r.Until && (r.From < at || r.Revision < rev)))
            throw new InvalidDataException("所属する家が支配する現地で募兵してください");
        long duration = checked(input.When - at);
        if (input.Amount is < 1 or > 100 || duration is < 86400 or > 2592000 || duration % 86400 != 0)
            throw new InvalidDataException("人数と1〜30日間の従軍期間を指定してください");
        if (input.Action == CourtAction.HireMarketSoldiers)
        {
            long wages = checked(input.Amount * DailyWage * (duration / 86400));
            if (!MaterialMarkets.Enabled(w) || !MaterialMarkets.Sites(w).Contains(place) ||
                FreeHires(w,place,at,rev) < input.Amount || TransportWork.WalletBefore(w, actor, at, rev) < wages)
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
