using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public static partial class NewGameMilitary
{
    public static NewMilitaryState[] LedgerStates(ContractWorld w)=>w.NewMilitary is {} current?[current,..w.OtherMilitaryLedgers??[]]:w.OtherMilitaryLedgers??[];
    public static EntityId[] UnitIdsFor(ContractWorld w)=>w.NewMilitary?.Binding?.Units??UnitIds;
    public static EntityId DepotFor(ContractWorld w)=>w.NewMilitary?.Binding?.Depot??Depot;
    public static EntityId CarryFor(ContractWorld w)=>w.NewMilitary?.Binding?.Carry??Carry;
    static string LedgerKey(NewMilitaryState s)=>Text(new[]{s.Terms.Owner.Value,s.Terms.Site.Value});
    static bool SameGarrison(NewMilitaryState s,NewMilitaryTerms t)=>s.Terms.Owner==t.Owner&&s.Terms.Site==t.Site;
    static ContractWorld LedgerContext(ContractWorld w,NewMilitaryState s,bool isolate=false)
    {
        var c=w.ForkMilitaryLedger();c.MilitaryLedgerHistory=w.MilitaryLedgerHistory??LedgerStates(w);c.NewMilitary=s;c.OtherMilitaryLedgers=null;
        if(!isolate)return c;
        c.Messaging=ContractJson.Copy(w.Messaging);
        var requests=s.Requests.Select(q=>q.Input.Id.Value).ToHashSet(StringComparer.Ordinal);
        bool keep(MessageEnvelope m)=>!MilitaryEnvelope(m)||requests.Contains(m.CorrelationId);
        var removed=c.Messaging.Messages.Where(m=>!keep(m)).Select(m=>m.Id).ToHashSet(StringComparer.Ordinal);
        c.Messaging.Messages=c.Messaging.Messages.Where(keep).ToArray();
        c.Messaging.Attempts=c.Messaging.Attempts.Where(a=>!removed.Contains(a.MessageId)).ToArray();
        c.Messaging.Receipts=c.Messaging.Receipts.Where(a=>!removed.Contains(a.MessageId)).ToArray();
        c.Messaging.Knowledge=c.Messaging.Knowledge.Where(a=>!removed.Contains(a.MessageId)).ToArray();
        c.SealedMessages=c.SealedMessages.Where(id=>!removed.Contains(id.Value)).ToArray();
        var allLots=LedgerStates(w).SelectMany(x=>new[]{x.Binding?.Depot??Depot,x.Binding?.Carry??Carry}).ToHashSet();
        var units=UnitIdsFor(c).ToHashSet();c.Units=w.Units.Where(u=>units.Contains(u.Id)).ToArray();
        c.ResourceLots=w.ResourceLots.Where(l=>!allLots.Contains(l.Id)||l.Id==DepotFor(c)||l.Id==CarryFor(c)).ToArray();
        return c;
    }
    static bool MilitaryEnvelope(MessageEnvelope m)=>m.Kind.StartsWith("new-military-",StringComparison.Ordinal)||UnitEnvelope(m);
    static bool ClosedForSwitch(ContractWorld w)
    {
        if(w.NewMilitary==null)return true;
        bool closed=CaseEvents(w).Any(e=>Known(w,e,w.ElapsedSeconds,w.CommitSequence)&&Read(w,e).Effect is MilitaryEffect.Returned or MilitaryEffect.Refused or MilitaryEffect.Expired);
        return closed&&!Outstanding(w,w.ElapsedSeconds,w.CommitSequence)&&!CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)&&!Deployed(w)&&AtHome(w)&&CarryAt(w,w.ElapsedSeconds,w.CommitSequence)==0;
    }
    static NewMilitaryTerms? CurrentHouseTarget(ContractWorld w)
    {
        try{return Terms(w,new("request:military-ledger-preview"),AuthorityContract,LedgerIdentityContract);}
        catch(InvalidDataException){return null;}
    }
    public static bool CanSwitchToCurrentHouse(ContractWorld w)
    {
        var target=CurrentHouseTarget(w);
        return target!=null&&w.NewMilitary is {} s&&!SameGarrison(s,target)&&ClosedForSwitch(w);
    }
    public static bool CanConsultCurrentHouse(ContractWorld w,EntityId principal)=>w.NewMilitary==null||w.NewMilitary.Terms.Principal==principal||CurrentHouseTarget(w)?.Principal==principal&&CanSwitchToCurrentHouse(w);
    public static string? CycleForCurrentHouse(ContractWorld w)
    {
        var target=CurrentHouseTarget(w);
        return target!=null&&LedgerStates(w).Any(s=>SameGarrison(s,target))?RenewalContract:null;
    }
    public static EntityId? SourceForCurrentHouse(ContractWorld w)=>CanSwitchToCurrentHouse(w)?ExistingLedgerIdentity(w)!.Ledger:null;
    static void ValidateSelectionSources(ContractWorld w)
    {
        if(w.OtherMilitaryLedgers==null){Need(w.NewMilitary?.Requests.All(q=>q.Input.FromLedger==null)!=false,"切替元の軍台帳の履歴が欠落しています");return;}
        var inputs=LedgerStates(w).SelectMany(s=>s.Requests.Select(q=>(Ledger:s,Request:q))).OrderBy(x=>x.Request.Revision).ToArray();
        for(int i=0;i<inputs.Length;i++){
            var q=inputs[i].Request;bool switched=i>0&&LedgerKey(inputs[i-1].Ledger)!=LedgerKey(inputs[i].Ledger);
            var expected=switched?LedgerIdentity(inputs[i-1].Ledger.Terms.Owner,inputs[i-1].Ledger.Terms.Site).Ledger:(EntityId?)null;
            Need(q.Input.FromLedger==expected,"切替元の軍台帳の履歴が欠落・置換されています");
        }
    }
    static void SelectRequestedLedger(ContractWorld w,NewMilitaryInput input)
    {
        if(input.Action!=NewMilitaryAction.Request||input.LedgerPolicy!=LedgerIdentityContract||w.NewMilitary==null)return;
        var target=Terms(w,input.Id,input.AuthorityPolicy,input.LedgerPolicy);
        if(SameGarrison(w.NewMilitary,target))return;
        Need(ClosedForSwitch(w),"旧主家の指揮・兵糧・返報の返納と終了通知を確認してください");
        Need(input.FromLedger==ExistingLedgerIdentity(w)!.Ledger,"表示した元台帳と現在の軍台帳が違います");
        var old=w.NewMilitary;var others=w.OtherMilitaryLedgers??[];var selected=others.SingleOrDefault(s=>SameGarrison(s,target));
        Need(selected==null||selected.Terms.Principal==target.Principal,"この駐屯軍の発令者変更は未対応です");
        w.NewMilitary=selected;w.OtherMilitaryLedgers=[..others.Where(s=>s!=selected),old];
    }
    static void ForEachLedger(ContractWorld w,Action<ContractWorld> action)
    {
        if(w.OtherMilitaryLedgers==null){action(w);return;}
        var current=w.NewMilitary!;var others=w.OtherMilitaryLedgers;var result=new List<NewMilitaryState>();var priorHistory=w.MilitaryLedgerHistory;var timeline=LedgerStates(w);
        try {foreach(var ledger in new[]{current}.Concat(others)){w.NewMilitary=ledger;w.OtherMilitaryLedgers=null;w.MilitaryLedgerHistory=timeline;action(w);timeline[result.Count]=w.NewMilitary!;result.Add(w.NewMilitary!);}}
        finally {w.MilitaryLedgerHistory=priorHistory;w.NewMilitary=result.Count>0?result[0]:current;w.OtherMilitaryLedgers=result.Count==others.Length+1?result.Skip(1).ToArray():others;}
    }
    internal static void Update(ContractWorld w)=>ForEachLedger(w,UpdateLedger);
    internal static void BeforeUnitMessages(ContractWorld w)=>ForEachLedger(w,BeforeLedgerUnitMessages);
    internal static void RecordOpening(ContractWorld w,EntityId actor,string id)=>ForEachLedger(w,c=>RecordLedgerOpening(c,actor,id));
    public static string[] MessageParticipants(ContractWorld w)=>LedgerStates(w).SelectMany(s=>LedgerMessageParticipants(LedgerContext(w,s))).Distinct(StringComparer.Ordinal).ToArray();
    public static bool HandlesInternalAuthorityMessage(ContractWorld w,string id)=>LedgerStates(w).Any(s=>HandlesLedgerInternalAuthorityMessage(LedgerContext(w,s),id));
    internal static long? NextBoundary(ContractWorld w,long target)=>LedgerStates(w).Select(s=>LedgerNextBoundary(LedgerContext(w,s),target)).Where(t=>t!=null).Order().FirstOrDefault();
    static NewMilitaryState? ControllingLedger(ContractWorld w,long at)=>(w.MilitaryLedgerHistory??LedgerStates(w)).SelectMany(s=>s.Requests.Where(q=>Before(q.At,q.Revision,at,w.CommitSequence)).Select(q=>(Ledger:s,Request:q))).OrderBy(x=>x.Request.Revision).LastOrDefault().Ledger;
    // The person's physical position belongs to the ledger active at that historical instant,
    // even while a late letter is being processed in a former house's ledger.
    public static FieldPoint? ActualMilitaryPosition(ContractWorld w,long at)
    {
        if(w.MilitaryLedgerHistory==null&&w.OtherMilitaryLedgers==null||w.MilitaryLedgerHistory is {Length: <=1})return w.NewMilitary!=null&&DeployedAt(w,at)?PersonPosition(w,at):null;
        if(w.MilitaryLedgerHistory==null&&w.NewMilitary is {} active&&at>=active.Requests[^1].At)return DeployedAt(w,at)?PersonPosition(w,at):null;
        var actual=ControllingLedger(w,at);if(actual==null)return null;
        if(w.NewMilitary is {} current&&LedgerKey(current)==LedgerKey(actual))return DeployedAt(w,at)?PersonPosition(w,at):null;
        var past=LedgerStateBefore(actual,at,w.CommitSequence);if(past==null)return null;
        var c=w.ForkMilitaryLedger();c.NewMilitary=past;c.OtherMilitaryLedgers=null;c.MilitaryLedgerHistory=null;
        return DeployedAt(c,at)?PersonPosition(c,at):null;
    }
    public static bool AllowsMobileLot(ContractWorld w,ResourceLot lot)=>LedgerStates(w).Any(s=>{
        var c=LedgerContext(w,s);return lot.Id==CarryFor(c)&&(lot.MobileHolder==w.PlayerControl||UnitCarries(c)&&lot.MobileHolder==UnitIdsFor(c)[0]);
    });
    public static bool Outstanding(ContractWorld w,long at,long rev)=>LedgerStates(w).Any(s=>LedgerOutstanding(LedgerContext(w,s),at,rev));
    public static long Net(ContractWorld w,EntityId lot)
    {
        long total=0;foreach(var s in LedgerStates(w)){var c=LedgerContext(w,s);total=checked(total+LedgerNet(c,lot));}return total;
    }
    internal static void Sync(ContractWorld w)
    {
        SyncLedger(w);if(w.OtherMilitaryLedgers==null)return;
        var units=w.Units.ToList();var lots=w.ResourceLots.ToList();
        foreach(var s in w.OtherMilitaryLedgers){var c=LedgerContext(w,s);SyncLedger(c);units.AddRange(c.Units);lots.AddRange(c.ResourceLots.Where(l=>l.Id==DepotFor(c)||l.Id==CarryFor(c)));}
        w.Units=units.ToArray();w.ResourceLots=lots.ToArray();
    }
    static void ValidateLedgerBinding(ContractWorld w)
    {
        var s=w.NewMilitary!;var first=s.Requests[0];
        if(first.Input.LedgerPolicy==null){Need(s.Binding==null,"旧軍台帳のIDを無断で置き換えています");return;}
        Need(first.Input.LedgerPolicy==LedgerIdentityContract&&s.Binding!=null&&Same(s.Binding,LedgerIdentity(s.Terms.Owner,s.Terms.Site))&&s.Terms.Army==s.Binding.Army,"主家・拠点と軍台帳のIDが一致しません");
    }
    internal static void Validate(ContractWorld w,ContractWorld? trustedPrior)
    {
        ValidateSelectionSources(w);
        if(w.OtherMilitaryLedgers==null){ValidateLedger(w,trustedPrior);return;}
        Need(w.NewMilitary!=null&&w.OtherMilitaryLedgers.Length>0,"操作対象のない軍台帳です");
        var ledgers=LedgerStates(w);Need(ledgers.Select(LedgerKey).Distinct(StringComparer.Ordinal).Count()==ledgers.Length,"同じ主家・拠点の軍台帳が重複しています");
        var requests=ledgers.SelectMany(s=>s.Requests.Select(q=>(Ledger:s,Request:q))).OrderBy(x=>x.Request.Revision).ToArray();
        Need(requests.Length>0&&requests.Select(x=>x.Request.Input.Id).Distinct().Count()==requests.Length&&requests.Select(x=>x.Request.Revision).Distinct().Count()==requests.Length,"主家をまたいだ軍務入力が重複しています");
        foreach(var ledger in ledgers)foreach(var frame in ledger.Observation?.Frames??[]){var selected=requests.LastOrDefault(x=>x.Request.Revision<=frame.Revision);Need(selected.Ledger!=null&&LedgerKey(selected.Ledger)==LedgerKey(ledger),"操作対象でない旧軍の目撃情報を追加しています");}
        Need(LedgerKey(requests[^1].Ledger)==LedgerKey(w.NewMilitary!),"最後に操作した軍台帳と選択対象が違います");
        foreach(var m in w.Messaging.Messages.Where(MilitaryEnvelope))Need(ledgers.Count(s=>s.Requests.Any(q=>q.Input.Id.Value==m.CorrelationId))==1,"軍務原本の帰属台帳がありません");
        foreach(var s in w.OtherMilitaryLedgers)Need(ClosedForSwitch(LedgerContext(w,s)),"未返納の旧軍台帳を放置できません");
        for(int i=1;i<requests.Length;i++)if(LedgerKey(requests[i-1].Ledger)!=LedgerKey(requests[i].Ledger)){
            var next=requests[i].Request;Need(next.Input.Action==NewMilitaryAction.Request&&next.Input.LedgerPolicy==LedgerIdentityContract,"軍務の申出なしで台帳を切り替えています");
            var prior=BeforeState(LedgerContext(w,requests[i-1].Ledger,true),next.At,next.Revision-1);Need(ClosedForSwitch(prior),"旧軍務の終了確認より前に新軍務を開始しています");
        }
        foreach(var s in ledgers){var c=LedgerContext(w,s,true);var old=trustedPrior==null?null:LedgerStates(trustedPrior).SingleOrDefault(x=>LedgerKey(x)==LedgerKey(s));ValidateLedger(c,old==null?null:LedgerContext(trustedPrior!,old,true),s==w.NewMilitary);}
        var normalized=ContractJson.Copy(w);Sync(normalized);Need(Same(w.Units,normalized.Units)&&Same(w.ResourceLots,normalized.ResourceLots)&&Same(w.Locations,normalized.Locations),"全主家の軍・兵糧・位置が元帳と一致しません");
    }
}
