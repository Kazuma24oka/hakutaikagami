using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

[Trait("Tier","Standard")]
public sealed class NationalFactionRelationsTests
{
    [Fact]
    public void NationalStartStoresKnownDirectedVassalRelationsAndRejectsDisclosureOrTampering()
    {
        var scenario=NationalStart.Scenario();
        var selected=scenario.Factions.Select(f=>f.Head).Single(h=>scenario.People.Single(p=>p.Id==h).Name.Contains("信長"));
        var world=NationalStart.Create(selected);
        var state=Assert.IsType<NationalFactionRelationsState>(world.FactionRelations);
        Assert.Equal(NationalFactionRelations.Policy,state.Policy);
        Assert.Equal(HakutaiDb.Houses.Count(h=>h.Overlord!=null),state.Relations.Length);
        Assert.Contains(state.Relations,r=>r.SubjectFaction!=null&&r.OverlordFaction!=null);
        var linked=state.Relations.First(r=>r.SubjectFaction!=null&&r.OverlordFaction!=null);
        var subject=scenario.People.First(p=>p.Faction==linked.SubjectFaction).Id;
        var overlord=scenario.People.First(p=>p.Faction==linked.OverlordFaction).Id;
        var subjectName=scenario.Factions.Single(f=>f.Id==linked.OverlordFaction).Name;
        var vassalName=scenario.Factions.Single(f=>f.Id==linked.SubjectFaction).Name;
        Assert.Contains("主家・"+subjectName,NationalFactionRelations.DisplayFor(world,new(subject))!);
        Assert.Contains("従属・"+vassalName,NationalFactionRelations.DisplayFor(world,new(overlord))!);
        Assert.DoesNotContain("従属・"+vassalName,NationalFactionRelations.DisplayFor(world,new(subject))!);
        var restored=ContractJson.Read<ContractWorld>(ContractJson.Write(world));
        Assert.Equal(ContractJson.CanonicalHash(state),ContractJson.CanonicalHash(restored.FactionRelations));
        ContractValidation.Validate(restored);
        var originalSession=new WorldSession(world);var store=new GenerationSaveStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"national-relations-"+Guid.NewGuid().ToString("N")),world.Definitions);
        Assert.True(store.Save(originalSession,"relations",world.WorldId).Published);
        var host=new WorldSessionHost(new WorldSession(TravelWorld.Create()));Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("relations"),host.Current,host.Current.Revision));
        Assert.Equal(ContractJson.Write(originalSession.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        NewGameWorldTests.At(originalSession,60);NewGameWorldTests.At(host.Current,60);
        Assert.Equal(ContractJson.Write(originalSession.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        var forged=ContractJson.Copy(world);forged.FactionRelations=forged.FactionRelations! with{Relations=forged.FactionRelations.Relations.Skip(1).ToArray()};
        Assert.Throws<InvalidDataException>(()=>ContractValidation.Validate(forged));
    }

    [Fact]
    public void NonNationalWorldDoesNotReceiveNationalRelations()
    {
        var world=TravelWorld.Create();
        Assert.Null(world.FactionRelations);
        Assert.Null(NationalFactionRelations.DisplayFor(world,world.PlayerControl));
        NationalFactionRelations.Validate(world);
    }
}
