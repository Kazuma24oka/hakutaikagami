using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class OfficerContextTrial : Control
{
    private readonly Vector2I[] sizes = { new(32, 48), new(48, 64), new(64, 96) };
    private Control contents = null!;
    private Label status = null!;
    private int selected, zoom;
    private bool dialogue;
    private readonly Button[] sizeButtons = new Button[3];
    private Button battleButton = null!, dialogueButton = null!, zoomButton = null!;
    public override void _Ready()
    {
        using var profile = System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://Data/officer_visual_profile.json"));
        var canvas = profile.RootElement.GetProperty("canvas_pixels");
        selected = Array.FindIndex(sizes, size => size.X == canvas[0].GetInt32() && size.Y == canvas[1].GetInt32());
        zoom = profile.RootElement.GetProperty("default_display_scale").GetInt32();
        if (selected < 0 || zoom is not (1 or 2)) throw new InvalidOperationException("Unsupported officer visual profile.");
        Theme = new Theme { DefaultFontSize = 18, DefaultFont = new SystemFont { FontNames = new[] { "Meiryo", "Yu Gothic UI", "sans-serif" } } };
        Label(this, "百代鏡｜ゲーム画面での大きさ比較（仮背景・仮UI）", new(20, 8));
        battleButton = Button("合戦", new(20, 43), () => { dialogue = false; Refresh(); });
        dialogueButton = Button("内応交渉", new(150, 43), () => { dialogue = true; Refresh(); });
        for (int i = 0; i < 3; i++)
        {
            int n = i;
            sizeButtons[i] = Button($"{sizes[i].X}×{sizes[i].Y}", new(330 + i * 140, 43), () => { selected = n; Refresh(); });
        }
        zoomButton = Button("倍率1 / 2", new(790, 43), () => { zoom = zoom == 1 ? 2 : 1; Refresh(); });
        status = Label(this, "", new(20, 92));
        contents = new Control(); AddChild(contents); contents.MouseFilter = MouseFilterEnum.Ignore;
        Refresh();
        if (OS.GetCmdlineUserArgs().Contains("--context-smoke-test")) _ = Smoke();
    }
    private Button Button(string caption, Vector2 at, Action action)
    {
        var b = new Button { Text = caption, Position = at, Size = new(120, 40) };
        b.Pressed += action; AddChild(b); return b;
    }
    private static Label Label(Node parent, string text, Vector2 at, int font = 18, string color = "e9e5d9")
    {
        var l = new Label { Text = text, Position = at, Modulate = new Color(color), MouseFilter = MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", font); parent.AddChild(l); return l;
    }
    private void Rect(Rect2 rect, string color)
    { contents.AddChild(new ColorRect { Position = rect.Position, Size = rect.Size, Color = new Color(color), MouseFilter = MouseFilterEnum.Ignore }); }
    private void Poly(Vector2[] points, string color)
    { contents.AddChild(new Polygon2D { Polygon = points, Color = new Color(color) }); }
    private void Ellipse(Vector2 center, Vector2 radius, string color)
    {
        Poly(Enumerable.Range(0, 24).Select(i => center + new Vector2(Mathf.Cos(i * Mathf.Tau / 24) * radius.X, Mathf.Sin(i * Mathf.Tau / 24) * radius.Y)).ToArray(), color);
    }
    private Sprite2D Officer(Vector2 sole)
    {
        var size = sizes[selected];
        var sprite = new Sprite2D { Texture = GD.Load<Texture2D>($"res://Assets/Trials/OfficerContextV1/front-{size.X}x{size.Y}.png"),
            Centered = false, TextureFilter = TextureFilterEnum.Nearest, Scale = Vector2.One * zoom,
            Position = sole - new Vector2(size.X * zoom / 2f, (size.Y - 4) * zoom) };
        contents.AddChild(sprite); return sprite;
    }
    private void Refresh()
    {
        if (contents == null) return;
        foreach (var child in contents.GetChildren()) { contents.RemoveChild(child); child.QueueFree(); }
        status.Text = $"{(dialogue ? "内応交渉" : "合戦")}　小型ドット {sizes[selected].X}×{sizes[selected].Y} / {zoom}倍　表示枠 {sizes[selected].X * zoom}×{sizes[selected].Y * zoom}px";
        if (dialogue) DrawDialogue(); else DrawBattle();
        Label(contents, "サイズ選定用。戦闘・交渉機能は未接続／背景・相手・会話は仮／外見・透過・動作の正式受入前", new(20, 689), 15, "c9c6b8");
    }
    private void DrawBattle()
    {
        Rect(new(0, 130, 1100, 470), "58654a");
        Poly(new[] { new Vector2(0, 170), new Vector2(180, 140), new Vector2(310, 190), new Vector2(490, 150), new Vector2(650, 185), new Vector2(820, 145), new Vector2(1100, 165), new Vector2(1100, 235), new Vector2(0, 235) }, "414f3e");
        Poly(new[] { new Vector2(450, 130), new Vector2(510, 130), new Vector2(560, 300), new Vector2(670, 450), new Vector2(820, 600), new Vector2(720, 600), new Vector2(610, 470), new Vector2(510, 320) }, "557e87");
        Poly(new[] { new Vector2(0, 520), new Vector2(0, 565), new Vector2(750, 320), new Vector2(1100, 295), new Vector2(1100, 265), new Vector2(730, 290) }, "a08c65");
        Rect(new(561, 402, 90, 26), "807459");
        for (int i = 0; i < 10; i++)
        {
            float x = 32 + i * 112;
            Ellipse(new(x, 198 + i % 3 * 22), new(22, 15), "354c3d");
        }
        var places = new[] { new Vector2(130, 330), new Vector2(260, 320), new Vector2(380, 350), new Vector2(150, 480), new Vector2(300, 495), new Vector2(475, 480), new Vector2(690, 330), new Vector2(840, 320), new Vector2(980, 345), new Vector2(730, 480), new Vector2(875, 495), new Vector2(1010, 475) };
        foreach (var at in places) Ellipse(at + new Vector2(0, -1), new(22, 7), "404937");
        foreach (var item in places.Select((at, i) => (at, i)).OrderBy(p => p.at.Y))
        {
            Officer(item.at);
            Rect(new(item.at.X - 21, item.at.Y + 7, 42, 5), item.i < 6 ? "dfc47d" : "98c0d1");
            Label(contents, item.i < 6 ? "鏡家" : "敵方", item.at + new Vector2(-22, 13), 13);
        }
        // Selection ring is an authoring marker, separate from the sprite's transform.
        var ring = new Line2D { Width = 2, DefaultColor = new("f4dda2"), Closed = true };
        foreach (var i in Enumerable.Range(0, 32)) ring.AddPoint(new Vector2(475 + Mathf.Cos(i * Mathf.Tau / 32) * 32, 481 + Mathf.Sin(i * Mathf.Tau / 32) * 10));
        contents.AddChild(ring);
        Rect(new(12, 610, 1076, 66), "202a2b");
        Label(contents, "選択：鏡家の武将（試料）　／　同じ人物を12体配置して混雑と見分けやすさを比較", new(28, 616), 17);
        Label(contents, "移動　　待機　　攻撃　　　※命令の実行・戦闘・歩行は未実装", new(28, 645), 17, "b8c4bd");
    }
    private void DrawDialogue()
    {
        Rect(new(0, 130, 1100, 310), "66513d");
        Rect(new(0, 130, 1100, 155), "d0c4a2");
        for (int x = 40; x < 1100; x += 135) Rect(new(x, 130, 9, 155), "6a5542");
        for (int y = 165; y < 285; y += 43) Rect(new(0, y, 1100, 4), "ad9b7e");
        for (int y = 305; y < 440; y += 40) Rect(new(0, y, 1100, 2), "826d50");
        Rect(new(500, 307, 100, 90), "40372d");
        Ellipse(new(330, 414), new(27, 8), "493e31");
        Ellipse(new(770, 414), new(27, 8), "493e31");
        Officer(new(330, 414)); Officer(new(770, 414));
        Label(contents, "使者", new(310, 418), 15); Label(contents, "対面相手（同一試料で代用）", new(680, 418), 15);
        Rect(new(12, 450, 1076, 226), "272b2b");
        Rect(new(25, 464, 166, 192), "9b8a71");
        contents.AddChild(new TextureRect {
            Texture = GD.Load<Texture2D>("res://Assets/Trials/OfficerContextV1/dialogue-face-reference.png"),
            Position = new(29, 468), Size = new(158, 184), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
        Label(contents, "6頭身側の顔参照", new(28, 653), 14, "dccb9d");
        Label(contents, "鏡家の使者", new(215, 466), 22, "e8cd8c");
        Label(contents, "『今ここで返答を迫るつもりはございません。\n　まずは、こちらの書状をお納めください。』", new(215, 504), 20);
        Rect(new(215, 578, 390, 36), "435049");
        Label(contents, "書状を差し出す", new(227, 580), 17);
        Rect(new(215, 622, 390, 36), "394344");
        Label(contents, "条件を確かめる", new(227, 624), 17);
        Label(contents, "顔カードの表示サイズは固定。\n小型ドットの寸法だけを変更。\n\n交渉結果・感情・信用の\nゲームロジックは未接続。", new(730, 483), 17, "b9c6c3");
    }
    private async Task Smoke()
    {
        try
        {
            int captures = 0, states = 0;
            foreach (int magnification in new[] { 1, 2 })
            {
                if (zoom != magnification) zoomButton.EmitSignal(BaseButton.SignalName.Pressed);
                foreach (bool talk in new[] { false, true })
                {
                    (talk ? dialogueButton : battleButton).EmitSignal(BaseButton.SignalName.Pressed);
                    for (int i = 0; i < 3; i++)
                    {
                        sizeButtons[i].EmitSignal(BaseButton.SignalName.Pressed);
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        var models = contents.GetChildren().OfType<Sprite2D>().ToArray();
                        if (models.Length != (talk ? 2 : 12) || models.Any(s => s.Texture.GetSize() != sizes[i] || s.TextureFilter != TextureFilterEnum.Nearest || s.Scale != Vector2.One * magnification))
                            throw new InvalidOperationException("Context size/filter/placement count mismatch.");
                        if (models.Any(s => s.Position.Y < 130 || s.Position.Y + s.Texture.GetHeight() * magnification > (talk ? 440 : 600) || s.Position.X < 0 || s.Position.X + s.Texture.GetWidth() * magnification > 1100))
                            throw new InvalidOperationException("Sprite extends into fixed UI or outside field.");
                        if (DisplayServer.GetName() != "headless")
                        {
                            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                            string suffix = magnification == 1 ? "-1x" : "";
                            string path = $"res://../Docs/Proposals/Evidence/officer-context-{(talk ? "dialogue" : "battle")}-{sizes[i].X}x{sizes[i].Y}{suffix}.png";
                            if (GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath(path)) != Error.Ok)
                                throw new InvalidOperationException("Context capture failed.");
                            captures++;
                        }
                        states++;
                    }
                }
            }
            GD.Print($"OFFICER_CONTEXT_SCENE_PASS scenes=2 sizes=3 states={states} captures={captures} zooms=1,2 native_input=0");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
