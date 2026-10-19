# 百代鏡：作業の入口

- main / design/foundation / Docs/Confirmed は変更しない。作業ブランチは prototype/godot-foundation。既存ファイルを無断削除しない。
- マウス操作・ネイティブ入力・カーソル設定変更は禁止。CLI、ヘッドレス検査、Godot内部の信号試験を用いる。

## 武将画像・素体・パーツに触る前

1. `Docs/Proposals/OFFICER_ART_PRODUCTION_AUDIT.md`、`Art/Officer/production-contract.json`、`Art/Officer/work-record.json` と、監査台帳にある該当工程の実資料を読む。要約・過去の「確認済み」だけで代用しない。
2. 固定条件は64×96・通常1倍、6頭身立ち絵と共通選択ID、顔・髪・衣服を焼き込まない肌色素体、性器の細部なし。これを生成の都合で変更しない。頭身・身体寸法・方向数・支点は未測定で、旧試作の仮値を承認値に昇格させない。
3. 生成、局所編集、マスク作成、寸法変換、再パック、動作出力の前に、既存の作業記録へ変更計画を保存し、`Scripts/officer_art_pipeline.py` の `require_plan` を実行する。直接API/GUIを使う場合も同じ条件。新しい制作入口は書込前にこのゲートを呼ぶ。
4. 計画には指摘・過去失敗・変更範囲・維持事項・累積決定の全ID、実入力/参照/コード/実マスクのSHA、固定等方登録、独立した身体基準点、全体回帰方法を要求する。条件不足なら画像を出さず記録を整える。ゲートを迂回する別スクリプトを作らない。
5. ユーザーはComfyUIを必須としていない。案件ごとに適切なツールを選び、今回の実行バックエンドを制作契約・計画に記録する。ComfyUIを使う場合はモデル実体SHA・workflow・seed/steps/CFG/sampler/scheduler/denoise・編集/保持マスクを固定する。ツール変更で寸法・マスク・保持範囲・実画像検査を省略しない。黙って別モデルへ代替せず、選択理由と実設定を記録する。
6. 所属ID、素材色、遮蔽、制御点/重み、接点重ね代、未観測面は別責務。色閾値だけで意味的な所属を認定しない。顔・髪の境界、単体、除去後、移動試験、接点を原画と原寸で見る。
7. 技術一致・意味・見た目・実行表示・ユーザー受入を分ける。再合成一致、連結数、同一セル、プロンプトの指定だけで完成にしない。入力/マスク/コード/条件/出力変更時は関連する旧合格を失効させる。比較対象を局所修正前後だけへ減らさない。
8. 現在の OfficerPartsV1 と body-v2 は不採用。原本は保持するが、再出力・正式登録・別の基準素材としての自動復活は禁止。正しい裸素体の現状は `Art/Officer/work-record.json` と `Docs/Proposals/OFFICER_BODY_SOURCE_V2.md` を参照する。正面候補の作者検査をユーザー受入や全方向/歩行の完成へ広げない。既存の64×96選択まで未決定に戻さない。

異世界冒険譚・戦国興謀録・購入素材・Dドライブ資料は読み取り専用で参照する。共有できる検査実装は `Scripts/art_shared/` の出典付き固定コピーを使い、他作品の数値・外見・承認を流用しない。書籍図版の未確認を隠さない。

## 原武将への対応（画素数だけでは通さない）

`Art/Officer/appearance-reference.json` を独立した外見参照とし、元絵を任意の参考画像へ格下げしない。制御画像作成と生成の両入口で `require_appearance_reference` を必須にする。BodyBaseV1の汎用4頭身は外見不採用であり、新しい身体寸法の根拠にしない。元絵の共通登録量、観測できる表面、遮蔽物と隠れた人体の作画範囲、各役割のマスク、原座標での重ね合わせ/比率/姿勢/描画密度の実画像検査を対応づける。参照台帳は現在のV2正面について実画像点検済み。変更時は関連点検を失効させ、statusの書換だけで解除しない。裸素体の完成を今回の仕組み修正と混同しない。

現在の正面素体は OfficerBodyBaseV2。原画対応の実画像検査は完了、ユーザー受入は未認定。初期の原画注記/診断画像だけは `require_reference_preparation` で入力/登録量/注記定義/コードを固定して作成し、実画像を点検してから通常の `require_plan` へ進む。この限定入口は生成・完成素体出力を許可しない。V1や不採用パーツを復活させない。

ユーザーはV2を「ひょろひょろで気持ち悪い」と評価しつつ、一旦の続行を許可した。体型・画風の正式採用へ読み替えない。部位/配色の仮試験は `Docs/Proposals/officer-body-layer-review.html`。素体の外見改善は保留課題として保持する。

以前のV2の正面顔/髪/甲冑一式は `Art/Officer/EquipmentPartsV2/part-manifest.json` と `Docs/Proposals/OFFICER_EQUIPMENT_PARTS_V2.md`。元絵との可視画素一致、全解除の素体一致、髪なしの顔保護を実画像で検査済み。髪本体owner2と落ち影owner4、顔owner1、装備owner3、人体遮蔽は別責務。このV2では甲冑は一式で、顔は髭を含む。V2だけを根拠に部位別甲冑/別髪型/別方向/歩行/6頭身対応の完成を推認しない。V2素体の外見課題と仮受入を維持する。

甲冑8部位は `Art/Officer/ArmorSplitV3/part-manifest.json` と `Docs/Proposals/OFFICER_ARMOR_SPLIT_V3.md`。顔/髪/素体は旧V2の同じ実体を使う。可視所属・限定補完・素体遮蔽・素手の前景描画を分け、元の2215装備画素を削除しない。各単体と除去後、全1024状態、全装備の元絵一致と全解除の素体一致を検査済み。範囲は元の正面静止で、方向/関節移動/歩行のゲートを解除しない。

正面の待機/歩行の制御試作は `Art/Officer/FrontMotionV4/manifest.json` と `Docs/Proposals/OFFICER_FRONT_MOTION_V4.md`。8部位原本は不変、共通表面の変形/個別遮蔽/素手前景/剛体刀を分離。12インデックス×1024状態と通常/低速のブラウザー実再生を検査済み。小振幅の正面足踏みだけで、自然な完成歩容・他方向・大きな可動・Godot移動・6頭身対応の承認ではない。新しいrig/マスク/変形コードへ旧検査を流用しない。

Godot表示の現在の入口はMainの「武将表示」タブ（`--officer-motion-review`）と `Docs/Proposals/OFFICER_GODOT_MOTION_V5.md`。V4の20独立表示/遮蔽シートを改変せず読む。実ゲーム12,288合成/132GPU状態と通常低速/動的着脱を検査済み。`Scripts/Validate-OfficerGodotMotion.ps1` が既存制作ゲート→ビルド→インポート→実表示検査を行う。1倍保持のためcanvas stretchはdisabled。表示時計はCore世界時刻を進めない。正面足踏み・仮体型の範囲を、他方向/完成歩容/6頭身/配布エクスポートの承認へ広げない。

現在の正面歩行は `Docs/Proposals/OFFICER_FRONT_WALK_V6.md` と `Art/Officer/FrontWalkV6/`。原本/V4は保持、V6シートを別出力。腕上下4px/足上げ5.5px/膝軌道1.5pxを着脱で検査し、Godotの右端に前回版を同期表示する。現行の検査は `Scripts/Validate-OfficerFrontWalk.ps1`、既存 `Validate-OfficerGodotMotion.ps1` もここへ委譲する。最新計画はwork-recordのfront_walk_plan/godot_motion_plan。旧V5のコードSHAに結び付いた合格を現在へ流用しない。範囲は正面の改善試作で、他方向・実移動・体型の最終採用へ広げない。

右向きの静止試作V7は `Docs/Proposals/OFFICER_RIGHT_VIEW_V7.md`、計画はwork-recordのright_view_plan。正面の承認を横の承認へ流用しない。64×96の固定マスク、原本、所有/素材候補/隠れ面/遮蔽/関節制御を保持する。限定肘±1px確認は横歩行ではない。隠れ面の単色補完と暫定体型は未完成事項。右向きGodot組込みは未実施。

右向き待機/歩行V8は `Docs/Proposals/OFFICER_RIGHT_MOTION_V8.md`。静止V7/正面V6は保持。work-recordのright_motion_planと独立シーンOfficerRightMotionTrialを使う。近遠の実可視寄与とLマスクalphaを回帰へ固定し、画素一致だけで描画欠落を合格にしない。遠側は同寸素材の限定補完、仮体型/未観測面の完成扱いをしない。Show-OfficerRightMotion.ps1で起動、Validate-OfficerRightMotion.ps1で実GPU確認。

背面V9/V10は `Docs/Proposals/OFFICER_BACK_VIEW_MOTION_V9_V10.md`。既存V2形状の登録と背面用肌面、新規の背面甲冑を使用。裸素体の画像生成拒否後は再試行/モデル代替をせず既存形状を再利用した。back_view_plan/back_motion_planと独立OfficerBackMotionTrialを使う。顔slotは背面では空、本人左が画面左で刀を佩用。耳を髪へ所有させず接続/連結を検査。仮体型/補完面を最終承認にしない。Show/Validate-OfficerBackMotion.ps1が入口。

左向きV11/V12は `Docs/Proposals/OFFICER_LEFT_VIEW_MOTION_V11_V12.md`。本人右が近側、左が遠側。新規左原画と既存横素体の反射登録を区別し、本人左腰の刀は全素体部位より後ろ。left_view_plan/left_motion_planと独立OfficerLeftMotionTrialを使う。顎の登録差・仮体型・遠側同寸補完を完成扱いにしない。Show/Validate-OfficerLeftMotion.ps1が入口。

4方向移動V13は `Docs/Proposals/OFFICER_MOVEMENT_V13.md`。既存4シートは不変、movement_planと独立OfficerMovementTrialを使う。共通足元(32,92)、通常1倍、共通装備選択。移動速度25px/sは仮値、足の厳密固定・地形衝突・Core世界時刻は未接続。Show/Validate-OfficerMovement.ps1が入口。
