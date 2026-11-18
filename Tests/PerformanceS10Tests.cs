using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
using Xunit;

namespace HakutaiKagami.Tests;

// S10（2026-11-17）：全国規模の性能。速さの直しが結果を変えないこと（表で引く・確かめ済みを飛ばす・道の計画・健康と離反の判断）と、NPCの城主の報告の再送。
[Trait("Tier","Heavy")]
public sealed class PerformanceS10Tests
{
    static readonly EntityId Site=new("site:kofu"),Head=new("person:shingen"),Agent=HistoricalHouseholdCatalog.Edition.People.Single().Id;
    static void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
    static void Advance(WorldSession s,long at){while(s.Seconds<at){long before=s.Seconds;s.SetPaused(false);s.AdvanceTo(at,100000);Assert.True(s.Seconds>before);}}

    [Fact]
    public void LookupTablesGiveTheSameAnswersAsScanningEverything()
    {
        using var noGuard=TurnSnapshotGuard.Off(); // 全国の世界で手番ごとの写しの照合は重すぎる。照合そのものは小さな世界の検査で確かめる（2026-11-18、検査の見直し）
        var s=new WorldSession(NationalStart.Create(NationalStartTests.Nobunaga()));
        NewGameWorldTests.At(s,s.Seconds+3600);
        var w=s.DiagnosticSnapshot();
        // 申し出を受けている人・依頼の ID・書状・仕事の表は、全部をなめた答えと同じ。
        Assert.Equal(w.Requests.Where(r=>NpcLocalOffers.IsOffer(w,r.Intent)).Select(r=>r.Intent.Executor).ToHashSet(),NpcLocalOffers.OfferedTo(w));
        Assert.Equal(w.Requests.Where(r=>NpcLocalOffers.IsOffer(w,r.Intent)).Select(r=>r.Id.Value).ToHashSet(),NpcLocalOffers.OfferIds(w));
        foreach(var actor in w.Tasks.Select(t=>t.Executor).Distinct().Take(20))
            Assert.Equal(w.Tasks.Where(t=>t.Executor==actor),NpcLocalOffers.TasksOf(w,actor));
        foreach(var r in w.Messaging.Messages.Select(m=>m.RecipientId).Distinct().Take(20))
            Assert.Equal(w.Messaging.Messages.Where(m=>m.RecipientId==r&&m.Kind is "contract-order" or "assessment-notice"),NpcLocalOffers.OfferMessagesTo(w,r));
        // 確かめ済みの依頼を飛ばしても、新しく足した不正な依頼は見つかる。
        var prior=s.DiagnosticSnapshot();ContractValidation.Validate(prior,null);
        var bad=ContractJson.Copy(prior);var last=bad.Requests[^1];
        bad.Requests=[..bad.Requests,last with{Id=new("request:forged-by-test"),Intent=last.Intent with{RequestId=new("request:forged-by-test"),WorkSeconds=last.Intent.WorkSeconds+1}}];
        Assert.ThrowsAny<InvalidDataException>(()=>NewGameWorld.Validate(bad,prior));
    }

    // 直す前の道の計画（全地点をなめて次を選ぶ）。速くした計画と同じ道になることを確かめる。
    static string[] OldPlan(ContractWorld w,string from,string to,HashSet<string> blocked,long departure)
    {
        var distance=BranchRoutes.GraphNodes(w).ToDictionary(n=>n.Id,_=>long.MaxValue,StringComparer.Ordinal);
        distance[from]=0;var paths=new Dictionary<string,string[]>(StringComparer.Ordinal){{from,[]}};var visited=new HashSet<string>();
        while(true){var current=distance.Where(p=>!visited.Contains(p.Key)&&p.Value<long.MaxValue).OrderBy(p=>p.Value).ThenBy(p=>p.Key,StringComparer.Ordinal).FirstOrDefault();
            if(current.Key==null)throw new InvalidDataException("No known traversable path");if(current.Key==to)return paths[to];visited.Add(current.Key);
            foreach(var e in BranchRoutes.GraphEdges(w).Where(e=>!blocked.Contains(e.Id)&&(e.From==current.Key||e.To==current.Key)).OrderBy(e=>e.Id,StringComparer.Ordinal)){
                var next=e.From==current.Key?e.To:e.From;var d=checked(current.Value+TravelWorld.End(w,e,departure+current.Value,true)-(departure+current.Value));var path=paths[current.Key].Append(e.Id).ToArray();
                if(d<distance[next]||d==distance[next]&&string.CompareOrdinal(string.Join("/",path),string.Join("/",paths.GetValueOrDefault(next,[])))<0){distance[next]=d;paths[next]=path;}}
        }
    }

    [Fact]
    public void FasterRoutesHealthAndDefectionChecksGiveTheSameAnswers()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var nodes=BranchRoutes.GraphNodes(w);var edges=BranchRoutes.GraphEdges(w);
        for(int i=0;i<6;i++)
        {
            var a=nodes[(i*37)%nodes.Length].Id;var b=nodes[(i*91+13)%nodes.Length].Id;var blocked=edges.Where((e,k)=>k%(29+i)==0).Select(e=>e.Id).ToHashSet(StringComparer.Ordinal);
            string[]? expected=null,actual=null;
            try{expected=OldPlan(w,a,b,blocked,w.ElapsedSeconds);}catch(InvalidDataException){}
            try{actual=BranchRoutes.Plan(a,b,blocked,true,w,w.ElapsedSeconds);}catch(InvalidDataException){}
            Assert.Equal(expected,actual);
        }
        // 判断の制限だけを調べても、すべての制限を数えた答えと同じ。
        foreach(var c in w.Characters.Take(300))Assert.Equal(PersonalHealth.Can(w,c.Id,HealthLimit.Judgment,w.ElapsedSeconds+90*86400L),PersonalHealth.CanJudge(w,c.Id,w.ElapsedSeconds+90*86400L));
        // 動揺した家を一度に数えても、家ごとに調べた答えと同じ。
        for(int y=1560,m=6;m<=9;m++){w.ElapsedSeconds=NationalStrategy.MonthStart(w,y,m);w.CommitSequence+=5;NationalStrategy.Apply(w);}
        var s=w.Strategy!;long now=w.ElapsedSeconds;
        foreach(var f in w.NewGame!.Start.Factions.Select(f=>f.Id))
            Assert.Equal((s.Successions??[]).Any(x=>x.House.Value==f&&x.At>now-365*86400L)||s.Outcomes.Where(o=>o.At>now-90*86400L).Any(o=>s.Campaigns.Single(c=>c.Id==o.Campaign) is var c&&(c.Attacker.Value==f&&!o.Captured||c.Defender.Value==f&&o.Captured)),StrategyDefections.Shaken(s,f,now));
    }

    [Fact]
    public void AnNpcCastellanResendsALostReportOnceItLearnsOfTheFailure()
    {
        var s=new WorldSession(NewGameWorld.Create(NewGameWorld.Scenario(HistoricalStartCatalog.Current),[],Head.Value,42,historicalCatalog:HistoricalStartCatalog.Current,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,worldArmyPolicy:NpcArmies.CompactCombatPolicy,economyPolicy:CastleAdministration.Policy));
        Good(s.ManageCastellan(new(new("request:report-appointment"),CastellanAction.Appoint,Site,Agent),s.PlayerControl,s.Revision));
        Advance(s,86400);
        var paper=s.DiagnosticSnapshot().CastellanReports!.Papers.Single();Assert.Equal(Agent,paper.Sender);
        s.DiagnosticTransport((m,t)=>MessageFlow.Fail(m,paper.Id));Advance(s,s.Seconds+4);
        var w=s.DiagnosticSnapshot();
        var failure=w.Messaging.Messages.Single(m=>m.Kind=="delivery-failure"&&w.Messaging.Attempts.Any(a=>a.Id==m.Body&&a.MessageId==paper.Id));
        // 城主が不達を知った世界（知らせを開いた）に、一覧の印を付けて手番を回す。
        if(!MessageFlow.Knows(w.Messaging,failure.Id,Agent.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(failure.Id,Agent.Value,w.ElapsedSeconds,failure.Id)];
        var without=ContractJson.Copy(w);CastellanReports.Tick(without);Assert.Single(without.CastellanReports!.Papers);
        w.NewGame=w.NewGame! with{NationalSystems=[NationalSystems.CastellanResend]};
        CastellanReports.Tick(w);
        var resent=w.CastellanReports!.Papers.Single(p=>p.Original==paper.Id);
        Assert.Equal(paper.Observation,resent.Observation);Assert.Equal(paper.Sender,resent.Sender);Assert.Contains(w.Messaging.Messages,m=>m.Id==resent.Id);
        CastellanReports.Tick(w);Assert.Equal(2,w.CastellanReports!.Papers.Length);
        CastellanReports.Validate(w);
        // 一覧の印の無い世界では、NPC の再送の原本は認めない。
        var forged=ContractJson.Copy(w);forged.NewGame=forged.NewGame! with{NationalSystems=null};
        Assert.Throws<InvalidDataException>(()=>CastellanReports.Validate(forged));
    }
}
