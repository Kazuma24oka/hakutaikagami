using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using HakutaiKagami.Core.Proto;

// 試作版の論功行賞の画面（2026-11-20 ユーザーの印。決まりは Core/Proto/ProtoHome.cs）と、攻城の攻め方の軍議（Core/Proto/ProtoSiege.cs）。
// 軍が鷹野城へ戻り、本人も城にいると開く。まず大将の言葉（話す場面）、そのあと出陣した者みなの役目・兵・働き・勲功の表。
// 「承る」で勲功を受け、預かった兵を返す。開いている間は時を止める。
public partial class ProtoGame
{
    // 攻城の軍議：城に取りついていれば、任務の窓の「攻城の軍議」からいつでも開ける。総大将の考えを聞いて、手を押して進言する。
    // 上の段＝続ける行動（包囲・力攻め）と調略（約束があれば「内応の合図」）。下の段＝勧告の条件（帰参・退去・切腹）。
    internal readonly Dictionary<string, Button> SiegeButtons = new();
    internal void OpenSiegeCouncil()
    {
        if (Scene.Open || askLayer != null || !World.SiegeCouncilOpen) return;
        string now = World.War.Siege!.Method; SiegeButtons.Clear();
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 12);
        Button Pick(HBoxContainer row, string key, string text, string act, bool on = false, bool off = false)
        {
            var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 0), Disabled = off };
            if (on) b.Modulate = new Color(1, 0.88f, 0.45f);
            b.Pressed += () => { var reply = World.Do(act); Scene.Close(); panelKey = ""; Say(reply, World.HeadOf("takano")); };
            row.AddChild(b); SiegeButtons[key] = b; return b;
        }
        var top = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; top.AddThemeConstantOverride("separation", 16); box.AddChild(top);
        foreach (var m in new[] { "包囲", "力攻め" }) Pick(top, m, m, "siege:" + m, m == now);
        string stage = World.PlotStage;
        Pick(top, "調略", stage == "約束" ? "内応合図" : "調略", "siege:調略", stage == "約束", stage is "密書" or "断られた" or "済");
        var campRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; // 本陣移動は攻め方と別の行
        if (World.CanMoveCamp) { var mv = new Button { Text = "本陣移動", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 0) }; mv.Pressed += () => { Scene.Close(); CampPick = true; }; campRow.AddChild(mv); SiegeButtons["本陣移動"] = mv; }
        var low = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; low.AddThemeConstantOverride("separation", 16); box.AddChild(low);
        low.AddChild(new Label { Text = "勧告：", Modulate = new Color(0.62f, 0.62f, 0.62f) }); // 見出し（押せない）は灰色にして、ボタンと分ける
        foreach (var t in ProtoWorld.SurrenderTerms) Pick(low, "勧告:" + t, t, "siege:勧告:" + t, false, World.Talking);
        if (campRow.GetChildCount() > 0) { box.AddChild(new HSeparator()); box.AddChild(campRow); }
        var lines = new List<(string, string)>(); if (World.LeadNear() && ProtoScene.Parse(World, World.SiegeSpeech) is { } said) lines.Add(said); // 総大将が遠ければ、面と向かっては話さない（頼みは使番が運ぶ）
        Scene.Show(lines, [World.Player.Id], box);
    }

    CanvasLayer? awardsLayer; internal Button? AwardsOk;
    void UpdateAwards() { if (FilmRoot == null) ShowAwards(); } // 動画では台本が開く
    internal void ShowAwards()
    {
        if (awardsLayer != null || Scene.Open || !World.AwardsOpen) return;
        var layer = awardsLayer = new CanvasLayer { Layer = 60, Visible = false }; AddChild(layer);
        var dimmer = new ColorRect { Color = new Color(0, 0, 0, 0.55f) }; dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(dimmer);
        var center = new CenterContainer { Theme = new Theme { DefaultFontSize = 22 } }; center.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(center);
        var frame = new PanelContainer(); center.AddChild(frame);
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.09f, 0.09f, 0.11f), BorderColor = new Color(0.75f, 0.6f, 0.3f), BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        var pad = new MarginContainer(); foreach (var m in new[] { "margin_left", "margin_right" }) pad.AddThemeConstantOverride(m, 36); foreach (var m in new[] { "margin_top", "margin_bottom" }) pad.AddThemeConstantOverride(m, 24); frame.AddChild(pad);
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 14); pad.AddChild(col);
        Color gold = new(1, 0.9f, 0.6f), dim = new(0.62f, 0.62f, 0.62f);
        var title = new Label { Text = "論功行賞", HorizontalAlignment = HorizontalAlignment.Center, Modulate = gold }; title.AddThemeFontSizeOverride("font_size", 34); col.AddChild(title);
        string result = World.War.Result switch { "鎮めた" when World.War.Siege != null => "岩切城を落とした", "開城させた" => "岩切城を開かせた", "鎮めた" => "岩切勝元の謀叛を鎮めた", "退かせた" => "岩切勢を退かせた", "守り抜いた" => "陣を守り抜いた", "退いた" => "兵を引いた", var r => r };
        col.AddChild(new Label { Text = result, HorizontalAlignment = HorizontalAlignment.Center });
        col.AddChild(new HSeparator());
        var grid = new GridContainer { Columns = 5 }; grid.AddThemeConstantOverride("h_separation", 40); grid.AddThemeConstantOverride("v_separation", 10); col.AddChild(grid);
        foreach (var h in new[] { "", "役目", "兵", "働き", "勲功" }) grid.AddChild(new Label { Text = h, Modulate = dim });
        string head = World.HeadOf("takano");
        foreach (var a in World.Awards())
        {
            var c = a.Unit == World.Player.Id ? new Color(1, 0.88f, 0.45f) : Colors.White;
            string deed = a.Did ? "役目を果たした" : a.Broken ? "隊が崩れた" : "—";
            foreach (var (text, right) in new[] { (a.Name, false), (a.Role, false), ($"{a.Now}／{a.Start}", true), (deed, false), (a.Unit == head ? "—" : a.Merit.ToString(), true) })
                grid.AddChild(new Label { Text = text, Modulate = c, HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left });
        }
        col.AddChild(new HSeparator());
        var ok = AwardsOk = new Button { Text = "拝受", FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(180, 0) }; col.AddChild(ok);
        ok.Pressed += () =>
        {
            World.Do("awards:take");
            awardsLayer?.QueueFree(); awardsLayer = null; AwardsOk = null; panelKey = ""; RefreshUi(); SaveGame();
        };
        // 大将の言葉を聞き終えたら（話す場面が閉じたら）、表を開く。
        if (ProtoScene.Parse(World, World.AwardsSpeech) is { } said) Scene.Show([said], [World.Player.Id], null, () => { if (IsInstanceValid(layer)) layer.Visible = true; }); else layer.Visible = true;
    }
}
