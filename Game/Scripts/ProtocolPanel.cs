using Godot;
using HakutaiKagami.Core.Commands;
using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Messaging;
using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public sealed record ProtocolSave(int Scenario, int Step, string Session);

public partial class ProtocolPanel : MarginContainer
{
    private string definition = "", path = "";
    private TaxSimulation tax = null!;
    private InspectionSession inspection = null!;
    private OptionButton scenario = null!;
    private Button reset = null!, next = null!, save = null!, load = null!;
    private Label summary = null!, detail = null!, notice = null!;
    private readonly List<(string Caption, Action Action)> steps = new();
    private int mode, step;
    public void Initialize(string json)
    {
        definition = json; path = ProjectSettings.GlobalizePath("user://shared-protocol-v1.json");
        foreach (var edge in new[] { "left", "top", "right", "bottom" }) AddThemeConstantOverride("margin_" + edge, 28);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 16); AddChild(column);
        column.AddChild(new Label { Text = "応答・代理受領・共通命令の段階検証", Modulate = new Color("e2c891") });
        column.AddChild(new Label { Text = "試作の管理表示です。配送・正式受領・開封・本人認識を分けて確認します。", Modulate = new Color("a6b7c7") });
        var row = new HBoxContainer(); column.AddChild(row);
        scenario = new OptionButton();
        foreach (var name in new[] { "1 取消応答の紛失・再送・遅着", "2 代理受領と本人への伝達", "2 担当交代と明示的な再委任", "3 共通基盤の別命令：城の視察" }) scenario.AddItem(name);
        row.AddChild(scenario); reset = Button(row, "開始 / リセット", () => Reset(scenario.Selected));
        summary = new Label(); column.AddChild(summary); column.AddChild(new HSeparator());
        detail = new Label { CustomMinimumSize = new Vector2(900, 190) }; column.AddChild(detail);
        next = Button(column, "次の操作", () => { steps[step].Action(); step++; });
        var storage = new HBoxContainer(); column.AddChild(storage);
        save = Button(storage, "この段階を保存", () => SnapshotCodec.SaveFile(path, SnapshotCodec.Write(new ProtocolSave(mode, step, mode == 3 ? inspection.SaveJson() : tax.SaveJson()))));
        load = Button(storage, "保存した段階に戻る", Load);
        notice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = "検証項目を選び、次の操作を順番に実行してください。" }; column.AddChild(notice);
        Reset(0);
    }
    private Button Button(Node row, string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(190, 48) }; row.AddChild(button);
        button.Pressed += () => { try { action(); notice.Text = "操作完了。保存・読込は各段階で使用できます。"; Refresh(); }
            catch (Exception e) { notice.Text = "操作に失敗：" + e.Message; GD.PushError(e.ToString()); } }; return button;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private void Reset(int selected)
    {
        mode = selected; step = 0; steps.Clear();
        var world = PrototypeScenario.Load(definition); tax = TaxSimulation.Create(world); inspection = InspectionSession.Create(world);
        void Add(string caption, Action action) => steps.Add((caption, action));
        if (mode == 0)
        {
            Add("徴税と取消を発送", () => { tax.Submit("tax", "character-001", "castle-001", 100); tax.DispatchCancellation("cancel", "tax", "character-001", 1, 1); });
            Add("1刻進行：取消成立、応答は配送中", () => { tax.AdvanceOneTick(); Check(tax.CancellationResultFor("cancel", "character-001") == null, "応答前に本人が認識"); });
            Add("取消応答を紛失させる", () => tax.FailMessage("cancel/reply"));
            Add("1刻進行：応答は届かない", () => { tax.AdvanceOneTick(); Check(tax.CancellationResultFor("cancel", "character-001") == null, "紛失応答を認識"); });
            Add("観測者から応答の配送失敗を報告", () => tax.ReportMessageFailure("cancel/reply/attempt/1", "failure"));
            Add("1刻進行：失敗報告が応答者に届く", () => tax.AdvanceOneTick());
            Add("応答を再送し、原本は3刻後の遅着へ", () => { tax.RetryCancellationReply("cancel", "character-002"); tax.ScheduleLateMessage("cancel/reply", 1, 3); });
            Add("1刻進行：再送応答を本人が認識", () => { tax.AdvanceOneTick(); Check(tax.CancellationResultFor("cancel", "character-001") == CancellationStatus.Applied, "再送未認識"); });
            Add("2刻進行：遅着原本も到達、二重効力なし", () => { tax.AdvanceOneTick(); tax.AdvanceOneTick(); Check(tax.MessageAttempts.Count(a => a.MessageId == "cancel/reply" && a.Status == AttemptStatus.Delivered) == 2 && tax.TreasuryRice == 100, "重複防止失敗"); });
        }
        else if (mode == 1)
        {
            Add("取消を明示委任した代理人へ配送", () => { tax.Submit("tax", "character-001", "castle-001", 100); tax.DispatchCancellation("cancel", "tax", "character-001", 1, 1);
                tax.SetProxyPermission(new("proxy", "character-002", "character-003", "castle-001", "cancel-command", "試作の明示委任", 0, 100, true, true)); tax.RouteMessage("cancel/command", "character-003"); });
            Add("1刻進行：代理受領・開封、担当本人は未認識", () => { tax.AdvanceOneTick(); Check(tax.Cancellations[0].Delivered && tax.Cancellations[0].PrincipalLearnedAt == null, "受領と認識が混同"); });
            Add("代理権限を失効（既知の記憶は保持）", () => { tax.RevokeProxy("proxy"); Check(tax.KnowsMessage("cancel/command", "character-003"), "記憶消失"); });
            Add("代理人が既知の内容を本人へ報告", () => tax.ForwardMessage("cancel/command", "character-003", "forward"));
            Add("1刻進行：本人認識後に取消効力、応答を発送", () => { tax.AdvanceOneTick(); Check(tax.Cancellations[0].ResolvedAt == 2 && tax.TreasuryRice == 100, "効力時点誤り"); });
            Add("1刻進行：発信者が取消成立を認識", () => { tax.AdvanceOneTick(); Check(tax.CancellationResultFor("cancel", "character-001") == CancellationStatus.Applied, "応答未認識"); });
        }
        else if (mode == 2)
        {
            Add("旧担当宛の徴税命令を発送", () => tax.Submit("old", "character-001", "castle-001", 30));
            Add("役職を交代、旧担当の徴税権限を失効", () => { tax.SetRole("character-002", "元奉行"); tax.SetRole("character-003", "奉行"); tax.Revoke("character-002", "collect-tax"); });
            Add("3刻進行：旧命令を拒否、新担当へ認識は移らない", () => { for (int i = 0; i < 3; i++) tax.AdvanceOneTick(); Check(tax.Orders[0].Phase == TaxPhase.Refused && tax.KnowledgeFor("character-003").Count == 0, "交代境界失敗"); });
            Add("新担当へ根拠・期間付き権限を明示委任", () => tax.GrantAuthority(new("character-003", "castle-001", "collect-tax", "新担当への明示委任", 0, 100)));
            Add("新担当宛に新しい命令を発送", () => tax.Submit("new", "character-001", "castle-001", 30, executorId: "character-003"));
            Add("4刻進行：新担当が30俵を徴収し報告", () => { for (int i = 0; i < 4; i++) tax.AdvanceOneTick(); Check(tax.TreasuryRice == 130, "新担当の実行失敗"); });
        }
        else
        {
            Add("共通基盤で城の視察命令を発送", () => inspection.Submit("inspect", "character-001"));
            Add("視察命令を紛失させる", () => inspection.Fail("inspect/request"));
            Add("1刻進行：命令は未到達", () => inspection.AdvanceOneTick());
            Add("観測者が配送失敗を報告", () => inspection.ReportFailure("inspect/request", "failure"));
            Add("1刻進行：失敗報告が発信者へ到達", () => inspection.AdvanceOneTick());
            Add("命令を再送し、原本は3刻後の遅着へ", () => { inspection.Retry("inspect/request", "character-001"); inspection.ScheduleLate("inspect/request", 1, 3); });
            Add("1刻進行：視察実行、応答到達までは結果不明", () => { inspection.AdvanceOneTick(); Check(inspection.AppliedCount == 1 && inspection.ResultFor("inspect", "character-001") == null, "視察境界失敗"); });
            Add("1刻進行：視察結果が本人へ到達", () => { inspection.AdvanceOneTick(); Check(inspection.ResultFor("inspect", "character-001")?.Outcome == InspectionOutcome.Completed, "視察結果未到達"); });
            Add("1刻進行：原本も到達、視察効力は1回", () => { inspection.AdvanceOneTick(); Check(inspection.AppliedCount == 1 && inspection.Attempts.Count(a => a.MessageId == "inspect/request" && a.Status == AttemptStatus.Delivered) == 2, "視察重複防止失敗"); });
        }
        Refresh();
    }
    private void Load()
    {
        var saved = SnapshotCodec.Read<ProtocolSave>(System.IO.File.ReadAllText(path));
        if (saved.Scenario is < 0 or > 3) throw new InvalidOperationException("検証項目が不正です。");
        // Validate before replacing the visible session.
        var restoredTax = saved.Scenario == 3 ? null : TaxSimulation.LoadJson(saved.Session);
        var restoredInspection = saved.Scenario == 3 ? InspectionSession.LoadJson(saved.Session) : null;
        int max = saved.Scenario is 0 or 3 ? 9 : 6;
        if (saved.Step < 0 || saved.Step > max) throw new InvalidOperationException("段階が不正です。");
        Reset(saved.Scenario); scenario.Select(mode); step = saved.Step;
        if (restoredTax != null) tax = restoredTax; if (restoredInspection != null) inspection = restoredInspection;
    }
    private void Refresh()
    {
        if (next == null) return;
        summary.Text = $"段階：{step} / {steps.Count}    世界時刻：{(mode == 3 ? inspection.Tick : tax.Tick)}刻\n{scenario.GetItemText(mode)}";
        next.Disabled = step == steps.Count; next.Text = next.Disabled ? "この検証は完了しました" : "次：" + steps[step].Caption;
        if (mode == 3) detail.Text = $"視察効力：{inspection.AppliedCount}回    未処理配送：{inspection.PendingCount}\n本人が知る視察結果：{inspection.ResultFor("inspect", "character-001")?.CastleName ?? "未認識"}\n\n{string.Join("\n", inspection.Attempts.Select(a => $"{a.MessageId}  #{a.Number}  {a.Status}"))}";
        else if (mode == 2) detail.Text = $"鏡家の米：{tax.TreasuryRice}俵\n旧担当の役職：{tax.RestoreWorld().Characters["character-002"].Role}\n新担当の役職：{tax.RestoreWorld().Characters["character-003"].Role}\n\n{string.Join("\n", tax.Orders.Select(o => $"{o.RequestId} → {o.ExecutorId}  {o.Phase}  徴収{o.Collected}俵"))}";
        else { var c = tax.Cancellations.FirstOrDefault(); detail.Text = $"鏡家の米：{tax.TreasuryRice}俵    取消の実状態：{c?.Status.ToString() ?? "未発送"}\n正式受領：{c?.ReceivedAt?.ToString() ?? "未受領"}    本人認識：{c?.PrincipalLearnedAt?.ToString() ?? "未認識"}\n発信者が知る取消結果：{tax.CancellationResultFor("cancel", "character-001")?.ToString() ?? "未認識"}\n\n{string.Join("\n", tax.MessageAttempts.Select(a => $"{a.MessageId}  #{a.Number}  {a.Status}"))}"; }
    }
    // Input stays inside this Godot viewport; no native cursor or desktop input API is used.
    private async Task Click(Button button)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var point = button.GetGlobalRect().GetCenter();
        Check(!button.Disabled && button.IsVisibleInTree() && GetViewportRect().HasPoint(point), "検証ボタンが画面外");
        int count = 0; void Pressed() => count++; button.Pressed += Pressed;
        try {
            using var down = new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left };
            using var up = new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false };
            GetViewport().PushInput(down, true);
            GetViewport().PushInput(up, true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); Check(count == 1 && !notice.Text.StartsWith("操作に失敗"), notice.Text);
        } finally { button.Pressed -= Pressed; }
    }
    public async Task SmokeTest()
    {
        try {
            path = ProjectSettings.GlobalizePath("user://shared-smoke-" + Guid.NewGuid().ToString("N") + ".json");
            int boundaries = 0;
            for (int i = 0; i < 4; i++) {
                scenario.Select(i); await Click(reset);
                while (!next.Disabled) {
                    await Click(save); var before = mode == 3 ? inspection.SaveJson() : tax.SaveJson(); int savedStep = step;
                    await Click(next); await Click(load);
                    Check(step == savedStep && before == (mode == 3 ? inspection.SaveJson() : tax.SaveJson()), "保存復旧の不一致");
                    await Click(next); boundaries++;
                }
            }
            notice.Text = $"4項目完了。{boundaries}段階で保存復旧一致。遅着と再送が到達しても効力は各1回。";
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(notice.GetGlobalRect().End.Y <= GetViewportRect().Size.Y, "表示が画面下にはみ出し");
            if (DisplayServer.GetName() != "headless") {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://../Docs/Proposals/Evidence/shared-protocol.png")) == Error.Ok, "画面保存失敗");
            }
            GD.Print($"SHARED_PROTOCOL_GUI_PASS scenarios=4 save_boundaries={boundaries} response_duplicate_effect=1 inspection_effect=1 viewport_input_only=1"); GetTree().Quit(0);
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
