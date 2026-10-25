using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class FactionStanceTests
{
    static readonly EntityId Takeda=new("org:takeda"),Oda=new("org:oda"),Shingen=new("person:shingen");
    static WorldSession Head()=>NewGameWorldTests.Session([],Shingen.Value);
    static FactionStanceInput Declare(long from=0,long until=20)=>new(new("request:hostility"),FactionStanceAction.DeclareHostile,Takeda,Oda,from,until);
    static void Accept(WorldSession s,FactionStanceInput q){var r=s.DecideFactionStance(q,s.PlayerControl,s.Revision);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);}
    static void At(WorldSession s,long at)=>NewGameWorldTests.At(s,at);
    static FactionStanceInput Withdraw(long at)=>new(new("request:withdraw-hostility"),FactionStanceAction.WithdrawOwnDeclaration,Takeda,Oda,at,0,new("request:hostility"));
    [Fact]public void LegacyWorldHasNoImplicitEnemyAndPreservesSerializedOmission()
    {
        var w=Head().DiagnosticSnapshot();Assert.Null(w.FactionStances);Assert.Empty(FactionStances.Truth(w,0));
        var bytes=ContractJson.Write(w);Assert.DoesNotContain("FactionStances",System.Text.Encoding.UTF8.GetString(bytes));Assert.Equal(bytes,ContractJson.Write(ContractJson.Read<ContractWorld>(bytes)));
    }
    [Fact]public void HeadDecisionCreatesDirectedTruthWithoutTroopsMoneyOrRemoteKnowledge()
    {
        var s=Head();var before=s.DiagnosticSnapshot();Accept(s,Declare());var w=s.DiagnosticSnapshot();
        var truth=Assert.Single(FactionStances.Truth(w,0));Assert.Equal(Takeda,truth.FromOwner);Assert.Equal(Oda,truth.ToOwner);
        Assert.Single(FactionStances.KnownOwnDeclarations(w,s.PlayerControl));Assert.Empty(FactionStances.KnownOwnDeclarations(w,new("person:nobunaga")));
        Assert.Equal(ContractJson.Write(before.Units),ContractJson.Write(w.Units));Assert.Equal(ContractJson.Write(before.ResourceLots),ContractJson.Write(w.ResourceLots));Assert.Equal(before.Messaging.Messages.Length,w.Messaging.Messages.Length);
    }
    [Fact]public void AnOrdinaryRetainerCannotDeclareWarForTheHouse()
    {
        var s=NewGameWorldTests.Session([NewGameWorldTests.Officer(site:"site:kofu",role:StartTemplate.Retainer,lord:Shingen.Value)]);var before=ContractJson.CanonicalHash(s.DiagnosticSnapshot());
        Assert.Equal(ResultCode.NotAvailable,s.DecideFactionStance(Declare(),s.PlayerControl,s.Revision).Code);Assert.Equal(before,ContractJson.CanonicalHash(s.DiagnosticSnapshot()));
    }
    [Fact]public void AnotherHeadCannotBeControlledThroughThePlayerCommand()
    {
        var s=Head();Assert.Equal(ResultCode.NotAvailable,s.DecideFactionStance(Declare(),new("person:nobunaga"),s.Revision).Code);Assert.Null(s.DiagnosticSnapshot().FactionStances);
    }
    [Fact]public void OwnDecisionCannotBorrowAnotherHousesAuthority()
    {
        var s=Head();Assert.Equal(ResultCode.NotAvailable,s.DecideFactionStance(Declare() with{Owner=Oda,Target=Takeda},s.PlayerControl,s.Revision).Code);
    }
    [Theory][InlineData("self")][InlineData("unknown")][InlineData("time")][InlineData("until")][InlineData("kind")]
    public void InvalidInputsAreAtomic(string kind)
    {
        var s=Head();var q=kind switch{"self"=>Declare() with{Target=Takeda},"unknown"=>Declare() with{Target=new("org:unknown")},"time"=>Declare() with{EffectiveAt=-1},"until"=>Declare() with{Until=0},_=>Declare() with{Action=(FactionStanceAction)999}};
        var before=ContractJson.CanonicalHash(s.DiagnosticSnapshot());Assert.Equal(ResultCode.NotAvailable,s.DecideFactionStance(q,s.PlayerControl,s.Revision).Code);Assert.Equal(before,ContractJson.CanonicalHash(s.DiagnosticSnapshot()));
    }
    [Fact]public void RepeatedIdIsIdempotentButChangedPayloadIsAConflict()
    {
        var s=Head();var q=Declare();Accept(s,q);var hash=ContractJson.CanonicalHash(s.DiagnosticSnapshot());Assert.Equal(ResultCode.AlreadyAccepted,s.DecideFactionStance(q,s.PlayerControl,0).Code);
        Assert.Equal(ResultCode.IdPayloadConflict,s.DecideFactionStance(q with{Until=30},s.PlayerControl,s.Revision).Code);Assert.Equal(hash,ContractJson.CanonicalHash(s.DiagnosticSnapshot()));
    }
    [Fact]public void StaleRevisionCannotMakeANewDecision(){var s=Head();s.SetPaused(false);Assert.Equal(ResultCode.StateChanged,s.DecideFactionStance(Declare(),s.PlayerControl,0).Code);}
    [Fact]public void EffectiveAndEndSecondsAreExactAndDoNotInformOtherPeople()
    {
        var s=Head();Accept(s,Declare(5,10));At(s,4);Assert.Empty(FactionStances.Truth(s.DiagnosticSnapshot(),4));At(s,5);Assert.Single(FactionStances.Truth(s.DiagnosticSnapshot(),5));At(s,10);Assert.Empty(FactionStances.Truth(s.DiagnosticSnapshot(),10));
        Assert.Empty(FactionStances.KnownOwnDeclarations(s.DiagnosticSnapshot(),new("person:ujiyasu")));
    }
    [Fact]public void WithdrawingOneDeclarationDoesNotMakeTheOtherSidePeaceful()
    {
        var s=Head();Accept(s,Declare());var w=s.DiagnosticSnapshot();w.CommitSequence++;
        FactionStances.Apply(w,new(new("request:opponent-hostility"),FactionStanceAction.DeclareHostile,Oda,Takeda,0,20),new("person:nobunaga"));s=new(w);Accept(s,Withdraw(0));
        var truth=Assert.Single(FactionStances.Truth(s.DiagnosticSnapshot(),0));Assert.Equal(Oda,truth.FromOwner);Assert.Equal(Takeda,truth.ToOwner);
    }
    [Fact]public void FutureDecisionCanBeWithdrawnBeforeItEverBecomesHostile()
    {
        var s=Head();Accept(s,Declare(10,20));Accept(s,Withdraw(5));At(s,12);Assert.Empty(FactionStances.Truth(s.DiagnosticSnapshot(),12));
    }
    [Fact]public void DuplicateOrOverlappingDeclarationsDoNotStack()
    {
        var s=Head();Accept(s,Declare());Assert.Equal(ResultCode.NotAvailable,s.DecideFactionStance(Declare() with{Id=new("request:duplicate-hostility")},s.PlayerControl,s.Revision).Code);Assert.Single(s.DiagnosticSnapshot().FactionStances!.Decisions);
    }
    [Fact]public void PoliticalDecisionAndTravelCannotReuseAnIdInEitherDirection()
    {
        var s=Head();Accept(s,Declare());var move=TravelWorld.Move(s.DiagnosticSnapshot(),Declare().Id.Value,"castle");Assert.Equal(ResultCode.IdPayloadConflict,s.Submit(move,s.Revision).Code);
        var other=Head();var first=TravelWorld.Move(other.DiagnosticSnapshot(),Declare().Id.Value,"castle");Assert.Equal(ResultCode.Accepted,other.Submit(first,other.Revision).Code);Assert.Equal(ResultCode.IdPayloadConflict,other.DecideFactionStance(Declare(),other.PlayerControl,other.Revision).Code);
    }
    [Fact]public void ForgedAuthorityInASaveIsRejected()
    {
        var s=Head();Accept(s,Declare());var w=s.DiagnosticSnapshot();var d=w.FactionStances!.Decisions[0];w.FactionStances.Decisions[0]=d with{Authority=d.Authority with{Office=new("office:forged")}};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Theory][InlineData(0)][InlineData(3)][InlineData(5)][InlineData(12)]
    public void GenerationRecoveryPreservesDecisionHistoryAndTheSameFuture(int at)
    {
        var s=Head();Accept(s,Declare(5,20));Accept(s,Withdraw(7));At(s,at);s.SetPaused(true);var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"faction-stance-"+Guid.NewGuid().ToString("N")),w.Definitions);
        Assert.True(store.Save(s,"case",w.WorldId,new(0,1)).Published);var host=new WorldSessionHost(Head());Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("case"),host.Current,host.Current.Revision));var copy=host.Current;
        Assert.Equal(ContractJson.CanonicalHash(w),ContractJson.CanonicalHash(copy.DiagnosticSnapshot()));foreach(var v in new[]{s,copy})At(v,25);
        Assert.Equal(ContractJson.CanonicalHash(s.DiagnosticSnapshot()),ContractJson.CanonicalHash(copy.DiagnosticSnapshot()));Assert.Empty(FactionStances.Truth(copy.DiagnosticSnapshot(),25));
    }
    [Fact]public void OriginalHeadKnowsOwnDecisionButSameHouseRetainerDoesNot()
    {
        var retainer=NewGameWorldTests.Officer(site:"site:kofu",role:StartTemplate.Retainer,lord:Shingen.Value);var setup=NewGameWorldTests.Setup([retainer]);var w=NewGameWorld.Create(setup.World,setup.Registered,retainer.Id);w.CommitSequence=1;
        FactionStances.Apply(w,Declare(),Shingen);var s=new WorldSession(w);Assert.Single(FactionStances.Truth(w,0));Assert.Empty(FactionStances.KnownOwnDeclarations(w,s.PlayerControl));Assert.Single(FactionStances.KnownOwnDeclarations(w,Shingen));
    }
    [Fact]public void TruthAndClaimQueriesCannotLookIntoTheUnsimulatedFuture()
    {
        var s=Head();Accept(s,Declare(5,20));var w=s.DiagnosticSnapshot();Assert.Throws<InvalidDataException>(()=>FactionStances.Truth(w,5));Assert.Throws<InvalidDataException>(()=>FactionStances.ClaimAt(w,Takeda,Oda,5,w.CommitSequence));
    }
}
