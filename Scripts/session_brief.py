"""会話の始まりと要約のたびに読ませる要点（.claude/settings.json の SessionStart から呼ぶ。2026-11-18 ユーザー「忘れていましたが多すぎる」）。
今のブランチ、約束の台帳（Docs/Development/OPEN_ITEMS.md）の済んでいない行、ファイルの台帳の漏れの有無を出す。"""
import os, subprocess, sys
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.stdout.reconfigure(encoding="utf-8")
branch = subprocess.run(["git", "-C", ROOT, "branch", "--show-current"], capture_output=True, text=True).stdout.strip()
print(f"# 今の作業（自動で読み込み）\nブランチ：{branch}　試作版の進め方：Docs/Development/PROTOTYPE_TRACK.md　決まり：AGENTS.md")
print("約束の台帳（Docs/Development/OPEN_ITEMS.md）の済んでいない行：")
for line in open(os.path.join(ROOT, "Docs", "Development", "OPEN_ITEMS.md"), encoding="utf-8"):
    cells = [c.strip() for c in line.split("|")]
    if len(cells) > 5 and cells[1].isdigit() and not cells[4].startswith("済"):
        print(f"  #{cells[1]} {cells[2][:80]}　→ {cells[4][:40]}　次：{cells[5][:100]}")
r = subprocess.run([sys.executable, os.path.join(ROOT, "Scripts", "file_ledger.py"), "check"], capture_output=True, text=True, encoding="utf-8", env={**os.environ, "PYTHONIOENCODING": "utf-8"})
print("ファイルの台帳：" + (r.stdout.strip().splitlines() or ["不明"])[-1])
