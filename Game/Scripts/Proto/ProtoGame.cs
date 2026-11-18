using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using HakutaiKagami.Core.Proto;

// 試作版の遊ぶ画面（2026-11-18）。地面を押すとそこへ歩く。任務は「行き先と着く時刻」のボタンで自動で向かう。
// 時間は本人が歩いている間と「待つ」の間だけ進む。1 単位 = 1m。X が東、Z が南、Y が高さ。
public partial class ProtoGame : Node3D
{
    internal ProtoWorld World = null!;
    // 保存は自動：ゲーム内の10分ごと、着いた時、閉じる時。前の保存を一つ残す。
    // 確かめる側（Claude）は HAKUTAI_PROTO_SAVE で別の保存を使い、遊ぶ人の保存に触らない。
    internal static string SavePath => System.Environment.GetEnvironmentVariable("HAKUTAI_PROTO_SAVE") is { Length: > 0 } custom ? custom : "user://proto-save.json";
    internal static bool Fresh;
    internal static bool HasSave => FileAccess.FileExists(SavePath);
    internal void SaveGame()
    {
        var path = ProjectSettings.GlobalizePath(SavePath); var temp = path + ".tmp";
        System.IO.File.WriteAllBytes(temp, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(World.Save(), ProtoMap.Json));
        if (System.IO.File.Exists(path)) System.IO.File.Copy(path, path + ".1", true);
        System.IO.File.Move(temp, path, true); savedBucket = World.Second / 600;
    }
    public override void _Notification(int what) { if (what == NotificationWMCloseRequest && World != null) SaveGame(); }
    public override void _ExitTree() { if (World != null) SaveGame(); }
    internal Camera3D Camera = null!;
    internal int Speed = 30;
    internal long WaitUntil;
    internal readonly Dictionary<string, Button> Buttons = new();
    internal string Message = "";
    double acc, uiTimer;
    bool dirty; long savedBucket = -1;
    float distance = 500, yaw = 0, pitch = 55;
    Vector3 pan;
    readonly Dictionary<string, (MeshInstance3D Body, Label3D Label)> actors = new();
    MeshInstance3D pathLine = null!, marker = null!;
    ImmediateMesh pathMesh = null!;
    Label clock = null!, foot = null!, journal = null!, message = null!;
    VBoxContainer panel = null!;
    Label status = null!;
    readonly Dictionary<string, Button> taskButtons = new();
    internal ProtoDebug Debug = null!;

    static readonly Color Takano = new(0.55f, 0.45f, 0.85f), Sawatari = new(0.88f, 0.42f, 0.28f), Neutral = new(0.85f, 0.85f, 0.8f);
    static Color HouseColor(string house) => house == "takano" ? Takano : house == "sawatari" ? Sawatari : house == "iwakiri" ? new Color(0.62f, 0.12f, 0.12f) : Neutral;

    public override void _Ready()
    {
        GetTree().AutoAcceptQuit = true; // スタート画面は閉じる前に保存を待つため×を止めている。試作版では×でそのまま閉じる。
        var map = ProtoMap.Load(); World = null!;
        if (!Fresh && FileAccess.FileExists(SavePath))
            try { World = ProtoWorld.Load(map, System.Text.Json.JsonSerializer.Deserialize<ProtoWorld.Saved>(System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(SavePath)), ProtoMap.Json)!); }
            catch (Exception) { var old = ProjectSettings.GlobalizePath(SavePath); System.IO.File.Copy(old, old + ".old-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); } // 版の違う保存は残して、はじめから
        World ??= new ProtoWorld(map); Fresh = false; SaveGame();
        var env = new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.62f, 0.75f, 0.86f), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.75f, 0.75f, 0.75f), AmbientLightEnergy = 0.6f } };
        AddChild(env);
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -30, 0), LightEnergy = 1.1f });
        Camera = new Camera3D { Fov = 45, Far = 20000, Near = 1 }; AddChild(Camera); Camera.MakeCurrent();
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(40000, 40000) }, Position = new Vector3(2000, -3, 1500), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.5f, 0.36f), Roughness = 1 } });
        BuildGround(); BuildTrees(); BuildSites(); BuildActors();
        pathMesh = new ImmediateMesh(); pathLine = new MeshInstance3D { Mesh = pathMesh, MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1, 0.85f, 0.2f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = true } }; AddChild(pathLine);
        marker = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 10, BottomRadius = 10, Height = 1 }, MaterialOverride = Unshaded(new Color(1, 0.9f, 0.2f, 0.8f)), Visible = false }; AddChild(marker);
        BuildHud();
        Debug = new ProtoDebug(this); AddChild(Debug);
        RefreshUi(); PlaceCamera();
    }

    static StandardMaterial3D Unshaded(Color c) => new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = c, Transparency = c.A < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled, NoDepthTest = false };

    internal float HeightAt(float x, float z) => World.Map.HeightAtCm((long)(x * 100), (long)(z * 100)) / 100f;
    Vector3 ToScene(long xCm, long yCm) => new(xCm / 100f, World.Map.HeightAtCm(xCm, yCm) / 100f, yCm / 100f);

    static Color GroundColor(Ground g, float h, int c, int r)
    {
        float n = ((c * 73856093) ^ (r * 19349663)) % 4 / 100f;
        Color k = g switch
        {
            Ground.Road => new Color(0.80f, 0.70f, 0.52f),
            Ground.Site => new Color(0.84f, 0.80f, 0.68f),
            Ground.Forest => new Color(0.24f, 0.44f, 0.24f),
            Ground.Mountain => new Color(0.48f, 0.50f, 0.36f).Lerp(new Color(0.66f, 0.62f, 0.55f), Math.Clamp(h / 140f, 0, 1)),
            Ground.Water => new Color(0.26f, 0.48f, 0.78f),
            Ground.Ford => new Color(0.46f, 0.64f, 0.82f),
            _ => new Color(0.56f, 0.69f, 0.38f),
        };
        return new Color(k.R + n, k.G + n, k.B + n);
    }

    void BuildGround()
    {
        var m = World.Map; float cell = m.Data.CellM;
        float Corner(int c, int r)
        {
            float sum = 0; int count = 0;
            for (int dc = -1; dc <= 0; dc++) for (int dr = -1; dr <= 0; dr++) if (m.Inside(c + dc, r + dr)) { sum += m.HeightAt(c + dc, r + dr); count++; }
            return sum / count / 100f;
        }
        var corners = new float[(m.Columns + 1) * (m.Rows + 1)];
        for (int r = 0; r <= m.Rows; r++) for (int c = 0; c <= m.Columns; c++) corners[r * (m.Columns + 1) + c] = Corner(c, r);
        var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles);
        for (int r = 0; r < m.Rows; r++)
            for (int c = 0; c < m.Columns; c++)
            {
                var g = m.GroundAt(c, r); float h = m.HeightAt(c, r) / 100f; var color = GroundColor(g, h, c, r);
                Vector3 V(int cc, int rr) => new(cc * cell, corners[rr * (m.Columns + 1) + cc] - (g is Ground.Water ? 1.5f : 0), rr * cell);
                var a = V(c, r); var b = V(c + 1, r); var d = V(c, r + 1); var e = V(c + 1, r + 1);
                foreach (var v in new[] { a, b, e, a, e, d }) { st.SetColor(color); st.AddVertex(v); }
            }
        st.GenerateNormals();
        var mesh = st.Commit();
        AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1 } });
    }

    void BuildTrees()
    {
        var m = World.Map; var spots = new List<Vector3>();
        for (int r = 0; r < m.Rows; r++) for (int c = 0; c < m.Columns; c++)
            {
                if (m.GroundAt(c, r) != Ground.Forest) continue;
                for (int k = 0; k < 2; k++) { int h = (c * 92821 + r * 68917 + k * 31337) & 0xffff; float x = (c + (h % 100) / 100f) * m.Data.CellM, z = (r + (h / 100 % 100) / 100f) * m.Data.CellM; spots.Add(new Vector3(x, HeightAt(x, z), z)); }
            }
        var cone = new CylinderMesh { TopRadius = 0, BottomRadius = 6, Height = 18, RadialSegments = 6 };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = cone, InstanceCount = spots.Count };
        for (int i = 0; i < spots.Count; i++) mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, spots[i] + new Vector3(0, 9, 0)));
        AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.34f, 0.18f) } });
    }

    void BuildSites()
    {
        foreach (var s in World.Map.Data.Sites)
        {
            // 城の段階（館・支城・本城）は、データに段階を持たせるまでの仮の割り当て。
            string model = s.Kind switch { "castle" => s.Id switch { "takano-castle" => "honjo", "iwakiri-castle" => "shijo", _ => "yakata" }, "temple" => "tera", "shrine" => "jinja", "town" => "machi", _ => "mura" };
            AddModel(model, s.Name, new Vector3(s.X, HeightAt(s.X, s.Y), s.Y), HouseColor(s.House));
        }
    }

    internal void AddModel(string model, string name, Vector3 pos, Color house)
    {
        var node = ProtoSiteModels.Build(model, house); node.Position = pos; AddChild(node);
        AddChild(new Label3D { Text = name, Position = pos + new Vector3(0, ProtoSiteModels.Height(model) + 14, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0011f, FontSize = 30, OutlineSize = 10, NoDepthTest = true, Modulate = new Color(1, 0.96f, 0.85f) });
    }

    void BuildActors()
    {
        foreach (var p in World.People)
        {
            float r = p.Player ? 7 : 5;
            var body = new MeshInstance3D { Mesh = new SphereMesh { Radius = r, Height = r * 2 }, MaterialOverride = new StandardMaterial3D { AlbedoColor = p.Player ? new Color(1, 1, 1) : HouseColor(p.House), EmissionEnabled = p.Player, Emission = new Color(0.4f, 0.4f, 0.2f) } };
            var label = new Label3D { Text = p.Name, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = p.Player ? 0.0011f : 0.0008f, FontSize = 30, OutlineSize = 10, NoDepthTest = true, Modulate = p.Player ? new Color(1, 1, 0.6f) : new Color(1, 1, 1) };
            AddChild(body); AddChild(label); actors[p.Id] = (body, label);
        }
    }

    void BuildHud()
    {
        var layer = new CanvasLayer(); AddChild(layer);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; root.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(root);
        var theme = new Theme { DefaultFontSize = 20 }; root.Theme = theme;
        var top = new PanelContainer(); top.SetAnchorsPreset(Control.LayoutPreset.TopWide); root.AddChild(top);
        var bar = new HBoxContainer(); bar.AddThemeConstantOverride("separation", 8); top.Theme = new Theme { DefaultFontSize = 17 }; top.AddChild(bar);
        clock = new Label(); bar.AddChild(clock); foot = new Label(); bar.AddChild(foot); status = new Label(); bar.AddChild(status);
        bar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        foreach (var v in new[] { 10, 30, 60, 120 }) Add(bar, "speed-" + v, v + "倍", () => { Speed = v; RefreshUi(); });
        Add(bar, "wait", "1時間待つ", () => { WaitUntil = World.Second + 3600; });
        Add(bar, "morning", "翌朝まで", () => { long d = ProtoWorld.Day; WaitUntil = (World.Second - 7 * 3600) / d * d + d + 7 * 3600; });
        Add(bar, "stop", "止まる", () => { World.Stop(); WaitUntil = 0; marker.Visible = false; RefreshUi(); });
        bar.AddChild(new VSeparator());
        Add(bar, "note", "印を残す", () => Debug.OpenNote());
        Add(bar, "stock", "ためた印", () => Debug.OpenStock());

        var side = new PanelContainer { CustomMinimumSize = new Vector2(440, 0), GrowHorizontal = Control.GrowDirection.Begin }; side.SetAnchorsPreset(Control.LayoutPreset.TopRight); side.OffsetLeft = -450; side.OffsetTop = 60; side.OffsetBottom = 60; side.OffsetRight = -10; root.AddChild(side);
        var col = new VBoxContainer(); side.AddChild(col);
        panel = col;

        var bottom = new PanelContainer { GrowVertical = Control.GrowDirection.Begin }; bottom.SetAnchorsPreset(Control.LayoutPreset.BottomLeft); bottom.OffsetTop = -10; bottom.OffsetBottom = -10; bottom.OffsetLeft = 10; bottom.OffsetRight = 760; root.AddChild(bottom);
        var log = new VBoxContainer(); bottom.AddChild(log);
        message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 0.95f, 0.7f) }; log.AddChild(message);
        journal = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; log.AddChild(journal);
    }

    internal Button Add(Container parent, string id, string text, Action action)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None }; parent.AddChild(b); Buttons[id] = b;
        b.Pressed += () => { try { action(); } catch (Exception e) { Message = e.Message; } RefreshUi(); };
        return b;
    }

    string panelKey = "";
    internal void RefreshUi()
    {
        var p = World.Player; var site = World.SiteOf(p);
        clock.Text = ProtoWorld.Clock(World.Second);
        foot.Text = ProtoMap.Name(World.Map.GroundAtCm(p.X, p.Y)) + (site != null ? "（" + World.SiteName(site) + "）" : "");
        status.Text = $"{World.RankName}　勲功{World.Merit}　銭{World.Money}文" + (p.Soldiers > 0 ? $"　兵{p.Soldiers}・士気{p.Morale}" : "") + (World.Second < p.HurtUntil ? "　手傷" : "");
        foreach (var v in new[] { 10, 30, 60, 120 }) Buttons["speed-" + v].Disabled = Speed == v;
        int stocked = Debug.Stock().Length; Buttons["stock"].Text = "ためた印（" + stocked + "）"; Buttons["stock"].Disabled = stocked == 0;
        // 中身が変わったときだけ作り直す（押している最中にボタンが消えないように）。
        var actions = World.Actions().ToArray(); var open = World.OpenTasks.ToArray(); var near = World.Nearby.ToArray();
        var key = string.Join("|", actions.Select(a => a.Id + a.Text)) + "#" + string.Join("|", open.Select(t => t.Id + t.Step)) + "#" + string.Join("|", near.Select(n => n.Id));
        if (key != panelKey)
        {
            panelKey = key;
            foreach (var c in panel.GetChildren()) { panel.RemoveChild(c); c.QueueFree(); }
            foreach (var k in Buttons.Keys.Where(k => k.StartsWith("task:") || k.StartsWith("talk:") || k.StartsWith("act:")).ToArray()) Buttons.Remove(k);
            if (actions.Length > 0)
            {
                panel.AddChild(new Label { Text = "やること" });
                foreach (var a in actions) { var aid = a.Id; Add(panel, "act:" + aid, a.Text, () => { if (aid == "council:open") OpenCouncil(); else { Message = World.Do(aid); ShowGoal(); } }); }
                panel.AddChild(new HSeparator());
            }
            panel.AddChild(new Label { Text = "任務" });
            foreach (var t in open)
            {
                panel.AddChild(new Label { Text = t.Title + "　" + (t.Current?.Text ?? ""), AutowrapMode = TextServer.AutowrapMode.WordSmart });
                var id = t.Id; var b = Add(panel, "task:" + id, "", () => { if (World.GoToTask(id)) ShowGoal(); }); taskButtons[id] = b;
            }
            if (near.Length > 0)
            {
                panel.AddChild(new HSeparator()); panel.AddChild(new Label { Text = "近くの人" });
                foreach (var n in near) { var nid = n.Id; Add(panel, "talk:" + nid, "話す：" + n.Name, () => Message = World.Do("talk:" + nid)); }
            }
        }
        foreach (var t in open)
        {
            if (!taskButtons.TryGetValue(t.Id, out var b) || !IsInstanceValid(b)) continue;
            var eta = World.TaskEta(t);
            b.Text = World.TaskPlace(t) + "へ　" + (eta == null ? "道がない" : eta <= 5 ? "ここ" : "着くのは" + HourMinute(World.Second + eta.Value) + "ごろ");
        }
        message.Text = Message;
        journal.Text = string.Join((char)10, World.Journal.TakeLast(6).Select(e => HourMinute(e.At) + "　" + e.Text));
    }
    static string HourMinute(long s) => $"{s % ProtoWorld.Day / 3600}時{s % 3600 / 60:00}分";

    void ShowGoal()
    {
        var p = World.Player; if (!p.Moving) { marker.Visible = false; return; }
        var (x, y) = p.Path[^1]; marker.Position = ToScene(x, y) + new Vector3(0, 0.6f, 0); marker.Visible = true;
    }

    internal bool ClickGround(Vector2 screen)
    {
        var from = Camera.ProjectRayOrigin(screen); var dir = Camera.ProjectRayNormal(screen);
        float t = 0, step = 4;
        for (int i = 0; i < 8000; i++)
        {
            var q = from + dir * t;
            if (q.X >= 0 && q.Z >= 0 && q.X < World.Map.Data.WidthM && q.Z < World.Map.Data.HeightM && q.Y <= HeightAt(q.X, q.Z)) return WalkTo(q.X, q.Z);
            t += step;
        }
        return false;
    }
    internal bool WalkTo(float x, float z)
    {
        if (!World.MoveTo((long)(x * 100), (long)(z * 100))) { Message = "そこへは行けない"; RefreshUi(); return false; }
        Message = ""; ShowGoal(); RefreshUi(); return true;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left) ClickGround(mb.Position);
            else if (mb.ButtonIndex == MouseButton.WheelUp) distance = Math.Max(120, distance * 0.85f);
            else if (mb.ButtonIndex == MouseButton.WheelDown) distance = Math.Min(4000, distance / 0.85f);
        }
        else if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.Q) yaw -= 15; else if (k.Keycode == Key.E) yaw += 15;
            else if (k.Keycode == Key.Space) pan = Vector3.Zero;
            else if (k.Keycode == Key.F8) Debug.OpenNote();
            else if (k.Keycode == Key.F9) Debug.OpenStock();
        }
    }

    internal void SetCamera(float? dist, float? yawDeg, float? pitchDeg) { if (dist != null) distance = dist.Value; if (yawDeg != null) yaw = yawDeg.Value; if (pitchDeg != null) pitch = pitchDeg.Value; PlaceCamera(); }
    internal string CameraText => $"距離{distance:0}m 向き{yaw:0} 傾き{pitch:0}";

    void PlaceCamera()
    {
        var p = World.Player; var target = ToScene(p.X, p.Y) + pan;
        var rot = Basis.FromEuler(new Vector3(-Mathf.DegToRad(pitch), Mathf.DegToRad(yaw), 0));
        Camera.Position = target + rot * new Vector3(0, 0, distance);
        Camera.LookAt(target, Vector3.Up);
    }

    public override void _Process(double delta)
    {
        var input = new Vector3((Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right) ? 1 : 0) - (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left) ? 1 : 0), 0, (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down) ? 1 : 0) - (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up) ? 1 : 0));
        if (input != Vector3.Zero && !Debug.NoteOpen) pan += Basis.FromEuler(new Vector3(0, Mathf.DegToRad(yaw), 0)) * input * (float)(distance * delta);
        bool running = !Debug.NoteOpen && (World.Player.Moving || WaitUntil > World.Second);
        if (running)
        {
            acc += delta * Speed; int n = Math.Min((int)acc, 5000); acc -= n;
            for (int i = 0; i < n; i++)
            {
                bool wasMoving = World.Player.Moving; World.Step();
                if (wasMoving && !World.Player.Moving) dirty = true;
                if (wasMoving && !World.Player.Moving && WaitUntil <= World.Second) { acc = 0; marker.Visible = false; break; }
                if (!wasMoving && WaitUntil <= World.Second) { acc = 0; break; }
            }
        }
        else acc = 0;
        if (dirty || World.Second / 600 != savedBucket) { dirty = false; SaveGame(); }
        UpdateActors(); DrawPath(); PlaceCamera();
        uiTimer -= delta; if (uiTimer <= 0) { uiTimer = running ? 0.5 : 0.25; RefreshUi(); }
    }

    readonly Dictionary<string, (MeshInstance3D Body, Label3D Label)> markers = new();
    readonly Dictionary<string, Label3D> flags = new();
    readonly List<MeshInstance3D> scoutDots = new();
    void UpdateActors()
    {
        bool close = distance < 700, war = World.War.Phase is "軍議待ち" or "行軍" or "合戦";
        foreach (var p in World.People)
        {
            var (body, label) = actors[p.Id]; bool seen = World.Sees(World.Player, p);
            bool unit = p.Soldiers > 0 && war;
            if (body.MaterialOverride is StandardMaterial3D m && !p.Player) m.AlbedoColor = HouseColor(p.House);
            body.Visible = seen; label.Visible = seen && (p.Player || close || unit);
            label.Text = unit ? $"{p.Name}　兵{p.Soldiers}" : p.Name;
            float grow = unit ? 1 + MathF.Sqrt(p.Soldiers) / 8 : 1, scale = Math.Max(1, distance / 450), r = (p.Player ? 7 : 5) * scale * grow; var pos = ToScene(p.X, p.Y) + new Vector3(0, r, 0);
            body.Position = pos; body.Scale = Vector3.One * scale * grow; label.Position = pos + new Vector3(0, r + distance * 0.035f, 0);
        }
        // 見えない敵の「最後に見た場所と時刻」の印。古いほど薄い。
        foreach (var mk in World.Markers)
        {
            if (!markers.TryGetValue(mk.Unit, out var v))
            {
                var mat = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.2f, 0.2f, 0.5f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
                v = (new MeshInstance3D { Mesh = new SphereMesh { Radius = 6, Height = 12 }, MaterialOverride = mat }, new Label3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0008f, FontSize = 28, OutlineSize = 8, NoDepthTest = true, Modulate = new Color(1, 0.75f, 0.75f) });
                AddChild(v.Body); AddChild(v.Label); markers[mk.Unit] = v;
            }
            var enemy = World.Person(mk.Unit); bool now = World.Sees(World.Player, enemy) || !enemy.Active;
            float age = (World.Second - mk.SeenAt) / 3600f, alpha = Math.Clamp(0.6f - age * 0.15f, 0.12f, 0.6f);
            ((StandardMaterial3D)v.Body.MaterialOverride).AlbedoColor = new Color(0.8f, 0.2f, 0.2f, alpha);
            float sc = Math.Max(1, distance / 450);
            var at = ToScene(mk.X, mk.Y) + new Vector3(0, 6 * sc, 0);
            v.Body.Position = at; v.Body.Scale = Vector3.One * sc; v.Label.Position = at + new Vector3(0, 8 * sc + distance * 0.03f, 0);
            v.Label.Text = $"{enemy.Name}　{HourMinute(mk.SeenAt)}（{mk.By}）";
            v.Body.Visible = v.Label.Visible = !now;
        }
        // 陣の場所の旗（合戦の間だけ）
        foreach (var pos in World.Map.Data.Positions ?? [])
        {
            if (!flags.TryGetValue(pos.Id, out var f)) { f = new Label3D { Text = "⚑ " + pos.Name, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0008f, FontSize = 26, OutlineSize = 8, NoDepthTest = true, Modulate = new Color(1, 1, 0.8f) }; AddChild(f); flags[pos.Id] = f; }
            f.Visible = war && pos.Id != "takano-castle"; f.Position = ToScene(pos.X * 100L, pos.Y * 100L) + new Vector3(0, 30, 0);
        }
        // 物見
        var live = World.Scouts.Where(x => x.Phase != "済").ToArray();
        while (scoutDots.Count < live.Length) { var d = new MeshInstance3D { Mesh = new SphereMesh { Radius = 3, Height = 6 }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1, 1, 1) } }; AddChild(d); scoutDots.Add(d); }
        for (int i = 0; i < scoutDots.Count; i++) { scoutDots[i].Visible = i < live.Length; if (i < live.Length) { float sc = Math.Max(1, distance / 450); scoutDots[i].Position = ToScene(live[i].X, live[i].Y) + new Vector3(0, 3 * sc, 0); scoutDots[i].Scale = Vector3.One * sc; } }
    }

    // ---- 軍議の窓 ----
    Window? councilWindow;
    internal void OpenCouncil()
    {
        if (councilWindow != null || !World.CouncilOpen) return;
        var view = World.Council();
        var w = new Window { Title = "軍議", Size = new Vector2I(760, 520), Exclusive = true, Transient = true };
        var box = new VBoxContainer { Theme = new Theme { DefaultFontSize = 18 } }; box.SetAnchorsPreset(Control.LayoutPreset.FullRect); box.OffsetLeft = 12; box.OffsetTop = 12; box.OffsetRight = -12; box.OffsetBottom = -12; w.AddChild(box);
        foreach (var line in view.Opinions) box.AddChild(new Label { Text = line, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        box.AddChild(new HSeparator());
        OptionButton Choice(string title, string[] items, int pick)
        {
            var row = new HBoxContainer(); box.AddChild(row); row.AddChild(new Label { Text = title, CustomMinimumSize = new Vector2(140, 0) });
            var o = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; foreach (var it in items) o.AddItem(it); o.Select(pick); row.AddChild(o); return o;
        }
        var pos = Choice("自分の陣", view.Positions.Select(p => p.Name).ToArray(), Array.FindIndex(view.Positions, p => p.Id == "forest-edge"));
        var dep = Choice("出立", view.Departures, 1);
        var sig = Choice("合図", view.Signals, 0);
        var ok = new Button { Text = "決める" }; box.AddChild(ok);
        ok.Pressed += () => { Message = World.DecideCouncil(view.Positions[pos.Selected].Id, view.Departures[dep.Selected], view.Signals[sig.Selected]); w.QueueFree(); councilWindow = null; RefreshUi(); };
        w.CloseRequested += () => { w.QueueFree(); councilWindow = null; };
        councilWindow = w; AddChild(w); w.PopupCentered();
    }

    void DrawPath()
    {
        pathMesh.ClearSurfaces(); var p = World.Player; if (!p.Moving) return;
        var points = new List<Vector3> { ToScene(p.X, p.Y) };
        for (int i = p.PathIndex; i < p.Path.Count; i++)
        {
            var a = i == p.PathIndex ? (p.X, p.Y) : p.Path[i - 1]; var b = p.Path[i];
            long len = (long)Math.Sqrt((double)(b.X - a.Item1) * (b.X - a.Item1) + (double)(b.Y - a.Item2) * (b.Y - a.Item2)); int parts = (int)Math.Clamp(len / 1000, 1, 200);
            for (int k = 1; k <= parts; k++) { long x = a.Item1 + (b.X - a.Item1) * k / parts, y = a.Item2 + (b.Y - a.Item2) * k / parts; points.Add(ToScene(x, y)); }
        }
        if (points.Count < 2) return;
        float half = Math.Max(1.5f, distance / 260);
        pathMesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for (int i = 0; i < points.Count; i++)
        {
            var d = points[Math.Min(i + 1, points.Count - 1)] - points[Math.Max(i - 1, 0)]; d.Y = 0; if (d.LengthSquared() < 1e-6f) d = Vector3.Right;
            var side = new Vector3(-d.Z, 0, d.X).Normalized() * half; var up = new Vector3(0, 1.5f, 0);
            pathMesh.SurfaceAddVertex(points[i] + side + up); pathMesh.SurfaceAddVertex(points[i] - side + up);
        }
        pathMesh.SurfaceEnd();
    }
}
