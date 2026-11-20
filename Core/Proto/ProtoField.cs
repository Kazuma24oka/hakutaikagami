namespace HakutaiKagami.Core.Proto;

// 試作版の陣の置き場所（2026-11-20 ユーザー「決まったポイントじゃなくて地形に効果があって、軍議のときも戦場図で選択する」）。
// ・「南の丘」「道の口」のような決まった点は使わない。隊ごとの陣は地図の上の一点（War.Post）で、効果は地形が持つ：
//   高い所から低い所へは打撃1.3倍（逆は0.7倍）、森にいる隊は打たれにくく（0.8倍）、止まっていれば近づくまで見えない。
//   森の中から仕掛けるとずっと奇襲、森を出ても5分は奇襲になる（Core/Proto/ProtoClash.cs・ProtoTactics.cs）。
// ・陣取りで全軍士気は動かさない（定位置で待つだけで勝てる形にしない）。
// ・軍議では、まず総大将が全員の陣と役目を決める（Proposal）。ほかの者は自分の陣と役目、作戦を再提案する（総大将が本人なら全員を決める）。
// ・敵（岩切勢）も地形から置き場所を選ぶ（高い所、城への道をふさぐ所、道のそばの森）。
public sealed record ProtoPiece(string Unit, string Name, string Role, long X, long Y, string Terrain, string Effect, int Soldiers);
public sealed record ProtoProposal(string Goal, string Plan, string Departure, string Signal, string RetreatAdd, ProtoPiece[] Pieces);

public sealed partial class ProtoWorld
{
    // 隊の陣（地図の上の一点）。名前は地形から付く。
    public ProtoPosition PostOf(string id)
    {
        if (War.Post.TryGetValue(id, out var p)) return new ProtoPosition("post:" + id, TerrainName(p[0], p[1]) + "の陣", (int)(p[0] / 100), (int)(p[1] / 100));
        return PositionOf("takano-castle");
    }
    void SetPost(string id, long x, long y) { var n = ProtoMove.Nearest(Map, x, y); War.Post[id] = [n.X, n.Y]; War.Assign[id] = "field"; }

    public string TerrainName(long x, long y)
    {
        var g = Map.GroundAtCm(x, y);
        return g == Ground.Forest ? "森" : g == Ground.Mountain || Map.HeightAtCm(x, y) >= 10_00 ? "丘" : g is Ground.Road or Ground.Bridge ? "道" : g == Ground.Site ? "城" : "野";
    }
    // その地形に陣を取ったときに受ける効果の名前（軍議の戦場図に出す）。
    public string TerrainEffect(long x, long y) => TerrainName(x, y) switch { "森" => "伏兵・奇襲", "丘" => "高所", _ => "" };
    internal bool InForest(long x, long y) => Map.GroundAtCm(x, y) == Ground.Forest;

    // ---- 地形から場所を探す ----
    // 岩切城へ通じる道（鷹野城の側から来る道）の上の、岩切城の手前 back（cm）の所。
    (long X, long Y) RoadPoint(long back)
    {
        var a = Map.Site("takano-castle"); var b = Map.Site("iwakiri-castle");
        // 岩切城で終わる道のうち、もう一方の端が鷹野城に近い道を、城から逆にたどる。
        var road = Map.Data.Terrain.Roads.Select(r => r.Points.Select(p => (X: (long)p[0], Y: (long)p[1])).ToList())
            .Select(pts => Math.Abs(pts[^1].X - b.X) + Math.Abs(pts[^1].Y - b.Y) <= 30 ? Enumerable.Reverse(pts).ToList() : pts)
            .Where(pts => Math.Abs(pts[0].X - b.X) + Math.Abs(pts[0].Y - b.Y) <= 30)
            .OrderBy(pts => Math.Pow(pts[^1].X - a.X, 2) + Math.Pow(pts[^1].Y - a.Y, 2)).FirstOrDefault();
        if (road == null) return ProtoMove.Nearest(Map, b.X * 100L, b.Y * 100L);
        double left = 0, want = back / 100.0;
        for (int i = 0; i < road.Count - 1; i++)
        {
            double seg = Math.Sqrt(Math.Pow(road[i + 1].X - road[i].X, 2) + Math.Pow(road[i + 1].Y - road[i].Y, 2));
            if (left + seg >= want && seg > 0) { double t = (want - left) / seg; return ProtoMove.Nearest(Map, (long)((road[i].X + (road[i + 1].X - road[i].X) * t) * 100), (long)((road[i].Y + (road[i + 1].Y - road[i].Y) * t) * 100)); }
            left += seg;
        }
        return ProtoMove.Nearest(Map, road[^1].X * 100L, road[^1].Y * 100L);
    }
    // (cx, cy) の近く（radius 以内）でいちばん高い所。
    (long X, long Y) HighGround(long cx, long cy, long radius)
    {
        long cell = Map.CellCm; (long X, long Y) best = (cx, cy); int top = int.MinValue;
        for (long y = cy - radius; y <= cy + radius; y += cell)
            for (long x = cx - radius; x <= cx + radius; x += cell)
            {
                if (x < 0 || y < 0 || x >= Map.Data.WidthM * 100L || y >= Map.Data.HeightM * 100L) continue;
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > radius * radius || Map.GroundAtCm(x, y) is Ground.Water or Ground.Site) continue;
                int h = Map.HeightAtCm(x, y); if (h > top) { top = h; best = (x, y); }
            }
        return ProtoMove.Nearest(Map, best.X, best.Y);
    }
    // (cx, cy) にいちばん近い森の中（縁から depth だけ入った所）。無ければ null。
    (long X, long Y)? ForestSpot(long cx, long cy, long radius, long depth)
    {
        long cell = Map.CellCm; (long X, long Y)? best = null; long bd = long.MaxValue;
        for (long y = cy - radius; y <= cy + radius; y += cell)
            for (long x = cx - radius; x <= cx + radius; x += cell)
            {
                if (x < depth || y < depth || x >= Map.Data.WidthM * 100L - depth || y >= Map.Data.HeightM * 100L - depth) continue;
                long d = (x - cx) * (x - cx) + (y - cy) * (y - cy); if (d > radius * radius || d >= bd) continue;
                if (!InForest(x, y) || !InForest(x + depth, y) || !InForest(x - depth, y) || !InForest(x, y + depth) || !InForest(x, y - depth)) continue;
                best = (x, y); bd = d;
            }
        return best;
    }
    // 戦場の要（鷹野から見た正面）：岩切城の手前650mの道の上。
    (long X, long Y) FrontPoint() => RoadPoint(650_00);
    public (long X, long Y) Front => FrontPoint();

    // ---- 総大将の案 ----
    // 役目ごとの陣を地形から決める：前へ出る役は道の上、守り・後詰・本隊は近くの高い所、伏兵・回り込みは近くの森の中。
    (long X, long Y) PostFor(string role)
    {
        var f = FrontPoint();
        return role switch
        {
            "伏兵" or "回り込み" or "森の守り" => ForestSpot(f.X, f.Y, 800_00, 40_00) ?? f,
            "本隊" or "後詰" or "丘の守り" => HighGround(f.X, f.Y, 500_00),
            _ => f,
        };
    }
    // 総大将が決める役目（大将・家老・本人）。
    (string Heir, string Karou, string Me) RolesBy(string plan, string? playerRole) => plan switch
    {
        "釣り野伏" => playerRole == "囮" ? ("本隊", "伏兵", "囮") : ("囮", "伏兵", playerRole ?? "伏兵"),
        "挟撃" => ("正面", "回り込み", playerRole ?? "回り込み"),
        "陣を固める" => ("道の守り", "丘の守り", playerRole ?? "森の守り"),
        _ => ("先手", "後詰", playerRole ?? "後詰"),
    };
    // 総大将の案。goal・plan・role を渡すと、その作戦での総大将の陣立てを返す（軍議の戦場図で、作戦を選び直したときに使う）。
    public ProtoProposal Proposal(string? goal = null, string? plan = null, string? role = null)
    {
        var heir = Person(Heir);
        string g = goal ?? Goals[0]; string p = plan != null && PlansOf(g).Contains(plan) ? plan : PlansOf(g)[0];
        if (role != null && !RolesOf(p).Any(r => r.Role == role)) role = null;
        var (rh, rk, rm) = RolesBy(p, role);
        ProtoPiece Piece(string id, string r, int soldiers) { var at = PostFor(r); var u = Person(id); return new(id, u.Name, r, at.X, at.Y, TerrainName(at.X, at.Y), TerrainEffect(at.X, at.Y), soldiers); }
        return new(g, p, Departures[1], Signals[0], heir.Temper == "慎重" ? RetreatAdds[1] : RetreatAdds[0],
            [Piece(Heir, rh, heir.Soldiers), Piece(Karou, rk, Person(Karou).Soldiers), Piece(Player.Id, rm, 30)]);
    }
    // 総大将が再提案を聞き入れるか：本人を嫌っていなければ聞く（軍全体に関わる分。自分の陣と役目はいつも聞く）。
    public bool CommanderListens => HeadOf("takano") == Player.Id || Favor(Heir) >= 0;

    // ---- 敵の陣 ----
    // 岩切勢は地形から陣を選ぶ：勝元は城の近くのいちばん高い所。井口は、城への道をふさぐ所か、道のそばの森（その時々で変わる）。
    void PlaceRebels()
    {
        var castle = Map.Site("iwakiri-castle"); long cx = castle.X * 100L, cy = castle.Y * 100L;
        var hill = HighGround(cx, cy, 700_00); SetPost(Katsumoto, hill.X, hill.Y);
        int pick = Hash("rebel-post", (int)((Player.X + Player.Y) / 100 + Merit + Money + War.DepartAt / 60)) % 3;
        var road = RoadPoint(pick == 0 ? 450_00 : 650_00);
        var spot = pick == 2 ? ForestSpot(road.X, road.Y, 800_00, 40_00) ?? road : road;
        SetPost(Iguchi, spot.X, spot.Y);
    }

    // 守る陣を敵に取られているか：陣の150m以内に敵がいて、味方がいない。
    bool PostLost(string side) => SideUnits(side).Where(u => War.Post.ContainsKey(u.Id)).Any(u =>
    {
        var p = War.Post[u.Id];
        bool foe = Units.Any(e => Side(e.House) == Foe(side) && !War.Routed.Contains(e.Id) && Dist(e, p[0], p[1]) <= 150_00);
        return foe && !Units.Any(f => Side(f.House) == side && !War.Routed.Contains(f.Id) && Dist(f, p[0], p[1]) <= 150_00);
    });
}
