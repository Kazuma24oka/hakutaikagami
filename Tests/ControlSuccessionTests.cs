using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// 操作の引き継ぎ（BD-07、2026-11-12）：操作する人物が死ぬと止まり、知っていた成人の子へ引き継いで続ける。
public sealed class ControlSuccessionTests
{
    const long DeathAt=12830400; // created:elder（1430年生まれ・seed 0）の自然死

    static ContractWorld World(bool policy,bool withChild=true)
    {
        var elder=NewGameWorldTests.Officer("created:elder") with{BirthYear=1430};
        var child=NewGameWorldTests.Officer() with{BirthYear=1530,BirthParents=[elder.Id]};
        var start=NewGameWorldTests.Setup(withChild?[child,elder]:[NewGameWorldTests.Officer() with{BirthYear=1530},elder]);
        var w=NewGameWorld.Create(start.World,start.Registered,"created:elder",0,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,comingOfAge:15,controlPolicy:policy?ControlSuccession.Policy:null);
        w.ElapsedSeconds=DeathAt-1;
        return w;
    }

    [Fact] public void DeathStopsTheWorldAndTheKnownAdultChildContinuesWithTheSameFuture()
    {
        var session=new WorldSession(World(true));session.SetPaused(false);
        var r=session.AdvanceTo(DeathAt+3600);
        Assert.Equal("PlayerDead",r.StopReason);
        var dead=session.DiagnosticSnapshot();
        Assert.Equal(DeathAt,dead.ElapsedSeconds);
        Assert.True(ControlSuccession.Awaiting(dead));
        Assert.Equal(new[]{new EntityId("created:test")},ControlSuccession.Candidates(dead));
        Assert.Equal("PlayerDead",session.AdvanceTo(DeathAt+7200).StopReason);
        Assert.Equal(ResultCode.NotAvailable,session.ContinueAs(new("created:elder"),dead.CommitSequence).Code);
        Assert.Equal(ResultCode.Accepted,session.ContinueAs(new("created:test"),dead.CommitSequence).Code);
        var after=session.DiagnosticSnapshot();
        Assert.Equal(new EntityId("created:test"),after.PlayerControl);
        Assert.False(ControlSuccession.Awaiting(after));
        Assert.Equal(new EntityId("created:elder"),ControlSuccession.PlayerAtRevision(after,dead.CommitSequence));
        Assert.Equal(new EntityId("created:test"),ControlSuccession.PlayerAtRevision(after,after.CommitSequence));
        // 引き継いだ後の途中の保存から復旧しても、同じ未来になる。
        var resumed=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(after)));
        foreach(var s in new[]{session,resumed}){s.SetPaused(false);Assert.NotEqual("PlayerDead",s.AdvanceTo(DeathAt+2*86400).StopReason);}
        Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(resumed.DiagnosticSnapshot()));
        // 引き継ぎの記録は書き換えられない。
        var tampered=ContractJson.Copy(session.DiagnosticSnapshot());tampered.ControlHandovers=[tampered.ControlHandovers![0] with{To=new("created:elder")}];
        Assert.Throws<InvalidDataException>(()=>ContractValidation.Validate(tampered));
    }

    [Fact] public void WithoutAChildTheLifeEndsAndOldWorldsKeepTheirRule()
    {
        var ended=new WorldSession(World(true,withChild:false));ended.SetPaused(false);
        Assert.Equal("PlayerDead",ended.AdvanceTo(DeathAt+3600).StopReason);
        Assert.True(ControlSuccession.Ended(ended.DiagnosticSnapshot()));
        var legacy=new WorldSession(World(false));legacy.SetPaused(false);
        Assert.NotEqual("PlayerDead",legacy.AdvanceTo(DeathAt+3600).StopReason);
        Assert.False(ControlSuccession.Awaiting(legacy.DiagnosticSnapshot()));
        Assert.Null(legacy.DiagnosticSnapshot().ControlHandovers);
    }

    // 全国開始：DBの父（今川義元）を、子（今川氏真）の実の親として持つ。義元の生没年は自然死に使う。
    [Fact] public void NationalStartKeepsDbFathersAndLifeYears()
    {
        var people=NationalStart.Scenario().People;
        string Of(string db)=>people.Single(p=>NationalStart.DbPerson(p.Id)?.Id==db).Id;
        var ujizane=people.Single(p=>p.Id==Of("P-005546"));
        Assert.Equal(new[]{Of("P-005312")},ujizane.BirthParents);
        Assert.Equal((1519,1560),NationalStart.Life(Of("P-005312")));
        Assert.True(people.Count(p=>p.BirthParents!=null)>50);
    }
}
