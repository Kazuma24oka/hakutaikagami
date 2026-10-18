using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class OfficerScaleTrial : Control
{
    private readonly Vector2I[] sizes = { new(32, 48), new(48, 64), new(64, 96) };
    private readonly Sprite2D[] sprites = new Sprite2D[3];
    private readonly Button[] zoomButtons = new Button[3];
    private Label note = null!;
    public override void _Ready()
    {
        Theme = new Theme { DefaultFontSize = 19,
            DefaultFont = new SystemFont { FontNames = new[] { "Meiryo", "Yu Gothic UI", "sans-serif" } } };
        AddLabel("百代鏡｜原寸と整数倍率の比較", new(24, 15));
        AddLabel("同じ原画・同じ足底・全体等方縮小。背景付きのサイズ試料です。", new(24, 53));
        AddLabel("左：32×48　　中央：48×64　　右：64×96　／　採用サイズは未決定", new(24, 84));
        for (int i = 0; i < 3; i++)
        {
            int zoom = new[] { 1, 2, 4 }[i];
            var button = new Button { Text = zoom + "倍で表示", Position = new(24 + i * 185, 126), Size = new(170, 45) };
            button.Pressed += () => SetZoom(zoom); AddChild(button); zoomButtons[i] = button;
        }
        for (int i = 0; i < 3; i++)
        {
            float x = 180 + 360 * i;
            AddLabel($"{sizes[i].X}×{sizes[i].Y}", new(x - 45, 192));
            sprites[i] = new Sprite2D {
                Texture = GD.Load<Texture2D>($"res://Assets/Trials/OfficerScaleV1/front-{sizes[i].X}x{sizes[i].Y}.png"),
                Centered = false, TextureFilter = TextureFilterEnum.Nearest };
            AddChild(sprites[i]);
            var line = new Line2D { Width = 1, DefaultColor = new("d78a75") };
            line.AddPoint(new(x - 155, 615)); line.AddPoint(new(x + 155, 615)); AddChild(line);
        }
        note = AddLabel("", new(24, 650));
        AddLabel("顔・髭の読みやすさを比較。透過・パーツ・リグ・歩行の合格ではありません。", new(24, 684));
        SetZoom(2);
        if (OS.GetCmdlineUserArgs().Contains("--scale-smoke-test")) _ = Smoke();
    }
    private Label AddLabel(string text, Vector2 position)
    { var label = new Label { Text = text, Position = position }; AddChild(label); return label; }
    private void SetZoom(int zoom)
    {
        for (int i = 0; i < 3; i++)
        {
            sprites[i].Scale = Vector2.One * zoom;
            // The sole is an author-defined source anchor; do not re-fit each output's alpha bounds.
            sprites[i].Position = new(180 + 360 * i - sizes[i].X * zoom / 2f, 615 - (sizes[i].Y - 4) * zoom);
        }
        note.Text = $"現在 {zoom}倍／表示枠：{32 * zoom}×{48 * zoom}・{48 * zoom}×{64 * zoom}・{64 * zoom}×{96 * zoom}px";
    }
    private async Task Smoke()
    {
        try
        {
            foreach (var button in zoomButtons)
            {
                button.EmitSignal(BaseButton.SignalName.Pressed);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                for (int i = 0; i < 3; i++)
                {
                    if (sprites[i].Texture.GetSize() != sizes[i] || sprites[i].TextureFilter != TextureFilterEnum.Nearest ||
                        sprites[i].Position.Y + (sizes[i].Y - 4) * sprites[i].Scale.Y != 615 ||
                        sprites[i].Scale.X != sprites[i].Scale.Y)
                        throw new InvalidOperationException("Scale/filter/anchor mismatch.");
                }
            }
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                if (GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://../Docs/Proposals/Evidence/officer-scale-godot.png")) != Error.Ok)
                    throw new InvalidOperationException("Viewport capture failed.");
            }
            GD.Print("OFFICER_SCALE_SCENE_PASS candidates=3 integer_zooms=3 nearest=1 sole=615 native_input=0");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
