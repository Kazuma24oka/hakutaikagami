using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class FactionReactionTests
{
    static readonly EntityId Head=new("person:nobunaga"),Oda=new("org:oda"),Own=new("org:paper-test"),Original=new("decision:paper-hostility"),Notice=new("request:paper-notice");
    static WorldSession Start()=>FactionNoticeTests.Start();
    static void At(WorldSession s,long at)=>NewGameWorldTests.At(s,at);
    static void Send(WorldSession s)=>FactionNoticeTests.Send(s);
    static long Arrival(WorldSession s)=>FactionNoticeTests.Arrival(s);
    static void Open(WorldSession s){foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,m.Id,s.Revision).Code);}
    [Fact]public void NoPoliticalResponseOrArmyAppearsBeforeActualReceipt()
    {
        var s=Start();Send(s);At(s,Arrival(s)-1);var w=s.DiagnosticSnapshot();Assert.Empty(w.FactionNotices!.Reactions);Assert.Empty(w.NpcArmies!.Musters);Assert.DoesNotContain(FactionStances.Truth(w,s.Seconds),h=>h.FromOwner==Oda);
    }
    [Fact]public void OpenedThreatProducesOwnHeadDecisionReplyAndActualArmyFromItsHome()
    {
        var s=Start();Send(s);long at=Arrival(s);At(s,at);var w=s.DiagnosticSnapshot();var reaction=Assert.Single(w.FactionNotices!.Reactions);Assert.Equal(Notice,reaction.Source);Assert.Equal(Head,reaction.Actor);Assert.Equal(FactionStanceAction.DeclareHostile,reaction.Decision.Action);Assert.Equal(Oda,reaction.Decision.Owner);Assert.Equal(Own,reaction.Decision.Target);Assert.Equal(at,reaction.At);Assert.Contains(w.FactionStances!.Decisions,d=>d.Input==reaction.Decision);Assert.Equal(2,w.FactionNotices.Requests.Length);var army=Assert.Single(w.NpcArmies!.Musters);Assert.Equal(new EntityId("site:castle"),army.Site);Assert.Equal(reaction.Decision.Id,army.Objective!.Claim.Declaration);Assert.Single(w.NpcArmies.Marches);Assert.Empty(w.Branches!.Journeys);
        At(s,at+2);Assert.Contains(s.DiagnosticSnapshot().Branches!.Journeys,j=>j.Actor==Head);
    }
    [Fact]public void PrivateWithdrawalDoesNotStopNpcFromActingOnTheOldReceivedThreat()
    {
        var s=Start();Send(s);Assert.Equal(ResultCode.Accepted,s.DecideFactionStance(new(new("decision:private-withdraw"),FactionStanceAction.WithdrawOwnDeclaration,Own,Oda,0,0,Original),s.PlayerControl,s.Revision).Code);At(s,Arrival(s));var w=s.DiagnosticSnapshot();var truth=FactionStances.Truth(w,s.Seconds);Assert.Contains(truth,h=>h.FromOwner==Oda&&h.ToOwner==Own);Assert.DoesNotContain(truth,h=>h.FromOwner==Own&&h.ToOwner==Oda);Assert.Single(w.NpcArmies!.Musters);
    }
    [Fact]public void NoticeToFormerHomeCannotInformAnAlreadyDepartedHead()
    {
        var s=Start();Send(s);long first=Arrival(s);At(s,first);Assert.Equal(ResultCode.Accepted,s.DecideFactionStance(new(new("decision:explicit-withdraw"),FactionStanceAction.WithdrawOwnDeclaration,Own,Oda,s.Seconds,0,Original),s.PlayerControl,s.Revision).Code);Assert.Equal(ResultCode.Accepted,s.NotifyFactionStance(new(new("request:withdrawal-notice"),Original),s.PlayerControl,s.Revision).Code);var w=s.DiagnosticSnapshot();var paper=w.FactionNotices!.Requests.Single(q=>q.Input.Id==new EntityId("request:withdrawal-notice"));long end=w.Couriers!.Missions.Single(c=>w.Messaging.Attempts.Any(a=>a.Id==c.Attempt&&a.MessageId==paper.Message)).ReadyAt;At(s,end);w=s.DiagnosticSnapshot();Assert.Single(w.FactionNotices!.Reactions);Assert.DoesNotContain(w.FactionNotices.Openings,o=>o.Message==paper.Message);Assert.DoesNotContain(w.Messaging.Attempts,a=>a.MessageId==paper.Message&&a.Status==HakutaiKagami.Core.Messaging.AttemptStatus.Delivered);Assert.Contains(FactionStances.Truth(w,s.Seconds),h=>h.FromOwner==Oda&&h.ToOwner==Own);
    }
    [Fact]public void NewExplicitWithdrawalNoticeCausesASeparateOwnNpcWithdrawalDecision()
    {
        var s=Start();Send(s);long first=Arrival(s);At(s,1);Assert.Equal(ResultCode.Accepted,s.DecideFactionStance(new(new("decision:explicit-withdraw"),FactionStanceAction.WithdrawOwnDeclaration,Own,Oda,s.Seconds,0,Original),s.PlayerControl,s.Revision).Code);Assert.Equal(ResultCode.Accepted,s.NotifyFactionStance(new(new("request:withdrawal-notice"),Original),s.PlayerControl,s.Revision).Code);var w=s.DiagnosticSnapshot();var paper=w.FactionNotices!.Requests.Single(q=>q.Input.Id==new EntityId("request:withdrawal-notice"));long end=w.Couriers!.Missions.Single(c=>w.Messaging.Attempts.Any(a=>a.Id==c.Attempt&&a.MessageId==paper.Message)).ReadyAt;At(s,end);w=s.DiagnosticSnapshot();Assert.Equal(2,w.FactionNotices!.Reactions.Length);var firstDecision=w.FactionNotices.Reactions[0].Decision;var last=w.FactionNotices.Reactions[1];Assert.Equal(FactionStanceAction.WithdrawOwnDeclaration,last.Decision.Action);Assert.Equal(firstDecision.Id,last.Decision.Original);Assert.DoesNotContain(FactionStances.Truth(w,s.Seconds),h=>h.FromOwner==Oda&&h.ToOwner==Own);Assert.Single(w.NpcArmies!.Musters);
    }
    [Fact]public void NpcReplyDoesNotBecomePlayerKnowledgeUntilItArrivesAndIsOpened()
    {
        var s=Start();Send(s);At(s,Arrival(s));var w=s.DiagnosticSnapshot();var reaction=w.FactionNotices!.Reactions.Single();var reply=w.FactionNotices.Requests.Single(q=>q.Sender==Head);long arrival=w.Couriers!.Missions.Single(c=>w.Messaging.Attempts.Any(a=>a.Id==c.Attempt&&a.MessageId==reply.Message)).ReadyAt;Assert.DoesNotContain(FactionStances.Known(w,s.PlayerControl),c=>c.Declaration==reaction.Decision.Id);At(s,arrival);Assert.DoesNotContain(FactionStances.Known(s.DiagnosticSnapshot(),s.PlayerControl),c=>c.Declaration==reaction.Decision.Id);Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(reply.Message),s.Revision).Code);Assert.Contains(FactionStances.Known(s.DiagnosticSnapshot(),s.PlayerControl),c=>c.Declaration==reaction.Decision.Id&&c.ObservedAt==reaction.At);
    }
    [Fact]public void RepeatedClockTurnsDoNotDuplicateReactionArmyOrDispatch()
    {
        var s=Start();Send(s);long arrival=Arrival(s);At(s,arrival+10);var w=s.DiagnosticSnapshot();Assert.Single(w.FactionNotices!.Reactions);Assert.Equal(2,w.FactionNotices.Requests.Length);Assert.Single(w.NpcArmies!.Musters);Assert.Single(w.NpcArmies.Marches);
    }
    [Theory][InlineData("source")][InlineData("actor")][InlineData("decision")][InlineData("missing")]
    public void UnreceivedOrAlteredPoliticalSourceCannotBeLoaded(string kind)
    {
        var s=Start();Send(s);At(s,Arrival(s));var w=s.DiagnosticSnapshot();var q=w.FactionNotices!.Reactions[0];
        if(kind=="source")w.FactionNotices.Reactions[0]=q with{Source=new("request:missing")};
        if(kind=="actor")w.FactionNotices.Reactions[0]=q with{Actor=s.PlayerControl};
        if(kind=="decision")w.FactionNotices.Reactions[0]=q with{Decision=q.Decision with{Until=q.Decision.Until+1}};
        if(kind=="missing")w.FactionNotices=w.FactionNotices with{Openings=[]};
        Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Fact]public void PaperToNpcReactionToArmyToRealLossAndHandbackUsesOnlyPublicPlayerActions()
    {
        var s=FactionNoticeTests.Start(declare:false);var proclamation=new FactionProclamationInput(new(Original,FactionStanceAction.DeclareHostile,Own,Oda,0,1000000),new(Notice,Original));Assert.Equal(ResultCode.Accepted,s.ProclaimFactionStance(proclamation,s.PlayerControl,s.Revision).Code);
        void Military(NewMilitaryAction a,long amount=0){SendMilitary(s,a,amount);}
        Military(NewMilitaryAction.Request);At(s,2);Open(s);Military(NewMilitaryAction.Accept);At(s,4);Open(s);Military(NewMilitaryAction.Pickup);Military(NewMilitaryAction.Load,NewGameMilitary.Capacity);
        At(s,Arrival(s)+2);var w=s.DiagnosticSnapshot();var march=w.Branches!.Journeys.Single(j=>j.Actor==Head);At(s,march.Legs[^1].EndAt-1);
        var permission=new NewMilitaryInput(new("request:paper-battle-engagement"),NewMilitaryAction.RequestEngagement){Engagement=new(NewGameMilitary.EngagementTaskContract,Oda,s.Seconds,s.Seconds+600)};Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(permission,s.PlayerControl,s.Revision).Code);At(s,s.Seconds+2);Open(s);Military(NewMilitaryAction.Deploy);At(s,s.Seconds+2);w=s.DiagnosticSnapshot();Assert.NotEmpty(WorldClashes.Losses(w,s.Seconds));int remaining=NewGameMilitary.TotalStrength(w,s.Seconds);Assert.True(remaining<100);
        s.SetPaused(true);w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"normal-entry-clash-"+Guid.NewGuid().ToString("N")),w.Definitions);Assert.True(store.Save(s,"case",w.WorldId).Published);var host=new WorldSessionHost(Start());Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("case"),host.Current,host.Current.Revision));var recovered=host.Current;Assert.Equal(ContractJson.Write(w),ContractJson.Write(recovered.DiagnosticSnapshot()));ExportEntry(w,"entry-clash","before");ExportEntry(recovered.DiagnosticSnapshot(),"entry-clash","restored");
        foreach(var run in new[]{s,recovered}){SendMilitary(run,NewMilitaryAction.Recall);SendMilitary(run,NewMilitaryAction.Enter);SendMilitary(run,NewMilitaryAction.Unload,NewGameMilitary.View(run.DiagnosticSnapshot()).Carry);SendMilitary(run,NewMilitaryAction.Report);SendMilitary(run,NewMilitaryAction.Return);At(run,run.Seconds+3);Open(run);}
        w=s.DiagnosticSnapshot();Assert.Equal(ContractJson.Write(w),ContractJson.Write(recovered.DiagnosticSnapshot()));Assert.Equal(remaining,w.NewMilitary!.Reports.Last().Soldiers);Assert.Equal(remaining,NewGameMilitary.TotalStrength(w,s.Seconds));Assert.False(NewGameMilitary.CustodyAt(w,s.Seconds,s.Revision));new WorldSession(w);ExportEntry(w,"entry-return","before");ExportEntry(recovered.DiagnosticSnapshot(),"entry-return","restored");
    }
    [Fact]public void PlayerCannotAdoptNpcPoliticalOrPaperIdsAsTheirOwnIdempotentActions()
    {
        var s=Start();Send(s);At(s,Arrival(s));var w=s.DiagnosticSnapshot();var reaction=w.FactionNotices!.Reactions[0];var reply=w.FactionNotices.Requests.Single(q=>q.Sender==Head);var before=ContractJson.Write(w);Assert.Equal(ResultCode.NotAvailable,s.DecideFactionStance(reaction.Decision,s.PlayerControl,s.Revision).Code);Assert.Equal(ResultCode.NotAvailable,s.NotifyFactionStance(reply.Input,s.PlayerControl,s.Revision).Code);Assert.Equal(ResultCode.NotAvailable,s.ProclaimFactionStance(new(reaction.Decision,reply.Input),s.PlayerControl,s.Revision).Code);Assert.Equal(before,ContractJson.Write(s.DiagnosticSnapshot()));
    }
    [Fact]public void PoliticalKnowledgeCannotBeQueriedAtAnUnsimulatedFutureOrRevision()
    {
        var s=Start();Send(s);var w=s.DiagnosticSnapshot();Assert.Throws<InvalidDataException>(()=>FactionNotices.Known(w,Head,1,w.CommitSequence));Assert.Throws<InvalidDataException>(()=>FactionNotices.Known(w,Head,0,w.CommitSequence+1));
    }

    static void SendMilitary(WorldSession s,NewMilitaryAction a,long amount=0)
    {
        var q=new NewMilitaryInput(new("request:paper-battle/"+s.Revision+"/"+a),a,amount){AuthorityPolicy=a==NewMilitaryAction.Request?NewGameMilitary.AuthorityContract:null,LedgerPolicy=a==NewMilitaryAction.Request?NewGameMilitary.LedgerIdentityContract:null};var result=s.DecideNewMilitary(q,s.PlayerControl,s.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
    }
    static void ExportEntry(ContractWorld world,string phase,string variant)
    {
        var dir=Environment.GetEnvironmentVariable("HAKUTAI_WORLD_CLASH_QA_DIR");if(string.IsNullOrEmpty(dir))return;Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,phase+"."+variant+".json"),ContractJson.Write(world));
    }

}
