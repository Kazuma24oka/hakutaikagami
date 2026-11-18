"""検査のチェックポイント（検証記録/検査の途中/*.log）を、新しい行からターミナルに流し続ける。Ctrl+C で止める。"""
import os, time
D = "D:/02_プロジェクト別/百代鏡/検証記録/検査の途中"
seen = {}
start = time.time() - 3600
while True:
    for name in sorted(os.listdir(D)) if os.path.isdir(D) else []:
        p = os.path.join(D, name)
        if not name.endswith(".log") or os.path.getmtime(p) < start:
            continue
        lines = open(p, encoding="utf-8", errors="replace").read().split("\n")
        for l in lines[seen.get(name, 0):-1]:
            print(f"◆ {name.rsplit('_', 1)[0]}　{l}", flush=True)
        seen[name] = max(len(lines) - 1, 0)
    time.sleep(3)
