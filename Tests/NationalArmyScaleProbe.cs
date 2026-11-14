using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;
namespace HakutaiKagami.Tests;
// 全国の開始で、各家の当主が隣の家へ敵対を表明して一斉に軍を出したときの重さと保存復旧（HAKUTAI_ARMY_SCALE=1 のときだけ）。
[Trait("Tier","Probe")]
public sealed class NationalArmyScaleProbe(ITestOutputHelper o)
{
    [Fact]
    public void ManyHousesMarchAtOnceAndRestoreToTheSameFuture()
    {
        if(Environment.GetEnvironmentVariable("HAKUTAI_ARMY_SCALE")!="1")return;
        var lines=new List<string>();void Log(string s){lines.Add(s);o.WriteLine(s);File.WriteAllLines(Environment.GetEnvironmentVariable("HAKUTAI_ARMY_OUT")!,lines);}
        bool guard=WorldSession.GuardTurnSnapshots;WorldSession.GuardTurnSnapshots=false;
        var sw=System.Diagnostics.Stopwatch.StartNew();
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Log($"作成 {sw.ElapsedMilliseconds}ms 人物{w.Characters.Length} 拠点{w.Definitions.Sites.Length}");
        var castles=w.NewGame!.Start.Castles.Where(c=>c.Faction!=null).ToArray();var owner=castles.ToDictionary(c=>c.Id,c=>c.Faction!);
        var own=w.NewGame.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;int declared=0;
        sw.Restart();
        foreach(var f in w.NewGame.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            if(f.Id==own)continue;
            var target=NationalStrategy.Neighbors(f.Home).Select(n=>owner.GetValueOrDefault(n)).FirstOrDefault(x=>x!=null&&x!=f.Id&&x!=own);
            if(target==null)continue;
            if(int.TryParse(Environment.GetEnvironmentVariable("HAKUTAI_ARMY_LIMIT"),out var limit)&&declared>=limit)break;
            w.CommitSequence++;try{FactionStances.Apply(w,new(new("decision:scale/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(f.Id))[..12]),FactionStanceAction.DeclareHostile,new(f.Id),new(target),0,1000000),new(f.Head));declared++;}catch(InvalidDataException){}
        }
        Log($"敵対の表明 {declared}家 {sw.ElapsedMilliseconds}ms");
        var s=new WorldSession(w);s.SetPaused(false);
        sw.Restart();NewGameWorldTests.At(s,(s.Seconds/NpcArmies.Cadence+1)*NpcArmies.Cadence+60);var a=s.DiagnosticSnapshot();
        Log($"出陣まで（次の毎時＋1分） {sw.ElapsedMilliseconds}ms 軍{a.NpcArmies!.Musters.Length} 行軍{a.NpcArmies.Marches.Length} 部隊{a.Units.Length}");
        if(Environment.GetEnvironmentVariable("HAKUTAI_ARMY_SHORT")=="1"){Log(Prof.Report());Prof.Reset();sw.Restart();NewGameWorldTests.At(s,s.Seconds+600);Log($"10分 {sw.ElapsedMilliseconds}ms");Log(Prof.Report());WorldSession.GuardTurnSnapshots=guard;return;}
        sw.Restart();NewGameWorldTests.At(s,s.Seconds+3600);var b=s.DiagnosticSnapshot();
        Log($"1時間 {sw.ElapsedMilliseconds}ms {ContractJson.CanonicalHash(b)} 軍{b.NpcArmies!.Musters.Length} 行軍{b.NpcArmies.Marches.Length} 帰還{b.NpcArmies.Returns.Length} 交戦{b.WorldClashes?.Steps.Length}");
        sw.Restart();var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(b)));Log($"保存から戻す {sw.ElapsedMilliseconds}ms 大きさ{ContractJson.Write(b).Length/1024}KB");
        sw.Restart();foreach(var x in new[]{s,restored})NewGameWorldTests.At(x,b.ElapsedSeconds+600);
        Log($"10分×2 {sw.ElapsedMilliseconds}ms 同じ未来={ContractJson.Write(s.DiagnosticSnapshot()).AsSpan().SequenceEqual(ContractJson.Write(restored.DiagnosticSnapshot()))}");
        WorldSession.GuardTurnSnapshots=guard;
    }
}
