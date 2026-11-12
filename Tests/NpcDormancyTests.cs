using System.Text;
using HakutaiKagami.Core.Appearance;
using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// 遠方の人物の休眠（2026-11-12、v108）。
[Trait("Tier","Heavy")]public sealed class NpcDormancyTests
{
    static long Km(RoutePoint a,RoutePoint b){long dx=a.EastMm-b.EastMm,dy=a.NorthMm-b.NorthMm;return (long)Math.Sqrt((double)(dx*dx+dy*dy))/1_000_000;}
    static readonly BranchNode[] nodes=CastleRoutes.Nodes;
    static RoutePoint At(string id)=>nodes.Single(n=>n.Id==id).Position;
    // 清洲（castle）の近くの城と、40〜60km 離れた城、そこへ近づく行き先（離れた城から20km 以内）。
    static (string Near,string Far,string Goal) Places()
    {
        var home=At("castle");var castles=nodes.Where(n=>n.Id.StartsWith("c-",StringComparison.Ordinal)).OrderBy(n=>n.Id,StringComparer.Ordinal).ToArray();
        var near=castles.First(n=>Km(n.Position,home)<10).Id;
        var far=castles.First(n=>Km(n.Position,home) is >=45 and <=60).Id;
        var goal=castles.Where(n=>Km(n.Position,At(far))<=15&&Km(n.Position,home)>=30).OrderBy(n=>Km(n.Position,home)).First().Id;
        return (near,far,goal);
    }
    static RegisteredOfficer Officer(string id,string node)=>new(id,null,"鏡山 宗久",1520,new(60,50,70,40),StartTemplate.Ronin,"site:"+node,null,null,AppearanceCatalog.Default(1));
    internal static ContractWorld World(string? dormancy)
    {
        var p=Places();var raw=WorldNetwork.WithCastles(NewGameWorld.Scenario());
        var records=NewGameWorld.Normalize([Officer("created:near",p.Near),Officer("created:far",p.Far)]);
        var request=new WorldSetupRequest(records,[]);var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        return NewGameWorld.Create(setup,records,"person:nobunaga",20261112,NewGameWorld.CompactPersonalAuthority,npcRoutinePolicy:NewGameNpcRoutine.ContinuingPolicy,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,demandPolicy:LocalWorkDemand.Policy,networkPolicy:WorldNetwork.CastlePolicy,dormancyPolicy:dormancy);
    }
    static EntityId[] Deciders(ContractWorld w)=>w.NpcWork!.Decisions.Select(d=>d.Actor).Distinct().ToArray();

    [Fact]
    public void WorldsWithoutTheVersionKeepEveryoneAwake()
    {
        var w=World(null);Assert.Null(w.NewGame!.NpcDormancyPolicy);Assert.DoesNotContain("NpcDormancyPolicy",Encoding.UTF8.GetString(ContractJson.Write(w)));
        var s=new WorldSession(w);NewGameWorldTests.At(s,60);
        var d=Deciders(s.DiagnosticSnapshot());Assert.Contains(new EntityId("created:near"),d);Assert.Contains(new EntityId("created:far"),d);
        Assert.Throws<InvalidDataException>(()=>NewGameWorld.Create(NewGameSetup.Confirm(NewGameWorld.Scenario(),new([],[]),NewGameSetup.Preview(NewGameWorld.Scenario(),new([],[]))),[],"person:nobunaga",dormancyPolicy:NpcDormancy.Policy));
    }

    [Fact]
    public void DistantPeopleSleepAndWakeWhenThePlayerComesNear()
    {
        var p=Places();var s=new WorldSession(World(NpcDormancy.Policy));NewGameWorldTests.At(s,2*3600);
        var w=s.DiagnosticSnapshot();var d=Deciders(w);
        Assert.Contains(new EntityId("created:near"),d);Assert.DoesNotContain(new EntityId("created:far"),d);
        Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(w,"request:approach",p.Goal),s.Revision).Code);
        NewGameWorldTests.At(s,3*86400);w=s.DiagnosticSnapshot();
        var far=w.NpcWork!.Decisions.Where(x=>x.Actor==new EntityId("created:far")).ToArray();
        Assert.NotEmpty(far);Assert.Equal(0,far[0].At%NpcDormancy.Interval);
        Assert.All(far,x=>Assert.True(NpcDormancy.Awake(w,x.Actor,x.At)));
        // 離れた清洲の近くの人物は、本人が30km より離れた区切りから後は判断しない。
        var left=Enumerable.Range(1,72).Select(h=>h*NpcDormancy.Interval).First(t=>!NpcDormancy.Awake(w,new("created:near"),t));
        Assert.DoesNotContain(w.NpcWork.Decisions,x=>x.Actor==new EntityId("created:near")&&x.At>=left);
        ContractValidation.Validate(w);
    }

    [Fact]
    public void WakingDoesNotDependOnStepSizeAndRestoresToTheSameFuture()=>SameFuture(NpcDormancy.Policy);
    static void SameFuture(string? policy)
    {
        var p=Places();
        WorldSession Start(){var s=new WorldSession(World(policy));NewGameWorldTests.At(s,60);Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:approach",p.Goal),s.Revision).Code);return s;}
        const long end=2*86400;
        var big=Start();NewGameWorldTests.At(big,end);
        var small=Start();for(long t=small.Seconds+777;t<end;t+=777)NewGameWorldTests.At(small,t);NewGameWorldTests.At(small,end);
        var half=Start();NewGameWorldTests.At(half,end/2);var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(half.DiagnosticSnapshot())));NewGameWorldTests.At(restored,end);
        // 進めた細かさで変わるのは変更の通し番号だけ。判断・依頼・旅の中身が同じであることを確かめる。
        string D(WorldSession x){var w=x.DiagnosticSnapshot();return string.Join(";",w.NpcWork!.Decisions.Select(d=>$"{d.Actor} {d.At} {d.Reason} {d.Request} {d.Result}"))+"|"+string.Join(";",w.Requests.Select(r=>$"{r.Id} {r.CreatedAt} {r.BodyHash}"))+"|"+string.Join(";",w.Tasks.Select(t=>$"{t.Id} {t.StartedAt} {t.FinishedAt} {t.Execution}"))+"|"+string.Join(";",w.Branches!.Journeys.Select(j=>$"{j.Actor} {j.Legs[0].StartAt} {j.Legs[^1].EndAt}"))+"|"+w.ElapsedSeconds;}
        NewGameWorldTests.At(half,end);Assert.Equal(ContractJson.Write(half.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot())); // 途中の保存から同じ未来（バイト一致）
        Assert.Equal(D(big),D(restored));
        Assert.Equal(D(big),D(small));
        Assert.Contains(big.DiagnosticSnapshot().NpcWork!.Decisions,x=>x.Actor==new EntityId("created:far"));
    }
}

// 休眠の世界の手番のまとめた検査（2026-11-12、v109）。
public sealed class NpcBatchValidationTests
{
    static string Run(bool fail)
    {
        WorldSession.FailBatchForTest=fail;WorldSession.BatchRetries=0;
        try{var s=new WorldSession(NpcDormancyTests.World(NpcDormancy.Policy));NewGameWorldTests.At(s,2*3600);
            if(fail)Assert.True(WorldSession.BatchRetries>0);else Assert.Equal(0,WorldSession.BatchRetries);
            var w=s.DiagnosticSnapshot();ContractValidation.Validate(w);Assert.NotEmpty(w.NpcWork!.Decisions);return Encoding.UTF8.GetString(ContractJson.Write(w));}
        finally{WorldSession.FailBatchForTest=false;}
    }
    [Fact]public void BatchedTurnsAndOneByOneRetryReachTheSameWorld()=>Assert.Equal(Run(false),Run(true));
}
