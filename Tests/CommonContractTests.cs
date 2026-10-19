using System.Text;
using System.Text.Json.Nodes;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.World;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class CommonContractTests
{
    private static WorldSession Session() => new(ContractFixture.Create());
    private static TaskState OnlyTask(WorldSession s) => Assert.Single(s.DiagnosticSnapshot().Tasks);
    private static byte[] Bytes(WorldSession s) => ContractJson.Write(s.DiagnosticSnapshot());
    [Theory] [InlineData("person:one")] [InlineData("task:12/a-b_C.3")]
    public void StableIdsRoundTrip(string id) => Assert.Equal(new EntityId(id),ContractJson.Copy(new EntityId(id)));
    [Theory] [InlineData("")] [InlineData("none")] [InlineData("person:空白")] [InlineData("person:with space")]
    public void InvalidIdsAreRejected(string id) => Assert.Throws<ArgumentException>(()=>new EntityId(id));
    [Fact] public void SecondsOverflowDoesNotWrap() => Assert.Throws<OverflowException>(()=>new SimTime(long.MaxValue).Add(1));
    [Fact] public void DefinitionAndStateRoundTripIncludesEveryRequiredSection()
    {var s=ContractFixture.Create();ContractValidation.Validate(ContractJson.Copy(s));Assert.Equal(ContractJson.Write(s),ContractJson.Write(ContractJson.Copy(s)));}
    [Theory] [InlineData("Characters")] [InlineData("Knowledge")] [InlineData("EffectLedger")] [InlineData("PendingEvents")] [InlineData("RandomStreams")]
    public void MissingRequiredSectionsAreNotDefaulted(string section)
    {var json=JsonNode.Parse(ContractJson.Write(ContractFixture.Create()))!;json.AsObject().Remove(section);Assert.Throws<InvalidDataException>(()=>ContractJson.Read<ContractWorld>(Encoding.UTF8.GetBytes(json.ToJsonString())));}
    [Fact] public void DuplicateJsonPropertiesAreRejectedBeforeMaterialization()
    {Assert.Throws<InvalidDataException>(()=>ContractJson.Read<Dictionary<string,int>>(Encoding.UTF8.GetBytes("{\"a\":1,\"a\":2}")));}
    [Fact] public void UnknownFieldsAreRejected()
    {var json=JsonNode.Parse(ContractJson.Write(ContractFixture.Create()))!;json["Surprise"]=1;Assert.Throws<InvalidDataException>(()=>ContractJson.Read<ContractWorld>(Encoding.UTF8.GetBytes(json.ToJsonString())));}
    [Fact] public void InvalidUtf8IsRejected() => Assert.Throws<InvalidDataException>(()=>ContractJson.Read<ContractWorld>(new byte[]{0xff,0xfe}));
    [Theory] [InlineData("01")] [InlineData("+1")] [InlineData("-0")] [InlineData("9223372036854775808")]
    public void NoncanonicalOrOverflowingInt64IsRejected(string value)
    {var json=JsonNode.Parse(ContractJson.Write(ContractFixture.Create()))!;json["ElapsedSeconds"]=value;Assert.Throws<InvalidDataException>(()=>ContractJson.Read<ContractWorld>(Encoding.UTF8.GetBytes(json.ToJsonString())));}
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void InvalidSnapshotsFailBeforeBecomingLive(int fault)
    {
        var s=ContractFixture.Create();
        switch(fault){
            case 0:s.Characters=[..s.Characters,s.Characters[0]];break;
            case 1:s.Memberships=[s.Memberships[0] with{Target=new("org:missing")}];break;
            case 2:s.Grants=[s.Grants[0] with{From=5,Until=5}];break;
            case 3:s.Locations=s.Locations[..2];break;
            case 4:s.ResourceLots[0]=s.ResourceLots[0] with{Quantity=-1};break;
            case 5:s.ResourceLots[0]=s.ResourceLots[0] with{Quantity=101};break;
            case 6:s.TimeDomain="LegacyPrototypeTicks";break;
            case 7:s.ServiceBonds=[..s.ServiceBonds,new(new("service:cycle"),ContractFixture.Lord,ContractFixture.Officer,"service",new("basis:service"),0,long.MaxValue,0)];break;
        }
        Assert.Throws<InvalidDataException>(()=>new WorldSession(s));
    }
    [Fact] public void SnapshotAndViewsCannotMutateLiveState()
    {
        var s=Session();var before=Bytes(s);s.DiagnosticSnapshot().Grants=[];
        var view=s.GetActorView(ContractFixture.Officer);Assert.Empty(view.Tasks);Assert.Equal(before,Bytes(s));
    }
    [Fact] public void CallerCannotMutateAnAcceptedIntentArrayOrInitialWorld()
    {var initial=ContractFixture.Create();var s=new WorldSession(initial);initial.Characters=[];Assert.Equal(3,s.DiagnosticSnapshot().Characters.Length);}
    [Fact] public void IdenticalRetryReturnsOriginalReceiptBeforeRevisionCheck()
    {
        var s=Session();var i=ContractFixture.Order("request:one");var first=s.Submit(i,0);var before=Bytes(s);
        var retry=s.Submit(i,0);Assert.Equal(ResultCode.AlreadyAccepted,retry.Code);Assert.Equal(first.CommitSequence,retry.CommitSequence);Assert.Equal(before,Bytes(s));
    }
    [Fact] public void SameIdChangedPayloadCannotChangeAnExistingOrder()
    {
        var s=Session();var i=ContractFixture.Order("request:one");s.Submit(i,0);var before=Bytes(s);
        Assert.Equal(ResultCode.IdPayloadConflict,s.Submit(i with{WorkSeconds=5},s.Revision).Code);Assert.Equal(before,Bytes(s));
    }
    [Fact] public void StaleNewRequestDoesNotAdvanceCountersOrClock()
    {var s=Session();s.Submit(ContractFixture.Order("request:one"),0);var before=Bytes(s);Assert.Equal(ResultCode.StateChanged,s.Submit(ContractFixture.Order("request:two"),0).Code);Assert.Equal(before,Bytes(s));}
    [Fact] public void FailureBeforeCommitRollsBackEntireWorld()
    {var s=Session();var before=Bytes(s);Assert.Throws<IOException>(()=>s.Submit(ContractFixture.Order("request:one"),0,()=>throw new IOException("injected")));Assert.Equal(before,Bytes(s));}
    [Fact] public void AcceptanceDoesNotStartRemoteWork()
    {var s=Session();s.Submit(ContractFixture.Order("request:one"),0);Assert.Equal(ExecutionPhase.NotStarted,OnlyTask(s).Execution);Assert.Equal(0,s.Seconds);}
    [Fact] public void TaxCompletesOnceButIssuerDoesNotKnowUntilReportArrival()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:tax",ActionKind.Transfer,40),0);s.AdvanceTo(6);
        Assert.Equal(ExecutionPhase.Completed,OnlyTask(s).Execution);Assert.Equal(40,s.DiagnosticSnapshot().ResourceLots.Single(l=>l.Id==ContractFixture.Treasury).Quantity);
        Assert.Equal(AssessmentPhase.Pending,OnlyTask(s).Assessment);Assert.Contains("結果未確認",Assert.Single(s.GetActorView(ContractFixture.Lord).Tasks).KnownStatus);
        s.AdvanceTo(8);Assert.Equal(AssessmentPhase.Confirmed,OnlyTask(s).Assessment);Assert.Equal("任務完了",Assert.Single(s.GetActorView(ContractFixture.Lord).Tasks).KnownStatus);
        Assert.Single(s.DiagnosticSnapshot().Transfers);
    }
    [Fact] public void SealedArrivalPausesWithoutOpeningOrRecognition()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:secret",important:true),0);var r=s.AdvanceTo(60);
        Assert.Equal(2,r.ReachedAt);Assert.Equal("Paused",r.StopReason);Assert.Equal(AgreementPhase.AwaitingRecognition,OnlyTask(s).Agreement);
        var m=Assert.Single(s.GetActorView(ContractFixture.Officer).Messages);Assert.True(m.Sealed);Assert.Null(m.KnownBody);Assert.Empty(s.GetActorView(ContractFixture.Officer).Tasks);
        Assert.Equal(ResultCode.Accepted,s.Open(ContractFixture.Officer,m.Id,s.Revision).Code);Assert.Equal(ExecutionPhase.Running,OnlyTask(s).Execution);
    }
    [Fact] public void VoluntaryRequestNeedsAssent()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:optional",kind:TaskKind.VoluntaryRequest),0);s.AdvanceTo(2);var t=OnlyTask(s);
        Assert.Equal(AgreementPhase.AwaitingAssent,t.Agreement);Assert.Empty(s.DiagnosticSnapshot().Actions);
        Assert.Equal(ResultCode.Accepted,s.Assent(t.Id,ContractFixture.Officer,true,s.Revision).Code);Assert.Equal(ExecutionPhase.Running,OnlyTask(s).Execution);
    }
    [Fact] public void DeadlineInclusiveButGrantUntilExclusive()
    {
        var s=Session();var i=ContractFixture.Order("request:at-deadline") with{Deadline=new(DeadlineCriterion.FormalReceipt,2,false,null,20)};
        s.Submit(i,0);s.AdvanceTo(6);Assert.Equal(ExecutionPhase.Completed,OnlyTask(s).Execution);
        var w=ContractFixture.Create();w.Grants=w.Grants.Select(g=>g.Agent==ContractFixture.Officer?g with{Until=2}:g).ToArray();s=new(w);s.Submit(i,0);s.AdvanceTo(6);Assert.Equal(ExecutionPhase.Stopped,OnlyTask(s).Execution);
    }
    [Fact] public void DeadlineAtCompletionAllowsExactBoundary()
    {var s=Session();s.Submit(ContractFixture.Order("request:deadline",deadline:6),0);s.AdvanceTo(6);Assert.Equal(ExecutionPhase.Completed,OnlyTask(s).Execution);}
    [Fact] public void ExpiryReleasesOnlyUnconsumedReservation()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:deadline",ActionKind.Transfer,40,deadline:3),0);s.AdvanceTo(4);
        Assert.Equal(ExecutionPhase.Stopped,OnlyTask(s).Execution);Assert.Equal(ReservationPhase.Released,Assert.Single(s.DiagnosticSnapshot().Reservations).Phase);Assert.Empty(s.DiagnosticSnapshot().Transfers);
    }
    [Fact] public void CancelAtSameBoundarySuppressesOnlyUncommittedTarget()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:tax",ActionKind.Transfer,40),0);var target=OnlyTask(s).Id;
        s.Submit(ContractFixture.Order("request:cancel",ActionKind.Cancel) with{TargetTask=target,EffectiveAt=6},s.Revision);s.AdvanceTo(6);
        Assert.Equal(ExecutionPhase.Stopped,s.DiagnosticSnapshot().Tasks.Single(t=>t.Id==target).Execution);Assert.Empty(s.DiagnosticSnapshot().Transfers);
    }
    [Fact] public void LateCancellationDoesNotUndoCommittedTransfer()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:tax",ActionKind.Transfer,40),0);s.AdvanceTo(6);var target=OnlyTask(s).Id;
        s.Submit(ContractFixture.Order("request:cancel",ActionKind.Cancel) with{TargetTask=target},s.Revision);s.AdvanceTo(8);
        Assert.Single(s.DiagnosticSnapshot().Transfers);Assert.Equal(40,s.DiagnosticSnapshot().ResourceLots.Single(l=>l.Id==ContractFixture.Treasury).Quantity);
    }
    [Fact] public void MainActionConflictDoesNotUseRequestRegistrationOrder()
    {
        string[] Run(string a,string b){var s=Session();s.Submit(ContractFixture.Order(a),0);s.Submit(ContractFixture.Order(b),s.Revision);s.AdvanceTo(2);Assert.Single(s.DiagnosticSnapshot().ConflictDecisions);Assert.Empty(s.DiagnosticSnapshot().Actions);return s.DiagnosticSnapshot().Tasks.Select(t=>t.Execution.ToString()).ToArray();}
        Assert.Equal(Run("request:a","request:b"),Run("request:b","request:a"));
    }
    [Fact] public void ThirdPartyPropertyRequiresSpecificOwnerGrant()
    {
        var w=ContractFixture.Create();w.Grants=w.Grants.Where(g=>!g.Actions.Contains(ActionKind.Transfer)).ToArray();var s=new WorldSession(w);var before=Bytes(s);
        Assert.Equal(ResultCode.NotAvailable,s.Submit(ContractFixture.Order("request:tax",ActionKind.Transfer,40),0).Code);Assert.Equal(before,Bytes(s));
    }
    [Fact] public void LostOriginalAndRetryDoNotDoubleExecute()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:tax",ActionKind.Transfer,40),0);var message=s.DiagnosticSnapshot().Messaging.Messages.Single().Id;
        s.DiagnosticTransport((m,t)=>MessageFlow.Fail(m,message));s.AdvanceTo(2);
        Assert.Throws<InvalidOperationException>(()=>s.DiagnosticTransport((m,t)=>MessageFlow.Retry(m,message,ContractFixture.Lord.Value,t)));
        s.DiagnosticTransport((m,t)=>MessageFlow.ReportFailure(m,"message:failure",message+"/attempt/1",ContractFixture.Clerk.Value,t));s.AdvanceTo(3);
        s.DiagnosticTransport((m,t)=>{MessageFlow.ScheduleLate(m,message,1,t,2);MessageFlow.Retry(m,message,ContractFixture.Lord.Value,t,2);});
        s.AdvanceTo(12);Assert.Single(s.DiagnosticSnapshot().Transfers);Assert.Equal(40,s.DiagnosticSnapshot().ResourceLots.Single(l=>l.Id==ContractFixture.Treasury).Quantity);
    }
    [Fact] public void EventBudgetNeverPretendsToReachRequestedTime()
    {var s=Session();s.Submit(ContractFixture.Order("request:one"),0);var r=s.AdvanceTo(100,1);Assert.Equal(0,r.ReachedAt);Assert.Equal("EventBudget",r.StopReason);}
    [Fact] public void ReadingViewsDoesNotDrawRandomNumbers()
    {var s=Session();var before=Bytes(s);for(int n=0;n<10;n++)s.GetActorView(ContractFixture.Lord);Assert.Equal(before,Bytes(s));}
    [Fact] public void RandomDecisionsAreIdempotentAndSurviveRoundTrip()
    {
        var s=Session();var id=new EntityId("decision:one");var a=s.Draw(id,"decisions");var before=Bytes(s);Assert.Equal(a,s.Draw(id,"decisions"));Assert.Equal(before,Bytes(s));
        var restored=new WorldSession(s.DiagnosticSnapshot());Assert.Equal(s.Draw(new("decision:two"),"decisions"),restored.Draw(new("decision:two"),"decisions"));
    }
    [Fact] public void LegacyProjectionDoesNotMutateOrPretendToMigrate()
    {
        var world=PrototypeScenario.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"prototype.json")));var tax=TaxSimulation.Create(world);var before=tax.SaveJson();
        var report=LegacyContractAdapter.Inspect(tax);Assert.Equal(ResultCode.MigrationRequired,report.ProductImport);Assert.Equal("LegacyPrototypeTicks",report.TimeDomain);Assert.Equal(before,tax.SaveJson());Assert.Equal(3,report.Characters.Length);
    }
    [Fact] public void FutureEffectiveOrderDoesNotBlockCurrentlyEffectiveWork()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:future") with{EffectiveAt=10},0);
        s.Submit(ContractFixture.Order("request:now"),s.Revision);s.AdvanceTo(2);
        Assert.Single(s.DiagnosticSnapshot().Actions);Assert.Empty(s.DiagnosticSnapshot().ConflictDecisions);
        Assert.Equal(ExecutionPhase.Running,s.DiagnosticSnapshot().Tasks.Single(t=>t.RequestId==new EntityId("request:now")).Execution);
    }
    [Fact] public void OpeningTimeCannotLeakToRemoteIssuerThroughDeadline()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:sealed",important:true) with{Deadline=new(DeadlineCriterion.Completion,null,true,5,50)},0);
        s.AdvanceTo(2);var letter=Assert.Single(s.GetActorView(ContractFixture.Officer).Messages);s.Open(ContractFixture.Officer,letter.Id,s.Revision);
        Assert.Equal(7,Assert.Single(s.GetActorView(ContractFixture.Officer).Tasks).DeadlineAt);
        Assert.Equal(50,Assert.Single(s.GetActorView(ContractFixture.Lord).Tasks).DeadlineAt);
    }
    [Fact] public void UnopenedSealedLetterStillExpiresAtAbsoluteLimit()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:sealed",important:true,deadline:4) with{Deadline=new(DeadlineCriterion.Completion,null,true,10,4)},0);
        s.AdvanceTo(2);s.SetPaused(false);s.AdvanceTo(5);Assert.Equal(AgreementPhase.Expired,OnlyTask(s).Agreement);Assert.Empty(s.DiagnosticSnapshot().Actions);
    }
    [Fact] public void RemovingSealedPolicyFromSaveIsNotAccepted()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:sealed",important:true),0);var w=s.DiagnosticSnapshot();w.SealedMessages=[];
        Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Fact] public void SavedCounterCannotReuseACommittedTaskIdentity()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:first"),0);var w=s.DiagnosticSnapshot();w.IdCounters["task"]=0;
        Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Fact] public void CancelCannotBeDeliveredToSomeoneOtherThanTargetExecutor()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:first"),0);var target=OnlyTask(s).Id;
        var before=Bytes(s);Assert.Equal(ResultCode.NotAvailable,s.Submit(ContractFixture.Order("request:cancel",ActionKind.Cancel,executor:ContractFixture.Clerk) with{TargetTask=target},s.Revision).Code);Assert.Equal(before,Bytes(s));
    }
    [Fact] public void InFlightMoveKeepsPhysicalProgressWhenStopped()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:move",ActionKind.Move),0);s.AdvanceTo(3);var target=OnlyTask(s).Id;
        var during=s.DiagnosticSnapshot().Locations.Single(l=>l.Actor==ContractFixture.Officer);Assert.Equal(LocationKind.Traveling,during.Kind);Assert.Equal(1000,during.ProgressMm);
        s.Submit(ContractFixture.Order("request:cancel",ActionKind.Cancel) with{TargetTask=target},s.Revision);s.AdvanceTo(5);
        var after=s.DiagnosticSnapshot().Locations.Single(l=>l.Actor==ContractFixture.Officer);Assert.Equal(LocationKind.Traveling,after.Kind);Assert.Equal(3000,after.ProgressMm);Assert.Null(after.Site);
    }
    [Fact] public void FailedAdvanceRollsBackElapsedTimeKnowledgeAndEffects()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:first"),0);var before=Bytes(s);
        Assert.Throws<IOException>(()=>s.AdvanceTo(2,beforeCommit:()=>throw new IOException("injected")));Assert.Equal(before,Bytes(s));
    }
    [Fact] public void GrantAtSubmissionCannotAuthorizeCompletionAfterExpiry()
    {
        var w=ContractFixture.Create();w.Grants=w.Grants.Select(g=>g.Agent==ContractFixture.Officer?g with{Until=6}:g).ToArray();var s=new WorldSession(w);
        s.Submit(ContractFixture.Order("request:tax",ActionKind.Transfer,40),0);s.AdvanceTo(6);Assert.Empty(s.DiagnosticSnapshot().Transfers);Assert.Equal(ExecutionPhase.Stopped,OnlyTask(s).Execution);
    }
    [Fact] public void MainActionConflictCanBeResolvedByTargetedCancellation()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:a"),0);s.Submit(ContractFixture.Order("request:b"),s.Revision);s.AdvanceTo(2);
        var a=s.DiagnosticSnapshot().Tasks.Single(t=>t.RequestId==new EntityId("request:a"));s.Submit(ContractFixture.Order("request:cancel",ActionKind.Cancel) with{TargetTask=a.Id},s.Revision);
        s.AdvanceTo(4);Assert.Single(s.DiagnosticSnapshot().Actions);Assert.Equal(ExecutionPhase.Running,s.DiagnosticSnapshot().Tasks.Single(t=>t.RequestId==new EntityId("request:b")).Execution);
    }
    [Fact] public void ReceiptProxyCannotOpenOrGivePrincipalInstantKnowledge()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:proxy",important:true),0);var mid=s.DiagnosticSnapshot().Messaging.Messages.Single().Id;
        s.DiagnosticTransport((m,t)=>{m.Proxies=[new("proxy:receive",ContractFixture.Officer.Value,ContractFixture.Clerk.Value,ContractFixture.Castle.Value,"contract-order","fixture delegation",0,30,true,false)];MessageFlow.RouteTo(m,mid,ContractFixture.Clerk.Value);});
        s.AdvanceTo(2);Assert.Equal(AgreementPhase.AwaitingRecognition,OnlyTask(s).Agreement);
        Assert.Empty(s.GetActorView(ContractFixture.Officer).Tasks);Assert.True(Assert.Single(s.GetActorView(ContractFixture.Clerk).Messages).Sealed);
        Assert.Equal(ResultCode.NotAvailable,s.Open(ContractFixture.Officer,new(mid),s.Revision).Code);
        Assert.Equal(ResultCode.NotAvailable,s.Open(ContractFixture.Clerk,new(mid),s.Revision).Code);
        s.DiagnosticTransport((m,t)=>m.Proxies=m.Proxies.Select(p=>p with{Open=true}).ToArray());
        s.Open(ContractFixture.Clerk,new(mid),s.Revision);Assert.Empty(s.GetActorView(ContractFixture.Officer).Tasks);
        s.DiagnosticTransport((m,t)=>MessageFlow.Forward(m,mid,ContractFixture.Clerk.Value,"message:forward",t));s.AdvanceTo(3);
        Assert.Equal(ExecutionPhase.Running,OnlyTask(s).Execution);Assert.Single(s.GetActorView(ContractFixture.Officer).Tasks);
    }
    [Fact] public void FailedCompletionReportDoesNotRevertTruthOrInventEvaluation()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:tax",ActionKind.Transfer,40),0);s.AdvanceTo(6);
        var report=s.DiagnosticSnapshot().Messaging.Messages.Single(m=>m.Kind=="contract-report");s.DiagnosticTransport((m,t)=>MessageFlow.Fail(m,report.Id));s.AdvanceTo(8);
        Assert.Equal(ExecutionPhase.Completed,OnlyTask(s).Execution);Assert.Equal(AssessmentPhase.Pending,OnlyTask(s).Assessment);Assert.Single(s.DiagnosticSnapshot().Transfers);
        Assert.Contains("結果未確認",Assert.Single(s.GetActorView(ContractFixture.Lord).Tasks).KnownStatus);
    }
    [Fact] public void UnknownEvidenceCannotBeSmuggledIntoKnowledge()
    {
        var w=ContractFixture.Create();w.Knowledge=[new(ContractFixture.Officer,new("evidence:missing"),0)];Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Fact] public void CircularEventGraphIsRejectedWithoutRecursiveStackGrowth()
    {
        var a=new EntityId("event:a");var b=new EntityId("event:b");var task=new EntityId("task:1");
        Assert.Throws<InvalidDataException>(()=>ContractValidation.CheckAcyclic([new(a,task,EventKind.Execute,1,[b]),new(b,task,EventKind.Execute,1,[a])]));
    }
    [Fact] public void ForgedCompletionBeforeOrderRecognitionIsRejected()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:one"),0);var w=s.DiagnosticSnapshot();var t=w.Tasks.Single();
        w.PendingEvents=[new(WorldSessionTestEventId(t.Id),t.Id,EventKind.Finish,1,[])];
        Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    private static EntityId WorldSessionTestEventId(EntityId task)=>new("event:"+task.Value.Replace(':','/')+"/finish");
    [Fact] public void DefinitionSetOrderingDoesNotChangeDependencyIdentity()
    {
        var d=ContractFixture.Create().Definitions;
        Assert.Equal(ContractJson.CanonicalHash(d),ContractJson.CanonicalHash(d with{Characters=d.Characters.Reverse().ToArray(),AuthorityBases=d.AuthorityBases.Reverse().ToArray()}));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void RandomStateMustMatchTheSavedMasterSeed(bool changeSeed)
    {
        var w=ContractFixture.Create();if(changeSeed)w.Seed++;else w.RandomStreams=[w.RandomStreams[0] with{State=1}];
        Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Fact] public void OpeningTriggerDoesNotCountAsMeetingTheSubsequentCompletionDeadline()
    {
        var s=Session();s.Submit(ContractFixture.Order("request:sealed",important:true) with{WorkSeconds=10,Deadline=new(DeadlineCriterion.Completion,null,true,5,50)},0);
        s.AdvanceTo(2);s.Open(ContractFixture.Officer,Assert.Single(s.GetActorView(ContractFixture.Officer).Messages).Id,s.Revision);s.SetPaused(false);s.AdvanceTo(8);
        Assert.Equal(ExecutionPhase.Stopped,OnlyTask(s).Execution);Assert.Equal(AgreementPhase.Expired,OnlyTask(s).Agreement);
    }
    [Fact] public void LegacyFormationProjectionPreservesMilitaryIdentityAndTickDomain()
    {
        var legacy=TaxSimulation.Create(PrototypeScenario.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"prototype.json"))));
        legacy.EnableOfficerIntegration();legacy.StartFormationMap();legacy.SendFormation("line","character-001",new(8,1),new(8,5),[1,3,5]);legacy.AdvanceOneTick();
        var before=legacy.SaveJson();var report=LegacyContractAdapter.Inspect(legacy);var projection=Assert.IsType<LegacyMilitaryProjection>(report.Military);
        Assert.Equal("LegacyPrototypeTicks",report.TimeDomain);Assert.Equal(1,projection.LegacyTick);Assert.Equal(3,projection.Units.Length);Assert.Equal(3,projection.Orders.Length);
        Assert.Equal(legacy.MilitaryDiagnostic!.Units.Select(u=>u.Id),projection.Units.Select(u=>u.Id));
        Assert.Equal(legacy.MilitaryDiagnostic.Orders.Select(o=>o.Status.ToString()),projection.Orders.Select(o=>o.Status));
        Assert.NotEmpty(report.Authorities);Assert.Equal(before,legacy.SaveJson());Assert.Equal(ResultCode.MigrationRequired,report.ProductImport);
    }
}
