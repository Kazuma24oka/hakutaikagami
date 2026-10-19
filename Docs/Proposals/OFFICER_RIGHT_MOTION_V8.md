# 右向き待機・歩行 V8

64×96・通常1倍の右向きに、待機4インデックスと歩行8コマ、顔・髪・甲冑8部位の独立着脱を追加した。ブラウザー確認は `officer-right-motion-review.html`。Godotは独立シーン `Game/Scenes/OfficerRightMotionTrial.tscn` とし、既存Mainの正面V6と静止V7を変更しない。Godot画面内のV7/正面比較は静止原本。

## 動かし方

`Scripts/Show-OfficerRightMotion.ps1` はパッケージのSHAゲートを確認して実Godotウィンドウを開く。待機/歩行、通常/1/4速、停止/コマ送り、全部戻す/裸素体、10部位切替を操作できる。確認画面は表示時計だけを使い、Coreの世界シミュレーションを起動しない。

## 固定した仕組み

- キャラクター左側が近側、右側が遠側。V7の17身体領域と固定64×96座標を維持し、近遠の腕・脚・履物を別表面に分けた。
- 共通の連続逆写像を同じ腕/脚の肌・甲冑・履物に適用する。頭と刀は剛体の平行移動。部品別拡縮や各コマへの再fitはしない。
- 肩y38/手首61/骨盤63/足首87/足裏91。腕振り5px、歩幅6px、足上げ3px、膝軌道1px、陣羽織裾1.5px。支持足をy91に保ち、近遠で接地と遊脚を交互にする。歩行120ms/コマ、待機240ms/インデックス。
- 遠側甲冑は同寸の近側素材から補った作画上の表面。RGB0.78倍で奥を区別する。形状/alphaを保持し、静止の中立姿勢では近側/胴に隠れて元のV7に画素一致する。未観測の遠側原画を復元したわけではない。
- 裸の遠側/近側には別々の既存身体ポリゴンを使う。顔・髪・服・性器細部を素体へ加えない。身体の仮体型と限定補完は最終採用へ昇格させない。
- 書き込み前にwork-recordの`right-motion-v8-probe`（parts）/`right-motion-v8`（motion）を既存`require_plan`へ通す。正面の人物同一性の拘束と、横向きの作画目標を分け、今回の右向きだけにレビューを限定する。

## 実測

Windows / Godot4.5.2 .NET / SDK10.0.302 / runtime.NET10.0.10 / OpenGL3.3 NVIDIA610.88 / RTX4060Ti。

- C#ビルド警告0・エラー0、Godotインポート成功。
- Python画像回帰101件成功。中立の全装備はV7元絵、全解除はV7裸素体とそれぞれ0画素差。裸の関節連結、支持足、逆写像収束/非反転、剛体頭/刀、遠側の実可視寄与、Lマスクalphaを検査。
- 12フレーム×1024選択＝12,288状態をPython出力/実Edge/実Godot CPUで照合。Godot GPU代表132状態はRGB差0。
- Godot実再生は通常/1/4速とも全8コマを観測。再生中の甲冑8部位切替成功。1100×720と1280×800で通常1倍を維持。
- 実GPU画面から保存した歩行全8枚の原寸64×96と4倍256×384を原画演算結果と比較し全画素一致。`godot-walk.gif` はその実画面PNGから作った閲覧用パレット変換で、PNG原本は別保存。
- 保存原本からの再出力でPNG30枚、データJSON、HTMLのSHAを比較、変更0。生成モデルの新規呼出し0。

`runtime-validation.json`、`browser-validation.json`、`capture-validation.json`、`replay-validation.json`、`python-tests.txt` と実画像は `Art/Officer/RightMotionV8/` に保存した。

## 見つけて直した問題

近脚を胴/腰/陣羽織より前へ描いたため隠れ面105画素が静止元絵を上書きした。腰・陣羽織の後ろへ戻して解消した。その後、L形式の遮蔽マスクをRGBAとして読むと全域alpha255になるため、先に描いた遠側が消えていることが実画像で分かった。L値をalphaへ明示代入し、遠側の実可視寄与を独立に検査する2件を追加した。クロスレンダーの一致だけではこの不具合を検出できなかった。修正前の実画面はHistoryに分け、現行の承認根拠にしない。

## 再実行と限界

`python Scripts/build_officer_right_motion.py`、`python -m unittest discover -s Scripts -p test_officer_right_motion.py`、`node Scripts/verify_officer_right_motion.cjs`、`Scripts/Validate-OfficerRightMotion.ps1`、`python Scripts/export_officer_right_capture.py`。依存コード/入力/マスク変更時は旧レビューを流用せず計画を再確認する。出力変更時はruntime_data/manifestの結果SHAも更新して再検査が必要。

これは小振幅の右向き足踏み試作であり、完成歩容や3D骨長保存ではない。隠れた布/胴の単色補完、遠側の専用作画、仮体型の改善が残る。左向きを左右反転で済ませず、刀の佩用側を考慮して別途扱う。背面、実部隊移動、世界時間との接続、6頭身との実装連携、Coreテストの再実行、配布エクスポート、GitHub CIの実行は今回未実施。マウス入力・カーソル設定変更は行っていない。
