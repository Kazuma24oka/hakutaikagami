# ドット絵の武将と兵 V1

[決定](../../Decisions/2026-11-12_DOT_SPRITE_OFFICERS.md)に従い、地図に立つ武将と兵をドット絵にした。

- 型の決まり：`Core/Contracts/DotOfficerLooks.cs`（人物の身分・年齢・家から型の名前を決める。世界の原本・保存は変えない）
- 型の一覧と書き出し：`Tools/DotOfficerBaker/make_looks.py` → `looks.json`、`bake.py`（甲冑アニメ工房を画面なしのブラウザで動かす）。60型・404枚（一般の武将の戦時は家の色7通り）
- ゲーム：`Game/Scripts/DotOfficerActor.cs`（カメラを向く板。時刻からコマを決める）、`NewGameRuntime.cs`（武将）、`NewGameRuntime.DotSoldiers.cs`（部隊＝足軽、寺社の武装勢力＝僧兵、賊＝浪人の甲冑）
- 平時は和服、本人が軍勢を預かる・出陣中・賊との接触中、NPC は軍勢を率いている・戦闘中のとき甲冑。

## 書き出しの手順
```
python Tools/DotOfficerBaker/make_looks.py
python Tools/DotOfficerBaker/bake.py "D:\02_プロジェクト別\共通素材\購入素材\ちびコマドット絵作成機(商用版)\甲冑アニメ工房\index.html" Tools/DotOfficerBaker/looks.json Game/Assets/DotOfficers/Local
```

## 確認
- Core：全開始版の全人物で平時・戦時の型が書き出し一覧にある、有名武将・公家・僧・家臣・浪人・大名の振り分け、家の色、世界を変えない（6件）。
- Godot（通常開始・内部入力・自前描画）：平時の本人は和服 → 賊との戦いで甲冑 → 賊も浪人の甲冑のドット絵 → 同じ時刻なら同じ絵。

## 残件
- ちびコマの規約確認（それまで絵は Git に入れない）。
- 騎乗・大筒・船・攻城（ツールにはある）の場面への接続、合戦の大部隊の表示。
- 一般の型の数（今は身分ごとに9型）、作成武将が自分で型を選ぶ画面。
- 表示の大きさは3Dの武将に合わせた。読みやすさの調整は試し遊びの後。
- 進捗の加点はしない（素材がローカルの試作のため）。
