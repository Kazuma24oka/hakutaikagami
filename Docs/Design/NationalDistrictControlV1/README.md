# 全国郡支配の初期色 V1

新規1560年全国開始の世界状態に、百代鏡DBの637郡分の支配記録を版付きで持たせた。開始前の史料地図と世界内3D地図は同じ郡台帳を描く。

地図境界データ721形状のうち633は百代鏡DBへ対応する。対応しない88形状は無所属色を表示し、支配者を創作していない。118形状は同一国内の方角語の違いから郡名を対応させた推定として別模様を付ける。津軽郡のような複数家の並立は台帳に全持分を保持する。

勢力色は安定した勢力IDから生成する仮色で、史実の旗色ではない。表示切替はゲーム状態を書き換えない。郡の支配変動への追随は後続工程。

## 実装

- `Core/Contracts/NationalDistrictControl.cs`：初期郡台帳、開始版の限定、保存状態の照合。
- `Game/Scripts/NationalDistrictControlOverlay.cs`：地図境界と台帳を結ぶ共有描画生成器。
- `Game/Scripts/NewGameMap.cs`：開始前地図の表示切替。
- `Game/Scripts/ContinuousWorldTrial.NationalDistrictControl.cs`：全国3D表示と実保存・復旧確認。

## 確認

`verification.json` にビルド、短い検査、ゲーム内部入力・描画、途中保存・復旧を記録した。生成画像・実保存ファイル・ログは `runtime-local/` に残し、Gitには入れない。
