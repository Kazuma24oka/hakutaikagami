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
- `Archive/STATUS_VASSALAGE_CAREER.md` と `Archive/CAREER_STIPEND_LAND_INDEPENDENCE.md` は検討履歴・詳細資料として保持する。重複した説明は統合案を優先参照するが、いずれも未確定。
- アーカイブ整理はユーザーの指示により保留中。旧資料は移動・削除しない。

## 武将の行動・時間進行（未確定）
- **優先参照先**：[`Proposals/ACTION_TIME_SYSTEM_CONSOLIDATED.md`](Proposals/ACTION_TIME_SYSTEM_CONSOLIDATED.md)。
- `Archive/TIME_PROGRESSION.md` と `Archive/CHARACTER_ACTION_TIME_SYSTEM.md` は初期比較案・選択履歴として保持する。旧記述の表現差は統合案を優先して参照するが、すべて未確定。
- アーカイブ整理は許可し、旧資料はArchiveに保管し、永久削除は事前承認が必要。

## 全国マップ・城・領地（未確定）
- **優先参照先**：[`Proposals/WORLD_MAP_CASTLE_LAND_CONSOLIDATED.md`](Proposals/WORLD_MAP_CASTLE_LAND_CONSOLIDATED.md)。
- `Archive/WORLD_MAP_CASTLE_LAND.md` は比較案・選択履歴として保持し、重複する説明は統合案を優先参照する（いずれも未確定）。
- 寺社勢力の反発、農民一揆、朝廷・将軍家工作、官位・将軍職は `Proposals/FUTURE_POLITICAL_SOCIAL_SYSTEM_MEMO.md` に将来検討メモとして保持し、今回確定しない。
- アーカイブ整理は許可し、旧資料はArchiveに保管し、永久削除は事前承認が必要。

## 在地社会・国人衆・寺社・一揆（未確定）
- **優先参照先**：[`Proposals/LOCAL_SOCIAL_SYSTEM_CONSOLIDATED.md`](Proposals/LOCAL_SOCIAL_SYSTEM_CONSOLIDATED.md)。
- `Archive/LOCAL_SOCIAL_FORCES.md` は比較案・ユーザー選択の検討履歴として保持する。重複した説明は統合案を優先参照するが、どちらも未確定。
- 寺社反発・一揆は未確定設計として検討開始。朝廷・将軍家工作、官位・官職、将軍職は `Proposals/FUTURE_POLITICAL_SOCIAL_SYSTEM_MEMO.md` に将来検討として残す。
- アーカイブ整理は許可。旧資料の移動・削除を行わない。

## 朝廷・将軍家・官位・将軍職（未確定）
- **優先参照先**：[`Proposals/COURT_SHOGUNATE_SYSTEM_CONSOLIDATED.md`](Proposals/COURT_SHOGUNATE_SYSTEM_CONSOLIDATED.md)。
- `Proposals/COURT_SHOGUNATE_RANKS.md` は比較案・ユーザー選択履歴として保持。重複する説明は統合案を優先参照するが、どちらも未確定。
- 出家・隠居・還俗は `Proposals/FUTURE_POLITICAL_SOCIAL_SYSTEM_MEMO.md` の将来検討事項として残す。
- アーカイブ整理は許可し、旧資料はArchiveに保管し、永久削除は事前承認が必要。

## 全設計の横断レビュー（未確定）
- [確定候補・未決定事項の重要度別レビュー（2026-10-17）](Proposals/CROSS_SYSTEM_REVIEW_2026-10-17.md)。
- P0／P1／P2で未決定事項を分類。確定候補は承認前の提案であり、Confirmedに昇格しない。
- 旧資料の「将来検討」表記の一部は古いが、今回のレビューで元資料の移動・削除・改訂は行わない。アーカイブ整理は許可。

## P0共通基盤5項目（未確定・比較案）
- [主体・権限／世界時刻／領地・会計／状態遷移／情報認識のABC比較](Proposals/P0_FOUNDATION_OPTIONS.md)。
- 推奨B案はユーザー選択前の候補であり、確定仕様への昇格ではない。
- アーカイブ整理は許可。Confirmed・mainは変更しない。

## P0共通基盤の統合設計（未確定）
- **優先参照先**：[`Proposals/P0_FOUNDATION_NINE_ITEMS_CONSOLIDATED.md`](Proposals/P0_FOUNDATION_NINE_ITEMS_CONSOLIDATED.md)。
- `Proposals/P0_FOUNDATION_OPTIONS.md` はABC比較案・B案選択・例外処理の検討履歴として保持。統合案を優先参照するが、どちらも未確定。
- 未解決の同時刻処理・権限・会計・帰属・責任は統合案に記載。Confirmed・mainは変更せず、アーカイブ整理は許可。

## F01〜F08の確定候補範囲（未確定）
- [基本方針と保留詳細のABC比較](Proposals/F01_F08_CONFIRMATION_SCOPE_OPTIONS.md)。
- A/B/Cは確定候補の範囲を検討する選択肢。ユーザーの明示的な確定承認まではConfirmedに移さない。
- Confirmed・mainは変更せず、アーカイブ整理は許可。

## F01〜F08共通基本方針（確定）
- **確定正本**：[`Confirmed/FOUNDATION_PRINCIPLES_F01_F08.md`](Confirmed/FOUNDATION_PRINCIPLES_F01_F08.md)。2026-10-17にB案の基本原則のみ確定。
- **詳細・例外は未確定**：[`Proposals/F01_F08_CONFIRMATION_SCOPE_OPTIONS.md`](Proposals/F01_F08_CONFIRMATION_SCOPE_OPTIONS.md) と [`Proposals/P0_FOUNDATION_NINE_ITEMS_CONSOLIDATED.md`](Proposals/P0_FOUNDATION_NINE_ITEMS_CONSOLIDATED.md) を参照。
- 旧比較案は選択履歴として保持。アーカイブ整理は許可し、mainへのマージは別途承認を要する。

## 外交・同盟・婚姻・人質（未確定）
- [外交・同盟・婚姻・人質の8項目ABC比較案](Archive/DIPLOMACY_ALLIANCE_MARRIAGE_HOSTAGES.md)。
- すべて選択前の比較案。既存のF01〜F08確定基本方針と情報・書状の確定仕様を維持する。
- Confirmed・mainは変更せず、アーカイブ整理は許可。

## 外交・同盟・婚姻・人質：統合設計（未確定）
- **優先参照先**：[`Proposals/DIPLOMACY_SYSTEM_CONSOLIDATED.md`](Proposals/DIPLOMACY_SYSTEM_CONSOLIDATED.md)。
- `Archive/DIPLOMACY_ALLIANCE_MARRIAGE_HOSTAGES.md` は基本8項目・詳細5項目・例外6項目の比較案と選択履歴として保持。重複記述は統合案を優先参照するが、どちらも未確定。
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
- [12項目のABC比較設計](Archive/BATTLE_MILITARY_TACTICS.md)。
- B案は未選択の推奨候補。軍勢・指揮権・野戦／攻城／籠城・士気・兵站・世界時刻・情報認識を既存設計に接続。
- 旧版・重複資料は固有情報・参照を確認してArchiveへ移動可能。永久削除には事前承認が必要。Confirmed・mainは変更しない。

## 合戦・軍制・戦術：統合設計（未確定）
- **優先参照先**：[`Proposals/BATTLE_MILITARY_TACTICS_CONSOLIDATED.md`](Proposals/BATTLE_MILITARY_TACTICS_CONSOLIDATED.md)。
- 基本12項目・詳細5項目のABC比較とB案選択履歴は [`Archive/BATTLE_MILITARY_TACTICS.md`](Archive/BATTLE_MILITARY_TACTICS.md) に移動。現在の未確定設計は統合案を優先参照。
- 移動記録：2026-10-17、`Proposals/BATTLE_MILITARY_TACTICS.md` → `Archive/BATTLE_MILITARY_TACTICS.md`。内容を保持し、元配置を除去。永久削除なし。Confirmed・mainは変更しない。

## 全設計横断レビュー（最新・未確定）
- **最新の優先参照先**：[`Proposals/CROSS_SYSTEM_REVIEW_2026-10-17_V2.md`](Proposals/CROSS_SYSTEM_REVIEW_2026-10-17_V2.md)。確定仕様3文書と現行の各領域統合案を横断照合し、依存関係・重複・表現差とP0（9項目）／P1（10領域）／P2（5領域）を整理。
- 旧横断レビューは前段階の検討履歴。移動時は固有情報・参照を照合してArchiveへ保管し、元配置を除去する。
- 確定仕様の変更なし。mainへのマージなし。資料内容の永久削除は事前承認が必要。

## P0共通基盤9項目：依存順の検討（未確定）
- **今回の比較案**：[`Proposals/P0_FOUNDATION_PHASE1_OPTIONS.md`](Archive/P0_FOUNDATION_PHASE1_OPTIONS.md)。P0-01〜09を3群に分割し、第1群（権限・世界時刻・情報認識）3項目をABC比較。未選択・未確定。
- 既存の確定情報システムを再選択・変更しない。第2群は帰属・会計・組織決議、第3群は条約・軍事指揮・NPCデータ。
- Confirmed・mainは変更しない。Archiveへの移動は比較履歴の内容・参照を照合してから実施し、永久削除は事前承認を要する。

## P0共通基盤・第2群（未確定）
- [領地帰属・資源会計・組織決議のABC比較](Archive/P0_FOUNDATION_PHASE2_OPTIONS.md)。P0-04〜06の3項目を既存確定原則・第1群B案と照合した未選択の比較案。
- Bは推奨であり確定ではない。既存Confirmed・mainは変更しない。
- 旧資料のArchive移動は固有情報・参照リンク照合後に行い、元配置を除去する。資料そのものの永久削除には事前承認を要する。

## P0共通基盤・第3群（未確定）
- [外交条約・軍事指揮・NPCデータのABC比較と短い具体例](Archive/P0_FOUNDATION_PHASE3_OPTIONS.md)。P0-07〜09の3項目。B案はユーザー選択済み・未確定。
- 第1・第2群のB優先案、既存ConfirmedのF01〜F08・情報システムとの依存と整合性を記録。Confirmed・mainは変更しない。
- 現行の検討資料はProposalsに保持。旧資料は内容・参照を照合した場合のみArchiveへ移動し、元配置を除去する。資料内容の永久削除には事前承認が必要。

## P0共通基盤9項目：統合設計（最新・未確定）
- **優先参照先**：[`Proposals/P0_FOUNDATION_NINE_ITEMS_CONSOLIDATED.md`](Proposals/P0_FOUNDATION_NINE_ITEMS_CONSOLIDATED.md)。第1〜第3群の9項目はすべてB優先・未確定。
- 比較・選択履歴：[`Archive/P0_FOUNDATION_PHASE1_OPTIONS.md`](Archive/P0_FOUNDATION_PHASE1_OPTIONS.md)、[`Archive/P0_FOUNDATION_PHASE2_OPTIONS.md`](Archive/P0_FOUNDATION_PHASE2_OPTIONS.md)、[`Archive/P0_FOUNDATION_PHASE3_OPTIONS.md`](Archive/P0_FOUNDATION_PHASE3_OPTIONS.md)。旧統合案：[`Archive/P0_FOUNDATION_CONSOLIDATED.md`](Archive/P0_FOUNDATION_CONSOLIDATED.md)。
- 移動記録：2026-10-17、上記4資料をProposalsからArchiveへ移動。元配置は内容照合後に削除。資料内容の永久削除なし。Confirmed・mainは変更しない。

## P0複合例外処理（未確定）
- [家督・独立・落城・徴税・条約・軍令・偽報の6項目ABC比較（短い具体例付き）](Proposals/P0_COMPOUND_EXCEPTIONS_OPTIONS.md)。Bは推奨候補、6項目はユーザー未選択。
- [P0共通基盤9項目の現行未確定正本](Proposals/P0_FOUNDATION_NINE_ITEMS_CONSOLIDATED.md)とConfirmedの基本原則を優先する。今回の例外比較はこれらを変更しない。
- 新規比較案のため旧資料のArchive移動なし。永久削除なし。Confirmed・mainは変更しない。

## P0追加確定候補のABC比較（未確定）
- [P0追加確定候補6項目：確定済み原則との差分、ABC比較、短い具体例、保留詳細](Proposals/P0_ADDITIONAL_CONFIRMATION_SCOPE_OPTIONS.md)。6項目の追加基本原則は正式確定済み。比較・選択履歴および詳細・例外は未確定として保持する。
- 現行のP0統合9項目と複合例外6項目は引き続きProposalsの参照資料として保持する。今回の旧資料移動なし。Confirmed・main変更なし、永久削除なし。

## P0追加基本原則6項目（確定）
- **確定正本**：[`Confirmed/P0_ADDITIONAL_PRINCIPLES.md`](Confirmed/P0_ADDITIONAL_PRINCIPLES.md)。既存F01〜F08・情報確定仕様と重複する部分を除いた追加原則のみ。
- **未確定詳細・例外と選択履歴**：[`Proposals/P0_ADDITIONAL_CONFIRMATION_SCOPE_OPTIONS.md`](Proposals/P0_ADDITIONAL_CONFIRMATION_SCOPE_OPTIONS.md)、[`Proposals/P0_COMPOUND_EXCEPTIONS_OPTIONS.md`](Proposals/P0_COMPOUND_EXCEPTIONS_OPTIONS.md)。
- 2026-10-17：design/foundationのみ更新。mainは変更せず、Archiveへの移動・永久削除は今回なし。

## P0同時刻イベント処理のABC比較（未確定）
- [同時刻イベント6項目の比較設計・具体例](Archive/P0_SIMULTANEOUS_EVENT_OPTIONS.md)。イベント依存、権限取消／代理受領、返答期限、落城／援軍、二重徴税、軍令／組織決議を検討。
- 6項目はB案優先・未確定。確定済みF02とP0-A02の因果・効力評価を変更せず、具体的な処理方法だけを比較。
- 新規比較資料につき今回のArchive移動なし。Confirmed・main変更なし、永久削除なし。

## P0同時刻イベントの境界・優先順位（未確定）
- [5項目のABC比較・短い具体例](Archive/P0_SIMULTANEOUS_EVENT_BOUNDARIES_OPTIONS.md)：期限ちょうど、権限取消／代理受領、イベント依存順序、二重徴税、開城／援軍。B案優先・未確定。
- 先行する同時刻イベントのB優先案は独自の検討履歴を持つためProposalsに維持。Confirmed・mainは変更せず、今回Archive移動・永久削除なし。

## P0同時刻イベント11項目の統合（最新・未確定）
- **優先参照先**：[`Proposals/P0_SIMULTANEOUS_EVENTS_CONSOLIDATED.md`](Proposals/P0_SIMULTANEOUS_EVENTS_CONSOLIDATED.md)。基本6項目＋境界5項目の選択済みB案を統合。
- **比較・選択履歴**：[`Archive/P0_SIMULTANEOUS_EVENT_OPTIONS.md`](Archive/P0_SIMULTANEOUS_EVENT_OPTIONS.md)、[`Archive/P0_SIMULTANEOUS_EVENT_BOUNDARIES_OPTIONS.md`](Archive/P0_SIMULTANEOUS_EVENT_BOUNDARIES_OPTIONS.md)。
- 2026-10-17：上記2資料をArchiveへ移動し、内容照合・参照更新後にProposalsの元配置を除去。Confirmed・main変更なし、永久削除なし。

## P0家督相続・独立・落城の帰属処理（未確定）
- [家臣・城・所領・軍勢・条約・債務のABC比較6項目と短い具体例](Archive/P0_SUCCESSION_INDEPENDENCE_FALL_OPTIONS.md)。Bは推奨候補、6項目はB案選択済み・未確定。
- 確定済みの家督・権利・占有・実効支配・忠誠の分離と段階更新を前提に、個別の帰属・承継条件を比較する。Confirmed・main変更なし。
- 現行統合案と比較案は独自情報を保持しており、今回Archive移動・永久削除なし。

## P0家督・独立・落城の重要詳細（未確定）
- [家臣服従・城内分裂・相続争い・条約・債務承継のABC比較5項目と短い例](Archive/P0_SUCCESSION_INDEPENDENCE_FALL_DETAILS_OPTIONS.md)。5項目ともB案選択済み・未確定。
- [先行する帰属処理6項目のB選択履歴](Archive/P0_SUCCESSION_INDEPENDENCE_FALL_OPTIONS.md)と既存Confirmedを参照。現行の比較・選択資料は固有情報があるため今回Archive移動なし。Confirmed・main変更なし、永久削除なし。

## P0家督・独立・落城11項目の統合（最新・未確定）
- **優先参照先**：[帰属変更11項目の統合設計](Proposals/P0_SUCCESSION_INDEPENDENCE_FALL_CONSOLIDATED.md)。基本6項目＋重要詳細5項目の選択済みB案を統合。
- **比較・選択履歴**：[基本6項目](Archive/P0_SUCCESSION_INDEPENDENCE_FALL_OPTIONS.md)、[詳細5項目](Archive/P0_SUCCESSION_INDEPENDENCE_FALL_DETAILS_OPTIONS.md)。両者を内容照合後にArchiveへ移動し、元配置を除去。
- 2026-10-17：Confirmed・main変更なし。資料内容の永久削除なし。

## P0帰属変更の追加確定候補（未確定）
- [帰属変更11項目から抽出した追加確定候補5項目のABC比較と短い例](Proposals/P0_SUCCESSION_ADDITIONAL_CONFIRMATION_SCOPE_OPTIONS.md)。5項目の追加基本原則は正式確定済み。比較・選択履歴と詳細・例外は未確定。
- [帰属変更11項目の現行未確定統合設計](Proposals/P0_SUCCESSION_INDEPENDENCE_FALL_CONSOLIDATED.md)と既存Confirmedの重複を区別。今回の旧資料Archive移動なし。Confirmed・main変更なし、永久削除なし。

## P0家督・独立・落城の追加基本原則5項目（確定）
- **確定正本**：[`Confirmed/P0_SUCCESSION_ADDITIONAL_PRINCIPLES.md`](Confirmed/P0_SUCCESSION_ADDITIONAL_PRINCIPLES.md)。既存F01〜F08・P0追加6原則と重複しない追加部分のみ。
- **未確定の詳細・例外と比較履歴**：[`Proposals/P0_SUCCESSION_ADDITIONAL_CONFIRMATION_SCOPE_OPTIONS.md`](Proposals/P0_SUCCESSION_ADDITIONAL_CONFIRMATION_SCOPE_OPTIONS.md)、[`Proposals/P0_SUCCESSION_INDEPENDENCE_FALL_CONSOLIDATED.md`](Proposals/P0_SUCCESSION_INDEPENDENCE_FALL_CONSOLIDATED.md)。
- 2026-10-17：design/foundationのみ更新。mainは変更せず、今回のArchive移動・永久削除なし。

## P0共通基盤・全体横断レビュー（2026-10-17）
- **最新の横断レビュー**：[確定済み原則、未確定詳細、重複・矛盾・依存関係、P0〜P2の残課題](Proposals/P0_FOUNDATION_CROSS_REVIEW_2026-10-17.md)。
- 今回は新たな確定原則なし。既存ConfirmedのF01〜F08・P0追加6原則・帰属追加5原則を維持。古い横断レビューV2は検討履歴として保持する。
- 比較資料の旧「未選択」表記よりも、後日の選択・確定記録を優先する。main変更なし、永久削除なし。

## P0イベント処理の信頼性（確定基本原則＋未確定詳細）
- **確定原則**：[イベント重複適用防止・失敗時整合性・セーブ継続整合性（R01〜R03）](Confirmed/P0_EVENT_RELIABILITY_PRINCIPLES.md)。既存F02/P0-A02/A04を変更しない追加品質要件。
- **未確定のABC比較**：[イベントID・重複実行防止・処理順序・復旧・セーブ再現性の5項目](Proposals/P0_EVENT_RELIABILITY_OPTIONS.md)。B案5項目はユーザー選択済み。基本処理契約はConfirmedに確定、詳細・例外は未確定。
- 2026-10-17：design/foundationのみ更新。旧版に相当する資料は今回特定せずArchive移動なし。main変更なし、永久削除なし。

## P0イベント処理のB案選択と追加確定（2026-10-17）
- [Confirmed：イベント信頼性R01〜R03と追加処理契約R04（5項目）](Confirmed/P0_EVENT_RELIABILITY_PRINCIPLES.md)。
- [Proposals：B案5項目の選択履歴、未確定のID生成・循環依存・復旧方式・セーブ互換](Proposals/P0_EVENT_RELIABILITY_OPTIONS.md)。
- 今回は現行資料の更新のみ。置換対象の旧資料なし、Archive移動・永久削除なし。main変更なし。

## P0イベント処理の詳細実装方式（未確定）
- [イベントID生成・処理状態・因果循環・競合裁定・失敗復旧・セーブ再現性のABC比較6項目](Proposals/P0_EVENT_PROCESSING_IMPLEMENTATION_OPTIONS.md)。6項目ともB案選択済み・未確定。
- 基本原則は既に[Confirmed](Confirmed/P0_EVENT_RELIABILITY_PRINCIPLES.md)に確定済み。今回の比較は実装詳細でありConfirmedへ新規追加なし。
- 先行5項目の選択履歴は現行Proposalsに維持。main変更なし、今回Archive移動・永久削除なし。

## P0イベント処理・詳細実装方式6項目のB案選択（2026-10-17）
- [選択済みB案6項目と未確定詳細](Proposals/P0_EVENT_PROCESSING_IMPLEMENTATION_OPTIONS.md)。イベントID・処理状態・因果循環・制度別競合・補償・セーブ再現性。
- 既存[確定原則R01〜R04](Confirmed/P0_EVENT_RELIABILITY_PRINCIPLES.md)と重複するため新たな基本原則の確定なし。main変更なし、今回Archive移動・永久削除なし。
