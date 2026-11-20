using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using HakutaiKagami.Core.Proto;

// 試作版の城の層の見え方（2026-11-20 ユーザーの図。決まりは Core/Proto/ProtoSiege.cs）。
// 城のまわりに、層の数だけ塀を四角く巡らせる（三層なら外から 三の丸・二の丸・本丸）。内の層ほど塀が高く、地面も一段高く明るい。
// 攻め手の陣の側に城門。攻城の間：今守っている層の城門は明るく、失った層は塀が崩れて低くなり、地面も暗くなる。
// 城の名前の下に、今の層・その耐久・城の兵の数（見えているとき）と、耐久の棒を出す。
public partial class ProtoGame
{
    const string CastleSite = "iwakiri-castle";
    sealed class Ring { public List<MeshInstance3D> Walls = new(); public MeshInstance3D Gate = null!, Floor = null!; public StandardMaterial3D Mat = null!, GateMat = null!, FloorMat = null!; public float Height; public Color Ground; }
    readonly List<Ring> castleRings = new(); Label3D? castleLabel; Sprite3D? castleBar; string castleKey = "";
    static readonly Color WallColor = new(0.78f, 0.74f, 0.66f), WallLost = new(0.25f, 0.22f, 0.2f);

    void BuildCastleRings()
    {
        var site = World.Map.Site(CastleSite); int layers = ProtoWorld.LayersOf(CastleSite); var c = ToScene(site.X * 100L, site.Y * 100L);
        for (int i = 0; i < layers; i++)
        {
            float r = ProtoWorld.LayerRadius(layers, i) / 100f, h = 9 + 4 * i, thick = 8;
            var ring = new Ring { Height = h, Mat = new StandardMaterial3D { AlbedoColor = WallColor }, GateMat = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.3f, 0.18f) } };
            // 層の地面：内の層ほど一段高く、色も明るい（外から 三の丸・二の丸・本丸）。
            ring.Ground = (layers - i) switch { <= 1 => new Color(0.86f, 0.76f, 0.46f), 2 => new Color(0.74f, 0.53f, 0.31f), _ => new Color(0.56f, 0.36f, 0.28f) };
            ring.FloorMat = new StandardMaterial3D { AlbedoColor = ring.Ground };
            ring.Floor = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2 * r, 1, 2 * r) }, MaterialOverride = ring.FloorMat, Position = c + new Vector3(0, 0.6f * (i + 1) / 2, 0), Scale = new Vector3(1, 0.6f * (i + 1), 1) };
            AddChild(ring.Floor);
            // 四辺の塀（地面の高さは城の真ん中に合わせる）。
            foreach (var (dx, dz, sx, sz) in new[] { (0f, -r, 2 * r + thick, thick), (0f, r, 2 * r + thick, thick), (-r, 0f, thick, 2 * r + thick), (r, 0f, thick, 2 * r + thick) })
            {
                var m = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(sx, 1, sz) }, MaterialOverride = ring.Mat, Position = c + new Vector3(dx, 0, dz) };
                AddChild(m); ring.Walls.Add(m);
            }
            // 城門：攻め手の陣の側の塀の上に、太い門。
            var g = World.GatePoint(i); var gp = ToScene(g.X, g.Y); var dir = gp - c; dir.Y = 0;
            var at = c + (dir.LengthSquared() > 0.01f ? dir.Normalized() : Vector3.Back) * r;
            ring.Gate = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(16, 1, 16) }, MaterialOverride = ring.GateMat, Position = at };
            AddChild(ring.Gate); castleRings.Add(ring);
        }
        castleLabel = new Label3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0009f, FontSize = 30, OutlineSize = 8, OutlineModulate = new Color(0, 0, 0, 0.9f), NoDepthTest = true, RenderPriority = 24, Visible = false };
        castleBar = new Sprite3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0016f, NoDepthTest = true, Shaded = false, TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, RenderPriority = 24, Visible = false };
        AddChild(castleLabel); AddChild(castleBar);
    }

    void UpdateCastleView()
    {
        if (castleRings.Count == 0) BuildCastleRings();
        var siege = World.War.Siege; int lost = siege?.Layer ?? 0; var site = World.Map.Site(CastleSite); var c = ToScene(site.X * 100L, site.Y * 100L);
        for (int i = 0; i < castleRings.Count; i++)
        {
            var ring = castleRings[i]; bool down = siege != null && i < lost, now = siege != null && i == lost;
            float h = down ? 2.5f : ring.Height;
            foreach (var m in ring.Walls) { m.Scale = new Vector3(1, h, 1); m.Position = m.Position with { Y = c.Y + h / 2 }; }
            ring.Mat.AlbedoColor = down ? WallLost : WallColor;
            ring.FloorMat.AlbedoColor = down ? ring.Ground.Darkened(0.55f) : ring.Ground;
            float gh = down ? 1.5f : ring.Height + 5;
            ring.Gate.Scale = new Vector3(1, gh, 1); ring.Gate.Position = ring.Gate.Position with { Y = c.Y + gh / 2 };
            ring.GateMat.AlbedoColor = down ? WallLost : now ? new Color(1, 0.8f, 0.3f) : new Color(0.45f, 0.3f, 0.18f);
        }
        // 城の欄：攻城の間、本人が城の近く（1km）にいるときだけ。城の兵の数は、見えている隊の分だけ足す。
        bool show = siege != null && siege.Layer < siege.Layers && World.War.Result == "" && c.DistanceTo(ToScene(World.Player.X, World.Player.Y)) <= 1000;
        castleLabel!.Visible = castleBar!.Visible = show; if (!show) return;
        int men = World.Garrison.Where(u => World.Sees(World.Player, u)).Sum(u => u.Soldiers);
        string key = $"{siege!.Layer}|{siege.Gate}|{men}";
        if (key != castleKey)
        {
            castleKey = key;
            castleLabel.Text = $"{ProtoWorld.LayerName(siege.Layers, siege.Layer)}　耐久{siege.Gate}" + (men > 0 ? $"　兵{men}" : "");
            var img = Image.CreateEmpty(100, 8, false, Image.Format.Rgba8); img.Fill(new Color(0.08f, 0.08f, 0.08f, 0.9f));
            img.FillRect(new Rect2I(1, 1, Math.Max(1, 98 * Math.Clamp(siege.Gate, 0, 100) / 100), 6), new Color(0.85f, 0.85f, 0.9f));
            castleBar.Texture = ImageTexture.CreateFromImage(img);
        }
        var top = c + new Vector3(0, 30 + distance * 0.05f, 0);
        castleLabel.Position = top; castleBar.Position = top - (Camera?.GlobalBasis.Y ?? Vector3.Up) * (distance * 0.03f);
    }
}
