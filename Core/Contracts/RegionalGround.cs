namespace HakutaiKagami.Core.Contracts;
// One height/cost definition serves routing, saved positions, mesh vertices and mouse picking.
public static class RegionalGround
{
    public const long Radius = 1000;
    public static long Height(FieldPoint p) => Math.Clamp((p.X - 20000) / 5, 0, 10000);
    public static long Distance(FieldPoint a, FieldPoint b) => (long)Math.Ceiling(Math.Sqrt((double)(a.X - b.X) * (a.X - b.X) + (double)(a.Y - b.Y) * (a.Y - b.Y)));
    public static bool Walkable(FieldPoint p)
    {
        for(int x=(int)Math.Floor((p.X-Radius)/10000.0);x<=(int)Math.Floor((p.X+Radius)/10000.0);x++)
        for(int y=(int)Math.Floor((p.Y-Radius)/10000.0);y<=(int)Math.Floor((p.Y+Radius)/10000.0);y++)
        {
            if(FieldWorld.Speed(FieldWorld.Terrain(x,y))>0)continue;
            var dx=p.X-Math.Clamp(p.X,x*10000L,(x+1)*10000L);
            var dy=p.Y-Math.Clamp(p.Y,y*10000L,(y+1)*10000L);
            if(dx*dx+dy*dy<Radius*Radius)return false;
        }
        return true;
    }
    static bool Clear(FieldPoint a, FieldPoint b, FieldPoint[] obstacles) { var count = Math.Max(1, Distance(a, b) / 250); for (long i = 0; i <= count; i++) { var p = new FieldPoint(a.X + (b.X - a.X) * i / count, a.Y + (b.Y - a.Y) * i / count); if (!Walkable(p) || obstacles.Any(o => Distance(p, o) < 2 * Radius + 250)) return false; } return true; }
    static FieldLeg[] Segment(FieldPoint a, FieldPoint b, long at, bool courier = false) { var result = new List<FieldLeg>(); long n = Math.Max(1, (Distance(a, b) + 249) / 250); var cuts = new List<FieldPoint> { a }; string kind = FieldWorld.TerrainAt(a); for (long i = 1; i < n; i++) { var p = new FieldPoint(a.X + (b.X - a.X) * i / n, a.Y + (b.Y - a.Y) * i / n); var next = FieldWorld.TerrainAt(p); if (next != kind) { cuts.Add(p); kind = next; } } cuts.Add(b); for (int i = 1; i < cuts.Count; i++) { var p = cuts[i - 1]; var q = cuts[i]; var length = Distance(p, q); if (length == 0) continue; kind = FieldWorld.TerrainAt(new((p.X + q.X) / 2, (p.Y + q.Y) / 2)); var speed = courier ? 2000 : FieldWorld.Speed(kind); if (speed <= 0) throw new InvalidDataException("Impassable segment"); var rise = Math.Abs(Height(q) - Height(p)); long ground = (long)Math.Ceiling(Math.Sqrt((double)length * length + (double)rise * rise)); var cost = Math.Max(1, (ground + rise + speed - 1) / speed); var end = checked(at + cost); result.Add(new(p, q, at, end, speed, kind)); at = end; } return result.ToArray(); }
    public static FieldLeg[] Plan(FieldPoint from, FieldPoint goal, long at, FieldPoint[]? obstacles = null, bool courier = false)
    {
        obstacles ??= []; if (!Walkable(from) || !Walkable(goal) || obstacles.Any(o => Distance(goal, o) < 2 * Radius)) throw new InvalidDataException("Blocked goal"); if (from == goal) return [];
        var points = new List<FieldPoint> { from, goal }; for (int x = -6; x <= 9; x++) for (int y = -6; y <= 9; y++) { var p = new FieldPoint(x * 10000L + 5000, y * 10000L + 5000); if (Walkable(p) && !points.Contains(p) && !obstacles.Any(o => Distance(p, o) < 2 * Radius + 250)) points.Add(p); }
        var cost = Enumerable.Repeat(long.MaxValue, points.Count).ToArray(); var previous = new int[points.Count]; Array.Fill(previous, -1); cost[0] = 0; var visited = new bool[points.Count];
        while (true) { var choices = Enumerable.Range(0, points.Count).Where(i => !visited[i] && cost[i] < long.MaxValue).OrderBy(i => cost[i]).ThenBy(i => i).ToArray(); if (choices.Length == 0) throw new InvalidDataException("No ground path"); var current = choices[0]; if (current == 1) break; visited[current] = true; for (int next = 0; next < points.Count; next++) { if (visited[next] || next == current) continue; var distance = Distance(points[current], points[next]); if (distance > 15000 && !(current == 0 && next == 1)) continue; if (!Clear(points[current], points[next], obstacles)) continue; var legs = Segment(points[current], points[next], at, courier); var d = checked(cost[current] + (legs.Length == 0 ? 0 : legs[^1].EndAt - at)); if (d < cost[next]) { cost[next] = d; previous[next] = current; } } }
        var chain = new List<FieldPoint>(); for (int i = 1; i != -1; i = previous[i]) chain.Add(points[i]); chain.Reverse(); var smoothed = new List<FieldPoint> { chain[0] }; for (int i = 0; i < chain.Count - 1;) { int chosen = i + 1; for (int j = chain.Count - 1; j > i + 1; j--) if (Clear(chain[i], chain[j], obstacles)) { var direct = Segment(chain[i], chain[j], 0, courier); long old = 0; for (int k = i + 1; k <= j; k++) old += Segment(chain[k - 1], chain[k], 0, courier).LastOrDefault()?.EndAt ?? 0; if (direct[^1].EndAt <= old) { chosen = j; break; } } smoothed.Add(chain[chosen]); i = chosen; }
        var result = new List<FieldLeg>(); for (int i = 1; i < smoothed.Count; i++) { var legs = Segment(smoothed[i - 1], smoothed[i], at, courier); result.AddRange(legs); if (legs.Length > 0) at = legs[^1].EndAt; }
        return result.ToArray();
    }
    public static void ValidatePath(FieldLeg[] path, FieldPoint origin, FieldPoint goal, bool courier = false) { var prior = origin; long? end = null; foreach (var l in path) { long length = Distance(l.From, l.To), rise = Math.Abs(Height(l.To) - Height(l.From)); long cost = Math.Max(1, ((long)Math.Ceiling(Math.Sqrt((double)length * length + (double)rise * rise)) + rise + l.Speed - 1) / Math.Max(1, l.Speed)); var terrain = FieldWorld.TerrainAt(new((l.From.X + l.To.X) / 2, (l.From.Y + l.To.Y) / 2)); if (l.From != prior || end != null && l.StartAt != end || l.StartAt < 0 || l.EndAt != l.StartAt + cost || l.Speed != (courier ? 2000 : FieldWorld.Speed(terrain)) || l.Speed <= 0 || l.Terrain != terrain || !Clear(l.From, l.To, [])) throw new InvalidDataException("Ground path geometry/cost invalid"); prior = l.To; end = l.EndAt; } if (prior != goal) throw new InvalidDataException("Ground route endpoint mismatch"); }
    public static FieldPoint At(FieldLeg[] path, FieldPoint initial, long time) { if (path.Length == 0 || time <= path[0].StartAt) return initial; var leg = path.FirstOrDefault(l => time < l.EndAt) ?? path[^1]; var n = Math.Clamp(time - leg.StartAt, 0, leg.EndAt - leg.StartAt); return new(leg.From.X + (leg.To.X - leg.From.X) * n / (leg.EndAt - leg.StartAt), leg.From.Y + (leg.To.Y - leg.From.Y) * n / (leg.EndAt - leg.StartAt)); }
    // Relative motion is linear between integer-second path boundaries: closest approach tests swept space, not only endpoints.
    public static bool Conflict(FieldLeg[] a, FieldPoint ai, FieldLeg[] b, FieldPoint bi, long from, long to) { for (long t = from; t <= to; t++) { var p = At(a, ai, t); var q = At(b, bi, t); var pn = At(a, ai, t + 1); var qn = At(b, bi, t + 1); double x = p.X - q.X, y = p.Y - q.Y, dx = (pn.X - qn.X) - x, dy = (pn.Y - qn.Y) - y; var den = dx * dx + dy * dy; var u = den == 0 ? 0 : Math.Clamp(-(x * dx + y * dy) / den, 0, 1); if ((x + dx * u) * (x + dx * u) + (y + dy * u) * (y + dy * u) < 4.0 * Radius * Radius) return true; } return false; }
}
