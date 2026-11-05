using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class NpcJudgmentTests
{
    internal static ContractWorld Start()
    {
        var old=NpcContinuingTests.Start(companions:true);var d=old.NewGame!;
        return NewGameWorld.Create(d.Start,d.Registered,d.Selected.Value,old.Seed,NewGameWorld.CompactPersonalAuthority,HistoricalStartCatalog.Current,NpcJudgment.Policy,LocalInspectionFindings.Policy,WorkFollowup.Policy,LocalWorkDemand.Policy,
            developmentPolicy:CharacterDevelopment.Policy,companionPolicy:PersonalCompanions.Policy,socialPolicy:CharacterRelations.Policy,
            judgment:new(NpcJudgment.Policy,HistoryMode.SemiHistorical,NpcAttention.Standard));
    }
    static void At(WorldSession s,long at)=>NewGameWorldTests.At(s,at);
    [Fact] public void NewSettingsAreRequiredAndCannotChangeOldWorlds()
    {
        var w=Start();w.NewGame=w.NewGame! with{Judgment=null};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
        w=NpcPurposeTests.Start();w.NewGame=w.NewGame! with{Judgment=new(NpcJudgment.Policy,HistoryMode.Fictional,NpcAttention.Equal)};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Fact] public void NearbyAndRemoteActorsUseDifferentReviewIntervalsButSamePlanner()
    {
        var s=new WorldSession(Start());At(s,20);var w=s.DiagnosticSnapshot();
        var nearby=NewGameNpcRoutine.Inputs(w,new("created:routine-0"),w.ElapsedSeconds,w.CommitSequence).Routine!.Judgment!;
        var distant=NewGameNpcRoutine.Inputs(w,new("created:routine-1"),w.ElapsedSeconds,w.CommitSequence).Routine!.Judgment!;
        Assert.Equal(NpcCadence.Daily,nearby.Cadence);Assert.Equal(86400,nearby.ReadyAt);
        Assert.Equal(NpcCadence.Monthly,distant.Cadence);Assert.Equal(31*86400,distant.ReadyAt);
        Assert.False(nearby.Due);Assert.False(distant.Due);new WorldSession(w);
    }
    [Fact] public void NoNewDailyTaskIsGeneratedByFineGrainedAdvance()
    {
        var s=new WorldSession(Start());At(s,20);int count=s.DiagnosticSnapshot().Requests.Length;
        At(s,3601);Assert.Equal(count,s.DiagnosticSnapshot().Requests.Length);
        At(s,86420);Assert.True(s.DiagnosticSnapshot().Requests.Length>count);
        Assert.Single(s.DiagnosticSnapshot().Requests,r=>r.Intent.Actor.Value=="created:routine-1");
    }
    [Fact] public void DifferentClockPartitionsKeepChoicesAndActualTasks()
    {
        var a=new WorldSession(Start());var b=new WorldSession(Start());At(a,86420);
        foreach(long t in new long[]{1,7,20,3601,86399,86400,86401,86420})At(b,t);
        Assert.Equal(a.DiagnosticSnapshot().Requests.Select(r=>(r.Id,r.CreatedAt,r.Intent.Action,r.Intent.TrainingSkill)),b.DiagnosticSnapshot().Requests.Select(r=>(r.Id,r.CreatedAt,r.Intent.Action,r.Intent.TrainingSkill)));
    }
    [Fact] public void UndisclosedRoadClosureCannotChangeTheFirstChoice()
    {
        var a=Start();var b=ContractJson.Copy(a);var edge=BranchRoutes.GraphEdges(b).First().Id;b.Couriers=b.Couriers! with{Conditions=[new(1,edge,true)]};
        foreach(var actor in a.Characters.Where(c=>c.Id!=a.PlayerControl))Assert.Equal(ContractJson.Write(NewGameNpcRoutine.Inputs(a,actor.Id,0,0)),ContractJson.Write(NewGameNpcRoutine.Inputs(b,actor.Id,0,0)));
    }
    [Fact] public void ForgedJudgmentCannotReplaceHistoricalInputs()
    {
        var s=new WorldSession(Start());At(s,20);var w=s.DiagnosticSnapshot();var d=w.NpcWork!.Decisions[0];var routine=d.Inputs.Routine!;
        var fake=d.Inputs with{Routine=routine with{Judgment=routine.Judgment! with{Choice="practice:Tactics"}}};
        w.NpcWork.Decisions[0]=d with{Inputs=fake,InputHash=ContractJson.CanonicalHash(fake)};
        Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
}
