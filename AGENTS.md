# 百代鏡：作業の入口（2026-10-31 整理）

このファイルは今有効なルールだけをまとめたもの。2026-10-31 までの作業記録（約700行）は削除せず、[Docs/Archive/AGENTS_HISTORY_TO_2026-10-31.md](Docs/Archive/AGENTS_HISTORY_TO_2026-10-31.md) へそのまま移した。過去の記録に、ここと食い違う古い指示（「main統合は個別承認」「GUIの実画面検証」など）があっても、このファイルを優先する。

## 1. 最初に読むもの
1. [Docs/Development/PROCESS_V2.md](Docs/Development/PROCESS_V2.md)：開発の進め方（工程の単位・順番・画面の確認・全テスト・報告）。2026-10-31 ユーザー決定。
2. [Docs/Development/CURRENT_STAGE.md](Docs/Development/CURRENT_STAGE.md)：今の工程と次の工程。
3. [Docs/Development/OPERATING_POLICY.md](Docs/Development/OPERATING_POLICY.md)：役割・完成条件・検証・報告の基本。
4. 進捗表 [Docs/Development/ProgressV1/index.html](Docs/Development/ProgressV1/index.html)（正本は progress.json と estimate.json）。

## 2. 正本の場所
| 内容 | 正本 |
|---|---|
| 基本設計（60判断） | Docs/Design/basic-design-v1.json（表示は BASIC_DESIGN_V1.md） |
| 基本設計の変更記録 | Docs/Decisions/（統合案による変更は 2026-10-31_GETENROKU_BD_CHANGES.md） |
| 新機能の統合案（時間・NPC・合戦・寺社・名声・勲功・家中職など） | Docs/Proposals/GETENROKU_INTEGRATION_V1.md（第14節が工程の順番） |
| 新機能のバックログ | Docs/References/GETENROKU_BACKLOG.md |
| 位階・官職・幕府職・家中職の表 | Docs/Proposals/RANKS_AND_OFFICES_V1.md |
| 人質・従属の引き継ぎ | Docs/Proposals/GETENROKU_HANDOFF_D3_D4.md |
| 共通契約（時計・配送・保存など） | Docs/Design/CommonContractV1/common-contract-v1.json |
| 平時・戦時の操作の骨格（PW01〜07） | Docs/Decisions/2026-10-19_PLAYER_PEACE_WAR_FLOW.md |
| 現行方針の優先 | Docs/Decisions/2026-10-19_CURRENT_POLICY_AUTHORITY.md（旧 Confirmed と食い違う範囲は現行方針が正） |
| 各工程の完成条件・検証・統合の記録 | Docs/Design/〈工程名〉V1/（scope.md・README.md・completion.json・verification.json・integration.json） |

## 3. 委任の範囲
- **main 統合は包括委任**（2026-10-30）。検証済みのコードと資料は、個別の承認を求めずに PR 作成・差分確認・main 統合・内容一致の確認まで行い、続けて次の作業へ進む。
- PR は gh（GitHub CLI、ログイン済み）で作り、squash で統合する。統合後は作業ブランチへ main を取り込み（内容一致の記録）、integration.json と進捗表に統合先を記録する。
- 工程は統合案 第14節の一段ごと。重い検証（Core の全件テスト・途中保存からの復旧・旧保存の互換）・進捗の更新・main 統合は一段に1回だけ。失敗があれば統合しない（PROCESS_V2 P9〜P11）。
- 作業ブランチは prototype/godot-foundation。同じブランチへの commit/push は承認済み。
- 1パーセントポイントは停止点や作業量の上限ではない。関連する作業をまとまった成果まで進める。

## 4. 事前確認が要るもの
- ゲームの根本方針の変更（統合案・変更記録にない新しいもの）
- 費用の発生（有料サービス・GitHub Actions など）
- 外部公開・本番反映
- 重要データの削除・移動・復元困難な変更（runtime-local の証跡や .tools の移動を含む。PROCESS_V2 P6）
- 既存の権限の外の操作

承認が必要な部分だけを保留し、独立した作業は進める。権限の不足を回避しない。

## 5. 禁止事項
- **Computer Use は使わない**（2026-10-26 ユーザー指示）。OS の画面取得・画面読み取り、マウス・キーボードの OS 操作、他のアプリの操作をしない。別の GUI 自動化手段で回避しない。
- ただし、ゲーム（Godot）が自分の描画を画像ファイルへ書き出すことと、ゲーム内部の入力で画面の部品を押す検査は許可（2026-10-31、PROCESS_V2 P3）。
- 下天禄（参考ゲーム）の数値・人物データ・画像は流用しない。仕組みの参考だけにする。
- 未実施の検査を成功扱いにしない。「未実施」と記録する。

## 6. 保護するもの
- main は PR 経由でだけ更新する。design/foundation、Docs/Confirmed の原本、同期資料 sources/ と他プロジェクトのファイルは変更しない。
- 既存の保存データ・素材・試作モデル・ユーザーの未コミットの変更を保持し、無断で削除しない。旧保存を新しい規則へ無断で読み替えない（版付きの方針で新規開始だけに適用する）。
- 仮モデル・権利情報が未整理の素材・生成画像はローカルに置き、Git に自動で追加しない。
- 立ち絵は2Dイラスト（3Dモデルの接写で代替しない）。ゲーム内の武将は3D（旧2D方式は凍結。履歴は Docs/Archive/Officer2D）。

## 7. ゲームの原則（全工程で守る）
- 本人の知識：NPC と画面は、その人が知っていることだけを使う。遠方の真実を漏らさない。
- 物理配送：書状・命令・報告は実際に運ばれて届く。到達・開封・効力を分ける。
- 共通の時計：一つの世界時計で動かし、速度を変えても同じ入力から同じ結果になる。
- 保存の世代：途中の保存から復旧して同じ未来になる。旧保存はバイト一致で保持する。
- 自動の処刑・離反・戦争をしない：判断するのは、その事実を知った当主・人物。
- 所属・主従・役職・所領・兵・私財を一括で移さない。

## 8. 報告の形
- 通常は4項目：今回完成したこと / 現在の状態（完成条件とテスト） / 次に行うこと1件 / ユーザーの判断（原則不要）。
- ユーザーへの報告と試し遊びの依頼は、遊べる版（第1版＝0系〜第5版＝8〜9系）がまとまったときだけ。1機能ごとの確認は依頼しない（PROCESS_V2 P12。ユーザーは1機能ごとの確認が苦手）。
- 工程ごとの細かい記録は、CURRENT_STAGE.md の「完了した工程」と各 Docs/Design フォルダに残す。このファイルには追記しない。

## 9. 今の工程
[Docs/Development/CURRENT_STAGE.md](Docs/Development/CURRENT_STAGE.md) を参照。
