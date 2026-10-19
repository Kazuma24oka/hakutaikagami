# 百代鏡 設計資料

## 武将個人の平時・戦時の操作方針（2026-10-19採用）

ユーザーは[操作と判断の7項目](Proposals/PLAYER_PEACE_WAR_FLOW.md)をすべてBで採用。[決定・整合確認記録](Decisions/2026-10-19_PLAYER_PEACE_WAR_FLOW.md)を参照する。任務＋自分の目標、本人の所在、拘束・競合による併用、目的・制約・報告条件による委任、本隊の地点指定と配下への命令、任務逸脱表示、自動進行＋任意停止＋重要時自動停止を設計前提とする。数値・例外・実装完了は別。

旧63項目等の「自動進行方式は採否未確定」は過去時点の履歴で、今回PW07により採用済み。速度・停止閾値等は引き続き未確定。Docs/Confirmedの原則を維持し、後続の採用内容は上記決定記録と操作設計で管理する。

現行の通常Mainは[作戦線・3部隊展開・連続ズーム試作](Proposals/FORMATION_WORLD_V1.md)、入口 `Scripts/Show-FormationWorld.ps1`。一本の線から個別軍令を発送し、伝令到達後に3部隊が展開する。全国概略と近景は同じワールドで、全国の実地理・進軍は未実装。旧入口は保持する。

## 現在の武将表現（2026-10-19）

ユーザー指示により旧2D方式を凍結し、3D方式へ変更。[切替記録と検証課題](Proposals/OFFICER_3D_TRANSITION.md)を先に参照する。旧OFFICER系資料は履歴であり、次の制作計画として自動継続しない。既存Confirmedは今回改変していない。

第2試作の実装・実測・未実施事項は[3D武将 第2試作](Proposals/OFFICER_3D_TRIAL_V2.md)。内応工作は3D対面構図の方向性だけを記録し、制作保留。

別具足・武将作成・混成表示の現行確認は[3D武将 第3試作](Proposals/OFFICER_3D_TRIAL_V3.md)。制作を繰り返す際は[固定した部品制作・検査手順](Proposals/OFFICER_3D_PARTS_GUIDE.md)を使う。外見登録とCoreの武将生成はまだ別。

現行の通常ゲームへの接続は[武将・通常保存・共通時計・小規模合戦の統合](Proposals/OFFICER_WORLD_INTEGRATION_V1.md)。既存武将への外見割当と通常保存を実装。新武将の能力・所属を伴う生成は未実装。合戦の数値は検証用で正式軍事仕様ではない。

その後の現行入口は[部隊・軍令配送・個人認識・地図の統合](Proposals/MILITARY_GRID_INTEGRATION_V1.md)と `Scripts/Show-MilitaryGrid.ps1`。既存共通配送、認識制限付きの表示、川・橋・林・街道の経路を接続した。旧接続検証は専用入口で保持。

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
- **未確定のABC比較**：[イベントID・重複実行防止・処理順序・復旧・セーブ再現性の5項目](Archive/P0_EVENT_RELIABILITY_OPTIONS.md)。B案5項目はユーザー選択済み。基本処理契約はConfirmedに確定、詳細・例外は未確定。
- 2026-10-17：design/foundationのみ更新。旧版に相当する資料は今回特定せずArchive移動なし。main変更なし、永久削除なし。

## P0イベント処理のB案選択と追加確定（2026-10-17）
- [Confirmed：イベント信頼性R01〜R03と追加処理契約R04（5項目）](Confirmed/P0_EVENT_RELIABILITY_PRINCIPLES.md)。
- [Proposals：B案5項目の選択履歴、未確定のID生成・循環依存・復旧方式・セーブ互換](Archive/P0_EVENT_RELIABILITY_OPTIONS.md)。
- 今回は現行資料の更新のみ。置換対象の旧資料なし、Archive移動・永久削除なし。main変更なし。

## P0イベント処理の詳細実装方式（未確定）
- [イベントID生成・処理状態・因果循環・競合裁定・失敗復旧・セーブ再現性のABC比較6項目](Archive/P0_EVENT_PROCESSING_IMPLEMENTATION_OPTIONS.md)。6項目ともB案選択済み・未確定。
- 基本原則は既に[Confirmed](Confirmed/P0_EVENT_RELIABILITY_PRINCIPLES.md)に確定済み。今回の比較は実装詳細でありConfirmedへ新規追加なし。
- 先行5項目の選択履歴は現行Proposalsに維持。main変更なし、今回Archive移動・永久削除なし。

## P0イベント処理・詳細実装方式6項目のB案選択（2026-10-17）
- [選択済みB案6項目と未確定詳細](Archive/P0_EVENT_PROCESSING_IMPLEMENTATION_OPTIONS.md)。イベントID・処理状態・因果循環・制度別競合・補償・セーブ再現性。
- 既存[確定原則R01〜R04](Confirmed/P0_EVENT_RELIABILITY_PRINCIPLES.md)と重複するため新たな基本原則の確定なし。main変更なし、今回Archive移動・永久削除なし。

## P0イベント処理11項目の統合（最新・未確定）
- **優先参照先**：[イベント処理11項目の統合設計](Proposals/P0_EVENT_PROCESSING_CONSOLIDATED.md)。基本5項目＋詳細6項目のB優先案を統合。
- **比較・選択履歴**：[基本5項目](Archive/P0_EVENT_RELIABILITY_OPTIONS.md)、[詳細6項目](Archive/P0_EVENT_PROCESSING_IMPLEMENTATION_OPTIONS.md)。内容一致・参照更新後に元配置を削除する。
- 確定原則は[イベント信頼性R01〜R04](Confirmed/P0_EVENT_RELIABILITY_PRINCIPLES.md)を優先。main変更なし、資料内容の永久削除なし。

## P0循環依存・競合・失敗復旧のABC比較（未確定）
- [循環検出・制度別競合・ロールバック・補償・再試行・セーブ中断の6項目](Archive/P0_EVENT_CYCLES_CONFLICTS_RECOVERY_OPTIONS.md)。6項目ともB案選択済み・未確定。
- [イベント処理11項目の統合案](Proposals/P0_EVENT_PROCESSING_CONSOLIDATED.md)と[確定原則R01〜R04](Confirmed/P0_EVENT_RELIABILITY_PRINCIPLES.md)を参照。新たな独立基本原則の確定なし。main変更なし、今回Archive移動・永久削除なし。

## P0循環依存・競合・失敗復旧6項目のB案選択（2026-10-17）
- [選択済みB案6項目と未確定詳細](Archive/P0_EVENT_CYCLES_CONFLICTS_RECOVERY_OPTIONS.md)。循環検出・制度別裁定・ロールバック／補償・再試行・セーブ中断。
- 既存[確定原則R01〜R04](Confirmed/P0_EVENT_RELIABILITY_PRINCIPLES.md)を重複確定しない。main変更なし、Archive移動・永久削除なし。

## P0イベント処理17項目の統合（最新・未確定）
- **優先参照先**：[基本5・詳細6・例外6の統合設計](Proposals/P0_EVENT_PROCESSING_CONSOLIDATED.md)。確定済み原則と未確定実装を分離。
- **例外6項目の比較・選択履歴**：[Archive](Archive/P0_EVENT_CYCLES_CONFLICTS_RECOVERY_OPTIONS.md)。内容一致と参照更新後にProposalsの旧配置を削除。
- 既存Confirmedの基本原則は変更しない。main変更なし、資料内容の永久削除なし。

## P0イベント処理の残課題（未確定）
- [循環解除・競合の確定境界・ロールバック／補償・再試行・セーブ復旧のABC比較6項目](Archive/P0_EVENT_RECOVERY_BOUNDARIES_OPTIONS.md)。6項目すべてB案選択済み・未確定。
- [イベント処理17項目の統合正本](Proposals/P0_EVENT_PROCESSING_CONSOLIDATED.md)と[確定原則R01〜R04](Confirmed/P0_EVENT_RELIABILITY_PRINCIPLES.md)を参照。既存基本原則と重複するため今回Confirmedへの追加なし。
- main変更なし、今回Archive移動・永久削除なし。

## P0イベント処理の残課題6項目・B案選択（2026-10-17）
- [循環解除・競合境界・ロールバック／補償・再試行・セーブ復旧のB選択履歴と未確定詳細](Archive/P0_EVENT_RECOVERY_BOUNDARIES_OPTIONS.md)。
- 既存[確定原則R01〜R04](Confirmed/P0_EVENT_RELIABILITY_PRINCIPLES.md)との整合を確認。追加確定なし。main変更なし、今回Archive移動・永久削除なし。

## P0イベント処理23項目の統合・完了判定（最新）
- **未確定統合正本**：[基本5＋詳細6＋例外6＋残課題6の統合とP0完了判定](Proposals/P0_EVENT_PROCESSING_CONSOLIDATED.md)。基本原則は確定済み、詳細・実装準備は未完了。P1並行検討は可能。
- **比較・選択履歴**：[残課題6項目](Archive/P0_EVENT_RECOVERY_BOUNDARIES_OPTIONS.md)。Archiveに内容照合後移動し元配置を除去。
- 既存Confirmedの基本原則を再確定しない。main変更なし、資料内容の永久削除なし。

## P0家督・独立・落城の複合状態遷移（未確定）
- [家臣・城・所領・軍勢・条約・債務・同時発生のABC比較7項目](Archive/P0_SUCCESSION_COMPOUND_TRANSITIONS_OPTIONS.md)。7項目すべてB案選択済み・未確定。
- [帰属変更11項目の統合](Proposals/P0_SUCCESSION_INDEPENDENCE_FALL_CONSOLIDATED.md)および[イベント処理23項目の統合](Proposals/P0_EVENT_PROCESSING_CONSOLIDATED.md)を参照。既存Confirmedと整合、追加確定なし。
- 現行正本に独自の未確定事項があるためArchive移動なし。main変更なし、永久削除なし。

## P0家督・独立・落城の複合状態遷移7項目のB案選択（2026-10-17）
- [家臣・城・所領・軍勢・条約・債務・同時発生のB案7項目と未確定詳細](Archive/P0_SUCCESSION_COMPOUND_TRANSITIONS_OPTIONS.md)。
- 既存[帰属変更の確定原則](Confirmed/P0_SUCCESSION_ADDITIONAL_PRINCIPLES.md)と[共通原則](Confirmed/P0_ADDITIONAL_PRINCIPLES.md)を再確定しない。main変更なし、今回Archive移動・永久削除なし。

## P0家督・独立・落城18項目の統合（最新・未確定）
- **優先参照先**：[帰属変更18項目の統合設計](Proposals/P0_SUCCESSION_INDEPENDENCE_FALL_CONSOLIDATED.md)。基本・詳細11＋複合7項目を統合し、重複・矛盾・依存関係を確認。
- **比較・選択履歴**：[複合7項目](Archive/P0_SUCCESSION_COMPOUND_TRANSITIONS_OPTIONS.md)。Archiveに内容照合後移動し元配置を除去。
- 確定済み基本原則は変更しない。main変更なし、資料内容の永久削除なし。

## P0共通基盤の最終横断レビュー・P1移行判定（2026-10-17）
- **最新レビュー**：[P0全体の確定済み原則・未確定詳細・重複・矛盾・依存関係・完了条件](Proposals/P0_FINAL_CROSS_REVIEW_AND_P1_READINESS.md)。
- **判定**：基本方針は確定済み、概念整合は概ね完了、実装前仕様と復旧検証は未完了。P1の並行設計は可能。
- 既存Confirmedの重複確定なし。先行横断レビューは固有の検討履歴として維持。main変更なし、今回Archive移動・永久削除なし。

## P1プレイヤー行動・ゲーム進行（未確定）
- [プレイヤーの立場・行動権限・行動指示・時間進行・NPC交渉・委任・結果通知のABC比較7項目](Archive/P1_PLAYER_ACTION_GAME_FLOW_OPTIONS.md)。7項目すべてB案選択済み・未確定。
- F01/F02/F04/F07/F08、情報・書状・P0共通原則との重複と整合性を確認。既存Confirmedの再確定なし。
- 今回置換できる旧資料は特定せずArchive移動なし。main変更なし、資料内容の永久削除なし。

## P1プレイヤー行動・ゲーム進行7項目のB案選択（2026-10-18）
- [選択済みB案7項目と未確定詳細](Archive/P1_PLAYER_ACTION_GAME_FLOW_OPTIONS.md)。立場・権限・複数行動・世界時間・交渉・委任・結果通知。
- F01/F02/F04/F07/F08および情報仕様と整合。自動進行＋任意停止は優先案であり未確定。既存Confirmedを重複確定しない。
- main変更なし、今回Archive移動・永久削除なし。

## P1プレイヤー行動・権限・越権行為（未確定）
- [行動メニュー・身分別操作・命令／嘆願・越権・成否・NPC反応・UI説明のABC比較7項目](Archive/P1_PLAYER_AUTHORITY_OVERRIDES_OPTIONS.md)。7項目すべてB案選択済み・未確定。
- [先行P1行動7項目](Archive/P1_PLAYER_ACTION_GAME_FLOW_OPTIONS.md)とF01/F04/F07/F08・P0-A01/A06に整合。新規Confirmed追加なし。
- 現行資料の固有内容を保持するためArchive移動なし。main変更なし、資料内容の永久削除なし。

## P1行動・権限・越権行為7項目のB案選択（2026-10-18）
- [行動メニュー・身分兼任・命令／嘆願・越権／密命・部分達成・NPC反応・可否説明のB案と未確定詳細](Archive/P1_PLAYER_AUTHORITY_OVERRIDES_OPTIONS.md)。
- F01/F04/F07/F08、P0-A01/A06、先行P1行動7項目と整合。既存Confirmedを重複確定しない。
- main変更なし、今回Archive移動・永久削除なし。

## P1委任・長期行動・時間進行（未確定）
- [行動併用・中断・再開／引継ぎ・NPC委任・世界時間／速度・通知／停止・緊急介入のABC比較7項目](Archive/P1_DELEGATION_LONG_ACTION_TIME_OPTIONS.md)。7項目すべてB案選択済み・未確定。
- F02/F04/F07/F08、情報仕様、P0-A01/A04/A06、イベント信頼性と整合。自動進行＋任意停止はまだ優先案であり正式確定ではない。
- 置換対象の旧資料なし、Archive移動・永久削除なし。main変更なし。

## P1委任・長期行動・時間進行7項目のB案選択（2026-10-18）
- [行動併用・中断・再開／引継ぎ・NPC委任・世界時間・通知／停止・緊急介入のB案と未確定詳細](Archive/P1_DELEGATION_LONG_ACTION_TIME_OPTIONS.md)。
- F02/F04/F07/F08、情報仕様、P0-A01/A04/A06、イベント信頼性と整合。自動進行＋任意停止は優先案であり未確定。既存Confirmedの重複確定なし。
- main変更なし、今回Archive移動・永久削除なし。

## P1プレイヤー行動21項目の統合（最新・未確定）
- **現行正本**：[基本7・権限越権7・委任時間7の統合設計](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)。重複・矛盾・依存関係と未確定詳細を整理。
- **比較・選択履歴**：[基本7](Archive/P1_PLAYER_ACTION_GAME_FLOW_OPTIONS.md)、[権限越権7](Archive/P1_PLAYER_AUTHORITY_OVERRIDES_OPTIONS.md)、[委任時間7](Archive/P1_DELEGATION_LONG_ACTION_TIME_OPTIONS.md)。内容一致と参照更新後に旧Proposals配置を削除する。
- F01/F02/F04/F07/F08等の確定原則は維持。自動進行＋任意停止は未確定。main変更なし、永久削除なし。

## P1プレイヤー操作フロー（未確定）
- [国人領主・陪臣・城主・大名の立場別行動、命令・移動・交渉・委任・結果確認のABC比較7項目](Archive/P1_PLAYER_OPERATION_FLOW_OPTIONS.md)。7項目すべてB案選択済み・未確定。
- [P1プレイヤー行動28項目の統合正本](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)とF01/F02/F04/F07/F08・P0-A01/A06に整合。新規Confirmed追加なし。
- 現行正本に独自の内容があるためArchive移動なし。main変更なし、永久削除なし。

## P1プレイヤー操作フロー7項目のB案選択（2026-10-18）
- [立場別行動・命令／嘆願・移動・委任・時間・結果通知・越権のB案と未確定詳細](Archive/P1_PLAYER_OPERATION_FLOW_OPTIONS.md)。
- [P1プレイヤー行動28項目の統合正本](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)とF01/F02/F04/F07/F08、P0-A01/A06に整合。自動進行＋任意停止は優先案であり未確定。
- 既存Confirmedの重複確定なし。main変更なし、今回Archive移動・永久削除なし。

## P1プレイヤー行動28項目の統合（最新・未確定）
- **現行正本**：[基本7＋権限越権7＋委任時間7＋操作フロー7の統合設計](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)。重複・矛盾・依存関係を整理。
- **比較・選択履歴**：[操作フロー7項目](Archive/P1_PLAYER_OPERATION_FLOW_OPTIONS.md)。内容一致と参照更新後に旧Proposals配置を削除。
- 自動進行＋任意停止は未確定。既存Confirmedの重複確定なし、main変更なし、永久削除なし。

## P1身分別操作メニュー（未確定・ABCD）
- [国人領主・陪臣・城主・大名の内政・軍事・外交・人事・謀略・兼任等7項目](Archive/P1_ROLE_BASED_ACTION_MENU_OPTIONS.md)。A/B/Cは短い具体例付き、Dはその他。7項目すべてB案選択済み・未確定。
- Dを選択した項目のみ次のチャットで自由記載を確認する。既存F01/F04/F07、P0-A01/A03/A06、P1行動28項目と整合。新規Confirmed追加なし。
- 置換可能な旧資料なし、Archive移動・永久削除なし。main変更なし。

## P1身分別操作メニュー7項目のB案選択（2026-10-18）
- [共通分類・内政・軍事・外交・人事・謀略・兼任の選択済みB案と未確定詳細](Archive/P1_ROLE_BASED_ACTION_MENU_OPTIONS.md)。
- F01/F04/F07、P0-A01/A03/A06、P1行動28項目と整合。既存Confirmedの重複確定なし。
- main変更なし、今回Archive移動・永久削除なし。

## P1越権行為・緊急権限・兼任（未確定・ABCD）
- [独立工作・密約・独断出陣・緊急防衛・役職兼任・権限取消・事後責任のABCD比較7項目](Archive/P1_UNAUTHORIZED_EMERGENCY_MULTIPLE_ROLES_OPTIONS.md)。Bは推奨、7項目すべてB案選択済み・未確定。D選択なし。
- F01/F02/F03/F07/F08、P0-A01/A02/A03/A06、P1行動28項目と身分別メニュー7項目に整合。新規Confirmed追加なし。
- 今回置換対象の旧資料なし、Archive移動・永久削除なし。main変更なし。

## P1越権行為・緊急権限・兼任7項目のB案選択（2026-10-18）
- [独立工作・密約・独断出陣・緊急防衛・役職兼任・権限取消・事後責任のB優先案と未確定詳細](Archive/P1_UNAUTHORIZED_EMERGENCY_MULTIPLE_ROLES_OPTIONS.md)。
- F01/F02/F03/F07/F08、P0-A01/A02/A03/A06、P1行動28項目・身分別メニュー7項目と整合。新規Confirmed追加なし。
- main変更なし、今回Archive移動・永久削除なし。

## P1プレイヤー行動42項目の統合（最新・未確定）
- **現行正本**：[基本28＋身分別メニュー7＋越権緊急兼任7の統合設計](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)。重複・矛盾・依存関係を確認。
- **比較・選択履歴**：[身分別メニュー7項目](Archive/P1_ROLE_BASED_ACTION_MENU_OPTIONS.md)、[越権緊急兼任7項目](Archive/P1_UNAUTHORIZED_EMERGENCY_MULTIPLE_ROLES_OPTIONS.md)。内容照合後に旧配置を削除。
- 確定済み原則は維持、詳細・例外は未確定。main変更なし、資料内容の永久削除なし。

## P1身分別の具体的コマンド一覧（未確定・ABCD）
- [国人領主・陪臣・城主・大名の内政・軍事・外交・人事・謀略15コマンド候補と7項目のABCD比較](Archive/P1_ROLE_COMMAND_CATALOG_OPTIONS.md)。Bは推奨、7項目すべてB案選択済み・未確定。D選択なし。
- [P1行動63項目の統合正本](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)とF01/F04/F07/F08、P0-A01/A03/A06に整合。新規Confirmed追加なし。
- 今回置換可能な旧資料なし、Archive移動・永久削除なし。main変更なし。

## P1身分別コマンド一覧7項目のB案選択（2026-10-18）
- [15コマンド候補・共通5分類・実行／申請／越権／不可・緊急例外・外交人事・謀略・兼任・結果のB案と未確定詳細](Archive/P1_ROLE_COMMAND_CATALOG_OPTIONS.md)。
- F01/F02/F04/F07/F08、P0-A01〜A04/A06、P1行動63項目と整合。既存Confirmedの重複確定なし。
- main変更なし、今回Archive移動・永久削除なし。

## P1コマンド拡充・網羅性（未確定・ABCD）
- [婚姻・養子・交易・金融・諜報・裁定・人質・寺社・文化と優先順位のABCD比較7項目](Archive/P1_COMMAND_COVERAGE_EXPANSION_OPTIONS.md)。Bは推奨、7項目すべてB案選択済み・未確定。D選択なし。
- [既存15コマンド](Archive/P1_ROLE_COMMAND_CATALOG_OPTIONS.md)、[P1行動63項目](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)とF01/F03/F07/F08、P0-A01/A03/A04/A06に整合。新規Confirmed追加なし。
- 置換可能な旧資料なし、Archive移動・永久削除なし。main変更なし。

## P1コマンド拡充・網羅性7項目のB案選択（2026-10-18）
- [婚姻・養子・交易・諜報・裁定・人質・寺社・文化とP1/P2優先順位のB案と未確定詳細](Archive/P1_COMMAND_COVERAGE_EXPANSION_OPTIONS.md)。
- F01/F03/F07/F08、P0-A01/A03/A04/A06、既存15コマンド・P1行動63項目と整合。新規Confirmed追加なし。
- main変更なし、今回Archive移動・永久削除なし。

## P1追加コマンドの具体化（未確定・ABCD）
- [婚姻・養子・交易・金融・諜報・裁定・人質・寺社・文化のコマンド名・対象・条件・結果とABCD比較7項目](Archive/P1_EXPANDED_COMMAND_DETAILS_OPTIONS.md)。Bは推奨、7項目すべてB案選択済み・未確定。D選択なし。
- [拡充7項目](Archive/P1_COMMAND_COVERAGE_EXPANSION_OPTIONS.md)、[既存15コマンド](Archive/P1_ROLE_COMMAND_CATALOG_OPTIONS.md)、[P1行動63項目](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)に整合。新規Confirmed追加なし。
- 今回置換可能な旧資料なし、Archive移動・永久削除なし。main変更なし。

## P1追加コマンド詳細7項目のB案選択（2026-10-18）
- [婚姻・養子・交易・金融・諜報・裁定・人質・寺社・文化の具体的コマンドとB案・未確定詳細](Archive/P1_EXPANDED_COMMAND_DETAILS_OPTIONS.md)。
- F01/F03/F07/F08、P0-A01/A03/A04/A06、先行P1拡充7項目・15コマンド・42項目と整合。既存Confirmedの重複確定なし。
- main変更なし、今回Archive移動・永久削除なし。

## P1プレイヤー行動63項目の統合（最新・未確定）
- **現行正本**：[既存42＋身分別コマンド7＋拡充7＋追加詳細7の統合設計](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)。共通5分類・操作状態・案件状態・依存関係を整理。
- **比較・選択履歴**：[身分別コマンド](Archive/P1_ROLE_COMMAND_CATALOG_OPTIONS.md)、[拡充](Archive/P1_COMMAND_COVERAGE_EXPANSION_OPTIONS.md)、[追加詳細](Archive/P1_EXPANDED_COMMAND_DETAILS_OPTIONS.md)。内容照合後に旧Proposals配置を削除。
- Confirmedの重複確定なし、main変更なし、永久削除なし。

## P1/P2実装範囲・完成条件（未確定・ABCD、2026-10-18）
- [63項目からP1必須・P2拡張・実装前依存・完了判定を比較する7項目](Archive/P1_P2_SCOPE_AND_ACCEPTANCE_OPTIONS.md)。7項目すべてB案選択済み・未確定。D選択なし。
- [63項目統合正本](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)およびF01/F02/F04/F07/F08・P0-A01〜A06と整合。自動進行＋任意停止は未確定。新規Confirmedなし。
- 置換対象の旧資料なし、Archive移動・永久削除なし。main変更なし。

## P1/P2実装範囲・完成条件7項目のB案選択（2026-10-18）
- [P1必須・P2拡張・身分別検証・共通契約・P0依存・時間／委任・P1完了判定のB優先案と未確定詳細](Archive/P1_P2_SCOPE_AND_ACCEPTANCE_OPTIONS.md)。
- F01/F02/F04/F07/F08、P0-A01〜A06、P1行動63項目と整合。自動進行＋任意停止は未確定。実装・テスト未実施、P1未完了。
- 新規Confirmedなし、main変更なし、今回Archive移動・永久削除なし。

## P1代表コマンド・完成テスト（未確定・ABCD）
- [5分類の代表コマンド、正常・部分達成・失敗・越権・中断・NPC反応・セーブ再現性の7項目](Archive/P1_REPRESENTATIVE_COMMAND_ACCEPTANCE_TEST_OPTIONS.md)。7項目すべてB案選択済み・未確定。D選択なし。
- [63項目統合](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)、[P1/P2範囲](Archive/P1_P2_SCOPE_AND_ACCEPTANCE_OPTIONS.md)およびF01/F02/F04/F07/F08、P0-A01〜A06と整合。Confirmedとmain変更なし。Archive移動・永久削除なし。

## P1代表コマンド・完成テスト7項目のB案選択（2026-10-18）
- [5分類の代表操作、成否・越権・中断／再開・NPC反応・同時刻競合／ロード・P1完了のB案と保留詳細](Archive/P1_REPRESENTATIVE_COMMAND_ACCEPTANCE_TEST_OPTIONS.md)。
- F01/F02/F04/F07/F08、P0-A01〜A06、P1行動63項目・P1/P2範囲と整合。自動進行＋任意停止は未確定。テスト実施・P1完了宣言なし。
- Confirmed・main変更なし。Archive移動・永久削除なし。

## P1受入テスト・合格条件（未確定・ABCD）
- [5分類×4立場、成否・越権・中断再開・NPC反応・イベント復旧・P1合格条件の7項目](Archive/P1_ACCEPTANCE_TEST_CRITERIA_OPTIONS.md)。7項目すべてB案選択済み・未確定。D選択なし。
- [代表コマンド試験](Archive/P1_REPRESENTATIVE_COMMAND_ACCEPTANCE_TEST_OPTIONS.md)、[P1/P2範囲](Archive/P1_P2_SCOPE_AND_ACCEPTANCE_OPTIONS.md)、[63項目統合](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)と整合。Confirmed・main変更なし。Archive移動・永久削除なし。

## P1受入テスト・合格条件7項目のB案選択（2026-10-18）
- [5分類×4立場・成否／資源・越権／取消／防衛・中断／再開・NPC／情報・競合／復旧・P1受入のB優先案と未決定詳細](Archive/P1_ACCEPTANCE_TEST_CRITERIA_OPTIONS.md)。
- F01/F02/F04/F07/F08、P0-A01〜A06、P1行動63項目・P1/P2範囲・代表テストに整合。新規Confirmed追加なし、実装・テスト未実施。
- main変更なし、今回Archive移動・永久削除なし。

## P1プレイヤー行動84項目・最終横断レビュー（最新、2026-10-18）
- **現行正本**：[84項目の最終統合、重複・矛盾・P0依存・P2移管・設計完了判定](Proposals/P1_PLAYER_ACTION_84_FINAL_REVIEW.md)。設計完了は未達、詳細は未確定。
- **旧比較・選択履歴**：[63項目](Archive/P1_PLAYER_ACTION_CONSOLIDATED.md)、[P1/P2範囲](Archive/P1_P2_SCOPE_AND_ACCEPTANCE_OPTIONS.md)、[代表試験](Archive/P1_REPRESENTATIVE_COMMAND_ACCEPTANCE_TEST_OPTIONS.md)、[受入試験](Archive/P1_ACCEPTANCE_TEST_CRITERIA_OPTIONS.md)。
- 新規Confirmedなし、main変更なし、資料内容の永久削除なし。

## P1共通コマンド契約（未確定・ABCD、2026-10-18）
- [コマンド／案件／イベントID、権限、状態、費用・引当、失敗復旧、通知、完成条件の7項目](Archive/P1_COMMON_COMMAND_CONTRACT_OPTIONS.md)。7項目すべてB案選択済み・未確定。D選択なし。
- [84項目最終統合](Proposals/P1_PLAYER_ACTION_84_FINAL_REVIEW.md)とF01/F02/F03/F04/F07/F08、P0-A01〜A06に整合。自動進行方式は未確定。新規Confirmed追加なし、main変更なし、Archive移動・永久削除なし。

## P1共通コマンド契約7項目のB案選択（2026-10-18）
- [ID・権限・状態・費用・復旧・通知・完成条件のB優先案と未決定詳細](Archive/P1_COMMON_COMMAND_CONTRACT_OPTIONS.md)。
- F01〜F04/F07/F08、P0-A01〜A06、P1設計84項目と整合。自動進行＋任意停止は未確定。新規Confirmed追加なし。
- main変更なし、今回Archive移動・永久削除なし。

## P1コマンドID・状態遷移・失敗復旧（未確定・ABCD）
- [ID生成、状態遷移、中断再開、再試行、補償、セーブ復旧、P0責任分界の7項目](Archive/P1_COMMAND_ID_STATE_RECOVERY_OPTIONS.md)。7項目すべてB案選択済み・未確定。D選択なし。
- [共通契約B優先案](Archive/P1_COMMON_COMMAND_CONTRACT_OPTIONS.md)・[84項目統合](Proposals/P1_PLAYER_ACTION_84_FINAL_REVIEW.md)と整合。新規Confirmed追加なし、main変更なし、Archive移動・永久削除なし。

## P1コマンドID・状態遷移・失敗復旧7項目のB案選択（2026-10-18）
- [ID・状態遷移・中断再開・失敗再試行・補償・保存復旧・P0責任分界のB優先案と未決定詳細](Archive/P1_COMMAND_ID_STATE_RECOVERY_OPTIONS.md)。
- F01/F02/F03/F04/F07/F08、P0-A01〜A06、P1共通契約7項目・84項目統合と整合。新規Confirmed追加なし。
- main変更なし、Archive移動・永久削除なし。実装・テスト未実施。

## P1 ID生成・状態遷移・復旧境界の詳細（未確定・ABCD）
- [採番・親子イベント・許可／禁止遷移・再試行限度・補償失敗・乱数保存・復旧受入の7項目](Archive/P1_ID_TRANSITION_RECOVERY_BOUNDARIES_OPTIONS.md)。7項目すべてB案選択済み・未確定。D選択なし。
- [P1 ID・状態・復旧のB優先案](Archive/P1_COMMAND_ID_STATE_RECOVERY_OPTIONS.md)、[共通契約](Archive/P1_COMMON_COMMAND_CONTRACT_OPTIONS.md)、[84項目統合](Proposals/P1_PLAYER_ACTION_84_FINAL_REVIEW.md)と整合。Confirmed・main変更なし。Archive移動・永久削除なし。

## P1 ID生成・状態遷移・復旧境界7項目のB案選択（2026-10-18）
- [採番・親子イベント・許可遷移・再試行・補償・乱数／ロード・復旧受入のB優先案と未確定詳細](Archive/P1_ID_TRANSITION_RECOVERY_BOUNDARIES_OPTIONS.md)。
- F01/F02/F03/F07/F08、P0-A01/A02/A04/A06、P1共通契約・ID状態復旧B案と整合。新規Confirmedなし。
- main変更なし、Archive移動・永久削除なし。実装・テスト未実施。

## P1共通コマンド契約21項目・統合（最新・未確定）
- **現行正本**：[21項目を10領域に統合しP0/P1責任分界・残課題を整理](Proposals/P1_COMMON_COMMAND_CONTRACT_21_CONSOLIDATED.md)。
- **選択履歴**：[基本7](Archive/P1_COMMON_COMMAND_CONTRACT_OPTIONS.md)、[ID・状態・復旧7](Archive/P1_COMMAND_ID_STATE_RECOVERY_OPTIONS.md)、[復旧境界詳細7](Archive/P1_ID_TRANSITION_RECOVERY_BOUNDARIES_OPTIONS.md)。
- Confirmed・main変更なし。旧配置は内容一致確認後のみ削除。永久削除なし。

## P1費用・資源の確定境界（未確定・ABCD）
- [見積・引当・消費・返還・債務／保証・資源不足／競合・補償／セーブの7項目](Archive/P1_RESOURCE_COMMIT_BOUNDARIES_OPTIONS.md)。7項目すべてB案選択済み・未確定。D選択なし。
- [P1共通契約21項目統合](Proposals/P1_COMMON_COMMAND_CONTRACT_21_CONSOLIDATED.md)、F01/F02/F03、P0-A01〜A06に整合。Confirmed・main変更なし。Archive移動・永久削除なし。

## P1費用・資源確定境界7項目のB案選択（2026-10-18）
- [見積・承認・引当・実消費・返還・債務・競合・補償／保存のB優先案と未確定詳細](Archive/P1_RESOURCE_COMMIT_BOUNDARIES_OPTIONS.md)。
- F01/F02/F03、P0-A01〜A06、P1共通契約21項目と整合。新規Confirmed追加なし。
- main変更なし、Archive移動・永久削除なし。実装・テスト未実施。

## P1資源確定時点・同時刻競合（未確定・ABCD）
- [引当／消費／返還時点、部分実行、同時刻裁定、競合後再評価、債務／担保、中断損失、補償／セーブの7項目](Archive/P1_RESOURCE_TIMING_CONFLICT_OPTIONS.md)。7項目すべてB案選択済み・未確定。D選択なし。
- [費用・資源境界7項目](Archive/P1_RESOURCE_COMMIT_BOUNDARIES_OPTIONS.md)と[共通契約21項目](Proposals/P1_COMMON_COMMAND_CONTRACT_21_CONSOLIDATED.md)、F01/F02/F03・P0-A01〜A06に整合。Confirmed・main変更なし。Archive移動・永久削除なし。

## P1資源確定時点・同時刻競合7項目のB案選択（2026-10-18）
- [引当・消費時点、部分実行、同時刻裁定、競合後再評価、債務・担保、中断損失、補償・保存のB優先案と保留詳細](Archive/P1_RESOURCE_TIMING_CONFLICT_OPTIONS.md)。
- F01/F02/F03、P0-A01〜A06、P1共通契約21項目・費用資源7項目と整合。新規Confirmed追加なし。
- main変更なし、Archive移動・永久削除なし。実装・テスト未実施。

## P1資源関連14項目の統合（最新・未確定）
- **現行正本**：[費用・資源14項目の統合、重複・依存・P0会計責任分界](Proposals/P1_RESOURCE_14_CONSOLIDATED.md)。
- **ABCD比較・B選択履歴**：[費用・資源7項目](Archive/P1_RESOURCE_COMMIT_BOUNDARIES_OPTIONS.md)、[資源時点・同時刻競合7項目](Archive/P1_RESOURCE_TIMING_CONFLICT_OPTIONS.md)。
- Confirmed・main変更なし。内容一致確認後に旧配置を削除、永久削除なし。

## P1資源不足・同時刻競合の制度別裁定（未確定・ABCD）
- [軍事・内政・交易・外交の優先、緊急軍需、部分実行最低量、待機期限、NPC裁量、再現性の7項目](Proposals/P1_RESOURCE_INSTITUTIONAL_ARBITRATION_OPTIONS.md)。7項目すべてB案選択済み・未確定。D選択なし。
- [P1資源14項目統合](Proposals/P1_RESOURCE_14_CONSOLIDATED.md)とF01/F02/F03/F07/F08、P0-A01〜A06に整合。Confirmed・main変更なし、Archive移動・永久削除なし。

## P1制度別競合裁定7項目のB案選択（2026-10-18）
- [軍事・内政・交易・外交の競合、緊急軍需と契約、部分実行、待機再評価、NPC裁量、復旧のB優先案と未確定詳細](Proposals/P1_RESOURCE_INSTITUTIONAL_ARBITRATION_OPTIONS.md)。
- F01/F02/F03/F07/F08、P0-A01〜A06、P1資源14項目統合と整合。新規Confirmed追加なし。
- main変更なし、Archive移動・永久削除なし。実装・テスト未実施。

## P0/P1最小プロトタイプ製造開始ゲート（未確定・ABCD）
- [P0/P1横断レビュー、技術・時間・権限・ID・資源・保存の着手ブロッカーと7項目の選択肢](Proposals/PROTOTYPE_READINESS_P0_P1_OPTIONS.md)。7項目すべてB案選択済み・未確定。D選択なし。
- [P1行動84項目](Proposals/P1_PLAYER_ACTION_84_FINAL_REVIEW.md)、[共通契約21項目](Proposals/P1_COMMON_COMMAND_CONTRACT_21_CONSOLIDATED.md)、[資源14項目](Proposals/P1_RESOURCE_14_CONSOLIDATED.md)とF01〜F08、P0-A01〜A06に整合。Confirmed・main変更なし、Archive移動・永久削除なし。

## 最小プロトタイプ製造開始ゲート7項目のB案選択（2026-10-18）
- [範囲・時間・権限・共通契約・資源会計・保存復旧・完成条件のB優先案と未決定事項](Proposals/PROTOTYPE_READINESS_P0_P1_OPTIONS.md)。
- F01/F02/F03/F04/F07/F08、P0-A01〜A06、P1行動84項目・共通契約21項目・資源14項目と整合。自動進行＋任意停止は正式未確定。
- 新規Confirmedなし、main変更なし、Archive移動・永久削除なし。製造開始ゲート未達、実装・テスト未実施。

## 最小プロトタイプ技術選定・実装仕様（未確定・ABCD）
- [ゲームエンジン・データ構造・セーブ形式・イベント／時間・最小UI・テスト環境・段階実装の7項目](Proposals/PROTOTYPE_TECHNICAL_IMPLEMENTATION_OPTIONS.md)。7項目すべてB案選択済み・未確定。D選択なし。
- [P0/P1製造開始ゲート](Proposals/PROTOTYPE_READINESS_P0_P1_OPTIONS.md)およびF01/F02/F03/F04/F07/F08・P0-A01〜A06と整合。Confirmed・main変更なし。Archive移動・永久削除なし。

## 最小プロトタイプ技術選定・実装仕様7項目のB案選択（2026-10-18）
- [Unity第一候補・ID分離・バージョン付き保存・共通イベント／時間・最小UI・自動／手動試験・段階実装のB優先案](Proposals/PROTOTYPE_TECHNICAL_IMPLEMENTATION_OPTIONS.md)。
- F01/F02/F03/F04/F07/F08、P0-A01〜A06、製造開始ゲート7項目と整合。新規Confirmed追加なし。
- main変更なし、Archive移動・永久削除なし。製造開始ゲート未達、実装・テスト未実施。

## Godot 4系技術基盤7項目（未確定・ABCD）
- [開発言語・データ管理・セーブ・武将パーツ・アニメーション・テスト・素材管理の比較](Proposals/GODOT_TECH_FOUNDATION_7_OPTIONS.md)。B案7項目を選択・基本方針はConfirmedへ確定。D選択なし。
- Godot 4系は第一候補、正式採用は未決定。従来の[Unity第一候補の技術比較](Proposals/PROTOTYPE_TECHNICAL_IMPLEMENTATION_OPTIONS.md)は選定履歴として保持。Confirmed・main変更なし、Archive移動・永久削除なし。

## Godot技術基盤7項目の正式決定（2026-10-18）
- **確定原則**：[C#中心・ID分離・バージョン付きセーブ・パーツ合成・共通モーション・自動試験・素材管理](Confirmed/GODOT_TECH_FOUNDATION_PRINCIPLES.md)。
- **比較履歴・未確定詳細**：[Godot 7項目ABCD](Proposals/GODOT_TECH_FOUNDATION_7_OPTIONS.md)。Godot 4系は第一候補、正式採用は技術検証後。
- Unity第一候補の古い比較は選定履歴であり現行推奨ではない。main変更なし、Archive移動・永久削除なし。製造未着手。

## Godot 4系技術検証7項目（未確定・ABCD）
- [HD-2D・武将パーツ・アニメーション・会話／謀略・世界時間・C#ロジック・セーブ／テストの検証範囲、依存、合格条件、代替方式](Proposals/GODOT_TECH_VALIDATION_7_OPTIONS.md)。B案7項目を正式選択・検証基本方針はConfirmedへ確定。D選択なし。
- [Godot技術基盤Confirmed](Confirmed/GODOT_TECH_FOUNDATION_PRINCIPLES.md)、[P0/P1製造開始ゲート](Proposals/PROTOTYPE_READINESS_P0_P1_OPTIONS.md)に整合。Godot正式採用・製造開始は未決定。main変更なし、Archive移動・永久削除なし。

## Godot技術検証7項目の決定（2026-10-18）
- **Confirmed**：[HD-2D・パーツ・アニメ・会話／謀略・世界時間・C#・セーブ／テストの検証基本方針](Confirmed/GODOT_TECH_VALIDATION_PRINCIPLES.md)。
- **Proposals**：[合格条件案・失敗時代替方式・実装詳細](Proposals/GODOT_TECH_VALIDATION_7_OPTIONS.md)。
- Godot正式採用・製造開始・技術検証の合格は未決定。main変更なし、Archive移動・永久削除なし。

## Godot実装準備7項目のB案確定（2026-10-18）
- **Confirmed**：[独立C#ロジック・機能別＋共通フォルダ・責務分離・パーツ規格・固定Seed・並行検証・合格方針](Confirmed/GODOT_IMPLEMENTATION_READINESS_PRINCIPLES.md)。
- **Proposals**：[ABCD比較履歴とクラスAPI・Pivot・スキーマ・受入閾値など未確定詳細](Proposals/GODOT_IMPLEMENTATION_READINESS_7_OPTIONS.md)。
- Godotの実機検証・正式採用・製造開始は未実施／未決定。既存Confirmedと整合、main変更なし、Archive移動・永久削除なし。


## 武将パーツ制作の再監査（2026-10-18）
- [指定資料・失敗・再利用した検査と固定工程](Proposals/OFFICER_ART_PRODUCTION_AUDIT.md)。旧パーツは不採用、裸素体は未制作。64×96・1倍の決定は維持。制作は記録した計画と共通ゲートを通す。


## 正面の裸素体候補（2026-10-18）
- [64×96・1倍、顔/髪/服なしの実画像と固定寸法・マスク・生成設定](Proposals/OFFICER_BODY_BASE_V1.md)。作者の正面検査済み、ユーザー外見受入・全方向・動作は未実施。[確認画面](Proposals/officer-body-base-review.html)。

- [元武将への対応を制作前に必須化](Proposals/OFFICER_SOURCE_CORRESPONDENCE_REPAIR.md)（素体の外見修正は未完了）

- [元武将に対応する裸素体 V2：実画像・重ね合わせ・検査](Proposals/OFFICER_BODY_SOURCE_V2.md)

- [仮素体：部位の着脱・肌色変更の確認画面](Proposals/officer-body-layer-review.html)（体型は外見未採用）

- [顔・髪・甲冑一式：着脱と元絵への復帰](Proposals/OFFICER_EQUIPMENT_PARTS_V2.md)（正面静止、仮素体）

- [甲冑8部位：個別着脱・元絵復帰の実画像検査](Proposals/OFFICER_ARMOR_SPLIT_V3.md)（64×96・1倍、正面静止）

- [正面の待機・歩行：8部位着脱と接地の制御試作](Proposals/OFFICER_FRONT_MOTION_V4.md)（64×96・1倍、正面足踏み、Godot未組込）

- [Godotゲーム画面：正面待機・歩行と甲冑8部位の着脱](Proposals/OFFICER_GODOT_MOTION_V5.md)（64×96・1倍、実GPU再生/画素検査済み）

- [正面歩行の改善と着脱耐久検査 V6](Proposals/OFFICER_FRONT_WALK_V6.md)（腕振り・膝・足上げ、前回比較、Godot実画面）
