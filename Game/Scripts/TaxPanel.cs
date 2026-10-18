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
    private Button failDelivery = null!;
    private Button receiveFailure = null!;
    private Button retryDelivery = null!;
    private OptionButton fixture = null!;
    private string savePath = "";
    private int requestNumber;

    public void Initialize(string json)
    {
        scenarioJson = json;
        simulation = TaxSimulation.Create(PrototypeScenario.Load(json));
        savePath = ProjectSettings.GlobalizePath("user://tax-prototype-v2.json");
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
        var deliveryActions = new HBoxContainer(); deliveryActions.AddThemeConstantOverride("separation", 12); column.AddChild(deliveryActions);
        failDelivery = AddButton(deliveryActions, "配送失敗を設定（試験）", () =>
        {
            var d = simulation.Deliveries.Last(d => !d.Delivered && !d.Lost && !d.Cancelled);
            simulation.FailNextDelivery(d.RequestId, d.Kind);
            notice.Text = "次の到達試行を失敗させます。この設定は人物の認識には登録しません。";
        });
        receiveFailure = AddButton(deliveryActions, "失敗報告を受領", () =>
        {
            var d = simulation.Deliveries.Last(d => d.Lost && !d.Cancelled && !simulation.FailureKnown(d.Id));
            simulation.ReceiveFailureNotice(d.Id, d.SenderId);
            notice.Text = "差出人が配送失敗の報告を受領しました。再送を選択できます。";
        });
        retryDelivery = AddButton(deliveryActions, "同じ命令・報告を再送", () =>
        {
            var d = RetryCandidate() ?? throw new InvalidOperationException("再送可能な配送がありません。");
            simulation.RetryDelivery(d.RequestId, d.Kind, d.SenderId);
            notice.Text = "同じ内容を再送しました。徴税要求や成果を新規作成しません。";
        });
        var storage = new HBoxContainer(); storage.AddThemeConstantOverride("separation", 12); column.AddChild(storage);
        save = AddButton(storage, "保存", () => { simulation.SaveFile(savePath); notice.Text = "保存しました。未処理・適用済み・乱数状態を保持します。"; });
        load = AddButton(storage, "読込", () =>
        {
            var sourcePath = savePath;
            if (!System.IO.File.Exists(sourcePath) && sourcePath == ProjectSettings.GlobalizePath("user://tax-prototype-v2.json"))
                sourcePath = ProjectSettings.GlobalizePath("user://tax-prototype-v1.json");
            var restored = TaxSimulation.LoadFile(sourcePath); // Validate fully before replacing the current session.
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
        failDelivery.Disabled = !simulation.Deliveries.Any(d => !d.Delivered && !d.Lost && !d.Cancelled && !d.FailOnArrival);
        receiveFailure.Disabled = !simulation.Deliveries.Any(d => d.Lost && !d.Cancelled && !simulation.FailureKnown(d.Id));
        retryDelivery.Disabled = RetryCandidate() == null;
        outcome.Text = simulation.Orders.Count == 0 ? "まだ命令はありません。\n「徴税100を発送」から開始してください。" :
            string.Join("\n\n", simulation.Orders.Select(o => $"{o.RequestId}  /  {Phase(o.Phase)}  /  要求{o.Requested}・引当{o.Reserved}・徴収{o.Collected}\n{(o.Reported ? "発行者が報告を受領済み" : "発行者への結果報告は未到達")}　{o.Reason}"));
        outcome.Text += $"\n\n個人認識：次郎の受領記録 {simulation.KnowledgeFor("character-002").Count}件 / 太郎の結果報告 {simulation.KnowledgeFor("character-001").Count}件 / 三郎 {simulation.KnowledgeFor("character-003").Count}件";
        if (simulation.Deliveries.Count > 0)
            outcome.Text += "\n\n配送履歴：\n" + string.Join("\n", simulation.Deliveries.Select(d =>
                $"{(d.Kind == DeliveryKind.Order ? "命令" : "報告")} {d.Attempt}/3回目：{(d.Cancelled ? "取消" : d.Delivered ? "到達済み" : d.Lost ? "紛失" : "配送中")} / 失敗認識：{(simulation.FailureKnown(d.Id) ? "あり" : "なし")}"));
        GD.Print($"TAX_STATE tick={simulation.Tick} source={simulation.SourceRice} treasury={simulation.TreasuryRice} reserved={simulation.ReservedRice} pending={simulation.PendingCount}");
    }

    private TaxDelivery? RetryCandidate() => simulation.Deliveries.LastOrDefault(d => d.Lost && !d.Cancelled &&
        simulation.FailureKnown(d.Id) && d.Attempt < TaxSimulation.MaxDeliveryAttempts &&
        !simulation.Deliveries.Any(other => other.MessageId == d.MessageId && other.Attempt > d.Attempt));

    // Injects only into this Godot viewport. It never moves the Windows pointer or invokes native input tools.
    private async Task ClickThroughGui(Button button)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (button.Disabled || !button.IsVisibleInTree()) throw new InvalidOperationException("Button cannot be clicked: " + button.Text);
        Vector2 position = button.GetGlobalRect().GetCenter();
        if (!GetViewportRect().HasPoint(position)) throw new InvalidOperationException("Button is outside viewport: " + button.Text);
        int presses = 0;
        void CountPress() => presses++;
        button.Pressed += CountPress;
        try
        {
            GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position,
                ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left }, true);
            GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position,
                ButtonIndex = MouseButton.Left, Pressed = false }, true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (presses != 1) throw new InvalidOperationException("GUI click did not activate exactly once: " + button.Text);
        }
        finally { button.Pressed -= CountPress; }
    }

    public async Task DeliveryRecoverySmokeTest()
    {
        try
        {
            savePath = ProjectSettings.GlobalizePath("user://delivery-smoke-" + Guid.NewGuid().ToString("N") + ".json");
            await ClickThroughGui(submit);
            await ClickThroughGui(failDelivery);
            await ClickThroughGui(save);
            await ClickThroughGui(advance); // The order is lost. No knowledge and no automatic retry.
            if (!retryDelivery.Disabled || simulation.KnowledgeFor("character-002").Count != 0 || simulation.PendingCount != 0)
                throw new InvalidOperationException("Loss was treated as recognition or automatic retry.");
            await ClickThroughGui(save);
            await ClickThroughGui(receiveFailure);
            await ClickThroughGui(load); // Restore the unrecognized loss.
            if (!retryDelivery.Disabled) throw new InvalidOperationException("Failure recognition leaked across load.");
            await ClickThroughGui(receiveFailure);
            await ClickThroughGui(retryDelivery);
            await ClickThroughGui(save); // Retried order is in flight.
            await ClickThroughGui(advance);
            await ClickThroughGui(load);
            await ClickThroughGui(advance);
            await ClickThroughGui(advance);
            await ClickThroughGui(advance); // Collection committed once; result report in flight.
            await ClickThroughGui(failDelivery);
            await ClickThroughGui(advance);
            if (simulation.Orders.Single().Reported || simulation.TreasuryRice != 160 || simulation.KnowledgeFor("character-001").Count != 0)
                throw new InvalidOperationException("Lost report changed collection or informed issuer.");
            await ClickThroughGui(receiveFailure);
            await ClickThroughGui(retryDelivery);
            await ClickThroughGui(save);
            await ClickThroughGui(advance);
            await ClickThroughGui(load);
            await ClickThroughGui(advance);
            if (!simulation.Orders.Single().Reported || simulation.TreasuryRice != 160 || simulation.SourceRice != 0 ||
                simulation.KnowledgeFor("character-002").Count != 1 || simulation.KnowledgeFor("character-001").Count != 1 ||
                simulation.Deliveries.Count(d => d.Lost) != 2 || simulation.Deliveries.Count(d => d.Delivered) != 2 || !retryDelivery.Disabled)
                throw new InvalidOperationException("Final recovery or duplicate effect check failed.");
            notice.Text = "命令・報告を各1回紛失→再送。途中保存から復旧し、徴収・認識は各1回です。";
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (notice.GetGlobalRect().End.Y > GetViewportRect().Size.Y) throw new InvalidOperationException("Recovery UI clipped.");
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var path = ProjectSettings.GlobalizePath("res://../Docs/Proposals/Evidence/delivery-recovery.png");
                if (GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok) throw new InvalidOperationException("Screenshot failed.");
            }
            GD.Print("DELIVERY_RECOVERY_GUI_PASS input=viewport_mouse_events order_loss=1 report_loss=1 retries=2 load=3 treasury=160 knowledge_each=1");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
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
