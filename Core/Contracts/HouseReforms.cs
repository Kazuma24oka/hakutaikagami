using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public enum ReformKind { ForeignLetters,MarriagePermission,SuccessionOrder,EqualPenalty,ReduceCastles,CastleResidence,
    ChangeFief,OpenMarket,RemoveBarrier,DirectService,Relocate,Confiscate,AllocateProvince }

public sealed record TransferBaggage(EntityId? Site,long Purse,PersonalObject[] PersonalItems,
    ResourceLot[] EntrustedLots,HouseFreightJob[] HouseShipments);

public static class HouseReforms
{
    public const string Proposal="house-reform-proposal",Answer="house-reform-answer",TransferOrder="estate-transfer-order";
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
    public static TransferBaggage? Baggage(ContractWorld w,CourtEvent order)
    {
        var actor=w.PlayerControl;
        if(order.Input.Action!=CourtAction.IssueTransfer || order.Input.Person!=actor ||
            !MessageFlow.Knows(w.Messaging,Letter(order.Input,TransferOrder),actor.Value))return null;
        var lots=w.ResourceLots.Where(l=>l.MobileHolder==actor && l.Holder==actor && l.Owner!=actor && l.Quantity>0).ToArray();
        var shipments=HouseLogistics.View(w)?.Jobs.Where(j=>j.Terms.Carrier==actor &&
            (j.Cargo>0 || j.Cash>0) && j.Phase is not (HouseFreightPhase.Completed or HouseFreightPhase.Returned)).ToArray()??[];
        return new(SocialWorld.Site(w,actor),NewGameWork.Balance(w,actor),SocialAssets.Inventory(w,actor),lots,shipments);
    }

    public static IEnumerable<CourtEvent> LevyGrants(ContractWorld w)
    {
        foreach(var e in HouseholdCourt.Events(w)){
            if(e.Input.Action==CourtAction.GrantLevyShare)yield return e;
            if(e.Input.Action is CourtAction.ExchangeLevyShare or CourtAction.ReceiveProvinceShare)yield return e with{Actor=e.Input.Person,
                Input=e.Input with{Person=e.Actor,Action=CourtAction.GrantLevyShare}};
        }
    }
    public static long LevyEnd(ContractWorld w,EntityId id)=>HouseholdCourt.Events(w)
        .FirstOrDefault(e=>(e.Input.Action is CourtAction.ExchangeLevyShare or CourtAction.SurrenderLevyShare) && e.Input.LevyGrant==id)?.Revision??long.MaxValue;

    public static long AccruedLevyRice(ContractWorld w,CourtEvent grant,long at,long revision)
    {
        if(grant.Input.Action!=CourtAction.GrantLevyShare)return 0;
        var orders=w.Communities?.Orders??[];
        var collected=orders.Where(o=>o.Input.Site==grant.Place && o.Input.Action==EconomyAction.CollectLevy &&
            o.Revision>grant.Revision && o.Revision<LevyEnd(w,grant.Input.Id) && (o.At<at || o.At==at && o.Revision<revision) &&
            w.LandRights.Any(r=>r.Kind=="control" && r.Target==grant.Place && r.Subject==grant.Input.Owner &&
                r.From<=o.At && o.At<r.Until)).Sum(o=>o.Input.Amount);
        return checked(collected/100*grant.Input.Amount+collected%100*grant.Input.Amount/100);
    }

    public static long ReceivedLevyRice(ContractWorld w,EntityId grant,long at,long revision)=>
        HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.ReceiveLevyShare && e.Input.Reference==grant &&
            (e.At<at || e.At==at && e.Revision<revision)).Sum(e=>e.Input.Amount);

    public static bool AnswerKnown(ContractWorld w,CourtEvent proposal,EntityId viewer)=>
        MessageFlow.Knows(w.Messaging,Letter(proposal.Input,Answer),viewer.Value);

    public static IEnumerable<CourtEvent> KnownLetterBreaches(ContractWorld w,EntityId viewer)=>
        HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.LetterBreach &&
            (e.Actor==viewer || e.Input.Reference is {} id && MessageFlow.Knows(w.Messaging,id.Value,viewer.Value)));

    public static bool LetterLawAt(ContractWorld w,EntityId owner,EntityId site,long at,long revision)=>
        LawAt(w,CourtAction.EnactLetterLaw,owner,site,at,revision);
    public static EntityId ResidenceAt(ContractWorld w,EntityId person,long at,long revision)=>
        HouseholdCourt.Events(w).LastOrDefault(e=>e.Input.Action==CourtAction.SettleCastleResidence && e.Actor==person &&
            (e.At<at || e.At==at && e.Revision<revision))?.Place ?? new EntityId(FamilyBirth.People(w).Single(p=>p.Id==person.Value).Site);
    public static CourtEvent? CastleReduction(ContractWorld w,EntityId site,long at,long revision)=>
        HouseholdCourt.Events(w).FirstOrDefault(e=>e.Input.Action==CourtAction.ReduceCastle && e.Place==site &&
            (e.At<at || e.At==at && e.Revision<revision));
    public static bool SuccessionLawAt(ContractWorld w,EntityId owner,EntityId site,long at,long revision)=>
        LawAt(w,CourtAction.EnactSuccessionLaw,owner,site,at,revision);
    public static bool MarriageLawAt(ContractWorld w,EntityId owner,EntityId site,long at,long revision)=>
        LawAt(w,CourtAction.EnactMarriageLaw,owner,site,at,revision);
    static bool LawAt(ContractWorld w,CourtAction law,EntityId owner,EntityId site,long at,long revision)=>
        HouseholdCourt.Events(w).Any(e=>e.Input.Action==law &&
            e.Input.Owner==owner && e.Input.Site==site && (e.At<at || e.At==at && e.Revision<revision) &&
            w.LandRights.Any(r=>r.Subject==owner && r.Target==site && r.Kind=="control" &&
                r.From<=e.At && at<r.Until));

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

    public static BranchEdge[] LocalRoads(ContractWorld w,EntityId site)
    {
        var node=NewGameWorld.Node(site.Value);
        return BranchRoutes.GraphEdges(w).Where(e=>!NationalRoutes.Sea(e.Id) && (e.From==node || e.To==node)).ToArray();
    }

    public static CourtEvent? BarrierAt(ContractWorld w,string edge,long at,long revision)
    {
        var last=HouseholdCourt.Events(w).LastOrDefault(e=>e.Input.BarrierEdge==edge &&
            (e.Input.Action is CourtAction.EstablishBarrier or CourtAction.AbolishBarrier) &&
            (e.At<at || e.At==at && e.Revision<revision));
        return last?.Input.Action==CourtAction.EstablishBarrier?last:null;
    }

    public static long WoodPurchaseLimit(ContractWorld w,EntityId site,long at,long revision)
    {
        if(!OpenMarketAt(w,site,at,revision))return 2;
        if(SeatDispute(w,site,at,revision)?.Input.Action==CourtAction.InsistSeat)return 2;
        return TempleMarketSeat(w,site,at,revision)==null?4:3;
    }

    public const string ResidenceNotice="castle-residence-notice";
    public static string? ReportedAddress(ContractWorld w,EntityId observer,EntityId person){
        var latest=HouseholdCourt.Events(w).LastOrDefault(e=>e.Input.Action==CourtAction.ReportResidence && e.Actor==person &&
            e.Input.Person==observer && MessageFlow.Knows(w.Messaging,Letter(e.Input,ResidenceNotice),observer.Value));
        return latest?.Place.Value;
    }
    public static string KnownAddress(ContractWorld w,EntityId observer,EntityId person)=>
        ReportedAddress(w,observer,person) ?? FamilyBirth.People(w).Single(p=>p.Id==person.Value).Site;
    internal static void SendResidenceNotice(ContractWorld w,CourtInput i,EntityId actor)=>
        Dispatch(w,i,ResidenceNotice,actor,i.Person,"城下に居所を移しました。届出先："+NewGameWorld.SiteName(i.Site.Value));
    static void Dispatch(ContractWorld w,CourtInput input,string kind,EntityId from,EntityId to,string body)
    {
        var address=KnownAddress(w,from,to);
        var letter=new MessageEnvelope(Letter(input,kind),input.Id.Value,kind,from.Value,to.Value,body,address,w.ElapsedSeconds){DeliverySite=address};
        MessageFlow.Send(w.Messaging,letter,w.ElapsedSeconds);
        w.SealedMessages=[..w.SealedMessages,new(letter.Id)];
        RegionalCampaign.Bind(w);
    }
    internal static void SendTransfer(ContractWorld w,CourtEvent proposal,CourtInput order,EntityId actor)=>
        Dispatch(w,order,TransferOrder,actor,proposal.Input.Person,
            proposal.Input.Reform==ReformKind.AllocateProvince?$"{NewGameWorld.SiteName(proposal.Input.Site.Value)}と{NewGameWorld.SiteName(proposal.Input.NewEstate!.Value.Value)}で、それぞれ徴収米の{proposal.Input.Amount}%を現地で引き取れ。":
            proposal.Input.Reform==ReformKind.Confiscate?$"現地で徴収米の給付原本 {proposal.Input.LevyGrant} を返上せよ。":
            NewGameWorld.SiteName(proposal.Input.Site.Value)+"で旧職を引き継ぎ、"+
            NewGameWorld.SiteName(proposal.Input.NewEstate!.Value.Value)+"へ赴任せよ。原本："+proposal.Input.Id.Value);

    internal static void Send(ContractWorld w,CourtInput input,EntityId actor)=>
        Dispatch(w,input,Proposal,actor,input.Person,(input.NewEstate is {} dest?NewGameWorld.SiteName(input.Site.Value)+"から"+NewGameWorld.SiteName(dest.Value)+"への":NewGameWorld.SiteName(input.Site.Value)+"での")+"「"+Name(input.Reform!.Value)+"」について意向を問う。"+(input.Reform==ReformKind.AllocateProvince && input.NewEstate!=null?$" 両拠点の徴収米を各{input.Amount}%、現地で個別に引き取る。":"")+(input.LevyGrant is {} right?$" 徴収米{input.Amount}%、原本 {right.Value}。旧領で発生済みの取り分は旧領で精算する。":""));

    public static string ReplyText(CourtAction action)=>action switch{
        CourtAction.AssentReform=>"従う",CourtAction.CounterReform=>"条件を再協議したい",
        CourtAction.RejectReform=>"拒む",_=>throw new ArgumentOutOfRangeException(nameof(action))};
    internal static void Reply(ContractWorld w,CourtEvent proposal,string response)=>
        Dispatch(w,proposal.Input,Answer,proposal.Input.Person,proposal.Actor,
            Name(proposal.Input.Reform!.Value)+"への返答："+response);

    internal static void Receive(ContractWorld w)
    {
        foreach(var shown in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.PresentServiceRelease && e.Input.Person!=w.PlayerControl && e.At<w.ElapsedSeconds)){
            var master=shown.Input.Person;
            if(HouseholdCourt.Events(w).Any(e=>e.Input.Reference==shown.Input.Id && e.Input.Action is CourtAction.ConsentServiceRelease or CourtAction.RefuseServiceRelease) ||
                SocialWorld.Site(w,master)!=shown.Place || SocialWorld.Site(w,shown.Actor)!=shown.Place ||
                Assassination.DeadAt(w,master,w.ElapsedSeconds,w.CommitSequence))continue;
            var willing=w.Characters.Single(c=>c.Id==master).WillObey &&
                (!CharacterRelations.Enabled(w) || FamilyConsentRules.IndividualAccepts(w,master,shown.Actor,w.ElapsedSeconds,w.CommitSequence));
            var answer=new CourtInput(HouseholdCourt.Id("request",shown.Input.Id.Value+"/release-answer"),
                willing?CourtAction.ConsentServiceRelease:CourtAction.RefuseServiceRelease,shown.Actor,shown.Input.Owner,shown.Place,Reference:shown.Input.Id){FormerOffice=shown.Input.FormerOffice};
            TransactionalWorld.TryApply(w,c=>HouseholdCourt.Execute(c,answer,master));
        }
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
            Reply(w,e,response);
        }
    }
}
