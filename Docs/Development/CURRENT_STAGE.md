# 現在の工程（2026-11-14 書き直し）

2026-10-31 までの工程記録は [Docs/Archive/CURRENT_STAGE_HISTORY_TO_2026-10-31.md](../Archive/CURRENT_STAGE_HISTORY_TO_2026-10-31.md)、2026-11-13 までの製品受入と工程の実測の入口は [Docs/Archive/CURRENT_STAGE_ACCEPTANCE_TO_2026-11-13.md](../Archive/CURRENT_STAGE_ACCEPTANCE_TO_2026-11-13.md) にある。各工程の詳しい完成条件・検証・統合の記録は Docs/Design/〈工程名〉V1/ にある。

## 差し替え前の完成へ（2026-11-11 ユーザー指示）

目標と範囲は [PRE_ART_COMPLETION.md](PRE_ART_COMPLETION.md)。正式な絵・3D の差し込み直前まで全枠を進める。2026-11-13 から Codex は止め、Claude が一人で進める（Codex の作業は引き継ぎ済み）。

- 作業の場所：C:\Workspace\Projects\Games\hakutaikagami の1か所、ブランチ prototype/claude-mainline（2026-11-14 に作業フォルダを1つにまとめた）。
- main への統合は差し替え前の完成に届いたときに1回だけ。全件テストはその前に1回だけ。進捗表（ProgressV1）の率もそのときに書き直す。今の表は 2026-11-13 の時点（約75%）。
- ユーザーへの報告と試し遊びの依頼は、差し替え前の完成に届いたときだけ（PROCESS_V2 第1・5節）。

### 工程の表（2026-11-12 に Claude が出した7行）
今の状態と残りは [Docs/Design/NationalSystemsV1/README.md](../Design/NationalSystemsV1/README.md) の「工程の表の今の状態」が正本。「切り上げ」は、残りを書き出して次の行へ進んだという意味で、その枠の100%ではない。

| 行 | 内容 | 状態（2026-11-14） |
|---|---|---|
| 1 | 武将の死亡と後継者で続ける（BD-07・01） | 切り上げ |
| 2 | 史実の事件・歴史モード（BD-03・02・08） | 切り上げ |
| 3 | 京の政治（BD-53〜56） | 切り上げ |
| 4 | 負傷・捕縛、合戦と家の滅亡・再興（BD-06・44・46） | 切り上げ |
| 5 | 全国の世界の残り（BD-04・20・05） | 作業中（多数の軍の重さ。Docs/Design/NationalWarfareV1/work-status.md） |
| 6 | 残りの枠を全国の開始で一通り動かす | 作業中 |
| 7 | 最後の確認と main への統合 | 未着手 |

## 進捗表の範囲（2026-10-31 ユーザー決定）
統合案の新機能群は、60枠の数と重みを変えずに、関係する各枠の「残り」と完成範囲に足す。完成範囲が広がるので、関係する枠の％は一度下がる。2026-10-31 に2回に分けて付け直し済み：1回目で29枠、2回目で19枠、3回目（データの側）で3枠、4回目（横断的な作業）で9枠、5回目（戦国史SE・戦国時代DXから取り込んだ仕組み、統合案 第26節）で15枠を下げ、全体は57.7%→51.4%→49.0%→48.4%→47.4%→46.4%（Docs/Design/ProgressScopeV1〜V5）。人物・家・拠点に持たせる項目の一覧は Docs/Proposals/CHARACTER_DATA_FIELDS_V1.md。

## 完了した工程（直近）
| 日付 | 工程 | 記録 | main |
|---|---|---|---|
| 2026-11-14 | 捕縛と処遇・身代金・本人の捕縛と解放・引き換え・家臣が率いる軍・画面 | Docs/Design/BattleCaptivityV1/ | 未統合（prototype/claude-mainline） |
| 2026-11-14 | 遠方の合戦（W4）・家の滅亡と再興・従属の離反 | Docs/Design/NationalStrategyV1/ | 未統合（prototype/claude-mainline） |
| 2026-11-14 | 全国の和睦の一巡・NPC の手当・他人の能力の推定・多数の軍の不具合と軽量化 | Docs/Design/NationalSystemsV1/・NationalWarfareV1/ | 未統合（prototype/claude-mainline） |
| 2026-11-13 | Codex の引継ぎ・傷病死・全国の健康と和睦 | Docs/Design/NationalSystemsV1/・HealthExpansionV1/ | 未統合（prototype/claude-mainline） |
| 2026-11-13 | 全国人物の生年・5能力（v115、新規v3と旧全国v1/v2保存の版分離を検証済み） | Docs/Design/NationalOfficerDataV1/ | PR148 → main 0d36e044、保存互換補修PR149 → main 17be7235 |
| 2026-11-13 | 全国1560年の年次名乗り（v114） | Docs/Design/NationalNameHistoryV1/ | PR147 → main b40dd017 |
| 2026-11-12 | 全国1560移動網へ川・湖沼・標高を反映（v111、BD-04 35→40・BD-20 45→50） | Docs/Design/NationalTerrainRoutesV1/ | PR #143 |
| 2026-11-12 | 全国郡の初期支配・並立・根拠を新規世界へ接続（v112、BD-20 50→55） | Docs/Design/NationalDistrictControlV1/ | PR #143 |
| 2026-11-12 | 操作の引き継ぎ・全国の家の戦略・位階と官職の表と奏請・出世の目標・画面の説明の文言の削除（v150） | Docs/Design/ControlSuccessionV1/・NationalStrategyV1/ | PR144 → main ee373079 |
| 2026-11-12 | 全国1560年の開始の版（BD-04 25→35・BD-20 35→45。休眠の世界の手番のまとめた検査 v109 を含む） | Docs/Design/NationalStartV1/・Docs/Design/NpcBatchValidationV1/ | PR142 → main cde0bf58 |
| 2026-11-12 | 遠方の人物の休眠（BD-04 22→25） | Docs/Design/NpcDormancyV1/ | PR141 → main 66503de0 |
| 2026-11-12 | 進行の重さの見直し 第1段（BD-04 20→22） | Docs/Design/AdvanceCostV1/ | PR140 → main fb8aea41 |
| 2026-11-12 | 城の移動網の世界の権利の記録と重さの測定（BD-04 18→20） | Docs/Design/CastleNetworkAuthorityV1/ | PR139 → main 0e65edd9 |
| 2026-11-12 | 地形（川・湖・山）の表示と切り替え（BD-20 32→35） | Docs/Design/MapTerrainV1/ | 202b744（PR136） |
| 2026-11-12 | 国と郡の境界の表示（経路・国・郡の切り替え。BD-20 30→32） | Docs/Design/MapBordersV1/ | 0902b51（PR131） |
| 2026-11-12 | 地図の範囲と寄り引き（北方領土・琉球・蝦夷地、朝鮮半島の城を除く。BD-20 28→30） | Docs/Design/MapExtentV1/ | 2a6a487（PR129） |
| 2026-11-12 | 城の移動網を世界ごとの版で使う（BD-04 15→18・BD-20 25→28） | Docs/Design/CastleNetworkWorldV1/ | a5c59b7（PR128） |
| 2026-11-12 | 百代鏡DB V4（参考サイトの文字・西美濃三人衆・津軽の並立の修正、城を拠点にした移動網のデータ、加点なし） | Docs/Design/HakutaiDbV4/ | 9a4233f（PR127） |
| 2026-11-12 | 百代鏡DB V3（所属の城・モブの地域の苗字、世界への配置の設計） | Docs/Design/HakutaiDbV3/ | 7b30092（PR126） |
| 2026-11-12 | 百代鏡DB V2（郡と石高・城主不明の城・1560年の名乗り・登場人数とモブ・〇△×、加点なし） | Docs/Design/HakutaiDbV2/ | 82195b5（PR125） |
| 2026-11-12 | 百代鏡DB（戦国DBの版から1560年の家・人物・城、史料の画面） | Docs/Design/HakutaiDbV1/ | dd89e99（PR124） |
| 2026-11-12 | ドット絵の武将と兵（平時は和服・戦時は甲冑、足軽・僧兵、加点なし） | Docs/Design/DotOfficerV1/ | bc3b9b9（PR123） |
| 2026-11-12 | 健康と療養（負傷・病・手当・行動制限） | Docs/Design/PersonalHealthV1/ | 500ce42（PR122） |
| 2026-11-11 | Codex の引継ぎ（村救済の上申）と、開始版の更新で壊れた旧テスト96件の修正 | （コミット 3bc35de） | 500ce42（PR122） |
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
