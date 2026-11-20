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
    Label troopsTakano = null!, troopsRebel = null!;
    string rowsKey = "";
    readonly Dictionary<string, (Label Text, ProgressBar Morale, ProgressBar Tactic, ProgressBar Chaos)> rows = new();
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

        spiritBox = new PanelContainer(); spiritBox.SetAnchorsPreset(Control.LayoutPreset.CenterTop); spiritBox.OffsetTop = 50; spiritBox.OffsetLeft = -380; spiritBox.OffsetRight = 380; root.AddChild(spiritBox);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; row.AddThemeConstantOverride("separation", 10); spiritBox.AddChild(row);
        row.AddChild(new Label { Text = "全軍士気", Modulate = new Color(1, 0.9f, 0.6f) }); row.AddChild(new VSeparator());
        row.AddChild(new Label { Text = "鷹野勢" }); spiritTakano = Bar(HouseColor("takano"), 150, new Vector2(170, 16)); row.AddChild(spiritTakano); spiritTakanoText = new Label(); row.AddChild(spiritTakanoText);
        row.AddChild(new VSeparator());
        row.AddChild(new Label { Text = "岩切勢" }); spiritRebel = Bar(HouseColor("iwakiri"), 150, new Vector2(170, 16)); row.AddChild(spiritRebel); spiritRebelText = new Label(); row.AddChild(spiritRebelText);

        commandBox = new PanelContainer { GrowVertical = Control.GrowDirection.Begin };
        commandBox.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.1f, 0.95f), BorderColor = new Color(0.75f, 0.6f, 0.3f), BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2, ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        commandBox.SetAnchorsPreset(Control.LayoutPreset.BottomRight); commandBox.GrowHorizontal = Control.GrowDirection.Begin; commandBox.OffsetLeft = -600; commandBox.OffsetRight = -10; commandBox.OffsetTop = -10; commandBox.OffsetBottom = -10; root.AddChild(commandBox);
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 6); commandBox.AddChild(col);
        // 上：両方の兵の合計と押し合い。中：このぶつかり合いに加わっている隊の一覧（左が味方、右が敵）。下：本人の気力と采配。
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 8); col.AddChild(top);
        sideTakano = new Label { CustomMinimumSize = new Vector2(40, 0), HorizontalAlignment = HorizontalAlignment.Right }; sideRebel = new Label { CustomMinimumSize = new Vector2(40, 0) };
        pushTexture = ImageTexture.CreateFromImage(pushImage);
        pushPic = new TextureRect { Texture = pushTexture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, CustomMinimumSize = new Vector2(220, 16), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        // 並び：兵の合計｜士気合計 数字 [棒] 数字｜兵の合計
        troopsTakano = new Label(); troopsRebel = new Label();
        top.AddChild(troopsTakano); top.AddChild(new VSeparator()); top.AddChild(new Label { Text = "総士気", Modulate = new Color(1, 0.9f, 0.6f) }); top.AddChild(sideTakano); top.AddChild(pushPic); top.AddChild(sideRebel); top.AddChild(new VSeparator()); top.AddChild(troopsRebel);
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
        var kakare = Add(orders, "clash-kakare", "強攻", () => Message = World.Do("kakare:me"));
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

    // ---- 打撃の数字（2026-11-19 ユーザー「側面を突かれているのか正面なのか分からない。ダメージも出ていない」） ----
    // 1分ごとに、打撃を受けた隊の上に数字を出して浮かべて消す。正面は白、側面は橙、背後は赤、奇襲を受けている間は「奇襲」を前に付ける。
    readonly List<(Label3D Label, double Born, Vector3 At)> popups = new();
    long hitsShown = -1;
    void ShowHits()
    {
        var hits = World.War.Hits;
        if (hits.Count > 0 && hits[0].At != hitsShown)
        {
            hitsShown = hits[0].At;
            foreach (var h in hits.Where(h => h.Loss > 0))
            {
                var u = World.Person(h.To); if (!World.Sees(World.Player, u)) continue;
                string head = (h.Surprise ? "奇襲 " : "") + (h.Kind == "正面" ? "" : h.Kind + " ");
                var color = h.Kind == "背後" ? new Color(1, 0.35f, 0.3f) : h.Kind is "側面" or "追い討ち" ? new Color(1, 0.65f, 0.25f) : new Color(1, 1, 1);
                var l = new Label3D { Text = $"{head}-{h.Loss}", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0009f, FontSize = 30, OutlineSize = 8, OutlineModulate = new Color(0, 0, 0, 0.9f), Modulate = color, NoDepthTest = true, RenderPriority = 30 };
                AddChild(l); popups.Add((l, Time.GetTicksMsec() / 1000.0, ToScene(u.X, u.Y)));
            }
        }
        double now = Time.GetTicksMsec() / 1000.0;
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var (l, born, at) = popups[i]; double age = now - born;
            if (age > 1.6) { l.QueueFree(); popups.RemoveAt(i); continue; }
            l.Position = at + new Vector3(0, distance * (0.05f + 0.03f * (float)age), 0);
            l.Modulate = l.Modulate with { A = (float)Math.Clamp(1.6 - age, 0, 1) };
        }
    }

    // ---- 剣の印：ぶつかっている二つの隊の間（触れている所）に、交差した二本の剣 ----
    static ImageTexture? swordTex;
    static ImageTexture SwordTexture()
    {
        if (swordTex != null) return swordTex;
        var img = Image.CreateEmpty(48, 48, false, Image.Format.Rgba8); img.Fill(new Color(0, 0, 0, 0));
        void Dot(int x, int y, int r, Color c) { for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++) if (x + dx is >= 0 and < 48 && y + dy is >= 0 and < 48) img.SetPixel(x + dx, y + dy, c); }
        var edge = new Color(0.1f, 0.1f, 0.1f); var blade = new Color(0.85f, 0.85f, 0.88f); var hilt = new Color(0.55f, 0.35f, 0.15f);
        foreach (var flip in new[] { false, true })
        {
            for (int i = 6; i <= 40; i++) { int x = flip ? 47 - i : i, y = 47 - i; Dot(x, y, 3, edge); }
            for (int i = 6; i <= 40; i++) { int x = flip ? 47 - i : i, y = 47 - i; Dot(x, y, 1, i > 14 ? blade : hilt); }
            for (int k = -5; k <= 5; k++) { int cx = flip ? 47 - 14 : 14, cy = 47 - 14; Dot(cx + (flip ? -k : k), cy + k, 1, hilt); }
        }
        return swordTex = ImageTexture.CreateFromImage(img);
    }
    readonly Dictionary<string, Sprite3D> swords = new();
    void DrawSwords()
    {
        var live = new HashSet<string>();
        foreach (var c in World.War.Clashes)
        {
            var a = World.Person(c.A); var b = World.Person(c.B);
            if (!World.Sees(World.Player, a) || !World.Sees(World.Player, b)) continue;
            var key = c.A + "|" + c.B; live.Add(key);
            if (!swords.TryGetValue(key, out var s)) { s = new Sprite3D { Texture = SwordTexture(), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0012f, NoDepthTest = true, Shaded = false, TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, RenderPriority = 25 }; AddChild(s); swords[key] = s; }
            s.Position = (ToScene(a.X, a.Y) + ToScene(b.X, b.Y)) / 2 + new Vector3(0, 4, 0); s.Visible = true;
        }
        foreach (var (k, s) in swords) if (!live.Contains(k)) s.Visible = false;
    }

    void UpdateBattleView()
    {
        ShowHits();
        var war = World.War; bool on = war.Phase is "合戦" or "終わり" && war.Spirit.Count > 0;
        spiritBox.Visible = on;
        if (on)
        {
            spiritTakano.Value = World.SpiritOf("takano"); spiritRebel.Value = World.SpiritOf("iwakiri");
            spiritTakanoText.Text = World.SpiritOf("takano").ToString(); spiritRebelText.Text = World.SpiritOf("iwakiri").ToString();
        }
        // ぶつかっている所に剣の印（2026-11-19 ユーザーの図）。押し合いのゲージは地図には出さない（右下の窓にある）。
        DrawSwords();
        var live = new HashSet<string>();
        foreach (var (key, line) in clashLine.Where(_ => false))
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
        // 合戦の窓が出ている間は、記録の窓をその左までに縮める（重ならないように）。
        logPanel.OffsetRight = commandBox.Visible ? Math.Clamp(GetViewport().GetVisibleRect().Size.X - commandBox.Size.X - 24, 300, 700) : 700;
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
            int start = war.StartSoldiers.TryGetValue(u.Id, out var st) ? st : u.Soldiers;
            r.Text.Text = $"{u.Name}　{u.Troop}{u.Soldiers}/{start}　士気{u.Morale}" + (down ? "　崩れ" : World.Confused(u.Id) ? "　混乱" : ""); r.Morale.Value = u.Morale; r.Tactic.Value = World.GaugeOf(u.Id); r.Chaos.Value = World.Confused(u.Id) ? 100 : World.ChaosOf(u.Id);
        }
        // 棒は士気の合計の比（2026-11-19 ユーザー「士気の合計を、数値も書いて」）。士気が20を切った隊は崩れるので、どちらが先に崩れそうかが分かる。
        int ma = allies.Where(u => !war.Routed.Contains(u.Id)).Sum(u => u.Morale), me2 = enemies.Where(u => !war.Routed.Contains(u.Id)).Sum(u => u.Morale);
        sideTakano.Text = ma.ToString(); sideRebel.Text = me2.ToString(); troopsTakano.Text = "兵" + allies.Sum(u => u.Soldiers); troopsRebel.Text = "兵" + enemies.Sum(u => u.Soldiers);
        PaintPush(pushImage, ma + me2 == 0 ? 0 : (ma - me2) * 100.0 / (ma + me2)); pushTexture.Update(pushImage);
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
        var morale = Bar(new Color(0.95f, 0.8f, 0.3f), 100, new Vector2(50, 6)); var tactic = Bar(new Color(0.45f, 0.75f, 0.95f), 100, new Vector2(50, 6));
        Label Tag(string t) { var l = new Label { Text = t, Modulate = new Color(0.75f, 0.75f, 0.75f) }; l.AddThemeFontSizeOverride("font_size", 12); return l; }
        var chaos = Bar(new Color(0.8f, 0.45f, 0.9f), 100, new Vector2(50, 6));
        bars.AddChild(Tag("士気")); bars.AddChild(morale); bars.AddChild(Tag("混乱")); bars.AddChild(chaos); bars.AddChild(Tag("戦法")); bars.AddChild(tactic);
        var tn = new Label { Text = ProtoWorld.TacticOf(u), Modulate = new Color(0.6f, 0.8f, 1f) }; tn.AddThemeFontSizeOverride("font_size", 13); bars.AddChild(tn);
        rows[u.Id] = (text, morale, tactic, chaos);
    }

    // ---- 混乱の印：混乱している隊の上で、渦が回り続ける（2026-11-20 ユーザー「クルクル回っているようなアイコン」） ----
    readonly Dictionary<string, MeshInstance3D> swirls = new(); ImageTexture? swirlTexture;
    void DrawSwirl(ProtoPerson p, Vector3 foot, bool show, bool card)
    {
        if (!swirls.TryGetValue(p.Id, out var m))
        {
            if (!show) return;
            if (swirlTexture == null)
            {
                var img = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8); img.Fill(new Color(0, 0, 0, 0));
                void Stroke(int r, Color c) { for (double t = 0.6; t < 5.2 * Math.PI; t += 0.02) { double rad = 1.75 * t; int cx = 32 + (int)Math.Round(Math.Cos(t) * rad), cy = 32 + (int)Math.Round(Math.Sin(t) * rad); for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++) if (dx * dx + dy * dy <= r * r && cx + dx >= 0 && cy + dy >= 0 && cx + dx < 64 && cy + dy < 64) img.SetPixel(cx + dx, cy + dy, c); } }
                Stroke(4, new Color(0.1f, 0.05f, 0.15f)); Stroke(2, new Color(1, 0.92f, 0.3f));
                swirlTexture = ImageTexture.CreateFromImage(img);
            }
            var mat = Flat(26); mat.AlbedoTexture = swirlTexture; mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
            m = new MeshInstance3D { Mesh = new PlaneMesh { Size = Vector2.One }, MaterialOverride = mat }; AddChild(m); swirls[p.Id] = m;
        }
        m.Visible = show; if (!show) return;
        float size = card ? 64 : Math.Max(16, distance * 0.05f);
        m.Position = foot + new Vector3(0, card ? 9 : 34, 0); m.Scale = new Vector3(size, 1, size); m.Rotation = new Vector3(0, -(float)(Time.GetTicksMsec() % 100000) / 180f, 0);
    }

    // ---- 隊の札（引いた視点） ----
    // 布陣図の凸（2026-11-19 ユーザー「向きそのものを変えてほしい」）：凸の形を地面に置き、出っ張りが隊の向く方を指すように回す（どの角度にも）。
    // 色は勢力の色、縁は明るい勢力の色（本人は金、崩れた隊は灰色）。名前・兵の合計・内訳は凸の上に立てて読めるように出し、下に士気の細い棒。
    // 大きさはカメラの遠さに合わせて、画面の上でほぼ同じ大きさに見える。
    const float CardDistance = 600; // 凸は世界の大きさなので、引きすぎると小さくなる。これより遠いと凸、近いとちびコマ
    sealed class Card { public MeshInstance3D Fill = null!, Edge = null!; public Label3D Name = null!, Total = null!, Parts = null!; public Sprite3D Morale = null!; public string Key = ""; }
    readonly Dictionary<string, Card> cards = new();
    static float Chars(string t) => t.Sum(ch => ch > 0x2000 ? 1f : 0.58f);
    const int CardFont = 48; // 文字は大きく作って縮める（凸と一緒に大きさが変わる）
    Label3D CardText() => new() { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = CardFont, OutlineSize = 8, OutlineModulate = new Color(0, 0, 0, 0.9f), NoDepthTest = true, RenderPriority = 22 };
    // 凸の形（向きは -Z が前＝出っ張り）。本体 幅0.8・奥行き0.8、出っ張り 幅0.35・奥行き0.3。中心は本体の真ん中。
    // 大きさは世界の長さで決まっている（CardSize m）。攻めかかる側は相手の80m手前で止まるので、出っ張りが相手の本体にちょうど触れる。
    const float CardSize = 73f;
    static ArrayMesh Totsu(float grow)
    {
        float w = 0.4f + grow, d = 0.4f + grow, tw = 0.175f + grow, td = 0.3f; // 本体は真四角（横から当たられても、出っ張りがちょうど面に触れる）
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
        float size = CardSize;
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
        float r = 0.36f * size, squash = Mathf.Sin(Mathf.DegToRad(pitch)), boxW = 1.7f * r, boxH = 1.7f * r * squash;
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
