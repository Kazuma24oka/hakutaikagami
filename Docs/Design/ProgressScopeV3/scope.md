# データの側の範囲の追加（3回目の再評価）

採用した機能のデータの側（史実人物の値の採録・拠点の新しい値・作成武将での設定）を、60枠の数と重みを変えずに関係する枠へ足す（2026-10-31 ユーザー決定）。項目の一覧は Docs/Proposals/CHARACTER_DATA_FIELDS_V1.md。

1. Add the data side of the adopted features (field list Docs/Proposals/CHARACTER_DATA_FIELDS_V1.md: historical value collection, site values, character-creation settings) to the affected existing items without changing the 60 items or weights.
2. Re-rate only the affected items downward in proportion to the added scope; record old and new percentages, the added scope and the reason for each item.
3. Record the total change as an excluded reassessment, never as earned implementation progress; no code, tests or assets change.
4. Re-freeze the next implementation stage (NPC purpose) on the third re-rated baseline before its code exists, keeping its per-item target increments.
5. Regenerate the progress view and pass the existing progress and approval-batch checks.

固定基準：approval-baseline-v48-scope.json（再評価）、approval-baseline-v49.json（次工程 NpcPurposeV1。v46〜v48 は置き換え済みの履歴）。
