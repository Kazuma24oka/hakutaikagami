# 返納済み駐屯軍の発令者引継ぎ V1

PR30統合後の継続工程。対象は選択中の同じ家・同じ拠点の駐屯軍。生前当主職交代後、現当主と家中の本人が現地で新しい軍務を明示的に再合意する。新当主本人の準備に加え、隠居本人が個人的な家臣にならず軍務だけを受け直す場合を含む。

原初の軍務条件は書き換えない。新しい案件に記録した版付き申出から、その案件の発令者を導出する。過去の許可・命令・返報・照会・権限根拠は当時の発令者のまま保持する。

旧案件の終了通知を知り、指揮・兵糧・返報を返納してから申出る。申出、条件提示、同意、許可開封、現地引受を分ける。軍務範囲だけの根拠を作り、政治権限・個人奉公・所属を変更しない。

同じ軍・兵・軍用倉・生涯台帳を使い、損耗・消費・在庫・整数kg未満の残りも保持する。新当主の再任で兵や米を復活させない。隠居の財布へ家の米や金を移さない。

活動中の軍の指揮移譲、別主家切替後の旧台帳へ戻る時の発令者変更、一般NPC軍の再動員、旧行政委任・条約・人質の再承認、死亡・家督争い・一般相続は後続。

通常入口の「軍務を相談・確認」から「新当主と軍務を再確認」を選ぶ。書状の話者は開封した原本の送信者を使う。外観とGUI操作の受入は別途必要。

完成条件（実装前固定基準 v22、軍務再合意の追加前固定基準 v23）：

1. An explicit new-head request renews the same returned garrison using a new case issuer without rewriting initial military terms, old commands, letters or proofs
2. Only a present actual successor head may request handover after known closure, return of custody and supplies; active, remote, stale and forged handovers are refused A present household member, including the retired head, may explicitly request a fresh successor-issued offer/permission without silently changing personal service; prior custody still blocks takeover.
3. Repeated loans, casualties, food use and depot transfers retain one physical lifetime ledger and no duplicated troops, stocks or command grants
4. Current and historical military issuers, permissions, physical reports and old inquiries retain the correct case/time; personal service remains separate
5. Normal entry and save/recovery/future equality, old save byte compatibility and headless Node3D data are checked separately from appearance acceptance
