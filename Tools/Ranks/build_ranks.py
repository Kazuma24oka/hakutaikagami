"""百代鏡：位階・官職・幕府職の表（Docs/Proposals/RANKS_AND_OFFICES_V1.md の「使用する職の一覧」）を Core/Data/ranks-offices-v1.json に書き出す。

  python Tools/Ranks/build_ranks.py

相当位階は同じ文書の ②-1・②-2 の表による。家中職は家中の仕組み（HouseholdCourt）が別に持つので、ここには入れない。
"""
from __future__ import annotations
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RANKS = "正一位・従一位・正二位・従二位・正三位・従三位・正四位上・正四位下・従四位上・従四位下・正五位上・正五位下・従五位上・従五位下・正六位".split("・")
ORDER = {r: i + 1 for i, r in enumerate(RANKS)}

GREAT = "大和・河内・伊勢・武蔵・下総・近江・越前・播磨・肥後".split("・")
PRINCE = "上総・常陸・上野".split("・")
UPPER = "山城・摂津・尾張・三河・遠江・駿河・甲斐・相模・美濃・信濃・下野・越中・越後・丹波・但馬・因幡・伯耆・出雲・美作・備前・備中・備後・安芸・周防・紀伊・阿波・讃岐・伊予・筑前・筑後・肥前・豊前・豊後・加賀".split("・")
MIDDLE = "安房・若狭・能登・佐渡・丹後・石見・長門・土佐・日向・大隅・薩摩".split("・")
LOWER = "和泉・伊賀・志摩・伊豆・飛騨・隠岐・淡路・壱岐・対馬".split("・")
OSHU = "磐城・岩代・陸前・陸中・陸奥".split("・")
USHU = "羽前・羽後".split("・")

# 京官：名称・相当位階（なければ None）・武家に多いか
KYOKAN = [
    ("摂政", None), ("関白", None), ("太政大臣", "従一位"), ("左大臣", "従二位"), ("右大臣", "従二位"), ("内大臣", "従二位"),
    ("大納言", "正三位"), ("中納言", "従三位"), ("参議", "従四位下"),
    ("左近衛大将", "従三位"), ("右近衛大将", "従三位"), ("左近衛中将", "従四位下"), ("右近衛中将", "従四位下"),
    ("左近衛少将", "正五位下"), ("右近衛少将", "正五位下"), ("左近衛将監", "正六位"), ("右近衛将監", "正六位"),
    ("左衛門督", "従四位下"), ("右衛門督", "従四位下"), ("左衛門佐", "従五位上"), ("右衛門佐", "従五位上"), ("左衛門尉", "正六位"), ("右衛門尉", "正六位"),
    ("左兵衛督", "従四位下"), ("右兵衛督", "従四位下"), ("左兵衛佐", "従五位上"), ("右兵衛佐", "従五位上"), ("左兵衛尉", "正六位"), ("右兵衛尉", "正六位"),
    ("弾正尹", "従三位"), ("弾正大弼", "従四位下"), ("弾正少弼", "正五位下"), ("弾正忠", "正六位"),
    ("中務卿", "正四位上"), *[(f"{x}卿", "正四位下") for x in "式部・治部・民部・兵部・刑部・大蔵・宮内".split("・")],
    ("中務大輔", "正五位上"), *[(f"{x}大輔", "正五位下") for x in "式部・治部・民部・兵部・刑部・大蔵・宮内".split("・")],
    ("中務少輔", "従五位上"), *[(f"{x}少輔", "従五位下") for x in "式部・治部・民部・兵部・刑部・大蔵・宮内".split("・")],
    ("修理大夫", "従四位下"), ("左京大夫", "従四位下"), ("右京大夫", "従四位下"), ("大膳大夫", "従四位下"),
    ("修理亮", "従五位下"), ("左京亮", "従五位下"), ("右京亮", "従五位下"), ("大膳亮", "従五位下"),
    ("左馬頭", "従五位上"), ("右馬頭", "従五位上"), *[(f"{x}頭", "従五位上") for x in "内蔵・主計・主税・図書・木工・雅楽・内匠・縫殿".split("・")],
    *[(f"{x}頭", "従五位下") for x in "大炊・主殿・掃部・典薬・玄蕃・陰陽".split("・")],
    ("主水正", "正六位"), ("内膳正", "正六位"), ("隼人正", "正六位"),
    ("侍従", "従五位下"), ("蔵人頭", None), ("蔵人", None), ("勘解由長官", "正五位下"), ("勘解由次官", "従五位下"),
    ("大宰大弐", "正五位上"), ("大宰少弐", "従五位下"), ("鎮守府将軍", "従五位上"),
]
WARRIOR = set("修理大夫・左京大夫・大膳大夫・弾正少弼・兵部大輔・刑部少輔・治部少輔・民部少輔・左馬頭".split("・"))
HYAKKAN = ("左衛門・右衛門・左衛門尉・右衛門尉・兵衛・左兵衛・右兵衛・兵衛尉・左近・右近・将監・中務・式部・治部・民部・兵部・刑部・大蔵・宮内・"
           "修理・修理亮・左京・左京亮・右京・右京亮・大膳・大膳亮・弾正・内蔵助・主計・主税・主水・主殿・主膳・大炊助・掃部・図書・縫殿・采女・隼人・玄蕃・木工・雅楽・"
           "勘解由・監物・外記・内記・帯刀・典膳・舎人").split("・")
BAKUFU = [("征夷大将軍", "家柄"), ("管領", "家柄"), ("管領代", "家柄"), ("侍所所司", "家柄"), ("政所執事", "家柄"), ("鎌倉公方", "家柄"), ("関東管領", "家柄"),
          ("奥州探題", "家柄"), ("羽州探題", "家柄"), ("九州探題", "家柄"), ("守護代", "地域"), ("分郡守護", "地域"),
          ("御相伴衆", "家格"), ("国持衆", "家格"), ("准国主", "家格"), ("外様衆", "家格"), ("御供衆", "家格"), ("御部屋衆", "家格"), ("申次衆", "家格"),
          ("偏諱", "待遇"), ("塗輿の免許", "待遇"), ("白傘袋の免許", "待遇"), ("毛氈鞍覆の免許", "待遇"), ("奉公衆", "幕臣"), ("幕府奉行人", "幕臣")]


def title(id_, name, kind, order, rank=None, grantor="court", warrior=False, certainty="確", province=None):
    return {"Id": id_, "Name": name, "Kind": kind, "Order": order, "Rank": rank, "Grantor": grantor, "Warrior": warrior, "Certainty": certainty, "Province": province}


def main() -> None:
    rows = [title(f"rank:{i+1}", r, "rank", i + 1, certainty="ゲーム" if r == "正六位" else "確") for i, r in enumerate(RANKS)]
    n = 0
    def kokushi(p, suffix, rank, certainty="確"):
        nonlocal n
        n += 1
        rows.append(title(f"kokushi:{n}", p + suffix, "kokushi", ORDER[rank], rank, certainty=certainty, province=p))
    for p in GREAT:
        kokushi(p, "守", "従五位上"); kokushi(p, "介", "正六位")
    for p in PRINCE:
        kokushi(p, "介", "正六位")
    for p in UPPER:
        kokushi(p, "守", "従五位下"); kokushi(p, "介", "正六位")
    for p in MIDDLE + LOWER:
        kokushi(p, "守", "正六位")
    for p in OSHU:
        kokushi(p, "守", "従五位上", "ゲーム")
    for p in USHU:
        kokushi(p, "守", "従五位下", "ゲーム")
    rows.append(title("kokushi:oshu-mutsu", "陸奥守（奥州）", "kokushi", ORDER["従五位上"], "従五位上", province="奥州"))
    rows.append(title("kokushi:ushu-dewa", "出羽守（羽州）", "kokushi", ORDER["従五位下"], "従五位下", province="羽州"))
    for i, (name, rank) in enumerate(KYOKAN):
        rows.append(title(f"kyokan:{i+1}", name, "kyokan", ORDER[rank] if rank else 0, rank, warrior=name in WARRIOR))
    for i, name in enumerate(HYAKKAN):
        rows.append(title(f"hyakkan:{i+1}", name, "hyakkan", 99, None, grantor="lord"))
    for i, (name, group) in enumerate(BAKUFU):
        rows.append(title(f"bakufu:{i+1}", name, "bakufu:" + group, 50, None, grantor="shogun", certainty="確" if group in ("家柄", "地域", "待遇") else "要確認"))
    provinces = GREAT + PRINCE + UPPER + MIDDLE + LOWER + OSHU + USHU
    for i, p in enumerate(provinces):
        rows.append(title(f"shugo:{i+1}", p + "守護", "bakufu:守護", 50, None, grantor="shogun", certainty="ゲーム" if p in OSHU + USHU else "確", province=p))
    counts = {k: sum(1 for r in rows if r["Kind"] == k or r["Kind"].startswith(k + ":")) for k in ("rank", "kokushi", "kyokan", "hyakkan", "bakufu")}
    assert counts == {"rank": 15, "kokushi": 118, "kyokan": 92, "hyakkan": 50, "bakufu": 25 + 73}, counts
    out = {"Schema": 1, "Id": "ranks-offices-v1", "Source": "Docs/Proposals/RANKS_AND_OFFICES_V1.md", "Titles": rows}
    (ROOT / "Core" / "Data" / "ranks-offices-v1.json").write_text(json.dumps(out, ensure_ascii=False, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(counts)


if __name__ == "__main__":
    main()
