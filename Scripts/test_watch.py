"""検査の時間の見張り（2026-11-18 ユーザー指摘「検査ごとに何分程度かわかってて、それを著しく超える場合は問題があるっていうのが検査じゃないの」）。

使い方:
  python Scripts/test_watch.py baseline <full.trx>
      全件の結果（trx）から、検査ごとのいつもの時間を Docs/Development/test-times.json に書く。
  python Scripts/test_watch.py run <結果の置き場> [dotnet test に渡す引数...]
      dotnet test を流しながら、30秒ごとに <置き場>/status.txt を書き直す。
      ・何件終わったか / 全部で何件か、失敗の数、残りの見込みの時間
      ・今動いている長い検査と、その経過時間・いつもの時間
      ・いつもの時間を大きく超えた検査は「警告」（3倍かつ5分以上の超過、記録がない検査は10分超）
      終わると、この回の時間でいつもの時間を書き直す（合格した検査だけ）。
"""
import json, os, re, subprocess, sys, threading, time
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TIMES = os.path.join(ROOT, "Docs", "Development", "test-times.json")
NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"


def seconds(d):
    h, m, s = d.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


def read_trx(path):
    root = ET.parse(path).getroot()
    tests = {}
    for r in root.iter(NS + "UnitTestResult"):
        if r.get("duration") and r.get("outcome") == "Passed":
            tests[r.get("testName")] = round(seconds(r.get("duration")), 1)
    times = root.find(NS + "Times")
    wall = None
    if times is not None:
        from datetime import datetime
        f = lambda s: datetime.fromisoformat(re.sub(r"(\.\d{6})\d*", r"\1", s))
        wall = round((f(times.get("finish")) - f(times.get("start"))).total_seconds())
    return tests, wall


def save_baseline(tests, wall, source):
    data = {"source": source, "wallSeconds": wall, "count": len(tests), "tests": dict(sorted(tests.items()))}
    with open(TIMES, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=0)
    print(f"いつもの時間を書いた：{len(tests)}件、全体 {wall} 秒 → {TIMES}")


def fmt(s):
    s = int(s)
    return f"{s // 3600}時間{s % 3600 // 60}分" if s >= 3600 else f"{s // 60}分{s % 60}秒"


def run(out_dir, extra):
    os.makedirs(out_dir, exist_ok=True)
    base = json.load(open(TIMES, encoding="utf-8")) if os.path.exists(TIMES) else {"tests": {}, "wallSeconds": None}
    expected = base["tests"]
    total_expected = sum(expected.values()) or 1
    parallel = total_expected / base["wallSeconds"] if base.get("wallSeconds") else 6.0
    cmd = ["dotnet", "test", os.path.join(ROOT, "Tests", "HakutaiKagami.Tests.csproj"), "--logger", "trx;LogFileName=full.trx",
           "--logger", "console;verbosity=normal", "--results-directory", out_dir] + extra + ["--", "xUnit.LongRunningTestSeconds=60"]
    done, failed, running = {}, [], {}
    started = time.time()
    log = open(os.path.join(out_dir, "console.log"), "w", encoding="utf-8")
    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
    finish_re = re.compile(r"^\s+(合格|失敗|スキップ|Passed|Failed|Skipped)\s+(\S+)\s+\[")
    long_re = re.compile(r"\[Long Running Test\] '([^']+)', Elapsed: (\d+):(\d+):(\d+)")
    lock = threading.Lock()

    def status(final=False):
        with lock:
            now = time.time() - started
            left = sum(v for k, v in expected.items() if k not in done)
            lines = [f"経過 {fmt(now)}　終わった {len(done)} / {len(expected) or '?'} 件　失敗 {len(failed)} 件" + ("　（終了）" if final else ""),
                     f"残りの見込み 約{fmt(left / parallel)}（いつもの時間の合計から）"]
            alerts = []
            for name, el in sorted(running.items(), key=lambda x: -x[1]):
                if name in done:
                    continue
                exp = expected.get(name)
                over = (el > max(exp * 3, exp + 300)) if exp else el > 600
                row = f"  {'警告 ' if over else ''}{name.split('.')[-1]}　{fmt(el)}　いつも {fmt(exp) if exp else '記録なし'}"
                (alerts if over else lines).append(row)
            if failed:
                lines.append("失敗：" + "、".join(n.split(".")[-1] for n in failed[:20]))
            text = "\n".join(alerts + lines) + "\n"
        with open(os.path.join(out_dir, "status.txt"), "w", encoding="utf-8") as f:
            f.write(text)

    def ticker():
        while proc.poll() is None:
            status(); time.sleep(30)
    threading.Thread(target=ticker, daemon=True).start()
    for line in proc.stdout:
        log.write(line); log.flush()
        m = finish_re.match(line)
        if m:
            with lock:
                done[m.group(2)] = True
                if m.group(1) in ("失敗", "Failed"):
                    failed.append(m.group(2))
            continue
        m = long_re.search(line)
        if m:
            with lock:
                running[m.group(1)] = int(m.group(2)) * 3600 + int(m.group(3)) * 60 + int(m.group(4))
    code = proc.wait()
    status(final=True)
    trx = os.path.join(out_dir, "full.trx")
    if os.path.exists(trx) and code == 0:
        tests, wall = read_trx(trx)
        save_baseline(tests, wall, trx)
    print(open(os.path.join(out_dir, "status.txt"), encoding="utf-8").read())
    return code


if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "baseline":
        t, w = read_trx(sys.argv[2]); save_baseline(t, w, sys.argv[2])
    elif len(sys.argv) >= 3 and sys.argv[1] == "run":
        sys.exit(run(sys.argv[2], sys.argv[3:]))
    else:
        print(__doc__); sys.exit(2)
