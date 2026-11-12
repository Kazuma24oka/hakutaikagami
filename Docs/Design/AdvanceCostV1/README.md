# 進行の重さの見直し 第1段 V1（2026-11-12）

基準：approval-baseline-v107（BD-04 20→22）。設計 Docs/Proposals/NATIONAL_PLACEMENT_DESIGN_V1.md 第4節3の前段。

城の移動網の世界に何もしない4,200人を置くと、進行1回に約5.5秒かかった（Docs/Design/CastleNetworkAuthorityV1）。
処理の記録（dotnet-trace、作業用の測定の実行体はリポジトリに入れていない）で調べると、人物の行動ではなく、毎回の写しと検査が原因だった。

| 原因 | 直したこと |
|---|---|
| 開始の配置の検査の記憶が 4 MiB までで、4,200人の開始は入らず、毎回の進行で開始を検査し直していた | 記憶の上限を 64 MiB に広げた（NewGameWorld.StartProjection.cs） |
| 人物ごとの外見・所在の検査で、人物・所在・旅の一覧を人物ごとになめていた | 一度だけ索引を作って引く（NewGameWorld.Validate）。同じ ID が複数なら今までどおり失敗する |
| 仕組みごとの手番の確認（約16の仕組み）が、そのたびに世界全体を写していた | 状態が変わらない間は1つの写しを共有する（WorldSession.TurnSnapshot）。検査ではそのたびに書き換えがないことを確かめる（Tests/TurnSnapshotGuard.cs） |
| 知識の検査で、知識ごとに証拠の一覧をなめていた | 証拠を ID の索引で引く（ContractValidation） |
| 拠点の位置の換算を、拠点の一覧を呼ぶたびにやり直していた | 一度だけ換算し、呼ぶたびに配列の写しを返す（NationalRoutes・RegionalCampaign・RegionalBranch） |

## 測り直した重さ（4,200人、城の移動網の世界、Release）
| | 前 | 後 |
|---|---|---|
| 最初の1分 | 10.6秒 | 1.5秒 |
| 1時間まで | 5.5秒 | 0.9秒 |
| 1日まで | 5.7秒 | 0.8秒 |

残りの大半は、1回の変更ごとに世界全体を写して全体を検査する作り（WorldSession.Change と ContractValidation）。これを変えるのは次の段（休眠と合わせて検討）。

確かめたこと：[verification.json](verification.json)。
