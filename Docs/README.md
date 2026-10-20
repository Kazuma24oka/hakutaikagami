# 百代鏡 設計資料

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
