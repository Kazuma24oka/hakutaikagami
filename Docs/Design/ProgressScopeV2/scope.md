# 統合案・バックログの範囲の追加（2回目の再評価）

1回目（ProgressScopeV1）で拾えていなかった決定と、採用済みのバックログの範囲を、60枠の数と重みを変えずに関係する枠へ足す（2026-10-31 ユーザー決定）。

1. Add the integration-proposal and adopted-backlog scope that the first reassessment missed to the affected existing items without changing the 60 items or weights.
2. Re-rate only the affected items downward in proportion to the added scope; record old and new percentages, the added scope and the reason for each item.
3. Record the total change as an excluded reassessment, never as earned implementation progress; no code, tests or assets change.
4. Re-freeze the next implementation stage (NPC purpose) on the second re-rated baseline before its code exists, keeping its per-item target increments.
5. Regenerate the progress view and pass the existing progress and approval-batch checks.

固定基準：approval-baseline-v47-scope.json（再評価）、approval-baseline-v48.json（次工程 NpcPurposeV1。v46・v47 は置き換え済みの履歴）。
