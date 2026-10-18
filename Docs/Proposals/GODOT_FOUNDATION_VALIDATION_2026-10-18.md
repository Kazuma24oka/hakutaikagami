# Godot .NET最小基盤：Windows実測結果（2026-10-18）

本書は今回の試作実装と実測の記録。ゲーム仕様の正式確定やGodot正式採用、技術検証7項目全体の合格を意味しない。今回のユーザー依頼を根拠に、設計検討から最小基盤の実装へ進めた。

## 作業元と参照

- リポジトリ：Kazuma24oka/hakutaikagami
- 設計元：design/foundation `f4a1ac6`。GitHubからfetchし、既存prototypeブランチがないことを確認して `prototype/godot-foundation` を新規作成。
- main `f793260`、design/foundation、既存Confirmed、既存Proposalsは変更しない。既存ファイルの削除・移動なし。
- 指定Confirmed：GODOT_TECH_FOUNDATION_PRINCIPLES、GODOT_TECH_VALIDATION_PRINCIPLES、GODOT_IMPLEMENTATION_READINESS_PRINCIPLES、FOUNDATION_PRINCIPLES_F01_F08、P0_ADDITIONAL_PRINCIPLESを確認。
- 指定Proposals：GODOT_TECH_FOUNDATION_7_OPTIONS、GODOT_TECH_VALIDATION_7_OPTIONS、GODOT_IMPLEMENTATION_READINESS_7_OPTIONS、PROTOTYPE_READINESS_P0_P1_OPTIONS、P1_COMMON_COMMAND_CONTRACT_21_CONSOLIDATED、P1_RESOURCE_14_CONSOLIDATEDを確認。
- Docs/README.mdから関連確定正本INFORMATION_SYSTEM、P0_EVENT_RELIABILITY_PRINCIPLES、P0_SUCCESSION_ADDITIONAL_PRINCIPLESも照合。独立Core、権限の明示的根拠、IDの安定性、所有と保管の分離、イベント依存関係を反映。
- 未確定の徴税・配送・時間方式・保存仕様は正式決定しない。今回の最小基盤を、製造開始ゲート全体の合格とは扱わない。

## 実装

| 場所 | 内容 |
|---|---|
| Core/Characters | 不変の武将定義ID、所属・役職・所在を持つ実行時状態 |
| Core/Castles | 城定義ID、城主職、実効支配、所有者と保管先を分けた資源データ |
| Core/Commands | CommandRequest、行為者・対象・行為・根拠・有効期間を分離したAuthority |
| Core/Events | EventScheduler。イベントID重複拒否、依存充足・効力時刻による準備判定のみ |
| Core/World | WorldState、参照整合検査、固定Seed、明示アルゴリズムと状態を持つ乱数 |
| Game/Data/prototype.json | 版付き静的定義。仮武将3人、鏡城1城、鏡家1家、Seed 20261018 |
| Game/Scenes、Scripts | C#のGodot UI。武将一覧、選択、名前・所属・役職・所在・ID、城主・支配・米100俵・金50を表示 |
| Tests | Godotへの参照なしで実行するxUnit単体テスト |
| Scripts/Validate-Foundation.ps1 | ビルド、単体テスト、Godot読込、シーン試験の再実行 |

静的定義、実行時WorldState、表示の選択IDを分離。武将の所属・役職、城主を変更しても武将ID・城IDは維持する。世界時刻は0のまま、自動進行は接続しない。UIは検証用管理表示で、プレイヤーの認識制限を実装した正式UIではない。

イベント基盤は効果を適用しない。Readyの同時刻ID順は一覧の決定的表示用であり、競合の優先権や登録順裁定ではない。適用済み集合は呼出側から与える試作API。循環依存・競合裁定・永続化・原子的更新は後続工程。

## 実行環境

- Windows作業場所：`C:\Workspace\Projects\Games\hakutaikagami`
- Godot：`4.5.2.stable.mono.official.6ce3de25a`（Windows x64 .NET版）
- .NET SDK：`10.0.302`（global.jsonで固定）、対象：`net8.0`、実行時：`Microsoft.NETCore.App 8.0.14`
- Godot.NET.Sdk：4.5.2、Microsoft.NET.Test.Sdk：17.14.1、xUnit：2.9.3、runner：3.1.1
- 実描画：OpenGL 3.3.0、NVIDIA 610.88、GeForce RTX 4060 Ti、Compatibilityレンダラ
- [公式配布ページ](https://godotengine.org/download/archive/4.5.2-stable/)から辿れる公式GitHub配布物：Godot_v4.5.2-stable_mono_win64.zip
- 配置先：リポジトリ内 `.tools/godot-4.5.2/`。管理者インストール・PATH変更なし。配布物とビルド生成物はGit管理外。
- ダウンロードZIPのローカルSHA256：`1D7062418AE29CBD29F5F1AD95E6F13F8AC6CB2D6EFFD673D3BE1B3398380887`（同一配布物の識別用。署名検証の代替とは扱わない）
- 外部画像・音源の採用なし。日本語はWindowsのSystemFont参照、フォントファイルは同梱なし。素材方針はGame/Assets/README.md。

## 実測結果

| 検証 | 結果 |
|---|---|
| `dotnet build Game/HakutaiKagami.Game.csproj` | 成功、警告0・エラー0（初回5.91秒、再実行ログはEvidence参照） |
| `dotnet test Tests/HakutaiKagami.Tests.csproj` | 成功、13件合格・失敗0・スキップ0（初回テスト期間224ms） |
| Godot `--headless --path Game --editor --import --quit` | 成功、終了コード0。プロジェクトとC#スクリプトを読込 |
| Godot `--headless --path Game -- --smoke-test` | 成功、3つの実ButtonのPressedシグナルから詳細切替を検証、終了コード0 |
| Godot `--path Game -- --smoke-test` | 成功、実GPU描画と3選択の試験、終了コード0、1100×720の実Viewport PNGを保存 |
| Windowsの実クリック・目視 | 成功。初期太郎（城主）→次郎（奉行）→三郎（家臣）をクリックし、名前・所属・役職・IDの変更と城情報の継続表示を確認。日本語文字欠け・切れなし |

単体テストは、初期化／武将・城ID一意性／重複武将拒否／重複城拒否／参照整合／所在参照破損拒否／城主参照破損拒否／所属・城主変更時のID維持／固定Seed100回の一致と世界間独立／異なるSeed／権限要素と期限境界／イベント重複と因果／CoreのGodot非依存の13件。

証跡：[実描画PNG](Evidence/godot-foundation.png)、[再実行ログ](Evidence/validation.txt)、[実クリック時ログ](Evidence/manual-ui.txt)。PNGは自動シーン試験の最後（三郎選択）をGodot自身が取得したもので、デスクトップ全体の画像ではない。

## 再実行

PowerShellでリポジトリのルートから実行：

```powershell
.\Scripts\Validate-Foundation.ps1 -Godot 'C:\Workspace\Projects\Games\hakutaikagami\.tools\godot-4.5.2\Godot_v4.5.2-stable_mono_win64\Godot_v4.5.2-stable_mono_win64_console.exe' -WithRendering
```

別のWindowsでは.NET SDK 10.0.302と.NET 8ランタイム、およびGodot 4.5.2 .NET版を用意し、-Godotに実パスを指定する。初回のNuGet復元はネットワークが必要。通常起動は同じGodotへ `--path Game`、編集は `--editor --path Game` を指定する。

## エラー・警告・未実施と次工程

- 最終ビルドとGodot試験でエラー・警告は検出なし。単体試験の再実行時にはTRX結果ファイル上書きの警告が1件あり、13件の試験結果への影響なし。作業開始時に制限環境内のGitアクセス／DNSが失敗したため、許可されたWindows作業への昇格実行でfetch・実装・検証を実施。プロジェクト動作の不具合ではない。
- **高・次工程**：徴税と資源引当・消費・返還、命令配送、NPCの個人認識、イベント効果の一度だけの確定、版付き保存とロード後継続は未実装・未検証。数量整合・障害復旧の本検証は未実施。
- **中・次工程**：HD-2D背景、武将パーツ合成、アニメーション、会話・謀略は未実装・未検証。今回の簡易UI合格をこれらの合格と混同しない。
- **中**：Windows配布用エクスポート、他PC、日本語フォント非搭載環境、長時間・負荷・リサイズ網羅試験、CIは未実施。
- **中**：固定Seedと乱数の再現性は同一実装の単体試験で確認。乱数状態の永続保存・ロード再開試験は未実施。
- **低**：API・JSONスキーマ・テストライブラリ・数値・SDK版数は今回の試作用選択。正式仕様への昇格なし。

今回依頼の最小基盤（読込、ビルド、3武将・1城表示、選択操作、独立Coreテスト）はWindows実測で合格。次は時間・権限・要求／案件／イベント・資源の確定境界を試作仕様として明文化し、徴税→配送→NPC→報告→保存／復旧の縦断を追加する。
