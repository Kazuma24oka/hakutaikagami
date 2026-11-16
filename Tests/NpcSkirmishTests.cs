using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// NPC どうしの近くの交戦（NpcSkirmish、2026-11-14）：本人の軍が出ていないとき、敵対する NPC の軍が出会うと、遠方の合戦と同じ接触の規則で戦い、
// 損害は部隊に残り、崩れた軍は帰る（大将が捕らえられれば散る）。出会う距離は検査のためだけに広げる。
public sealed class NpcSkirmishTests(ITestOutputHelper o)
{
    [Fact]
    public void HostileNpcArmiesThatMeetFightAndTheBrokenSideWithdraws()
    {
        bool guard=WorldSession.GuardTurnSnapshots;WorldSession.GuardTurnSnapshots=false;long contact=NpcArmies.ContactMm;
        try{
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Assert.True(NationalSystems.On(w,"NpcSkirmish"));
        var player=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var owner=w.NewGame.Start.Castles.Where(c=>c.Faction!=null).ToDictionary(c=>c.Id,c=>c.Faction!);
        // 境を接する二つの家が互いに敵対を表明し、両方の当主がすぐ出陣できる組を選ぶ。
        ContractWorld Declare(ContractWorld x,string from,string to){x.CommitSequence++;FactionStances.Apply(x,new(new("decision:test/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(from))[..12]),FactionStanceAction.DeclareHostile,new(from),new(to),0,30*86400L),HouseSuccession.Head(x,new(from)));return x;}
        ContractWorld? chosen=null;
        foreach(var f in w.NewGame.Start.Factions.Where(f=>f.Id!=player).OrderBy(f=>f.Id,StringComparer.Ordinal))
            foreach(var t in NationalStrategy.Neighbors(f.Home).Select(n=>owner.GetValueOrDefault(n)).Where(x=>x!=null&&x!=f.Id&&x!=player).Distinct())
            {
                ContractWorld trial;try{trial=Declare(Declare(ContractJson.Copy(w),f.Id,t!),t!,f.Id);}catch(InvalidDataException){continue;}
                if(NpcArmies.Candidate(trial,HouseSuccession.Head(trial,new(f.Id)),new(f.Id))!=null&&NpcArmies.Candidate(trial,HouseSuccession.Head(trial,new(t!)),new(t!))!=null){chosen=trial;break;}
                if(chosen!=null)break;
            }
        w=chosen??throw new InvalidOperationException("互いに出陣できる組がありません");
        NpcArmies.ContactMm=500_000_000; // 500km（検査だけ）
        var s=new WorldSession(w);
        NewGameWorldTests.At(s,NpcDormancy.Interval+120);
        var now=s.DiagnosticSnapshot();o.WriteLine($"軍 {now.NpcArmies!.Musters.Length}");
        var k=Assert.Single(now.NpcArmies.Skirmishes!);
        o.WriteLine($"交戦 崩れ A={k.Battle.AttackerBroke} B={k.Battle.DefenderBroke} 損害 {k.Battle.AttackerLost}/{k.Battle.DefenderLost} 捕縛 {k.Capture?.Person.Value}");
        Assert.Equal(k.Battle,StrategyBattles.Fight(now,k.Id,k.Battle.AttackerSoldiers,k.Battle.DefenderSoldiers));
        // 損害は部隊の兵に残る。
        var ma=now.NpcArmies.Musters.Single(m=>m.Id==k.A);long left=now.Units.Where(u=>u.Army==NewGameMilitary.LedgerIdentity(ma.Owner,ma.Site).Army).Sum(u=>(long)u.Soldiers);
        Assert.True(left<=100-k.Battle.AttackerLost+2,$"{left}");
        // 崩れた軍は目的をやめる（帰る、または大将が捕らえられて散る）。
        var broken=k.Battle.AttackerBroke?k.A:k.Battle.DefenderBroke?k.B:(EntityId?)null;
        if(broken is {} id){var m=now.NpcArmies.Musters.Single(x=>x.Id==id);Assert.True(NpcArmies.BrokeInSkirmish(now,m));}
        if(k.Capture is {} cap)Assert.True(BattleCaptivity.HeldAt(now,cap.Person,now.ElapsedSeconds,now.CommitSequence+1));
        // 保存から戻して同じ。
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(now)));
        foreach(var x in new[]{s,restored})NewGameWorldTests.At(x,now.ElapsedSeconds+600);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
        }finally{WorldSession.GuardTurnSnapshots=guard;NpcArmies.ContactMm=contact;}
    }
}
