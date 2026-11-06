using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public class FamilyKinTests
{
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