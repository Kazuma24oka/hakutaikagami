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

## 在地社会・国人衆・寺社・一揆（未確定）
- **優先参照先**：[`Proposals/LOCAL_SOCIAL_SYSTEM_CONSOLIDATED.md`](Proposals/LOCAL_SOCIAL_SYSTEM_CONSOLIDATED.md)。
- `Proposals/LOCAL_SOCIAL_FORCES.md` は比較案・ユーザー選択の検討履歴として保持する。重複した説明は統合案を優先参照するが、どちらも未確定。
- 寺社反発・一揆は未確定設計として検討開始。朝廷・将軍家工作、官位・官職、将軍職は `Proposals/FUTURE_POLITICAL_SOCIAL_SYSTEM_MEMO.md` に将来検討として残す。
- アーカイブ整理は保留。旧資料の移動・削除を行わない。

## 朝廷・将軍家・官位・将軍職（未確定）
- **優先参照先**：[`Proposals/COURT_SHOGUNATE_SYSTEM_CONSOLIDATED.md`](Proposals/COURT_SHOGUNATE_SYSTEM_CONSOLIDATED.md)。
- `Proposals/COURT_SHOGUNATE_RANKS.md` は比較案・ユーザー選択履歴として保持。重複する説明は統合案を優先参照するが、どちらも未確定。
- 出家・隠居・還俗は `Proposals/FUTURE_POLITICAL_SOCIAL_SYSTEM_MEMO.md` の将来検討事項として残す。
- アーカイブ整理は保留し、旧資料の移動・削除は行わない。

## 全設計の横断レビュー（未確定）
- [確定候補・未決定事項の重要度別レビュー（2026-10-17）](Proposals/CROSS_SYSTEM_REVIEW_2026-10-17.md)。
- P0／P1／P2で未決定事項を分類。確定候補は承認前の提案であり、Confirmedに昇格しない。
- 旧資料の「将来検討」表記の一部は古いが、今回のレビューで元資料の移動・削除・改訂は行わない。アーカイブ整理は保留。

## P0共通基盤5項目（未確定・比較案）
- [主体・権限／世界時刻／領地・会計／状態遷移／情報認識のABC比較](Proposals/P0_FOUNDATION_OPTIONS.md)。
- 推奨B案はユーザー選択前の候補であり、確定仕様への昇格ではない。
- アーカイブ整理は保留。Confirmed・mainは変更しない。

## P0共通基盤の統合設計（未確定）
- **優先参照先**：[`Proposals/P0_FOUNDATION_CONSOLIDATED.md`](Proposals/P0_FOUNDATION_CONSOLIDATED.md)。
- `Proposals/P0_FOUNDATION_OPTIONS.md` はABC比較案・B案選択・例外処理の検討履歴として保持。統合案を優先参照するが、どちらも未確定。
- 未解決の同時刻処理・権限・会計・帰属・責任は統合案に記載。Confirmed・mainは変更せず、アーカイブ整理は保留。

## F01〜F08の確定候補範囲（未確定）
- [基本方針と保留詳細のABC比較](Proposals/F01_F08_CONFIRMATION_SCOPE_OPTIONS.md)。
- A/B/Cは確定候補の範囲を検討する選択肢。ユーザーの明示的な確定承認まではConfirmedに移さない。
- Confirmed・mainは変更せず、アーカイブ整理は保留。

## F01〜F08共通基本方針（確定）
- **確定正本**：[`Confirmed/FOUNDATION_PRINCIPLES_F01_F08.md`](Confirmed/FOUNDATION_PRINCIPLES_F01_F08.md)。2026-10-17にB案の基本原則のみ確定。
- **詳細・例外は未確定**：[`Proposals/F01_F08_CONFIRMATION_SCOPE_OPTIONS.md`](Proposals/F01_F08_CONFIRMATION_SCOPE_OPTIONS.md) と [`Proposals/P0_FOUNDATION_CONSOLIDATED.md`](Proposals/P0_FOUNDATION_CONSOLIDATED.md) を参照。
- 旧比較案は選択履歴として保持。アーカイブ整理は保留し、mainへのマージは別途承認を要する。

## 外交・同盟・婚姻・人質（未確定）
- [外交・同盟・婚姻・人質の8項目ABC比較案](Proposals/DIPLOMACY_ALLIANCE_MARRIAGE_HOSTAGES.md)。
- すべて選択前の比較案。既存のF01〜F08確定基本方針と情報・書状の確定仕様を維持する。
- Confirmed・mainは変更せず、アーカイブ整理は保留。

## 外交・同盟・婚姻・人質：統合設計（未確定）
- **優先参照先**：[`Proposals/DIPLOMACY_SYSTEM_CONSOLIDATED.md`](Proposals/DIPLOMACY_SYSTEM_CONSOLIDATED.md)。
- `Proposals/DIPLOMACY_ALLIANCE_MARRIAGE_HOSTAGES.md` は基本8項目・詳細5項目・例外6項目の比較案と選択履歴として保持。重複記述は統合案を優先参照するが、どちらも未確定。
- 旧版・重複資料は正本・固有情報・リンクを確認してDocs/Archiveへ移動可能。永久削除には事前承認が必要。Confirmed・mainは変更しない。

## 武将の能力・性格・人間関係（未確定）
- [9項目のABC比較設計](Archive/CHARACTER_ABILITIES_PERSONALITY_RELATIONSHIPS.md)。
- 推奨B案は未選択の比較案。能力・性格・関係・評判と本人の認識を区別し、F01〜F08の確定基本方針を維持。
- Archiveへの移動・整理は正本と参照を確認したうえで可能。永久削除には事前承認が必要。Confirmed・mainは変更しない。

## 武将の能力・性格・人間関係：統合設計（未確定）
- **優先参照先**：[`Proposals/CHARACTER_PERSONALITY_RELATIONSHIPS_CONSOLIDATED.md`](Proposals/CHARACTER_PERSONALITY_RELATIONSHIPS_CONSOLIDATED.md)。
- `Archive/CHARACTER_ABILITIES_PERSONALITY_RELATIONSHIPS.md` は基本9項目・詳細5項目の比較案とユーザー選択履歴として保持。重複説明は統合案を優先参照するが、どちらも未確定。
- 古い・重複資料のArchive移動は正本・固有情報・参照を確認して実施可能。永久削除には事前承認が必要。Confirmed・mainは変更しない。

## 内政・経済・兵站（未確定）
- [12項目のABC比較設計](Archive/DOMESTIC_ECONOMY_LOGISTICS.md)。
- B案は未選択の推奨候補。生産・徴税・会計・輸送・兵站とF01〜F08の確定基本方針を接続する。
- Archive移動は固有情報・参照リンク確認後に実施可能。永久削除には事前承認が必要。Confirmed・mainは変更しない。

## 内政・経済・兵站：統合設計（未確定）
- **優先参照先**：[`Proposals/DOMESTIC_ECONOMY_LOGISTICS_CONSOLIDATED.md`](Proposals/DOMESTIC_ECONOMY_LOGISTICS_CONSOLIDATED.md)。
- 基本12項目・詳細5項目の選択履歴は [`Archive/DOMESTIC_ECONOMY_LOGISTICS.md`](Archive/DOMESTIC_ECONOMY_LOGISTICS.md) に移動して保持。比較案の初期記述は履歴であり、ユーザー選択済みB案を優先する。
- 永久削除なし。Confirmed・mainは変更しない。

## 合戦・軍制・戦術（未確定）
- [12項目のABC比較設計](Proposals/BATTLE_MILITARY_TACTICS.md)。
- B案は未選択の推奨候補。軍勢・指揮権・野戦／攻城／籠城・士気・兵站・世界時刻・情報認識を既存設計に接続。
- 旧版・重複資料は固有情報・参照を確認してArchiveへ移動可能。永久削除には事前承認が必要。Confirmed・mainは変更しない。
