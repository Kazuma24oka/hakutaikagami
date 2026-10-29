# 城主交代・行政と保管の引継ぎ V1

PR28統合後の新規v20。軍事占有・記録所有・期限付き城主担当・現物保管を別に扱う。現地の当主と同意する成人直属家臣が1〜30日の行政担当を任命・交代・解除する。役職だけで兵・直属・所有・家金・私財・未払いを移さない。開始時役職の記録は保持し、現行担当は任命原本から導出する。

既存の地域処分枠・家の調達枠・運搬の現地代理受領を再利用し、再任命で月枠をリセットしない。保管確認は所有家と当時の米・木材・武具・家金を記録するだけで、現物を増やさない。他家の実部隊が城門を占有する間は新規の地域処分・調達・運搬受領を保留する。全軍事・私財取引の一般封鎖ではない。過去の有効な履行は巻き戻さない。

担当者本人の現地報告と、NPCの実際の現地行政に基づく観測を封書で配送する。宛先は合意地点で固定し、遠隔の当主の現在位置を追跡しない。到着と開封を区別し、不在NPCから現在在庫を漏らさない。担当者本人の再送は、その時点で不達を知った同じ観測に限り原本込み三通まで。任命交代後も古い報告原本を残し、現在の在庫・権限に読み替えない。NPCの自動再送、家督・領有権移転・一般相続は後続。

実装前の元基準はapproval-baseline-v19.json（67f7209で固定）。報告追加前にapproval-baseline-v20.json（e725e29）へ追加範囲を固定し、元の評価・重み・分母を維持する。進捗の評価訂正は加点しない。

## 完成条件

1. Same-owner head and physically present consenting adult direct retainer agree on a bounded castellan appointment, replacing earlier appointments without transferring ownership, service, troops or debts
2. Appointment history grants only local administration and store receipt within existing monthly limits; head-only political acts and budget changes remain restricted
3. Explicit local store custody acceptance records dated owner-separated quantities without creating stock or claiming ownership; replacement preserves prior records and outstanding freight
4. Military possession conflicting with recorded owner suspends new administrative disposals and deliveries at the actual site; no historical operation is retroactively invalidated
5. Castellan reports bind to the original appointment and dated owner-separated stock; physical delivery/opening controls remote knowledge, known failure permits bounded same-observation resend, NPC reports derive from actual local administration, and generation recovery/future equality covers both office and letters

外観・GUI操作は別受入。関連テスト、実世代保存復旧、旧保存のバイト一致、画面なしGodotのNode3D座標照合を区別して記録する。
