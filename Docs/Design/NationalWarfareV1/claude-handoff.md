# 生活の目標の保存の不整合（解決済み、2026-11-13）

生活の目標の入力（LifeInput）の報復の相手（Subject）は、空のとき保存に書かない一方、読込みでは必須と扱われ、目標を決めた世界を保存から読めなかった。Codex が全国の戦の接続の検査で見つけた。

ContractJson の必須項目の例外に LifeInput.Subject を加えて直した。NationalSystemsTests を含む関連の検査10件が合格。
