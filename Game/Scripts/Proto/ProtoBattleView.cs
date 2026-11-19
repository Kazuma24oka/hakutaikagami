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
    // 布陣図の凸（2026-11-19 ユーザー「向きそのものを変えてほしい」）：凸の形を地面に置き、出っ張りが隊の向く方を指すように回す（どの角度にも）。
    // 色は勢力の色、縁は明るい勢力の色（本人は金、崩れた隊は灰色）。名前・兵の合計・内訳は凸の上に立てて読めるように出し、下に士気の細い棒。
    // 大きさはカメラの遠さに合わせて、画面の上でほぼ同じ大きさに見える。
    const float CardDistance = 1000;
    sealed class Card { public MeshInstance3D Fill = null!, Edge = null!; public Label3D Name = null!, Total = null!, Parts = null!; public Sprite3D Morale = null!; public string Key = ""; }
    readonly Dictionary<string, Card> cards = new();
    static float Chars(string t) => t.Sum(ch => ch > 0x2000 ? 1f : 0.58f);
    const int CardFont = 48; // 文字は大きく作って縮める（凸と一緒に大きさが変わる）
    Label3D CardText() => new() { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = CardFont, OutlineSize = 8, OutlineModulate = new Color(0, 0, 0, 0.9f), NoDepthTest = true, RenderPriority = 22 };
    // 凸の形（向きは -Z が前＝出っ張り）。本体 幅1・奥行き0.6、出っ張り 幅0.42・奥行き0.28。中心は本体の真ん中。
    static ArrayMesh Totsu(float grow)
    {
        float w = 0.5f + grow, d = 0.3f + grow, tw = 0.21f + grow, td = 0.28f;
        var v = new Vector3[] { new(-w, 0, d), new(w, 0, d), new(w, 0, -d), new(-w, 0, -d), new(-tw, 0, -d + 0.001f), new(tw, 0, -d + 0.001f), new(tw, 0, -d - td - grow), new(-tw, 0, -d - td - grow) };
        var idx = new[] { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 };
        var arr = new Godot.Collections.Array(); arr.Resize((int)Mesh.ArrayType.Max); arr[(int)Mesh.ArrayType.Vertex] = v; arr[(int)Mesh.ArrayType.Index] = idx;
        var m = new ArrayMesh(); m.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arr); return m;
    }
    static readonly ArrayMesh TotsuFill = Totsu(0), TotsuEdge = Totsu(0.035f);
    static StandardMaterial3D Flat(int priority) => new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled, RenderPriority = priority, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
    readonly Dictionary<string, float> cardYaw = new();
    void DrawCard(ProtoPerson p, Vector3 foot, bool show, string name, int total, string parts)
    {
        if (!cards.TryGetValue(p.Id, out var c))
        {
            c = new Card
            {
                Edge = new MeshInstance3D { Mesh = TotsuEdge, MaterialOverride = Flat(18) }, Fill = new MeshInstance3D { Mesh = TotsuFill, MaterialOverride = Flat(19) },
                Name = CardText(), Total = CardText(), Parts = CardText(),
                Morale = new Sprite3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Shaded = false, TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, RenderPriority = 21 },
            };
            foreach (var n in new Node3D[] { c.Edge, c.Fill, c.Name, c.Total, c.Parts, c.Morale }) AddChild(n);
            cards[p.Id] = c;
        }
        foreach (var n in new GeometryInstance3D[] { c.Edge, c.Fill, c.Name, c.Total, c.Morale }) n.Visible = show;
        c.Parts.Visible = show && parts != "";
        if (!show) return;
        bool routed = World.War.Routed.Contains(p.Id);
        // 地面の凸：遠さに合わせた大きさで、向きの角度へなめらかに回す（1秒に120度まで）。
        float size = distance * 0.105f;
        float want = World.War.Facing.TryGetValue(p.Id, out var f) && !routed ? (float)Math.Atan2(-f[0], -f[1]) : cardYaw.GetValueOrDefault(p.Id, 0); // -Z が前
        float yaw = cardYaw.TryGetValue(p.Id, out var was) ? was : want;
        float diff = Mathf.Wrap(want - yaw, -Mathf.Pi, Mathf.Pi), step = Mathf.DegToRad(120) * (float)GetProcessDeltaTime();
        yaw += Math.Clamp(diff, -step, step); cardYaw[p.Id] = yaw;
        var ground = foot + new Vector3(0, 2, 0);
        c.Fill.Transform = c.Edge.Transform = new Transform3D(new Basis(Vector3.Up, yaw).Scaled(new Vector3(size, 1, size)), ground);
        var key = $"{name}|{total}|{parts}|{p.Morale / 5}|{routed}";
        if (key != c.Key)
        {
            c.Key = key; c.Name.Text = name; c.Total.Text = total.ToString(); c.Parts.Text = parts == "" ? "" : $"({parts})";
            ((StandardMaterial3D)c.Fill.MaterialOverride).AlbedoColor = routed ? new Color(0.32f, 0.32f, 0.32f, 0.92f) : HouseColor(p.House).Darkened(0.1f) with { A = 0.92f };
            ((StandardMaterial3D)c.Edge.MaterialOverride).AlbedoColor = p.Player ? new Color(1, 0.85f, 0.35f) : routed ? new Color(0.6f, 0.6f, 0.6f) : HouseColor(p.House).Lightened(0.4f);
            var img = Image.CreateEmpty(100, 8, false, Image.Format.Rgba8); img.Fill(new Color(0.08f, 0.08f, 0.08f, 0.9f));
            img.FillRect(new Rect2I(1, 1, Math.Max(1, 98 * Math.Clamp(p.Morale, 0, 100) / 100), 6), new Color(0.95f, 0.8f, 0.3f));
            c.Morale.Texture = ImageTexture.CreateFromImage(img);
        }
        // 見えない〇枠：本体の中の円（半径は本体の奥行きの半分の9割）。円は回しても同じなので、文字と棒はいつも凸の中に収まる。
        // 地面の円は斜めから見ると縦が縮むので、縦は傾きの分だけ詰める。行ごとの高さと、横幅に収まる大きさの小さいほうで文字の大きさを決める。
        float r = 0.27f * size, squash = Mathf.Sin(Mathf.DegToRad(pitch)), boxW = 1.7f * r, boxH = 1.7f * r * squash;
        bool hasParts = parts != ""; int rows = hasParts ? 4 : 3; float rowH = boxH / rows;
        float Fit(Label3D l, float scale) => Math.Min(rowH * scale, boxW / Math.Max(1, Chars(l.Text))) / CardFont;
        c.Name.PixelSize = Fit(c.Name, 0.85f); c.Total.PixelSize = Fit(c.Total, 1f); c.Parts.PixelSize = Fit(c.Parts, 0.75f);
        c.Morale.PixelSize = boxW * 0.8f / 100;
        var up = Camera?.GlobalBasis.Y ?? Vector3.Up; var center = ground + new Vector3(0, 1, 0);
        float top = boxH / 2 - rowH / 2;
        var order = hasParts ? new Node3D[] { c.Name, c.Total, c.Parts, c.Morale } : new Node3D[] { c.Name, c.Total, c.Morale };
        for (int k = 0; k < order.Length; k++) order[k].Position = center + up * (top - rowH * k);
    }
}
