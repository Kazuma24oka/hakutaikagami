# 百代鏡（hakutaikagami）

幾世代にもわたる人と家の興亡を描く戦国のゲーム。英語タイトル：Mirror of Ages

## 入口
| 知りたいこと | 場所 |
|---|---|
| 作業の決まり | [AGENTS.md](AGENTS.md) |
| 約束と、今やっていること | [Docs/Development/OPEN_ITEMS.md](Docs/Development/OPEN_ITEMS.md) |
| 試作版の進め方と進捗 | [Docs/Development/PROTOTYPE_TRACK.md](Docs/Development/PROTOTYPE_TRACK.md)、[Docs/Development/PrototypeProgress/index.html](Docs/Development/PrototypeProgress/index.html) |
| 全ファイルの台帳 | [Docs/Development/FileLedger/review.html](Docs/Development/FileLedger/review.html)（`python Scripts/file_ledger.py report` で作り直す） |

## ブランチ
- prototype/small-world：今の作業（小さな試作版で遊びの核を作り直す）。
- prototype/claude-mainline：本作業（全国の世界・進捗表）。資料・工程表・確かめのスクリプトはこちらに全部残っている。

## このフォルダ
- 主な中身：Core（ゲームの仕組み。試作版は Core/Proto）・Game（Godot の画面。試作版は Game/Scripts/Proto）・Tests（検査）・Scripts（道具）・Docs（資料）・Art（絵の原本）。
- ゲームを動かす Godot は `.tools/godot-4.5.2` にある（Git の外）。スタート画面の「試作版：はじめから／続きから」で試作版。

## Git に入れない物の置き場所
使っていない物と大きな記録は `D:\02_プロジェクト別\百代鏡` に置く。

| 置き場所 | 中身 |
|---|---|
| `参考資料\戦国下天禄` | 戦国下天禄の解析と旧メモ（仕組みの参考だけ。数値・人物・画像は流用しない） |
| `素材\VRoid\CharaCreate` | ユーザーの VRoid モデル（再配布不可。Git に入れない） |
| `素材\人物の試作` | 使わなくなった人物の試作 |
| `検証記録` | 検査の記録。試作版の印（`試作版\ためておく`・`送った`）、検査の回ごとの時間（`検査の時間`）、検査のチェックポイント（`検査の途中`） |
| `作業の一時ファイル` | 消してもよい一時出力 |

台帳で「手元だけ」にしたファイル（生成画像・権利の整理されていない素材・Godot が作る .uid など）は、このフォルダの中にあっても Git に入れない。
