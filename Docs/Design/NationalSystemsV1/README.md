# 全国の開始で仕組みを動かす V1（2026-11-13）

工程の表（2026-11-12 に Claude が出した7行）の6行目「残りの枠を全国の開始で一通り動かす」。地方の版で作った仕組みの多くが、全国の開始では有効になっていなかった（94のうち63。Tests/NationalCoverageProbe.cs で数えた）。

- 仕掛け：`NationalSystems.Current` に名前を足し、その仕組みの `Enabled` に `NationalSystems.On(w,"名前")` を加える。新規開始のときに一覧を `NewGameState.NationalSystems` に写して保存し、保存済みの世界は変えない。専用の設定（SocialPolicy など）を持つ仕組みは、`NationalStart.Create`・`New` でその設定を渡す。
- 有効にしたもの：生活と目標、同行者、人間関係、婚姻、養子、密書、天候、治安（賊）、家督、歴史モード（`NewGameState.History`。全国の家の戦略の史実の出陣の重みに効く）、健康と診療・傷病死、和睦と同盟の交渉、合戦での捕縛と処遇、家臣が NPC の軍を率いること（RetainerCommand）、NPC の自分からの手当（NpcSelfCare）、他人の能力の推定（AbilityReputation：家の当主と同じ家の者は評判で、面識のある相手は狭い幅で、段階・推定幅・出典・時点を出し、真の値は渡さない。BD-16）、遠方の合戦（StrategyBattle）、家の滅亡と再興（StrategyRestoration）、従属の離反（StrategyDefection）、戦・兵站・輸送・航海・道（Codex が接続した24項目。記録は Docs/Design/NationalWarfareV1）。
- 開始条件の画面の説明口調の文言を名前だけにした。

## 工程の表の今の状態
| 工程 | 担当 | 状態 |
|---|---|---|
| 武将の死亡と後継者で続ける（BD-07・01） | Claude | 切り上げ。危篤・出家は残件 |
| 史実の事件・歴史モード（BD-03・02・08） | Claude | 切り上げ |
| 京の政治（BD-53〜56） | Claude | 切り上げ。全国の仲裁は和睦交渉が軍勢の世界を前提にするため残件 |
| 負傷・捕縛、合戦と家の滅亡・再興（BD-06・44・46） | Claude（2026-11-13 に Codex から引継ぎ） | 作業中 |
| 全国の世界の残り（BD-04・20・05） | Claude（2026-11-13 に Codex から引継ぎ） | 未着手 |
| 残りの枠を全国の開始で一通り動かす | Claude | 作業中（このフォルダ） |
| 最後の確認と main への統合 | Claude | 未着手 |

## 確かめたこと
- 全国の開始で上の仕組みが有効になり、目標を決めて進め、途中の保存から戻して同じになる（NationalSystemsTests）。
- 歴史モードを持ち、地方の版には付けられない。
- 重さ（2026-11-14、PC が空いた状態、織田信長で1日分、実際のゲームと同じく検査用の写しの書き換え確認 GuardTurnSnapshots を切って測定。Tests/NationalSystemsCostProbe.cs の HAKUTAI_NS_SET=now）：今の全国の開始（v4 の郡の収入、上の仕組みすべて、Codex が接続した戦・兵站の24項目、健康・和睦・捕縛）で38.7秒。遊べる目安の60秒に収まる。内訳は保存の検証が約14秒（31回）、確定の処理が約3秒（246回）、残りは NPC の手番。計測の仕掛けは Core/Contracts/Prof.cs（HAKUTAI_PROF=1。測りたい処理を Prof.T で囲む）。
- 重さの直し：家督と奉公の同期の開始時の関係の使い回し（NewGameWorld.InitialRelations）、能力の同期の辞書引き（CharacterDevelopment.Derive）、病の判定と生年の使い回し（PersonalHealth）。郡の収入の帳簿・在庫・所在の使い回しは Codex の直し（Docs/Design/NationalWarfareV1）。

- 全国での和睦交渉の一巡（NationalSystemsTests、2026-11-14）：織田信長が今川家の当主へ不戦20日の書状を送り、相手が開封して承諾の返書を出し、本人が開封して成立を知るまで、ゲーム内で4日。保存から戻して同じ。検査は検査用の写しの書き換え確認を入れたままで27分かかる（実際のゲームの重さとは別）。

## 残り
- 健康：NPC の手当は「発症の1日後に城や町にいれば受ける」だけ（NpcSelfCare、PersonalHealth.NpcCare）。医者を探して移る・家人に頼む判断は未実装。
- 同盟交渉（TreatyNegotiations）の全国での一巡は未実施。
