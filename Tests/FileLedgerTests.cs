using System.Diagnostics;
using Xunit;

namespace HakutaiKagami.Tests;

// ファイルの台帳（Docs/Development/FileLedger/ledger.tsv、Scripts/file_ledger.py）に、すべてのファイルが載っていて、
// 「未確認」が無いことを、普段の検査で確かめる（2026-11-18 ユーザー「ひとつも確認漏れを許しません。再現性がある仕組みに」）。
// 新しいファイルを足したら、台帳に判断と理由を書く。書き忘れるとこの検査が失敗する。
public sealed class FileLedgerTests
{
    static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    static string[] Git(params string[] args)
    {
        var info = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
        foreach (var a in new[] { "-C", Root, "-c", "core.quotepath=false" }.Concat(args)) info.ArgumentList.Add(a);
        using var p = Process.Start(info)!; var text = p.StandardOutput.ReadToEnd(); p.WaitForExit();
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public void EveryFileIsInTheLedgerWithADecision()
    {
        var files = Git("ls-files").Concat(Git("ls-files", "-o", "--exclude-standard")).ToHashSet(StringComparer.Ordinal);
        var rows = File.ReadAllLines(Path.Combine(Root, "Docs", "Development", "FileLedger", "ledger.tsv")).Skip(1)
            .Select(l => l.Split('\t')).ToDictionary(c => c[0], c => c, StringComparer.Ordinal);
        string[] decisions = ["使う", "記録", "書き直す", "移す", "消す", "手元だけ"];
        var missing = files.Where(f => !rows.ContainsKey(f)).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        var gone = rows.Keys.Where(f => !files.Contains(f)).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        var unchecked_ = rows.Where(r => !decisions.Contains(r.Value[1]) || string.IsNullOrWhiteSpace(r.Value[2])).Select(r => r.Key).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0, "台帳に無いファイル：" + string.Join("、", missing.Take(20)) + "（python Scripts/file_ledger.py scan で足して判断を書く）");
        Assert.True(gone.Length == 0, "もう無いファイルの行：" + string.Join("、", gone.Take(20)));
        Assert.True(unchecked_.Length == 0, $"判断か理由の無い行 {unchecked_.Length} 件：" + string.Join("、", unchecked_.Take(20)));
    }
}
