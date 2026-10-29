using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public enum FreightLetterAction { Cancel,ResendCancellation }
public sealed record FreightLetterInput(EntityId Id,EntityId Job,FreightLetterAction Action,EntityId Address,long MeetAt,string? Original=null);
public sealed record FreightLetterEntry(FreightLetterInput Input,EntityId Actor,long At,long Revision);
public sealed record FreightObservation(EntityId Operation,long ObservedAt,string Statement,long Cargo,long Cash,HouseFreightPhase Phase) { [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public EntityId? AtSite {get;init;} [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? SupplyDeliveredKg {get;init;} };
public sealed record FreightPaper(string Id,EntityId Job,string Kind,EntityId Sender,EntityId Recipient,EntityId Address,long MeetAt,long At,long Revision,FreightObservation? Observation,string? Original);
public sealed record FreightLetterReceipt(string Message,long At,long Revision);
public sealed record FreightOperationsState(int Schema,string Policy,FreightLetterEntry[] Entries,FreightPaper[] Papers,FreightLetterReceipt[] Receipts);
public static class FreightOperations
{
    public const string Policy="physical-freight-dated-reports-rendezvous-cancellation-v14",ReportKind="freight-status-report",CancelKind="freight-cancel-order",AckKind="freight-cancel-ack";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or FieldConvoy.Policy or ArmyRoadMarch.Policy or RoadConvoy.Policy or PersonalLife.Policy or CastleHandover.Policy or CastleAdministration.Policy;
    static string Hash(string s)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(s));
    static bool Before(long t,long r,long at,long rev)=>t<at||t==at&&r<rev;
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.FreightOperations?.Entries.Any(e=>e.Input.Id==id)==true;
    internal static bool Stopped(ContractWorld w,EntityId job,long at,long rev)=>Enabled(w)&&w.FreightOperations!.Receipts.Any(r=>Before(r.At,r.Revision,at,rev)&&w.FreightOperations.Papers.Any(p=>p.Id==r.Message&&p.Job==job&&p.Kind==CancelKind));
    static HouseFreightTerms Terms(ContractWorld w,EntityId job)
    {var normal=w.HouseFreight?.Entries.SingleOrDefault(e=>e.Terms?.Id==job);if(normal!=null)return normal.Terms!;var supply=w.CampConvoys?.Entries.SingleOrDefault(e=>e.Terms?.Id==job)?.Terms;Need(RoadConvoy.Enabled(w)&&supply?.RoadDestination!=null,"有効な運搬または街道補給原本がありません");return new(supply.Id,supply.Carrier,supply.Principal,supply.Owner,supply.Home,supply.RoadDestination.Value,null,supply.RiceKg,0,supply.At,supply.Until,supply.Revision);}
    static MessageEnvelope Envelope(FreightPaper p)=>new(p.Id,p.Job.Value,p.Kind,p.Sender.Value,p.Recipient.Value,System.Text.Encoding.UTF8.GetString(ContractJson.Write(p)),p.Address.Value,p.At);
    static void Send(ContractWorld w,FreightPaper p){Need(!w.Messaging.Messages.Any(m=>m.Id==p.Id),"発送原本IDが重複しています");w.FreightOperations=w.FreightOperations! with{Papers=[..w.FreightOperations!.Papers,p]};MessageFlow.Send(w.Messaging,Envelope(p),p.At,1);w.SealedMessages=[..w.SealedMessages,new(p.Id)];}
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.FreightOperations=new(1,Policy,[],[],[]);}
    static FreightPaper Cancellation(ContractWorld w,FreightLetterEntry e)
    {
        var i=e.Input;var t=Terms(w,i.Job);Need(t.Principal==e.Actor&&e.Actor==w.PlayerControl&&t.Carrier!=e.Actor&&e.At>=t.At&&e.Revision>t.Revision,"当主本人が合意した家臣運搬を指定してください");
        Need(Enum.IsDefined(i.Action)&&(i.Address==t.Source||i.Address==t.Home)&&i.MeetAt>=e.At+1&&i.MeetAt<=Math.Max(t.Until,e.At+86400),"当初の仕入先または帰還先と、将来の待合時刻を指定してください");
        if(i.Action==FreightLetterAction.Cancel)Need(i.Original==null&&!w.FreightOperations!.Entries.Any(q=>q.Input.Job==t.Id&&q.Input.Action==FreightLetterAction.Cancel&&q.Revision<e.Revision),"取消原本は既に発送済みです");
        else{Need(i.Original!=null,"再送する取消原本を指定してください");var old=w.FreightOperations!.Papers.SingleOrDefault(p=>p.Id==i.Original&&p.Job==t.Id&&p.Kind==CancelKind&&p.At<=e.At&&p.Revision<e.Revision);Need(old!=null&&KnownFailure(w,old,e.At)!=null,"本人が知った不達を確認してから再送してください");Need(w.FreightOperations.Papers.Count(p=>p.Job==t.Id&&p.Kind==CancelKind&&p.Revision<e.Revision)<3,"同じ取消の発送は三通までです");}
        return new("message:freight-cancel/"+Hash(i.Id.Value),t.Id,CancelKind,t.Principal,t.Carrier,i.Address,i.MeetAt,e.At,e.Revision,null,i.Original);
    }
    internal static void Apply(ContractWorld w,FreightLetterInput i){var e=new FreightLetterEntry(i,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence);var p=Cancellation(w,e);w.FreightOperations=w.FreightOperations! with{Entries=[..w.FreightOperations!.Entries,e]};Send(w,p);}
    static FreightObservation Observe(ContractWorld w,HouseFreightEntry e)
    {
        var t=e.Terms??Terms(w,e.Input.Job!.Value);var j=HouseLogistics.FreightJobs(w,e.At,e.Revision+1).Single(j=>j.Terms.Id==t.Id);return new(e.Input.Id,e.At,e.Input.Action==HouseFreightAction.Buy?"現地で仕入れた":e.Input.Action==HouseFreightAction.Withdraw?"新規の仕入れを取りやめ、現物と余金を返す":"元の家で現物と余金を精算した",j.Cargo,j.Cash,j.Phase);
    }
    static FreightPaper StatusPaper(ContractWorld w,HouseFreightEntry e){var t=Terms(w,e.Input.Job!.Value);return new("message:freight-status/"+Hash(e.Input.Id.Value),t.Id,ReportKind,t.Carrier,t.Principal,t.Home,e.At+1,e.At,e.Revision,Observe(w,e),e.Input.Id.Value);}
    static FreightPaper StatusPaper(ContractWorld w,CampConvoyEntry e)
    {
        var t=w.CampConvoys!.Entries.Single(q=>q.Terms?.Id==e.Input.Job).Terms!;var j=FieldConvoy.Jobs(w,e.At,e.Revision+1).Single(q=>q.Terms.Id==t.Id);
        string statement=e.Input.Action switch{CampConvoyAction.Deliver=>"集合拠点で"+e.TransferredKg+"kgを受渡し",CampConvoyAction.Return=>"元の家で残米"+e.ReturnedKg+"kgを返納",CampConvoyAction.ReportStop=>"街道の通行止めで停止し経路を再検討",CampConvoyAction.CancelAtMeeting=>"現地で取消を受領し残米を返す",_=>"残米を保持して元の家へ帰還"};
        var phase=j.Returned!=null?(j.DeliveredKg>0?HouseFreightPhase.Completed:HouseFreightPhase.Returned):j.ReturnStarted!=null?HouseFreightPhase.ReturnRequired:HouseFreightPhase.Carrying;
        var point=RegionalCampaign.RoadActorPointAt(w,t.Carrier,e.At);var node=BranchRoutes.GraphNodes(w).FirstOrDefault(n=>n.Position==point);var observation=new FreightObservation(e.Input.Id,e.At,statement,j.CargoKg,0,phase){AtSite=node==null?null:RegionalCampaign.Site(node.Id),SupplyDeliveredKg=j.DeliveredKg};
        return new("message:freight-status/"+Hash(e.Input.Id.Value),t.Id,ReportKind,t.Carrier,t.Principal,t.Home,e.At+1,e.At,e.Revision,observation,e.Input.Id.Value);
    }
    static FreightPaper StatusOriginal(ContractWorld w,string original)
    {var e=w.HouseFreight!.Entries.SingleOrDefault(e=>e.Input.Id.Value==original);return e!=null?StatusPaper(w,e):StatusPaper(w,w.CampConvoys!.Entries.Single(e=>e.Input.Id.Value==original));}
    static MessageEnvelope? KnownFailure(ContractWorld w,FreightPaper p,long at)=>w.Messaging.Messages.FirstOrDefault(m=>m.Kind=="delivery-failure"&&m.RecipientId==p.Sender.Value&&w.Messaging.Attempts.Any(a=>a.Id==m.Body&&a.MessageId==p.Id)&&MessageFlow.LearnedAt(w.Messaging,m.Id,p.Sender.Value) is {} learned&&learned<=at);
    static long RetryAt(ContractWorld w,FreightPaper p,MessageEnvelope failure)=>checked(MessageFlow.LearnedAt(w.Messaging,failure.Id,p.Sender.Value)!.Value+60);
    internal static void Tick(ContractWorld w)
    {
        if(!Enabled(w))return;
        foreach(var e in w.HouseFreight!.Entries.Where(e=>e.Actor!=w.PlayerControl&&e.Input.Action is HouseFreightAction.Buy or HouseFreightAction.Withdraw or HouseFreightAction.Receive).ToArray()){
            var p=StatusPaper(w,e);if(!w.FreightOperations!.Papers.Any(q=>q.Id==p.Id))Send(w,p);
        }
        if(RoadConvoy.Enabled(w))foreach(var e in w.CampConvoys!.Entries.Where(e=>e.Input.Job!=null&&e.Input.Action is CampConvoyAction.Deliver or CampConvoyAction.StartReturn or CampConvoyAction.CancelAtMeeting or CampConvoyAction.Return or CampConvoyAction.ReportStop).Where(e=>w.CampConvoys.Entries.Any(q=>q.Terms?.Id==e.Input.Job&&q.Terms?.RoadDestination!=null)).ToArray()){
            var p=StatusPaper(w,e);if(!w.FreightOperations!.Papers.Any(q=>q.Id==p.Id))Send(w,p);
        }
        foreach(var p in w.FreightOperations!.Papers.Where(p=>p.Kind==CancelKind).ToArray()){
            if(w.FreightOperations.Receipts.Any(r=>r.Message==p.Id))continue;var delivered=w.Messaging.Attempts.Where(a=>a.MessageId==p.Id&&a.ReceiverId==p.Recipient.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=w.ElapsedSeconds).OrderBy(a=>a.FinishedAt).FirstOrDefault();if(delivered==null)continue;
            var receipt=new FreightLetterReceipt(p.Id,delivered.FinishedAt!.Value,w.CommitSequence);w.FreightOperations=w.FreightOperations with{Receipts=[..w.FreightOperations.Receipts,receipt]};if(!MessageFlow.Knows(w.Messaging,p.Id,p.Recipient.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(p.Id,p.Recipient.Value,receipt.At,p.Id)];
            var t=Terms(w,p.Job);Send(w,new("message:freight-ack/"+Hash(p.Id),p.Job,AckKind,t.Carrier,t.Principal,t.Home,receipt.At+1,receipt.At,receipt.Revision,null,p.Id));
        }
        foreach(var old in w.FreightOperations.Papers.Where(p=>p.Kind==ReportKind).ToArray()){
            var failure=KnownFailure(w,old,w.ElapsedSeconds);if(failure==null||w.ElapsedSeconds<RetryAt(w,old,failure)||w.FreightOperations.Papers.Count(p=>p.Job==old.Job&&p.Observation==old.Observation)>=3)continue;
            var place=w.Locations.Single(l=>l.Actor==old.Sender);if(place.Kind!=LocationKind.AtSite||place.Site==null||!TransportWork.At(w,old.Sender,place.Site.Value,w.ElapsedSeconds,w.CommitSequence+1,true))continue;
            string id="message:freight-retry/"+Hash(old.Id+"/"+failure.Id);if(w.FreightOperations.Papers.Any(p=>p.Id==id))continue;Send(w,new(id,old.Job,ReportKind,old.Sender,old.Recipient,old.Address,w.ElapsedSeconds+1,w.ElapsedSeconds,w.CommitSequence,old.Observation,old.Id));
        }
    }
    internal static long? NextBoundary(ContractWorld w,long target)
    {if(!Enabled(w))return null;return w.FreightOperations!.Papers.Where(p=>p.Kind==ReportKind).Select(p=>{var f=KnownFailure(w,p,w.ElapsedSeconds);return f==null?(long?)null:RetryAt(w,p,f);}).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().FirstOrDefault();}
    public static string? Address(ContractWorld w,MessageEnvelope m)=>Enabled(w)&&w.FreightOperations!.Papers.SingleOrDefault(p=>p.Id==m.Id) is {} p?RegionalCampaign.Destination(w,p.Address):null;
    internal static long? Appointment(ContractWorld w,MessageEnvelope m)=>Enabled(w)?w.FreightOperations!.Papers.SingleOrDefault(p=>p.Id==m.Id)?.MeetAt:null;
    public static FreightObservation? KnownObservation(ContractWorld w,EntityId job)=>!Enabled(w)?null:w.FreightOperations!.Papers.Where(p=>p.Job==job&&p.Kind==ReportKind&&MessageFlow.Knows(w.Messaging,p.Id,w.PlayerControl.Value)).Select(p=>p.Observation).OrderBy(p=>p!.ObservedAt).ThenBy(p=>p!.Operation.Value,StringComparer.Ordinal).LastOrDefault();
    internal static string KnownStatus(ContractWorld w,EntityId job,string fallback)
    {if(!Enabled(w))return fallback;var papers=w.FreightOperations!.Papers.Where(p=>p.Job==job).ToArray();if(papers.Any(p=>p.Kind==AckKind&&MessageFlow.Knows(w.Messaging,p.Id,w.PlayerControl.Value)))return "取消受領の返報を開封・現物と余金の帰還を待つ";if(papers.Any(p=>p.Kind==CancelKind&&p.Sender==w.PlayerControl))return "取消を発送・受領の返報は未確認";return KnownObservation(w,job)!=null?"開封した時点付き報告あり・現在の状態は未確認":fallback;}
    public static long SuggestedMeeting(ContractWorld w,EntityId job,EntityId address)
    {var t=Terms(w,job);string from=RegionalCampaign.Destination(w,t.Home),source=RegionalCampaign.Destination(w,t.Source);long depart=checked((t.At/60+1)*60+1);var path=BranchRoutes.Plan(from,source,[],w:w,departureAt:depart);long arrival=BranchRoutes.Schedule(from,path,depart,w)[^1].EndAt;if(address==t.Home){var back=BranchRoutes.Plan(source,from,[],w:w,departureAt:arrival+60);arrival=BranchRoutes.Schedule(source,back,arrival+60,w)[^1].EndAt;}return Math.Max(w.ElapsedSeconds+1,arrival);}
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.FreightOperations==null&&!w.Messaging.Messages.Any(m=>m.Kind is ReportKind or CancelKind or AckKind),"旧版へ運搬書状を追加できません");return;}Need(w.FreightOperations is{Schema:1,Policy:Policy,Entries:not null,Papers:not null,Receipts:not null},"運搬書状の原本がありません");var s=w.FreightOperations;long at=0,rev=0;var ids=new HashSet<EntityId>();
        foreach(var e in s.Entries){Need(EntityId.IsValid(e.Input.Id.Value)&&ids.Add(e.Input.Id)&&e.At>=at&&e.At<=w.ElapsedSeconds&&e.Revision>rev&&e.Revision<=w.CommitSequence&&!FactionStances.OtherIdUsed(w,e.Input.Id,includeFreightOperations:false)&&!FactionStances.OwnIdUsed(w,e.Input.Id),"運搬書状のID/順序が不正です");Need(s.Papers.Contains(Cancellation(w,e)),"取消の発送と原本が違います");at=e.At;rev=e.Revision;}
        Need(s.Papers.Select(p=>p.Id).Distinct().Count()==s.Papers.Length&&s.Receipts.Select(r=>r.Message).Distinct().Count()==s.Receipts.Length,"封書・受領が重複しています");
        foreach(var p in s.Papers){Need(p.At<=w.ElapsedSeconds&&p.Revision>0&&p.Revision<=w.CommitSequence&&w.Messaging.Messages.Contains(Envelope(p))&&w.SealedMessages.Contains(new(p.Id)),"共通配送の封書原本が違います");
            if(p.Kind==CancelKind)Need(s.Entries.Any(e=>Cancellation(w,e)==p),"取消に本人の発送操作がありません");
            else if(p.Kind==AckKind){var r=s.Receipts.Single(r=>r.Message==p.Original);var old=s.Papers.Single(o=>o.Id==r.Message);var t=Terms(w,p.Job);Need(p==new FreightPaper("message:freight-ack/"+Hash(old.Id),old.Job,AckKind,t.Carrier,t.Principal,t.Home,r.At+1,r.At,r.Revision,null,old.Id),"取消返報と受領が違います");}
            else{Need(p.Kind==ReportKind&&p.Observation!=null,"未知の運搬書状です");if(p.Id.StartsWith("message:freight-status/",StringComparison.Ordinal)){Need(p==StatusOriginal(w,p.Original!),"報告が当時の現物観測と違います");}else{var old=s.Papers.Single(o=>o.Id==p.Original&&o.Kind==ReportKind);var fail=KnownFailure(w,old,p.At);Need(fail!=null&&p.Id=="message:freight-retry/"+Hash(old.Id+"/"+fail.Id)&&p.At>=RetryAt(w,old,fail)&&p.Sender==old.Sender&&p.Recipient==old.Recipient&&p.Address==old.Address&&p.Job==old.Job&&p.Observation==old.Observation&&p.MeetAt==p.At+1&&p.Revision>=old.Revision,"再送が既知の不達/同じ観測と違います");}Need(s.Papers.Count(q=>q.Job==p.Job&&q.Observation==p.Observation)<=3,"報告の再送上限を超えました");}
        }
        foreach(var paper in s.Papers)foreach(var attempt in w.Messaging.Attempts.Where(a=>a.MessageId==paper.Id)){var courier=w.Couriers!.Missions.Single(c=>c.Attempt==attempt.Id);Need(courier.Destination==RegionalCampaign.Destination(w,paper.Address)&&courier.Appointment==paper.MeetAt,"Freight courier differs from its original agreed address/time");}
        foreach(var r in s.Receipts){var p=s.Papers.Single(p=>p.Id==r.Message&&p.Kind==CancelKind);Need(r.At>=p.At&&r.At<=w.ElapsedSeconds&&r.Revision>=p.Revision&&r.Revision<=w.CommitSequence&&w.Messaging.Attempts.Any(a=>a.MessageId==r.Message&&a.ReceiverId==p.Recipient.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt==r.At)&&MessageFlow.Knows(w.Messaging,p.Id,p.Recipient.Value),"取消受領が物理到達と違います");}
        if(RoadConvoy.Enabled(w))foreach(var p in s.Papers.Where(p=>p.Kind==CancelKind&&w.CampConvoys!.Entries.Any(e=>e.Terms?.Id==p.Job&&e.Terms?.RoadDestination!=null))){var reached=w.Messaging.Attempts.Where(a=>a.MessageId==p.Id&&a.ReceiverId==p.Recipient.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=w.ElapsedSeconds).OrderBy(a=>a.FinishedAt).FirstOrDefault();Need(reached==null?!s.Receipts.Any(r=>r.Message==p.Id):s.Receipts.Any(r=>r.Message==p.Id&&r.At==reached.FinishedAt),"取消の物理到達と受領記録が一致しません");}
        Need(w.Messaging.Messages.Count(m=>m.Kind is ReportKind or CancelKind or AckKind)==s.Papers.Length,"発送原本のない運搬書状があります");
    }
}
public sealed partial class WorldSession
{
    public CommandResult ManageFreightLetters(FreightLetterInput input,EntityId actor,long revision){lock(gate){if(actor!=state.PlayerControl||!FreightOperations.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の運搬書状ではありません");var old=state.FreightOperations!.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"発送済みです");if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の原本で使ったIDです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"条件を読み直してください");try{Change(w=>FreightOperations.Apply(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"封書を発送しました。相手の受領までは当初の合意が続きます");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}}
}
