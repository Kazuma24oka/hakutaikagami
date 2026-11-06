using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public class FamilyKinTests
{
    [Fact] public void YoungOfficerWritesHomeOnComingOfAgeWithoutEnteringService()
    {
        var parent=NewGameWorldTests.Officer(site:"site:akutagawa");
        var child=NewGameWorldTests.Officer("created:junior",site:"site:kyoto") with{BirthYear=1546,BirthParents=[parent.Id]};
        var start=NewGameWorldTests.Setup([parent,child]);
        var world=NewGameWorld.Create(start.World,start.Registered,parent.Id,comingOfAge:15);
        const long newYear=366L*86400;
        world.ElapsedSeconds=newYear-1;
        var session=new WorldSession(world);
        NewGameWorldTests.At(session,newYear);
        world=session.DiagnosticSnapshot();
        var letter=Assert.Single(world.Messaging.Messages,m=>m.Kind==FamilyNotice.MaturityKind);
        var young=world.NewGame!.Start.People.Single(p=>p.Id==child.Id);
        Assert.True(FamilyNotice.WitnessedMaturity(world,young,new(child.Id)));
        Assert.False(FamilyNotice.WitnessedMaturity(world,young,new(parent.Id)));
        Assert.Equal(child.Id,letter.SenderId);
        Assert.Equal(parent.Id,letter.RecipientId);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(world)));
        foreach(var branch in new[]{session,restored})
        {
            Assert.NotEqual(ResultCode.Accepted,branch.Open(branch.PlayerControl,new(letter.Id),branch.Revision).Code);
            NewGameWorldTests.At(branch,world.Messaging.Attempts.Single(a=>a.MessageId==letter.Id).DueAt);
            Assert.Equal(ResultCode.Accepted,branch.Open(branch.PlayerControl,new(letter.Id),branch.Revision).Code);
            Assert.DoesNotContain(branch.DiagnosticSnapshot().ServiceBonds,b=>b.Subject.Value==child.Id);
        }
        Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }

    [Fact] public void DistantChildLearnsFromWitnessLetterOnlyAfterDeliveryAndOpening()
    {
        var elder=NewGameWorldTests.Officer("created:elder",site:"site:kyoto") with{BirthYear=1430};
        var child=NewGameWorldTests.Officer(site:"site:akutagawa") with{BirthParents=[elder.Id]};
        var witness=NewGameWorldTests.Officer("created:witness",site:elder.Site);
        var start=NewGameWorldTests.Setup([elder,child,witness]);
        var world=NewGameWorld.Create(start.World,start.Registered,child.Id,0,comingOfAge:15);
        const long at=12830400;
        world.ElapsedSeconds=at-1;
        var session=new WorldSession(world);
        NewGameWorldTests.At(session,at);
        world=session.DiagnosticSnapshot();
        var death=Assert.Single(world.NaturalDeaths!);
        var message=Assert.Single(world.Messaging.Messages,m=>m.Kind==FamilyNotice.Kind);
        Assert.True(FamilyNotice.Knows(world,death,new(witness.Id)));
        Assert.False(FamilyNotice.Knows(world,death,session.PlayerControl));
        var attempt=Assert.Single(world.Messaging.Attempts,a=>a.MessageId==message.Id);
        Assert.True(attempt.DueAt>at);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(world)));
        foreach(var branch in new[]{session,restored})
        {
            NewGameWorldTests.At(branch,attempt.DueAt);
            Assert.False(FamilyNotice.Knows(branch.DiagnosticSnapshot(),death,branch.PlayerControl));
            var opened=branch.Open(branch.PlayerControl,new(message.Id),branch.Revision);
            Assert.True(opened.Code==ResultCode.Accepted,opened.VisibleReason);
            Assert.True(FamilyNotice.Knows(branch.DiagnosticSnapshot(),death,branch.PlayerControl));
        }
        Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }

    [Fact] public void NaturalDeathIsRecordedOnceWithEstateIntactAcrossRecovery()
    {
        var elder=NewGameWorldTests.Officer("created:elder") with{BirthYear=1430};
        var start=NewGameWorldTests.Setup([NewGameWorldTests.Officer(),elder]);
        var w=NewGameWorld.Create(start.World,start.Registered,"created:test",0,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,comingOfAge:15);
        const long at=12830400;
        w.ElapsedSeconds=at-1;
        var uninterrupted=new WorldSession(w);
        var resumed=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        foreach(var session in new[]{uninterrupted,resumed})
        {
            NewGameWorldTests.At(session,at+1);
            var after=session.DiagnosticSnapshot();
            var death=Assert.Single(after.NaturalDeaths!);
            Assert.Equal(at,death.At);
            Assert.Equal(elder.Id,death.Person.Value);
            Assert.Equal(LifeState.Dead,after.Characters.Single(c=>c.Id==death.Person).Life);
            Assert.Equal(NewGameWork.InitialCoins,NewGameWork.Balance(after,death.Person));
            Assert.Equal(ContractJson.Write(after),ContractJson.Write(new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(after))).DiagnosticSnapshot()));
        }
        Assert.Equal(ContractJson.Write(uninterrupted.DiagnosticSnapshot()),ContractJson.Write(resumed.DiagnosticSnapshot()));
    }

    [Fact] public void NewYearMaturesOnceWithoutAppointingChildAndLegacyKeepsItsRule()
    {
        var child=NewGameWorldTests.Officer("created:junior") with{BirthYear=1546};
        var start=NewGameWorldTests.Setup([NewGameWorldTests.Officer(),child]);
        const long newYear=366L*86400;
        foreach(int age in new[]{FamilyKin.ComingOfAge,NewGameWorld.TrialAdultAge})
        {
            var w=NewGameWorld.Create(start.World,start.Registered,"created:test",comingOfAge:age);
            w.ElapsedSeconds=newYear-1;
            var session=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
            Assert.Equal(LifeState.Minor,session.DiagnosticSnapshot().Characters.Single(c=>c.Id.Value==child.Id).Life);
            NewGameWorldTests.At(session,newYear);
            var after=session.DiagnosticSnapshot();
            var person=after.Characters.Single(c=>c.Id.Value==child.Id);
            Assert.Equal(age==15?LifeState.Adult:LifeState.Minor,person.Life);
            Assert.DoesNotContain(after.ServiceBonds,b=>b.Subject==person.Id);
            var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(after)));
            NewGameWorldTests.At(restored,newYear+1);
            Assert.Equal(person,restored.DiagnosticSnapshot().Characters.Single(c=>c.Id==person.Id));
        }
    }
    [Fact] public void KnownParentSurvivesStartAndLoadWithoutInventingASecondParent()
    {
        var parent=NewGameWorld.Scenario().People.First(p=>!p.Created);
        var start=NewGameWorldTests.Setup([NewGameWorldTests.Officer() with{BirthParents=[parent.Id]}]);
        var scenario=start.World;
        var people=scenario.People;
        var session=new WorldSession(NewGameWorld.Create(scenario,start.Registered,"created:test"));
        var bytes=ContractJson.Write(session.DiagnosticSnapshot());
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(bytes));
        Assert.Equal(parent.Id,Assert.Single(restored.DiagnosticSnapshot().NewGame!.Start.People.Single(p=>p.Id=="created:test").BirthParents!));
        Assert.Equal(bytes,ContractJson.Write(restored.DiagnosticSnapshot()));
        var cycle=scenario with{People=people.Select(p=>p.Id==parent.Id?p with{BirthParents=["created:test"]}:p).ToArray()};
        Assert.Throws<InvalidDataException>(()=>NewGameSetup.Validate(cycle));
    }
}