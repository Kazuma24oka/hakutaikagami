using HakutaiKagami.Core.Proto;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class ProtoWorldTests
{
    static readonly ProtoMap Map = ProtoMap.Load();
    static (long, long) At(int xm, int ym) => (xm * 100L, ym * 100L);

    [Fact]
    public void BakesTheTerrainFromData()
    {
        Assert.Equal(200, Map.Columns); Assert.Equal(150, Map.Rows);
        Assert.Equal(Ground.Mountain, Map.GroundAtCm(730_00, 1250_00));
        Assert.Equal(Ground.Forest, Map.GroundAtCm(1500_00, 1380_00));
        Assert.Equal(Ground.Water, Map.GroundAtCm(2770_00, 475_00 + 10_00 * 0));
        Assert.Equal(Ground.Site, Map.GroundAtCm(870_00, 2200_00));
        Assert.True(Map.HeightAtCm(730_00, 1250_00) > 100_00);
    }

    [Fact]
    public void MountainAndForestAreSlowerThanTheRoad()
    {
        // 同じ長さ（約500m）を、道・森・山で歩いた時間を比べる。
        long Walk((long X, long Y) a, (long X, long Y) b) => ProtoMove.Eta(Map, a, [b]);
        long road = Walk(At(1000, 2193), At(1500, 2165));
        long forest = Walk(At(1300, 1380), At(1700, 1380));
        long mountain = Walk(At(500, 1250), At(960, 1250));
        Assert.True(road * 2 < forest, $"道{road}秒 森{forest}秒");
        Assert.True(forest < mountain, $"森{forest}秒 山{mountain}秒");
    }

    [Fact]
    public void PlansAroundTheRiverAndUsesTheFord()
    {
        var path = ProtoMove.Plan(Map, At(2400, 1450), At(3100, 1450));
        Assert.NotNull(path);
        Assert.Equal(At(3100, 1450), path![^1]);
    }

    [Fact]
    public void SameInputsGiveTheSameWorld()
    {
        string Run(params long[] chunks)
        {
            var w = new ProtoWorld(Map); w.GoToTask(w.Tasks[0].Id);
            foreach (var c in chunks) w.Advance(c);
            return string.Join("|", w.People.Select(p => $"{p.Id}:{p.X},{p.Y},{p.Doing}")) + string.Join("|", w.Letters.Select(l => $"{l.Id}:{l.Carrier}:{l.State}"));
        }
        Assert.Equal(Run(7200), Run(1, 59, 3000, 4140));
    }

    [Fact]
    public void ThePlayerCarriesTheLetterAndGetsAReply()
    {
        var w = new ProtoWorld(Map);
        var task = Assert.Single(w.Tasks);
        var eta = w.TaskEta(task); Assert.NotNull(eta);
        Assert.True(w.GoToTask(task.Id));
        w.Advance(eta!.Value + 120);
        var letter = Assert.Single(w.Deliverable);
        Assert.Contains("返書", w.Hand(letter.Id));
        Assert.Equal(TaskState.Done, task.State);
        Assert.Equal("鷹野義正へ返書を届ける", w.Tasks[1].Title);
    }

    [Fact]
    public void TheEnemyCourierWalksTheLetterToTheLord()
    {
        var w = new ProtoWorld(Map);
        w.Advance(8 * 3600);
        var letter = w.Letters.Single(l => l.From == "sawatari-nobufusa");
        Assert.Equal(LetterState.Delivered, letter.State);
        Assert.True(letter.DeliveredAt > 9 * 3600 + 600);
    }

    [Fact]
    public void ThePlayerOnlySeesNearbyPeople()
    {
        var w = new ProtoWorld(Map);
        Assert.Contains(w.Visible, p => p.Id == "takano-yoshimasa");
        Assert.DoesNotContain(w.Visible, p => p.Id == "sawatari-nobufusa");
    }

    [Fact]
    public void SavingAndLoadingGivesTheSameFuture()
    {
        static string Look(ProtoWorld w) => string.Join("|", w.People.Select(p => $"{p.Id}:{p.X},{p.Y},{p.Doing},{p.PathIndex}")) + string.Join("|", w.Letters.Select(l => $"{l.Id}:{l.Carrier}:{l.State}")) + w.Journal.Count + w.Tasks.Count;
        var straight = new ProtoWorld(Map); straight.GoToTask(straight.Tasks[0].Id); straight.Advance(3 * 3600 + 17);
        var first = new ProtoWorld(Map); first.GoToTask(first.Tasks[0].Id); first.Advance(5000);
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(first.Save(), ProtoMap.Json);
        var loaded = ProtoWorld.Load(Map, System.Text.Json.JsonSerializer.Deserialize<ProtoWorld.Saved>(bytes, ProtoMap.Json)!);
        Assert.Equal(Look(first), Look(loaded));
        loaded.Advance(3 * 3600 + 17 - 5000);
        Assert.Equal(Look(straight), Look(loaded));
    }
}
