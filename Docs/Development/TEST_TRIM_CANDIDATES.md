# 重い検査の見直しの候補（2026-11-18、まだ何も変えていない）

対象：11月16日の全件で1分を超えた146件（全体の時間の58%）。秒数は並べて流したときの値で、混み具合で膨らんでいる。
1件ずつ中身を読んで仕分けた。読んだのは Claude の手伝いの2つ（各73件）で、**検査を消す・変える前に、Claude が1件ずつ確かめ直す**。

## 仕分けと合計
| 仕分け | 意味 | 件数 | 秒 |
|---|---|---|---|
| 削る候補（重複） | ほかの検査ですでに確かめている | 2 | 2,351 |
| 準備を共有 | 同じ準備（世界を作る・軍を出す等）を検査ごとに作り直している。1回作って写して配る | 109 | 15,327 |
| 進め方が長すぎる | 時計を何か月も進める・くじが当たるまで回す・1秒刻みで何百回も写しを取る | 17 | 9,305 |
| 小さな世界で足りる | 全国の世界を作っているが、確かめているのは1〜2人の計算 | 4 | 1,082 |
| 残す | 全国の規模や長い進行でしか確かめられない | 14 | 4,044 |
| 計 | | 146 | 32,109 |

**消すのは「削る候補」の2件だけ。** 残りは検査の中身（確かめること）を変えずに、速くするだけ。

## 削る候補（2件）
| 秒 | 検査 | 理由 | 代わりに確かめている検査 |
|---|---|---|---|
| 1547 | NationalCourtTests.TheShogunPetitionsTheCourtThroughYamashinaInKyoto | 訪問・贈り物・取次の依頼・奏請の流れは地方の版で検査済み。全国に特有なのは開始時の取次と将軍の確認だけで、3日回す必要はない（その部分だけ残して短くする案もある） | CentralTitlesTests.cs:28-39、CentralMediationTests.cs:188-213 |
| 804 | NationalFactionRelationsTests.NationalStartStoresKnownDirectedVassalRelations… | 保存して戻し60秒進めて一致するかは NationalStartTests の移動の検査と重なる。従属関係の確認は作るだけで足りる | NationalStartTests.cs:55-63 |

## 全体に効く直し（大きい順）
1. **写しの照合を、全国の重い検査では切る。** 検査では手番ごとに世界のハッシュを取る照合（Tests/TurnSnapshotGuard.cs:9）が既定でオン。全国の世界ではこれが一番重い。上位7本（約7,300秒）が付けたまま。照合そのものは小さな世界の検査で確かめている。
2. **軍の前提「Away」を1か所にまとめて写して配る。** Away() が8つのファイルに同じ形で写して置かれ、70本以上（約8,000秒）が毎回作り直している。同じ形の共有は TreatyDefenseTests.cs:85、ActiveMilitarySuccessionTests.cs:48 にすでにある。
3. **全国の開始を一度だけ作って写して配る。** NationalStart.Create を検査ごとに呼んでいる。作った世界と、1時間進めた世界を1つずつ持てば、全国の検査の多くで作る時間が消える。
4. **待ちを縮める。** 収穫まで9か月進める・米を使い切るまで6か月進める・くじが当たるまで52か月回す、などは、その直前の状態を直接作るか、種を固定する。
5. **保存の読み込み先のためにもう1つ世界を作っている所（4か所）は、`new WorldSession(w)` で足りる。**

## 146件の一覧
| # | 秒 | 検査 | 仕分け | 理由 |
|---|---|---|---|---|
|1|1962|NationalDistrictRevenue.NationalPublicMoneyCanBuyFiniteLocalGoods…|長すぎ|照合を付けたまま全国を31日進める。照合を切り、月末の直前から始める|
|2|1547|NationalCourt.TheShogunPetitionsTheCourtThroughYamashinaInKyoto|削る候補|上の表|
|3|1537|BattleCaptivity.ThePlayerAndAnEnemyRetainerCapturedInOneBattle…|長すぎ|1秒刻みで最大900回、毎回写しを2つ。まとめて進めて後から探す|
|4|1476|PersonalHealth.IllNpcDoesNotChooseBlockedPractice|長すぎ|300の種を探し、回復直前まで数週間進める。種を固定する|
|5|1214|SkirmishPassing.ArmiesThatPassedEachOtherBetweenChecksStillMeet|共有|敵対する組を探すたびに全国の世界を丸ごと写す（#7 と同じ探し方）|
|6|1110|NationalTerritory.NearbyHeadsWorkTheirLand…|共有|全国の開始を毎時の区切りまで進める部分が #15・#17・#19 と同じ。照合付き|
|7|940|NpcSkirmish.HostileNpcArmiesThatMeetFight…|共有|#5 と同じ|
|8|821|NationalSystems.LifeAndSocialSystemsRun…|残す|多くの仕組みが同じ未来に戻るかは全国が要る。照合を切って短くできる|
|9|804|NationalFactionRelations.NationalStartStoresKnownDirectedVassalRelations…|削る候補|上の表|
|10|669|NpcPurpose.ActualCustodyPreventsDiscretionaryWork|共有|#34 と同じ人質の前提|
|11|669|HealthMortality.ACriticalIllnessKills…|長すぎ|400の種×全員の病を探し、危篤まで進める|
|12|664|PersonalHealth.FeverBlocksPracticeUntilRecovery…|長すぎ|#4 と同じ探し方|
|13|611|RegionalDomain.NpcHungerProcurement…|長すぎ|小さな世界を3年8か月進める。飢えの状態を直接作る|
|14|594|StrategyRealArmy.ANearbyCampaignMarches…|残す|全国の戦略で本物の軍が出陣して決着するまでの受け入れ|
|15|585|ParallelRoutine.ParallelRoutineGivesTheSameWorld…|残す|並べた計算と1人ずつの計算の一致は全国の人数が要る。1時間を10分に縮められる|
|16|557|HouseCondition.LocalRevenueEntitlement…|長すぎ|収穫期に入るためだけに9か月進める|
|17|556|NpcTeacher.NpcsLearnFromTheMostSkilled…|共有|全国を約40分進めた世界は #6・#15 の途中と同じ|
|18|535|NationalTruceRound.ATruceLetterTravels…|残す|全国で書状の往復を一巡|
|19|531|NationalStart.NobunagaCanStartTheNationalWorld|共有|作った直後の確認が主。最初の1分は #22 と重なる|
|20|503|Skills32.TheNationalStartUses32Skills…|小さな世界|技能の曲線と独習は小さな世界で足りる|
|21|495|BattleCaptivity.ThePlayerRoutedWhileWithdrawing…|長すぎ|#3 と同じ|
|22|492|NationalStart.NobunagaTravels…|残す|全国の道の移動と途中の保存から同じ未来、の代表。照合を切れる|
|23|432|LearnFromTeacher.LearningFromAMoreSkilled…|小さな世界|同じ拠点の2人で足りる|
|24|393|UnitDirectiveRetry.DeadlineCannotBeExtended…|共有|Away の前提を作り直し|
|25|340|UnitDirectiveCancelHolding.RetainedCancellation…|共有|Away の前提|
|26|319|MilitaryEconomy.NpcCannotMusterWithoutRice…|長すぎ|米を使い切るためだけに6か月。米0を直接作る|
|27|237|NationalWarfareActivation.RecordingNationalNpcDecisions…|残す|全国の60秒を2回、すでに軽い|
|28|200|UnitDirectiveCancellationBoundary.FutureEffectNotice…|共有|Away の前提|
|29|193|CastleSiege.StormAllDistricts…|残す|区画ごとに60秒ずつ。縮めにくい|
|30|192|HouseholdMilitary.DeployedCaptainSigns…|共有|AppointCaptain の前提を3本が作り直し|
|31|190|UnitDirectiveCancellation.FutureCancellationWaits…|共有|Away の前提|
|32|168|NewGameWork.ExhaustedPayerCannotMintCoins|長すぎ|作業を51回繰り返して銭を使い切る。残りが少ない所から|
|33|167|NpcDormancy.WakingDoesNotDependOnStepSize…|残す|2日を細かさを変えて3通り。起きた直後までに縮められる|
|34|155|NpcContinuing.HeldPersonCannotInspect…|共有|#10 と同じ前提|
|35〜45の一部（9件）|121〜155|UnitDirectiveHolding.RealOriginalAndCopy…|共有|Away・PersonOffCamp・FailedOriginal を作り直し|
|36,39|154/144|RegionalDomain.ActualGenerationRecovery…|共有|同じ前置き（約30日）を作り直し|
|38|145|HouseSalary.StandingCompanyPays…|長すぎ|給金日2回ぶん数か月|
|40|143|NationalNameHistory.NationalNameChanges…|共有|重いのは全国の世界を作ることだけ|
|41|143|RepeatedExercise.SecondRoutAndRegroup…|共有|Authorized() を11本が作り直し|
|43|141|CoalitionPact.InvitesDifferentHomes…|長すぎ|多くの分かれ道を流して36日まで|
|46,49,61|103〜121|UnitDirectiveCancelRetry.…|共有|Away の前提|
|47|118|ClinicalHealth.DelayedTreatment…|長すぎ|100の種を探して発症まで|
|48|118|NationalStrategy.AStrongVassalLeaves…|長すぎ|月10%のくじが当たるまで最大52か月。くじを固定|
|50|111|HouseCondition.ConsultationSurvivesTransit…|長すぎ|月ごとの記録を見るためだけに32日|
|51|108|HouseCondition.AgreedTransfer…|長すぎ|引き換えの場合は収穫まで進める|
|52|107|NationalPersonEstimate.NationalStartUsesEstimates…|共有|全国の世界を2回作る|
|53,59,67|99〜107|UnitDirectiveRetry.PhysicalKnownFailure…|共有|Away の前提|
|54|107|UnitDirectiveCancellationBoundary.ReturnedAuthority…|共有|Away の前提|
|55|106|PublicWorks.CompletedAgricultureAffectsAutumnHarvest|長すぎ|小さな世界2つを9か月ずつ|
|56,65|100/106|UnitDirectiveCancellation.SameTickCancellation…|共有|Away の前提|
|57,62,66|99〜104|UnitRecipient.…|共有|Away の前提を13本が作り直し|
|58|104|UnitDirectiveRetry.CopyDoesNotRenew…|共有|Away の前提|
|60|103|NpcCaptor.AnNpcCaptorDemandsRansom…|共有|1本で全国の世界を3回作る|
|63|101|RemoteMilitarySuccession.GenerationRecovery…|共有|12の段階が Ready() を作り直し|
|64,69|96/100|RoutRegroup.SaveRestorationPreserves…|共有|Contact() を作り直し|
|68,70,71|95〜98|UnitDirectiveCancelHolding.InvalidCancellationPaper…|共有|同じ前提を8通り|
|72|95|UnitDirectiveCancellation.SavedCancellationEffectTampering|共有|Away の前提|
|73|95|ActiveMilitarySuccession.ActualFormationReturn…|共有|Holding() を作り直し|
|74〜78,85,87|89〜95|UnitDirectiveCancelHolding.InvalidCancellationPaperReferenceIsAtomic ほか|共有|Away+MissedCancel+HomeUnits を8組が作り直し|
|79|93|RemoteMilitarySuccession.GenerationRecoveryPreservesOfferAcceptance…|共有|Ready() を12組が作り直し、読み込み先にも世界を作る|
|80|92|NationalStrategy.HousesDecideCampaignsMonthly…|残す|全国の月初めの出陣判断。#106 と準備を共有できる|
|81|92|UnitDirectiveHolding.SavedRetainedHolderTamperingIsRejected|共有|Away の前提|
|82,88,89,92,102,103|86〜92|UnitDirectiveRetry.ForgedCopyReference… ほか|共有|Away+FailedOriginal+HomeUnits|
|83,115,120,139|64〜91|UnitDirectiveHolding.RealOriginalAndCopyExecuteOneRoot…|共有|9組が作り直し|
|84,93,94,99|87〜90|RoutRegroup.SaveRestorationPreservesRoutAndRegroupFuture|共有|6組とも150秒×2。1本の途中で写しを取れば1回|
|86|90|ActiveMilitarySuccession.ActualFormationReturnReport…|共有|Holding を組ごとに作り直し|
|90|89|UnitDirectiveCancellation.SameTickCancellationSuppresses…|共有|Away+FailedOriginal|
|91|88|HouseCondition.AgreedTransferStillRequiresAnOrder…|共有（待ち）|収穫まで数か月。収穫後の世界を使い回す|
|95,96,104,107,110,112,117,141|63〜87|UnitDirective.PhysicalReceiptMovesOnlyTheSelectedUnit|共有|9組とも Away を作り直し|
|97,130,135|65〜87|MilitaryRecognition.…|共有|Away の写しで足りる|
|98|87|NationalStrategy.AFallenHouseIsRecorded…|共有|全国の世界の作成を共有すれば大半が消える|
|100,111,113,114|79〜87|ActiveMilitarySuccession.ActualGenerationRecovery…|共有|作成済みの世界があるのに、保存の読み込み先でもう1つ作る|
|101|87|UnitDirectiveRetry.FailureIsUnavailableUntilTheCourierPhysicallyReportsIt|共有|Away の写しで足りる|
|105,119,140|64〜86|UnitDirectiveCancelRetry.InvalidCancellationRetryIsAtomic|共有|8組とも作り直し|
|106|85|NationalStrategy.DistantBattlesUseTheContactRules…|共有|#80 と同じ準備|
|108,123,127,131|65〜84|UnitRecipient.…|共有|Away の写しで足りる|
|109|83|NpcHouseCare.TheBestHealerOfTheHouse…|小さな世界|時間を進めない2人の計算|
|116|76|UnitDirectiveCancelRetry.ThreeRealMissedAttempts…|共有|Away の部分を共有|
|118,121|73/74|HouseCondition.NewEstatePaysOnlyCollectionsAfterExchange|共有（待ち）|最大100の種で世界を作って探し、収穫まで。種を固定|
|122,145|61/72|UnitDirectiveCancelHolding.…|共有|Away の部分を共有|
|124|70|UnitDirectiveCancelRetry.OriginalReceiptDeadlineCannotBeExtendedByRetry|共有|Away+MissedCancel|
|125|69|CentralMediation.PetitionRequiresMeeting…|残す（待ち）|2か月の待ちを元と写しの両方で。写しを授与の直前に取れば半分|
|126,132|65/69|KnownMilitaryTarget.…|共有|Away の写しで足りる|
|128,137|65/68|StandingAdministration.…|共有|30日と32日の進行が重なる|
|129|66|TreatyDefense.LongerSortieFunds…|残す|派兵と帰還の全体で米の返却を見る|
|133|65|NationalControlProjection.DeliveredButSealedCapture…|共有|全国の世界を1つ共有すればよい|
|134|65|HouseCondition.CrossHouseDispatchAfterLocalEnactment…|残す（待ち）|種を最大32個試す。通る種を固定|
|136|65|MilitaryEconomy.SeasonalLossRemainsAfterReturn…|残す（待ち）|比較用の世界でも9か月進めている。1回で使い回す|
|138|64|WorkExperience.TheOrdererGainsExperience…|小さな世界|1人・1拠点で足りる|
|142|63|UnitCommandHolding.ActualUnitAbsentRejectsLateOriginal…|共有|Failed() を十数件が作り直し|
|143|63|LandSurvey.TaxStartsAfterPostedEffectiveDate…|残す（待ち）|秋の査定まで9か月。秋の直前から始められる|
|144,146|60/63|UnitDirectiveCancellationBoundary.…|共有|Away の写しで足りる|

※ 番号をまとめた行は、同じ検査の引数違い（Theory の組）。
