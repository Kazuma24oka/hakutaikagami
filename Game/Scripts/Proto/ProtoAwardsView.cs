using Godot;
using System.Linq;
using HakutaiKagami.Core.Proto;

// 試作版の論功行賞の画面（2026-11-20 ユーザーの印。決まりは Core/Proto/ProtoHome.cs）。
// 軍が鷹野城へ戻り、本人も城にいると開く。まず大将の言葉（話す場面）、そのあと出陣した者みなの役目・兵・働き・勲功の表。
// 「承る」で勲功を受け、預かった兵を返す。開いている間は時を止める。
public partial class ProtoGame
{
    CanvasLayer? awardsLayer; internal Button? AwardsOk;
    void UpdateAwards()
    {
        if (awardsLayer != null || Scene.Open || FilmRoot != null || !World.AwardsOpen) return;
        var layer = awardsLayer = new CanvasLayer { Layer = 60, Visible = false }; AddChild(layer);
        var dimmer = new ColorRect { Color = new Color(0, 0, 0, 0.55f) }; dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(dimmer);
        var center = new CenterContainer { Theme = new Theme { DefaultFontSize = 22 } }; center.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(center);
        var frame = new PanelContainer(); center.AddChild(frame);
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.09f, 0.09f, 0.11f), BorderColor = new Color(0.75f, 0.6f, 0.3f), BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        var pad = new MarginContainer(); foreach (var m in new[] { "margin_left", "margin_right" }) pad.AddThemeConstantOverride(m, 36); foreach (var m in new[] { "margin_top", "margin_bottom" }) pad.AddThemeConstantOverride(m, 24); frame.AddChild(pad);
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 14); pad.AddChild(col);
        Color gold = new(1, 0.9f, 0.6f), dim = new(0.62f, 0.62f, 0.62f);
        var title = new Label { Text = "論功行賞", HorizontalAlignment = HorizontalAlignment.Center, Modulate = gold }; title.AddThemeFontSizeOverride("font_size", 34); col.AddChild(title);
        string result = World.War.Result switch { "鎮めた" => "岩切勝元の謀叛を鎮めた", "退かせた" => "岩切勢を退かせた", "守り抜いた" => "陣を守り抜いた", "退いた" => "兵を引いた", var r => r };
        col.AddChild(new Label { Text = result, HorizontalAlignment = HorizontalAlignment.Center });
        col.AddChild(new HSeparator());
        var grid = new GridContainer { Columns = 5 }; grid.AddThemeConstantOverride("h_separation", 40); grid.AddThemeConstantOverride("v_separation", 10); col.AddChild(grid);
        foreach (var h in new[] { "", "役目", "兵", "働き", "勲功" }) grid.AddChild(new Label { Text = h, Modulate = dim });
        string head = World.HeadOf("takano");
        foreach (var a in World.Awards())
        {
            var c = a.Unit == World.Player.Id ? new Color(1, 0.88f, 0.45f) : Colors.White;
            string deed = a.Did ? "役目を果たした" : a.Broken ? "隊が崩れた" : "—";
            foreach (var (text, right) in new[] { (a.Name, false), (a.Role, false), ($"{a.Now}／{a.Start}", true), (deed, false), (a.Unit == head ? "" : a.Merit.ToString(), true) })
                grid.AddChild(new Label { Text = text, Modulate = c, HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left });
        }
        col.AddChild(new HSeparator());
        var ok = AwardsOk = new Button { Text = "承る", FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(180, 0) }; col.AddChild(ok);
        ok.Pressed += () =>
        {
            World.Do("awards:take");
            awardsLayer?.QueueFree(); awardsLayer = null; AwardsOk = null; panelKey = ""; RefreshUi(); SaveGame();
        };
        // 大将の言葉を聞き終えたら（話す場面が閉じたら）、表を開く。
        if (ProtoScene.Parse(World, World.AwardsSpeech) is { } said) Scene.Show([said], [World.Player.Id], null, () => { if (IsInstanceValid(layer)) layer.Visible = true; }); else layer.Visible = true;
    }
}
