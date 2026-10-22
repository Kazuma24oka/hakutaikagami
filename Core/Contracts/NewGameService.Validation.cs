using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public static partial class NewGameService
{
    public static void Validate(ContractWorld w)
    {
        if(w.Service==null){if(NewGameWorld.Enabled(w))Need(!w.Messaging.Messages.Any(m=>m.Kind is "service-request" or "service-notice")&&w.IdCounters.GetValueOrDefault("service-event")==0,"仕官書状だけが残っています");return;}Need(NewGameWorld.Enabled(w),"仕官案件は新規開始の同一人物だけに結合します");var s=w.Service;Need(s.Schema==1&&s.Contract==Contract,"仕官契約の版が違います");
        Need(s.Cases.Select(c=>c.Id).Distinct().Count()==s.Cases.Length&&s.Requests.Select(q=>q.Input.Id).Distinct().Count()==s.Requests.Length&&s.Events.Select(e=>e.Message).Distinct().Count()==s.Events.Length&&s.Openings.Select(o=>o.Message).Distinct().Count()==s.Openings.Length,"仕官台帳のID重複");
        Need(w.Messaging.Messages.Count(m=>m.Kind=="service-request")==s.Requests.Length&&w.Messaging.Messages.Count(m=>m.Kind=="service-notice")==s.Events.Length&&w.IdCounters.GetValueOrDefault("service-event")==s.Events.Length,"仕官と書状の台帳が不一致です");
        foreach(var c in s.Cases){Need(EntityId.IsValid(c.Id.Value)&&c.Principal!=w.PlayerControl&&w.Characters.Any(p=>p.Id==c.Principal&&p.Life==LifeState.Adult),"仕官相手が不正です");if(c.Initial)Need(c==Initial(w),"開始時の直属案件が不正です");else {var app=s.Requests.Single(q=>q.Case==c.Id&&q.Input.Action==CareerAction.Apply);var house=w.NewGame!.Start.Factions.Single(f=>f.Head==c.Principal.Value);Need(c.Id==app.Input.Id&&c.Principal==app.Input.Principal&&c.House.Value==house.Id&&LocalAt(w,c,app.At),"申し出時の相手・勢力・物理面会が一致しません");}}
        foreach(var q in s.Requests){
            var c=s.Cases.Single(c=>c.Id==q.Case);var m=w.Messaging.Messages.Single(m=>m.Id==q.Message);var v=View(w,q.At,q.Revision-1);Need(Enum.IsDefined(q.Input.Action)&&EntityId.IsValid(q.Input.Id.Value)&&q.At>=0&&q.At<=w.ElapsedSeconds&&q.Revision>=1&&q.Revision<=w.CommitSequence,"仕官入力の時点・種類が不正です");
            Need(!w.Requests.Any(r=>r.Id==q.Input.Id)&&w.TravelDecisions?.Any(d=>d.Request.Id==q.Input.Id)!=true&&!w.SealedMessages.Any(id=>id.Value==q.Message),"仕官入力IDの競合・封書方針が不正です");
            Need(q.Message==RequestId(q.Input.Id)&&m.Kind=="service-request"&&m.CorrelationId==c.Id.Value&&m.Body==Text(q.Input)&&m.SenderId==w.PlayerControl.Value&&m.RecipientId==c.Principal.Value&&m.Scope==c.Site.Value&&m.CreatedAt==q.At,"申し出の原文・相手・発送時点が不正です");
            if(q.Input.Action==CareerAction.Apply)Need(!c.Initial&&q.Input.Case==null&&q.Input.Principal==c.Principal&&v.CanApply&&LocalAt(w,c,q.At),"申し出前の資格・面会がありません");
            else{Need(q.Input.Case==c.Id&&q.Input.Principal==null&&(c.Initial||s.Requests.Any(a=>a.Case==c.Id&&a.Input.Action==CareerAction.Apply&&a.Revision<q.Revision)),"自分の過去の案件ではありません");var card=v.Cases.Single(x=>x.Case==c.Id);Need(q.Input.Action switch{CareerAction.Accept=>card.CanAccept,CareerAction.Decline=>card.CanDecline,CareerAction.RequestRelease=>card.CanRelease,CareerAction.Inquire=>true,_=>false},"未開封条件に回答したか、未認識の所属を使っています");}
            long? due=q.Input.Action==CareerAction.Accept?Event(w,c.Id,CareerEventKind.Offered)!.At+OfferSeconds-1:null;Need(m.DeliverBy==due,"回答の到達期限が違います");
        }
        foreach(var o in s.Openings){var m=w.Messaging.Messages.Single(m=>m.Id==o.Message);Need(m.Kind=="service-notice"&&m.RecipientId==w.PlayerControl.Value&&o.At>=0&&o.At<=w.ElapsedSeconds&&o.Revision>=1&&o.Revision<=w.CommitSequence&&MessageFlow.LearnedAt(w.Messaging,m.Id,w.PlayerControl.Value)==o.At&&s.Events.Any(e=>e.Message==m.Id&&e.Revision<=o.Revision),"通知の物理受領・開封根拠がありません");}
        for(int n=0;n<s.Events.Length;n++){
            var e=s.Events[n];var c=s.Cases.Single(c=>c.Id==e.Case);var m=w.Messaging.Messages.Single(m=>m.Id==e.Message);Need(e.Id==new EntityId("service-event:"+(n+1))&&e.At>=0&&e.At<=w.ElapsedSeconds&&e.Revision>=1&&e.Revision<=w.CommitSequence&&(n==0||Before(s.Events[n-1],e.At,e.Revision)),"仕官出来事の順序が違います");
            var req=s.Requests.SingleOrDefault(q=>q.Message==e.Related);bool Source(CareerAction a)=>req!=null&&req.Case==c.Id&&req.Input.Action==a&&req.Revision<e.Revision&&Received(w,req.Message,c,e.At);
            bool Resolved()=>s.Events.Any(x=>x.Case==c.Id&&x.Kind is CareerEventKind.Joined or CareerEventKind.Declined or CareerEventKind.Refused or CareerEventKind.Expired&&x.Revision<e.Revision);
            var offer=Event(w,c.Id,CareerEventKind.Offered);CareerStatement statement;long? effective=null,until=null;
            switch(e.Kind){
                case CareerEventKind.Offered:Need(!c.Initial&&Source(CareerAction.Apply)&&!Cases(w).Any(other=>Active(w,other,e.At,e.Revision-1)),"受領した申し出・空き所属がありません");statement=CareerStatement.Offered;until=e.At+OfferSeconds;break;
                case CareerEventKind.Refused:Need(!c.Initial&&Source(CareerAction.Apply)&&Cases(w).Any(other=>Active(w,other,e.At,e.Revision-1)),"不承諾の根拠がありません");statement=CareerStatement.Refused;break;
                case CareerEventKind.Joined:Need(!c.Initial&&offer!=null&&offer.Revision<e.Revision&&Source(CareerAction.Accept)&&e.At<offer.At+OfferSeconds&&!Resolved(),"合意の到達・期限が不正です");statement=CareerStatement.Agreed;effective=e.At;break;
                case CareerEventKind.Declined:Need(!c.Initial&&offer!=null&&Source(CareerAction.Decline)&&!Resolved(),"辞退の受領がありません");statement=CareerStatement.Declined;break;
                case CareerEventKind.Expired:Need(!c.Initial&&offer!=null&&e.At>=offer.At+OfferSeconds&&!Resolved()&&(e.Related==offer.Message||Source(CareerAction.Accept)),"期限切れの根拠がありません");statement=CareerStatement.Expired;break;
                case CareerEventKind.ReleaseWaiting:Need(Source(CareerAction.RequestRelease)&&Active(w,c,e.At,e.Revision-1)&&!ReportsKnown(w,c,e.At,e.Revision-1),"未着返報の引継ぎ待ちではありません");statement=CareerStatement.AwaitingReports;break;
                case CareerEventKind.Released:Need(Source(CareerAction.RequestRelease)&&Active(w,c,e.At,e.Revision-1)&&e.At>Joined(w,c)!.At&&ReportsKnown(w,c,e.At,e.Revision-1)&&FreeOfOffice(w),"辞去の受領・実際の返報・役職引継ぎが不足しています");statement=CareerStatement.Ended;effective=e.At;break;
                case CareerEventKind.StatusSent:Need(Source(CareerAction.Inquire),"照会原本がありません");var status=Status(w,c,e.At,e.Revision-1);statement=status.State;effective=status.Effective;break;
                default:throw new InvalidDataException("未対応の仕官出来事です");
            }
            Need(e.Message==NoticeId(c.Id,e.Kind,e.Related)&&m.Kind=="service-notice"&&m.CorrelationId==c.Id.Value&&m.SenderId==c.Principal.Value&&m.RecipientId==w.PlayerControl.Value&&m.Scope==c.Site.Value&&m.CreatedAt==e.At&&m.DeliverBy==null&&m.Body==Text(new CareerNotice(c.Id,statement,effective,until,e.Related))&&w.SealedMessages.Any(id=>id.Value==e.Message),"通知の原文・効力時点・封書が不正です");
        }
        foreach(var c in s.Cases)foreach(var kind in Enum.GetValues<CareerEventKind>().Where(k=>k!=CareerEventKind.StatusSent))Need(s.Events.Count(e=>e.Case==c.Id&&e.Kind==kind)<=1,"同じ仕官効果が重複しています");
    }
}
