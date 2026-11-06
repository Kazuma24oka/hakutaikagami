namespace HakutaiKagami.Core.Contracts;

public static class RegentKin
{
    internal static bool Handles(CourtAction a)=>a is CourtAction.RequestRegentKin or CourtAction.AcceptRegentKin or CourtAction.DeclineRegentKin;
    public static bool Qualified(ContractWorld w,EntityId person,long at,long rev)=>HouseholdCourt.Events(w)
        .Any(e=>e.Input.Action==CourtAction.AcceptRegentKin && e.Actor==CentralCourtCatalog.Konoe && e.Input.Person==person && e.At<=at && e.Revision<=rev);
    static bool WillAccept(ContractWorld w,EntityId person,long at,long rev)=>SocialWorld.ConsentBonus(w,CentralCourtCatalog.Konoe,person,at,rev)>=2;
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {
        bool asking=i.Action==CourtAction.RequestRegentKin;
        var applicant=asking?actor:i.Person;var patron=asking?i.Person:actor;
        if(w.NewGame?.HistoricalCatalog!=CentralCourtCatalog.RegentEdition || patron!=CentralCourtCatalog.Konoe || applicant==patron ||
            place!=CentralCourtCatalog.Kyoto || i.Site!=place || DefectionPacts.HouseAt(w,applicant,at,rev-1)!=i.Owner ||
            new[]{applicant,patron}.Any(p=>Assassination.DeadAt(w,p,at,rev) || !TransportWork.At(w,p,place,at,rev,true)) ||
            Qualified(w,applicant,at,rev-1))throw new InvalidDataException("京で近衛家当主と本人が猶子の縁を相談してください");
        var rows=HouseholdCourt.Events(w).Where(e=>Handles(e.Input.Action) && e.At<=at && e.Revision<rev).ToArray();
        var pending=rows.Where(e=>e.Input.Action==CourtAction.RequestRegentKin && e.Actor==applicant && e.Input.Owner==i.Owner &&
            !rows.Any(r=>r.Input.Reference==e.Input.Id));
        if(asking ? i.Reference!=null || pending.Any() || !SocialWorld.Ledger(w).Any(e=>e.Actor==applicant && e.Input.Action==SocialWorldAction.Visit && e.Input.Person==patron && e.Revision<rev)
                  : !pending.Any(e=>e.Input.Id==i.Reference && e.At<at) || actor!=w.PlayerControl && (i.Action==CourtAction.AcceptRegentKin)!=WillAccept(w,applicant,at,rev))
            throw new InvalidDataException("面識と未回答の依頼を確認し、近衛家当主が諾否を決めます");
    }
    public static IEnumerable<CourtInput> Answers(ContractWorld w){
        var rows=HouseholdCourt.Events(w);
        foreach(var e in rows.Where(e=>e.Input.Action==CourtAction.RequestRegentKin && e.At<w.ElapsedSeconds && !rows.Any(r=>Handles(r.Input.Action)&&r.Input.Reference==e.Input.Id))){
            foreach(var action in new[]{CourtAction.AcceptRegentKin,CourtAction.DeclineRegentKin}){
                if(w.PlayerControl!=CentralCourtCatalog.Konoe && (action==CourtAction.AcceptRegentKin)!=WillAccept(w,e.Actor,w.ElapsedSeconds,w.CommitSequence))continue;
                yield return new(HouseholdCourt.Id(action.ToString(),e.Input.Id.Value),action,e.Actor,e.Input.Owner,e.Place,Reference:e.Input.Id);
            }
        }
    }
}