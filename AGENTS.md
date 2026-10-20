# 百代鏡：作業の入口

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
