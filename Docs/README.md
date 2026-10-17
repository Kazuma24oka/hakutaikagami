# 百代鏡 設計資料

## 管理規則
- `Confirmed/`: ユーザーが明示的に確定した仕様の正本。
- `Proposals/`: 検討中の案・未決定事項。提案を確定扱いしない。
- `Decisions/`: 決定日、変更理由、旧仕様からの差分を記録。
- `References/`: 参考作品、史料、既存リポジトリへのリンク。参考情報は確定仕様ではない。

確定時はConfirmedを更新し、Proposalsの対応する記述を削除・更新して正本の重複をなくす。古い・重複・却下済み資料は更新時に棚卸しし、削除前に対象と理由を提示して承認を得る。履歴はGitで追跡する。mainへのマージは別途承認を要する。

## 情報・書状・通知システムの資料参照順（未確定）
- **統合参照先**：[`Proposals/INFORMATION_SYSTEM_CONSOLIDATED.md`](Proposals/INFORMATION_SYSTEM_CONSOLIDATED.md)。書状・伝達・認識・機密・通知・代理権限について、既存の検討案を横断整理した最新版。
- 従来の `LETTER_HANDLING.md`、`LETTER_SECURITY_AND_DELEGATION.md`、`INFORMATION_TRANSMISSION.md`、`KNOWLEDGE_MODEL.md`、`KNOWLEDGE_ACQUISITION.md`、`CONFIDENTIAL_INFORMATION.md`、`EVENT_NOTIFICATION.md` は検討経緯・詳細の参照資料として保持する。記述が重複または食い違う場合は統合参照先の整合整理を優先するが、いずれも未確定。
- 旧資料の削除・大幅な縮約は未実施。既存の管理規則に従い、削除対象と理由を提示して承認を得てから実施する。
