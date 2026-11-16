using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// 区切りの間にすれ違った軍（SkirmishPassing、2026-11-15）：今は離れていても、直前の1時間のうちに出会う距離まで近づいていれば出会ったとする。
public sealed class SkirmishPassingTests(ITestOutputHelper o)
{
    [Fact]
    public void ArmiesThatPassedEachOtherBetweenChecksStillMeet()
    {
        bool guard=WorldSession.GuardTurnSnapshots;WorldSession.GuardTurnSnapshots=false;long contact=NpcArmies.ContactMm;
        try{
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Assert.True(NationalSystems.On(w,"SkirmishPassing"));
        var player=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var owner=w.NewGame.Start.Castles.Where(c=>c.Faction!=null).ToDictionary(c=>c.Id,c=>c.Faction!);
        ContractWorld Declare(ContractWorld x,string from,string to){x.CommitSequence++;FactionStances.Apply(x,new(new("decision:test/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(from))[..12]),FactionStanceAction.DeclareHostile,new(from),new(to),0,30*86400L),HouseSuccession.Head(x,new(from)));return x;}
        ContractWorld? chosen=null;
        foreach(var f in w.NewGame.Start.Factions.Where(f=>f.Id!=player).OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            foreach(var t in NationalStrategy.Neighbors(f.Home).Select(n=>owner.GetValueOrDefault(n)).Where(x=>x!=null&&x!=f.Id&&x!=player).Distinct())
            {
                ContractWorld trial;try{trial=Declare(Declare(ContractJson.Copy(w),f.Id,t!),t!,f.Id);}catch(InvalidDataException){continue;}
                if(NpcArmies.Candidate(trial,HouseSuccession.Head(trial,new(f.Id)),new(f.Id))!=null&&NpcArmies.Candidate(trial,HouseSuccession.Head(trial,new(t!)),new(t!))!=null){chosen=trial;break;}
            }
            if(chosen!=null)break;
        }
        NpcArmies.ContactMm=1; // 出会わせずに進める
        var s=new WorldSession(chosen!);NewGameWorldTests.At(s,NpcDormancy.Interval+1800);
        var now=s.DiagnosticSnapshot();var ms=now.NpcArmies!.Musters.OrderBy(m=>m.Id.Value,StringComparer.Ordinal).ToArray();Assert.Equal(2,ms.Length);
        // 二つの軍が近づいてすれ違い、離れ始めるまで進める。
        for(int h=0;h<12&&D(now.ElapsedSeconds)<=D(now.ElapsedSeconds-300);h++){NewGameWorldTests.At(s,s.Seconds+1800);now=s.DiagnosticSnapshot();}
        long D(long at){var a=NpcArmies.Position(now,NewGameMilitary.LedgerIdentity(ms[0].Owner,ms[0].Site).Army,at);var b=NpcArmies.Position(now,NewGameMilitary.LedgerIdentity(ms[1].Owner,ms[1].Site).Army,at);return (long)Math.Sqrt((double)(a.EastMm-b.EastMm)*(a.EastMm-b.EastMm)+(double)(a.NorthMm-b.NorthMm)*(a.NorthMm-b.NorthMm));}
        long at=now.ElapsedSeconds,start=Math.Max(ms[0].At,ms[1].At);
        var past=Enumerable.Range(1,12).Select(k=>at-k*300L).Where(t=>t>=start).Select(D).ToArray();
        o.WriteLine($"今 {D(at)} 過去 {string.Join(",",past)}");
        Assert.NotEmpty(past);long closest=past.Min();
        Assert.True(closest<D(at),"すれ違っていない");
        NpcArmies.ContactMm=closest+1; // 過去の最も近い距離だけで出会う（平方根の切り捨ての分を足す）
        Assert.NotNull(NpcArmies.Encounter(now));
        now.NewGame=now.NewGame! with{NationalSystems=now.NewGame.NationalSystems!.Where(x=>x!="SkirmishPassing").ToArray()};
        Assert.Null(NpcArmies.Encounter(now)); // 一覧を持たない世界は今の距離だけ
        }finally{WorldSession.GuardTurnSnapshots=guard;NpcArmies.ContactMm=contact;}
    }
}
