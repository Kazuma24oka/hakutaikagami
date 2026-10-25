using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class EngagementTaskDeliveryTests
{
    static void At(WorldSession s,long at)=>NewGameWorldTests.At(s,at);
    static void Open(WorldSession s){foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,m.Id,s.Revision).Code);}
    static void Send(WorldSession s,NewMilitaryAction action){var r=s.DecideNewMilitary(new(new("request:engagement-delivery/"+s.Revision+"/"+action),action),s.PlayerControl,s.Revision);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);}
    static WorldSession Ready()
    {
        var s=new WorldSession(MilitaryDebriefTests.New());
        var input=new NewMilitaryInput(new("request:engagement-loan"),NewMilitaryAction.Request){AuthorityPolicy=NewGameMilitary.AuthorityContract,LedgerPolicy=NewGameMilitary.LedgerIdentityContract};
        Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(input,s.PlayerControl,s.Revision).Code);
        At(s,2);Open(s);Send(s,NewMilitaryAction.Accept);At(s,4);Open(s);Send(s,NewMilitaryAction.Pickup);return s;
    }
    static NewMilitaryInput Request(WorldSession s,long? from=null,long? until=null)=>new(new("request:real-engagement"),NewMilitaryAction.RequestEngagement){Engagement=new(NewGameMilitary.EngagementTaskContract,s.DiagnosticSnapshot().Definitions.Organizations.First(o=>o.Id!=s.DiagnosticSnapshot().NewMilitary!.Terms.Owner).Id,from??s.Seconds,until??s.Seconds+60)};
    static void Start(WorldSession s,NewMilitaryInput? request=null){var r=s.DecideNewMilitary(request??Request(s),s.PlayerControl,s.Revision);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);}
    static EngagementMandate[] Terms(WorldSession s)=>NewGameMilitary.EngagementMandates(s.DiagnosticSnapshot(),s.Seconds);
    static void Cancel(WorldSession s){var r=s.DecideNewMilitary(new(new("request:real-engagement-cancel"),NewMilitaryAction.CancelEngagement){EngagementOriginal=new("request:real-engagement")},s.PlayerControl,s.Revision);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);}

    [Fact]public void RequestPermissionArrivalAndPersonalOpeningAreSeparate()
    {
        var s=Ready();Start(s);Assert.Empty(Terms(s));var status=NewGameMilitary.EngagementKnownStatus(s.DiagnosticSnapshot());
        At(s,5);Assert.Null(Assert.Single(Terms(s)).ReceivedAt);Assert.Equal(status,NewGameMilitary.EngagementKnownStatus(s.DiagnosticSnapshot()));
        At(s,6);Assert.Null(Assert.Single(Terms(s)).ReceivedAt);Open(s);Assert.Equal(6,Assert.Single(Terms(s)).ReceivedAt);
        Assert.DoesNotContain(RegionalAction.Engage,Assert.Single(s.DiagnosticSnapshot().NewMilitary!.Authority!.Grants).Mandate.Actions);
    }
    [Fact]public void PermissionAloneCannotStartWarOrCreateOpponentTroops()
    {
        var s=Ready();var before=s.DiagnosticSnapshot();Start(s);At(s,6);Open(s);var w=s.DiagnosticSnapshot();
        Assert.Equal(ContractJson.Write(before.Units),ContractJson.Write(w.Units));Assert.Equal(before.Characters.Length,w.Characters.Length);
        var m=Assert.Single(Terms(s));var own=w.Units[0];var enemy=own with{Id=new("unit:external-existing"),Army=new("army:external-existing"),Organization=m.TargetOwner,Commander=new("person:external-existing")};
        var r=FieldEngagement.Resolve(s.Seconds,[new(own,new(0,0),0,100,"standing",EngagementIntent.Mission,true),new(enemy,new(3000,0),0,100,"standing",EngagementIntent.SelfDefense,true)],Terms(s),[]);
        Assert.Empty(r.Losses);
    }
    [Fact]public void ActualReceivedTermsScopeOnlyCurrentOwnerArmyAndCommander()
    {
        var s=Ready();Start(s);At(s,6);Open(s);var m=Assert.Single(Terms(s));var w=s.DiagnosticSnapshot();
        Assert.Equal(w.PlayerControl,m.Agent);Assert.Equal(w.NewMilitary!.Terms.Army,m.Army);Assert.Equal(NewGameMilitary.UnitIdsFor(w),m.Units);
        var notice=w.NewMilitary.Events.Single(e=>e.Effect==MilitaryEffect.EngagementPermission);
        var body=ContractJson.Read<MilitaryNotice>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(x=>x.Id==notice.Message).Body));
        Assert.Equal(w.NewMilitary.Terms.Principal,body.Engagement!.Authority.Issuer);Assert.Equal(5,body.Engagement.Authority.At);
    }
    [Fact]public void CancellationDoesNotActAtDispatchOrBeforePersonalOpening()
    {
        var s=Ready();Start(s);At(s,6);Open(s);Cancel(s);Assert.Null(Assert.Single(Terms(s)).CancelledAt);
        At(s,7);Assert.Null(Assert.Single(Terms(s)).CancelledAt);At(s,8);Assert.Null(Assert.Single(Terms(s)).CancelledAt);
        Open(s);Assert.Equal(8,Assert.Single(Terms(s)).CancelledAt);
    }
    [Fact]public void OriginalAndCancellationCanArriveBeforeEitherIsOpened()
    {
        var s=Ready();Start(s);Cancel(s);At(s,6);Open(s);var m=Assert.Single(Terms(s));Assert.Equal(6,m.ReceivedAt);Assert.Equal(6,m.CancelledAt);
    }
    [Fact]public void ExpiredAtIssuerProducesRefusalWithoutAnAttackGrant()
    {
        var s=Ready();Start(s,Request(s,until:5));At(s,6);Assert.Empty(Terms(s));Assert.DoesNotContain("許可されません",NewGameMilitary.EngagementKnownStatus(s.DiagnosticSnapshot()));Open(s);
        Assert.Contains("許可されません",NewGameMilitary.EngagementKnownStatus(s.DiagnosticSnapshot()));Assert.Empty(Terms(s));
    }
    [Fact]public void RepeatedSameRequestIsIdempotentAndChangedTargetIsRejected()
    {
        var s=Ready();var q=Request(s);Start(s,q);var w=s.DiagnosticSnapshot();
        Assert.Equal(ResultCode.AlreadyAccepted,s.DecideNewMilitary(q,s.PlayerControl,s.Revision).Code);
        Assert.Equal(ResultCode.IdPayloadConflict,s.DecideNewMilitary(q with{Engagement=q.Engagement! with{Until=99}},s.PlayerControl,s.Revision).Code);
        Assert.Equal(ContractJson.CanonicalHash(w),ContractJson.CanonicalHash(s.DiagnosticSnapshot()));
    }
    [Theory][InlineData("self")][InlineData("unknown")][InlineData("version")][InlineData("expired")][InlineData("wrong-action")]
    public void InvalidScopeIsRejectedAtomically(string kind)
    {
        var s=Ready();var w=s.DiagnosticSnapshot();var q=Request(s);var x=q.Engagement!;
        q=kind switch{"self"=>q with{Engagement=x with{TargetOwner=w.NewMilitary!.Terms.Owner}},"unknown"=>q with{Engagement=x with{TargetOwner=new("org:unknown")}},"version"=>q with{Engagement=x with{Contract="unknown"}},"expired"=>q with{Engagement=x with{Until=x.EffectiveFrom}},_=>q with{Action=NewMilitaryAction.Inquire}};
        Assert.Equal(ResultCode.NotAvailable,s.DecideNewMilitary(q,s.PlayerControl,s.Revision).Code);Assert.Equal(ContractJson.CanonicalHash(w),ContractJson.CanonicalHash(s.DiagnosticSnapshot()));
    }
    [Theory][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)][InlineData(8)]
    public void GenerationRecoveryPreservesUnopenedPermissionAndCancellation(int at)
    {
        var s=Ready();Start(s);Cancel(s);At(s,at);s.SetPaused(true);var w=s.DiagnosticSnapshot();
        var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"engagement-tasks-"+Guid.NewGuid().ToString("N")),w.Definitions);
        Assert.True(store.Save(s,"case",w.WorldId,new(0,1)).Published);
        var host=new WorldSessionHost(new WorldSession(MilitaryDebriefTests.New()));Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("case"),host.Current,host.Current.Revision));var copy=host.Current;
        Assert.Equal(ContractJson.CanonicalHash(w),ContractJson.CanonicalHash(copy.DiagnosticSnapshot()));
        foreach(var v in new[]{s,copy}){At(v,9);Open(v);At(v,12);}
        Assert.Equal(ContractJson.CanonicalHash(s.DiagnosticSnapshot()),ContractJson.CanonicalHash(copy.DiagnosticSnapshot()));
        Assert.Equal(ContractJson.Write(Terms(s)),ContractJson.Write(Terms(copy)));
    }
    [Fact]public void SavedPermissionCannotAcquireDifferentOwnershipOrAuthority()
    {
        var s=Ready();Start(s);At(s,6);Open(s);var w=s.DiagnosticSnapshot();var n=w.NewMilitary!.Events.Single(e=>e.Effect==MilitaryEffect.EngagementPermission);
        int i=Array.FindIndex(w.Messaging.Messages,m=>m.Id==n.Message);var m=w.Messaging.Messages[i];var b=ContractJson.Read<MilitaryNotice>(System.Text.Encoding.UTF8.GetBytes(m.Body));
        w.Messaging.Messages[i]=m with{Body=System.Text.Encoding.UTF8.GetString(ContractJson.Write(b with{Engagement=b.Engagement! with{Owner=b.Engagement.TargetOwner}}))};
        Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Fact]public void OldSerializedInputsOmitAllNewFields()
    {
        var input=new NewMilitaryInput(new("request:legacy-input"),NewMilitaryAction.Inquire);
        var text=System.Text.Encoding.UTF8.GetString(ContractJson.Write(input));Assert.DoesNotContain("Engagement",text);
        Assert.Equal(input,ContractJson.Read<NewMilitaryInput>(ContractJson.Write(input)));
    }
    [Fact]public void OpenedInquiryQuoteRecoversPermissionWithoutOpeningOriginal()
    {
        var s=Ready();Start(s);At(s,6);Send(s,NewMilitaryAction.Inquire);At(s,8);var w=s.DiagnosticSnapshot();
        var quote=w.NewMilitary!.Events.Last(e=>e.Effect==MilitaryEffect.StatusSent);Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(quote.Message),s.Revision).Code);
        Assert.Equal(8,Assert.Single(Terms(s)).ReceivedAt);Assert.Contains("開封",NewGameMilitary.EngagementKnownStatus(s.DiagnosticSnapshot()));
        var original=w.NewMilitary.Events.Single(e=>e.Effect==MilitaryEffect.EngagementPermission);Assert.DoesNotContain(s.DiagnosticSnapshot().NewMilitary!.Openings,o=>o.Message==original.Message);
        new WorldSession(s.DiagnosticSnapshot());
    }
    [Fact]public void OpenedInquiryQuoteAlsoRecoversCancellation()
    {
        var s=Ready();Start(s);At(s,6);Open(s);Cancel(s);At(s,8);Send(s,NewMilitaryAction.Inquire);At(s,10);var w=s.DiagnosticSnapshot();
        var quote=w.NewMilitary!.Events.Last(e=>e.Effect==MilitaryEffect.StatusSent);Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(quote.Message),s.Revision).Code);
        Assert.Equal(10,Assert.Single(Terms(s)).CancelledAt);Assert.Contains("取消",NewGameMilitary.EngagementKnownStatus(s.DiagnosticSnapshot()));new WorldSession(s.DiagnosticSnapshot());
    }
    [Fact]public void CancellationArrivingBeforeRetriedRequestPreventsAReissuedPermission()
    {
        var s=Ready();Start(s);string original=s.DiagnosticSnapshot().NewMilitary!.Requests.Last().Message!;
        s.DiagnosticTransport((m,t)=>HakutaiKagami.Core.Messaging.MessageFlow.Fail(m,original));At(s,6);Cancel(s);At(s,8);Open(s);
        Assert.Empty(Terms(s));
        s.DiagnosticTransport((m,t)=>HakutaiKagami.Core.Messaging.MessageFlow.Retry(m,original,s.PlayerControl.Value,t));At(s,10);Open(s);
        Assert.Empty(Terms(s));Assert.DoesNotContain(s.DiagnosticSnapshot().NewMilitary!.Events,e=>e.Effect==MilitaryEffect.EngagementPermission);
        Assert.Contains(s.DiagnosticSnapshot().NewMilitary!.Events,e=>e.Effect==MilitaryEffect.EngagementRejected);new WorldSession(s.DiagnosticSnapshot());
    }
}
