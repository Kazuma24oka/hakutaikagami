# 百代鏡 設計資料

## 現行の設計

ユーザー確認：「現行方針が正です」。旧資料との食い違いでは後続採用を優先します。[適用記録](Decisions/2026-10-19_CURRENT_POLICY_AUTHORITY.md)。

- **[基本設計書HTML](Design/basic-design-v1.html)** ／ [テキスト版](Design/BASIC_DESIGN_V1.md)。15分野60判断を委任採用済み。
- [機械可読正本](Design/basic-design-v1.json) ／ [共通境界・参考作品・詳細設計への引継ぎ8群](Design/basic-design-rules.json)。
- [採用と整合確認](Decisions/2026-10-19_BASIC_DESIGN_V1.md) ／ [決定履歴](Decisions/DECISION_LOG.md)。
- [平時・戦時の個人操作](Proposals/PLAYER_PEACE_WAR_FLOW.md) ／ [作戦線・連続世界の現行試作](Proposals/FORMATION_WORLD_V1.md)。
- [3D移行方針](Proposals/OFFICER_3D_TRANSITION.md) ／ [3D部品制作・検査手順](Proposals/OFFICER_3D_PARTS_GUIDE.md)。立ち絵は絵、ゲーム内は3D。旧2Dは凍結。
- [既存Confirmed原則](Confirmed/)は改変せず維持し、後続決定の適用範囲を上記採用記録で確認します。

## 完了と残作業

基本設計V1の管理単位60件は解消済みです。詳細設計8群、史料・地理データ、制作・実装、性能達成、[予定受入170件](Design/basic-design-acceptance.json)の実行は別です。HTML等の検査179項目・既存Core317試験の成功を、新仕様全体の実装合格とは扱いません。

## 履歴とブランチ差分

- [design/foundationとmainの差分・アーカイブ判断](Decisions/2026-10-19_DESIGN_BRANCH_AND_ARCHIVE.md)。
- [統合済みの旧案と台帳](Archive/BasicDesignV1/README.md) ／ [移動対応表](Archive/basic-design-v1-moves.json)。旧未確定表記を現行の未選択状態へ戻さないでください。
- [以前の資料索引](Archive/BasicDesignV1/DOCS_INDEX_BEFORE_ARCHIVE.md)は当時の状態・詳細資料の探索用です。現行の状態判断には使いません。

## 管理規則

Confirmedは既存確定原則、Designは現行基本設計、Decisionsは採用・変更理由、Proposalsは現行試作・検討中の詳細・履歴への案内、Archiveは旧案です。履歴は削除せず保持し、通常の設計導線から分けます。design/foundationと同期sourcesは編集しません。
