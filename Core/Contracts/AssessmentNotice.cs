using System.Text;
using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public static class AssessmentNotice
{
    public const string Ruleset="officer-assessment-notice-v1";
    public const string Contract="sealed-evidence-snapshot-revision-chain-physical-response-v1";
    public const string Kind="assessment-notice",ResponseKind="assessment-response";
    public enum Reply { Received, RequestReview }
    public sealed record Payload(EntityId Task,EntityId Assessor,EntityId Recipient,int Version,EntityId? Supersedes,MissionReview.Review Review);
    public sealed record Response(EntityId Notice,Reply Reply);
    public sealed record Card(EntityId Message,bool CanOpen,Payload? Known);
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    public static bool Available(ContractWorld w)=>Enabled(w)||NewGameWork.Enabled(w);
    public static ContractWorld Create(){var w=MissionReview.Create();w.WorldId=new("world:"+Ruleset);w.Definitions=w.Definitions with{Id=new("scenario:"+Ruleset),Ruleset=Ruleset};w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};ContractValidation.Validate(w);return w;}
    internal static Payload Read(string body)=>ContractJson.Read<Payload>(Encoding.UTF8.GetBytes(body));
    internal static Response ReadResponse(string body)=>ContractJson.Read<Response>(Encoding.UTF8.GetBytes(body));
    internal static string Body<T>(T value)=>Encoding.UTF8.GetString(ContractJson.Write(value));
    internal static bool KnowsAt(ContractWorld w,string message,EntityId actor,long at)=>w.Messaging.Knowledge.Any(k=>k.MessageId==message&&k.CharacterId==actor.Value&&k.LearnedAt<=at);
    internal static MessageEnvelope[] Authored(ContractWorld w,EntityId task,EntityId assessor)=>w.Messaging.Messages.Where(m=>m.Kind==Kind&&m.SenderId==assessor.Value&&Read(m.Body).Task==task).OrderBy(m=>Read(m.Body).Version).ToArray();
    internal static MissionReview.Review ReviewAt(ContractWorld w,TaskState t,EntityId assessor,long at)
    {
        VisibleMessage? Known(EntityId id){var m=w.Messaging.Messages.SingleOrDefault(m=>m.Id==id.Value);return m!=null&&KnowsAt(w,m.Id,assessor,at)?new(id,m.SenderId,false,m.Body):null;}
        var review=MissionReview.FromClaims(t.Id,Known(WorldSession.Child("message",t.Id,"report")),Known(MissionReview.ExplanationId(t.Id)));return NewGameWork.Review(w,t,assessor,at,review);
    }
    public static EntityId NextMessageId(WorldSession session,EntityId task,EntityId assessor)=>new("message:assessment/"+task.Value.Replace(':','/')+"/"+(Authored(session.DiagnosticSnapshot(),task,assessor).Length+1));
    public static bool CanPublish(WorldSession session,EntityId task,EntityId assessor)
    {
        var w=session.DiagnosticSnapshot();var t=w.Tasks.SingleOrDefault(t=>t.Id==task);
        if(!Available(w)||t==null||t.Action!=ActionKind.Inspect||t.Issuer!=assessor||!w.Characters.Any(c=>c.Id==assessor&&c.Life==LifeState.Adult)||!w.Grants.Any(g=>g.Agent==assessor&&g.Scope==t.Scope&&g.Actions.Contains(ActionKind.Inspect)&&g.From<=w.ElapsedSeconds&&w.ElapsedSeconds<g.Until))return false;
        var review=ReviewAt(w,t,assessor,w.ElapsedSeconds);var chain=Authored(w,task,assessor);
        return review.Achievement!="結果未確認"&&(chain.Length==0||ContractJson.CanonicalHash(Read(chain[^1].Body).Review)!=ContractJson.CanonicalHash(review));
    }
    public static Card[] Inbox(WorldSession session,EntityId actor)
    {
        var v=session.GetActorView(actor);var w=session.DiagnosticSnapshot();
        return v.Messages.Where(m=>w.Messaging.Messages.Single(x=>x.Id==m.Id.Value).Kind==Kind)
            .OrderBy(m=>w.Messaging.Attempts.Where(a=>a.MessageId==m.Id.Value&&a.ReceiverId==actor.Value&&a.Status==AttemptStatus.Delivered).Max(a=>a.FinishedAt))
            .Select(m=>new Card(m.Id,m.Sealed,m.KnownBody==null?null:Read(m.KnownBody))).ToArray();
    }
    public static Payload[] LatestKnown(WorldSession session,EntityId actor)=>Inbox(session,actor).Where(c=>c.Known!=null).Select(c=>c.Known!).GroupBy(c=>c.Task).Select(g=>g.OrderByDescending(c=>c.Version).First()).OrderBy(c=>c.Task.Value,StringComparer.Ordinal).ToArray();
    public static Response[] KnownResponses(WorldSession session,EntityId assessor)
    {
        var v=session.GetActorView(assessor);var w=session.DiagnosticSnapshot();
        return v.Messages.Where(m=>m.KnownBody!=null&&w.Messaging.Messages.Single(x=>x.Id==m.Id.Value).Kind==ResponseKind).Select(m=>ReadResponse(m.KnownBody!)).ToArray();
    }
    public static void Validate(ContractWorld w)
    {
        foreach(var group in w.Messaging.Messages.Where(m=>m.Kind==Kind).GroupBy(m=>Read(m.Body).Task)){
            var chain=group.OrderBy(m=>Read(m.Body).Version).ToArray();
            for(int n=0;n<chain.Length;n++){
                var m=chain[n];var p=Read(m.Body);var t=w.Tasks.Single(t=>t.Id==p.Task);
                if(t.Action!=ActionKind.Inspect||p.Version!=n+1||p.Supersedes!=(n==0?null:new EntityId(chain[n-1].Id))||p.Assessor!=t.Issuer||p.Recipient!=t.Executor||m.SenderId!=p.Assessor.Value||m.RecipientId!=p.Recipient.Value||m.CorrelationId!=t.RequestId.Value||m.Scope!=t.Scope.Value||!w.SealedMessages.Contains(new(m.Id)))throw new InvalidDataException("Invalid assessment revision/envelope");
                var expected=ReviewAt(w,t,p.Assessor,m.CreatedAt);
                if(expected.Achievement=="結果未確認"||ContractJson.CanonicalHash(p.Review)!=ContractJson.CanonicalHash(expected))throw new InvalidDataException("Assessment contains unrecognized evidence or edited claim");
                if(n>0&&ContractJson.CanonicalHash(Read(chain[n-1].Body).Review)==ContractJson.CanonicalHash(p.Review))throw new InvalidDataException("Assessment revision without new basis");
            }
        }
        foreach(var m in w.Messaging.Messages.Where(m=>m.Kind==ResponseKind)){
            var p=ReadResponse(m.Body);var notice=w.Messaging.Messages.Single(x=>x.Id==p.Notice.Value&&x.Kind==Kind);
            if(!Enum.IsDefined(p.Reply)||m.Id!=WorldSession.Child("message",p.Notice,"response").Value||m.SenderId!=notice.RecipientId||m.RecipientId!=notice.SenderId||m.CorrelationId!=notice.CorrelationId||m.Scope!=notice.Scope||!KnowsAt(w,notice.Id,new(m.SenderId),m.CreatedAt))throw new InvalidDataException("Unknown or forged assessment response");
        }
    }
}
