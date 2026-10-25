using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class FactionNoticeTests
{
    static readonly EntityId Oda=new("org:oda"),Own=new("org:paper-test"),Head=new("person:nobunaga"),Original=new("decision:paper-hostility"),Notice=new("request:paper-notice");
    internal static WorldSession Start(bool combat=true,bool declare=true)
    {
        var p=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:"site:fork");var setup=NewGameWorldTests.Setup([p],[new(p.Id,Own.Value,[p.Site],false)]);var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,p.Id,worldArmyPolicy:combat?NpcArmies.CompactCombatPolicy:NpcArmies.Policy));if(declare)Assert.Equal(ResultCode.Accepted,s.DecideFactionStance(new(Original,FactionStanceAction.DeclareHostile,Own,Oda,0,1000000),s.PlayerControl,s.Revision).Code);return s;
    }
    internal static void Send(WorldSession s){var r=s.NotifyFactionStance(new(Notice,Original),s.PlayerControl,s.Revision);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);}
    internal static long Arrival(WorldSession s)=>s.DiagnosticSnapshot().Couriers!.Missions.Single(c=>s.DiagnosticSnapshot().Messaging.Attempts.Any(a=>a.Id==c.Attempt&&a.MessageId==s.DiagnosticSnapshot().FactionNotices!.Requests[0].Message)).ReadyAt;
    static FactionStanceClaim[] Known(WorldSession s)=>FactionStances.Known(s.DiagnosticSnapshot(),Head).Where(k=>k.Declaration==Original).ToArray();
    [Fact]public void DispatchCreatesDatedSealedPhysicalPaperWithoutInformingOrCommandingOpponent()
    {
        var s=Start();var before=s.DiagnosticSnapshot();Send(s);var w=s.DiagnosticSnapshot();var q=Assert.Single(w.FactionNotices!.Requests);Assert.Equal(Head,q.Recipient);Assert.Equal(new EntityId("site:castle"),q.Address);Assert.Equal(0,q.Claim.ObservedAt);Assert.Empty(Known(s));Assert.Empty(w.Units);Assert.Equal(ContractJson.Write(before.FactionStances),ContractJson.Write(w.FactionStances));Assert.Contains(new EntityId(q.Message),w.SealedMessages);Assert.Equal("castle",w.Couriers!.Missions.Single().Destination);
    }
    [Fact]public void NpcLearnsOnlyAtActualArrivalAndOpening()
    {
        var s=Start();Send(s);long arrival=Arrival(s);Assert.True(arrival>1);NewGameWorldTests.At(s,arrival-1);Assert.Empty(Known(s));NewGameWorldTests.At(s,arrival);var claim=Assert.Single(Known(s));Assert.Equal(1000000,claim.Until);Assert.Equal(0,claim.ObservedAt);var opening=Assert.Single(s.DiagnosticSnapshot().FactionNotices!.Openings);Assert.Equal(arrival,opening.At);Assert.Equal(Head,opening.Actor);
    }
    [Fact]public void PrivateWithdrawalCannotRewriteAnOldDispatchedOrReceivedPaper()
    {
        var s=Start();Send(s);Assert.Equal(ResultCode.Accepted,s.DecideFactionStance(new(new("decision:paper-withdraw"),FactionStanceAction.WithdrawOwnDeclaration,Own,Oda,0,0,Original),s.PlayerControl,s.Revision).Code);Assert.Empty(FactionStances.Truth(s.DiagnosticSnapshot(),0));NewGameWorldTests.At(s,Arrival(s));Assert.Equal(1000000,Assert.Single(Known(s)).Until);
    }
    [Fact]public void LaterExplicitNotificationCarriesTheUpdatedDatedWithdrawal()
    {
        var s=Start();Send(s);NewGameWorldTests.At(s,1);Assert.Equal(ResultCode.Accepted,s.DecideFactionStance(new(new("decision:paper-withdraw"),FactionStanceAction.WithdrawOwnDeclaration,Own,Oda,s.Seconds,0,Original),s.PlayerControl,s.Revision).Code);Assert.Equal(ResultCode.Accepted,s.NotifyFactionStance(new(new("request:paper-update"),Original),s.PlayerControl,s.Revision).Code);var w=s.DiagnosticSnapshot();long end=w.Couriers!.Missions.Max(c=>c.ReadyAt);NewGameWorldTests.At(s,end);Assert.Equal(1,Assert.Single(Known(s)).Until);Assert.Equal(1,Assert.Single(Known(s)).ObservedAt);
    }
    [Fact]public void DuplicateIdDoesNotSendAnotherPaperAndChangedOriginalConflicts()
    {
        var s=Start();Send(s);var hash=ContractJson.CanonicalHash(s.DiagnosticSnapshot());Assert.Equal(ResultCode.AlreadyAccepted,s.NotifyFactionStance(new(Notice,Original),s.PlayerControl,0).Code);Assert.Equal(ResultCode.IdPayloadConflict,s.NotifyFactionStance(new(Notice,new("decision:elsewhere")),s.PlayerControl,s.Revision).Code);Assert.Equal(hash,ContractJson.CanonicalHash(s.DiagnosticSnapshot()));
    }
    [Fact]public void ActorControlSourceOwnershipVersionAndRevisionAreChecked()
    {
        var s=Start();Assert.Equal(ResultCode.NotAvailable,s.NotifyFactionStance(new(Notice,Original),Head,s.Revision).Code);Assert.Equal(ResultCode.NotAvailable,s.NotifyFactionStance(new(Notice,new("decision:missing")),s.PlayerControl,s.Revision).Code);Assert.Equal(ResultCode.StateChanged,s.NotifyFactionStance(new(Notice,Original),s.PlayerControl,0).Code);var old=Start(false);Assert.Equal(ResultCode.NotAvailable,old.NotifyFactionStance(new(Notice,Original),old.PlayerControl,old.Revision).Code);
    }
    [Fact]public void NotificationIdsCannotCollideWithPoliticalOrTravelInputs()
    {
        var s=Start();Send(s);Assert.Equal(ResultCode.IdPayloadConflict,s.DecideFactionStance(new(Notice,FactionStanceAction.WithdrawOwnDeclaration,Own,Oda,0,0,Original),s.PlayerControl,s.Revision).Code);Assert.Equal(ResultCode.IdPayloadConflict,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),Notice.Value,"castle"),s.Revision).Code);Assert.Equal(ResultCode.IdPayloadConflict,s.ChangeTravel(new(Notice,s.PlayerControl,new("task:missing"),null),s.Revision).Code);var other=Start();Assert.Equal(ResultCode.IdPayloadConflict,other.NotifyFactionStance(new(Original,Original),other.PlayerControl,other.Revision).Code);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void GenerationRecoveryPreservesPhysicalDeliveryOpeningAndSameFuture(int phase)
    {
        var s=Start();Send(s);long end=Arrival(s);NewGameWorldTests.At(s,phase==0?0:phase==1?end/2:end);s.SetPaused(true);var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"faction-paper-"+Guid.NewGuid().ToString("N")),w.Definitions);Assert.True(store.Save(s,"case",w.WorldId).Published);var host=new WorldSessionHost(Start());Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("case"),host.Current,host.Current.Revision));Assert.Equal(ContractJson.Write(w),ContractJson.Write(host.Current.DiagnosticSnapshot()));foreach(var x in new[]{s,host.Current})NewGameWorldTests.At(x,end+2);Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
    }
    [Theory][InlineData("address")][InlineData("claim")][InlineData("opening")][InlineData("body")][InlineData("missing")]
    public void FabricatedAddressClaimOpeningBodyOrMissingSourceCannotLoad(string kind)
    {
        var s=Start();Send(s);var w=s.DiagnosticSnapshot();var q=w.FactionNotices!.Requests[0];
        if(kind=="address")w.FactionNotices.Requests[0]=q with{Address=new("site:fork")};
        if(kind=="claim")w.FactionNotices.Requests[0]=q with{Claim=q.Claim with{Until=50}};
        if(kind=="opening")w.FactionNotices=w.FactionNotices with{Openings=[new(q.Message,Head,0,w.CommitSequence)]};
        if(kind=="body")w.Messaging.Messages=w.Messaging.Messages.Select(m=>m.Id==q.Message?m with{Body="forged"}:m).ToArray();
        if(kind=="missing")w.FactionNotices=null;
        Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
}
