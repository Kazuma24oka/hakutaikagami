using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using HakutaiKagami.Core.Proto;

// 試作版の攻城の間の城の見え方（決まりは Core/Proto/ProtoSiege.cs）。
// 城そのものは、もとの模型（本城・支城・館）を4倍にしたもの（Game/Scripts/Proto/ProtoGame.cs の拠点の模型。2026-11-21 ユーザー）。ここでは足すものだけを描く。
// ・城門（城の南）のまわりに、明るい枠がゆっくり点滅する（攻城の間。門の色は変えない）。
// ・城内の武将と兵は、合戦の姿のちびコマで、城門の両脇の柵の内に並ぶ（城が打ち合っている間は打つ動き）。
// ・城の名前の下に、耐久・城の兵・士気と、耐久の棒を出す（本人が城の近くにいるとき）。
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
        // 城の欄：攻城の間、本人が城の近く（1km）にいるときだけ。
        bool show = live && c.DistanceTo(ToScene(World.Player.X, World.Player.Y)) <= 1000;
        castleLabel!.Visible = castleBar!.Visible = show; if (!show) return;
        int men = World.Garrison.Sum(u => u.Soldiers);
        string key = $"{siege!.Gate}|{men}|{World.CastleMorale}";
        if (key != castleKey)
        {
            castleKey = key;
            castleLabel.Text = $"耐久{siege.Gate}　兵{men}　士気{World.CastleMorale}";
            var img = Image.CreateEmpty(100, 8, false, Image.Format.Rgba8); img.Fill(new Color(0.08f, 0.08f, 0.08f, 0.9f));
            img.FillRect(new Rect2I(1, 1, Math.Max(1, 98 * Math.Clamp(siege.Gate, 0, 100) / 100), 6), new Color(0.85f, 0.85f, 0.9f));
            castleBar.Texture = ImageTexture.CreateFromImage(img);
        }
        var top = c + new Vector3(0, 34 * CastleScale + 30 + distance * 0.05f, 0);
        castleLabel.Position = top; castleBar.Position = top - (Camera?.GlobalBasis.Y ?? Vector3.Up) * (distance * 0.03f);
    }

    // 城内の武将と兵を、城門の両脇の柵の内に並べる（合戦の姿のちびコマ。南を向く）。
    void DrawWallGuards(Vector3 c, Vector3 gate)
    {
        var garrison = World.Garrison.OrderByDescending(u => u.Id == World.HeadOf("iwakiri")).ThenBy(u => u.Id).ToArray();
        bool show = garrison.Length > 0 && World.War.Result == "" && World.War.Phase is "行軍" or "合戦" && c.DistanceTo(ToScene(World.Player.X, World.Player.Y)) <= 1000;
        float px = Math.Max(0.12f, distance / 380f); string anim = World.CastleFighting ? "attack" : "idle"; double t = Time.GetTicksMsec() / 1000.0;
        // 柵のすぐ内（門から北へ少し）。門の両脇へ、交互に並べる。
        Vector3 Spot(int n) { var at = gate + new Vector3((n % 2 == 0 ? 1 : -1) * (30 + 13 * (n / 2)), 0, -22); at.Y = HeightAt(at.X, at.Z) + 1; return at; }
        foreach (var (_, d) in wallOfficers) d.Visible = false;
        int slot = 0;
        if (show) foreach (var p in garrison)
        {
            if (!wallOfficers.TryGetValue(p.Id, out var d)) { d = new DotOfficerActor { OnTop = true, Solid = true }; AddChild(d); wallOfficers[p.Id] = d; }
            if (!d.Apply(SpriteKey(World, p))) continue;
            d.Visible = true; d.PixelSize = px * 1.2f; d.Position = Spot(slot++); d.Freeze(anim, t + slot * 0.4); d.Face(Vector3.Back, Camera);
        }
        int want = show ? Math.Clamp(garrison.Sum(u => u.Soldiers) / 12, 2, 12) : 0; var mix = show ? TroopMix(garrison[0]) : [];
        while (wallSoldiers.Count < want) { var d = new DotOfficerActor { OnTop = true, Solid = true }; AddChild(d); wallSoldiers.Add(d); }
        for (int i = 0; i < wallSoldiers.Count; i++)
        {
            var d = wallSoldiers[i]; d.Visible = i < want; if (i >= want) continue;
            d.Apply($"soldier/ashigaru/{mix[i % mix.Length]}/war/{ColorOf("iwakiri")}");
            d.PixelSize = px * FollowerSize; d.Position = Spot(slot + i); d.Freeze(anim, t + i * 0.21); d.Face(Vector3.Back, Camera);
        }
    }
}
