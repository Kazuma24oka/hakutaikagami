using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

public static class ShogunateAppointments
{
    public const string Petition="shugo-petition",Reply="shugo-decision";
    static EntityId Broker=>CentralCourtCatalog.ShogunBroker;
    static EntityId Addressee(ContractWorld w,long at,long revision)=>SupremeOffice.ShogunAt(w,at,revision) ?? throw new InvalidDataException("守護補任の申請先となる将軍が空位です");
    static EntityId Kyoto=>CentralCourtCatalog.Kyoto;
    internal static bool Handles(CourtAction a)=>a is CourtAction.AskShugo or CourtAction.RecommendShugo or CourtAction.DeclineShugo or CourtAction.AppointShugo or CourtAction.RejectShugo;
    static string Letter(CourtInput i)=>HouseholdCourt.Id("shugo-letter",i.Id.Value).Value;
    static bool Ruling(CourtAction a)=>a is CourtAction.AppointShugo or CourtAction.RejectShugo;
    static bool HasLand(ContractWorld w,EntityId owner,EntityId site,long at,long rev)=>
        w.LandRights.Any(r=>r.Kind=="control" && r.Subject==owner && r.Target==site && r.From<=at && at<r.Until && (r.From<at || r.Revision<=rev)) &&
        !CastleAdministration.Blocked(w,site,at,rev);
    public static IEnumerable<EntityId> Districts(ContractWorld w,EntityId owner)=>w.NewGame!.Start.Castles
        .Select(c=>new EntityId(c.Id)).Where(site=>HouseTerritoryScale.Province(site)!=null && HasLand(w,owner,site,w.ElapsedSeconds,w.CommitSequence));
    static string Body(CourtInput i)=>HouseTerritoryScale.Province(i.Site)+"守護の"+(i.Action switch{
        CourtAction.RecommendShugo=>"補任を将軍に申請する。",CourtAction.AppointShugo=>"補任を認める。",_=>"補任申請を退ける。"});
    internal static void Dispatch(ContractWorld w,CourtInput i,EntityId actor)
    {
        bool submit=i.Action==CourtAction.RecommendShugo;
        if(!submit && !Ruling(i.Action))return;
        var recipient=submit?Addressee(w,w.ElapsedSeconds,w.CommitSequence):i.Person;
        var m=new MessageEnvelope(Letter(i),i.Id.Value,submit?Petition:Reply,actor.Value,recipient.Value,Body(i),Kyoto.Value,w.ElapsedSeconds){DeliverySite=Kyoto.Value};
        MessageFlow.Send(w.Messaging,m,w.ElapsedSeconds);w.SealedMessages=[..w.SealedMessages,new(m.Id)];RegionalCampaign.Bind(w);
    }
    internal static void ValidateLetter(ContractWorld w,CourtEvent e)
    {
        bool submit=e.Input.Action==CourtAction.RecommendShugo;
        if(!submit && !Ruling(e.Input.Action))return;
        var id=Letter(e.Input);var m=w.Messaging.Messages.SingleOrDefault(x=>x.Id==id);
        if(m==null || m.Kind!=(submit?Petition:Reply) || m.Body!=Body(e.Input) || m.SenderId!=e.Actor.Value ||
           m.RecipientId!=(submit?Addressee(w,e.At,e.Revision-1):e.Input.Person).Value || m.CreatedAt!=e.At || m.CorrelationId!=e.Input.Id.Value ||
           m.DeliverySite!=Kyoto.Value || !w.SealedMessages.Any(x=>x.Value==id))throw new InvalidDataException("幕府の書状と原本が一致しません");
    }
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {
        if(!CentralCourtCatalog.HasShogunate(w.NewGame?.HistoricalCatalog) || place!=Kyoto || HouseTerritoryScale.Province(i.Site)==null ||
            !TransportWork.At(w,actor,place,at,rev,true) || Assassination.DeadAt(w,actor,at,rev))
            throw new InvalidDataException("京で対象国と申請本人を確認してください");
        if((Ruling(i.Action) && SupremeOffice.ShogunAt(w,at,rev-1)!=actor) ||
            (i.Action==CourtAction.RecommendShugo && SupremeOffice.ShogunAt(w,at,rev-1)==null))
            throw new InvalidDataException("守護補任を決裁する在職中の将軍が必要です");
        var prior=HouseholdCourt.Events(w).Where(e=>Handles(e.Input.Action) && e.At<=at && e.Revision<rev).ToArray();
        if(i.Action==CourtAction.AskShugo){
            if(i.Person!=Broker || i.Reference!=null || actor==Broker || actor==SupremeOffice.ShogunAt(w,at,rev-1) || HouseSuccession.Head(w,i.Owner,at,rev-1)!=actor ||
               !HasLand(w,i.Owner,i.Site,at,rev-1) || !TransportWork.At(w,Broker,place,at,rev,true) ||
               !SocialWorld.Ledger(w).Any(e=>e.Actor==actor && e.Input.Person==Broker && e.Input.Action==SocialWorldAction.Visit && e.Revision<rev) ||
               prior.Any(e=>e.Actor==actor && e.Input.Action==i.Action && e.Input.Site==i.Site))
                throw new InvalidDataException("京で藤孝と面会し、自家の支配拠点を指定してください");
            return;
        }
        var source=prior.SingleOrDefault(e=>e.Input.Id==i.Reference);
        if(source==null || source.At>=at || i.Owner!=source.Input.Owner || i.Site!=source.Input.Site ||
           prior.Any(e=>e.Input.Reference==source.Input.Id))throw new InvalidDataException("同一国の未処理の申請原本が必要です");
        if(!Ruling(i.Action)){
            if(actor!=Broker || source.Input.Action!=CourtAction.AskShugo || i.Person!=source.Actor ||
               !TransportWork.At(w,i.Person,place,at,rev,true) || (actor!=ControlSuccession.PlayerAtRevision(w,rev) &&
               (i.Action==CourtAction.RecommendShugo)!=(SocialWorld.ConsentBonus(w,actor,i.Person,at,rev)>=2)))
                throw new InvalidDataException("取次本人の判断が必要です");
            return;
        }
        if(source.Input.Action!=CourtAction.RecommendShugo || source.Input.Person!=i.Person || actor!=Addressee(w,source.At,source.Revision-1) ||
           MessageFlow.LearnedAt(w.Messaging,Letter(source.Input),actor.Value) is not {} opened || opened>at)
            throw new InvalidDataException("将軍が申請書を読んでから決裁してください");
        if(i.Action==CourtAction.AppointShugo && prior.Any(e=>e.Input.Action==CourtAction.AppointShugo &&
            HouseTerritoryScale.Province(e.Input.Site)==HouseTerritoryScale.Province(i.Site) && !Assassination.DeadAt(w,e.Input.Person,at,rev)))
            throw new InvalidDataException("その国の現任守護がいます");
    }
    public static IEnumerable<CourtInput> AvailableAnswers(ContractWorld w,EntityId actor)
    {
        var incumbent=SupremeOffice.ShogunAt(w,w.ElapsedSeconds,w.CommitSequence);
        if(!CentralCourtCatalog.HasShogunate(w.NewGame?.HistoricalCatalog) || actor!=w.PlayerControl ||
            (actor!=incumbent && actor!=Broker))yield break;
        bool ruling=actor==incumbent;
        var wanted=ruling?CourtAction.RecommendShugo:CourtAction.AskShugo;
        foreach(var source in HouseholdCourt.Events(w).Where(e=>e.Input.Action==wanted)){
            if(ruling && !MessageFlow.Knows(w.Messaging,Letter(source.Input),actor.Value))continue;
            var applicant=ruling?source.Input.Person:source.Actor;
            foreach(var action in ruling?new[]{CourtAction.AppointShugo,CourtAction.RejectShugo}:new[]{CourtAction.RecommendShugo,CourtAction.DeclineShugo}){
                var answer=new CourtInput(HouseholdCourt.Id(action.ToString(),source.Input.Id.Value),action,applicant,source.Input.Owner,source.Input.Site,Reference:source.Input.Id);
                try{Check(w,answer,actor,Kyoto,w.ElapsedSeconds,w.CommitSequence+1);}
                catch(InvalidDataException){continue;}
                yield return answer;
            }
        }
    }
    internal static void Process(ContractWorld w)
    {
        if(!CentralCourtCatalog.HasShogunate(w.NewGame?.HistoricalCatalog))return;
        var rows=HouseholdCourt.Events(w);
        foreach(var e in rows.Where(e=>e.Input.Action is CourtAction.AskShugo or CourtAction.RecommendShugo &&
            e.At<w.ElapsedSeconds && !rows.Any(r=>r.Input.Reference==e.Input.Id && Handles(r.Input.Action)))){
            bool ruling=e.Input.Action==CourtAction.RecommendShugo;
            var respondent=ruling?SupremeOffice.ShogunAt(w,w.ElapsedSeconds,w.CommitSequence):Broker;
            if(respondent is not {} actor)continue;
            if(actor==w.PlayerControl || Assassination.DeadAt(w,actor,w.ElapsedSeconds,w.CommitSequence))continue;
            var applicant=ruling?e.Input.Person:e.Actor;
            CourtAction action;
            if(ruling){
                var id=Letter(e.Input);
                if(!w.Messaging.Receipts.Any(r=>r.MessageId==id && r.ReceiverId==actor.Value))continue;
                if(!MessageFlow.Knows(w.Messaging,id,actor.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(id,actor.Value,w.ElapsedSeconds,id)];
                bool occupied=HouseholdCourt.Events(w).Any(r=>r.Input.Action==CourtAction.AppointShugo &&
                    HouseTerritoryScale.Province(r.Input.Site)==HouseTerritoryScale.Province(e.Input.Site) && !Assassination.DeadAt(w,r.Input.Person,w.ElapsedSeconds,w.CommitSequence));
                action=occupied?CourtAction.RejectShugo:CourtAction.AppointShugo;
            }else action=SocialWorld.ConsentBonus(w,actor,applicant,w.ElapsedSeconds,w.CommitSequence)>=2?CourtAction.RecommendShugo:CourtAction.DeclineShugo;
            var input=new CourtInput(HouseholdCourt.Id("shugo-answer",e.Input.Id.Value),action,applicant,e.Input.Owner,e.Input.Site,Reference:e.Input.Id);
            TransactionalWorld.TryApply(w,c=>HouseholdCourt.Execute(c,input,actor));
        }
    }
    public static string[] KnownOffices(ContractWorld w,EntityId applicant)=>HouseholdCourt.Events(w)
        .Where(e=>e.Input.Action==CourtAction.AppointShugo && e.Input.Person==applicant && MessageFlow.Knows(w.Messaging,Letter(e.Input),applicant.Value))
        .Select(e=>HouseTerritoryScale.Province(e.Input.Site)+"守護").ToArray();
}
