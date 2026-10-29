using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public enum TruceConfirmationAction { Send,Resend,Acknowledge }
public enum TruceConfirmationKind { Confirmation,Receipt,FailureNotice }
public sealed record TruceConfirmationInput(EntityId Id,TruceConfirmationAction Action,EntityId Owner,EntityId Acceptance,string? Original=null);
public sealed record TruceConfirmationEntry(TruceConfirmationInput Input,EntityId Actor,long At,long Revision,string Paper);
public sealed record TruceConfirmationPaper(string Id,EntityId Request,TruceConfirmationKind Kind,EntityId Owner,EntityId Acceptance,TruceTerms Terms,EntityId Sender,EntityId Recipient,EntityId Address,EntityId ReturnAddress,EntityId Requester,long At,long Revision,FactionStanceAuthority? Authority,string Root,string? Related,string? CopyOf,long? ObservedAt,string? Evidence);
public sealed record TruceConfirmationState(int Schema,string Policy,TruceConfirmationEntry[] Entries,TruceConfirmationPaper[] Papers,FactionNoticeOpening[] Openings,FactionNoticeOpening[] FailureOpenings);
public sealed record TruceConfirmationCard(string Root,EntityId Owner,EntityId Acceptance,EntityId Sender,EntityId Recipient,long Until,string Status,string[] Retryable);
public sealed record TruceConfirmationCandidate(EntityId Owner,EntityId Acceptance,EntityId Signer,long Until);
public sealed record TruceConfirmationView(TruceConfirmationCard[] Sent,TruceConfirmationPaper[] Reports,string[] Acknowledgeable);
// The political signer, the person arranging the letter and the report recipient are distinct.
public static partial class TruceConfirmations
{
    public const string Policy="signed-successor-confirmation-physical-requester-receipts-failure-relay-v1",Kind="truce-succession-confirmation";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string text){if(!ok)throw new InvalidDataException(text);}
    static readonly TruceConfirmationState Empty=new(1,Policy,[],[],[],[]);
    static TruceConfirmationState State(ContractWorld w)=>w.TruceConfirmations??Empty;
    public static bool Enabled(ContractWorld w)=>HouseSuccession.Enabled(w)&&TruceNegotiations.Enabled(w);
    static bool Before(long t,long r,long at,long rev)=>t<at||t==at&&r<=rev;
    static TruceConfirmationPaper[] Papers(ContractWorld w,long at,long rev)=>State(w).Papers.Where(p=>Before(p.At,p.Revision,at,rev)).ToArray();
    static string Hash(string s)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(s));
    static string MessageId(EntityId id)=>"message:truce-confirmation/"+Hash(id.Value);
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>State(w).Papers.Any(p=>p.Request==id);
    static EntityId Other(TruceConfirmationPaper p)=>p.Terms.A==p.Owner?p.Terms.B:p.Terms.A;
    static bool Opened(ContractWorld w,string message,EntityId actor,long at,long rev)=>State(w).Openings.Any(o=>o.Message==message&&o.Actor==actor&&Before(o.At,o.Revision,at,rev));
    static bool Local(ContractWorld w,EntityId actor,EntityId site,long at,long rev)=>CommunityStewardship.Local(w,actor,site,at,rev+1);
    static TruceConfirmationPaper Root(ContractWorld w,TruceConfirmationPaper p)=>State(w).Papers.Single(q=>q.Id==p.Root&&q.Kind==TruceConfirmationKind.Confirmation&&q.CopyOf==null);
    static MessageEnvelope? Failure(ContractWorld w,TruceConfirmationPaper p,long at,long rev)=>w.Messaging.Messages.FirstOrDefault(m=>m.Kind=="delivery-failure"&&m.RecipientId==p.Sender.Value&&w.Messaging.Attempts.Any(a=>a.Id==m.Body&&a.MessageId==p.Id)&&State(w).FailureOpenings.Any(o=>o.Message==m.Id&&o.Actor==p.Sender&&Before(o.At,o.Revision,at,rev)));
    static bool FailureKnownByRequester(ContractWorld w,TruceConfirmationPaper p,long at,long rev)=>p.Requester==p.Sender?Failure(w,p,at,rev)!=null:Papers(w,at,rev).Any(n=>n.Kind==TruceConfirmationKind.FailureNotice&&n.Related==p.Id&&n.Recipient==p.Requester&&Opened(w,n.Id,p.Requester,at,rev));
    static void LocalSigner(ContractWorld w,EntityId owner,EntityId acceptance,EntityId signer,EntityId requester,EntityId home,long at,long rev)
    {
        Need(HouseSuccession.Head(w,owner,at,rev)==signer&&Local(w,signer,home,at,rev)&&Local(w,requester,home,at,rev)&&!PersonalLife.Resting(w,at,rev)&&!CastleAdministration.Blocked(w,home,at,rev),"現当主と依頼者が現地で実務できる状態ではありません");
        Need((signer==requester||w.Characters.Single(c=>c.Id==signer).WillObey)&&w.TruceInheritance?.Entries.Any(e=>e.Input.Owner==owner&&e.Input.Acceptance==acceptance&&e.Successor==signer&&(requester==signer||e.Previous==requester)&&Before(e.At,e.Revision,at,rev))==true&&TruceNegotiations.Known(w,signer,at,rev).Any(f=>f.Acceptance==acceptance),"実際に確認した原本と、新当主本人の同意が必要です");
    }
    static TruceConfirmationPaper Make(ContractWorld w,TruceConfirmationInput i,EntityId actor,long at,long rev)
    {
        Need(Enabled(w)&&EntityId.IsValid(i.Id.Value)&&Enum.IsDefined(i.Action)&&actor==w.PlayerControl,"本人の確認書操作ではありません");long before=rev-1;
        if(i.Action==TruceConfirmationAction.Acknowledge){Need(i.Original!=null,"受け取った確認書を指定してください");var p=Papers(w,at,before).Single(p=>p.Id==i.Original);Need(p.Kind==TruceConfirmationKind.Confirmation&&p.Recipient==actor&&Other(p)==i.Owner&&p.Acceptance==i.Acceptance,"本人宛ての同じ確認書ではありません");return Receipt(w,p,i.Id,at,rev);}
        var formation=TruceNegotiations.Known(w,HouseSuccession.Head(w,i.Owner,at,before),at,before).SingleOrDefault(f=>f.Acceptance==i.Acceptance);Need(formation!=null&&at<formation.Terms.Until&&(formation.Terms.A==i.Owner||formation.Terms.B==i.Owner),"現当主が知る期限内の成立原本が必要です");
        var head=HouseSuccession.Head(w,i.Owner,at,before);var home=new EntityId(HouseSuccession.Factions(w,at,before).Single(f=>f.Id==i.Owner.Value).Home);LocalSigner(w,i.Owner,i.Acceptance,head,actor,home,at,before);
        var authority=FactionStances.ResolveAuthority(w,head,i.Owner,at,before);Need(authority!=null,"現在の署名権限がありません");
        if(i.Action==TruceConfirmationAction.Resend){Need(i.Original!=null,"既知不達の原本を指定してください");var old=Papers(w,at,before).Single(p=>p.Id==i.Original);var root=Root(w,old);Need(old.Kind==TruceConfirmationKind.Confirmation&&old.Owner==i.Owner&&old.Acceptance==i.Acceptance&&root.Requester==actor&&root.Sender==head&&FailureKnownByRequester(w,old,at,before)&&!Papers(w,at,before).Any(q=>q.Root==root.Id&&q.Kind==TruceConfirmationKind.Receipt&&Opened(w,q.Id,actor,at,before))&&Papers(w,at,before).Count(p=>p.Root==root.Id&&p.Kind==TruceConfirmationKind.Confirmation)<3,"本人が不達返報を開封してから同じ原本を三通まで再送できます");Need(HouseSuccession.Head(w,Other(root),at,before)==root.Recipient,"相手代表の再確認が必要です");return root with{Id=MessageId(i.Id),Request=i.Id,At=at,Revision=rev,Authority=authority,CopyOf=old.Id};}
        Need(i.Original==null&&!Papers(w,at,before).Any(p=>p.Kind==TruceConfirmationKind.Confirmation&&p.Owner==i.Owner&&p.Acceptance==i.Acceptance),"この成立原本の確認書は既に発送しています");
        var original=w.Truces!.Papers.Single(p=>p.Input.Id==i.Acceptance);var other=formation.Terms.A==i.Owner?formation.Terms.B:formation.Terms.A;var recipient=original.Input.Owner==other?original.Author:original.Recipient;var address=original.Input.Owner==other?original.Authority.Home:original.Address;
        Need(HouseSuccession.Head(w,other,at,before)==recipient,"既知の相手代表が現代表ではありません。再確認が必要です");string id=MessageId(i.Id);return new(id,i.Id,TruceConfirmationKind.Confirmation,i.Owner,i.Acceptance,formation.Terms,head,recipient,address,home,actor,at,rev,authority,id,null,null,null,null);
    }
    static TruceConfirmationPaper Receipt(ContractWorld w,TruceConfirmationPaper p,EntityId id,long at,long rev)
    {
        Need(p.Kind==TruceConfirmationKind.Confirmation&&Opened(w,p.Id,p.Recipient,at,rev-1)&&!Papers(w,at,rev-1).Any(q=>q.Root==p.Root&&q.Kind==TruceConfirmationKind.Receipt),"同じ確認書の本人開封が必要です。受領返報は一度だけです");
        var root=Root(w,p);var authority=FactionStances.ResolveAuthority(w,p.Recipient,Other(p),at,rev-1);Need(authority!=null,"現代表本人の受領返報ではありません");var o=State(w).Openings.Single(o=>o.Message==p.Id&&o.Actor==p.Recipient);return new(MessageId(id),id,TruceConfirmationKind.Receipt,Other(p),p.Acceptance,p.Terms,p.Recipient,root.Requester,root.ReturnAddress,root.Address,root.Requester,at,rev,authority,root.Id,p.Id,null,o.At,p.Id);
    }
    static EntityId AutoId(ContractWorld w,string kind,string source){string root="request:truce-confirmation-"+kind+"/"+Hash(source);var id=new EntityId(root);int n=0;while(FactionStances.OtherIdUsed(w,id,includeTruceConfirmations:false)||FactionStances.OwnIdUsed(w,id)||State(w).Entries.Any(e=>e.Input.Id==id))id=new(root+"/n"+(++n));return id;}
    static TruceConfirmationPaper FailureNotice(ContractWorld w,TruceConfirmationPaper p,MessageEnvelope failure,EntityId id,long at,long rev){var root=Root(w,p);long? known=MessageFlow.LearnedAt(w.Messaging,failure.Id,p.Sender.Value);Need(known!=null&&known<=at&&p.Sender!=p.Requester,"署名者が実際に認識した不達が必要です");return new(MessageId(id),id,TruceConfirmationKind.FailureNotice,p.Owner,p.Acceptance,p.Terms,p.Sender,root.Requester,root.ReturnAddress,root.Address,root.Requester,at,rev,null,root.Id,p.Id,null,known,failure.Id);}
    static TruceConfirmationPaper? Next(ContractWorld w)
    {
        if(!Enabled(w))return null;long at=w.ElapsedSeconds,rev=w.CommitSequence+1;var ps=Papers(w,at,rev-1);
        foreach(var p in ps.Where(p=>p.Kind==TruceConfirmationKind.Confirmation)){
            if(p.Recipient!=w.PlayerControl&&Opened(w,p.Id,p.Recipient,at,rev-1)&&!ps.Any(q=>q.Root==p.Root&&q.Kind==TruceConfirmationKind.Receipt)&&FactionStances.ResolveAuthority(w,p.Recipient,Other(p),at,rev-1)!=null)return Receipt(w,p,AutoId(w,"receipt",p.Root),at,rev);
            if(p.Sender!=w.PlayerControl&&p.Sender!=p.Requester&&Failure(w,p,at,rev-1) is {} failure&&!ps.Any(q=>q.Kind==TruceConfirmationKind.FailureNotice&&q.Evidence==failure.Id))return FailureNotice(w,p,failure,AutoId(w,"failure",failure.Id),at,rev);
        }return NextReportRetry(w,ps,at,rev);
    }
    static MessageEnvelope Envelope(TruceConfirmationPaper p)=>new(p.Id,p.Root,Kind,p.Sender.Value,p.Recipient.Value,System.Text.Encoding.UTF8.GetString(ContractJson.Write(p)),p.Address.Value,p.At);
    static void Send(ContractWorld w,TruceConfirmationPaper p){w.TruceConfirmations=State(w) with{Papers=[..State(w).Papers,p]};MessageFlow.Send(w.Messaging,Envelope(p),p.At,1);w.SealedMessages=[..w.SealedMessages,new(p.Id)];}
    internal static void Apply(ContractWorld w,TruceConfirmationInput i){var p=Make(w,i,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence);w.TruceConfirmations=State(w) with{Entries=[..State(w).Entries,new(i,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence,p.Id)]};Send(w,p);}
    internal static void Automatic(ContractWorld w){var before=w.ForkTransportLocation();before.CommitSequence--;var p=Next(before);Need(p!=null,"返報の根拠がありません");Send(w,p);}
    internal static void TakeTurns(WorldSession session){for(int n=0;n<32;n++){if(Next(session.DiagnosticSnapshot())==null)return;session.AutoTruceConfirmation();}}
    internal static void RecordOpening(ContractWorld w,EntityId actor,string id){var p=State(w).Papers.SingleOrDefault(p=>p.Id==id&&p.Recipient==actor);if(p==null){if(w.Messaging.Messages.Any(m=>m.Id==id&&m.Kind=="delivery-failure"&&m.RecipientId==actor.Value&&State(w).Papers.Any(q=>w.Messaging.Attempts.Any(a=>a.Id==m.Body&&a.MessageId==q.Id)))&&!State(w).FailureOpenings.Any(o=>o.Message==id)){Need(MessageFlow.Knows(w.Messaging,id,actor.Value),"本人の不達認識ではありません");w.TruceConfirmations=State(w) with{FailureOpenings=[..State(w).FailureOpenings,new(id,actor,w.ElapsedSeconds,w.CommitSequence)]};}return;}if(State(w).Openings.Any(o=>o.Message==id))return;Need(MessageFlow.Knows(w.Messaging,id,actor.Value),"本人の開封ではありません");w.TruceConfirmations=State(w) with{Openings=[..State(w).Openings,new(id,actor,w.ElapsedSeconds,w.CommitSequence)]};}
    internal static void Update(ContractWorld w)
    {
        if(w.TruceConfirmations==null)return;
        foreach(var p in State(w).Papers.Where(p=>p.Recipient!=w.PlayerControl).ToArray())if(w.Messaging.Attempts.Any(a=>a.MessageId==p.Id&&a.ReceiverId==p.Recipient.Value&&a.Status==AttemptStatus.Delivered)){if(!MessageFlow.Knows(w.Messaging,p.Id,p.Recipient.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(p.Id,p.Recipient.Value,w.ElapsedSeconds,p.Id)];RecordOpening(w,p.Recipient,p.Id);}
        foreach(var f in w.Messaging.Messages.Where(m=>m.Kind=="delivery-failure"&&m.RecipientId!=w.PlayerControl.Value&&State(w).Papers.Any(p=>w.Messaging.Attempts.Any(a=>a.Id==m.Body&&a.MessageId==p.Id))).ToArray())if(w.Messaging.Attempts.Any(a=>a.MessageId==f.Id&&a.ReceiverId==f.RecipientId&&a.Status==AttemptStatus.Delivered)){if(!MessageFlow.Knows(w.Messaging,f.Id,f.RecipientId))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(f.Id,f.RecipientId,w.ElapsedSeconds,f.Id)];RecordOpening(w,new(f.RecipientId),f.Id);}
    }
    public static string? Address(ContractWorld w,MessageEnvelope m)=>State(w).Papers.SingleOrDefault(p=>p.Id==m.Id) is {} p?NewGameWorld.Node(p.Address.Value):null;
    internal static bool Learned(ContractWorld w,EntityId actor,EntityId acceptance,long at,long rev)=>Papers(w,at,rev).Any(p=>p.Kind==TruceConfirmationKind.Confirmation&&p.Acceptance==acceptance&&p.Recipient==actor&&Opened(w,p.Id,actor,at,rev));
    public static TruceConfirmationView View(ContractWorld w)
    {
        long at=w.ElapsedSeconds,rev=w.CommitSequence;var known=State(w).Papers.Where(p=>p.Recipient==w.PlayerControl&&Opened(w,p.Id,w.PlayerControl,at,rev)).ToArray();var roots=State(w).Papers.Where(p=>p.Kind==TruceConfirmationKind.Confirmation&&p.CopyOf==null&&(p.Requester==w.PlayerControl||p.Sender==w.PlayerControl));var cards=roots.Select(p=>{var receipt=known.FirstOrDefault(q=>q.Root==p.Id&&q.Kind==TruceConfirmationKind.Receipt);var retry=new List<string>();foreach(var q in State(w).Papers.Where(q=>q.Root==p.Id&&q.Kind==TruceConfirmationKind.Confirmation)){try{Make(w,new(new("request:truce-retry-preview"),TruceConfirmationAction.Resend,p.Owner,p.Acceptance,q.Id),w.PlayerControl,at,rev+1);retry.Add(q.Id);}catch(InvalidDataException){}}string status=receipt!=null?$"相手開封の返報を確認（観測 {receipt.ObservedAt}秒）":State(w).Papers.Any(q=>q.Root==p.Id&&q.Kind==TruceConfirmationKind.Confirmation&&FailureKnownByRequester(w,q,at,rev))?"不達の返報を認識・到達とは別":"発送控え・相手の到達は未確認";return new TruceConfirmationCard(p.Id,p.Owner,p.Acceptance,p.Sender,p.Recipient,p.Terms.Until,status,retry.ToArray());}).ToArray();
        return new(cards,known.Where(p=>p.Kind!=TruceConfirmationKind.Confirmation).ToArray(),known.Where(p=>p.Kind==TruceConfirmationKind.Confirmation&&!State(w).Papers.Any(q=>q.Root==p.Root&&q.Kind==TruceConfirmationKind.Receipt)&&FactionStances.ResolveAuthority(w,w.PlayerControl,Other(p),at,rev)!=null).Select(p=>p.Id).ToArray());
    }
    public static TruceConfirmationCandidate[] Candidates(ContractWorld w){var result=new List<TruceConfirmationCandidate>();foreach(var e in w.TruceInheritance?.Entries??[]){try{var p=Make(w,new(new("request:truce-send-preview"),TruceConfirmationAction.Send,e.Input.Owner,e.Input.Acceptance),w.PlayerControl,w.ElapsedSeconds,w.CommitSequence+1);result.Add(new(p.Owner,p.Acceptance,p.Sender,p.Terms.Until));}catch(InvalidDataException){}}return result.ToArray();}
    internal static IEnumerable<(long At,long Revision)> Inputs(ContractWorld w)=>State(w).Papers.Select(p=>(p.At,p.Revision)).Concat(State(w).Openings.Where(o=>o.Actor==w.PlayerControl).Select(o=>(o.At,o.Revision))).Concat(State(w).FailureOpenings.Where(o=>o.Actor==w.PlayerControl).Select(o=>(o.At,o.Revision)));
    internal static bool Pending(ContractWorld w)=>Next(w)!=null;
    internal static int BoundaryCost(ContractWorld w)=>State(w).Papers.Select(p=>p.Recipient).Distinct().Count()*2;
    internal static void Rewind(ContractWorld w,long at,long rev){if(w.TruceConfirmations is {} s)w.TruceConfirmations=s with{Entries=s.Entries.Where(e=>Before(e.At,e.Revision,at,rev)).ToArray(),Papers=s.Papers.Where(p=>Before(p.At,p.Revision,at,rev)).ToArray(),Openings=s.Openings.Where(o=>Before(o.At,o.Revision,at,rev)).ToArray(),FailureOpenings=s.FailureOpenings.Where(o=>Before(o.At,o.Revision,at,rev)).ToArray()};}
    internal static void Validate(ContractWorld w)
    {
        if(w.TruceConfirmations==null)return;Need(Enabled(w)&&w.TruceConfirmations is {Schema:1,Policy:Policy,Entries:not null,Papers:not null,Openings:not null,FailureOpenings:not null},"確認書の版が不正です");var s=State(w);Need(s.Papers.Select(p=>p.Id).Distinct().Count()==s.Papers.Length&&s.Papers.Select(p=>p.Request).Distinct().Count()==s.Papers.Length&&s.Openings.Select(o=>o.Message).Distinct().Count()==s.Openings.Length,"確認書・開封が重複しています");Need(s.Papers.Zip(s.Papers.Skip(1)).All(q=>q.First.At<=q.Second.At&&q.First.Revision<q.Second.Revision),"Confirmation paper chronology differs");long at=0,rev=0;
        foreach(var e in s.Entries){Need(e.At>=at&&e.At<=w.ElapsedSeconds&&e.Revision>rev&&e.Revision<=w.CommitSequence&&e.Actor==w.PlayerControl&&s.Papers.Single(p=>p.Id==e.Paper)==Make(w,e.Input,e.Actor,e.At,e.Revision),"確認書の依頼・署名根拠が一致しません");at=e.At;rev=e.Revision;}
        foreach(var p in s.Papers){Need(p.At<=w.ElapsedSeconds&&p.Revision>0&&p.Revision<=w.CommitSequence&&w.Messaging.Messages.Contains(Envelope(p))&&w.SealedMessages.Contains(new(p.Id))&&!FactionStances.OtherIdUsed(w,p.Request,includeTruceConfirmations:false)&&!FactionStances.OwnIdUsed(w,p.Request),"確認書の封書・原本・IDが不正です");if(!s.Entries.Any(e=>e.Paper==p.Id)){var prior=w.ForkTransportLocation();prior.ElapsedSeconds=p.At;prior.CommitSequence=p.Revision-1;Need(Next(prior)==p,"自動返報の認識・宛先・観測が一致しません");}}
        foreach(var o in s.Openings){var p=s.Papers.Single(p=>p.Id==o.Message);Need(o.Actor==p.Recipient&&o.At>=p.At&&o.At<=w.ElapsedSeconds&&o.Revision>p.Revision&&o.Revision<=w.CommitSequence&&MessageFlow.LearnedAt(w.Messaging,p.Id,o.Actor.Value)==o.At&&w.Messaging.Attempts.Any(a=>a.MessageId==p.Id&&a.ReceiverId==o.Actor.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=o.At),"確認書の受領・本人開封が違います");}
        Need(s.FailureOpenings.Select(o=>o.Message).Distinct().Count()==s.FailureOpenings.Length,"不達認識が重複しています");foreach(var o in s.FailureOpenings){var f=w.Messaging.Messages.Single(m=>m.Id==o.Message);Need(f.Kind=="delivery-failure"&&f.RecipientId==o.Actor.Value&&o.At<=w.ElapsedSeconds&&o.Revision>0&&o.Revision<=w.CommitSequence&&MessageFlow.LearnedAt(w.Messaging,f.Id,o.Actor.Value)<=o.At&&s.Papers.Any(p=>w.Messaging.Attempts.Any(a=>a.Id==f.Body&&a.MessageId==p.Id))&&w.Messaging.Attempts.Any(a=>a.MessageId==f.Id&&a.ReceiverId==o.Actor.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=o.At),"不達返報の本人認識が違います");}
        foreach(var p in s.Papers)Need(MessageFlow.Knows(w.Messaging,p.Id,p.Recipient.Value)==s.Openings.Any(o=>o.Message==p.Id),"未開封の確認書を知っています");Need(w.Messaging.Messages.Count(m=>m.Kind==Kind)==s.Papers.Length,"確認書に配送原本がありません");
    }
}
public sealed partial class WorldSession
{
    internal void AutoTruceConfirmation(){lock(gate)Change(TruceConfirmations.Automatic);}
    public TruceConfirmationCandidate[] TruceConfirmationCandidates(){lock(gate)return TruceConfirmations.Candidates(state);}
    public TruceConfirmationView TruceConfirmationView(){lock(gate)return TruceConfirmations.View(state);}
    public CommandResult SendTruceConfirmation(TruceConfirmationInput i,EntityId actor,long revision){lock(gate){if(actor!=state.PlayerControl)return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,"本人の確認書操作ではありません");var old=state.TruceConfirmations?.Entries.SingleOrDefault(e=>e.Input.Id==i.Id);if(old!=null)return new(old.Input==i?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"受付済みです");if(FactionStances.OtherIdUsed(state,i.Id)||FactionStances.OwnIdUsed(state,i.Id))return new(ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"別の原本です");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,i.Id,state.CommitSequence,"表示を更新してください");try{Change(w=>TruceConfirmations.Apply(w,i));return new(ResultCode.Accepted,i.Id,state.CommitSequence,"現当主の確認書・返報を発送しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,e.Message);}}}
}
