using System.Text.Json.Nodes;
using HakutaiKagami.Core.Commands;
using HakutaiKagami.Core.Messaging;
using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.World;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class SharedProtocolTests
{
    private static WorldState World() => PrototypeScenario.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prototype.json")));
    private static TaxSimulation Tax(long effect = 1)
    {
        var s = TaxSimulation.Create(World()); s.Submit("tax-001", "character-001", "castle-001", 100);
        s.DispatchCancellation("cancel-001", "tax-001", "character-001", 1, effect); return s;
    }
    private static void Advance(TaxSimulation s, int n) { for (int i = 0; i < n; i++) s.AdvanceOneTick(); }
    private static void Advance(InspectionSession s, int n) { for (int i = 0; i < n; i++) s.AdvanceOneTick(); }
    private static ProxyPermission Proxy(string principal, string kind, bool open = true, long until = 100) =>
        new("proxy-" + kind, principal, "character-003", "castle-001", kind, "試作の明示委任", 0, until, true, open);

    [Fact] public void PendingCountIncludesCancellationCommandAndReplyTransport()
    {
        var s = Tax(); Assert.Equal(2, s.PendingCount); s.AdvanceOneTick();
        Assert.Equal(2, s.PendingCount); s.AdvanceOneTick(); Assert.Equal(0, s.PendingCount);
    }

    [Theory] [InlineData(1, CancellationStatus.Applied)] [InlineData(4, CancellationStatus.TooLate)]
    public void CancellationReplyTruthAndSenderKnowledgeAreDifferent(long effect, CancellationStatus expected)
    {
        var s = Tax(effect); Advance(s, (int)effect); Assert.Equal(expected, s.Cancellations[0].Status);
        Assert.Null(s.CancellationResultFor("cancel-001", "character-001")); s.AdvanceOneTick();
        Assert.Equal(expected, s.CancellationResultFor("cancel-001", "character-001"));
    }
    [Fact] public void UnauthorizedCancellationReplyAlsoTravelsBeforeSenderKnows()
    {
        var s = Tax(2); s.AdvanceOneTick(); s.Revoke("character-001", "cancel-tax"); s.AdvanceOneTick();
        Assert.Equal(CancellationStatus.Unauthorized, s.Cancellations[0].Status); Assert.Null(s.CancellationResultFor("cancel-001", "character-001"));
        s.AdvanceOneTick(); Assert.Equal(CancellationStatus.Unauthorized, s.CancellationResultFor("cancel-001", "character-001"));
    }
    [Fact] public void LostCancellationRequiresDeliveredFailureReportBeforeRetry()
    {
        var s = Tax(); s.FailCancellation("cancel-001"); s.AdvanceOneTick(); var before = s.SaveJson();
        Assert.Throws<InvalidOperationException>(() => s.RetryCancellation("cancel-001", "character-001")); Assert.Equal(before, s.SaveJson());
        s.ReportMessageFailure("cancel-001/command/attempt/1", "failure-001");
        Assert.Throws<InvalidOperationException>(() => s.RetryCancellation("cancel-001", "character-001"));
        s.AdvanceOneTick(); s.RetryCancellation("cancel-001", "character-001"); s.AdvanceOneTick();
        Assert.Equal(CancellationStatus.Applied, s.Cancellations[0].Status); Assert.Equal(100, s.TreasuryRice);
    }
    [Fact] public void LostReplyRetryAndLateOriginalDoNotRepeatCancellationOrKnowledge()
    {
        var s = Tax(); s.AdvanceOneTick(); s.FailMessage("cancel-001/reply"); s.AdvanceOneTick();
        Assert.Null(s.CancellationResultFor("cancel-001", "character-001")); s.ReportMessageFailure("cancel-001/reply/attempt/1", "reply-failure");
        s.AdvanceOneTick(); s.ScheduleLateMessage("cancel-001/reply", 1, 3); s.RetryCancellationReply("cancel-001", "character-002");
        Advance(s, 5); Assert.Equal(CancellationStatus.Applied, s.CancellationResultFor("cancel-001", "character-001"));
        Assert.Equal(2, s.MessageAttempts.Count(a => a.MessageId == "cancel-001/reply" && a.Status == AttemptStatus.Delivered));
        Assert.Equal(100, s.TreasuryRice); Assert.Single(s.Deliveries, d => d.Kind == DeliveryKind.Report);
    }
    [Fact] public void CancellationRetryRechecksAuthorityAndCannotUseOldGrant()
    {
        var s = Tax(); s.FailCancellation("cancel-001"); s.AdvanceOneTick(); s.ReportMessageFailure("cancel-001/command/attempt/1", "failure");
        s.AdvanceOneTick(); s.Revoke("character-001", "cancel-tax"); var before = s.SaveJson();
        Assert.Throws<UnauthorizedAccessException>(() => s.RetryCancellation("cancel-001", "character-001")); Assert.Equal(before, s.SaveJson());
    }
    [Fact] public void ProxyReceiptDoesNotInformPrincipalOrExecuteBeforeForwardArrives()
    {
        var s = Tax(); s.SetProxyPermission(Proxy("character-002", "cancel-command")); s.RouteMessage("cancel-001/command", "character-003");
        s.AdvanceOneTick(); Assert.True(s.Cancellations[0].Delivered); Assert.Equal(1, s.Cancellations[0].ReceivedAt);
        Assert.Null(s.Cancellations[0].PrincipalLearnedAt); Assert.Equal(CancellationStatus.AwaitingEffect, s.Cancellations[0].Status);
        Assert.True(s.KnowsMessage("cancel-001/command", "character-003")); Assert.False(s.KnowsMessage("cancel-001/command", "character-002"));
        s.ForwardMessage("cancel-001/command", "character-003", "agent-report"); s.AdvanceOneTick();
        Assert.Equal(2, s.Cancellations[0].ResolvedAt); Assert.Equal(100, s.TreasuryRice);
    }
    [Fact] public void ProxyReplyDoesNotInformIssuerUntilForwarded()
    {
        var s = Tax(); s.AdvanceOneTick(); s.SetProxyPermission(Proxy("character-001", "cancel-result"));
        s.RouteMessage("cancel-001/reply", "character-003"); s.AdvanceOneTick();
        Assert.Null(s.CancellationResultFor("cancel-001", "character-001")); Assert.Equal(CancellationStatus.Applied, s.CancellationResultFor("cancel-001", "character-003"));
        s.ForwardMessage("cancel-001/reply", "character-003", "issuer-report"); s.AdvanceOneTick();
        Assert.Equal(CancellationStatus.Applied, s.CancellationResultFor("cancel-001", "character-001"));
    }
    [Fact] public void ReceiptOnlyProxyRevokedBeforeOpeningCannotLearnTheContent()
    {
        var s = Tax(); var p = Proxy("character-002", "cancel-command", false); s.SetProxyPermission(p);
        s.RouteMessage("cancel-001/command", "character-003"); s.AdvanceOneTick(); s.RevokeProxy(p.Id);
        var before = s.SaveJson(); Assert.Throws<UnauthorizedAccessException>(() => s.OpenProxyMessage("cancel-001/command", "character-003"));
        Assert.Equal(before, s.SaveJson()); Assert.False(s.KnowsMessage("cancel-001/command", "character-003"));
    }
    [Fact] public void WrongProxyScopeRejectsDeliveryWithoutGrantingKnowledge()
    {
        var s = Tax(); s.SetProxyPermission(Proxy("character-002", "cancel-command") with { Scope = "another-castle" });
        s.RouteMessage("cancel-001/command", "character-003"); s.AdvanceOneTick();
        Assert.Equal(CancellationStatus.DeliveryRejected, s.Cancellations[0].Status); Assert.False(s.KnowsMessage("cancel-001/command", "character-003"));
    }
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(6)]
    public void LostReplyRetryAndLateArrivalRecoverAtEveryBoundary(int boundary)
    {
        var s = Tax(); s.AdvanceOneTick(); s.FailMessage("cancel-001/reply");
        void Next(TaxSimulation session)
        {
            if (session.Tick == 2) session.ReportMessageFailure("cancel-001/reply/attempt/1", "failure");
            if (session.Tick == 3) { session.RetryCancellationReply("cancel-001", "character-002"); session.ScheduleLateMessage("cancel-001/reply", 1, 3); }
            session.AdvanceOneTick();
        }
        while (s.Tick < boundary) Next(s); var restored = TaxSimulation.LoadJson(s.SaveJson());
        while (s.Tick < 8) { Next(s); Next(restored); }
        Assert.Equal(s.SaveJson(), restored.SaveJson()); Assert.Equal(100, restored.TreasuryRice);
        Assert.Equal(CancellationStatus.Applied, restored.CancellationResultFor("cancel-001", "character-001"));
    }
    [Fact] public void CorruptSharedRecognitionAndRetryHistoryCannotBeLoaded()
    {
        var s = Tax(); s.AdvanceOneTick(); s.FailMessage("cancel-001/reply"); s.AdvanceOneTick();
        s.ReportMessageFailure("cancel-001/reply/attempt/1", "failure"); s.AdvanceOneTick(); s.RetryCancellationReply("cancel-001", "character-002");
        var original = s.SaveJson(); var corrupt = JsonNode.Parse(original)!;
        corrupt["Messaging"]!["Knowledge"]!.AsArray().Clear();
        Assert.Throws<InvalidDataException>(() => TaxSimulation.LoadJson(corrupt.ToJsonString()));
        Assert.Equal(original, s.SaveJson());
        corrupt = JsonNode.Parse(original)!; corrupt["Messaging"]!["Attempts"]!.AsArray().Last()!["SentAt"] = 2;
        Assert.Throws<InvalidDataException>(() => TaxSimulation.LoadJson(corrupt.ToJsonString()));
    }
    [Fact] public void ReceiptPermissionDoesNotAutomaticallyPermitOpening()
    {
        var s = Tax(); s.SetProxyPermission(Proxy("character-002", "cancel-command", false)); s.RouteMessage("cancel-001/command", "character-003"); s.AdvanceOneTick();
        Assert.True(s.Cancellations[0].Delivered); Assert.False(s.KnowsMessage("cancel-001/command", "character-003"));
        Assert.Throws<UnauthorizedAccessException>(() => s.ForwardMessage("cancel-001/command", "character-003", "bad-forward"));
        Assert.Throws<UnauthorizedAccessException>(() => s.OpenProxyMessage("cancel-001/command", "character-003"));
        s.SetProxyPermission(Proxy("character-002", "cancel-command") with { Id = "open-grant" });
        s.OpenProxyMessage("cancel-001/command", "character-003"); s.ForwardMessage("cancel-001/command", "character-003", "forward");
        s.AdvanceOneTick(); Assert.Equal(CancellationStatus.Applied, s.Cancellations[0].Status);
    }
    [Fact] public void RevokedProxyCannotReceiveOrOpenNewContentButRetainsMemory()
    {
        var s = Tax(); var p = Proxy("character-002", "cancel-command"); s.SetProxyPermission(p); s.RouteMessage("cancel-001/command", "character-003");
        s.AdvanceOneTick(); s.RevokeProxy(p.Id); Assert.True(s.KnowsMessage("cancel-001/command", "character-003"));
        s.ForwardMessage("cancel-001/command", "character-003", "remembered-report"); s.AdvanceOneTick();
        Assert.Equal(CancellationStatus.Applied, s.Cancellations[0].Status);
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public void ProxyRevokedOrExpiredAtArrivalHasNoReceiptOrKnowledge(long until)
    {
        var s = Tax(); s.SetProxyPermission(Proxy("character-002", "cancel-command", until: until)); s.RouteMessage("cancel-001/command", "character-003"); s.AdvanceOneTick();
        Assert.Equal(CancellationStatus.DeliveryRejected, s.Cancellations[0].Status); Assert.False(s.Cancellations[0].Delivered);
        Assert.False(s.KnowsMessage("cancel-001/command", "character-003")); Advance(s, 4); Assert.Equal(160, s.TreasuryRice);
    }
    [Fact] public void RoleChangeDoesNotTransferOldCommandKnowledgeOrExecutionAuthority()
    {
        var s = TaxSimulation.Create(World()); s.Submit("old", "character-001", "castle-001", 30);
        s.SetRole("character-002", "元奉行"); s.SetRole("character-003", "奉行"); s.Revoke("character-002", "collect-tax");
        s.GrantAuthority(new("character-003", "castle-001", "collect-tax", "明示的な新担当委任", 0, 100));
        Advance(s, 3); Assert.Equal(TaxPhase.Refused, s.Orders.Single().Phase); Assert.Empty(s.KnowledgeFor("character-003"));
        s.Submit("new", "character-001", "castle-001", 30, executorId: "character-003"); Advance(s, 4);
        Assert.Equal(130, s.TreasuryRice); Assert.Equal("character-003", s.Orders.Single(o => o.RequestId == "new").ExecutorId);
        Assert.Equal("奉行", s.RestoreWorld().Characters["character-003"].Role);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void CancellationReplySaveAtEveryBoundaryMatchesContinuousRun(int boundary)
    {
        var s = Tax(); Advance(s, boundary); var loaded = TaxSimulation.LoadJson(s.SaveJson()); Advance(s, 6); Advance(loaded, 6);
        Assert.Equal(s.SaveJson(), loaded.SaveJson()); Assert.Equal(CancellationStatus.Applied, loaded.CancellationResultFor("cancel-001", "character-001"));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ProxyWaitingAndForwardSaveBoundariesPreserveSeparateRecognition(int boundary)
    {
        var s = Tax(); s.SetProxyPermission(Proxy("character-002", "cancel-command")); s.RouteMessage("cancel-001/command", "character-003"); s.AdvanceOneTick();
        if (boundary >= 1) s.ForwardMessage("cancel-001/command", "character-003", "forward");
        if (boundary >= 2) s.AdvanceOneTick(); var loaded = TaxSimulation.LoadJson(s.SaveJson());
        if (boundary == 0) { s.ForwardMessage("cancel-001/command", "character-003", "forward"); loaded.ForwardMessage("cancel-001/command", "character-003", "forward"); }
        Advance(s, 5); Advance(loaded, 5); Assert.Equal(s.SaveJson(), loaded.SaveJson());
    }
    [Theory] [InlineData("tick-0")] [InlineData("tick-1")] [InlineData("tick-2")] [InlineData("tick-3")] [InlineData("tick-4")]
    [InlineData("cancel-transit")] [InlineData("effect-wait")] [InlineData("cancel-applied")] [InlineData("cancel-late")]
    public void V4SavesImportActualTransportWithoutInventingReplyRecognition(string suffix)
    {
        var s = TaxSimulation.LoadJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tax-v4-" + suffix + ".json")));
        Assert.Equal(5, JsonNode.Parse(s.SaveJson())!["Version"]!.GetValue<int>());
        if (suffix == "cancel-applied") Assert.Null(s.CancellationResultFor("cancel-001", "character-001"));
        Advance(s, 8); Assert.Equal(suffix is "cancel-transit" or "effect-wait" or "cancel-applied" ? 100 : 160, s.TreasuryRice);
    }
    [Fact] public void SecondDomainUsesSharedMessagingAndHidesResultUntilResponseArrival()
    {
        var s = InspectionSession.Create(World()); s.Submit("inspect-1", "character-001"); s.AdvanceOneTick();
        Assert.Equal(1, s.AppliedCount); Assert.Null(s.ResultFor("inspect-1", "character-001"));
        s.AdvanceOneTick(); Assert.Equal(InspectionOutcome.Completed, s.ResultFor("inspect-1", "character-001")!.Outcome);
        Assert.Equal("鏡城（仮）", s.ResultFor("inspect-1", "character-001")!.CastleName);
    }
    [Fact] public void InspectionLostRetryAndLateCopyDoNotRepeatBusinessEffect()
    {
        var s = InspectionSession.Create(World()); s.Submit("inspect-1", "character-001"); s.Fail("inspect-1/request"); s.AdvanceOneTick();
        s.ReportFailure("inspect-1/request", "inspect-failure"); s.AdvanceOneTick(); s.ScheduleLate("inspect-1/request", 1, 3);
        s.Retry("inspect-1/request", "character-001"); Advance(s, 6); Assert.Equal(1, s.AppliedCount);
        Assert.Equal(InspectionOutcome.Completed, s.ResultFor("inspect-1", "character-001")!.Outcome);
    }
    [Fact] public void InspectionCancellationAndRevocationUseSharedTransportAndPermissions()
    {
        var s = InspectionSession.Create(World()); s.Submit("inspect-1", "character-001", delay: 3); s.Cancel("inspect-1", "character-001"); Advance(s, 5);
        Assert.Equal(InspectionOutcome.Cancelled, s.ResultFor("inspect-1", "character-001")!.Outcome); Assert.Equal(1, s.AppliedCount);
        var revoked = InspectionSession.Create(World()); revoked.Submit("revoked", "character-001"); revoked.Revoke("character-001"); Advance(revoked, 2);
        Assert.Equal(InspectionOutcome.Denied, revoked.ResultFor("revoked", "character-001")!.Outcome);
    }
    [Fact] public void InspectionDeliveryDeadlineDoesNotRevealOrExecuteExpiredQuery()
    {
        var s = InspectionSession.Create(World()); s.Submit("late", "character-001", delay: 2, deliverBy: 1); Advance(s, 3);
        Assert.Equal(0, s.AppliedCount); Assert.Null(s.ResultFor("late", "character-001"));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void InspectionSaveUsesSameTransactionCodecAndPreservesContinuation(int boundary)
    {
        var s = InspectionSession.Create(World()); s.Submit("inspect-1", "character-001"); Advance(s, boundary);
        var loaded = InspectionSession.LoadJson(s.SaveJson()); Advance(s, 4); Advance(loaded, 4); Assert.Equal(s.SaveJson(), loaded.SaveJson());
    }
    [Fact] public void InspectionFailedCommitLeavesNoPartialKnowledgeOrResult()
    {
        var s = InspectionSession.Create(World()); s.Submit("inspect-1", "character-001"); var before = s.SaveJson();
        Assert.Throws<IOException>(() => s.AdvanceOneTick(() => throw new IOException("injected"))); Assert.Equal(before, s.SaveJson());
        Advance(s, 2); Assert.Equal(1, s.AppliedCount);
    }
}
