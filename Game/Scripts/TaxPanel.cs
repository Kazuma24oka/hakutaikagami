using Godot;
using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.World;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class TaxPanel : MarginContainer
{
    public event Action<WorldState>? WorldChanged;
    private TaxSimulation simulation = null!;
    private string scenarioJson = "";
    private Label summary = null!;
    private Label outcome = null!;
    private Label notice = null!;
    private Button submit = null!;
    private Button advance = null!;
    private Button pause = null!;
    private Button save = null!;
    private Button load = null!;
    private Button cancel = null!;
    private Button reset = null!;
    private OptionButton fixture = null!;
    private string savePath = "";
    private int requestNumber;

    public void Initialize(string json)
    {
        scenarioJson = json;
        simulation = TaxSimulation.Create(PrototypeScenario.Load(json));
        savePath = ProjectSettings.GlobalizePath("user://tax-prototype-v1.json");
        foreach (var edge in new[] { "left", "top", "right", "bottom" }) AddThemeConstantOverride("margin_" + edge, 28);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); AddChild(column);
        column.AddChild(new Label { Text = "徴税・命令配送・保存復旧  /  試作検証", Modulate = new Color("e2c891") });
        column.AddChild(new Label { Text = "1刻ずつ進めて、到達 → 判断・引当 → 徴収 → 報告を確認します。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var modes = new HBoxContainer(); modes.AddThemeConstantOverride("separation", 16); column.AddChild(modes);
        fixture = new OptionButton();
        foreach (var text in new[] { "部分達成：米60 / 要求100", "正常：米100 / 要求100", "拒否：NPCが従わない", "失敗：徴収可能な米0" }) fixture.AddItem(text);
        modes.AddChild(fixture);
        reset = AddButton(modes, "初期化", () =>
        {
            int rice = fixture.Selected == 1 ? 100 : fixture.Selected == 3 ? 0 : 60;
            simulation = TaxSimulation.Create(PrototypeScenario.Load(scenarioJson), rice, fixture.Selected == 2 ? 0 : 100);
            requestNumber = 0;
            notice.Text = "試作条件を初期化しました。保存済みファイルは維持します。";
        });
        summary = new Label(); column.AddChild(summary);
        var operations = new HBoxContainer(); operations.AddThemeConstantOverride("separation", 12); column.AddChild(operations);
        submit = AddButton(operations, "徴税100を発送", () =>
        {
            string id = "tax-" + (requestNumber + 1).ToString("D3");
            simulation.Submit(id, "character-001", "castle-001", 100); requestNumber++;
            notice.Text = "命令を発送しました。NPCはまだ内容を知りません。";
        });
        advance = AddButton(operations, "1刻進める", () => { simulation.AdvanceOneTick(); notice.Text = simulation.Paused ? "停止中です。" : "1刻進めました。"; });
        pause = AddButton(operations, "停止", () => simulation.SetPaused(!simulation.Paused));
        cancel = AddButton(operations, "未確定命令を取消", () =>
        {
            var order = simulation.Orders.Last(o => o.Phase is TaxPhase.Dispatched or TaxPhase.Received or TaxPhase.Reserved);
            simulation.Cancel(order.RequestId); notice.Text = "未使用の引当を解除し、取消報告を配送します。";
        });
        var storage = new HBoxContainer(); storage.AddThemeConstantOverride("separation", 12); column.AddChild(storage);
        save = AddButton(storage, "保存", () => { simulation.SaveFile(savePath); notice.Text = "保存しました。未処理・適用済み・乱数状態を保持します。"; });
        load = AddButton(storage, "読込", () =>
        {
            var restored = TaxSimulation.LoadFile(savePath); // Validate fully before replacing the current session.
            simulation = restored;
            requestNumber = simulation.Orders.Select(o => int.TryParse(o.RequestId.Replace("tax-", ""), out int n) ? n : 0).DefaultIfEmpty(0).Max();
            notice.Text = "読込完了。保存した時刻・資源・命令状態から再開します。";
        });
        column.AddChild(new HSeparator());
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 80) };
        column.AddChild(scroll);
        outcome = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        scroll.AddChild(outcome);
        notice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = "検証用の管理表示です。正式なゲームUI・徴税制度ではありません。", Modulate = new Color("a6b7c7") };
        column.AddChild(notice);
        Refresh();
    }

    private Button AddButton(HBoxContainer row, string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 44) };
        button.Pressed += () =>
        {
            try { action(); }
            catch (Exception error) { notice.Text = "操作に失敗しました。現在の状態は保持します：" + error.Message; GD.Print("TAX_OPERATION_FAILED " + error.GetType().Name); }
            Refresh();
        };
        row.AddChild(button); return button;
    }
    private static string Phase(TaxPhase phase) => phase switch {
        TaxPhase.Dispatched => "配送中", TaxPhase.Received => "本人が受領", TaxPhase.Reserved => "判断済み・引当中",
        TaxPhase.Completed => "全量達成", TaxPhase.Partial => "部分達成", TaxPhase.Refused => "NPC拒否",
        TaxPhase.Failed => "失敗", TaxPhase.Cancelled => "取消", _ => "不明" };
    private void Refresh()
    {
        WorldChanged?.Invoke(simulation.RestoreWorld());
        summary.Text = $"世界時刻：{simulation.Tick}刻  /  {(simulation.Paused ? "停止中" : "手動進行")}\n村落所有の米：{simulation.SourceRice}俵（引当 {simulation.ReservedRice}）    鏡家所有の米：{simulation.TreasuryRice}俵\n未処理イベント：{simulation.PendingCount}    適用済み：{simulation.AppliedCount}";
        pause.Text = simulation.Paused ? "再開" : "停止";
        cancel.Disabled = !simulation.Orders.Any(o => o.Phase is TaxPhase.Dispatched or TaxPhase.Received or TaxPhase.Reserved);
        outcome.Text = simulation.Orders.Count == 0 ? "まだ命令はありません。\n「徴税100を発送」から開始してください。" :
            string.Join("\n\n", simulation.Orders.Select(o => $"{o.RequestId}  /  {Phase(o.Phase)}  /  要求{o.Requested}・引当{o.Reserved}・徴収{o.Collected}\n{(o.Reported ? "発行者が報告を受領済み" : "発行者への結果報告は未到達")}　{o.Reason}"));
        outcome.Text += $"\n\n個人認識：次郎の受領記録 {simulation.KnowledgeFor("character-002").Count}件 / 太郎の結果報告 {simulation.KnowledgeFor("character-001").Count}件 / 三郎 {simulation.KnowledgeFor("character-003").Count}件";
        GD.Print($"TAX_STATE tick={simulation.Tick} source={simulation.SourceRice} treasury={simulation.TreasuryRice} reserved={simulation.ReservedRice} pending={simulation.PendingCount}");
    }

    public async Task SmokeTest()
    {
        try
        {
            // Isolate test saves from normal user saves and preserve all previous test outputs.
            savePath = ProjectSettings.GlobalizePath("user://tax-smoke-" + Guid.NewGuid().ToString("N") + ".json");
            submit.EmitSignal(BaseButton.SignalName.Pressed);
            advance.EmitSignal(BaseButton.SignalName.Pressed); // Delivery.
            pause.EmitSignal(BaseButton.SignalName.Pressed);
            advance.EmitSignal(BaseButton.SignalName.Pressed);
            if (simulation.Tick != 1) throw new InvalidOperationException("Pause failed.");
            pause.EmitSignal(BaseButton.SignalName.Pressed);
            advance.EmitSignal(BaseButton.SignalName.Pressed); // Reservation.
            save.EmitSignal(BaseButton.SignalName.Pressed);
            if (!System.IO.File.Exists(savePath)) throw new InvalidOperationException("Save missing.");
            advance.EmitSignal(BaseButton.SignalName.Pressed); // Collection.
            if (simulation.TreasuryRice != 160) throw new InvalidOperationException("Collection failed.");
            load.EmitSignal(BaseButton.SignalName.Pressed);
            if (simulation.Tick != 2 || simulation.TreasuryRice != 100 || simulation.ReservedRice != 60)
                throw new InvalidOperationException("Load boundary mismatch.");
            advance.EmitSignal(BaseButton.SignalName.Pressed);
            advance.EmitSignal(BaseButton.SignalName.Pressed);
            if (!simulation.Orders.Single().Reported || simulation.TreasuryRice != 160 || !outcome.Text.Contains("部分達成"))
                throw new InvalidOperationException("Report or UI mismatch.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (notice.GetGlobalRect().End.Y > GetViewportRect().Size.Y)
                throw new InvalidOperationException("Status message extends below viewport: " + notice.GetGlobalRect());
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                string path = ProjectSettings.GlobalizePath("res://../Docs/Proposals/Evidence/tax-vertical.png");
                DirAccess.MakeDirRecursiveAbsolute(System.IO.Path.GetDirectoryName(path)!);
                if (GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok) throw new InvalidOperationException("Screenshot failed.");
            }
            var beforeBadLoad = simulation.SaveJson();
            System.IO.File.WriteAllText(savePath, "{broken");
            load.EmitSignal(BaseButton.SignalName.Pressed);
            if (simulation.SaveJson() != beforeBadLoad || !notice.Text.Contains("操作に失敗"))
                throw new InvalidOperationException("Failed load replaced current session.");
            save.EmitSignal(BaseButton.SignalName.Pressed);
            for (int mode = 1; mode <= 3; mode++)
            {
                fixture.Select(mode);
                reset.EmitSignal(BaseButton.SignalName.Pressed);
                submit.EmitSignal(BaseButton.SignalName.Pressed);
                for (int i = 0; i < 4; i++) advance.EmitSignal(BaseButton.SignalName.Pressed);
                var expected = mode == 1 ? TaxPhase.Completed : mode == 2 ? TaxPhase.Refused : TaxPhase.Failed;
                if (simulation.Orders.Single().Phase != expected || !simulation.Orders[0].Reported)
                    throw new InvalidOperationException("Fixture scene mismatch: " + mode);
            }
            GD.Print("TAX_SCENE_PASS modes=4 delivery=1 pause=1 reserve=60 collect=60 save=1 load=1 report=1 corrupt_load_preserved=1");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
