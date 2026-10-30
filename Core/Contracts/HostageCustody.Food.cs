namespace HakutaiKagami.Core.Contracts;
public sealed record CustodyMeal(EntityId House,EntityId Site,EntityId Custodian,long RiceKg,string Outcome);
public sealed record CustodyMealCard(EntityId Original,long ObservedAt,long RiceKg,string Outcome);
public static partial class HostageCustody
{
    public const long MealInterval=86400;
    public static long MealQuantity(long houseRice,long reserved){if(houseRice<0||reserved<0)throw new ArgumentOutOfRangeException();return houseRice>reserved?1:0;}
    static bool MealDue(CustodyEntry[] rows,EntityId root,long at){var person=rows.Single(e=>e.Input.Id==root).Terms!.Person;return !rows.Any(e=>e.Meal?.RiceKg==1&&at-e.At<MealInterval&&rows.Single(p=>p.Input.Id==e.Input.Original).Terms!.Person==person);}
    static CustodyMeal PrepareMeal(ContractWorld w,CustodyTerms t,CustodyEntry[] prior,long at,long rev){
        Need(CommunityEconomy.Enabled(w)&&Held(prior,t.Id)&&LocalOriginal(w,t,at,rev)&&LocalAuthorities(w,Effective(t,prior),at,rev),"本人と確認済みの両家代表者が預かり先に在席する必要があります");
        Need(MealDue(prior,t.Id,at),"前回の食事提供から一日を空けてください");var current=Effective(t,prior);
        Need(RegionalDomains.Owner(w,t.Site)==t.CustodianHouse&&RegionalDomains.Authority(w,current.Custodian,t.Site,at,rev),"受入側の家の現地米を処分できません");
        var before=w.ForkTransportLocation();before.Custody=w.Custody! with{Entries=prior};long quantity=MealQuantity(CommunityEconomy.AccountAt(before,t.Site,at,rev).HouseRice,HouseSalary.Reserved(before,t.Site,at,rev));
        return new(t.CustodianHouse,t.Site,current.Custodian,quantity,quantity==1?"provided":"unfulfilled-free-stock-shortage");
    }
    internal static (long At,long Revision,long Amount)[] HouseTransfers(ContractWorld w,EntityId site,long at,long rev)=>Before(w,at,rev).Where(e=>e.Meal is {RiceKg:1}&&e.Meal.Site==site).Select(e=>(e.At,e.Revision,Amount:-e.Meal!.RiceKg)).ToArray();
    public static bool CanRequestMeal(ContractWorld w,EntityId original){if(!CommunityEconomy.Enabled(w))return false;var t=Entries(w).SingleOrDefault(e=>e.Input.Id==original)?.Terms;return t!=null&&Held(Entries(w),t.Id)&&LocalOriginal(w,t,w.ElapsedSeconds,w.CommitSequence+1)&&LocalAuthorities(w,Effective(t,Entries(w)),w.ElapsedSeconds,w.CommitSequence+1)&&MealDue(Entries(w),t.Id,w.ElapsedSeconds);}
    public static CustodyMealCard[] Meals(ContractWorld w)=>Entries(w).Where(e=>e.Meal!=null).Select(e=>new CustodyMealCard(e.Input.Original!.Value,e.At,e.Meal!.RiceKg,e.Meal.Outcome)).ToArray();
}
