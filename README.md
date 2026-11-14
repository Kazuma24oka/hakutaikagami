# 百代鏡（hakutaikagami）

幾世代にもわたる人と家の興亡を描く戦国のゲーム。英語タイトル：Mirror of Ages

## 入口
| 知りたいこと | 場所 |
|---|---|
| 全体の進み具合（％） | [Docs/Development/ProgressV1/index.html](Docs/Development/ProgressV1/index.html)（ブラウザで開く。main へ統合するときに書き直す） |
| 今やっている工程 | [Docs/Development/CURRENT_STAGE.md](Docs/Development/CURRENT_STAGE.md) |
| 資料の案内（設計・記録・提案の場所） | [Docs/README.md](Docs/README.md) |
| 作業の決まり（AI 向け） | [AGENTS.md](AGENTS.md) |

## このフォルダ
- 作業はこのフォルダ1か所で行う。ブランチは prototype/claude-mainline（main へは差し替え前の完成のときに1回だけ統合する）。
- 主な中身：Core（ゲームの仕組み）・Game（Godot の画面）・Tests（検査）・Tools / Scripts（道具）・Docs（資料）・Art（Git に入れる絵の原本）。
- ゲームを動かす Godot は `.tools/godot-4.5.2` にある（Git の外）。

## Git に入れない物の置き場所（2026-11-14 整理）
使っていない物と大きな記録は `D:\02_プロジェクト別\百代鏡` に置く。

| 置き場所 | 中身 |
|---|---|
| `参考資料\戦国下天禄` | 戦国下天禄の解析レポートと旧メモ（仕組みの参考だけ。数値・人物・画像は流用しない） |
| `素材\VRoid\CharaCreate` | ユーザーの VRoid モデル（再配布不可。Git に入れない） |
| `素材\人物の試作` | 使わなくなった人物の試作（素体・部品・着せ替えの場面・3D 武将の原型・画像の道具） |
| `検証記録` | 各工程の検証の記録（runtime-local）。Docs と同じ並びで置く。このフォルダの Docs/Design/〈工程〉/runtime-local は D へのつなぎなので、新しい記録も最初から D に書かれる。新しい工程のフォルダを作ったら `python Scripts/link_runtime_local.py` を実行してつなぐ |
| `作業の一時ファイル` | 画像生成の環境・検査の一時出力（消してもよい物） |

ゲームで今も使う 3D 武将の試作モデル（Game/Assets/Officer3D、ドット絵の武将の土台）はこのフォルダに残す。
