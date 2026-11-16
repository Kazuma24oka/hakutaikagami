using System.Runtime.CompilerServices;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

// 世界の一覧の引き方の索引（2026-11-14）。一覧（配列）は書き足すときに新しい配列に置き換わるので、配列ごとに一度だけ作って覚える。
// 答えは一覧を頭から探したときと同じ。人数と記録が多い全国の世界で、人ごとの判断のたびに全部を探さないために使う。
internal static class WorldIndex
{
    static readonly ConditionalWeakTable<RequestReceipt[],Dictionary<EntityId,RequestReceipt>> requestsById=new();
    static readonly ConditionalWeakTable<RequestReceipt[],ILookup<EntityId,RequestReceipt>> requestsByActor=new();
    static readonly ConditionalWeakTable<TaskState[],Dictionary<EntityId,TaskState>> tasksByRequest=new();
    static readonly ConditionalWeakTable<TaskState[],ILookup<EntityId,TaskState>> tasksByExecutor=new();
    static readonly ConditionalWeakTable<PersonalKnowledge[],Dictionary<(string,string),long>> knowledge=new();
    static readonly ConditionalWeakTable<MessageEnvelope[],Dictionary<string,MessageEnvelope>> messagesById=new();

    internal static RequestReceipt? Request(ContractWorld w,EntityId id)=>requestsById.GetValue(w.Requests,a=>a.GroupBy(r=>r.Id).ToDictionary(g=>g.Key,g=>g.Single())).GetValueOrDefault(id);
    internal static IEnumerable<RequestReceipt> RequestsBy(ContractWorld w,EntityId actor)=>requestsByActor.GetValue(w.Requests,a=>a.ToLookup(r=>r.Intent.Actor))[actor];
    // 依頼に対する仕事（依頼ごとに一つ。無ければ例外：Tasks.Single と同じ）。
    internal static TaskState TaskFor(ContractWorld w,EntityId request)=>tasksByRequest.GetValue(w.Tasks,a=>a.GroupBy(t=>t.RequestId).ToDictionary(g=>g.Key,g=>g.Single()))[request];
    internal static IEnumerable<TaskState> TasksOf(ContractWorld w,EntityId executor)=>tasksByExecutor.GetValue(w.Tasks,a=>a.ToLookup(t=>t.Executor))[executor];
    // 人がその書状を知った時刻（知らなければ空）。
    internal static long? LearnedAt(ContractWorld w,string message,string character)=>knowledge.GetValue(w.Messaging.Knowledge,a=>{var d=new Dictionary<(string,string),long>();foreach(var k in a)if(!d.ContainsKey((k.MessageId,k.CharacterId)))d[(k.MessageId,k.CharacterId)]=k.LearnedAt;return d;}).TryGetValue((message,character),out var at)?at:null;
    // 同じ人・同じ書状の知識が複数あれば、どれか一つでも条件に合うかで比べる（Any と同じ）。
    internal static bool KnewBefore(ContractWorld w,string message,string character,long at)=>w.Messaging.Knowledge.Length<64?w.Messaging.Knowledge.Any(k=>k.CharacterId==character&&k.MessageId==message&&k.LearnedAt<at):EarliestKnown(w,message,character) is {} t&&t<at;
    internal static bool KnewBy(ContractWorld w,string message,string character,long at)=>w.Messaging.Knowledge.Length<64?w.Messaging.Knowledge.Any(k=>k.CharacterId==character&&k.MessageId==message&&k.LearnedAt<=at):EarliestKnown(w,message,character) is {} t&&t<=at;
    static readonly ConditionalWeakTable<PersonalKnowledge[],Dictionary<(string,string),long>> earliest=new();
    static long? EarliestKnown(ContractWorld w,string message,string character)=>earliest.GetValue(w.Messaging.Knowledge,a=>{var d=new Dictionary<(string,string),long>();foreach(var k in a){var key=(k.MessageId,k.CharacterId);if(!d.TryGetValue(key,out var t)||k.LearnedAt<t)d[key]=k.LearnedAt;}return d;}).TryGetValue((message,character),out var v)?v:null;
    static readonly ConditionalWeakTable<MessageEnvelope[],ILookup<string,MessageEnvelope>> messagesByCorrelation=new();
    internal static IEnumerable<MessageEnvelope> MessagesFor(ContractWorld w,string correlation)=>messagesByCorrelation.GetValue(w.Messaging.Messages,a=>a.ToLookup(m=>m.CorrelationId,StringComparer.Ordinal))[correlation];
    static readonly ConditionalWeakTable<TaskState[],Dictionary<EntityId,TaskState>> tasksById=new();
    internal static TaskState? Task(ContractWorld w,EntityId id)=>tasksById.GetValue(w.Tasks,a=>{var d=new Dictionary<EntityId,TaskState>();foreach(var t in a)d.TryAdd(t.Id,t);return d;}).GetValueOrDefault(id);
    static readonly ConditionalWeakTable<TimedRelation[],ILookup<EntityId,TimedRelation>> relationsBySubject=new();
    internal static IEnumerable<TimedRelation> RelationsOf(TimedRelation[] relations,EntityId subject)=>relationsBySubject.GetValue(relations,a=>a.ToLookup(r=>r.Subject))[subject];
    internal static MessageEnvelope Message(ContractWorld w,string id)=>messagesById.GetValue(w.Messaging.Messages,a=>a.GroupBy(m=>m.Id).ToDictionary(g=>g.Key,g=>g.Single()))[id];
}
