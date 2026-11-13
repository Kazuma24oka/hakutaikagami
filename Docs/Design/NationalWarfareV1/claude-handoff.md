# Claude担当部分で検出した保存形式の不整合

全国の戦の接続に伴う関連検査で、生活の目標を設定できない問題を再現した。担当外のため実装は変更していない。

- 対象：PersonalLife.cs の LifeInput.Subject と ContractJson.cs の必須項目判定。
- Subject は null のとき出力を省く一方、読込みでは必須と判定される。
- 最小再現：Subject を指定しない LifeInput（SetGoal / KeepHouse / Target=1）を ContractJson.Write → Read。JsonException: missing required properties: Subject。
- NationalSystemsTests.LifeAndSocialSystemsRunInTheNationalStartAndRestoreToTheSameFuture が21行目で失敗する。全国の進行以前の生活操作で発生する。
- Claude側でこの任意項目の読込みと、旧保存の省略された項目を確認してほしい。生活・健康の仕組みや、その仕様にはCodex側から手を入れない。

証跡：runtime-local/life-json-check（ローカルのみ）。関連3件（全国の戦の初期化・一覧のない旧全国保存・物資の改ざんと受渡し）は成功。工程全体・全件検査の成功とは扱わない。
