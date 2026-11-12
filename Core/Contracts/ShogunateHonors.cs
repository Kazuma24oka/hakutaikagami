namespace HakutaiKagami.Core.Contracts;

public static class ShogunateHonors
{
    public static string Name(CourtAction action)=>action switch{
        CourtAction.GrantCompanionHonor=>"御相伴衆",CourtAction.PermitLacquerPalanquin=>"塗輿の免許",
        _=>throw new ArgumentOutOfRangeException(nameof(action))};
    internal static bool Handles(CourtAction action)=>action is CourtAction.GrantCompanionHonor or CourtAction.PermitLacquerPalanquin;
    internal static void Check(ContractWorld w,CourtInput i,EntityId issuer,EntityId place,long at,long rev){using var _ccRev=ControlContext.At(rev);
        if(place!=CentralCourtCatalog.KyotoIn(w) || i.Site!=place || i.Reference!=null || SupremeOffice.ShogunAt(w,at,rev-1)!=issuer ||
            i.Person==issuer || HouseSuccession.Head(w,i.Owner,at,rev-1)!=i.Person ||
            !TransportWork.At(w,issuer,place,at,rev,true) || !TransportWork.At(w,i.Person,place,at,rev,true) ||
            Assassination.DeadAt(w,i.Person,at,rev) || HouseholdCourt.Events(w).Any(e=>e.At<=at && e.Revision<rev && e.Input.Action==i.Action &&
                e.Input.Owner==i.Owner && (i.Action==CourtAction.GrantCompanionHonor || i.Person==e.Input.Person)))
            throw new InvalidDataException("在職中の将軍が京で当主本人に、家格・待遇の栄典を一度だけ授けます");
    }
    public static IEnumerable<CourtInput> Available(ContractWorld w,EntityId issuer){
        if(SupremeOffice.ShogunAt(w,w.ElapsedSeconds,w.CommitSequence)!=issuer)yield break;
        foreach(var f in HouseSuccession.Factions(w))foreach(var action in new[]{CourtAction.GrantCompanionHonor,CourtAction.PermitLacquerPalanquin}){
            var input=new CourtInput(HouseholdCourt.Id(action.ToString(),f.Id+"/"+f.Head),action,new(f.Head),new(f.Id),CentralCourtCatalog.KyotoIn(w));
            try{Check(w,input,issuer,input.Site,w.ElapsedSeconds,w.CommitSequence+1);}catch(InvalidDataException){continue;}
            yield return input;
        }
    }
    public static IEnumerable<string> Witnessed(ContractWorld w,EntityId person)=>HouseholdCourt.Events(w)
        .Where(e=>Handles(e.Input.Action) && (e.Actor==person || e.Input.Person==person)).Select(e=>Name(e.Input.Action));
}
