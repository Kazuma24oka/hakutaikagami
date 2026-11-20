using HakutaiKagami.Core.Proto;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class ProtoWorldTests
{
    static readonly ProtoMap Map = ProtoMap.Load();
    static (long, long) At(int xm, int ym) => (xm * 100L, ym * 100L);
    const long Day = ProtoWorld.Day, Hour = ProtoWorld.Hour;

    [Fact]
    public void BakesTheTerrainFromData()
    {
        Assert.Equal(200, Map.Columns); Assert.Equal(150, Map.Rows);
        Assert.Equal(Ground.Mountain, Map.GroundAtCm(730_00, 1250_00));
        Assert.Equal(Ground.Forest, Map.GroundAtCm(1500_00, 1380_00));
        Assert.Equal(Ground.Water, Map.GroundAtCm(2770_00, 475_00));
        Assert.Equal(Ground.Site, Map.GroundAtCm(870_00, 2200_00));
        Assert.True(Map.HeightAtCm(730_00, 1250_00) > 100_00);
    }

    [Fact]
    public void MountainAndForestAreSlowerThanTheRoad()
    {
        long Walk((long X, long Y) a, (long X, long Y) b) => ProtoMove.Eta(Map, a, [b]);
        long road = Walk(At(1000, 2193), At(1500, 2165));
        long forest = Walk(At(1300, 1380), At(1700, 1380));
        long mountain = Walk(At(500, 1250), At(960, 1250));
        Assert.True(road * 2 < forest, $"道{road}秒 森{forest}秒");
        Assert.True(forest < mountain, $"森{forest}秒 山{mountain}秒");
        Assert.True(ProtoMove.Eta(Map, At(1000, 2193), [At(1500, 2165)], percent: 150) < road);
    }

    [Fact]
    public void PlansAroundTheRiverAndUsesTheFord()
    {
        var path = ProtoMove.Plan(Map, At(2400, 1450), At(3100, 1450));
        Assert.NotNull(path);
        Assert.Equal(At(3100, 1450), path![^1]);
    }

    static string Look(ProtoWorld w) => string.Join("|", w.People.Select(p => $"{p.Id}:{p.Active},{p.X},{p.Y},{p.Doing},{p.PathIndex}")) + "#" + string.Join("|", w.Letters.Select(l => $"{l.Id}:{l.Carrier}:{l.State}"))
        + "#" + string.Join("|", w.Tasks.Select(t => $"{t.Id}:{t.State}:{t.Step}")) + "#" + string.Join("|", w.Facts.Select(f => f.Id)) + "#" + w.Journal.Count + w.Merit + w.Money;

    [Fact]
    public void SameInputsGiveTheSameWorld()
    {
        string Run(params long[] chunks)
        {
            var w = new ProtoWorld(Map); w.GoToTask(w.OpenTasks.First().Id);
            foreach (var c in chunks) w.Advance(c);
            return Look(w);
        }
        Assert.Equal(Run(2 * Day), Run(1, 59, 3000, 2 * Day - 3060));
    }

    [Fact]
    public void ThePlayerCarriesTheLetterAndGetsAReply()
    {
        var w = new ProtoWorld(Map);
        var task = Assert.Single(w.OpenTasks);
        var eta = w.TaskEta(task); Assert.NotNull(eta);
        Assert.True(w.GoToTask(task.Id));
        w.Advance(eta!.Value + 60);
        var hand = Assert.Single(w.Actions(), a => a.Id == "step:" + task.Id);
        Assert.Contains("受け取った", w.Do(hand.Id));
        Assert.Equal("鷹野義正に返書を渡す", task.Current!.Text);
        w.GoToTask(task.Id); w.Advance(w.TaskEta(task)!.Value + 60);
        w.Do("step:" + task.Id);
        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal(3, w.Merit);
    }

    [Fact]
    public void ThePlayerOnlySeesNearbyPeople()
    {
        var w = new ProtoWorld(Map);
        Assert.Contains(w.Visible, p => p.Id == "takano-yoshimasa");
        Assert.DoesNotContain(w.Visible, p => p.Id == "sawatari-nobufusa");
    }

    // 2. 自分がいなくても世界が動く：誰も何もしなくても、境目の争いは小競り合いまで進み、峠の賊は商人を襲い、その話は城主から殿へ届く。
    [Fact]
    public void TheWorldMovesWithoutThePlayer()
    {
        var w = new ProtoWorld(Map);
        w.Advance(4 * Day);
        var ids = w.Facts.Select(f => f.Id).ToArray();
        Assert.Contains("dispute", ids);
        Assert.Contains("talk-set", ids);
        Assert.Contains("talk-failed", ids);
        Assert.Contains("stakes", ids);
        // 小競り合いは、正之が西の村へ来られたときだけ起きる（合戦で隊が崩れて来られなければ起きない）。
        Assert.True(ids.Contains("skirmish") || ids.Any(i => i.StartsWith("rout:takano-masayuki:")));
        Assert.Contains(ids, id => id.StartsWith("robbery:echigoya"));
        Assert.Contains("bandits-reported", ids);
        Assert.True(w.Knows(w.Person("takano-yoshimasa"), "bandits-reported"));
        Assert.False(w.Knows(w.Player, "stakes"));
        Assert.Equal("kosaka-shigetada", w.Letters.Single(l => l.Tag == "dispute-reply").Carrier);
    }

    // 1. やることが湧き続ける：毎朝7時に殿が務めを出し、受けなかった務めは家中の者が代わりに行く。
    [Fact]
    public void TheLordGivesDutiesEveryMorning()
    {
        var w = new ProtoWorld(Map);
        w.Advance(Day);
        var offered = w.OffersHere.Select(t => t.Title).ToArray();
        Assert.Contains("西の村と峠の見回り", offered);
        Assert.Contains(w.Actions(), a => a.Text == "受ける：西の村と峠の見回り");
        w.Advance(5 * Hour);
        Assert.All(w.Tasks.Where(t => t.Kind == "patrol"), t => Assert.Equal(TaskState.Expired, t.State));
        Assert.Equal("west-village", w.Appointments.Last(a => a.Person == "takano-masayuki").Site);
    }

    // 3. やったことが返ってくる：務めを果たすと勲功がたまり、殿のそばで褒美と役目をもらう。馬廻になると速く歩ける。
    [Fact]
    public void MeritBringsRewardsAndAHorse()
    {
        var w = new ProtoWorld(Map);
        void Finish(ProtoTask t)
        {
            for (int guard = 0; guard < 20 && t.State == TaskState.Open; guard++)
            {
                w.GoToTask(t.Id); w.Advance((w.TaskEta(t) ?? 0) + 30);
                var a = w.Actions().FirstOrDefault(a => a.Id == "step:" + t.Id); if (a != null) w.Do(a.Id);
            }
            Assert.Equal(TaskState.Done, t.State);
        }
        Finish(w.OpenTasks.First());
        w.Advance(Day + 7 * Hour - w.Second + 60);
        var patrol = w.OffersHere.First(t => t.Kind == "patrol"); w.Do("accept:" + patrol.Id);
        var tax = w.OffersHere.First(t => t.Kind == "tax"); w.Do("accept:" + tax.Id);
        Finish(tax); Finish(patrol);
        Assert.True(w.Merit >= 8, "勲功" + w.Merit);
        var lord = w.Person("takano-yoshimasa"); w.MoveTo(lord.X, lord.Y); w.Advance(1200);
        Assert.Equal(1, w.Rank);
        Assert.Equal(150, w.PlayerPercent);
        Assert.Contains(w.Journal, e => e.Text.Contains("馬廻"));
    }

    [Fact]
    public void ThePlayerCanDriveOffTheBanditsAfterTheyAreReported()
    {
        var w = new ProtoWorld(Map);
        w.Advance(2 * Day);
        var duty = w.OffersHere.FirstOrDefault(t => t.Kind == "bandits");
        Assert.NotNull(duty);
        w.Do("accept:" + duty!.Id);
        for (int tries = 0; tries < 8 && duty.Current?.Do == "fight"; tries++)
        {
            w.GoToTask(duty.Id); w.Advance((w.TaskEta(duty) ?? 0) + 60);
            var fight = w.Actions().FirstOrDefault(a => a.Text == "賊と戦う"); if (fight != null) w.Do(fight.Id); else w.Advance(600);
        }
        Assert.Equal("meet", duty.Current!.Do);
        Assert.True(w.Knows(w.Player, "bandits-gone"));
        Assert.All(w.People.Where(p => p.House == "bandit"), b => Assert.False(b.Active));
    }

    [Fact]
    public void SavingAndLoadingGivesTheSameFuture()
    {
        var straight = new ProtoWorld(Map); straight.GoToTask(straight.OpenTasks.First().Id); straight.Advance(2 * Day + 17);
        var first = new ProtoWorld(Map); first.GoToTask(first.OpenTasks.First().Id); first.Advance(Day + 5000);
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(first.Save(), ProtoMap.Json);
        var loaded = ProtoWorld.Load(Map, System.Text.Json.JsonSerializer.Deserialize<ProtoWorld.Saved>(bytes, ProtoMap.Json)!);
        Assert.Equal(Look(first), Look(loaded));
        loaded.Advance(2 * Day + 17 - (Day + 5000));
        Assert.Equal(Look(straight), Look(loaded));
    }
}
