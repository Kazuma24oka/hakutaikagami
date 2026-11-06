namespace HakutaiKagami.Core.Contracts;

public static class RankLeave
{
    internal static bool Handles(CourtAction action) => action is CourtAction.AskRankLeave or CourtAction.GrantRankLeave or CourtAction.RefuseRankLeave;
    internal static bool Eligible(ContractWorld w,EntityId applicant,EntityId house,long at,long revision)
    {
        var lord=HouseSuccession.Head(w,house,at,revision-1);
        return lord==applicant || (DefectionPacts.HouseAt(w,applicant,at,revision-1)==house &&
            HouseholdCourt.Events(w).Any(e=>e.At<=at && e.Revision<revision && e.Actor==lord &&
                e.Input.Action==CourtAction.GrantRankLeave && e.Input.Person==applicant && e.Input.Owner==house));
    }
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId site,long at,long rev)
    {
        bool asking=i.Action==CourtAction.AskRankLeave;
        var lord=asking?i.Person:actor;
        var applicant=asking?actor:i.Person;
        if(w.NewGame?.HistoricalCatalog!=CentralCourtCatalog.ShogunateEdition || i.Site!=site || lord==applicant ||
            HouseSuccession.Head(w,i.Owner,at,rev-1)!=lord || DefectionPacts.HouseAt(w,applicant,at,rev-1)!=i.Owner ||
            new[]{lord,applicant}.Any(p=>Assassination.DeadAt(w,p,at,rev) || !TransportWork.At(w,p,site,at,rev,true)))
            throw new InvalidDataException("家臣と現在の当主が同じ場所で相談してください");
        var earlier=HouseholdCourt.Events(w).Where(e=>e.At<=at && e.Revision<rev).ToArray();
        var requests=earlier.Where(e=>e.Input.Action==CourtAction.AskRankLeave && e.Actor==applicant &&
            e.Input.Owner==i.Owner && e.Input.Person==lord && !earlier.Any(r=>Handles(r.Input.Action) && r.Input.Reference==e.Input.Id));
        if(asking ? i.Reference!=null || requests.Any() || Eligible(w,applicant,i.Owner,at,rev)
                  : !requests.Any(e=>e.Input.Id==i.Reference && e.At<at))
            throw new InvalidDataException("未回答の相談一件に当主が一度だけ答えます");
    }
    public static IEnumerable<CourtEvent> Requests(ContractWorld w,EntityId lord) =>
        HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.AskRankLeave && e.Input.Person==lord &&
            e.At<w.ElapsedSeconds && !HouseholdCourt.Events(w).Any(r=>Handles(r.Input.Action) && r.Input.Reference==e.Input.Id));
    internal static IEnumerable<(CourtInput,EntityId)> Pending(ContractWorld w)
    {
        foreach(var lord in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.AskRankLeave).Select(e=>e.Input.Person).Distinct()){
            if(lord==w.PlayerControl)continue;
            foreach(var e in Requests(w,lord)){
                var action=SocialWorld.ConsentBonus(w,lord,e.Actor,w.ElapsedSeconds,w.CommitSequence)>=0?CourtAction.GrantRankLeave:CourtAction.RefuseRankLeave;
                yield return (new(HouseholdCourt.Id("rank-leave",e.Input.Id.Value),action,e.Actor,e.Input.Owner,e.Place,Reference:e.Input.Id),lord);
            }
        }
    }
}