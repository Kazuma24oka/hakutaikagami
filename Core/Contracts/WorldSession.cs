using HakutaiKagami.Core.Messaging;
using HakutaiKagami.Core.World;

namespace HakutaiKagami.Core.Contracts;

// A new seconds-domain integration session. Legacy sessions and the normal game entry stay untouched.
public sealed partial class WorldSession
{
    private readonly object gate = new();
    private ContractWorld state;
    public WorldSession(ContractWorld initial) { ContractValidation.Validate(initial); state = ContractJson.Copy(initial); }
    public EntityId PlayerControl { get { lock(gate) return state.PlayerControl; } }
    public long Revision { get { lock(gate) return state.CommitSequence; } }
    public long Seconds { get { lock(gate) return state.ElapsedSeconds; } }
    public bool Paused { get { lock(gate) return state.Paused; } }
    public ContractWorld DiagnosticSnapshot() { lock(gate) return ContractJson.Copy(state); }
    internal bool TryHandoff(long revision,Action swap)
    {lock(gate){if(state.CommitSequence!=revision)return false;swap();return true;}}
    internal static EntityId Allocate(ContractWorld s, string kind)
    {
        var n = checked(s.IdCounters.GetValueOrDefault(kind)+1); s.IdCounters[kind] = n;
        return new(kind+":"+n.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    internal static EntityId Child(string kind, EntityId task, string suffix) => new(kind+":"+task.Value.Replace(':','/')+"/"+suffix);
    private void Change(Action<ContractWorld> change, Action? beforeCommit = null)
    {
        var next = ContractJson.Copy(state); next.CommitSequence = checked(next.CommitSequence+1);
        change(next);NewGameService.Sync(next);NewGameMilitary.Sync(next);RegionalBattle.Sync(next); if(RegionalCampaign.Enabled(next))RegionalCampaign.Bind(next); NewGameMilitary.RecordMilitaryObservation(next); ContractValidation.Validate(next,state); beforeCommit?.Invoke(); state = next;
    }
    public CommandResult SetFormation(FieldRequest request,long revision){if(RegionalBattle.Enabled(state))return IssueRegional(new(request.Id,request.Actor,request.Actor,RegionalAction.Move,request.A,request.B),revision);lock(gate){var old=state.Field?.Orders.SingleOrDefault(o=>o.Request.Id==request.Id);if(old!=null)return new(old.Request==request?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,request.Id,state.CommitSequence,"受付済み");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,request.Id,state.CommitSequence,"表示を更新してください");try{var order=FieldWorld.Prepare(state,request);Change(s=>{s.Field=new([..s.Field!.Orders,order]);FieldWorld.Advance(s);});return new(ResultCode.Accepted,request.Id,state.CommitSequence,"自隊の布陣を変更しました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,request.Id,state.CommitSequence,"通行可能な陸地へ布陣線を指定してください");}}}
    public CommandResult IssueRegional(RegionalRequest request,long revision){lock(gate){var old=state.Region?.Commands.SingleOrDefault(c=>c.Request.Id==request.Id);if(old!=null)return new(old.Request==request?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,request.Id,state.CommitSequence,"受付済み");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,request.Id,state.CommitSequence,"表示を更新してください");try{Change(s=>RegionalBattle.Issue(s,request));return new(ResultCode.Accepted,request.Id,state.CommitSequence,"判断を受け付けました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,request.Id,state.CommitSequence,"現在はその判断を実行できません");}}}
    public CommandResult SetRegionalActive(bool active,long revision){lock(gate){var id=new EntityId("request:regional-site");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");try{Change(s=>{OfficerLoop.Transition(s,active);if(FieldPresence.Enabled(s))FieldPresence.SetPlayer(s,active);else RegionalBattle.SetActive(s,active);FieldSupply.Transition(s,active);if(OfficerLoop.Enabled(s))OfficerLoop.SetSiteLocations(s);if(CreatedCampaign.Enabled(s)&&!active){ProcessReady(s);FlushOutbox(s);}});return new(ResultCode.Accepted,id,state.CommitSequence,active?"野外へ出ました":"拠点に入りました");}catch(InvalidDataException){return new(ResultCode.NotAvailable,id,state.CommitSequence,"帰還・停止後に操作してください");}}}
    public CommandResult ChangeTravel(PersonalTravelRequest request,long expectedRevision,Action? beforeCommit=null)
    {lock(gate){
        if(CareerMilitary.OwnIdUsed(state,request.Id)||state.Career?.Requests.Any(r=>r.Input.Id==request.Id)==true)return new(ResultCode.IdPayloadConflict,request.Id,state.CommitSequence,"同一IDの内容が異なります");
        if(state.Region?.Commands.Any(c=>c.Request.Id==request.Id)==true)return new(ResultCode.IdPayloadConflict,request.Id,state.CommitSequence,"同一IDの内容が異なります");
        if(state.Field?.Orders.Any(o=>o.Request.Id==request.Id)==true)return new(ResultCode.IdPayloadConflict,request.Id,state.CommitSequence,"同一IDの内容が異なります");
        var old=state.TravelDecisions?.SingleOrDefault(d=>d.Request.Id==request.Id);if(old!=null)return new(old.Request==request?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,request.Id,state.CommitSequence,"受付済みの判断です");
        if(expectedRevision!=state.CommitSequence)return new(ResultCode.StateChanged,request.Id,state.CommitSequence,"表示を更新してください");
        try{if(state.Requests.Any(r=>r.Id==request.Id))throw new InvalidDataException("ID already used");var decision=TravelControl.Prepare(state,request);Change(s=>{s.TravelDecisions=[..s.TravelDecisions!,decision];TravelControl.ApplyDue(s);FlushOutbox(s);},beforeCommit);return new(ResultCode.Accepted,request.Id,state.CommitSequence,request.Destination==null?"出航を取りやめ、この港に留まりました":"次の拠点で行先を変更します");}
        catch(Exception e)when(e is InvalidDataException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,request.Id,state.CommitSequence,"現在はその判断を実行できません");}
    }}
    public CommandResult Submit(Intent intent, long expectedRevision, Action? beforeCommit = null)
    {
        lock(gate)
        {
            string hash;
            try{hash=ContractJson.CanonicalHash(intent);}
            catch(Exception e) when(e is System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
            {return new(ResultCode.InvalidInput,intent.RequestId,state.CommitSequence,"入力条件を確認してください");}
            if(CareerMilitary.OwnIdUsed(state,intent.RequestId)||state.Career?.Requests.Any(r=>r.Input.Id==intent.RequestId)==true)return new(ResultCode.IdPayloadConflict,intent.RequestId,state.CommitSequence,"同一IDの内容が異なります");
            if(state.Region?.Commands.Any(c=>c.Request.Id==intent.RequestId)==true)return new(ResultCode.IdPayloadConflict,intent.RequestId,state.CommitSequence,"同一IDの内容が異なります");
            if(state.Field?.Orders.Any(o=>o.Request.Id==intent.RequestId)==true)return new(ResultCode.IdPayloadConflict,intent.RequestId,state.CommitSequence,"同一IDの内容が異なります");
            if(state.TravelDecisions?.Any(d=>d.Request.Id==intent.RequestId)==true)return new(ResultCode.IdPayloadConflict,intent.RequestId,state.CommitSequence,"同一IDの内容が異なります");
            var old = state.Requests.SingleOrDefault(r => r.Id == intent.RequestId);
            if (old != null) return new(old.BodyHash == hash ? ResultCode.AlreadyAccepted : ResultCode.IdPayloadConflict,
                intent.RequestId,old.CommitSequence,old.BodyHash == hash ? "受付済み" : "同一IDの内容が異なります");
            if (expectedRevision != state.CommitSequence) return new(ResultCode.StateChanged,intent.RequestId,state.CommitSequence,"表示を更新してください");
            try
            {
                if(FieldPresence.PlayerDeployed(state)||NewGameMilitary.Deployed(state))throw new InvalidDataException("Return to the site before road travel");
                ContractValidation.ValidateIntentShape(intent,state.Characters.Select(c=>c.Id).ToHashSet(),state.Definitions.Sites.Select(s=>s.Id).ToHashSet(),state.ResourceLots.Select(l=>l.Id).ToHashSet());
                if(RouteContract.Enabled(state))RouteContract.ValidateOrder(state,intent);
                if(FieldWorld.Enabled(state)&&state.Field!.Orders.Length>0&&Enumerable.Range(0,3).Any(i=>FieldWorld.Sample(state,i,state.ElapsedSeconds).Moving))throw new InvalidDataException("Local force still moving");
                if(BranchRoutes.Enabled(state))BranchRoutes.ValidateOrder(state,intent);
                checked { _ = state.ElapsedSeconds + intent.DeliverySeconds + intent.WorkSeconds + intent.ReportSeconds;
                    _ = intent.EffectiveAt + intent.WorkSeconds + intent.ReportSeconds;
                    _ = intent.Deadline.AbsoluteLatestAt + intent.WorkSeconds + intent.ReportSeconds; }
            }
            catch(Exception e) when(e is InvalidDataException or OverflowException or ArgumentException)
            { return new(ResultCode.InvalidInput,intent.RequestId,state.CommitSequence,"入力条件を確認してください"); }
            if (intent.Deadline.AbsoluteLatestAt < state.ElapsedSeconds || intent.Deadline.At < state.ElapsedSeconds ||
                !Permits(state,intent.Actor,intent,state.ElapsedSeconds)) return new(ResultCode.NotAvailable,intent.RequestId,state.CommitSequence,"現在は実行できません");
            if ((intent.Action is ActionKind.Cancel or ActionKind.Redirect) && !state.Tasks.Any(t=>t.Id==intent.TargetTask && t.Scope==intent.Scope && t.Executor==intent.Executor && (t.Issuer==intent.Actor || t.Executor==intent.Actor)))
                return new(ResultCode.NotAvailable,intent.RequestId,state.CommitSequence,"現在は実行できません");
            Change(s => {
                var taskId = Allocate(s,"task");
                s.Requests = [..s.Requests,new(intent.RequestId,hash,intent,s.ElapsedSeconds,s.CommitSequence)];
                var deadline = intent.Deadline.FromOpening ? null : intent.Deadline.At;
                var task = new TaskState(taskId,intent.RequestId,intent.Actor,intent.Executor,intent.Scope,intent.Action,intent.Kind,
                    AgreementPhase.AwaitingReceipt,ExecutionPhase.NotStarted,ReportingPhase.NotCreated,AssessmentPhase.Pending,
                    0,null,null,deadline,intent.Deadline.AbsoluteLatestAt,intent.EffectiveAt,"",s.CommitSequence);
                s.Tasks = [..s.Tasks,task];
                var mid = Child("message",taskId,"order");
                MessageFlow.Send(s.Messaging,new(mid.Value,intent.RequestId.Value,"contract-order",intent.Actor.Value,intent.Executor.Value,
                    taskId.Value,intent.Scope.Value,s.ElapsedSeconds,intent.Deadline.Criterion==DeadlineCriterion.FormalReceipt?deadline:null),s.ElapsedSeconds,intent.DeliverySeconds);
                if(intent.Important) s.SealedMessages=[..s.SealedMessages,mid];
            },beforeCommit);
            return new(ResultCode.Accepted,intent.RequestId,state.CommitSequence,"発送を受け付けました");
        }
    }
    internal static bool Permits(ContractWorld s, EntityId actor, Intent i, long at)
    {
        if(!s.Characters.Any(c=>c.Id==actor && c.Life==LifeState.Adult)) return false;
        return s.Grants.Any(g=>g.Agent==actor && g.Scope==i.Scope && g.Actions.Contains(i.Action) && g.From<=at && at<g.Until &&
            i.Amount<=g.Limit && (i.Action!=ActionKind.Transfer || g.Assets.Contains(i.SourceLot!.Value) &&
            s.ResourceLots.Any(l=>l.Id==i.SourceLot && l.Owner==g.Principal)));
    }
    private static Intent IntentFor(ContractWorld s, TaskState task) => s.Requests.Single(r=>r.Id==task.RequestId).Intent;
    private static void Replace(ContractWorld s, TaskState task) => s.Tasks = s.Tasks.Select(t=>t.Id==task.Id?task:t).ToArray();
    private static bool Terminal(TaskState t) => t.Execution is ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed;
    public void SetPaused(bool paused) { lock(gate) if(state.Paused!=paused) Change(s=>s.Paused=paused); }
    public CommandResult Open(EntityId actor, EntityId message, long expectedRevision)
    {
        lock(gate)
        {
            if(expectedRevision!=state.CommitSequence)return new(ResultCode.StateChanged,message,state.CommitSequence,"表示を更新してください");
            var m=state.Messaging.Messages.SingleOrDefault(m=>m.Id==message.Value);
            bool physicallyReceived=m!=null&&state.Messaging.Attempts.Any(a=>a.MessageId==m.Id&&a.ReceiverId==actor.Value&&a.Status==AttemptStatus.Delivered);
            if(!physicallyReceived)return new(ResultCode.NotAvailable,message,state.CommitSequence,"現在は開けません");
            bool principal=m!.RecipientId==actor.Value;
            if(!principal&&!state.Messaging.Proxies.Any(p=>p.PrincipalId==m.RecipientId&&p.AgentId==actor.Value&&p.Scope==m.Scope&&p.Kind==m.Kind&&p.Open&&p.ValidFrom<=state.ElapsedSeconds&&state.ElapsedSeconds<p.ValidUntil))
                return new(ResultCode.NotAvailable,message,state.CommitSequence,"現在は開けません");
            if(MessageFlow.Knows(state.Messaging,m.Id,actor.Value))return new(ResultCode.AlreadyAccepted,message,state.CommitSequence,"開封済み");
            Change(s=>{
                if(principal)s.Messaging.Knowledge=[..s.Messaging.Knowledge,new(m.Id,actor.Value,s.ElapsedSeconds,m.Id)];
                else MessageFlow.OpenAsProxy(s.Messaging,m.Id,actor.Value,s.ElapsedSeconds);
                NewGameMilitary.RecordOpening(s,actor,m.Id);NewGameService.RecordOpening(s,actor,m.Id);Career.RecordOpening(s,actor,m.Id);CareerMilitary.RecordOpening(s,actor,m.Id);
                SynchronizeEvidence(s); Recognize(s); Expire(s); ProcessReady(s); FlushOutbox(s);
            });
            return new(ResultCode.Accepted,message,state.CommitSequence,"開封しました");
        }
    }
    public CommandResult Assent(EntityId taskId, EntityId actor, bool accept, long expectedRevision)
    {
        lock(gate)
        {
            if(expectedRevision!=state.CommitSequence)return new(ResultCode.StateChanged,taskId,state.CommitSequence,"表示を更新してください");
            var task=state.Tasks.SingleOrDefault(t=>t.Id==taskId);
            if(task==null||task.Executor!=actor||task.Agreement!=AgreementPhase.AwaitingAssent)return new(ResultCode.NotAvailable,taskId,state.CommitSequence,"現在は回答できません");
            Change(s=>{
                var t=s.Tasks.Single(t=>t.Id==taskId);
                if(accept)Replace(s,t with{Agreement=AgreementPhase.Active,Execution=ExecutionPhase.Waiting,Revision=s.CommitSequence});
                else Stop(s,t,"declined",AgreementPhase.Declined);
                Expire(s);ProcessReady(s);FlushOutbox(s);
            });
            return new(ResultCode.Accepted,taskId,state.CommitSequence,accept?"受諾しました":"辞退しました");
        }
    }
    public AdvanceResult AdvanceTo(long target, int eventBudget=10000, Action? beforeCommit=null)
    {
        lock(gate)
        {
            if(target<state.ElapsedSeconds||eventBudget<1)throw new ArgumentOutOfRangeException(nameof(target));
            int processed=0;
            while(state.ElapsedSeconds<target)
            {
                if(state.Paused)return new(state.ElapsedSeconds,processed,"Paused");
                long boundary=NextBoundary(state,target);
                int cost=1+(NpcWork.Enabled(state)?6:0)+(TravelControl.Enabled(state)?state.TravelDecisions!.Count(d=>d.AppliedAt==null&&d.EffectiveAt==boundary):0)+state.PendingEvents.Count(e=>e.At==boundary)+state.Messaging.Attempts.Count(a=>a.Status==AttemptStatus.InTransit&&a.DueAt==boundary||a.Status==AttemptStatus.Lost&&a.LateDueAt==boundary);
                if(cost>eventBudget-processed)return new(state.ElapsedSeconds,processed,"EventBudget");
                Change(s=>{
                    long priorAt=s.ElapsedSeconds;s.ElapsedSeconds=boundary;
                    if(RegionalCampaign.Enabled(s))RegionalCampaign.ApplyConditions(s);
                    if(TravelControl.Enabled(s))TravelControl.ApplyDue(s);
                    UpdateProgress(s);if(FieldWorld.Enabled(s))FieldWorld.Advance(s);FieldSupply.Tick(s,priorAt);NewGameMilitary.Tick(s,priorAt);NewGameMilitary.Sync(s);RegionalBattle.Tick(s);
                    var known=s.Messaging.Knowledge.Select(k=>(k.MessageId,k.CharacterId)).ToHashSet();
                    if(RegionalCampaign.Enabled(s))RegionalCampaign.Arrivals(s);else if(MovementControl.Enabled(s))MovementControl.CheckArrivals(s);
                    CourierRecovery.BeforeMessages(s);RegionalBattle.BeforeMessages(s);NewGameMilitary.BeforeUnitMessages(s);MessageFlow.Advance(s.Messaging,boundary);RegionalBattle.AfterMessages(s);DelegationMission.Update(s);DelegationInquiry.Update(s);
                    if(RegionalCampaign.Enabled(s))RegionalCampaign.Failures(s);else if(MovementControl.Enabled(s))MovementControl.ReportFailures(s); // Existing common transport, not a second delivery simulator.
                    // Opt-in sealed-letter policy at this adapter boundary; legacy behavior is unchanged.
                    s.Messaging.Knowledge=s.Messaging.Knowledge.Where(k=>known.Contains((k.MessageId,k.CharacterId))||
                        !s.SealedMessages.Any(id=>id.Value==k.MessageId)||k.EvidenceMessageId!=k.MessageId).ToArray();
                    if(s.Messaging.Attempts.Any(a=>a.Status==AttemptStatus.Delivered&&a.FinishedAt==boundary&&a.ReceiverId==s.PlayerControl.Value&&s.SealedMessages.Any(id=>id.Value==a.MessageId)))s.Paused=true;
                    SynchronizeEvidence(s);Recognize(s);Expire(s);ProcessReady(s);FlushOutbox(s);SynchronizeEvidence(s);CareerMilitary.Update(s);Career.Update(s);NewGameMilitary.Update(s);NewGameService.Update(s);
                },beforeCommit);
                if(NpcWork.Enabled(state))NpcWork.TakeTurns(this);
                processed+=cost;
            }
            return new(state.ElapsedSeconds,processed,state.Paused?"Paused":"ReachedTarget");
        }
    }
    private static long NextBoundary(ContractWorld s,long target)
    {
        var times=s.Messaging.Attempts.SelectMany(a=>a.Status==AttemptStatus.InTransit?new[]{a.DueAt}:
            a.Status==AttemptStatus.Lost&&a.LateDueAt!=null?new[]{a.LateDueAt.Value}:Array.Empty<long>())
            .Concat(s.PendingEvents.Select(e=>e.At)).Where(t=>t>s.ElapsedSeconds&&t<=target).ToList();
        if(RegionalBattle.Active(s)&&FieldPresence.NeedsSecondSteps(s,s.ElapsedSeconds))times.Add(s.ElapsedSeconds+1);
        if(NewGameMilitary.NextBoundary(s,target) is {} militaryLoanBoundary)times.Add(militaryLoanBoundary);
        if(NewGameService.NextBoundary(s,target) is {} serviceBoundary)times.Add(serviceBoundary);
        if(CareerMilitary.NextBoundary(s,target) is {} militaryBoundary)times.Add(militaryBoundary);
        if(Career.Enabled(s)&&Career.NextBoundary(s,target) is {} careerBoundary)times.Add(careerBoundary);
        if(NpcWork.Enabled(s)&&NpcWork.NextBoundary(s,target) is {} npcBoundary)times.Add(npcBoundary);
        if(RegionalCampaign.Enabled(s)&&RegionalCampaign.NextBoundary(s,target) is {} courierBoundary)times.Add(courierBoundary);
        if(FieldWorld.Enabled(s)&&FieldWorld.NextBoundary(s,target) is {} fieldBoundary)times.Add(fieldBoundary);
        if(TravelControl.Enabled(s)&&TravelControl.NextBoundary(s,target) is {} personalBoundary)times.Add(personalBoundary);
        if(BranchRoutes.Enabled(s)&&BranchRoutes.NextBoundary(s,target) is {} graphBoundary)times.Add(graphBoundary);
        foreach(var t in s.Tasks.Where(t=>!Terminal(t)))
        {
            long deadline=ExpiryLimit(s,t);
            if(deadline<long.MaxValue&&deadline>=s.ElapsedSeconds&&deadline<target)times.Add(deadline+1);
        }
        return times.Count==0?target:times.Min();
    }
    internal static long ExpiryLimit(ContractWorld s,TaskState t)
    {
        var i=IntentFor(s,t);var deadline=t.DeadlineAt;
        bool met=i.Deadline.Criterion switch{
            DeadlineCriterion.Dispatch=>true,
            DeadlineCriterion.FormalReceipt=>s.Messaging.Receipts.Any(r=>r.MessageId==Child("message",t.Id,"order").Value),
            DeadlineCriterion.Opening=>MessageFlow.Knows(s.Messaging,Child("message",t.Id,"order").Value,t.Executor.Value),
            _=>Terminal(t)};
        return met||deadline==null?t.AbsoluteLatestAt:Math.Min(deadline.Value,t.AbsoluteLatestAt);
    }
    private static void SynchronizeEvidence(ContractWorld s)
    {
        foreach(var k in s.Messaging.Knowledge.Where(k=>s.Characters.Any(c=>c.Id.Value==k.CharacterId)))
        {
            var id=new EntityId("evidence:"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(k.MessageId+"|"+k.CharacterId)));
            if(s.Evidence.Any(e=>e.Id==id))continue;
            var m=s.Messaging.Messages.Single(m=>m.Id==k.MessageId);var task=s.Tasks.SingleOrDefault(t=>t.RequestId.Value==m.CorrelationId);
            var observer=new EntityId(k.CharacterId);
            s.Evidence=[..s.Evidence,new(id,observer,task?.Id??observer,m.Body,m.CreatedAt,k.LearnedAt,new(m.Id),null)];
            s.Knowledge=[..s.Knowledge,new(observer,id,k.LearnedAt)];
            if(task!=null&&m.Kind=="contract-report"&&observer==task.Issuer)
            {
                // Assessment is fixture confirmation of received evidence, not omniscient campaign evaluation.
                Replace(s,task with{Report=ReportingPhase.KnownByRecipient,Assessment=AssessmentPhase.Confirmed,Revision=s.CommitSequence});
                s.Chronicle=[..s.Chronicle,new(Allocate(s,"chronicle"),observer,"返報を認識: "+m.Body,s.ElapsedSeconds)];
            }
        }
    }
    private static void Recognize(ContractWorld s)
    {
        foreach(var t0 in s.Tasks.Where(t=>!Terminal(t)&&t.Agreement is AgreementPhase.AwaitingReceipt or AgreementPhase.AwaitingRecognition).ToArray())
        {
            var t=s.Tasks.Single(t=>t.Id==t0.Id);var mid=Child("message",t.Id,"order");
            if(!s.Messaging.Receipts.Any(r=>r.MessageId==mid.Value))continue;
            if(!MessageFlow.Knows(s.Messaging,mid.Value,t.Executor.Value))
            {Replace(s,t with{Agreement=AgreementPhase.AwaitingRecognition,Revision=s.CommitSequence});continue;}
            var i=IntentFor(s,t);long? deadline=t.DeadlineAt;
            if(i.Deadline.FromOpening)deadline=Math.Min(checked(s.ElapsedSeconds+i.Deadline.DurationSeconds!.Value),t.AbsoluteLatestAt);
            if(s.ElapsedSeconds>t.AbsoluteLatestAt || deadline<s.ElapsedSeconds)
            {Stop(s,t,"expired",AgreementPhase.Expired);continue;}
            Replace(s,t with{Agreement=t.Kind==TaskKind.VoluntaryRequest?AgreementPhase.AwaitingAssent:AgreementPhase.Active,
                Execution=ExecutionPhase.Waiting,DeadlineAt=deadline,Revision=s.CommitSequence});
        }
    }
    private static void ProcessReady(ContractWorld s)
    {
        // Cancellation only precedes the explicitly targeted still-uncommitted effect.
        foreach(var t in s.Tasks.Where(t=>!Terminal(t)&&t.Action==ActionKind.Cancel&&t.Agreement==AgreementPhase.Active).ToArray())
        {
            if(t.EffectiveAt>s.ElapsedSeconds){Schedule(s,t,EventKind.Cancel,t.EffectiveAt);continue;}
            var i=IntentFor(s,t);var target=s.Tasks.Single(x=>x.Id==i.TargetTask);
            if(!Permits(s,t.Issuer,i,s.ElapsedSeconds)||!Permits(s,t.Executor,i,s.ElapsedSeconds))Stop(s,t,"unavailable");
            else if(Terminal(target))Complete(s,t,target.Execution==ExecutionPhase.Completed?"too-late":"already-stopped");
            else{Stop(s,target,"cancelled");Complete(s,t,"cancelled");}
        }
        foreach(var t in s.Tasks.Where(t=>!Terminal(t)&&t.Action==ActionKind.Redirect&&t.Agreement==AgreementPhase.Active).ToArray())
        {
            if(t.EffectiveAt>s.ElapsedSeconds){Schedule(s,t,EventKind.Cancel,t.EffectiveAt);continue;}
            var i=IntentFor(s,t);var target=s.Tasks.Single(x=>x.Id==i.TargetTask);
            if(!MovementControl.Enabled(s)||!Permits(s,t.Issuer,i,s.ElapsedSeconds)||!Permits(s,t.Executor,i,s.ElapsedSeconds)){Stop(s,t,"unavailable");continue;}
            bool applied=MovementControl.Redirect(s,t,target);
            var reason=applied?"redirected":target.Execution==ExecutionPhase.Completed?"too-late":target.Execution==ExecutionPhase.Stopped?"already-stopped":"unavailable";
            s.EffectLedger=[..s.EffectLedger,new(Child("effect",t.Id,"finish"),t.Id.Value+"/finish/1",ContractJson.CanonicalHash(i),t.Id,s.ElapsedSeconds,reason)];
            s.PendingEvents=s.PendingEvents.Where(e=>e.Task!=t.Id).ToArray();Complete(s,t,reason);
        }
        var due=s.PendingEvents.Where(e=>e.At<=s.ElapsedSeconds).ToArray();
        var unprocessed=due.ToList();
        while(unprocessed.Count>0)
        {
            var e=unprocessed.FirstOrDefault(e=>e.DependsOn.All(d=>s.EffectLedger.Any(x=>x.Id==d)));
            if(e==null)throw new InvalidDataException("SCHEDULER_INVARIANT: unmet due-event dependency.");
            unprocessed.Remove(e);
            s.PendingEvents=s.PendingEvents.Where(x=>x.Id!=e.Id).ToArray();
            var t=s.Tasks.Single(t=>t.Id==e.Task);
            if(e.Kind==EventKind.Finish&&!Terminal(t))Finish(s,t);
            s.EffectLedger=[..s.EffectLedger,new(e.Id,"event/"+e.Id.Value,ContractJson.CanonicalHash(e),e.Task,s.ElapsedSeconds,"processed")];
        }
        var waiting=s.Tasks.Where(t=>!Terminal(t)&&t.Action is not(ActionKind.Cancel or ActionKind.Redirect)&&t.Agreement==AgreementPhase.Active&&t.Execution==ExecutionPhase.Waiting).ToArray();
        foreach(var t in waiting.Where(t=>t.EffectiveAt>s.ElapsedSeconds))Schedule(s,t,EventKind.Execute,t.EffectiveAt);
        var ready=waiting.Where(t=>t.EffectiveAt<=s.ElapsedSeconds&&(!CreatedCampaign.Enabled(s)||t.Action!=ActionKind.Inspect||s.Locations.Any(l=>l.Actor==t.Executor&&l.Kind==LocationKind.AtSite&&l.Site==t.Scope))).ToArray();
        foreach(var group in ready.GroupBy(t=>t.Executor))
        {
            var candidates=group.ToArray();
            if(candidates.Length>1)
            {
                var input=candidates.Select(t=>t.Id).OrderBy(x=>x.Value,StringComparer.Ordinal).ToArray();
                var signature=string.Join("|",input);
                if(!s.ConflictDecisions.Any(c=>c.Rule=="equal-main-action-hold-v1"&&string.Join("|",c.Inputs)==signature))
                    s.ConflictDecisions=[..s.ConflictDecisions,new(Allocate(s,"conflict"),input,"equal-main-action-hold-v1","hold-and-query",s.ElapsedSeconds)];
                continue;
            }
            var t=candidates[0];if(t.EffectiveAt>s.ElapsedSeconds){Schedule(s,t,EventKind.Execute,t.EffectiveAt);continue;}
            if(s.Actions.Any(a=>a.Actor==t.Executor&&a.Phase==ExecutionPhase.Running))continue;
            Start(s,t);
        }
    }
    private static void UpdateProgress(ContractWorld s)
    {
        foreach(var a in s.Actions.Where(a=>a.Phase==ExecutionPhase.Running).ToArray())
        {
            long progress=Math.Min(s.ElapsedSeconds-a.StartedAt,a.Until-a.StartedAt);
            s.Actions=s.Actions.Select(x=>x.Id==a.Id?x with{Progress=progress}:x).ToArray();
            var t=s.Tasks.Single(t=>t.Id==a.Task);
            if(t.Action==ActionKind.Move&&BranchRoutes.Enabled(s)){if(!BranchRoutes.Advance(s,t))Stop(s,t,"route-unavailable");}
            else if(t.Action==ActionKind.Move)s.Locations=s.Locations.Select(l=>l.Actor==a.Actor&&l.Kind==LocationKind.Traveling?
                l with{ProgressMm=RouteContract.Enabled(s)?RouteContract.DistanceAt(progress,s):checked(progress*1000),
                    SegmentIndex=RouteContract.Enabled(s)?RouteContract.Segment(RouteContract.DistanceAt(progress,s),s):l.SegmentIndex,Revision=s.CommitSequence}:l).ToArray();
        }
    }
    private static void Schedule(ContractWorld s,TaskState t,EventKind kind,long at)
    {
        var id=Child("event",t.Id,kind.ToString().ToLowerInvariant());
        if(!s.PendingEvents.Any(e=>e.Id==id))s.PendingEvents=[..s.PendingEvents,new(id,t.Id,kind,at,[])];
    }
    private static void Start(ContractWorld s,TaskState t)
    {
        var i=IntentFor(s,t);
        if(s.ElapsedSeconds>ExpiryLimit(s,t)){Stop(s,t,"expired",AgreementPhase.Expired);return;}
        if(!Permits(s,t.Issuer,i,s.ElapsedSeconds)||!Permits(s,t.Executor,i,s.ElapsedSeconds)||!s.Characters.Single(c=>c.Id==t.Executor).WillObey)
        {Stop(s,t,"unavailable");return;}
        var location=s.Locations.Single(l=>l.Actor==t.Executor);
        if(location.Kind!=LocationKind.AtSite||(!(TravelWorld.Enabled(s)&&t.Action==ActionKind.Move&&t.Issuer==t.Executor)&&location.Site!=t.Scope)){Stop(s,t,"wrong-location");return;}
        if(t.Action==ActionKind.Transfer)
        {
            var source=s.ResourceLots.Single(l=>l.Id==i.SourceLot);var dest=s.ResourceLots.Single(l=>l.Id==i.DestinationLot);
            var reserved=s.Reservations.Where(r=>r.Lot==source.Id&&r.Phase is ReservationPhase.Active or ReservationPhase.PartiallyConsumed).Sum(r=>r.Amount-r.Consumed);
            if(source.Site!=t.Scope||dest.Site!=t.Scope||source.Unit!=dest.Unit||source.Kind!=dest.Kind||source.Quantity-reserved<i.Amount)
            {Stop(s,t,"resource-unavailable");return;}
            s.Reservations=[..s.Reservations,new(Child("reservation",t.Id,"resource"),source.Id,t.Id,i.Amount,0,ReservationPhase.Active)];
        }
        var until=t.Action==ActionKind.Move&&BranchRoutes.Enabled(s)?BranchRoutes.Start(s,t):checked(s.ElapsedSeconds+i.WorkSeconds);
        s.Actions=[..s.Actions,new(Child("action",t.Id,"main"),t.Id,t.Executor,"main",ExecutionPhase.Running,s.ElapsedSeconds,until,0)];
        if(t.Action==ActionKind.Move&&BranchRoutes.Enabled(s))s.Locations=s.Locations.Select(l=>l.Actor==t.Executor?BranchRoutes.Location(s,t):l).ToArray();
        else if(t.Action==ActionKind.Move)s.Locations=s.Locations.Select(l=>l.Actor==t.Executor?new LocationState(t.Executor,LocationKind.Traveling,null,Child("route",t.Id,RouteContract.Enabled(s)?"corridor":"fixture"),0,0,null,null,null,RouteContract.Enabled(s)?RouteContract.Revision(s):"fixture-local-v1",s.CommitSequence):l).ToArray();
        Replace(s,t with{Execution=ExecutionPhase.Running,StartedAt=s.ElapsedSeconds,Revision=s.CommitSequence});Schedule(s,t,EventKind.Finish,until);
        if(t.Action==ActionKind.Move&&BranchRoutes.Enabled(s)&&!BranchRoutes.Advance(s,s.Tasks.Single(x=>x.Id==t.Id)))Stop(s,s.Tasks.Single(x=>x.Id==t.Id),"route-unavailable");
    }
    private static void Finish(ContractWorld s,TaskState t)
    {
        var i=IntentFor(s,t);
        if(s.ElapsedSeconds>ExpiryLimit(s,t)){Stop(s,t,"expired",AgreementPhase.Expired);return;}
        if(!Permits(s,t.Issuer,i,s.ElapsedSeconds)||!Permits(s,t.Executor,i,s.ElapsedSeconds)){Stop(s,t,"unavailable");return;}
        var key=t.Id.Value+"/finish/1";var hash=ContractJson.CanonicalHash(i);
        var old=s.EffectLedger.SingleOrDefault(e=>e.BusinessKey==key);
        if(old!=null){if(old.BodyHash!=hash)throw new InvalidDataException("Effect payload conflict.");return;}
        var effect=Child("effect",t.Id,"finish");
        s.EffectLedger=[..s.EffectLedger,new(effect,key,hash,t.Id,s.ElapsedSeconds,"completed")];
        if(t.Action==ActionKind.Transfer)
        {
            var reservation=s.Reservations.Single(r=>r.Task==t.Id);
            s.ResourceLots=s.ResourceLots.Select(l=>l.Id==i.SourceLot?l with{Quantity=checked(l.Quantity-i.Amount),Revision=s.CommitSequence}:
                l.Id==i.DestinationLot?l with{Quantity=checked(l.Quantity+i.Amount),Revision=s.CommitSequence}:l).ToArray();
            s.Transfers=[..s.Transfers,new(Child("transfer",t.Id,"finish"),effect,i.SourceLot!.Value,i.DestinationLot!.Value,i.Amount,s.ElapsedSeconds)];
            s.Reservations=s.Reservations.Select(r=>r.Id==reservation.Id?r with{Consumed=r.Amount,Phase=ReservationPhase.Consumed}:r).ToArray();
        }
        if(t.Action==ActionKind.Move)s.Locations=s.Locations.Select(l=>l.Actor==t.Executor?new LocationState(t.Executor,LocationKind.AtSite,MovementControl.Enabled(s)?MovementControl.Destination(s,t):i.DestinationSite,null,0,0,null,null,null,null,s.CommitSequence):l).ToArray();
        s.Actions=s.Actions.Select(a=>a.Task==t.Id?a with{Phase=ExecutionPhase.Completed,Progress=a.Until-a.StartedAt}:a).ToArray();
        Complete(s,t,"completed");
    }
    private static void Complete(ContractWorld s,TaskState t,string reason)
    {
        Replace(s,t with{Execution=ExecutionPhase.Completed,Progress=1,FinishedAt=s.ElapsedSeconds,InternalReason=reason,Revision=s.CommitSequence});
        QueueReport(s,t,reason);
    }
    internal static void Stop(ContractWorld s,TaskState t,string reason,AgreementPhase? agreement=null)
    {
        if(Terminal(t))return;
        s.PendingEvents=s.PendingEvents.Where(e=>e.Task!=t.Id).ToArray();
        s.Reservations=s.Reservations.Select(r=>r.Task==t.Id&&r.Phase is ReservationPhase.Active or ReservationPhase.PartiallyConsumed?r with{Phase=ReservationPhase.Released}:r).ToArray();
        s.Actions=s.Actions.Select(a=>a.Task==t.Id&&a.Phase==ExecutionPhase.Running?a with{Phase=ExecutionPhase.Stopped}:a).ToArray();
        // A stopped march stays at its physical route progress; never teleport back to the assigned castle.
        Replace(s,t with{Execution=ExecutionPhase.Stopped,Agreement=agreement??t.Agreement,FinishedAt=s.ElapsedSeconds,InternalReason=CreatedCampaign.Enabled(s)&&t.Issuer!=t.Executor&&!MissionReview.KnownOrder(s,t,t.Executor)?"unrecognized/"+reason:reason,Revision=s.CommitSequence});
        QueueReport(s,t,reason);
    }
    private static void QueueReport(ContractWorld s,TaskState task,string reason)
    {
        if(CreatedCampaign.Enabled(s)&&task.Issuer!=task.Executor&&!MissionReview.KnownOrder(s,task,task.Executor))return;
        var id=Child("message",task.Id,"report");if(s.Outbox.Any(o=>o.Id==id))return;
        var i=IntentFor(s,task);
        // Public report deliberately excludes hidden institutional reasons.
        var text=reason=="executor-interrupted"&&MissionReview.Enabled(s)?MissionReview.InterruptedReport:reason=="completed"?"任務完了":reason=="redirected"?"行先変更を適用":reason=="too-late"?"既に完了済み":reason=="service-suspended"?"運休のため港で移動を中止":reason=="road-unavailable"?"通行不能のため拠点で移動を中止":reason=="port-cancelled"?"出航を取りやめ":reason=="cancelled"?"取消を処理":reason=="expired"?"期限超過":"履行できず・照会が必要";
        s.Outbox=[..s.Outbox,new(id,task.Id,task.Executor,task.Issuer,"contract-report",text,s.ElapsedSeconds,RegionalCampaign.Enabled(s)?1:BranchRoutes.Enabled(s)?BranchRoutes.ReportDelay(s,task):RouteContract.Enabled(s)?RouteContract.Courier(s,task.Executor,task.Issuer):i.ReportSeconds,false)];
        var current=s.Tasks.Single(t=>t.Id==task.Id);Replace(s,current with{Report=ReportingPhase.Queued});
    }
    private static void FlushOutbox(ContractWorld s)
    {
        foreach(var o in s.Outbox.Where(o=>!o.Sent).ToArray())
        {
            var t=s.Tasks.Single(t=>t.Id==o.Task);
            MessageFlow.Send(s.Messaging,new(o.Id.Value,t.RequestId.Value,o.Kind,o.Sender.Value,o.Recipient.Value,o.Body,t.Scope.Value,o.At),o.At,o.Delay);
            s.Outbox=s.Outbox.Select(x=>x.Id==o.Id?x with{Sent=true}:x).ToArray();Replace(s,t with{Report=ReportingPhase.InTransit});
        }
    }
    private static void Expire(ContractWorld s)
    {
        foreach(var t in s.Tasks.Where(t=>!Terminal(t)).ToArray())if(s.ElapsedSeconds>ExpiryLimit(s,t))Stop(s,t,"expired",AgreementPhase.Expired);
    }
    public BranchView GetBranchView(EntityId actor)
    {lock(gate){if(!BranchRoutes.Enabled(state))throw new InvalidOperationException("Branch rules required");return BranchRoutes.View(state,GetActorView(actor));}}
    public RouteActorView GetRouteActorView(EntityId actor)
    {
        lock(gate)
        {
            if(!RouteContract.Enabled(state))throw new InvalidOperationException("Route view requires its registered ruleset");
            var view=GetActorView(actor);var offset=RouteContract.Offset(view.Location,state);
            var action=state.Actions.SingleOrDefault(a=>a.Actor==actor&&a.Phase==ExecutionPhase.Running);
            bool walking=action!=null&&state.Tasks.Single(t=>t.Id==action.Task).Action==ActionKind.Move;
            return new(view,RouteContract.Point(offset,state),offset,walking,walking?action!.Until:null);
        }
    }
    public ActorView GetActorView(EntityId actor)
    {
        lock(gate)
        {
            var location=state.Locations.SingleOrDefault(l=>l.Actor==actor)??throw new ArgumentException("NOT_AVAILABLE");
            var tasks=state.Tasks.Where(t=>t.Issuer==actor||MessageFlow.Knows(state.Messaging,Child("message",t.Id,"order").Value,actor.Value)).Select(t=>{
                var report=state.Messaging.Messages.SingleOrDefault(m=>m.Id==Child("message",t.Id,"report").Value);
                var text=report!=null&&MessageFlow.Knows(state.Messaging,report.Id,actor.Value)?report.Body:
                    t.Executor==actor&&MessageFlow.Knows(state.Messaging,Child("message",t.Id,"order").Value,actor.Value)?"本人の履行: "+t.Execution:"発送済・結果未確認";
                var i=IntentFor(state,t);
                // Opening at a remote executor must not reveal its time to the issuer.
                long? visibleDeadline=i.Deadline.FromOpening&&actor!=t.Executor?i.Deadline.AbsoluteLatestAt:t.DeadlineAt;
                return new VisibleTask(t.Id,t.Issuer,t.Executor,text,visibleDeadline);
            }).ToArray();
            var evidence=state.Knowledge.Where(k=>k.Character==actor).Select(k=>state.Evidence.Single(e=>e.Id==k.Evidence)).ToArray();
            var messages=state.Messaging.Messages.Where(m=>state.Messaging.Attempts.Any(a=>a.MessageId==m.Id&&a.ReceiverId==actor.Value&&a.Status==AttemptStatus.Delivered)).Select(m=>
                new VisibleMessage(new(m.Id),m.SenderId,state.SealedMessages.Any(id=>id.Value==m.Id)&&!MessageFlow.Knows(state.Messaging,m.Id,actor.Value),MessageFlow.Knows(state.Messaging,m.Id,actor.Value)?m.Body:null)).ToArray();
            return ContractJson.Copy(new ActorView(actor,state.CommitSequence,state.ElapsedSeconds,location,tasks,evidence,messages));
        }
    }
    public uint Draw(EntityId decisionId,string purpose,Action? beforeCommit=null)
    {
        lock(gate)
        {
            var prior=state.RandomDraws.SingleOrDefault(d=>d.Id==decisionId);
            if(prior!=null){if(prior.Purpose!=purpose)throw new InvalidOperationException("Decision ID conflict.");return prior.Result;}
            uint result=0;Change(s=>{
                var stream=s.RandomStreams.Single(r=>r.Purpose==purpose);var rng=new SeededRandom(stream.State);result=rng.Next();
                s.RandomDraws=[..s.RandomDraws,new(decisionId,purpose,stream.State,result,rng.State,s.ElapsedSeconds)];
                s.RandomStreams=s.RandomStreams.Select(r=>r.Purpose==purpose?r with{State=rng.State,DrawCount=checked(r.DrawCount+1)}:r).ToArray();
            },beforeCommit);return result;
        }
    }
    public NearbyActor[] GetNearbyActors(EntityId observer){lock(gate)return RegionalCampaign.Nearby(state,observer);}
    public CommandResult InspectRoad(EntityId actor,string edge,long revision)
    {lock(gate){var id=new EntityId("request:inspect-road");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");try{Change(s=>RegionalCampaign.InspectRoad(s,actor,edge));return new(ResultCode.Accepted,id,state.CommitSequence,"現地で通行状況を確認しました");}catch(Exception e)when(e is InvalidOperationException or InvalidDataException){return new(ResultCode.NotAvailable,id,state.CommitSequence,"道の入口で確認してください");}}}
    public CourierView[] GetCourierView(EntityId observer){lock(gate)return RegionalCampaign.Views(state,observer);}
    public CommandResult RetryCourier(EntityId actor,string message,string destination,long appointment,long revision)
    {lock(gate){if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,new(message),state.CommitSequence,"表示を更新してください");
        try{Change(s=>RegionalCampaign.Retry(s,actor,message,destination,appointment));return new(ResultCode.Accepted,new(message),state.CommitSequence,"再発送しました");}
        catch(Exception e) when(e is InvalidDataException or InvalidOperationException or ArgumentException){return new(ResultCode.NotAvailable,new(message),state.CommitSequence,"既知の失敗と届け先を確認してください");}}}
    // Explicit test/diagnostic transport control; never exposed by an ordinary ActorView.
    public void DiagnosticTransport(Action<MessageState,long> control)
    {lock(gate)Change(s=>control(s.Messaging,s.ElapsedSeconds));}
}
