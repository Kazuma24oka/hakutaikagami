# 人質の保証原本と当主交代

既存の個別原本の再承認方式を人質の受渡しへ接続する。元の保証と本人を保持し、現地の新権限者を別記録で確認する。

1. Preserve the original hostage guarantee, personal consent, location and custody when a household representative changes; no automatic release or transfer of ownership.

2. Add a separate local reconfirmation record naming current lawful household representatives. Require the original player guardian and held person at the original site, both current representatives physically present and consenting. Never rewrite original signatures or retarget them remotely.

3. Release uses only the latest explicitly reconfirmed representatives; another head change holds release again. Reconfirmation cannot renew expiry, erase overdue duties, change treatment, create another hostage or teleport anyone.

4. Expose local reconfirmation in the existing custody UI without revealing absent representatives through remote views. Reject spoofed, stale, duplicate-conflicting and invalid reconfirmations atomically.

5. Verify actual house succession, refusal/absence, release, old saved bytes, generation recovery and equal futures, and headless Godot position. Capture/escort/ransom/exchange and actual food consumption remain unfinished.

実装時に確認した依存事項：新当主の現地確認には実移動が必要。既存の共通同行任務を使い、現在同席する新代表者本人の了承による、保証原本の預かり先限定の同行を追加する。雇用・遠隔呼出し・自由なNPC操作・自動移動は追加しない。進捗の目標値と分母は変更しない。
