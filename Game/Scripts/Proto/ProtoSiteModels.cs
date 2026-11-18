using Godot;
using System;

// 拠点の立体（2026-11-18）。箱・屋根・石垣などの部品を組み立てて、館・支城・本城を作る。
// 色は武将のドット絵と同じ色の表。面には粗い模様をぼかさずに貼り、外側に濃いふちを付ける。
// 向き：+Z が正面（門のある側）。1 単位 = 1m。史実の年代には合わせない（城の格を見せる形）。
public static class ProtoSiteModels
{
    static Color C(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
    static readonly Color Ink = C(30, 22, 32), Gold = C(236, 184, 56);
    static readonly Color PlasterL = C(246, 247, 249), PlasterM = C(236, 232, 224), PlasterD = C(199, 197, 199);
    static readonly Color TileM = C(100, 104, 116), TileD = C(60, 54, 68);
    static readonly Color WoodL = C(168, 114, 72), WoodM = C(112, 70, 44), WoodD = C(64, 52, 60);
    static readonly Color ThatchM = C(204, 168, 100), ShingleM = C(142, 114, 90), ShingleD = C(98, 78, 66);
    static readonly Color StoneL = C(188, 188, 180), StoneM = C(146, 146, 142);
    static readonly Color EarthL = C(190, 160, 110), EarthM = C(150, 120, 84), EarthD = C(104, 80, 60);
    static readonly Color GrassM = C(84, 140, 76), PineL = C(96, 150, 84), PineM = C(52, 108, 72);
    static readonly Color RoadM = C(196, 172, 132), WaterM = C(92, 150, 204), WaterL = C(176, 212, 236);
    static readonly Color RedM = C(176, 36, 40), HiwadaM = C(98, 72, 58), DaubL = C(226, 204, 160), Indigo = C(52, 72, 128), ThatchD = C(150, 112, 64), GrassL = C(132, 176, 88);

    public static readonly string[] CastleKinds = ["yakata", "shijo", "honjo"];
    public static readonly string[] Kinds = ["yakata", "shijo", "honjo", "tera", "jinja", "machi", "mura"];
    public static float Height(string kind) => kind switch { "honjo" => 58, "shijo" => 34, "tera" => 30, "jinja" => 22, "machi" => 18, "mura" => 14, _ => 20 };

    static ImageTexture? grain;
    static ImageTexture Grain()
    {
        if (grain != null) return grain;
        var image = Image.CreateEmpty(16, 16, false, Image.Format.Rgb8);
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { float v = 0.82f + ((x * 73856093 ^ y * 19349663 ^ (x * y + 7) * 83492791) & 0x7fffffff) % 5 * 0.045f; image.SetPixel(x, y, new Color(v, v, v)); }
        return grain = ImageTexture.CreateFromImage(image);
    }

    sealed class Builder
    {
        readonly SurfaceTool body = new(), edge = new();
        Vector3 center; public bool Rim = true;
        const float Tile = 16 * 1.4f, RimM = 0.45f;
        public Builder() { body.Begin(Mesh.PrimitiveType.Triangles); edge.Begin(Mesh.PrimitiveType.Triangles); }

        void Tri(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            var n = (b - a).Cross(c - a); if (n.LengthSquared() < 1e-6f) return; n = n.Normalized();
            if (n.Dot((a + b + c) / 3 - center) < 0) { (b, c) = (c, b); n = -n; }
            bool flat = Mathf.Abs(n.Y) > 0.7f; Vector3 u = flat ? Vector3.Right : Vector3.Up.Cross(n).Normalized(), v = flat ? Vector3.Back : Vector3.Up;
            foreach (var p in new[] { a, c, b }) // Godot は時計回りが表
            {
                body.SetNormal(n); body.SetColor(color); body.SetUV(new Vector2(p.Dot(u), p.Dot(v)) / Tile); body.AddVertex(p);
                if (Rim) edge.AddVertex(p + new Vector3(Sign(p.X - center.X), Sign(p.Y - center.Y), Sign(p.Z - center.Z)) * RimM);
            }
        }
        static float Sign(float v) => v > 0.01f ? 1 : v < -0.01f ? -1 : 0;
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) { Tri(a, b, c, color); Tri(a, c, d, color); }

        // 底の中心 c0 から、下の半幅 (bx,bz)・上の半幅 (tx,tz)・高さ h の台。箱もこれで作る。
        public void Frustum(Vector3 c0, float bx, float bz, float tx, float tz, float h, Color side, Color? top = null)
        {
            center = c0 + Vector3.Up * h / 2;
            Vector3 B(int sx, int sz) => c0 + new Vector3(sx * bx, 0, sz * bz); Vector3 T(int sx, int sz) => c0 + new Vector3(sx * tx, h, sz * tz);
            Quad(B(-1, 1), B(1, 1), T(1, 1), T(-1, 1), side); Quad(B(1, -1), B(-1, -1), T(-1, -1), T(1, -1), side);
            Quad(B(1, 1), B(1, -1), T(1, -1), T(1, 1), side); Quad(B(-1, -1), B(-1, 1), T(-1, 1), T(-1, -1), side);
            Quad(T(-1, 1), T(1, 1), T(1, -1), T(-1, -1), top ?? side);
        }
        public void Box(float x0, float y0, float z0, float x1, float y1, float z1, Color side, Color? top = null)
            => Frustum(new Vector3((x0 + x1) / 2, y0, (z0 + z1) / 2), (x1 - x0) / 2, (z1 - z0) / 2, (x1 - x0) / 2, (z1 - z0) / 2, y1 - y0, side, top);

        // 屋根。軒の半幅 (hx,hz)、棟の半分の長さ ridge（0 で四方に流れる、長い側の半幅と同じで切妻）。alongZ で棟を奥行きの向きにする。
        public void Roof(Vector3 c0, float hx, float hz, float rise, float ridge, Color color, Color gable, bool alongZ = false)
        {
            center = c0 + Vector3.Up * rise * 0.3f;
            Vector3 P(float along, float y, float across) => c0 + (alongZ ? new Vector3(across, y, along) : new Vector3(along, y, across));
            float la = alongZ ? hz : hx, sa = alongZ ? hx : hz;
            Vector3 e00 = P(-la, 0, -sa), e10 = P(la, 0, -sa), e11 = P(la, 0, sa), e01 = P(-la, 0, sa), r0 = P(-ridge, rise, 0), r1 = P(ridge, rise, 0);
            Quad(e01, e11, r1, r0, color); Quad(e10, e00, r0, r1, color);
            bool flatEnd = ridge >= la - 0.01f;
            Tri(e00, e01, r0, flatEnd ? gable : color); Tri(e11, e10, r1, flatEnd ? gable : color);
            Quad(e00, e10, e11, e01, WoodD);
        }

        // 壁と屋根の建物。ridge < 0 は寄棟（四方に流れる）、gable は切妻。
        public void Building(float cx, float cz, float hx, float hz, float y0, float wallH, float rise, Color wall, Color roof, bool gable = false, bool alongZ = false, float over = 1.6f)
        {
            Box(cx - hx, y0, cz - hz, cx + hx, y0 + wallH, cz + hz, wall);
            float la = (alongZ ? hz : hx) + over, sa = (alongZ ? hx : hz) + over;
            Roof(new Vector3(cx, y0 + wallH, cz), hx + over, hz + over, rise, gable ? la : Math.Max(la - sa * 0.9f, 0), roof, wall, alongZ);
        }

        // 四角い囲い（土塁・塀・柵・濠）。正面（+Z）の中央に幅 gate の切れ目。
        public void Ring(Vector3 c0, float hx, float hz, float t, float h, Color side, Color top, float gate = 0)
        {
            float y0 = c0.Y, y1 = c0.Y + h;
            Box(c0.X - hx, y0, c0.Z - hz, c0.X + hx, y1, c0.Z - hz + t, side, top);
            Box(c0.X - hx, y0, c0.Z - hz + t, c0.X - hx + t, y1, c0.Z + hz - t, side, top);
            Box(c0.X + hx - t, y0, c0.Z - hz + t, c0.X + hx, y1, c0.Z + hz - t, side, top);
            if (gate <= 0) { Box(c0.X - hx, y0, c0.Z + hz - t, c0.X + hx, y1, c0.Z + hz, side, top); return; }
            Box(c0.X - hx, y0, c0.Z + hz - t, c0.X - gate / 2, y1, c0.Z + hz, side, top);
            Box(c0.X + gate / 2, y0, c0.Z + hz - t, c0.X + hx, y1, c0.Z + hz, side, top);
        }

        // 窓（四方の壁に、濃い小さな四角）。
        public void Windows(float cx, float cz, float hx, float hz, float y, int countX, int countZ, float size = 1.6f)
        {
            bool rim = Rim; Rim = false;
            for (int n = 0; n < countX; n++) { float x = cx + hx * 2 * ((n + 1f) / (countX + 1) - 0.5f); foreach (int s in new[] { -1, 1 }) Box(x - size / 2, y, cz + s * hz - 0.2f, x + size / 2, y + size, cz + s * hz + 0.2f, Ink); }
            for (int n = 0; n < countZ; n++) { float z = cz + hz * 2 * ((n + 1f) / (countZ + 1) - 0.5f); foreach (int s in new[] { -1, 1 }) Box(cx + s * hx - 0.2f, y, z - size / 2, cx + s * hx + 0.2f, y + size, z + size / 2, Ink); }
            Rim = rim;
        }

        public void Pine(float x, float y, float z, float scale = 1)
        {
            Box(x - 0.7f * scale, y, z - 0.7f * scale, x + 0.7f * scale, y + 3.5f * scale, z + 0.7f * scale, WoodD);
            Frustum(new Vector3(x, y + 3 * scale, z), 4.2f * scale, 4.2f * scale, 1.2f * scale, 1.2f * scale, 5 * scale, PineM, PineL);
            Frustum(new Vector3(x, y + 7 * scale, z), 3 * scale, 3 * scale, 0.2f, 0.2f, 5.5f * scale, PineM, PineL);
        }

        public void Banner(float x, float y, float z, Color color)
        {
            Box(x - 0.3f, y, z - 0.3f, x + 0.3f, y + 12, z + 0.3f, WoodD);
            Box(x + 0.3f, y + 5.5f, z - 0.15f, x + 3.6f, y + 11.5f, z + 0.15f, color);
        }

        // 井楼・火の見（4本の柱の上に小さな小屋）。
        public void Tower(float x, float y, float z, float h, Color roof)
        {
            foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) Box(x + sx * 2.6f - 0.5f, y, z + sz * 2.6f - 0.5f, x + sx * 2.6f + 0.5f, y + h, z + sz * 2.6f + 0.5f, WoodD);
            Box(x - 3.6f, y + h - 0.5f, z - 3.6f, x + 3.6f, y + h + 0.5f, z + 3.6f, WoodM);
            Building(x, z, 2.8f, 2.8f, y + h + 0.5f, 3.2f, 2.6f, WoodL, roof, over: 1.4f); Windows(x, z, 2.8f, 2.8f, y + h + 1.3f, 1, 1, 1.4f);
        }

        // 通りに面した暖簾などの色の布。
        public void Cloth(float x0, float y0, float z0, float x1, float y1, float z1, Color color) { bool rim = Rim; Rim = false; Box(x0, y0, z0, x1, y1, z1, color); Rim = rim; }

        public Node3D Commit()
        {
            var root = new Node3D();
            root.AddChild(new MeshInstance3D { Mesh = body.Commit(), MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, AlbedoTexture = Grain(), TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, Roughness = 1 } });
            root.AddChild(new MeshInstance3D { Mesh = edge.Commit(), MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Ink, CullMode = BaseMaterial3D.CullModeEnum.Front } });
            return root;
        }
    }

    public static Node3D Build(string kind, Color house)
    {
        var b = new Builder();
        switch (kind)
        {
            case "honjo": Honjo(b, house); break; case "shijo": Shijo(b, house); break;
            case "tera": Tera(b); break; case "jinja": Jinja(b); break; case "machi": Machi(b); break; case "mura": Mura(b); break;
            default: Yakata(b); break;
        }
        return b.Commit();
    }

    // 館：水の濠と土塁の囲い、門、主殿と小さな建物。質素に。
    static void Yakata(Builder b)
    {
        b.Rim = false; b.Ring(new Vector3(0, -0.5f, 0), 31, 27, 5, 1, WaterM, WaterM); b.Rim = true;
        b.Box(-4, -0.5f, 21, 4, 0.8f, 33, EarthM, RoadM);
        b.Ring(Vector3.Zero, 25, 21, 4, 3, EarthM, GrassM, 8);
        b.Box(-4.8f, 0, 18.4f, -3.6f, 5.5f, 20.4f, WoodD); b.Box(3.6f, 0, 18.4f, 4.8f, 5.5f, 20.4f, WoodD);
        b.Roof(new Vector3(0, 5.5f, 19.4f), 6.4f, 2.2f, 1.8f, 6.4f, ShingleM, WoodM);
        b.Building(-4, -7, 10, 5.5f, 0, 4.5f, 4.5f, PlasterM, ShingleM); b.Windows(-4, -7, 10, 5.5f, 1.4f, 4, 1);
        b.Building(12, 6, 4.5f, 6, 0, 3.5f, 3, WoodL, ShingleD, gable: true, alongZ: true);
        b.Pine(-15, 0, 9, 0.9f);
    }

    // 支城：空堀と柵、土を盛った主郭に主殿と井楼、外の郭の小屋、櫓門、幟。木と土の城。
    static void Shijo(Builder b, Color house)
    {
        b.Rim = false; b.Ring(new Vector3(0, -0.5f, 0), 47, 41, 6, 0.9f, EarthD, EarthD); b.Rim = true;
        b.Box(-4, -0.5f, 34, 4, 0.7f, 49, EarthM, RoadM);
        b.Ring(Vector3.Zero, 40, 34, 1.2f, 4.5f, WoodM, WoodL, 9);
        b.Frustum(new Vector3(0, 0, -10), 27, 20, 23, 16, 7, EarthM, GrassM);
        b.Box(-3, 0, 10, 3, 2.3f, 14, EarthM, RoadM); b.Box(-3, 0, 6.5f, 3, 4.6f, 10.5f, EarthM, RoadM); b.Box(-3, 0, 3, 3, 7.1f, 7, EarthM, RoadM);
        b.Ring(new Vector3(0, 7, -10), 22.5f, 15.5f, 1, 3, WoodM, WoodL, 6);
        b.Building(-5, -13, 10, 5.5f, 7, 5, 5, WoodL, ShingleM); b.Windows(-5, -13, 10, 5.5f, 8.6f, 4, 1);
        // 井楼
        b.Tower(15, 7, -18, 12, ShingleD);
        // 外の郭の小屋
        b.Building(-26, 20, 6.5f, 4, 0, 3.5f, 3.2f, WoodL, ThatchM, gable: true);
        b.Building(27, 16, 4.5f, 6.5f, 0, 3.5f, 3.2f, WoodL, ThatchM, gable: true, alongZ: true);
        // 櫓門
        b.Box(-7.5f, 0, 31.6f, -4.5f, 6, 36.4f, WoodM); b.Box(4.5f, 0, 31.6f, 7.5f, 6, 36.4f, WoodM);
        b.Rim = false; b.Box(-4.5f, 0, 33.4f, 4.5f, 6, 34.6f, WoodD); b.Rim = true;
        b.Building(0, 34, 8, 3, 6, 3.4f, 3, WoodL, ShingleM); b.Windows(0, 34, 8, 3, 6.9f, 3, 0, 1.4f);
        b.Banner(-13, 0, 36, house); b.Banner(13, 0, 36, house);
        b.Pine(-31, 0, -22); b.Pine(32, 0, -8, 0.85f);
    }

    // 本城：水堀、石垣の台、白い土塀と四隅の櫓、櫓門、御殿、天守台の上に三重の天守と金の鯱。
    static void Honjo(Builder b, Color house)
    {
        b.Rim = false; b.Ring(new Vector3(0, -0.5f, 0), 58, 52, 8, 1, WaterM, WaterM); b.Rim = true;
        b.Box(-5, -0.5f, 43, 5, 1, 60, StoneM, RoadM);
        b.Frustum(Vector3.Zero, 50, 44, 46, 40, 6, StoneM, EarthL);
        b.Ring(new Vector3(0, 6, 0), 46, 40, 1.6f, 2.8f, PlasterM, PlasterM, 12);
        b.Ring(new Vector3(0, 8.8f, 0), 46.4f, 40.4f, 2.4f, 0.8f, TileM, TileM, 12);
        foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
        {
            bool front = sz > 0; float h = front ? 7.5f : 5.5f;
            b.Building(sx * 41, sz * 35, 5, 5, 6, h, 3.6f, PlasterL, TileM); b.Windows(sx * 41, sz * 35, 5, 5, 6 + h - 3, 2, 2, 1.4f);
        }
        // 石段と櫓門
        b.Box(-5, 0, 44, 5, 2, 48.5f, StoneL); b.Box(-5, 0, 41.5f, 5, 4, 45.5f, StoneL); b.Box(-5, 0, 39, 5, 6.1f, 42.5f, StoneL);
        b.Box(-10, 6, 36.5f, -6, 11.5f, 42, StoneM); b.Box(6, 6, 36.5f, 10, 11.5f, 42, StoneM);
        b.Rim = false; b.Box(-6, 6, 38.6f, 6, 11.5f, 39.8f, WoodD); b.Rim = true;
        b.Building(0, 39.2f, 10.5f, 3.4f, 11.5f, 4, 3.6f, PlasterL, TileM); b.Windows(0, 39.2f, 10.5f, 3.4f, 12.6f, 4, 0, 1.5f);
        // 天守
        float cx = 9, cz = -12;
        b.Frustum(new Vector3(cx, 6, cz), 20, 17, 15.5f, 13, 10, StoneM, StoneL);
        b.Box(cx - 14, 16, cz - 11.5f, cx + 14, 24, cz + 11.5f, PlasterL); b.Windows(cx, cz, 14, 11.5f, 19, 4, 3);
        b.Frustum(new Vector3(cx, 24, cz), 16.5f, 14, 10.5f, 8.5f, 3.5f, TileM);
        b.Box(cx - 10, 27, cz - 8, cx + 10, 34.5f, cz + 8, PlasterL); b.Windows(cx, cz, 10, 8, 30.4f, 3, 2);
        b.Frustum(new Vector3(cx, 34.5f, cz), 12.5f, 10.5f, 7, 5.5f, 3, TileM);
        b.Box(cx - 6.5f, 37, cz - 5, cx + 6.5f, 43.5f, cz + 5, PlasterL); b.Windows(cx, cz, 6.5f, 5, 39.8f, 2, 1);
        b.Roof(new Vector3(cx, 43.5f, cz), 9, 7.5f, 5.5f, 4.5f, TileM, PlasterL);
        b.Box(cx - 5.2f, 48.8f, cz - 0.6f, cx - 3.8f, 51.1f, cz + 0.6f, Gold); b.Box(cx + 3.8f, 48.8f, cz - 0.6f, cx + 5.2f, 51.1f, cz + 0.6f, Gold);
        // 御殿
        b.Building(-24, 12, 13, 7, 6, 4.5f, 4.6f, PlasterM, TileM); b.Windows(-24, 12, 13, 7, 7.4f, 5, 2, 1.4f);
        b.Building(-30, -16, 7, 10, 6, 4, 4.2f, PlasterM, TileM, alongZ: true);
        b.Banner(-15, 6, 37, house); b.Banner(15, 6, 37, house);
        b.Pine(31, 6, 22); b.Pine(23, 6, 30, 0.8f); b.Pine(-38, 6, 30, 0.9f); b.Pine(36, 6, -30);
    }

    // 寺：築地塀の囲い、山門、石の壇の上の本堂、三重の塔、鐘楼。
    static void Tera(Builder b)
    {
        b.Ring(Vector3.Zero, 28, 24, 1.4f, 2.6f, DaubL, DaubL, 7); b.Ring(new Vector3(0, 2.6f, 0), 28.4f, 24.4f, 2.2f, 0.7f, TileM, TileM, 7);
        b.Box(-4.2f, 0, 21.6f, -3, 5.5f, 24.4f, WoodD); b.Box(3, 0, 21.6f, 4.2f, 5.5f, 24.4f, WoodD);
        b.Building(0, 23, 5, 2, 5.5f, 1.2f, 2.6f, WoodM, TileM, gable: true, over: 1.2f);
        b.Rim = false; b.Box(-1.6f, 0, 2, 1.6f, 0.25f, 21.6f, StoneL); b.Rim = true;
        b.Box(-18, 0, -17, 6, 1.4f, 0, StoneM, StoneL); b.Box(-9, 0, 0, -3, 0.7f, 2, StoneL);
        b.Building(-6, -8.5f, 10, 6.5f, 1.4f, 5, 7.5f, WoodM, TileM, over: 2.4f); b.Windows(-6, -8.5f, 10, 6.5f, 2.6f, 5, 0, 1.8f);
        // 三重の塔
        float x = 17, z = -6;
        b.Box(x - 5, 0, z - 5, x + 5, 1, z + 5, StoneM, StoneL);
        b.Box(x - 3.4f, 1, z - 3.4f, x + 3.4f, 5.5f, z + 3.4f, RedM); b.Frustum(new Vector3(x, 5.5f, z), 6.4f, 6.4f, 3, 3, 2, TileM);
        b.Box(x - 2.9f, 7, z - 2.9f, x + 2.9f, 11, z + 2.9f, RedM); b.Frustum(new Vector3(x, 11, z), 5.6f, 5.6f, 2.5f, 2.5f, 2, TileM);
        b.Box(x - 2.4f, 12.5f, z - 2.4f, x + 2.4f, 16, z + 2.4f, RedM); b.Roof(new Vector3(x, 16, z), 5, 5, 3.4f, 0, TileM, TileM);
        b.Box(x - 0.3f, 19.2f, z - 0.3f, x + 0.3f, 24, z + 0.3f, Gold);
        // 鐘楼
        b.Box(11, 0, 10, 17, 0.8f, 16, StoneM, StoneL);
        foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) b.Box(14 + sx * 2 - 0.4f, 0.8f, 13 + sz * 2 - 0.4f, 14 + sx * 2 + 0.4f, 4.6f, 13 + sz * 2 + 0.4f, WoodD);
        b.Roof(new Vector3(14, 4.6f, 13), 4, 4, 2.6f, 0.8f, TileM, TileM);
        b.Pine(-21, 0, 14); b.Pine(-22, 0, -19, 0.85f); b.Pine(22, 0, -19, 0.9f);
    }

    // 神社：朱の鳥居、石の参道、拝殿、玉垣の中の本殿、まわりの杜。
    static void Jinja(Builder b)
    {
        b.Rim = false; b.Box(-1.6f, 0, -4, 1.6f, 0.25f, 26, StoneL); b.Rim = true;
        foreach (float z in new[] { 22f, 10f })
        {
            float k = z > 15 ? 1 : 0.8f;
            b.Box(-4.2f * k, 0, z - 0.5f, -3.2f * k, 8 * k, z + 0.5f, RedM); b.Box(3.2f * k, 0, z - 0.5f, 4.2f * k, 8 * k, z + 0.5f, RedM);
            b.Box(-5 * k, 5.6f * k, z - 0.35f, 5 * k, 6.4f * k, z + 0.35f, RedM);
            b.Box(-6.2f * k, 8 * k, z - 0.6f, 6.2f * k, 9 * k, z + 0.6f, WoodD);
        }
        b.Box(-9, 0, -9, 9, 0.9f, 2, StoneM, StoneL);
        b.Building(0, -3.5f, 7, 4, 0.9f, 3.8f, 4.4f, PlasterM, HiwadaM, gable: true, over: 2);
        foreach (int sx in new[] { -1, 1 }) b.Box(sx * 7 - 0.4f, 0.9f, 0.1f, sx * 7 + 0.4f, 4.7f, 0.9f, RedM);
        b.Ring(new Vector3(0, 0, -17), 8, 7, 0.6f, 2, RedM, RedM, 4);
        b.Box(-5, 0, -21, 5, 2.2f, -13, StoneM, StoneL);
        b.Building(0, -17, 3.6f, 3, 2.2f, 3.6f, 4.2f, RedM, HiwadaM, gable: true, alongZ: true, over: 1.6f);
        b.Box(-0.3f, 10, -20.6f, 0.3f, 10.6f, -13.4f, Gold);
        foreach (var (x, z, s) in new[] { (-15f, -20f, 1.3f), (15f, -22f, 1.4f), (-18f, -6f, 1.2f), (18f, -8f, 1.25f), (-14f, 8f, 1.1f), (15f, 9f, 1.15f), (-4f, -29f, 1.35f), (7f, -30f, 1.2f), (-19f, 20f, 1f), (19f, 21f, 1f) }) b.Pine(x, 0, z, s);
    }

    // 町：通りの両側に並ぶ町家（大きさと屋根を変える）、白い蔵、火の見、暖簾。
    static void Machi(Builder b)
    {
        var cloths = new[] { Indigo, RedM, PlasterL, Indigo, WoodD, RedM };
        void Row(float cz, int face, (float X, float Hx, float Hz, float H, Color Roof)[] houses)
        {
            int n = face > 0 ? 0 : 3;
            foreach (var h in houses)
            {
                bool tall = h.H > 5; float z = cz - face * (h.Hz - 5);
                b.Building(h.X, z, h.Hx, h.Hz, 0, h.H, tall ? 3 : 3.4f, tall ? PlasterM : WoodL, h.Roof, gable: true, over: 1.2f);
                float front = z + face * h.Hz;
                b.Cloth(h.X - h.Hx * 0.6f, 1.6f, front + face * 0.1f - 0.15f, h.X + h.Hx * 0.6f, 3, front + face * 0.1f + 0.15f, cloths[n++ % cloths.Length]);
                if (tall) b.Windows(h.X, z, h.Hx, h.Hz, h.H - 1.9f, 3, 0, 1.2f);
            }
        }
        Row(-10, 1, [(-36, 5, 5, 3.6f, ShingleM), (-24.5f, 5.5f, 6, 6.2f, TileM), (-12.5f, 5.5f, 5, 3.4f, ShingleD), (11.5f, 5, 5.5f, 3.6f, ShingleM), (23.5f, 6, 6, 6, TileM), (36, 5, 5, 3.4f, ShingleD)]);
        Row(10, -1, [(-35, 6, 5.5f, 3.4f, ShingleD), (-22.5f, 5, 5, 3.6f, ShingleM), (-11, 5.5f, 6, 6, TileM), (12, 6, 5, 3.4f, ShingleM), (24, 5, 5.5f, 6.2f, TileM), (35, 5, 5, 3.6f, ShingleD)]);
        // 横の通りに沿った家と蔵
        b.Building(-9.5f, -27, 4.5f, 6, 0, 3.4f, 3.2f, WoodL, ShingleM, gable: true, alongZ: true, over: 1.2f); b.Cloth(-5.05f, 1.6f, -30, -4.75f, 3, -24, Indigo);
        b.Building(10, 28, 4.5f, 6.5f, 0, 3.4f, 3.2f, WoodL, ShingleD, gable: true, alongZ: true, over: 1.2f); b.Cloth(5.25f, 1.6f, 25, 5.55f, 3, 31, RedM);
        b.Building(-29, -27, 4, 5, 0, 6.5f, 3.2f, PlasterL, TileM, gable: true, alongZ: true, over: 0.9f); b.Windows(-29, -27, 4, 5, 4, 0, 1, 1.2f);
        b.Building(-20, -28, 3.5f, 4.5f, 0, 6, 3, PlasterL, TileM, gable: true, alongZ: true, over: 0.9f);
        b.Building(28, 28, 5, 4, 0, 6.5f, 3.2f, PlasterL, TileM, gable: true, over: 0.9f); b.Windows(28, 28, 5, 4, 4, 1, 0, 1.2f);
        b.Tower(10, 0, -25, 11, ShingleD);
        b.Box(-1.6f, 0, -1.6f, 1.6f, 1.3f, 1.6f, StoneM, WaterM);
        b.Pine(-30, 0, 30, 0.9f); b.Pine(30, 0, -28); b.Pine(-9, 0, 30, 0.8f);
    }

    // 村：茅葺きの農家、納屋、田、稲むら、木立。
    static void Mura(Builder b)
    {
        b.Rim = false;
        for (int r = 0; r < 2; r++) for (int c = 0; c < 4; c++)
            b.Box(-30 + c * 13, -0.3f, 9 + r * 11, -30 + c * 13 + 12, 0.3f, 9 + r * 11 + 10, EarthM, (r + c) % 3 == 0 ? WaterL : (r + c) % 2 == 0 ? GrassL : GrassM);
        b.Rim = true;
        b.Building(-15, -10, 7.5f, 5, 0, 3, 6.5f, DaubL, ThatchM, over: 1.8f); b.Windows(-15, -10, 7.5f, 5, 0.9f, 3, 0, 1.4f);
        b.Building(7, -17, 5, 6.5f, 0, 3, 6, DaubL, ThatchM, alongZ: true, over: 1.8f);
        b.Building(22, -3, 6, 4.5f, 0, 3, 5.5f, DaubL, ThatchD, over: 1.8f); b.Windows(22, -3, 6, 4.5f, 0.9f, 2, 0, 1.4f);
        b.Building(-27, 0, 3.5f, 3, 0, 2.6f, 2.6f, WoodL, ShingleD, gable: true);
        foreach (var (x, z) in new[] { (-3f, -3f), (1.5f, 0.5f), (12f, 3f) }) b.Frustum(new Vector3(x, 0, z), 2.3f, 2.3f, 0.3f, 0.3f, 4.2f, ThatchM, ThatchD);
        b.Pine(-26, 0, -18); b.Pine(-5, 0, -24, 1.1f); b.Pine(27, 0, -17, 0.9f); b.Pine(31, 0, 6, 0.8f);
    }
}
