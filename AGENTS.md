# 仕官・離職後の同一人生（2026-10-22）

CareerV1 / created-career-peace-v1。追加開始「浪人・仕官と離職後の継続」。申し出/条件の封書/本人回答/主君受領による効力/通知開封を分離し、所属と直属の期間は別々に記録。本人向けRoleは開封履歴だけ。NPC面談待ちは自分の提示/解決の保存順で検査し、後の離職で昔のBindingOrderを書換えない。辞去の引継ぎは主君が実受領した返報。検証時のみ任務返報後の担当解除を初期条件で宣言し、懲罰的人事AIと扱わない。失職後も同じID/世界/時計/記憶/位置で甲府の任意仕事まで継続。軍/役職/土地/私有物資は増やさない。

全1233件、63実GPU、274画像比較+3素材寿命比較差分0。5ケース29保存点/58全世界比較も一致、同じ今後の操作と保存時停止を再現。旧PNG42枚同一。旧浪人JSON実物とNPC入力の旧形を保持。verify_career.py→実PNG点検→check_career_binding.py。給与/出世/懲罰的人事/他家仕官/同一Careerでの軍務は未実装。次は明示軍務の引受・返還を同じ人物の平時へ接続し、平戦の通し検査へ。通常起動の初期表示/旧開始/旧Main/保護ブランチは保持し、承認不要なら継続。

# 原返報の既知不達後再送（2026-10-22）

DelegationReturnRetryV1：本人が物理照会で再送も頼んだときだけ、相手が不達報告を知った受令/到達原本を同ID/本文/報告時刻・最大3便で再送。旧照会の動作/JSON形を保持。宛先は受領した本人便の最新発送時刻、同時刻は保存済み発送台帳順。旧照会遅着/本人移動を真GPSで補わない。通常照会の引用認識と原本正式受領を別にし、本人向けOriginalReceivedは知識と正式受領の両方を要す。全1202件・53実GPU・216保存比較+3素材寿命比較差分0。旧PNG33同一/変更5再点検。verify_delegation_return_retry.py→PNG点検→check_delegation_return_retry_binding.py。照会返答自身/失敗報告の再送や一般代理統合は未実装。次は継続的な仕官/離職・解任後の同一人生。段階境界では止めない。

# 委任先への物理照会（2026-10-22）

DelegationInquiryV1：本人の発送済み委任案件だけを通知営地/指定Move先へ照会。受領後に相手自身の認識・行動/停止/別計画/到達の申告を独立返書へ固定。原到達報告が不達でも引用を受領して情報回復できるが、原本の正式受領や知識を捏造しない。旧位置の引用は新しい目撃を戻さず、責任/処罰は未審査。全1181件・51実GPU・206保存比較+3素材寿命比較差分0。旧PNG24枚同一/変更10枚再点検。verify_delegation_inquiry.py→PNG点検→check_delegation_inquiry_binding.py。停止理由の単体fixtureと衝突シミュレーション試験を区別。次は返報側の既知不達後再送と最後に受領した返書先。ユーザー判断不要なら続行。

# 委任の再送基盤（2026-10-22）

DelegationTransportV1：不達報告を本人が物理受領した直近試行だけを同ID/本文・最大3便で再送。個々の試行に実経路と発送元/既知宛先を保存し、二重操作は同じ受付へ。受令/到達の返書先は実際に成功した便の発送元。通常画面の再送ボタンは不達認識後だけ表示。全1159件、49実GPU、196保存比較+3素材寿命比較差分0。verify_delegation_transport.py→PNG確認→check_delegation_transport_binding.py。テスト手順の通常保存操作漏れを修正し元失敗記録保持。次は物理照会・返報不達回復・停止理由の申告。ユーザー判断不要なら続行。

# 他家軍の明示委任（2026-10-22）

DelegationMissionV1 / commit2973f84：他家org:houseの所有/主従を維持した3部隊へのMove/Hold/Retreat許可。受令判断と到達の物理返報を分離し、後続計画が適用済みなら親/祖先の遅着でも巻き戻さない。1144件・47実GPU・186保存比較+3素材寿命比較は当時の実績。現在実行物の証拠は最新工程参照。

# 本人の所在と継続する野外（2026-10-22）

FieldPresenceV1：別の継続世界開始。本人の出陣名簿とRegion.Activeを分離し、本人が在所で点検/街道移動してもNPC部隊を止めない。BranchView/観測/発送元/3Dは実所在、兵糧は本人の出陣区間だけ。別働隊はorg:houseの初期演習移動で、無料指揮権や移籍を作らない。静止区間のみ次イベントへまとめ進行し、枯渇秒を固定して消費台帳を刻み方から独立。全1114件、41実GPU/156保存比較+3素材寿命比較差分0。旧PNG19枚はf9dcd84と同一。verify_field_presence.py→目視→check_field_presence_binding.py。次は他家の所有と既存主従を維持した明示許可、受令判断と実到達の別返報。旧開始/保存/保護ブランチは維持し、承認不要なら停止せず続ける。

# 主家兵糧の携行・消費・返還（2026-10-22）

FieldSupplyV1：別の補給付き演習開始。ResourceLot.MobileHolderで物理所在を保管者へ結び、所有は主家のまま。登録拠点の原子的受渡し/ID再実行拒否、兵数×秒の整数人秒消費、同兵数区間を結合。元のResourceLotsへ移動/消費台帳を結び二重在庫を作らない。全1094件、現行39実GPU/146保存比較+3素材寿命比較差分0、旧PNG14枚は511fe68と同一。SceneryVisualAssetsは有限の元素材を強参照保持。一度のGodot未初期化GCHandleエラーの厳密な競合原因は未確定だが、対策後144強制GC位置更新と全回帰成功。失敗ログ保持。verify_field_supply.py→目視→check_field_supply_binding.py。次は他家軍の所有を維持した明示委任と、受令判断/実到達返報を分ける。既存の別働隊はorg:houseであり織田家への無断移籍や無料指揮許可をしない。

# 明示交戦任務と自隊報告（2026-10-22）

EngagementMissionV1：配置開始を保持し、別の架空演習開始に対象敵組織/主君/本人/期限/根拠を登録。自隊現況は発送時刻までの損害台帳で固定し、敵の隠れた兵数を混ぜない。物理到達前に主君のViewへ出さず、同IDの再操作は再送/本文更新しない。全1068件、実GPU34プロセス/136比較差分0、旧PNG11枚はc30f379と全バイト一致。verify_engagement_mission.py→実画像点検→check_engagement_mission_binding.py。次は携行物資の引当/消費/返還。攻城/捕縛/人物死亡判定は未実装。

# 作成家臣と本隊配置の同一世界（2026-10-22）

CreatedCampaignV1：配置任務の別開始。主家兵100/明示Move・Hold・Retreat許可。所属だけで交戦/他隊命令しない。指揮官を部隊表から取得。未知任務で移動を拘束せず、野外点検は帰還後。通常書状は発送時の物理所在と地形上の第一行程を保存。旧JSON実物を保持。全1047件、実GPU32プロセス/126比較差分0。verify_created_campaign.py→実画像目視→check_created_campaign_binding.py。次は明示交戦任務・損耗・物理報告。ユーザー確認が不要なら停止せず進める。

# 作成家臣の開始（2026-10-22）

RetainerStartV1：作成人物の架空の開始合意として織田家所属/信長直属と両者の初期認識を別に登録。史実の任命/軍/土地/徴税権ではない。主君はBindingOrder、他家はVoluntaryRequest。本人の既知命令の中断と独立の理由書状を既存配送へ接続し、相手の認識や責任確定を先取りしない。全1010件/実GPU26プロセス/96比較差分0。verify_retainer_start.py→実画像点検→check_retainer_start_binding.py。旧開始包/旧世界を保持。次は固定指揮官IDを部隊定義へ置き換え、同じ世界の出陣帰還へ進む。

# 通常入口の作成人物開始（2026-10-21）

StartSelectionV1：WorkHubの既存初期表示/二タブを保ち、三番目に名前入力→開始と保存一覧からの再開を追加。開始包内容ハッシュ/保存インスタンスごとに分離し、同名再開始でも上書きしない。戻る際は世界を保持/非表示停止、未保存の切替はゲーム内確認。明示開始定義がノード自動改名より優先。全985件、実GPU20プロセス/66比較差分0、旧保存全ファイル不変。不正名/破損/取消も確認。verify_start_selection.py→実画像点検→check_start_selection_binding.py。次は家臣開始の所属/直属/許可を分けて接続する。

# 独立した作成人物（2026-10-21）

PersonalStartV1：created-person-peace-v1、person:created/playerの浪人。旧人物のID/記憶/所属/軍/徴税権をコピーしない。共有画面の操作主体/受信箱/3D/既知部隊をS.PlayerControlへ統一。全985件、実GPU18プロセス/56比較差分0、旧PNG65枚は477d3cfと同一。正本verify_personal_start.py→実画像点検→check_personal_start_binding.py。独立シーンのみで通常入口にはまだ未接続。次は名前入力と開始前検査を通常WorkHubへ追加する。

# 開始設定の契約（2026-10-21）

StartConfigurationV1：六立場/三歴史設定を開始前世界のハッシュへ結合し、権限・所在・予算を検査する追加API。史実人物の権限は詳細採録前なので選択拒否。全970件（追加25件）成功、旧ゲーム動作を変えない。UI/開始世界接続は次段階。60判断の静的棚卸しはImplementationAuditV1、製品受入合格数ではない。以前の地形116比較はc1a1897の履歴。

# 地表判定の索引（2026-10-21）

LandIndexV1：旧ray式と境界を維持する緯度帯索引、全国投影の固定原点を再利用。全945テスト成功。全国216表示でCPU更新中央値62.96→34.12ms（45.79%減）。FPS達成ではない。現行UI/行動104比較・変更前保存6比較・全国変更前後6比較の計116比較差分0。世界/保存版は変更しない。verify_land_index.py→実画像点検→check_land_index_binding.py。次は確定基本設計と実装の差を棚卸しし、遊べる最小範囲を固定する。

# 地形負荷の内訳（2026-10-21）

TerrainBreakdownV1：計測指定時のみタイマーを有効化。全国216表示/214生成で全更新中央値62.96ms、地表判定46.24ms、高さ5.01ms、メッシュ確定0.43ms。実行前後に入力ハッシュを固定。今回の計測専用変更は現行Core933テストと6比較差分0で確認。110比較は804fe65時点の履歴と区別。verify_terrain_breakdown.py→点検済み画像との全バイト一致→check_terrain_breakdown_binding.py。次は旧Land判定を独立の比較基準へ保存して厳密な空間索引で探索量を減らす。未承認の仕様変更ではなく判定結果不変の性能調整として行う。

# 海岸線の再生成削減（2026-10-21）

MapCacheV1：中景は海岸線を生成せず、広域は固定原点で一度作り相対位置だけ更新。通行/武将位置はCoreのまま。全国216表示の冷間中央値63.17%減（174.41→64.23ms）、再利用p95 4.89ms。FPS達成ではない。変更前後6比較/全倍率/総合行動と合計110比較差分0、933テスト成功。verify_map_cache.py→実画像点検→check_map_cache_binding.py。次は残る処理負荷の内訳を実測して改善箇所を選ぶ。

# 現行背景と行動の総合検査（2026-10-21）

VisualAcceptanceV1：現行ソース/同じ実行物で移動・物理伝令/返報・橋渡りの9保存点を別プロセス復旧/続行し24比較差分0。直前の933テスト/80比較へハッシュで結合。全国108拠点×2倍率を再測定、冷間中央値174.41ms/再利用p95 3.13ms、メモリ予算内。FPSや製品規模達成ではない。verify_visual_acceptance.py→実画像点検→check_visual_acceptance_binding.py。次は不要な海岸線再生成を抑え視点移動負荷を調整する。

# 回転後の視点移動（2026-10-21）

CameraPanV1：GUIと共通のPanCameraで画面2点の投影線から地面上の差を計算。4方位×4倍率で投影移動誤差1画素以内、世界不変。OSマウスは操作しない。933テスト・80復旧等比較差分0。verify_camera_pan.py→実画像点検→check_camera_pan_binding.py。従来の再検査画像は目視済み画像とバイト一致で照合。次はVisualAcceptanceV1の既存移動/伝令/橋渡りと全国再測定。

# 地表材質と周期（2026-10-21）

GroundMaterialV1：固定無彩色土粒とミップマップを開発時生成。色IDは既存、周期はTileSizeMm（道4m/草6m）を正本とし、区画近傍原点からUV計算。UVScale/Offset/Repeat/両面描画を統一し外部材質既定値による地面消失を防ぐ。実メッシュ345600頂点の位相/負座標/遠方/実描画画素を検査。933テスト・73比較差分0。verify_ground_material.py→実画像点検→check_ground_material_binding.py。Core/旧素材/保存規則は維持。次は総合行動と視点入力。

# 背景の静的モデル（2026-10-21）

FieldModelsV1：既存配置ID/Seed/占有範囲を変えず岩2種/木/試作城郭を追加。元モデル/手続き形状を代替として保持。史実建築復元ではない。Scripts/generate_field_models.pyとGame/Tools/author_field_rocks.gdから再生成し全形状ハッシュ一致を検査。実GetFacesで占有/4m余白・欠損/不正寸法拒否。933テスト・68復旧等比較差分0とモデル3/素材4可視変化。verify_field_models.py→実画像点検→check_field_models_binding.py。Core/保存/旧素材は保持。次は地表材質と繰返し寸法。

# 既知部隊の中景表示（2026-10-21）

ForceLodV1：240m超〜30kmでRegionalBattle.Viewの把握済み部隊だけ記号化。古い目撃は灰色/凡例、真の位置/損耗を変えても表示不変。観測を削ると非表示。部隊名は5km以内で地名/他部隊名と重ならないものだけ表示し、全倍率で字体を固定。世界/操作権限/保存は変更しない。933テスト・62復旧等比較差分0と4素材可視変化。verify_force_lod.py→実画像点検→check_force_lod_binding.py。次は背景の既存配置/判定を維持したモデル改善。

# 倍率境界のカメラ（2026-10-21）

CameraContinuityV1：横幅基準を統一し30km〜500kmで全国画角へ連続接続。注視点の高さも連続化。3境界最大0.021%、全海岸線が画面内。カメラ7/中景7/素材8/旧36の58比較差分0、933単体テスト。verify_camera_continuity.py→実画像点検→check_camera_continuity_binding.py。自動検査指定時だけControl.MouseFilter.Ignoreで外部ポインターの強調色を排除。OSのマウス表示/位置/権限は変えず通常プレイも変更しない。次に既知部隊の中景記号を維持する。

# 倍率間の素材IDと地名（2026-10-21）

中景の独立パレットを廃止。MidTerrainRendererはSceneryAppearanceの既存役割/skinの材質色を参照し、概略経路線にも道路/水面色を使用。細部テクスチャ/モデルは詳細表示。地名はフォント寸法と投影矩形で距離/ID順に間引き、文字を経路線より手前へ。世界/保存は不変。正本LodAppearanceV1、verify_lod_appearance.py→実画像点検→check_lod_appearance_binding.py。933テスト・復旧等51比較差分0、4可視変化、全国216表示。旧性能レポートは以前の配色の履歴。現行負荷はLodAppearanceV1のperformanceと紐付く。

# 全国地形の負荷調整（2026-10-21）

TerrainPerformanceV1：108拠点×2倍率を実GPUで検査。共有頂点再利用・交換メッシュ解放・同一中心/倍率/寸法の地図再利用。世界不変と2048三角形を確認。冷間中央値11.7%減、再利用p95 2.53ms。FPSではない。前後6比較/復旧7比較/旧36比較差分0、933単体テスト。正本Docs/Design/TerrainPerformanceV1、check_terrain_performance_binding.py。旧共有コードの記録は履歴。

# 中間倍率の地形（2026-10-21）

240m超〜30kmはMidTerrainRendererで最大2048三角形。Core高さ/地表を参照する表示専用近似。通行規則/旧保存は変更しない。正本Docs/Design/MidTerrainV1。verify_mid_terrain.py→実画像点検→check_mid_terrain_binding.py。通常入口の回帰36比較と7倍率復旧7比較差分0。全国108拠点の負荷測定へ続行。

# 通常起動の選択入口（2026-10-21、承認済み）

ユーザー「従います。すすめて」によりWorkHubTrialへの通常起動切替を承認。例外はGame/project.godotのmain_sceneと必要な検証だけ。旧Main・保存・保護ブランチは保持。中間倍率改善へ連続して進める。正本Docs/Design/WorkHubStartupV1。

# 通常起動の選択入口候補（2026-10-21、切替未承認）

WorkHubTrial / Docs/Design/WorkHubV1/README.md。地域NPCの仕事を初期表示し旧Mainを遅延読込する独立候補。別セッション/保存と明示し、非表示の処理を止めて各状態を保持。旧起動指定は従来Mainそのものへ遷移して実ルート互換を維持。verify_work_hub.py→実画像点検→check_work_hub_binding.py。933テスト合格版Core/Tests・実GPU14プロセス36比較差分0。Docs/Proposals/WorkHubEntry.patchは未適用。既存の通常Main保持例外を広げず、通常起動切替の確認後にだけ例外記録と適用を行う。段階境界では停止せず地域依頼とNPC選定を完了してここへ到達。

# 現地NPCの自動依頼（2026-10-21）

NpcWorkTrial / npc-local-work-v1 / Docs/Design/NpcWorkV1/README.md。本人の現地目撃・Inspection許可・発送履歴・物理返報と3600秒再提示間隔だけで依頼を選ぶ。未返報の真の完了を知った扱いにせず、未知のWillObeyや遠方状態を使わない。判断入力/参照Revision/ハッシュ/根拠/受付結果を保存し、記録時点の移動と知識へ照合。入力不変なら維持し、共通時計・処理予算で更新する。旧規則のJSONへNPC欄を出さず、新平時試作だけ野外展開拒否。verify_npc_work.py→実画像点検→check_npc_work_binding.py。全933テスト、実GPU15プロセス58比較差分0。樹冠1画素差は許容差を増やさず固定回転で修正。通常入口の導線へ続ける。

# 複数拠点の任意依頼（2026-10-21）

RegionalWorkTrial / regional-peace-work-v1 / Docs/Design/RegionalWorkV1/README.md。清洲/甲府/小田原を既存街道で移動して任意点検を受ける。史実の立場と試作用Inspect合意を分離。氏康は1559年家督譲渡後。架空の主従/土地/徴税/軍事権を追加しない。依頼/返報はTask.Scopeへ物理配送し、自動追跡しない。旧保存/旧入口は保持。verify_regional_work.py→実画像点検→check_regional_work_binding.py。段階完了でもユーザー判断不要なら終了せず次工程へ続ける（OPERATING_POLICY末尾）。

# 全国人物台帳・在所の仕事（2026-10-21）

NationalPersonnel / Core/Data/national-personnel-v1.json / Docs/Design/NationalPersonnelV1/README.md。既存3人と3勢力の本拠候補・出典・確認日、全国108拠点の収録状態を固定。史実の本拠候補と現在地/領域/主従/命令権を混同しない。残る105拠点に領主を推測で加えない。台帳は検証用の収録管理画面で主人公の観測ではない。

通常Main→武将の任務→収録台帳。既存の試作主君命令/書役依頼は入力前に所在・主従有効期間・成人/権限・拘束を確認し、既存IncomingMissionのIntentと配送/保存を使用する。拒否意図を可否に漏らさない。既存規則/保存版は変更しない。全国任務生成・実在人物の役職/権限収録・NPC自律は未実装。今回の正本はverify_national_personnel.py→実画像点検→check_national_personnel_binding.py。旧共有ソースの検証記録は履歴として維持。

# 通常起動の武将任務タブ（承認済み）

ユーザー「はい」により通常Mainへの入口追加を承認。Main.TaskEntry.csが末尾に遅延読込タブを追加する。既存タブ番号と既存機能は保持。通常Main保持の例外はこの追加と必要な検証/修正に限る。mainブランチの変更許可ではない。正本Docs/Design/TaskEntryV1/README.mdとCURRENT_STAGE.md。旧初期化失敗ノードの後始末を修正し、旧布陣の再検査ゲートは維持。

# 評価通知・訂正・本人応答

AssessmentNoticeTrial / Docs/Design/AssessmentNoticeV1/README.md。根拠を発送時に固定した封書、後着理由による訂正版、旧原本の物理再送、本人の受領確認/再審査希望を接続。未開封本文/版/根拠は非表示。古い版で最新既知版を戻さない。通常Mainへの統合差分はDocs/Proposals/MainEntryIntegration.patchの未適用案だけとし、保護規則を維持。

# 任務差異・暫定審査の入口

MissionReviewTrial / Docs/Design/MissionReviewV1/README.md。本人の既知任務中断、独立の理由書状、評価者の受領情報だけの暫定審査。主君パネルは検証視点で主人公への通知ではない。成果・差異・理由・責任を分け、自動処罰しない。通常Main・旧保存・旧規則を保持。

# 任務受信の検証入口

IncomingMissionTrialは主君命令/配下依頼の独立試作。仕様と検証はDocs/Design/IncomingMissionV1/README.md。未着/未開封の任務内容をUIに渡さない。判断理由に未知の内部動機を混ぜない。既存の通常Main・旧規則版・旧保存を保持。

## 軍議の作戦線・担当者入力：完成（2026-10-21）

入口Scripts/Show-CouncilInput.ps1、CouncilEntryTrial。既存officer-mission-entry-v1のRegionalRequest/Commandと保存をそのまま使うUI拡張。新世界規則や二重時計はない。軍議で本隊/別働隊を選び、実地面に青い下書き線→明示送信→金色の送信済み線。下書きや担当変更、軍議中の誤ドラッグでは動かさない。本人指示と物理伝令/受令/返報を区別。既知の送信線だけを命令から描画し、判断結果はRegionalBattle.Status経由。到着報告とは扱わない。下書きは保存せず復旧時に破棄。

完成5/5、全816テスト（追加14）、実Godot9プロセス・4ケース・12保存点・24比較差分0、44PNG。正本Docs/Design/CouncilInputV1/verification.json。verify_council_input.py→実PNG点検→check_council_input_binding.py。旧bindingは共通コード更新で再検証まで失効。前段階の原則/成績は履歴として保持。Current stageと運用はDocs/Developmentへ。次は主君/配下の任務提示と判断理由を接続する。通常の報告は4項目・次作業1件。

## 在所任務入力：完成（2026-10-21）

入口Scripts/Show-MissionEntry.ps1、officer-mission-entry-v1。現在の運用はDocs/Development/OPERATING_POLICY.md、完成条件/次段階はCURRENT_STAGE.mdを参照。任務を点検/徴税・本人/書役・量1〜100・完了期限1〜3600秒で入力し、既存Intent/TaskState/権限/所在/資源/配送/保存へ接続。旧規則と旧保存は維持し読み替えない。下書きは保存せず送信済み内容を復元。

5条件達成、全802テスト（追加15）、実Godot9プロセス・4ケース・12保存点・24比較差分0、44PNG。正本Docs/Design/MissionEntryV1/verification.json。verify_mission_entry.py→実PNG点検→check_mission_entry_binding.py。旧bindingは共通コード更新で再検証まで失効。次は軍議の作戦線・担当者の入力。新しい報告形式を優先し、通常の次作業報告は1件。約5段階の見通しはCURRENT_STAGE.mdに残す。

## 開発運用方針（2026-10-21、現行）

最初にDocs/Development/OPERATING_POLICY.mdとCURRENT_STAGE.mdを読む。技術判断・実行・検証・進捗管理はCodexが担い、ユーザーは創作と重要操作を判断する。完成条件は3〜5件に固定し、不要な機能追加を避ける。既存の承認範囲内で再質問しない。現在の同じブランチへのcommit/push承認、保護対象、マウス操作ツール禁止を維持する。

報告は「今回完成したこと / 現在の状態（完成条件とテスト） / 次に行うこと1件 / ユーザーの判断（原則不要）」。下に残る古い「毎回5件」の記録は履歴。見通し5件はCURRENT_STAGE.mdへ残し、新しい報告形式を優先する。他プロジェクトや同期sources/へ適用しない。

## 武将の平時任務・軍議（2026-10-21、最新）

ユーザー「すすめて。次から5個先くらいまでの次作業書いて」。入口Scripts/Show-OfficerLoop.ps1、新規則officer-peace-war-loop-v1。Docs/Decisions/2026-10-21_OFFICER_LOOP_V1.mdとDocs/Design/OfficerLoopV1/README.mdを読む。

在所の本人点検＋書役徴税40の委任、main担当者拘束、返報待ちの野外展開拒否、軍議から物理配送する固定線布陣、本隊地点移動、拒否・返報逸失・交戦、帰還配送から次の平時任務へ接続。既存の時計・任務・資源・配送・戦闘・世代保存を再利用。旧プロファイルの挙動/版を保持。軍議開閉は世界を止めず、省略可能CouncilOpenで保存する。結果はGetActorViewとRegionalBattle.Status経由。返報前に移動しても使者は自動追跡しない。

Core 787件（追加13）、実Godot 10プロセス、4ケース・20保存点、復旧/続行40画面比較は差分0。比較PNG64枚、24秒576フレーム動画を実画像で点検。全国街道移動の旧プロファイル回帰も合格。

件数とSHAの正本はverification.json。verify_officer_loop.py→実画像/24秒動画点検→check_officer_loop_binding.py。未完：自由な目的/制約/報告編集、全国の任務生成と経済、人物評価、軍議人物演出、到着返報。親試作の全国地形/大量AIの制限も維持。通常Main・main・design/foundation・Confirmed・旧素材を保持。マウス操作ツール不使用。共通ソース変更で旧bindingは再検証まで失効。

今後の完了報告は次の作業を約5件、順番付きで提示する。次は①任務/軍議の目的・担当・制約入力、②主君/配下の任務提示と評価、③全国の人物・勢力・平時任務、④中間倍率LOD、⑤各拠点の地形/城郭と外観改善。

## 全国・連続地形（2026-10-20、最新）

ユーザー「全部やって」：隣接地域・境界保存・全国拡縮・負荷制御・命令戦闘・全国展開・背景改善の7項目を独立試作へ統合。入口Scripts/Show-ContinuousWorld.ps1、continuous-world-v1 / world:continuous-world-v1。Docs/Decisions/2026-10-20_CONTINUOUS_WORLD_V1.md と Docs/Design/ContinuousWorldV1/README.md を参照。

全国108拠点118経路を保持し、既存概略海岸線の陸海マスクと合成地形を共通10m格子・80m区画で遅延生成。最大25区画/12800地面三角形、遠方は詳細0。世界の秒時計・物理配送/返報・拒否・権限・接敵/同時損害/敗走は既存Core。地形三角形・経路切断・保存高さ・足元・地面選択を共用。640m/16384セルの局所探索、長距離は帰還/拠点経由で既存街道と固定海路。到着後に展開しても他武将・損害・過去の目撃を初期化しない。

新規則だけAtlas必須。旧世界/保存を読み替えず、世代保存の任意WorldCamera/ScenerySkinでカメラと外観を復元。実UIの再開ボタンで復旧後の続行も確認。全国は全海岸線の画面収まりを検査。人物のLODは保存カメラから決定的に選択。道/岩/植生/砦を段階的に改善し、既存の外観ID・材質/静的scene契約を再利用する。指定した道テクスチャを上書きしないこと、396実岩モデルの読込も確認。

Core774件（追加16）、実Godot25プロセス12ケース36保存点、GPU387検査、84比較差分0、168比較PNG。全国108拠点の実GPU巡回、26秒624フレーム動画のデコード/実画像点検済み。件数とSHAの正本はDocs/Design/ContinuousWorldV1/verification.json。verify_continuous_world.py→実画像点検→check_continuous_binding.py。runtime-localは生成証拠、マウス操作ツール不使用。通常Main・main・design/foundation・Confirmed・旧素材保持。共通コード変更により旧bindingは再検証まで失効。

全国の起伏・川・城郭の史実復元、固有城郭の制作、全城収録、全国NPC大量配置/AI、数万部隊の性能保証、中間倍率の立体LODは未実装。合成道路と全国リンクの全線形を詳細再現したものではない。集合位置は拠点代表点付近の試作で、城門内外の連続歩行演出は未実装。伝令の専用キャラ描画は新画面では未実装（物理経路と返報・UI結果は接続）。次は平時任務・軍議からこの共通世界へつなぐプレイループ、その後固有地形/城郭・中間LODを改善。

## 背景外観の差し替え（2026-10-20、最新）

入口Scripts/Show-SceneryAppearance.ps1。Docs/Decisions/2026-10-20_SCENERY_APPEARANCE_V1.md と Docs/Design/SceneryAppearanceV1/README.md を参照。外観ID・地域名前空間付き固定配置・安定したvariantキー・材質/静的scene対応表を分離。道は共有地形の材質交換、岩は静的モデル交換を実検証。制作基準寸法・地面中央ピボット・占有範囲を検査し、欠損/不適合は通知して手続き形状へ戻す。見た目を理由に移動判定を変更しない。

旧保存の省略可能ScenerySkinは基本外観、世界本体は不変。758テスト（追加20）、実Godot21プロセス10ケース30保存点、66画面一致比較は差分0。108PNG、26秒624フレーム動画、実画像点検済み。実測正本はverification.json。交換例は検証用の暫定素材。通常Main・main・design/foundation・Confirmed・旧素材保持、マウス操作不使用。旧bindingは共有コード変更で失効。次は隣接地域の境界・連続移動・保存復旧、その後全国負荷と背景品質。

## 一地域の3D背景（2026-10-20、最新）

入口Scripts/Show-RegionalScenery.ps1、regional-scenery-v1 / world:regional-scenery-v1。Docs/Decisions/2026-10-20_REGIONAL_SCENERY_V1.md参照。160mの合成地域、頂点0〜43mの山、森、土の道、川、木橋、木柵の砦。歴史的な清洲の復元ではない。背景は手続き形状、武将は既存3D。

RegionalSceneryの512三角形・通行種別・木/城壁/建物/橋柵の矩形・城門経路を正本に、移動/所要/保存高さ/モデル足元/描画/地面選択を共用。新プロファイルだけ適用し、旧傾斜面の保存定義は維持。版違い読み替え禁止。城門は6m、中庭の奥から整列。樹冠と屋根庇は頭上装飾、地面の通行判定は矩形と半径1m。視線遮蔽・攻城・城内階層・全国地形は未実装。

Core738件（新11）合格、実Godot9プロセス64検査40PNG。12保存点の復旧・続行24比較は3D差分0。26秒624フレーム動画。verify_regional_scenery.py→実画像点検→check_scenery_binding.py。件数とSHAはDocs/Design/RegionalSceneryV1/verification.jsonを正とする。森/橋/城/麓の実GPU復旧・続行比較と26秒動画。マウス操作ツール不使用、出力runtime-local、通常Main・main・design/foundation・Confirmed・旧素材保持。旧bindingは共有コード変更で再検証まで失効。次は複数地域の地形境界で移動・表示・保存を接続し、その後LOD/全国負荷と背景品質を調整。

## 一地域の6段階統合検証（2026-10-20、最新）

ユーザー「検証、全部やってください」。入口Scripts/Show-RegionalIntegration.ps1、新ruleset regional-integrated-validation-v1 / world:regional-integrated-validation-v1。schema2のRegionは新規則だけ必須、旧保存は読み替えない。通常Main・main・design/foundation・Confirmed・旧素材を保持。HTMLは必要なし。

Docs/Decisions/2026-10-20_REGIONAL_INTEGRATION_V1.mdとDocs/Design/RegionalIntegrationV1/source-manifest.jsonを参照。160m合成地形で平滑化・半径と連続衝突・発進予約・途中停止、共通の高さを経路/保存/3D/地面選択に使用。拠点帰還から既存道路へ出て往復。共有秒時計で接敵/同時損害/士気25以下の敗走、配下への物理伝令/在所/権限/服従/競合保留/物理返報。未知の結果は返報前に見せず、範囲外の位置と向きは最終目撃を保持。

Core727件（新28）全合格、実Godot15プロセス43検査70PNG。7ケース21保存点の復旧・続行42比較は3D差分0。26秒624フレーム実動画。verify_regional_integration.py → 実画像点検 → check_integration_binding.py。実測の最終件数・画素比較・動画ハッシュはverification.jsonを正とする。9/36分隊は一地域の試作検査で全国性能保証ではない。地形遮蔽・複雑な戦術AI・補給天候・攻城・全国地形制作は未実装。次は同じ高さ/地形契約で山・森・道・川・城の3D背景表現へ。その後に複数地域と全国規模の負荷へ。旧bindingは共通ソース更新で再検証まで失効。出力はruntime-local、マウス操作ツール不使用。

# 百代鏡：作業の入口

## 陸上道外移動・自由布陣（2026-10-20、最新）

入口Scripts/Show-FieldWorld.ps1、national-land-field-v1 / world:national-land-field-v1。清洲の同じ全国座標・世界時計に接続した160m合成地形。史実の地形ではない。海路は固定航路を保持。自隊100人を40/30/30の3分隊へ分け、地面ドラッグの任意線の端・中点へ移動。自隊への現地指示で、他武将の遠隔操作ではない。10mセル4方向の中継探索、実目標は丸めず任意座標。道2m/s・平地1・森0.5・斜面0.4は検証係数。水面・崖・区画外を通さない。地形速度タグで、高低差描画は未実装。

新規則のみFieldモジュール必須、旧規則は省略・存在拒否、保存を読み替えない。履歴を再計算して位置・経路・任意目標を検査し、256セルの地形/速度も定義版へ結合。既存WorldSessionの時計・原子的確定・世代保存を共用。AtPositionと同じ投影を使用。清洲の帰着点へ戻ればAtSiteで全国移動でき、戻って再び布陣できる。海路と全国往復はCore検査、今回の画面は局所布陣専用。

Core699件（新15）、実Godot3プロセス、4保存点を別プロセス復旧・続行8比較で3D差分0。森林・斜面の実際の移動時刻を検査。地面ドラッグはGodot内部GuiInputで確認（マウス操作ツール不使用）。26秒624フレーム実動画。verify_field_world.py→実画像点検→check_field_binding.py。代表モデル拡大は占有幅でなく表示用、歩行位相を補正。到着方向は経路から決定、名称は整数画面座標へ。Docs/Decisions/2026-10-20_FIELD_WORLD_V1.md参照。

全国地形・占有幅・分隊の衝突回避・接敵・敵AI・地形偵察は未実装。任意道外地点から全国道路へ自動接続せず、まず清洲帰着点を使う。次は経路の滑らかさと分隊同士の回避、その後接敵と他武将への命令配送。通常Main・main・design/foundation・Confirmed・未追跡素材保持。出力はruntime-local。共通ソース変更で旧bindingは失効し、旧GPU画像を転用しない。


## 通行止め・運休の競合（2026-10-20、最新）

入口Scripts/Show-TrafficWorld.ps1（-Case road/detour/port/sailed）。national-traffic-conflicts-v1 / world:national-traffic-conflicts-v1、schema2のTrafficStopsを新規則だけ必須、他規則で省略・存在拒否。旧保存は読み替えない。条件は合成検証日程で史実ではない。通常Main・main・design/foundation・Confirmed・既存未追跡素材を保持。

道は進入時に現地確認し、既知情報で迂回、不能ならその拠点AtSiteで中止。海は港到達〜出航時刻ちょうどまで運休を確認し中止、出航後は到着港まで航行。歩き始めた区間も後発閉鎖で巻き戻さない。同時刻は通行条件→本人判断効力→現地進入確認。再開で自動移動せず、現地確認して新たな旅として出発。乗船準備もやり直す。

配送は既存時計・RoadConditionと共有。使者も運休前後の境界を守り、失敗返報は原発送住所へ物理的に戻す。現在位置への追跡ではない。未知の閉鎖は本人計画へ漏らさず、主君は返報受領で知る。停止記録と目撃・真実・時刻・現在位置・次の出発地を保存検査。

Core684件（新17）、実Godot9プロセス40検査40PNG。4ケース12保存点の別プロセス復旧と続行24比較で3D差分0。26秒624フレーム実動画。verify_traffic_world.py→実画像点検→check_traffic_binding.py。Docs/Decisions/2026-10-20_TRAFFIC_WORLD_V1.md参照。出力はruntime-local、マウス操作なし。旧bindingは共通ソース変更で失効。自動再開・船定員・運賃・天候・港権限は未実装。次は各地の人物・勢力と任務を到着・停止・再出発へ接続する。


## 本人の道中変更・出航取りやめ（2026-10-20、最新）

入口Scripts/Show-TravelControl.ps1、national-personal-control-v1 / world:national-personal-control-v1。前のTravelWorldV1保存を読み替えない。schema2のTravelDecisionsは新規則だけ必須、旧規則では省略し存在を拒否する。行先変更は今の区間の終点で効く（海上なら次の港）。通過済み区間を保持、変更待ち中の重ね操作は拒否。港の取りやめは出航時刻より前のみ即時適用、同時・後は拒否。AtSiteでその港に留まり次の旅を始められる。路上Uターン・海上停止・遠隔主君命令ではない。

既存WorldSessionの時計・原子的確定・効果台帳・配送・行動・保存を共用。受付ID/本人/対象/新行先/受付・効力・適用時刻/効力拠点/変更前後の経路/既知封鎖/結果を保存検査。重複再適用・過去の区間改変・未知情報漏出を拒否。同時刻は通行条件→本人判断→位置・進行→既存配送と完了処理。自己宛て記録から伝令人物を二重描画しない。

Core667件（新21）、実Godot7プロセス30検査30PNG。陸路・港・海上の9保存点を別プロセス復旧し続行、18比較で3D差分0。26秒624フレーム実動画。verify_travel_control.py→実画像点検→check_travel_control_binding.py。詳細Docs/Decisions/2026-10-20_TRAVEL_CONTROL_V1.md。マウス操作なし。通常Main・main・design/foundation・Confirmed保持。元素材と出力はruntime-local、既存未追跡素材保持。旧bindingは共通ソース変更で失効。次は通行止め・運休と変更待ち／港待機の競合、その後人物勢力と任務を拡充。天候・船の定員等と全国NPC AIは未実装。


## 全国での連続移動・出航待機（2026-10-20、最新）

入口Scripts/Show-TravelWorld.ps1、TravelWorldV1。national-personal-travel-v1 / world:national-personal-travel-v1を独立追加。本人が現在地から選ぶ自己Move、清洲→堺→洲本→清洲を継続。現在Locationは最後の旅、過去の出発地・時系列・配送原点は全旅を保存検査。旧保存を読み替えない。通常Main・main・design/foundation・Confirmed保持。

港は準備300秒＋毎時3600秒境界出航の試作便。本人・伝令・探索・表示・保存で同じ時刻、待機は距離0/idle、出航後は簡易船。定員・運休・料金・天候未実装。信長/織田家/清洲、信玄/武田家/甲府、氏康/北条家/小田原の出典付き静止NPC3名。本拠候補を使った初期配置で当日の所在復元ではない。政治領土・全国AI未設定。外見は汎用試作。80m外の生位置を描画しない。

Core646件（新15）、実Godot3プロセス14検査14PNG、4段階保存の別プロセス復旧と続行8比較で3D差分0。26秒624フレーム実動画。verify_travel_world.py→実画像点検→check_travel_binding.py、入力/DLL/元資産/出力/自己レビューを結合。runtime-localはローカルのみ。マウス操作なし。Docs/Decisions/2026-10-20_TRAVEL_WORLD_V1.md参照。次は本人の道中での行先変更・港で出航取りやめ。その後運休・人物勢力任務を拡充。粗い海岸線と平面地形、仮の箱船を史実の地形/港復元と呼ばない。共通ソース変更で旧bindingは失効。


## 全国規模の地理・移動基盤（2026-10-20、最新方針）

ユーザーは厳密な測量を不要とし、概略整合で全国規模まで進めることを指示。以前の測量資料待ちを全国拡張の条件にしない。別ruleset national-inferred-routes-v1 / world:national-inferred-routes-v1で108拠点118区間（陸106／海12）を接続。蝦夷地南部・対馬・琉球を含む地理的接続で政治所有は未設定。主要拠点網であり、すべての城・全国NPCの完成ではない。既存定義や保存は読み替えない。通常Main・main・design/foundation・Confirmedは保持。

入口Scripts/Show-NationalWorld.ps1。全国→地域→3D本人は同じ世界状態。初期全国、全行先を選択可能、本人の現在地へ地域カメラが追従。海は簡易船表示、個別乗船・天候・運休は未実装。陸距離は大円×1.25、海は×1.10、速度は試作値。推定区間を測量済みと呼ばない。現代道路・建物・ダム・橋を導入しない。現在は清洲発の移動に限定。次は到着地を起点にした連続移動と道中変更、その後に港の条件・各地域の城と人物・勢力をつなぐ。

Core631件（新21）、実Godot3プロセス12検査・15PNG、保存復旧と続行8比較で3D差分0。26秒624フレームの実動画。verify_national_world.py→実画像点検→check_national_binding.py。基図・カタログ・コード・DLL・素材・出力をハッシュで結合。共通ソース更新で旧試作のbindingは再検証まで失効。Docs/Decisions/2026-10-20_NATIONAL_WORLD_V1.mdとDocs/Design/NationalWorldV1/source-manifest.json参照。画像・動画・保存・既存素材はruntime-local、マウス操作ツール不使用。

## 二村山指定図の実画像確認・採否（2026-10-20、後続）

Docs/Decisions/2026-10-20_SURVEY_ROAD_REVIEW_V1.md参照。文化庁掲載の10画像をすべて確認し、画像6に指定範囲の桃色着色を確認。図上の画素9点を記録したが、縮尺・測地系・測量端点がなく地理座標への裏付け未確保。GSI標準地図12枚と照合しても独立検証できる基準点を確定できず、変換係数は作っていない。今回置換0区間。現代道路に合わせる／旧200m仮線を実道路へ昇格させることは禁止。

SurveyRoadReviewが画素とメートル座標を分離し、年代・端点と中心線・座標照合と独立検査・水平誤差・権威出典・SHAを検査。現在候補は不採用。実データの世界取込は未実装。Core610件／新13件、既存Godot3プロセス11検査・保存復旧続行8比較で3D差分0。これは既存仮線の不変検証。入口Scripts/review_survey_roads.py（事前にverify_historical_roads.py→実画像点検）。元画像・地図タイルはruntime-local、再配布なし。次は座標・縮尺・基準点を持つ測量図／地籍図と1560年線形の根拠が必要。NDL事例1000387489に関連書籍・地籍図の案内あり、本文未確認。

## 道路の年代・距離根拠と旧道参考区間（2026-10-20、後続）

入口Scripts/Show-HistoricalRoad.ps1。owari-historical-road-preview-v1 / world:historical-road-preview-v1。14地点15区間、全区間の距離根拠と出典を固定。二村山鎌倉街道の公表約200mを徒歩200秒／伝令100秒へ接続。ただしPDFは本文抽出のみ、直接取得403/リダイレクト反復で図面・端点は未確認。代表座標に置いた200m直線は表示用仮線で、実際の線形ではない。仮線内では1秒1mと歩行距離の一致を検査済み。戦国期の実測道路長への全面置換は未完了、正式史実採用は0区間。現代道路・ダム・ビル・DEMを当時の世界へ取り込まない。全国海岸線も現代の位置参照。2017-04-01は史跡指定日で建設年／発行年ではない。

年代・線形・出典・距離を検査し、未来施設と未確認線形を史実として採用しない。新22件を含むCore597件、Godot3プロセス・8画像比較差分0、26秒624フレームの実動画。verify_historical_roads.py→実画像点検→check_historical_binding.py。旧保存を読み替えない。通常Main・main・design/foundation・Confirmedは保持。Docs/Decisions/2026-10-20_HISTORICAL_ROAD_V1.md参照。次は指定範囲／測量図と年代の一次資料で端点・曲折線を確定し、距離と3D地上速度を同時更新する。元資料・画像・動画はruntime-local、再配布しない。

## 中島橋周辺の推定経路・渡河と動画（2026-10-20、後続）

入口Scripts/Show-RiverRoad.ps1、新ruleset owari-river-roads-v1、world:river-roads-v1。12地点13区間、三河への概略リンクを橋の入口・渡河・検証用迂回へ分ける。名古屋市資料と地図PDFを画像で確認。ただし新しい地点は±250mの位置推定、距離は大円参考値であり、実道路の実長や1560年の橋の復元ではない。仮設渡しは合成した試験選択肢。詳しくはDocs/Decisions/2026-10-20_RIVER_ROAD_V1.mdとsource-manifest.json。

Core575件／新7件、Godot12検査、別プロセス復旧・続行8比較で3D差分0。26秒・624フレーム・24fps・720pの実Godot動画を出力し、デコードと主要場面を画像で確認。動画はruntime-localのみ。verify_river_roads.pyで収録、check_river_binding.pyで現行ソース／DLL／資産／動画を照合。自己レビューはユーザー採用ではない。未受領の封鎖は予定や主君へ出さない。旧保存・通常Main・main・design/foundationは変更しない。

次は地理参照できる史料・測量データへの対応づけと実長化。今回の位置・道路・川の形状を確定史実として扱わない。全国経路網、木曽川／矢作川、船便の待ち行列や運休は今回未実装。


## 物理伝令・再送・通行条件と隣接地域（2026-10-20、後続）

入口Scripts/Show-RegionalCampaign.ps1。新ruleset owari-campaign-v1、world:regional-campaign-v1、schema2／Couriers必須。旧保存へ読み替えない。通常Main、main、design/foundation、Confirmedは保持。HTML・動画の報告は必要な場合のみという最新ユーザー指定を適用する。Docs/Decisions/2026-10-20_REGIONAL_CAMPAIGN_V1.mdを参照。

伝令は既存MessageFlowのattemptへ一対一で経路・部分区間距離・出発位置・待合せ・到達を結合する。同じ世界時計で移動し、先着時に待つ。不在で受領させず、失敗報告も物理経路で戻す。差出人が報告を受け取る前の再送は拒否。原命令で知る城／待合せ／行先だけ再指定する。封鎖／解除の履歴と現地確認を保存し、古い報告は新しい観測を上書きしない。現地80m以外にライブ座標を表示しない（遮蔽物なしの試作係数）。

通常ボタンで移動、納入委任、同行、取消・帰城変更、開封、再送、現地確認、保存復旧を操作可能。奉行・書役の異なる行先を同時に表示し、Core18武将の同時移動／途中保存を検査。尾張5地点に稲葉山城・岡崎城を追加。7地点7区間の概略距離であり、1560年の実道路・橋・渡河・所有権ではない。出典・精度と未確定箇所はsource-manifest.jsonへ記録。全国の経路網・全国AI／負荷・実道路復元は未完了。

Core568件（新24）、Godot36検査・4プロセス・39枚。途中保存9段階、復旧・続行・秘匿15比較で差分0画素。verify_regional_campaign.py、check_regional_campaign_binding.pyで入力／DLL／素材／TRX／実画像と自己レビューを固定。画像・保存はruntime-localのみ。自己点検はユーザー採用ではない。既存入口のbindingは共通ソース更新で失効し、古い画像を新入力へ転用しない。

物理回収経路のない「紛失原本を指定秒後に遅着させる」注入は新規則では拒否。旧規則の遅着／重複検証は維持。回収原本の物理再配送は未実装。次は道路と渡河の史料照合・実長化、回収原本の再配送、その後に全国経路と規模検証へ進む。


## 尾張での道中取消・帰城変更（2026-10-20、後続）

入口Scripts/Show-RegionalControl.ps1。ユーザー指示により今回の検証HTMLは作成しない。Docs/Decisions/2026-10-20_REGIONAL_CONTROL_V1.mdとDocs/Design/RegionalControlV1/verification.jsonを参照。新ruleset owari-movement-control-v1、schema2。既存地域の5拠点・概略線・出典・CC BYクレジットを維持し、旧保存を新規則へ読み替えない。通常Mainは保持。

熱田の既知待合せを使用。使者の最低所要5828秒と予定到達11656秒を使い、早い使者は待合せまで待つ。宛先の生位置で配送所要を算出しない。実到達時の本人位置照合だけが受領を許可する。通り過ぎていれば受領せず、失敗報告も遅延配送。動く相手の任意追跡・使者自身の経路と3D・再指定は未実装。

受領・未開封・開封・効力を分離。例：5秒発送、11656秒受領・重要書状で停止、11658秒開封、11660秒効力。取消はその位置で停止。帰城変更は現在の熱田→笠寺区間を完歩して15590秒の笠寺から戻り、31179秒清洲着。到着済みの未開封命令は完了を巻き戻さない。再送・遅着原本と同時取消優先を検証。

Core544件（新19）、実Godot40検査・4プロセス・26枚。両命令で配送中／未開封／効力待ち／適用後の4保存を別プロセス復旧し、続行を含む16比較で3D差分0画素。経路・効果台帳・配送・表示位相の継続一致も単体検査。verify_regional_control.py→実画像自己確認→check_regional_control_binding.pyで現行入力・DLL・資産・画像を照合。画像はruntime-localのみ。自己確認はユーザー採用ではない。

次は伝令自身の移動・待合せ失敗後の再送／再指定をこの地域へ接続し、宛先の現在地を漏らさず到達・失敗・返報・保存復旧が一致することを検証する。検証HTMLはユーザーから再度求められるまで必須にしない。

## 尾張の分岐・未知封鎖・迂回（2026-10-20、後続）

入口Scripts/Show-RegionalBranch.ps1、Docs/Design/RegionalBranchV1/report.html、Docs/Decisions/2026-10-20_REGIONAL_BRANCH_V1.md。新ruleset owari-geographic-branches-v1、world schema2／Branches保存を共用。Core/Data/owari-branches-v1.jsonは清洲・熱田・善照寺・笠寺観音・丹下の5代表点5概略区間。実道路・橋の線形や封鎖の史実ではない。距離は大円下限参考、速度は検証係数。

BranchRoutesにシナリオ別Nodes／Edges／版を渡し、既存の本人証拠による経路探索・現地目撃・配送・任務・世代保存を再利用。熱田で初めて封鎖を目撃し、笠寺・丹下経由へ変更。主君は報告受領まで知らない。行先を失う場合もその到達位置を確定して停止。旧MovementControlのLocation.TerrainRevisionはBranchRoutes.Versionのまま保持し、旧保存を新地域へ読み替えない。

丹下の座標は『日本歴史地名大系』施設・地点項目データセット（CODH作成）doi:10.20676/00000456、CC BY 4.0。ゲーム画面・HTML・manifestに帰属と変換内容を記載。元精度未提示。その他は改訂付きWikidata CC0。Scripts/build_regional_branches.pyで取得スナップショットから再生成。同期資料・原本は変更しない。

Core525件（新19）、実Godot25検査・4プロセス・19枚。目撃前の封鎖あり／なしで本人・主君の全画面2比較、目撃前／迂回途中の同一・別プロセス復旧と続行6比較、合計8比較で差分0画素。全出口封鎖時の現地停止も実描画。入力・DLL・資産・画像と自己レビューはcheck_regional_branch_binding.pyで照合。画像はruntime-localのみ、自己確認はユーザー採用ではない。

次はこの地域で道中の取消・行先変更を配送し、受領・開封・効力を分離して遅着競合と保存復旧を検証する。動的な解除・自由な目的地・道路橋復元・全国網・多数部隊は未実施。通常Mainは保持。

## 尾張の実在拠点と移動・保存復旧（2026-10-20、後続）

入口Scripts/Show-RegionalRoute.ps1、Docs/Design/RegionalRouteV1/report.html、Docs/Decisions/2026-10-20_REGIONAL_ROUTE_V1.md。新ruleset owari-geographic-corridor-v1／世界schema1。清洲城→熱田神宮→善照寺砦の通過順を愛知県資料で確認。Core/Data/owari-route-v1.jsonを埋込正本に、現代代表座標・改訂・精度・大円距離・速度・投影名を版固定。旧架空シナリオや保存を読み替えない。

道路形状は拠点間の概略線で史実復元ではない。徒歩1m/s・使者2m/sは検証係数。18,337.692mは大円距離の下限値で道路実長ではない。徒歩18,339秒、受領1秒で18,340秒到着、返報9,170秒で27,510秒受領。登場人物・権限・委任内容は検証用。近景の地形高／川／橋／城建物、分岐・迂回・道中の命令変更はこのrulesetでは未実施。通常Mainは保持。

既存RouteContractを登録シナリオ別の形状へ拡張し、既存MessageFlow・時計・効果台帳・世代保存を共用。描画は本人向け位置／知識＋公開経路のみ。他人のライブ位置を描画しない。原点差引き後、長い経路を倍精度で可視範囲へ切って描画する。近景では経路が人物を覆わない深度判定。

Core506件（新18）、実Godot26検査・2プロセス・27枚。前半／熱田通過／後半／到着の4保存を同一と別プロセスで復旧し、別プロセスから返報受領まで続行。固定12m視点で12比較すべて3D差分0画素。保存小数秒位相と速度は復旧するがカメラ設定自体の保存は対象外。verify_regional_route.py→実画像自己確認→check_regional_binding.pyで入力・DLL・資産・画像を照合。変更で過去のbindingを失効させ、自己確認をユーザー採用にしない。次は史料に沿った分岐・橋・通行条件と未知封鎖の迂回をこの地域へつなぐ。

## 全国地図の表示基盤（2026-10-20、後続）

入口Scripts/Show-JapanMap.ps1、Docs/Design/JapanMapV1/report.html、Docs/Decisions/2026-10-20_JAPAN_MAP_V1.md。Natural Earth固定コミットの109ポリゴンとWikidataの30地点候補を同じ3Dワールドへ配置。緯度経度E7→表示用固定mm→描画前の原点差引き。全国・地域120km・周辺4km・現地80m・比較12m、回転と傾斜。人物・所属・旧架空経路を実地理へ自動移行しない。

海岸線は現代の概略、地点は現代代表点で1560年の城域・存在・所属未確認。城名も表示候補。出典・精度・改訂・SHAはカタログとsource-manifestに保持。地理投影を経路距離・所要時間に流用しない。比較用武将は世界上の人物配置ではない。地形高・道・建物・全国AI・全国保存復旧は未実装／未検証。

Core488件（新13）、実Godot51検査・12枚。全海岸線頂点が全国画面内、30地点の再原点化、25中間フレーム、同一ワールド／ノード／時計／乱数／任務／知識、全国に戻す比較の画素差分0。画像はruntime-localのみ。verify_japan_map.py→実画像自己確認→check_japan_binding.py。自己確認はユーザー採用ではない。入力変更で失効。通常Mainは変更なし。次は一地域の史料・実在拠点・経路を版固定して新Coreへ接続し、移動・委任・返報・保存復旧を同じ地理表示で検証する。

## 移動中の制御と共有した全国像（2026-10-20、後続）

製品目標は日本全国の多数の城・集落・街道。同一座標／世界時計で全国→地域→現地を連続表示する。局所の架空グラフは検証規模であり製品規模ではない。ユーザーは急ぎではなく、この像の共有を確認し、推奨順の続行を承認した。次は全国概略地図と主要城・拠点の配置へ進む。

今回の入口Scripts/Show-MovementControl.ps1、Docs/Design/MovementControlV1/report.html、Docs/Decisions/2026-10-20_MOVEMENT_CONTROL_V1.md。MovementControlの新ルールを登録し、既知の分岐点での受渡し、受領／開封／効力の分離、取消時の現在位置保持、次の分岐からの帰城変更を実装。World schema2／既存配送・時計・効果台帳・保存を共用。任意地点への追跡・任意目的地・使者3Dは未実施。

Core475件、新18件、実Godot58検査・4プロセス・29枚。発送中／未開封／効力待ち／適用後の各保存を両命令で別プロセス復旧し、16比較で3D差分0。合法な遅着原本＋正規再送の重複到達、同時取消優先もCore検査。Scripts/verify_movement_control.pyとcheck_movement_binding.pyで現行入力・画像・DLL・素材・レビューを照合する。入力変更で過去のbindingを転用しない。画像はローカル限定。

## 分岐経路・未知封鎖・迂回（2026-10-20、後続）

入口Scripts/Show-BranchWorld.ps1、Docs/Design/BranchWorldV1/report.html、Docs/Decisions/2026-10-20_BRANCH_WORLD_V1.md。BranchRoutesの5地点5区間を秒基準の同じWorldSession／MessageFlowへ接続。探索は本人のEvidence＋Knowledgeのみを参照し、真実のBlockedEdgesを予定経路・予定時刻・描画へ漏らさない。区間入口で目撃して迂回し、歩いた区間は保存。主君は遅延報告で知る。

世界Schema 2／保存Worldモジュール2はBranches必須。旧Schema 1は拡張を省略して従来形式を維持し、旧保存を自動変換しない。一本道／通常Mainは履歴として保持。変更入力のSHA、元アセット、DLL、画像レビューはScripts/check_branch_binding.pyで照合する。共通ソース更新により旧入口のbindingは失効し、過去の画像確認を新入力へ転用しない。

全Core457件、新29件、Godot24検査・4プロセス・GPU27枚。未知封鎖の有無による本人／主君の全画素比較2件、目撃前／迂回途中復旧と続行の3D比較6件で差分0。検査はScripts/verify_branch_world.py、実画像を確認してレビューを更新。3D画像はruntime-local限定でGitに入れない。静的封鎖の小規模既知地図であり、動的な解除・通行権・全国・追跡配送は未実施。次は移動中の命令変更・取消と安全な停止。

## 新Coreと3D経路の接続（2026-10-20、後続）

入口Scripts/Show-RouteWorld.ps1、Docs/Design/RouteWorldV1/report.html、Docs/Decisions/2026-10-20_ROUTE_WORLD_V1.md。RouteContractがmm・整数秒・区間別速度を固定し、依存ハッシュをDefinitionPack.Versionに保存する。新ルールcommon-route-fixture-v1を旧4秒移動と混同しない。新Core→本人用RouteActorView→既存3D骨格へ接続。通常Main・旧保存は保持。

架空の平道40m＋峠道80m、徒歩100秒／使者50秒、委任4秒は検証係数。分岐探索・全国実地理・移動中宛先への追跡配送・通常Main移行は未実施。移動中受令者への新規入力は拒否。主君に配下のライブ位置を出さず、返報まで結果未確認とする。保存の小数秒表示位相と復旧時停止を維持。Core428件、Godot12＋別プロセス3検査、GPU20枚、復旧3比較で3D領域差分0画素。画像はローカル限定。

入力・依存変更時はScripts/verify_route_world.pyを再実行し、実画像確認とverification.jsonのレビューを更新する。Scripts/check_route_binding.pyはコード・実行DLL・元アセット・画像の変更で失効する。旧CommonContractV1の412件報告は当時の履歴で、旧入口の入力ゲートを新コードへ無断適合させない。次は分岐・既知通行条件・迂回を同じ経路契約へ追加する。

## 共通Coreの実装（2026-10-20）

G1の4項目を実装。Core/ContractsのWorldSessionはGameSecondsV1の独立検証系、旧TaxSimulationと通常Mainは保持。CC-01〜24のうち共通の検証用部分を接続し、全制度完成とは扱わない。Docs/Decisions/2026-10-20_COMMON_CORE_IMPLEMENTATION.mdとDocs/Design/CommonContractV1/implementation-report.htmlを参照。全Core412件（新95）、別プロセス/途中終了、独立Godot画面9検査・GPU4枚を確認。新形式はcommon-core-fixture-v1だけに対応。実地理・製品係数・全48受入・補償実行・本格NPC・通常3D接続は未完了。

独立画面の入口Scripts/Show-CommonContract.ps1。変更時はCore試験、Scripts/verify_common_contract_process.py、Scripts/verify_common_contract_gpu.py、報告生成/文書検査を関連範囲で再実施し、コード/画像/DLLのSHA対応を更新する。新制度へ別配送器・別時計を作らず、旧Tickの無断移行・未知情報の画面/NPC参照を禁止。

ユーザー希望：毎回、最終報告の末尾に「次にやる作業」と「その完了条件」を簡潔に添える。

## 共通契約の詳細設計V1（2026-10-19）

ユーザーが推奨選定を委任し、Docs/Design/CommonContractV1/common-contract-v1.jsonのCC-01〜24を採用。HTML/Markdown/受入予定48件はScripts/build_common_contract.pyで生成。決定はDocs/Decisions/2026-10-19_COMMON_CONTRACT_V1.md。これは共通契約の設計採用で、新WorldSession・game-world-save/1等は未実装。旧試作Version1〜5と旧入口を保持し、製品秒への自動移行は現段階で非対応。旧Tickを秒へ読み替えない。ID・権限・所在・知識・効果台帳・隔離Load・世代保存の規則を各制度で共用する。実装前に照合元SHA、変更点、予定試験を確認し、24/24採用と全制度詳細完成を混同しない。

## 現行方針の優先（2026-10-19・ユーザー明示確認）

ユーザーが「現行方針が正です」と確認。旧Confirmedやdesign/foundationと後続採用方針が食い違う範囲では、基本設計V1・PW01〜07・3D移行等の現行方針を適用する。旧原本の名称やConfirmed分類だけを理由に現行決定を差し戻さない。食い違いのない既存原則は引き続き維持。現行仕様の採用と実装・検証の完了は区別する。記録はDocs/Decisions/2026-10-19_CURRENT_POLICY_AUTHORITY.md。

## 基本設計V1の採用と今回のmainマージ許可（2026-10-19）

ユーザーが「全部推奨」「基本設計を完了」「commit/pushとmainへのマージ」「HTMLで」と明示指示。今回の基本設計と既存prototype/godot-foundation成果のmainマージは許可済みで、下記の通常のmain変更禁止に対するこの作業限りの例外。design/foundation・既存Docs/Confirmed・同期sources・外部プロジェクト原本は編集しない。

現行正本はDocs/Design/basic-design-v1.jsonとbasic-design-rules.json。HTML/MarkdownはScripts/build_basic_design.pyから生成。15分野60判断を委任採用し、決定記録はDocs/Decisions/2026-10-19_BASIC_DESIGN_V1.md。旧未確定表記を理由に同じ基本判断を再度未選択に戻さない。既存ConfirmedとPW01〜07を維持し、詳細の後続DD-01〜08、受入予定170件（未実行）、既存Core317件等の試作証拠を区別する。

初回製品は全国と武将個人の人生、1560年開始一シナリオ。制作順のみ一地域→全国。旧2D復帰、別戦闘空間・別時計、全知AI、所属・領地・債務一括移転、瞬間配置を禁止。性能目標・新制度・全国地理・自由カメラ等を実装済みにしない。将来の基本方針変更は理由・影響・版・再検査を記録する。

## 平時・戦時の操作骨格（2026-10-19、7項目B採用）

Docs/Proposals/PLAYER_PEACE_WAR_FLOW.mdとDocs/Decisions/2026-10-19_PLAYER_PEACE_WAR_FLOW.mdを現行の操作設計として読む。ユーザーはPW01〜07のBをすべて採用。任務と自分の目的、所在、拘束・競合による併用、目的・制約・報告条件での委任、本隊地点指定＋配下命令、既知任務との逸脱表示、自動進行＋任意停止＋重要時自動停止を前提とする。

旧63項目の「自動進行は採否未確定」に戻さない。数値・閾値・例外は未確定で、今回の採用を実装済みや63項目全詳細の確定と扱わない。入力の自由は権限の取得・物理的即応・配下の服従・免責を意味しない。重要書状の未開封本文、代理受領と本人認識、未知任務変更・偽書真偽の秘匿を維持する。カメラ方針は斜め見下ろし＋回転・ズーム・一定範囲の傾斜変更を採用済み、現実装は固定角＋ズームまで。

## 現行の作戦線試作（2026-10-19）

通常MainはFormationWorldStage、入口Scripts/Show-FormationWorld.ps1。Docs/Proposals/FORMATION_WORLD_V1.mdとArt/Officer3D/FormationWorldV1の契約・検証結果を読む。3部隊の一本の布陣線を個別軍令へ分解し、既存MessageFlowで配送。線を描いただけで移動せず、受領・権限・服従判断後に履行する。履行開始返書と配置完了報告を区別する。

全国概略と近景は同じGodotワールド・座標・時計。架空の概略地形と11×7の実移動地域であり、日本全国の地理・経路探索・多数部隊交戦の完成を主張しない。描画はMilitaryViewForと発送者限定FormationPlansFor。未知位置のズーム開示を禁止。自己点検と採用を分ける。

旧2部隊地図・旧接続・V3/V2は明示入口で保持。試作は一つの不変計画／初期化で、線差替え・独断の責任UI・全国AIは未実装。Core317件、新GPU28枚・別プロセス復旧、旧25/30/283/187枚を検査。専用user://formation-world-test*以外を試験で書かない。

- main / design/foundation / Docs/Confirmed は変更しない。作業ブランチはprototype/godot-foundation。既存ファイルを無断削除しない。
- マウス操作・ネイティブ入力・カーソル設定変更は禁止。CLI、バックグラウンド検査、Godot内部試験を使う。
- sources/等の同期資料と外部参照は読み取り専用。

## 現在の武将制作方針：3D

2026-10-19のユーザー指示で3Dへ切替。最初にDocs/Proposals/OFFICER_3D_TRANSITION.mdとArt/Officer3D/production-status.jsonを読む。ゲーム内3Dを第一案とし、2D画像への焼き出しへ勝手に戻さない。原本.blend／動画は変更しない。

同じ人物の形状・骨格・色・装備・外見設定は全周で共有する。顔と体・耳・手の肌色は共通設定、髪・目・眉・口・装備を保護する。Blender内コントローラーがGodotでも動くと推定しない。新素材を増やす前に、実Godotで出力往復／肌色／着脱／動作が成立する検査を実装し、実画面で確認する。未実装のゲートを完成扱いにしない。

単位・原点・前方向・基準姿勢・骨名・取付・部品／素材ID・アニメーションクリップを固定し、変更入力とコードと出力のSHAを管理する。変更時は関連レビューを失効させる。技術一致、意味的対応、外見、実行表示、ユーザー承認を混同しない。

## 旧2D方式は凍結

Art/Officer、Officer系2D素材・シーン・スクリプト・旧Proposalsは復元用の履歴。通常の新規制作の根拠／現行計画として使わず、自動再開しない。64×96・1倍・方向別原画・6頭身／4頭身の別素材規格は旧2Dの条件で、3Dへ自動適用しない。

旧AGENTSはDocs/Archive/Officer2D/AGENTS_BEFORE_3D_2026-10-19.md、検証済みZIPとGit bundleの場所はArt/Officer3D/archive-verification.json。旧原本は削除していない。旧2Dを再開する場合はユーザーの明示指示が必要。

共通Core・世界時間・命令／配送／保存復旧の基盤は保持し、表示を変更してもゲーム状態の権限や決定性を変えない。

立ち絵はユーザー選択により2Dイラスト。ゲーム内3Dモデルの接写で代替しない。2026-10-19にユーザーがDocs/Proposals/OFFICER_3D_OPTIONS.mdの全推奨Aを採用。実装済みとは区別する。旧2Dスプライト工程の再開ではない。

3D試作の入口はScripts/officer3d_pipeline.py。TrialV1の契約・元.blend・素材所属・コードSHAが変われば新しい計画と再検査が必要。モデル／テクスチャは権利情報整理までローカルのみで、Gitへ自動追加しない。

3D部品は、結合前に各部品の非Armature加工をRESTの評価済み形状へ固定する。結合後に先頭部品のSubdivision/Solidify/Bevelを全体へ適用しない。位置・UV・重み・トポロジー一致が必須。
顔のSKIN材質内にも口線がある。材質名だけで保護完了にしない。元テクスチャSHA、UV定義、口線色成分と肌下地の分離、実GPUのRGB数値検査を保持する。肌の白飛びと保護範囲外の画素変化を検査する。
確認入口はScripts/Show-Officer3DTrial.ps1。GPU結果と自己画像確認のbindingが現行計画と一致しなければ表示しない。自己検査をユーザー承認に置換しない。近・中・遠LODと新規顔髪・立ち絵素材は未完成。

## 第2試作（2026-10-19）

入口はScripts/Show-Officer3DV2.ps1、契約と計画はArt/Officer3D/TrialV2、実測と限界はDocs/Proposals/OFFICER_3D_TRIAL_V2.md。V1は比較用に保持。近・中・遠、材質ID由来の布／漆／金属マスク、共通肌、顔／髪／髭の交換試作を同じ骨格で扱う。Blenderの再サンプリングで動作の長さを変えない。元V1の動作samplerをボーン名で移し、REST変換・長さ・姿勢を実Godotで照合する。

通常Mainの3D武将・配送タブは既存TaxPanelの同じセッションを使用する。使者位置は配送から導く表示で、一般の進軍・地理・合戦のCore仕様を実装済みと扱わない。停止・時刻・乱数・命令・保存の所有者を3D表示へ移さない。試験の保存先は専用user://officer3d-v2-test-*。元資産と出力は利用条件整理までローカルのみ。

入力・コード変更時はV2のGPU結果と自己画像確認を失効。原本187枚と派生比較画像を区別し、自己確認をユーザー承認へ読み替えない。遠LODの細部欠け、簡易髪／髭、全組合せ未検査、近50体の60fps未達を報告から消さない。

内応工作はユーザーが3Dの左手前自分／右奥相手の対面構図を評価した。これは立ち絵一般の廃止ではなく、内応工作の方向性の記録。会話動作・交渉デモの制作は保留し、3D武将基盤を優先する。

## 第3試作（2026-10-19）

武将作成・別具足の入口はScripts/Show-Officer3DV3.ps1。Docs/Proposals/OFFICER_3D_TRIAL_V3.mdとOFFICER_3D_PARTS_GUIDE.md、TrialV3の契約・計画・結果を読む。Dドライブの甲冑資料は現在D:/開発データ/資料/プロジェクト別/百代鏡/戦国武将_甲冑。確認した写真11枚のSHAを契約に保持し、写真を出力テクスチャとして流用しない。

頭皮・顔・髪・髭の接触形状は全LODで共有し、別々の簡略化による頭皮露出を再発させない。部品・材質カテゴリとパレットのHEX→線形変換を固定し、同一環境の再生成バイト一致、実GPU色領域外変化0、着脱・動作・保存復旧・混成表示を検査する。原本283枚と比較表/GIFの派生を区別する。

V3は外見用の独立武将作成画面。Main/Coreの武将への外見割当、能力・所属を持つ武将追加、通常保存への統合は未実装。全組合せ・全時刻の無貫通や完成アート品質、50体60fpsを達成扱いにしない。V1/V2を上書きせず、権利情報未整理の原素材・GLB・描画画像はローカルに保つ。

## 通常世界との統合（2026-10-19、第3試作の後続）

現行入口はScripts/Show-OfficerWorld.ps1、Docs/Proposals/OFFICER_WORLD_INTEGRATION_V1.md、Art/Officer3D/WorldIntegrationV1。ユーザーは62c3875時点の見た目を一旦採用。今回の統合画面の採用は別途確認。

通常Mainの外見割当・部隊はTaxPanelと同じTaxSimulationを使用。通常v5保存のOfficers(schema1)に外見schema3・部隊・軍令・損害・表示位相を保持する。拡張なしの旧保存には既定外見を補い、部分欠落や未知IDは全体拒否。Core/Appearance/OfficerAppearance.csを外見検証・部品IDの単一正本とし、Godot側へ別の検証定義を複製しない。

世界時計の窓口はTaxPanelのみ。旧V2表示にも独立の進行時計を戻さない。Coreが確定した移動と損害を表示へ写し、描画やアニメ終了通知から判定を発生させない。保存時に途中位相を保持し、停止・倍速・別プロセス復旧を実画像で検査する。

field-fixture-v1の赤青2部隊は明示した検証入力。軍事数値・全国の政治的敵対・地理・NPC判断を正式確定しない。権限の根拠・対象・管轄・期間、到達、服従意思、効力を分離。敗走・退場を武将死亡や所属変更へ直結しない。通常Core・GPU原本30枚・V3作成・V2比較を回帰検査し、既存の検証履歴と現在のbindingを混同しない。試験ファイルは専用user://officer-world-*に分離。

## 部隊・軍令・認識・地図（2026-10-19、上記の後続）

現行通常MainはMilitaryGridStage。入口Scripts/Show-MilitaryGrid.ps1、Docs/Proposals/MILITARY_GRID_INTEGRATION_V1.md、Art/Officer3D/MilitaryGridV1。旧接続はShow-OfficerWorld.ps1の明示フラグで維持。旧FieldBattleを新Militaryへ自動変換・削除せず、同時起動を拒否する。

軍勢の組織・指揮官、部隊の軍勢・現地指揮官、行為者の指揮権、演習の対抗関係を分離する。軍勢IDの違いだけで開戦せず、指揮官への任命だけで権限や政治的所属を変えない。数値・視界・同時2部隊のルールは検証用で、正式軍制・軍事AI・全国地図の完成と扱わない。

軍令・結果返書・戦場報告は既存MessageFlowを使用し、別配送シミュレータを増やさない。軍令の期限は着手期限、取消は到達・認識・指定効力時点を満たす。紛失未認識の自動再送、原本／再送の重複履行、過去の損害の取消巻戻しを禁止する。

描画はMilitaryViewFor(observer)とAppearanceForを使い、MilitaryDiagnosticやOfficerStateの真実を読まない。未知の相手は行・モデルを出さず、古い観測は日付付き記憶として保持。遅延報告へ現在値を混ぜず、観測日と受領日を区別する。不在確認も当人の目撃・報告受領で更新する。

地図ID・地形コスト・見通し・経路はCore側のMilitaryMapで固定。隠れた相手の位置を読む経路探索をしない。経路・進捗・知識・共通配送を通常保存し、Core302件と新GPU25枚・旧30枚・V3の283枚・V2の187枚を回帰検査する。試験は専用user://military-grid-test*のみ。自己点検とユーザー採用を混同しない。
