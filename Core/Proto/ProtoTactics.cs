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
    // 間合い（2026-11-19 ユーザーの図「遠巻きでは凸の出っ張りが相手の面に触れる」）：打ち合える遠さ100m、攻めかかる側は相手の80m手前で止まる、味方どうしは90m空ける（敵どうしは間合いの80mまで）。
    // 地図の凸（幅64m・奥行き48m・出っ張り32m）がちょうど触れ合う長さ。
    internal const long Reach = 100_00, Contact = 80_00, Gap = 90_00;
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
            // 狙っている相手がいる間は、その相手の方を向いたまま（横から別の敵に当たられても、すぐには向き直れない）。
            if (War.Target.TryGetValue(u.Id, out var aim) && Person(aim) is { Active: true } at && !War.Routed.Contains(aim) && Dist(u, at) <= 300_00) { War.FacingFoe[u.Id] = aim; Face(u, at.X - u.X, at.Y - u.Y); continue; }
            if (War.FacingFoe.TryGetValue(u.Id, out var fid)) { var f = Person(fid); if (f.Active && !War.Routed.Contains(fid) && Dist(u, f) <= Reach + 20_00) foe = f; }
            foe ??= all.Where(b => Hostile(u.House, b.House) && !War.Routed.Contains(b.Id) && Dist(u, b) <= Reach).OrderBy(b => Dist(u, b)).ThenBy(b => b.Id).FirstOrDefault();
            if (foe != null) { War.FacingFoe[u.Id] = foe.Id; Face(u, foe.X - u.X, foe.Y - u.Y); continue; }
            War.FacingFoe.Remove(u.Id);
            // 歩いているときは、向かう敵の方（いなければ行き先の方）を向く。道の次の点は曲がり角で後ろを指すことがある。
            if (War.Target.TryGetValue(u.Id, out var tid)) { var t = Person(tid); Face(u, t.X - u.X, t.Y - u.Y); }
            else if (u.Moving) { var end = u.Path[^1]; Face(u, end.X - u.X, end.Y - u.Y); }
            else
            {
                // 止まって構えている隊は、見えているいちばん近い敵の方を向く。見えなければ敵の本陣の方（2026-11-19 ユーザー「伏兵はなぜ左を向いているのか」。前は最後に歩いた向きのままで、意味が無かった）。
                var seen = all.Where(e => Hostile(u.House, e.House) && !War.Routed.Contains(e.Id) && InSight(u, e)).OrderBy(e => Dist(u, e)).ThenBy(e => e.Id).FirstOrDefault();
                if (seen != null) Face(u, seen.X - u.X, seen.Y - u.Y);
                else { var foeHead = all.FirstOrDefault(e => Hostile(u.House, e.House)); if (foeHead != null) { var camp = CampOf(foeHead); Face(u, camp.X - u.X, camp.Y - u.Y); } }
            }
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
            var r = all.Where(b => Hostile(a.House, b.House) && War.Routed.Contains(b.Id) && b.Active && Dist(a, b) <= Reach).OrderBy(b => Dist(a, b)).ThenBy(b => b.Id).FirstOrDefault();
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
        bool fleeing = War.Routed.Contains(t.Id) || MovingAway(t, u) && Dist(u, t) > Reach && !War.Target.ContainsKey(t.Id);
        if (!fleeing) return true;
        if (Pursues(u) && Dist(u, t) <= 600_00) { u.Doing = $"{t.Name}の兵を追っている"; return true; }
        if (!Flag($"hold:{u.Id}:{t.Id}")) { Set($"hold:{u.Id}:{t.Id}"); Happen($"hold:{u.Id}:{t.Id}", $"{u.Name}は退く{t.Name}の兵を深追いせず、止まった。", u.X, u.Y, u); }
        u.Path = []; u.PathIndex = 0; u.Doing = "止まって様子を見ている";
        return false;
    }

    // ---- 釣り野伏の場面（検査 TheFeignedRetreatDrawsTheEnemyIntoTheAmbush と「合戦を見る」で同じものを使う） ----
    // 謀叛が起きたら軍議で「釣り野伏」を選び、合図の1分後に、開けた所へ囮の本人・逸る井口・森の縁に伏せた小坂を置く（正之と勝元はこの場面から外す）。
    public void SetupTsuriPractice()
    {
        while (War.Phase != "軍議待ち" && Second < 4 * Day) Advance(600);
        var castle = Map.Site("takano-castle"); MoveTo(castle.X * 100L, castle.Y * 100L);
        for (int i = 0; i < 120 && !CouncilOpen; i++) Advance(60);
        DecideCouncil("road-mouth", Departures[0], Signals[0], Plans[1]);
        GoToTask(OpenTasks.First(t => t.Kind == "muster").Id);
        Advance(War.SignalAt + 60 - Second);
        var kosaka = Person(Karou); var iguchi = Person(Iguchi); var me = Player;
        foreach (var id in new[] { Heir, Katsumoto }) { Person(id).Active = false; War.Target.Remove(id); }
        War.StartSoldiers.Clear(); War.RetreatRules.Clear(); // この試しでは軍としての撤退は見ない
        foreach (var u in new[] { kosaka, iguchi, me }) { u.Path = []; u.PathIndex = 0; War.Target.Remove(u.Id); War.Routed.Remove(u.Id); }
        me.Soldiers = 30; me.Morale = 100; iguchi.Soldiers = 80; iguchi.Morale = 100; kosaka.Soldiers = 100; kosaka.Morale = 100;
        var hide = PositionOf("forest-edge"); kosaka.X = hide.X * 100L; kosaka.Y = hide.Y * 100L;
        iguchi.X = kosaka.X + 400_00; iguchi.Y = kosaka.Y - 60_00; me.X = iguchi.X - 60_00; me.Y = iguchi.Y;
    }
    // 囮が下がる先：伏せた小坂のすぐ北（60m）を通って、森の縁沿いに西へ350m。
    public (long X, long Y) TsuriLure() { var k = Person(Karou); return (k.X - 350_00, k.Y - 60_00); }

    // 後ろへ下がっている（2026-11-19 ユーザーの印「後退は普通より遅い。釣り野伏のつもりが本当に壊走することもある」）：
    // 合戦の間、崩れていない隊が、300m以内の敵から遠ざかる向きに歩いている。敵に背を見せずに下がるので速さは75%、受ける打撃は1.5倍。
    internal bool Retreating(ProtoPerson u) => War.Phase == "合戦" && u.Soldiers > 0 && u.Moving && !War.Routed.Contains(u.Id)
        && Units.Any(e => Hostile(u.House, e.House) && !War.Routed.Contains(e.Id) && Dist(u, e) <= 300_00 && MovingAway(u, e));

    // 間合い：u が t に当たるときに止まる所（t から u の側へ80m）。
    (long X, long Y) ContactPoint(ProtoPerson u, ProtoPerson t)
    {
        double dx = u.X - t.X, dy = u.Y - t.Y, len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) { dx = 1; dy = 0; len = 1; }
        var p = (t.X + (long)(dx / len * Contact), t.Y + (long)(dy / len * Contact));
        return Map.GroundAtCm(p.Item1, p.Item2) == Ground.Water ? (t.X, t.Y) : p;
    }

    // 間を空ける（毎分）：隊どうしが70mより近ければ、互いに半分ずつ離す（重なって一つに見えないように。世界の本当の位置を動かす）。
    void Spread(ProtoPerson[] all)
    {
        var us = all.Where(u => u.Active && u.Soldiers > 0).OrderBy(u => u.Id).ToArray();
        for (int i = 0; i < us.Length; i++) for (int j = i + 1; j < us.Length; j++)
        {
            var a = us[i]; var b = us[j]; double dx = b.X - a.X, dy = b.Y - a.Y, len = Math.Sqrt(dx * dx + dy * dy);
            long gap = Hostile(a.House, b.House) ? Contact - 5_00 : Gap; // 敵どうしは間合いまで寄れる
            if (len >= gap) continue;
            if (len < 1) { double ang = (i * 7 + j * 13) % 360 * Math.PI / 180; dx = Math.Cos(ang); dy = Math.Sin(ang); len = 1; }
            long px = (long)(dx / len * (gap - len) / 2), py = (long)(dy / len * (gap - len) / 2);
            if (Map.GroundAtCm(a.X - px, a.Y - py) != Ground.Water) { a.X -= px; a.Y -= py; }
            if (Map.GroundAtCm(b.X + px, b.Y + py) != Ground.Water) { b.X += px; b.Y += py; }
        }
    }

    // t が u から遠ざかる向きに歩いているか。
    bool MovingAway(ProtoPerson t, ProtoPerson u)
    {
        // 道は曲がるので、次の点ではなく行き先で見る（行き先が今より50m以上遠い）。
        if (!t.Moving) return false; var n = t.Path[^1];
        return Dist(u, n.X, n.Y) > Dist(u, t) + 50_00;
    }

    // 伏せていた隊は、目の前（150m）に敵が来たら自分から仕掛ける。
    void Ambushers()
    {
        foreach (var u in Units.Where(u => Hidden(u) && !War.Target.ContainsKey(u.Id) && (u.House == Rebel || War.Assign.ContainsKey(u.Id)) && Auto(u)))
        {
            // 目の前（150m）まで引きつけてから出る（遠くで出ると、同じ速さで逃げられて追いつけない）。
            // 横か後ろを取れるまで待つ（敵が目の前を通り過ぎてから出る）。正面からしか当たれないなら、90mまで引きつける。
            var e = Units.Where(e => Hostile(u.House, e.House) && !War.Routed.Contains(e.Id) && InSight(u, e) && (Dist(u, e) <= 150_00 && FlankOf(e, u).Kind != "正面" || Dist(u, e) <= 90_00)).OrderBy(e => Dist(u, e)).ThenBy(e => e.Id).FirstOrDefault();
            if (e != null) War.Target[u.Id] = e.Id;
        }
    }
}
