# 背面静止V9・待機/歩行V10

背面の64×96・通常1倍を追加した。顔・髪・甲冑8部位の共通選択IDを維持し、待機4インデックス/歩行8コマを独立したGodot確認シーンに組み込んだ。既存Mainの正面V6、右向きV7/V8は変更していない。

確認ページ: `officer-back-motion-review.html`。Godotは `Scripts/Show-OfficerBackMotion.ps1` または `Game/Scenes/OfficerBackMotionTrial.tscn`。通常/1/4速、停止/コマ送り、10スロットの着脱を試せる。背面では顔を描かず、顔※は方向上の非表示として選択を保持する。

## 制作条件・保存先

甲冑の新しい背面原画にはimagegenスキルと組込み `image_gen.imagegen` を使用。最初は頭が小さく肩が高かったため不採用。元の正面・右向きと位置ガイドで比率を修正した。Seed/モデル版は公開されず、生成し直しての同一結果は保証しない。

- 採用原本: `Art/Officer/BackViewV9/armor-raw.png`（raw alpha保持）
- 最終生成文: [armor-prompt-v2.txt](../../Art/Officer/BackViewV9/armor-prompt-v2.txt)
- 全生成文・参照・結果SHA: [generation-history.json](../../Art/Officer/BackViewV9/generation-history.json)
- 静止実素材: `Game/Assets/Trials/OfficerBackViewV9/`（body/hair/face/8装備、owner/material/hidden/body-boundary）
- 動作実素材: `Game/Assets/Trials/OfficerBackMotionV10/` と `Game/Data/officer_back_motion_v10.json`
- 実Godot画像/動画: `Art/Officer/BackMotionV10/godot-walk.gif`、`godot-walk-0..7.png`

原本全体1024×1536を1/16のnearestで縮小、48色・alpha>=200の二値化。部位別fit/フレーム別fitなし。採用背面原寸の外接範囲はx9..51,y6..91。髪上端はガイド目標4に対して実画像6であり、同じ値だったとは報告しない。肩38/腰63/足裏91等は元素体に基づく作画目標で、甲冑の下の実人体を測った値ではない。

裸素体の新規生成はツールの出力段階で拒否され、画像は得られなかった。再試行や別モデルへの切替はしていない。衣服付きへの置換もしない。続行許可された既存V2の固定形状を左右登録し、胸側の陰影を除いて肩甲骨/背の肌色を明示作画した。頭/骨盤は無地で、顔・髪・服・下着・性器細部なし。実原画の耳/襟足をbody所属とし、耳への接続マスクを先に定義した。V2の仮体型と概略の側方関節位置は保持され、最終的な美観や人体適合の承認に昇格していない。

## パーツと動作

胴・肩・腕・腰・脚・履物・陣羽織・刀を個別に着脱。本人の左＝画面左へ刀を佩用。陣羽織の下には別の限定された胴の札板補完、肩の下には黒い袖の補完を用意した。補完は当時の見えない原画を復元したものではない。写真は構造参照として目視し、画素の複写や再配布はしていない。

所属・素材候補・body-boundary・hidden・身体の17部位と描画順を分離。刀が腕に分断される所属と、帯が陣羽織に混ざる所属、手の境界から漏れる1画素を実画像で見つけて修正した。髪を外しても耳は消えず、全装備は原画、全解除は固定素体へ0画素差で復元する。

背面の絵そのものを正面の反転で代用していない。動作の座標場は既存の連続体表変形を背面の本人左右へ登録して再利用する。肌/甲冑/遮蔽は同じ写像、頭/髪は剛体移動、刀は腰の剛体子。腕上下3px、足上げ3.5px、膝軌道1.2px、胴の横揺れ0.6px。支持足を足裏y91に保ち、8コマ120msで足踏みする。待機4インデックスは350ms。完全な3D骨長保存や完成した自然歩容は主張しない。

## 実測結果

Windows / Godot4.5.2.NET / SDK10.0.302 / runtime.NET10.0.10 / OpenGL3.3 NVIDIA610.88 / RTX4060Ti。

- C#ビルド警告0・エラー0、Godot取込成功。
- Pythonの画像関連回帰113件成功（既存101＋背面12）。元絵/全解除の一致、顔の非表示、耳を含む身体の連結、刀の左右/連結、支持足、剛体、逆写像収束、alphaと原寸、各スロットの実寄与を検査。
- 12フレーム×1024選択＝12,288状態をPython/実Edge/Godot CPUで比較。背面の顔は非表示なので、顔bitだけ異なる状態は同じ画像になる。全状態が別画像という意味ではない。
- 実GPUの代表132状態はRGB差0。実画面の原寸64×96と4倍256×384は、歩行8枚すべてで演算結果と全画素一致。
- Godot/Edgeで通常・1/4速とも8インデックスを実再生し、甲冑8スロットを再生中に切替。1100×720と1280×800で通常1倍を保持。
- 保存原本から静止PNG/動作PNG/JSON/HTMLを再構築し、`replay-validation.json` の全SHA一致。閲覧GIFは実画面PNGからのパレット変換で、正確な証拠PNGは別保存。

## 再検査

`python Scripts/build_officer_back.py` → `python Scripts/build_officer_back_motion.py` → `python -m unittest discover -s Scripts -p test_officer_back.py` → `node Scripts/verify_officer_back_motion.cjs` → `Scripts/Validate-OfficerBackMotion.ps1` → `python Scripts/export_officer_back_capture.py`。

work-recordの`back_view_plan`/`back_motion_plan`が入口。入力/マスク/コードSHA変更時は旧合格を流用しない。初期正面対応は人物同一性の拘束として維持し、背面の実測や全方向の承認へ読み替えない。

素体の見た目、単純化した補完面、甲冑と裸形状の側方の近似対応は改善課題。左向き、実部隊移動、世界時間への接続、6頭身実装、Core単体テストの再実行、配布出力、GitHub CIは今回未実施。マウス入力・カーソル設定は操作していない。
