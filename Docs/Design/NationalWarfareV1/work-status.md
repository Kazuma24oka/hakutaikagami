# 全国での戦の有効化（担当2の作業記録）

2026-11-13。工程を小分けにせず、BD-06・44・46・W4と全国への接続をまとめる。工程の全件検査・基準・進捗更新・PRは末尾の一度のみ。

Claudeの05240f09のNationalSystemsを取り込み、保存する一覧と新規開始の接続を保持した。旧版のHealthPolicyは過去に作った保存を読める形で残すが、新しい全国開始ではClaudeの一覧を採用し、診療設定を強制追加しない。

作業中の接続：NpcArmies、WorldClashes、BattlefieldRules、CastleSiege、CastleWorks、HostageCustody、VoyageExpense、MilitaryEconomy、HouseLogistics、HouseFreightReceiving、FreightOperations、NpcFreight、FieldConvoy、ArmyRoadMarch、RoadConvoy。全国の交戦は疎な履歴を初期化する。船賃は一覧の版のときだけ標準値を使い、旧保存の明示値は維持する。

未接続：RegionalBattle、FieldSupply、FieldPresence、FieldWorld、RegionalScenery。RiverRoads・HistoricalRoads・NationalRoutesも一覧へ接続中。旧地方の固定人物・固定拠点・固定軍の前提を全国へそのまま持ち込まない。

初期化・旧保存の代表検査2件成功。これは装備の受渡しや兵站の一巡まで遊べたという判定ではない。

有効化後の全国1日60秒以内、途中保存からの同じ未来、実機FPSは未実施。全国の4543人・1958拠点・500軍の旧軍事規則による負荷場面を準備したが、軍引受は4514秒、初期検査37秒であり、完成や性能合格とは扱わない。FPSもまだ計測していない。

生活・健康などClaude担当を追加変更しない。既存の診療部分の引継ぎはHealthExpansionV1/handoff-to-claude.md。死亡・後継の処理を二重実装しない。

全国1日の計測はDebug版で60秒を超え、中断した。完了時間・復旧一致は未判定。判定用のRelease版で時間を区切って測る。全件検査の追加はしない。

実測：現在の全国開始（4043人・1958拠点・18機能）で1日275.294秒、途中保存から同じ未来は一致。60秒条件は未達。遅さは全件検査ではなく通常同期が中心（同期2192回で地域帳簿39.7秒・市場29.8秒・財布17.7秒など）。診断用の一時計測は取り除き、開始時の城所有者の索引化と軍台帳識別子の再利用を実装している。全国の1日・保存復旧で再測定中。

武将の負傷原本は新規全国開始のOfficerBattleHealth一覧でのみ発生・保存できるよう接続中。医術・回復・生活の実装はClaude担当を維持し、健康のEnabledを勝手に変更しない。

所有者索引後：1日184.469秒（144.150＋40.319）、復旧後38.098秒、同じ未来に一致。改善したが60秒条件は未達。武将負傷は独立したOfficerBattleHealth項目で版を分け、既に保存した18項目の世界へ遡及適用しない。
