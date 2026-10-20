using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;
public partial class RegionalSceneryTrial : Control
{
    WorldSessionHost host = null!; WorldSession Session => host.Current; GenerationSaveStore store = null!;
    Node3D world = null!; Camera3D camera = null!; SubViewport viewport = null!; SubViewportContainer surface = null!;
    Label status = null!, notice = null!, details = null!; readonly Dictionary<string, Button> buttons = new();
    readonly Dictionary<EntityId, (Officer3DCustomActor Model, Label Label)> models = new();
    readonly List<MeshInstance3D> terrain = new(); readonly List<(Vector3 A, Vector3 B, Vector3 C)> triangles = new();
    readonly List<MeshInstance3D> couriers = new(); readonly List<string> checks = new();
    FieldPoint center = new(20000, 18000); FieldPoint? dragStart; float span = 205, yaw; double fraction; bool auto, testing; int speed = 1;
    string Mode => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--scenery-")) ?? "";
    string Case => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--case="))?[7..] ?? "move";
    string Output => ProjectSettings.GlobalizePath("res://../Docs/Design/RegionalSceneryV1/runtime-local");
    ContractWorld Initial() => RegionalScenery.Create(Case == "scale" ? 12 : 3, Case == "refuse", Case == "rout");
    string SaveRoot => testing ? Output + "/saves/" + Guid.ParseExact(OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--save-key="))[11..], "N").ToString("N") : ProjectSettings.GlobalizePath("user://regional-scenery-v1");
    public override void _Ready()
    {
        try
        {
            testing = Mode != ""; host = new(new(Initial())); store = new(SaveRoot, Initial().Definitions);
            Theme = new Theme { DefaultFontSize = 17, DefaultFont = new SystemFont { FontNames = new[] { "Yu Gothic UI", "Meiryo", "sans-serif" } } };
            var margin = new MarginContainer(); AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 12);
            var col = new VBoxContainer(); margin.AddChild(col); col.AddChild(new Label { Text = "百代鏡 / 山・森・道・川・城" });
            col.AddChild(new Label { Text = "清洲座標に接続した合成の一地域。山と地面、幹と城壁、川と橋を移動判定と共用。", Modulate = new Color("e6c785") });
            var bar = new HFlowContainer(); col.AddChild(bar); bar.Visible = !Mode.EndsWith("video");
            Add(bar, "move", "森・麓へ移動", () => Issue(RegionalAction.Move, false, new(25000,25000),new(65000,-25000)));
            Add(bar,"bridge","橋を渡る",()=>Issue(RegionalAction.Move,false,new(-15000,85000),new(5000,85000)));
            Add(bar,"castle","城門から中庭へ",()=>Issue(RegionalAction.Move,false,new(-42000,-32000),new(-30000,-32000)));
            Add(bar,"ridge","麓の道へ",()=>Issue(RegionalAction.Move,false,new(25000,-25000),new(65000,-25000)));
            Add(bar, "clerk", "別働隊：布陣命令を発送", () => Issue(RegionalAction.Move, true, new(-25000, 25000), new(5000, 35000)));
            Add(bar, "engage", "自隊：接敵", () => Issue(RegionalAction.Engage)); Add(bar, "retreat", "自隊：帰還", () => Issue(RegionalAction.Retreat)); Add(bar, "hold", "自隊：停止", () => Issue(RegionalAction.Hold));
            Add(bar, "one", "1秒", () => Advance(Session.Seconds + 1)); Add(bar, "ten", "10秒", () => Advance(Session.Seconds + 10)); Add(bar, "auto", "自動進行", () => auto = !auto); Add(bar, "speed", "速度切替", () => speed = speed == 1 ? 10 : 1);
            Add(bar, "save", "保存", () => Save("manual")); Add(bar, "load", "復旧", () => Load("manual")); Add(bar, "wide", "全体", () => span = 205); Add(bar, "near", "自隊へ寄る", () => { span = 65; center = RegionalBattle.Position(Session.DiagnosticSnapshot(), ContractFixture.Officer); }); Add(bar, "rotate", "視点回転", () => yaw += 30);
            Add(bar, "site", "拠点に入る／野外へ", () => { var r = Session.SetRegionalActive(!Session.DiagnosticSnapshot().Region!.Active, Session.Revision); notice.Text = r.VisibleReason; });
            Add(bar, "road", "拠点から熱田へ", () => { var r = Session.Submit(TravelWorld.Move(Session.DiagnosticSnapshot(), "request:road/" + Session.Revision, "fork"), Session.Revision); notice.Text = r.VisibleReason; });
            status = new Label(); notice = new Label { Text = "地面を左ドラッグして自隊の布陣線を指定。別働隊は命令が届いて判断してから動き、返報も時間がかかります。" }; col.AddChild(status); col.AddChild(notice);
            surface = new SubViewportContainer { Stretch = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new(640, 350) }; col.AddChild(surface);
            viewport = new SubViewport { OwnWorld3D = true, Size = new(1200, 650), RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; surface.AddChild(viewport); world = new Node3D(); viewport.AddChild(world);
            world.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("bac2b3"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = .72f } }); world.AddChild(new DirectionalLight3D { RotationDegrees = new(-48, -25, 0), LightEnergy = .95f });
            SetCameraPreset();BuildGround(); camera = new Camera3D { Current = true, Projection = Camera3D.ProjectionType.Orthogonal }; world.AddChild(camera);
            details = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; col.AddChild(details); details.Visible = !Mode.EndsWith("video"); col.AddChild(new Label { Text = "山・森・街道・支流・木橋・小規模な砦。配置は検証用の合成で、清洲の史実復元ではありません。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
            surface.GuiInput += InputGround; Refresh(); if (Mode.EndsWith("video")) CallDeferred(MethodName.BeginVideo); else if (testing) CallDeferred(MethodName.RunChecks);
        }
        catch (Exception e) { GD.PrintErr(e); GetTree().Quit(1); }
    }
    Vector3 Point(FieldPoint p) => new((p.X - center.X) / 1000f, RegionalGround.Height(p,true) / 1000f, -(p.Y - center.Y) / 1000f);
    static StandardMaterial3D GroundMaterial(string hex) => new() { AlbedoColor = new Color(hex), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
    RegionalSceneryRenderer scenery=null!;
    void BuildGround(){scenery=new RegionalSceneryRenderer();world.AddChild(scenery);}
    void UpdateGround(){scenery.Visible=true;scenery.Rebuild(center);triangles.Clear();triangles.AddRange(scenery.PickTriangles);}
    FieldPoint? Pick(Vector2 pixel) { var ray = camera.ProjectRayOrigin(pixel); var normal = camera.ProjectRayNormal(pixel); Vector3? nearest = null; float distance = float.MaxValue; foreach (var t in triangles) { var hit = Geometry3D.RayIntersectsTriangle(ray, normal, t.A, t.B, t.C); if (hit.VariantType != Variant.Type.Nil) { var p = hit.AsVector3(); if (ray.DistanceTo(p) < distance) { nearest = p; distance = ray.DistanceTo(p); } } } return nearest is { } n ? new((long)Math.Round(n.X * 1000) + center.X, (long)Math.Round(-n.Z * 1000) + center.Y) : null; }
    void InputGround(InputEvent e) { if (e is not InputEventMouseButton m) return; if (m.Pressed && m.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown) { span = Mathf.Clamp(span * (m.ButtonIndex == MouseButton.WheelUp ? .8f : 1.25f), 25, 240); Refresh(); return; } if (m.ButtonIndex != MouseButton.Left) return; var p = Pick(m.Position * new Vector2(viewport.Size.X / surface.Size.X, viewport.Size.Y / surface.Size.Y)); if (m.Pressed) dragStart = p; else if (p is { } goal && dragStart is { } start) { dragStart = null; Issue(RegionalAction.Move, false, start, goal); Refresh(); } }
    void Drag(FieldPoint a, FieldPoint b) { Vector2 Screen(FieldPoint p) => camera.UnprojectPosition(Point(p)) * new Vector2(surface.Size.X / viewport.Size.X, surface.Size.Y / viewport.Size.Y); surface.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = Screen(a) }); surface.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = Screen(b) }); }
    void Issue(RegionalAction action, bool clerk = false, FieldPoint? a = null, FieldPoint? b = null) { var r = Session.IssueRegional(new(new("request:regional/" + Session.Revision), ContractFixture.Officer, clerk ? ContractFixture.Clerk : ContractFixture.Officer, action, a ?? new(0, 0), b ?? new(0, 0)), Session.Revision); notice.Text = r.VisibleReason; if (testing && r.Code != ResultCode.Accepted) throw new Exception("Command rejected " + action);if(testing&&!clerk&&Session.DiagnosticSnapshot().Region!.Commands.Last().Result!="applied")throw new Exception("Route held for "+action); }
    void Refresh()
    {
        if (camera == null) return; var w = Session.DiagnosticSnapshot(); if (!w.Region!.Active) { DrawRoad(w); return; }
        foreach (var r in roadMeshes) r.Visible = false; foreach (var t in terrain) t.Visible = true; UpdateGround(); camera.Near = .05f; camera.Far = 5000; camera.Size = span; camera.Position = new(Mathf.Sin(Mathf.DegToRad(yaw)) * span * .6f, span * .85f, Mathf.Cos(Mathf.DegToRad(yaw)) * span * .7f); camera.LookAt(new(0, 8, 0));
        foreach (var model in models.Values) { model.Model.Visible = false; model.Label.Visible = false; }
        foreach (var view in RegionalBattle.View(w, ContractFixture.Officer))
        {
            if (!models.TryGetValue(view.Unit, out var item)) { var m = new Officer3DCustomActor(); world.AddChild(m); var unit = w.Region!.Units.Single(u => u.Id == view.Unit); m.Apply(Officer3DCustomActor.Preset(unit.Side == 1 ? "commander" : unit.Commander == ContractFixture.Clerk ? "veteran" : "red_guard")); m.Scale = Vector3.One * 2; var label = new Label { MouseFilter = MouseFilterEnum.Ignore, Modulate = unit.Side == 1 ? new Color("ffb3a3") : new Color("fff0b5") }; label.AddThemeFontSizeOverride("font_size", 14); AddChild(label); item = (m, label); models[view.Unit] = item; }
            item.Model.Scale = Vector3.One * 2; var u = w.Region!.Units.Single(u => u.Id == view.Unit); var p = view.Position; var next = view.Live ? RegionalGround.At(u.Route, u.RouteOrigin, w.ElapsedSeconds + 1) : p; var interp = new FieldPoint(p.X + (long)((next.X - p.X) * fraction), p.Y + (long)((next.Y - p.Y) * fraction)); item.Model.Visible = view.Soldiers > 0; item.Model.Position = Point(interp); item.Model.Rotation = new(0, Mathf.Atan2(view.Heading.X, -view.Heading.Y), 0);
            var clip = view.Live && next != p ? "walk" : view.Phase == "engaged" ? "attack" : "idle"; if (!item.Model.Clips.ContainsKey(clip)) clip = "idle"; var duration = item.Model.Player.GetAnimation(item.Model.Clips[clip]).Length; double distance = view.Live ? u.Route.Sum(l => w.ElapsedSeconds >= l.EndAt ? RegionalGround.Distance(l.From, l.To) : w.ElapsedSeconds <= l.StartAt ? 0 : RegionalGround.Distance(l.From, l.To) * (w.ElapsedSeconds - l.StartAt) / (l.EndAt - l.StartAt)) : 0; var time = clip == "walk" ? (distance + RegionalGround.Distance(p, next) * fraction) / 1000 / 2 / item.Model.WalkStride * duration : (w.ElapsedSeconds + fraction); item.Model.Freeze(clip, time % duration);
            var pixel = camera.UnprojectPosition(Point(p) + new Vector3(0, 6, 0)); item.Label.Text = $"{(u.Side == 1 ? "敵" : u.Commander == ContractFixture.Clerk ? "別働" : "自隊")} {view.Soldiers} / 士気{view.Morale}" + (view.Live ? "" : "（旧情報）"); item.Label.Position = (surface.GlobalPosition + pixel * new Vector2(surface.Size.X / viewport.Size.X, surface.Size.Y / viewport.Size.Y) - new Vector2(30, 18)).Round(); item.Label.Visible = view.Soldiers > 0 && (w.Region.PerForce == 3 || w.Region.Units.First(x => x.Commander == u.Commander).Id == u.Id);var bounds=surface.GetGlobalRect();item.Label.Visible=item.Label.Visible&&bounds.HasPoint(item.Label.Position)&&bounds.HasPoint(item.Label.Position+new Vector2(180,22));
        }
        foreach (var m in couriers) m.Visible = false; int ci = 0; foreach (var d in w.Region!.Deliveries) { var attempt = w.Messaging.Attempts.Single(a => a.Id == d.Attempt); if (attempt.Status != HakutaiKagami.Core.Messaging.AttemptStatus.InTransit) continue; var p = RegionalGround.At(d.Path, d.Origin, w.ElapsedSeconds); if (RegionalGround.Distance(p, RegionalBattle.Position(w, ContractFixture.Officer)) > 80000) continue; if (ci == couriers.Count) { var m = new MeshInstance3D { Mesh = new SphereMesh { Radius = .7f, Height = 1.4f }, MaterialOverride = GroundMaterial("ffdf72") }; world.AddChild(m); couriers.Add(m); } couriers[ci].Visible = true; couriers[ci++].Position = Point(p) + Vector3.Up; }
        status.Text = $"世界時間 {Session.Seconds}秒 / {(Session.Paused ? "停止" : "進行可能")} / {w.Region.Units.Length}分隊 / 損害適用 {w.Region.Losses.Length}件 / 共有地形0〜43m";
        details.Text = string.Join(" ／ ", w.Region.Commands.TakeLast(3).Select(c => $"{c.Request.Action} → {RegionalBattle.Status(w, c, ContractFixture.Officer)}")) + "\n左ドラッグ：自隊布陣。黄球：近傍で目撃した伝令。敵は80m内の目撃と最終既知情報のみ。";
    }
    readonly List<MeshInstance3D> roadMeshes = new();
    void DrawRoad(ContractWorld w)
    {
        scenery.Visible=false;foreach (var t in terrain) t.Visible = false; foreach (var m in models.Values) { m.Model.Visible = false; m.Label.Visible = false; }
        foreach (var c in couriers) c.Visible = false; var view = Session.GetBranchView(ContractFixture.Officer); var p = new RoutePoint(view.Position.EastMm + (long)((view.NextPosition.EastMm - view.Position.EastMm) * fraction), view.Position.NorthMm + (long)((view.NextPosition.NorthMm - view.Position.NorthMm) * fraction)); Vector3 Local(RoutePoint q) => new((q.EastMm - p.EastMm) / 1000f, 0, -(q.NorthMm - p.NorthMm) / 1000f); int i = 0; foreach (var e in NationalRoutes.Edges) { if (i == roadMeshes.Count) { var mesh = new MeshInstance3D { MaterialOverride = GroundMaterial(NationalRoutes.Sea(e.Id) ? "598ac4" : "cbb988") }; world.AddChild(mesh); roadMeshes.Add(mesh); } var a = Local(NationalRoutes.Nodes.Single(n => n.Id == e.From).Position); var b = Local(NationalRoutes.Nodes.Single(n => n.Id == e.To).Position); var m = roadMeshes[i++]; m.Mesh = new BoxMesh { Size = new(55, 2, (b - a).Length()) }; m.Position = (a + b) / 2; m.Rotation = new(0, Mathf.Atan2((b - a).X, (b - a).Z), 0); m.Visible = true; }
        camera.Near = 1; camera.Far = 2000000; camera.Size = 30000; camera.Position = new(18000, 25000, 23000); camera.LookAt(Vector3.Zero); var item = models[FieldWorld.UnitIds[0]]; item.Model.Visible = true; item.Model.Scale = Vector3.One * 600; item.Model.Position = Vector3.Zero; item.Model.Rotation = new(0, Mathf.Atan2(view.Heading.EastMm, -view.Heading.NorthMm), 0); var clip = view.Walking ? "walk" : "idle"; var duration = item.Model.Player.GetAnimation(item.Model.Clips[clip]).Length; item.Model.Freeze(clip, (Session.Seconds + fraction) % duration); item.Label.Visible = true; item.Label.Text = "本人：道路網を移動"; var pixel = camera.UnprojectPosition(new(0, 240, 0)); item.Label.Position = (surface.GlobalPosition + pixel * new Vector2(surface.Size.X / viewport.Size.X, surface.Size.Y / viewport.Size.Y)).Round(); status.Text = $"世界時間 {Session.Seconds}秒 / 道路網 / 同じ位置・経路を表示"; details.Text = $"現在位置 {view.Position.EastMm}, {view.Position.NorthMm} mm / {(view.Walking ? "移動中" : "到着・待機")} / 経路：{string.Join(" → ", view.PlannedEdges)}";
    }
    void Add(Container c, string id, string title, Action a) { var b = new Button { Text = title }; c.AddChild(b); buttons[id] = b; b.Pressed += () => { try { a(); Refresh(); } catch (Exception e) { notice.Text = e.Message; interaction = e; } }; }
    Exception? interaction;
    void Click(string id) { interaction = null; buttons[id].EmitSignal(Button.SignalName.Pressed); if (interaction != null) throw interaction; }
    void Advance(long t) { fraction = 0; var result = Session.AdvanceTo(t); if (testing && result.ReachedAt != t) throw new Exception("Clock did not reach target"); Refresh(); }
    void Save(string slot) { store.Save(Session, slot, Session.DiagnosticSnapshot().WorldId, new(fraction, speed)); }
    void Load(string slot) { var p = store.Prepare(slot); if (host.CommitLoad(p, Session, Session.Revision) != ResultCode.Accepted) throw new Exception("Load refused"); fraction = p.Presentation.DisplayFraction; speed = p.Presentation.DisplaySpeed; auto = false; Refresh(); }
    string Pose() => System.Text.Json.JsonSerializer.Serialize(new { Session.Seconds, Fraction = fraction, Center = center, Span = span, Yaw = yaw, View = RegionalBattle.View(Session.DiagnosticSnapshot(), ContractFixture.Officer), BranchPosition = Session.GetBranchView(ContractFixture.Officer).Position, BranchHeading = Session.GetBranchView(ContractFixture.Officer).Heading, Units = models.OrderBy(x => x.Key.Value, StringComparer.Ordinal).Where(x => x.Value.Model.Visible).Select(x => new { Id = x.Key.Value, x.Value.Model.Position, x.Value.Model.Rotation, Label = x.Value.Label.Text }) });
    void Check(bool ok, string title) { if (!ok) throw new Exception(title); checks.Add(title); }
    async Task Capture(string name) { Refresh(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); if (Case == "road" && name == "s1") { var image = viewport.GetTexture().GetImage(); int visible = 0; for (int y = 0; y < image.GetHeight(); y++) for (int x = 0; x < image.GetWidth(); x++) { var c = image.GetPixel(x, y); if (c.R > .5f && c.G > .35f) visible++; } Check(visible > 500, "actual road geometry remains visibly rendered at distant camera"); } GetViewport().GetTexture().GetImage().SavePng(Output + "/" + Case + "-" + name + ".png"); }
    void Stage(string stage) { Refresh(); Save(Case + "-" + stage); System.IO.File.WriteAllText(Output + "/" + Case + "-" + stage + "-pose.json", Pose()); }
    void SetCameraPreset(){(center,span,yaw)=Case switch{"bridge"=>(new FieldPoint(5000,55000),135f,25f),"castle"=>(new FieldPoint(-30000,-20000),115f,35f),"forest"=>(new FieldPoint(25000,10000),150f,-15f),_ =>(new FieldPoint(20000,18000),205f,0f)};}
    long[] Stages=>new long[]{7,55,150};
    long Final=>600;
    async void RunChecks()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Output); if (Mode.EndsWith("restore")) { for (int i = 0; i < Stages.Length; i++) { Load(Case + "-s" + i); Check(Pose() == System.IO.File.ReadAllText(Output + "/" + Case + "-s" + i + "-pose.json"), "restore saved positions/heights/visibility/poses " + i); await Capture("restored-s" + i); Session.SetPaused(false); Advance(Final); fraction = 0; await Capture("continued-s" + i); } }
            else
            {
                var timer=System.Diagnostics.Stopwatch.StartNew();Click(Case=="castle"?"castle":Case=="bridge"?"bridge":Case=="ridge"?"ridge":"move");
                Check(scenery.TreeCount>0&&scenery.StructureCount>=8,"rendered forest, keep, walls and rails from authoritative catalog");
                Check(scenery.CatalogHash==RegionalScenery.Version,"renderer uses same immutable catalog version as movement");
                var picked=Pick(camera.UnprojectPosition(Point(new(5000,55000))));Check(picked!=null&&RegionalGround.Distance(picked,new(5000,55000))<20,"bridge picking and walking surface share the same height");
                Check(Case == "road" ? !Session.DiagnosticSnapshot().Region!.Active : Session.DiagnosticSnapshot().Region!.Commands[0].Result is "applied" or "in-transit", "command accepted and planned"); for (int i = 0; i < Stages.Length; i++) { Advance(Stages[i]); fraction = .375; Stage("s" + i); await Capture("s" + i); }
                Advance(Final); fraction = 0; await Capture("final"); var w=Session.DiagnosticSnapshot();ContractValidation.Validate(w);
                Check(w.Region!.Units.Where(u=>u.Commander==ContractFixture.Officer).All(u=>u.Position==u.Goal),"own detachments reach targets without crossing trunks, water or walls");
                Check(w.Region.Commands[0].Result=="applied","scenery route applied, not silently held");
                foreach(var view in RegionalBattle.View(w,ContractFixture.Officer).Where(v=>v.Live)){Check(Math.Abs(models[view.Unit].Model.Position.Y-view.Height/1000f)<.002f,"model feet agree with authoritative terrain height "+view.Unit.Value);}
                timer.Stop(); GD.Print("CASE_ELAPSED_MS " + timer.ElapsedMilliseconds);
            }
            System.IO.File.WriteAllText(Output + "/" + Case + "-" + (Mode.EndsWith("restore") ? "restore" : "check") + "-checks.json", System.Text.Json.JsonSerializer.Serialize(new { Checks = checks, ViewportRect = new[] { surface.GlobalPosition.X, surface.GlobalPosition.Y, surface.Size.X, surface.Size.Y } })); GD.Print("SCENERY_CHECK_PASSED " + checks.Count); GetTree().Quit();
        }
        catch (Exception e) { GD.PrintErr(e); GetTree().Quit(1); }
    }
    bool videoReady;int frame;long movieBase;
    void BeginVideo(){System.IO.Directory.CreateDirectory(Output);Click("move");videoReady=true;}
    void Video(){int f=frame++;if(f==96){center=new(25000,10000);span=150;yaw=-15;}
        if(f<192){double clock=f*.8;Advance((long)clock);fraction=clock%1;}
        if(f==192){movieBase=Session.Seconds;Click("bridge");center=new(5000,55000);span=135;yaw=25;}
        if(f>=192&&f<384){double clock=movieBase+(f-192)*1.25;Advance((long)clock);fraction=clock%1;}
        if(f==384){movieBase=Session.Seconds;Click("castle");center=new(-30000,-20000);span=115;yaw=35;}
        if(f>=384&&f<528){double clock=movieBase+(f-384)*2.5;Advance((long)clock);fraction=clock%1;}
        if(f==528){fraction=.375;Save("movie");movieBase=Session.Seconds;}
        if(f==552){Load("movie");Session.SetPaused(false);}
        if(f>=576){double clock=movieBase+1+(f-576)*.5;Advance((long)clock);fraction=clock%1;}
        notice.Text=f<96?"① 一地域の山・森・道・川・城を共通地形から描画":f<192?"② 木の幹を避け、森と麓を移動":f<384?"③ 川には入れず、木橋を渡って対岸へ":f<528?"④ 城壁を迂回し、城門から中庭へ":f<576?"⑤ 途中保存 → 同じ位置と背景で復旧":"⑥ 復旧後も同じ地形判定で移動を継続";Refresh();if(f>=623){GD.Print("SCENERY_VIDEO_PASSED 624 frames");GetTree().Quit();}}
    public override void _Process(double delta) { if (videoReady) { Video(); return; } if (testing || !auto || Session.Paused) return; fraction += Math.Min(delta, .25) * speed; long n = (long)fraction; if (n > 0) { Session.AdvanceTo(Session.Seconds + n); fraction -= n; } Refresh(); }
}
