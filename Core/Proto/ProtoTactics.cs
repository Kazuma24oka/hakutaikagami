namespace HakutaiKagami.Core.Proto;

// 試作版の合戦の駆け引き（2026-11-19 ユーザー「包囲殲滅・釣り野伏・挟撃を、正面の力押しより強く」。ボタンではなく、地図の上の動きで起こす）。
// ・向き：隊は最初にぶつかった相手の方を向き続ける。側面から当たると1.5倍、背後からは2倍で、背後を突かれた側は士気が毎分5さらに下がる。
// ・退き道：崩れた隊の本陣への道の先に敵がいると逃げられず壊滅する（兵の多くを失い、大将は捕らえられる）。
// ・追い討ち：崩れて退く隊は、近くの敵に一方的に削られる。追っている隊は隊形が崩れて守りが弱い（受ける打撃1.25倍）。
// ・追うかどうかは本人が決める：逸る者・知略の低い者は退く敵を追い、慎重な者・知略の高い者は深追いしない。
//   崩れていないのに退いて見せる隊（本人が歩いて下がるなど）も、追う者は追う。これで釣り野伏ができる。
// ・伏せる：森の中で動かずにいる隊は、敵からは近く（80m）まで見えない。伏せていた隊が仕掛けると奇襲（5分のあいだ1.5倍、相手の士気-30、その間は毎分さらに-4）。側面を突かれると毎分-2。
public sealed partial class ProtoWorld
{
    public static bool Pursues(ProtoPerson u) => u.Temper == "逸る" || u.Wits < 55;

    // 伏せている：森の中で、動かず、ぶつかっておらず、崩れていない。
    // 森の縁（30m以内に森がある）も木立に隠れられる。
    public bool Hidden(ProtoPerson u) => u.Soldiers > 0 && InCover(u.X, u.Y) && !u.Moving && ClashOf(u.Id) == null && !War.Routed.Contains(u.Id);
    bool InCover(long x, long y) => new[] { (0L, 0L), (30_00L, 0L), (-30_00L, 0L), (0L, 30_00L), (0L, -30_00L) }.Any(d => Map.GroundAtCm(x + d.Item1, y + d.Item2) == Ground.Forest);

    // 向きの更新（毎分）：ぶつかっている相手がいればその方、いなければ進む方。
    void UpdateFacing(ProtoPerson[] all)
    {
        foreach (var u in all)
        {
            if (War.Routed.Contains(u.Id)) { War.FacingFoe.Remove(u.Id); continue; }
            ProtoPerson? foe = null;
            if (War.FacingFoe.TryGetValue(u.Id, out var fid)) { var f = Person(fid); if (f.Active && !War.Routed.Contains(fid) && Dist(u, f) <= 100_00) foe = f; }
            foe ??= all.Where(b => Hostile(u.House, b.House) && !War.Routed.Contains(b.Id) && Dist(u, b) <= 80_00).OrderBy(b => Dist(u, b)).ThenBy(b => b.Id).FirstOrDefault();
            if (foe != null) { War.FacingFoe[u.Id] = foe.Id; Face(u, foe.X - u.X, foe.Y - u.Y); continue; }
            War.FacingFoe.Remove(u.Id);
            // 歩いているときは、向かう敵の方（いなければ行き先の方）を向く。道の次の点は曲がり角で後ろを指すことがある。
            if (War.Target.TryGetValue(u.Id, out var tid)) { var t = Person(tid); Face(u, t.X - u.X, t.Y - u.Y); }
            else if (u.Moving) { var end = u.Path[^1]; Face(u, end.X - u.X, end.Y - u.Y); }
        }
        foreach (var u in all.Where(Hidden)) War.HiddenAt[u.Id] = Second;
    }
    // 20mより近い（重なっている）相手の方は当てにならないので、向きを変えない（くるくる回らないように）。
    void Face(ProtoPerson u, long dx, long dy) { double len = Math.Sqrt((double)dx * dx + (double)dy * dy); if (len > 20_00) War.Facing[u.Id] = [dx / len, dy / len]; }

    // 攻める側 a が守る側 b のどこを突いているか。
    public (double Mul, string Kind) FlankOf(ProtoPerson b, ProtoPerson a)
    {
        if (!War.Facing.TryGetValue(b.Id, out var f)) return (1, "正面");
        double dx = a.X - b.X, dy = a.Y - b.Y, len = Math.Sqrt(dx * dx + dy * dy); if (len < 1) return (1, "正面");
        double cos = (f[0] * dx + f[1] * dy) / len;
        return cos >= 0.5 ? (1, "正面") : cos > -0.5 ? (1.5, "側面") : (2, "背後");
    }

    // 退き道がふさがれているか：本陣への向きの先（300m以内）に、崩れていない敵がいる。
    internal bool CutOff(ProtoPerson u)
    {
        var camp = CampOf(u); double cx = camp.X - u.X, cy = camp.Y - u.Y, len = Math.Sqrt(cx * cx + cy * cy); if (len < 1) return false;
        return Units.Any(e => Hostile(u.House, e.House) && !War.Routed.Contains(e.Id) && Dist(u, e) <= 300_00 &&
            ((e.X - u.X) * cx + (e.Y - u.Y) * cy) / (len * Math.Max(1, Dist(u, e))) >= 0.6);
    }

    // 壊滅：兵の7割を失い、大将は捕らえられる（本人は辛くも逃れる）。立て直しはできない。
    void Annihilate(ProtoPerson u)
    {
        int lost = (int)Math.Round(u.Soldiers * 0.7); u.Soldiers -= lost; if (!War.Recovered.Contains(u.Id)) War.Recovered.Add(u.Id);
        if (u.Player) { Happen($"cutoff:{u.Id}:{Second}", $"{u.Name}の隊は退き道をふさがれ、兵{lost}を失った。辛くも囲みを破った。", u.X, u.Y, u); return; }
        Happen($"cutoff:{u.Id}:{Second}", $"{u.Name}の隊は退き道をふさがれて壊滅した（兵{lost}を失い、{u.Name}は捕らえられた）。", u.X, u.Y, u);
        u.Active = false; u.Path = []; u.PathIndex = 0; u.Doing = "捕らえられた";
        AddSpirit(Side(u.House), -10);
    }

    // 追い討ち（毎分）：ぶつかっている相手がいない隊は、80m以内の崩れて退く敵を一方的に削る。
    void Pursue(ProtoPerson[] all, Dictionary<string, double> hits)
    {
        foreach (var a in all.Where(a => !War.Routed.Contains(a.Id) && ClashOf(a.Id) == null))
        {
            var r = all.Where(b => Hostile(a.House, b.House) && War.Routed.Contains(b.Id) && b.Active && Dist(a, b) <= 80_00).OrderBy(b => Dist(a, b)).ThenBy(b => b.Id).FirstOrDefault();
            if (r == null) continue;
            hits[r.Id] = hits.GetValueOrDefault(r.Id) + a.Soldiers * 0.006 * (a.Valor / 50.0) * 1.5;
            a.Doing = $"{r.Name}の兵を追い討ちしている";
        }
    }

    // 追うかどうか（向かう相手が退いているとき）：追う者は追い、追わない者はその場で止まる。
    // 戻り値：追い続けるなら true。
    internal bool KeepsChasing(ProtoPerson u, ProtoPerson t)
    {
        // 退いている：崩れているか、誰にも向かわずに遠ざかっている（味方を追っている敵は退いているのではない）。
        bool fleeing = War.Routed.Contains(t.Id) || MovingAway(t, u) && Dist(u, t) > 80_00 && !War.Target.ContainsKey(t.Id);
        if (!fleeing) return true;
        if (Pursues(u) && Dist(u, t) <= 600_00) { u.Doing = $"{t.Name}の兵を追っている"; return true; }
        if (!Flag($"hold:{u.Id}:{t.Id}")) { Set($"hold:{u.Id}:{t.Id}"); Happen($"hold:{u.Id}:{t.Id}", $"{u.Name}は退く{t.Name}の兵を深追いせず、止まった。", u.X, u.Y, u); }
        u.Path = []; u.PathIndex = 0; u.Doing = "止まって様子を見ている";
        return false;
    }

    // t が u から遠ざかる向きに歩いているか。
    bool MovingAway(ProtoPerson t, ProtoPerson u)
    {
        // 道は曲がるので、次の点ではなく行き先で見る（行き先が今より50m以上遠い）。
        if (!t.Moving) return false; var n = t.Path[^1];
        return Dist(u, n.X, n.Y) > Dist(u, t) + 50_00;
    }

    // 伏せていた隊は、目の前に敵が来たら自分から仕掛ける。
    void Ambushers()
    {
        foreach (var u in Units.Where(u => Hidden(u) && !War.Target.ContainsKey(u.Id) && (u.House == Rebel || War.Assign.ContainsKey(u.Id)) && !u.Player))
        {
            // 目の前（120m）まで引きつけてから出る（遠くで出ると、同じ速さで逃げられて追いつけない）。
            var e = Units.Where(e => Hostile(u.House, e.House) && !War.Routed.Contains(e.Id) && Dist(u, e) <= 120_00 && InSight(u, e)).OrderBy(e => Dist(u, e)).ThenBy(e => e.Id).FirstOrDefault();
            if (e != null) War.Target[u.Id] = e.Id;
        }
    }
}
