# Godot：正面の待機・歩行と甲冑着脱 V5

Godotのゲーム本体に「武将表示」タブを追加。待機4インデックス／歩行8インデックスを64×96・1倍で再生し、胴・肩・腕・腰・脚・履物・陣羽織・刀を個別に着脱する。顔・髪も解除可能。通常／1/4速度、停止／コマ送り、全装備／甲冑なし／裸素体の操作を用意した。着脱で再生位置はリセットしない。

[全装備のゲーム画面](Evidence/officer-motion-godot-equipped.png) ／ [腕を外した状態](Evidence/officer-motion-godot-arms-off.png) ／ [裸素体](Evidence/officer-motion-godot-bare.png) ／ [1280×800](Evidence/officer-motion-godot-resized.png)

## 表示と構成

- ゲームの街道背景内に通常1倍で表示。比較欄も1倍。右側の4倍は点検用と明示した。表示変更に合わせてPNGを拡縮・再生成しない。
- V4の独立した素体・顔・髪・8部位・個別遮蔽・素手前景シートをそのまま使用。共通選択ID、描画順、遮蔽、刀の腰追従はV4と同じ。元の素体/パーツ/動作PNGは変更なし。
- `OfficerMotionFrames.cs` はGodot標準Texture2Dを読み、実際にデコードされた20シート×12枠のRGBA SHAを照合。透明画素のRGBだけ正規化する。二値alphaを前提に、身体遮蔽→顔→8装備→籠手なしの素手→髪の順で合成する。
- `OfficerMotionTrial.cs` は現在の選択の12コマをキャッシュし、Godotの実Processで再生。待機/歩行を一つの表示時計で同期し、各比較欄も同じコマを表示する。見えないタブでは進めない。新しい世界時刻は作らず、Coreのtickを表示アニメーションから進めない。
- Sprite64×96、Scale1、Nearest、整数座標。`project.godot` のcanvas_itemsによるウィンドウ追従拡大をdisabledへ変更し、ウィンドウを広げてもキャラを引き伸ばさない。既存UIは元の座標・寸法を保持する。
- 本体Mainの「武将表示」タブから利用でき、単独の `Game/Scenes/OfficerMotionTrial.tscn` も用意。旧画面・不採用素材は削除しない。

制作前に既存work-recordへGodot限定のmotion計画を保存し、既存require_planで原本・マスク・コード・参照のSHAと前段レビューを検証。`prepare_officer_godot_motion.py` は検証後にGame/Dataの対応表/240デコード枠SHA/12,288比較値だけを出力する。新しい画像生成やマスク編集はない。機械的な一致を最終外見採用へ広げない。

## 実行・実測

Windows上でGodot4.5.2 .NET、SDK10.0.302、実行時.NET10.0.10、net8.0ターゲット。OpenGL3.3互換レンダラー、NVIDIA610.88 / RTX4060Tiで実ゲームを起動。

1. C#再ビルド成功、警告0／エラー0。Godot素材インポート成功、最終実行ログも警告/エラー0。
2. C#の全1024選択×12枠＝12,288状態がPython側の既存RGBA SHAと一致。
3. 実GPUの画面を132状態（全12枠×全装備/裸素体/顔髪のみ/各8部位解除）で切り出し、背景を含めて照合。RGB最大差0。これはヘッドレスのダミー描画ではない。
4. 待機/歩行それぞれ通常／1/4速度で実再生し、全コマ番号と1周以上のループを観測。再生中に8部位を順次解除しても再生継続。停止・コマ送り・全復元の実UI信号を検証。OSのマウス操作/入力注入0。
5. 1100×720→1280×800へ実ウィンドウを変更してもNative64×96。保存した同じ姿勢の1倍切り出しが差分0。4倍の保存画像も、全装備・腕なし・裸素体の3状態で最近傍拡大に差分0。
6. 実画像として全装備の歩行、腕なしの歩行、裸素体の待機、ウィンドウ拡大を目視。初期に見つけた足裏ガイドの6pxずれを修正。生画像読み込みのGodot警告も正規リソース読み込みへ修正した。初期証拠はHistoryに保持。
7. Coreの単体テスト233件、既存画像工程67件が成功。従来の本体UIもヘッドレスで3武将選択/城表示のsmoke成功。ゲーム用JSONの同じ計画からの再出力SHA不変、元の22PNG不変。

実入力/コード/証拠のSHAと実行報告は `Art/Officer/GodotMotionV5/`、生ログは `Docs/Proposals/Evidence/officer-motion-godot-*.txt`。Core/Confirmed/main/designは変更しない。GitHub上のCI成功は未確認。

## 起動

Godot .NETで `Game/project.godot` を開いて実行し、「武将表示」タブを選ぶ。既にビルド・インポート済みのこのPCでは、リポジトリルートから次でも直接開ける。

```powershell
& ./.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64.exe --path Game -- --officer-motion-review
```

再ビルド・インポート・実画面の自動検査（検査後は自動終了）：

```powershell
./Scripts/Validate-OfficerGodotMotion.ps1
```

この入口は既存制作ゲートを必須とする。実GPU検査は表示環境が必要。初回の素体の細さは保留のままで、自然な完成歩容・他方向・6頭身対応・合戦/交渉・移動命令への接続、配布用エクスポートは今回の完成範囲ではない。
