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
}
