namespace HakutaiKagami.Core.Contracts;

public static class SupremeOffice
{
    public static readonly EntityId Bakufu=new("org:central-bakufu");
    public static EntityId? ShogunAt(ContractWorld w,long at,long revision)
    {using var _ccRev=ControlContext.At(revision);
        if(!CentralCourtCatalog.HasShogunate(w.NewGame?.HistoricalCatalog))return null;
        var grant=SupremeInvestiture.LastGrant(w,CourtAction.PetitionShogun,at,revision);
        var incumbent=grant?.Input.Person??CentralCourtCatalog.Shogun;
        if(Assassination.DeadAt(w,incumbent,at,revision))return null;
        var resigned=HouseholdCourt.Events(w).Any(e=>e.Input.Action==CourtAction.ResignShogun &&
            e.Actor==incumbent && e.Revision>(grant?.Revision??0) && e.At<=at && e.Revision<=revision);
        return resigned || ShogunExile.Departed(w,incumbent,at,revision,grant?.Revision??0)?null:incumbent;
    }
    internal static bool IsSupport(CourtAction a)=>a is CourtAction.OfferShogunSupport or CourtAction.AcceptShogunSupport or CourtAction.DeclineShogunSupport;
    public static bool ControlsKyoto(ContractWorld w,EntityId head,EntityId house,long at,long revision)=>
        FactionStances.ResolveAuthority(w,head,house,at,Math.Min(revision,w.CommitSequence))!=null &&
        w.LandRights.Any(r=>r.Kind=="control" && r.Subject==house && r.Target==CentralCourtCatalog.Kyoto && r.From<=at &&
            (r.From<at || r.Revision<=revision) && at<r.Until) && !CastleAdministration.Blocked(w,CentralCourtCatalog.Kyoto,at,revision);
    internal static void CheckSupport(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {using var _ccRev=ControlContext.At(rev);
        bool offer=i.Action==CourtAction.OfferShogunSupport;
        var sponsor=offer?actor:i.Person;
        var candidate=offer?i.Person:actor;
        if(!CentralCourtCatalog.HasShogunate(w.NewGame?.HistoricalCatalog) || sponsor==candidate || place!=CentralCourtCatalog.Kyoto ||
            place!=i.Site || !ControlsKyoto(w,sponsor,i.Owner,at,rev-1) ||
            new[]{sponsor,candidate}.Any(p=>!TransportWork.At(w,p,place,at,rev,true) || Assassination.DeadAt(w,p,at,rev) || HostageCustody.HeldAt(w,p,at,rev-1)) ||
            !w.Characters.Any(c=>c.Id==candidate && c.Life==LifeState.Adult))
            throw new InvalidDataException("京を支配する当主と成人の候補本人が、京で擁立を相談してください");
        var rows=HouseholdCourt.Events(w).Where(e=>IsSupport(e.Input.Action) && e.At<=at && e.Revision<rev).ToArray();
        var offers=rows.Where(e=>e.Input.Action==CourtAction.OfferShogunSupport && e.Actor==sponsor && e.Input.Person==candidate && e.Input.Owner==i.Owner);
        if(offer ? i.Reference!=null || offers.Any(e=>!rows.Any(r=>r.Input.Reference==e.Input.Id && r.Input.Action==CourtAction.DeclineShogunSupport))
                 : !offers.Any(e=>e.Input.Id==i.Reference && e.At<at && !rows.Any(r=>r.Input.Reference==e.Input.Id)))
            throw new InvalidDataException("同じ擁立の申し出に候補本人が一度だけ返答します");
    }
    public static IEnumerable<(CourtInput Answer,EntityId Candidate)> SupportAnswers(ContractWorld w,bool player)
    {
        foreach(var offer in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.OfferShogunSupport &&
            (w.PlayerControl==e.Input.Person)==player && e.At<w.ElapsedSeconds && !HouseholdCourt.Events(w).Any(r=>IsSupport(r.Input.Action)&&r.Input.Reference==e.Input.Id))){
            foreach(var action in new[]{CourtAction.AcceptShogunSupport,CourtAction.DeclineShogunSupport}){
                if(!player && (action==CourtAction.AcceptShogunSupport)!=(SocialWorld.ConsentBonus(w,offer.Input.Person,offer.Actor,w.ElapsedSeconds,w.CommitSequence)>=0))continue;
                yield return (new(HouseholdCourt.Id(action.ToString(),offer.Input.Id.Value),action,offer.Actor,offer.Input.Owner,offer.Place,Reference:offer.Input.Id),offer.Input.Person);
            }
        }
    }
    public static IEnumerable<EntityId> Sponsors(ContractWorld w,EntityId candidate,long at,long revision)
    {using var _ccRev=ControlContext.At(revision);
        var events=HouseholdCourt.Events(w).Where(e=>e.At<=at && e.Revision<=revision).ToArray();
        foreach(var answer in events.Where(e=>e.Input.Action==CourtAction.AcceptShogunSupport && e.Actor==candidate)){
            var sponsor=answer.Input.Person;
            if(!Assassination.DeadAt(w,sponsor,at,revision) && ControlsKyoto(w,sponsor,answer.Input.Owner,at,revision))
                yield return answer.Input.Owner;
        }
    }
    internal static void CheckResignation(ContractWorld w,CourtInput input,EntityId actor,EntityId place,long at,long revision)
    {using var _ccRev=ControlContext.At(revision);
        if(input.Owner!=Bakufu || input.Site!=CentralCourtCatalog.Kyoto || place!=input.Site || input.Person!=actor ||
            input.Reference!=null || ShogunAt(w,at,revision-1)!=actor || !TransportWork.At(w,actor,place,at,revision,true))
            throw new InvalidDataException("在職中の将軍本人が京で辞任してください");
    }
}