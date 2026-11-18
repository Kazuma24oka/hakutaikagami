"""ファイルの台帳（2026-11-18 ユーザー「すべてのファイルを確認して整理してください。ひとつも確認漏れを許しません。
二度と同じような指摘がないように再現性がある仕組みにしてください」）。

台帳：Docs/Development/FileLedger/ledger.tsv（1ファイル1行）
  列：path / 判断 / 理由 / 根拠 / 確かめた日
  判断は次のどれか：
    使う        今のゲーム・検査・道具が使っている
    記録        過去の工程の記録として残す（読み返す理由がある）
    書き直す    中身が古い・重なっている。書き直して残す
    移す        Docs/Archive などへ移す候補（ユーザーの承認が要る）
    消す        要らない候補（ユーザーの承認が要る）
    手元だけ    Git に入れずに手元に置く（権利・大きさの理由）
    未確認      まだ確かめていない（check で失敗する）

使い方:
  python Scripts/file_ledger.py scan      全ファイルの事実（大きさ・最後の更新・ほかのファイルからの参照の数）を
                                          Docs/Development/FileLedger/facts.tsv に書き、台帳に無いファイルを「未確認」で足し、
                                          もう無いファイルの行を消す。
  python Scripts/file_ledger.py check     台帳に無いファイル・「未確認」・もう無いファイルの行が1つでもあれば失敗（終了コード1）。
                                          Tests/FileLedgerTests.cs が同じことを確かめるので、普段の検査で必ず止まる。
  python Scripts/file_ledger.py summary   判断ごとの数を出す。
  python Scripts/file_ledger.py report    台帳から、判断ごと・場所ごとの一覧のページ Docs/Development/FileLedger/review.html を作る。
  python Scripts/file_ledger.py import <確かめた結果.tsv>
                                          path・判断・理由・根拠 の表を台帳に取り込む。表の path が台帳に無い、判断の書き方が違う、
                                          理由か根拠が空、のどれかがあれば何も取り込まずに失敗する。

対象：git に入っているファイルと、入っていないが .gitignore で外していないファイルの全部。
"""
import csv, os, re, subprocess, sys, time
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, "Docs", "Development", "FileLedger")
LEDGER = os.path.join(DIR, "ledger.tsv")
FACTS = os.path.join(DIR, "facts.tsv")
DECISIONS = ["使う", "記録", "書き直す", "移す", "消す", "手元だけ", "未確認"]
HEAD = ["path", "判断", "理由", "根拠", "確かめた日"]
NL = chr(10)
TEXT = {".cs", ".md", ".json", ".py", ".tscn", ".tres", ".gd", ".gdshader", ".html", ".txt", ".ps1", ".cjs", ".csproj", ".godot", ".import", ".cfg", ".yml", ".yaml", ".gitignore", ".js", ".css", ".xml", ".sln"}


def git(*args):
    return subprocess.run(["git", "-C", ROOT, "-c", "core.quotepath=false", *args], capture_output=True, text=True, encoding="utf-8").stdout


def files():
    tracked = [l for l in git("ls-files").split("\n") if l]
    untracked = [l for l in git("ls-files", "-o", "--exclude-standard").split("\n") if l]
    return sorted(set(tracked) | set(untracked)), set(tracked)


def read_ledger():
    rows = {}
    if os.path.exists(LEDGER):
        with open(LEDGER, encoding="utf-8", newline="") as f:
            for r in csv.DictReader(f, delimiter="\t"):
                rows[r["path"]] = r
    return rows


def write_ledger(rows):
    os.makedirs(DIR, exist_ok=True)
    with open(LEDGER, "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=HEAD, delimiter="\t", lineterminator="\n")
        w.writeheader()
        for p in sorted(rows):
            w.writerow({k: rows[p].get(k, "") for k in HEAD})


def last_commits():
    """ファイルごとの最後のコミットの日付（一度の git log で集める）。"""
    out = git("log", "--format=@%ad", "--date=short", "--name-only")
    seen, day = {}, ""
    for line in out.split("\n"):
        if line.startswith("@"):
            day = line[1:]
        elif line and line not in seen:
            seen[line] = day
    return seen


def references(all_files):
    """ほかのファイルからの参照の数：ファイル名（と、.cs は型の名前）が、ほかのテキストのファイルに何回出てくるか。"""
    names = defaultdict(set)
    for p in all_files:
        base = os.path.basename(p)
        names[base].add(p)
        if p.endswith(".cs"):
            names[base[:-3]].add(p)
        if base.endswith(".import"):
            names[base[:-7]].add(p)
    count = Counter()
    token = re.compile(r"[\w\-.぀-ヿ一-鿿]+")
    for p in all_files:
        ext = os.path.splitext(p)[1].lower()
        if ext not in TEXT and not p.endswith(".gitignore"):
            continue
        try:
            text = open(os.path.join(ROOT, p), encoding="utf-8", errors="replace").read()
        except OSError:
            continue
        for t in set(token.findall(text)):
            for q in names.get(t, ()):
                if q != p:
                    count[q] += 1
    return count


def scan():
    all_files, tracked = files()
    commits = last_commits()
    refs = references(all_files)
    os.makedirs(DIR, exist_ok=True)
    with open(FACTS, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, delimiter="\t", lineterminator="\n")
        w.writerow(["path", "git", "bytes", "最後のコミット", "参照の数"])
        for p in all_files:
            full = os.path.join(ROOT, p)
            size = os.path.getsize(full) if os.path.exists(full) else -1
            w.writerow([p, "入っている" if p in tracked else "入っていない", size, commits.get(p, ""), refs.get(p, 0)])
    rows = read_ledger()
    gone = [p for p in rows if p not in set(all_files)]
    for p in gone:
        del rows[p]
    added = 0
    for p in all_files:
        if p not in rows:
            rows[p] = {"path": p, "判断": "未確認", "理由": "", "根拠": "", "確かめた日": ""}
            added += 1
    write_ledger(rows)
    print(f"全 {len(all_files)} ファイル（Git {len(tracked)}）。台帳に足した {added}、無くなって消した {len(gone)}。事実 → {FACTS}")


def check():
    all_files, _ = files()
    rows = read_ledger()
    missing = [p for p in all_files if p not in rows]
    unknown = [p for p, r in rows.items() if r["判断"] == "未確認"]
    bad = [p for p, r in rows.items() if r["判断"] not in DECISIONS]
    gone = [p for p in rows if p not in set(all_files)]
    noreason = [p for p, r in rows.items() if r["判断"] != "未確認" and not r["理由"].strip()]
    problems = [("台帳に無いファイル", missing), ("未確認", unknown), ("判断の書き方が違う", bad), ("もう無いファイルの行", gone), ("理由が空", noreason)]
    ok = True
    for title, items in problems:
        if items:
            ok = False
            print(f"{title}：{len(items)} 件　例：{items[:5]}")
    print("台帳：漏れなし" if ok else "台帳：漏れあり")
    return 0 if ok else 1


def summary():
    rows = read_ledger()
    c = Counter(r["判断"] for r in rows.values())
    for d in DECISIONS:
        print(f"{d}\t{c.get(d, 0)}")
    print(f"計\t{len(rows)}")


def report():
    import html
    rows = read_ledger()
    order = ["消す", "移す", "手元だけ", "書き直す", "記録", "使う"]
    by = defaultdict(lambda: defaultdict(list))
    for r in rows.values():
        parts = r["path"].split("/")
        folder = "/".join(parts[:3]) if len(parts) > 3 else "/".join(parts[:-1]) or "（ルート）"
        by[r["判断"]][folder].append(r)
    c = Counter(r["判断"] for r in rows.values())
    out = [f"""<!doctype html><html lang="ja"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>ファイルの整理の候補</title>
<style>:root{{--bg:#f6f4ee;--card:#fff;--text:#2b2a27;--muted:#6b6962;--line:#ddd9cf}}@media (prefers-color-scheme:dark){{:root{{--bg:#1d1c1a;--card:#272623;--text:#ece9e1;--muted:#a6a39a;--line:#3a3833}}}}
body{{margin:0;background:var(--bg);color:var(--text);font-family:"Yu Gothic UI",sans-serif;line-height:1.7}}main{{max-width:1100px;margin:0 auto;padding:24px 16px 48px}}
h1{{font-size:22px}}h2{{font-size:18px;margin-top:28px}}details{{background:var(--card);border:1px solid var(--line);border-radius:10px;margin:6px 0;padding:6px 12px}}
summary{{cursor:pointer}}table{{width:100%;border-collapse:collapse;font-size:13px}}td{{border-top:1px solid var(--line);padding:4px 6px;vertical-align:top;word-break:break-all}}
.n{{color:var(--muted)}}</style></head><body><main><h1>ファイルの整理の候補</h1>
<p class="n">台帳 Docs/Development/FileLedger/ledger.tsv から {time.strftime('%Y-%m-%d %H:%M')} に作った。全 {len(rows)} 件：""" + "　".join(f"{d} {c.get(d,0)}" for d in order) + "</p>"]
    for d in order:
        out.append(f"<h2>{d}　{c.get(d,0)} 件</h2>")
        for folder, items in sorted(by[d].items(), key=lambda x: -len(x[1])):
            out.append(f"<details><summary>{html.escape(folder)}　<span class='n'>{len(items)} 件</span></summary><table>")
            for r in sorted(items, key=lambda r: r["path"]):
                out.append(f"<tr><td>{html.escape(r['path'])}</td><td>{html.escape(r['理由'])}</td><td class='n'>{html.escape(r['根拠'])}</td></tr>")
            out.append("</table></details>")
    out.append("</main></body></html>")
    path = os.path.join(DIR, "review.html")
    with open(path, "w", encoding="utf-8", newline=NL) as f:
        f.write(NL.join(out))
    print(f"一覧 → {path}")


def import_(path):
    rows = read_ledger()
    with open(path, encoding="utf-8", newline="") as f:
        got = list(csv.DictReader(f, delimiter=chr(9)))
    errors = []
    for r in got:
        p = r.get("path", "")
        if p not in rows: errors.append(f"台帳に無い：{p}")
        elif r.get("判断") not in DECISIONS[:-1]: errors.append(f"判断が違う：{p} {r.get('判断')}")
        elif not (r.get("理由") or "").strip() or not (r.get("根拠") or "").strip(): errors.append(f"理由か根拠が空：{p}")
    if len({r['path'] for r in got}) != len(got): errors.append("同じ path が2回ある")
    if errors:
        print(f"取り込まない（{len(errors)} 件の問題）："); [print("  " + e) for e in errors[:30]]; return 1
    today = time.strftime("%Y-%m-%d")
    for r in got:
        rows[r["path"]].update({"判断": r["判断"], "理由": r["理由"].strip(), "根拠": r["根拠"].strip(), "確かめた日": today})
    write_ledger(rows)
    print(f"取り込んだ {len(got)} 件（{os.path.basename(path)}）")
    return 0


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "scan":
        scan()
    elif cmd == "check":
        sys.exit(check())
    elif cmd == "summary":
        summary()
    elif cmd == "report":
        report()
    elif cmd == "import" and len(sys.argv) > 2:
        sys.exit(import_(sys.argv[2]))
    else:
        print(__doc__); sys.exit(2)
