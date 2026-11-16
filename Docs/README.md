# 資料の案内（Docs）

百代鏡の資料の入口。何がどこにあるかだけを書く。2026-11-13 までこのファイルにあった更新の記録は [Archive/DOCS_README_HISTORY_TO_2026-11-13.md](Archive/DOCS_README_HISTORY_TO_2026-11-13.md) に移した。

## まず見るもの
| 知りたいこと | 資料 |
|---|---|
| 全体の進み具合（％） | [Development/ProgressV1/index.html](Development/ProgressV1/index.html)（ブラウザで開く。正本は同じフォルダの progress.json・estimate.json。main へ統合するときに書き直す） |
| 今やっている工程と工程の表 | [Development/CURRENT_STAGE.md](Development/CURRENT_STAGE.md) |
| 完成の目標（差し替え前の完成） | [Development/PRE_ART_COMPLETION.md](Development/PRE_ART_COMPLETION.md) |
| 開発の進め方 | [Development/PROCESS_V2.md](Development/PROCESS_V2.md)・[Development/OPERATING_POLICY.md](Development/OPERATING_POLICY.md) |
| 作業の決まり（AI 向け） | [../AGENTS.md](../AGENTS.md) |

## 設計の正本
| 内容 | 資料 |
|---|---|
| 基本設計（60判断） | [Design/basic-design-v1.json](Design/basic-design-v1.json)（読みやすい形は [Design/BASIC_DESIGN_V1.md](Design/BASIC_DESIGN_V1.md)・[Design/basic-design-v1.html](Design/basic-design-v1.html)） |
| 新機能の統合案（工程の順番は第14節） | [Proposals/GETENROKU_INTEGRATION_V1.md](Proposals/GETENROKU_INTEGRATION_V1.md) |
| 新機能のバックログ | [References/GETENROKU_BACKLOG.md](References/GETENROKU_BACKLOG.md) |
| 決めたことの記録 | [Decisions/](Decisions/)（[DECISION_LOG.md](Decisions/DECISION_LOG.md) が一覧） |
| 共通契約（時計・配送・保存） | [Design/CommonContractV1/common-contract-v1.json](Design/CommonContractV1/common-contract-v1.json) |
| 位階・官職の表 | [Proposals/RANKS_AND_OFFICES_V1.md](Proposals/RANKS_AND_OFFICES_V1.md) |
| 人物・家・拠点の項目 | [Proposals/CHARACTER_DATA_FIELDS_V1.md](Proposals/CHARACTER_DATA_FIELDS_V1.md) |

## フォルダごとの役割
| フォルダ | 中身 |
|---|---|
| [Development/](Development/) | 進め方・今の工程・進捗表 |
| [Design/](Design/) | 工程ごとの完成条件・検証・統合の記録（一覧は [Design/INDEX.md](Design/INDEX.md)） |
| [Proposals/](Proposals/) | 設計の提案・具体化（一覧は [Proposals/INDEX.md](Proposals/INDEX.md)） |
| [Decisions/](Decisions/) | ユーザーと決めたことの記録 |
| [References/](References/) | 参考作品から採った仕組みのバックログ・毛利元就動画の研究台帳 |
| [Confirmed/](Confirmed/) | 初期の確定資料の原本（変更しない） |
| [Archive/](Archive/) | 役目を終えた資料・過去の記録（消さずに置く） |

一覧（INDEX.md）は `python Scripts/make_docs_index.py` で作り直す。

## Git に入れない物
検証記録・使っていない素材・参考作品の分析は D ドライブにある。場所は [../README.md](../README.md) の「Git に入れない物の置き場所」。
