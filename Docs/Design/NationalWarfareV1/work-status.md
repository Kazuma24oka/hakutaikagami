# 全国での戦の有効化（担当2の作業記録）

BD-06・44・46・W4と全国への接続を一工程で進める。途中は担当ブランチへのコミット・pushのみ。全件検査・基準・進捗加点・PR・main統合は工程の終わりにまとめる。

## 接続した仕組み
新規開始の保存済み一覧は24項目。Codex担当はOfficerBattleHealth、FieldPresence、FieldSupply、FieldWorld、RiverRoads、HistoricalRoads、NationalRoutes、MilitaryEconomy、HouseLogistics、HouseFreightReceiving、FreightOperations、NpcFreight、FieldConvoy、ArmyRoadMarch、RoadConvoy、NpcArmies、WorldClashes、BattlefieldRules、CastleSiege、CastleWorks、HostageCustody、VoyageExpense。ClaudeのPersonalLife・HouseSuccessionを保持。古い一覧なし／FieldWorldなしの保存の規則は変えない。

FieldPresence・FieldSupply・FieldWorldは既存のNewGameMilitary・NpcArmiesの原本から求める。地方の架空人物・軍・兵糧・別時計は追加しない。全国のPresence・Supply・Fieldはnullのまま。布陣・携行米の操作は既存の軍務受付を通り、既存の消費と重複受付防止を使う。現地で本人が直接出す配置命令は即時有効で、遠隔命令の配送を省略する変更ではない。FieldSupplyで旧架空演習を連鎖起動しない。

## 今回の検証
- 全国の実在家臣・家・米で軍務申出→本人承諾→引受→60kgの現地受領→10kgの積載→出陣→3部隊の布陣・消費→実保存→復旧→帰還→残量返納の一巡が成功。積載の同じIDを二重実行せず、復旧直後と同じ入力で進行した世界全体がバイト一致。関連1件、8分15秒。NPCの日課なしで軍そのものを検査しており、全NPC同時行動の検査ではない。
- 配置線を他隊の現在地へ置いていた検査側の誤りを修正。最初の失敗を地形だけの問題と断定した説明は取り消す。
- 全国初期化・一覧のない旧保存・旧FieldWorldの通行／配置／復旧、新地形の全城集合位置とメッシュ高さに関係する18件が成功、11秒（Debug）。
- Godot 4.5.2 .NET / OpenGL / RTX4060Ti、1028×720。実際の布陣途中保存（4043人・1958拠点・世界7秒）をゲームへ読み込み、自前描画を確認。地形の実メッシュ38,400頂点・選択用3,200三角形と移動判定の高さを照合して一致。本人の模型の高さも一致。Computer Useは使っていない。証跡はruntime-local/native-field。
- 全国の船旅の表示がVoyageRulesを直接読んで落ちたため、支払いと同じVoyageExpense.Fareを読むよう修正。修正後のゲーム描画が成功。
- 全国で道路移動へ戻ると地方専用Fieldを参照する経路を修正。新地形の参照は保存されたFieldWorldの一覧項目で分岐する。

## 地形の範囲
national-castle-field-ground-v1は既存の試作地形を実在の各城の周囲へ配置した仮地形。史実の城郭の測量や全国の実地形の完成を意味しない。全城の集合3位置が通行可能であることを確認。移動、伝令の最初の区間、模型の接地、実メッシュ、選択用三角形に同じ高さを使う。地形の版を新規開始の定義ハッシュへ含める。正式素材へ差し替える余地を残す。

## 性能
2026-11-14、今の全国の開始（24項目に健康・和睦・捕縛を足した構成）で織田信長の1日分が38.7秒（Docs/Design/NationalSystemsV1/README.md）。60秒以内を満たした。以前の記録（95.082秒など）は構成と計測の条件が違う。

再利用は原本が空で値が不変な開始時の所有者・財布・物資・地域帳簿に限定し、外へ返す配列は複製する。NPCの判断順や実行を変えず、記録だけで全帳簿を再同期しない。

500軍の旧規則場面は4543人・1958拠点・1500部隊。Release作成40.713秒、引受1.225秒、初期検査1.884秒。停止中の実描画は近景790.7FPS、全国133.7FPS。全員同時歩行・交戦中・最新の全国規則の受入とは別（未実施）。

## 残り
2026-11-13 から Claude が引き継いだ（Docs/Design/NationalSystemsV1 の工程の表）。
- RegionalBattle・RegionalScenery の全国接続は取り込み済み（検査10件合格）。
- 捕縛は V1（Docs/Design/BattleCaptivityV1）。処遇（身代金・交換・登用・処断）、W4（遠方の合戦の簡略計算）、内応・離反・独立、滅亡・再興、500軍の移動・復旧は残り。
- 工程全体の完成・全件検査は未実施。
