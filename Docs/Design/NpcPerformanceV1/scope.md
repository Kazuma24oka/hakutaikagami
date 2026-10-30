# NPC継続行動の処理負荷低減

1. Measure the same 29 active NPC scenario in three sequential standalone test processes before and after, with no other agent-started tests/builds/probes running; record wall time, binary hashes and conditions, not FPS.

2. Remove redundant NPC population scans and diagnostic world copies without exposing mutable state or changing actor order, decisions, revisions, clocks or event budgets.

3. Preserve exact saved bytes and future results for the previous continuing-policy recovery generations, as well as earlier saved policies; verify hidden information and occupancy regressions.

4. Run the full Core test suite and build Game; verify actual headless Godot saved/restored projection equality. GUI appearance remains untested.

5. Publish measured gains and any limits, commit and push code/docs and integrate only after successful checks; do not change fixed progress weights or claim the nationwide product load target.
