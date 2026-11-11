# 地図のデータの出典

ゲームの地図に使うデータと利用条件。製品のクレジット画面には、この一覧を載せる。

| ファイル | 内容 | 出典 | 利用条件 |
|---|---|---|---|
| japan-map-v1.json | 日本の海岸線 | Natural Earth 1:10m admin-0（Japan） | パブリックドメイン |
| japan-map-v1.json | 地名の位置の一部 | Wikidata | CC0 |
| northern-territories-v1.json | 北方領土の島の形 | jpn-atlas 1.0.2（国土地理院「地球地図日本」をもとにした TopoJSON） | BSD-3-Clause。データの出典：国土地理院 地球地図日本 |
| borders-v1.json | 国と郡の境界の線と名前 | 「郡地図 Ver 1.1」を加工して作成（郡地図研究会） | CC BY 4.0（クリエイティブ・コモンズ 表示 4.0 国際） |

borders-v1.json の加工：明治の国の区分のまま（北海道は国「蝦夷」にまとめ、渡島国の範囲を郡「渡島」とした）、明治の市・区を除いた、隣と接する境目だけを線にした、約200mで間引いた、日本測地系から世界測地系へ近似の式で変換した（Tools/HakutaiDb/build_borders.py）。
