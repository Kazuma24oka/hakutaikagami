# 城の移動網を世界ごとの版で使う V1（2026-11-12）

範囲は [scope.md](scope.md)。

- `Core/Contracts/WorldNetwork.cs`：世界ごとの移動網の版。`NetworkPolicy` が `castle-routes-1560-v1` の世界は、百代鏡DBの城を拠点に足した移動網（CastleRoutes、城の拠点1,856・道2,628）を使う。版を持たない世界は今の全国の移動網のまま。
- `NewGameWorld.Create(..., networkPolicy:)` で城の移動網の世界を作れる。開始配置には城の拠点を持ち主なしで足す（`WorldNetwork.WithCastles`）。
- 経路探索（BranchRoutes）・拠点の検査（RegionalBranch）・拠点の名前（NewGameWorld.SiteName）・天候の地域（RegionalWeather）を世界の版に合わせた。
- 版の項目は空なら保存に書かない。今までの開始と旧保存の中身は変わらない。

## 残り
- 調べる権利の記録の作り直し（城の世界では今の101拠点の分だけ）。
- 新規ゲームの画面で版を選ぶ、地図・ゲーム中の画面の城の拠点への対応。
- 全国の武将の配置と遠方の休眠。

確かめたこと：[verification.json](verification.json)。
