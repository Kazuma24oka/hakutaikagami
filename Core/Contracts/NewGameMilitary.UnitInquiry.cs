using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record UnitInquiryState(string Contract,TroopDispatch[] Dispatches,TroopReceipt[] Receipts,TroopReply[] Replies);
public sealed record UnitPostInquiry(EntityId Request,string Role,string[] Originals);
public sealed record UnitOriginalStatus(string Original,bool Received,bool Applied,long? ReceivedAt);
public sealed record UnitPostInquiryAnswer(string Inquiry,long At,string Unit,int Soldiers,string RoutPhase,bool Moving,bool AtHome,FieldPoint Position,UnitOriginalStatus[] Originals);
public static partial class NewGameMilitary
{
    public const string UnitInquiryContract="receiver-own-record-physical-query-quote-not-original-receipt-v1";
    public const string UnitInquiryRole="standing-unit-reception/status-only";
    static bool UnitInquiryAction(NewMilitaryAction a)=>a is NewMilitaryAction.InquireUnitKnown or NewMilitaryAction.InquireUnitHome;
    public static bool UnitInquiryAllowed(ContractWorld w,NewMilitaryAction a)=>UnitInquiryAction(a)&&Detached(w)&&Deployed(w)&&CustodyAt(w,w.ElapsedSeconds,w.CommitSequence);
    static void ApplyUnitInquiry(ContractWorld w,NewMilitaryInput i)
    {
        if(!UnitInquiryAction(i.Action))return;
        var x=w.NewMilitary!.Withdrawal!;var sight=x.Sightings.Last();var from=PersonPosition(w,w.ElapsedSeconds);var to=i.Action==NewMilitaryAction.InquireUnitHome?Home(w,0):sight.Position;
        var route=RegionalGround.Plan(from,to,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(checked(w.ElapsedSeconds+1),route.LastOrDefault()?.EndAt??0);string message=UnitMessage(i.Id,"inquiry");
        // Only originals already authored by this sender in this loan case can be requested.
        var originals=x.Dispatches.Select(d=>d.Message).Distinct(StringComparer.Ordinal).ToArray();
        MessageFlow.Send(w.Messaging,new(message,i.Id.Value,"unit-post-inquiry",w.PlayerControl.Value,UnitIds[0].Value,Text(new UnitPostInquiry(i.Id,UnitInquiryRole,originals)),w.NewMilitary.Terms.Site.Value,w.ElapsedSeconds),w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));
        var s=w.NewMilitary.UnitInquiry??new(UnitInquiryContract,[],[],[]);
        w.NewMilitary=w.NewMilitary with{UnitInquiry=s with{Dispatches=[..s.Dispatches,new(i.Id,w.ElapsedSeconds,w.CommitSequence,UnitIds[0],UnitInquiryRole,i.Action==NewMilitaryAction.InquireUnitHome?w.NewMilitary.Terms.Site:sight.Id,from,to,route,due,message,MessageFlow.Latest(w.Messaging,message).Id)]}};
    }
    static UnitPostInquiryAnswer UnitQueryAnswer(ContractWorld w,TroopDispatch d)
    {
        var query=ContractJson.Read<UnitPostInquiry>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==d.Message).Body));
        var rows=query.Originals.Select(id=>{
            var receipt=w.Messaging.Receipts.SingleOrDefault(r=>r.MessageId==id&&r.PrincipalId==d.Recipient.Value);
            bool applied=UnitReceipts(w).Any(r=>r.Applied&&UnitDispatches(w).Single(t=>t.Id==r.Id).Message==id);
            return new UnitOriginalStatus(id,receipt!=null,applied,receipt?.ReceivedAt);
        }).ToArray();
        var position=Position(w,0,w.ElapsedSeconds);
        return new(d.Message,w.ElapsedSeconds,d.Recipient.Value,StrengthAt(w,0,w.ElapsedSeconds),RoutPhase(w,0),Position(w,0,w.ElapsedSeconds+1)!=position,position==Home(w,0),position,rows);
    }
    static UnitInquiryState? UnitInquiryBefore(UnitInquiryState? s,long at,long rev){if(s==null)return null;var dispatches=s.Dispatches.Where(d=>Before(d.At,d.Revision,at,rev)).ToArray();return dispatches.Length==0?null:new(s.Contract,dispatches,s.Receipts.Where(d=>Before(d.At,d.Revision,at,rev)).ToArray(),s.Replies.Where(d=>Before(d.At,d.Revision,at,rev)).ToArray());}
    static void ValidateUnitInquiry(ContractWorld w)
    {
        var s=w.NewMilitary!;var x=s.UnitInquiry;Need((x!=null)==s.Requests.Any(q=>UnitInquiryAction(q.Input.Action)),"部隊照会と原判断が一致しません");if(x==null)return;
        Need(x.Contract==UnitInquiryContract&&x.Dispatches.Length==s.Requests.Count(q=>UnitInquiryAction(q.Input.Action)),"部隊照会の原判断が欠落しています");
        Need(x.Receipts.All(r=>!r.Applied)&&x.Receipts.Length==x.Dispatches.Count(d=>d.DueAt<=w.ElapsedSeconds)&&x.Replies.Length==x.Receipts.Count(r=>r.ReplyDueAt<=w.ElapsedSeconds),"照会で命令を実行したか、物理返書が欠落しています");
        Need(x.Receipts.Select(r=>r.Id).Distinct().Count()==x.Receipts.Length&&x.Replies.Select(r=>r.Id).Distinct().Count()==x.Replies.Length,"照会受領が重複しています");
    }
    public static string UnitInquiryStatus(ContractWorld w)
    {
        var x=w.NewMilitary?.UnitInquiry;var d=x?.Dispatches.LastOrDefault();if(d==null)return "";
        var r=x!.Receipts.SingleOrDefault(r=>r.Id==d.Id);if(r==null||!MessageFlow.Knows(w.Messaging,r.ReplyMessage,w.PlayerControl.Value))return "本隊への照会返書は未受領";
        if(!r.Delivered)return "照会伝令は指定場所で本隊に会えなかった";
        var reply=ContractJson.Read<UnitPostInquiryAnswer>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==r.ReplyMessage).Body));
        return "本隊の返書（開始"+reply.At+"秒）："+reply.Soldiers+"人・"+(reply.AtHome?"帰還拠点":reply.Moving?"移動中":"野外待機");
    }
}
