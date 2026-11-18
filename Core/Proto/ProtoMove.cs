namespace HakutaiKagami.Core.Proto;

// 歩く道筋と、1秒ごとの歩み。道探しは20m格子の A*（8方向）。かかる時間は本線と同じく
// (斜めの長さ＋上り下りの高さ)÷その地面の速さ。山を越えると高さの分だけ遅くなる。
public static class ProtoMove
{
    static readonly (int Dc, int Dr)[] Steps = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    public static bool Passable(ProtoMap map, int c, int r) => map.Inside(c, r) && ProtoMap.Speed(map.GroundAt(c, r)) > 0;

    // 通れない所を押されたら、いちばん近い通れる格子へ寄せる。
    public static (long X, long Y) Nearest(ProtoMap map, long x, long y)
    {
        var (c0, r0) = map.CellOf(x, y);
        if (Passable(map, c0, r0)) return (Math.Clamp(x, 0, map.Columns * (long)map.CellCm - 1), Math.Clamp(y, 0, map.Rows * (long)map.CellCm - 1));
        for (int radius = 1; radius < 40; radius++)
        {
            (long X, long Y)? best = null; long bestD = long.MaxValue;
            for (int dr = -radius; dr <= radius; dr++) for (int dc = -radius; dc <= radius; dc++)
                {
                    if (Math.Max(Math.Abs(dc), Math.Abs(dr)) != radius || !Passable(map, c0 + dc, r0 + dr)) continue;
                    var p = map.Center(c0 + dc, r0 + dr); long d = (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y);
                    if (d < bestD) { bestD = d; best = p; }
                }
            if (best != null) return best.Value;
        }
        return (x, y);
    }

    static double StepSeconds(ProtoMap map, int c0, int r0, int c1, int r1)
    {
        double len = map.CellCm * (c0 != c1 && r0 != r1 ? Math.Sqrt(2) : 1), rise = Math.Abs(map.HeightAt(c1, r1) - map.HeightAt(c0, r0));
        return (Math.Sqrt(len * len + rise * rise) + rise) / ProtoMap.Speed(map.GroundAt(c1, r1));
    }

    public static List<(long X, long Y)>? Plan(ProtoMap map, (long X, long Y) from, (long X, long Y) to)
    {
        to = Nearest(map, to.X, to.Y);
        var (sc, sr) = map.CellOf(from.X, from.Y); var (gc, gr) = map.CellOf(to.X, to.Y);
        if (!Passable(map, gc, gr)) return null;
        int n = map.Columns * map.Rows, start = sr * map.Columns + sc, goal = gr * map.Columns + gc;
        var cost = new double[n]; Array.Fill(cost, double.PositiveInfinity); var prev = new int[n]; Array.Fill(prev, -1); var done = new bool[n];
        var open = new PriorityQueue<int, (double, int)>();
        cost[start] = 0; open.Enqueue(start, (0, start));
        while (open.TryDequeue(out int cur, out _))
        {
            if (done[cur]) continue; done[cur] = true; if (cur == goal) break;
            int cc = cur % map.Columns, cr = cur / map.Columns;
            foreach (var (dc, dr) in Steps)
            {
                int nc = cc + dc, nr = cr + dr; if (!Passable(map, nc, nr)) continue;
                if (dc != 0 && dr != 0 && (!Passable(map, cc + dc, cr) || !Passable(map, cc, cr + dr))) continue;
                int next = nr * map.Columns + nc; if (done[next]) continue;
                double d = cost[cur] + StepSeconds(map, cc, cr, nc, nr);
                if (d < cost[next]) { cost[next] = d; prev[next] = cur; double hx = (nc - gc) * (double)map.CellCm, hy = (nr - gr) * (double)map.CellCm; open.Enqueue(next, (d + Math.Sqrt(hx * hx + hy * hy) / 200, next)); }
            }
        }
        if (!done[goal]) return null;
        var cells = new List<int>(); for (int at = goal; at != -1; at = prev[at]) cells.Add(at); cells.Reverse();
        var points = new List<(long X, long Y)>();
        for (int i = 1; i < cells.Count - 1; i++)
        {
            int a = cells[i - 1], b = cells[i], c = cells[i + 1];
            int d1 = b - a, d2 = c - b; if (d1 == d2 && map.GroundAt(b % map.Columns, b / map.Columns) == map.GroundAt(c % map.Columns, c / map.Columns)) continue;
            points.Add(map.Center(b % map.Columns, b / map.Columns));
        }
        points.Add(to);
        return points;
    }

    // 1秒ぶん歩く。戻り値は着いたかどうか。
    public static bool Step(ProtoMap map, ref long x, ref long y, List<(long X, long Y)> path, ref int index, int percent = 100)
    {
        double remaining = 1.0;
        while (remaining > 1e-9 && index < path.Count)
        {
            var (tx, ty) = path[index]; double dx = tx - x, dy = ty - y, dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1) { x = tx; y = ty; index++; continue; }
            double ux = dx / dist, uy = dy / dist; long px = x + (long)Math.Round(ux * 100), py = y + (long)Math.Round(uy * 100);
            double speed = ProtoMap.Speed(map.GroundAtCm(x, y)) * percent / 100.0; if (speed <= 0) speed = 50;
            double rise = Math.Abs(map.HeightAtCm(px, py) - map.HeightAtCm(x, y));
            double eff = speed * 100 / (Math.Sqrt(100 * 100 + rise * rise) + rise);
            double move = eff * remaining;
            if (move >= dist) { x = tx; y = ty; index++; remaining -= dist / eff; }
            else { x += (long)Math.Round(ux * move); y += (long)Math.Round(uy * move); remaining = 0; }
        }
        return index >= path.Count;
    }

    // 着くまでの秒数（本当に歩くのと同じ計算で数える）。
    public static long Eta(ProtoMap map, (long X, long Y) from, List<(long X, long Y)> path, long limit = 12 * 3600, int percent = 100)
    {
        long x = from.X, y = from.Y; int index = 0;
        for (long s = 1; s <= limit; s++) if (Step(map, ref x, ref y, path, ref index, percent)) return s;
        return limit;
    }
}
