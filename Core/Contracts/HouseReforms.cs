using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public enum ReformKind { ForeignLetters,MarriagePermission,SuccessionOrder,EqualPenalty,ReduceCastles,CastleResidence,
    ChangeFief,OpenMarket,RemoveBarrier,DirectService,Relocate,Confiscate,AllocateProvince }

public static class HouseReforms
{
    public const string Proposal="house-reform-proposal",Answer="house-reform-answer";
    public static string Name(ReformKind kind)=>kind switch
    {
        ReformKind.ForeignLetters=>"他国との私的書状を禁じる",ReformKind.MarriagePermission=>"婚姻を許可制にする",
        ReformKind.SuccessionOrder=>"後継の既定順位を定める",ReformKind.EqualPenalty=>"喧嘩両成敗を定める",
        ReformKind.ReduceCastles=>"城割り",ReformKind.CastleResidence=>"城下集住",ReformKind.ChangeFief=>"知行替え",
        ReformKind.OpenMarket=>"楽市",ReformKind.RemoveBarrier=>"関所撤廃",ReformKind.DirectService=>"直臣化",
        ReformKind.Relocate=>"移封",ReformKind.Confiscate=>"改易",ReformKind.AllocateProvince=>"国割り",
        _=>throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public static string Letter(CourtInput input,string kind)=>HouseholdCourt.Id(kind,input.Id.Value).Value;
    public static bool AnswerKnown(ContractWorld w,CourtEvent proposal,EntityId viewer)=>
        MessageFlow.Knows(w.Messaging,Letter(proposal.Input,Answer),viewer.Value);

    public static bool OpenMarketAt(ContractWorld w,EntityId site,long at,long revision)=>
        HouseholdCourt.Events(w).Any(e=>e.Input.Action==CourtAction.OpenLocalMarket && e.Input.Site==site &&
            (e.At<at || e.At==at && e.Revision<revision) &&
            w.LandRights.Any(r=>r.Kind=="control" && r.Subject==e.Input.Owner && r.Target==site &&
                r.From<=e.At && r.From<=at && at<r.Until && r.Revision<=revision));

    public static CourtEvent? TempleMarketSeat(ContractWorld w,EntityId site,long at,long revision)=>
        HouseholdCourt.Events(w).LastOrDefault(e=>e.Input.Action==CourtAction.GrantTempleSeat &&
            e.Input.Site==site && (e.At<at || e.At==at && e.Revision<revision));

    public static CourtEvent? SeatDispute(ContractWorld w,EntityId site,long at,long revision)=>
        HouseholdCourt.Events(w).LastOrDefault(e=>e.Input.Site==site &&
            (e.Input.Action is CourtAction.ChallengeSeat or CourtAction.InsistSeat or CourtAction.SettleSeat) &&
            (e.At<at || e.At==at && e.Revision<revision));

    public static long WoodPurchaseLimit(ContractWorld w,EntityId site,long at,long revision)
    {
        if(!OpenMarketAt(w,site,at,revision))return 2;
        if(SeatDispute(w,site,at,revision)?.Input.Action==CourtAction.InsistSeat)return 2;
        return TempleMarketSeat(w,site,at,revision)==null?4:3;
    }

    static void Dispatch(ContractWorld w,CourtInput input,string kind,EntityId from,EntityId to,string body)
    {
        var address=FamilyBirth.People(w).Single(p=>p.Id==to.Value).Site;
        var letter=new MessageEnvelope(Letter(input,kind),input.Id.Value,kind,from.Value,to.Value,body,address,w.ElapsedSeconds);
        MessageFlow.Send(w.Messaging,letter,w.ElapsedSeconds);
        w.SealedMessages=[..w.SealedMessages,new(letter.Id)];
        RegionalCampaign.Bind(w);
    }
    internal static void Send(ContractWorld w,CourtInput input,EntityId actor)=>
        Dispatch(w,input,Proposal,actor,input.Person,NewGameWorld.SiteName(input.Site.Value)+"での「"+Name(input.Reform!.Value)+"」について意向を問う。");

    internal static void Receive(ContractWorld w)
    {
        foreach(var e in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.ConsultReform))
        {
            var i=e.Input;
            if(w.Messaging.Messages.Any(m=>m.Id==Letter(i,Answer)))continue;
            var recipient=i.Person;
            var id=Letter(i,Proposal);
            if(recipient==w.PlayerControl || !w.Messaging.Receipts.Any(r=>r.MessageId==id&&r.ReceiverId==recipient.Value) ||
                Assassination.DeadAt(w,recipient,w.ElapsedSeconds,w.CommitSequence))continue;
            if(!MessageFlow.Knows(w.Messaging,id,recipient.Value))
                w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(id,recipient.Value,w.ElapsedSeconds,id)];
            var score=CharacterRelations.Enabled(w)?CharacterRelations.DiagnosticScores(w,recipient,e.Actor).Trust-
                CharacterRelations.DiagnosticScores(w,recipient,e.Actor).Hostility:0;
            if(w.LandRights.Any(r=>r.Subject==recipient && r.Target==i.Site && r.Kind=="control" && r.From<=w.ElapsedSeconds &&
                w.ElapsedSeconds<r.Until) && i.Reform is ReformKind.Confiscate or ReformKind.ChangeFief or ReformKind.Relocate)score-=40;
            var response=DefectionPacts.HouseAt(w,recipient,w.ElapsedSeconds,w.CommitSequence)!=i.Owner||score< -15?"拒む":
                score<15?"条件を再協議したい":"従う";
            Dispatch(w,i,Answer,recipient,e.Actor,Name(i.Reform!.Value)+"への返答："+response);
        }
    }
}
