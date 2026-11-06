using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class AssassinationTests
{
    [Fact] public void LastSightingSurvivesDepartureButCannotAuthorizeRemoteContact()
    {
        EntityId target=new("created:contact"), home=new("site:kyoto");
        var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer(site:home.Value),NewGameWorldTests.Officer(target.Value,site:home.Value)]);
        var session=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,"created:test",42,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy,
            economyPolicy:HouseSuccession.Policy,territoryPolicy:CastleSiege.Policy));
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        var sight=new SchemeInput(new("request:saw-person"),SchemeAction.ObservePerson,session.PlayerControl,target);
        var contact=new SchemeInput(new("request:seek-person"),SchemeAction.SeekContact,session.PlayerControl,target,Reference:sight.Id);
        Assert.Equal(ResultCode.NotAvailable,session.ManageScheme(contact,session.Revision).Code);
        Good(session.ManageScheme(sight,session.Revision));
        Good(session.ManageScheme(contact,session.Revision));
        Good(session.ManageCompanion(new(new("request:hire"),target,CompanionAction.Agree,20),session.PlayerControl,session.Revision));
        var leave=new SchemeInput(new("request:travel"),SchemeAction.SendScout,target,session.PlayerControl,Destination:new("site:akutagawa"));
        Good(session.ManageScheme(leave,session.Revision));
        var trip=session.DiagnosticSnapshot().Schemes!.Events.Single(e=>e.Input.Id==leave.Id).Movement!;
        NewGameWorldTests.At(session,trip.EffectiveAt+trip.DeliverySeconds+trip.WorkSeconds);
        var w=session.DiagnosticSnapshot();
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        foreach(var branch in new[]{session,restored})
        {
            var known=Assert.Single(Assassination.Sightings(branch.DiagnosticSnapshot(),branch.PlayerControl));
            Assert.Equal(home,known.Site);
            Assert.Equal(0,known.ObservedAt);
            Assert.Equal(ResultCode.NotAvailable,branch.ManageScheme(contact with{Id=new("request:stale-contact")},branch.Revision).Code);
        }
    }
    [Fact] public void FatalEncounterSurvivesLoadAndTargetCannotBeKilledTwice()
    {
        EntityId target=new("created:contact"),site=new("site:kyoto");
        var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer(site:site.Value),NewGameWorldTests.Officer(target.Value,site:site.Value)]);
        var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,"created:test",0,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy,territoryPolicy:CastleSiege.Policy));
        var sight=new SchemeInput(new("request:sighting"),SchemeAction.ObservePerson,s.PlayerControl,target);
        var contact=sight with { Id=new("request:contact"),Action=SchemeAction.SeekContact,Reference=sight.Id };
        var attack=contact with { Id=new("request:attack"),Action=SchemeAction.AttemptAssassination,Reference=contact.Id };
        foreach(var i in new[]{sight,contact,attack})
        {
            var result=s.ManageScheme(i,s.Revision);
            Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        }
        var w=s.DiagnosticSnapshot();
        Assert.Equal(LifeState.Dead,w.Characters.Single(c=>c.Id==target).Life);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.Equal(ResultCode.AlreadyAccepted,restored.ManageScheme(attack,restored.Revision).Code);
        Assert.Equal(ResultCode.NotAvailable,restored.ManageScheme(attack with{Id=new("request:again")},restored.Revision).Code);
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
    [Fact] public void CapturedAttackerCannotLeaveUntilReleasedByActualCustodian()
    {
        EntityId actor=new("created:test"),target=new("created:contact"),site=new("site:kyoto");
        var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer(site:site.Value),NewGameWorldTests.Officer(target.Value,site:site.Value)]);
        var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,actor.Value,1,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy,territoryPolicy:CastleSiege.Policy));
        var sight=new SchemeInput(new("request:sight"),SchemeAction.ObservePerson,actor,target);
        var contact=sight with{Id=new("request:contact"),Action=SchemeAction.SeekContact,Reference=sight.Id};
        var attempt=contact with{Id=new("request:attack"),Action=SchemeAction.AttemptAssassination,Reference=contact.Id};
        foreach(var input in new[]{sight,contact,attempt}) Assert.Equal(ResultCode.Accepted,s.ManageScheme(input,s.Revision).Code);
        var w=s.DiagnosticSnapshot();
        var capture=Assert.Single(w.Schemes!.Events,e=>e.Input.Action==SchemeAction.CaughtInEncounter);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.Equal(ResultCode.NotAvailable,restored.Submit(TravelWorld.Move(w,"request:escape","akutagawa"),restored.Revision).Code);
        w.CommitSequence++;
        var release=new SchemeInput(new("request:release"),SchemeAction.ReleaseCaptive,target,actor,Reference:capture.Input.Id);
        Assert.Throws<InvalidDataException>(()=>InformationSchemes.Execute(w,release,actor));
        InformationSchemes.Execute(w,release,target);
        var free=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.Null(Assassination.CaptivityAt(w,actor,w.ElapsedSeconds,w.CommitSequence+1));
        var moved=free.Submit(TravelWorld.Move(w,"request:travel","akutagawa"),free.Revision);
        Assert.True(moved.Code==ResultCode.Accepted,moved.VisibleReason);
    }
    [Theory][InlineData(0,false)][InlineData(28,true)] public void ScoutLocationIsKnownOnlyAfterOpeningReportAndKeepsObservationTime(int seed,bool captured)
    {
        EntityId agent=new("created:agent"),target=new("created:target"),dest=new("site:akutagawa");
        var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer(site:"site:kyoto"),NewGameWorldTests.Officer(agent.Value,site:"site:kyoto"),NewGameWorldTests.Officer(target.Value,site:dest.Value)]);
        var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,"created:test",(ulong)seed,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy,economyPolicy:HouseSuccession.Policy,territoryPolicy:CastleSiege.Policy));
        void Good(CommandResult result)=>Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        Good(s.ManageCompanion(new(new("request:hire"),agent,CompanionAction.Agree,20),s.PlayerControl,s.Revision));
        var input=new SchemeInput(new("request:recon"),SchemeAction.SendScout,agent,s.PlayerControl,Destination:dest);
        Good(s.ManageScheme(input,s.Revision));
        var outward=s.DiagnosticSnapshot().Schemes!.Events.Single(e=>e.Input.Id==input.Id).Movement!;
        NewGameWorldTests.At(s,outward.EffectiveAt+outward.DeliverySeconds+outward.WorkSeconds);
        var w=s.DiagnosticSnapshot();
        var personal=Assert.Single(Assassination.Sightings(w,agent),e=>e.Person==target);
        Assert.Empty(Assassination.Sightings(w,s.PlayerControl));
        var back=w.Schemes!.Events.Single(e=>e.Input.Action==SchemeAction.PlanReturn).Movement!;
        NewGameWorldTests.At(s,back.EffectiveAt+back.DeliverySeconds+back.WorkSeconds+2);
        Assert.Empty(Assassination.Sightings(s.DiagnosticSnapshot(),s.PlayerControl));
        Good(s.Open(s.PlayerControl,new(InformationSchemes.ReportId(input.Id)),s.Revision));
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(s.DiagnosticSnapshot())));
        Assert.Equal(personal,Assert.Single(Assassination.Sightings(restored.DiagnosticSnapshot(),s.PlayerControl),e=>e.Person==target));
        var assignment=new SchemeInput(new("request:assassin"),SchemeAction.SendScout,agent,target,SchemeTopic.Assassination,personal.Original,dest);
        Good(restored.ManageScheme(assignment,restored.Revision));
        var departure=restored.DiagnosticSnapshot().Schemes!.Events.Single(e=>e.Input.Id==assignment.Id).Movement!;
        NewGameWorldTests.At(restored,departure.EffectiveAt+departure.DeliverySeconds+departure.WorkSeconds);
        var arrived=restored.DiagnosticSnapshot();
        var result=Assert.Single(arrived.Schemes!.Events,e=>Assassination.IsConclusion(e.Input.Action));
        Assert.Equal(captured,result.Input.Action==SchemeAction.CaughtInEncounter);
        var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"assassin-"+Guid.NewGuid().ToString("N")),arrived.Definitions);
        restored.SetPaused(true);Assert.True(store.Save(restored,"mission",arrived.WorldId).Published);
        var host=new WorldSessionHost(new WorldSession(arrived));
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("mission"),host.Current,host.Current.Revision));
        foreach(var branch in new[]{restored,host.Current})
        {
            var message=new EntityId(InformationSchemes.ReportId(assignment.Id));
            if(captured)
            {
                NewGameWorldTests.At(branch,branch.Seconds+30);
                Assert.DoesNotContain(branch.DiagnosticSnapshot().Messaging.Messages,m=>m.Id==message.Value);
                Assert.NotNull(Assassination.CaptivityAt(branch.DiagnosticSnapshot(),agent,branch.Seconds,branch.Revision+1));
            }
            else
            {
                var returnTrip=arrived.Schemes.Events.Single(e=>e.Input.Action==SchemeAction.PlanReturn&&e.Input.Reference==assignment.Id).Movement!;
                NewGameWorldTests.At(branch,returnTrip.EffectiveAt+returnTrip.DeliverySeconds+returnTrip.WorkSeconds+2);
                Assert.DoesNotContain(branch.DiagnosticSnapshot().Evidence,e=>e.Observer==branch.PlayerControl&&e.Message==message);
                Good(branch.Open(branch.PlayerControl,message,branch.Revision));
                Assert.Contains(branch.DiagnosticSnapshot().Evidence,e=>e.Observer==branch.PlayerControl&&e.Message==message);
            }
        }
        Assert.Equal(ContractJson.Write(restored.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
    }
    [Fact] public void PersonalMoveIssuedBeforeDeathCannotStartAfterDelivery()
    {
        EntityId victim=new("created:test"),attacker=new("created:contact"),site=new("site:kyoto");
        var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer(site:site.Value),NewGameWorldTests.Officer(attacker.Value,site:site.Value)]);
        var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,victim.Value,5,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy,territoryPolicy:CastleSiege.Policy));
        var move=TravelWorld.Move(s.DiagnosticSnapshot(),"request:last-move","akutagawa");
        Assert.Equal(ResultCode.Accepted,s.Submit(move,s.Revision).Code);
        var w=s.DiagnosticSnapshot();
        var sight=new SchemeInput(new("request:sight"),SchemeAction.ObservePerson,attacker,victim);
        var contact=sight with{Id=new("request:contact"),Action=SchemeAction.SeekContact,Reference=sight.Id};
        foreach(var input in new[]{sight,contact,contact with{Id=new("request:attack"),Action=SchemeAction.AttemptAssassination,Reference=contact.Id}})
        {
            w.CommitSequence++;
            InformationSchemes.Execute(w,input,attacker);
        }
        Assert.Equal(LifeState.Dead,w.Characters.Single(c=>c.Id==victim).Life);
        var original=new WorldSession(w);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        foreach(var branch in new[]{original,restored})
        {
            NewGameWorldTests.At(branch,move.EffectiveAt+move.DeliverySeconds+2);
            var end=branch.DiagnosticSnapshot();
            Assert.Equal(NewGameWork.InitialCoins,NewGameWork.Balance(end,victim));
            Assert.DoesNotContain(end.Branches!.Journeys,j=>j.Actor==victim);
            var task=Assert.Single(end.Tasks,t=>t.RequestId==move.RequestId);
            Assert.True(task.Execution is ExecutionPhase.Stopped or ExecutionPhase.Failed,task.Execution.ToString());
            Assert.Equal(site,end.Locations.Single(l=>l.Actor==victim).Site);
        }
        Assert.Equal(ContractJson.Write(original.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
}
