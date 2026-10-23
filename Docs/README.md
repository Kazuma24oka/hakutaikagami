[分隊別の固定宛先・照会・返書 V1](Design/UnitRecipientV1/README.md)：左右分隊の情報分離、不達回復、保存復旧の完成条件。

[保存中の画面応答 V1](Design/ResponsiveSaveV1/README.md)：正常/失敗・再試行・描画継続・保存復旧の完成条件。

- 現在：[既知部隊の選択と記録地点への本人移動](Design/KnownMilitaryTargetV1/README.md)（完成判定は同工程のverification.json）。前工程0ea1154の実績と現行状態を区別する。

- 現在：[本人の目撃・返書と軍務表示](Design/RecognizedMilitaryV1/README.md)（完成判定は同工程のverification.json）。前工程b7cc152の実績と現行の検証状態を区別する。

[現行工程：反復演習と生涯の損耗](Design/RepeatedExerciseV1/README.md)

以下は各当時の工程履歴として保持する。

[現行工程：同じ主君への再任と生涯台帳](Design/MilitaryRenewalV1/README.md)

以下は各当時の工程履歴として保持する。

[現行工程：取消結果の不達回収と権限終了](Design/UnitCancellationRecoveryV1/README.md)

[直前完了：特定の撤退原本の物理取消](Design/UnitCancellationV1/README.md)

以下の現行工程表記は各当時の履歴として保持する。

[現行工程：発送者が確認した受領証拠と再送](Design/UnitSenderEvidenceV1/README.md)

[現行工程：撤退命令の正式受領期限](Design/UnitOrderDeadlineV1/README.md)

[現行工程：命令原本の保持・実物遅着と受令返書](Design/UnitCommandHoldingV1/README.md)

[現行工程：保持原本・未受領報告の実物受渡し](Design/UnitHeldDeliveryV1/README.md)

[現行工程：返書自身の物理不達回復と原本保持](Design/UnitReplyRecoveryV1/README.md)

[現行工程：部隊受信役の物理照会と時点報告](Design/UnitPostInquiryV1/README.md)

[現行工程：部隊宛の既知不達後再送](Design/UnitPostRecoveryV1/README.md)

[現行工程：自動敗走・到達・再集結](Design/RoutRegroupV1/README.md)

[現行工程：本人退避・部隊撤退・運搬実体と指揮連絡](Design/PersonalWithdrawalV1/README.md)

[現行工程：明示演習交戦と損害・復旧検証](Design/ContactCombatV1/README.md)

[新入口の軍務・現地引受・指揮返還](Design/NewGameMilitaryV1/README.md)

[通常起動と仕官・辞去の現行検証](Design/ServiceEntryV1/README.md)

# 近距離動作の計測と60fps確認

[MotionResponseV1](Design/MotionResponseV1/README.md)：同じ局所場面の調整前後、60fps制限での実測と足運び・地形再利用。全国負荷の保証とは区別。

新規ゲームの通し接続候補：[NewGameFlowV1](Design/NewGameFlowV1/README.md)。管理→配置→地図/一覧選択→同じ人物の生活→保存再開。旧入口/保存を保持し、通常起動の採用判断は最後に行う。

登録武将・新規世界配置：[NewGameSetupV1](Design/NewGameSetupV1/README.md)。独立した登録/グループ複製/保存と、開始前の勢力・領土・浪人随行の検査。通常入口への接続は後続。

武将の人生の入口候補：[LifeEntryV1](Design/LifeEntryV1/README.md)。名前から同じ世界へ開始・再開し、ゲーム操作を整理した独立候補。通常起動の切替は未実施。

軍務の不達回復：[MilitaryInquiryV1](Design/MilitaryInquiryV1/README.md)。原本の受領を偽らず、物理照会の引用から条件/許可/返還認識を回復。既知不達後の原本再送と局所回収の二重到達も検査。

同一人生の軍務・現地引受/返還：[CareerMilitaryV1](Design/CareerMilitaryV1/README.md)。仕官だけでは軍/兵糧を取得せず、同一人物で平時→出陣→帰還→辞去へ継続。現在の結果と次工程は[完成条件](Development/CURRENT_STAGE.md)。

通常起動の選択入口：[WorkHubStartupV1](Design/WorkHubStartupV1/README.md)。ユーザー承認を受けて切替済み。旧Main/入口/保存は保持。

現地NPCの自動依頼：[NpcWorkV1](Design/NpcWorkV1/README.md)。本人の目撃と既知返報に基づく任意依頼・返報待ち・再提示。

複数拠点の任意点検：[RegionalWorkV1](Design/RegionalWorkV1/README.md)。実街道移動・3武将・固定宛先の返報。NPC自動選定は次段階。

全国人物台帳・在所の仕事：[NationalPersonnelV1](Design/NationalPersonnelV1/README.md)。3人・3勢力の本拠候補と108拠点の収録状況。全国任務生成は未実装。

通常起動の武将任務タブ：[TaskEntryV1](Design/TaskEntryV1/README.md)。

評価通知・訂正・本人応答の最新試作：[AssessmentNoticeV1](Design/AssessmentNoticeV1/README.md)。

任務差異・暫定審査の最新試作：[MissionReviewV1](Design/MissionReviewV1/README.md)。

任務受信の最新試作：[IncomingMissionV1](Design/IncomingMissionV1/README.md)。

## 軍議の作戦線・担当者入力

[入口と検証](Design/CouncilInputV1/README.md) / [現在段階・完成条件](Development/CURRENT_STAGE.md)

## 現在の開発運用と完成条件

[Codex運用方針](Development/OPERATING_POLICY.md) / [現在段階・完成条件・見通し](Development/CURRENT_STAGE.md)

## 武将の平時任務・軍議（2026-10-21）

[入口・検証](Design/OfficerLoopV1/README.md) / [設計と次の5件](Decisions/2026-10-21_OFFICER_LOOP_V1.md)

最新の全国・連続地形：`../Scripts/Show-ContinuousWorld.ps1`。[7項目の実装契約・範囲](Decisions/2026-10-20_CONTINUOUS_WORLD_V1.md)、[操作手順](Design/ContinuousWorldV1/README.md)、[実測](Design/ContinuousWorldV1/verification.json)。全国108拠点のGPU巡回、774テスト、36保存点84画面比較、26秒実動画。全国地形と城郭は合成の試作。

最新の背景交換基盤：`../Scripts/Show-SceneryAppearance.ps1`。[外観ID・固定配置・素材交換の契約](Decisions/2026-10-20_SCENERY_APPEARANCE_V1.md)、[素材交換手順](Design/SceneryAppearanceV1/README.md)、[実測](Design/SceneryAppearanceV1/verification.json)。道の材質・岩モデルを交換しても移動と保存復旧を維持。

最新の3D背景：`../Scripts/Show-RegionalScenery.ps1`。[山・森・道・川・城と移動判定の共用、検証範囲](Decisions/2026-10-20_REGIONAL_SCENERY_V1.md)。実測は [検証記録](Design/RegionalSceneryV1/verification.json)。

最新の一地域統合検証：`../Scripts/Show-RegionalIntegration.ps1`。[6段階の実装・検証・範囲](Decisions/2026-10-20_REGIONAL_INTEGRATION_V1.md)。地形起伏・衝突回避・命令判断・接敵・士気退却・道路接続と保存復旧。実測は [検証記録](Design/RegionalIntegrationV1/verification.json) を参照。

# 百代鏡 設計資料

最新の陸上布陣：`../Scripts/Show-FieldWorld.ps1`。[地形・権限・保存・実測と範囲](Decisions/2026-10-20_FIELD_WORLD_V1.md)。清洲近傍の合成地形で任意線へ自隊3分隊を配置。Core699件、4保存点の復旧・続行8比較で3D差分0。海路は固定航路を維持。


最新の通行条件競合：`../Scripts/Show-TrafficWorld.ps1`。[規則・検証・範囲](Decisions/2026-10-20_TRAFFIC_WORLD_V1.md)。行先変更と閉鎖、港での運休、出航後の継続。Core684件、12保存点の別プロセス復旧・続行24比較で3D差分0。


最新の本人操作：`../Scripts/Show-TravelControl.ps1`。[道中の行先変更・出航取りやめ・検証と範囲](Decisions/2026-10-20_TRAVEL_CONTROL_V1.md)。Core667件、9保存点の別プロセス復旧と続行18比較で3D差分0。


最新の全国連続移動：`../Scripts/Show-TravelWorld.ps1`。[出航待機・人物配置・実測と範囲](Decisions/2026-10-20_TRAVEL_WORLD_V1.md)。清洲→堺→洲本→清洲、本人の行先選択、港待機、3名の史料付き初期配置。Core646件、別プロセス復旧と続行8比較で3D差分0。


渡河の最新試作：[中島橋周辺の推定経路と検証範囲](Decisions/2026-10-20_RIVER_ROAD_V1.md)。入口 `../Scripts/Show-RiverRoad.ps1`。26秒の実画面動画はローカル保存。位置推定・検証用渡河と史実復元を区別する。

最新の地域縦断試作：`../Scripts/Show-RegionalCampaign.ps1`。物理伝令・再送・通行条件・複数武将・尾張／美濃／三河の概略リンクは [決定と実装範囲](Decisions/2026-10-20_REGIONAL_CAMPAIGN_V1.md) を参照。実道路・全国経路網は未完了。

## 現行の設計

ユーザー確認：「現行方針が正です」。旧資料との食い違いでは後続採用を優先します。[適用記録](Decisions/2026-10-19_CURRENT_POLICY_AUTHORITY.md)。

- **[基本設計書HTML](Design/basic-design-v1.html)** ／ [テキスト版](Design/BASIC_DESIGN_V1.md)。15分野60判断を委任採用済み。
- [機械可読正本](Design/basic-design-v1.json) ／ [共通境界・参考作品・詳細設計への引継ぎ8群](Design/basic-design-rules.json)。
- [採用と整合確認](Decisions/2026-10-19_BASIC_DESIGN_V1.md) ／ [決定履歴](Decisions/DECISION_LOG.md)。
- [平時・戦時の個人操作](Proposals/PLAYER_PEACE_WAR_FLOW.md) ／ [作戦線・連続世界の現行試作](Proposals/FORMATION_WORLD_V1.md)。
- [3D移行方針](Proposals/OFFICER_3D_TRANSITION.md) ／ [3D部品制作・検査手順](Proposals/OFFICER_3D_PARTS_GUIDE.md)。立ち絵は絵、ゲーム内は3D。旧2Dは凍結。
- [既存Confirmed原則](Confirmed/)は改変せず維持し、後続決定の適用範囲を上記採用記録で確認します。

## 尾張の道中取消・帰城変更（2026-10-20、後続）

[契約・実測・限界](Decisions/2026-10-20_REGIONAL_CONTROL_V1.md)。熱田での受渡し、受領・開封・効力の分離、現在位置停止と次の分岐からの帰城。全Core544件、実Godot40検査・4プロセス・26枚。8保存の復旧・続行16比較が差分0画素。[入力ハッシュと実行結果](Design/RegionalControlV1/verification.json)。入口 `Scripts/Show-RegionalControl.ps1`。今回以降、ユーザーから求められるまで検証HTMLを必須としません。

## 尾張の分岐・未知封鎖・迂回（2026-10-20、後続）

**[実画面付きHTML報告](Design/RegionalBranchV1/report.html)**。熱田で目撃してから笠寺・丹下へ迂回。主君には遅延報告。Core525件、新19件、実Godot25検査・4プロセス・19枚。未知状態の全画面2比較と保存復旧・続行の3D6比較がすべて差分0。入口 `Scripts/Show-RegionalBranch.ps1`。[契約・出典・範囲](Decisions/2026-10-20_REGIONAL_BRANCH_V1.md)。実在代表点と検証用概略線で、道路・橋・封鎖の史実を復元したものではありません。次は道中の取消・行先変更へ進みます。

## 尾張の実在拠点と移動・保存復旧（2026-10-20、後続）

**[実画面付きHTML報告](Design/RegionalRouteV1/report.html)**。清洲城→熱田神宮→善照寺砦の概略経路を新Coreの移動・委任・返報へ接続。Core506件、実Godot26検査・2プロセス・27枚、4段階の保存復旧と続行の12比較で3D差分0画素。入口 `Scripts/Show-RegionalRoute.ps1`。[契約・史料と限界](Decisions/2026-10-20_REGIONAL_ROUTE_V1.md)。現代代表点を結ぶ概略線で、1560年の道路・橋の復元ではありません。次はこの地域の分岐・通行条件・迂回へ進みます。

## 全国地図の表示基盤（2026-10-20、後続）

**[実画面付きHTML報告](Design/JapanMapV1/report.html)**。海岸線109ポリゴンと出典付き城・館30地点候補。全国→地域→現地を同じ3Dワールドと座標で表示。Core488件、実Godot51検査・12枚、全国へ戻す画素差分0。入口 `Scripts/Show-JapanMap.ps1`。[データと境界](Decisions/2026-10-20_JAPAN_MAP_V1.md)。現代の代表点であり1560年の地理シナリオ確定ではありません。次は一地域の実在拠点・経路を新Coreへ接続します。

## 移動中の命令変更・取消（2026-10-20、後続）

**[実画面付きHTML報告](Design/MovementControlV1/report.html)**。伝令の受渡し・開封・効力を分け、取消時の位置保持と次の分岐からの帰城変更を確認。Core475件、新18件、Godot58検査・GPU29枚、4段階の保存を両命令で復旧して16比較の3D画素差分0。入口 `Scripts/Show-MovementControl.ps1`。[共有した全国像・契約・限界](Decisions/2026-10-20_MOVEMENT_CONTROL_V1.md)。次は日本全国の概略地図と主要城・拠点へ進みます。

## 分岐経路と迂回（2026-10-20、後続）

**[分岐経路・未知封鎖・保存復旧のHTML報告](Design/BranchWorldV1/report.html)**。本人の認識だけで経路探索し、現地目撃後に迂回。主君には遅延報告。全Core457件、新29件、Godot24検査、GPU27枚。未知封鎖2比較と復旧・続行6比較で画素差分0。入口 `Scripts/Show-BranchWorld.ps1`。[契約・新保存版・限界](Decisions/2026-10-20_BRANCH_WORLD_V1.md)。静的封鎖の小規模架空グラフまでで、全国・動的な解除・通常Main移行は未実施です。

## 新Coreと3D経路の接続（2026-10-20、後続）

**[今回の短いHTML報告](Design/RouteWorldV1/report.html)**。架空の120m経路と区間別所要時間を固定し、新Coreの移動・委任・返報を既存3Dで表示。全Core428件、Godot15検査、GPU20枚、同一／別プロセス復旧の3D画素差分0を確認。入口 `Scripts/Show-RouteWorld.ps1`。[契約と限界](Decisions/2026-10-20_ROUTE_WORLD_V1.md)。全国実地理・分岐経路・通常Mainの移行は未実施。下記の412件報告は前段の履歴です。

## 共通Coreの実装結果（2026-10-20）

**[短いHTML報告](Design/CommonContractV1/implementation-report.html)**。G1の4項目完了後、共通の秒時計・命令・保存を実装し、架空地域で3任務の一巡と独立Godot画面まで検証。Core412件（既存317＋新95）成功、別プロセス復旧・途中終了・GPU4枚を確認。通常3D Mainと旧保存は維持。全制度・全国・全48受入の完了ではありません。[実装記録](Decisions/2026-10-20_COMMON_CORE_IMPLEMENTATION.md)。

## 共通契約の詳細設計V1

[共通データ・状態遷移・保存仕様HTML](Design/CommonContractV1/common-contract-v1.html)。推奨委任により24契約を採用。8状態軸・6 API境界・旧セーブ方針・実装順を記載。[採用記録](Decisions/2026-10-19_COMMON_CONTRACT_V1.md)。上記の後続実装は共通の検証用部分まで進んでいます。設計時の受入48件を全件合格した状態ではありません。

## 完了と残作業

基本設計V1の管理単位60件は解消済みです。詳細設計8群、史料・地理データ、制作・実装、性能達成、[予定受入170件](Design/basic-design-acceptance.json)の実行は別です。HTML等の検査179項目・既存Core317試験の成功を、新仕様全体の実装合格とは扱いません。

## 履歴とブランチ差分

- [design/foundationとmainの差分・アーカイブ判断](Decisions/2026-10-19_DESIGN_BRANCH_AND_ARCHIVE.md)。
- [統合済みの旧案と台帳](Archive/BasicDesignV1/README.md) ／ [移動対応表](Archive/basic-design-v1-moves.json)。旧未確定表記を現行の未選択状態へ戻さないでください。
- [以前の資料索引](Archive/BasicDesignV1/DOCS_INDEX_BEFORE_ARCHIVE.md)は当時の状態・詳細資料の探索用です。現行の状態判断には使いません。

## 管理規則

Confirmedは既存確定原則、Designは現行基本設計、Decisionsは採用・変更理由、Proposalsは現行試作・検討中の詳細・履歴への案内、Archiveは旧案です。履歴は削除せず保持し、通常の設計導線から分けます。design/foundationと同期sourcesは編集しません。
