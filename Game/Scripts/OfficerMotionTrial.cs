using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>Front-only inspection scene; presentation time is independent of world time.</summary>
public partial class OfficerMotionTrial : Control
{
    private OfficerMotionFrames art = null!;
    private ImageTexture[] previousFrames = null!;
    private Sprite2D previousSprite = null!;
    private int selection, frame;
    private bool walking = true, playing = true;
    private double elapsed, speed = 1;
    private readonly Dictionary<string, CheckBox> checks = new();
    private readonly Dictionary<int, ImageTexture[]> cache = new();
    private readonly Queue<int> cacheOrder = new();
    private Sprite2D native = null!, large = null!;
    private readonly List<(Sprite2D sprite, int selection)> comparisons = new();
    private Label status = null!;
    private Button idle = null!, walk = null!, pause = null!, slow = null!, step = null!, restore = null!, armorOff = null!, bare = null!;
    private bool tracking;
    private readonly HashSet<int> observed = new();
    private int observedWraps;
    private readonly List<object> liveResults = new();
    private int FrameIndex => frame + (walking ? 4 : 0);
    private int ClipCount => walking ? 8 : 4;
    private int Duration => walking ? art.WalkDurationMs : art.IdleDurationMs;

    public override void _Ready()
    {
        art = new OfficerMotionFrames(); selection = art.All;
        var previous = new OfficerMotionFrames("res://Data/officer_motion_trial.json");
        previousFrames = Enumerable.Range(0, 12).Select(i => {
            if (OfficerMotionFrames.Hash(previous.Compose(previous.All, i)) != previous.Expected(previous.All, i)) throw new InvalidOperationException("Previous baseline changed.");
            using var image = previous.MakeImage(previous.All, i); return ImageTexture.CreateFromImage(image);
        }).ToArray();
        GetWindow().Title = "百代鏡 — 正面歩行 改善試作";
        Theme = new Theme { DefaultFontSize = 17, DefaultFont = new SystemFont { FontNames = new[] { "Meiryo", "Yu Gothic UI", "sans-serif" } } };
        TextureFilter = TextureFilterEnum.Nearest;
        Text("百代鏡｜武将表示 — 正面歩行の改善試作", new(18, 10), 23, "e6cc95");
        idle = Button("待機", new(18, 48), () => SetClip(false));
        walk = Button("歩行", new(118, 48), () => SetClip(true));
        pause = Button("停止", new(218, 48), () => { playing = !playing; elapsed = 0; UpdateStatus(); });
        slow = Button("低速 1/4", new(318, 48), () => { speed = speed == 1 ? .25 : 1; elapsed = 0; UpdateStatus(); });
        step = Button("1コマ", new(418, 48), () => { playing = false; Advance(); UpdateStatus(); });
        status = Text("", new(18, 91), 16);
        DrawField();
        Text("実寸 64×96・1倍", new(272, 164), 17, "fff0c9");
        native = Sprite(new(312, 254), 1);
        Text("表示確認用の仮背景。合戦処理・移動命令には未接続。", new(36, 402), 15, "e0dbc8");
        Text("装備を着脱（左右一対）", new(704, 48), 18, "e6cc95");
        for (int i = 0; i < art.Keys.Length; i++)
        {
            string key = art.Keys[i];
            var check = new CheckBox { Text = art.Labels[i], Position = new(700 + i % 2 * 160, 81 + i / 2 * 29), Size = new(158, 29), ButtonPressed = true };
            check.Toggled += enabled => { int bit = 1 << Array.IndexOf(art.Keys, key); selection = enabled ? selection | bit : selection & ~bit; Refresh(); };
            checks[key] = check; AddChild(check);
        }
        restore = Button("全装備", new(18, 452), () => Choose(art.All));
        armorOff = Button("甲冑なし", new(128, 452), () => Choose(art.Head));
        bare = Button("裸素体", new(238, 452), () => Choose(0));
        Text("改善案と着脱の比較・右端は前回（1倍）", new(18, 494), 16, "e6cc95");
        var states = new[] { ("全装備", art.All), ("腕・肩なし", art.Without(art.All, "arms", "shoulders")), ("陣羽織なし", art.Without(art.All, "coat")), ("甲冑なし", art.Head), ("裸素体", 0) };
        for (int i = 0; i < states.Length; i++)
        {
            int x = 26 + i * 126;
            Box(new(x, 540, 64, 96), "858d7c");
            Text(states[i].Item1, new(x - 4, 518), 15);
            comparisons.Add((Sprite(new(x, 540), 1), states[i].Item2));
        }
        Box(new(656, 540, 64, 96), "858d7c");
        Text("前回", new(656, 518), 15); previousSprite = Sprite(new(656, 540), 1);
        Text("確認用・4倍（主表示は1倍）", new(704, 232), 17, "e6cc95");
        Box(new(748, 260, 256, 384), "858d7c"); large = Sprite(new(748, 260), 4);
        Text("正面の小さな足踏み試作。素体の体型は仮採用のまま。", new(18, 655), 15, "b4c1c9");
        Refresh();
        GD.Print("OFFICER_MOTION_READY native=64x96 scale=1 parts=8 clips=idle,walk native_input=0");
    }
    private void DrawField()
    {
        Box(new(18, 128, 650, 312), "59684b");
        Box(new(18, 128, 650, 72), "3c5140");
        for (int i = 0; i < 9; i++)
        {
            var tree = new Polygon2D { Color = new Color("2c4436"), Polygon = new[] { new Vector2(45 + i * 75, 136), new Vector2(25 + i * 75, 191), new Vector2(70 + i * 75, 191) } }; AddChild(tree);
        }
        AddChild(new Polygon2D { Color = new Color("9b8b65"), Polygon = new[] { new Vector2(18, 377), new Vector2(18, 405), new Vector2(668, 254), new Vector2(668, 237) } });
        Box(new(270, 345, 150, 2), "d7c58e");
        Text("足裏基準", new(425, 331), 14, "e6dbc1");
        Text("街道・表示検証", new(40, 212), 16);
    }
    private void Box(Rect2 rect, string color) => AddChild(new ColorRect { Position = rect.Position, Size = rect.Size, Color = new Color(color), MouseFilter = MouseFilterEnum.Ignore });
    private Label Text(string text, Vector2 at, int size = 17, string color = "e8e4d8")
    {
        var label = new Label { Text = text, Position = at, Modulate = new Color(color), MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); AddChild(label); return label;
    }
    private Button Button(string text, Vector2 at, Action action)
    {
        var b = new Button { Text = text, Position = at, Size = new(96, 36) }; b.Pressed += action; AddChild(b); return b;
    }
    private Sprite2D Sprite(Vector2 at, int scale)
    {
        var sprite = new Sprite2D { Position = at, Scale = Vector2.One * scale, Centered = false, TextureFilter = TextureFilterEnum.Nearest };
        AddChild(sprite); return sprite;
    }
    private ImageTexture[] Frames(int value)
    {
        if (cache.TryGetValue(value, out var result)) return result;
        result = Enumerable.Range(0, 12).Select(i => { using var image = art.MakeImage(value, i); return ImageTexture.CreateFromImage(image); }).ToArray();
        cache[value] = result; cacheOrder.Enqueue(value);
        if (cache.Count > 16) { int old = cacheOrder.Dequeue(); cache.Remove(old); }
        return result;
    }
    private void SetClip(bool value) { walking = value; frame = 0; elapsed = 0; Refresh(); }
    private void Choose(int value) { selection = value; Refresh(); }
    private void Refresh()
    {
        native.Texture = Frames(selection)[FrameIndex]; large.Texture = native.Texture;
        foreach (var (sprite, selected) in comparisons) sprite.Texture = Frames(selected)[FrameIndex];
        previousSprite.Texture = previousFrames[FrameIndex];
        foreach (var item in checks) item.Value.SetPressedNoSignal(art.Has(selection, item.Key));
        UpdateStatus();
    }
    private void UpdateStatus()
    {
        if (status == null) return;
        pause.Text = playing ? "停止" : "再生"; slow.Text = speed == 1 ? "低速 1/4" : "通常速度";
        status.Text = $"{(walking ? "歩行" : "待機")}　{frame + 1}/{ClipCount}コマ　{(speed == 1 ? "通常速度" : "1/4速度")}　{(playing ? "再生中" : "停止中")}　｜　64×96・1倍";
    }
    private void Advance()
    {
        int old = frame; frame = (frame + 1) % ClipCount;
        if (tracking) { observed.Add(frame); if (frame < old) observedWraps++; }
        Refresh();
    }
    public override void _Process(double delta)
    {
        if (art == null || !playing || !IsVisibleInTree()) return;
        elapsed += delta * speed;
        while (elapsed >= Duration / 1000.0) { elapsed -= Duration / 1000.0; Advance(); }
    }
    private async Task Drawn()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }
    private Rect2I NativeRect()
    {
        var tr = native.GetGlobalTransformWithCanvas();
        if (!tr.X.IsEqualApprox(Vector2.Right) || !tr.Y.IsEqualApprox(Vector2.Down) || !native.Scale.IsEqualApprox(Vector2.One) || native.TextureFilter != TextureFilterEnum.Nearest)
            throw new InvalidOperationException("Native sprite transform/filter differs from1x.");
        return new Rect2I(new Vector2I(Mathf.RoundToInt(tr.Origin.X), Mathf.RoundToInt(tr.Origin.Y)), new(64, 96));
    }
    private void Save(Image image, string file)
    {
        string path = ProjectSettings.GlobalizePath("res://../Docs/Proposals/Evidence/" + file);
        if (image.SavePng(path) != Error.Ok) throw new InvalidOperationException("Godot screenshot failed.");
    }
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    public async Task SmokeTest()
    {
        try
        {
            Require(DisplayServer.GetName() != "headless", "GPU viewport test requires a real display renderer.");
            int cpu = 0;
            for (int flags = 0; flags < 1024; flags++) for (int i = 0; i < 12; i++)
            { Require(OfficerMotionFrames.Hash(art.Compose(flags, i)) == art.Expected(flags, i), $"Native CPU mismatch flags={flags} frame={i}"); cpu++; }
            foreach (bool moving in new[] { true, false }) foreach (bool quarter in new[] { false, true })
            {
                (moving ? walk : idle).EmitSignal(BaseButton.SignalName.Pressed);
                if ((speed == .25) != quarter) slow.EmitSignal(BaseButton.SignalName.Pressed);
                playing = true; elapsed = 0; observed.Clear(); observed.Add(frame); observedWraps = 0; tracking = true;
                await ToSignal(GetTree().CreateTimer((Duration / 1000.0 * ClipCount + .25) / speed), SceneTreeTimer.SignalName.Timeout);
                tracking = false; Require(observed.Count == ClipCount && observedWraps > 0, "Live loop missed frames.");
                liveResults.Add(new { clip = moving ? "walk" : "idle", speed, frames = observed.Order().ToArray(), wraps = observedWraps });
            }
            pause.EmitSignal(BaseButton.SignalName.Pressed); Require(!playing, "Pause signal failed.");
            int before = frame; step.EmitSignal(BaseButton.SignalName.Pressed); Require(frame == (before + 1) % ClipCount && !playing, "Step signal failed.");
            // Check real UI wires while playback continues, without native input.
            walk.EmitSignal(BaseButton.SignalName.Pressed); if (speed != 1) slow.EmitSignal(BaseButton.SignalName.Pressed);
            restore.EmitSignal(BaseButton.SignalName.Pressed); pause.EmitSignal(BaseButton.SignalName.Pressed);
            int toggled = 0;
            foreach (string key in art.Order)
            {
                int previous = FrameIndex;
                checks[key].SetPressedNoSignal(false); checks[key].EmitSignal(BaseButton.SignalName.Toggled, false);
                await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
                Require(!art.Has(selection, key) && playing && FrameIndex != previous, "Dynamic item toggle stopped motion."); toggled++;
            }
            pause.EmitSignal(BaseButton.SignalName.Pressed); playing = false;
            armorOff.EmitSignal(BaseButton.SignalName.Pressed); Require(selection == art.Head, "Armor-off signal failed.");
            bare.EmitSignal(BaseButton.SignalName.Pressed); Require(selection == 0, "Bare signal failed.");
            restore.EmitSignal(BaseButton.SignalName.Pressed); Require(selection == art.All, "Restore signal failed.");
            // Compare actual GPU viewport pixels with independent Python RGBA baseline.
            native.Visible = false; await Drawn();
            using var background = GetViewport().GetTexture().GetImage(); background.Convert(Image.Format.Rgba8);
            var bounds = NativeRect(); var bg = background.GetRegion(bounds).GetData(); native.Visible = true;
            int gpu = 0, maxDifference = 0;
            foreach (int flags in new[] { art.All, 0, art.Head }.Concat(art.Order.Select(k => art.Without(art.All, k))))
            {
                for (int i = 0; i < 12; i++)
                {
                    walking = i >= 4; frame = i >= 4 ? i - 4 : i; Choose(flags); await Drawn();
                    using var viewport = GetViewport().GetTexture().GetImage(); viewport.Convert(Image.Format.Rgba8);
                    using var region = viewport.GetRegion(bounds); var actual = region.GetData(); var expected = art.Compose(flags, i);
                    for (int p = 0; p < expected.Length; p += 4) for (int c = 0; c < 3; c++)
                    {
                        int target = expected[p + 3] == 0 ? bg[p + c] : expected[p + c];
                        int difference = Math.Abs(actual[p + c] - target); maxDifference = Math.Max(maxDifference, difference);
                        Require(difference <= 1, $"GPU pixel mismatch flags={flags} frame={i} byte={p + c} diff={difference}");
                    }
                    gpu++;
                    if (flags == art.All && i == 6) Save(viewport, "officer-walk-v6-godot-equipped.png");
                    if (flags == art.Without(art.All, "arms") && i == 6) Save(viewport, "officer-walk-v6-godot-arms-off.png");
                    if (flags == 0 && i == 2) Save(viewport, "officer-walk-v6-godot-bare.png");
                }
            }
            // The stretch mode must retain literal pixels when the game window changes size.
            GetWindow().Size = new(1280, 800); Choose(art.All); walking = true; frame = 2; Refresh(); await Drawn(); await Drawn();
            var resized = NativeRect(); Require(resized.Size == new Vector2I(64, 96), "Resize changed native dimensions.");
            using (var shot = GetViewport().GetTexture().GetImage()) { Require(shot.GetWidth() == 1280 && shot.GetHeight() == 800, "Window resize not rendered."); Save(shot, "officer-walk-v6-godot-resized.png"); }
            GetWindow().Size = new(1100, 720); await Drawn();
            var largeAt = large.GetGlobalTransformWithCanvas().Origin;
            var report = new { status = "PASS", engine = Engine.GetVersionInfo()["string"].AsString(), dotnet = System.Environment.Version.ToString(), binding = art.Binding, native_canvas = new[] {64,96}, display_scale = 1, native_viewport_rect = new[] { bounds.Position.X, bounds.Position.Y, 64, 96 }, enlarged_viewport_rect = new[] { (int)largeAt.X, (int)largeAt.Y, 256, 384 }, resize_native_viewport_rect = new[] { resized.Position.X, resized.Position.Y, 64, 96 }, cpu_cases = cpu, gpu_cases = gpu, max_rgb_difference = maxDifference, live = liveResults, dynamic_toggles = toggled, resize_viewport = new[] {1280,800}, native_input = 0, previous_baseline_frames = 12, scope = "Improved front walk and previous baseline comparison; world time unchanged" };
            string reportPath = ProjectSettings.GlobalizePath("res://../Art/Officer/FrontWalkV6/runtime-validation.json");
            System.IO.File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            GD.Print($"OFFICER_MOTION_GODOT_PASS cpu={cpu} gpu={gpu} max_rgb_difference={maxDifference} live_clips=2 speeds=2 toggles={toggled} native=64x96 scale=1 native_input=0");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
