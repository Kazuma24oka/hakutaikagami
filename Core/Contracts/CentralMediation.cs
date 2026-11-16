namespace HakutaiKagami.Core.Contracts;

public static class CentralMediation
{
    internal static bool Handles(CourtAction action)=>action is CourtAction.AskCourtIntroduction or
        CourtAction.AcceptCourtIntroduction or CourtAction.DeclineCourtIntroduction;

    static IEnumerable<CourtEvent> Earlier(ContractWorld w,long at,long rev)=>
        HouseholdCourt.Events(w).Where(e=>Handles(e.Input.Action) && e.At<=at && e.Revision<rev);

    public static EntityId? ApplicantHouse(ContractWorld w,EntityId actor)=>
        DefectionPacts.HouseAt(w,actor,w.ElapsedSeconds,w.CommitSequence);

    // 取次は公家それぞれが、本人が知る依頼人との関係で決める。
    static bool WillRecommend(ContractWorld w,EntityId broker,EntityId applicant,long at,long rev)=>
        SocialWorld.ConsentBonus(w,broker,applicant,at,rev)>=2;

    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {using var _ccRev=ControlContext.At(rev);
        var broker=i.Action==CourtAction.AskCourtIntroduction?i.Person:actor;
        if(!CentralCourtCatalog.HasCourt(w.NewGame?.HistoricalCatalog) || i.Site!=CentralCourtCatalog.KyotoIn(w) || place!=i.Site ||
           !TransportWork.At(w,actor,place,at,rev,true) || !TransportWork.At(w,i.Person,place,at,rev,true) ||
           Assassination.DeadAt(w,i.Person,at,rev) || Assassination.DeadAt(w,actor,at,rev))
            throw new InvalidDataException("京で取次本人と面会して相談してください");
        var rows=Earlier(w,at,rev).ToArray();
        if(i.Action==CourtAction.AskCourtIntroduction){
            if(i.Title is {} title && CentralTitles.Title(title) is not {Grantor:"court",Kind:"rank" or "kokushi" or "kyokan"})
                throw new InvalidDataException("朝廷へ奏請できる位階・官職ではありません");
            if(CentralCourtCatalog.HasShogunate(w.NewGame?.HistoricalCatalog) && TransportWork.WalletBefore(w,actor,at,rev)<CentralRank.Offering)
                throw new InvalidDataException("奏請に添える本人の献金60文が必要です");
            if(CentralCourtCatalog.HasShogunate(w.NewGame?.HistoricalCatalog) && !RankLeave.Eligible(w,actor,i.Owner,at,rev))
                throw new InvalidDataException("家臣の奏請には現在の当主の許可が必要です");
            if(CentralCourtCatalog.IsCourtBroker(w,actor) || !CentralCourtCatalog.IsCourtBroker(w,i.Person) || i.Reference!=null || DefectionPacts.HouseAt(w,actor,at,rev-1)!=i.Owner ||
               !SocialWorld.Ledger(w).Any(e=>e.Input.Action==SocialWorldAction.Visit && e.Actor==actor &&
                    e.Input.Person==broker && e.At<=at && e.Revision<rev) ||
               rows.Any(e=>e.Actor==actor && e.Input.Action==i.Action && !rows.Any(r=>r.Input.Reference==e.Input.Id && r.Input.Action==CourtAction.DeclineCourtIntroduction) &&
                   !(i.Title!=null && e.Input.Title!=null && CentralRank.Decided(HouseholdCourt.Events(w).Where(x=>x.At<at||x.At==at&&x.Revision<rev).ToArray(),e))))
                throw new InvalidDataException("本人との面識と所属を確認し、同じ奏請の返答を待ってください");
            return;
        }
        var petition=rows.SingleOrDefault(e=>e.Input.Id==i.Reference && e.Input.Action==CourtAction.AskCourtIntroduction);
        if(petition==null || actor!=petition.Input.Person || i.Person!=petition.Actor || i.Owner!=petition.Input.Owner ||
           at<=petition.At || rows.Any(e=>e.Input.Reference==petition.Input.Id))
            throw new InvalidDataException("取次本人が未返答の奏請に一度だけ返答します");
        if(actor!=w.PlayerControl && (i.Action==CourtAction.AcceptCourtIntroduction)!=WillRecommend(w,actor,petition.Actor,at,rev))
            throw new InvalidDataException("取次本人が知る依頼人との関係に合わない返答です");
    }

    public static IEnumerable<CourtInput> AvailableAnswers(ContractWorld w)
    {
        if(!CentralCourtCatalog.IsCourtBroker(w,w.PlayerControl) || !CentralCourtCatalog.HasCourt(w.NewGame?.HistoricalCatalog))yield break;
        var rows=Earlier(w,w.ElapsedSeconds,w.CommitSequence+1).ToArray();
        foreach(var petition in rows.Where(e=>e.Input.Action==CourtAction.AskCourtIntroduction && e.Input.Person==w.PlayerControl && e.At<w.ElapsedSeconds &&
            !rows.Any(r=>r.Input.Reference==e.Input.Id))){
            if(!TransportWork.At(w,petition.Actor,petition.Place,w.ElapsedSeconds,w.CommitSequence+1,true) ||
                Assassination.DeadAt(w,petition.Actor,w.ElapsedSeconds,w.CommitSequence))continue;
            foreach(var action in new[]{CourtAction.AcceptCourtIntroduction,CourtAction.DeclineCourtIntroduction})
                yield return new(HouseholdCourt.Id(action.ToString(),petition.Input.Id.Value),action,petition.Actor,
                    petition.Input.Owner,petition.Place,Reference:petition.Input.Id);
        }
    }
    internal static IEnumerable<(CourtInput Reply,EntityId Broker)> Pending(ContractWorld w)
    {
        if(!CentralCourtCatalog.HasCourt(w.NewGame?.HistoricalCatalog))yield break;
        var rows=Earlier(w,w.ElapsedSeconds,w.CommitSequence+1).ToArray();
        foreach(var petition in rows.Where(e=>e.Input.Action==CourtAction.AskCourtIntroduction && e.Input.Person!=w.PlayerControl && e.At<w.ElapsedSeconds &&
            !rows.Any(r=>r.Input.Reference==e.Input.Id))){
            var broker=petition.Input.Person;
            var action=WillRecommend(w,broker,petition.Actor,w.ElapsedSeconds,w.CommitSequence)?CourtAction.AcceptCourtIntroduction:CourtAction.DeclineCourtIntroduction;
            yield return (new(HouseholdCourt.Id("court-introduction",petition.Input.Id.Value),action,petition.Actor,
                petition.Input.Owner,petition.Place,Reference:petition.Input.Id),broker);
        }
    }
}
