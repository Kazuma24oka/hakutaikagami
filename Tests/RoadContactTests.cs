using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public class RoadContactTests
{
    [Fact] public void RestoreBetweenSightingAndContactOnActualJourneys()
    {
        EntityId agent=new("created:contact"), home=new("site:kyoto");
        var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer(site:home.Value),NewGameWorldTests.Officer(agent.Value,site:home.Value)]);
        var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,"created:test",42,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy,
            economyPolicy:HouseSuccession.Policy,territoryPolicy:CastleSiege.Policy));
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        Good(s.ManageCompanion(new(new("request:hire"),agent,CompanionAction.Agree,20),s.PlayerControl,s.Revision));
        Good(s.ManageScheme(new(new("request:send"),SchemeAction.SendScout,agent,s.PlayerControl,Destination:new("site:akutagawa")),s.Revision));
        var move=TravelWorld.Move(s.DiagnosticSnapshot(),"request:follow","akutagawa");
        Good(s.Submit(move,s.Revision));
        NewGameWorldTests.At(s,move.EffectiveAt+move.DeliverySeconds+30);
        var world=s.DiagnosticSnapshot();
        var road=Assassination.RoadMeeting(world,s.PlayerControl,agent,world.ElapsedSeconds);
        Assert.NotNull(road);
        var sight=new SchemeInput(new("request:road-sighting"),SchemeAction.ObservePerson,s.PlayerControl,agent);
        Good(s.ManageScheme(sight,s.Revision));
        Assert.Equal(road,Assassination.Sightings(s.DiagnosticSnapshot(),s.PlayerControl).Single().Site);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(s.DiagnosticSnapshot())));
        foreach(var branch in new[]{s,restored})
            Good(branch.ManageScheme(sight with{Id=new("request:road-contact"),Action=SchemeAction.SeekContact,Reference=sight.Id},branch.Revision));
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
}