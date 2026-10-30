# 現在の工程（2026-10-31 整理）

2026-10-31 までの工程記録は削除せず [Docs/Archive/CURRENT_STAGE_HISTORY_TO_2026-10-31.md](../Archive/CURRENT_STAGE_HISTORY_TO_2026-10-31.md) へそのまま移した。各工程の詳しい完成条件・検証・統合の記録は Docs/Design/〈工程名〉V1/ にある。

## 今の工程：NPCの既知の用件と空き時間の修行（未着手・完成条件は凍結済み）
- 完成条件：[Docs/Design/NpcPurposeV1/scope.md](../Design/NpcPurposeV1/scope.md)
- 固定基準：Docs/Development/ProgressV1/approval-baseline-v47.json（commit 2d57d0b で再評価後の基準に凍結し直し。旧 v46 は置き換え済み）。実装時に Scripts/check_approval_batch.py の BASELINES へ 47 を足す。目標は BD-13 70・BD-58 71。
- 進め方は PROCESS_V2.md に従う（遊べる一巡を完成条件に入れる、例外は代表1〜2件）。

## その次：統合案 第14節の順番へ移る（PROCESS_V2 P2）
1. 0a 時間の仕組み：日の速度・次の出来事まで・自動の停止と減速・出来事ごとにまとめて進める計算（統合案 第19節 T1〜T6）
2. 0b NPCの判断の土台：全員同じ仕組み・重要度による判断の頻度・性格と志・歴史モードの重み・本人への関わりの難易度（第20節 W1〜W8）
3. 0c 史実の事件の仕組みと年代記（第22節 E1〜E8）
4. 0 画面の土台：4つの動詞（会う・書く・任せる・動く）・手帳・予備の一覧（第18節 U1〜U9）
5. 1〜：関係・贈り物・寺社・名声・名物・勲功…（第14節の表）

本線がすでに作った部分（条約・人質・能力・関係・婚姻・養子・NPCの継続行動など）は、各段と突き合わせて重複を除く。人質・従属は GETENROKU_HANDOFF_D3_D4.md を入力にする。

## 進捗表の範囲（2026-10-31 ユーザー決定）
統合案の新機能群は、60枠の数と重みを変えずに、関係する各枠の「残り」と完成範囲に足す。完成範囲が広がるので、関係する枠の％は一度下がる。2026-10-31 に付け直し済み：29枠を下げ、全体は57.7%→51.4%（Docs/Design/ProgressScopeV1）。

## 完了した工程（直近）
| 日付 | 工程 | 記録 | main |
|---|---|---|---|
| 2026-10-31 | 統合案の範囲を進捗表へ加える再評価（57.7%→51.4%、加点なし） | Docs/Design/ProgressScopeV1/ | （この PR） |
| 2026-10-31 | NPC継続行動の処理負荷低減（行動不変、29 NPC場面の中央値6404ms→5558ms、加点なし） | Docs/Design/NpcPerformanceV1/ | c089fc5 |
| 2026-10-30 | 登録武将を含むNPCの必要に応じた継続行動 | Docs/Design/NpcContinuingV1/ | ea0b93f（PR49） |
| 2026-10-30 | 人質の実護送と到着後の返還確認 | Docs/Design/CustodyReturnV1/ | b659ed0（PR48） |
| 2026-10-30 | 人質の現地食事と家の実在米 | Docs/Design/CustodyFoodV1/ | 3964df8（PR47） |

以後、工程を終えるたびにこの表の先頭へ1行足す。詳しい説明は各 Docs/Design フォルダの README.md に書く。
