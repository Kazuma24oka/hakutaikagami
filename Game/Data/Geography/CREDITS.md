# 地図のデータの出典

ゲームの地図に使うデータと利用条件。製品のクレジット画面には、この一覧を載せる。

| ファイル | 内容 | 出典 | 利用条件 |
|---|---|---|---|
| japan-map-v1.json | 日本の海岸線 | Natural Earth 1:10m admin-0（Japan） | パブリックドメイン |
| japan-map-v1.json | 地名の位置の一部 | Wikidata | CC0 |
| northern-territories-v1.json | 北方領土の島の形 | jpn-atlas 1.0.2（国土地理院「地球地図日本」をもとにした TopoJSON） | BSD-3-Clause。データの出典：国土地理院 地球地図日本 |
| borders-v2.json・district-raster-v1.bin | 国と郡の範囲・境界の線・名前 | 「郡地図 Ver 1.1」を加工して作成（郡地図研究会） | CC BY 4.0（クリエイティブ・コモンズ 表示 4.0 国際） |
| terrain-v1.json・terrain-shade-v1.png | 川・湖・山の陰影 | 「地球地図日本」（国土地理院）を加工して作成（地球地図日本 第2.2版 ベクタ・第1.1版 標高） | 国土地理院コンテンツ利用規約（出典の記載） |

borders-v2.json・district-raster-v1.bin の加工（Tools/DistrictRaster）：地図の陸地を約0.002度の升目に分け、国は国地図・郡は郡地図で決め（食い違いは国を優先）、どの郡にも入らない陸は同じ国の隣の郡に取り込み、境界線は隣り合う升目の郡が違う所から作った。以前の borders-v1.json の加工：明治の国の区分のまま（北海道は国「蝦夷」にまとめ、渡島国の範囲を郡「渡島」とした）、明治の市・区を除いた、隣と接する境目だけを線にした、約200mで間引いた、国の形の穴を湖として使った、日本測地系から世界測地系へ近似の式で変換した、国の外にはみ出した郡の部分を隣の国の郡に取り込んだ（Tools/HakutaiDb/build_borders.py）。

terrain-v1.json・terrain-shade-v1.png の加工：川（riverl）と湖・広い川面（inwatera）を約100mで間引いた、標高（30秒）から北西の光の陰影の画像を作った（Tools/HakutaiDb/build_terrain.py）。
