using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public enum InformationAction { Ask, Accept, Pay }
public sealed record InformationInput(EntityId Id,EntityId Person,InformationAction Action,EntityId? Quote=null);
public sealed record InformationQuote(EntityId Id,EntityId Speaker,EntityId Site,long Fee,EntityId[] Sources,long At,long Revision);
public sealed record InformationOrder(InformationInput Input,long At,long Revision);
public sealed record InformationOpening(string Message,long At,long Revision);
public sealed record PrivateInformationState(int Schema,string Contract,InformationQuote[] Quotes,InformationOrder[] Orders,InformationOpening[] Openings);
public sealed record InformationFact(EntityId Source,string Claim,long ObservedAt);
public sealed record InformationPayload(EntityId Quote,EntityId Speaker,InformationFact[] Facts);
public sealed record InformationCard(EntityId Quote,EntityId Person,long Fee,string Stage,bool CanAccept,bool CanPay);
// Private contact supplies the speaker's dated first-hand road observations. No allegiance or global truth transfer.
public static class PrivateInformation
{
    public const string Policy="local-private-firsthand-road-info-fee20-repeat15-sealed-v1",Kind="private-road-information";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.InformationPolicy==Policy;
    static PrivateInformationState State(ContractWorld w)=>w.PrivateInformation??new(1,Policy,[],[],[]);
    static bool Before(long at,long rev,long time,long cut)=>at<time||at==time&&rev<cut;
    static string Hash(string s)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(s));
    static string Message(EntityId quote)=>"message:private-info/"+Hash(quote.Value);
    static EntityId EvidenceId(EntityId quote,EntityId source)=>new("evidence:private-info/"+Hash(quote.Value+"/"+source.Value));
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>State(w).Orders.Any(o=>o.Input.Id==id);
    static bool Paid(ContractWorld w,EntityId quote,long at=long.MaxValue,long rev=long.MaxValue)=>State(w).Orders.Any(o=>o.Input.Quote==quote&&o.Input.Action==InformationAction.Pay&&Before(o.At,o.Revision,at,rev));
    static bool Accepted(ContractWorld w,EntityId quote,long at,long rev)=>State(w).Orders.Any(o=>o.Input.Quote==quote&&o.Input.Action==InformationAction.Accept&&Before(o.At,o.Revision,at,rev));
    internal static bool Local(ContractWorld w,EntityId person,EntityId site,long at,long rev)
    {
        if(person==w.PlayerControl||w.Characters.SingleOrDefault(c=>c.Id==person)?.Life!=LifeState.Adult||NpcArmies.CommanderBusyAt(w,person,at,rev))return false;
        foreach(var actor in new[]{person,w.PlayerControl}){
            if(actor==w.PlayerControl&&NewGameMilitary.LedgerStates(w).SelectMany(s=>s.Requests).Where(q=>Before(q.At,q.Revision,at,rev)).OrderBy(q=>q.Revision).LastOrDefault(q=>q.Input.Action is NewMilitaryAction.Deploy or NewMilitaryAction.Enter)?.Input.Action==NewMilitaryAction.Deploy)return false;
            var requests=w.Requests.Where(r=>Before(r.CreatedAt,r.CommitSequence,at,rev)).ToArray();if(requests.Any(r=>r.Intent.Executor==actor&&w.Tasks.Any(t=>t.RequestId==r.Id&&(t.FinishedAt==null||t.FinishedAt>at))))return false;
            var c=w.ForkTransportLocation();c.Branches=w.Branches! with{Journeys=w.Branches!.Journeys.Where(j=>requests.Any(r=>w.Tasks.Any(t=>t.Id==j.Task&&t.RequestId==r.Id))).ToArray()};if(RegionalCampaign.RoadActorPointAt(c,actor,at)!=BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==site).Position)return false;
        }return true;
    }
    static EntityId[] Sources(ContractWorld w,EntityId speaker,long at)=>w.Evidence.Where(e=>e.Observer==speaker&&e.Message==null&&e.ObservedAt!=null&&e.RecordedAt<=at&&w.Knowledge.Any(k=>k.Character==speaker&&k.Evidence==e.Id&&k.LearnedAt<=at)&&(e.Claim.StartsWith("route-block:",StringComparison.Ordinal)||e.Claim.StartsWith("route-open:",StringComparison.Ordinal))).GroupBy(e=>e.Claim[(e.Claim.StartsWith("route-open:",StringComparison.Ordinal)?11:12)..]).Select(g=>g.OrderByDescending(e=>e.ObservedAt).ThenByDescending(e=>e.RecordedAt).First().Id).OrderBy(id=>id.Value,StringComparer.Ordinal).ToArray();
    static InformationQuote Quote(ContractWorld w,InformationOrder o,EntityId site)
    {
        var i=o.Input;Need(i.Action==InformationAction.Ask&&i.Quote==null&&Local(w,i.Person,site,o.At,o.Revision),"現地で会っている成人へ打診してください");
        Need(!State(w).Quotes.Any(q=>q.Speaker==i.Person&&Accepted(w,q.Id,o.At,o.Revision)&&!Paid(w,q.Id,o.At,o.Revision)),"先の情報交換がまだ終わっていません");var sources=Sources(w,i.Person,o.At);Need(sources.Length>0,"私が自分で確認して話せる道の情報は、まだありません");long fee=State(w).Quotes.Any(q=>q.Speaker==i.Person&&Paid(w,q.Id,o.At,o.Revision))?15:20;return new(i.Id,i.Person,site,fee,sources,o.At,o.Revision);
    }
    static InformationPayload Payload(ContractWorld w,InformationQuote q)=>new(q.Id,q.Speaker,q.Sources.Select(id=>{var e=w.Evidence.Single(e=>e.Id==id);return new InformationFact(id,e.Claim,e.ObservedAt!.Value);}).ToArray());
    static MessageEnvelope Envelope(ContractWorld w,InformationQuote q,InformationOrder order)=>new(Message(q.Id),order.Input.Id.Value,Kind,q.Speaker.Value,w.PlayerControl.Value,System.Text.Encoding.UTF8.GetString(ContractJson.Write(Payload(w,q))),q.Site.Value,order.At);
    static void CheckAnswer(ContractWorld w,InformationOrder o,InformationQuote q)
    {
        Need(o.Input.Person==q.Speaker&&o.Input.Quote==q.Id&&Before(q.At,q.Revision,o.At,o.Revision)&&Local(w,q.Speaker,q.Site,o.At,o.Revision),"同じ相手・提示条件で現地の受渡しをしてください");
        if(o.Input.Action==InformationAction.Accept)Need(!Accepted(w,q.Id,o.At,o.Revision)&&!State(w).Quotes.Any(x=>x.Id!=q.Id&&x.Speaker==q.Speaker&&Accepted(w,x.Id,o.At,o.Revision)&&!Paid(w,x.Id,o.At,o.Revision)),"既に引き受けた情報交換です");
        else{Need(o.Input.Action==InformationAction.Pay&&Accepted(w,q.Id,o.At,o.Revision)&&!Paid(w,q.Id,o.At,o.Revision)&&State(w).Openings.Any(x=>x.Message==Message(q.Id)&&Before(x.At,x.Revision,o.At,o.Revision)),"受け取って開いた情報の未払い分だけ支払えます");Need(TransportWork.WalletBefore(w,w.PlayerControl,o.At,o.Revision)>=q.Fee,"私財が足りません。未払い分は残ります");}
    }
    internal static void Apply(ContractWorld w,InformationInput input)
    {
        Need(Enabled(w)&&EntityId.IsValid(input.Id.Value)&&Enum.IsDefined(input.Action),"この保存で使える情報交換ではありません");var o=new InformationOrder(input,w.ElapsedSeconds,w.CommitSequence);
        if(input.Action==InformationAction.Ask){var site=w.Locations.Single(l=>l.Actor==w.PlayerControl);Need(site.Kind==LocationKind.AtSite&&site.Site!=null,"現地に到着してから打診してください");var q=Quote(w,o,site.Site.Value);w.PrivateInformation=State(w) with{Quotes=[..State(w).Quotes,q],Orders=[..State(w).Orders,o]};}
        else{var q=State(w).Quotes.SingleOrDefault(q=>q.Id==input.Quote);Need(q!=null,"本人が聞いた条件を指定してください");CheckAnswer(w,o,q);w.PrivateInformation=State(w) with{Orders=[..State(w).Orders,o]};if(input.Action==InformationAction.Accept){MessageFlow.Send(w.Messaging,Envelope(w,q,o),o.At,1);w.SealedMessages=[..w.SealedMessages,new(Message(q.Id))];}}
    }
    internal static void RecordOpening(ContractWorld w,EntityId actor,string message)
    {
        if(!Enabled(w)||actor!=w.PlayerControl||State(w).Openings.Any(o=>o.Message==message))return;var q=State(w).Quotes.SingleOrDefault(q=>Message(q.Id)==message);if(q==null)return;Need(MessageFlow.Knows(w.Messaging,message,actor.Value)&&w.Messaging.Attempts.Any(a=>a.MessageId==message&&a.ReceiverId==actor.Value&&a.Status==AttemptStatus.Delivered),"届いて開いた情報ではありません");w.PrivateInformation=State(w) with{Openings=[..State(w).Openings,new(message,w.ElapsedSeconds,w.CommitSequence)]};
        foreach(var fact in Payload(w,q).Facts){var id=EvidenceId(q.Id,fact.Source);w.Evidence=[..w.Evidence,new(id,actor,q.Speaker,fact.Claim,fact.ObservedAt,w.ElapsedSeconds,new(message),null)];w.Knowledge=[..w.Knowledge,new(actor,id,w.ElapsedSeconds)];}
    }
    internal static bool IsSourceReport(ContractWorld w,Evidence evidence)
    {
        if(!Enabled(w)||evidence.Observer!=w.PlayerControl||evidence.Message==null)return false;
        foreach(var q in State(w).Quotes){var opening=State(w).Openings.SingleOrDefault(o=>o.Message==Message(q.Id));if(opening==null||evidence.Message.Value.Value!=opening.Message||!MessageFlow.Knows(w.Messaging,opening.Message,w.PlayerControl.Value))continue;
            foreach(var id in q.Sources){var source=w.Evidence.SingleOrDefault(e=>e.Id==id&&e.Observer==q.Speaker&&e.Message==null);if(source==null)continue;var expected=new Evidence(EvidenceId(q.Id,id),w.PlayerControl,q.Speaker,source.Claim,source.ObservedAt,opening.At,new(opening.Message),null);if(evidence==expected&&w.Knowledge.Contains(new(w.PlayerControl,evidence.Id,opening.At))&&w.Messaging.Messages.Any(m=>m.Id==opening.Message&&m.Kind==Kind&&m.SenderId==q.Speaker.Value&&m.RecipientId==w.PlayerControl.Value))return true;}
        }return false;
    }
    public static long Net(ContractWorld w,EntityId lot,long at=long.MaxValue,long rev=long.MaxValue)
    {long n=0;foreach(var o in State(w).Orders.Where(o=>o.Input.Action==InformationAction.Pay&&Before(o.At,o.Revision,at,rev))){var q=State(w).Quotes.Single(q=>q.Id==o.Input.Quote);if(lot==NewGameWork.Wallet(w.PlayerControl))n=checked(n-q.Fee);if(lot==NewGameWork.Wallet(q.Speaker))n=checked(n+q.Fee);}return n;}
    public static InformationCard[] View(ContractWorld w)
    {if(!Enabled(w))return [];return State(w).Quotes.Select(q=>{bool accept=Accepted(w,q.Id,long.MaxValue,long.MaxValue),open=State(w).Openings.Any(o=>o.Message==Message(q.Id)),paid=Paid(w,q.Id),local=Local(w,q.Speaker,q.Site,w.ElapsedSeconds,w.CommitSequence+1);bool pending=State(w).Quotes.Any(x=>x.Id!=q.Id&&x.Speaker==q.Speaker&&Accepted(w,x.Id,w.ElapsedSeconds,w.CommitSequence+1)&&!Paid(w,x.Id));return new InformationCard(q.Id,q.Speaker,q.Fee,paid?"情報料を支払った":open?"開封した情報・未払い":accept?"受渡しを依頼・内容は未確認":"提示条件を聞いた",!accept&&local&&!pending,accept&&open&&!paid&&local);}).ToArray();}
    public static string? Address(ContractWorld w,MessageEnvelope m)=>m.Kind==Kind&&State(w).Quotes.SingleOrDefault(q=>Message(q.Id)==m.Id) is {} q?NewGameWorld.Node(q.Site.Value):null;
    internal static void Rewind(ContractWorld w,long at,long rev)
    {if(w.PrivateInformation is not {} s)return;var openings=s.Openings.Where(o=>Before(o.At,o.Revision,at,rev+1)).ToArray();var messages=s.Quotes.Select(q=>Message(q.Id)).ToHashSet(StringComparer.Ordinal);var known=openings.Select(o=>o.Message).ToHashSet(StringComparer.Ordinal);w.PrivateInformation=s with{Quotes=s.Quotes.Where(q=>Before(q.At,q.Revision,at,rev+1)).ToArray(),Orders=s.Orders.Where(o=>Before(o.At,o.Revision,at,rev+1)).ToArray(),Openings=openings};w.Evidence=w.Evidence.Where(e=>e.Message==null||!messages.Contains(e.Message.Value.Value)||known.Contains(e.Message.Value.Value)).ToArray();w.Knowledge=w.Knowledge.Where(k=>w.Evidence.Any(e=>e.Id==k.Evidence)).ToArray();}
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.PrivateInformation==null&&!w.Evidence.Any(e=>e.Id.Value.StartsWith("evidence:private-info/",StringComparison.Ordinal))&&!w.Messaging.Messages.Any(m=>m.Kind==Kind),"旧世界へ情報交換を追加できません");return;}var s=State(w);Need(w.PrivateInformation!=null&&s.Schema==1&&s.Contract==Policy&&s.Quotes!=null&&s.Orders!=null&&s.Openings!=null,"私的接触の原本がありません");Need(s.Orders.Select(o=>o.Input.Id).Distinct().Count()==s.Orders.Length&&s.Orders.Zip(s.Orders.Skip(1)).All(p=>p.First.Revision<p.Second.Revision&&p.First.At<=p.Second.At),"私的接触のID・順序が不正です");
        foreach(var o in s.Orders){Need(o.At>=0&&o.At<=w.ElapsedSeconds&&o.Revision>0&&o.Revision<=w.CommitSequence&&!FactionStances.OtherIdUsed(w,o.Input.Id,includeInformation:false)&&!FactionStances.OwnIdUsed(w,o.Input.Id),"私的接触の時点・IDが不正です");var q=s.Quotes.Single(q=>q.Id==(o.Input.Action==InformationAction.Ask?o.Input.Id:o.Input.Quote));if(o.Input.Action==InformationAction.Ask)Need(ContractJson.CanonicalHash(q)==ContractJson.CanonicalHash(Quote(w,o,q.Site)),"相手が知った情報・取引条件が原本と違います");else{CheckAnswer(w,o,q);if(o.Input.Action==InformationAction.Accept)Need(ContractJson.CanonicalHash(w.Messaging.Messages.Single(m=>m.Id==Message(q.Id)))==ContractJson.CanonicalHash(Envelope(w,q,o))&&w.SealedMessages.Contains(new(Message(q.Id))),"私的な情報提供の原文が違います");}}
        Need(s.Quotes.Select(q=>q.Id).Distinct().Count()==s.Quotes.Length&&s.Quotes.All(q=>s.Orders.Any(o=>o.Input.Id==q.Id&&o.Input.Action==InformationAction.Ask))&&s.Openings.Select(o=>o.Message).Distinct().Count()==s.Openings.Length,"情報提示・開封が重複しています");var ids=new HashSet<EntityId>();foreach(var o in s.Openings){var q=s.Quotes.Single(q=>Message(q.Id)==o.Message);Need(o.At<=w.ElapsedSeconds&&o.Revision>q.Revision&&o.Revision<=w.CommitSequence&&MessageFlow.LearnedAt(w.Messaging,o.Message,w.PlayerControl.Value)==o.At&&w.Messaging.Attempts.Any(a=>a.MessageId==o.Message&&a.ReceiverId==w.PlayerControl.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=o.At),"私的情報の到達・開封が不正です");foreach(var fact in Payload(w,q).Facts){var id=EvidenceId(q.Id,fact.Source);ids.Add(id);Need(w.Evidence.Contains(new(id,w.PlayerControl,q.Speaker,fact.Claim,fact.ObservedAt,o.At,new(o.Message),null))&&w.Knowledge.Contains(new(w.PlayerControl,id,o.At)),"情報源の観測日を受領日へすり替えています");}}
        Need(ids.SetEquals(w.Evidence.Where(e=>e.Id.Value.StartsWith("evidence:private-info/",StringComparison.Ordinal)).Select(e=>e.Id)),"開封前に情報を知っています");Need(w.Messaging.Messages.Count(m=>m.Kind==Kind)==s.Orders.Count(o=>o.Input.Action==InformationAction.Accept),"情報提供の原本がありません");
    }
}
