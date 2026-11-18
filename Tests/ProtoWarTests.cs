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
        cp.Expect(ids.Contains("rebellion-crushed") || ids.Contains("expedition-failed"), "合戦の決着がつく");
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
        cp.Expect(w.War.Assign[w.Player.Id] == "south-hill" && w.War.PlayerAtCouncil, "本人の選んだ陣が通る");
        var katsumoto = w.Person("iwakiri-katsumoto");
        cp.Expect(!w.Visible.Contains(katsumoto), "遠くの敵の隊は見えない");
        var hill = w.PositionOf("south-hill"); w.MoveTo(hill.X * 100L, hill.Y * 100L);
        var scout = w.Actions().FirstOrDefault(a => a.Id == "scout:road-mouth");
        cp.Expect(scout != null, "行軍中は物見を出せる");
        cp.Log(w.Do(scout!.Id));
        Run(w, 3 * Hour, cp);
        cp.Log("印：" + string.Join("、", w.Markers.Select(m => $"{w.Person(m.Unit).Name} {ProtoWorld.Clock(m.SeenAt)} {m.By}")));
        cp.Expect(w.Journal.Any(e => e.Text.StartsWith("物見が戻った")), "物見が戻って知らせる");
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
        cp.Expect(muster != null && muster.Speech.Contains("道の口"), "出陣の任務が出る（口上つき）");
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
        while (w.War.Result == "" && w.Second < 4 * Day) { w.Advance(300); foreach (var f in w.Facts.Skip(seen)) cp.Log($"{ProtoWorld.Clock(f.At)}　{f.Text}"); seen = w.Facts.Count; }
        cp.Log($"{ProtoWorld.Clock(w.Second)} 段階 {w.War.Phase} 合図 {w.War.Signaled} 結果 {w.War.Result}");
        cp.Expect(w.War.Result != "", "決着がつく");
    }
}
