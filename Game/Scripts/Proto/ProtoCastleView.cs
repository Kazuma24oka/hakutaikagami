using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using HakutaiKagami.Core.Proto;

// 試作版の攻城の間の城の見え方（決まりは Core/Proto/ProtoSiege.cs）。
// 城そのものは、もとの模型（本城・支城・館）を4倍にしたもの（Game/Scripts/Proto/ProtoGame.cs の拠点の模型。2026-11-21 ユーザー）。ここでは足すものだけを描く。
// ・城門（城の南）のまわりに、明るい枠がゆっくり点滅する（攻城の間。門の色は変えない）。
// ・城内の武将と兵は、合戦の姿のちびコマで、城の中（柵の内の広場）にいる。城が打ち合っている間は打つ動きをするだけで、門の前には立たない（2026-11-21 ユーザー）。
// ・城の名前の下に、門と城の兵、門の棒を出す（本人の目か、城と戦う味方の知らせで分かるとき）。士気は上の帯。
public partial class ProtoGame
{
    const string CastleSite = "iwakiri-castle";
    internal const float CastleScale = 4; // 城の模型の倍率
    Label3D? castleLabel; Sprite3D? castleBar; string castleKey = "";
    MeshInstance3D? gateGlow; StandardMaterial3D? gateGlowMat;
    readonly Dictionary<string, DotOfficerActor> wallOfficers = new(); readonly List<DotOfficerActor> wallSoldiers = new();

    void UpdateCastleView()
    {
        if (castleLabel == null)
        {
            castleLabel = new Label3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0009f, FontSize = 30, OutlineSize = 8, OutlineModulate = new Color(0, 0, 0, 0.9f), NoDepthTest = true, RenderPriority = 24, Visible = false };
            castleBar = new Sprite3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0016f, NoDepthTest = true, Shaded = false, TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, RenderPriority = 24, Visible = false };
            gateGlowMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(1, 0.95f, 0.6f, 0.3f), CullMode = BaseMaterial3D.CullModeEnum.Disabled };
            gateGlow = new MeshInstance3D { Mesh = new BoxMesh { Size = Vector3.One }, MaterialOverride = gateGlowMat, Visible = false };
            AddChild(castleLabel); AddChild(castleBar); AddChild(gateGlow);
        }
        var siege = World.War.Siege; var site = World.Map.Site(CastleSite); var c = ToScene(site.X * 100L, site.Y * 100L);
        var g = World.GatePoint; var gate = new Vector3(g.X / 100f, c.Y, g.Y / 100f);
        bool live = siege != null && World.War.Result == "";
        // 城門のハイライト。
        gateGlow!.Visible = live;
        if (live)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin(Time.GetTicksMsec() / 380f);
            gateGlowMat!.AlbedoColor = new Color(1, 0.95f, 0.6f, 0.12f + 0.24f * pulse);
            gateGlow.Position = gate + new Vector3(0, 16, -8); gateGlow.Scale = new Vector3(46, 34, 30) * (1 + 0.05f * pulse);
        }
        DrawWallGuards(c, gate);
        DrawRing(c, live && siege!.CampAt > 0);
        // 城の欄：攻城の間、本人が城の近く（1km）にいるときだけ。
        bool show = live && World.KnowsCastle();
        castleLabel!.Visible = castleBar!.Visible = show; if (!show) return;
        int men = World.Garrison.Sum(u => u.Soldiers);
        string key = $"{siege!.Gate}|{men}";
        if (key != castleKey)
        {
            castleKey = key;
            castleLabel.Text = $"門{siege.Gate}　兵{men}"; // 士気は上の帯（岩切勢＝城の士気）
            var img = Image.CreateEmpty(100, 8, false, Image.Format.Rgba8); img.Fill(new Color(0.08f, 0.08f, 0.08f, 0.9f));
            img.FillRect(new Rect2I(1, 1, Math.Max(1, 98 * Math.Clamp(siege.Gate, 0, 100) / 100), 6), new Color(0.85f, 0.85f, 0.9f));
            castleBar.Texture = ImageTexture.CreateFromImage(img);
        }
        var top = c + new Vector3(0, 34 * CastleScale + 30 + distance * 0.05f, 0);
        castleLabel.Position = top; castleBar.Position = top - (Camera?.GlobalBasis.Y ?? Vector3.Up) * (distance * 0.03f);
    }

    // 囲み（Core/Proto/ProtoSiege.cs）：城から450mの薄い輪と付城。付城は、普請が進むにつれて柵が立っていき、築き終えると櫓と小屋が建つ。
    MeshInstance3D? siegeRing; readonly Node3D?[] forts = new Node3D?[6]; readonly List<Node3D>[] fortPales = [.. Enumerable.Range(0, 6).Select(_ => new List<Node3D>())]; readonly Node3D?[] fortKeep = new Node3D?[6];
    void DrawRing(Vector3 c, bool on)
    {
        if (siegeRing == null)
        {
            var mat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(HouseColor("takano"), 0.35f), NoDepthTest = true, RenderPriority = 12 };
            siegeRing = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.99f, OuterRadius = 1f, Rings = 128, RingSegments = 4 }, MaterialOverride = mat, Visible = false }; AddChild(siegeRing);
        }
        siegeRing.Visible = on;
        if (on) { float r = ProtoWorld.SiegeRingRadius / 100f; siegeRing.Position = c + new Vector3(0, 2, 0); siegeRing.Scale = new Vector3(r, Math.Max(1, distance / 60f), r); }
        for (int i = 0; i < forts.Length; i++)
        {
            int prog = on ? World.FortProgress(i) : 0;
            if (prog > 0 && forts[i] == null) forts[i] = BuildFort(i);
            if (forts[i] == null) continue;
            forts[i]!.Visible = prog > 0;
            var pales = fortPales[i]; int up = prog >= 100 ? pales.Count : pales.Count * prog / 100;
            for (int k = 0; k < pales.Count; k++) pales[k].Visible = k < up;
            fortKeep[i]!.Visible = prog >= 100;
        }
    }
    Node3D BuildFort(int i)
    {
        var n = new Node3D(); AddChild(n); var p = World.SectorPost(i); var at = ToScene(p.X, p.Y); at.Y = HeightAt(at.X, at.Z); n.Position = at;
        var wood = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.32f, 0.2f) }; var roof = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.5f, 0.42f) }; var flag = new StandardMaterial3D { AlbedoColor = HouseColor("takano") };
        // 柵：すき間なく並べた板塀（城に向いた側に木戸のすき間）。普請の進みに合わせて、木戸の脇から順に立つ。
        var c = World.Map.Site("iwakiri-castle"); var toCastle = new Vector2(c.X - p.X / 100f, c.Y - p.Y / 100f).Normalized(); float gate = Mathf.Atan2(toCastle.Y, toCastle.X);
        var earth = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.42f, 0.3f) }; var plaster = new StandardMaterial3D { AlbedoColor = new Color(0.86f, 0.84f, 0.78f) };
        const int N = 32; const float R = 34;
        // 土塁（低い盛り土の輪）。普請を始めた時から見える。
        n.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = R - 5, OuterRadius = R + 4, Rings = 48, RingSegments = 8 }, MaterialOverride = earth, Position = new Vector3(0, 0.5f, 0), Scale = new Vector3(1, 0.35f, 1) });
        // 柵：すき間のある尖った杭と、上下2本の横木。城に向いた側に木戸。
        for (int k = 2; k < N - 1; k++)
        {
            float a = gate + k * Mathf.Tau / N;
            var pale = new Node3D { Position = new Vector3(Mathf.Cos(a) * R, 2, Mathf.Sin(a) * R), Rotation = new Vector3(0, -a, 0) };
            foreach (float off in new[] { -2.4f, 0f, 2.4f })
            {
                pale.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.1f, 13, 1.1f) }, MaterialOverride = wood, Position = new Vector3(0, 6.5f, off) });
                pale.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(1.1f, 2, 1.1f) }, MaterialOverride = wood, Position = new Vector3(0, 14, off) });
            }
            foreach (float h in new[] { 4f, 10f }) pale.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.7f, 0.9f, 7.4f) }, MaterialOverride = wood, Position = new Vector3(0.7f, h, 0) });
            n.AddChild(pale); fortPales[i].Add(pale);
        }
        // 出来上がると：木戸の脇に二重の櫓（白壁）、中に小屋と蔵、幟を3本。
        var keep = new Node3D(); n.AddChild(keep); fortKeep[i] = keep;
        var g = new Vector3(Mathf.Cos(gate), 0, Mathf.Sin(gate)) * (R - 9) + new Vector3(Mathf.Cos(gate + 1.2f), 0, Mathf.Sin(gate + 1.2f)) * 6;
        foreach (var (x, z) in new[] { (-4.5f, -4.5f), (4.5f, -4.5f), (-4.5f, 4.5f), (4.5f, 4.5f) }) keep.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.4f, 16, 1.4f) }, MaterialOverride = wood, Position = g + new Vector3(x, 8, z) });
        keep.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(12, 7, 12) }, MaterialOverride = plaster, Position = g + new Vector3(0, 19.5f, 0) });
        keep.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(15, 3.5f, 15) }, MaterialOverride = roof, Position = g + new Vector3(0, 24.7f, 0) });
        keep.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(8, 4.5f, 8) }, MaterialOverride = plaster, Position = g + new Vector3(0, 28.6f, 0) });
        keep.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(11, 3.5f, 11) }, MaterialOverride = roof, Position = g + new Vector3(0, 32.5f, 0) });
        keep.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(16, 6, 10) }, MaterialOverride = wood, Position = new Vector3(-6, 3, -8) });
        keep.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(18, 5, 12) }, MaterialOverride = roof, Position = new Vector3(-6, 8.5f, -8) });
        keep.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(9, 6, 8) }, MaterialOverride = plaster, Position = new Vector3(9, 3, 8) });
        keep.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(11, 4, 10) }, MaterialOverride = roof, Position = new Vector3(9, 8, 8) });
        for (int k = 0; k < 3; k++)
        {
            var pole = new Vector3(-14 + k * 6, 0, 14);
            keep.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.5f, 22, 0.5f) }, MaterialOverride = wood, Position = pole + new Vector3(0, 11, 0) });
            keep.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.3f, 10, 3.2f) }, MaterialOverride = flag, Position = pole + new Vector3(0, 16, 1.7f) });
        }
        return n;
    }

    // 城内の武将と兵を、城の中の広場に置く（合戦の姿のちびコマ。南を向く）。
    void DrawWallGuards(Vector3 c, Vector3 gate)
    {
        var garrison = World.Garrison.OrderByDescending(u => u.Id == World.HeadOf("iwakiri")).ThenBy(u => u.Id).ToArray();
        bool show = garrison.Length > 0 && World.War.Phase is "行軍" or "合戦" && World.KnowsCastle(); // 本人の目か、城と戦う味方の知らせで分かるとき
        float px = Math.Max(0.12f, distance / 380f); string anim = World.CastleFighting ? "attack" : "idle"; double t = Time.GetTicksMsec() / 1000.0;
        // 柵の内の広場（城の真ん中から南へ60〜80m。主郭の段と門の建物の間）に、左右へ散らして置く。
        Vector3 Spot(int n) { var at = c + new Vector3((n % 2 == 0 ? 1 : -1) * (18 + 9 * (n / 2)), 0, 62 + 10 * (n % 3)); at.Y = HeightAt(at.X, at.Z) + 1; return at; }
        foreach (var (_, d) in wallOfficers) d.Visible = false;
        int slot = 0;
        if (show) foreach (var p in garrison)
        {
            if (!wallOfficers.TryGetValue(p.Id, out var d)) { d = new DotOfficerActor { OnTop = true, Solid = true }; AddChild(d); wallOfficers[p.Id] = d; }
            if (!d.Apply(SpriteKey(World, p))) continue;
            d.Visible = true; d.PixelSize = px * 1.2f; d.Position = Spot(slot++); d.Freeze(anim, t + slot * 0.4); d.Face(Vector3.Back, Camera);
        }
        int want = show ? Math.Clamp(garrison.Sum(u => u.Soldiers) / 15, 2, 8) : 0; var mix = show ? TroopMix(garrison[0]) : [];
        while (wallSoldiers.Count < want) { var d = new DotOfficerActor { OnTop = true, Solid = true }; AddChild(d); wallSoldiers.Add(d); }
        for (int i = 0; i < wallSoldiers.Count; i++)
        {
            var d = wallSoldiers[i]; d.Visible = i < want; if (i >= want) continue;
            d.Apply($"soldier/ashigaru/{mix[i % mix.Length]}/war/{ColorOf("iwakiri")}");
            d.PixelSize = px * FollowerSize; d.Position = Spot(slot + i); d.Freeze(anim, t + i * 0.21); d.Face(Vector3.Back, Camera);
        }
    }
}
