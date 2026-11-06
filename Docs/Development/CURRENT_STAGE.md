# 現在の工程（2026-10-31 整理）

2026-10-31 までの工程記録は削除せず [Docs/Archive/CURRENT_STAGE_HISTORY_TO_2026-10-31.md](../Archive/CURRENT_STAGE_HISTORY_TO_2026-10-31.md) へそのまま移した。各工程の詳しい完成条件・検証・統合の記録は Docs/Design/〈工程名〉V1/ にある。

## 製品受入：1560年の尾張の偵察機会

[信長の現地判断と浪人の偵察](../Design/OwariHistoricalOpportunityV1/entry-verification.md)。次は今川方の史料と当主・本拠の初期配置を照合する。

## 製品受入：城役の終了と引渡しの順序

[辞令と本丸引渡しの競合](../Design/CastleOfficeOrderV1/entry-verification.md)。次は全体進捗表の未達項目から、通常のプレイで到達できる範囲を広げる。

## 製品受入：本丸受領の遠隔報告

[受領報告・開封と中途復旧](../Design/AlliedKeepReportsV1/entry-verification.md)。次は現地の職の失効と通知の到着順を扱う。

## 製品受入：追随城主の現地引渡し

[城門受領後の復旧と本丸引渡し](../Design/AlliedCastleGateV1/entry-verification.md)。次に交渉中の権限喪失と引渡し結果の遠隔報告を扱う。

## 製品受入：他城への独立宣言

[書状と返書の実測](../Design/IndependenceLettersV1/entry-verification.md)。次は他城の守備と実効支配を扱う。

## 製品受入：城主独立時の城内係争

[守備・交渉・本丸受領の実測](../Design/IndependenceGarrisonV1/entry-verification.md)。次は独立宣言を他の城へ届け、現地の武将・守備隊が個別に返答する経路を扱う。

## 工程9b 包囲網の代表経路を検証済み

[包囲網の実測と残件](../Design/CoalitionV1/entry-verification.md)。次は第5版を通常入口から通し、残る全国製品版の条件と照合する。

[工程9aの実測](../Design/SupremeOfficeV1/entry-verification.md)。

[工程9の実測](../Design/CentralMediationV1/entry-verification.md)。

[工程8の実測](../Design/HouseholdDetachmentV1/entry-verification.md)。

[工程7cの実測](../Design/KokujinCouncilV1/entry-verification.md)。

[工程7bの実測](../Design/SubmissionDemandV1/entry-verification.md)。

[工程7aの実測](../Design/VillageResistanceV1/entry-verification.md)。

募兵・陣触れ・開戦の代表経路を確認した。[工程7の実測](../Design/RecruitmentMusterV1/entry-verification.md)。

## 次の工程（統合案第14節）
1. 9b：包囲網の呼びかけ・各家の判断・盟約・切り崩し
2. 第14節の後続段へ進む。

ユーザーへの試し遊びの依頼は、遊べる版（第1版＝0系、第2版＝1〜3系、第3版＝4〜5系、第4版＝6〜7系、第5版＝8〜9系）がまとまったときだけ（PROCESS_V2 P12）。

本線がすでに作った部分（条約・人質・能力・関係・婚姻・養子・NPCの継続行動など）は、各段と突き合わせて重複を除く。人質・従属は GETENROKU_HANDOFF_D3_D4.md を入力にする。

## 進捗表の範囲（2026-10-31 ユーザー決定）
統合案の新機能群は、60枠の数と重みを変えずに、関係する各枠の「残り」と完成範囲に足す。完成範囲が広がるので、関係する枠の％は一度下がる。2026-10-31 に2回に分けて付け直し済み：1回目で29枠、2回目で19枠、3回目（データの側）で3枠、4回目（横断的な作業）で9枠、5回目（戦国史SE・戦国時代DXから取り込んだ仕組み、統合案 第26節）で15枠を下げ、全体は57.7%→51.4%→49.0%→48.4%→47.4%→46.4%（Docs/Design/ProgressScopeV1〜V5）。人物・家・拠点に持たせる項目の一覧は Docs/Proposals/CHARACTER_DATA_FIELDS_V1.md。

## 完了した工程（直近）
| 日付 | 工程 | 記録 | main |
|---|---|---|---|
| 2026-11-06 | 家督争い・後見 | Docs/Design/GuardianshipV1/ | 7265807（PR85） |
| 2026-11-06 | 後継指名・誓紙・縁戚 | Docs/Design/HeirDesignationV1/ | 693dd69（PR83） |
| 2026-11-06 | 家族の生死と知らせ | Docs/Design/FamilyLifeV1/ | 6451f93（PR82） |
| 2026-11-06 | 現地の暗殺・街道接触 | Docs/Design/AssassinationV1/ | f6b1185（PR81） |
| 2026-11-06 | 破壊系計略 | Docs/Design/DestructiveSchemesV1/ | 3d93132（PR80） |
| 2026-11-06 | 攻城と海上補給 | Docs/Design/CastleSiegeV1/ | 98c8022（PR79） |
| 2026-11-06 | 城の区画普請と石工 | Docs/Design/CastleWorksV1/ | c44f1e8（PR77） |
| 2026-11-06 | 治安と警固 | Docs/Design/LocalSecurityV1/ | db8fd89（PR76） |
| 2026-11-06 | 兵種・陣形・療養・寺社協力 | Docs/Design/BattlefieldRulesV1/ | 9214d60（PR75） |
| 2026-11-06 | 賊・護衛・討伐 | Docs/Design/BanditEncounterV1/ | 2498ca9（PR74） |
| 2026-11-05 | 能力と仕事 | Docs/Design/AbilityEffectsV1/ | e701bd5（PR73） |
| 2026-11-05 | 季節・地域災害 | Docs/Design/RegionalWeatherV1/ | b20f90f（PR72） |
| 2026-11-05 | 指出・検地・年貢 | Docs/Design/LandSurveyV1/ | a022b2f（PR71） |
| 2026-11-05 | 職人・技術・教会と交易船 | Docs/Design/CraftTradeV1/ | f7efc98（PR70） |
| 2026-11-05 | 常設奉行・定例評定と家の方針 | Docs/Design/StandingAdministrationV1/ | 0096bb0（PR69） |
| 2026-11-05 | 開発の普請・造船 | Docs/Design/PublicWorksV1/ | bfed5dc（PR68） |
| 2026-11-05 | 書状の使い・買付納品・口入れ | Docs/Design/BrokerWorkV1/ | 04b3a69（PR67） |
| 2026-11-05 | 登用・常設職・評定 | Docs/Design/HouseholdCourtV1/ | 0942fc3（PR66） |
| 2026-11-05 | 情報系計略・書状照合・密偵 | Docs/Design/InformationSchemesV1/ | 3a580e1（PR65） |
| 2026-11-05 | 勲功・恩賞・不満と顔 | Docs/Design/MeritRewardsV1/ | 9f10959（PR64） |
| 2026-11-05 | 私有物・紹介状・名物と茶会 | Docs/Design/SocialAssetsV1/ | 573f883（PR63） |
| 2026-11-05 | 人物関係・贈答・寺社・名声 | Docs/Design/SocialWorldV1/ | 417d5a9（PR62） |
| 2026-11-05 | 0系 動詞と手帳 | Docs/Design/VerbNotebookV1/ | 2669739（PR61） |
| 2026-11-05 | 0c 事件の前提と年代記 | Docs/Design/HistoricalChronicleV1/ | f62af8c（PR60） |
| 2026-11-05 | 0b NPCの判断の土台 | Docs/Design/NpcJudgmentV1/ | 068a82a（PR59） |
| 2026-11-05 | 0a 時間の仕組み（T1〜T6） | Docs/Design/WorldTimeV1/ | 9778e6d（PR58） |
| 2026-11-05 | NPCの既知の用件と空き時間の修行 | Docs/Design/NpcPurposeV1/ | 411b0b2（PR57） |
| 2026-10-31 | 他の参考作品からの範囲の追加（5回目の再評価、47.4%→46.4%、加点なし） | Docs/Design/ProgressScopeV5/ | 8b3171e（PR56） |
| 2026-10-31 | 横断的な範囲の追加（4回目の再評価、48.4%→47.4%、加点なし） | Docs/Design/ProgressScopeV4/ | e9f580f（PR55） |
| 2026-10-31 | データの側の範囲の追加（3回目の再評価、49.0%→48.4%、加点なし） | Docs/Design/ProgressScopeV3/ | b1353c4（PR54） |
| 2026-10-31 | 統合案・バックログの範囲の追加（2回目の再評価、51.4%→49.0%、加点なし） | Docs/Design/ProgressScopeV2/ | 908fb4c（PR52） |
| 2026-10-31 | 統合案の範囲を進捗表へ加える再評価（57.7%→51.4%、加点なし） | Docs/Design/ProgressScopeV1/ | 559fd9e（PR51） |
| 2026-10-31 | NPC継続行動の処理負荷低減（行動不変、29 NPC場面の中央値6404ms→5558ms、加点なし） | Docs/Design/NpcPerformanceV1/ | c089fc5 |
| 2026-10-30 | 登録武将を含むNPCの必要に応じた継続行動 | Docs/Design/NpcContinuingV1/ | ea0b93f（PR49） |
| 2026-10-30 | 人質の実護送と到着後の返還確認 | Docs/Design/CustodyReturnV1/ | b659ed0（PR48） |
| 2026-10-30 | 人質の現地食事と家の実在米 | Docs/Design/CustodyFoodV1/ | 3964df8（PR47） |

以後、工程を終えるたびにこの表の先頭へ1行足す。詳しい説明は各 Docs/Design フォルダの README.md に書く。
