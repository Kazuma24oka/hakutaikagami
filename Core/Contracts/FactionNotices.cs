using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public sealed record FactionNoticeInput(EntityId Id,EntityId Original);
public sealed record FactionNoticeRequest(FactionNoticeInput Input,EntityId Sender,EntityId Recipient,EntityId Address,long At,long Revision,FactionStanceClaim Claim,FactionStanceAuthority Authority,string Message);
public sealed record FactionNoticeOpening(string Message,EntityId Actor,long At,long Revision);
public sealed record FactionNoticeState(int Schema,string Contract,FactionNoticeRequest[] Requests,FactionNoticeOpening[] Openings,FactionNoticeReaction[] Reactions);
// A physical dated paper, never a change to the addressee's political will or live GPS.
public static class FactionNotices
{
    public const string Policy="head-owned-stance-dated-paper-physical-receipt-personal-opening-v1",Kind="faction-stance-notice";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool b,string why){if(!b)throw new InvalidDataException(why);}
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.FactionNotices?.Requests.Any(q=>q.Input.Id==id)==true;
    static string MessageId(EntityId id)=>"message:faction-stance/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(id.Value));
    static FactionNoticeRequest Prepare(ContractWorld w,FactionNoticeInput input,EntityId sender,long at,long rev)
    {
        Need(WorldClashes.Enabled(w)&&EntityId.IsValid(input.Id.Value)&&EntityId.IsValid(input.Original.Value),"方針通知の版・原本が不正です");
        var original=w.FactionStances?.Decisions.SingleOrDefault(d=>d.Input.Id==input.Original&&d.Input.Action==FactionStanceAction.DeclareHostile&&d.Authority.Issuer==sender&&(d.At<at||d.At==at&&d.Revision<=rev));
        Need(original!=null,"本人が記録した自家の原判断を指定してください");
        var a=FactionStances.ResolveAuthority(w,sender,original.Input.Owner,at,rev);Need(a!=null,"自家の方針を通知する当主権限がありません");
        var target=HouseSuccession.Factions(w,at,rev).Single(f=>f.Id==original.Input.Target.Value);var claim=FactionStances.OwnAt(w,sender,at,rev).Single(c=>c.Declaration==input.Original);
        return new(input,sender,new(target.Head),new(target.Home),at,rev,claim,a,MessageId(input.Id));
    }
    static MessageEnvelope Envelope(FactionNoticeRequest q)=>new(q.Message,q.Input.Id.Value,Kind,q.Sender.Value,q.Recipient.Value,System.Text.Encoding.UTF8.GetString(ContractJson.Write(q.Claim)),q.Address.Value,q.At);
    public static void Dispatch(ContractWorld w,FactionNoticeInput input,EntityId sender)
    {
        Need(w.CommitSequence>0&&!OwnIdUsed(w,input.Id)&&!FactionStances.OtherIdUsed(w,input.Id)&&!FactionStances.OwnIdUsed(w,input.Id),"別の操作で使ったIDです");
        var q=Prepare(w,input,sender,w.ElapsedSeconds,w.CommitSequence);var s=w.FactionNotices??new(1,Policy,[],[],[]);
        w.FactionNotices=s with{Requests=[..s.Requests,q]};MessageFlow.Send(w.Messaging,Envelope(q),q.At,1);w.SealedMessages=[..w.SealedMessages,new(q.Message)];
    }
    public static string? Address(ContractWorld w,MessageEnvelope message)=>message.Kind==Kind&&w.FactionNotices?.Requests.SingleOrDefault(q=>q.Message==message.Id) is {} q?NewGameWorld.Node(q.Address.Value):null;
    internal static void RecordOpening(ContractWorld w,EntityId actor,string message)
    {
        if(w.FactionNotices is not {} s||s.Openings.Any(o=>o.Message==message)||!s.Requests.Any(q=>q.Message==message&&q.Recipient==actor))return;
        Need(w.Messaging.Attempts.Any(a=>a.MessageId==message&&a.ReceiverId==actor.Value&&a.Status==AttemptStatus.Delivered)&&MessageFlow.Knows(w.Messaging,message,actor.Value),"本人に届いて開いた原本が必要です");
        w.FactionNotices=s with{Openings=[..s.Openings,new(message,actor,w.ElapsedSeconds,w.CommitSequence)]};
    }
    internal static void ReceiveNpc(ContractWorld w)
    {
        if(w.FactionNotices is not {} s)return;
        foreach(var q in s.Requests){if(q.Recipient==w.PlayerControl||w.Characters.Single(c=>c.Id==q.Recipient).Life!=LifeState.Adult||s.Openings.Any(o=>o.Message==q.Message)||!w.Messaging.Attempts.Any(a=>a.MessageId==q.Message&&a.ReceiverId==q.Recipient.Value&&a.Status==AttemptStatus.Delivered))continue;
            if(!MessageFlow.Knows(w.Messaging,q.Message,q.Recipient.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(q.Message,q.Recipient.Value,w.ElapsedSeconds,q.Message)];RecordOpening(w,q.Recipient,q.Message);
        }
    }
    public static FactionStanceClaim[] Known(ContractWorld w,EntityId actor,long? at=null,long? rev=null)
    {
        if(w.FactionNotices is not {} s)return [];long time=at??w.ElapsedSeconds,revision=rev??w.CommitSequence;Need(time>=0&&time<=w.ElapsedSeconds&&revision>=0&&revision<=w.CommitSequence,"未確定の通知知識は参照しません");
        return s.Requests.Where(q=>s.Openings.Any(o=>o.Message==q.Message&&o.Actor==actor&&(o.At<time||o.At==time&&o.Revision<=revision))).Select(q=>q.Claim).ToArray();
    }
    public static void Validate(ContractWorld w)
    {
        if(w.FactionNotices is not {} s){Need(!w.Messaging.Messages.Any(m=>m.Kind==Kind),"通知原本だけが残っています");return;}
        Need(WorldClashes.Enabled(w)&&s.Schema==1&&s.Contract==Policy&&s.Requests.Length>0,"方針通知の保存版が不正です");
        Need(s.Requests.Select(q=>q.Input.Id).Distinct().Count()==s.Requests.Length&&s.Openings.Select(o=>o.Message).Distinct().Count()==s.Openings.Length,"通知・開封が重複しています");
        for(int n=0;n<s.Requests.Length;n++){var q=s.Requests[n];Need(q.At>=0&&q.At<=w.ElapsedSeconds&&q.Revision>0&&q.Revision<=w.CommitSequence&&(n==0||q.At>=s.Requests[n-1].At&&q.Revision>s.Requests[n-1].Revision),"通知の時刻・順序が不正です");
            Need(!FactionStances.OtherIdUsed(w,q.Input.Id,true,false)&&!FactionStances.OwnIdUsed(w,q.Input.Id),"通知IDが他の操作と重複しています");
            var expected=Prepare(w,q.Input,q.Sender,q.At,q.Revision);Need(q==expected,"当主自身の原判断と通知条件が違います");
            var message=w.Messaging.Messages.Single(m=>m.Id==q.Message);Need(ContractJson.CanonicalHash(message)==ContractJson.CanonicalHash(Envelope(q))&&w.SealedMessages.Any(id=>id.Value==q.Message),"通知原文・受取人・封印が違います");
            foreach(var mission in w.Couriers!.Missions.Where(c=>w.Messaging.Attempts.Any(a=>a.Id==c.Attempt&&a.MessageId==q.Message)))Need(mission.Destination==NewGameWorld.Node(q.Address.Value),"通知先を相手の現在位置へすり替えています");
        }
        Need(w.Messaging.Messages.Count(m=>m.Kind==Kind)==s.Requests.Length,"通知原本の数が違います");
        foreach(var o in s.Openings){var q=s.Requests.Single(q=>q.Message==o.Message);Need(o.Actor==q.Recipient&&o.At>=q.At&&o.At<=w.ElapsedSeconds&&o.Revision>q.Revision&&o.Revision<=w.CommitSequence&&w.Messaging.Attempts.Any(a=>a.MessageId==o.Message&&a.ReceiverId==o.Actor.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=o.At)&&MessageFlow.LearnedAt(w.Messaging,o.Message,o.Actor.Value)==o.At,"物理到達と本人の開封が一致しません");}
        foreach(var q in s.Requests)Need(MessageFlow.Knows(w.Messaging,q.Message,q.Recipient.Value)==s.Openings.Any(o=>o.Message==q.Message),"開封前に政治情報を知っています");
    }
}
