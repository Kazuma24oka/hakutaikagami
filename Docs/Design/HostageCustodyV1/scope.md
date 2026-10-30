# 人質の現地受入と解放：既存BD-44の最初の実装

採用済み基本設計の人質/捕虜の区別・所在/待遇/義務・一意性を実装する段階。基本設計や製品範囲を変更しない。軍事捕縛・護送・交換・身代金は今回の完成扱いにしない。

1. Implement the already adopted BD-44 hostage category separately from military capture. Start with a locally present adult registered personal retainer, an explicit guarantee original, the person consent and current household custodians; no custody is inferred from loss of troops or unknown captivity.

2. Admission requires actual local presence, valid authority and no existing task, military command, another custody or conflicting undertaking. Record the same person, fixed holding site, custodian, guardian, treatment and return obligation without changing affiliation, office, property or life state.

3. During custody, shared availability checks prevent ordinary movement, work or military use of the held person. Do not impersonate the player, move people instantly, expose remote hidden state or invalidate earlier lawful records.

4. Release is a separate physically local action with current lawful custodian and return recipient. Deadline produces an overdue obligation rather than disappearance, automatic execution or teleport. Withdrawal before admission and duplicate inputs are atomic.

5. Connect the actual player-facing local admission and release flow. Verify consent/refusal/busy/deadline/release, old saved bytes, actual generation recovery/equal future and headless Godot location. Military capture, escorts, exchange and ransom remain explicitly unfinished.
