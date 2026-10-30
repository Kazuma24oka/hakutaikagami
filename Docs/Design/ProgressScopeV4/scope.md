# 横断的な範囲の追加（4回目の再評価）

決定ごとの縦の照合では抜けていた、どの機能にもまたがる作業（物資・NPCが自分から使う判断・素材と文面・情報の伝わり方・歴史モードの差・性能）を、60枠の数と重みを変えずに関係する枠へ足す（2026-10-31 ユーザー決定）。

1. Add the cross-cutting scope of the adopted features (goods, NPC initiative, art assets and dialogue text, information spread, history-mode differences, load) to the affected existing items without changing the 60 items or weights; extend the field list with goods, organizations and definition tables.
2. Re-rate only the affected items downward in proportion to the added scope; record old and new percentages, the added scope and the reason for each item.
3. Record the total change as an excluded reassessment, never as earned implementation progress; no code, tests or assets change.
4. Re-freeze the next implementation stage (NPC purpose) on the fourth re-rated baseline before its code exists, keeping its per-item target increments.
5. Regenerate the progress view and pass the existing progress and approval-batch checks.

固定基準：approval-baseline-v49-scope.json（再評価）、approval-baseline-v50.json（次工程 NpcPurposeV1。v46〜v49 は置き換え済みの履歴）。
