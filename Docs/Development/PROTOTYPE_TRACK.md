# 試作版の進め方と、本作業へ戻る流れ（2026-11-18）

## なぜ試作版か
2026-11-18 の試し遊びで、ユーザーは全国の世界を「ゲームと呼べるレベルではない」と判断した。重さと分かりにくさで、遊びの芯が良いか判断できなかった。そこで進捗表（ProgressV1）の100%を目指す本作業を**保留**にし、小さな試作版で遊びの芯を作り直す。今のコードとデータは消さない。

## ブランチ
| ブランチ | 何をするか | 状態 |
|---|---|---|
| `prototype/small-world` | 試作版と、そこから先の作り直し・修正 | **今の作業はここ** |
| `prototype/claude-mainline` | 本作業（進捗表100%の工程 S12〜S14） | 保留。2026-11-18 の時点の内容を main に統合済み |
| `main` | 統合先。PR 経由でだけ更新する | — |

- 作業フォルダは C:\Workspace\Projects\Games\hakutaikagami の1か所のまま。ブランチを切り替えて使う。
- 試作版のブランチへの commit・push は承認済み（2026-11-18 ユーザー「試作版以降の話とか修正とかは別のブランチでやりたい」）。

## 試作版の中身（どこに何があるか）
| 内容 | 場所 |
|---|---|
| 地図・拠点・地形・人のデータ | Core/Data/proto-map-v1.json |
| 世界の動き（時計・歩き・道探し・日課・書状・噂・保存） | Core/Proto/ProtoWorld.cs・ProtoMove.cs |
| 務め・頼み・事件（国境の争い・峠の賊・市）・勲功と褒美 | Core/Proto/ProtoLife.cs |
| 画面（地面を押して歩く・任務のボタン・近くの人） | Game/Scripts/Proto/ProtoGame.cs、Game/Scenes/ProtoGame.tscn |
| 確認用の窓口と印 | Game/Scripts/Proto/ProtoDebug.cs |
| 検査 | Tests/ProtoWorldTests.cs |
| 進捗表 | [PrototypeProgress/index.html](PrototypeProgress/index.html) |

- 起動：スタート画面の「試作版：はじめから」「試作版：続きから」。
- 保存：自動（ゲーム内の10分ごと・着いた時・×で閉じる時）。場所は Godot の user://proto-save.json（前の保存は .1）。
- 印：「印を残す」で別の窓に一言を書くと、窓を開く前の画面の絵・本人が知っている状態・世界の全部と一緒に、D:\02_プロジェクト別\百代鏡\検証記録\試作版\ためておく\ に残る。「ためた印」で選んで「送る」と 送った\〈日時〉\ に一つの束になる。ユーザーがチャットで「送った」と言ったら、Claude はその束を読んで直す。送られるまで直し始めない。
- 確認用の窓口：試作版の起動中、http://127.0.0.1:8765/ （このパソコンの中だけ）。Claude が自分で確かめるときは別に起動し、HAKUTAI_PROTO_PORT=8766 と HAKUTAI_PROTO_SAVE=user://claude-check-save.json にして、ユーザーの画面と保存に触らない。Claude の確かめで作った保存と印は、終わったら消す。

## 試作版での進め方
- ユーザーが触る → 印をためて送る → Claude が読んで直す → 同じ場面で確かめて絵を見せる → ユーザーが触る、の繰り返し。
- 直したら、関係する検査（Tests/ProtoWorldTests.cs）を通してから commit・push する。
- 全件の検査と main への統合は、ユーザーが決めた時だけ。
- 進捗表（PrototypeProgress/index.html）は、項目が終わったり増えたりしたら書き直す。

## 本作業へ戻るとき
1. 試作版の作業を commit・push する。
2. `git switch prototype/claude-mainline`
3. `git merge origin/main`（main に入った分を取り込む）
4. Docs/Development/CURRENT_STAGE.md と PLAN_TO_100.json の S12 から続ける（`python Scripts/plan_to_100.py` で今の率と次の工程）。
5. 試作版で決まったことを本作業に入れるかどうかは、ユーザーの判断。入れるときは、試作版のブランチを main へ PR で統合してから 3. で取り込む。

## 試作版へ戻るとき
1. `git switch prototype/small-world`
2. 必要なら `git merge origin/main`
3. この資料と進捗表から続ける。
