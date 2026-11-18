"""検査の時間の見張り（2026-11-18 ユーザー指摘「検査ごとに何分程度かわかってて、それを著しく超える場合は問題があるっていうのが検査じゃないの」
「忘れていたとか再現性がないことしないでください」）。

決まり（AGENTS.md）：全件や長い検査は必ずこれで流す。

記録と基準:
  ・回ごとの記録：流した回ごとに「始めた日時・コミット・検査ごとの時間」を
    D:/02_プロジェクト別/百代鏡/検証記録/検査の時間/<日時>.json に残す。消さずに積み上げる。
    途中で止まった回も、それまでに終わった分を残す（30秒ごとに書き直す）。
  ・いつもの時間：検査ごとに、記録のある直近5回の中央値。Docs/Development/test-times.json（Git）に、
    どの回から作ったかと一緒に書く。回が終わるたびに作り直す。
  ・長いかどうか：今の経過がいつもの時間の3倍を超え、かつ5分以上多いと「警告」。
    いつもの時間の記録がない検査は、10分を超えたら「警告」。
  ・今動いている検査の経過は、xUnit の長引いている検査の知らせ（60秒ごと）から取る。

使い方:
  python Scripts/test_watch.py run <結果の置き場> [dotnet test に渡す引数...]
      dotnet test を流しながら、30秒ごとに <置き場>/status.txt を書き直す。
      通った検査は <置き場>/passed.txt に1件ずつ残す。止まったら同じ置き場でもう一度 run すると、
      全部が通った組（クラス）を外して続きから流す。
  python Scripts/test_watch.py baseline <full.trx>
      全件の結果（trx）を1回分の記録として足し、いつもの時間を作り直す。
  python Scripts/test_watch.py compare [件数]
      回ごとの検査1つ1つの時間を並べ、最後の回で長い順に出す（どこが詰まっているかを見る）。
      検査の時間/比較.txt にも書く。
  python Scripts/test_watch.py record <結果の置き場>
      置き場の console.log から1回分の記録を作って足し、いつもの時間を作り直す（古い版の見張りで流した回のため）。
"""
import json, os, re, subprocess, sys, threading, time
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TIMES = os.path.join(ROOT, "Docs", "Development", "test-times.json")
HISTORY = os.environ.get("HAKUTAI_TEST_HISTORY", "D:/02_プロジェクト別/百代鏡/検証記録/検査の時間")
KEEP = 5
NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
NL = chr(10)
PASSED = ("成功", "合格", "Passed")
FINISH = re.compile(r"^\s+(成功|合格|失敗|スキップ|Passed|Failed|Skipped)\s+(.+?)\s+\[([^\]]*)\]\s*$")
LONG = re.compile(r"\[Long Running Test\] '([^']+)', Elapsed: (\d+):(\d+):(\d+)")
DUR = re.compile(r"(\d+(?:\.\d+)?)\s*(ms|h|m|s)")


def dur(text):
    return round(sum(float(v) * {"ms": 0.001, "s": 1, "m": 60, "h": 3600}[u] for v, u in DUR.findall(text)), 1)


def fmt(s):
    s = int(s)
    return f"{s // 3600}時間{s % 3600 // 60}分" if s >= 3600 else f"{s // 60}分{s % 60}秒"


def commit():
    try:
        return subprocess.run(["git", "-C", ROOT, "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
    except OSError:
        return ""


def read_trx(path):
    root = ET.parse(path).getroot()
    tests = {}
    for r in root.iter(NS + "UnitTestResult"):
        if r.get("duration") and r.get("outcome") == "Passed":
            h, m, s = r.get("duration").split(":")
            tests[r.get("testName")] = round(int(h) * 3600 + int(m) * 60 + float(s), 1)
    times = root.find(NS + "Times")
    wall = None
    if times is not None:
        from datetime import datetime
        f = lambda s: datetime.fromisoformat(re.sub(r"(\.\d{6})\d*", r"\1", s))
        wall = round((f(times.get("finish")) - f(times.get("start"))).total_seconds())
    return tests, wall


def from_console(path):
    tests = {}
    for line in open(path, encoding="utf-8", errors="replace"):
        m = FINISH.match(line)
        if m and m.group(1) in PASSED:
            tests[m.group(2)] = dur(m.group(3))
    return tests


def write_run(run_id, tests, wall, kind, source, sha=None):
    os.makedirs(HISTORY, exist_ok=True)
    data = {"run": run_id, "commit": sha if sha is not None else commit(), "kind": kind, "source": source, "wallSeconds": wall,
            "count": len(tests), "tests": dict(sorted(tests.items()))}
    with open(os.path.join(HISTORY, run_id + ".json"), "w", encoding="utf-8", newline=NL) as f:
        json.dump(data, f, ensure_ascii=False, indent=0)


def rebuild():
    """検査ごとに、記録のある直近5回の中央値で、いつもの時間を作り直す。"""
    runs = []
    if os.path.isdir(HISTORY):
        for name in sorted(os.listdir(HISTORY)):
            if name.endswith(".json"):
                runs.append(json.load(open(os.path.join(HISTORY, name), encoding="utf-8")))
    per = {}
    for r in runs:  # 古い順（名前が日時）
        for t, sec in r["tests"].items():
            per.setdefault(t, []).append(sec)
    med = {}
    for t, xs in per.items():
        xs = sorted(xs[-KEEP:]); n = len(xs)
        med[t] = round(xs[n // 2] if n % 2 else (xs[n // 2 - 1] + xs[n // 2]) / 2, 1)
    walls = [r["wallSeconds"] for r in runs if r.get("kind") in ("full", "imported") and r.get("wallSeconds")][-KEEP:]
    wall = sorted(walls)[len(walls) // 2] if walls else None
    data = {"method": f"検査ごとに記録のある直近{KEEP}回の中央値", "history": HISTORY, "runs": [r["run"] for r in runs][-20:],
            "wallSeconds": wall, "count": len(med), "tests": dict(sorted(med.items()))}
    with open(TIMES, "w", encoding="utf-8", newline=NL) as f:
        json.dump(data, f, ensure_ascii=False, indent=0)
    print(f"いつもの時間を作り直した：{len(med)}件（回の記録 {len(runs)} 回）→ {TIMES}")


PAGE = os.path.join(os.path.dirname(HISTORY), "検査の様子.html")


def page(text, final, out_dir):
    """あなたが自分で見られる様子のページ。開いておくと30秒ごとに読み直す。"""
    import html
    now = time.strftime("%H時%M分%S秒")
    state = "終わった" if final else "動いている"
    color = "#2f7d4f" if final else "#3a3f8f"
    rows = "".join(f"<li class='{'warn' if l.strip().startswith('警告') else ''}'>{html.escape(l.strip())}</li>" for l in text.split(NL) if l.strip())
    body = f"""<!doctype html><html lang="ja"><head><meta charset="utf-8"><meta http-equiv="refresh" content="30">
<meta name="viewport" content="width=device-width, initial-scale=1"><title>検査の様子</title>
<style>body{{font-family:"Yu Gothic UI",sans-serif;background:#f6f4ee;color:#2b2a27;margin:0;padding:24px;line-height:1.8}}
@media (prefers-color-scheme:dark){{body{{background:#1d1c1a;color:#ece9e1}}}}
.state{{display:inline-block;padding:4px 14px;border-radius:999px;color:#fff;background:{color};font-weight:600}}
li{{list-style:none;padding:6px 10px;border-bottom:1px solid #8884}} .warn{{color:#fff;background:#b3261e;border-radius:6px}}
small{{opacity:.7}}</style></head><body>
<p><span class="state">{state}</span>　<small>最後に書き直した時刻 {now}（30秒ごと。この時刻が2分以上古いなら、見張りが止まっている）</small></p>
<ul>{rows}</ul><p><small>置き場：{html.escape(out_dir)}</small></p></body></html>"""
    with open(PAGE, "w", encoding="utf-8") as f:
        f.write(body)


def run(out_dir, extra):
    if not os.path.isabs(out_dir):  # 名前だけなら検証記録の下に置く（ターミナルで日本語の道を打たなくて済むように）
        out_dir = os.path.join(os.path.dirname(HISTORY), out_dir)
    os.makedirs(out_dir, exist_ok=True)
    base = json.load(open(TIMES, encoding="utf-8")) if os.path.exists(TIMES) else {"tests": {}, "wallSeconds": None}
    expected = base["tests"]
    partial = "--filter" in extra  # 一部だけ流すときは、全部の件数と残りの見込みを出さない
    total_expected = sum(expected.values()) or 1
    parallel = total_expected / base["wallSeconds"] if base.get("wallSeconds") else 6.0
    # 続きから：同じ置き場で前に通った検査は流し直さない。全部が通った組（クラス）を外して流す。
    passed_file = os.path.join(out_dir, "passed.txt")
    old_log = os.path.join(out_dir, "console.log")
    if not os.path.exists(passed_file) and os.path.exists(old_log):
        with open(passed_file, "w", encoding="utf-8") as f:
            for t in from_console(old_log):
                f.write(t + NL)
    before = set(open(passed_file, encoding="utf-8").read().split(NL)) - {""} if os.path.exists(passed_file) else set()
    filt = []
    if before:
        listed = subprocess.run(["dotnet", "test", os.path.join(ROOT, "Tests", "HakutaiKagami.Tests.csproj"), "--list-tests", "--no-build"],
                                capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
        names = [l.strip() for l in listed.splitlines() if l.startswith("    HakutaiKagami.")]
        classes = {}
        for n in names:
            classes.setdefault(n.split("(")[0].rsplit(".", 1)[0], []).append(n)
        skip = [c for c, ts in classes.items() if all(t in before for t in ts)]
        if skip:
            filt = ["--filter", "&".join("FullyQualifiedName!~" + c + "." for c in skip)]
        print(f"続きから：前に通った {len(before)} 件、外す組 {len(skip)} / {len(classes)}")
    trx_name = "full.trx" if not before else f"rest-{int(time.time())}.trx"
    cmd = ["dotnet", "test", os.path.join(ROOT, "Tests", "HakutaiKagami.Tests.csproj"), "--logger", "trx;LogFileName=" + trx_name,
           "--logger", "console;verbosity=normal", "--results-directory", out_dir] + filt + extra + \
          ["--", "xUnit.LongRunningTestSeconds=60", "xUnit.DiagnosticMessages=true"]
    done, failed, running, times = {n: True for n in before}, [], {}, {}
    started = time.time()
    run_id = time.strftime("%Y-%m-%d_%H%M%S") + ("_続き" if before else "_全件")
    kind = "rest" if before else "full"
    sha = commit()
    log = open(old_log, "a", encoding="utf-8")
    passed_out = open(passed_file, "a", encoding="utf-8")
    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
    lock = threading.Lock()

    def status(final=False):
        with lock:
            now = time.time() - started
            left = sum(v for k, v in expected.items() if k not in done)
            lines = [f"経過 {fmt(now)}　終わった {len(done)}" + ("" if partial else f" / {len(expected) or '?'}") + f" 件　失敗 {len(failed)} 件" + ("　（終了）" if final else "")]
            if not partial:
                lines.append(f"残りの見込み 約{fmt(left / parallel)}（いつもの時間の合計から）")
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
            text = NL.join(alerts + lines) + NL
            snap = dict(times)
        with open(os.path.join(out_dir, "status.txt"), "w", encoding="utf-8") as f:
            f.write(text)
        page(text, final, out_dir)
        write_run(run_id, snap, round(time.time() - started), kind if final else "running", out_dir, sha)

    def say(text):
        print(text, flush=True)

    # 検査が書くチェックポイント（検証記録/検査の途中/*.log）の新しい行を、そのままターミナルに流す。
    cp_dir = os.path.join(os.path.dirname(HISTORY), "検査の途中")
    cp_seen = {}

    def checkpoints():
        if not os.path.isdir(cp_dir):
            return
        for name in sorted(os.listdir(cp_dir)):
            path = os.path.join(cp_dir, name)
            if not name.endswith(".log") or os.path.getmtime(path) < started - 5:
                continue
            with open(path, encoding="utf-8", errors="replace") as f:
                lines = f.read().split(NL)
            for l in lines[cp_seen.get(name, 0):-1]:
                say(f"   ◆ {name.rsplit('_', 1)[0]}　{l}")
            cp_seen[name] = max(len(lines) - 1, 0)

    def cp_loop():
        while proc.poll() is None:
            checkpoints(); time.sleep(3)
        checkpoints()
    threading.Thread(target=cp_loop, daemon=True).start()

    def ticker():
        while proc.poll() is None:
            status()
            with lock:
                say(f"── 経過 {fmt(time.time() - started)}　終わった {len(done)}" + ("" if partial else f" / {len(expected)}") + f" 件　失敗 {len(failed)} 件")
            time.sleep(30)
    threading.Thread(target=ticker, daemon=True).start()
    for line in proc.stdout:
        log.write(line); log.flush()
        m = FINISH.match(line)
        if m:
            with lock:
                done[m.group(2)] = True
                if m.group(1) in ("失敗", "Failed"):
                    failed.append(m.group(2))
                elif m.group(1) in PASSED:
                    passed_out.write(m.group(2) + NL); passed_out.flush()
                    times[m.group(2)] = dur(m.group(3))
                n = len(done)
            short = m.group(2).split(".", 2)[-1]
            exp = expected.get(m.group(2))
            slow = exp and dur(m.group(3)) > max(exp * 3, exp + 300)
            if m.group(1) in ("失敗", "Failed"):
                say(f"[{n}] 失敗　{short}　{m.group(3)}")
            elif slow or dur(m.group(3)) >= 60:
                say(f"[{n}] {'遅い ' if slow else ''}成功　{short}　{m.group(3)}（いつも {fmt(exp) if exp else '記録なし'}）")
            continue
        m = LONG.search(line)
        if m:
            el = int(m.group(2)) * 3600 + int(m.group(3)) * 60 + int(m.group(4))
            with lock:
                running[m.group(1)] = el
            exp = expected.get(m.group(1)); over = (el > max(exp * 3, exp + 300)) if exp else el > 600
            say(f"   {'警告 ' if over else ''}動いている　{m.group(1).split('.', 2)[-1]}　{fmt(el)}（いつも {fmt(exp) if exp else '記録なし'}）")
            continue
        if "error" in line.lower() or "エラー" in line or "中止" in line:
            say("   " + line.rstrip())
    code = proc.wait()
    if code != 0 and not failed:
        kind = "stopped"
    status(final=True)
    rebuild()
    compare()
    print(open(os.path.join(out_dir, "status.txt"), encoding="utf-8").read())
    return code


def compare(top=40):
    """回ごとの、検査1つ1つの時間を並べる。最後の回で長い順。いつもより大きく遅い検査に印。"""
    runs = []
    for name in sorted(os.listdir(HISTORY)):
        if name.endswith(".json"):
            runs.append(json.load(open(os.path.join(HISTORY, name), encoding="utf-8")))
    runs = runs[-6:]
    last = runs[-1]["tests"]
    base = json.load(open(TIMES, encoding="utf-8"))["tests"] if os.path.exists(TIMES) else {}
    head = "検査　|　" + "　|　".join(r["run"] for r in runs) + "　|　いつも"
    rows = [head]
    for t in sorted(set().union(*[r["tests"] for r in runs]), key=lambda t: -last.get(t, 0))[:top]:
        cells = [fmt(r["tests"][t]) if t in r["tests"] else "－" for r in runs]
        exp = base.get(t); mark = "遅い " if exp and t in last and last[t] > max(exp * 3, exp + 300) else ""
        rows.append(f"{mark}{t.split('.')[-1]}　|　" + "　|　".join(cells) + f"　|　{fmt(exp) if exp else '－'}")
    text = NL.join(rows) + NL
    with open(os.path.join(HISTORY, "比較.txt"), "w", encoding="utf-8") as f:
        f.write(text)
    print(text)


if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "baseline":
        t, w = read_trx(sys.argv[2])
        write_run(time.strftime("%Y-%m-%d_%H%M%S", time.localtime(os.path.getmtime(sys.argv[2]))) + "_取り込み", t, w, "imported", sys.argv[2], "")
        rebuild()
    elif len(sys.argv) >= 3 and sys.argv[1] == "record":
        log = os.path.join(sys.argv[2], "console.log")
        write_run(time.strftime("%Y-%m-%d_%H%M%S", time.localtime(os.path.getmtime(log))) + "_記録から", from_console(log), None, "from-console", sys.argv[2], "")
        rebuild()
    elif len(sys.argv) >= 2 and sys.argv[1] == "compare":
        compare(int(sys.argv[2]) if len(sys.argv) >= 3 else 40)
    elif len(sys.argv) >= 3 and sys.argv[1] == "run":
        sys.exit(run(sys.argv[2], sys.argv[3:]))
    else:
        print(__doc__); sys.exit(2)
