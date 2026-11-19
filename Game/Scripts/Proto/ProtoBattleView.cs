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
    ProgressBar spiritTakano = null!, spiritRebel = null!, kiBar = null!;
    Label spiritTakanoText = null!, spiritRebelText = null!, sideTakano = null!, sideRebel = null!;
    TextureRect pushPic = null!;
    VBoxContainer allyList = null!, enemyList = null!;
    string rowsKey = "";
    readonly Dictionary<string, (Label Text, ProgressBar Morale, ProgressBar Tactic)> rows = new();
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
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = new Theme { DefaultFontSize = 16 } }; root.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(root);

        spiritBox = new PanelContainer(); spiritBox.SetAnchorsPreset(Control.LayoutPreset.CenterTop); spiritBox.OffsetTop = 50; spiritBox.OffsetLeft = -330; spiritBox.OffsetRight = 330; root.AddChild(spiritBox);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; row.AddThemeConstantOverride("separation", 10); spiritBox.AddChild(row);
        row.AddChild(new Label { Text = "鷹野勢" }); spiritTakano = Bar(HouseColor("takano"), 150, new Vector2(170, 16)); row.AddChild(spiritTakano); spiritTakanoText = new Label(); row.AddChild(spiritTakanoText);
        row.AddChild(new VSeparator());
        row.AddChild(new Label { Text = "岩切勢" }); spiritRebel = Bar(HouseColor("iwakiri"), 150, new Vector2(170, 16)); row.AddChild(spiritRebel); spiritRebelText = new Label(); row.AddChild(spiritRebelText);

        commandBox = new PanelContainer { GrowVertical = Control.GrowDirection.Begin };
        commandBox.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.1f, 0.95f), BorderColor = new Color(0.75f, 0.6f, 0.3f), BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2, ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        commandBox.SetAnchorsPreset(Control.LayoutPreset.BottomRight); commandBox.OffsetLeft = -560; commandBox.OffsetRight = -10; commandBox.OffsetTop = -10; commandBox.OffsetBottom = -10; root.AddChild(commandBox);
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 6); commandBox.AddChild(col);
        // 上：両方の兵の合計と押し合い。中：このぶつかり合いに加わっている隊の一覧（左が味方、右が敵）。下：本人の気力と采配。
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 8); col.AddChild(top);
        sideTakano = new Label { CustomMinimumSize = new Vector2(70, 0) }; sideRebel = new Label { CustomMinimumSize = new Vector2(70, 0), HorizontalAlignment = HorizontalAlignment.Right };
        pushTexture = ImageTexture.CreateFromImage(pushImage);
        pushPic = new TextureRect { Texture = pushTexture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, CustomMinimumSize = new Vector2(300, 16), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        top.AddChild(sideTakano); top.AddChild(pushPic); top.AddChild(sideRebel);
        var lists = new HBoxContainer(); lists.AddThemeConstantOverride("separation", 14); col.AddChild(lists);
        allyList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; enemyList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        lists.AddChild(allyList); lists.AddChild(new VSeparator()); lists.AddChild(enemyList);
        col.AddChild(new HSeparator());
        var gauges = new HBoxContainer(); gauges.AddThemeConstantOverride("separation", 8); col.AddChild(gauges);
        gauges.AddChild(new Label { Text = "気力" }); kiBar = Bar(new Color(0.9f, 0.75f, 0.3f), 100, new Vector2(150, 10)); gauges.AddChild(kiBar);
        var orders = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; orders.AddThemeConstantOverride("separation", 6); col.AddChild(orders);
        foreach (var s in ProtoWorld.Stances) { var st = s; Add(orders, "clash-stance-" + st, st, () => Message = World.Do("stance:" + st)); }
        orders.AddChild(new VSeparator());
        Add(orders, "clash-tactic", "戦法", () => Message = World.Do("tactic:me"));
        var kakare = Add(orders, "clash-kakare", "かかれ！", () => Message = World.Do("kakare:me"));
        kakare.AddThemeFontSizeOverride("font_size", 20);
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
        foreach (var (key, line) in clashLine)
        {
            var b = World.Person(key);
            if (!World.Sees(World.Player, b) || line.As.All(x => !World.Sees(World.Player, World.Person(x)))) continue;
            live.Add(key);
            if (!clashBars.TryGetValue(key, out var v))
            {
                var img = Image.CreateEmpty(120, 12, false, Image.Format.Rgba8); var tex = ImageTexture.CreateFromImage(img);
                var spr = new Sprite3D { Texture = tex, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0014f, NoDepthTest = true, Shaded = false, TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, RenderPriority = 10 };
                AddChild(spr); clashBars[key] = v = (spr, tex, img);
            }
            PaintPush(v.Img, line.Push); v.Tex.Update(v.Img);
            v.Bar.Position = line.M + new Vector3(0, 20 + distance * 0.05f, 0); v.Bar.Visible = true;
        }
        foreach (var (key, v) in clashBars) if (!live.Contains(key)) v.Bar.Visible = false;
        // 本人の采配：本人とつながっているぶつかり合い（同じ敵と戦う味方、その敵と戦う味方…）を全部まとめて見せる。
        var mine = World.ClashOf(World.Player.Id);
        commandBox.Visible = mine != null;
        if (mine == null) { lastClash = ""; return; }
        var me = World.Player;
        var group = new HashSet<string> { me.Id }; var clashes = new List<ProtoClash>();
        for (bool grew = true; grew;)
        {
            grew = false;
            foreach (var c in war.Clashes.Where(c => !clashes.Contains(c) && (group.Contains(c.A) || group.Contains(c.B))))
            { clashes.Add(c); grew |= group.Add(c.A) | group.Add(c.B); }
        }
        var allies = group.Select(World.Person).Where(u => u.House != "iwakiri").OrderByDescending(u => u.Player).ThenBy(u => u.Id).ToList();
        var enemies = group.Select(World.Person).Where(u => u.House == "iwakiri").OrderBy(u => u.Id).ToList();
        var ck = string.Join(",", allies.Select(u => u.Id)) + "|" + string.Join(",", enemies.Select(u => u.Id));
        if (ck != lastClash)
        {
            if (lastClash == "" && Speed > 10 && FilmRoot == null) { Speed = 10; RefreshUi(); } // 動画では落とさない
            lastClash = ck;
        }
        if (ck != rowsKey)
        {
            rowsKey = ck; rows.Clear();
            foreach (var list in new[] { allyList, enemyList }) foreach (var n in list.GetChildren()) { list.RemoveChild(n); n.QueueFree(); }
            foreach (var u in allies) AddRow(allyList, u, false);
            foreach (var u in enemies) AddRow(enemyList, u, true);
        }
        foreach (var u in allies.Concat(enemies))
        {
            var r = rows[u.Id]; bool down = war.Routed.Contains(u.Id);
            r.Text.Text = $"{u.Name}　{u.Soldiers}" + (down ? "　崩れ" : ""); r.Morale.Value = u.Morale; r.Tactic.Value = World.GaugeOf(u.Id);
        }
        sideTakano.Text = allies.Sum(u => u.Soldiers).ToString(); sideRebel.Text = enemies.Sum(u => u.Soldiers).ToString();
        PaintPush(pushImage, clashes.Count == 0 ? 0 : clashes.Average(c => c.Push)); pushTexture.Update(pushImage);
        kiBar.Value = war.Ki;
        foreach (var s in ProtoWorld.Stances) Buttons["clash-stance-" + s].Disabled = war.Stance == s;
        Buttons["clash-tactic"].Text = "戦法：" + ProtoWorld.TacticOf(me); Buttons["clash-tactic"].Disabled = World.GaugeOf(me.Id) < 100;
        Buttons["clash-kakare"].Disabled = !World.CanKakare;
    }

    // 一覧の一行：小さなちびコマ・名前と兵・士気（金）と戦法のゲージ（青）と戦法の名前。本人の行は金の文字。
    void AddRow(VBoxContainer list, ProtoPerson u, bool enemy)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); list.AddChild(row);
        row.AddChild(new TextureRect { Texture = DotOfficerActor.Frame(SpriteKey(World, u), "attack"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(36, 36), TextureFilter = CanvasItem.TextureFilterEnum.Nearest, FlipH = enemy });
        var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; info.AddThemeConstantOverride("separation", 2); row.AddChild(info);
        var text = new Label { Modulate = u.Player ? new Color(1, 0.88f, 0.45f) : Colors.White }; text.AddThemeFontSizeOverride("font_size", 15); info.AddChild(text);
        var bars = new HBoxContainer(); bars.AddThemeConstantOverride("separation", 4); info.AddChild(bars);
        var morale = Bar(new Color(0.95f, 0.8f, 0.3f), 100, new Vector2(70, 6)); var tactic = Bar(new Color(0.45f, 0.75f, 0.95f), 100, new Vector2(70, 6));
        bars.AddChild(morale); bars.AddChild(tactic);
        var tn = new Label { Text = ProtoWorld.TacticOf(u), Modulate = new Color(0.6f, 0.8f, 1f) }; tn.AddThemeFontSizeOverride("font_size", 13); bars.AddChild(tn);
        rows[u.Id] = (text, morale, tactic);
    }

    // ---- 隊の札（引いた視点） ----
    // 凸の形の札（2026-11-19 ユーザーの図。名前の長さで形が変わらないよう、どの札も同じ大きさ）。
    // 札は勢力の色。上の出っ張りに家の名、下の広い所に名前（軍団なら〇〇勢）・兵の合計・内訳、いちばん下に士気の細い棒。
    // 名前が長いときは文字を小さくして収める。本人の札は金の縁。崩れた隊は灰色。
    const float CardDistance = 1000;
    const int CardW = 132, TabW = 58, TabH = 24, BodyH = 78;
    sealed class Card { public Sprite3D Back = null!; public Label3D House = null!, Name = null!, Total = null!, Parts = null!; public string Key = ""; }
    readonly Dictionary<string, Card> cards = new();
    static float Chars(string t) => t.Sum(ch => ch > 0x2000 ? 1f : 0.58f);
    Label3D CardText(int size) => new() { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0008f, FontSize = size, OutlineSize = 3, OutlineModulate = new Color(0, 0, 0, 0.9f), NoDepthTest = true, RenderPriority = 21 };
    static string HouseShort(string house) => house switch { "takano" => "鷹野", "iwakiri" => "岩切", "sawatari" => "沢渡", _ => "" };
    // 出っ張りは隊の向き（画面の上下左右のうち近いほう）を指す。向きが分からないときは上。
    int CardDir(ProtoPerson p, Vector3 at)
    {
        if (!World.War.Facing.TryGetValue(p.Id, out var f) || Camera == null) return 0;
        var a = Camera.UnprojectPosition(at); var b = Camera.UnprojectPosition(at + new Vector3((float)f[0], 0, (float)f[1]) * 20);
        var d = b - a; if (d.LengthSquared() < 1) return 0;
        return Math.Abs(d.X) > Math.Abs(d.Y) ? (d.X > 0 ? 1 : 3) : (d.Y < 0 ? 0 : 2); // 0上 1右 2下 3左
    }
    void DrawCard(ProtoPerson p, Vector3 foot, bool show, string name, int total, string parts)
    {
        if (!cards.TryGetValue(p.Id, out var c))
        {
            c = new Card { Back = new Sprite3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0008f, NoDepthTest = true, Shaded = false, TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, RenderPriority = 20 }, House = CardText(16), Name = CardText(17), Total = CardText(20), Parts = CardText(15) };
            foreach (var n in new Node3D[] { c.Back, c.House, c.Name, c.Total, c.Parts }) AddChild(n);
            cards[p.Id] = c;
        }
        foreach (var n in new GeometryInstance3D[] { c.Back, c.House, c.Name, c.Total, c.Parts }) n.Visible = show;
        if (!show) return;
        var at = foot + new Vector3(0, 8 + distance * 0.025f, 0);
        foreach (var n in new Node3D[] { c.Back, c.House, c.Name, c.Total, c.Parts }) n.Position = at;
        bool routed = World.War.Routed.Contains(p.Id); int dir = routed ? 0 : CardDir(p, at);
        var key = $"{name}|{total}|{parts}|{p.Morale / 5}|{routed}|{dir}";
        if (key == c.Key) return;
        c.Key = key; c.Name.Text = name; c.Total.Text = total.ToString(); c.Parts.Text = parts == "" ? "" : $"({parts})";
        c.House.Text = dir is 0 or 2 ? HouseShort(p.House) : HouseShort(p.House).Length > 0 ? HouseShort(p.House)[..1] : "";
        c.Name.FontSize = Math.Min(17, (int)((CardW - 16) / Math.Max(1, Chars(name))));
        c.Parts.FontSize = Math.Min(15, (int)((CardW - 16) / Math.Max(1, Chars(c.Parts.Text))));
        // 形：上下なら幅 CardW・高さ TabH+BodyH、左右なら幅 CardW+TabH・高さ BodyH。出っ張りは辺の真ん中。
        bool vertical = dir is 0 or 2; int w = vertical ? CardW : CardW + TabH, h = vertical ? TabH + BodyH : BodyH;
        int bx = dir == 3 ? TabH : 0, by = dir == 0 ? TabH : 0;
        var tab = dir switch { 0 => new Rect2I((w - TabW) / 2, 0, TabW, TabH + 2), 2 => new Rect2I((w - TabW) / 2, BodyH - 2, TabW, TabH + 2), 1 => new Rect2I(CardW - 2, (h - TabW) / 2, TabH + 2, TabW), _ => new Rect2I(0, (h - TabW) / 2, TabH + 2, TabW) };
        var edge = p.Player ? new Color(1, 0.85f, 0.35f) : routed ? new Color(0.6f, 0.6f, 0.6f) : HouseColor(p.House).Lightened(0.35f);
        var fill = routed ? new Color(0.32f, 0.32f, 0.32f, 0.95f) : HouseColor(p.House).Darkened(0.1f); // 勢力の色（文字は輪郭線で読む）
        var tabFill = routed ? fill : HouseColor(p.House).Darkened(0.3f);
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8); img.Fill(new Color(0, 0, 0, 0));
        img.FillRect(tab, edge); img.FillRect(new Rect2I(bx, by, CardW, BodyH), edge);
        img.FillRect(tab.Grow(-2), tabFill);
        img.FillRect(new Rect2I(bx + 2, by + 2, CardW - 4, BodyH - 4), fill);
        // 出っ張りと本体のつなぎ目の縁を消す
        var seam = dir switch { 0 => new Rect2I(tab.Position.X + 2, by, TabW - 4, 2), 2 => new Rect2I(tab.Position.X + 2, BodyH - 2, TabW - 4, 2), 1 => new Rect2I(CardW - 2, tab.Position.Y + 2, 2, TabW - 4), _ => new Rect2I(TabH, tab.Position.Y + 2, 2, TabW - 4) };
        img.FillRect(seam, tabFill);
        img.FillRect(new Rect2I(bx + 6, by + BodyH - 10, CardW - 12, 5), new Color(0.12f, 0.12f, 0.12f));
        img.FillRect(new Rect2I(bx + 6, by + BodyH - 10, Math.Max(1, (CardW - 12) * Math.Clamp(p.Morale, 0, 100) / 100), 5), new Color(0.95f, 0.8f, 0.3f));
        c.Back.Texture = ImageTexture.CreateFromImage(img);
        // 文字の位置（札の真ん中からのずれ、上が正）
        Vector2 Off(float x, float y) => new(x - w / 2f, h / 2f - y);
        float cx = bx + CardW / 2f;
        c.House.Offset = Off(tab.Position.X + tab.Size.X / 2f, tab.Position.Y + tab.Size.Y / 2f + 1);
        c.Name.Offset = Off(cx, by + 15);
        c.Total.Offset = Off(cx, by + (parts == "" ? 42 : 37));
        c.Parts.Offset = Off(cx, by + 57);
    }
}
