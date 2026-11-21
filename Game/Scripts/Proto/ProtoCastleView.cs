using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using HakutaiKagami.Core.Proto;

// 試作版の城の見え方（2026-11-20 ユーザーの図、2026-11-21 印「色は戻す。城の構造に見えるように建造物を」「作りこみがあまい。三の丸とかにみえない」）。
// 決まりは Core/Proto/ProtoSiege.cs。輪郭式の城として、層ごとに次を置く（三層なら外から 三の丸・二の丸・本丸）。
// ・堀（水）と、城門の前の橋。　・石垣の上に白壁と瓦の塀（内の層ほど石垣が高い）。　・四隅に二重の隅櫓。　・攻め手の陣の側に櫓門（下は門、上は櫓）。
// ・三の丸：侍屋敷の並びと蔵。　・二の丸：御殿（大きな屋根の建物）と蔵。　・本丸：隅に三重の天守（真ん中は、もとからある城の館）。　・松。
// ・層の名前の札（三の丸・二の丸・本丸）を、その層の上に出す。
// 城の形は、層の数（地図のデータ。1〜5）から一度だけ組み立てて持っておく（毎回は描かない）。層が増えるごとに、外へ一つずつ曲輪が足される：
//   内から 本丸（天守）・二の丸（御殿）・三の丸（侍屋敷）・外曲輪（長屋）・総構（町屋と畑）。
// 攻城の間：今守っている層の櫓門のまわりに、明るい枠がゆっくり点滅する（門の色はほかと同じ）。失った層は、塀も建物も焼け落ちて低く黒くなる。
// 城内の武将と兵は、合戦の姿のちびコマで、今守っている層の城門の両脇の塀の上に立つ（打ち合っている間は打つ動き）。
// 城の名前の下に、今の層・その耐久・城の兵・士気と、耐久の棒を出す。
public partial class ProtoGame
{
    const string CastleSite = "iwakiri-castle";
    sealed class Ring
    {
        public Node3D Root = null!; public MeshInstance3D Floor = null!; public Label3D Name = null!;
        public StandardMaterial3D Stone = null!, Plaster = null!, Tile = null!, Wood = null!, Leaf = null!, FloorMat = null!;
        public Vector3 Gate; public float WallTop, GateHeight; // 城門の場所（城の真ん中から）・塀の上の高さ・櫓門の高さ
    }
    readonly List<Ring> castleRings = new(); Label3D? castleLabel; Sprite3D? castleBar; string castleKey = "";
    MeshInstance3D? gateGlow; StandardMaterial3D? gateGlowMat; Vector3 castleGateAxis, castleGateSide; bool castleGateOnX;
    readonly Dictionary<string, DotOfficerActor> wallOfficers = new(); readonly List<DotOfficerActor> wallSoldiers = new();
    static readonly Color StoneColor = new(0.56f, 0.55f, 0.52f), PlasterColor = new(0.94f, 0.92f, 0.87f), TileColor = new(0.24f, 0.25f, 0.3f), WoodColor = new(0.42f, 0.3f, 0.2f),
        LeafColor = new(0.2f, 0.38f, 0.22f), EarthColor = new(0.69f, 0.63f, 0.5f), WaterColor = new(0.27f, 0.43f, 0.6f), Burnt = new(0.2f, 0.18f, 0.17f);

    static MeshInstance3D Box(Node3D parent, Vector3 size, Vector3 at, Material mat)
    {
        var m = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = mat, Position = at }; parent.AddChild(m); return m;
    }
    // 四方へ流れる屋根（櫓・天守）。at は屋根の下端の真ん中。
    static void HipRoof(Node3D parent, float w, float d, float h, Vector3 at, Material mat) =>
        parent.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.74f, Height = 1, RadialSegments = 4, Rings = 1 }, MaterialOverride = mat, Position = at + new Vector3(0, h / 2, 0), Rotation = new Vector3(0, Mathf.Pi / 4, 0), Scale = new Vector3(w, h, d) });
    // 切妻の屋根（屋敷・御殿・蔵）。棟は長い辺に沿う。
    static void GableRoof(Node3D parent, float w, float d, float h, Vector3 at, Material mat)
    {
        bool alongX = w >= d;
        parent.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = alongX ? new Vector3(d * 1.15f, h, w * 1.08f) : new Vector3(w * 1.15f, h, d * 1.08f) }, MaterialOverride = mat, Position = at + new Vector3(0, h / 2, 0), Rotation = new Vector3(0, alongX ? Mathf.Pi / 2 : 0, 0) });
    }
    // 建物：壁の箱と切妻の屋根。
    static void Hall(Node3D parent, Vector3 at, float w, float h, float d, Material wall, Material roof)
    {
        Box(parent, new Vector3(w, h, d), at + new Vector3(0, h / 2, 0), wall); GableRoof(parent, w, d, Math.Max(2.5f, Math.Min(w, d) * 0.45f), at + new Vector3(0, h, 0), roof);
    }
    // 重ねの櫓（隅櫓は二重、天守は三重）：石垣の台の上に、上ほど小さい階を重ねる。
    static void Tower(Node3D parent, Vector3 at, float w, int tiers, float baseH, Material stone, Material wall, Material roof)
    {
        Box(parent, new Vector3(w + 3, baseH, w + 3), at + new Vector3(0, baseH / 2, 0), stone);
        float y = baseH, size = w;
        for (int t = 0; t < tiers; t++)
        {
            float h = t == 0 ? 6 : 4.5f;
            Box(parent, new Vector3(size, h, size), at + new Vector3(0, y + h / 2, 0), wall); y += h;
            bool top = t == tiers - 1; float rh = top ? size * 0.45f : 1.6f;
            if (top) HipRoof(parent, size * 1.35f, size * 1.35f, rh, at + new Vector3(0, y, 0), roof);
            else { Box(parent, new Vector3(size * 1.3f, 1.2f, size * 1.3f), at + new Vector3(0, y + 0.6f, 0), roof); y += 1.2f; }
            size *= 0.74f;
        }
    }
    static void Pine(Node3D parent, Vector3 at, Material wood, Material leaf)
    {
        Box(parent, new Vector3(1.2f, 4, 1.2f), at + new Vector3(0, 2, 0), wood);
        parent.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 4.2f, Height = 8, RadialSegments = 6, Rings = 1 }, MaterialOverride = leaf, Position = at + new Vector3(0, 7, 0) });
    }

    void BuildCastleRings()
    {
        var site = World.Map.Site(CastleSite); int layers = World.LayersOf(CastleSite); var c = ToScene(site.X * 100L, site.Y * 100L);
        var water = new StandardMaterial3D { AlbedoColor = WaterColor }; var bridge = new StandardMaterial3D { AlbedoColor = WoodColor }; var dark = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.09f, 0.08f) };
        // 城門の向き（攻め手の陣の側）。塀は東西南北に沿っているので、いちばん近い辺に門を置く。
        var g0 = World.GatePoint(0); var gdir = ToScene(g0.X, g0.Y) - c; gdir.Y = 0; gdir = gdir.LengthSquared() > 0.01f ? gdir.Normalized() : Vector3.Back;
        bool gateOnX = Math.Abs(gdir.X) > Math.Abs(gdir.Z); // 門が東西の辺にある
        Vector3 gateAxis = gateOnX ? new Vector3(Math.Sign(gdir.X), 0, 0) : new Vector3(0, 0, Math.Sign(gdir.Z));
        float gateSlide = Math.Clamp(gateOnX ? gdir.Z / Math.Abs(gdir.X) : gdir.X / Math.Abs(gdir.Z), -0.5f, 0.5f); // 辺の上でのずれ（−0.5〜0.5）
        Vector3 side = gateOnX ? new Vector3(0, 0, 1) : new Vector3(1, 0, 0);
        Vector3 GateAt(float r) => gateAxis * r + side * (gateSlide * r);
        castleGateAxis = gateAxis; castleGateSide = side; castleGateOnX = gateOnX;
        // 門から門へ続く道の上か（建物や木を置かない）。
        bool OnRoad(Vector3 p) { float along = p.Dot(gateAxis), off = Math.Abs(p.Dot(side) - gateSlide * Math.Max(0, along)); return along > 0 && off < 15; }

        for (int i = 0; i < layers; i++)
        {
            float r = ProtoWorld.LayerRadius(layers, i) / 100f, lift = 0.6f * (i + 1), stoneH = 4 + 2.5f * i, moat = i == 0 ? 18 : 9;
            var ring = new Ring
            {
                Stone = new StandardMaterial3D { AlbedoColor = StoneColor }, Plaster = new StandardMaterial3D { AlbedoColor = PlasterColor }, Tile = new StandardMaterial3D { AlbedoColor = TileColor },
                Wood = new StandardMaterial3D { AlbedoColor = WoodColor }, Leaf = new StandardMaterial3D { AlbedoColor = LeafColor }, FloorMat = new StandardMaterial3D { AlbedoColor = EarthColor },
            };
            // 堀：塀の外をぐるりと水。その内に層の地面（内の層ほど一段高い。色はどの層も同じ）。
            Box(this, new Vector3(2 * (r + moat), 0.5f, 2 * (r + moat)), c + new Vector3(0, 0.25f + 0.02f * i + lift - 0.6f, 0), water);
            ring.Floor = Box(this, new Vector3(2 * r, lift, 2 * r), c + new Vector3(0, lift / 2 + 0.05f, 0), ring.FloorMat);
            ring.Root = new Node3D { Position = c + new Vector3(0, lift, 0) }; AddChild(ring.Root);
            // 塀：石垣・白壁・瓦。
            foreach (var (dx, dz, alongX) in new[] { (0f, -r, true), (0f, r, true), (-r, 0f, false), (r, 0f, false) })
            {
                var at = new Vector3(dx, 0, dz); float len = 2 * r;
                Vector3 S(float thick, float h) => alongX ? new Vector3(len + thick, h, thick) : new Vector3(thick, h, len + thick);
                Box(ring.Root, S(9, stoneH), at + new Vector3(0, stoneH / 2 - lift, 0), ring.Stone);
                Box(ring.Root, S(3.5f, 3.2f), at + new Vector3(0, stoneH + 1.6f - lift, 0), ring.Plaster);
                Box(ring.Root, S(5.5f, 1), at + new Vector3(0, stoneH + 3.7f - lift, 0), ring.Tile);
            }
            // 四隅に二重の隅櫓。
            foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }) Tower(ring.Root, new Vector3(sx * r, -lift, sz * r), 11, 2, stoneH + 1, ring.Stone, ring.Plaster, ring.Tile);
            // 櫓門：下は石垣と門、上は櫓。門の前に、堀を渡る橋。
            {
                var at = GateAt(r); ring.Gate = at; ring.WallTop = stoneH + 4.2f; ring.GateHeight = stoneH + 13; var gate = new Node3D { Position = at + new Vector3(0, -lift, 0), Rotation = new Vector3(0, gateOnX ? Mathf.Pi / 2 : 0, 0) }; ring.Root.AddChild(gate);
                Box(gate, new Vector3(30, stoneH + 3, 11), new Vector3(0, (stoneH + 3) / 2, 0), ring.Stone);
                Box(gate, new Vector3(9, stoneH + 1, 11.6f), new Vector3(0, (stoneH + 1) / 2, 0), dark); // 門の口
                Box(gate, new Vector3(26, 5, 9), new Vector3(0, stoneH + 3 + 2.5f, 0), ring.Plaster);
                GableRoof(gate, 30, 11, 4.5f, new Vector3(0, stoneH + 8, 0), ring.Tile);
                Box(this, gateOnX ? new Vector3(moat + 8, 1, 10) : new Vector3(10, 1, moat + 8), c + at + gateAxis * (moat / 2 + 2) + new Vector3(0, lift - 0.1f, 0), bridge);
            }
            if (i < layers - 1)
            {
                // 層の中：塀の内側に沿って建物を並べる。三の丸＝侍屋敷と蔵、二の丸＝御殿と蔵。
                float inner = ProtoWorld.LayerRadius(layers, i + 1) / 100f + 9 + 6, outer = r - 8, band = outer - inner, mid = (inner + outer) / 2;
                int depth = layers - i; bool samurai = depth >= 3; int k = 0; // depth：内から数えた層（1＝本丸）
                foreach (var (ax, az) in new[] { (0f, -1f), (0f, 1f), (-1f, 0f), (1f, 0f) })
                {
                    var axis = new Vector3(ax, 0, az); var along = new Vector3(Math.Abs(az), 0, Math.Abs(ax));
                    float step = depth >= 5 ? 30 : depth == 4 ? 17 : samurai ? 21 : 46; // 総構はまばら、外曲輪は長屋が詰まって並ぶ
                    for (float t = -mid + 4; t <= mid - 4; t += step, k++)
                    {
                        var at = axis * mid + along * (t + step / 2); if (Math.Abs(at.Dot(along)) > mid - 6 || OnRoad(at)) continue;
                        bool houseAlongX = az != 0;
                        if (depth >= 5)
                        {
                            // 総構：町屋と畑。
                            if (k % 3 == 1) Box(ring.Root, new Vector3(houseAlongX ? 18 : 12, 0.3f, houseAlongX ? 12 : 18), at + new Vector3(0, 0.15f, 0), ring.Leaf);
                            else Hall(ring.Root, at, houseAlongX ? 11 : 8, 3.6f, houseAlongX ? 8 : 11, ring.Wood, ring.Wood);
                        }
                        else if (depth == 4)
                        {
                            // 外曲輪：足軽の長屋。
                            Hall(ring.Root, at, houseAlongX ? 13 : 7, 3.4f, houseAlongX ? 7 : 13, ring.Wood, ring.Tile);
                            if (k % 5 == 2) Pine(ring.Root, at + along * 8.5f + axis * (band * 0.3f), ring.Wood, ring.Leaf);
                        }
                        else if (samurai)
                        {
                            if (k % 5 == 3) Hall(ring.Root, at, houseAlongX ? 8 : 9, 6.5f, houseAlongX ? 9 : 8, ring.Plaster, ring.Tile);                       // 蔵
                            else Hall(ring.Root, at, houseAlongX ? 15 : Math.Min(band * 0.55f, 10), 4.2f, houseAlongX ? Math.Min(band * 0.55f, 10) : 15, ring.Wood, ring.Tile); // 侍屋敷
                            if (k % 4 == 1) Pine(ring.Root, at + along * 10.5f + axis * (band * 0.3f), ring.Wood, ring.Leaf);
                        }
                        else
                        {
                            if (k % 3 == 2) Hall(ring.Root, at, houseAlongX ? 9 : 10, 7, houseAlongX ? 10 : 9, ring.Plaster, ring.Tile);                             // 蔵
                            else Hall(ring.Root, at, houseAlongX ? 34 : Math.Min(band * 0.6f, 14), 6, houseAlongX ? Math.Min(band * 0.6f, 14) : 34, ring.Plaster, ring.Tile); // 御殿
                            if (k % 2 == 0) Pine(ring.Root, at + along * 22 + axis * (band * 0.25f), ring.Wood, ring.Leaf);
                        }
                    }
                }
            }
            else
            {
                // 本丸：門と反対の隅に三重の天守。
                var corner = -gateAxis * (r - 17) + side * (gateSlide <= 0 ? 1 : -1) * (r - 17);
                Tower(ring.Root, corner + new Vector3(0, -lift, 0), 15, 3, stoneH + 4, ring.Stone, ring.Plaster, ring.Tile);
            }
            // 層の名前の札。
            float nameR = i < layers - 1 ? (r + ProtoWorld.LayerRadius(layers, i + 1) / 100f) / 2 : r * 0.45f;
            ring.Name = new Label3D { Text = ProtoWorld.LayerName(layers, i), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0007f, FontSize = 30, OutlineSize = 8, OutlineModulate = new Color(0, 0, 0, 0.9f), Modulate = new Color(1, 0.95f, 0.8f), NoDepthTest = true, RenderPriority = 23, Position = c + (-gateAxis * nameR * (i < layers - 1 ? 1 : 0)) + side * (i < layers - 1 ? 0 : -nameR * 0.6f) + new Vector3(0, 26, 0) };
            AddChild(ring.Name); castleRings.Add(ring);
        }
        castleLabel = new Label3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0009f, FontSize = 30, OutlineSize = 8, OutlineModulate = new Color(0, 0, 0, 0.9f), NoDepthTest = true, RenderPriority = 24, Visible = false };
        castleBar = new Sprite3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0016f, NoDepthTest = true, Shaded = false, TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, RenderPriority = 24, Visible = false };
        AddChild(castleLabel); AddChild(castleBar);
    }

    // 今守っている層の櫓門を、明るい枠の点滅で示す（門の色は変えない）。
    void DrawGateGlow(ProtoSiege? siege, Vector3 c)
    {
        if (gateGlow == null)
        {
            gateGlowMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(1, 0.95f, 0.6f, 0.3f), CullMode = BaseMaterial3D.CullModeEnum.Disabled };
            gateGlow = new MeshInstance3D { Mesh = new BoxMesh { Size = Vector3.One }, MaterialOverride = gateGlowMat, Visible = false }; AddChild(gateGlow);
        }
        bool on = siege != null && siege.Layer < castleRings.Count && World.War.Result == "";
        gateGlow.Visible = on; if (!on) return;
        var ring = castleRings[siege!.Layer]; float pulse = 0.5f + 0.5f * MathF.Sin(Time.GetTicksMsec() / 380f), grow = 1 + 0.06f * pulse;
        gateGlowMat!.AlbedoColor = new Color(1, 0.95f, 0.6f, 0.14f + 0.26f * pulse);
        gateGlow.Position = c + ring.Gate + new Vector3(0, ring.GateHeight / 2 + ring.Root.Position.Y - c.Y, 0);
        gateGlow.Scale = (castleGateOnX ? new Vector3(15, ring.GateHeight + 4, 36) : new Vector3(36, ring.GateHeight + 4, 15)) * grow;
    }
    // 城内の武将と兵を、今守っている層の城門の両脇の塀の上に立たせる（合戦の姿のちびコマ）。
    void DrawWallGuards(ProtoSiege? siege, Vector3 c)
    {
        var garrison = World.Garrison.OrderByDescending(u => u.Id == World.HeadOf("iwakiri")).ThenBy(u => u.Id).ToArray();
        int layer = Math.Min(siege?.Layer ?? 0, castleRings.Count - 1);
        bool show = garrison.Length > 0 && World.War.Result == "" && World.War.Phase is "行軍" or "合戦" && c.DistanceTo(ToScene(World.Player.X, World.Player.Y)) <= 1000;
        var ring = castleRings[layer]; float px = Math.Max(0.12f, distance / 380f); string anim = World.CastleFighting ? "attack" : "idle"; double t = Time.GetTicksMsec() / 1000.0;
        float y = c.Y + ring.WallTop; // 塀の瓦の上
        Vector3 Spot(int n) { var at = c + ring.Gate + castleGateSide * ((n % 2 == 0 ? 1 : -1) * (24 + 9 * (n / 2))); at.Y = y; return at; }
        foreach (var (id, d) in wallOfficers) d.Visible = false;
        int slot = 0;
        if (show) foreach (var p in garrison)
        {
            if (!wallOfficers.TryGetValue(p.Id, out var d)) { d = new DotOfficerActor { OnTop = true, Solid = true }; AddChild(d); wallOfficers[p.Id] = d; }
            if (!d.Apply(SpriteKey(World, p))) continue;
            d.Visible = true; d.PixelSize = px * 1.2f; d.Position = Spot(slot++); d.Freeze(anim, t + slot * 0.4); d.Face(castleGateAxis, Camera);
        }
        int want = show ? Math.Clamp(garrison.Sum(u => u.Soldiers) / 12, 2, 12) : 0; var mix = show ? TroopMix(garrison[0]) : [];
        while (wallSoldiers.Count < want) { var d = new DotOfficerActor { OnTop = true, Solid = true }; AddChild(d); wallSoldiers.Add(d); }
        for (int i = 0; i < wallSoldiers.Count; i++)
        {
            var d = wallSoldiers[i]; d.Visible = i < want; if (i >= want) continue;
            d.Apply($"soldier/ashigaru/{mix[i % mix.Length]}/war/{ColorOf("iwakiri")}");
            d.PixelSize = px * FollowerSize; d.Position = Spot(slot + i); d.Freeze(anim, t + i * 0.21); d.Face(castleGateAxis, Camera);
        }
    }

    void UpdateCastleView()
    {
        if (castleRings.Count == 0) BuildCastleRings();
        var siege = World.War.Siege; int lost = siege?.Layer ?? 0; var site = World.Map.Site(CastleSite); var c = ToScene(site.X * 100L, site.Y * 100L);
        bool near = distance < 1100;
        for (int i = 0; i < castleRings.Count; i++)
        {
            var ring = castleRings[i]; bool down = siege != null && i < lost, now = siege != null && i == lost && World.War.Result == "";
            ring.Root.Scale = new Vector3(1, down ? 0.3f : 1, 1); // 焼け落ちる
            ring.Stone.AlbedoColor = down ? Burnt.Lightened(0.12f) : StoneColor; ring.Plaster.AlbedoColor = down ? Burnt : PlasterColor; ring.Tile.AlbedoColor = down ? Burnt : TileColor;
            ring.Wood.AlbedoColor = down ? Burnt : WoodColor; ring.Leaf.AlbedoColor = down ? Burnt : LeafColor;
            ring.FloorMat.AlbedoColor = down ? EarthColor.Darkened(0.4f) : EarthColor;
            ring.Name.Visible = near; ring.Name.Modulate = down ? new Color(0.6f, 0.6f, 0.6f) : now ? new Color(1, 0.85f, 0.4f) : new Color(1, 0.95f, 0.8f);
        }
        DrawGateGlow(siege, c); DrawWallGuards(siege, c);
        // 城の欄：攻城の間、本人が城の近く（1km）にいるときだけ。
        bool show = siege != null && siege.Layer < siege.Layers && World.War.Result == "" && c.DistanceTo(ToScene(World.Player.X, World.Player.Y)) <= 1000;
        castleLabel!.Visible = castleBar!.Visible = show; if (!show) return;
        int men = World.Garrison.Sum(u => u.Soldiers);
        string key = $"{siege!.Layer}|{siege.Gate}|{men}|{World.CastleMorale}";
        if (key != castleKey)
        {
            castleKey = key;
            castleLabel.Text = $"耐久{siege.Gate}　兵{men}　士気{World.CastleMorale}";
            var img = Image.CreateEmpty(100, 8, false, Image.Format.Rgba8); img.Fill(new Color(0.08f, 0.08f, 0.08f, 0.9f));
            img.FillRect(new Rect2I(1, 1, Math.Max(1, 98 * Math.Clamp(siege.Gate, 0, 100) / 100), 6), new Color(0.85f, 0.85f, 0.9f));
            castleBar.Texture = ImageTexture.CreateFromImage(img);
        }
        var top = c + new Vector3(0, 55 + distance * 0.05f, 0);
        castleLabel.Position = top; castleBar.Position = top - (Camera?.GlobalBasis.Y ?? Vector3.Up) * (distance * 0.03f);
    }
}
