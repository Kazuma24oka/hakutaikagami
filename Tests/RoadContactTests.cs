using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public class RoadContactTests
{
    [Theory]
    [InlineData(3,SchemeAction.FatalEncounter)]
    [InlineData(4,SchemeAction.CaughtInEncounter)]
    [InlineData(0,SchemeAction.EscapedEncounter)]
    public void RestoreBetweenSightingAndContactOnActualJourneys(int seed,SchemeAction expected)
    {
        EntityId agent=new("created:contact"), home=new("site:kyoto");
        var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer(site:home.Value),NewGameWorldTests.Officer(agent.Value,site:home.Value)]);
        var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,"created:test",checked((ulong)seed),
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
        {
            var contact=sight with{Id=new("request:road-contact"),Action=SchemeAction.SeekContact,Reference=sight.Id};
            Good(branch.ManageScheme(contact,branch.Revision));
            Good(branch.ManageScheme(contact with{Id=new("request:road-attack"),Action=SchemeAction.AttemptAssassination,Reference=contact.Id},branch.Revision));
            var snapshot=branch.DiagnosticSnapshot();
            var outcome=Assert.Single(snapshot.Schemes!.Events,e=>Assassination.IsConclusion(e.Input.Action));
            Assert.Equal(road,outcome.Site);
            Assert.Equal(expected,outcome.Input.Action);
            foreach(var person in new[]{agent,s.PlayerControl})
            {
                if(expected==SchemeAction.CaughtInEncounter || expected==SchemeAction.FatalEncounter && person==agent)
                {
                    var point=RegionalCampaign.RoadActorPointAt(snapshot,person,snapshot.ElapsedSeconds);
                    Assert.Equal(point,RegionalCampaign.RoadActorPointAt(snapshot,person,snapshot.ElapsedSeconds+300));
                }
            }
            Assert.Equal(ContractJson.Write(snapshot),ContractJson.Write(new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(snapshot))).DiagnosticSnapshot()));
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
}