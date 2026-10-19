# 共通データ・状態遷移・保存仕様 詳細設計V1

ユーザーが「1を推奨で決めてhtmlで」と委任。共通契約の推奨判断24件を採用。実装の完了ではない。

DD-01の共通境界とDD-05/08の保存・時間との接続を確定。全制度個別の遷移・API実装や性能達成は対象外。

## CC-01 正本と更新単位

**採用**：定義データ・実行状態・人物別表示を分離する。WorldSessionが唯一の更新入口となり、各制度のサービスは同じ状態へ変更案を返す。徴税を世界全体の親にしない。

**データ**：DefinitionPack / WorldSession / ActorView。各集約はId・Revision、SessionはCommitSequenceを持つ。

**境界**：人物・組織・土地・部隊・案件・情報・物流を別集約とし、複数集約に及ぶ一つの業務効果は一括コミット。GodotオブジェクトをCoreへ保存しない。

**既存**：独立CoreとTaxSimulation内の複製→検査→公開方式を保持。

**変更**：TaxSnapshot中心の所有関係をWorldSessionへ段階移管。既存試作入口はそのまま保持。

**normal**：米の所有移転と徴税実績が同じコミットで確定する。

**failure**：検査失敗なら台帳・乱数・イベントを含め全部不変。

## CC-02 ID・版・同一性

**採用**：人物は失職・改名・死亡・所属変更でも同じID。要求・命令本文・配送試行・効果・報告は別IDにする。

**データ**：EntityId: namespace:key（ASCII、1〜128字）。WorldId / DefinitionId / RequestId / MessageId / AttemptId / EffectId / CorrelationId / Revision。

**境界**：シナリオIDは固定文字列、動的IDは保存済みの名前空間別連番。表示名・配列位置・現実時刻・乱数抽選からIDを作らない。序数比較、再利用禁止。初回受付でCoreが固定するCreatedAtを含む全不変業務字段をハッシュに含める。同じ要求の再提出は保存済みのCreatedAtを使い、現在時刻を再付与しない。

**既存**：文字列IDと要求ID再利用チェックを保持。

**変更**：型付きID、世界ID、集約版、保存される連番を追加。MessageFlow.SendのCreatedAt差の許容を製品契約へ持ち込まず、再送は専用操作とする。

**normal**：同じ要求ID・同じ本文の再提出で同じ受付結果を返す。

**failure**：同じIDで量・期限・宛先・作成時刻が違えばID_PAYLOAD_CONFLICT、既存本文は不変。

## CC-03 人物・所属・主従・権利

**採用**：所属、直属主君、役職、忠誠、家督、指揮権、土地権利を別の期間付き関係として保持する。

**データ**：Character{Id,LifeState,AppearanceId} / Membership / ServiceBond / OfficeAssignment / SuccessionCase / LandRight。関係はSource・ValidFrom・ValidUntil・DisputeIdを持つ。

**境界**：不在や浪人は関連なしで表す。死亡は墓石レコードを残し参照を維持。兼任は許すが職務範囲と拘束を検査。独立宣言では関係を一括移転しない。

**既存**：人物定義と変更可能な所属の分離を保持。

**変更**：Character.OrganizationId・Role・CastleIdだけで全制度を表さない。Castle.LordIdは役職参照へ移し、実効支配とは別。

**normal**：城主交代後も、商人所有の倉庫の米と旧主家への債務は残る。

**failure**：独立宣言だけで配下・城門・条約・土地を全部取得しない。

## CC-04 所在・座標・移動

**採用**：人物の物理所在と組織上の所属先を分ける。全国・近景は同じ座標系の同じ物を表示する。

**データ**：Location=AtSite{SiteId,AnchorId} | Traveling{RouteId,SegmentIndex,ProgressMm} | AtPosition{LatE7,LonE7,HeightMm,TerrainRevision}。視覚位置は派生値。

**境界**：緯度経度はWGS84整数、標高の基準は地形マニフェストに固定。距離は整数mm。経路途中は所属城へ丸めない。拘束・搭乗は所在とは別関係で保持。実投影・通行容量はDD-02。

**既存**：MapCellと部隊経路の進捗保持を試作として保持。

**変更**：11×7の架空MapCellを日本の実座標へ無根拠に変換しない。地形・経路版を新設。

**normal**：全国と近景から同じ橋ID・同じ経路進捗が得られる。

**failure**：橋の描画を動かしてもCoreの所在・敵の知識は変化しない。

## CC-05 世界時刻・期間境界

**採用**：製品の時刻は開始暦日に対する経過秒Int64。全制度の判定時刻は一つで、倍速は現実時間から進める量だけを変える。

**データ**：SimTime{ElapsedSeconds} / EpochDate / TimeDomain=GameSecondsV1。日時区間は[From,Until)、DeadlineはAt以下を有効とする。

**境界**：時刻演算はchecked。1・10・60・360倍を初期値。先発グレゴリオ暦・史料日付対応表を分ける。加速は次イベント境界で刻み、通知S停止の境界を越えない。余剰の現実時間は停止解除後に勝手に消化しない。

**既存**：共通Tickと停止の一本化を保持。

**変更**：旧Tickを秒と読み替えない。Godotの表示位相・実時間アキュムレータをゲーム時刻と分ける。

**normal**：期限Tで受領すれば期限内、権限Until=TはTで失効する。

**failure**：360倍でも途中の到達・取消・戦闘を飛ばさない。

## CC-06 権限・代理・服従

**採用**：行為の正当性と人物が知る権限、従う意思を別に判定する。肩書だけから権限を作らない。

**データ**：Grant{Id,PrincipalId,AgentId,ActionKinds,Scope,Limits,BasisRef,ValidFrom,ValidUntil,Redelegate,RevocationRef}。DecisionReasonは真実用と本人認識用を分ける。

**境界**：発送・正式受領・効力時・段階実行時に当該行為の権限を再検査。既に合法に発生した独立効果は遡及消去しない。受領・開封・回答・執行・再委任は別権限。複数根拠が衝突すれば制度裁定へ。

**既存**：Authority・ProxyPermission・MilitaryGrantの明示根拠と半開期間を保持。

**変更**：共通Grantへの変換器を用意し、単なる文字列Roleからの許可を禁止。服従判断をGrantから分離。

**normal**：受領代理は有効でも開封不可なら、本文知識は本人にも代理にも生じない。

**failure**：未知の解任で履行できない場合、NPC向け理由に秘密の解任理由を漏らさない。

## CC-07 原本・配送・再送

**採用**：論理本文は不変、配送試行だけを増やす。紛失と不達の報告も実際に届く必要がある。

**データ**：Message{Id,BodyType,BodyVersion,CanonicalPayload,PayloadHash,Sender,Recipient,Scope,CreatedAt,DeadlinePolicy} / Attempt{Id,Number,Route,Status,DueAt} / Receipt。

**境界**：Receiptは正式受領であり読了ではない。Lostは発送者に自動公開しない。遅着原本は同じMessageIdに到達事実だけ追加。再送上限はルール定義に保存し、初期値3（原本含む）。既知の失敗・権限・期限・未受領を満たすとき再送可能。

**既存**：MessageFlowの配送・失敗返報・代理・遅着構造を活かす。

**変更**：重要書状は本人受領でも自動開封しない。再送回数を固定定数から版付きルールへ。

**normal**：原本と再送が同時に届いても正式受領と業務効果は一回。

**failure**：失敗返報が不達なら発送者の「配送失敗を知る」状態は進まない。

## CC-08 事実・証拠・本人認識

**採用**：真実、観測・報告、組織保管、本人認識を別レコードにする。訂正は証拠を上書きせず追加する。

**データ**：FactId / Evidence{Id,Claim,ObservedAt,RecordedAt,Source,MessageId,Supersedes} / Knowledge{CharacterId,EvidenceId,LearnedAt,Confidence} / CustodyAccess。

**境界**：ObservedAt≦RecordedAt≦LearnedAtを基本とし、伝聞の観測不明はnull。信用値は出典と制度ルールによる。遅着した古い報告で新しい観測を若返らせない。死亡者の秘密は操作後継へ一括移さない。

**既存**：PersonalKnowledgeとMilitaryIntelのObservedAt・LearnedAt・出典を保持。

**変更**：税知識・軍情報を共通Evidenceへ接続。機械的に一つの真実を全員へ配布しない。

**normal**：偽報を信じる本人と誤りを知る別人物の表示が異なる。

**failure**：後継へ切替えても未開封密書の本文を閲覧できない。

## CC-09 要求受付・命令成立・差替え

**採用**：UIとNPCは同じCommandGatewayへ意図を提出する。受付成功は発送・到達・受諾・履行成功とは違う。

**データ**：Submit{WorldId,RequestId,ActorId,ExpectedRevision,CommandKind,BodyVersion,Payload} / Accepted{RequestId,CommitSequence}。Cancel・Supersedeは新RequestId＋TargetId。

**境界**：同一IDの重複照合を新規版チェックより先に行い、完全一致なら元結果を返す。別本文なら拒否。新規要求の古いRevisionはSTATE_CHANGED。運送再試行・UI再提出・新命令の差替えを区別する。複数部隊の計画作成は全件検査後一括発送、到達後の服従は個別。

**既存**：Submitの一意性とFormationPlanから各命令への分解を保持。

**変更**：汎用Gatewayと非例外の業務結果を追加。線の変更は旧計画の書換えではなく明示差替え。

**normal**：同じ提出の通信再試行では計画も返書も増えない。

**failure**：三部隊の一つに発送権限がなければ計画発送自体は未成立。既発送後の一部拒否とは区別。

## CC-10 実行行動と拘束

**採用**：人物の主行動、補助行動、委任案件を別管理し、場所・移動・負傷・注意資源に基づく同時可否をルール表で判定する。

**データ**：ActionExecution{Id,ActorId,TaskId,Mode,LocationRequirement,Locks,Progress,Phase} / OccupancyLock{Actor,Channel,From,Until,Source}。

**境界**：進捗は整数作業単位。行為ごとにPresence・Travel・Attention・Commandの使用量を宣言。主行動は原則1、補助は容量内。委任先が実行主体になり、依頼者の場所を代行しない。引継ぎは進捗・責任・引当の移管取引。

**既存**：既存移動・交戦のphaseは制度内実行として保持。

**変更**：人物拘束・同時可否を追加。具体的所要時間・病傷係数はDD-03/05へ。

**normal**：行軍中に許可された書状処理はできるが、城内普請の現地監督はできない。

**failure**：引継ぎ失敗で元担当と新担当が同じ進捗を二重計上しない。

## CC-11 任務・履行・報告・評価

**採用**：一つの巨大Statusにまとめず、成立・履行・報告・評価を直交する状態軸に分ける。

**データ**：Task{Id,Kind=BindingOrder|VoluntaryRequest,Purpose,Issuer,Executor,Constraints,DeadlinePolicy,CostLimit,ReportPolicy} / Agreement / Execution / Report / Assessment。

**境界**：命令は正当な到達・必要認識を受けて履行判断、任意依頼は受諾合意で成立。Execution=NotStarted|Waiting|Running|Suspended|PartiallyCompleted|Completed|Stopped|Failed。部分達成量は終端後も保持。報告不達でも達成の真実は残る。

**既存**：TaxOrder・MilitaryOrderの制度状態を互換アダプタで参照。

**変更**：共通状態軸と制度別遷移表を新設。軍令すべてを任意契約の承諾待ちに変えない。

**normal**：徴収完了・返報紛失ならExecutionはCompleted、ReportはInTransitまたはLost、主君の評価は未確定。

**failure**：期限超過で部分成果を消してNotStartedへ戻さない。

## CC-12 期限・取消・効力

**採用**：全期限に起点・対象段階・発送／正式受領／開封の判定・最終失効を明記する。取消を送っただけでは止まらない。

**データ**：DeadlinePolicy{Trigger,DurationSeconds,AbsoluteLatestAt,Criterion} / Cancellation{Id,TargetId,RequestedEffectiveAt,ReceiptAt,KnownAt,Outcome}。

**境界**：起点が開封でも最終失効日時を必須。効果は必要な受領・本人認識または執行代理認識・指定時刻・権限を全て満たす時点以降。完了済み効果は巻き戻さずTooLate、未使用引当のみ解放。権限終了は半開、期限ちょうどは有効。

**既存**：取消到達・AwaitingEffect・TooLateと期限停止を保持。

**変更**：取消本文の対象・委任範囲を共通契約化。権限によって代理執行を選べるが受領代理から自動昇格しない。

**normal**：Tに期限内原本と有効な明示取消が認識され、同じ未着手効果へ競合すれば取消が抑止する。

**failure**：T以前に完了した徴税はTの取消で返金されず、必要なら別補償案件へ。

## CC-13 同時刻・イベント競合

**採用**：時刻順の候補収集→因果依存→同一対象の明示取消／差替え→制度別競合裁定→効果コミットの順に解く。登録順を優先権にしない。

**データ**：ScheduledEvent{Id,At,DependsOn,Reads,Writes,ConflictKey,RuleId,Payload} / ConflictDecision{Id,Inputs,RuleVersion,Result,Reason}。

**境界**：Tに権限失効・到達・認識・効力があればT時点の条件を使う。期限Tの到達を処理してから未達を失効させる。取消の優先は明示対象と未確定効果だけ。無関係な案件全てに取消優先の大域順を課さない。同格の排他命令は安全維持・照会。依存循環は不正として隔離、時刻を進めて隠さない。

**既存**：EventSchedulerの依存表現を保持。Idソートは独立事象の安定列挙だけに限定。

**変更**：書込集合・制度裁定・循環検査・同時刻閉包を追加。Tで生成する同時刻事象は有界の因果子だけ、未来の処理はT+1以降。

**normal**：同時刻のイベント登録順を逆にしても、競合裁定と最終状態が一致する。

**failure**：満たせない依存が残ればSCHEDULER_INVARIANTでトランザクション全体を不成立にする。

## CC-14 所有・保管・引当・輸送

**採用**：物資ロットと債権、所有権・処分権・保管・占有・引当を別に保持する。輸送中も資源の所在を持つ。

**データ**：ResourceLot{Id,Kind,QuantityBaseUnit,Owner,Holder,Location,Quality} / Reservation{Id,LotId,Amount,TaskId,State} / Transfer / Debt。米・木材などは定義済み整数基準単位、貨幣は勘定単位。

**境界**：未使用引当合計≦可処分量。分割は親ロットと対応量を記録。所有移転・出庫・輸送・消費は異なる効果。既所有・輸送・予約を保護後、法的根拠→緊急救命防衛→既契約期限→通常任務。同順位は分割比例、不可分同条件だけ保存抽選。違反転用は物理上の可能性・責任・補償を記録。

**既存**：RiceAccountと徴収時保存量の検査を活かす。

**変更**：汎用ロット・部分引当・搬送・収支台帳へ拡張。画面の合計残高を正本にしない。

**normal**：100のうち40消費して中止なら60だけ引当解放、40は実績に残る。

**failure**：落城で倉庫を占有しても第三者の米の所有者は勝手に変わらない。

## CC-15 効果・冪等性・補償

**採用**：効果IDと業務上の一意キーを両方保持し、効果・台帳・進捗・返報送信予定を同じコミットへ含める。

**データ**：Effect{Id,OperationId,StepId,Revision,Occurrence,PayloadHash,AppliedAt} / LedgerEntry / Outbox / Compensation{Id,OriginalEffectId,Outstanding,Status}。

**境界**：同一キー同一内容は適用済み結果、別内容はエラー。正当な繰り返し消費はOccurrenceを増やし、同じ刻を二度適用しない。効果の記録前後で別々に保存しない。Outboxは通知・返報生成を再開できる。補償失敗はPendingのまま、元履歴を消さない。

**既存**：EffectLedgerとApplied配列の一度だけという原則を保持。

**変更**：単なるstring配列から内容ハッシュ・業務キー・外向き効果の台帳へ。履歴削減でも処理済みキーは失わない。

**normal**：効果確定後・返報生成前の復旧でも出金は一回、返報は同じIDで作成できる。

**failure**：返金失敗後の再試行で、成功した他の返金を二重に払わない。

## CC-16 乱数と再現性

**採用**：同じ世界・定義版・入力順・規則版なら同じ結果。判断の再表示・保存・再読込で抽選しない。

**データ**：RandomStream{PurposeId,Algorithm=lcg64-v1,StateDecimal,DrawCount} / DrawRecord{DecisionId,StreamId,Before,Result,After}。

**境界**：既存LCGの定数を保持。新世界の流れ別初期値はSeedをuint64 little-endian、PurposeIdのUTF-8 byte長をuint32 little-endian、そのUTF-8 bytesの順で連結しSHA-256の先頭8byteをlittle-endianで読む（全0も許容）。抽選範囲は棄却法。試作移行は既存Stateをlegacy-mainへ保存、勝手に再seedしない。描画乱数は別でCoreへ影響しない。

**既存**：SeededRandomの明示アルゴリズムと状態を保持。

**変更**：用途別ストリームと抽選記録を追加。旧系の決定性は旧ルール内で保持し、新旧が同じ戦果になるとは保証しない。

**normal**：保存前後の次の抽選値と判定結果が一致する。

**failure**：UIを何度開き直してもNPC判断の抽選回数が増えない。

## CC-17 表示・NPC・秘密の境界

**採用**：画面とNPCはActorViewだけを判断材料に使い、真実ストアは整合判定と物理処理専用にする。

**データ**：ActorView{WorldRevision,ObserverId,KnownAt,KnownTasks,KnownEntities,EvidenceRefs,AllowedIntentHints}。Scopeは本人・組織閲覧根拠付き。

**境界**：未知対象は行を返さない、古い報告は観測時刻を付ける。真の権限不足と本人が知る理由は分ける。自由カメラも同じ表示フィルタ。キャッシュはRevision・Observer・閲覧根拠版をキーにし、失効時に破棄。

**既存**：KnowledgeFor・MilitaryViewForの本人別読み出しを保持。

**変更**：他制度も同じView境界へ接続。NPCへWorldSessionの可変状態を渡さない。

**normal**：同じ部隊を自軍・敵軍・古い報告だけの人物で別表示できる。

**failure**：秘密の敵位置をクリックしても存在確認になる詳細エラーを返さない。

## CC-18 公開API・エラー

**採用**：業務上の拒否は型付き結果、データ破損・プログラム不整合は障害として分ける。内部理由と人物に見せる理由を別に返す。

**データ**：Submit / AdvanceTo / GetActorView / RequestSave / PrepareLoad / CommitLoad。Result{Code,RequestId,VisibleReason,EvidenceRefs,RetryHint,CommitSequence}。

**境界**：Code=ACCEPTED|ALREADY_ACCEPTED|STATE_CHANGED|INVALID_INPUT|ID_PAYLOAD_CONFLICT|NOT_AVAILABLE|CONFLICT_WAIT|MIGRATION_REQUIRED|UNSUPPORTED_VERSION|CORRUPT_SAVE|DEPENDENCY_MISMATCH|IO_FAILURE|SCHEDULER_INVARIANT。制限なしの管理画面を通常プレイAPIへ混ぜない。時刻・数量オーバーフローは入力拒否または内部不整合で、飽和値にしない。

**既存**：例外による内部検査は残し、境界で分類する。

**変更**：非公開権限・対象の存在を漏らす例外文字列をそのままGodotへ表示しない。

**normal**：古い画面から新規操作を出すとSTATE_CHANGED、世界は不変。

**failure**：不明な人物と未開示人物への不正照会は同じNOT_AVAILABLEの公開応答。

## CC-19 保存内容と形式

**採用**：製品保存は既存TaxSnapshotのVersion6ではなく、独立したgame-world-save/1形式にする。正本は検証可能なUTF-8 JSON、圧縮は初期導入しない。

**データ**：Envelope{Format,EnvelopeVersion,WorldId,GenerationId,CommitSequence,TimeDomain,SchemaVersions,Ruleset,DefinitionManifest,PayloadHash,PayloadByteLength}＋Payload。

**境界**：Payloadには全集約・未処理・処理済みキー・乱数・配送・知識・進捗・補償待ち・IDカウンタ・必要な表示位相を含む。Godotノード、キャッシュ、派生残高は保存しない。UIカメラ・音量等は別Presentationだが時間の連続に必要な位相はWorldと同じ世代へ。Load直後は常に全体停止。

**既存**：SnapshotCodecのDTOと未知フィールド拒否方針を保持。

**変更**：Envelope・マニフェスト・型ごとの版・必須項目存在検査を追加。未知enum・重複JSONキーも拒否。SHAは破損検出であって改ざん防止ではない。

**normal**：配送中・交戦中・部分実行中から、同じ経過と本人認識へ復旧する。

**failure**：必要なAppliedや乱数が欠ける保存を既定値で補って開かない。

## CC-20 ファイル保存の確定手順

**採用**：スロットごとに不変の世代ディレクトリを作り、検査済み世代への小さな参照だけを最後に公開する。現行正常世代を先に壊さない。

**データ**：slot/{generation}/payload.json・manifest.json・presentation.json、current.refとprevious.ref、保存中一意tmp。世代IDはCommitSequence＋保存連番。

**境界**：同じローカルボリュームで書込→flush要求→再読込SHA/長さ/参照検査→世代公開→previous/current参照切替。Windowsの置換に失敗したら保存失敗とし現行参照を保持。スロットは単一writer。ゲーム操作は止めず確定Revisionのスナップショットを背景保存し、完了時刻を明示。初期は最新＋過去3正常世代を保持、未検証世代や唯一の正常保存を自動掃除しない。 新規スロットの初回公開ではpreviousなしを許す。世代参照は世代ID・マニフェストSHA付きの二重参照レコードで検証し、切替途中の不一致は復旧候補として提示する。

**既存**：現行SaveFileの一時ファイル→置換を出発点にする。

**変更**：現行実装には明示flush・バックアップ世代・整合した参照切替がない。異常終了・電源断耐性は実機障害試験を通るまで保証しない。クラウド同期やネットワークフォルダは初期保証外。

**normal**：保存失敗後もcurrentかpreviousの正常世代を選んで開ける。

**failure**：書込途中に落ちても部分JSONを正常保存として表示しない。

## CC-21 読込・破損・不足依存

**採用**：読み込む前に隔離領域で全検査し、成功後に一度だけ現在セッションを置換する。失敗時は現在のゲームを継続できる。

**データ**：LoadCandidate{Header,VerifiedPayload,DependencyResolution,MigrationReport,ValidationResult} / LoadCommit。

**境界**：サイズ上限は初期256MiB、JSON深度64、UTF-8厳密・参照パスは許可ルート内の相対パスのみ。Hash→形式版→必須→定義依存→参照→時刻因果→資源保存→知識出典→効果一意→乱数・カウンタの順に検査。ゲーム定義SHA違いは拒否。表示だけの素材不足は同じAppearanceIdの代替を明示し能力を変えない。 LoadCandidateは一回限り・候補ハッシュ付きとし、元ファイル更新時は再PrepareLoad。64bit整数・時刻・数量の範囲、保存内の同一時計、終了済みイベントの再待機、参照循環も検査する。

**既存**：LoadJsonの隔離生成とValidateを保持。

**変更**：正常世代へ黙って巻き戻さず、破損と復旧可能時刻を表示してユーザーに世代選択を求める。指定Loadの成否を偽装しない。

**normal**：不正保存のLoad後も、開いていたセッションの時刻・残高・乱数が変わらない。

**failure**：将来版・不足ルール・破損出典は拒否し、未知項目を捨てて部分復元しない。

## CC-22 旧セーブと明示移行

**採用**：旧税Version1〜5と旧軍事fixtureは旧入口で読める状態を維持し、製品ローダーへ直接混ぜない。製品移行は適用対象を登録したオフライン変換だけにする。

**データ**：MigrationRecipe{FromFormat,FromRuleset,ToFormat,ToRuleset,TimeMapping,LocationMapping,IdentityMap,AllowedDefaults,Preconditions} / MigrationReport{InputHash,RecipeHash,Warnings,OutputHash}。

**境界**：旧Tickの実時間換算を示す正当な定義が今はないため、既存試作からGameSecondsV1への移行は現段階では非対応と決める。勝手な秒数係数・1560年配置・欠落知識の推定はしない。将来レシピは全時刻・期限・権限・遅着・進捗へ同じ変換を適用し、変換後検査する。元保存上書き禁止、新スロットへ。

**既存**：旧1〜5の読込・移行処理とfixturesは変更しない。

**変更**：製品保存の混用拒否とレシピ登録境界を追加。旧試作の保存を捨てず、旧ルールでの再現性を残す。

**normal**：旧保存は旧入口で継続でき、製品入口ではMIGRATION_REQUIREDを返す。

**failure**：適用レシピがないのに「移行成功」としてTickをSecondsへコピーしない。

## CC-23 更新・同時実行・検査

**採用**：シミュレーションは単一writer、読取は不変スナップショット。ユーザー操作・NPC・イベント・保存復旧が同じコミット規約を通る。

**データ**：Transaction{BaseRevision,ReadSet,WriteSet,ProposedEffects,Outbox,RngChanges,IdChanges}。Commit=validate→publish。

**境界**：一つの因果的業務単位を原子化し、全日分を巨大取引にしない。ただし同時刻の競合集合はまとめて裁定。初期実装は現行の全体複製を維持、計測後にcopy-on-writeへ差替え可。非同期保存が古い世代を書き終えても新しいcurrentを巻き戻さない。制御されたローカル入力順も記録する。

**既存**：SessionTransaction.ApplyとCore非依存性を再利用。

**変更**：公開後のDTO書換えを禁止し、集約不変条件と失敗注入点を追加。性能のために整合検査を削らない。

**normal**：保存中に次の操作をしても、保存は一貫した一つのRevisionから作られる。

**failure**：同じRevisionの競合書込みを二つとも公開しない。

## CC-24 導入順と完了判定

**採用**：一括置換を避け、現行試作を動かしたまま互換層→共通Gateway→各制度→新保存の順に導入する。今回の採用は共通契約の詳細設計であり、全制度の詳細設計完了ではない。

**データ**：Gate G1:型・定義・検査、G2:時刻/権限/表示、G3:命令・案件・資源/効果、G4:保存/復旧/移行拒否、G5:平時の一巡。

**境界**：今回の48受入例を実装後の試験へ割当。既存317試験は継続、回帰結果を新機能の成功証拠に転用しない。制度ごとの合法性・係数・地理実装・史料採録は各詳細設計で固める。APIや保存項目の追加は契約版と影響範囲・再検査を記録。

**既存**：既存CoreとGodotの分離・試作入口・fixturesを保持。

**変更**：新実装の保存形式を既存のVersion番号だけで判別しない。各Gateの実行証拠が揃うまで通常入口を新系へ切替えない。

**normal**：二制度以上（徴税と閲覧、軍令を含む）で共通契約を使い分けの矛盾なく実行する。

**failure**：一制度で動いただけで全制度完了・全国性能達成と表示しない。

## 状態遷移

[
  {
    "axis": "配送試行",
    "states": "InTransit → Delivered / Lost / Rejected。Lost → Deliveredは遅着の到達証拠付きだけ。",
    "guard": "再送は新Attempt、同じ本文。Lostの過去観測は別履歴に残す。",
    "terminal": "試行の終わりは案件・命令の終わりではない。"
  },
  {
    "axis": "正式受領と開封",
    "states": "ReceiptAbsent → FormallyReceived。Sealed → Openedは受領後の独立操作。",
    "guard": "重要書状は手元到達で自動開封しない。ReceiveとOpenの代理権を別検査。",
    "terminal": "受領は一度、開封・本人認識は人物ごとに記録。"
  },
  {
    "axis": "命令履行の成立",
    "states": "BindingOrder: Issued → AwaitingReceipt → AwaitingRecognition → ReadyForDecision → Authorized / Refused / Expired。",
    "guard": "Authorizedは履行判断を許す状態で、本人の服従や実行完了とは別。違法行為の試行は別の実行意図と責任記録。",
    "terminal": "不受領・期限失効・拒否で運送履歴を消さない。"
  },
  {
    "axis": "任意依頼の成立",
    "states": "VoluntaryRequest: Offered → AwaitingAssent → AgreementActive / Declined / Expired。",
    "guard": "権限ある当事者の同一版の合意と、制度で定めた同意到達が必要。外交条約は双方への適法到達で成立。",
    "terminal": "受領や発送だけで契約成立としない。"
  },
  {
    "axis": "実行",
    "states": "NotStarted → Waiting → Running → Suspended / PartiallyCompleted / Completed / Stopped / Failed。Suspended・PartiallyCompleted → Runningは再開条件付き。",
    "guard": "Waitingからの未着手取消はStopped。Runningからの中断は安全停止点へ。Completed/Stopped/Failedから旧IDで再実行しない。",
    "terminal": "進捗・消費・部分成果を保持。再依頼は別ID。"
  },
  {
    "axis": "報告と評価",
    "states": "Report: NotRequired / NotCreated → Queued → InTransit → KnownByRecipient。Assessment: Pending → Provisional / Confirmed / Disputed。",
    "guard": "配送失敗・再送は別Attempt。評価は評価者の証拠受領後。訂正は新EvidenceとAssessmentRevision。",
    "terminal": "実行完了でも報告・評価は未完了になり得る。"
  },
  {
    "axis": "引当・補償",
    "states": "Reservation: Active → PartiallyConsumed / Consumed / Released。Compensation: Pending → PartlySettled / Settled / Disputed。",
    "guard": "消費済みの引当を解放しない。補償は元効果と一意キーで結ぶ。",
    "terminal": "部分返還後の再送で同じ部分を二度返さない。"
  },
  {
    "axis": "取消・差替え",
    "states": "Created → InTransit → Received → AwaitingRecognitionOrEffect → Applied / AlreadyStopped / TooLate / Unauthorized / Expired。",
    "guard": "再送可能な運送失敗は取消の業務終端と分ける。明示したTargetIdと未確定効果だけに作用。",
    "terminal": "返報は取消結果と別配送。取消が成功した真実を発送者へ即時公開しない。"
  }
]

## 公開API

[
  {
    "name": "Submit(Intent, ExpectedRevision)",
    "input": "WorldId、ActorId、RequestId、CommandKind、BodyVersion、Payload、ExpectedRevision。Coreが受付時の時刻を付与する。発送本文はこの後に固定。",
    "output": "ResultCode、RequestId、CommitSequence、本人に開示可能な理由。重複は保存済み受付結果を返す。",
    "mutation": "単一トランザクション。権限ある他者への命令は配送を作り、瞬間実行しない。"
  },
  {
    "name": "AdvanceTo(TargetElapsedSeconds, EventBudget)",
    "input": "目標時刻は現在以降、EventBudgetは正の有界値。",
    "output": "ReachedAt、ProcessedCount、StopReason、ActorNotifications。",
    "mutation": "次のイベント境界まで進む。予算超過なら時刻の進んだ位置を返し、未処理のままTarget到達を偽装しない。"
  },
  {
    "name": "GetActorView(ObserverId, ViewQuery)",
    "input": "現在の操作人物、問い合わせ範囲。管理用途の特権は別入口。",
    "output": "不変の本人向けViewとRevision、出典・鮮度。",
    "mutation": "なし。乱数消費・開封・評価確定を読み出しで起こさない。"
  },
  {
    "name": "RequestSave(SlotId, ExpectedWorldId)",
    "input": "安全なスロットIDと現在世界ID。任意絶対パスを通常UIから渡さない。",
    "output": "SaveJobId、CapturedRevision、完了後GenerationIdまたはIO_FAILURE。",
    "mutation": "正本の確定Revisionを取得。背景ファイル保存は進行中の世界を変更しない。"
  },
  {
    "name": "PrepareLoad(SlotId, GenerationId)",
    "input": "指定世代、許可保存ルート。原本を読み取り専用で扱う。",
    "output": "隔離LoadCandidateまたは公開可能な理由付き失敗。復旧候補と時刻を提示可。",
    "mutation": "現在の世界を変更しない。旧系ならMIGRATION_REQUIREDと旧入口案内。"
  },
  {
    "name": "CommitLoad(CandidateId, ExpectedCurrentWorldRevision)",
    "input": "PrepareLoad済みの候補と現在セッションの版。",
    "output": "新しい停止中セッションの識別子・Revision、またはSTATE_CHANGED。",
    "mutation": "実行直前に候補の依存SHAと期限・対象版を再照合し、一度だけ置換。古い候補をロード中の進行へ黙って適用しない。"
  }
]

## 保存のwire契約

{
  "format": "game-world-save",
  "envelope_version": 1,
  "encoding": "UTF-8 without BOM; LF; strict JSON, duplicate properties rejected",
  "canonical_hash": "PayloadHashはファイルpayload.jsonの生UTF-8 bytesのSHA-256（小文字64桁）。業務本文のCanonicalPayloadは契約版付きJSON：オブジェクトkeyはOrdinal順、順序あり配列は保存順、集合は契約で定めたID順、文字列は値を正規化せず標準JSONエスケープ。整数は符号・桁を正規化、浮動小数はCore業務本文に禁止。Amount・時刻・uint64はwire上10進文字列に統一し先頭ゼロを許さない（0のみ例外）。bool/nullはJSONの型。ローカル表示の小数位相は有限doubleかつ0以上1未満で別Presentationへ。",
  "version_policy": "EnvelopeVersion・各SchemaVersion・RulesetId/Versionを別照合。読める版は明示登録した組だけ。未知フィールド、重複key、型違い、未知enum、未登録版は拒否。任意拡張を暗黙に読み飛ばさない。",
  "required_payload_sections": [
    "WorldClock",
    "Characters",
    "Organizations",
    "Memberships",
    "ServiceBonds",
    "Offices",
    "Grants",
    "Sites",
    "LandRights",
    "Locations",
    "Units",
    "Tasks",
    "Actions",
    "ResourceLots",
    "Reservations",
    "Transfers",
    "Debts",
    "Messages",
    "Attempts",
    "Receipts",
    "Evidence",
    "Knowledge",
    "Proxies",
    "PendingEvents",
    "EffectLedger",
    "Outbox",
    "Compensations",
    "RandomStreams",
    "IdCounters",
    "ConflictDecisions",
    "PlayerControl",
    "Chronicle"
  ],
  "empty_sections": "空配列・空オブジェクトを明記し省略しない。各SchemaVersionが各項目の必須・nullableを定義。未実装制度を既定値の補完で実装済みに見せない。",
  "retention": "正当な履歴はゲーム内に保持。UI向けの要約・索引は再構築可だが、未処理参照、同一性墓石、効果一意キー、抽選根拠、移行情報、知識出典は落とさない。物理圧縮や分割は後続の版変更と負荷検証後。"
}

## 例示用ヘッダー（ロード不可）

{
  "_note": "説明用ヘッダー断片。ロード可能なセーブではない。SHA・worldId等は例示用。",
  "Format": "game-world-save",
  "EnvelopeVersion": 1,
  "WorldId": "world:example",
  "GenerationId": "generation:42-1",
  "CommitSequence": "42",
  "TimeDomain": "GameSecondsV1",
  "SchemaVersions": {
    "World": 1,
    "Messaging": 1,
    "Resources": 1
  },
  "Ruleset": {
    "Id": "rules:common-contract-v1",
    "Version": "1.0",
    "Sha256": "<64桁の定義SHA>"
  },
  "DefinitionManifest": [
    {
      "Id": "scenario:1560",
      "Version": "1",
      "Sha256": "<実採録後の定義SHA>"
    }
  ],
  "PayloadByteLength": "<実際のbyte数>",
  "PayloadHash": "<payload.jsonのSHA-256>"
}
