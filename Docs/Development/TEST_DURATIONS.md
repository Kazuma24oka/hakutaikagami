# 検査ごとの所要時間（2026-11-12）

Scripts/test_tiers.py が全件の記録（TRX）から書く。全 4504 件・合計 1496 分（並列で実時間 約1時間10分）。
重い組（合計 300 秒を超える組）69 組に `Tier=Heavy` を付けた。短い段は残りの 2814 件・合計 153 分（全体の 10%）。

- 短い段：`dotnet test Tests -c Release --filter "Tier!=Heavy"`（版の途中と統合の前）
- 全件：`dotnet test Tests -c Release`（遊べる版をまとめるとき、別の作業用フォルダで裏で流す）

| 組 | 件数 | 合計（分） | 一番重い1件（秒） |
|---|---|---|---|
| PersonalHealthTests | 10 | 57.9 | 1547 |
| UnitDirectiveCancellationTests | 13 | 52.1 | 557 |
| UnitDirectiveRetryTests | 12 | 46.8 | 933 |
| ActiveMilitarySuccessionTests | 42 | 46.4 | 282 |
| UnitDirectiveCancelHoldingTests | 18 | 45.2 | 308 |
| UnitDirectiveTests | 27 | 44.8 | 275 |
| RoutRegroupTests | 26 | 42.9 | 258 |
| RemoteMilitarySuccessionTests | 33 | 42.5 | 321 |
| UnitDirectiveHoldingTests | 24 | 39.2 | 412 |
| UnitDirectiveCancellationBoundaryTests | 14 | 37.9 | 581 |
| UnitDirectiveCancelRetryTests | 20 | 36.5 | 306 |
| UnitCommandHoldingTests | 31 | 35.4 | 98 |
| UnitCancellationTests | 24 | 31.7 | 123 |
| UnitRecipientTests | 20 | 30.7 | 218 |
| MilitarySuccessionTests | 40 | 30.2 | 129 |
| TruceConfirmationTests | 48 | 29.3 | 109 |
| UnitHeldDeliveryTests | 18 | 28.6 | 129 |
| TreatyDefenseTests | 39 | 27.7 | 192 |
| TreatySupplyTests | 35 | 27.0 | 160 |
| UnitSenderEvidenceTests | 19 | 26.8 | 135 |
| UnitOrderDeadlineTests | 17 | 22.0 | 183 |
| HouseConditionTests | 22 | 21.6 | 510 |
| MilitaryRenewalTests | 23 | 21.2 | 160 |
| KnownMilitaryTargetTests | 16 | 20.4 | 219 |
| RepresentativeContactTests | 41 | 20.3 | 105 |
| UnitReplyRecoveryTests | 21 | 19.7 | 122 |
| CustodyReturnTests | 22 | 18.8 | 98 |
| UnitRetryTests | 17 | 18.2 | 104 |
| NpcPurposeTests | 23 | 16.9 | 841 |
| HouseholdMilitaryTests | 18 | 16.8 | 615 |
| PersonalWithdrawalTests | 42 | 16.6 | 78 |
| RepeatedExerciseTests | 21 | 16.5 | 531 |
| TruceSuccessionTests | 20 | 15.0 | 64 |
| CastleSiegeTests | 6 | 14.7 | 512 |
| RegionalDomainTests | 45 | 14.4 | 346 |
| MilitaryRecognitionTests | 20 | 13.9 | 211 |
| UnitInquiryTests | 18 | 13.6 | 67 |
| MilitaryEconomyTests | 38 | 13.2 | 478 |
| FreightOperationsTests | 20 | 11.9 | 108 |
| FieldConvoyTests | 21 | 11.8 | 145 |
| EngagementMissionTests | 21 | 11.8 | 140 |
| DelegationReturnRetryTests | 21 | 11.6 | 47 |
| CustodySuccessionTests | 11 | 11.5 | 83 |
| NpcDormancyTests | 3 | 11.0 | 524 |
| HouseFreightReceivingTests | 17 | 10.3 | 78 |
| StewardSuccessionTests | 61 | 10.2 | 110 |
| UnitCancellationRecoveryTests | 8 | 10.0 | 87 |
| DelegationMissionTests | 30 | 9.9 | 72 |
| DelegationInquiryTests | 22 | 9.8 | 44 |
| NpcContinuingTests | 18 | 9.8 | 416 |
| HouseSuccessionTests | 36 | 9.5 | 58 |
| RegionalBattleTests | 28 | 9.4 | 99 |
| CustodyFoodTests | 18 | 9.2 | 111 |
| CentralMediationTests | 12 | 8.5 | 188 |
| LandSurveyTests | 16 | 8.4 | 119 |
| NpcArmyTests | 52 | 8.1 | 52 |
| HouseLogisticsTests | 37 | 7.7 | 46 |
| RegionalSceneryTests | 11 | 7.3 | 104 |
| NewGameMilitaryTests | 28 | 7.1 | 51 |
| SparseCombatTests | 9 | 7.0 | 165 |
| HouseSalaryTests | 31 | 7.0 | 244 |
| NewGameWorkTests | 40 | 7.0 | 366 |
| RoadConvoyTests | 42 | 6.9 | 59 |
| NewGameCombatTests | 22 | 6.9 | 75 |
| CoalitionPactTests | 1 | 6.5 | 391 |
| DefectionPactTests | 16 | 6.2 | 159 |
| NpcFreightTests | 28 | 6.1 | 49 |
| PersonalLifeTests | 47 | 5.7 | 43 |
| FieldPresenceTests | 20 | 5.6 | 127 |

## 全国郡収入の追加測定（2026-11-13）

NationalDistrictRevenueTests：1件12分11秒。全国初期化・月境界・実購入・世代保存復旧を含むためHeavyへ分類。初回は既存ロード時停止の比較条件不足で失敗し、保存前停止を明示して修正。成功実績には数えない。
