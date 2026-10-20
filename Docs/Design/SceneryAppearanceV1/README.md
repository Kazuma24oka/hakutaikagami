# 背景素材の交換手順

1. 既存の外観・配置IDを維持し、素材を Game/Assets/Scenery/ 以下へ追加する。モデルは地面中央ピボット・静的メッシュとし、制作時の基準寸法を記録する。
2. Game/Data/Scenery/scenery-appearance-v1.json の Materials/Visuals にIDとリソースを追加する。sceneには同じ役割の手続き形状Fallbackを指定する。地表は材質交換のみ。
3. SkinsのBindingsで役割のAppearanceを対応Visualへ結ぶ。配置Seed・IDや既存variant:a/bを付け替えない。
4. Scripts/Show-SceneryAppearance.ps1 -Verify でビルド・全Coreテスト・実GPU・保存復旧・動画を再生成する。visual_reviewはPENDINGへ戻るため、画像/動画を確認して証拠のハッシュと所見を記録する。比較対象はbaselineとswap-demo、回帰用fallback-test/invalid-test。新しいskinや役割の交換を追加する場合は、そのケースも検証に追加する。
5. Scripts/check_appearance_binding.py で現在のコード・素材と実測の一致を確認し、Scripts/Show-SceneryAppearance.ps1 で確認画面を開く。外観ボタンで基本/交換を選べる。画像比較は描画領域を対象とし、UI文字列の差を除外する。

詳しい制約・保存契約・実測は ../../Decisions/2026-10-20_SCENERY_APPEARANCE_V1.md と verification.json。配置や通行範囲を変える改修は素材交換として扱わない。
