# 城主任命・保管原本の再確認 V1

実装前に固定した完成条件。承認済みPR32をmain7704391へ統合し一致確認後、同じ作業ブランチで続行。

1. Current head may accept an explicitly consenting local household member personal pledge after the original reciprocal service is individually released; membership, office, private assets and obligations remain separate and busy subjects cannot silently change service
2. After actual head succession an unexpired old castellan original can be reauthorized once by local current head and directly bound consenting agent; preserve original expiry, ownership, old receipts and dated authority without reviving superseded appointments
3. Reauthorized castellan custody, local NPC administration, procurement/freight delegated budgets, dated sealed report recipients and late old reports stay attached to their distinct originals; no inventory duplication or monthly reset
4. Normal castle and personal-service screens expose valid local operations and distinguish ineffective old appointment from effective authority; unknown remote stock and unconsenting candidates are not disclosed
5. Wrong/stale/repeated/absent/refused/expired/forged operations, actual generation recovery and future equality, old-save compatibility and headless 3D data are verified; GUI appearance remains separate

## 追加範囲（不戦合意の実装前固定）

城主・保管だけで区切らず、組織当事者の成立済み不戦合意を旧代表者が現地で新当主へ示す。元の条件・期限・宛先を保持。未開封・交渉途中の書状を新当主の承諾へ読み替えず、旧代表者の自動処理に現政治権限を要求する。一般同盟・人質・婚姻は後続。基準26を保持して27へ追加し、重み・分母と既存評価は変更しない。

## 実装上の区別

- 個人奉公の受入は、同じ家の現地成人本人の同意と元の奉公の整理を必要とする。所属・役職・兵・財産を移さない。
- 城主の再確認は最新の期限内原本から一度だけ別原本を作る。元期限を延長せず、担当者変更後も月の処分・調達・運搬引当枠を共有する。保管数量は確認時点の観測であり、蔵の所有や現物移転ではない。
- 成立済み不戦合意の当事者は組織。旧代表者の知識と新当主の知識を分け、旧代表者が実際に開封して知った原本を現地で示す。未成立・未開封・期限切れの原本は確認できない。
- 元の宛先・旧書状・条件・期限・過去の履行を保持する。新当主が読んでも、他家の認識は更新しない。隠居後に旧宛先へ届いた承諾を開封しても、現権限のない本人は自動で政治確認書を発行しない。
- 今回は旧代表者から直近の現当主への現地確認。一般同盟・人質・婚姻、複数世代の外交連鎖、新代表者による追加確認書配送、活動軍の引継ぎ、死亡・係争は対象外。
- 検査はCLIと画面なしGodotのデータ照合。外観・GUI操作の受入とは別。実測結果はverification.json、完成条件との照合はcompletion.jsonに記録する。

### 検証器の参照修正

城主段階の実テスト1581件とGodot168組/822Node3Dは成功したが、初回のまとめ処理は過去資料中の最初のprojection-resultを選び、さらに古い工程を参照して停止した。実行前入力・ログ・保存対を保持し、明示的なprojection_resultを使って独立照合した記録がcastle-stage-verification.json。最終の城主/不戦合算検証は参照を修正した検証器で別に実行し、実行前入力と終了後の一致を照合する。

前版からの未完事項：新当主でもあるNPC運搬者が自律的に承継確認・精算する一般方針は未実装。既存の運搬原本と実前渡金は保持する。今回の城主・不戦引継ぎで、その自律精算まで完了したとは扱わない。
