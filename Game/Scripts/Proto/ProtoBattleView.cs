using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using HakutaiKagami.Core.Proto;

// 試作版の合戦の見え方（2026-11-19 組み合わせ案。Core/Proto/ProtoClash.cs）。
// ・上：両方の総士気。
// ・地図：ぶつかっている隊どうしの間に押し合いのゲージ（左が鷹野、右が岩切。押している側が伸びる）。要所の旗は持っている家の色。
// ・下：本人の隊がぶつかっている間だけ、采配（構え・戦法・かかれ）。ぶつかった瞬間に速さを10倍に落とす。
public partial class ProtoGame
{
    PanelContainer spiritBox = null!, commandBox = null!;
    ProgressBar spiritTakano = null!, spiritRebel = null!, kiBar = null!, tacticBar = null!;
    Label spiritTakanoText = null!, spiritRebelText = null!, meText = null!, foeText = null!;
    TextureRect mePic = null!, foePic = null!, pushPic = null!;
    readonly Image pushImage = Image.CreateEmpty(240, 18, false, Image.Format.Rgba8);
    ImageTexture pushTexture = null!;
    readonly Dictionary<string, (Sprite3D Bar, ImageTexture Tex, Image Img)> clashBars = new();
    string lastClash = "";

    static ProgressBar Bar(Color fill, float max, Vector2 size)
    {
        var b = new ProgressBar { MaxValue = max, ShowPercentage = false, CustomMinimumSize = size, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        b.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = fill });
        b.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.14f) });
        return b;
    }

    void BuildBattleView()
    {
        var layer = new CanvasLayer { Layer = 2 }; AddChild(layer);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = new Theme { DefaultFontSize = 18 } }; root.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(root);

        spiritBox = new PanelContainer(); spiritBox.SetAnchorsPreset(Control.LayoutPreset.CenterTop); spiritBox.OffsetTop = 50; spiritBox.OffsetLeft = -330; spiritBox.OffsetRight = 330; root.AddChild(spiritBox);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; row.AddThemeConstantOverride("separation", 10); spiritBox.AddChild(row);
        row.AddChild(new Label { Text = "鷹野勢" }); spiritTakano = Bar(HouseColor("takano"), 150, new Vector2(170, 16)); row.AddChild(spiritTakano); spiritTakanoText = new Label(); row.AddChild(spiritTakanoText);
        row.AddChild(new VSeparator());
        row.AddChild(new Label { Text = "岩切勢" }); spiritRebel = Bar(HouseColor("iwakiri"), 150, new Vector2(170, 16)); row.AddChild(spiritRebel); spiritRebelText = new Label(); row.AddChild(spiritRebelText);

        commandBox = new PanelContainer { GrowVertical = Control.GrowDirection.Begin };
        commandBox.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.1f, 0.95f), BorderColor = new Color(0.75f, 0.6f, 0.3f), BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2, ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        commandBox.SetAnchorsPreset(Control.LayoutPreset.CenterBottom); commandBox.OffsetLeft = -300; commandBox.OffsetRight = 300; commandBox.OffsetTop = -260; commandBox.OffsetBottom = -16; root.AddChild(commandBox);
        var col = new VBoxContainer(); commandBox.AddChild(col);
        var face = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; face.AddThemeConstantOverride("separation", 12); col.AddChild(face);
        TextureRect Pic(bool flip) => new() { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(110, 110), TextureFilter = CanvasItem.TextureFilterEnum.Nearest, FlipH = flip };
        mePic = Pic(false); foePic = Pic(true);
        pushTexture = ImageTexture.CreateFromImage(pushImage);
        pushPic = new TextureRect { Texture = pushTexture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, CustomMinimumSize = new Vector2(240, 22), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        face.AddChild(mePic); face.AddChild(pushPic); face.AddChild(foePic);
        var names = new HBoxContainer(); col.AddChild(names);
        meText = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; foeText = new Label { HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        names.AddChild(meText); names.AddChild(foeText);
        var gauges = new HBoxContainer(); gauges.AddThemeConstantOverride("separation", 8); col.AddChild(gauges);
        gauges.AddChild(new Label { Text = "気力" }); kiBar = Bar(new Color(0.9f, 0.75f, 0.3f), 100, new Vector2(170, 14)); gauges.AddChild(kiBar);
        gauges.AddChild(new Label { Text = "戦法" }); tacticBar = Bar(new Color(0.45f, 0.75f, 0.95f), 100, new Vector2(170, 14)); gauges.AddChild(tacticBar);
        var orders = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; orders.AddThemeConstantOverride("separation", 6); col.AddChild(orders);
        foreach (var s in ProtoWorld.Stances) { var st = s; Add(orders, "clash-stance-" + st, st, () => Message = World.Do("stance:" + st)); }
        orders.AddChild(new VSeparator());
        Add(orders, "clash-tactic", "戦法", () => Message = World.Do("tactic:me"));
        var kakare = Add(orders, "clash-kakare", "かかれ！", () => Message = World.Do("kakare:me"));
        kakare.AddThemeFontSizeOverride("font_size", 24);
        spiritBox.Visible = commandBox.Visible = false;
    }

    static void PaintPush(Image img, double push)
    {
        int w = img.GetWidth(), h = img.GetHeight(); int split = Math.Clamp((int)Math.Round(w / 2.0 + push / 100.0 * (w / 2.0 - 2)), 1, w - 1);
        img.Fill(new Color(1, 1, 1));
        img.FillRect(new Rect2I(1, 1, split - 1, h - 2), HouseColor("takano"));
        img.FillRect(new Rect2I(split, 1, w - split - 1, h - 2), HouseColor("iwakiri"));
        img.FillRect(new Rect2I(w / 2, 0, 1, h), new Color(1, 0.9f, 0.5f));
    }

    void UpdateBattleView()
    {
        var war = World.War; bool on = war.Phase is "合戦" or "終わり" && war.Spirit.Count > 0;
        spiritBox.Visible = on;
        if (on)
        {
            spiritTakano.Value = World.SpiritOf("takano"); spiritRebel.Value = World.SpiritOf("iwakiri");
            spiritTakanoText.Text = World.SpiritOf("takano").ToString(); spiritRebelText.Text = World.SpiritOf("iwakiri").ToString();
        }
        // 地図の押し合いのゲージ
        var live = new HashSet<string>();
        foreach (var c in war.Clashes)
        {
            var a = World.Person(c.A); var b = World.Person(c.B);
            if (!World.Sees(World.Player, a) || !World.Sees(World.Player, b)) continue;
            var key = c.A + "|" + c.B; live.Add(key);
            if (!clashBars.TryGetValue(key, out var v))
            {
                var img = Image.CreateEmpty(120, 12, false, Image.Format.Rgba8); var tex = ImageTexture.CreateFromImage(img);
                var spr = new Sprite3D { Texture = tex, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0014f, NoDepthTest = true, Shaded = false, TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, RenderPriority = 10 };
                AddChild(spr); clashBars[key] = v = (spr, tex, img);
            }
            PaintPush(v.Img, c.Push); v.Tex.Update(v.Img);
            var mid = (ToScene(a.X, a.Y) + ToScene(b.X, b.Y)) / 2; v.Bar.Position = mid + new Vector3(0, 20 + distance * 0.05f, 0); v.Bar.Visible = true;
        }
        foreach (var (key, v) in clashBars) if (!live.Contains(key)) v.Bar.Visible = false;
        // 本人の采配
        var mine = World.ClashOf(World.Player.Id);
        commandBox.Visible = mine != null;
        if (mine == null) { lastClash = ""; return; }
        var me = World.Player; var foe = World.Person(mine.A == me.Id ? mine.B : mine.A);
        var ck = mine.A + "|" + mine.B;
        if (ck != lastClash)
        {
            lastClash = ck; if (Speed > 10) { Speed = 10; RefreshUi(); }
            mePic.Texture = DotOfficerActor.Frame(SpriteKey(World, me), "attack"); foePic.Texture = DotOfficerActor.Frame(SpriteKey(World, foe), "attack");
        }
        PaintPush(pushImage, mine.Push); pushTexture.Update(pushImage); // 本人はいつも鷹野方（左）
        meText.Text = $"{me.Name}　兵{me.Soldiers}・士気{me.Morale}";
        foeText.Text = $"{foe.Name}　兵{foe.Soldiers}・士気{foe.Morale}";
        kiBar.Value = war.Ki; tacticBar.Value = World.GaugeOf(me.Id);
        foreach (var s in ProtoWorld.Stances) Buttons["clash-stance-" + s].Disabled = war.Stance == s;
        Buttons["clash-tactic"].Text = "戦法：" + ProtoWorld.TacticOf(me); Buttons["clash-tactic"].Disabled = World.GaugeOf(me.Id) < 100;
        Buttons["clash-kakare"].Disabled = !World.CanKakare;
    }
}
