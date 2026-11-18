using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HakutaiKagami.Core.Proto;

// 試作版の軽い土台（2026-11-18 ユーザー決定「軽い土台で新しく」）。
// 地図・拠点・地形・人はデータ（Core/Data/proto-map-v1.json）で書き、ここで格子に焼く。
// 長さは cm、時間は秒。同じデータと同じ入力から必ず同じ格子と同じ結果になる。

public enum Ground : byte { Grass, Road, Forest, Mountain, Water, Ford, Site }

public sealed record ProtoHouse(string Id, string Name, string Head);
public sealed record ProtoSite(string Id, string Name, string Kind, string House, int X, int Y, int RadiusM);
public sealed record ProtoEllipse(int X, int Y, int Rx, int Ry, int PeakM = 0);
public sealed record ProtoLine(int WidthM, int[][] Points);
public sealed record ProtoCircle(int X, int Y, int RadiusM);
public sealed record ProtoTerrain(ProtoEllipse[] Mountains, ProtoEllipse[] Forests, ProtoLine[] Rivers, ProtoCircle[] Fords, ProtoLine[] Roads);
public sealed record ProtoPersonData(string Id, string Name, string House, string Role, string Home, int Age, string[]? Visits = null, bool Player = false,
    int Command = 50, int Valor = 50, int Wits = 50, int Loyalty = 70, string Temper = "普通", int Soldiers = 0, string[]? Skills = null);
public sealed record ProtoPosition(string Id, string Name, int X, int Y);
public sealed record ProtoMapData(int Schema, string Name, int WidthM, int HeightM, int CellM, int StartHour, ProtoHouse[] Houses, ProtoSite[] Sites, ProtoTerrain Terrain, ProtoPersonData[] People, ProtoPosition[]? Positions = null);

public sealed class ProtoMap
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public ProtoMapData Data { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int CellCm { get; }
    readonly Ground[] ground;
    readonly int[] heightCm;

    public ProtoMap(ProtoMapData data)
    {
        Data = data;
        if (data.Schema != 1 || data.CellM <= 0 || data.WidthM % data.CellM != 0 || data.HeightM % data.CellM != 0) throw new InvalidDataException("試作の地図の大きさが不正です");
        Columns = data.WidthM / data.CellM; Rows = data.HeightM / data.CellM; CellCm = data.CellM * 100;
        ground = new Ground[Columns * Rows]; heightCm = new int[Columns * Rows];
        var ids = new HashSet<string>();
        foreach (var s in data.Sites) if (!ids.Add(s.Id)) throw new InvalidDataException("拠点の id が重なっています：" + s.Id);
        foreach (var p in data.People) { if (!ids.Add(p.Id)) throw new InvalidDataException("人の id が重なっています：" + p.Id); if (data.Sites.All(s => s.Id != p.Home)) throw new InvalidDataException("住む拠点がありません：" + p.Id); }
        if (data.People.Count(p => p.Player) != 1) throw new InvalidDataException("操作する人は一人です");
        Bake();
    }

    public static ProtoMap Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ProtoMapV1") ?? throw new InvalidDataException("試作の地図がありません");
        return new ProtoMap(JsonSerializer.Deserialize<ProtoMapData>(stream, Json) ?? throw new InvalidDataException("試作の地図を読めません"));
    }

    void Bake()
    {
        var t = Data.Terrain;
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                double x = (c + 0.5) * Data.CellM, y = (r + 0.5) * Data.CellM;
                var g = Ground.Grass; double h = 0;
                foreach (var m in t.Mountains) { double d = Norm(x, y, m); if (d < 1) { h = Math.Max(h, m.PeakM * (1 - d * d)); g = Ground.Mountain; } }
                foreach (var f in t.Forests) if (Norm(x, y, f) < 1 && g == Ground.Grass) g = Ground.Forest;
                foreach (var w in t.Rivers) if (NearLine(x, y, w)) g = Ground.Water;
                foreach (var f in t.Fords) if (g == Ground.Water && Dist(x, y, f.X, f.Y) <= f.RadiusM) g = Ground.Ford;
                foreach (var road in t.Roads) if (NearLine(x, y, road)) g = Ground.Road;
                foreach (var s in Data.Sites) if (Dist(x, y, s.X, s.Y) <= s.RadiusM) g = Ground.Site;
                ground[r * Columns + c] = g; heightCm[r * Columns + c] = (int)Math.Round(h * 100);
            }
    }

    static double Norm(double x, double y, ProtoEllipse e) => Math.Sqrt(Math.Pow((x - e.X) / e.Rx, 2) + Math.Pow((y - e.Y) / e.Ry, 2));
    static double Dist(double x, double y, double ax, double ay) => Math.Sqrt((x - ax) * (x - ax) + (y - ay) * (y - ay));
    static bool NearLine(double x, double y, ProtoLine line)
    {
        for (int i = 0; i + 1 < line.Points.Length; i++)
        {
            double ax = line.Points[i][0], ay = line.Points[i][1], bx = line.Points[i + 1][0], by = line.Points[i + 1][1];
            double dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy;
            double k = len2 == 0 ? 0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / len2, 0, 1);
            if (Dist(x, y, ax + k * dx, ay + k * dy) <= line.WidthM / 2.0) return true;
        }
        return false;
    }

    public bool Inside(int c, int r) => c >= 0 && r >= 0 && c < Columns && r < Rows;
    public Ground GroundAt(int c, int r) => ground[r * Columns + c];
    public int HeightAt(int c, int r) => heightCm[r * Columns + c];
    public (int C, int R) CellOf(long xCm, long yCm) => ((int)Math.Clamp(xCm / CellCm, 0, Columns - 1), (int)Math.Clamp(yCm / CellCm, 0, Rows - 1));
    public (long X, long Y) Center(int c, int r) => (c * (long)CellCm + CellCm / 2, r * (long)CellCm + CellCm / 2);
    public Ground GroundAtCm(long x, long y) { var (c, r) = CellOf(x, y); return GroundAt(c, r); }
    public int HeightAtCm(long x, long y) { var (c, r) = CellOf(x, y); return HeightAt(c, r); }
    public ProtoSite Site(string id) => Data.Sites.First(s => s.Id == id);
    public ProtoSite? SiteAt(long xCm, long yCm) => Data.Sites.FirstOrDefault(s => Dist(xCm / 100.0, yCm / 100.0, s.X, s.Y) <= s.RadiusM);

    // 歩きの速さ（cm/秒）。今の本線の決まり（道2m/秒・草1m/秒・森0.5m/秒・斜面0.4m/秒・水は通れない）に合わせる。
    public static int Speed(Ground g) => g switch { Ground.Road or Ground.Site => 200, Ground.Grass => 100, Ground.Forest => 50, Ground.Mountain => 40, Ground.Ford => 50, _ => 0 };
    public static string Name(Ground g) => g switch { Ground.Road => "道", Ground.Site => "拠点", Ground.Grass => "野", Ground.Forest => "森", Ground.Mountain => "山", Ground.Ford => "浅瀬", _ => "川" };
}
