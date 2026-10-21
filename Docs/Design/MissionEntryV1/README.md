# 在所任務の入力

入口：`Scripts/Show-MissionEntry.ps1`。点検/徴税、本人/書役、徴税量、完了期限を指定して既存の任務へ送る独立試作。旧保存は読み替えない。

[運用方針](../../Development/OPERATING_POLICY.md) / [完成条件・現在段階](../../Development/CURRENT_STAGE.md) / [実測結果](verification.json)

実行：`python Scripts/verify_mission_entry.py` → 実PNGの点検 → `python Scripts/check_mission_entry_binding.py`。

送信済みの任務内容を保存する。未送信の下書きは保存しない。量と秒は試作入力範囲で正式な税率ではない。既存のGit対象外ローカル3D素材を使用し、モデルを今回勝手に公開しない。旧検証書は共有コード変更で再検証まで失効する。

完成条件5/5。全802テスト、実Godot9プロセス、12保存点・24画面比較は差分0。44PNGを生成し実画像を確認した。動画/HTMLは今回の判断に不要なため新規作成していない。
