namespace HakutaiKagami.Core.Contracts;

public static class CourtPedigree
{
    internal static bool Handles(CourtAction a)=>a is CourtAction.AskPedigree or CourtAction.AttestPedigree or CourtAction.ReportMissingPedigree or CourtAction.RecognizePedigree or CourtAction.FilePedigree;
    static bool FoundingLine(string id)=>id is "person:ashikaga-yoshiteru" or "person:shingen";
    static bool Documented(ContractWorld w,EntityId applicant,long at,long rev,bool adoption)
    {using var _ccRev=ControlContext.At(rev);
        var people=FamilyBirth.People(w).ToDictionary(p=>p.Id);
        var entries=w.Adoption?.Entries.Where(e=>e.At<=at && e.Revision<=rev).ToArray()??[];
        var visited=new HashSet<string>();var pending=new Stack<string>();pending.Push(applicant.Value);
        while(pending.TryPop(out var id)){
            if(!visited.Add(id) || !people.TryGetValue(id,out var person))continue;
            if(FoundingLine(id))return true;
            foreach(var parent in person.BirthParents??[])pending.Push(parent);
            if(adoption)foreach(var offer in entries.Where(e=>e.Terms?.Child.Value==id &&
                entries.Any(done=>done.Input.Original==e.Input.Id && done.Input.Action==AdoptionAction.Implement)))pending.Push(offer.Terms!.Parent.Value);
        }
        return false;
    }
    public static bool Qualified(ContractWorld w,EntityId person,long at,long rev)=>Documented(w,person,at,rev,false) ||
        HouseholdCourt.Events(w).Any(e=>e.Input.Action==CourtAction.RecognizePedigree && e.Input.Person==person && e.At<=at && e.Revision<=rev);
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId site,long at,long rev)
    {using var _ccRev=ControlContext.At(rev);
        var rows=HouseholdCourt.Events(w).Where(e=>Handles(e.Input.Action)&&e.Revision<rev && e.At<=at).ToArray();
        if(!CentralCourtCatalog.HasShogunate(w.NewGame?.HistoricalCatalog) || i.Site!=site || actor==i.Person ||
            !TransportWork.At(w,actor,site,at,rev,true) || !TransportWork.At(w,i.Person,site,at,rev,true) ||
            Assassination.DeadAt(w,actor,at,rev)||Assassination.DeadAt(w,i.Person,at,rev))
            throw new InvalidDataException("系図の調査・提出には本人と担当者の現地面会が必要です");
        if(i.Action==CourtAction.RecognizePedigree){
            var filing=rows.SingleOrDefault(e=>e.Input.Id==i.Reference && e.Input.Action==CourtAction.FilePedigree);
            if(filing==null || filing.At>=at || filing.Actor!=i.Person || filing.Input.Owner!=i.Owner || actor!=CentralCourtCatalog.CourtBrokerIn(w) ||
                site!=CentralCourtCatalog.KyotoIn(w) || rows.Any(e=>e.Input.Action==i.Action && e.Input.Person==i.Person))
                throw new InvalidDataException("京の担当者が提出済みの本人の系図原本を認定します");
            return;
        }
        if(i.Action==CourtAction.FilePedigree){
            if(site!=CentralCourtCatalog.KyotoIn(w) || i.Person!=CentralCourtCatalog.CourtBrokerIn(w) ||
                !rows.Any(e=>e.Input.Id==i.Reference && e.Input.Action==CourtAction.AttestPedigree && e.Input.Person==actor && e.Input.Owner==i.Owner) ||
                rows.Any(e=>e.Input.Action==i.Action && e.Actor==actor))throw new InvalidDataException("京に本人の調査済み系図を提出してください");
            return;
        }
        bool asking=i.Action==CourtAction.AskPedigree;
        var applicant=asking?actor:i.Person;var scribe=asking?i.Person:actor;
        if(!(scribe==CentralCourtCatalog.CourtBrokerIn(w) && site==CentralCourtCatalog.KyotoIn(w) ||
            scribe==CentralCourtCatalog.TempleBroker && site==TempleMilitary.Site) ||
            DefectionPacts.HouseAt(w,applicant,at,rev-1)!=i.Owner)throw new InvalidDataException("所属と現地の調査担当者を確認してください");
        var requests=rows.Where(e=>e.Input.Action==CourtAction.AskPedigree && e.Actor==applicant && e.Input.Person==scribe && e.Input.Owner==i.Owner &&
            !rows.Any(r=>r.Input.Reference==e.Input.Id));
        if(asking?i.Reference!=null||requests.Any():!requests.Any(e=>e.Input.Id==i.Reference && e.At<at) ||
            (i.Action==CourtAction.AttestPedigree)!=Documented(w,applicant,at,rev-1,true))
            throw new InvalidDataException("調査依頼と実親・成立済み養縁の記録を照合してください");
    }
    public static IEnumerable<(CourtInput,EntityId)> Reports(ContractWorld w,bool player){
        foreach(var f in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.FilePedigree && e.At<w.ElapsedSeconds &&
            (CentralCourtCatalog.CourtBrokerIn(w)==w.PlayerControl)==player && !HouseholdCourt.Events(w).Any(r=>r.Input.Reference==e.Input.Id && r.Input.Action==CourtAction.RecognizePedigree))){
            yield return (new(HouseholdCourt.Id("pedigree-recognition",f.Input.Id.Value),CourtAction.RecognizePedigree,f.Actor,f.Input.Owner,f.Place,Reference:f.Input.Id),CentralCourtCatalog.CourtBrokerIn(w));
        }
        foreach(var e in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.AskPedigree && e.At<w.ElapsedSeconds &&
            (e.Input.Person==w.PlayerControl)==player && !HouseholdCourt.Events(w).Any(r=>Handles(r.Input.Action)&&r.Input.Reference==e.Input.Id))){
            var action=Documented(w,e.Actor,w.ElapsedSeconds,w.CommitSequence,true)?CourtAction.AttestPedigree:CourtAction.ReportMissingPedigree;
            yield return (new(HouseholdCourt.Id("pedigree-report",e.Input.Id.Value),action,e.Actor,e.Input.Owner,e.Place,Reference:e.Input.Id),e.Input.Person);
        }
    }
}