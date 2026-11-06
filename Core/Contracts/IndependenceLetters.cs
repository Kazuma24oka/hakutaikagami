using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

public static class IndependenceLetters
{
    public const string Kind="public-independence-letter";
    public const string Reply="public-independence-answer";
    public const string KeepReport="independent-keep-receipt";
    internal static bool Answer(CourtAction a)=>a is CourtAction.JoinDeclaredHouse or CourtAction.RemainWithHouse or CourtAction.StayNeutral;
    internal static bool Handles(CourtAction a)=>Outgoing(a)||Answer(a)||a==CourtAction.ReportIndependentKeep;
    static string MessageId(CourtInput i)=>HouseholdCourt.Id(i.Action==CourtAction.ReportIndependentKeep?"keep-receipt":Answer(i.Action)?"independence-answer":"independence-public",i.Id.Value).Value;
    public static string Label(CourtAction a)=>a switch{
        CourtAction.JoinDeclaredHouse=>"個人として新しい家に仕える",
        CourtAction.RemainWithHouse=>"今の家に留まる",
        CourtAction.StayNeutral=>"加担を控える",
        _=>throw new ArgumentOutOfRangeException(nameof(a))
    };
    internal static bool Outgoing(CourtAction a)=>a is CourtAction.AnnounceIndependence or CourtAction.SeekDeclaredSupport;
    // 本人が開いた現地点検の依頼状、または本人が知る代表者の宛所だけを使う。
    static EntityId? Address(ContractWorld w,EntityId sender,EntityId recipient,long at,long rev)
    {
        var residence=HouseholdCourt.Events(w).LastOrDefault(e=>e.Input.Action==CourtAction.ReportResidence && e.Actor==recipient &&
            e.Input.Person==sender && e.Revision<rev && MessageFlow.LearnedAt(w.Messaging,
                HouseReforms.Letter(e.Input,HouseReforms.ResidenceNotice),sender.Value) is {} opened && opened<=at);
        var visit=w.SocialWorld?.Events.LastOrDefault(e=>e.Input.Action==SocialWorldAction.Visit && e.Revision<rev &&
            (e.Actor==sender && e.Input.Person==recipient || e.Actor==recipient && e.Input.Person==sender));
        if(visit!=null && (residence==null || visit.Revision>residence.Revision))return visit.Site;
        if(residence!=null)return residence.Place;
        var local=w.Requests.LastOrDefault(r=>r.CommitSequence<rev && r.Intent.Actor==recipient && r.Intent.Executor==sender &&
            r.Intent.Action==ActionKind.Inspect && w.Tasks.Any(t=>t.RequestId==r.Id && w.Messaging.Messages.Any(m=>
                m.RecipientId==sender.Value && m.SenderId==recipient.Value && m.Id=="message:task/"+t.Id.Value.Split(':')[1]+"/order" &&
                MessageFlow.LearnedAt(w.Messaging,m.Id,sender.Value) is {} learned && learned<=at)));
        if(local!=null)return local.Intent.Scope;
        foreach(var house in w.NewGame!.Start.Factions){
            var representative=RepresentativeKnowledge.Known(w,sender,new(house.Id),at,rev-1);
            if(representative?.Person==recipient)return representative.Home;
        }
        return null;
    }
    static CourtInput? ReportFor(ContractWorld w,CourtEvent receipt,long rev)
    {
        if(receipt.Input.Action!=CourtAction.ReceiveIndependentKeep || receipt.Revision>=rev)return null;
        var events=HouseholdCourt.Events(w);
        var joined=events.SingleOrDefault(e=>e.Input.Id==receipt.Input.Reference && e.Input.Action==CourtAction.JoinDeclaredHouse);
        if(joined==null || events.Any(e=>e.Revision<rev && e.Input.Action==CourtAction.ReportIndependentKeep && e.Input.Reference==receipt.Input.Id))return null;
        var call=events.Single(e=>e.Input.Id==joined.Input.Reference && e.Input.Action==CourtAction.SeekDeclaredSupport);
        return new(HouseholdCourt.Id("send-keep-report",receipt.Input.Id.Value),CourtAction.ReportIndependentKeep,call.Actor,
            joined.Input.Owner,call.Place,Reference:receipt.Input.Id);
    }
    public static IEnumerable<CourtInput> KeepReports(ContractWorld w,EntityId actor)
    {
        var site=SocialWorld.Site(w,actor);
        foreach(var receipt in HouseholdCourt.Events(w).Where(e=>e.Actor==actor && e.Place==site && e.Input.Action==CourtAction.ReceiveIndependentKeep))
            if(ReportFor(w,receipt,w.CommitSequence+1) is {} report)yield return report;
    }
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {
        if(i.Action==CourtAction.ReportIndependentKeep){
            var receipt=HouseholdCourt.Events(w).SingleOrDefault(e=>e.Input.Id==i.Reference && e.Actor==actor && e.Place==place);
            if(receipt==null || ReportFor(w,receipt,rev)!=i || !TransportWork.At(w,actor,place,at,rev,true) || Assassination.DeadAt(w,actor,at,rev))
                throw new InvalidDataException("現地で本丸を受領した本人の未報告の原本が必要です");
            return;
        }
        if(Answer(i.Action)){CheckAnswer(w,i,actor,place,at,rev);return;}
        var d=w.Declarations?.SingleOrDefault(d=>d.House==i.Owner && d.Founder==actor && d.Revision<rev);
        if(d==null || i.Reference!=null || i.Person==actor || i.Site==place || !TransportWork.At(w,actor,place,at,rev,true) ||
            Assassination.DeadAt(w,actor,at,rev) || Address(w,actor,i.Person,at,rev)!=i.Site ||
            HouseholdCourt.Events(w).Any(e=>e.Revision<rev && Outgoing(e.Input.Action) && e.Input.Owner==i.Owner && e.Input.Person==i.Person))
            throw new InvalidDataException("独立した本人が他城の既知の宛先に一通だけ宣言を発送できます");
    }
    public static IEnumerable<CourtInput> Choices(ContractWorld w,EntityId actor)
    {
        var place=SocialWorld.Site(w,actor);
        if(place==null)yield break;
        foreach(var d in w.Declarations??[]){
            if(d.Founder!=actor)continue;
            foreach(var p in FamilyBirth.People(w)){
                var recipient=new EntityId(p.Id);
                if(recipient==actor || Address(w,actor,recipient,w.ElapsedSeconds,w.CommitSequence+1) is not {} address || address==place)continue;
                foreach(var action in new[]{CourtAction.AnnounceIndependence,CourtAction.SeekDeclaredSupport}){
                    var input=new CourtInput(HouseholdCourt.Id(action.ToString(),d.House.Value+"/"+p.Id),action,recipient,d.House,address);
                    try{Check(w,input,actor,place.Value,w.ElapsedSeconds,w.CommitSequence+1);}catch(InvalidDataException){continue;}
                    yield return input;
                }
            }
        }
    }
    static void CheckAnswer(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {
        var rows=HouseholdCourt.Events(w).Where(e=>e.Revision<rev).ToArray();
        var call=rows.SingleOrDefault(e=>e.Input.Id==i.Reference && e.Input.Action==CourtAction.SeekDeclaredSupport);
        if(call==null || call.Input.Person!=actor || i.Owner!=call.Input.Owner || i.Person!=call.Actor || i.Site!=place ||
            place!=call.Input.Site || !TransportWork.At(w,actor,place,at,rev,true) || Assassination.DeadAt(w,actor,at,rev) ||
            MessageFlow.LearnedAt(w.Messaging,MessageId(call.Input),actor.Value) is not {} opened || opened>at ||
            rows.Any(e=>Answer(e.Input.Action) && e.Input.Reference==call.Input.Id))
            throw new InvalidDataException("宛名の本人が開いた支持要請に一度だけ返答できます");
        if(i.Action!=CourtAction.JoinDeclaredHouse)return;
        var d=w.Declarations!.Single(d=>d.House==i.Owner);
        var formerHead=HouseSuccession.Head(w,d.FormerHouse,at,rev-1);
        if(actor==formerHead || DefectionPacts.HouseAt(w,actor,at,rev-1)!=d.FormerHouse ||
            !w.ServiceBonds.Any(s=>s.Kind=="service" && s.Subject==actor && s.Target==formerHead &&
                s.From<=at && (at<s.Until || at==s.Until && rev<=s.Revision)))
            throw new InvalidDataException("旧主家の直属家臣だけが自分の奉公先を変えられます");
    }
    public static IEnumerable<CourtInput> Responses(ContractWorld w,EntityId actor)
    {
        var site=SocialWorld.Site(w,actor);if(site==null)yield break;
        foreach(var call in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.SeekDeclaredSupport && e.Input.Person==actor)){
            foreach(var action in new[]{CourtAction.JoinDeclaredHouse,CourtAction.RemainWithHouse,CourtAction.StayNeutral}){
                var i=new CourtInput(HouseholdCourt.Id(action.ToString(),call.Input.Id.Value),action,call.Actor,call.Input.Owner,site.Value,Reference:call.Input.Id);
                try{CheckAnswer(w,i,actor,site.Value,w.ElapsedSeconds,w.CommitSequence+1);}catch(InvalidDataException){continue;}
                yield return i;
            }
        }
    }
    public static IEnumerable<(string Id,string? Body)> Delivered(ContractWorld w,EntityId actor)=>
        w.Messaging.Messages.Where(m=>m.Kind is Kind or Reply or KeepReport && m.RecipientId==actor.Value &&
            w.Messaging.Receipts.Any(r=>r.MessageId==m.Id && r.ReceiverId==actor.Value))
            .Select(m=>(m.Id,MessageFlow.Knows(w.Messaging,m.Id,actor.Value)?m.Body:null));
    internal static IEnumerable<(CourtInput Input,EntityId Actor)> NpcResponses(ContractWorld w)
    {
        foreach(var m in w.Messaging.Messages.Where(m=>m.Kind is Kind or Reply or KeepReport && m.RecipientId!=w.PlayerControl.Value)){
            var actor=new EntityId(m.RecipientId);
            if(SocialWorld.Site(w,actor)?.Value!=m.DeliverySite || Assassination.DeadAt(w,actor,w.ElapsedSeconds,w.CommitSequence) ||
                !w.Messaging.Receipts.Any(r=>r.MessageId==m.Id && r.ReceiverId==actor.Value))continue;
            if(!MessageFlow.Knows(w.Messaging,m.Id,m.RecipientId))
                w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(m.Id,m.RecipientId,w.ElapsedSeconds,m.Id)];
            if(m.Kind!=Kind)continue;
            var choices=Responses(w,actor).Where(i=>i.Reference?.Value==m.CorrelationId).ToArray();
            if(choices.Length==0)continue;
            var trust=SocialWorld.ConsentBonus(w,actor,new(m.SenderId),w.ElapsedSeconds,w.CommitSequence);
            yield return(choices.FirstOrDefault(i=>i.Action==CourtAction.JoinDeclaredHouse && trust>=2) ??
                choices.Single(i=>i.Action==(trust<0?CourtAction.RemainWithHouse:CourtAction.StayNeutral)),actor);
        }
    }
    static MessageEnvelope Original(ContractWorld w,CourtEvent e)
    {
        if(e.Input.Action==CourtAction.ReportIndependentKeep){
            var receipt=HouseholdCourt.Events(w).Single(r=>r.Input.Id==e.Input.Reference);
            var date=DateOnly.ParseExact(w.EpochDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture).AddDays((int)(receipt.At/86400));
            var receiptText=$"{date:yyyy年M月d日}、{NewGameWorld.SiteName(receipt.Place.Value)}の本丸を受領しました。";
            return new(MessageId(e.Input),receipt.Input.Id.Value,KeepReport,e.Actor.Value,e.Input.Person.Value,receiptText,
                e.Input.Site.Value,e.At){DeliverySite=e.Input.Site.Value};
        }
        if(Answer(e.Input.Action)){
            var call=HouseholdCourt.Events(w).Single(c=>c.Input.Id==e.Input.Reference);
            return new(MessageId(e.Input),call.Input.Id.Value,Reply,e.Actor.Value,call.Actor.Value,Label(e.Input.Action)+"と返答します。",
                call.Place.Value,e.At){DeliverySite=call.Place.Value};
        }
        var name=FamilyBirth.People(w).Single(p=>p.Id==e.Actor.Value).Name;
        var body=name+"は独立を宣言しました。"+(e.Input.Action==CourtAction.SeekDeclaredSupport?"貴殿の支持を求めます。":"この書状は独立の通知です。");
        return new(MessageId(e.Input),e.Input.Id.Value,Kind,
            e.Actor.Value,e.Input.Person.Value,body,e.Input.Site.Value,e.At){DeliverySite=e.Input.Site.Value};
    }
    internal static void Send(ContractWorld w,CourtEvent e)
    {
        var letter=Original(w,e);MessageFlow.Send(w.Messaging,letter,e.At);
        w.SealedMessages=[..w.SealedMessages,new(letter.Id)];RegionalCampaign.Bind(w);
    }
    internal static void Validate(ContractWorld w,CourtEvent e)
    {
        var expected=Original(w,e);
        if(!w.Messaging.Messages.Contains(expected) || !w.SealedMessages.Contains(new(expected.Id)))
            throw new InvalidDataException("独立宣言の宛先または公開内容が発送原本と異なります");
    }
}
