using Godot;
using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.World;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class TaxPanel : MarginContainer
{
    public event Action<WorldState>? WorldChanged;
    public TaxSimulation Simulation => simulation;
    // Reuse the same UI transaction boundary; 3D never owns a separate simulation.
    public void InvokeFrom3D(string action)
    {
        var button=action switch {"submit"=>submit,"advance"=>advance,"pause"=>pause,"save"=>save,"load"=>load,"reset"=>reset,_=>throw new ArgumentException("Unknown 3D action")};
        if(button.Disabled)throw new InvalidOperationException("Action disabled");
        button.EmitSignal(Button.SignalName.Pressed);
    }
    public bool Automatic {get;set;}
    public int ClockSpeed {get;private set;}=1;
    public double PresentationFraction {get;private set;}
    public void SetClockSpeed(int value){if(!new[]{1,2,4}.Contains(value))throw new ArgumentException();ClockSpeed=value;}
    public void ChangeIntegration(Action<TaxSimulation> action){action(simulation);Refresh();}
    public override void _Process(double delta)=>AdvanceDisplayClock(delta);
    public void AdvanceDisplayClock(double delta)
    {
        if(!Automatic||simulation is null||simulation.Paused)return;
        PresentationFraction+=Math.Max(0,Math.Min(delta,.25))*ClockSpeed/1.5;
        while(PresentationFraction>=1-1e-9){simulation.AdvanceOneTick();PresentationFraction=Math.Max(0,PresentationFraction-1);Refresh();}
    }
    void RestoreClock(){PresentationFraction=simulation.OfficerState?.DisplayFraction??0;ClockSpeed=simulation.OfficerState?.DisplaySpeed??1;}
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
    private Button failFailureReport = null!;
    private Button lateArrival = null!;
    private OptionButton fixture = null!;
    private OptionButton deadlineChoice = null!;
    private OptionButton cancellationEffect = null!;
    private Button failCancellation = null!;
    private Button lateCancellation = null!;
    private string savePath = "";
    private int requestNumber;
    private int cancellationNumber;

    public void Initialize(string json, string? storagePath = null)
    {
        scenarioJson = json;
        simulation = TaxSimulation.Create(PrototypeScenario.Load(json));
        simulation.EnableOfficerIntegration();
        savePath = storagePath ?? ProjectSettings.GlobalizePath("user://tax-prototype-v5.json");
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
            simulation.EnableOfficerIntegration();PresentationFraction=0;Automatic=false;ClockSpeed=1;
            requestNumber = 0;
            cancellationNumber = 0;
            notice.Text = "試作条件を初期化しました。保存済みファイルは維持します。";
        });
        deadlineChoice = new OptionButton();
        foreach (var text in new[] { "期限なし", "履行期限：発送2刻後", "履行期限：発送3刻後" }) deadlineChoice.AddItem(text);
        modes.AddChild(deadlineChoice);
        cancellationEffect = new OptionButton();
        foreach (var text in new[] { "取消：到達時に有効", "取消：発送3刻後に有効", "取消：発送4刻後に有効" }) cancellationEffect.AddItem(text);
        modes.AddChild(cancellationEffect);
        summary = new Label(); column.AddChild(summary);
        var operations = new HBoxContainer(); operations.AddThemeConstantOverride("separation", 12); column.AddChild(operations);
        submit = AddButton(operations, "徴税100を発送", () =>
        {
            string id = "tax-" + (requestNumber + 1).ToString("D3");
            long? deadline = deadlineChoice.Selected == 0 ? null : simulation.Tick + (deadlineChoice.Selected == 1 ? 2 : 3);
            simulation.Submit(id, "character-001", "castle-001", 100, deadline); requestNumber++;
            notice.Text = "命令を発送しました。NPCはまだ内容を知りません。";
        });
        advance = AddButton(operations, "1刻進める", () => { simulation.AdvanceOneTick(); if(!simulation.Paused)PresentationFraction=0; notice.Text = simulation.Paused ? "停止中です。" : "1刻進めました。"; });
        pause = AddButton(operations, "停止", () => simulation.SetPaused(!simulation.Paused));
        cancel = AddButton(operations, "取消命令を発送", () =>
        {
            var order = simulation.Orders.Last();
            long effective = simulation.Tick + (cancellationEffect.Selected == 0 ? 1 : cancellationEffect.Selected == 1 ? 3 : 4);
            string id = "cancel-" + (cancellationNumber + 1).ToString("D3");
            simulation.DispatchCancellation(id, order.RequestId, "character-001", 1, effective); cancellationNumber++;
            notice.Text = "取消命令を発送しました。到達・効力時刻までは元の命令が継続します。";
        });
        var deliveryActions = new HBoxContainer(); deliveryActions.AddThemeConstantOverride("separation", 12); column.AddChild(deliveryActions);
        failDelivery = AddButton(deliveryActions, "配送失敗を設定（試験）", () =>
        {
            var d = simulation.Deliveries.Last(d => !d.Delivered && !d.Lost && !d.Cancelled);
            simulation.FailNextDelivery(d.RequestId, d.Kind);
            notice.Text = "次の到達試行を失敗させます。この設定は人物の認識には登録しません。";
        });
        receiveFailure = AddButton(deliveryActions, "失敗報告を発送", () =>
        {
            var d = FailureReportCandidate() ?? throw new InvalidOperationException("発送可能な失敗報告がありません。");
            simulation.DispatchFailureReport(d.Id, "character-003");
            notice.Text = "配送担当者が失敗報告を発送しました。差出人の認識は報告の到達後に更新します。";
        });
        var lateActions = new HBoxContainer(); lateActions.AddThemeConstantOverride("separation", 12); column.AddChild(lateActions);
        failFailureReport = AddButton(lateActions, "失敗報告も紛失（試験）", () =>
        {
            var report = simulation.FailureReports.Last(r => !r.Delivered && !r.Lost);
            simulation.FailFailureReport(report.Id); notice.Text = "配送中の失敗報告を紛失させます。元の差出人は失敗を知りません。";
        });
        lateArrival = AddButton(lateActions, "紛失原本を4刻後に到達（試験）", () =>
        {
            var d = simulation.Deliveries.Last(d => d.Lost && !d.Delivered && !d.Cancelled && d.LateDueAt == null);
            simulation.ScheduleLateArrival(d.Id, 4); notice.Text = "原本の遅着を予約しました。再送分が先に届いても論理命令は一度だけ処理します。";
        });
        retryDelivery = AddButton(deliveryActions, "同じ命令・報告を再送", () =>
        {
            var d = RetryCandidate() ?? throw new InvalidOperationException("再送可能な配送がありません。");
            simulation.RetryDelivery(d.RequestId, d.Kind, d.SenderId);
            notice.Text = "同じ内容を再送しました。徴税要求や成果を新規作成しません。";
        });
        var storage = new HBoxContainer(); storage.AddThemeConstantOverride("separation", 12); column.AddChild(storage);
        save = AddButton(storage, "保存", () => { simulation.CaptureDisplayClock(PresentationFraction,ClockSpeed);simulation.SaveFile(savePath); notice.Text = "保存しました。未処理・適用済み・乱数状態を保持します。"; });
        load = AddButton(storage, "読込", () =>
        {
            var sourcePath = savePath;
            if (!System.IO.File.Exists(sourcePath) && sourcePath == ProjectSettings.GlobalizePath("user://tax-prototype-v5.json"))
            {
                sourcePath = ProjectSettings.GlobalizePath("user://tax-prototype-v4.json");
                if (!System.IO.File.Exists(sourcePath)) sourcePath = ProjectSettings.GlobalizePath("user://tax-prototype-v3.json");
                if (!System.IO.File.Exists(sourcePath)) sourcePath = ProjectSettings.GlobalizePath("user://tax-prototype-v2.json");
                if (!System.IO.File.Exists(sourcePath)) sourcePath = ProjectSettings.GlobalizePath("user://tax-prototype-v1.json");
            }
            var restored = TaxSimulation.LoadFile(sourcePath); // Validate fully before replacing the current session.
            restored.EnableOfficerIntegration();
            simulation = restored;RestoreClock();
            requestNumber = simulation.Orders.Select(o => int.TryParse(o.RequestId.Replace("tax-", ""), out int n) ? n : 0).DefaultIfEmpty(0).Max();
            cancellationNumber = simulation.Cancellations.Select(c => int.TryParse(c.Id.Replace("cancel-", ""), out int n) ? n : 0).DefaultIfEmpty(0).Max();
            notice.Text = "読込完了。保存した時刻・資源・命令状態から再開します。";
        });
        failCancellation = AddButton(storage, "取消配送を紛失（試験）", () =>
        {
            simulation.FailCancellation(simulation.Cancellations.Last(c => c.Status == CancellationStatus.InTransit).Id);
            notice.Text = "取消の配送失敗を設定しました。原命令は発送だけでは止まりません。";
        });
        lateCancellation = AddButton(storage, "取消を1刻後に遅着（試験）", () =>
        {
            simulation.ScheduleLateCancellation(simulation.Cancellations.Last(c => c.Status == CancellationStatus.Lost && c.LateDueAt == null).Id, 1);
            notice.Text = "取消の遅着を予約しました。効力は受領より前へ遡りません。";
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
        TaxPhase.Failed => "失敗", TaxPhase.Cancelled => "取消", TaxPhase.Expired => "期限超過", _ => "不明" };
    private static string CancellationPhase(CancellationStatus status) => status switch {
        CancellationStatus.InTransit => "配送中", CancellationStatus.AwaitingEffect => "受領済み・効力待ち",
        CancellationStatus.Applied => "取消有効", CancellationStatus.AlreadyStopped => "既に停止済み",
        CancellationStatus.TooLate => "徴収確定後の到達", CancellationStatus.Unauthorized => "権限失効", CancellationStatus.Lost => "紛失", _ => "不明" };
    private void Refresh()
    {
        WorldChanged?.Invoke(simulation.RestoreWorld());
        summary.Text = $"世界時刻：{simulation.Tick}刻  /  {(simulation.Paused ? "停止中" : "手動進行")}\n村落所有の米：{simulation.SourceRice}俵（引当 {simulation.ReservedRice}）    鏡家所有の米：{simulation.TreasuryRice}俵\n未処理：{simulation.PendingCount}  適用済み：{simulation.AppliedCount}  失敗報告：到達{simulation.FailureReports.Count(r => r.Delivered)} / 発送{simulation.FailureReports.Count}";
        pause.Text = simulation.Paused ? "再開" : "停止";
        cancel.Disabled = simulation.Orders.Count == 0;
        failCancellation.Disabled = !simulation.Cancellations.Any(c => c.Status == CancellationStatus.InTransit && !c.FailOnArrival);
        lateCancellation.Disabled = !simulation.Cancellations.Any(c => c.Status == CancellationStatus.Lost && c.LateDueAt == null);
        failDelivery.Disabled = !simulation.Deliveries.Any(d => !d.Delivered && !d.Lost && !d.Cancelled && !d.FailOnArrival);
        receiveFailure.Disabled = FailureReportCandidate() == null;
        retryDelivery.Disabled = RetryCandidate() == null;
        failFailureReport.Disabled = !simulation.FailureReports.Any(r => !r.Delivered && !r.Lost && !r.FailOnArrival);
        lateArrival.Disabled = !simulation.Deliveries.Any(d => d.Lost && !d.Delivered && !d.Cancelled && d.LateDueAt == null);
        outcome.Text = simulation.Orders.Count == 0 ? "まだ命令はありません。\n「徴税100を発送」から開始してください。" :
            string.Join("\n\n", simulation.Orders.Select(o => $"{o.RequestId}  /  {Phase(o.Phase)}  /  要求{o.Requested}・引当{o.Reserved}・徴収{o.Collected}\n履行期限：{(o.ExecuteBy?.ToString() ?? "なし")}  停止時刻：{(o.StoppedAt?.ToString() ?? "なし")}  徴収時刻：{(o.CollectedAt?.ToString() ?? "未確定")}\n{(o.Reported ? "発行者が報告を受領済み" : "発行者への結果報告は未到達")}　{o.Reason}"));
        if (simulation.Cancellations.Count > 0)
            outcome.Text += "\n取消配送：" + string.Join(" / ", simulation.Cancellations.Select(c => $"{CancellationPhase(c.Status)}（受領{c.ReceivedAt?.ToString() ?? "未"}・指定効力{c.EffectiveAt}・評価{c.ResolvedAt?.ToString() ?? "未"}）"));
        outcome.Text += $"\n\n個人認識：次郎の受領記録 {simulation.KnowledgeFor("character-002").Count}件 / 太郎の結果報告 {simulation.KnowledgeFor("character-001").Count}件 / 三郎 {simulation.KnowledgeFor("character-003").Count}件";
        if (simulation.Deliveries.Count > 0)
            outcome.Text += "\n\n配送履歴：\n" + string.Join("\n", simulation.Deliveries.Select(d =>
                $"{(d.Kind == DeliveryKind.Order ? "命令" : "報告")} {d.Attempt}/3回目：{(d.Cancelled ? "取消" : d.ArrivedLate ? "遅着済み" : d.Delivered ? "到達済み" : d.Lost ? "紛失" : "配送中")} / 失敗認識：{(simulation.FailureKnown(d.Id) ? "あり" : "なし")}"));
        if (simulation.FailureReports.Count > 0)
            outcome.Text += "\n失敗報告の配送：\n" + string.Join("\n", simulation.FailureReports.Select(r => $"報告{r.Copy}通目：{(r.Delivered ? "到達" : r.Lost ? "紛失" : "配送中")}"));
        GD.Print($"TAX_STATE tick={simulation.Tick} source={simulation.SourceRice} treasury={simulation.TreasuryRice} reserved={simulation.ReservedRice} pending={simulation.PendingCount}");
    }

    public async Task DeadlineCancellationSmokeTest()
    {
        try
        {
            savePath = ProjectSettings.GlobalizePath("user://deadline-smoke-" + Guid.NewGuid().ToString("N") + ".json");
            deadlineChoice.Select(1); await ClickThroughGui(submit); // Complete by tick 2 inclusive.
            await ClickThroughGui(advance); await ClickThroughGui(advance); await ClickThroughGui(save);
            await ClickThroughGui(advance);
            if (simulation.Orders[0].Phase != TaxPhase.Expired || simulation.TreasuryRice != 100) throw new InvalidOperationException("Expiry did not release reservation.");
            await ClickThroughGui(load); await ClickThroughGui(advance);
            if (simulation.Orders[0].StoppedAt != 3 || simulation.ReservedRice != 0) throw new InvalidOperationException("Expiry restore mismatch.");

            await ClickThroughGui(reset); deadlineChoice.Select(2); await ClickThroughGui(submit);
            for (int i = 0; i < 3; i++) await ClickThroughGui(advance);
            if (simulation.Orders[0].CollectedAt != 3 || simulation.TreasuryRice != 160) throw new InvalidOperationException("Inclusive deadline failed.");

            await ClickThroughGui(reset); deadlineChoice.Select(0); cancellationEffect.Select(1);
            await ClickThroughGui(submit); await ClickThroughGui(cancel); await ClickThroughGui(advance);
            if (simulation.Cancellations[0].Status != CancellationStatus.AwaitingEffect || simulation.Orders[0].Phase != TaxPhase.Received)
                throw new InvalidOperationException("Receipt prematurely stopped work.");
            await ClickThroughGui(advance); await ClickThroughGui(save); await ClickThroughGui(advance);
            if (simulation.Cancellations[0].Status != CancellationStatus.Applied || simulation.TreasuryRice != 100) throw new InvalidOperationException("Same-tick cancellation failed.");
            await ClickThroughGui(load); await ClickThroughGui(advance);
            if (simulation.Cancellations[0].ResolvedAt != 3 || simulation.ReservedRice != 0) throw new InvalidOperationException("Cancellation restore failed.");

            await ClickThroughGui(reset); cancellationEffect.Select(0); await ClickThroughGui(submit); await ClickThroughGui(cancel);
            await ClickThroughGui(failCancellation); await ClickThroughGui(advance); await ClickThroughGui(save);
            await ClickThroughGui(lateCancellation); await ClickThroughGui(advance);
            if (simulation.Cancellations[0].ResolvedAt != 2 || simulation.TreasuryRice != 100) throw new InvalidOperationException("Early late-cancellation failed.");
            await ClickThroughGui(load); await ClickThroughGui(lateCancellation); await ClickThroughGui(advance);

            await ClickThroughGui(reset); await ClickThroughGui(submit); await ClickThroughGui(cancel); await ClickThroughGui(failCancellation);
            for (int i = 0; i < 3; i++) await ClickThroughGui(advance);
            await ClickThroughGui(lateCancellation); await ClickThroughGui(save); await ClickThroughGui(advance);
            if (simulation.Cancellations[0].Status != CancellationStatus.TooLate || simulation.TreasuryRice != 160) throw new InvalidOperationException("Late cancellation undid committed rice.");
            await ClickThroughGui(load); await ClickThroughGui(advance);

            await ClickThroughGui(reset); await ClickThroughGui(submit); await ClickThroughGui(failDelivery); await ClickThroughGui(advance);
            await ClickThroughGui(receiveFailure); await ClickThroughGui(advance); await ClickThroughGui(lateArrival);
            await ClickThroughGui(retryDelivery); await ClickThroughGui(cancel); await ClickThroughGui(save);
            await ClickThroughGui(advance); await ClickThroughGui(load); await ClickThroughGui(advance);
            for (int i = 0; i < 3; i++) await ClickThroughGui(advance);
            if (simulation.Orders[0].Phase != TaxPhase.Cancelled || simulation.TreasuryRice != 100 || simulation.ReservedRice != 0 ||
                simulation.Deliveries.Count(d => d.Kind == DeliveryKind.Order && d.Delivered) != 2 ||
                simulation.KnowledgeFor("character-002").Count != 1 || simulation.Orders[0].DecisionRoll != null || simulation.PendingCount != 0)
                throw new InvalidOperationException("Cancellation/late-original race restarted work.");
            notice.Text = "期限ちょうどは確定可。取消は受領後の効力時刻で停止。遅着2通も徴収せず、保存5境界から復旧。";
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (notice.GetGlobalRect().End.Y > GetViewportRect().Size.Y) throw new InvalidOperationException("Deadline UI clipped.");
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var path = ProjectSettings.GlobalizePath("res://../Docs/Proposals/Evidence/deadline-cancellation.png");
                if (GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok) throw new InvalidOperationException("Screenshot failed.");
            }
            GD.Print("DEADLINE_CANCEL_GUI_PASS scenarios=6 loads=5 inclusive=1 cancellation_receipt_effect=1 late_before_after=1 original_receptions=2 collection=0 treasury=100");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private TaxDelivery? FailureReportCandidate() => simulation.Deliveries.LastOrDefault(d => d.Lost && !d.Cancelled && !simulation.FailureKnown(d.Id) &&
        simulation.FailureReports.Count(r => r.DeliveryId == d.Id) < TaxSimulation.MaxDeliveryAttempts &&
        !simulation.FailureReports.Any(r => r.DeliveryId == d.Id && !r.Delivered && !r.Lost));
    private TaxDelivery? RetryCandidate() => simulation.Deliveries.LastOrDefault(d => d.Lost && !d.Delivered && !d.Cancelled &&
        simulation.FailureKnown(d.Id) && d.Attempt < TaxSimulation.MaxDeliveryAttempts &&
        !simulation.Deliveries.Any(other => other.MessageId == d.MessageId && other.Delivered) &&
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
            await ClickThroughGui(advance); // The failure report itself must arrive before retry.
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
            await ClickThroughGui(advance);
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

    public async Task LateDuplicateSmokeTest()
    {
        try
        {
            savePath = ProjectSettings.GlobalizePath("user://late-smoke-" + Guid.NewGuid().ToString("N") + ".json");
            await ClickThroughGui(submit);
            await ClickThroughGui(failDelivery);
            await ClickThroughGui(advance); // 1: order lost.
            await ClickThroughGui(receiveFailure);
            await ClickThroughGui(save); // Failure report in transit; no sender recognition yet.
            await ClickThroughGui(advance);
            await ClickThroughGui(load);
            if (!retryDelivery.Disabled) throw new InvalidOperationException("Loss report recognition leaked before arrival.");
            await ClickThroughGui(advance); // 2: failure report received.
            await ClickThroughGui(lateArrival); // Original arrives at 6.
            await ClickThroughGui(retryDelivery);
            await ClickThroughGui(save);
            await ClickThroughGui(advance);
            await ClickThroughGui(load);
            await ClickThroughGui(advance); // 3: retry received.
            await ClickThroughGui(advance); // 4: decision/reservation.
            await ClickThroughGui(advance); // 5: collect once.
            await ClickThroughGui(failDelivery);
            await ClickThroughGui(advance); // 6: late original + lost result report.
            if (simulation.KnowledgeFor("character-002").Count != 1 || simulation.TreasuryRice != 160)
                throw new InvalidOperationException("Late original repeated logical execution.");
            await ClickThroughGui(receiveFailure);
            await ClickThroughGui(failFailureReport);
            await ClickThroughGui(advance); // 7: failure report itself lost.
            if (!retryDelivery.Disabled || simulation.Orders.Single().Reported) throw new InvalidOperationException("Lost failure report informed sender.");
            await ClickThroughGui(receiveFailure);
            await ClickThroughGui(advance); // 8: a new copy of the failure report is received.
            await ClickThroughGui(lateArrival); // Original result report at 12.
            await ClickThroughGui(retryDelivery);
            await ClickThroughGui(save);
            await ClickThroughGui(advance);
            await ClickThroughGui(load);
            await ClickThroughGui(advance); // 9: result report retry.
            for (int i = 0; i < 3; i++) await ClickThroughGui(advance); // 12: late original result report.
            if (simulation.Deliveries.Count(d => d.Delivered) != 4 || simulation.Deliveries.Count(d => d.ArrivedLate) != 2 ||
                simulation.KnowledgeFor("character-002").Count != 1 || simulation.KnowledgeFor("character-001").Count != 1 ||
                simulation.TreasuryRice != 160 || simulation.SourceRice != 0 || simulation.PendingCount != 0 || !retryDelivery.Disabled ||
                simulation.FailureReports.Count(r => r.Lost) != 1 || simulation.FailureReports.Count(r => r.Delivered) != 2)
                throw new InvalidOperationException("Late duplicate or failure transport mismatch.");
            notice.Text = "原本・再送が4通到達。命令認識・NPC判断・徴収・結果認識は各1回。失敗報告の紛失と再配送も復旧。";
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (notice.GetGlobalRect().End.Y > GetViewportRect().Size.Y) throw new InvalidOperationException("Late UI clipped.");
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var path = ProjectSettings.GlobalizePath("res://../Docs/Proposals/Evidence/late-duplicate.png");
                if (GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok) throw new InvalidOperationException("Screenshot failed.");
            }
            GD.Print("LATE_DUPLICATE_GUI_PASS physical_receptions=4 late=2 logical_order=1 result_knowledge=1 failure_reports_delivered=2 lost=1 loads=3 treasury=160");
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
