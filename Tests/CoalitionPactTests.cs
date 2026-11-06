using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class CoalitionPactTests
{
    [Fact]
    public void InvitesDifferentHomesWithoutRevealingAnswersAndRestoresDispatch()
    {
        var original=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var raider=NewGameWorldTests.Officer("created:coalition-raider",StartTemplate.Daimyo,"site:ishiyama");
        var defender=NewGameWorldTests.Officer("created:coalition-target",StartTemplate.Daimyo,"site:sakai");
        var founder=NewGameWorldTests.Officer("created:coalition-founder-139",StartTemplate.Daimyo,"site:castle") with{BirthYear=1465};
        var witness=NewGameWorldTests.Officer("created:coalition-witness",StartTemplate.Retainer,founder.Site,founder.Id);
        var registered=NewGameWorld.Normalize([raider,defender,founder,witness]);
        var selection=new WorldSetupRequest(registered,[new(raider.Id,"org:coalition-raider",[raider.Site],false),
            new(defender.Id,"org:coalition-target",[defender.Site],false),new(founder.Id,"org:coalition-founder",[founder.Site],false)]);
        var preview=NewGameSetup.Preview(original,selection);
        Assert.True(preview.IsEligible(),string.Join(",",preview.Errors));
        var setup=NewGameSetup.Confirm(original,selection,preview);
        var founders=setup.Factions.Where(f=>setup.People.Any(p=>p.Id==f.Head && p.Site==f.Home) &&
            f.Head!=raider.Id && f.Head!=defender.Id && f.Head!=founder.Id).Take(1).ToArray();
        var houses=new[]{setup.Factions.Single(f=>f.Head==founder.Id),setup.Factions.Single(f=>f.Head==defender.Id),setup.Factions.Single(f=>f.Head==raider.Id),founders[0]};
        var leader=houses[0];var members=houses.Skip(2).Select(f=>new EntityId(f.Id)).ToArray();
        var w=NewGameWorld.Create(setup,registered,leader.Head,42,historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,
            worldArmyPolicy:NpcArmies.CompactCombatPolicy,trucePolicy:TruceNegotiations.Policy,comingOfAge:FamilyKin.ComingOfAge);
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
        // 加盟の返書とは独立して、自分に届いた招請の内容だけを別の世界で密告する。
        var tipped=ContractJson.Copy(invited);
        var informant=new EntityId(houses[2].Head);var target=new EntityId(houses[1].Head);
        var warning=Assert.Single(CoalitionPacts.Warnings(tipped,informant));
        tipped.CommitSequence++;HouseholdCourt.Execute(tipped,warning,informant);
        Assert.Empty(CoalitionPacts.Warnings(tipped,informant));
        var secret=Assert.Single(tipped.Messaging.Messages,m=>m.Kind==CoalitionPacts.Warning);
        Assert.Equal(target.Value,secret.RecipientId);
        Assert.DoesNotContain(houses[3].Name,secret.Body);
        Assert.Empty(CoalitionPacts.DeliveredPapers(tipped,target));
        var confidential=new WorldSession(ContractJson.Copy(tipped));
        NewGameWorldTests.At(confidential,Delivery(tipped,CoalitionPacts.Warning)+2);
        var learned=confidential.DiagnosticSnapshot();
        Assert.Contains(CoalitionPacts.DeliveredPapers(learned,target),m=>m.Id==secret.Id && m.Body==secret.Body);
        Assert.DoesNotContain(CoalitionPacts.DeliveredPapers(learned,new(houses[3].Head)),m=>m.Id==secret.Id);
        Assert.Empty(CoalitionPacts.Known(learned,target));
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
            var bereaved=new WorldSession(ContractJson.Copy(end));
            NewGameWorldTests.At(bereaved,36L*86400+43200);
            var funeral=bereaved.DiagnosticSnapshot();
            var death=Assert.Single(funeral.NaturalDeaths!,d=>d.Person==bereaved.PlayerControl);
            Assert.True(FamilyNotice.Knows(funeral,death,new(witness.Id)));
            var signer=new EntityId(raider.Id);
            Assert.False(Assert.Single(CoalitionPacts.Known(funeral,signer)).FounderDeathKnown);
            var obituary=Assert.Single(funeral.Messaging.Messages,m=>m.Kind==FamilyNotice.Kind && m.RecipientId==raider.Id);
            Assert.DoesNotContain(funeral.Messaging.Messages,m=>m.Kind==FamilyNotice.Kind && m.RecipientId==defender.Id);
            File.WriteAllBytes(path,ContractJson.Write(funeral));
            var courier=new WorldSession(ContractJson.Read<ContractWorld>(File.ReadAllBytes(path)));
            NewGameWorldTests.At(courier,Delivery(funeral,FamilyNotice.Kind)+1);
            var informed=courier.DiagnosticSnapshot();
            Assert.Contains(CoalitionPacts.DeliveredPapers(informed,signer),paper=>paper.Id==obituary.Id && paper.Body==obituary.Body);
            Assert.True(Assert.Single(CoalitionPacts.Known(informed,signer)).FounderDeathKnown);
            Assert.Empty(CoalitionPacts.Withdrawals(informed,signer));
            var campaign=ContractJson.Copy(end);
            campaign.CommitSequence++;
            var attacker=new EntityId(houses[2].Head);
            FactionStances.Apply(campaign,new(new("decision:coalition-campaign"),FactionStanceAction.DeclareHostile,
                members[0],new(houses[1].Id),campaign.ElapsedSeconds,campaign.ElapsedSeconds+1000000),attacker);
            var military=new WorldSession(campaign);
            NewGameWorldTests.At(military,military.Seconds+5);
            var marching=military.DiagnosticSnapshot();
            var muster=Assert.Single(marching.NpcArmies!.Musters,m=>m.Commander==attacker);
            var outbound=Assert.Single(marching.NpcArmies.Marches,m=>m.Muster==muster.Id);
            var order=outbound.Input;
            Assert.Equal(new EntityId(houses[1].Home),order.DestinationSite);
            var task=Assert.Single(marching.Tasks,t=>t.RequestId==order.RequestId);
            Assert.NotNull(task.StartedAt);
            File.WriteAllBytes(path,ContractJson.Write(marching));
            var resumedMarch=new WorldSession(ContractJson.Read<ContractWorld>(File.ReadAllBytes(path)));
            NewGameWorldTests.At(resumedMarch,military.Seconds+2L*order.WorkSeconds+120);
            var returned=resumedMarch.DiagnosticSnapshot();
            var dispatch=Assert.Single(returned.Court!.Events,e=>e.Input.Action==CourtAction.ReportCoalitionSortie);
            Assert.Equal(order.RequestId,dispatch.Input.ActivityEvidence);
            var letterHome=Assert.Single(returned.Messaging.Messages,m=>m.Kind==CoalitionPacts.ActivityReport);
            Assert.Equal(leader.Head,letterHome.RecipientId);
            Assert.Contains(task.StartedAt.Value.ToString(),letterHome.Body);
            NewGameWorldTests.At(resumedMarch,Delivery(returned,CoalitionPacts.ActivityReport)+1);
            Assert.Equal(ResultCode.Accepted,resumedMarch.Open(resumedMarch.PlayerControl,new(letterHome.Id),resumedMarch.Revision).Code);
            var separate=new WorldSession(ContractJson.Copy(end));
            var proposal=new TruceInput(new("request:separate-peace"),TruceAction.Propose,new(leader.Id),new(houses[1].Id),Days:30);
            var offered=separate.NegotiateTruce(proposal,separate.PlayerControl,separate.Revision);
            Assert.True(offered.Code==ResultCode.Accepted,offered.VisibleReason);
            Assert.Empty(CoalitionPacts.ActivityReports(separate.DiagnosticSnapshot(),separate.PlayerControl));
            NewGameWorldTests.At(separate,Delivery(separate.DiagnosticSnapshot(),TruceNegotiations.Kind)+1);
            var bilateral=separate.DiagnosticSnapshot();
            var acceptance=Assert.Single(bilateral.Truces!.Papers,p=>p.Input.Action==TruceAction.Accept && p.Input.Source==proposal.Id);
            NewGameWorldTests.At(separate,Delivery(bilateral,TruceNegotiations.Kind)+1);
            Assert.Empty(CoalitionPacts.ActivityReports(separate.DiagnosticSnapshot(),separate.PlayerControl));
            Assert.Equal(ResultCode.Accepted,separate.Open(separate.PlayerControl,new(acceptance.Message),separate.Revision).Code);
            var report=Assert.Single(CoalitionPacts.ActivityReports(separate.DiagnosticSnapshot(),separate.PlayerControl));
            Assert.Equal(CourtAction.ReportSeparatePeace,report.Action);
            Assert.Equal(acceptance.Input.Id,report.ActivityEvidence);
            var reported=separate.ManageCourt(report,separate.Revision);Assert.True(reported.Code==ResultCode.Accepted,reported.VisibleReason);
            File.WriteAllBytes(path,ContractJson.Write(separate.DiagnosticSnapshot()));
            var savedReport=new WorldSession(ContractJson.Read<ContractWorld>(File.ReadAllBytes(path))).DiagnosticSnapshot();
            Assert.Equal(2,savedReport.Messaging.Messages.Count(m=>m.Kind==CoalitionPacts.ActivityReport));
            Assert.Empty(CoalitionPacts.ActivityReports(savedReport,separate.PlayerControl));
            var commander=new EntityId(houses[2].Head);
            Assert.Empty(CoalitionPacts.ActivityReports(end,commander));
            foreach(var action in new[]{CourtAction.ReportCoalitionSortie,CourtAction.ReportSeparatePeace}){
                var invented=new CourtInput(new("request:unproven-activity"),action,s.PlayerControl,members[0],new(houses[2].Home),Reference:call.Id){ActivityEvidence=signature.Id};
                var copy=ContractJson.Copy(end);copy.CommitSequence++;
                Assert.Throws<InvalidDataException>(()=>HouseholdCourt.Execute(copy,invented,commander));
                Assert.DoesNotContain(copy.Messaging.Messages,m=>m.Kind==CoalitionPacts.ActivityReport);
            }
            var departing=new EntityId(houses[2].Head);
            var withdrawal=Assert.Single(CoalitionPacts.Withdrawals(end,departing));
            end.CommitSequence++;HouseholdCourt.Execute(end,withdrawal,departing);
            Assert.DoesNotContain(members[0],Assert.Single(CoalitionPacts.Known(end,departing)).Houses);
            Assert.Contains(members[0],Assert.Single(CoalitionPacts.Known(end,s.PlayerControl)).Houses);
            Assert.Empty(CoalitionPacts.Withdrawals(end,departing));
            var notices=end.Messaging.Messages.Where(m=>m.Kind==CoalitionPacts.Withdrawal).ToArray();
            Assert.Equal(2,notices.Length);Assert.DoesNotContain(notices,m=>m.RecipientId==houses[1].Head);
            File.WriteAllBytes(path,ContractJson.Write(end));
            var afterDeparture=new WorldSession(ContractJson.Read<ContractWorld>(File.ReadAllBytes(path)));
            NewGameWorldTests.At(afterDeparture,Delivery(end,CoalitionPacts.Withdrawal)+1);
            var sealedState=afterDeparture.DiagnosticSnapshot();
            Assert.Contains(members[0],Assert.Single(CoalitionPacts.Known(sealedState,s.PlayerControl)).Houses);
            var myNotice=Assert.Single(notices,m=>m.RecipientId==s.PlayerControl.Value);
            Assert.Equal(ResultCode.Accepted,afterDeparture.Open(s.PlayerControl,new(myNotice.Id),afterDeparture.Revision).Code);
            Assert.DoesNotContain(members[0],Assert.Single(CoalitionPacts.Known(afterDeparture.DiagnosticSnapshot(),s.PlayerControl)).Houses);
        }finally{File.Delete(path);}
    }
}
