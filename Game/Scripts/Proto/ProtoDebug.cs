using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HakutaiKagami.Core.Proto;

// 試作版の確認用の窓口と印のため置き（2026-11-18 ユーザー決定）。
// ・窓口：このパソコンの中（127.0.0.1）からだけ受け付ける。画面の絵・本人が知っている状態・世界の全部を読み、操作を送れる。
// ・印：F8 で今の画面・状態と一言を「ためておく」に残す。F9 で一覧を開き、選んだものを「送る」と一つの束にまとめる。
//   送られた束だけを Claude が読む。送るまで修正は始めない。
public partial class ProtoDebug : Node
{
    readonly ProtoGame game;
    readonly ConcurrentQueue<(string Path, Dictionary<string, string> Query, TaskCompletionSource<string> Reply)> requests = new();
    TcpListener? listener;
    Thread? thread;
    volatile bool running = true;
    internal string Root { get; }
    Window? stockWindow;

    public ProtoDebug(ProtoGame game)
    {
        this.game = game;
        var custom = System.Environment.GetEnvironmentVariable("HAKUTAI_PROTO_NOTES");
        Root = !string.IsNullOrEmpty(custom) ? custom : Directory.Exists("D:/02_プロジェクト別/百代鏡/検証記録") ? "D:/02_プロジェクト別/百代鏡/検証記録/試作版" : ProjectSettings.GlobalizePath("user://proto-notes");
        Directory.CreateDirectory(Path.Combine(Root, "ためておく")); Directory.CreateDirectory(Path.Combine(Root, "送った"));
    }

    public override void _Ready()
    {
        int port = int.TryParse(System.Environment.GetEnvironmentVariable("HAKUTAI_PROTO_PORT"), out var p) ? p : 8765;
        try { listener = new TcpListener(IPAddress.Loopback, port); listener.Start(); thread = new Thread(Serve) { IsBackground = true }; thread.Start(); GD.Print($"PROTO debug http://127.0.0.1:{port}/ notes={Root}"); }
        catch (Exception e) { GD.PrintErr("PROTO debug off: " + e.Message); }
    }

    public override void _ExitTree() { running = false; try { listener?.Stop(); } catch (Exception) { } }

    void Serve()
    {
        while (running)
        {
            TcpClient client;
            try { client = listener!.AcceptTcpClient(); } catch (Exception) { return; }
            ThreadPool.QueueUserWorkItem(_ => Handle(client));
        }
    }

    void Handle(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream(); var reader = new StreamReader(stream, Encoding.UTF8);
                var line = reader.ReadLine() ?? ""; while (!string.IsNullOrEmpty(reader.ReadLine())) { }
                var parts = line.Split(' '); var target = parts.Length > 1 ? parts[1] : "/";
                var q = target.IndexOf('?'); var path = q < 0 ? target : target[..q];
                var query = new Dictionary<string, string>();
                if (q >= 0) foreach (var kv in target[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries)) { var i = kv.IndexOf('='); query[Uri.UnescapeDataString(i < 0 ? kv : kv[..i])] = i < 0 ? "" : Uri.UnescapeDataString(kv[(i + 1)..].Replace('+', ' ')); }
                var tcs = new TaskCompletionSource<string>(); requests.Enqueue((path, query, tcs));
                var body = tcs.Task.Wait(60000) ? tcs.Task.Result : "{\"error\":\"時間切れ\"}";
                var bytes = Encoding.UTF8.GetBytes(body);
                var head = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                stream.Write(head); stream.Write(bytes);
            }
            catch (Exception) { }
        }
    }

    public override void _Process(double delta)
    {
        while (requests.TryDequeue(out var r)) Run(r.Path, r.Query, r.Reply);
    }

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static string Write(object o) => JsonSerializer.Serialize(o, Json);

    async void Run(string path, Dictionary<string, string> q, TaskCompletionSource<string> reply)
    {
        try
        {
            var w = game.World;
            string F(string k) => q.TryGetValue(k, out var v) ? v : throw new ArgumentException(k + " がありません");
            switch (path)
            {
                case "/state": reply.SetResult(Write(Known())); return;
                case "/all": reply.SetResult(Write(All())); return;
                case "/walk": game.WalkTo(float.Parse(F("x")), float.Parse(F("y"))); break;
                case "/click": game.ClickGround(new Vector2(float.Parse(F("x")), float.Parse(F("y")))); break;
                case "/do": game.Message = w.Do(F("id")); game.RefreshUi(); break;
                case "/press": if (!game.Buttons.TryGetValue(F("id"), out var b)) throw new ArgumentException("ボタンがありません：" + F("id")); b.EmitSignal(BaseButton.SignalName.Pressed); break;
                case "/advance": { long s = long.Parse(F("s")); for (long i = 0; i < s; i++) { w.Step(); if (q.ContainsKey("untilArrive") && !w.Player.Moving) break; } game.RefreshUi(); break; }
                case "/model": game.AddModel(F("kind"), q.TryGetValue("name", out var mn) ? mn : "", new Vector3(float.Parse(F("x")), game.HeightAt(float.Parse(F("x")), float.Parse(F("y"))), float.Parse(F("y"))), new Color(0.85f, 0.85f, 0.8f)); break;
                case "/speed": game.Speed = int.Parse(F("v")); break;
                case "/council-window": game.OpenCouncil(); break;
                case "/stop-contact": game.stopAtContact.ButtonPressed = F("v") == "1"; break;
                case "/wheel": { var ev = new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true, Position = new Vector2(float.Parse(F("x")), float.Parse(F("y"))), GlobalPosition = new Vector2(float.Parse(F("x")), float.Parse(F("y"))) }; Input.ParseInputEvent(ev); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); reply.SetResult(Write(new { distance = game.CameraDistance, width = GetViewport().GetVisibleRect().Size.X, height = GetViewport().GetVisibleRect().Size.Y })); return; }
                case "/ask": (F("yes") == "1" ? game.AskYes : game.AskNo)?.EmitSignal(BaseButton.SignalName.Pressed); break;
                case "/siege-pick": if (game.SiegeButtons.TryGetValue(F("m"), out var sb)) sb.EmitSignal(BaseButton.SignalName.Pressed); break;
                case "/camp-move": game.Message = w.Do($"camp:move:{(long)(float.Parse(F("x")) * 100)},{(long)(float.Parse(F("y")) * 100)}"); game.RefreshUi(); break;
                case "/look": game.LookAt(float.Parse(F("x")), float.Parse(F("z"))); break;
                case "/awards-ok": game.AwardsOk?.EmitSignal(BaseButton.SignalName.Pressed); break;
                // 軍議の窓の選択肢を選ぶ（確かめ用）：plan・role は並びの番号。
                case "/council-select": if (game.councilChoices is { } cc) { if (q.TryGetValue("goal", out var cg)) { cc.Goal.Select(int.Parse(cg)); cc.Goal.EmitSignal(OptionButton.SignalName.ItemSelected, long.Parse(cg)); } if (q.TryGetValue("retreat", out var cx)) cc.Retreat.Select(int.Parse(cx)); if (q.TryGetValue("plan", out var cp)) { cc.Plan.Select(int.Parse(cp)); cc.Plan.EmitSignal(OptionButton.SignalName.ItemSelected, long.Parse(cp)); } if (q.TryGetValue("role", out var cr)) { cc.Role.Select(int.Parse(cr)); cc.Role.EmitSignal(OptionButton.SignalName.ItemSelected, long.Parse(cr)); } if (q.TryGetValue("dep", out var cd)) cc.Dep.Select(int.Parse(cd)); } break;
                case "/council-place": game.PlaceCouncilPiece(q.TryGetValue("id", out var cpi) ? cpi : null, float.Parse(F("x")), float.Parse(F("y"))); break;
                case "/council-ok": game.councilChoices?.Ok.EmitSignal(BaseButton.SignalName.Pressed); break;
                case "/hover": game.Hover(q.TryGetValue("id", out var hv) ? hv : null); break;
                case "/act": game.Act(F("id")); break;
                case "/talk": game.Act("talk:" + F("id")); break;
                case "/scene-close": game.Scene.Close(); break;
                case "/scene-next": game.Scene.Next(); break;
                case "/council": game.Message = w.DecideCouncil(F("pos"), q.TryGetValue("dep", out var dp) ? ProtoWorld.Departures[int.Parse(dp)] : null, q.TryGetValue("sig", out var sg) ? ProtoWorld.Signals[int.Parse(sg)] : null, q.TryGetValue("plan", out var pl) ? ProtoWorld.Plans[int.Parse(pl)] : null); game.RefreshUi(); break;
                case "/camera": game.SetCamera(q.ContainsKey("distance") ? float.Parse(q["distance"]) : null, q.ContainsKey("yaw") ? float.Parse(q["yaw"]) : null, q.ContainsKey("pitch") ? float.Parse(q["pitch"]) : null); break;
                case "/shot": { await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); var dir = Path.Combine(Root, "絵"); Directory.CreateDirectory(dir); var file = Path.Combine(dir, (q.TryGetValue("name", out var n) ? n : DateTime.Now.ToString("yyyyMMdd-HHmmss")) + ".png"); GetViewport().GetTexture().GetImage().SavePng(file); reply.SetResult(Write(new { file })); return; }
                case "/note": reply.SetResult(Write(new { dir = SaveNote(GetViewport().GetTexture().GetImage(), Write(Known()), Write(All()), F("text")) })); return;
                case "/note-submit": if (noteText == null || noteSave == null) throw new ArgumentException("印の窓が開いていません"); noteText.Text = F("text"); noteSave(); break;
                case "/quit": reply.SetResult("{}"); GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest); GetTree().Quit(); return;
                case "/stock": reply.SetResult(Write(Stock())); return;
                case "/sent": reply.SetResult(Write(Directory.GetDirectories(Path.Combine(Root, "送った")).OrderBy(x => x).Select(x => new { bundle = x, notes = Directory.GetDirectories(x).Select(Path.GetFileName).ToArray() }).ToArray())); return;
                default: reply.SetResult(Write(new { paths = new[] { "/state", "/all", "/walk?x=&y=", "/click?x=&y=", "/press?id=", "/advance?s=[&untilArrive]", "/speed?v=", "/camera?distance=&yaw=&pitch=", "/shot?name=", "/sent" } })); return;
            }
            reply.SetResult(Write(Known()));
        }
        catch (Exception e) { reply.TrySetResult(Write(new { error = e.Message })); }
    }

    // 本人が知っていること（画面に出ているもの）。
    internal object Known()
    {
        var w = game.World; var p = w.Player;
        return new
        {
            clock = ProtoWorld.Clock(w.Second), second = w.Second, speed = game.Speed, camera = game.CameraText,
            player = new { p.Name, xM = p.X / 100.0, yM = p.Y / 100.0, ground = ProtoMap.Name(w.Map.GroundAtCm(p.X, p.Y)), site = w.SiteOf(p), p.Doing, moving = p.Moving },
            seen = w.Visible.Where(x => !x.Player).Select(x => new { x.Name, x.Role, xM = x.X / 100.0, yM = x.Y / 100.0, x.Doing }).ToArray(),
            tasks = w.Tasks.Where(t => t.State is TaskState.Open or TaskState.Offered).Select(t => new { t.Id, t.Title, state = t.State.ToString(), step = t.Current?.Text, giver = w.Person(t.Giver).Name }).ToArray(),
            status = new { rank = w.RankName, merit = w.Merit, money = w.Money, hurt = w.Second < p.HurtUntil },
            actions = w.Actions().Select(a => new { a.Id, a.Text }).ToArray(),
            carrying = w.Letters.Where(l => l.Carrier == p.Id && l.State == LetterState.Carried).Select(l => new { l.Id, to = w.Person(l.To).Name }).ToArray(),
            buttons = game.Buttons.Where(b => b.Value.IsInsideTree() && b.Value.IsVisibleInTree()).Select(b => new { id = b.Key, text = b.Value.Text, disabled = b.Value.Disabled }).ToArray(),
            message = game.Message,
            war = new { w.War.Phase, w.War.Result, assign = w.War.Assign, camp = w.CampPlan is { } cp0 ? new[] { cp0.X, cp0.Y } : null, markers = w.Markers.Select(m => $"{w.Person(m.Unit).Name} {ProtoWorld.Clock(m.SeenAt)} {m.By}").ToArray(), soldiers = p.Soldiers, morale = p.Morale },
            journal = w.Journal.TakeLast(10).Select(e => ProtoWorld.Clock(e.At) + " " + e.Text).ToArray(),
        };
    }

    // 世界の本当の全部（確かめる側だけが見る）。
    object All()
    {
        var w = game.World;
        return new
        {
            clock = ProtoWorld.Clock(w.Second),
            people = w.People.Select(x => new { x.Id, x.Name, x.Role, xM = x.X / 100.0, yM = x.Y / 100.0, site = w.SiteOf(x), x.Doing, x.Goal, moving = x.Moving }).ToArray(),
            tasks = w.Tasks.Select(t => new { t.Id, t.Title, state = t.State.ToString(), step = t.Current?.Text }).ToArray(),
            facts = w.Facts.Select(f => ProtoWorld.Clock(f.At) + " " + f.Text).ToArray(),
            letters = w.Letters.Select(l => new { l.Id, from = w.Person(l.From).Name, to = w.Person(l.To).Name, carrier = w.Person(l.Carrier).Name, state = l.State.ToString(), l.Body }).ToArray(),
        };
    }

    // ---- 印 ----
    // 一言はゲームの画面とは別の窓（メモ帳のような複数行）に書く。開いている間は時間を止める。絵は窓を出す前の画面を撮る。
    internal bool NoteOpen => noteWindow != null;
    Window? noteWindow; TextEdit? noteText; Action? noteSave;
    internal void OpenNote()
    {
        if (noteWindow != null) { noteWindow.GrabFocus(); return; }
        var image = GetViewport().GetTexture().GetImage(); var state = Write(Known()); var all = Write(All());
        var window = new Window { Visible = false, Title = "印　" + ProtoWorld.Clock(game.World.Second), Size = new Vector2I(720, 480), MinSize = new Vector2I(360, 240), ForceNative = true, Unresizable = false, WrapControls = false };
        var root = new VBoxContainer { Theme = new Theme { DefaultFontSize = 18 } }; root.SetAnchorsPreset(Control.LayoutPreset.FullRect); root.OffsetLeft = 8; root.OffsetTop = 8; root.OffsetRight = -8; root.OffsetBottom = -8; window.AddChild(root);
        var text = new TextEdit { SizeFlagsVertical = Control.SizeFlags.ExpandFill, WrapMode = TextEdit.LineWrappingMode.Boundary }; root.AddChild(text); noteText = text;
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; root.AddChild(row);
        void Done() { window.QueueFree(); noteWindow = null; noteText = null; noteSave = null; game.RefreshUi(); }
        noteSave = () => { SaveNote(image, state, all, text.Text); Done(); };
        var ok = new Button { Text = "保存" }; ok.Pressed += () => noteSave?.Invoke(); row.AddChild(ok);
        var cancel = new Button { Text = "取消" }; cancel.Pressed += Done; row.AddChild(cancel);
        window.CloseRequested += Done;
        noteWindow = window; AddChild(window); window.PopupCentered(); text.GrabFocus();
    }

    internal string SaveNote(Image image, string state, string all, string note)
    {
        var dir = Path.Combine(Root, "ためておく", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")); Directory.CreateDirectory(dir);
        image.SavePng(Path.Combine(dir, "画面.png")); File.WriteAllText(Path.Combine(dir, "本人が知っていること.json"), state); File.WriteAllText(Path.Combine(dir, "世界の全部.json"), all); File.WriteAllText(Path.Combine(dir, "一言.txt"), note);
        game.Message = "印を残した"; return dir;
    }

    void Close(ref Window? window) { window?.QueueFree(); window = null; game.RefreshUi(); }

    internal string[] Stock() => Directory.GetDirectories(Path.Combine(Root, "ためておく")).OrderBy(x => x).ToArray();

    internal void OpenStock()
    {
        if (stockWindow != null) return;
        stockWindow = new Window { Visible = false, Title = "印一覧", Size = new Vector2I(720, 480), ForceNative = true };
        var box = new VBoxContainer(); box.SetAnchorsPreset(Control.LayoutPreset.FullRect); box.OffsetLeft = 10; box.OffsetRight = -10; box.OffsetTop = 10; box.OffsetBottom = -10; stockWindow.AddChild(box);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; box.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(list);
        var checks = new List<(CheckBox Box, string Dir)>();
        foreach (var dir in Stock())
        {
            var note = File.Exists(Path.Combine(dir, "一言.txt")) ? File.ReadAllText(Path.Combine(dir, "一言.txt")) : "";
            var c = new CheckBox { Text = Path.GetFileName(dir) + "　" + note, ButtonPressed = true }; list.AddChild(c); checks.Add((c, dir));
        }
        var row = new HBoxContainer(); box.AddChild(row);
        var send = new Button { Text = "送信" }; row.AddChild(send);
        send.Pressed += () =>
        {
            var chosen = checks.Where(x => x.Box.ButtonPressed).Select(x => x.Dir).ToArray(); if (chosen.Length == 0) return;
            var bundle = Path.Combine(Root, "送った", DateTime.Now.ToString("yyyyMMdd-HHmmss")); Directory.CreateDirectory(bundle);
            foreach (var d in chosen) Directory.Move(d, Path.Combine(bundle, Path.GetFileName(d)));
            game.Message = chosen.Length + "件を送った"; Close(ref stockWindow);
        };
        var remove = new Button { Text = "削除" }; row.AddChild(remove);
        remove.Pressed += () => { foreach (var x in checks.Where(x => x.Box.ButtonPressed)) Directory.Delete(x.Dir, true); Close(ref stockWindow); };
        var cancel = new Button { Text = "閉" }; row.AddChild(cancel); cancel.Pressed += () => Close(ref stockWindow);
        stockWindow.CloseRequested += () => Close(ref stockWindow);
        AddChild(stockWindow); stockWindow.PopupCentered();
    }
}
