using HakutaiKagami.Core.Proto;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// 試作版の合戦（Core/Proto/ProtoWar.cs）。途中の出来事はチェックポイントに書く（Tests/Checkpoints.cs）。
public sealed class ProtoWarTests(ITestOutputHelper o)
{
    static readonly ProtoMap Map = ProtoMap.Load();
    const long Day = ProtoWorld.Day, Hour = ProtoWorld.Hour;

    static void Run(ProtoWorld w, long seconds, Checkpoints cp)
    {
        int seen = w.Facts.Count;
        for (long t = 0; t < seconds; t += 600)
        {
            w.Advance(Math.Min(600, seconds - t));
            foreach (var f in w.Facts.Skip(seen)) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}");
            seen = w.Facts.Count;
        }
    }

    // 誰も何もしなければ：当主が死に、訃報が届き、沢渡の誘いを受けた城主が謀叛し、軍議・行軍・狼煙・合戦まで進む。
    [Fact]
    public void WithoutThePlayerTheRebellionIsRaisedAndFought()
    {
        using var cp = new Checkpoints("試作版の謀叛と合戦", o);
        var w = new ProtoWorld(Map);
        Run(w, 5 * Day, cp);
        var ids = w.Facts.Select(f => f.Id).ToArray();
        cp.Expect(ids.Contains("lord-dead"), "3日目の朝に当主が死ぬ");
        cp.Expect(w.HeadOf("takano") == "takano-masayuki", "後継ぎが家を継ぐ");
        cp.Expect(ids.Contains("rebellion"), "訃報と沢渡の誘いを受けた城主が謀叛する");
        cp.Expect(ids.Contains("council"), "手切れの書状が届いて軍議が開かれる（本人がいなければ大将が決める）");
        cp.Expect(ids.Contains("signal") || ids.Any(i => i.StartsWith("nukegake:")), "合図の狼煙か抜け駆けで合戦になる");
        cp.Expect(ids.Any(i => i.StartsWith("rout:")), "どちらかの隊が崩れる");
        cp.Expect(ids.Contains("rebellion-crushed") || ids.Contains("expedition-failed") || ids.Any(i => i.StartsWith("withdrawn:")) || ids.Contains("held") || ids.Contains("draw"), "合戦の決着がつく");
        cp.Expect(!w.Knows(w.Player, "invite"), "沢渡の誘いは、本人は知らない（密かな書状）");
    }

    // 訃報を自分で届けて、その場で説き伏せれば、謀叛は起きない。
    [Fact]
    public void PersuadingTheCastellanPreventsTheRebellion()
    {
        using var cp = new Checkpoints("試作版の説き伏せ", o);
        var w = new ProtoWorld(Map);
        Run(w, 2 * Day + 5 * Hour + 120 - w.Second, cp);
        var notice = w.OffersHere.FirstOrDefault(t => t.Kind == "death-notice");
        cp.Expect(notice != null, "当主の死の後、後継ぎから訃報を届ける頼みが出る");
        w.Do("accept:" + notice!.Id);
        w.GoToTask(notice.Id); w.Advance(w.TaskEta(notice)!.Value + 60);
        cp.Log(w.Do("step:" + notice.Id));
        var persuade = w.Actions().FirstOrDefault(a => a.Id == "persuade:katsumoto");
        cp.Expect(persuade != null, "訃報を届けた後、城主を説き伏せられる");
        cp.Log(w.Do(persuade!.Id));
        Run(w, 3 * Hour, cp);
        cp.Expect(w.Facts.Any(f => f.Id == "katsumoto-loyal"), "説き伏せられた城主は新しい当主に従う");
        cp.Expect(!w.Facts.Any(f => f.Id == "rebellion"), "謀叛は起きない");
    }

    // 軍議に出れば自分の陣と出立と合図を選べる。敵の隊は見える範囲に入るまで見えず、物見を出すと印が付く。
    [Fact]
    public void TheCouncilFogAndScoutsWork()
    {
        using var cp = new Checkpoints("試作版の軍議と物見", o);
        var w = new ProtoWorld(Map);
        while (!w.Facts.Any(f => f.Id == "defiance") && w.Second < 4 * Day) Run(w, 600, cp);
        cp.Expect(w.War.Phase == "軍議待ち", "手切れの書状で軍議待ちになる");
        var council = w.OffersHere.FirstOrDefault(t => t.Kind == "council") ?? w.Tasks.First(t => t.Kind == "council");
        if (council.State == TaskState.Offered && w.OffersHere.Contains(council)) w.Do("accept:" + council.Id);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L); w.Advance(Math.Max(0, w.War.CouncilAt - w.Second) + 60);
        cp.Expect(w.CouncilOpen, "刻限に鷹野城にいれば軍議に加われる");
        foreach (var line in w.Council().Opinions) cp.Log(line);
        cp.Log(w.DecideCouncil("south-hill", ProtoWorld.Departures[0], ProtoWorld.Signals[0]));
        cp.Expect(w.PostOf(w.Player.Id).Name == "丘の陣" && w.War.PlayerAtCouncil, "本人の選んだ陣が通る（陣の名前は地形から付く）");
        var katsumoto = w.Person("iwakiri-katsumoto");
        cp.Expect(!w.Visible.Contains(katsumoto), "遠くの敵の隊は見えない");
        var hill = w.PositionOf("south-hill"); w.MoveTo(hill.X * 100L, hill.Y * 100L);
    }

    // 物見は自動：隊はいつも周りを探っていて、知略の高い武将は少し遠くの森の伏兵にも気づく（Core/Proto/ProtoChaos.cs の AutoScout）。
    [Fact]
    public void WiseCommandersSpotAmbushersInTheForest()
    {
        using var cp = new Checkpoints("試作版の自動の物見", o);
        var w = new ProtoWorld(Map); w.SetupTsuriPractice();
        var kosaka = w.Person("kosaka-shigetada"); var iguchi = w.Person("iguchi-yashichi"); var me = w.Player;
        cp.Log($"見つける見込み（5分ごと）：小坂（知略{kosaka.Wits}）{w.ScoutChance(kosaka)}%　井口（知略{iguchi.Wits}）{w.ScoutChance(iguchi)}%");
        cp.Expect(w.ScoutChance(kosaka) > w.ScoutChance(iguchi) * 3, "知略が高いほど見つけやすい");
        // 井口を森の中に、小坂を300m離れた森の外に置く（小坂は物見上手なので350mまで探れる）。本人は遠くへ。
        (iguchi.X, iguchi.Y) = (kosaka.X, kosaka.Y); kosaka.X = iguchi.X + 300_00; kosaka.Y = iguchi.Y - 150_00;
        me.X = 2600_00; me.Y = 1900_00; me.Path = []; me.PathIndex = 0; w.War.Target.Remove(me.Id);
        cp.Expect(w.TerrainName(iguchi.X, iguchi.Y) == "森" && w.TerrainName(kosaka.X, kosaka.Y) != "森" && !w.Visible.Contains(iguchi), "森の中の井口は、離れた所からは見えない");
        for (int i = 0; i < 60 && !w.Facts.Any(f => f.Id.StartsWith("spotted:" + iguchi.Id)); i++) { foreach (var u in new[] { kosaka, iguchi }) { u.Path = []; u.PathIndex = 0; w.War.Target.Remove(u.Id); } w.Advance(60); }
        var found = w.Facts.FirstOrDefault(f => f.Id.StartsWith("spotted:" + iguchi.Id));
        cp.Log(found == null ? "見つけられなかった" : ProtoWorld.Clock(found.At) + "　" + found.Text);
        cp.Expect(found != null && found.Text.Contains(kosaka.Name), "小坂の物見が、森の中の井口に気づく");
        cp.Expect(!w.InAmbush(iguchi.Id), "気づかれた隊は伏兵でなくなる");
        w.Advance(20 * 60);
        cp.Log("印：" + string.Join("、", w.Markers.Select(m => $"{w.Person(m.Unit).Name} {ProtoWorld.Clock(m.SeenAt)} {m.By}")));
        cp.Expect(w.Markers.Any(m => m.Unit == iguchi.Id && m.By.Contains(kosaka.Name)), "味方の見つけた敵は、知らせが届いてから印になる");
    }

    [Fact]
    public void TheWarIsTheSameWhateverTheStepSizeAndAfterSaving()
    {
        static string Look(ProtoWorld w) => string.Join("|", w.People.Select(p => $"{p.Id}:{p.Active},{p.House},{p.X},{p.Y},{p.Soldiers},{p.Morale}")) + "#" + string.Join("|", w.Facts.Select(f => f.Id)) + "#" + w.War.Phase + w.War.Result;
        var a = new ProtoWorld(Map); a.Advance(4 * Day);
        var b = new ProtoWorld(Map); b.Advance(Day + 7); b.Advance(2 * Day); b.Advance(Day - 7);
        Assert.Equal(Look(a), Look(b));
        var c = new ProtoWorld(Map); c.Advance(3 * Day + 3 * Hour);
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(c.Save(), ProtoMap.Json);
        var d = ProtoWorld.Load(Map, System.Text.Json.JsonSerializer.Deserialize<ProtoWorld.Saved>(bytes, ProtoMap.Json)!);
        c.Advance(Day - 3 * Hour); d.Advance(Day - 3 * Hour);
        Assert.Equal(Look(c), Look(d));
        Assert.Equal(Look(a), Look(c));
    }

    // 行軍と合戦の様子を10分ごとに書く（確かめの記録。決着の形が変わったら、ここを読んで理由を見る）。
    [Fact]
    public void TheMarchAndBattleAreLogged()
    {
        using var cp = new Checkpoints("試作版の行軍の様子", o);
        var w = new ProtoWorld(Map);
        w.Advance(3 * Day + 3 * Hour + 30 * 60 - w.Second);
        for (int i = 0; i < 24; i++)
        {
            w.Advance(600);
            cp.Log(ProtoWorld.Clock(w.Second) + "　" + w.War.Phase + "　" + string.Join("、", w.Units.Where(u => u.House is "takano" or "iwakiri").Select(u =>
                $"{u.Name}({u.Soldiers}/{u.Morale}) {u.X / 100},{u.Y / 100} {u.Doing}")));
        }
        cp.Expect(w.War.Phase is "合戦" or "終わり", "夜明けの後に合戦になる");
    }

    // 本人が軍議に出て、今夜のうちに南の丘へ出る案で戦ったときの成り行きを書く（勝ち負けは確かめない。釣り合いを見るため）。
    [Fact]
    public void ThePlayersPlanIsLogged()
    {
        using var cp = new Checkpoints("試作版の本人の案の合戦", o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L); w.Advance(Math.Max(0, w.War.CouncilAt - w.Second) + 60);
        cp.Log(w.DecideCouncil("south-hill", ProtoWorld.Departures[0], ProtoWorld.Signals[0]));
        var hill = w.PositionOf("south-hill"); w.MoveTo(hill.X * 100L, hill.Y * 100L);
        int seen = w.Facts.Count;
        while (w.War.Result == "" && w.Second < 12 * Day) { w.Advance(300); StarveQuickly(w); foreach (var f in w.Facts.Skip(seen)) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}"); seen = w.Facts.Count; }
        cp.Log("結果：" + w.War.Result + "　本人の兵" + w.Player.Soldiers + "　勲功" + w.Merit);
        cp.Expect(w.War.Result != "", "決着がつく");
    }

    // 軍議に加わると兵を預かって出陣の任務が出る。見えている敵の隊には攻めかかれる（合図の前なら自分の抜け駆け）。敵の隊とは話せない。
    [Fact]
    public void ThePlayerMustersAndCanAttack()
    {
        using var cp = new Checkpoints("試作版の出陣と攻めかかり", o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        cp.Expect(w.CouncilOpen, "城にいて大将と家老がそろえば、すぐ軍議に加われる（刻限まで待たない）");
        cp.Expect(w.Player.Soldiers == 0, "軍議の前は兵を持っていない");
        w.DecideCouncil("road-mouth", ProtoWorld.Departures[0], ProtoWorld.Signals[1]);
        cp.Expect(w.Player.Soldiers == 30, "軍議で兵30を預かる");
        var muster = w.OpenTasks.FirstOrDefault(t => t.Kind == "muster");
        cp.Expect(muster != null && muster.Speech.Contains("の陣"), "出陣の任務が出る（口上つき）");
        cp.Expect(w.TaskPlace(muster!) == "道の口", "出陣の行き先は陣の名前で出る");
        var katsumoto = w.Person("iwakiri-katsumoto");
        w.MoveTo(katsumoto.X, katsumoto.Y);
        for (int i = 0; i < 2000 && !w.Actions().Any(a => a.Id.StartsWith("attack:")); i++) w.Advance(30); // 城内の敵は狙えない。城を出てくる（朝4時）まで待つ
        cp.Log($"{ProtoWorld.Clock(w.Second)}　{w.War.Phase}　本人 {w.Player.X / 100},{w.Player.Y / 100} 兵{w.Player.Soldiers} {w.Player.Doing}　勝元 {katsumoto.X / 100},{katsumoto.Y / 100} {katsumoto.Doing} 城内{w.Sheltered(katsumoto)}　見える{w.SeesEnemy(katsumoto)}　崩れ{w.War.Routed.Contains(w.Player.Id)}");
        var attack = w.Actions().FirstOrDefault(a => a.Id.StartsWith("attack:"));
        cp.Expect(attack != null, "見えている敵の隊に攻めかかれる");
        cp.Expect(!w.Nearby.Any(p => p.House == "iwakiri"), "敵の隊とは話せない");
        cp.Log(w.Do(attack!.Id));
        cp.Expect(w.War.Phase == "合戦" && w.Facts.Any(f => f.Id == "nukegake:" + w.Player.Id), "合図の前に攻めると自分の抜け駆けになる");
    }

    // 本人の隊がぶつかると押し合いができ、構え・かかれ・戦法が効く（Core/Proto/ProtoClash.cs）。
    [Fact]
    public void ThePlayerCommandsInAClash()
    {
        using var cp = new Checkpoints("試作版の采配", o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        w.DecideCouncil("road-mouth", ProtoWorld.Departures[0], ProtoWorld.Signals[1]);
        w.Do("tactic-auto:off"); // この検査は、本人が自分で戦法を使う場合（合戦の窓の「自動」を外した形）
        { var foe = Map.Site("iwakiri-castle"); w.War.Camp["takano"] = [foe.X * 100L, foe.Y * 100L + 500_00]; } // 本陣を城の近くに置いたことにする（兵糧が届く所で戦う）
        var katsumoto = w.Person("iwakiri-katsumoto"); w.MoveTo(katsumoto.X, katsumoto.Y);
        for (int i = 0; i < 2000 && !w.Actions().Any(a => a.Id.StartsWith("attack:")); i++) w.Advance(30); // 城内の敵は狙えない。城を出てくる（朝4時）まで待つ
        w.Do(w.Actions().First(a => a.Id.StartsWith("attack:")).Id);
        for (int i = 0; i < 120 && w.ClashOf(w.Player.Id) == null; i++) w.Advance(60);
        var clash = w.ClashOf(w.Player.Id);
        cp.Expect(clash != null, "攻めかかった相手とぶつかると押し合いができる　" + ProtoWorld.Clock(w.Second));
        cp.Log($"気力 {w.War.Ki}　総士気 鷹野{w.SpiritOf("takano")} 岩切{w.SpiritOf("iwakiri")}");
        // かかれ：気力を25使って、すぐ押し返す。
        { int ki = w.War.Ki; cp.Expect(w.CanKakare, "ぶつかっていて気力が25あれば強攻できる"); cp.Log(w.Do("kakare:me"));
          cp.Expect(w.War.Ki == ki - 25, "強攻は気力を25使う"); }
        // 構え：同じ世界を二つに分けて、防御のほうが兵の減りが少ない。
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(w.Save(), ProtoMap.Json);
        var guard = ProtoWorld.Load(Map, System.Text.Json.JsonSerializer.Deserialize<ProtoWorld.Saved>(bytes, ProtoMap.Json)!);
        int start = w.Player.Soldiers; guard.Do("stance:防御"); w.Advance(5 * 60); guard.Advance(5 * 60);
        cp.Log($"5分で 白兵 -{start - w.Player.Soldiers}　防御 -{start - guard.Player.Soldiers}");
        cp.Expect(start - guard.Player.Soldiers < start - w.Player.Soldiers, "防御の構えは兵の減りが少ない");
        // 戦法：ゲージがたまると使える（本人は技能が無いので鼓舞）。
        // 戦法の仕組みだけを確かめるので、ゲージがたまるまで崩れないように兵と士気を足しておく。
        for (int i = 0; i < 30 && guard.GaugeOf(guard.Player.Id) < 100 && !guard.War.Routed.Contains(guard.Player.Id); i++) { guard.Player.Soldiers = Math.Max(guard.Player.Soldiers, 30); guard.Player.Morale = Math.Max(guard.Player.Morale, 60); guard.Advance(60); }
        guard.War.Chaos.Remove(guard.Player.Id); guard.War.ChaosUntil.Remove(guard.Player.Id); guard.War.Routed.Remove(guard.Player.Id); // 戦法の仕組みだけを見るので、混乱と崩れは外しておく
        int morale = guard.Player.Morale; var said = guard.Do("tactic:me"); cp.Log(said + $"　ゲージ {guard.GaugeOf(guard.Player.Id)}　士気 {morale}→{guard.Player.Morale}");
        cp.Expect(said.Contains("鼓舞") && guard.GaugeOf(guard.Player.Id) == 0, "戦法のゲージがたまると使える");
        // 「自動」を入れると、ゲージがたまりしだい自分で使う。
        guard.Do("tactic-auto:on"); int used = guard.Facts.Count(f => f.Text.Contains(guard.Player.Name + "が戦法"));
        for (int i = 0; i < 40 && guard.ClashOf(guard.Player.Id) != null && guard.Facts.Count(f => f.Text.Contains(guard.Player.Name + "が戦法")) == used; i++) { guard.Player.Soldiers = Math.Max(guard.Player.Soldiers, 30); guard.Player.Morale = Math.Max(guard.Player.Morale, 60); guard.Advance(60); }
        cp.Log(guard.Facts.LastOrDefault(f => f.Text.Contains("が戦法"))?.Text ?? "使わなかった");
        cp.Expect(guard.Facts.Count(f => f.Text.Contains(guard.Player.Name + "が戦法")) > used, "自動を入れると、たまりしだい戦法を使う");
    }

    // 駆け引き（Core/Proto/ProtoTactics.cs）：側面・背後は強い、退き道をふさがれると壊滅、森で伏せると見えない、追うかどうかは気性と知略。
    [Fact]
    public void FlanksCutOffAmbushAndPursuitWork()
    {
        using var cp = new Checkpoints("試作版の駆け引き", o);
        var w = new ProtoWorld(Map); w.Advance(3 * Day + 6 * Hour + 5 * 60 - w.Second);
        cp.Expect(w.War.Phase == "合戦", "6時の合図の後は合戦　" + ProtoWorld.Clock(w.Second));
        var k = w.Person("iwakiri-katsumoto"); var kosaka = w.Person("kosaka-shigetada"); var heir = w.Person("takano-masayuki"); var iguchi = w.Person("iguchi-yashichi");
        // 向き：勝元が東（+X）を向いているとき、東から＝正面、北から＝側面、西から＝背後。
        w.War.Facing[k.Id] = [1, 0];
        long kx = k.X, ky = k.Y;
        kosaka.X = kx + 50_00; kosaka.Y = ky; var front = w.FlankOf(k, kosaka);
        kosaka.X = kx; kosaka.Y = ky + 50_00; var side = w.FlankOf(k, kosaka);
        kosaka.X = kx - 50_00; kosaka.Y = ky; var back = w.FlankOf(k, kosaka);
        cp.Log($"正面 {front.Mul}　側面 {side.Mul}　背後 {back.Mul}");
        cp.Expect(front.Kind == "正面" && side.Kind == "側面" && back.Kind == "背後" && back.Mul > side.Mul && side.Mul > front.Mul, "背後 ＞ 側面 ＞ 正面");
        // 退き道：井口の本陣（岩切城）への道の先に鷹野の隊がいると壊滅、いなければ本陣へ退く。
        var castle = Map.Site("iwakiri-castle"); long cx = castle.X * 100L, cy = castle.Y * 100L;
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(w.Save(), ProtoMap.Json);
        ProtoWorld Copy() => ProtoWorld.Load(Map, System.Text.Json.JsonSerializer.Deserialize<ProtoWorld.Saved>(bytes, ProtoMap.Json)!);
        foreach (var blocked in new[] { true, false })
        {
            var v = Copy(); var ig = v.Person(iguchi.Id); var ko = v.Person(kosaka.Id);
            ig.X = cx + 500_00; ig.Y = cy; v.War.Routed.Remove(ig.Id); v.War.Recovered.Remove(ig.Id); ig.Active = true; ig.Soldiers = 80; int before = ig.Soldiers; // 戦いの成り行きに関係なく同じ形で試す
            v.War.Routed.Remove(ko.Id); ko.Active = true; ko.Soldiers = Math.Max(ko.Soldiers, 50);
            if (blocked) { ko.X = cx + 300_00; ko.Y = cy; } else { ko.X = cx + 1500_00; ko.Y = cy + 1500_00; }
            foreach (var u in v.Units.Where(u => u.House == "takano" && u.Id != ko.Id)) { u.X = cx + 3000_00; u.Y = cy + 3000_00; }
            v.Rout(ig);
            cp.Log($"{(blocked ? "ふさがれた" : "開いている")}：兵 {before}→{ig.Soldiers}　{ig.Doing}");
            cp.Expect(blocked ? !ig.Active && ig.Soldiers < before / 2 : ig.Active && ig.Soldiers == before, blocked ? "退き道をふさがれると壊滅する" : "退き道が開いていれば本陣へ退く");
        }
        // 伏せる：森の中で動かずにいる本人は、200m先の敵からは見えず、60mまで来ると見える。
        var forest = FindForest(w); var me = w.Player; me.X = forest.X; me.Y = forest.Y; me.Path = []; me.PathIndex = 0; me.Soldiers = 30;
        k.X = forest.X + 200_00; k.Y = forest.Y; bool far = w.InSight(k, me);
        k.X = forest.X + 60_00; bool near = w.InSight(k, me);
        cp.Expect(w.Hidden(me) && !far && near, "森で伏せると近くまで見えない");
        // 追うかどうか：崩れて退く敵を、勇猛な正之は追い、慎重な小坂は深追いしない。
        var r = w.Person(iguchi.Id); r.Active = true; r.Soldiers = 40; if (!w.War.Routed.Contains(r.Id)) w.War.Routed.Add(r.Id);
        heir.X = r.X + 200_00; heir.Y = r.Y; kosaka.X = r.X - 200_00; kosaka.Y = r.Y;
        cp.Expect(w.KeepsChasing(heir, r) && !w.KeepsChasing(kosaka, r), "勇猛な者は追い、慎重な者は追わない");
    }
    // 釣り野伏の基本の仕組み（決まった筋書きではなく、置き方だけ決めて仕組みで起こす）：
    // 開けた所で、囮の本人が勇猛な井口とぶつかってから、森の縁に伏せた小坂の横を通って下がる。井口は追い、小坂が目の前で奇襲し、本人が向き直って挟むと井口は崩れる。
    // ほかの隊（正之・勝元）はこの試しから外す。
    [Fact]
    public void TheFeignedRetreatDrawsTheEnemyIntoTheAmbush()
    {
        using var cp = new Checkpoints("試作版の釣り野伏", o);
        var w = new ProtoWorld(Map); w.SetupTsuriPractice();
        var kosaka = w.Person("kosaka-shigetada"); var iguchi = w.Person("iguchi-yashichi"); var me = w.Player;
        w.Advance(120);
        cp.Expect(w.Hidden(kosaka), "小坂は伏せている");
        cp.Expect(w.ClashOf(me.Id) != null, "本人と井口がぶつかる");
        void State(string t) => cp.Log($"　　{t} {ProtoWorld.Clock(w.Second)} 本人 兵{me.Soldiers} 士気{me.Morale}　井口 兵{iguchi.Soldiers} 士気{iguchi.Morale} {iguchi.Doing}　小坂 {kosaka.Doing}　井口まで本人{Math.Sqrt(Math.Pow(me.X - iguchi.X, 2) + Math.Pow(me.Y - iguchi.Y, 2)) / 100:F0}m 小坂{Math.Sqrt(Math.Pow(kosaka.X - iguchi.X, 2) + Math.Pow(kosaka.Y - iguchi.Y, 2)) / 100:F0}m");
        State("ぶつかった");
        // わざと退く：小坂のすぐ北を通って西へ。
        var lure = w.TsuriLure(); w.War.Target.Remove(me.Id); w.MoveTo(lure.X, lure.Y);
        int seen = w.Facts.Count;
        for (int i = 0; i < 20 && !w.Facts.Any(f => f.Id.StartsWith("surprise:" + kosaka.Id)); i++)
        { w.Advance(60); foreach (var f in w.Facts.Skip(seen)) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}"); seen = w.Facts.Count; State("退く"); }
        cp.Expect(w.Facts.Any(f => f.Id.StartsWith("surprise:" + kosaka.Id)), "追ってきた井口を、伏せていた小坂が奇襲する");
        // 向き直って挟む。
        if (w.Actions().Any(a => a.Id == "attack:" + iguchi.Id)) w.Do("attack:" + iguchi.Id);
        for (int i = 0; i < 30 && !w.War.Routed.Contains(iguchi.Id) && iguchi.Active; i++)
        { w.Advance(60); foreach (var f in w.Facts.Skip(seen)) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}"); seen = w.Facts.Count; State("挟む"); }
        cp.Expect(w.War.Routed.Contains(iguchi.Id) || !iguchi.Active, "挟まれた井口は崩れる");
    }

    static (long X, long Y) FindForest(ProtoWorld w)
    {
        var p = w.PositionOf("forest-edge");
        for (int d = 0; d < 400; d += 10) for (int a = 0; a < 360; a += 15)
        {
            long x = p.X * 100L + (long)(Math.Cos(a * Math.PI / 180) * d * 100), y = p.Y * 100L + (long)(Math.Sin(a * Math.PI / 180) * d * 100);
            if (w.Map.GroundAtCm(x, y) == Ground.Forest) return (x, y);
        }
        throw new InvalidOperationException("森が見つからない");
    }

    // 軍議で作戦と役目を決め、「指図どおりに動く」を選ぶと、総勢で着陣の地まで進み、着いてから役目ごとに分かれ、役目どおりに動く（Core/Proto/ProtoOrders.cs）。
    [Theory]
    [InlineData("釣り野伏", "囮")]
    [InlineData("釣り野伏", "伏兵")]
    [InlineData("挟撃", "回り込み")]
    [InlineData("正面から当たる", "先手")]
    public void TheArmyMarchesTogetherAndActsOnRoles(string plan, string role)
    {
        using var cp = new Checkpoints($"試作版の軍令_{plan}_{role}", o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        cp.Log(w.DecideCouncil(null, ProtoWorld.Departures[0], ProtoWorld.Signals[0], plan, role));
        foreach (var or in w.Orders()) cp.Log($"軍令　{or.Name}：{or.Role}　{or.Text}");
        cp.Expect(w.Orders().Any(x => x.Unit == w.Player.Id && x.Role == role), "自分の役目が軍令に出る");
        cp.Log(w.Do("order:follow"));
        var heir = w.Person("takano-masayuki"); int seen = w.Facts.Count; bool together = true, sawBody = false;
        while (w.War.Result == "" && w.Second < 12 * Day)
        {
            w.Advance(60); StarveQuickly(w);
            if (w.Body.Any()) { sawBody = true; if (w.Body.Any(u => Math.Sqrt(Math.Pow(u.X - heir.X, 2) + Math.Pow(u.Y - heir.Y, 2)) > 400_00)) together = false; }
            foreach (var f in w.Facts.Skip(seen).Where(f => !f.Text.Contains("杭") && !f.Text.Contains("怪しい") && !f.Text.Contains("越後屋") && !f.Text.Contains("小競り"))) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}");
            seen = w.Facts.Count;
        }
        w.Advance(60); // 決着の次の分に、作戦の最後の段が決まる
        cp.Log($"結果 {w.War.Result}　本人 兵{w.Player.Soldiers}　総士気 鷹野{w.SpiritOf("takano")} 岩切{w.SpiritOf("iwakiri")}");
        var steps = w.PlanSteps().ToArray();
        cp.Log($"作戦 {plan}　{w.PlanState}：" + string.Join("、", steps.Select(x => x.Text + (x.State != "" ? "＝" + x.State : ""))));
        cp.Expect(steps.Take(2).All(x => x.State == "成功"), "出立と着陣の段は成功になる");
        cp.Expect(w.PlanState != "" && (w.PlanState == "成功") == steps.All(x => x.State == "成功") && (w.PlanState == "失敗") == steps.Any(x => x.State == "失敗"), "決着までに作戦の成否が決まる（全部の段が成功なら成功、どれかが失敗なら失敗）");
        cp.Expect(w.Facts.Any(f => f.Id == "marched") && w.Facts.Any(f => f.Id == "split"), "総勢で城を出て、着陣してから分かれる");
        cp.Expect(sawBody && together, "着陣までは、総勢が大将から400m以内にまとまっている");
        cp.Expect(w.War.Result != "", "決着がつく");
    }

    // 大目標と撤退条件（Core/Proto/ProtoGoals.cs）：撤退条件は状況で初めから決まっていて、軍議では足せるが減らせない。敵も持つ。
    [Theory]
    [InlineData("敵を崩す", "正面から当たる", "先手", "足さない", 0)]
    [InlineData("敵を崩す", "釣り野伏", "伏兵", "全軍士気が半分を切る", 0)]
    [InlineData("陣を守り抜く", "陣を固める", "森の守り", "昼までに決まらない", 1)]
    [InlineData("陣を守り抜く", "陣を固める", "丘の守り", "足さない", 0)]
    public void TheArmyHasAGoalAndRetreatRules(string goal, string plan, string role, string add, int dep)
    {
        using var cp = new Checkpoints($"試作版の大目標_{goal}_{plan}_{add}_{dep}", o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        cp.Expect(ProtoWorld.PlansOf(goal).Contains(plan), "大目標ごとに選べる作戦がある");
        cp.Log(w.DecideCouncil(null, ProtoWorld.Departures[dep], ProtoWorld.Signals[0], plan, role, goal, add));
        var rules = w.RetreatRules().ToArray();
        cp.Log("鷹野の撤退条件：" + string.Join("、", rules.Select(r => r.Text + (r.Added ? "（足した）" : ""))));
        cp.Expect(rules.Count(r => !r.Added) >= 3 && rules.Any(r => r.Id == "head") && rules.Any(r => r.Id == "spirit0"), "初めから決まっている撤退条件がある");
        cp.Expect(rules.Any(r => r.Id == (goal == "陣を守り抜く" ? "lost-post" : "dusk")), "大目標に合う撤退条件が付く");
        cp.Expect(rules.Count(r => r.Added) == (add == "足さない" ? 0 : 1), "軍議で足した条件だけが増える");
        cp.Log(w.Do("order:follow"));
        int seen = w.Facts.Count; bool loggedRebel = false; string rebelGoal = "";
        while (w.War.Result == "" && w.War.Siege == null && w.Second < 5 * Day) // 野の戦いの決着まで（岩切勢が城へ退けば攻城になる）
        {
            w.Advance(60);
            if (!loggedRebel && w.RetreatRules("iwakiri").Any()) { loggedRebel = true; rebelGoal = w.GoalOf("iwakiri"); cp.Log($"岩切の大目標：{w.GoalOf("iwakiri")}　撤退条件：" + string.Join("、", w.RetreatRules("iwakiri").Select(r => r.Text + (r.Added ? "（足した）" : "")))); }
            foreach (var f in w.Facts.Skip(seen).Where(f => !f.Text.Contains("杭") && !f.Text.Contains("怪しい") && !f.Text.Contains("越後屋") && !f.Text.Contains("小競り"))) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}");
            seen = w.Facts.Count;
        }
        cp.Log($"結果 {(w.War.Siege != null ? "攻城へ" : w.War.Result)}　{ProtoWorld.Clock(w.Second)}　本人 兵{w.Player.Soldiers}　総士気 鷹野{w.SpiritOf("takano")} 岩切{w.SpiritOf("iwakiri")}");
        cp.Log($"作戦 {plan}　{w.PlanState}：" + string.Join("、", w.PlanSteps().Select(x => x.Text + (x.State != "" ? "＝" + x.State : ""))));
        cp.Expect(loggedRebel && rebelGoal == (dep == 0 ? "敵を崩す" : "陣を守り抜く"), "岩切勢も大目標と撤退条件を持つ（鷹野が夜のうちに出ていれば討って出る）");
        cp.Expect(w.War.Result != "" || w.War.Siege != null, "決着がつく（岩切勢が城へ退いたら攻城になる）");
        if (w.War.Siege != null) cp.Expect(w.Facts.Any(f => f.Id == "siege") && !w.Retreated("iwakiri"), "岩切勢は（退き貝で、または崩れて）城へ逃げ込み、城に籠もった");
        foreach (var side in new[] { "takano", "iwakiri" })
            if (w.Retreated(side)) cp.Expect(w.Facts.Any(f => f.Id == "retreat:" + side) && w.RetreatRules(side).Count(r => r.Met) == 1, $"{side} は撤退条件を満たして退き貝を鳴らした");
        cp.Expect(w.War.Result is not ("退いた" or "退かせた") || w.Retreated("takano") || w.Retreated("iwakiri"), "退いた決着は、撤退条件を満たしたときだけ");
    }

    // 伏兵・奇襲・混乱（Core/Proto/ProtoChaos.cs）：見つかっていない隊は伏兵になる（森は1分、森の外は5分）。
    // 伏兵がぶつかると奇襲（一度きりの打撃と混乱+30）。混乱が100になると3分動けない。
    [Fact]
    public void UnseenUnitsAmbushAndConfusedUnitsCannotMove()
    {
        using var cp = new Checkpoints("試作版の伏兵と混乱", o);
        var w = new ProtoWorld(Map); w.SetupTsuriPractice();
        var kosaka = w.Person("kosaka-shigetada"); var iguchi = w.Person("iguchi-yashichi"); var me = w.Player;
        // 本人は敵から遠い野へ。小坂は森の中。
        me.X = 2600_00; me.Y = 1900_00; me.Path = []; me.PathIndex = 0; w.War.Target.Remove(me.Id);
        cp.Expect(w.TerrainName(me.X, me.Y) != "森" && w.TerrainName(kosaka.X, kosaka.Y) == "森", "本人は森の外、小坂は森の中にいる");
        iguchi.X = kosaka.X + 900_00; w.War.Target.Remove(iguchi.Id); iguchi.Path = []; iguchi.PathIndex = 0;
        w.Advance(150);
        cp.Expect(w.InAmbush(kosaka.Id) && !w.InAmbush(me.Id), "森の中は1分で伏兵になる。森の外は、まだならない");
        w.Advance(5 * 60);
        cp.Expect(w.InAmbush(me.Id), "森の外でも、5分見つからなければ伏兵になる");
        // 井口を小坂のすぐ前へ置くと、小坂が奇襲をかける。
        iguchi.X = kosaka.X + 60_00; iguchi.Y = kosaka.Y - 70_00; int before = iguchi.Soldiers;
        for (int i = 0; i < 10 && !w.Facts.Any(f => f.Id.StartsWith("surprise:" + kosaka.Id)); i++) w.Advance(60);
        cp.Log(w.Facts.Last().Text + $"　井口 兵{before}→{iguchi.Soldiers} 混乱{w.ChaosOf(iguchi.Id)}");
        cp.Expect(w.Facts.Any(f => f.Id.StartsWith("surprise:" + kosaka.Id)) && (w.ChaosOf(iguchi.Id) >= 30 || w.Confused(iguchi.Id)), "伏兵がぶつかると奇襲になり、相手の混乱が30以上たまる");
        cp.Expect(!w.InAmbush(kosaka.Id), "仕掛けた隊は伏兵でなくなる");
        // 混乱：100になると3分のあいだ動けない。
        w.War.Routed.Remove(iguchi.Id); iguchi.Morale = 100; iguchi.Soldiers = 80; w.War.Chaos[iguchi.Id] = 99;
        for (int i = 0; i < 5 && !w.Confused(iguchi.Id); i++) w.Advance(60);
        cp.Expect(w.Confused(iguchi.Id), "混乱が100になると混乱する");
        long x = iguchi.X, y = iguchi.Y; w.War.Target[iguchi.Id] = me.Id; w.Advance(60);
        cp.Expect(iguchi.X == x && iguchi.Y == y, "混乱している隊は動けない");
        w.Advance(ProtoWorld.ChaosTime);
        cp.Expect(!w.Confused(iguchi.Id), "3分で混乱が解ける");
    }

    // 軍議の戦場図に、役目ごとの勲功の見込みが出る。決着で、役目を果たしたかと勝ち負けで勲功が決まる。
    [Fact]
    public void TheCouncilShowsExpectedMeritAndTheBattleAwardsIt()
    {
        using var cp = new Checkpoints("試作版の勲功の見込み", o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        var prop = w.Proposal("敵を崩す", "釣り野伏", "伏兵");
        foreach (var pc in prop.Pieces) cp.Log($"{pc.Name}　{pc.Role}　{pc.Terrain}　{pc.Effect}　功{pc.Merit}");
        cp.Expect(prop.Pieces.First(x => x.Role == "囮").Merit > prop.Pieces.First(x => x.Role == "伏兵").Merit, "危ない役（囮）は伏兵より見込みが大きい");
        w.DecideCouncil(null, ProtoWorld.Departures[0], ProtoWorld.Signals[0], "釣り野伏", "伏兵", "敵を崩す"); w.Do("order:follow");
        int merit = w.Merit;
        while (w.War.Result == "" && w.Second < 12 * Day) { w.Advance(60); StarveQuickly(w); }
        int atEnd = w.Merit; w.Advance(60); // 出陣の任務の分は、ここまでに入っている
        cp.Expect(w.Merit == atEnd, "決着しただけでは合戦の勲功は入らない（城へ戻ってからの論功行賞で入る）");
        for (int i = 0; i < 24 * 60 && !w.AwardsOpen; i++) w.Advance(60);
        w.Do("awards:take");
        cp.Log($"結果 {w.War.Result}　勲功 {merit}→{w.Merit}　" + string.Join(" ", w.Journal.Where(e => e.Text.Contains("勲功")).Select(e => e.Text).TakeLast(2)));
        cp.Expect(w.Merit - merit is > 0 and <= 22, "合戦で働けば、見込みに戦果（6まで）を足した勲功が入る");
    }

    // 決着しても軍は解かず、総勢で鷹野城へ戻ってから論功行賞（Core/Proto/ProtoHome.cs）。
    // 負けても、隊が崩れても名を呼ばれる。本人が城にいなければ1日待ち、いないまま済ませて勲功だけ届く。
    [Theory]
    [InlineData("そのまま")]
    [InlineData("隊が崩れた")]
    [InlineData("城に戻らない")]
    public void TheArmyReturnsHomeBeforeTheAwards(string mode)
    {
        using var cp = new Checkpoints("試作版の帰陣と論功行賞_" + mode, o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        w.DecideCouncil(null, ProtoWorld.Departures[0], ProtoWorld.Signals[0], "釣り野伏", "伏兵", "敵を崩す"); w.Do("order:follow");
        var me = w.Player; int merit = w.Merit;
        while (w.War.Result == "" && w.Second < 12 * Day) // 攻城になれば、総大将の決めた攻め方で終わるまで
        {
            w.Advance(60); StarveQuickly(w);
            if (mode == "隊が崩れた" && w.War.Phase == "合戦" && !w.War.Routed.Contains(me.Id)) { me.Soldiers = 4; w.War.Routed.Add(me.Id); }
        }
        merit = w.Merit; // 出陣の任務の分は、ここまでに入っている
        cp.Log($"決着 {ProtoWorld.Clock(w.Second)}　{w.War.Result}　本人 兵{me.Soldiers}{(w.War.Routed.Contains(me.Id) ? "（崩れた）" : "")}");
        cp.Expect(w.War.Result != "" && w.Returning, "決着しても、軍はまだ解かれていない");
        int held = me.Soldiers; w.Advance(120);
        cp.Expect(w.Merit == merit && me.Soldiers == held, "決着の場では、勲功は入らず、預かった兵もそのまま");
        var site = Map.Site(w.AwardsSite); cp.Log($"論功行賞の場所 {site.Name}");
        // 城に戻らない：論功行賞のある城とは別の城へ行ってしまう。
        if (mode == "城に戻らない") { w.Do("order:free"); var away = Map.Site(w.AwardsSite == "takano-castle" ? "iwakiri-castle" : "takano-castle"); w.MoveTo(away.X * 100L, away.Y * 100L); }
        bool together = false; long far = 0;
        for (int i = 0; i < 24 * 60 && !w.AwardsReady; i++)
        {
            w.Advance(60);
            int host = w.People.Count(u => w.InReturn(u)); together |= host >= 2;
            if (i % 20 == 0) cp.Log($"　{ProtoWorld.Clock(w.Second)}　総勢{host}　" + string.Join("　", w.Awards().Select(a => { var u = w.Person(a.Unit); return $"{u.Name} {u.Doing}"; })));
            far = Math.Max(far, w.Second);
        }
        var heir = w.Person(w.AwardsHostId); // 大将が捕らえられていれば家老が行う
        cp.Log($"{ProtoWorld.Clock(w.Second)}　大将 {heir.X / 100},{heir.Y / 100} {w.SiteOf(heir)} {heir.Active}　城 {castle.X},{castle.Y}　" + string.Join(" ", w.Facts.TakeLast(3).Select(f => f.Id + ":" + f.Text)));
        cp.Expect(w.AwardsReady && w.SiteOf(heir) == w.AwardsSite, "大将が城に着くと論功行賞になる（落とした城か、鷹野城）");
        if (w.War.Result is "退かせた" or "守り抜いた" or "引き分け") cp.Expect(together, "野で勝ったときは、総勢でまとまって戻る");
        var rows = w.Awards().ToArray();
        foreach (var a in rows) cp.Log($"{a.Name}　{a.Role}　兵{a.Now}/{a.Start}　{(a.Did ? "役目を果たした" : a.Broken ? "隊が崩れた" : "—")}　勲功{a.Merit}");
        cp.Expect(rows.Length == 3 && rows.Any(a => a.Unit == me.Id), "出陣した者はみな名を呼ばれる（負けても、隊が崩れても）");
        int see = w.WarMerit(me.Id);
        if (mode == "城に戻らない")
        {
            cp.Expect(!w.AwardsOpen && w.Returning, "本人が城にいなければ、論功行賞は開かない（待っている）");
            for (int i = 0; i < 30 * 60 && w.Returning; i++) w.Advance(60);
            cp.Log(string.Join("　", w.Journal.TakeLast(2).Select(e => e.Text)));
            cp.Expect(!w.Returning && w.Merit == merit + see, "1日待っても来なければ、いないまま済ませて勲功だけ届く");
            cp.Expect(w.Tasks.Where(t => t.Kind == "awards").All(t => t.State == TaskState.Done), "来なかったことで任務の失敗にはしない");
        }
        else
        {
            for (int i = 0; i < 6 * 60 && !w.AwardsOpen; i++) w.Advance(60); // 崩れた隊は少し遅れて着く
            cp.Log($"{ProtoWorld.Clock(w.Second)}　本人 {w.SiteOf(me)} {me.Doing}");
            cp.Expect(w.AwardsOpen, "委任の本人は軍と一緒に城へ戻り、論功行賞が開く");
            w.Do("awards:take");
            cp.Log(string.Join("　", w.Journal.TakeLast(2).Select(e => e.Text)));
            cp.Expect(w.Merit == merit + see, "論功行賞で勲功を受ける");
        }
        cp.Expect(me.Soldiers == 0 && !w.Returning, "論功行賞が済むと、預かった兵を返して軍が解かれる");
    }

    // 籠城になったら、城の兵糧と士気を尽きかけにして、次の朝6時に開城させる（攻城の長さを待たずに、そのあとの流れを確かめる検査で使う）。
    static void StarveQuickly(ProtoWorld w) { if (w.War.Siege is { } s && w.War.Result == "") { s.Store = 0; foreach (var r in w.Garrison) r.Morale = Math.Min(r.Morale, 10); } }

    // 総大将への頼み：遠ければ使番が運ぶので、返事が戻るまで進めて、その返事を返す（Core/Proto/ProtoCamp.cs の SendAsk）。
    static string Ask(ProtoWorld w, string action)
    {
        var r = w.Do(action);
        if (!r.StartsWith("使番を")) return r;
        for (int i = 0; i < 120 && w.War.Asks.Count > 0; i++) w.Advance(60);
        return "（使番）" + w.Journal.Last().Text;
    }

    // 攻城（Core/Proto/ProtoSiege.cs、案は Docs/Proposals/SIEGE_REDESIGN.md）。どの検査も、野の戦いのあと岩切勢が城へ籠もった所から始める。
    ProtoWorld ToSiege(Checkpoints cp)
    {
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        w.DecideCouncil(null, ProtoWorld.Departures[0], ProtoWorld.Signals[0], "釣り野伏", "伏兵", "敵を崩す"); w.Do("order:follow");
        while (w.War.Result == "" && w.War.Siege == null && w.Second < 5 * Day) w.Advance(60);
        cp.Log($"{ProtoWorld.Clock(w.Second)}　野の戦いのあと　結果「{w.War.Result}」　段「{w.War.Phase}」　" + string.Join("　", w.Journal.TakeLast(6).Select(e => e.Text)));
        cp.Expect(w.War.Siege != null && w.War.Result == "", "岩切勢が城へ退くと、決着にならず攻城になる");
        for (int i = 0; i < 6 * 60 && !w.SiegeCouncilOpen; i++) w.Advance(60);
        for (int i = 0; i < 3 * 60 && w.War.Result == "" && w.People.Any(u => u.House == "iwakiri" && u.Active && u.Soldiers > 0 && !w.Sheltered(u)); i++) w.Advance(60); // 城の外で戦っている隊も、城へ入りきるまで
        cp.Log($"{ProtoWorld.Clock(w.Second)}　籠城の始まり　城兵{w.Garrison.Sum(u => u.Soldiers)}（{string.Join("・", w.Garrison.Select(u => u.Name + u.Soldiers))}）　城の兵糧{w.CastleFoodDays}日　城の士気{w.CastleMorale}　攻め手{w.People.Where(u => u.House == "takano" && u.Soldiers > 0 && w.War.StartSoldiers.ContainsKey(u.Id)).Sum(u => u.Soldiers)}");
        return w;
    }
    static string SiegeLine(ProtoWorld w) => w.War.Siege is { } s
        ? $"{ProtoWorld.Clock(w.Second)}　門{s.Gate}　穴{w.OpenSectors}　付城{s.Forts.Count(f => f)}　城兵{w.Garrison.Sum(u => u.Soldiers)}　城の兵糧{w.CastleFoodDays}日　城の士気{w.CastleMorale}　本陣の兵糧{w.StockOf("takano")}日　鷹野の全軍士気{w.SpiritOf("takano")}　後詰「{s.Relief}」　攻め手 " + string.Join(" ", w.People.Where(u => u.House == "takano" && u.Soldiers > 0 && w.War.StartSoldiers.ContainsKey(u.Id)).Select(u => $"{u.Name}{u.Soldiers}"))
        : "";
    static void LogFacts(Checkpoints cp, ProtoWorld w, ref int seen)
    {
        foreach (var f in w.Facts.Skip(seen).Where(f => !f.Id.StartsWith("free:") && !f.Id.StartsWith("flank:") && !f.Id.StartsWith("spotted:") && !f.Id.StartsWith("tactic:") && !f.Text.Contains("市が立つ"))) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}");
        seen = w.Facts.Count;
    }

    // 包囲：城の士気は、兵糧が切れるまで下がらない。付城で囲みを閉じ、兵糧が切れて士気が折れると開く。何週間もかかる。
    [Fact]
    public void TheBlockadeTakesWeeks()
    {
        using var cp = new Checkpoints("試作版の攻城_包囲", o);
        var w = ToSiege(cp);
        cp.Log("　" + Ask(w, "siege:包囲"));
        int morale = w.CastleMorale, seen = w.Facts.Count; long start = w.Second; int moraleAt5 = -1;
        while (w.War.Result == "" && w.Second < start + 45 * Day)
        {
            w.Advance(60); LogFacts(cp, w, ref seen);
            if ((w.Second - start) % Day == 0) cp.Log("　" + SiegeLine(w));
            if (w.Second == start + 5 * Day) moraleAt5 = w.CastleMorale;
        }
        cp.Log($"結果「{w.War.Result}」　{(w.Second - start) / Day}日{(w.Second - start) % Day / Hour}時間");
        cp.Expect(w.Second - start >= 7 * Day || w.War.Result == "退いた", "包囲だけでは、7日のうちには落ちない");
        cp.Expect(moraleAt5 == -1 || moraleAt5 == morale, "兵糧があるうちは、城の士気は下がらない（打ち合いでも下がらない）");
        cp.Expect(w.War.Siege!.Forts.Any(f => f), "囲む隊が付城を築く");
        cp.Expect(w.War.Result != "", "攻城に決着がつく（45日のうちに）");
    }

    // 力攻め：城兵の10倍なら3日ほどで門が破れる。5倍では3日では破れない。3倍に届かなければ、総大将は力攻めを認めない。
    [Theory]
    [InlineData(10)]
    [InlineData(5)]
    [InlineData(2)]
    public void StormingNeedsTenfoldMen(int ratio)
    {
        using var cp = new Checkpoints("試作版の攻城_力攻め_" + ratio + "倍", o);
        var w = ToSiege(cp);
        int mine = w.People.Where(u => u.House == "takano" && u.Soldiers > 0 && w.War.StartSoldiers.ContainsKey(u.Id) && !w.War.Routed.Contains(u.Id)).Sum(u => u.Soldiers);
        var g = w.Garrison.ToArray(); int want = Math.Max(1, mine / ratio), total = g.Sum(u => u.Soldiers);
        foreach (var u in g) u.Soldiers = Math.Max(1, u.Soldiers * want / Math.Max(1, total)); // 城兵を、攻め手の 1/ratio にする
        w.War.Siege!.Store = w.Garrison.Sum(u => u.Soldiers) * 20;
        cp.Log($"攻め手{mine}　城兵{w.Garrison.Sum(u => u.Soldiers)}");
        string said = Ask(w, "siege:力攻め"); cp.Log("　" + said);
        if (ratio == 2) { cp.Expect(w.War.Siege.Method == "包囲" && said.Contains("無駄死に"), "3倍に届かなければ、総大将は力攻めを認めない"); return; }
        cp.Expect(w.War.Siege.Method == "力攻め", "力攻めと決まる");
        int seen = w.Facts.Count; long start = w.Second; int gateAt3 = -1;
        while (w.War.Result == "" && w.Second < start + 8 * Day)
        {
            w.Advance(60); LogFacts(cp, w, ref seen);
            if ((w.Second - start) % (6 * Hour) == 0 || w.Second - start <= Hour && (w.Second - start) % 300 == 0) cp.Log("　" + SiegeLine(w) + "　城方 " + string.Join(" ", w.People.Where(u => u.House == "iwakiri").Select(u => $"{u.Name}{u.Soldiers}{(w.Sheltered(u) ? "城" : "外")}{(w.War.Target.TryGetValue(u.Id, out var tg) ? "→" + tg : "")}「{u.Doing}」")));
            if (w.Second == start + 3 * Day) gateAt3 = w.War.Siege.Gate;
        }
        cp.Log("城方が受けた打撃：" + string.Join(" ", w.War.Hits.Where(h => w.Person(h.To).House == "iwakiri" && h.Loss > 0).TakeLast(30).Select(h => $"{ProtoWorld.Clock(h.At)}{w.Person(h.To).Name}-{h.Loss}{h.Kind}")));
        cp.Log($"結果「{w.War.Result}」　{(w.Second - start) / Day}日{(w.Second - start) % Day / Hour}時間　3日目の門{gateAt3}");
        if (ratio == 10) cp.Expect(w.War.Result == "鎮めた" && w.Second - start <= 5 * Day && w.Second - start >= 2 * Day, "10倍なら、2〜5日で門が破れて落ちる");
        else cp.Expect(gateAt3 > 0 || w.War.Result != "鎮めた", "5倍では、3日では門は破れない");
    }

    // 攻城の軍議で打つ手：勧告（兵糧・士気・後詰の当てで受けやすさが変わる。交渉中は攻めない）と調略（密書→内応の約束→合図、流言）。
    [Theory]
    [InlineData("勧告")]
    [InlineData("調略_約す")]
    [InlineData("調略_断る")]
    [InlineData("流言")]
    public void EnvoysAndPlotsWorkThroughTheSiegeCouncil(string mode)
    {
        using var cp = new Checkpoints("試作版の勧告と調略_" + mode, o);
        var w = ToSiege(cp);
        var sg = w.War.Siege!; var iguchi = w.Person("iguchi-yashichi"); var katsumoto = w.Person("iwakiri-katsumoto"); int seen = w.Facts.Count;
        void WaitTalks() { for (int i = 0; i < 4 * 60 && w.Talking; i++) w.Advance(60); LogFacts(cp, w, ref seen); }
        if (mode == "勧告")
        {
            cp.Log("帰参：" + Ask(w, "siege:勧告:帰参"));
            cp.Expect(!w.Talking, "勇猛な総大将は、押しているうちは帰参を認めない");
            cp.Log("切腹：" + Ask(w, "siege:勧告:切腹"));
            cp.Expect(w.Talking && sg.Method == "包囲", "使者を出すと包囲になり、交渉が始まる");
            cp.Log("交渉中に力攻め：" + Ask(w, "siege:力攻め"));
            cp.Expect(sg.Method == "包囲", "交渉中は攻めない");
            WaitTalks();
            cp.Expect(!w.Talking && w.War.Result == "" && w.Facts.Any(f => f.Id.StartsWith("envoy-back:")), "使者が戻る。兵糧のある城主は切腹の条件を受けない");
            cp.Log("同じ日にもう一度：" + Ask(w, "siege:勧告:退去"));
            cp.Expect(!w.Talking, "使者は1日に一度");
            for (int i = 0; i < 120 && !w.SiegeCouncilOpen; i++) w.Advance(60);
            sg.Store = w.Garrison.Sum(u => u.Soldiers) * 2; sg.ParleyAt = -1; cp.Log($"城の兵糧が残り{w.CastleFoodDays}日のとき 退去：" + Ask(w, "siege:勧告:退去")); WaitTalks();
            cp.Expect(w.War.Result == "開城させた" && !katsumoto.Active && iguchi.House == "takano", "退去：城主は城を明け渡して去り、城内の者は鷹野に戻る");
            cp.Expect(w.Awards().All(x => x.Unit != iguchi.Id) && w.Awards().Count() == 3, "降って戻った者は、論功行賞の鷹野勢には並ばない");
        }
        else if (mode == "流言")
        {
            int before = w.CastleMorale; cp.Log("流言：" + Ask(w, "siege:流言")); LogFacts(cp, w, ref seen);
            cp.Expect(w.CastleMorale == Math.Max(0, before - 10), "流言で城の士気が10下がる");
            cp.Log("同じ日にもう一度：" + Ask(w, "siege:流言"));
            cp.Expect(w.CastleMorale == Math.Max(0, before - 10), "流言は1日に一度");
        }
        else
        {
            iguchi.Loyalty = mode == "調略_約す" ? 95 : 10;
            cp.Log("調略：" + Ask(w, "siege:調略"));
            cp.Expect(w.PlotStage == "密書", "総大将が認めれば、城内の武将へ密書を忍ばせる");
            w.Advance(2 * 3600 + 120); LogFacts(cp, w, ref seen);
            cp.Expect(w.PlotStage == (mode == "調略_約す" ? "約束" : "断られた"), "2時間で返事。内応を約すかは、その武将が決める");
            int gate = sg.Gate, men = katsumoto.Soldiers;
            for (int i = 0; i < 120 && !w.SiegeCouncilOpen; i++) w.Advance(60);
            cp.Log("もう一度 調略：" + Ask(w, "siege:調略")); w.Advance(120); LogFacts(cp, w, ref seen);
            if (mode == "調略_約す")
            {
                cp.Expect(w.PlotStage == "済", "約束があれば、内応の合図を出せる（一度きり）");
                cp.Expect(w.Facts.Any(f => f.Id is "gate-broken" or "plot-found" or "plot-none") || w.War.Result == "開城させた", "合図の結果は、城主を捕らえて開城・門を開く・露見・動かない のどれか");
                cp.Log($"門 {gate}→{sg.Gate}　勝元の兵 {men}→{katsumoto.Soldiers}　井口：{iguchi.House} {iguchi.Doing}　結果「{w.War.Result}」");
            }
            else cp.Expect(w.PlotStage == "断られた" && sg.Gate == gate, "断られたら、合図は出せない");
        }
    }

    // 城の兵糧が残り3日を切ると、朝5時に朝駆けに出る。40分たつか、隊が崩れたら城へ引き上げる。囲んでいるだけでは、城の兵は出てこない。
    [Fact]
    public void TheStarvingGarrisonSalliesAtDawn()
    {
        using var cp = new Checkpoints("試作版の朝駆け", o);
        var w = ToSiege(cp);
        Ask(w, "siege:包囲");
        cp.Expect(w.War.Siege is { Method: "包囲" }, "包囲と決まる");
        w.Advance(3 * 3600);
        var foe = Map.Site("iwakiri-castle"); double Far(ProtoPerson u) => Math.Sqrt(Math.Pow(u.X - foe.X * 100.0, 2) + Math.Pow(u.Y - foe.Y * 100.0, 2)) / 100;
        var rebels = w.People.Where(u => u.House == "iwakiri" && u.Active).ToArray();
        cp.Expect(w.War.Clashes.Count == 0 && rebels.All(u => Far(u) <= 200), "囲んでいる間、城の兵は城から出ず、打ち合いも起きない");
        w.War.Siege!.Store = w.Garrison.Sum(u => u.Soldiers) * 2; // 兵糧が残り2日になったことにする
        int seen = w.Facts.Count;
        while (w.Second % Day != 5 * 3600 + 60) w.Advance(60);
        cp.Expect(w.Facts.Skip(seen).Any(f => f.Id.StartsWith("sally:")), "兵糧が残り3日を切ると、朝5時に朝駆けに出る");
        double farthest = 0;
        for (int i = 0; i < 90 && w.War.Result == ""; i++) { w.Advance(60); farthest = Math.Max(farthest, rebels.Where(u => u.Active).Select(Far).DefaultIfEmpty(0).Max()); }
        LogFacts(cp, w, ref seen);
        cp.Log($"いちばん遠くまで出た所 城から{farthest:0}m　結果「{w.War.Result}」　城方：" + string.Join("　", rebels.Select(u => $"{u.Name} 兵{u.Soldiers} {Far(u):0}m {u.Doing}")));
        cp.Expect(farthest > 350, "城から出て、囲みの隊へ向かう");
        cp.Expect(w.War.Result != "" || rebels.Where(u => u.Active && u.Soldiers > 0).All(u => Far(u) <= 260), "決着しなければ、城へ引き上げている");
    }

    // 攻め手の兵糧と本人の役目：小荷駄が2日ごとに鷹野城から本陣へ運ぶ（侍が付いていれば賊は襲わない）。兵のない本人は、朝に役目を申し付けられる。
    [Fact]
    public void TheConvoyFeedsTheCampAndThePlayerGetsDuties()
    {
        using var cp = new Checkpoints("試作版の攻城_小荷駄と役目", o);
        var w = ToSiege(cp);
        Ask(w, "siege:包囲");
        int seen = w.Facts.Count; long start = w.Second; var duties = new List<string>();
        while (w.War.Result == "" && w.Second < start + 5 * Day)
        {
            w.Advance(60); LogFacts(cp, w, ref seen);
            foreach (var t in w.Tasks.Where(t => t.Kind == "duty" && !duties.Contains(t.Id))) { duties.Add(t.Id); cp.Log($"{ProtoWorld.Clock(w.Second)}　役目の申し付け「{t.Title}」　{t.Speech}"); }
            if ((w.Second - start) % Day == 0) cp.Log("　" + SiegeLine(w));
        }
        cp.Expect(w.Facts.Any(f => f.Id.StartsWith("convoy-in:") || f.Id.StartsWith("robbery:konida:")), "小荷駄が本陣へ向かう（届くか、賊に奪われる）");
        cp.Expect(w.Player.Soldiers > 0 || duties.Count > 0, "兵のない本人は、朝に役目を申し付けられる");
        cp.Log("役目：" + string.Join("　", w.Tasks.Where(t => t.Kind == "duty").Select(t => $"{t.Title}＝{t.State}")) + "　" + string.Join(" / ", w.Journal.Where(e => e.Text.Contains("果たした") || e.Text.Contains("守れなかった")).Select(e => ProtoWorld.Clock(e.At) + e.Text)));
        cp.Expect(w.Player.Soldiers > 0 || w.Tasks.Any(t => t.Kind == "duty" && t.State == TaskState.Done), "委任の本人は、自分で役目の場所へ向かって果たす");
    }

    // 後詰：沢渡の隊が岩切方として城へ向かい、囲みの隊に当たる。破れるか城の決着で、沢渡へ引き上げ、家は元に戻る。
    [Fact]
    public void TheReliefArmyComesAndGoesHome()
    {
        using var cp = new Checkpoints("試作版の攻城_後詰", o);
        var w = ToSiege(cp);
        Ask(w, "siege:包囲");
        var nao = w.Person("sawatari-nobunao"); w.War.Siege!.Relief = "出陣"; w.War.Siege.ReliefAt = w.Second + 60; // 沢渡が後詰を出すと決めたことにする
        int seen = w.Facts.Count; long start = w.Second; bool fought = false;
        while (w.War.Result == "" && w.Second < start + 3 * Day && w.War.Siege.Relief != "破れた")
        {
            w.Advance(60); LogFacts(cp, w, ref seen); fought |= w.ClashOf(nao.Id) != null;
            if ((w.Second - start) % (3 * Hour) == 0) cp.Log($"　{ProtoWorld.Clock(w.Second)}　{nao.Name} {nao.House} 兵{nao.Soldiers} {nao.Doing}　後詰「{w.War.Siege.Relief}」");
        }
        w.Advance(120); // 決着の次の分に、後詰は引き上げる
        cp.Expect(fought, "後詰は囲みの隊に当たる");
        cp.Expect(w.War.Siege.Relief == "破れた" || w.War.Result != "", "後詰が破れるか、城が決着する");
        cp.Expect(nao.House == "sawatari", "引き上げると、沢渡の家に戻る");
    }

    // 本陣（Core/Proto/ProtoCamp.cs）：軍議で置く所を決め、着いた時から本陣。指示と兵糧は1,000mまで。崩れた隊は収容されて7割で戻る。取られると兵糧を失う。
    [Fact]
    public void TheCampFeedsCommandsSheltersAndCanBeTaken()
    {
        using var cp = new Checkpoints("試作版の本陣", o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        var def = w.CampDefault; long[] want = [def.X - 150_00, def.Y + 100_00];
        w.DecideCouncil(null, ProtoWorld.Departures[0], ProtoWorld.Signals[0], "正面から当たる", "後詰", "敵を崩す", null, new() { ["camp"] = want }); w.Do("order:follow");
        var plan = w.CampPlan!.Value;
        cp.Log($"総大将の案 {def.X / 100},{def.Y / 100}　本人の再提案 {want[0] / 100},{want[1] / 100}　決まった所 {plan.X / 100},{plan.Y / 100}");
        cp.Expect(Math.Abs(plan.X - want[0]) <= 60_00 && Math.Abs(plan.Y - want[1]) <= 60_00, "軍議で、本陣を置く所を再提案できる（総大将が聞き入れたとき）");
        cp.Expect(w.CampAt("takano") == null, "着くまでは、まだ本陣は無い（出てきた城が本陣の扱い）");
        var me = w.Player; var heir = w.Person("takano-masayuki"); var kosaka = w.Person("kosaka-shigetada"); var iguchi = w.Person("iguchi-yashichi");
        cp.Expect(w.Ordered(me) && w.Supplied(me), "本陣を置く前は、指示も兵糧も届く");
        for (int i = 0; i < 600 && w.CampAt("takano") == null; i++) w.Advance(60);
        var camp = w.CampAt("takano")!.Value;
        cp.Log($"{ProtoWorld.Clock(w.Second)}　本陣 {camp.X / 100},{camp.Y / 100}　大将から{Math.Sqrt(Math.Pow(heir.X - camp.X, 2) + Math.Pow(heir.Y - camp.Y, 2)) / 100:0}m");
        cp.Expect(camp == plan && w.Facts.Any(f => f.Id == "camp-set"), "総勢が着いた所に本陣が置かれる");
        // 指示と兵糧の届く範囲。
        w.Do("order:free"); me.Path = []; me.PathIndex = 0; (me.X, me.Y) = (camp.X - 1500_00, camp.Y + 300_00);
        cp.Expect(!w.Ordered(me) && !w.Supplied(me) && w.Ordered(heir) && w.Supplied(heir), "本陣から1,000mを超えると、指示も兵糧も届かない");
        int men = me.Soldiers, morale = me.Morale, heirMen = heir.Soldiers;
        for (int i = 0; i < 60 && w.War.Phase == "行軍"; i++) { me.Path = []; me.PathIndex = 0; w.Advance(60); }
        cp.Expect(me.Soldiers == men && me.Morale == morale, "範囲の外に出ても、手持ちの兵糧で2日は保つ");
        w.War.OutSince[me.Id] = w.Second - 2 * Day; // 2日たったことにする
        for (int i = 0; i < 180 && w.War.Phase == "行軍"; i++) { me.Path = []; me.PathIndex = 0; w.Advance(60); }
        cp.Log($"手持ちが尽きて3時間：本人 兵{men}→{me.Soldiers} 士気{morale}→{me.Morale}　大将 兵{heirMen}→{heir.Soldiers}");
        cp.Expect(me.Morale < morale && me.Soldiers < men && w.Facts.Any(f => f.Id == "hungry:" + me.Id), "手持ちが尽きると、じわじわ士気と兵が減る");
        cp.Expect(heir.Soldiers == heirMen, "届いている隊は減らない");
        // 敗走の収容と復活：本陣で半刻休むと、残った兵の7割で戻る。何度でも。
        foreach (var r in w.People.Where(r => r.House == "iwakiri")) { r.X = camp.X + 4000_00; r.Y = camp.Y; r.Path = []; r.PathIndex = 0; } // 敵は遠くへ
        (me.X, me.Y) = (camp.X + 50_00, camp.Y); w.Do("order:follow"); w.War.RetreatRules["takano"] = []; w.War.RetreatRules["iwakiri"] = []; // この検査では、軍としての撤退は見ない
        while (w.War.Phase != "合戦" && w.Second < 5 * Day) w.Advance(60);
        foreach (var round in new[] { 1, 2 })
        {
            { var now = w.CampOf(kosaka); camp = (now.X, now.Y); } // 攻城になると、本陣は城の前へ進む
            kosaka.X = camp.X; kosaka.Y = camp.Y; kosaka.Path = []; kosaka.PathIndex = 0; kosaka.Soldiers = 100; kosaka.Morale = 10; if (!w.War.Routed.Contains(kosaka.Id)) w.War.Routed.Add(kosaka.Id);
            for (int i = 0; i < 180 && w.War.Routed.Contains(kosaka.Id); i++) { var now2 = w.CampOf(kosaka); kosaka.Path = []; kosaka.PathIndex = 0; (kosaka.X, kosaka.Y) = (now2.X, now2.Y); w.Advance(60); } // 本陣が進めば、進んだ先で休む
            cp.Log($"{round}度目：崩れ{w.War.Routed.Contains(kosaka.Id)} 兵100→{kosaka.Soldiers}　{w.War.Phase}「{w.War.Result}」 本陣まで{Math.Sqrt(Math.Pow(kosaka.X - w.CampOf(kosaka).X, 2) + Math.Pow(kosaka.Y - w.CampOf(kosaka).Y, 2)) / 100:0}m {w.CampOf(kosaka).Name} 収容{w.War.AtCamp.ContainsKey(kosaka.Id)} 総崩れ{w.War.Recovered.Contains(kosaka.Id)}");
            cp.Expect(!w.War.Routed.Contains(kosaka.Id) && kosaka.Soldiers == 70, $"本陣に収容されて半刻で、7割の兵で戻る（{round}度目）");
        }
        // 本陣を取られる：味方がそばにいないとき、敵の隊が踏み込む。
        { var now = w.CampOf(kosaka); camp = (now.X, now.Y); }
        foreach (var u in new[] { heir, kosaka, me }) { u.X = camp.X - 1200_00; u.Y = camp.Y; u.Path = []; u.PathIndex = 0; w.War.Target.Remove(u.Id); }
        int spirit = w.SpiritOf("takano"); iguchi.Active = true; iguchi.Soldiers = Math.Max(iguchi.Soldiers, 40); w.War.Routed.Remove(iguchi.Id); (iguchi.X, iguchi.Y) = (camp.X + 20_00, camp.Y); iguchi.Path = []; iguchi.PathIndex = 0;
        w.Advance(120);
        cp.Log($"取られた{w.CampLost("takano")}　本陣の兵糧{w.StockOf("takano")}　全軍士気 {spirit}→{w.SpiritOf("takano")}　" + (w.Facts.LastOrDefault(f => f.Id.StartsWith("camp-lost"))?.Text ?? ""));
        cp.Expect(w.CampLost("takano") && w.StockOf("takano") == 0 && w.SpiritOf("takano") <= spirit - 30, "本陣を取られると、兵糧を失い、全軍士気が大きく下がる");
        cp.Expect(!w.Supplied(heir), "取られたあとは、どの隊にも兵糧が届かない");
    }

    // 本陣を移す（Core/Proto/ProtoCamp.cs）：本陣にいる本人が進言する。移している間は本陣が無い扱いで、移し終えると新しい所から届く。
    [Fact]
    public void TheCampCanBeMovedButIsDownWhileMoving()
    {
        using var cp = new Checkpoints("試作版の本陣を移す", o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        w.DecideCouncil(null, ProtoWorld.Departures[0], ProtoWorld.Signals[1], "正面から当たる", "後詰", "敵を崩す"); w.Do("order:follow");
        for (int i = 0; i < 600 && w.CampAt("takano") == null; i++) w.Advance(60);
        var me = w.Player; var heir = w.Person("takano-masayuki"); var camp = w.CampAt("takano")!.Value;
        w.Do("order:free"); me.Path = []; me.PathIndex = 0; (me.X, me.Y) = (camp.X + 1200_00, camp.Y);
        cp.Expect(!w.CanMoveCamp && !w.Actions().Any(a => a.Id == "camp:pick"), "本陣から離れていると、移す進言はできない");
        (me.X, me.Y) = (camp.X + 50_00, camp.Y);
        cp.Expect(w.CanMoveCamp && w.Actions().Any(a => a.Id == "camp:pick"), "本陣にいれば、移す進言ができる");
        var front = w.Front; long tx = (camp.X + front.X) / 2, ty = (camp.Y + front.Y) / 2;
        cp.Log(w.Do($"camp:move:{tx},{ty}"));
        var to = w.CampMoveTarget!.Value; double dist = Math.Sqrt(Math.Pow(to.X - camp.X, 2) + Math.Pow(to.Y - camp.Y, 2)) / 100;
        cp.Log($"{camp.X / 100},{camp.Y / 100} → {to.X / 100},{to.Y / 100}（{dist:0}m）　あと{w.CampMoveLeft / 60}分");
        cp.Expect(w.CampMoving && w.CampMoveLeft >= 30 * 60, "聞き入れられると移し始める。半刻と道のりの分だけかかる");
        (heir.X, heir.Y) = (camp.X, camp.Y); heir.Path = []; heir.PathIndex = 0;
        cp.Expect(!w.Supplied(heir) && w.Ordered(heir) && w.CampAt("takano") == camp, "移している間は補給が止まる（指示は総大将の隊から）。本陣はまだ元の所");
        cp.Expect(!w.CanMoveCamp, "移している最中に、もう一度は移せない");
        long left = w.CampMoveLeft; for (int i = 0; i < left / 60 + 2; i++) { (heir.X, heir.Y) = (to.X, to.Y); heir.Path = []; heir.PathIndex = 0; w.Advance(60); }
        cp.Log($"{ProtoWorld.Clock(w.Second)}　本陣 {w.CampAt("takano")!.Value.X / 100},{w.CampAt("takano")!.Value.Y / 100}　" + (w.Facts.LastOrDefault(f => f.Id.StartsWith("camp-at:"))?.Text ?? ""));
        cp.Expect(!w.CampMoving && w.CampAt("takano") == to && w.Supplied(heir), "移し終えると、新しい所から兵糧が届く");
    }

    // 味方の隊とぶつかっている敵の隊は、本人から遠くても見える。ぶつかっていなければ、見える範囲の外の敵は見えない。
    [Fact]
    public void EnemiesFightingFriendsAreSeenFromAfar()
    {
        using var cp = new Checkpoints("試作版の味方と戦う敵が見える", o);
        var w = new ProtoWorld(Map); w.SetupTsuriPractice();
        var me = w.Player; var kosaka = w.Person("kosaka-shigetada"); var iguchi = w.Person("iguchi-yashichi");
        (me.X, me.Y) = (kosaka.X + 1100_00, kosaka.Y + 900_00); me.Path = []; me.PathIndex = 0; w.War.Target.Remove(me.Id);
        (iguchi.X, iguchi.Y) = (kosaka.X + 600_00, kosaka.Y - 500_00); iguchi.Path = []; iguchi.PathIndex = 0; w.War.Target.Remove(iguchi.Id);
        cp.Expect(!w.Sees(me, iguchi), "遠くの敵は、ぶつかっていなければ見えない");
        (iguchi.X, iguchi.Y) = (kosaka.X + 60_00, kosaka.Y - 60_00);
        for (int i = 0; i < 10 && w.ClashOf(kosaka.Id) == null; i++) { me.Path = []; me.PathIndex = 0; w.Advance(60); }
        cp.Expect(w.ClashOf(kosaka.Id) != null && !w.Sees(me, iguchi), "本陣が無ければ、見えない所の戦いは知らせが来ないので見えない");
        // 本陣を、本人と小坂の間に置く（どちらも本陣の範囲の中）。
        w.SetCampForTest((me.X + kosaka.X) / 2, (me.Y + kosaka.Y) / 2);
        long since = w.War.Clashes.First(c => c.A == kosaka.Id || c.B == kosaka.Id).Since; bool early = w.Sees(me, iguchi);
        for (int i = 0; i < 20 && !w.Sees(me, iguchi); i++) { me.Path = []; me.PathIndex = 0; w.Advance(60); }
        cp.Log($"ぶつかってから{(w.Second - since) / 60}分で見えた　本人から井口まで{Math.Sqrt(Math.Pow(me.X - iguchi.X, 2) + Math.Pow(me.Y - iguchi.Y, 2)) / 100:0}m");
        cp.Expect(w.Sees(me, iguchi) && w.Second - since >= 5 * 60, "本陣の範囲の中なら、使番が本陣を経て知らせが届いてから見える");
        cp.Expect(w.Actions().Any(a => a.Id == "attack:" + iguchi.Id), "見えているので、攻めかかる相手に選べる");
    }

    // 撤収（Core/Proto/ProtoCamp.cs）：退き貝なら本陣へ退いて畳み（殿が前を固める）、勝てば本陣に集まって畳んでから戻る。畳むのに半刻。
    [Theory]
    [InlineData("退き貝")]
    [InlineData("勝ち")]
    public void TheCampIsPackedBeforeGoingHome(string mode)
    {
        using var cp = new Checkpoints("試作版の撤収_" + mode, o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        if (mode == "退き貝") w.DecideCouncil(null, ProtoWorld.Departures[0], ProtoWorld.Signals[0], "正面から当たる", "後詰", "敵を崩す");
        else w.DecideCouncil(null, ProtoWorld.Departures[0], ProtoWorld.Signals[0], "陣を固める", "道の守り", "陣を守り抜く");
        w.Do("order:follow");
        while (w.War.Phase != "合戦" && w.Second < 5 * Day) w.Advance(60);
        var camp = w.CampAt("takano")!.Value; var heir = w.Person("takano-masayuki");
        w.Advance(5 * 60);
        // 片方の全軍士気を半分より下げ、その側の撤退条件を満たさせる。
        string side = mode == "退き貝" ? "takano" : "iwakiri";
        w.War.RetreatRules[side] = ["spirit-half"]; w.War.RetreatRules[side == "takano" ? "iwakiri" : "takano"] = []; w.War.Spirit[side] = 40;
        int seen = w.Facts.Count; long start = w.Second;
        void Flush() { foreach (var f in w.Facts.Skip(seen).Where(f => f.Id.StartsWith("retreat") || f.Id.StartsWith("camp-") || f.Id.StartsWith("rearguard") || f.Id.StartsWith("withdrawn") || f.Id.StartsWith("home-march"))) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}"); seen = w.Facts.Count; }
        if (mode == "退き貝")
        {
            w.Advance(120); Flush();
            cp.Expect(w.CampPacking && w.War.Result == "", "退き貝が鳴ると本陣を畳み始め、畳み終えるまでは決着しない");
            var rg = w.People.FirstOrDefault(u => w.Facts.Any(f => f.Id == "rearguard:" + u.Id));
            cp.Log("殿：" + (rg?.Name ?? "なし"));
            cp.Expect(rg != null && rg.Id != heir.Id && !rg.Player, "総大将は一隊を殿に残す（大将と本人は除く）");
            for (int i = 0; i < 25; i++) w.Advance(60);
            var back = w.People.Where(u => u.House == "takano" && w.War.Assign.ContainsKey(u.Id) && u.Active && u.Soldiers > 0 && w.Facts.Any(f => f.Id == "retreat:takano") && u.Id != rg!.Id && !u.Player).ToArray();
            cp.Log("畳んでいる最中：" + string.Join("　", back.Select(u => $"{u.Name} 本陣まで{Math.Sqrt(Math.Pow(u.X - camp.X, 2) + Math.Pow(u.Y - camp.Y, 2)) / 100:0}m {u.Doing}")));
            cp.Expect(back.All(u => Math.Sqrt(Math.Pow(u.X - camp.X, 2) + Math.Pow(u.Y - camp.Y, 2)) <= 400_00), "各隊は城ではなく本陣へ退いている");
            for (int i = 0; i < 20 && w.War.Result == ""; i++) w.Advance(60); Flush();
            var packed = w.Facts.FirstOrDefault(f => f.Id == "camp-packed"); var done = w.Facts.FirstOrDefault(f => f.Id == "withdrawn:takano");
            cp.Expect(packed != null && done != null && packed.At - start >= 30 * 60 - 120 && done.At >= packed.At && w.War.Result == "退いた", "半刻で畳み終え、そこで決着（退いた）");
            cp.Expect(w.CampAt("takano") == null, "畳み終えた本陣は無くなる");
        }
        else
        {
            for (int i = 0; i < 300 && w.War.Result == ""; i++) w.Advance(60);
            cp.Log($"決着 {ProtoWorld.Clock(w.Second)}「{w.War.Result}」");
            for (int i = 0; i < 180 && !w.Facts.Any(f => f.Id == "home-march"); i++) w.Advance(60); Flush();
            var packed = w.Facts.FirstOrDefault(f => f.Id == "camp-packed"); var march = w.Facts.FirstOrDefault(f => f.Id == "home-march"); var pack = w.Facts.FirstOrDefault(f => f.Id == "camp-pack");
            cp.Expect(w.War.Result == "退かせた" && pack != null && packed != null && march != null && packed.At - pack.At >= 30 * 60 - 60 && march.At >= packed.At, "勝てば本陣に集まって半刻で畳み、それから総勢で戻る");
            cp.Log($"畳み始め {ProtoWorld.Clock(pack!.At)}　畳み終え {ProtoWorld.Clock(packed!.At)}　引き上げ {ProtoWorld.Clock(march!.At)}");
        }
    }

    // 兵を失って戦えない本人は、委任なら本陣で控える（攻城の城門へは行かない）。
    [Fact]
    public void TheBrokenPlayerWaitsAtCamp()
    {
        using var cp = new Checkpoints("試作版の本人は本陣で控える", o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        w.DecideCouncil(null, ProtoWorld.Departures[0], ProtoWorld.Signals[0], "釣り野伏", "伏兵", "敵を崩す"); w.Do("order:follow");
        while (w.War.Result == "" && w.War.Siege == null && w.Second < 5 * Day) w.Advance(60);
        var me = w.Player; me.Soldiers = 0;
        for (int i = 0; i < 6 * 60 && w.War.Result == ""; i++) w.Advance(60);
        var camp = w.CampOf(me); var gate = w.GatePoint;
        double toCamp = Math.Sqrt(Math.Pow(me.X - camp.X, 2) + Math.Pow(me.Y - camp.Y, 2)) / 100, toGate = Math.Sqrt(Math.Pow(me.X - gate.X, 2) + Math.Pow(me.Y - gate.Y, 2)) / 100;
        cp.Log($"{ProtoWorld.Clock(w.Second)}　本人 {camp.Name}まで{toCamp:0}m 城門まで{toGate:0}m　{me.Doing}　結果「{w.War.Result}」");
        cp.Expect(w.War.Result != "" || toCamp <= 100 && me.Doing.Contains("控えて"), "兵を失った本人は、本陣で控えている");
        // 本陣で控えていても、本陣の範囲の中の味方の隊は分かる。
        var heir = w.Person("takano-masayuki"); var hq = w.CampAt("takano");
        if (hq is { } h && heir.Soldiers > 0 && Math.Sqrt(Math.Pow(heir.X - h.X, 2) + Math.Pow(heir.Y - h.Y, 2)) <= ProtoWorld.CampRange)
            cp.Expect(w.Sees(me, heir), "本陣で控えていても、本陣の範囲の中の味方の隊は見える");
        while (w.War.Result == "" && w.Second < 20 * Day) { w.Advance(60); StarveQuickly(w); }
        // 決着のあと：兵のない本人も総大将について戻り、先に城へ入って待たない。
        var host = w.Person("takano-masayuki"); long gap = 0; bool alone = false;
        for (int i = 0; i < 6 * 60 && !w.AwardsReady; i++)
        {
            w.Advance(60);
            if (w.War.Result != "" && w.SiteOf(me) == w.AwardsSite && w.SiteOf(host) != w.AwardsSite) alone = true;
            if (w.Facts.Any(f => f.Id == "home-march")) gap = Math.Max(gap, (long)Math.Sqrt(Math.Pow(me.X - host.X, 2) + Math.Pow(me.Y - host.Y, 2)) / 100);
        }
        cp.Log($"戻る間の総大将との遠さ（いちばん離れた所）{gap}m　先に城へ入っていた{alone}");
        cp.Log($"{ProtoWorld.Clock(w.Second)}　結果「{w.War.Result}」　" + string.Join(" / ", w.Facts.TakeLast(6).Select(f => ProtoWorld.Clock(f.At) + " " + f.Text)) + $"　本人 {w.SiteOf(me)} {me.Doing}　大将 {w.SiteOf(host)} {host.Doing} 兵{host.Soldiers}");
        cp.Expect(w.AwardsReady && !alone && gap <= 500, "兵のない本人も総大将について戻り、一人で先に城へ入って待たない");
    }

    // 合戦の間の味方の隊：本人の目か、本陣の範囲の中なら使番の知らせで分かる。範囲の外の遠くの味方は分からない。
    [Fact]
    public void AlliesAreKnownThroughTheCamp()
    {
        using var cp = new Checkpoints("試作版の味方が分かる", o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        w.DecideCouncil(null, ProtoWorld.Departures[0], ProtoWorld.Signals[1], "正面から当たる", "後詰", "敵を崩す"); w.Do("order:follow");
        for (int i = 0; i < 600 && w.CampAt("takano") == null; i++) w.Advance(60);
        var me = w.Player; var kosaka = w.Person("kosaka-shigetada"); var camp = w.CampAt("takano")!.Value;
        w.Do("order:free"); me.Path = []; me.PathIndex = 0; kosaka.Path = []; kosaka.PathIndex = 0;
        (me.X, me.Y) = (camp.X + 30_00, camp.Y); (kosaka.X, kosaka.Y) = (camp.X - 800_00, camp.Y);
        cp.Expect(w.Sees(me, kosaka), "本人も味方も本陣の範囲の中なら、800m離れていても味方は分かる");
        (kosaka.X, kosaka.Y) = (camp.X - 1600_00, camp.Y);
        cp.Expect(!w.Sees(me, kosaka), "本陣の範囲の外の遠くの味方は分からない");
        (me.X, me.Y) = (camp.X - 1400_00, camp.Y);
        cp.Expect(w.Sees(me, kosaka), "本人の目で見える所の味方は分かる");
        // 本陣を移している間は、総大将の隊にも知らせが集まる。
        var heir = w.Person("takano-masayuki"); (me.X, me.Y) = (camp.X + 30_00, camp.Y); heir.Path = []; heir.PathIndex = 0;
        w.Do("camp:move:" + (camp.X + 600_00) + "," + camp.Y);
        cp.Expect(w.CampMoving, "本陣を移し始める");
        (heir.X, heir.Y) = (camp.X - 1300_00, camp.Y); (kosaka.X, kosaka.Y) = (camp.X - 2100_00, camp.Y); (me.X, me.Y) = (camp.X - 900_00, camp.Y);
        cp.Expect(w.Sees(me, kosaka), "本陣を移している間は、総大将の隊の近くの味方も、総大将を通して分かる");
    }

    // 作戦ごとの勝ち負けの表（釣り合いを見るための記録。本人は委任）。
    [Fact]
    public void WritesTheTableOfBattles()
    {
        using var cp = new Checkpoints("試作版の作戦ごとの勝ち負け", o);
        var cases = new List<(string Goal, string Plan, string Role)>();
        foreach (var goal in ProtoWorld.Goals) foreach (var plan in ProtoWorld.PlansOf(goal)) foreach (var r in ProtoWorld.RolesOf(plan)) cases.Add((goal, plan, r.Role));
        int wins = 0, total = 0;
        foreach (var dep in new[] { 0, 1 })
            foreach (var (goal, plan, role) in cases)
            {
                var w = new ProtoWorld(Map);
                while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
                var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
                for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
                w.DecideCouncil(null, ProtoWorld.Departures[dep], ProtoWorld.Signals[0], plan, role, goal); w.Do("order:follow");
                int merit = w.Merit;
                while (w.War.Result == "" && w.War.Siege == null && w.Second < 5 * Day) w.Advance(60);
                string field = w.War.Siege != null ? "城へ退かせた" : w.War.Result; long fieldEnd = w.Second;
                while (w.War.Result == "" && w.Second < 12 * Day) { w.Advance(60); StarveQuickly(w); } // 攻城は総大将が攻め方を決める（本人は軍議で何も言わない）
                w.Advance(60); total++; if (w.War.Result is "鎮めた" or "開城させた" or "退かせた" or "守り抜いた") wins++;
                for (int i = 0; i < 24 * 60 && !w.AwardsOpen; i++) w.Advance(60);
                w.Do("awards:take");
                long mins = fieldEnd / 60 - w.War.SignalAt / 60;
                int chaos = w.Facts.Count(f => f.Id.StartsWith("chaos:")), surprise = w.Facts.Count(f => f.Id.StartsWith("surprise:")), spotted = w.Facts.Count(f => f.Id.StartsWith("spotted:"));
                cp.Log($"{ProtoWorld.Departures[dep]}｜{goal}｜{plan}｜{role}｜野 {field} {mins}分｜{(w.War.Siege != null ? "攻城 " + w.War.Siege.Method + " " + (w.Second - fieldEnd) / 3600 + "時間 耐久" + w.War.Siege.Gate + " " : "")}{w.War.Result}｜作戦{w.PlanState}｜奇襲{surprise} 混乱{chaos} 見破り{spotted}｜勲功{w.Merit - merit}｜岩切の大目標 {w.GoalOf("iwakiri")}");
                cp.Expect(w.War.Result != "", $"決着がつく（{plan}・{role}・{ProtoWorld.Departures[dep]}）");
            }
        cp.Log($"鷹野の勝ち {wins}/{total}");
    }

    // 兵種・戦法・技能と、忠誠による能力の上げ下げ（Core/Proto/ProtoArms.cs）。
    [Fact]
    public void TroopsTacticsAndLoyaltyShapeTheFight()
    {
        using var cp = new Checkpoints("試作版の兵種と戦法", o);
        var w = new ProtoWorld(Map); w.SetupTsuriPractice();
        var heir = w.Person("takano-masayuki"); var kosaka = w.Person("kosaka-shigetada"); var iguchi = w.Person("iguchi-yashichi"); var katsumoto = w.Person("iwakiri-katsumoto");
        foreach (var u in new[] { heir, kosaka, iguchi, katsumoto, w.Player }) cp.Log($"{u.Name}　{u.Troop}　戦法 {ProtoWorld.TacticOf(u)}　技能 {string.Join("・", u.Skills)}　気性 {u.Temper}　統率 {u.Command}→{w.Cmd(u)}　武勇 {u.Valor}→{w.Val(u)}　知略 {u.Wits}→{w.Wit(u)}（忠誠{u.Loyalty}）");
        cp.Expect(heir.Troop == "騎馬" && ProtoWorld.TacticOf(heir) == "騎突" && iguchi.Troop == "足軽" && ProtoWorld.TacticOf(iguchi) == "槍衾", "兵種に合う戦法を持つ");
        cp.Expect(ProtoWorld.TacticOf(w.Player) == "鼓舞", "戦法の無い者は鼓舞");
        cp.Expect(w.Val(kosaka) > kosaka.Valor && w.Val(heir) == heir.Valor && w.Val(katsumoto) == katsumoto.Valor, "忠誠の高い家臣は申告より働く。当主はそのまま");
        cp.Expect(new[] { heir, kosaka, iguchi, katsumoto }.All(u => u.Temper is "勇猛" or "臨機" or "慎重"), "気性は勇猛・臨機・慎重");
        // 槍衾：井口が本人と打ち合い、戦法がたまると使う（5分間、与える打撃が増える）。
        var me = w.Player; me.Soldiers = 200; kosaka.X += 2000_00; // 小坂は遠くへ（この試しでは伏兵を見ない）
        for (int i = 0; i < 40 && !w.Facts.Any(f => f.Id.StartsWith("tactic:" + iguchi.Id)); i++) { iguchi.Morale = 100; me.Morale = 100; w.Advance(60); }
        cp.Log(w.Facts.LastOrDefault(f => f.Id.StartsWith("tactic:"))?.Text ?? "戦法なし");
        cp.Expect(w.Facts.Any(f => f.Id.StartsWith("tactic:" + iguchi.Id) && f.Text.Contains("槍衾")), "足軽の井口は槍衾を使う");
        // 騎突：野で井口の前に置いた正之（騎馬）が、たまった戦法で一度に兵を削る。
        heir.Active = true; heir.Soldiers = 120; heir.Morale = 100; w.War.Routed.Remove(heir.Id); (heir.X, heir.Y) = (iguchi.X + 60_00, iguchi.Y - 40_00); heir.Path = []; heir.PathIndex = 0;
        iguchi.Soldiers = 80; iguchi.Morale = 100; w.War.Routed.Remove(iguchi.Id); w.War.Gauge[heir.Id] = 100;
        cp.Expect(w.TerrainName(heir.X, heir.Y) != "森", "正之は森の外にいる");
        int before = iguchi.Soldiers; w.Advance(60);
        var hit = w.Facts.LastOrDefault(f => f.Id.StartsWith("tactic:" + heir.Id));
        cp.Log($"{hit?.Text}　井口 兵{before}→{iguchi.Soldiers}");
        cp.Expect(hit != null && hit.Text.Contains("騎突") && before - iguchi.Soldiers >= 5, "騎馬の正之は騎突で一度に兵を削る");
    }

    // 立て直し：崩れた隊は本陣で30分休むと、一度だけ戦いに戻る（士気は60を基準に、武勇で増減）。
    [Fact]
    public void TheRoutedUnitRegroupsAtCamp()
    {
        using var cp = new Checkpoints("試作版の立て直し", o);
        var w = new ProtoWorld(Map); w.SetupTsuriPractice();
        var iguchi = w.Person("iguchi-yashichi"); var castle = Map.Site("iwakiri-castle");
        // 鷹野の本陣（大将の陣）に、崩れた小坂を置く。敵は遠くへ。
        var kosaka = w.Person("kosaka-shigetada"); var camp = w.CampOf(kosaka);
        foreach (var u in w.Units.Where(u => u.House == "iwakiri")) { u.X = camp.X + 3000_00; u.Y = camp.Y + 3000_00; u.Path = []; u.PathIndex = 0; w.War.Target.Remove(u.Id); }
        kosaka.X = camp.X; kosaka.Y = camp.Y; kosaka.Path = []; kosaka.PathIndex = 0; kosaka.Soldiers = 50; kosaka.Morale = 10; w.War.Routed.Add(kosaka.Id);
        w.Advance(20 * 60); cp.Expect(w.War.Routed.Contains(kosaka.Id), "20分ではまだ崩れたまま");
        w.Advance(15 * 60); cp.Log($"35分後：崩れ{w.War.Routed.Contains(kosaka.Id)} 士気{kosaka.Morale}");
        cp.Expect(!w.War.Routed.Contains(kosaka.Id) && kosaka.Morale == Math.Clamp(60 + (w.Val(kosaka) - 50) / 2, 40, 80), "30分休むと戦いに戻る（戻る士気は60を基準に、武勇が高いほど多い）");
        // 岩切の崩れた隊が城へ逃げ込むと、城という一つの部隊の中身になる（個別には狙えず、戦わない）。
        iguchi.X = castle.X * 100L; iguchi.Y = castle.Y * 100L; iguchi.Path = []; iguchi.PathIndex = 0; iguchi.Soldiers = 50; iguchi.Morale = 10; if (!w.War.Routed.Contains(iguchi.Id)) w.War.Routed.Add(iguchi.Id);
        w.Advance(120);
        cp.Log($"井口：崩れ{w.War.Routed.Contains(iguchi.Id)} 城内{w.Sheltered(iguchi)} 攻城{w.War.Siege != null}");
        cp.Expect(w.War.Siege != null && w.Sheltered(iguchi) && !w.War.Routed.Contains(iguchi.Id), "崩れた隊が城へ逃げ込むと攻城になり、その隊は城の兵に戻る");
        cp.Expect(!w.Actions().Any(a => a.Id == "attack:" + iguchi.Id), "城内の隊は個別には狙えない");
    }

    // 見た目を確かめるための保存（HAKUTAI_WRITE_SCENE に置き場所を渡したときだけ書く。ふだんは何もしない）。
    // 動画と同じ筋で、6時20分を過ぎて本人の隊がぶつかっている場面。ゲームを HAKUTAI_PROTO_SAVE でこれを読ませて絵を撮る。
    [Fact]
    public void WritesTheBattleSceneWhenAsked()
    {
        if (Environment.GetEnvironmentVariable("HAKUTAI_WRITE_SCENE") is not { Length: > 0 } path) return;
        var w = new ProtoWorld(Map);
        w.Advance(2 * Day + 5 * Hour + 2 * 60 - w.Second);
        var notice = w.OffersHere.First(t => t.Kind == "death-notice"); w.Do("accept:" + notice.Id);
        w.GoToTask(notice.Id); w.Advance(w.TaskEta(notice)!.Value + 60); w.Do("step:" + notice.Id);
        w.GoToTask(notice.Id); w.Advance(w.TaskEta(notice)!.Value + 60); w.Do("step:" + notice.Id);
        while (!w.CouncilOpen && w.Second < 3 * Day) w.Advance(60);
        w.DecideCouncil("south-hill", ProtoWorld.Departures[0], ProtoWorld.Signals[0]);
        w.GoToTask(w.OpenTasks.First(t => t.Kind == "muster").Id);
        w.Advance(3 * Day + 6 * Hour + 3 * 60 - w.Second);
        w.Do(w.Actions().First(a => a.Id.StartsWith("attack:")).Id);
        w.Advance(3 * Day + 6 * Hour + 20 * 60 - w.Second);
        for (int i = 0; i < 60 && w.ClashOf(w.Player.Id) == null; i++) w.Advance(60);
        File.WriteAllBytes(path, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(w.Save(), ProtoMap.Json));
        o.WriteLine($"{ProtoWorld.Clock(w.Second)} → {path}");
    }

    // 動画（Game/Scripts/Proto/ProtoFilm.cs）と同じ手順：訃報を自分で届けて説かずに戻り、軍議で南の丘・今夜・6時を選んで丘へ。合図で合戦になって決着がつく。
    [Fact]
    public void TheFilmsStoryReachesABattle()
    {
        using var cp = new Checkpoints("試作版の動画の筋", o);
        var w = new ProtoWorld(Map);
        w.Advance(2 * Day + 5 * Hour + 2 * 60 - w.Second);
        var notice = w.OffersHere.First(t => t.Kind == "death-notice"); w.Do("accept:" + notice.Id);
        w.GoToTask(notice.Id); w.Advance(w.TaskEta(notice)!.Value + 60); w.Do("step:" + notice.Id);
        w.GoToTask(notice.Id); w.Advance(w.TaskEta(notice)!.Value + 60); w.Do("step:" + notice.Id);
        while (!w.CouncilOpen && w.Second < 3 * Day) w.Advance(60);
        cp.Expect(w.CouncilOpen, "軍議に加われる　" + ProtoWorld.Clock(w.Second));
        cp.Log(w.DecideCouncil("south-hill", ProtoWorld.Departures[0], ProtoWorld.Signals[0]));
        cp.Log($"出立 {ProtoWorld.Clock(w.War.DepartAt)}　合図 {ProtoWorld.Clock(w.War.SignalAt)}");
        var muster = w.OpenTasks.First(t => t.Kind == "muster"); w.GoToTask(muster.Id);
        int seen = w.Facts.Count;
        long half = 0;
        while (w.War.Result == "" && w.Second < 8 * Day)
        {
            w.Advance(300); StarveQuickly(w); foreach (var f in w.Facts.Skip(seen)) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}"); seen = w.Facts.Count;
            // 合図の後は30分ごとに、隊ごとの兵・士気・崩れ・していることを書く（決着がつかないときに理由を見る）。
            if (w.War.Signaled && w.Second / 1800 != half && (half = w.Second / 1800) > 0)
                cp.Log("　　" + string.Join("、", w.Units.Where(u => u.House is "takano" or "iwakiri" && (u.House == "iwakiri" || w.War.Assign.ContainsKey(u.Id)))
                    .Select(u => $"{u.Name}({u.Soldiers}/{u.Morale}{(w.War.Routed.Contains(u.Id) ? "崩" : "")}) {u.Doing}")));
        }
        cp.Log($"{ProtoWorld.Clock(w.Second)} 段階 {w.War.Phase} 合図 {w.War.Signaled} 結果 {w.War.Result}");
        cp.Expect(w.War.Result != "", "決着がつく");
        cp.Expect(w.Facts.Any(f => f.Id.StartsWith("tactic:")), "武将が戦法を使う");
        cp.Log("立て直した隊：" + w.Facts.Count(f => f.Id.StartsWith("regroup:")) + "（成り行きしだい。仕組みは TheRoutedUnitRegroupsAtCamp で確かめる）");
        cp.Log($"総士気 鷹野{w.SpiritOf("takano")} 岩切{w.SpiritOf("iwakiri")}");
    }
}
