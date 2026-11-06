using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class DestructiveSchemesTests
{
    [Theory]
    [InlineData(SchemeTopic.Arson)]
    [InlineData(SchemeTopic.Sabotage)]
    [InlineData(SchemeTopic.SeizeRice)]
    [InlineData(SchemeTopic.Agitation)]
    public void LocalAttemptIsRepeatableAfterLoadAndCannotBeRerolled(SchemeTopic topic)
    {
        EntityId site=new("site:sakai");
        var actor=NewGameWorldTests.Officer("created:scheme-lord",StartTemplate.Daimyo,site.Value);
        var setup=NewGameWorldTests.Setup([actor],[new(actor.Id,"created:scheme-house",[site.Value],false)]);
        var initial=NewGameWorld.Create(setup.World,setup.Registered,actor.Id,21,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy,territoryPolicy:CastleSiege.Policy,weatherPolicy:RegionalWeather.Policy);
        var s=new WorldSession(initial);
        var input=new SchemeInput(new("request:local-operation"),SchemeAction.ExecuteOperation,s.PlayerControl,site,topic);
        var result=s.ManageScheme(input,s.Revision);
        Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var w=s.DiagnosticSnapshot();
        Assert.EndsWith("を実行した",DestructiveSchemes.Outcome(w,Assert.Single(w.Schemes!.Events)));
        if(topic==SchemeTopic.Arson)Assert.Equal(20,RegionalWeather.Damage(w,site,WorksKind.RepairTown,s.Seconds,s.Revision+1));
        if(topic==SchemeTopic.Sabotage)Assert.Equal(20,CastleWorks.Defense(w,site,CastleSection.Gate,s.Seconds,s.Revision+1).Damage);
        if(topic==SchemeTopic.SeizeRice){
            Assert.Equal(5,HouseSalary.PrivateRice(w,s.PlayerControl));
            Assert.Equal(initial.Communities!.Accounts.Single(a=>a.Site==site).HouseRice-5,w.Communities!.Accounts.Single(a=>a.Site==site).HouseRice);
        }
        if(topic==SchemeTopic.Agitation)Assert.Equal(ResultCode.NotAvailable,s.StartPublicWork(new(new("request:resisted-work"),site,WorksKind.Agriculture),s.Revision).Code);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        foreach(var branch in new[]{s,restored})
        {
            Assert.Equal(ResultCode.AlreadyAccepted,branch.ManageScheme(input,branch.Revision).Code);
            Assert.Equal(ResultCode.NotAvailable,branch.ManageScheme(input with { Id=new("request:reroll") },branch.Revision).Code);
            Assert.Equal(ContractJson.Write(w),ContractJson.Write(branch.DiagnosticSnapshot()));
        }
    }
    [Fact] public void AgentMustReturnAndReportBeforeEmployerLearnsOutcome()
    {
        EntityId agent=new("created:scheme-agent"),target=new("site:akutagawa");
        var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer(site:"site:kyoto"),NewGameWorldTests.Officer(agent.Value,site:"site:kyoto")]);
        var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,"created:test",42,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,
            companionPolicy:PersonalCompanions.Policy,economyPolicy:HouseSuccession.Policy,territoryPolicy:CastleSiege.Policy,weatherPolicy:RegionalWeather.Policy));
        void Good(CommandResult result)=>Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        Good(s.ManageCompanion(new(new("request:escort"),agent,CompanionAction.Agree,20),s.PlayerControl,s.Revision));
        var input=new SchemeInput(new("request:send-agent"),SchemeAction.SendScout,agent,s.PlayerControl,SchemeTopic.Sabotage,Destination:target);
        Good(s.ManageScheme(input,s.Revision));
        var outward=s.DiagnosticSnapshot().Schemes!.Events.Single(e=>e.Input.Id==input.Id).Movement!;
        NewGameWorldTests.At(s,outward.EffectiveAt+outward.DeliverySeconds+outward.WorkSeconds);
        var w=s.DiagnosticSnapshot();
        var attempt=Assert.Single(w.Schemes!.Events,e=>e.Input.Action==SchemeAction.ExecuteOperation);
        Assert.Equal(target,attempt.Site);
        var report=new EntityId(InformationSchemes.ReportId(input.Id));
        Assert.DoesNotContain(w.Evidence,e=>e.Observer==s.PlayerControl&&e.Message==report);
        var forged=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        var observed=Assert.Single(forged.Schemes!.Events,e=>e.Input.Action==SchemeAction.CaptureScout).Sources.Single();
        forged.Evidence=forged.Evidence.Select(e=>e.Id==observed?e with{Claim="城が明け渡された"}:e).ToArray();
        Assert.Throws<InvalidDataException>(()=>new WorldSession(forged));
        var back=Assert.Single(w.Schemes.Events,e=>e.Input.Action==SchemeAction.PlanReturn).Movement!;
        s.SetPaused(true);
        var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"operation-agent-"+Guid.NewGuid().ToString("N")),w.Definitions);
        Assert.True(store.Save(s,"agent",w.WorldId).Published);
        var host=new WorldSessionHost(new WorldSession(w));
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("agent"),host.Current,host.Current.Revision));
        foreach(var branch in new[]{s,host.Current})
        {
            NewGameWorldTests.At(branch,back.EffectiveAt+back.DeliverySeconds+back.WorkSeconds+2);
            Assert.DoesNotContain(branch.DiagnosticSnapshot().Evidence,e=>e.Observer==branch.PlayerControl&&e.Message==report);
            Good(branch.Open(branch.PlayerControl,report,branch.Revision));
            Assert.Contains(branch.DiagnosticSnapshot().Evidence,e=>e.Observer==branch.PlayerControl&&e.Message==report&&e.Claim==DestructiveSchemes.Outcome(w,attempt));
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
    }
}
