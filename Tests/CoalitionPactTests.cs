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
        long Delivery(ContractWorld snapshot,string kind)=>snapshot.Couriers!.Missions.Where(m=>snapshot.Messaging.Attempts.Any(a=>a.Id==m.Attempt &&
            snapshot.Messaging.Messages.Any(letter=>letter.Id==a.MessageId && letter.Kind==kind))).Max(m=>m.ReadyAt);
        NewGameWorldTests.At(restored,Delivery(state,CoalitionPacts.Invitation)+2);
        var invited=restored.DiagnosticSnapshot();
        foreach(var member in houses.Skip(2)){
            var actor=new EntityId(member.Head);
            var answer=Assert.Single(CoalitionPacts.Invitations(invited,actor),row=>row.Answer?.Action==CourtAction.JoinCoalition).Answer!;
            invited.CommitSequence++;
            HouseholdCourt.Execute(invited,answer,actor);
        }
        var waiting=new WorldSession(ContractJson.Copy(invited));
        Assert.Empty(CoalitionPacts.Confirmations(invited,waiting.PlayerControl));
        NewGameWorldTests.At(waiting,Delivery(invited,CoalitionPacts.Reply)+1);
        var replies=CoalitionPacts.DeliveredPapers(waiting.DiagnosticSnapshot(),waiting.PlayerControl).ToArray();
        Assert.Equal(2,replies.Length);
        foreach(var reply in replies){
            Assert.Null(reply.Body);
            Assert.Empty(CoalitionPacts.Confirmations(waiting.DiagnosticSnapshot(),waiting.PlayerControl));
            Assert.Equal(ResultCode.Accepted,waiting.Open(waiting.PlayerControl,new(reply.Id),waiting.Revision).Code);
        }
        var signature=Assert.Single(CoalitionPacts.Confirmations(waiting.DiagnosticSnapshot(),waiting.PlayerControl));
        var signed=waiting.ManageCourt(signature,waiting.Revision);Assert.True(signed.Code==ResultCode.Accepted,signed.VisibleReason);
        var pact=waiting.DiagnosticSnapshot();
        Assert.Equal(2,pact.Messaging.Messages.Count(m=>m.Kind==CoalitionPacts.Certificate));
        Assert.Empty(CoalitionPacts.DeliveredPapers(pact,new(houses[1].Head)));
        Assert.Empty(CoalitionPacts.Confirmations(pact,waiting.PlayerControl));
        Assert.Equal(3,Assert.Single(CoalitionPacts.Known(pact,waiting.PlayerControl)).Houses.Length);
        foreach(var member in houses.Skip(2))Assert.Empty(CoalitionPacts.Known(pact,new(member.Head)));
        var path=Path.GetTempFileName();
        try{
            File.WriteAllBytes(path,ContractJson.Write(pact));
            var replay=new WorldSession(ContractJson.Read<ContractWorld>(File.ReadAllBytes(path)));
            foreach(var run in new[]{waiting,replay})NewGameWorldTests.At(run,Delivery(pact,CoalitionPacts.Certificate)+1);
            Assert.Equal(ContractJson.Write(waiting.DiagnosticSnapshot()),ContractJson.Write(replay.DiagnosticSnapshot()));
            var end=replay.DiagnosticSnapshot();
            foreach(var member in houses.Skip(2)){
                Assert.Single(CoalitionPacts.DeliveredPapers(end,new(member.Head)));
                Assert.Equal(3,Assert.Single(CoalitionPacts.Known(end,new(member.Head))).Houses.Length);
            }
            Assert.Empty(CoalitionPacts.Known(end,new(houses[1].Head)));
        }finally{File.Delete(path);}
    }
}
