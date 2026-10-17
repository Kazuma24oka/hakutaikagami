# 百代鏡 設計資料

## 管理規則
- `Confirmed/`：ユーザーが明示的に確定した仕様の正本。
- `Proposals/`：未決定事項・設計候補。確定仕様と混同しない。
- `Decisions/`：決定日・理由・変更履歴。
- `References/`：参考史料・作品。参考情報は仕様ではない。
- `Archive/`：旧版、重複、廃止、却下された資料の保管場所。通常の設計参照から分離する。

## アーカイブ運用
1. 古い・重複・廃止候補は削除ではなく、原則 `Docs/Archive/` に移す。分類や日付が必要なら下位フォルダを使用する。
2. 移動前に確定・未確定の正本が別に存在すること、独自の情報が失われないこと、参照リンクの修正が必要かを確認する。
3. 移動時は移動元・移動先・理由を記録し、参照リンクを修正する。Git履歴も残す。
4. 永久削除は例外とし、対象と理由を提示してユーザーの明示的な承認を得る。
5. 確定時はConfirmedを更新し、Proposalsの重複部分を整理する。旧資料は必要に応じてArchiveへ移す。
6. `main` へのマージは別途ユーザー承認を要する。

## 情報・書状・通知システム
- **確定方針の正本**：[`Confirmed/INFORMATION_SYSTEM.md`](Confirmed/INFORMATION_SYSTEM.md)。
- **未決定事項の案内**：[`Proposals/INFORMATION_SYSTEM_CONSOLIDATED.md`](Proposals/INFORMATION_SYSTEM_CONSOLIDATED.md)。
- 従来の `LETTER_HANDLING.md`、`LETTER_SECURITY_AND_DELEGATION.md`、`INFORMATION_TRANSMISSION.md`、`KNOWLEDGE_MODEL.md`、`KNOWLEDGE_ACQUISITION.md`、`CONFIDENTIAL_INFORMATION.md`、`EVENT_NOTIFICATION.md` は検討履歴。内容が重複する場合は確定正本を優先する。
- 旧資料のアーカイブ移動は別途棚卸し対象。今回の正本移行に伴う永久削除は行わない。

## 武将の身分・主従・出世・俸禄・知行・独立（未確定）
- **優先参照先**：[`Proposals/CHARACTER_SOCIAL_SYSTEM_CONSOLIDATED.md`](Proposals/CHARACTER_SOCIAL_SYSTEM_CONSOLIDATED.md)。
- `Proposals/STATUS_VASSALAGE_CAREER.md` と `Proposals/CAREER_STIPEND_LAND_INDEPENDENCE.md` は検討履歴・詳細資料として保持する。重複した説明は統合案を優先参照するが、いずれも未確定。
- アーカイブ整理はユーザーの指示により保留中。旧資料は移動・削除しない。

## 武将の行動・時間進行（未確定）
- **優先参照先**：[`Proposals/ACTION_TIME_SYSTEM_CONSOLIDATED.md`](Proposals/ACTION_TIME_SYSTEM_CONSOLIDATED.md)。
- `Proposals/TIME_PROGRESSION.md` と `Proposals/CHARACTER_ACTION_TIME_SYSTEM.md` は初期比較案・選択履歴として保持する。旧記述の表現差は統合案を優先して参照するが、すべて未確定。
- アーカイブ整理は保留し、旧資料の移動・削除は行わない。

## 全国マップ・城・領地（未確定）
- **優先参照先**：[`Proposals/WORLD_MAP_CASTLE_LAND_CONSOLIDATED.md`](Proposals/WORLD_MAP_CASTLE_LAND_CONSOLIDATED.md)。
- `Proposals/WORLD_MAP_CASTLE_LAND.md` は比較案・選択履歴として保持し、重複する説明は統合案を優先参照する（いずれも未確定）。
- 寺社勢力の反発、農民一揆、朝廷・将軍家工作、官位・将軍職は `Proposals/FUTURE_POLITICAL_SOCIAL_SYSTEM_MEMO.md` に将来検討メモとして保持し、今回確定しない。
- アーカイブ整理は保留し、旧資料の移動・削除は行わない。
