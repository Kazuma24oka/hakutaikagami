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
    // 合戦を見る（Watch）ときは別の保存を使い、ふだんの保存に触らない。
    internal static string SavePath => System.Environment.GetEnvironmentVariable("HAKUTAI_PROTO_SAVE") is { Length: > 0 } custom ? custom : Watch ? "user://proto-watch-save.json" : "user://proto-save.json";
    // 合戦を見る（2026-11-19 ユーザー「合戦の所を出せ。最初からやるのはだるい。指示もしたくない。問題ある所だけコメントしたい」）：
    // 釣り野伏の合戦の手前から始まり、動画と同じ台本で勝手に進む。寄り引き・地図の移動・印を残すはふだんどおり使える。
    internal static bool Watch;
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
    internal static readonly int[] Speeds = [10, 30, 60, 120, 500];
    // 時間（2026-11-19 ユーザー「HoI4 のように。ボタンを増やさない」）：止めているか、進めているか。進めている間は、歩いていなくても時が流れる。
    internal bool Paused = true;
    double edgeDwell;
    HSlider speedSlider = null!; Label speedText = null!;
    internal CheckBox stopAtContact = null!; readonly HashSet<string> closingSeen = new();
    // ぶつかる手前（150m）に新しく入った組があるか。離れた（300m）組は、次に近づいたときまた止める。
    bool NewContact()
    {
        closingSeen.IntersectWith(World.Closing(300_00, true));
        bool fresh = false; foreach (var k in World.Closing(150_00)) fresh |= closingSeen.Add(k);
        return fresh;
    }
    long skipUntil; int skipJournal;
    internal void Skip(long until) { World.Stop(); marker.Visible = false; skipUntil = until; skipJournal = World.Journal.Count; Message = ""; }
    internal readonly Dictionary<string, Button> Buttons = new();
    internal string Message = "";
    string messageShown = ""; double messageAt; // 操作の返事は8秒で消す
    double acc, uiTimer;
    bool dirty; long savedBucket = -1;
    float distance = 500, yaw = 0, pitch = 55;
    Vector3 pan;
    readonly Dictionary<string, (MeshInstance3D Body, Label3D Label)> actors = new();
    MeshInstance3D pathLine = null!, marker = null!;
    ImmediateMesh pathMesh = null!;
    Label clock = null!, foot = null!, journal = null!, message = null!;
    PanelContainer logPanel = null!; ScrollContainer sideScroll = null!; PanelContainer sidePanel = null!; bool? panelOpen; string journalKey = ""; double journalShown;
    static readonly string[] LogTabs = ["全部", "戦闘", "会話"];
    string logTab = "全部"; bool logMin; VBoxContainer logBody = null!;
    readonly List<ProtoEvent> damageLog = new(); long damageSeen = -1;
    static readonly string[] BattleWords = ["崩れ", "突いた", "戦法", "奇襲", "押さえ", "立て直", "討ち取", "壊滅", "総崩れ", "攻めかか", "狼煙", "深追い", "仕掛け", "手前で止ま", "鎮ま", "退けられ", "兵を引いた"];
    // 戦闘：合戦の出来事と、1分ごとの打撃。会話：台詞（「」のある行）。全部：記録のすべて。
    IEnumerable<ProtoEvent> LogLines()
    {
        var hits = World.War.Hits;
        if (hits.Count > 0 && hits[0].At != damageSeen)
        {
            damageSeen = hits[0].At;
            foreach (var h in hits.Where(h => h.Loss > 0 && World.Sees(World.Player, World.Person(h.To))))
                damageLog.Add(new ProtoEvent(h.At, $"{World.Person(h.To).Name} -{h.Loss}（{(h.Surprise ? "奇襲・" : "")}{h.Kind}）"));
            if (damageLog.Count > 200) damageLog.RemoveRange(0, damageLog.Count - 200);
        }
        return logTab switch
        {
            "戦闘" => World.Journal.Where(e => BattleWords.Any(e.Text.Contains)).Concat(damageLog).OrderBy(e => e.At),
            "会話" => World.Journal.Where(e => e.Text.Contains('「')),
            _ => World.Journal,
        };
    }
    VBoxContainer panel = null!;
    Label status = null!;
    readonly Dictionary<string, Button> taskButtons = new();
    internal ProtoDebug Debug = null!;
    internal ProtoScene Scene = null!;

    static readonly Color Takano = new(0.55f, 0.45f, 0.85f), Sawatari = new(0.88f, 0.42f, 0.28f), Neutral = new(0.85f, 0.85f, 0.8f);
    static Color HouseColor(string house) => house == "takano" ? Takano : house == "sawatari" ? Sawatari : house == "iwakiri" ? new Color(0.62f, 0.12f, 0.12f) : Neutral;

    public override void _Ready()
    {
        GetTree().AutoAcceptQuit = true; // スタート画面は閉じる前に保存を待つため×を止めている。試作版では×でそのまま閉じる。
        if (OS.GetCmdlineUserArgs().Contains("--proto-watch")) Watch = true;
        if (Watch) Fresh = true;
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
        BuildHud(); BuildBattleView();
        Debug = new ProtoDebug(this); AddChild(Debug);
        Scene = new ProtoScene(this); AddChild(Scene);
        RefreshUi(); PlaceCamera();
        if (FilmRoot != null) RunFilm();
        else if (Watch) RunWatch();
    }

    static StandardMaterial3D Unshaded(Color c) => new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = c, Transparency = c.A < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled, NoDepthTest = false };

    internal float HeightAt(float x, float z) => World.Map.HeightAtCm((long)(x * 100), (long)(z * 100)) / 100f;
    Vector3 ToScene(long xCm, long yCm) => new(xCm / 100f, World.Map.HeightAtCm(xCm, yCm) / 100f, yCm / 100f);

    static Color GroundColor(Ground g, float h, int c, int r)
    {
        // マスごとのまだらは升目に見えるので、ゆるやかな濃淡にする（2026-11-19「ごちゃごちゃ」の案4）。
        float n = (MathF.Sin(c * 0.11f) * MathF.Cos(r * 0.09f) + MathF.Sin((c + r) * 0.05f)) * 0.012f;
        Color k = g switch
        {
            Ground.Road => new Color(0.66f, 0.60f, 0.48f),
            Ground.Bridge => new Color(0.48f, 0.33f, 0.20f),
            Ground.Site => new Color(0.84f, 0.80f, 0.68f),
            Ground.Forest => new Color(0.22f, 0.34f, 0.23f),
            Ground.Mountain => new Color(0.44f, 0.46f, 0.36f).Lerp(new Color(0.58f, 0.56f, 0.50f), Math.Clamp(h / 140f, 0, 1)),
            Ground.Water => new Color(0.28f, 0.44f, 0.62f),
            Ground.Ford => new Color(0.42f, 0.56f, 0.68f),
            _ => new Color(0.45f, 0.52f, 0.37f),
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

    // ちびコマ（甲冑アニメ工房のドット絵）：役と年で絵を選び、合戦の間は家の色の甲冑を着る（鷹野は紫、沢渡は朱、謀叛の岩切は黒漆）。
    internal static string SpriteKey(ProtoWorld w, ProtoPerson p)
    {
        string kind = p.Role switch { "当主" or "嫡男" => "lord", "住職" => "monk", "庄屋" or "商人" or "賊" => "ronin", _ => "samurai" };
        var data = w.Map.Data.People.FirstOrDefault(d => d.Id == p.Id); int age = data?.Age ?? 30;
        string band = age < 30 ? "young" : age < 50 ? "prime" : "old"; int variants = band == "young" ? 3 : band == "prime" ? 4 : 2;
        int n = Math.Abs(p.Id.Aggregate(17, (h, c) => unchecked(h * 31 + c))) % variants;
        bool armored = p.Soldiers > 0 && w.War.Phase is "行軍" or "合戦" || p.House == "bandit";
        string color = p.House switch { "takano" => "紫", "sawatari" => "朱", "iwakiri" => "黒漆", _ => "鉄錆" };
        return armored ? $"generic/{kind}/{band}/{n}/war/{color}" : $"generic/{kind}/{band}/{n}/peace";
    }
    readonly Dictionary<string, DotOfficerActor> dots = new();
    // 部隊の足軽：兵10人につき1つ（最大15）、大将の後ろに隊列を組んでついてくる。兵種の並び（今は槍・鉄砲・弓）は TroopMix で決める。
    // 騎馬などの兵種が増えたら、TroopMix と足軽の絵の名前（soldier/ashigaru/〈兵種〉/war/〈色〉）を足す。
    readonly Dictionary<string, List<DotOfficerActor>> followers = new();
    readonly Dictionary<string, Vector3> heading = new();
    internal static string[] TroopMix(ProtoPerson p) => p.Skills.Contains("槍衾") ? ["yari", "yari", "yari", "yari", "teppo"] : ["yari", "yari", "teppo", "yari", "yumi"];
    static string ColorOf(string house) => house switch { "takano" => "紫", "sawatari" => "朱", "iwakiri" => "黒漆", _ => "鉄錆" };
    // 同じ場所に止まっている同じ家の隊は陣形に並べる（2026-11-19 ユーザー「大将とその兵士、後ろに横並びで武将達と兵士、向きは大将に合わせる」）。
    // 大将（当主・嫡男・城主・家老の順、次に統率）とその足軽が前、ほかの武将は後ろの列に横並びで、それぞれの足軽を後ろに連れる。描くときだけの並びで、世界の位置は変えない。
    readonly Dictionary<string, (Vector3 Foot, Vector3 Dir, int Count, int Total, string Parts)> formation = new();
    static int RankOf(ProtoPerson p) => p.Role switch { "当主" => 0, "嫡男" => 1, "城主" => 2, "家老" => 3, _ => 4 };
    // 足軽の並び：歩くときは横3列の縦隊、止まっているときは横5列の横隊。
    static int Wide(ProtoPerson p) => p.Moving ? 3 : 5;
    static int Rows(ProtoPerson p) => (Math.Clamp((p.Soldiers + 9) / 10, 0, 15) + Wide(p) - 1) / Wide(p);
    const float FollowerSize = 0.5f, FollowerGap = 0.42f;
    void PlanFormations(float px)
    {
        formation.Clear();
        var units = World.People.Where(p => p.Active && p.Soldiers > 0 && !p.Moving && World.ClashOf(p.Id) == null && World.Sees(World.Player, p)).ToList();
        var placed = new HashSet<string>(); float gap = 26 * px * FollowerGap;
        foreach (var lead in units.OrderBy(RankOf).ThenByDescending(p => p.Command).ThenBy(p => p.Id))
        {
            if (placed.Contains(lead.Id)) continue;
            var rest = units.Where(p => p != lead && !placed.Contains(p.Id) && p.House == lead.House && Math.Abs(p.X - lead.X) + Math.Abs(p.Y - lead.Y) < 9000)
                .OrderBy(RankOf).ThenByDescending(p => p.Command).ThenBy(p => p.Id).ToList();
            placed.Add(lead.Id);
            // 向きは、見えているいちばん近い他家の隊の方。見えなければ最後に進んだ向き。
            var foe = World.People.Where(e => e.Active && e.Soldiers > 0 && e.House != lead.House && World.Sees(World.Player, e)).OrderBy(e => Math.Abs(e.X - lead.X) + Math.Abs(e.Y - lead.Y)).FirstOrDefault();
            var dir = foe != null && (foe.X != lead.X || foe.Y != lead.Y) ? new Vector3(foe.X - lead.X, 0, foe.Y - lead.Y).Normalized() : heading.TryGetValue(lead.Id, out var h) ? h : new Vector3(0, 0, -1);
            if (rest.Count == 0) { formation[lead.Id] = (ToScene(lead.X, lead.Y), dir, 1, lead.Soldiers, ""); continue; }
            var back = -dir; var side = new Vector3(-back.Z, 0, back.X);
            var front = ToScene(lead.X, lead.Y); int total = lead.Soldiers + rest.Sum(p => p.Soldiers);
            formation[lead.Id] = (front, dir, rest.Count + 1, total, string.Join("+", new[] { lead }.Concat(rest).Select(u => u.Soldiers)));
            float behind = gap * (Rows(lead) + 2.2f);
            for (int i = 0; i < rest.Count; i++)
            {
                var at = front + back * behind + side * (gap * 6.2f * (i - (rest.Count - 1) / 2f));
                at.Y = HeightAt(at.X, at.Z); formation[rest[i].Id] = (at, dir, 0, 0, ""); placed.Add(rest[i].Id);
            }
        }
    }
    // ぶつかり合いは「前線」で見せる（2026-11-19 案3）：二つの隊を間の線から後ろへ離して向かい合わせ、足軽は線の所で打ち合う。
    // 描くときだけの並びで、世界の位置は変えない。M：間の線の真ん中、D：鷹野方から岩切方への向き。
    // 同じ相手とぶつかっている味方は、線の手前に横に並べる。押し合いのゲージも相手ごとに一つ（押し合いは平均）。
    // キーは岩切方の隊。M：線の真ん中、D：鷹野方から岩切方への向き、As：その相手とぶつかっている鷹野方。
    internal readonly Dictionary<string, (Vector3 M, Vector3 D, string[] As, double Push)> clashLine = new();
    void PlanClashLines(float px)
    {
        clashLine.Clear(); float gap = 26 * px * FollowerGap;
        foreach (var g in World.War.Clashes.GroupBy(c => c.B))
        {
            var b = World.Person(g.Key); var As = g.Select(c => c.A).OrderBy(x => x).ToArray();
            var pb = ToScene(b.X, b.Y); var pa = As.Select(x => ToScene(World.Person(x).X, World.Person(x).Y)).Aggregate(Vector3.Zero, (s, v) => s + v) / As.Length;
            var d = pb - pa; d.Y = 0;
            if (d.LengthSquared() < 1) d = heading.TryGetValue(As[0], out var h) ? h : new Vector3(1, 0, 0);
            d = d.Normalized(); double push = 0;
            var m = (pa + pb) / 2 + d * (float)(push / 100.0) * gap * 1.5f; // 押している側へ線が動く
            clashLine[g.Key] = (m, d, As, push);
        }
    }
    void DrawFollowers(ProtoPerson p, Vector3 foot, float px, bool show, string anim)
    {
        if (!followers.TryGetValue(p.Id, out var list)) followers[p.Id] = list = new();
        int want = show ? Math.Clamp((p.Soldiers + 9) / 10, 0, 15) : 0;
        var mix = TroopMix(p);
        while (list.Count < want) { var d = new DotOfficerActor { OnTop = true }; AddChild(d); list.Add(d); }
        // 進む向きの反対側（後ろ）に、横3列で並べる。止まっているときは最後に進んだ向きのまま。
        var dir = heading.TryGetValue(p.Id, out var h) ? h : new Vector3(0, 0, -1);
        // 足軽は大将の後ろに並ぶ（2026-11-19 ユーザーの図「大将どうしが向かい合い、兵は後ろ」）。ぶつかっている間は打つ動き。
        bool front = World.ClashOf(p.Id) != null; if (front) anim = "attack";
        var back = -dir; var side = new Vector3(-back.Z, 0, back.X);
        float gap = 26 * px * FollowerGap; int wide = front ? 5 : Wide(p);
        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i]; d.Visible = i < want; if (i >= want) continue;
            d.Apply($"soldier/ashigaru/{mix[i % mix.Length]}/war/{ColorOf(p.House)}");
            int row = i / wide + 1; float col = i % wide - (wide - 1) / 2f;
            var at = foot + back * (gap * row) + side * (gap * col * 1.1f);
            at.Y = HeightAt(at.X, at.Z);
            d.PixelSize = px * FollowerSize; d.Position = at; d.Freeze(anim, Time.GetTicksMsec() / 1000.0 + i * 0.21);
            d.Face(dir, Camera);
        }
    }
    readonly Dictionary<string, Vector3> lastPos = new();

    void BuildActors()
    {
        foreach (var p in World.People)
        {
            float r = p.Player ? 7 : 5;
            var body = new MeshInstance3D { Mesh = new SphereMesh { Radius = r, Height = r * 2 }, MaterialOverride = new StandardMaterial3D { AlbedoColor = p.Player ? new Color(1, 1, 1) : HouseColor(p.House), EmissionEnabled = p.Player, Emission = new Color(0.4f, 0.4f, 0.2f) } };
            var label = new Label3D { Text = p.Name, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = p.Player ? 0.0011f : 0.0008f, FontSize = 30, OutlineSize = 10, NoDepthTest = true, Modulate = p.Player ? new Color(1, 1, 0.6f) : new Color(1, 1, 1) };
            AddChild(body); AddChild(label); actors[p.Id] = (body, label);
            if (DotOfficerActor.Available) { var dot = new DotOfficerActor { OnTop = true }; AddChild(dot); dots[p.Id] = dot; }
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
        // 止める・進める（スペースでも）と、速さの目盛り。
        Add(bar, "pause", "進める", () => { Paused = !Paused; RefreshUi(); });
        speedSlider = new HSlider { MinValue = 0, MaxValue = Speeds.Length - 1, Step = 1, CustomMinimumSize = new Vector2(160, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, FocusMode = Control.FocusModeEnum.None };
        speedSlider.ValueChanged += v => { Speed = Speeds[(int)v]; speedText.Text = Speed + "倍"; };
        bar.AddChild(speedSlider); speedText = new Label { CustomMinimumSize = new Vector2(60, 0) }; bar.AddChild(speedText);
        // 接敵で止める（2026-11-20 ユーザーの印）：入れていると、隊どうしがぶつかる手前で時を止める。入れていなければ止めない（速さも変えない）。
        stopAtContact = new CheckBox { Text = "接敵で止める", FocusMode = Control.FocusModeEnum.None }; bar.AddChild(stopAtContact);
        bar.AddChild(new VSeparator());
        Add(bar, "note", "印を残す", () => Debug.OpenNote());
        Add(bar, "stock", "ためた印", () => Debug.OpenStock());

        Add(bar, "panel", "任務", () => panelOpen = !(panelOpen ?? World.War.Phase != "合戦"));
        var side = sidePanel = new PanelContainer { CustomMinimumSize = new Vector2(440, 0), GrowHorizontal = Control.GrowDirection.Begin }; side.SetAnchorsPreset(Control.LayoutPreset.TopRight); side.OffsetLeft = -450; side.OffsetTop = 60; side.OffsetBottom = 60; side.OffsetRight = -10; root.AddChild(side);
        // 中身が長いときは窓の中で送る（画面の下にはみ出さない）。
        sideScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; side.AddChild(sideScroll);
        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; sideScroll.AddChild(col);
        panel = col;

        var bottom = logPanel = new PanelContainer { GrowVertical = Control.GrowDirection.Begin }; bottom.SetAnchorsPreset(Control.LayoutPreset.BottomLeft); bottom.OffsetTop = -10; bottom.OffsetBottom = -10; bottom.OffsetLeft = 10; bottom.OffsetRight = 700; root.AddChild(bottom);
        var log = new VBoxContainer(); bottom.AddChild(log);
        // 記録（2026-11-19 ユーザー「最小化と、種類でタブを分けたい。戦闘のダメージ、会話だけ、全部」）。
        var tabs = new HBoxContainer(); tabs.AddThemeConstantOverride("separation", 4); tabs.Theme = new Theme { DefaultFontSize = 15 }; log.AddChild(tabs);
        foreach (var t in LogTabs) { var tab = t; Add(tabs, "log-" + tab, tab, () => { logTab = tab; journalKey = ""; RefreshUi(); }); }
        tabs.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        Add(tabs, "log-min", "－", () => { logMin = !logMin; RefreshUi(); });
        logBody = new VBoxContainer(); log.AddChild(logBody);
        message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 0.95f, 0.7f) }; logBody.AddChild(message);
        journal = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; logBody.AddChild(journal);
    }

    internal Button Add(Container parent, string id, string text, Action action)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None }; parent.AddChild(b); Buttons[id] = b;
        b.Pressed += () => { try { action(); } catch (Exception e) { Message = e.Message; } RefreshUi(); };
        return b;
    }

    string panelKey = ""; string? missionTab;
    // 軍令の行にマウスを乗せると、その隊と行き先を地図で光らせる。
    string? hoverUnit; Vector3 hoverSpot; MeshInstance3D hoverRingUnit = null!, hoverRingSpot = null!;
    internal void Hover(string? id) { hoverUnit = id; if (id != null && World.Orders().FirstOrDefault(o => o.Unit == id) is { } o) hoverSpot = ToScene(o.X, o.Y); }
    void UpdateHover()
    {
        if (hoverRingUnit == null)
        {
            MeshInstance3D Ring() { var m = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.86f, OuterRadius = 1f }, MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1, 0.9f, 0.3f), NoDepthTest = true, RenderPriority = 30 }, Visible = false }; AddChild(m); return m; }
            hoverRingUnit = Ring(); hoverRingSpot = Ring();
        }
        // 任務の窓は、右下の合戦の窓の上までにする（重ならない。長い分は窓の中で送る。2026-11-20 ユーザーの印）。
        float room = GetViewport().GetVisibleRect().Size.Y - 80 - (commandBox.Visible ? commandBox.Size.Y + 16 : 0);
        sideScroll.CustomMinimumSize = new Vector2(440, Math.Min(panel.GetCombinedMinimumSize().Y, Math.Max(140, room)));
        bool on = hoverUnit != null && sidePanel.Visible; hoverRingUnit.Visible = hoverRingSpot.Visible = on; if (!on) return;
        var u = World.Person(hoverUnit!); float pulse = 1 + 0.12f * MathF.Sin(Time.GetTicksMsec() / 150f), r = Math.Max(45, distance * 0.05f) * pulse;
        hoverRingUnit.Position = ToScene(u.X, u.Y) + new Vector3(0, 3, 0); hoverRingUnit.Scale = new Vector3(r, 1, r);
        hoverRingSpot.Position = hoverSpot + new Vector3(0, 3, 0); hoverRingSpot.Scale = new Vector3(r * 0.7f, 1, r * 0.7f);
    }
    internal void RefreshUi()
    {
        var p = World.Player; var site = World.SiteOf(p);
        clock.Text = ProtoWorld.Clock(World.Second);
        foot.Text = ProtoMap.Name(World.Map.GroundAtCm(p.X, p.Y)) + (site != null ? "（" + World.SiteName(site) + "）" : "");
        status.Text = $"{World.RankName}　勲功{World.Merit}　銭{World.Money}文" + (p.Soldiers > 0 ? $"　兵{p.Soldiers}・士気{p.Morale}" : "") + (World.Second < p.HurtUntil ? "　手傷" : "");
        Buttons["pause"].Text = Paused ? "進める" : "止める";
        int si = Math.Max(0, Array.IndexOf(Speeds, Speed)); if ((int)speedSlider.Value != si) speedSlider.SetValueNoSignal(si); speedText.Text = Speed + "倍";
        int stocked = Debug.Stock().Length; Buttons["stock"].Text = "ためた印（" + stocked + "）"; Buttons["stock"].Disabled = stocked == 0;
        // 中身が変わったときだけ作り直す（押している最中にボタンが消えないように）。
        var actions = World.Actions().ToArray(); var open = World.OpenTasks.ToArray(); var near = World.Nearby.ToArray();
        // 「任務」の窓（2026-11-19 ユーザー）：軍令（軍議で決まった各隊の役目と行き先、合戦の指図）と依頼（頼まれごと・話す）をタブで切り替える。
        var orders = World.Orders().ToArray(); bool inWar = (World.War.Phase is "行軍" or "合戦" || World.Returning) && orders.Length > 0; // 決着のあとも、論功行賞が済むまで作戦の成否を見られる
        string tab = !inWar ? "依頼" : missionTab ?? "軍令";
        static bool IsWarAction(string id) => id.StartsWith("attack:") || id.StartsWith("withdraw:") || id.StartsWith("order:") || id.StartsWith("council:");
        var key = tab + inWar + "#" + string.Join("|", actions.Select(a => a.Id + a.Text)) + "#" + string.Join("|", open.Select(t => t.Id + t.Step)) + "#" + string.Join("|", near.Select(n => n.Id)) + "#" + string.Join("|", orders.Select(o => o.Unit + o.Role + o.Text)) + World.War.PlayerFollows + World.PlanState + string.Join("", World.PlanSteps().Select(x => x.State)) + World.War.Phase + string.Join("", World.RetreatRules().Select(x => x.Id + x.Met)) + World.Retreated("iwakiri");
        if (key != panelKey)
        {
            panelKey = key; hoverUnit = null;
            foreach (var c in panel.GetChildren()) { panel.RemoveChild(c); c.QueueFree(); }
            foreach (var k in Buttons.Keys.Where(k => k.StartsWith("task:") || k.StartsWith("talk:") || k.StartsWith("act:") || k.StartsWith("mission-")).ToArray()) Buttons.Remove(k);
            var tabs = new HBoxContainer(); panel.AddChild(tabs);
            foreach (var t in new[] { "軍令", "依頼" }) { var name = t; var b = Add(tabs, "mission-" + name, name, () => { missionTab = name; RefreshUi(); }); b.Disabled = name == tab || name == "軍令" && !inWar; }
            if (tab == "軍令")
            {
                // 作戦の名と成否、段取り（済んだ段は成功・失敗、今の段は白、先の段は薄く）。
                Color good = new(0.6f, 0.9f, 0.6f), bad = new(1, 0.5f, 0.45f), dim = new(0.62f, 0.62f, 0.62f);
                // 委任／手動（今の方が押し込まれている）。合戦の窓が出ていても押せるように、いちばん上に置く。
                if (actions.Any(a => a.Id.StartsWith("order:")))
                {
                    var mode = new HBoxContainer(); panel.AddChild(mode);
                    foreach (var (id, text, on) in new[] { ("order:follow", "委任", World.War.PlayerFollows), ("order:free", "手動", !World.War.PlayerFollows) })
                    { var aid = id; var b = Add(mode, "act:" + aid, (on ? "● " : "○ ") + text, () => { Act(aid); RefreshUi(); }); if (on) b.Modulate = new Color(1, 0.88f, 0.45f); b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; }
                }
                string state = World.PlanState;
                panel.AddChild(new Label { Text = "大目標　" + World.GoalOf("takano"), Modulate = new Color(1, 0.9f, 0.6f) });
                panel.AddChild(new Label { Text = World.War.Plan + (state != "" ? "　" + state : ""), Modulate = state == "失敗" ? bad : state == "成功" ? good : new Color(1, 0.9f, 0.6f) });
                var steps = World.PlanSteps().ToArray(); int now = state == "" ? Array.FindIndex(steps, x => x.State == "") : -1;
                for (int i = 0; i < steps.Length; i++)
                    panel.AddChild(new Label { Text = "一二三四五六七八九"[i] + "　" + steps[i].Text + (steps[i].State != "" ? "　" + steps[i].State : ""), AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = steps[i].State == "成功" ? good : steps[i].State == "失敗" ? bad : i == now ? Colors.White : dim });
                panel.AddChild(new HSeparator());
                foreach (var o in orders)
                {
                    bool mine = o.Unit == World.Player.Id;
                    // 自分の隊だけ指図の文も出す（ほかの隊は名前と役目。乗せると行き先が光る）。
                    var row = new Label { Text = o.Name + "　" + o.Role + (mine ? (char)10 + "　" + o.Text : ""), AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Stop, Modulate = mine ? new Color(1, 0.88f, 0.45f) : Colors.White };
                    var ord = o; row.MouseEntered += () => { hoverUnit = ord.Unit; hoverSpot = ToScene(ord.X, ord.Y); }; row.MouseExited += () => { if (hoverUnit == ord.Unit) hoverUnit = null; };
                    panel.AddChild(row);
                }
                panel.AddChild(new HSeparator());
                foreach (var a in actions.Where(a => IsWarAction(a.Id) && !a.Id.StartsWith("order:"))) { var aid = a.Id; Add(panel, "act:" + aid, a.Text, () => Act(aid)); }
                // 撤退の条件（足した分は金色、満たして退き貝が鳴った条件は赤）。
                var rules = World.RetreatRules().ToArray(); bool gone = World.Retreated("takano");
                if (rules.Length > 0)
                {
                    panel.AddChild(new HSeparator());
                    panel.AddChild(new Label { Text = gone ? "撤退" : "撤退の条件", Modulate = gone ? bad : new Color(1, 0.9f, 0.6f) });
                    foreach (var r in rules) panel.AddChild(new Label { Text = "・" + r.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = r.Met ? bad : r.Added ? new Color(1, 0.88f, 0.45f) : gone ? dim : Colors.White });
                }
            }
            else
            {
                foreach (var o in World.OffersHere) panel.AddChild(new Label { Text = $"{World.Person(o.Giver).Name}「{o.Speech}」", AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(0.85f, 0.85f, 0.75f) });
                foreach (var a in actions.Where(a => !inWar || !IsWarAction(a.Id))) { var aid = a.Id; Add(panel, "act:" + aid, a.Text, () => Act(aid)); }
                foreach (var t in open)
                {
                    panel.AddChild(new Label { Text = t.Title + "　" + (t.Current?.Text ?? ""), AutowrapMode = TextServer.AutowrapMode.WordSmart });
                    if (t.Speech != "") panel.AddChild(new Label { Text = $"{World.Person(t.Giver).Name}「{t.Speech}」", AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(0.85f, 0.85f, 0.75f) });
                    var id = t.Id; var b = Add(panel, "task:" + id, "", () => { if (World.GoToTask(id)) ShowGoal(); }); taskButtons[id] = b;
                }
                if (near.Length > 0)
                {
                    panel.AddChild(new HSeparator());
                    foreach (var n in near) { var nid = n.Id; Add(panel, "talk:" + nid, "話す：" + n.Name, () => Say(World.Do("talk:" + nid), nid)); }
                }
            }
        }
        foreach (var t in open)
        {
            if (!taskButtons.TryGetValue(t.Id, out var b) || !IsInstanceValid(b)) continue;
            var eta = World.TaskEta(t);
            b.Text = World.TaskPlace(t) + "へ　" + (eta == null ? "道がない" : eta <= 5 ? "ここ" : "着くのは" + HourMinute(World.Second + eta.Value) + "ごろ");
        }
        double nowSec = Time.GetTicksMsec() / 1000.0;
        if (Message != messageShown) { messageShown = Message; messageAt = nowSec; }
        if (Message != "" && nowSec - messageAt > 8) { Message = ""; messageShown = ""; }
        message.Text = Message; message.Visible = Message != "";
        var lines = string.Join((char)10, LogLines().TakeLast(logTab == "全部" ? 3 : 6).Select(e => HourMinute(e.At) + "　" + e.Text));
        logBody.Visible = !logMin; Buttons["log-min"].Text = logMin ? "＋" : "－";
        foreach (var t in LogTabs) Buttons["log-" + t].Disabled = logTab == t;
        if (lines != journalKey) { journalKey = lines; journalShown = Time.GetTicksMsec() / 1000.0; }
        journal.Text = lines;
        sidePanel.Visible = panelOpen ?? World.War.Phase != "合戦";
    }
    static string HourMinute(long s) => $"{s % ProtoWorld.Day / 3600}時{s % 3600 / 60:00}分";

    void ShowGoal()
    {
        var p = World.Player; if (!p.Moving) { marker.Visible = false; return; }
        var (x, y) = p.Path[^1]; marker.Position = ToScene(x, y) + new Vector3(0, 0.6f, 0); marker.Visible = true;
    }

    Vector3? PickGround(Vector2 screen)
    {
        var from = Camera.ProjectRayOrigin(screen); var dir = Camera.ProjectRayNormal(screen);
        float t = 0, step = 4;
        for (int i = 0; i < 8000; i++)
        {
            var q = from + dir * t;
            if (q.X >= 0 && q.Z >= 0 && q.X < World.Map.Data.WidthM && q.Z < World.Map.Data.HeightM && q.Y <= HeightAt(q.X, q.Z)) return q;
            t += step;
        }
        return null;
    }
    internal bool ClickGround(Vector2 screen)
    {
        if (PickGround(screen) is not { } q) return false;
        // 委任している間は、地図を押しても動かさない。委任を解くかを聞く（2026-11-20 ユーザー）。
        if (World.War.PlayerFollows && (World.War.Phase is "行軍" or "合戦" || World.Returning)) { AskRelease(q.X, q.Z); return false; }
        return WalkTo(q.X, q.Z);
    }
    // 「委任を解きますか」：はい＝手動にして、押した所へ向かう。いいえ＝そのまま。開いている間は時を止める。
    CanvasLayer? askLayer; internal Button? AskYes, AskNo; internal float CameraDistance => distance;
    void AskRelease(float x, float z)
    {
        if (askLayer != null) return;
        var layer = askLayer = new CanvasLayer { Layer = 55 }; AddChild(layer);
        var back = new ColorRect { Color = new Color(0, 0, 0, 0.3f) }; back.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(back);
        var center = new CenterContainer { Theme = new Theme { DefaultFontSize = 22 } }; center.SetAnchorsPreset(Control.LayoutPreset.FullRect); layer.AddChild(center);
        var frame = new PanelContainer(); center.AddChild(frame);
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.09f, 0.09f, 0.11f), BorderColor = new Color(0.75f, 0.6f, 0.3f), BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2, ContentMarginLeft = 30, ContentMarginRight = 30, ContentMarginTop = 20, ContentMarginBottom = 20, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 16); frame.AddChild(col);
        col.AddChild(new Label { Text = "委任を解きますか", HorizontalAlignment = HorizontalAlignment.Center });
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; row.AddThemeConstantOverride("separation", 16); col.AddChild(row);
        void Done(bool yes)
        {
            askLayer?.QueueFree(); askLayer = null; AskYes = AskNo = null;
            if (yes) { World.ActOnOwn(); WalkTo(x, z); }
            RefreshUi();
        }
        AskYes = new Button { Text = "はい", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(120, 0) }; AskYes.Pressed += () => Done(true); row.AddChild(AskYes);
        AskNo = new Button { Text = "いいえ", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(120, 0) }; AskNo.Pressed += () => Done(false); row.AddChild(AskNo);
    }
    // 窓（任務・合戦・記録・上の帯など）の上にマウスがあるか。あれば、地図は押さない・カメラも寄せ引きしない。
    bool OverWindow(Vector2 at)
    {
        if (GetViewport().GuiGetHoveredControl() != null) return true;
        foreach (var c in new Control[] { sidePanel, commandBox, logPanel, spiritBox }) if (c.IsVisibleInTree() && c.GetGlobalRect().HasPoint(at)) return true;
        return false;
    }
    internal bool WalkTo(float x, float z)
    {
        if (!World.MoveTo((long)(x * 100), (long)(z * 100))) { Message = "そこへは行けない"; RefreshUi(); return false; }
        Message = ""; ShowGoal(); RefreshUi(); return true;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (FilmRoot != null) return; // 動画を撮っている間は、マウスやキーで画面が動かないようにする
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            if (askLayer != null || OverWindow(mb.Position)) return;
            if (mb.ButtonIndex == MouseButton.Left) ClickGround(mb.Position);
            else if (mb.ButtonIndex == MouseButton.WheelUp) distance = Math.Max(120, distance * 0.85f);
            else if (mb.ButtonIndex == MouseButton.WheelDown) distance = Math.Min(4000, distance / 0.85f);
        }
        else if (e is InputEventMouseMotion mm && (mm.ButtonMask & MouseButtonMask.Middle) != 0)
        {
            // 中ボタンでつかんで動かす
            pan -= Basis.FromEuler(new Vector3(0, Mathf.DegToRad(yaw), 0)) * new Vector3(mm.Relative.X, 0, mm.Relative.Y) * (distance / 700f);
        }
        else if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.Q) yaw -= 15; else if (k.Keycode == Key.E) yaw += 15;
            else if (k.Keycode == Key.Space) { Paused = !Paused; RefreshUi(); }
            else if (k.Keycode == Key.Home) pan = Vector3.Zero;
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
        // 画面の端にマウスを置くと地図が動く（HoI4 のように）。窓に入っているときだけ。
        // 上下の端は、窓の枠やタスクバーで端まで届かないことがあるので、端の少し外（40px）まで数える（2026-11-19 ユーザーの印）。
        var mouse = GetViewport().GetMousePosition(); var size = GetViewport().GetVisibleRect().Size; const float edge = 24, outside = 40;
        // ボタンや目盛りの上にいる間は動かさない。端に0.4秒とどまってから動かす（ボタンを押しに行くだけで動かないように。2026-11-19 ユーザーの印）。
        var edgeMove = Vector3.Zero; bool onButton = GetViewport().GuiGetHoveredControl() is BaseButton or Slider;
        if (DisplayServer.WindowIsFocused() && !onButton && mouse.X >= -outside && mouse.Y >= -outside && mouse.X <= size.X + outside && mouse.Y <= size.Y + outside)
        {
            if (mouse.X < edge) edgeMove.X -= 1; else if (mouse.X > size.X - edge) edgeMove.X += 1;
            if (mouse.Y < edge) edgeMove.Z -= 1; else if (mouse.Y > size.Y - edge) edgeMove.Z += 1;
        }
        edgeDwell = edgeMove == Vector3.Zero ? 0 : edgeDwell + delta;
        if (edgeDwell >= 0.4) input += edgeMove;
        if (input != Vector3.Zero && !Debug.NoteOpen && FilmRoot == null) pan += Basis.FromEuler(new Vector3(0, Mathf.DegToRad(yaw), 0)) * input * (float)(distance * delta);
        if (skipUntil > World.Second && !Debug.NoteOpen)
        {
            // 一気に飛ばす：1フレームに最大2万秒。本人に知らせ（日誌の行）が入ったら止まる。
            for (int i = 0; i < 20000 && World.Second < skipUntil; i++) { World.Step(); if (World.Journal.Count > skipJournal) { skipUntil = 0; break; } }
            if (World.Second >= skipUntil) skipUntil = 0;
            dirty = true; UpdateActors(); PlaceCamera(); RefreshUi(); SaveGame(); return;
        }
        // 動画（台本）は前のとおり「歩いている間と、待つと決めた時刻まで」進む。遊ぶときは止める・進めるで決まる。
        bool running = !Debug.NoteOpen && !Scene.Open && awardsLayer == null && askLayer == null && !FilmPause && (FilmRoot != null ? World.Player.Moving || WaitUntil > World.Second : !Paused);
        if (running)
        {
            acc += delta * Speed; int n = Math.Min((int)acc, 5000); acc -= n;
            for (int i = 0; i < n; i++)
            {
                bool wasMoving = World.Player.Moving; World.Step();
                if (FilmRoot == null && World.Second % 5 == 0 && World.War.Phase is "行軍" or "合戦" && NewContact() && stopAtContact.ButtonPressed) { Paused = true; acc = 0; dirty = true; RefreshUi(); break; }
                if (wasMoving && !World.Player.Moving) dirty = true;
                if (FilmRoot == null) { if (wasMoving && !World.Player.Moving) marker.Visible = false; continue; }
                if (wasMoving && !World.Player.Moving && WaitUntil <= World.Second) { acc = 0; marker.Visible = false; break; }
                if (!wasMoving && WaitUntil <= World.Second) { acc = 0; break; }
            }
        }
        else acc = 0;
        if (dirty || World.Second / 600 != savedBucket) { dirty = false; SaveGame(); }
        UpdateActors(); UpdateBattleView(); UpdateHover(); UpdateAwards(); DrawPath(); PlaceCamera();
        journal.GetParent<Control>().GetParent<Control>().Modulate = new Color(1, 1, 1, (float)Math.Clamp(1.4 - (Time.GetTicksMsec() / 1000.0 - journalShown) / 8.0, 0.35, 1));
        uiTimer -= delta; if (uiTimer <= 0) { uiTimer = running ? 0.5 : 0.25; RefreshUi(); }
    }

    readonly Dictionary<string, (MeshInstance3D Body, Label3D Label)> markers = new();
    void UpdateActors()
    {
        bool close = distance < 700, war = World.War.Phase is "軍議待ち" or "行軍" or "合戦" || World.Returning;
        if (war) PlanFormations(Math.Max(0.12f, distance / 380f) * 1.25f); else formation.Clear();
        PlanClashLines(Math.Max(0.12f, distance / 380f) * 1.25f);
        foreach (var p in World.People)
        {
            var (body, label) = actors[p.Id]; bool seen = World.Sees(World.Player, p);
            bool unit = p.Soldiers > 0 && war;
            if (body.MaterialOverride is StandardMaterial3D m && !p.Player) m.AlbedoColor = HouseColor(p.House);
            body.Visible = seen; label.Visible = seen && (p.Player || close || unit);
            label.Text = unit ? $"{p.Name}　兵{p.Soldiers}" : p.Name;
            float grow = unit ? 1 + MathF.Sqrt(p.Soldiers) / 8 : 1, scale = Math.Max(1, distance / 450), r = (p.Player ? 7 : 5) * scale * grow; var pos = ToScene(p.X, p.Y) + new Vector3(0, r, 0);
            body.Position = pos; body.Scale = Vector3.One * scale * grow; label.Position = pos + new Vector3(0, r + distance * 0.035f, 0);
            if (dots.TryGetValue(p.Id, out var dot) && dot.Apply(SpriteKey(World, p)))
            {
                // ちびコマ：足元を地面に置き、近づいても遠ざかっても見える大きさにする。歩く・戦う・止まるで動きを変える。
                body.Visible = false; dot.Visible = seen;
                // 隊はいつも世界の本当の位置に描く（描くときだけ動かすと、飛んだり勝手に位置が変わって見える。2026-11-19 ユーザーの印）。
                var real = ToScene(p.X, p.Y); bool inLine = formation.TryGetValue(p.Id, out var fm); var foot = real;
                float px = Math.Max(0.12f, distance / 380f) * (unit ? 1.25f : 1f) * (p.Player ? 1.15f : 1f); dot.PixelSize = px;
                dot.Position = foot;
                string anim = p.Doing.Contains("戦って") ? "attack" : p.Doing.Contains("退いて") || p.Doing.Contains("敗走") ? "damage" : p.Moving ? "walk" : "idle";
                dot.Freeze(anim, Time.GetTicksMsec() / 1000.0 + p.Id.Length * 0.37);
                if (lastPos.TryGetValue(p.Id, out var before)) { dot.Face(real - before, Camera); var mv = real - before; mv.Y = 0; if (mv.LengthSquared() > 0.01f) heading[p.Id] = mv.Normalized(); }
                lastPos[p.Id] = real;
                // 向きは世界の向き一つ（凸と同じ）。合戦の間は、ちびコマも足軽もこれに合わせる。
                if (World.War.Facing.TryGetValue(p.Id, out var wf)) { var fdir = new Vector3((float)wf[0], 0, (float)wf[1]); dot.Face(fdir, Camera); heading[p.Id] = fdir; }
                if (inLine && fm.Count > 1) label.Text = $"{p.Name}勢　{fm.Total}（{fm.Parts}）";
                else if (inLine && fm.Count == 0) label.Visible = seen && (p.Player || close);
                // 引いた視点では、隊を札一枚で見せる（2026-11-19 案2）。近づくと大将と足軽のちびコマ。
                bool card = unit && distance > CardDistance;
                DrawFollowers(p, foot, px, seen && unit && !card, anim);
                if (card) { dot.Visible = false; label.Visible = false; }
                // 総勢で行軍している間は、大将の凸一つにまとめる（「鷹野正之勢」と兵の合計・内訳）。
                bool inBody = World.InHost(p), bodyLead = inBody && p.Id == World.HeadOf("takano");
                var host = bodyLead ? World.Host.OrderByDescending(u => u.Id == p.Id).ThenBy(u => u.Id).ToArray() : [];
                bool group = inLine && fm.Count > 1;
                if (bodyLead && host.Length > 1) DrawCard(p, foot, card && seen, p.Name + "勢", host.Sum(u => u.Soldiers), string.Join("+", host.Select(u => u.Soldiers)));
                else DrawCard(p, foot, card && seen && !inBody && !(inLine && fm.Count == 0), group ? p.Name + "勢" : p.Name, group ? fm.Total : p.Soldiers, group ? fm.Parts : "");
                DrawSwirl(p, foot, seen && unit && World.Confused(p.Id), card);
                // 謀叛方の隊の名前は足元の下に出す（ぶつかっている相手の名前と重ならないように）。
                label.Position = p.House == "iwakiri" && unit ? foot + new Vector3(0, -6 * px - distance * 0.02f, 0) : foot + new Vector3(0, 30 * px + distance * 0.02f, 0);
            }
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
    }

    // ---- 話す場面 ----
    // 返事に台詞（「」）があれば話す場面を前に出す。受けるときは、頼む人の口上から。
    internal void Act(string aid)
    {
        if (aid == "council:open") { OpenCouncil(); return; }
        var lines = new List<(string, string)>();
        if (aid.StartsWith("accept:") && World.Tasks.FirstOrDefault(t => t.Id == aid[7..]) is { } task && task.Speech != "") lines.Add((task.Giver, task.Speech));
        var result = World.Do(aid); ShowGoal();
        if (ProtoScene.Parse(World, result) is { } said) lines.Add(said); else Message = result;
        if (lines.Count > 0) { Message = ""; Scene.Show(lines, [World.Player.Id]); }
    }
    void Say(string result, string who)
    {
        if (ProtoScene.Parse(World, result) is { } said) Scene.Show([said], [World.Player.Id, who]); else Message = result;
    }

    // ---- 軍議（話す場面の上で、意見を聞いてから選ぶ） ----
    internal (OptionButton Role, OptionButton Dep, OptionButton Sig, Button Ok, OptionButton Plan, OptionButton Goal, OptionButton Retreat)? councilChoices;
    // 軍議（2026-11-20 ユーザー）：まず総大将の案（大目標・作戦・全員の役目と陣）が戦場図に出る。自分の駒は地図を押して置き直せる
    // （総大将が本人なら、駒を選んで全員を置ける）。地形とそこで受ける効果の名前を駒の一覧に出す。変えた所があれば再提案になる。
    CanvasLayer? councilLayer; Action<string, long, long>? councilPlace;
    internal void PlaceCouncilPiece(string? id, float xM, float yM) => councilPlace?.Invoke(id ?? World.Player.Id, (long)(xM * 100), (long)(yM * 100));
    internal void OpenCouncil()
    {
        if (Scene.Open || !World.CouncilOpen) return;
        var view = World.Council(); var prop = World.Proposal(); bool lead = World.HeadOf("takano") == World.Player.Id;
        councilLayer?.QueueFree(); councilLayer = new CanvasLayer { Layer = 60, Visible = false }; AddChild(councilLayer);
        var dimmer = new ColorRect { Color = new Color(0, 0, 0, 0.55f) }; dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect); councilLayer.AddChild(dimmer);
        var frame = new PanelContainer(); frame.SetAnchorsPreset(Control.LayoutPreset.FullRect); frame.OffsetLeft = 30; frame.OffsetTop = 24; frame.OffsetRight = -30; frame.OffsetBottom = -24; councilLayer.AddChild(frame);
        var pad = new MarginContainer(); foreach (var m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" }) pad.AddThemeConstantOverride(m, 14); frame.AddChild(pad);
        var row0 = new HBoxContainer(); row0.AddThemeConstantOverride("separation", 18); pad.AddChild(row0);

        // 戦場図：正面の地点を真ん中に、東西1800m・南北1400m。
        var map = World.Map; int cell = map.Data.CellM; var front = World.Front;
        int wM = 1800, hM = 1400, x0 = (int)Math.Clamp(front.X / 100 - wM / 2, 0, map.Data.WidthM - wM) / cell * cell, y0 = (int)Math.Clamp(front.Y / 100 - hM / 2 + 100, 0, map.Data.HeightM - hM) / cell * cell;
        var img = Image.CreateEmpty(wM / cell, hM / cell, false, Image.Format.Rgb8);
        for (int r = 0; r < hM / cell; r++) for (int c = 0; c < wM / cell; c++) { int mc = x0 / cell + c, mr = y0 / cell + r; img.SetPixel(c, r, GroundColor(map.GroundAt(mc, mr), map.HeightAt(mc, mr) / 100f, mc, mr)); }
        var size = new Vector2(wM, hM) * 0.36f; float k = size.X / wM;
        var board = new TextureRect { Texture = ImageTexture.CreateFromImage(img), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = CanvasItem.TextureFilterEnum.Nearest, CustomMinimumSize = size, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Stop, ClipContents = true };
        row0.AddChild(board);
        Vector2 OnBoard(long x, long y) => new((x / 100f - x0) * k, (y / 100f - y0) * k);
        Label Tag(string text, Vector2 at, Color color, int font = 15) { var l = new Label { Text = text, Position = at, MouseFilter = Control.MouseFilterEnum.Ignore }; l.AddThemeFontSizeOverride("font_size", font); l.AddThemeColorOverride("font_color", color); l.AddThemeColorOverride("font_outline_color", Colors.Black); l.AddThemeConstantOverride("outline_size", 5); board.AddChild(l); return l; }
        foreach (var site in map.Data.Sites.Where(t => t.X >= x0 && t.X < x0 + wM && t.Y >= y0 && t.Y < y0 + hM)) Tag(site.Name, OnBoard(site.X * 100L, site.Y * 100L) + new Vector2(-24, -10), new Color(1, 1, 0.85f));
        // 知っている敵の印（最後に見た場所）。
        foreach (var mk in World.Markers) { var at = OnBoard(mk.X, mk.Y); board.AddChild(new ColorRect { Color = HouseColor("iwakiri"), Position = at - new Vector2(6, 6), Size = new Vector2(12, 12), MouseFilter = Control.MouseFilterEnum.Ignore }); Tag(World.Person(mk.Unit).Name, at + new Vector2(8, -10), new Color(1, 0.7f, 0.7f), 13); }

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; row0.AddChild(right);
        right.AddChild(new Label { Text = "総大将　" + view.Commander, Modulate = new Color(1, 0.9f, 0.6f) });
        var grid = new GridContainer { Columns = 1 }; right.AddChild(grid);
        OptionButton Choice(string title, string[] items, int pick)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; grid.AddChild(row); row.AddChild(new Label { Text = title, CustomMinimumSize = new Vector2(150, 0) });
            var o = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; foreach (var it in items) o.AddItem(it); o.Select(Math.Max(0, pick)); row.AddChild(o); return o;
        }
        var goal = Choice("大目標", view.Goals, Array.IndexOf(view.Goals, prop.Goal));
        var plan = Choice("作戦", ProtoWorld.PlansOf(prop.Goal), Array.IndexOf(ProtoWorld.PlansOf(prop.Goal), prop.Plan));
        string myRole = prop.Pieces.First(x => x.Unit == World.Player.Id).Role; var roleNames = ProtoWorld.RolesOf(prop.Plan).Select(r => r.Role).ToArray();
        var role = Choice("自分の役目", roleNames, Array.IndexOf(roleNames, myRole));
        var dep = Choice("出立", view.Departures, Array.IndexOf(view.Departures, prop.Departure));
        var sig = Choice("合図", view.Signals, Array.IndexOf(view.Signals, prop.Signal));
        var retreat = Choice("撤退の条件を足す", view.RetreatAdds, Array.IndexOf(view.RetreatAdds, prop.RetreatAdd));
        right.AddChild(new HSeparator());
        var list = new VBoxContainer(); right.AddChild(list);
        var ok = new Button { Text = "従う" }; right.AddChild(ok);

        // 駒
        var pieces = prop.Pieces.ToDictionary(x => x.Unit, x => (x.X, x.Y)); var moved = new HashSet<string>(); string pick = World.Player.Id;
        var marks = new Dictionary<string, (ColorRect Body, Label Name)>();
        string Now() => goal.Selected + "|" + plan.GetItemText(plan.Selected) + "|" + role.GetItemText(role.Selected) + "|" + dep.Selected + "|" + sig.Selected + "|" + retreat.Selected;
        string first = Now();
        void Draw()
        {
            var p = World.Proposal(view.Goals[goal.Selected], plan.GetItemText(plan.Selected), role.GetItemText(role.Selected));
            foreach (var c in list.GetChildren()) { list.RemoveChild(c); c.QueueFree(); }
            foreach (var pc in p.Pieces)
            {
                var (x, y) = pieces[pc.Unit]; bool mine = pc.Unit == World.Player.Id, sel = pc.Unit == pick && (lead || mine);
                if (!marks.TryGetValue(pc.Unit, out var mk))
                {
                    var body = new ColorRect { Size = new Vector2(18, 18), MouseFilter = Control.MouseFilterEnum.Ignore }; board.AddChild(body);
                    marks[pc.Unit] = mk = (body, Tag(pc.Name, Vector2.Zero, Colors.White, 14));
                }
                // 同じ所に置かれた駒は、少しずらして重ならないようにする（図の上だけ）。
                int same = p.Pieces.TakeWhile(q => q.Unit != pc.Unit).Count(q => Math.Abs(pieces[q.Unit].X - x) + Math.Abs(pieces[q.Unit].Y - y) < 40_00);
                var at = OnBoard(x, y) + new Vector2(0, same * 20); mk.Body.Position = at - new Vector2(9, 9); mk.Body.Color = mine ? new Color(1, 0.85f, 0.3f) : HouseColor("takano").Lightened(0.25f); mk.Name.Position = at + new Vector2(11, -11);
                string effect = World.TerrainEffect(x, y);
                var line = new Button { Text = $"{pc.Name}　{pc.Role}　{World.Person(pc.Unit).Troop}{pc.Soldiers}　{World.TerrainName(x, y)}" + (effect != "" ? "　" + effect : "") + $"　功{pc.Merit}", Alignment = HorizontalAlignment.Left, Flat = true, Disabled = !lead && !mine };
                line.AddThemeColorOverride("font_color", sel ? new Color(1, 0.88f, 0.45f) : Colors.White); line.AddThemeColorOverride("font_disabled_color", Colors.White);
                var id = pc.Unit; line.Pressed += () => { pick = id; Draw(); };
                list.AddChild(line);
            }
            ok.Text = Now() != first || moved.Count > 0 ? "再提案する" : "従う";
        }
        // 作戦や役目を変えたら、総大将の陣立てを引き直す（自分で置いた駒は、役目が変わらなければそのまま）。
        void Relay(bool keepMine)
        {
            var p = World.Proposal(view.Goals[goal.Selected], plan.GetItemText(plan.Selected), role.GetItemText(role.Selected));
            foreach (var pc in p.Pieces) if (!(keepMine && moved.Contains(pc.Unit))) { pieces[pc.Unit] = (pc.X, pc.Y); moved.Remove(pc.Unit); }
            Draw();
        }
        void Roles() { role.Clear(); foreach (var r in ProtoWorld.RolesOf(plan.GetItemText(plan.Selected))) role.AddItem(r.Role); var mine = World.Proposal(view.Goals[goal.Selected], plan.GetItemText(plan.Selected)).Pieces.First(x => x.Unit == World.Player.Id).Role; for (int n = 0; n < role.ItemCount; n++) if (role.GetItemText(n) == mine) role.Select(n); }
        goal.ItemSelected += n => { plan.Clear(); foreach (var p in ProtoWorld.PlansOf(view.Goals[(int)n])) plan.AddItem(p); plan.Select(0); Roles(); Relay(false); };
        plan.ItemSelected += _ => { Roles(); Relay(false); };
        role.ItemSelected += _ => Relay(false);
        dep.ItemSelected += _ => Draw(); sig.ItemSelected += _ => Draw(); retreat.ItemSelected += _ => Draw();
        councilPlace = (id, x, y) =>
        {
            if (!pieces.ContainsKey(id) || !lead && id != World.Player.Id) return;
            var n = ProtoMove.Nearest(World.Map, Math.Clamp(x, x0 * 100L, (x0 + wM) * 100L - 1), Math.Clamp(y, y0 * 100L, (y0 + hM) * 100L - 1));
            pieces[id] = (n.X, n.Y); moved.Add(id); Draw();
        };
        board.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) councilPlace?.Invoke(pick, (long)((mb.Position.X / k + x0) * 100), (long)((mb.Position.Y / k + y0) * 100)); };
        Draw();
        councilChoices = (role, dep, sig, ok, plan, goal, retreat);
        ok.Pressed += () =>
        {
            var posts = moved.Where(id => lead || id == World.Player.Id).ToDictionary(id => id, id => new[] { pieces[id].X, pieces[id].Y });
            var reply = World.DecideCouncil(null, view.Departures[dep.Selected], view.Signals[sig.Selected], plan.GetItemText(plan.Selected), role.GetItemText(role.Selected), view.Goals[goal.Selected], view.RetreatAdds[retreat.Selected], posts);
            councilLayer?.QueueFree(); councilLayer = null; councilPlace = null; councilChoices = null;
            Scene.Close();
            var after = new List<(string, string)>();
            if (ProtoScene.Parse(World, reply) is { } r) after.Add(r);
            if (World.OpenTasks.FirstOrDefault(t => t.Kind == "muster") is { } m) after.Add((m.Giver, m.Speech));
            if (after.Count > 0) Scene.Show(after, [World.Player.Id]);
        };
        // 皆の意見を聞き終えたら、戦場図を開く。
        var gate = new Control(); var layer = councilLayer; gate.TreeEntered += () => { if (IsInstanceValid(layer)) layer.Visible = true; };
        var lines = view.Opinions.Select(o => ProtoScene.Parse(World, o.Replace("（大将）", ""))).Where(x => x != null).Select(x => x!.Value).ToList();
        Scene.Show(lines, [World.Player.Id], gate);
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
