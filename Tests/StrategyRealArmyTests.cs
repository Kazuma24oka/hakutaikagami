using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// 全国の家の戦略で、操作する人物の近くの城への出陣を実際の軍勢で行う（StrategyRealArmies、2026-11-14）。
// 出陣の決め方（月の初めのくじ）は既存の検査で見ているので、ここでは近くの城への出陣の記録と敵対の表明を置いてから、
// NPC の軍がその城へ出陣し、着いて留まり終えたときに決着することと、途中の保存から戻して同じ未来になることを確かめる。
public sealed class StrategyRealArmyTests(ITestOutputHelper o)
{
    [Fact]
    public void ANearbyCampaignMarchesARealArmyAndSettlesOnArrival()
    {
        // 全国の開始で何時間も進めるので、検査用の写しの書き換え確認は切る（実際のゲームと同じ）。
        bool guard=WorldSession.GuardTurnSnapshots;WorldSession.GuardTurnSnapshots=false;
        try{Run();}finally{WorldSession.GuardTurnSnapshots=guard;}
    }
    void Run()
    {
        var log=Environment.GetEnvironmentVariable("HAKUTAI_TEST_LOG");void L(string x){o.WriteLine(x);if(log!=null)File.AppendAllText(log,x+Environment.NewLine);}
        var sw=System.Diagnostics.Stopwatch.StartNew();
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(NationalSystems.On(w,"StrategyRealArmies"));
        var (campaign,attacker,target)=PlaceNearbyCampaign(w);
        L($"出陣 {attacker.Value} → {target.Value}");
        var s=new WorldSession(w);
        // 当主は毎時の初めに出陣を考える。
        NewGameWorldTests.At(s,NpcDormancy.Interval+60);
        var mustered=s.DiagnosticSnapshot();
        var army=NpcArmies.Musters(mustered).Single(m=>m.Objective?.Claim.Declaration==NationalStrategy.DeclarationId(campaign));
        Assert.Equal(target,army.Objective!.TargetSite);
        Assert.Equal(attacker,army.Owner);
        Assert.DoesNotContain(mustered.Strategy!.Outcomes,x=>x.Campaign==campaign);
        L($"出陣まで {sw.ElapsedMilliseconds}ms");
        // 着いて留まり終えるまで進める（最大12時間）。
        ContractWorld now=mustered;
        for(int h=0;h<12&&!now.Strategy!.Outcomes.Any(x=>x.Campaign==campaign);h++){NewGameWorldTests.At(s,s.Seconds+3600);now=s.DiagnosticSnapshot();L($"{h+2}時間 {sw.ElapsedMilliseconds}ms");}
        var outcome=now.Strategy!.Outcomes.Single(x=>x.Campaign==campaign);
        var m=NpcArmies.Musters(now).Single(m=>m.Id==army.Id);
        Assert.Equal(NpcArmies.HoldEnd(now,m),outcome.At); // 留まり終えた秒に決着する（月の初めではない）
        Assert.Equal(outcome.Captured,NationalStrategy.Owner(now,target,now.ElapsedSeconds,now.CommitSequence)==attacker); // 落ちれば城は攻めた家のもの
        L($"決着 {(outcome.Captured?"落城":"退く")} {sw.ElapsedMilliseconds}ms");
        // 保存から戻して同じ未来。
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(now)));
        foreach(var x in new[]{s,restored})NewGameWorldTests.At(x,now.ElapsedSeconds+600);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }

    // 操作する人物から30km以内の、本人の家でない城と、その城の隣に城を持つ家で、当主が今すぐ出陣できる組を探して、出陣の記録と敵対の表明を置く。
    static (EntityId Campaign,EntityId Attacker,EntityId Target) PlaceNearbyCampaign(ContractWorld w)
    {
        w.Strategy??=new(1,NationalStrategy.Policy,[],[]);
        var owners=NationalStrategy.Owners(w,w.Strategy);var near=NationalStrategy.NearPlayerSites(w);
        var player=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        foreach(var site in near.OrderBy(x=>x,StringComparer.Ordinal))
        {
            var defender=owners.GetValueOrDefault(site);if(defender==null||defender==player)continue;
            foreach(var n in NationalStrategy.Neighbors(site))
            {
                var attacker=owners.GetValueOrDefault(n);if(attacker==null||attacker==defender||attacker==player)continue;
                var head=HouseSuccession.Head(w,new(attacker));
                var id=new EntityId("campaign:national/test/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(site+attacker))[..12]);
                var trial=ContractJson.Copy(w);trial.CommitSequence++;
                try{FactionStances.Apply(trial,new(NationalStrategy.DeclarationId(id),FactionStanceAction.DeclareHostile,new(attacker),new(defender),0,NationalStrategy.RealDays*86400L),head);}catch(InvalidDataException){continue;}
                trial.Strategy=trial.Strategy! with{Campaigns=[..trial.Strategy.Campaigns,new(id,new(attacker),new(defender),new(site),0,trial.CommitSequence,"border"){Real=true}]};
                if(NpcArmies.Candidate(trial,head,new(attacker)) is not {} objective||objective.TargetSite!=new EntityId(site))continue;
                w.CommitSequence++;FactionStances.Apply(w,new(NationalStrategy.DeclarationId(id),FactionStanceAction.DeclareHostile,new(attacker),new(defender),0,NationalStrategy.RealDays*86400L),head);
                w.Strategy=w.Strategy with{Campaigns=[..w.Strategy.Campaigns,new(id,new(attacker),new(defender),new(site),0,w.CommitSequence,"border"){Real=true}]};
                ContractValidation.Validate(w);
                return (id,new(attacker),new(site));
            }
        }
        throw new InvalidOperationException("近くに出陣できる組が見つかりません");
    }
}
