namespace HakutaiKagami.Core.Contracts;

public static partial class PrivateRecruitment
{
    const int RationDays=30;
    static IEnumerable<CourtEvent> Payments(ContractWorld w,CourtEvent company,long at,long rev) =>
        HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.PrivateCompanyPayment &&
            e.Input.Reference==company.Input.Id && e.At<=at && e.Revision<rev);

    static int Released(ContractWorld w,CourtEvent company,long at,long rev) =>
        checked((int)Payments(w,company,at,rev).Sum(e=>e.Input.Amount));

    static long PaidUntil(ContractWorld w,CourtEvent company,long at,long rev) =>
        Payments(w,company,at,rev).LastOrDefault()?.Input.When??company.Input.When;

    static IEnumerable<CourtEvent> Standing(ContractWorld w,long at,long rev) =>
        HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.KeepPrivateCompany && e.At<=at && e.Revision<rev);

    static CourtEvent Company(ContractWorld w,EntityId? id) =>
        Contracts(w).SingleOrDefault(e=>e.Input.Id==id)??throw new InvalidDataException("募兵原本がありません");

    public static IEnumerable<CourtEvent> Renewable(ContractWorld w,EntityId actor,EntityId site,long at,long rev)
    {
        if(Standing(w,at,rev).Where(e=>e.Actor==actor).Any(e=>PaidUntil(w,Company(w,e.Input.Reference),at,rev)>at))
            return [];
        return ActiveCompanies(w,actor,at,rev).Where(e=>e.Place==site && Available(w,e,at,rev)>0);
    }

    internal static void CheckStanding(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {
        if(i.Person!=actor || i.Site!=place || !TransportWork.At(w,actor,place,at,rev,true) ||
           !Renewable(w,actor,place,at,rev).Any(e=>e.Input.Id==i.Reference && e.Input.Owner==i.Owner))
            throw new InvalidDataException("現地で本人の有期募兵原本を一つ指定してください");
    }

    static long PaymentCost(ContractWorld w,CourtEvent entry,bool wages)
    {
        var company=Company(w,entry.Input.Reference);
        if(wages && company.Input.Action!=CourtAction.HireMarketSoldiers)return 0;
        long count=Available(w,company,entry.At,entry.Revision)-entry.Input.Amount;
        return checked(count*RationDays*(wages?DailyWage:1));
    }

    static long RenewalCost(ContractWorld w,EntityId person,long at,long rev,bool wages) =>
        HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.PrivateCompanyPayment && e.Actor==person &&
            e.At<=at && e.Revision<=rev).Sum(e=>PaymentCost(w,e,wages));

    internal static long RenewalWages(ContractWorld w,EntityId wallet,long at,long rev) =>
        HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.PrivateCompanyPayment &&
            NewGameWork.Wallet(e.Actor)==wallet && (e.At<at || e.At==at && e.Revision<rev)).Sum(e=>PaymentCost(w,e,true));

    static bool Ended(ContractWorld w,CourtEvent company,long at,long rev) =>
        Payments(w,company,at,rev).Any(e=>e.Input.When==e.At);

    internal static IEnumerable<long> DueDates(ContractWorld w)
    {
        foreach(var order in Standing(w,w.ElapsedSeconds,w.CommitSequence+1)){
            var company=Company(w,order.Input.Reference);
            if(!Ended(w,company,w.ElapsedSeconds,w.CommitSequence+1))
                yield return PaidUntil(w,company,w.ElapsedSeconds,w.CommitSequence+1);
        }
    }

    static CourtEvent Payment(ContractWorld w,CourtEvent company,long at,long rev)
    {
        int soldiers=Available(w,company,at,rev);
        long kept=Math.Min(soldiers,Math.Max(0,HouseSalary.PrivateRice(w,company.Actor,at,rev-1))/RationDays);
        if(company.Input.Action==CourtAction.HireMarketSoldiers)
            kept=Math.Min(kept,Math.Max(0,TransportWork.WalletBefore(w,company.Actor,at,rev))/(DailyWage*RationDays));
        if(Assassination.DeadAt(w,company.Actor,at,rev))kept=0;
        var id=HouseholdCourt.Id("company-payment",company.Input.Id.Value+"/"+at);
        return new(new(id,CourtAction.PrivateCompanyPayment,company.Actor,company.Input.Owner,company.Place,
            Amount:soldiers-kept,Reference:company.Input.Id,When:kept>0?checked(at+RationDays*86400L):at),
            company.Actor,company.Place,at,rev,false);
    }

    internal static void SettleDue(ContractWorld w)
    {
        foreach(var order in Standing(w,w.ElapsedSeconds,w.CommitSequence+1).ToArray()){
            var company=Company(w,order.Input.Reference);
            if(Ended(w,company,w.ElapsedSeconds,w.CommitSequence+1) ||
               PaidUntil(w,company,w.ElapsedSeconds,w.CommitSequence+1)!=w.ElapsedSeconds)continue;
            var entry=Payment(w,company,w.ElapsedSeconds,w.CommitSequence);
            w.Court=w.Court! with{Events=[..w.Court.Events,entry]};
        }
    }

    internal static void ValidatePayment(ContractWorld w,CourtEvent entry)
    {
        var company=Company(w,entry.Input.Reference);
        if(!Standing(w,entry.At,entry.Revision).Any(e=>e.Input.Reference==company.Input.Id) ||
           Ended(w,company,entry.At,entry.Revision) || PaidUntil(w,company,entry.At,entry.Revision)!=entry.At ||
           Payment(w,company,entry.At,entry.Revision)!=entry)
            throw new InvalidDataException("私兵の継続人数・手持ち米・賃金と精算原本が一致しません");
    }
}
