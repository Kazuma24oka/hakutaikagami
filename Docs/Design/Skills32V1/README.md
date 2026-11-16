# 32技能の版 V1（BD-05・BD-13・BD-20、2026-11-14）

決定は [Docs/Decisions/2026-11-06_CHARACTER_ABILITIES_SKILLS_GROWTH.md](../../Decisions/2026-11-06_CHARACTER_ABILITIES_SKILLS_GROWTH.md)。版は `CharacterDevelopment.Policy32`（five-base-32-skills-experience-curve-v2）で、新規の全国開始から使う。旧版（7技能）の世界はそのまま。

## 入れたもの
- 技能：決定の32技能（軍事10・内政5・外交と人事4・情報と謀略4・商業と産業4・教養と育成5）。`DevelopmentSkill` に25技能を足した（旧版の世界は最初の7つだけを使う。`CharacterDevelopment.Skills`）。
- レベル：0〜100。レベルLの累積経験の下限は 3600×L×(L+1)（`LevelXp`・`LevelOf`）。
- 経験：独習（修行）は実働の50%、実務（点検）は100%。1人の経験に数える実働は1日8時間まで（旧版と同じ数え方）。
- 基礎能力：統率・武勇・知略・政務・魅力。使った技能の主能力へ 7,200,000 経験で1上がる（決定の表の最初の主能力。`Main`）。
- 開始時の熟練：史料の採録がまだないので、試作の初期値。各人物は得意な技能を4つ（武器の1つと、主能力の高い順に3つ）だけ持ち、レベルは5〜40。ほかは0。画面には「試作の初期値（未採録）」と出す。
- NPC の修行の選択（日課の判断の土台の版）：32技能から選ぶ。新しい技能の性格の重みは試作。
- 画面：修行の選択肢と人物の技能の一覧が32技能になる。
- 作成武将の経歴（2026-11-14）：武辺・兵法・内政・商い・文雅・医術・忍びの推奨配分（予算は標準実務3000時間相当、1技能50まで。CharacterDevelopment.Backgrounds・CheckCreated）。登録画面で選ぶと、その配分が開始時の熟練になる（RegisteredOfficer.StartSkills）。
- NPC の師事（2026-11-14、全国の開始の一覧の NpcTeachers）：NPC が修行するとき、同じ拠点にその技能が自分より高い成人がいれば、最も高い人に教わる（CharacterDevelopment.NpcTeacher）。検査は Tests/Skills32Tests.cs の NpcTeacherTests。
- 熟練による作業時間（2026-11-14）：基準時間×(1−0.25×技能/100)（CharacterDevelopment.WorkSeconds。技能は作業を始めた時点の値）。普請（工事・造船）は普請、検地と指出は農政の技能。旧版の世界は変えない。検査は Tests/Skills32Tests.cs の SkilledWorkTimeTests。
- 師事（2026-11-14）：同じ拠点にいて、その技能が本人より高い人に教わる（CharacterDevelopment.Learn・Teachers。修行の画面に「〜に師事」）。教える人が始めと終わりに同じ拠点にいれば、本人は100%＋教える人の教育×0.5%の経験、教える人は教えた時間を教育の実務として得る（1日8時間の内）。検査は Tests/Skills32Tests.cs の LearnFromTeacherTests。
- 地方の新規ゲームも、遊ぶときは32技能の版で始まる（画面の確認の場面は今までの版のまま）。
- 実際の仕事の経験（2026-11-15、全国の開始の一覧の WorkExperience）：普請・検地を命じた人は、仕事が終わった時点で、工期1日あたり1時間の実務の経験を仕事の種類の技能で得る（開墾・用水は農政、町は商業、工房は工房管理、鉱山は鉱山、船は水軍、ほかの工事は普請、検地と指出は農政。1日8時間の内）。検査は Tests/WorkExperienceTests.cs。職人の生産は職人の一座の技で決まるので、武将の技能は使わない。
- 医術を使う家人の手当（PersonalHealth.HouseCarer）。医者を頼む（2026-11-15、全国の開始の一覧の NpcDoctor）：同じ拠点にいる家の外の医術30以上の成人にも頼める。病人は動かず、その拠点で頼める人に頼む（遠くの医者へ移る判断は持たない）。検査は Tests/WorkExperienceTests.cs の NpcDoctorTests。
- 兵科の技能（2026-11-15、全国の開始の一覧の TroopSkills。決定の第3節）：部隊の兵の種類に合う大将の技能（槍・弓・鉄砲・騎馬、輸送は兵站）が、統率を上限として与える損害を最大2割増やす（BattlefieldRules.TroopSkillBonus。技能だけ高くても統率の低さは消えない）。別の兵科適性は持たない。検査は Tests/WorkExperienceTests.cs の TroopSkillTests。
- 作成武将の家系と信仰（2026-11-15）：登録画面で一覧から選ぶ（家系は源氏・平氏・藤原氏・橘氏・その他、信仰は浄土真宗・浄土宗・禅宗・日蓮宗・天台宗・真言宗・時宗・神道・キリシタン）。未設定は推測しない。RegisteredOfficer.Lineage・Faith、検査は Tests/WorkExperienceTests.cs の LineageFaithTests と登録画面の検査。
- 作成武将の自由な配分（2026-11-15）：経歴の一覧の最後の「自由」で32技能の欄が開き、1技能50まで、合計は予算の内で選ぶ（残りの時間を表示。超えれば保存しない）。検査は OfficerLibraryTrial の画面の検査（ゲーム内の入力）。

## 残り
- 史実人物の開始熟練の採録（戦国DB）。

## 確かめたこと
- Tests/Skills32Tests：全国の開始で32技能、曲線の値、開始時の熟練が4つで試作の印付き、医術を30分独習すると900経験、保存から戻して同じ。
- 旧版の検査（CharacterDevelopmentTests・NpcJudgmentTests）は変わらず合格。
