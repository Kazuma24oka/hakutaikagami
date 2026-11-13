# 全国での戦の有効化（担当2の作業記録）

工程はBD-06・44・46・W4と全国への接続をまとめる。小機能ごとに全件検査・基準・進捗加点・PR・main統合は行わない。ClaudeのNationalSystemsを取り込み、現在は作業ブランチだけへ保存している。

## 接続した入口
NpcArmies、WorldClashes、BattlefieldRules、CastleSiege、CastleWorks、HostageCustody、VoyageExpense、MilitaryEconomy、HouseLogistics、HouseFreightReceiving、FreightOperations、NpcFreight、FieldConvoy、ArmyRoadMarch、RoadConvoy、RiverRoads、HistoricalRoads、NationalRoutes、OfficerBattleHealth。全国交戦は疎な履歴を初期化し、負傷は独立した保存済み一覧の項目で遡及適用を避ける。

## 実測と限界
- 初期化・旧保存・同じ軍の二重生成防止・兵糧保存則：関連4件成功（Release、11秒）。一巡の軍務や輸送まで遊べた判定ではない。
- 4043人・1958拠点・18機能のRelease版：1日275.294秒、途中保存から同じ未来に一致。同期の反復走査を調査し、診断用変更は取り除いた。
- 開始台帳の所有者索引後：1日184.469秒（144.150＋40.319）、復旧後の後半38.098秒。同じ未来に一致。60秒の条件は未達。
- 既存拠点に検査用500家を置いた旧軍事規則の場面：4543人・1958拠点・500軍・1500部隊。Release版で作成40.713秒、軍引受1.225秒、初期検査1.884秒。以前のDebug版の軍引受4514秒・検査37秒とはビルド構成も違うため厳密な比較倍率にはしない。軍の識別子を繰り返し計算せず、ロットを集合で照合するようにした。
- 同じ500軍の保存をゲームへ読み込んだ停止中の描画：Godot 4.5.2 .NET / OpenGL compatibility / RTX 4060 Ti / 実描画1028×720。3秒以上測定して近景790.7 FPS（95%フレーム1.414ms）、全国133.7 FPS（8.072ms）。自前描画も確認。Computer Use未使用。全員の同時歩行・交戦中・全国全機能の版のFPSではない。

## 残り
RegionalBattle、FieldSupply、FieldPresence、FieldWorld、RegionalSceneryは地方用の固定人物・拠点・軍の前提を全国へそのまま持ち込まず、実在軍と同じ原本に接続する。全国1日60秒以内、500軍を含む移動と保存復旧、NPCの自発的判断、捕縛・処遇・交換・身代金、W4、内応・離反・独立と滅亡・再興も残る。完成・性能合格・全件成功とは扱わない。

生活・健康などClaude担当を追加変更しない。診療の引継ぎはHealthExpansionV1/handoff-to-claude.md。新しい全国開始はClaudeの共通一覧を採用し、HealthPolicy付きの既存保存は読めるまま保持する。医術・死亡・後継処理を二重実装しない。
