# 成人養子：当人・家の合意と実施を分離

BD-43に従う最初の実装は成人の現地養子縁組。婚姻・出生・史実の血縁台帳・遠隔書状は後続。親族関係と家督/所属/役職/所領は別の原本を持つ。

1. Introduce an explicit new-game adult adoption policy and versioned case/relationship records. Separate proposer personal assent, the other adult personal answer, required house authority assent, implementation and existing succession/office/land systems. Preserve old saves without automatic migration.

2. A proposal records the exact parent/child roles, fixed local meeting site, known house representatives, original deadline and version. Both individuals must actually be adult and present for personal consultation; NPC answers are deterministic personal choices and never override player decisions.

3. Required house consents are distinct from personal consent and require actual local authority; player house consent is an explicit action. Missing/changed authority or absent people leave pending/unavailable rather than substituting a hidden new representative. Implementation rechecks all current consents and presence.

4. Withdrawal, deadline, refusals, duplicate actions and competing/cyclic adoption are validated without teleport, duplicate relation, automatic membership/office/land transfer or automatic heir entitlement. Completed relationship remains separately recorded; proposals are not already family.

5. Verify real proposal/answer/house approval/implementation and refusal paths, unchanged money and legal rights, actual generation recovery/equal future, old saved worlds and headless Godot positions; integrate tested code/docs automatically and continue.

両者の成人・現地所在・同意と、関係する家の権限者の現地承諾を別々に確認する。本人の提案は本人の意思だけを表し、家の承諾を代行しない。家督候補への正式な資格/承認は既存の別手続きを要する。
