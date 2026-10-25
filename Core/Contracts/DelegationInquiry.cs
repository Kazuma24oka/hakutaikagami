using HakutaiKagami.Core.Messaging;
using System.Text.Json.Serialization;
namespace HakutaiKagami.Core.Contracts;
public sealed record DelegationQuestion(EntityId Id,EntityId Directive,bool Destination,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] bool Resend=false);
public sealed record DelegationAnswer(EntityId Query,EntityId Directive,long At,string Receipt,string Progress,string? Arrival,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string[]? Retried=null);
public sealed record DelegationInquiryRow(EntityId Query,EntityId Directive,string State,string? Receipt,string? Progress,long? ReportedAt,int? RetriedCount=null,bool OriginalReceived=false);
public static class DelegationInquiry
{
    public const string QueryKind="region-query",ReplyKind="region-query-reply";
    static string Hash(string id)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(id));
    public static string MessageId(EntityId id)=>"message:delegation-query/"+Hash(id.Value);
    static string ReplyId(EntityId id)=>"message:delegation-answer/"+Hash(id.Value);
    static string Text<T>(T value)=>System.Text.Encoding.UTF8.GetString(ContractJson.Write(value));
    static T Read<T>(MessageEnvelope m)=>ContractJson.Read<T>(System.Text.Encoding.UTF8.GetBytes(m.Body));
    internal static FieldPoint Address(ContractWorld w,DelegationQuestion q){var c=w.Region!.Commands.Single(c=>c.Request.Id==q.Directive);return q.Destination&&c.Request.Action==RegionalAction.Move?c.Request.A:w.Delegation!.Terms.KnownAddress;}
    internal static void Send(ContractWorld w,DelegationQuestion q,EntityId actor)
    {
        if(!DelegationMission.Enabled(w)||actor!=w.PlayerControl||!EntityId.IsValid(q.Id.Value)||!w.Region!.Commands.Any(c=>c.Request.Id==q.Directive&&DelegationMission.Remote(w,c.Request)))throw new InvalidDataException("Only a personally issued delegated directive may be queried");
        if(w.Region.Commands.Any(c=>c.Request.Id==q.Id)||w.Requests.Any(r=>r.Id==q.Id)||w.Field!.Orders.Any(o=>o.Request.Id==q.Id)||w.TravelDecisions!.Any(d=>d.Request.Id==q.Id))throw new InvalidDataException("Query request ID already used");
        RegionalBattle.Send(w,new(MessageId(q.Id),q.Directive.Value,QueryKind,actor.Value,ContractFixture.Clerk.Value,Text(q),ContractFixture.Castle.Value,w.ElapsedSeconds),RegionalBattle.Position(w,actor),Address(w,q));
    }
    internal static void Update(ContractWorld w)
    {
        if(!DelegationMission.Enabled(w))return;
        foreach(var message in w.Messaging.Messages.Where(m=>m.Kind==QueryKind&&MessageFlow.Knows(w.Messaging,m.Id,ContractFixture.Clerk.Value)).ToArray())
        {
            var q=Read<DelegationQuestion>(message);if(w.Messaging.Messages.Any(m=>m.Id==ReplyId(q.Id)))continue;
            var c=w.Region!.Commands.Single(c=>c.Request.Id==q.Directive);bool recognized=MessageFlow.Knows(w.Messaging,c.Message,c.Request.Recipient.Value);var arrival=recognized?w.Delegation!.Arrivals.SingleOrDefault(a=>a.Directive==q.Directive):null;
            string receipt=recognized?c.Result:"unrecognized";string progress=!recognized?"命令をまだ認識していない申告":c.Result!="applied"?"未着手の申告":arrival!=null?"到達済みの申告":DelegationMission.HasAppliedReplacement(w,q.Directive)?"別計画へ変更済みの申告":w.Region.Units.Where(u=>DelegationMission.Units.Contains(u.Id)).Any(u=>u.Phase=="blocked")?"通行・接触により停止の申告":w.Region.Units.Where(u=>DelegationMission.Units.Contains(u.Id)).Any(u=>u.Route.Any(l=>l.EndAt>w.ElapsedSeconds))?"行動中の申告":"停止中の申告";
            var retried=q.Resend?DelegationReturnRetry.Apply(w,q):null;var answer=new DelegationAnswer(q.Id,q.Directive,w.ElapsedSeconds,receipt,progress,arrival?.Message,retried);var original=DelegationTransport.ReturnDelivery(w,message.Id);
            RegionalBattle.Send(w,new(ReplyId(q.Id),q.Directive.Value,ReplyKind,ContractFixture.Clerk.Value,w.PlayerControl.Value,Text(answer),ContractFixture.Castle.Value,w.ElapsedSeconds),RegionalBattle.Position(w,ContractFixture.Clerk),original.Origin);
        }
    }
    static DelegationAnswer[] Known(ContractWorld w,EntityId observer)=>!DelegationMission.Enabled(w)||observer!=w.PlayerControl?[]:w.Messaging.Messages.Where(m=>m.Kind==ReplyKind&&MessageFlow.Knows(w.Messaging,m.Id,observer.Value)).Select(Read<DelegationAnswer>).OrderBy(a=>a.At).ThenBy(a=>a.Query.Value,StringComparer.Ordinal).ToArray();
    internal static DelegationAnswer? LatestKnown(ContractWorld w,EntityId directive,EntityId observer)=>Known(w,observer).LastOrDefault(a=>a.Directive==directive);
    internal static bool KnowsArrival(ContractWorld w,DelegationArrival r,EntityId observer)=>MessageFlow.Knows(w.Messaging,r.Message,observer.Value)||Known(w,observer).Any(a=>a.Directive==r.Directive&&a.Arrival==r.Message);
    public static DelegationInquiryRow[] View(WorldSession session,EntityId observer)
    {
        var w=session.DiagnosticSnapshot();if(!DelegationMission.Enabled(w)||observer!=w.PlayerControl)return [];
        return w.Messaging.Messages.Where(m=>m.Kind==QueryKind&&m.SenderId==observer.Value).Select(m=>{var q=Read<DelegationQuestion>(m);var answer=Known(w,observer).SingleOrDefault(a=>a.Query==q.Id);string state=answer!=null?"照会返答を受領":MessageFlow.FailureKnown(w.Messaging,MessageFlow.Latest(w.Messaging,m.Id).Id,observer.Value)?"照会不達の報告を受領":"照会発送済・返答未確認";return new DelegationInquiryRow(q.Id,q.Directive,state,answer?.Receipt,answer?.Progress,answer?.At,answer?.Retried?.Length,w.Delegation!.Arrivals.Any(r=>r.Directive==q.Directive&&MessageFlow.Knows(w.Messaging,r.Message,observer.Value)&&w.Messaging.Receipts.Any(x=>x.MessageId==r.Message&&x.PrincipalId==observer.Value)));}).ToArray();
    }
    public static void Validate(ContractWorld w)
    {
        var queries=w.Messaging.Messages.Where(m=>m.Kind==QueryKind).ToArray();var replies=w.Messaging.Messages.Where(m=>m.Kind==ReplyKind).ToArray();if(!DelegationMission.Enabled(w)){if(queries.Length+replies.Length>0)throw new InvalidDataException("Inquiry requires delegation profile");return;}
        foreach(var m in queries){var q=Read<DelegationQuestion>(m);var c=w.Region!.Commands.Single(c=>c.Request.Id==q.Directive);if(!EntityId.IsValid(q.Id.Value)||m.Id!=MessageId(q.Id)||m.CorrelationId!=q.Directive.Value||m.SenderId!=w.PlayerControl.Value||m.RecipientId!=ContractFixture.Clerk.Value||m.Scope!=ContractFixture.Castle.Value||m.CreatedAt<c.IssuedAt||m.CreatedAt>w.ElapsedSeconds||!DelegationMission.Remote(w,c.Request)||Text(q)!=m.Body||w.Region.Deliveries.Where(d=>w.Messaging.Attempts.Single(a=>a.Id==d.Attempt).MessageId==m.Id).Any(d=>d.Address!=Address(w,q)))throw new InvalidDataException("Inquiry exceeds issued purpose or declared destination");bool received=MessageFlow.Knows(w.Messaging,m.Id,m.RecipientId);if(replies.Count(r=>r.Id==ReplyId(q.Id))!=(received?1:0))throw new InvalidDataException("Missing or premature inquiry reply");}
        var allowed=new[]{"命令をまだ認識していない申告","未着手の申告","到達済みの申告","別計画へ変更済みの申告","通行・接触により停止の申告","行動中の申告","停止中の申告"};
        foreach(var m in replies){var a=Read<DelegationAnswer>(m);var q=queries.Single(m=>m.Id==MessageId(a.Query));var payload=Read<DelegationQuestion>(q);var c=w.Region!.Commands.Single(c=>c.Request.Id==a.Directive);bool recognized=c.RecognizedAt is {} at&&at<=a.At;if(m.Id!=ReplyId(a.Query)||a.Directive!=payload.Directive||a.At!=m.CreatedAt||a.At>w.ElapsedSeconds||MessageFlow.LearnedAt(w.Messaging,q.Id,q.RecipientId) is not {} learned||learned>a.At||m.SenderId!=q.RecipientId||m.RecipientId!=q.SenderId||m.CorrelationId!=q.CorrelationId||m.Scope!=q.Scope||Text(a)!=m.Body||a.Receipt!=(recognized?c.Result:"unrecognized")||!allowed.Contains(a.Progress)||!recognized&&a.Progress!="命令をまだ認識していない申告")throw new InvalidDataException("Inquiry reply lacks received question or own recognition");if(a.Arrival!=null&&!w.Delegation!.Arrivals.Any(r=>r.Directive==a.Directive&&r.Message==a.Arrival&&r.At<=a.At))throw new InvalidDataException("Reply cites a nonexistent or future arrival");if((a.Arrival!=null)!=(a.Progress=="到達済みの申告"))throw new InvalidDataException("Arrival claim lacks its immutable report");var returnTo=DelegationTransport.ReturnDelivery(w,q.Id).Origin;if(w.Region!.Deliveries.Where(d=>w.Messaging.Attempts.Single(x=>x.Id==d.Attempt).MessageId==m.Id).Any(d=>d.Address!=returnTo))throw new InvalidDataException("Inquiry reply tracks an unknown address");}
        DelegationReturnRetry.Validate(w,replies);
    }
}
