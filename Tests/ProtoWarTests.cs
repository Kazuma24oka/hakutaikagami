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
        while (w.War.Result == "" && w.Second < 5 * Day) { w.Advance(300); foreach (var f in w.Facts.Skip(seen)) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}"); seen = w.Facts.Count; }
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
        for (int i = 0; i < 240 && !w.Actions().Any(a => a.Id.StartsWith("attack:")); i++) w.Advance(30);
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
        var katsumoto = w.Person("iwakiri-katsumoto"); w.MoveTo(katsumoto.X, katsumoto.Y);
        for (int i = 0; i < 240 && !w.Actions().Any(a => a.Id.StartsWith("attack:")); i++) w.Advance(30);
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
        while (w.War.Result == "" && w.Second < 5 * Day)
        {
            w.Advance(60);
            if (w.Body.Any()) { sawBody = true; if (w.Body.Any(u => Math.Sqrt(Math.Pow(u.X - heir.X, 2) + Math.Pow(u.Y - heir.Y, 2)) > 400_00)) together = false; }
            foreach (var f in w.Facts.Skip(seen).Where(f => !f.Text.Contains("杭") && !f.Text.Contains("怪しい") && !f.Text.Contains("越後屋") && !f.Text.Contains("小競り"))) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}");
            seen = w.Facts.Count;
        }
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
        if (w.War.Siege != null) cp.Expect(w.Facts.Any(f => f.Id == "retreat:iwakiri") && !w.Retreated("iwakiri"), "岩切勢は撤退条件を満たして退き貝を鳴らし、城に籠もった");
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
        while (w.War.Result == "" && w.Second < 5 * Day) w.Advance(60);
        int atEnd = w.Merit; w.Advance(60); // 出陣の任務の分は、ここまでに入っている
        cp.Expect(w.Merit == atEnd, "決着しただけでは合戦の勲功は入らない（城へ戻ってからの論功行賞で入る）");
        for (int i = 0; i < 24 * 60 && !w.AwardsOpen; i++) w.Advance(60);
        w.Do("awards:take");
        cp.Log($"結果 {w.War.Result}　勲功 {merit}→{w.Merit}　" + string.Join(" ", w.Journal.Where(e => e.Text.Contains("勲功")).Select(e => e.Text).TakeLast(2)));
        cp.Expect(w.Merit - merit is > 0 and <= 16, "合戦で働けば、見込みに近い勲功が入る");
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
        while (w.War.Result == "" && w.Second < 9 * Day) // 攻城になれば、総大将の決めた攻め方で終わるまで
        {
            w.Advance(60);
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

    // 攻城（Core/Proto/ProtoSiege.cs）：野の戦いで岩切勢が城へ退くと攻城になる。力攻め・囲む・開城を説く。
    [Theory]
    [InlineData("力攻め", false)]
    [InlineData("包囲", false)]
    [InlineData("包囲", true)]
    public void TheCastleIsStormedStarvedOrTalkedOpen(string method, bool parley)
    {
        using var cp = new Checkpoints("試作版の攻城_" + method + (parley ? "_説く" : ""), o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        w.DecideCouncil(null, ProtoWorld.Departures[0], ProtoWorld.Signals[0], "釣り野伏", "伏兵", "敵を崩す"); w.Do("order:follow");
        while (w.War.Result == "" && w.War.Siege == null && w.Second < 5 * Day) w.Advance(60);
        string Line() => $"{ProtoWorld.Clock(w.Second)}　{(w.War.Siege is { } sg ? (sg.Layer < sg.Layers ? ProtoWorld.LayerName(sg.Layers, sg.Layer) + sg.Gate : "落ちた") + $"　包囲{(sg.Ringed ? "成" : "未")}（{w.RingHave}/{w.RingNeed}）　城の兵糧{sg.Food} 攻め手{sg.Supply}" : "")}　全軍士気 鷹野{w.SpiritOf("takano")} 岩切{w.SpiritOf("iwakiri")}　" + string.Join(" ", w.People.Where(u => u.Soldiers > 0 && u.House is "takano" or "iwakiri" && w.War.StartSoldiers.ContainsKey(u.Id)).Select(u => $"{u.Name}{u.Soldiers}{(w.War.Routed.Contains(u.Id) ? "崩" : "")}"));
        cp.Log("野の戦いのあと　" + Line());
        cp.Expect(w.War.Siege != null && w.War.Result == "", "岩切勢が城へ退くと、決着にならず攻城になる");
        for (int i = 0; i < 6 * 60 && !w.SiegeCouncilOpen; i++) w.Advance(60);
        cp.Log($"{ProtoWorld.Clock(w.Second)}　{w.SiegeSpeech}");
        cp.Expect(w.SiegeCouncilOpen, "城の前に陣を敷くと、攻め方の軍議が開く");
        cp.Log("　" + w.Do("siege:" + method));
        cp.Expect(w.War.Siege!.Method == method, "本人の進言が通る（総大将の心証が悪くなければ）");
        int seen = w.Facts.Count; long start = w.Second;
        cp.Log($"　本人 兵{w.Player.Soldiers}　城まで{Math.Sqrt(Math.Pow(w.Player.X - Map.Site("iwakiri-castle").X * 100.0, 2) + Math.Pow(w.Player.Y - Map.Site("iwakiri-castle").Y * 100.0, 2)) / 100:0}m");
        while (w.War.Result == "" && w.Second < start + 6 * Day)
        {
            w.Advance(60);
            foreach (var f in w.Facts.Skip(seen).Where(f => !f.Id.StartsWith("free:") && !f.Id.StartsWith("flank:") && !f.Id.StartsWith("spotted:") && !f.Id.StartsWith("tactic:") && !f.Text.Contains("越後屋") && !f.Text.Contains("市が立つ"))) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}"); seen = w.Facts.Count;
            if ((w.Second - start) % (method == "力攻め" ? 600 : 6 * 3600) == 0) cp.Log("　" + Line());
            if (parley && w.Second % Day == 12 * 3600 && w.CanParley) cp.Log($"{ProtoWorld.Clock(w.Second)}　説く：" + w.Do("parley:katsumoto"));
        }
        cp.Log($"結果 {w.War.Result}　{(w.Second - start) / 3600}時間　" + Line());
        cp.Expect(w.War.Result != "", "攻城に決着がつく");
        if (method == "力攻め") cp.Expect(w.Facts.Any(f => f.Id == "layer:1") && (w.War.Result is not ("鎮めた" or "開城させた") || w.War.Siege!.Layer == w.War.Siege.Layers), "力攻め：外の層から一層ずつ失う（落ちるのは最後の層を失ったとき）");
        else if (!parley) cp.Expect(w.GateOf == 100 && (w.War.Result == "開城させた" && w.War.Siege!.Food == 0 || w.War.Result == "鎮めた" && w.War.Siege!.Food <= 1), "囲む：城の兵糧が尽きて開城するか、残り1日で打って出た城の兵が崩れて落城");
        else cp.Expect(w.War.Result == "開城させた" && w.GateOf == 100 && w.War.Siege!.Food > 0, "囲んで説けば、兵糧が尽きるのを待たずに城が開く");
        if (w.War.Result is "鎮めた" or "開城させた")
        {
            for (int i = 0; i < 24 * 60 && !w.AwardsOpen; i++) w.Advance(60);
            cp.Log($"{ProtoWorld.Clock(w.Second)}　論功行賞の場所 {w.AwardsSite}　本人 {w.SiteOf(w.Player)}");
            cp.Expect(w.AwardsOpen && w.AwardsSite == "iwakiri-castle", "城を落とした・開かせたときは、岩切城に入って論功行賞");
            int merit = w.Merit; w.Do("awards:take"); cp.Log($"勲功 {merit}→{w.Merit}");
        }
    }

    // 囲まれた城は、兵糧が残り1日になると朝駆けに出る（慎重な城主でも）。40分たつか、隊が崩れたら城へ引き上げる。
    [Fact]
    public void TheStarvingGarrisonSalliesAtDawn()
    {
        using var cp = new Checkpoints("試作版の朝駆け", o);
        var w = new ProtoWorld(Map);
        while (w.War.Phase != "軍議待ち" && w.Second < 4 * Day) w.Advance(600);
        var castle = Map.Site("takano-castle"); w.MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !w.CouncilOpen; i++) w.Advance(60);
        w.DecideCouncil(null, ProtoWorld.Departures[0], ProtoWorld.Signals[0], "釣り野伏", "伏兵", "敵を崩す"); w.Do("order:follow");
        while (w.War.Result == "" && w.War.Siege == null && w.Second < 5 * Day) w.Advance(60);
        for (int i = 0; i < 6 * 60 && !w.SiegeCouncilOpen; i++) w.Advance(60);
        w.Do("siege:包囲");
        cp.Expect(w.War.Siege is { Method: "包囲" }, "包囲と決まる");
        w.Advance(3 * 3600);
        var foe = Map.Site("iwakiri-castle"); double Far(ProtoPerson u) => Math.Sqrt(Math.Pow(u.X - foe.X * 100.0, 2) + Math.Pow(u.Y - foe.Y * 100.0, 2)) / 100;
        var rebels = w.People.Where(u => u.House == "iwakiri" && u.Active).ToArray();
        cp.Log("囲み：" + string.Join("　", w.People.Where(u => u.House == "takano" && u.Soldiers > 0 && w.War.StartSoldiers.ContainsKey(u.Id)).Select(u => $"{u.Name} 城から{Far(u):0}m")) + "　城方：" + string.Join("　", rebels.Select(u => $"{u.Name} {Far(u):0}m")));
        cp.Expect(w.War.Clashes.Count == 0 && rebels.All(u => Far(u) <= 150), "囲んでいる間、城の兵は城から出ず、打ち合いも起きない");
        w.War.Siege!.Food = 1; // 兵糧が残り1日になったことにする
        int seen = w.Facts.Count;
        while (w.Second % Day != 5 * 3600 + 60) w.Advance(60);
        cp.Expect(w.Facts.Skip(seen).Any(f => f.Id.StartsWith("sally:")), "兵糧が残り1日になると、朝5時に朝駆けに出る");
        double farthest = 0;
        for (int i = 0; i < 90 && w.War.Result == ""; i++) { w.Advance(60); farthest = Math.Max(farthest, rebels.Where(u => u.Active).Select(Far).DefaultIfEmpty(0).Max()); }
        foreach (var f in w.Facts.Skip(seen).Where(f => !f.Id.StartsWith("free:"))) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}");
        cp.Log($"いちばん遠くまで出た所 城から{farthest:0}m　結果「{w.War.Result}」　城方：" + string.Join("　", rebels.Select(u => $"{u.Name} 兵{u.Soldiers} {Far(u):0}m {u.Doing}")));
        cp.Expect(farthest > 300, "城から出て、囲みの隊へ向かう");
        cp.Expect(w.War.Result != "" || rebels.Where(u => u.Active && u.Soldiers > 0).All(u => Far(u) <= 200), "決着しなければ、城へ引き上げている");
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
                while (w.War.Result == "" && w.Second < 9 * Day) w.Advance(60); // 攻城は総大将が攻め方を決める（本人は軍議で何も言わない）
                w.Advance(60); total++; if (w.War.Result is "鎮めた" or "開城させた" or "退かせた" or "守り抜いた") wins++;
                for (int i = 0; i < 24 * 60 && !w.AwardsOpen; i++) w.Advance(60);
                w.Do("awards:take");
                long mins = fieldEnd / 60 - w.War.SignalAt / 60;
                int chaos = w.Facts.Count(f => f.Id.StartsWith("chaos:")), surprise = w.Facts.Count(f => f.Id.StartsWith("surprise:")), spotted = w.Facts.Count(f => f.Id.StartsWith("spotted:"));
                cp.Log($"{ProtoWorld.Departures[dep]}｜{goal}｜{plan}｜{role}｜野 {field} {mins}分｜{(w.War.Siege != null ? "攻城 " + w.War.Siege.Method + " " + (w.Second - fieldEnd) / 3600 + "時間 失った層" + w.War.Siege.Layer + " " : "")}{w.War.Result}｜作戦{w.PlanState}｜奇襲{surprise} 混乱{chaos} 見破り{spotted}｜勲功{w.Merit - merit}｜岩切の大目標 {w.GoalOf("iwakiri")}");
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
        // 敵のいない本陣（岩切城）に、崩れた井口を置く。
        foreach (var u in w.Units.Where(u => u.House == "takano")) { u.X = castle.X * 100L + 3000_00; u.Y = castle.Y * 100L + 3000_00; u.Path = []; u.PathIndex = 0; w.War.Target.Remove(u.Id); }
        iguchi.X = castle.X * 100L; iguchi.Y = castle.Y * 100L; iguchi.Path = []; iguchi.PathIndex = 0; iguchi.Soldiers = 50; iguchi.Morale = 10; w.War.Routed.Add(iguchi.Id);
        w.Advance(20 * 60); cp.Expect(w.War.Routed.Contains(iguchi.Id), "20分ではまだ崩れたまま");
        w.Advance(15 * 60); cp.Log($"35分後：崩れ{w.War.Routed.Contains(iguchi.Id)} 士気{iguchi.Morale}");
        cp.Expect(!w.War.Routed.Contains(iguchi.Id) && iguchi.Morale == Math.Clamp(60 + (w.Val(iguchi) - 50) / 2, 40, 80), "30分休むと戦いに戻る（戻る士気は60を基準に、武勇が高いほど多い）");
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
        while (w.War.Result == "" && w.Second < 4 * Day)
        {
            w.Advance(300); foreach (var f in w.Facts.Skip(seen)) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}"); seen = w.Facts.Count;
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
