using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// 長い検査のチェックポイント（2026-11-18 ユーザー「検査ログというか内容のチェックポイントみたいなのをつけてチェックするのが普通じゃないの？」）。
// ・途中の様子をその場でファイルへ書く（検査が止められても、どこまで進んだかが残る）。置き場は HAKUTAI_TEST_CHECKPOINTS、
//   無ければ D:\02_プロジェクト別\百代鏡\検証記録\検査の途中（無い環境では一時フォルダ）。
// ・Expect で途中の確かめをする。外れたら、何を確かめて外れたかと途中の様子のファイルを書いた失敗にする。
sealed class Checkpoints : IDisposable
{
    readonly StreamWriter file; readonly ITestOutputHelper? output; readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    public string Path { get; }
    public Checkpoints(string test, ITestOutputHelper? output = null)
    {
        this.output = output;
        var root = Environment.GetEnvironmentVariable("HAKUTAI_TEST_CHECKPOINTS") is { Length: > 0 } custom ? custom
            : Directory.Exists(@"D:\02_プロジェクト別\百代鏡\検証記録") ? @"D:\02_プロジェクト別\百代鏡\検証記録\検査の途中" : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hakutai-checkpoints");
        Directory.CreateDirectory(root);
        Path = System.IO.Path.Combine(root, $"{test}_{DateTime.Now:yyyyMMdd-HHmmss}.log");
        file = new StreamWriter(Path, false, new UTF8Encoding(false)) { AutoFlush = true };
        Log("始め");
    }
    public static string Game(long seconds) => $"{seconds / 86400 + 1}日目{seconds % 86400 / 3600}時{seconds % 3600 / 60:00}分";
    public void Log(string text)
    {
        var line = $"[実時間 {clock.Elapsed.Hours:00}:{clock.Elapsed.Minutes:00}:{clock.Elapsed.Seconds:00}] {text}";
        file.WriteLine(line); output?.WriteLine(line);
    }
    public void Expect(bool ok, string what)
    {
        Log((ok ? "確かめ OK　" : "確かめ NG　") + what);
        Assert.True(ok, $"{what}（途中の様子：{Path}）");
    }
    // 二つの世界が同じかを確かめ、違えば「どこが違うか」（JSON の道を最大20か所）を書き出してから失敗にする。
    public void ExpectSame(string a, string b, string what)
    {
        if (a == b) { Log("確かめ OK　" + what); return; }
        var diffs = new List<string>();
        void Walk(System.Text.Json.JsonElement x, System.Text.Json.JsonElement y, string path)
        {
            if (diffs.Count >= 20) return;
            if (x.ValueKind != y.ValueKind) { diffs.Add($"{path}：種類が違う"); return; }
            switch (x.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    var names = x.EnumerateObject().Select(p => p.Name).Union(y.EnumerateObject().Select(p => p.Name));
                    foreach (var n in names)
                    {
                        bool hx = x.TryGetProperty(n, out var vx), hy = y.TryGetProperty(n, out var vy);
                        if (!hx || !hy) diffs.Add($"{path}.{n}：片方にだけある"); else if (vx.GetRawText() != vy.GetRawText()) Walk(vx, vy, path + "." + n);
                    }
                    break;
                case System.Text.Json.JsonValueKind.Array:
                    int lx = x.GetArrayLength(), ly = y.GetArrayLength();
                    if (lx != ly) diffs.Add($"{path}：数が違う {lx} と {ly}");
                    for (int i = 0; i < Math.Min(lx, ly); i++) if (x[i].GetRawText() != y[i].GetRawText()) Walk(x[i], y[i], $"{path}[{i}]");
                    break;
                default:
                    var tx = x.GetRawText(); var ty = y.GetRawText();
                    diffs.Add($"{path}：{(tx.Length > 80 ? tx[..80] : tx)} と {(ty.Length > 80 ? ty[..80] : ty)}"); break;
            }
        }
        using (var dx = System.Text.Json.JsonDocument.Parse(a)) using (var dy = System.Text.Json.JsonDocument.Parse(b)) Walk(dx.RootElement, dy.RootElement, "世界");
        foreach (var d in diffs) Log("　違い　" + d);
        Expect(false, what);
    }
    public void Dispose() { Log("終わり"); file.Dispose(); }
}
