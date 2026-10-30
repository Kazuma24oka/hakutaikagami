# 武将の能力・技能・成長：実装前の完成条件

基礎能力は統率・武勇・知略・政務・交渉。農政・商取引・普請・兵站・戦術・情報・礼法の熟練と分離する。能力上昇で身分・役職・領土・勲功を自動付与しない。

1. Introduce an explicit versioned new-game development policy and separately validated optional state. Preserve old saves and libraries byte-identically when that policy is absent. Keep registered legacy four-ability data; add compatible explicit negotiation authoring and correct five-ability UI without silently overwriting old files or assigning office/land.

2. Separate five base abilities, seven skills, experience and merit. Seed stable provisional NPC game values reproducibly and label their balancing provenance. Actual completed practice and representative work award actor-specific, time-backed experience once; replay/resend/load/chunking cannot farm duplicate growth or automatically grant status.

3. Connect practice and representative practical work to the common world clock, existing explicit meal/rest/busy/movement/cancel rules and deterministic bounded ability/skill/situation calculations. Poor characters must retain free practice, walking and work access; introduce no automatic starvation/death or mandatory paid service. Explain the basis in the action and own character sheet; do not invent virtual completed work or change old task timing retroactively.

4. Show exact own abilities/skills and progress, but other people only receive unknown or dated evidence-based estimates. Never derive an estimate directly from hidden true values. Known reports, observations and correction have sources/dates, and counterfactual hidden-profile changes leave the uninformed observer view unchanged.

5. Verify normal new-game authoring through practice/work and visible data DTOs, interruption/recovery/equal future, old saves/libraries and actual Godot placement. Build and integrate tested code/docs automatically, leave GUI appearance unclaimed, then continue into personalities, directed relationships and loyalty.

## 境界

- Do not mix uncertain historical ability ratings with verified historical facts.
- Five base abilities are Leadership, Valor, Intelligence, Politics, Negotiation. Seven skills are Agriculture, Commerce, Construction, Logistics, Tactics, IntelligenceGathering, Etiquette.
- Negotiation compatibility must preserve old four-value serialization and old creation budgets when no new explicit value is stored.
- Prefer real common task records to a parallel fake training timer. Partial and repeated completion must not duplicate experience.
- Other-person estimates use only the observer's actual information; hidden traits and true private ability values must not be passed into the view.
- No retroactive job rescheduling or automatic old-save migration; record the exact scope and remaining aging/injury/education work.
- Existing trial creation budget is 1000, not an assumed500. Preserve old four-value costs when negotiation is absent; include the explicit new value when authored.
- New-game character selection is scenario configuration and may sort/display authored or labeled provisional base values; in-game character views remain limited to the protagonist's information, with no hidden traits or live private ratings.
- Do not run Godot UI signal-emitting self-tests or GUI automation. Use Core public authoring/command/view APIs, builds and CLI-only Node3D data verification; appearance and GUI interaction remain user review.

BD-13は60、BD-16は50を上限とした候補。健康・負傷・加齢、教育者、全仕事への適用、噂と訂正の一般化は残る。完成条件を満たす前に進捗へ加点しない。
