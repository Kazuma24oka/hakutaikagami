# 共通データ・状態遷移・保存仕様V1の委任採用

ユーザー指示：「1を推奨で決めてhtmlで教えてください」。直前の推奨1は、人物・所属・権限・所在・知識・命令・資源の保持と更新、旧保存の移行方針を含む共通データ・状態遷移・保存仕様の固定。推奨選定を委任されたため、比較案を未選択のまま残さず24契約を採用した。

## 正本と範囲

- [HTML詳細設計](../Design/CommonContractV1/common-contract-v1.html)
- [機械可読正本](../Design/CommonContractV1/common-contract-v1.json) ／ [テキスト版](../Design/CommonContractV1/COMMON_CONTRACT_V1.md)
- [受入予定48件](../Design/CommonContractV1/acceptance-plan.json) ／ [文書検査と表示確認](../Design/CommonContractV1/document-review.json)

基本設計V1、PW01〜07、共通境界C01〜10を維持する。DD-01の共通契約とDD-05/08の時間・保存接続を具体化したもの。全制度個別の状態遷移・Payload定義、係数・史料・地理・素材・性能の詳細完了は主張しない。新共通契約のゲーム実装はこの変更では行わない。

## 今回の固定点

1. 定義・世界状態・本人別表示を分離。WorldSessionを唯一の更新入口、人物等は別集約とする。
2. 同一ID・同一本文の再提出は元の受付結果、変更は参照付き新要求。配送試行・効果・報告は別ID。
3. 所属・直属主君・役職・忠誠・家督・指揮・所在を分離。所有・保管・占有・引当・消費・債務も分離。
4. 製品時計はゲーム秒。期間[From,Until)、期限At以下。因果依存と制度規則で競合裁定し、登録順で権利を作らない。
5. 正式受領・開封・本人認識・任務成立・履行・報告・評価を独立して記録。重要書状を自動開封しない。
6. 効果と台帳・進捗・Outboxを原子化。補償は別取引、未処理・重複防止・乱数状態を保存。
7. 製品形式game-world-save/1は旧TaxSnapshot Version1〜5と区別。不変世代へ保存して検証後に参照切替、読込は隔離検証後に一括置換。
8. 旧Tickや架空地図の変換根拠がないため、旧試作→製品の自動移行は現段階で非対応。旧入口・旧読込・fixturesを保持。将来の移行は登録レシピと新スロットによる。

## 既存Coreとの照合

共通正本に23入力の内容SHAと各契約の照合元を記録。SessionTransaction・SnapshotCodec、MessageFlow、Authority、EffectLedger、TaxSimulationと取消、MilitaryModels/Intel/Map、FormationFlow、SeededRandom等を読み、既存を活かす部分と変更点を個別記載した。既存のMessageFlow.SendでCreatedAt差を許容する挙動や本人受領の即認識、全体TaxSnapshot所有、単純ファイル置換を、新製品契約の完了実装として扱わない。

文書検査340項目は、正本・出典・再生成一致・リンク・検索・開閉・印刷準備・オフライン動作・1440/390幅を確認したもの。6画像を自己確認。48受入例はPLANNED_NOT_EXECUTED。今回ゲームコードに変更はなく、Core回帰試験と新仕様の動作試験は実施していない。以前の317成功を今回の新規仕様の合格に転用しない。

導入はG1型と不変条件→G2時刻・権限・View→G3案件・資源・効果→G4保存・復旧→G5平時一巡。通常入口の切替は実証後とし、旧試作の一括作り直しはしない。
