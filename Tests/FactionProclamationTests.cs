using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class FactionProclamationTests
{
    static readonly EntityId Own=new("org:paper-test"),Oda=new("org:oda"),Decision=new("decision:proclaim"),Notice=new("request:proclaim");
    static FactionProclamationInput Input()=>new(new(Decision,FactionStanceAction.DeclareHostile,Own,Oda,0,1000000),new(Notice,Decision));
    [Fact]public void OwnDeclarationAndDatedDispatchCommitTogetherWithoutInformingOpponent()
    {
        var s=FactionNoticeTests.Start(declare:false);Assert.Equal(ResultCode.Accepted,s.ProclaimFactionStance(Input(),s.PlayerControl,s.Revision).Code);var w=s.DiagnosticSnapshot();var d=w.FactionStances!.Decisions.Single();var q=w.FactionNotices!.Requests.Single();Assert.Equal(d.Revision,q.Revision);Assert.Equal(d.At,q.At);Assert.Equal(d.Input.Id,q.Input.Original);Assert.Empty(FactionStances.Known(w,new("person:nobunaga")));
    }
    [Fact]public void RepeatedCompositeCommandIsIdempotentAndChangedPayloadConflicts()
    {
        var s=FactionNoticeTests.Start(declare:false);var q=Input();Assert.Equal(ResultCode.Accepted,s.ProclaimFactionStance(q,s.PlayerControl,s.Revision).Code);var before=ContractJson.Write(s.DiagnosticSnapshot());Assert.Equal(ResultCode.AlreadyAccepted,s.ProclaimFactionStance(q,s.PlayerControl,0).Code);Assert.Equal(ResultCode.IdPayloadConflict,s.ProclaimFactionStance(q with{Decision=q.Decision with{Until=2000000}},s.PlayerControl,s.Revision).Code);Assert.Equal(before,ContractJson.Write(s.DiagnosticSnapshot()));
    }
    [Fact]public void InvalidNoticeOriginalCannotLeaveADeclarationWithoutItsPaper()
    {
        var s=FactionNoticeTests.Start(declare:false);var before=ContractJson.Write(s.DiagnosticSnapshot());var q=Input() with{Notice=new(Notice,new("decision:other"))};Assert.Equal(ResultCode.NotAvailable,s.ProclaimFactionStance(q,s.PlayerControl,s.Revision).Code);Assert.Equal(before,ContractJson.Write(s.DiagnosticSnapshot()));
    }
    [Fact]public void InvalidOwnAuthorityOrPeriodCannotLeaveAnyPartialPoliticalState()
    {
        foreach(var q in new[]{Input() with{Decision=Input().Decision with{Owner=Oda,Target=Own}},Input() with{Decision=Input().Decision with{Until=0}}}){var s=FactionNoticeTests.Start(declare:false);var before=ContractJson.Write(s.DiagnosticSnapshot());Assert.Equal(ResultCode.NotAvailable,s.ProclaimFactionStance(q,s.PlayerControl,s.Revision).Code);Assert.Equal(before,ContractJson.Write(s.DiagnosticSnapshot()));}
    }
    [Fact]public void OwnWithdrawalAndUpdatedDatedNoticeShareOneCommit()
    {
        var s=FactionNoticeTests.Start(declare:false);Assert.Equal(ResultCode.Accepted,s.ProclaimFactionStance(Input(),s.PlayerControl,s.Revision).Code);NewGameWorldTests.At(s,1);var q=new FactionProclamationInput(new(new("decision:proclaim-withdraw"),FactionStanceAction.WithdrawOwnDeclaration,Own,Oda,s.Seconds,0,Decision),new(new("request:proclaim-withdraw"),Decision));Assert.Equal(ResultCode.Accepted,s.ProclaimFactionStance(q,s.PlayerControl,s.Revision).Code);var w=s.DiagnosticSnapshot();Assert.Empty(FactionStances.Truth(w,s.Seconds));var d=w.FactionStances!.Decisions.Last();var notice=w.FactionNotices!.Requests.Last();Assert.Equal(d.Revision,notice.Revision);Assert.Equal(1,notice.Claim.Until);Assert.Equal(Decision,notice.Claim.Declaration);new WorldSession(w);
    }
}
