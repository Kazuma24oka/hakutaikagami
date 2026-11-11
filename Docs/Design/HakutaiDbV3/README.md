# 百代鏡DB V3：所属の城と地域に合ったモブの名前（2026-11-12）

基準：approval-baseline-v101（BD-20 22→25）。

- 登場する武将全員に所属の城を決める（Core/Contracts/HakutaiDbStart.cs）。DBの人物は、DBの城がその家の城（または本拠）ならそれ、なければ本拠。
- モブは家の城のうち武将の少ない城から割り当て（同じなら郡の石高の多い城）、城のある国の武将に多い苗字（上位8つ）と、よくある通称で名前を付ける。同じ家で名前は重ならない。
- 史料の画面の武将一覧は「〇 名前　城」だけ。
- 世界に置く作り直しは設計だけ：[NATIONAL_PLACEMENT_DESIGN_V1.md](../../Proposals/NATIONAL_PLACEMENT_DESIGN_V1.md)（測った重さつき）。

確かめたこと：[verification.json](verification.json)。
