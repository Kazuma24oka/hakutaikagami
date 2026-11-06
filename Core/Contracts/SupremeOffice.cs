namespace HakutaiKagami.Core.Contracts;

public static class SupremeOffice
{
    public static readonly EntityId Bakufu=new("org:central-bakufu");
    public static EntityId? ShogunAt(ContractWorld w,long at,long revision)
    {
        if(!CentralCourtCatalog.HasShogunate(w.NewGame?.HistoricalCatalog))return null;
        var incumbent=CentralCourtCatalog.Shogun;
        if(Assassination.DeadAt(w,incumbent,at,revision))return null;
        var resigned=HouseholdCourt.Events(w).Any(e=>e.Input.Action==CourtAction.ResignShogun &&
            e.Actor==incumbent && e.At<=at && e.Revision<=revision);
        return resigned?null:incumbent;
    }
    internal static void CheckResignation(ContractWorld w,CourtInput input,EntityId actor,EntityId place,long at,long revision)
    {
        if(input.Owner!=Bakufu || input.Site!=CentralCourtCatalog.Kyoto || place!=input.Site || input.Person!=actor ||
            input.Reference!=null || ShogunAt(w,at,revision-1)!=actor || !TransportWork.At(w,actor,place,at,revision,true))
            throw new InvalidDataException("在職中の将軍本人が京で辞任してください");
    }
}