"""Build the versioned, evidence-layered 1560 national person estimate table."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DB_PATH = ROOT / "Core/Data/hakutai-db-1560-v2.json"
OUT_PATH = ROOT / "Core/Data/national-person-estimates-v1.json"

LEADERSHIP = ("当主", "指導者", "城主", "領主", "重臣")
VALOR = ("軍事", "軍役", "合戦", "武将", "武士", "在城", "守備", "軍勢")
INTELLIGENCE = ("軍師", "軍略", "調略", "謀", "兵法", "忍")
POLITICS = ("政治", "行政", "奉行", "官僚", "取次", "守護", "所領役帳")


def assessment(person: dict, people: dict[str, dict]) -> dict:
    birth = person["Birth"]
    if birth is not None:
        kind, basis, confidence = "explicit", "百代鏡DBの生年をそのまま採用", "source-explicit"
    else:
        father = people.get(person["Father"])
        if father and father["Birth"] is not None:
            birth = min(1545, father["Birth"] + 25)
            kind, basis, confidence = "estimated-father-gap", "DBに本人の生年なし。実父の明記生年＋25年を世代間隔の試作推定に使用", "estimate-low"
        else:
            birth = 1530
            kind, basis, confidence = "estimated-default-age", "DBに本人と実父の生年なし。1560年開始時30歳を中立的な試作値として使用", "estimate-very-low"
        if person["Death"] is not None and birth > person["Death"] - 16:
            birth = person["Death"] - 16
            kind += "+death-bound"
            basis += "。記録された没年より16年以上前へ調整"
    role = person.get("Role") or ""

    def value(terms: tuple[str, ...]) -> tuple[int, str]:
        matched = [term for term in terms if term in role]
        if not matched:
            return 50, "DBの役割語に対応する記録なし。試作基準値50"
        return min(80, 50 + 15 * len(matched)), "DB役割語からの弱い推定：" + "・".join(matched)

    leadership, leadership_basis = value(LEADERSHIP)
    valor, valor_basis = value(VALOR)
    intelligence, intelligence_basis = value(INTELLIGENCE)
    politics, politics_basis = value(POLITICS)
    return {
        "Id": person["Id"],
        "BirthYear": birth,
        "BirthKind": kind,
        "BirthBasis": basis,
        "BirthConfidence": confidence,
        "Abilities": {
            "Leadership": leadership,
            "Valor": valor,
            "Intelligence": intelligence,
            "Politics": politics,
            "Charm": 50,
        },
        "AbilityBasis": {
            "Leadership": leadership_basis,
            "Valor": valor_basis,
            "Intelligence": intelligence_basis,
            "Politics": politics_basis,
            "Charm": "能力根拠をDBで確認できないため基準値50。旧交渉値は転用しない",
        },
        "AbilityConfidence": "estimate-very-low",
        "BirthParentAccepted": person.get("Father") in people,
        "BirthParentBasis": "DBの父IDを確認" if person.get("Father") in people else "DBに開始対象の父IDなし",
    }


def build() -> dict:
    db = json.loads(DB_PATH.read_bytes())
    people = {person["Id"]: person for person in db["People"]}
    rows = [assessment(person, people) for person in sorted(db["People"], key=lambda row: row["Id"])]
    by_id = {row["Id"]: row for row in rows}

    # 父本人の生年も不明で、明記生年の子がいる場合は、開始年-30の一律値より子の記録を優先して親の世代を推定。
    explicit_children: dict[str, list[int]] = {}
    for child in people.values():
        father_id = child.get("Father")
        father = people.get(father_id)
        if father_id and father and child["Birth"] is not None and father["Birth"] is None:
            father_father = people.get(father.get("Father"))
            if father_father is None or father_father["Birth"] is None:
                explicit_children.setdefault(father_id, []).append(child["Birth"])
    for father_id, births in explicit_children.items():
        person, row = people[father_id], by_id[father_id]
        candidate = min(births) - 25
        upper = min(1560, person["Death"] - 16 if person["Death"] is not None else 1560)
        if row["BirthKind"].startswith("estimated-default-age") and 1400 <= candidate <= upper:
            row["BirthYear"] = candidate
            row["BirthKind"] += "+child-generation"
            row["BirthBasis"] += "。父自身と父の父の生年不明、明記生年の最年長の子より25年前を世代推定"

    # 親から子へ一度だけ順に処理し、親子双方の推定値を反復変更しない。
    children: dict[str, list[str]] = {person_id: [] for person_id in people}
    indegree = {person_id: 0 for person_id in people}
    for person in people.values():
        father_id = person.get("Father")
        if father_id in people:
            children[father_id].append(person["Id"])
            indegree[person["Id"]] += 1
    ready = [person_id for person_id, degree in indegree.items() if degree == 0]
    import heapq
    heapq.heapify(ready)
    ordered: list[str] = []
    while ready:
        person_id = heapq.heappop(ready)
        ordered.append(person_id)
        for child_id in sorted(children[person_id]):
            indegree[child_id] -= 1
            if indegree[child_id] == 0:
                heapq.heappush(ready, child_id)
    if len(ordered) != len(people):
        raise ValueError("百代鏡DBの父子記録に循環があります")

    for person_id in ordered:
        person, row = people[person_id], by_id[person_id]
        father_id = person.get("Father")
        if father_id not in people:
            row["BirthParentAccepted"] = False
            row["BirthParentBasis"] = "DBに開始対象の父IDなし"
            continue
        father_row = by_id[father_id]
        if father_row["BirthYear"] >= row["BirthYear"] and person["Birth"] is None:
            candidate = father_row["BirthYear"] + 20
            upper = min(1560, person["Death"] - 16 if person["Death"] is not None else 1560)
            if candidate <= upper:
                row["BirthYear"] = candidate
                if "+parent-generation" not in row["BirthKind"]:
                    row["BirthKind"] += "+parent-generation"
                row["BirthBasis"] += "。親の推定生年より20年後となるよう親から子へ調整"
        row["BirthParentAccepted"] = father_row["BirthYear"] < row["BirthYear"]
        row["BirthParentBasis"] = (
            "DB父子記録を生年順序と照合して採用"
            if row["BirthParentAccepted"]
            else "DB父子記録と明記値または没年制約が両立せず、開始世界ではリンクを採用しない"
        )

    return {
        "Schema": 1,
        "Policy": "national-person-data-v1",
        "SourceEdition": db["Id"],
        "SourceCommit": db["Source"]["Commit"],
        "SourceSha256": hashlib.sha256(DB_PATH.read_bytes()).hexdigest(),
        "Rules": {
            "Birth": "明記値は原文通り。不明は実父の明記生年+25年、なければ開始年-30年。父自身・父の父が不明で明記生年の子がいる場合は最年長の子-25年。没年の16年前より後なら補正。親から子へ一度だけ年代順に照合し、推定値のみ補正。明記値同士・没年制約が両立しない父子リンクは新規全国世界に採用しない。全て推定層を明示。",
            "Abilities": "各基礎値は50を起点にDBの役割語1語ごと15加算、最大80。役割語は能力の史実証明ではなく試作推定。能力根拠のない魅力は50。記録件数・実在確度・交渉値は能力へ換算しない。",
        },
        "People": rows,
    }


if __name__ == "__main__":
    data = build()
    OUT_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"people={len(data['People'])} sha256={hashlib.sha256(OUT_PATH.read_bytes()).hexdigest()}")
