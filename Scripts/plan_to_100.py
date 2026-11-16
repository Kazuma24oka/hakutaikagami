"""進捗表100%までの工程表（Docs/Development/PLAN_TO_100.json）を読み、目標・今の率・次の工程を示す。

--brief : 会話の開始・圧縮の後に文脈へ入れる短い要約（.claude/settings.json の SessionStart から呼ぶ）。
--check : 工程表の決まりを確かめる（100%未満の枠がすべて工程表にある、「切り上げ」が無い、後回しには戻る工程がある）。
"""
from pathlib import Path
import json, sys

R = Path(__file__).resolve().parent.parent
PLAN = R / "Docs/Development/PLAN_TO_100.json"
EST = R / "Docs/Development/ProgressV1/estimate.json"
PROG = R / "Docs/Development/ProgressV1/progress.json"

RULES = [
    "目標は進捗表（ProgressV1）の100%（2026-11-12 ユーザー「次で100％になるまで進めてほしい」）。",
    "工程表は60枠すべての残りを入れて100%に届くものだけ。届かない部分（正式な絵の制作など）は工程表を出すときに最初に示してユーザーの判断をもらう。",
    "「切り上げ」で次へ進まない。残りのある枠を「完了」と言わない。後回しは、前提の工程と戻る工程を工程表に書く。",
    "率と残りは毎回このスクリプト（progress.json・estimate.json）から言う。会話の要約から言わない。",
]


def load(p):
    return json.loads(p.read_bytes())


def state():
    e, p = load(EST), load(PROG)
    leaves = {k: v["percent"] for k, v in e["leaves"].items()}
    remaining = {i["id"]: i.get("remaining", "") for i in p["items"]}
    total = e.get("approval_batch", {}).get("current")
    plan = load(PLAN) if PLAN.is_file() else None
    return leaves, remaining, total, plan


def check(leaves, plan):
    errors = []
    if plan is None:
        return ["工程表 Docs/Development/PLAN_TO_100.json がまだ無い"]
    steps = plan.get("steps", [])
    ids = [s["id"] for s in steps]
    # 残りのある枠は、まだ終わっていない工程の枠に入っていること（終わった工程だけに入っている枠は、誰も100%へ上げない）。
    covered = {f for s in steps if s.get("status") != "完了" for f in s.get("frames", [])}
    for f, pct in leaves.items():
        if pct < 100 and f not in covered:
            errors.append(f"{f}（{pct}%）が、まだ終わっていない工程のどれにも無い")
    # 後回しは、戻る工程の枠に、後回しした工程の枠のどれかが入っていること。
    for s in steps:
        for d in s.get("deferred", []):
            back = next((x for x in steps if x["id"] == d.get("returns_in")), None)
            if back is not None and d.get("frames") and not set(d["frames"]) <= set(back.get("frames", [])):
                errors.append(f"工程 {s['id']} の後回し「{d.get('what','')}」の枠が、戻る工程 {back['id']} の枠に無い")
    for s in steps:
        if "切り上げ" in s.get("status", ""):
            errors.append(f"工程 {s['id']} が「切り上げ」")
        for dep in s.get("depends_on", []):
            if dep not in ids or ids.index(dep) >= ids.index(s["id"]):
                errors.append(f"工程 {s['id']} の前提 {dep} が前の工程に無い")
        if s.get("status") == "完了" and any(x.get("status") != "完了" for x in steps if x["id"] in s.get("depends_on", [])):
            errors.append(f"工程 {s['id']} が前提の終わる前に完了になっている")
        for d in s.get("deferred", []):
            back = d.get("returns_in")
            if back not in ids or ids.index(back) <= ids.index(s["id"]):
                errors.append(f"工程 {s['id']} の後回し「{d.get('what','')}」に、後で戻る工程が無い")
    return errors


def main():
    leaves, remaining, total, plan = state()
    errors = check(leaves, plan)
    below = sorted((v, k) for k, v in leaves.items() if v < 100)
    if "--check" in sys.argv:
        for x in errors:
            print("NG:", x)
        print("PASS" if not errors else f"FAIL {len(errors)}")
        sys.exit(1 if errors else 0)
    print("【百代鏡：100%までの約束（Scripts/plan_to_100.py が毎回出す）】")
    for r in RULES:
        print("- " + r)
    print(f"- 今の全体：{total:.2f}%（estimate.json）。100%未満の枠 {len(below)}/60。低い順：" + "、".join(f"{k} {v}%" for v, k in below[:10]))
    if plan is None:
        print("- 工程表（Docs/Development/PLAN_TO_100.json）はまだ無い。次にすることは、60枠すべての残りを入れた工程表を作ってユーザーに見せること。")
    else:
        nxt = next((s for s in plan["steps"] if s.get("status") != "完了"), None)
        done = sum(1 for s in plan["steps"] if s.get("status") == "完了")
        print(f"- 工程表：{done}/{len(plan['steps'])} 工程完了。次の工程：" + (f"{nxt['id']} {nxt.get('title','')}（枠 {'・'.join(nxt.get('frames', []))}）" if nxt else "なし"))
        if nxt and nxt.get("user_decision"):
            print("- 次の工程を始める前にユーザーの判断が要る：" + nxt["user_decision"])
    if errors:
        print("- 工程表の決まりの違反：" + "／".join(errors[:5]))


if __name__ == "__main__":
    main()
