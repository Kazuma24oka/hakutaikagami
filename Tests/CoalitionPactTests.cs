using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class CoalitionPactTests
{
    [Fact]
    public void InvitesDifferentHomesWithoutRevealingAnswersAndRestoresDispatch()
    {
        var setup=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var houses=setup.Factions.Where(f=>setup.People.Any(p=>p.Id==f.Head && p.Site==f.Home)).Take(4).ToArray();
        Assert.Equal(4,houses.Length);
        var leader=houses[0];var members=houses.Skip(2).Select(f=>new EntityId(f.Id)).ToArray();
        var w=NewGameWorld.Create(setup,[],leader.Head,42,historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,
            worldArmyPolicy:NpcArmies.CompactCombatPolicy,trucePolicy:TruceNegotiations.Policy);
        var s=new WorldSession(w);
        var call=new CourtInput(new("request:coalition-one"),CourtAction.ProposeCoalition,s.PlayerControl,new(leader.Id),new(leader.Home)){
            Coalition=new(new(houses[1].Id),members,CoalitionPurpose.Both,2592000)};
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(call with{Coalition=call.Coalition with{Invitees=[members[0],members[0]]}},s.Revision).Code);
        var accepted=s.ManageCourt(call,s.Revision);Assert.True(accepted.Code==ResultCode.Accepted,accepted.VisibleReason);
        Assert.Equal(ResultCode.AlreadyAccepted,s.ManageCourt(call,s.Revision).Code);
        var state=s.DiagnosticSnapshot();
        var mail=state.Messaging.Messages.Where(m=>m.Kind==CoalitionPacts.Invitation).ToArray();Assert.Equal(2,mail.Length);
        foreach(var house in houses.Skip(2)){
            var letter=Assert.Single(mail,m=>m.RecipientId==house.Head);
            Assert.Equal(house.Home,letter.DeliverySite);
            Assert.Equal(NewGameWorld.Node(house.Home),state.Couriers!.Missions.Single(m=>state.Messaging.Attempts.Any(a=>a.Id==m.Attempt&&a.MessageId==letter.Id)).Destination);
            Assert.DoesNotContain(state.Messaging.Knowledge,k=>k.MessageId==letter.Id && k.CharacterId==houses[1].Head);
            var answer=new CourtInput(new("request:early-join"),CourtAction.JoinCoalition,new(leader.Head),new(house.Id),new(house.Home),Reference:call.Id);
            var copy=ContractJson.Copy(state);copy.CommitSequence++;
            Assert.Throws<InvalidDataException>(()=>HouseholdCourt.Execute(copy,answer,new(house.Head)));
        }
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(state)));
        Assert.Equal(ContractJson.Write(state),ContractJson.Write(restored.DiagnosticSnapshot()));
        Assert.DoesNotContain(state.Messaging.Messages,m=>m.Kind==CoalitionPacts.Reply);
    }
}
