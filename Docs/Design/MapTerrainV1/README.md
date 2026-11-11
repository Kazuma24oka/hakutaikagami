# 地形（川・湖・山）の表示 V1（2026-11-12）

基準：approval-baseline-v105（BD-20 32→35）。2026-11-12 ユーザーの指示（取り寄せの許可あり）。

- 国土地理院「地球地図日本」（第2.2版 ベクタ・第1.1版 標高、国土地理院コンテンツ利用規約）を取り寄せ、戦国DBの資料フォルダ（MAPDATA/gm-japan）に置いた。
- Tools/HakutaiDb/build_terrain.py → Game/Data/Geography/terrain-v1.json（川4,795・湖や広い川面862、約100mで間引き）と terrain-shade-v1.png（標高30秒から北西の光の陰影、3840×2640、約1MB）。出典は Game/Data/Geography/CREDITS.md。
- 地図の左上に「地形」の切り替えを足した（経路・国・郡と同じ）。陰影と湖はいつも、川は1ドット1.6kmより寄ったときに描く。
- 表示だけに使い、世界の計算・保存の形は変えない。山を移動の決まりに使うのは別の工程。
- 取り寄せたが使わなかったもの：Natural Earth の川（日本は3本だけ）、国土数値情報「湖沼」（今の湖でダム湖を含む）。

確かめたこと：[verification.json](verification.json)。
