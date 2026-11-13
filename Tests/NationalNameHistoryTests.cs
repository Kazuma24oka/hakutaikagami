using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

[Trait("Tier","Standard")]
public sealed class NationalNameHistoryTests
{
    [Fact]
    public void NationalNameChangesAtYearBoundaryAndResumesIdenticallyAfterFileRestore()
    {
        var scenario=NationalStart.Scenario();
        var subject=scenario.People.Single(p=>NationalStart.DbPerson(p.Id)?.Id=="P-005796");
        var actor=new EntityId(subject.Id);
        var selected=scenario.Factions.Select(f=>f.Head).Single(h=>scenario.People.Single(p=>p.Id==h).Name.Contains("信長"));
        var world=NationalStart.Create(selected);
        Assert.Equal("口羽通良",world.Definitions.Characters.Single(c=>c.Id==actor).Name);
        Assert.Equal(NationalNameHistory.Policy,world.NewGame!.NameHistoryPolicy);
        Assert.Empty(world.NationalNames!.Changes);

        long yearBoundary=(new DateTime(1561,1,1)-new DateTime(1560,1,1)).Ticks/TimeSpan.TicksPerSecond;
        world.ElapsedSeconds=yearBoundary-1;
        var session=new WorldSession(world);
        var before=session.DiagnosticSnapshot();
        Assert.Equal("口羽通良",MeritRewards.DisplayName(before,session.PlayerControl,actor));
        Assert.Equal(yearBoundary,NationalNameHistory.NextBoundary(before,yearBoundary));

        var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"national-name-"+Guid.NewGuid().ToString("N")),before.Definitions);
        Assert.True(store.Save(session,"name-history",before.WorldId).Published);
        var host=new WorldSessionHost(new WorldSession(TravelWorld.Create()));
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("name-history"),host.Current,host.Current.Revision));
        var restored=host.Current;
        Assert.Equal(ContractJson.Write(before),ContractJson.Write(restored.DiagnosticSnapshot()));

        NewGameWorldTests.At(session,yearBoundary);
        NewGameWorldTests.At(restored,yearBoundary);
        var after=session.DiagnosticSnapshot();
        Assert.Equal("口羽通良",MeritRewards.DisplayName(after,session.PlayerControl,actor));
        var remoteObserver=scenario.Factions.Select(f=>f.Head).Single(h=>scenario.People.Single(p=>p.Id==h).Name.Contains("信長"));
        Assert.Equal("口羽通良",MeritRewards.DisplayName(after,new(remoteObserver),actor));
        var encounter=ContractJson.Copy(after);var subjectSite=SocialWorld.Site(encounter,actor)!.Value;
        encounter.Tasks=encounter.Tasks.Where(t=>t.Executor!=new EntityId(remoteObserver)||t.Execution is ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed).ToArray();
        encounter.Locations=encounter.Locations.Select(l=>l.Actor==new EntityId(remoteObserver)?l with{Kind=LocationKind.AtSite,Site=subjectSite,Route=null}:l).ToArray();
        NationalNameHistory.ObserveLocal(encounter);
        Assert.Equal("志道通良",MeritRewards.DisplayName(encounter,new(remoteObserver),actor));
        var encounterReloaded=ContractJson.Read<ContractWorld>(ContractJson.Write(encounter));
        Assert.Equal("志道通良",MeritRewards.DisplayName(encounterReloaded,new(remoteObserver),actor));
        Assert.Single(after.NationalNames!.Changes,c=>c.Person==actor&&c.Year==1561&&c.Name=="志道通良");
        Assert.Equal(before.Definitions.Characters.Select(c=>c.Id),after.Definitions.Characters.Select(c=>c.Id));
        Assert.Equal("",NationalNameHistory.DisplayName(after,session.PlayerControl,new("person:not-placed")));
        Assert.Equal(ContractJson.Write(after),ContractJson.Write(restored.DiagnosticSnapshot()));

        ContractValidation.Validate(after);
    }

    [Fact]
    public void NonNationalWorldKeepsNameHistoryAbsentAndRejectsInjectedState()
    {
        var world=TravelWorld.Create();
        Assert.Null(world.NationalNames);
        NationalNameHistory.Validate(world);
        world.NationalNames=NationalNameHistory.Initial();
        Assert.Throws<InvalidDataException>(()=>NationalNameHistory.Validate(world));
    }

    [Fact]
    public void ExistingNationalProfileWithoutNamePolicyRoundTripsWithoutAddingTheFeature()
    {
        var scenario=NationalStart.Scenario();
        var selected=scenario.Factions.Select(f=>f.Head).Single(h=>scenario.People.Single(p=>p.Id==h).Name.Contains("信長"));
        var world=NationalStart.Create(selected);
        world.NewGame=world.NewGame! with{NameHistoryPolicy=null};
        world.NationalNames=null;
        world.Definitions=world.Definitions with{Version=NewGameWorld.Version(world.NewGame)};
        ContractValidation.Validate(world);
        var bytes=ContractJson.Write(world);
        var json=System.Text.Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("NameHistoryPolicy",json,StringComparison.Ordinal);
        Assert.DoesNotContain("NationalNames",json,StringComparison.Ordinal);
        Assert.Equal(bytes,ContractJson.Write(ContractJson.Read<ContractWorld>(bytes)));
    }
}
