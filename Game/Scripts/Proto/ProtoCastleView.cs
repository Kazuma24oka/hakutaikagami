using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using HakutaiKagami.Core.Proto;

// 試作版の城の層の見え方（2026-11-20 ユーザーの図、2026-11-21 印「色に意味はないので色合いは戻す。城の構造に見えるように建造物を」）。
// 決まりは Core/Proto/ProtoSiege.cs。
// 城のまわりに、層の数だけ塀を四角く巡らせる（三層なら外から 三の丸・二の丸・本丸）。内の層ほど塀が高く、地面も一段高い（色はどの層も同じ土の色）。
// 塀の四隅に隅櫓、攻め手の陣の側に櫓門、層の中に屋敷と蔵。攻城の間：今守っている層の櫓門の屋根は金色。失った層は、塀が崩れ、建物が焼け落ちて黒くなる。
// 城の名前の下に、今の層・その耐久・城の兵の数（見えているとき）と、耐久の棒を出す。
public partial class ProtoGame
{
    const string CastleSite = "iwakiri-castle";
    sealed class Ring
    {
        public List<MeshInstance3D> Walls = new(); public List<(Node3D Node, float Height)> Buildings = new(); public MeshInstance3D Floor = null!;
        public StandardMaterial3D Wall = null!, Plaster = null!, Roof = null!, GateRoof = null!, FloorMat = null!; public float Height;
    }
    readonly List<Ring> castleRings = new(); Label3D? castleLabel; Sprite3D? castleBar; string castleKey = "";
    static readonly Color WallColor = new(0.78f, 0.74f, 0.66f), PlasterColor = new(0.93f, 0.91f, 0.86f), RoofColor = new(0.27f, 0.28f, 0.33f), EarthColor = new(0.69f, 0.63f, 0.5f), Burnt = new(0.2f, 0.18f, 0.17f);

    // 建物一つ：白壁の箱に、四方へ流れる屋根。足元が原点。
    static Node3D House(float w, float h, float d, StandardMaterial3D plaster, StandardMaterial3D roof)
    {
        var n = new Node3D();
        n.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(w, h, d) }, MaterialOverride = plaster, Position = new Vector3(0, h / 2, 0) });
        float rh = Math.Max(3, Math.Min(w, d) * 0.45f);
        n.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.78f, Height = 1, RadialSegments = 4, Rings = 1 }, MaterialOverride = roof, Position = new Vector3(0, h + rh / 2, 0), Rotation = new Vector3(0, Mathf.Pi / 4, 0), Scale = new Vector3(w * 1.05f, rh, d * 1.05f) });
        return n;
    }

    void BuildCastleRings()
    {
        var site = World.Map.Site(CastleSite); int layers = ProtoWorld.LayersOf(CastleSite); var c = ToScene(site.X * 100L, site.Y * 100L);
        for (int i = 0; i < layers; i++)
        {
            float r = ProtoWorld.LayerRadius(layers, i) / 100f, h = 9 + 4 * i, thick = 7, lift = 0.6f * (i + 1);
            var ring = new Ring
            {
                Height = h, Wall = new StandardMaterial3D { AlbedoColor = WallColor }, Plaster = new StandardMaterial3D { AlbedoColor = PlasterColor },
                Roof = new StandardMaterial3D { AlbedoColor = RoofColor }, GateRoof = new StandardMaterial3D { AlbedoColor = RoofColor }, FloorMat = new StandardMaterial3D { AlbedoColor = EarthColor },
            };
            // 層の地面：内の層ほど一段高い。色はどの層も同じ。
            ring.Floor = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2 * r, 1, 2 * r) }, MaterialOverride = ring.FloorMat, Position = c + new Vector3(0, lift / 2, 0), Scale = new Vector3(1, lift, 1) };
            AddChild(ring.Floor);
            // 四辺の塀。
            foreach (var (dx, dz, sx, sz) in new[] { (0f, -r, 2 * r + thick, thick), (0f, r, 2 * r + thick, thick), (-r, 0f, thick, 2 * r + thick), (r, 0f, thick, 2 * r + thick) })
            {
                var m = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(sx, 1, sz) }, MaterialOverride = ring.Wall, Position = c + new Vector3(dx, 0, dz) };
                AddChild(m); ring.Walls.Add(m);
            }
            void Put(Node3D n, Vector3 at, float height, float yaw = 0) { n.Position = new Vector3(at.X, c.Y + lift, at.Z); n.Rotation = new Vector3(0, yaw, 0); AddChild(n); ring.Buildings.Add((n, height)); }
            // 四隅の隅櫓。
            foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }) Put(House(13, h + 7, 13, ring.Plaster, ring.Roof), c + new Vector3(sx * r, 0, sz * r), h + 7);
            // 櫓門：攻め手の陣の側。
            var g = World.GatePoint(i); var gp = ToScene(g.X, g.Y); var dir = gp - c; dir.Y = 0; dir = dir.LengthSquared() > 0.01f ? dir.Normalized() : Vector3.Back;
            // 城門はいちばん近い辺の上に置く（塀は東西南北に沿っている）。
            var onWall = Math.Abs(dir.X) > Math.Abs(dir.Z) ? new Vector3(Math.Sign(dir.X) * r, 0, Math.Clamp(dir.Z / Math.Abs(dir.X), -0.7f, 0.7f) * r) : new Vector3(Math.Clamp(dir.X / Math.Abs(dir.Z), -0.7f, 0.7f) * r, 0, Math.Sign(dir.Z) * r);
            bool alongX = Math.Abs(dir.X) <= Math.Abs(dir.Z);
            Put(House(alongX ? 26 : 12, h + 4, alongX ? 12 : 26, ring.Plaster, ring.GateRoof), c + onWall, h + 4);
            // 層の中の屋敷と蔵（本丸の中は、もとからある城の建物）。塀の内側に沿って並べる。
            if (i < layers - 1)
            {
                float inner = ProtoWorld.LayerRadius(layers, i + 1) / 100f, mid = (r + inner) / 2, band = r - inner;
                int k = 0;
                foreach (var (ax, az) in new[] { (-0.55f, -1f), (0.45f, -1f), (-1f, -0.4f), (-1f, 0.5f), (1f, -0.5f), (1f, 0.35f), (-0.35f, 1f), (0.6f, 1f) })
                {
                    var at = c + new Vector3(ax * mid, 0, az * mid);
                    if (new Vector2(at.X - (c + onWall).X, at.Z - (c + onWall).Z).Length() < 30) continue; // 城門の前は空ける
                    bool wide = Math.Abs(az) >= 0.99f; float bw = Math.Min(band * 0.5f, k % 3 == 0 ? 20 : 14), bd = Math.Min(band * 0.4f, 11);
                    Put(House(wide ? bw : bd, k % 3 == 0 ? 6 : 8, wide ? bd : bw, ring.Plaster, ring.Roof), at, 8); k++;
                }
            }
            castleRings.Add(ring);
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
            var ring = castleRings[i]; bool down = siege != null && i < lost, now = siege != null && i == lost && World.War.Result == "";
            float h = down ? 2.5f : ring.Height;
            foreach (var m in ring.Walls) { m.Scale = new Vector3(1, h, 1); m.Position = m.Position with { Y = c.Y + h / 2 }; }
            ring.Wall.AlbedoColor = down ? Burnt : WallColor; ring.Plaster.AlbedoColor = down ? Burnt : PlasterColor; ring.Roof.AlbedoColor = down ? Burnt : RoofColor;
            ring.GateRoof.AlbedoColor = down ? Burnt : now ? new Color(1, 0.8f, 0.3f) : RoofColor;
            ring.FloorMat.AlbedoColor = down ? EarthColor.Darkened(0.35f) : EarthColor;
            foreach (var (n, _) in ring.Buildings) n.Scale = new Vector3(1, down ? 0.3f : 1, 1); // 焼け落ちる
        }
        // 城の欄：攻城の間、本人が城の近く（1km）にいるときだけ。
        bool show = siege != null && siege.Layer < siege.Layers && World.War.Result == "" && c.DistanceTo(ToScene(World.Player.X, World.Player.Y)) <= 1000;
        castleLabel!.Visible = castleBar!.Visible = show; if (!show) return;
        int men = World.Garrison.Sum(u => u.Soldiers);
        string key = $"{siege!.Layer}|{siege.Gate}|{men}|{World.CastleMorale}";
        if (key != castleKey)
        {
            castleKey = key;
            castleLabel.Text = $"{ProtoWorld.LayerName(siege.Layers, siege.Layer)}　耐久{siege.Gate}　兵{men}　士気{World.CastleMorale}";
            var img = Image.CreateEmpty(100, 8, false, Image.Format.Rgba8); img.Fill(new Color(0.08f, 0.08f, 0.08f, 0.9f));
            img.FillRect(new Rect2I(1, 1, Math.Max(1, 98 * Math.Clamp(siege.Gate, 0, 100) / 100), 6), new Color(0.85f, 0.85f, 0.9f));
            castleBar.Texture = ImageTexture.CreateFromImage(img);
        }
        var top = c + new Vector3(0, 30 + distance * 0.05f, 0);
        castleLabel.Position = top; castleBar.Position = top - (Camera?.GlobalBasis.Y ?? Vector3.Up) * (distance * 0.03f);
    }
}
