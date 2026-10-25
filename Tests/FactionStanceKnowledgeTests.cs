using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class FactionStanceKnowledgeTests
{
    static readonly EntityId Head=new("person:shingen"),Owner=new("org:takeda"),Target=new("org:oda");
    static WorldSession Ready()
    {
        var p=NewGameWorldTests.Officer(site:"site:kofu",role:StartTemplate.Retainer,lord:Head.Value);var setup=NewGameWorldTests.Setup([p]);var w=NewGameWorld.Create(setup.World,setup.Registered,p.Id);w.CommitSequence=1;
        FactionStances.Apply(w,new(new("request:npc-head-hostility"),FactionStanceAction.DeclareHostile,Owner,Target,0,40),Head);
        var s=new WorldSession(w);Send(s,new(new("request:stance-loan"),NewMilitaryAction.Request){AuthorityPolicy=NewGameMilitary.AuthorityContract});At(s,2);Open(s);Send(s,new(new("request:stance-accept"),NewMilitaryAction.Accept));At(s,4);Open(s);Send(s,new(new("request:stance-pickup"),NewMilitaryAction.Pickup));
        Send(s,new(new("request:stance-permission"),NewMilitaryAction.RequestEngagement){Engagement=new(NewGameMilitary.EngagementTaskContract,Target,4,30)});return s;
    }
    static void Send(WorldSession s,NewMilitaryInput q){var r=s.DecideNewMilitary(q,s.PlayerControl,s.Revision);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);}
    static void At(WorldSession s,long at)=>NewGameWorldTests.At(s,at);
    static void Open(WorldSession s){foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,m.Id,s.Revision).Code);}
    [Fact]public void SameHouseAndDeliveredButUnopenedLetterDoNotRevealHostility()
    {
        var s=Ready();Assert.Empty(FactionStances.Known(s.DiagnosticSnapshot(),s.PlayerControl));At(s,6);Assert.Empty(FactionStances.Known(s.DiagnosticSnapshot(),s.PlayerControl));Open(s);
        var claim=Assert.Single(FactionStances.Known(s.DiagnosticSnapshot(),s.PlayerControl));Assert.Equal(5,claim.ObservedAt);Assert.Equal(Owner,claim.FromOwner);Assert.Equal(Target,claim.ToOwner);
        Assert.Empty(FactionStances.Known(s.DiagnosticSnapshot(),new("person:ujiyasu")));
    }
    [Fact]public void SecretWithdrawalDoesNotRefreshAnOlderReceivedClaim()
    {
        var s=Ready();At(s,6);Open(s);var old=Assert.Single(FactionStances.Known(s.DiagnosticSnapshot(),s.PlayerControl));var w=s.DiagnosticSnapshot();w.CommitSequence++;
        FactionStances.Apply(w,new(new("request:npc-standdown"),FactionStanceAction.WithdrawOwnDeclaration,Owner,Target,6,0,new("request:npc-head-hostility")),Head);s=new(w);At(s,8);w=s.DiagnosticSnapshot();
        Assert.Empty(FactionStances.Truth(w,8));Assert.Equal(old,Assert.Single(FactionStances.Known(w,s.PlayerControl)));Assert.Equal(40,old.Until);
    }
    [Fact]public void OpenedInquiryQuotationCarriesTheOriginalObservationTime()
    {
        var s=Ready();At(s,6);Send(s,new(new("request:stance-inquiry"),NewMilitaryAction.Inquire));At(s,8);var w=s.DiagnosticSnapshot();var quote=w.NewMilitary!.Events.Last(e=>e.Effect==MilitaryEffect.StatusSent);
        Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(quote.Message),s.Revision).Code);var c=Assert.Single(FactionStances.Known(s.DiagnosticSnapshot(),s.PlayerControl));Assert.Equal(5,c.ObservedAt);new WorldSession(s.DiagnosticSnapshot());
    }
    [Fact]public void SavedQuotationCannotInventAnotherHostilityClaim()
    {
        var s=Ready();At(s,6);Open(s);var w=s.DiagnosticSnapshot();var e=w.NewMilitary!.Events.Single(e=>e.Effect==MilitaryEffect.EngagementPermission);int i=Array.FindIndex(w.Messaging.Messages,m=>m.Id==e.Message);var m=w.Messaging.Messages[i];var n=ContractJson.Read<MilitaryNotice>(System.Text.Encoding.UTF8.GetBytes(m.Body));
        var changed=n with{Engagement=n.Engagement! with{Hostility=n.Engagement.Hostility! with{ToOwner=new("org:hojo")}}};w.Messaging.Messages[i]=m with{Body=System.Text.Encoding.UTF8.GetString(ContractJson.Write(changed))};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Theory][InlineData(4)][InlineData(5)][InlineData(6)]public void RecoveryKeepsTruthAndUnopenedKnowledgeSeparate(int at)
    {
        var s=Ready();At(s,at);s.SetPaused(true);var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"stance-known-"+Guid.NewGuid().ToString("N")),w.Definitions);Assert.True(store.Save(s,"case",w.WorldId,new(0,1)).Published);
        var host=new WorldSessionHost(Ready());Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("case"),host.Current,host.Current.Revision));var copy=host.Current;
        foreach(var v in new[]{s,copy}){At(v,8);Open(v);}
        Assert.Equal(ContractJson.CanonicalHash(s.DiagnosticSnapshot()),ContractJson.CanonicalHash(copy.DiagnosticSnapshot()));Assert.Equal(ContractJson.Write(FactionStances.Known(s.DiagnosticSnapshot(),s.PlayerControl)),ContractJson.Write(FactionStances.Known(copy.DiagnosticSnapshot(),copy.PlayerControl)));
    }
    [Fact]public void ActualAccepted613d135SaveLoadsAndRoundTripsWithoutNewFields()
    {
        var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","engagement-613d135-accepted.json"));
        Assert.Equal("36aea0003d9bac21798c655a8f25634c8fad11e73b7de6b96160dad16ca149b0",ContractJson.Hash(bytes));
        var w=ContractJson.Read<ContractWorld>(bytes);Assert.Null(w.FactionStances);var session=new WorldSession(w);
        Assert.Equal("a8c2de8363e262ae45ac6a4f5db7078bd9bee9cbfbe54861094737e9ce6c5dc8",ContractJson.CanonicalHash(session.DiagnosticSnapshot()));
        Assert.Equal(bytes,ContractJson.Write(session.DiagnosticSnapshot()));Assert.Empty(FactionStances.Known(session.DiagnosticSnapshot(),session.PlayerControl));
    }
    [Fact]public void PureContactGateUsesWorldTruthAndPersonallyReceivedTerms()
    {
        var s=Ready();At(s,6);Open(s);var w=s.DiagnosticSnapshot();var own=w.Units[0];
        var opponent=new UnitState(new("unit:test-adversary"),new("army:test-adversary"),new("person:nobunaga"),Target,30,0);
        EngagementUnit[] contact=[new(own,new(0,0),0,100,"standing",EngagementIntent.Mission,true),new(opponent,new(3000,0),0,100,"standing",EngagementIntent.SelfDefense,true)];
        string before=ContractJson.CanonicalHash(w);var result=FieldEngagement.Resolve(6,contact,NewGameMilitary.EngagementMandates(w,6),FactionStances.Truth(w,6));
        Assert.Equal(2,result.Losses.Length);Assert.Contains(result.Strikes,x=>x.Basis==EngagementBasis.Mission);Assert.Equal(before,ContractJson.CanonicalHash(w));
    }
}
