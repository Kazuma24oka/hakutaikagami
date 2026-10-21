using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public static partial class Career
{
    public static CareerView View(WorldSession session,EntityId observer)=>View(session.DiagnosticSnapshot(),observer);
    internal static CareerView View(ContractWorld w,EntityId observer,long? at=null,long? revision=null)
    {
        if(!Enabled(w)||observer!=w.PlayerControl)return new("未確認",false,[]);
        long time=at??w.ElapsedSeconds,rev=revision??w.CommitSequence;bool Prior(long t,long r)=>t<time||t==time&&r<=rev;
        bool ReadAt(string id)=>Known(w,id,observer)&&w.Career!.Openings.Any(o=>o.Message==id&&Prior(o.At,o.Revision));
        var notices=w.Messaging.Messages.Where(m=>m.Kind=="career-notice"&&m.RecipientId==observer.Value&&ReadAt(m.Id)).Select(m=>(Message:m,Body:Read<CareerNotice>(m))).ToArray();
        var relation=notices.Where(n=>n.Body.Statement is CareerStatement.Agreed or CareerStatement.Ended).OrderBy(n=>n.Body.EffectiveAt).ThenBy(n=>n.Body.Statement==CareerStatement.Agreed?Event(w,n.Body.Case,CareerEventKind.Joined)!.Revision:End(w,n.Body.Case)!.Revision).LastOrDefault();
        bool employed=relation.Body?.Statement==CareerStatement.Agreed;EntityId? knownCase=employed?relation.Body!.Case:null;
        var cards=new List<CareerCard>();bool pending=false;
        foreach(var app in w.Career!.Requests.Where(r=>r.Input.Action==CareerAction.Apply&&Prior(r.At,r.Revision))){
            var own=w.Career.Requests.Where(r=>r.Case==app.Case&&Prior(r.At,r.Revision)).ToArray();var known=notices.Where(n=>n.Body.Case==app.Case).OrderBy(n=>n.Message.CreatedAt).ThenBy(n=>n.Message.Id,StringComparer.Ordinal).ToArray();var last=known.LastOrDefault();var offer=known.LastOrDefault(n=>n.Body.Statement==CareerStatement.Offered);
            bool answered=own.Any(r=>r.Input.Action is CareerAction.Accept or CareerAction.Decline);bool offerValid=offer.Body!=null&&time<offer.Body.OfferUntil;
            bool closed=known.Any(n=>n.Body.Statement is CareerStatement.Refused or CareerStatement.Declined or CareerStatement.Expired or CareerStatement.Ended);
            bool release=knownCase==app.Case&&!own.Any(r=>r.Input.Action==CareerAction.RequestRelease);
            string status=last.Body?.Statement switch{CareerStatement.Offered=>answered?"回答発送・成立通知未確認（回答到達は"+offer.Body!.OfferUntil+"秒未満）":offerValid?"条件：織田家所属・信長直属 / 回答到達は"+offer.Body!.OfferUntil+"秒未満":"条件の回答期限を経過",CareerStatement.Agreed=>"仕官成立の通知を認識",CareerStatement.Ended=>"関係終了の通知を認識",CareerStatement.Refused=>"不承諾の返答を認識",CareerStatement.Declined=>"辞退の確認を認識",CareerStatement.Expired=>"期限切れ不成立の通知を認識",CareerStatement.AwaitingReports=>"引継ぎの返報確認待ち",_=>"申し出発送・結果未確認"};
            if(own.Any(r=>r.Input.Action==CareerAction.RequestRelease)&&!known.Any(n=>n.Body.Statement is CareerStatement.Ended or CareerStatement.AwaitingReports))status="辞去願い発送・返答未確認";
            var unopened=time!=w.ElapsedSeconds||rev!=w.CommitSequence?null:w.Messaging.Messages.Where(m=>m.Kind=="career-notice"&&m.CorrelationId==app.Case.Value&&m.RecipientId==observer.Value&&!Known(w,m.Id,observer)&&w.Messaging.Attempts.Any(a=>a.MessageId==m.Id&&a.Status==AttemptStatus.Delivered&&a.ReceiverId==observer.Value)).OrderBy(m=>m.CreatedAt).FirstOrDefault();
            cards.Add(new(app.Case,status,unopened==null?null:new EntityId(unopened.Id),offerValid&&!answered&&!closed,offerValid&&!answered&&!closed,release));
            if(!closed&&(last.Body==null||last.Body.Statement!=CareerStatement.Agreed)&&(answered||offer.Body==null||offerValid))pending=true;
        }
        return new(employed?"織田家所属・信長直属（本人が通知を認識）":"浪人（本人の認識）",!employed&&!pending,cards.ToArray());
    }
}
