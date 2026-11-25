namespace HakutaiKagami.Core.Proto;

// 試作版の攻城（2026-11-25 作り直し。案は Docs/Proposals/SIEGE_REDESIGN.md、ユーザー「案のとおり」）。
// ・始まり：岩切の隊が岩切城へ逃げ込んだら攻城（鷹野の大目標が「敵を崩す」で、戦える隊が残っているとき）。
// ・城は一つの部隊。城内の隊は城の中身で、個別には戦わず、狙われない。城が持つ値は 兵・兵糧・士気・門 の四つ。
//   兵糧：城の蓄え（人日。籠もった兵の20日分）を城兵の数で割った「あと何日」。攻め手には見えない。毎朝6時に城兵の数だけ減る。
//   士気：下がるのは、兵糧が切れたとき（毎朝15、兵も5％逃げ散る）と、調略の流言のときだけ。打ち合いでは下がらない。0で開城。
//   門：力攻めでしか削れない。0で落城。
// ・囲み：城のまわりの6つの持ち場（城から450m）。持ち場は、隊が付いている（150m）か、付城が築かれていれば埋まる。
//   埋まっていない持ち場が一つでもあれば、夜（0時）のうちに、空いた持ち場一つにつき城兵の4分の1日分の兵糧が入り、
//   後詰を頼む使いも抜け出せる（見回りの本人がいれば、半分は捕らえる）。付城は、隊がその持ち場で2日かけて普請する。
// ・攻め手の兵糧は本陣の蓄え（日）。毎朝1日分減る。2日ごとに鷹野城から小荷駄が3日分を運ぶ（朝9時に出る）（道で賊に奪われることがある）。
//   長陣で毎朝、攻め手の全軍士気が1下がる。兵糧が尽きると6時間ごとに15下がる。囲みの陣で休む隊は、兵糧があれば1時間ごとに士気が5戻る。
// ・力攻め：昼（6時〜18時）だけ城門に寄せる。城の利は大きい（城が受ける打撃は3分の1）。門は、城門に寄せた兵が城兵の10倍あれば
//   3日（昼36時間）で尽き、2倍以下では削れない。門が立っている間、城兵はほとんど討たれない。寄せた側は3日で兵の2割ほどを失う。夜は持ち場へ戻る。
// ・勧告：条件（帰参・退去・切腹）を選んで降伏の使者を出す（1日に一度。総大将が認めたとき）。受けるかは城主が決める：
//   城の士気・残りの兵糧（外からは見えない）・削れた門・兵の差・後詰の当て・城主の気性と心証。
// ・調略：城内の武将への密書（内応の約束 → 合図）と、流言（城の士気を10下げる。1日に一度）。
// ・後詰：城を抜けた使いが沢渡城に着くと、沢渡信房が後詰を出すか決める。出すなら、半日で支度して沢渡信直の隊が城へ向かい、
//   囲みの隊に当たる（その間は岩切方として戦う）。破れれば城は当てを失い、勧告を受けやすくなる。
// ・本人の毎日の役目（兵のないとき、総大将から朝に申し付けられる）：付城の普請・小荷駄の護衛・夜の見回り・勧告の使者。
//   果たせば勲功（論功行賞の勲功に足す）。力攻めで門を破ったとき、城門のそばにいれば一番乗り。
public sealed class ProtoSiege
{
    public long Since { get; set; }
    public long CampAt { get; set; }      // 城の前に陣を敷いた時刻（0＝まだ）
    public string Method { get; set; } = "包囲";
    public string PrevMethod { get; set; } = "包囲"; // 変える前の攻め方（使番が着くまでは、こちらで動く）
    public long MethodAt { get; set; }
    public bool Ringed { get; set; }      // 6つの持ち場がみな埋まっている
    public int Gate { get; set; } = 100;  // 門
    public double GateHits { get; set; }
    public int Store { get; set; }        // 城の蓄え（人日）
    public int StoreAtStart { get; set; }
    public int Days { get; set; }
    public long SallyUntil { get; set; }
    public long ParleyAt { get; set; } = -1;
    public string Terms { get; set; } = "";
    public long TalksUntil { get; set; }  // 使者の返事が戻る時刻（0＝交渉していない。-1＝使者が城門へ向かっている）
    public long EnvoyBy { get; set; }     // 本人が使者のとき、ここまでに城門へ着かなければ別の者が行く
    public string Envoy { get; set; } = ""; // 本人でない使者（城門まで歩いて行く）
    public int EnvoyMen { get; set; }       // 使者が持ち場に置いていった兵
    public string Plot { get; set; } = "";   // 調略：""・密書・約束・断られた・済
    public string PlotWith { get; set; } = "";
    public long PlotAt { get; set; }
    public long RumorDay { get; set; } = -1;
    public int[] FortWork { get; set; } = new int[6]; // 付城の普請（半分ずつの分）
    public bool[] Forts { get; set; } = new bool[6];
    public Dictionary<string, int> Duty { get; set; } = []; // 隊の受け持ちの持ち場
    public Dictionary<string, int> Built { get; set; } = []; // 隊ごとの、築き上げた付城の数
    public string Relief { get; set; } = ""; // 後詰：""・使い・出陣・向かう・着いた・来ない・破れた
    public long ReliefAt { get; set; }
    public long ConvoyDay { get; set; } = -1;
    public string ConvoyLeg { get; set; } = "";
    public Dictionary<string, double> Carry { get; set; } = []; // 城の打ち合いの端数（1に満たない打撃を、たまるまで持ち越す） // 小荷駄：""（城にいる）・行き・帰り
}

public sealed partial class ProtoWorld
{
    public static readonly string[] SiegeMethods = ["力攻め", "包囲"];
    const string FoeCastle = "iwakiri-castle";
    const int Sectors = 6, StoreDays = 20, FortHalfMinutes = 2 * 24 * 60 * 2;
    const long PostRadius = 450_00, HoldRange = 150_00;
    public const long SiegeRingRadius = PostRadius;
    bool InFoeCastle(ProtoPerson u) => Dist(u, castle: true) <= 180_00;
    public const long CastleRadius = 190_00;
    public (long X, long Y) GatePoint { get { var c = Map.Site(FoeCastle); return (c.X * 100L, c.Y * 100L + CastleRadius - 40_00); } }
    bool CastleStands => Flag("rebellion") && War.Result == "" && War.Phase is "行軍" or "合戦";
    bool InWalls(ProtoPerson u) => Dist(u, castle: true) <= CastleRadius + 10_00;
    // 城内の隊：城という一つの部隊の中身。後詰（沢渡の隊）は城に入らない。
    public bool Sheltered(ProtoPerson u) => u.House == Rebel && u.Active && CastleStands && InWalls(u) && !War.Target.ContainsKey(u.Id) && u.Id != Nobunao
        && (War.Siege != null || War.Routed.Contains(u.Id) || Withdrawing(u) || !Flag("rebels-out"));
    public IEnumerable<ProtoPerson> Garrison => People.Where(r => r.Soldiers > 0 && Sheltered(r));
    int GarrisonMen => Garrison.Sum(u => u.Soldiers);
    const long AssaultReach = 110_00;
    (long X, long Y) AssaultPoint(ProtoPerson u)
    {
        var c = Map.Site(FoeCastle); long cx = c.X * 100L, cy = c.Y * 100L; var g = GatePoint;
        double dx = g.X - cx, dy = g.Y - cy, len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy)); dx /= len; dy /= len;
        var order = People.Where(InArmy).OrderByDescending(x => x.Id == Heir).ThenBy(x => x.Id).Select(x => x.Id).ToList(); int i = Math.Max(0, order.IndexOf(u.Id));
        int side = i == 0 ? 0 : (i % 2 == 1 ? 1 : -1) * ((i + 1) / 2);
        return (g.X + (long)(dx * 75_00 - dy * side * 50_00), g.Y + (long)(dy * 75_00 + dx * side * 50_00));
    }
    // 城が立っている間、城の内へは入れない：行き先が城の内なら、外（＋35m）へ押し出す。
    (long X, long Y) OutsideWalls(long x, long y, long fromX, long fromY)
    {
        if (!CastleStands) return (x, y);
        var c = Map.Site(FoeCastle); long cx = c.X * 100L, cy = c.Y * 100L;
        long r = CastleRadius + 35_00; double dx = x - cx, dy = y - cy, d = Math.Sqrt(dx * dx + dy * dy);
        if (d >= r) return (x, y);
        if (d < 1) { dx = fromX - cx; dy = fromY - cy; d = Math.Max(1, Math.Sqrt(dx * dx + dy * dy)); }
        return (cx + (long)(dx / d * r), cy + (long)(dy / d * r));
    }

    // ---- 力攻め：城門に寄せた攻め手と、城（城内の兵の合計）の打ち合い（Fight から呼ぶ） ----
    static bool Daytime(long second) => second % Day is >= 6 * Hour and < 18 * Hour;
    void CastleFight(ProtoPerson[] all, Dictionary<string, double> hits, Dictionary<(string, string), double> dealt)
    {
        var s = War.Siege; if (s == null || War.Result != "" || Talking || !Daytime(Second)) return;
        var g = Garrison.ToArray(); int men = g.Sum(u => u.Soldiers); if (men == 0) return;
        var gate = GatePoint;
        var att = all.Where(a => InArmy(a) && a.Soldiers > 0 && !War.Routed.Contains(a.Id) && !Retreating(a) && Dist(a, gate.X, gate.Y) <= AssaultReach).ToArray(); if (att.Length == 0) return;
        var lord = g.OrderByDescending(u => u.Id == HeadOf(Rebel)).ThenBy(u => u.Id).First(); double into = 0; int morale = CastleMorale;
        // 城内の武将の能力が、城の能力になる：守りは城内でいちばん高い統率、打ち返しはいちばん高い武勇。
        int castleCmd = g.Max(Cmd), castleVal = g.Max(Val);
        foreach (var a in att)
        {
            Set("fought:" + a.Id);
            double atk = a.Soldiers * 0.006 * (Val(a) / 50.0) * TroopAttack(a) * (a.Morale / 100.0 + 0.5) * (SpiritOf("takano") / 200.0 + 0.5) * 0.004 / (castleCmd / 50.0); // 城の利：門が立っている間、城兵はほとんど討たれない（塀と櫓の内）
            if (Confused(a.Id)) atk *= 0.5; if (Buffed("spear", a.Id)) atk *= Apt(a) ? 2.0 : 1.8;
            into += atk;
            dealt[(a.Id, lord.Id)] = dealt.GetValueOrDefault((a.Id, lord.Id)) + atk;
            a.Doing = "岩切城の城門を攻めている"; War.Gauge[a.Id] = Math.Min(100, GaugeOf(a.Id) + (a.Player ? 12 : 6));
        }
        // 門：寄せた兵が城兵の10倍で昼36時間、2倍以下では削れない。
        double ratio = att.Sum(a => a.Soldiers) / (double)men;
        s.GateHits += 100.0 / (36 * 60) * Math.Clamp((ratio - 2) / 8, 0, 1.5);
        // 打撃は1に満たないことが多いので、端数を持ち越して、たまった分だけ兵を減らす（Fight は打たれれば最低1減らすため）。
        void Hit(string id, double h) { double c = s.Carry.GetValueOrDefault(id) + h; int whole = (int)c; s.Carry[id] = c - whole; if (whole > 0) hits[id] = hits.GetValueOrDefault(id) + whole; }
        foreach (var u in g) { Hit(u.Id, into * u.Soldiers / men); Set("fought:" + u.Id); }
        double back = men * 0.006 * (castleVal / 50.0) * (morale / 100.0 + 0.5) * 0.1; int total = att.Sum(a => a.Soldiers); // 寄せた側は、3日寄せれば兵の2割ほどを失う
        foreach (var a in att)
        {
            double h = back * a.Soldiers / total / (Cmd(a) / 50.0); if (Buffed("guard", a.Id)) h *= 0.5; if (Confused(a.Id)) h *= 1.5;
            Hit(a.Id, h); dealt[(lord.Id, a.Id)] = dealt.GetValueOrDefault((lord.Id, a.Id)) + h;
        }
    }
    void WearGate()
    {
        var s = War.Siege; if (s == null || s.Gate <= 0 || s.GateHits < 1) return;
        int d = (int)s.GateHits; s.GateHits -= d; s.Gate = Math.Max(0, s.Gate - d);
        if (s.Gate > 0) return;
        // 一番乗り：門が破れたとき、本人が城門のそばで戦っていれば。
        var g = GatePoint; if (InArmy(Player) && Player.Soldiers > 0 && Dist(Player, g.X, g.Y) <= AssaultReach) { Set("ichiban"); Happen("ichiban", $"{Player.Name}が岩切城に一番乗りした。", g.X, g.Y, [.. People.Where(u => InArmy(u) && u.Active)]); }
        FallCastle("岩切城の門が破られた。");
    }

    IEnumerable<ProtoPerson> AbleArmy => People.Where(u => InArmy(u) && u.Active && u.Soldiers > 0 && !War.Routed.Contains(u.Id));
    ProtoPerson SiegeLead => AbleArmy.OrderByDescending(u => u.Id == Heir).ThenByDescending(u => u.Id == Karou).ThenBy(u => u.Id).FirstOrDefault() ?? Person(Heir);
    (long X, long Y) SiegeCamp => RoadPoint(550_00);

    // ---- 囲みの持ち場 ----
    // 持ち場 i の場所：本陣の側を0として、60度ずつ。
    public (long X, long Y) SectorPost(int i)
    {
        var c = Map.Site(FoeCastle); long cx = c.X * 100L, cy = c.Y * 100L; var camp = SiegeCamp;
        double a = Math.Atan2(camp.Y - cy, camp.X - cx) + i * Math.PI * 2 / Sectors;
        return ProtoMove.Nearest(Map, cx + (long)(Math.Cos(a) * PostRadius), cy + (long)(Math.Sin(a) * PostRadius));
    }
    // 持ち場の名前は、城から見た方角（北・北東…）。6つは60度ずつ離れているので、同じ名前にはならない。
    public string SectorName(int i)
    {
        var c = Map.Site(FoeCastle); var p = SectorPost(i); double a = Math.Atan2(-(p.Y - c.Y * 100L), p.X - c.X * 100L) * 180 / Math.PI; // 北が上
        string[] dir = ["東", "北東", "北", "北西", "西", "南西", "南", "南東"]; return dir[(int)Math.Round(((a % 360) + 360) % 360 / 45) % 8] + "の持ち場";
    }
    public string? SectorAt(long x, long y) { for (int i = 0; i < Sectors; i++) if (SectorPost(i) == (x, y)) return SectorName(i); return null; }
    public int FortProgress(int i) => War.Siege is { } s ? (s.Forts[i] ? 100 : Math.Min(99, s.FortWork[i] * 100 / FortHalfMinutes)) : 0;
    bool Holds(ProtoPerson u, int i) { var p = SectorPost(i); return Dist(u, p.X, p.Y) <= HoldRange; }
    public bool SectorHeld(int i) => War.Siege is { } s && (s.Forts[i] || AbleArmy.Any(u => Holds(u, i)));
    public int OpenSectors => War.Siege == null ? 0 : Enumerable.Range(0, Sectors).Count(i => !SectorHeld(i));
    public bool SectorFort(int i) => War.Siege?.Forts[i] ?? false;

    // 囲む隊の受け持ち：総大将は本陣の側（0）。ほかの隊は、付城の無い持ち場を一つずつ受け持ち、築き終えたら次へ移る。
    int DutyOf(ProtoPerson u) => u.Id == SiegeLead.Id ? 0 : War.Siege!.Duty.TryGetValue(u.Id, out var d) ? d : -1;
    void AssignDuties(ProtoPerson[] hold)
    {
        var s = War.Siege!; var lead = SiegeLead;
        foreach (var u in hold.Where(u => u.Id != lead.Id).OrderBy(u => u.Id))
        {
            int now = s.Duty.TryGetValue(u.Id, out var d) ? d : -1;
            if (now >= 0 && !s.Forts[now]) continue;
            var taken = s.Duty.Where(kv => kv.Key != u.Id).Select(kv => kv.Value).ToHashSet(); taken.Add(0);
            var free = Enumerable.Range(1, Sectors - 1).Where(i => !s.Forts[i] && !taken.Contains(i)).OrderBy(i => Dist(u, SectorPost(i).X, SectorPost(i).Y)).ToArray();
            if (free.Length > 0) s.Duty[u.Id] = free[0];
            else if (now < 0) s.Duty[u.Id] = Enumerable.Range(1, Sectors - 1).OrderBy(i => taken.Contains(i)).ThenBy(i => i).First();
        }
    }
    // 付城の普請（毎分）：受け持ちの持ち場にいる隊が1、普請の役目の本人が0.5ずつ進める。2日（隊一つ）で築ける。
    void BuildForts(ProtoPerson[] hold)
    {
        var s = War.Siege!;
        for (int i = 0; i < Sectors; i++)
        {
            if (s.Forts[i]) continue;
            int work = hold.Count(u => DutyOf(u) == i && Holds(u, i) && ClashOf(u.Id) == null) * 2;
            if (DutyStep("build") is { } st && Dist(Player, st.X, st.Y) <= 100_00 && SectorPost(i) == (st.X, st.Y)) work += 1;
            if (work == 0) continue;
            s.FortWork[i] += work;
            if (s.FortWork[i] < FortHalfMinutes) continue;
            s.Forts[i] = true; var p = SectorPost(i);
            foreach (var u in hold.Where(u => DutyOf(u) == i && Holds(u, i))) s.Built[u.Id] = s.Built.GetValueOrDefault(u.Id) + 1;
            Happen($"fort:{i}", $"岩切城の囲みの{SectorName(i)}に付城が築かれた。", p.X, p.Y, [.. People.Where(u => InArmy(u) && u.Active)]);
        }
    }

    // ---- 攻城の始まり ----
    bool CanBesiege => War.Siege == null && GoalOf("takano") == Goals[0] && AbleArmy.Any() && Units.Any(u => u.House == Rebel && StillFights(u));
    void StartSiege()
    {
        var k = Person(HeadOf(Rebel));
        War.Siege = new ProtoSiege { Since = Second };
        War.RetreatBy.Remove(Rebel); War.RetreatAt.Remove(Rebel); War.RetreatRules[Rebel] = []; War.Goals[Rebel] = Goals[1];
        foreach (var r in People.Where(r => r.House == Rebel))
        {
            foreach (var f in new[] { "retreat-heard:", "withdrawing:", "retreat-at:", "stays:" }) flags.Remove(f + r.Id);
            War.Target.Remove(r.Id);
        }
        foreach (var u in People.Where(InArmy)) War.Target.Remove(u.Id);
        if (War.RetreatRules.TryGetValue("takano", out var rules)) { rules.Remove("dusk"); if (!rules.Contains("supply")) rules.Add("supply"); } // 攻城は日をまたぐので、夕暮れの条件は外す
        // 城に入った兵は気を取り直す（士気60まで）。蓄えは、岩切勢の兵（城の外にいる隊も含めて）の20日分。
        foreach (var r in People.Where(r => r.House == Rebel && r.Active && r.Soldiers > 0)) r.Morale = Math.Max(r.Morale, 60);
        var s = War.Siege; s.Store = s.StoreAtStart = Math.Max(1, People.Where(r => r.House == Rebel && r.Active).Sum(r => r.Soldiers)) * StoreDays;
        Happen("siege", "岩切勢は岩切城に籠もった。", k.X, k.Y, [.. AbleArmy, k]);
    }

    public int GateOf => War.Siege?.Gate ?? 0;
    // 城の兵糧（あと何日）。外からは見えない（画面には出さない）。
    public int CastleFoodDays { get { var s = War.Siege; int men = GarrisonMen; return s == null ? 0 : men == 0 ? 99 : s.Store / men; } }
    double SiegeRatio { get { int men = People.Where(r => r.House == Rebel && r.Active && r.Id != Nobunao && (Sheltered(r) || !War.Routed.Contains(r.Id))).Sum(r => r.Soldiers); return men == 0 ? 99 : AbleArmy.Sum(u => u.Soldiers) / (double)men; } } // 城の外の岩切勢も数える
    // 画面用：本人が知っている城の兵糧の見込み（兆しを見たときだけ）。
    public int? SiegeKnownMorale => flags.TryGetValue("castle-scout-morale", out var v) ? (int)v : null;
    public string KnownCastleFood => War.Siege == null ? "" : Facts.Any(f => f.Id.StartsWith("starving:") && Knows(Player, f.Id)) ? "尽きた" : Knows(Player, "castle-food:scarce") ? "乏しい" : Knows(Player, "castle-food:low") ? "減っている" : flags.ContainsKey("castle-scout-food") ? "多い" : "";
    // 物見の場所：本陣の側、城から420m（城門の矢の届かない所）。
    (long X, long Y) ScoutPost { get { var c = Map.Site(FoeCastle); var p = SectorPost(0); double a = Math.Atan2(p.Y - c.Y * 100L, p.X - c.X * 100L) + 0.45; return ProtoMove.Nearest(Map, c.X * 100L + (long)(Math.Cos(a) * 420_00), c.Y * 100L + (long)(Math.Sin(a) * 420_00)); } } // 総大将の持ち場から少し横へ（駒に重ならない）
    // 囲みの輪の上を回る道：まっすぐ行くと城の近く（300m）を通るなら、行き先に近い側の隣の持ち場の外側へまず行く。
    (long X, long Y) RingWay(long tx, long ty, ProtoPerson? who = null)
    {
        who ??= Player;
        var c = Map.Site(FoeCastle); double cx = c.X * 100L, cy = c.Y * 100L, px = who.X, py = who.Y, vx = tx - px, vy = ty - py, l2 = Math.Max(1, vx * vx + vy * vy);
        double k = Math.Clamp(((cx - px) * vx + (cy - py) * vy) / l2, 0, 1), nx = px + vx * k - cx, ny = py + vy * k - cy;
        if (Math.Sqrt(nx * nx + ny * ny) > 300_00 || War.Siege == null) return (tx, ty);
        double a0 = Math.Atan2(py - cy, px - cx), a1 = Math.Atan2(ty - cy, tx - cx), d = Math.IEEERemainder(a1 - a0, Math.PI * 2), a = a0 + Math.Sign(d) * Math.PI / 3;
        if (Math.Abs(d) <= Math.PI / 3) return (tx, ty); // 行き先と同じ側まで来たら、まっすぐ（城の前や城門へ寄るときも）
        return ((long)(cx + Math.Cos(a) * (PostRadius + 60_00)), (long)(cy + Math.Sin(a) * (PostRadius + 60_00)));
    }
    // 画面用：本人が知っている攻め方（軍議で決まったと知るまでは出さない）。
    public string KnownSiegeMethod => War.Siege is { } s && Facts.Any(f => f.Id.StartsWith("siege-method:") && Knows(Player, f.Id)) ? s.Method : "";
    // 総大将の案：兵の差が大きければ力攻め（勇猛なら5倍、ほかは8倍から）、そうでなければ包囲。
    public string SiegeProposal => SiegeRatio >= (Person(Heir).Temper == "勇猛" ? 5 : 8) ? SiegeMethods[0] : SiegeMethods[1];
    static string MethodText(string m) => m == SiegeMethods[0] ? "力攻めで落とす" : "囲んで兵糧を断つ";
    public string SiegeSpeech => $"{SiegeLead.Name}「岩切城は、{MethodText(Flag("siege-decided") ? War.Siege!.Method : SiegeProposal)}。」";
    public bool Garrisoned(ProtoPerson u) => Sheltered(u);
    public bool CastleFighting => War.Siege != null && War.Clashes.Any(c => Sheltered(Person(c.A)) || Sheltered(Person(c.B)));
    public (long X, long Y)? PlayerSiegePost => War.Siege != null && War.Result == "" && InArmy(Player) ? SiegePost(Player) : null;
    // 攻城の軍議：本陣（300m）で、いつでも開ける（本陣を取られたあとは、総大将のそば）。
    public bool SiegeCouncilOpen => War.Siege != null && War.Result == "" && InArmy(Player)
        && (answering || (CampAt("takano") is { } hq && !CampLost("takano") ? Dist(Player, hq.X, hq.Y) <= 300_00 || CampMoveTarget is { } mv && Dist(Player, mv.X, mv.Y) <= 300_00 : Dist(Player, SiegeLead) <= 300_00));
    string DoSiege(string id) => id == "調略" ? Scheme() : id == "流言" ? Rumor() : id.StartsWith("勧告:") ? Recommend(id[3..]) : DecideSiege(id);
    string DecideSiege(string? want)
    {
        var s = War.Siege; if (s == null || War.Result != "") return "";
        if (want != null && !SiegeCouncilOpen) return "";
        if (want == SiegeMethods[0] && s.TalksUntil != 0) return $"{SiegeLead.Name}「使者が戻るまでは攻めぬ。」";
        if (want == SiegeMethods[0] && SiegeRatio < 3) return $"{SiegeLead.Name}「この兵で力攻めなど、無駄死にさせるだけじゃ。」";
        string prop = Flag("siege-decided") ? s.Method : SiegeProposal; bool present = want != null && SiegeMethods.Contains(want), refused = present && want != prop && !CommanderListens;
        string next = present && !refused ? want! : prop; var lead = SiegeLead; bool first = !Flag("siege-decided"); Set("siege-decided");
        if (next == s.Method && !present && !first) return ""; // 初めて決めたときは、同じ攻め方でも皆に知らせる
        if (next != s.Method) { s.PrevMethod = s.Method; s.MethodAt = Second; }
        s.Method = next;
        foreach (var u in People.Where(InArmy)) War.Target.Remove(u.Id);
        Happen("siege-method:" + Second, $"軍議で決まった：岩切城は、{MethodText(s.Method)}。", lead.X, lead.Y, [.. People.Where(u => InArmy(u) && u.Active && Dist(u, lead) <= 2500_00)]); // 兵のない本人にも、使番が伝える
        return !present ? "" : refused ? $"{lead.Name}「いや、{MethodText(prop)}。」" : want == prop ? $"{lead.Name}「うむ。」" : $"{lead.Name}「よかろう。{MethodText(s.Method)}。」";
    }

    // 囲む隊の陣：受け持ちの持ち場（総大将は本陣の側）。
    (long X, long Y) SiegePost(ProtoPerson u) { if (War.Siege == null) return SiegeCamp; int d = DutyOf(u); return d < 0 ? SiegeCamp : SectorPost(d); } // 総大将は持ち場0（本陣より城寄り。本陣の札と、控える本人に重ならない）

    // ---- 毎分（合戦の間、攻城になっていれば） ----
    void SiegeMoves(ProtoPerson[] takano, ProtoPerson[] rebels)
    {
        var s = War.Siege!; var castle = Map.Site(FoeCastle); long cx = castle.X * 100L, cy = castle.Y * 100L;
        UpdatePlan();
        foreach (var r in rebels.Where(r => Withdrawing(r) && InFoeCastle(r))) { flags.Remove("withdrawing:" + r.Id); r.Path = []; r.PathIndex = 0; r.Doing = "城を守っている"; }
        // 城へ逃げ込んだ隊は、城の兵に戻る（士気60まで立て直す）。
        foreach (var r in People.Where(r => r.House == Rebel && r.Active && r.Soldiers > 0 && InWalls(r) && War.Routed.Contains(r.Id) && r.Id != Nobunao).ToArray())
        { War.Routed.Remove(r.Id); War.AtCamp.Remove(r.Id); r.Morale = Math.Max(r.Morale, Math.Min(60, (int)flags.GetValueOrDefault("sally-morale", 60))); r.Path = []; r.PathIndex = 0; r.Doing = "城を守っている"; }
        var auto = takano.Where(u => Auto(u) && !War.Routed.Contains(u.Id) && u.Id != s.Envoy).ToArray();
        // 本人でない使者：城門まで歩き、着いたら門を叩く。返事が出たら持ち場へ戻る。
        if (s.Envoy != "")
        {
            var e = Person(s.Envoy); var g = GatePoint;
            var home = SiegePost(e);
            if (!e.Active || s.TalksUntil == 0 && Dist(e, home.X, home.Y) <= 120_00) EnvoyBack(); // 持ち場に戻ってから兵を返す（城門の前で兵を戻すと、そのまま門を攻めてしまう）
            else if (s.TalksUntil == 0) { if (!e.Moving) { var (wx, wy) = RingWay(home.X, home.Y, e); Walk(e, ProtoMove.Nearest(Map, wx, wy), "囲みの陣", "持ち場へ戻っている"); } }
            else if (s.TalksUntil == -1 && Dist(e, g.X, g.Y) <= 80_00) { s.TalksUntil = Second + 30 * 60; e.Path = []; e.PathIndex = 0; e.Doing = "使者として城門にいる"; Happen("envoy-at:" + Second, $"{e.Name}が使者として岩切城の門を叩いた。", e.X, e.Y, [.. People.Where(u => InArmy(u) && u.Active), Person(HeadOf(Rebel))]); }
            else if (s.TalksUntil == -1 && !e.Moving) { var (wx, wy) = RingWay(g.X, g.Y, e); Walk(e, ProtoMove.Nearest(Map, wx, wy), "城門", "使者として城門へ向かっている"); }
        }
        foreach (var (id, tid) in War.Target.ToArray()) if (Person(id).House != Rebel && Sheltered(Person(tid))) War.Target.Remove(id);
        var lead = SiegeLead; var camp = SiegeCamp;
        if (s.CampAt == 0 && Dist(lead, camp.X, camp.Y) <= 150_00) { s.CampAt = Second; Happen("siege-camp", $"{lead.Name}勢は岩切城の前に陣を敷いた。", lead.X, lead.Y, [.. AbleArmy.Where(u => Dist(u, lead) <= 2500_00)]); }
        if (s.CampAt > 0 && Second >= s.CampAt + 30 * 60 && !Flag("siege-decided") && s.TalksUntil == 0) DecideSiege(null);
        // 囲みが埋まったか・解けたか。
        bool ring = s.CampAt > 0 && OpenSectors == 0;
        if (ring != s.Ringed) { s.Ringed = ring; Happen($"ring:{ring}:{Second}", ring ? "岩切城の囲みが閉じた。" : "岩切城の囲みに穴があいた。", lead.X, lead.Y, [.. People.Where(u => InArmy(u) && u.Active)]); }
        // 攻め方は使番が運ぶ：着いた隊から新しい攻め方で動く。力攻めでも、夜は持ち場へ戻る。
        string MethodFor(ProtoPerson u) => Second >= s.MethodAt + (RunnerAt(u) - Second) ? s.Method : s.PrevMethod;
        var storm = auto.Where(u => MethodFor(u) == SiegeMethods[0] && Daytime(Second) && s.TalksUntil == 0).ToArray(); var hold = auto.Except(storm).ToArray();
        if (Second % 600 == 0) AssignDuties(auto);
        foreach (var u in storm.Where(u => !War.Target.ContainsKey(u.Id)))
        {
            var outside = rebels.Where(r => !Sheltered(r) && !Withdrawing(r) && (InSight(u, r) || ClashOf(r.Id) != null)).OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault();
            if (outside != null) { War.Target[u.Id] = outside.Id; continue; }
            var at = AssaultPoint(u);
            if (!u.Moving && Dist(u, at.X, at.Y) > 40_00) Walk(u, at, "城門", "岩切城の城門へ寄せている");
        }
        foreach (var u in hold)
        {
            if (War.Target.TryGetValue(u.Id, out var tid) && ClashOf(u.Id) == null && Dist(Person(tid), cx, cy) <= 250_00) War.Target.Remove(u.Id);
            if (War.Target.ContainsKey(u.Id)) continue;
            var outside = rebels.Where(r => !Sheltered(r) && !Withdrawing(r) && Dist(r, cx, cy) > 250_00 && (InSight(u, r) || ClashOf(r.Id) != null)).OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault();
            if (outside != null) { War.Target[u.Id] = outside.Id; continue; }
            var post = SiegePost(u);
            if (!u.Moving && ClashOf(u.Id) == null && Dist(u, post.X, post.Y) > 100_00) Walk(u, post, "囲みの陣", s.Forts[Math.Max(0, DutyOf(u))] ? "岩切城を囲んでいる" : "付城を築いている");
            else if (!u.Moving && ClashOf(u.Id) == null) u.Doing = DutyOf(u) is var d && d >= 0 && !s.Forts[d] ? "付城を築いている" : "岩切城を囲んでいる";
        }
        if (s.CampAt > 0) BuildForts(auto);
        SiegeTalks();
        if (War.Result != "") return;
        Relief();
        if (War.Result != "") return;
        SiegeDays(takano, rebels);
        if (War.Result != "") return;
        Convoy();
        Duties();
    }

    void SiegeDays(ProtoPerson[] takano, ProtoPerson[] rebels)
    {
        var s = War.Siege!; var k = Person(HeadOf(Rebel)); var lead = SiegeLead; long ofDay = Second % Day;
        if (Second <= s.Since + Hour) return;
        // 夜（0時）：空いた持ち場から兵糧が入る。後詰を頼む使いも抜ける。
        if (ofDay == 0)
        {
            int open = OpenSectors, men = GarrisonMen;
            if (open > 0 && men > 0)
            {
                s.Store = Math.Min(s.StoreAtStart, s.Store + open * men / 12);
                Happen("smuggle:" + DayIndex, $"夜のうちに、囲みの穴から岩切城へ荷が運び込まれたらしい。", lead.X, lead.Y, [.. People.Where(u => InArmy(u) && u.Active)]);
            }
            if (open > 0 && s.Relief == "" && k.Active)
            {
                bool caught = DutyStep("watch") != null && Dist(Player, castle: true) <= 800_00 && Roll("courier:" + DayIndex) % 2 == 0;
                if (caught) Happen("courier-caught:" + DayIndex, "見回りの者が、岩切城を抜け出そうとした使いを捕らえた。沢渡へ後詰を頼む書状を持っていた。", Player.X, Player.Y, [Player, lead]);
                else { s.Relief = "使い"; var sw = Map.Site("sawatari-castle"); s.ReliefAt = Second + (long)Math.Sqrt(Math.Pow(sw.X * 100L - Map.Site(FoeCastle).X * 100L, 2) + Math.Pow(sw.Y * 100L - Map.Site(FoeCastle).Y * 100L, 2)) / RunnerSpeed; Happen("courier", "岩切城から、沢渡へ後詰を頼む使いが囲みを抜けた。", k.X, k.Y, k); }
            }
        }
        // 朝6時：城兵が蓄えを食う。尽きていれば士気が落ち、兵が逃げ散る。攻め手も本陣の兵糧を1日分食う。
        if (ofDay == 6 * Hour)
        {
            s.Days++; int men = GarrisonMen; s.Store = Math.Max(0, s.Store - men);
            if (s.Store == 0 && men > 0)
            {
                foreach (var r in Garrison.ToArray()) { r.Morale = Math.Max(0, r.Morale - 15); r.Soldiers = Math.Max(1, r.Soldiers * 95 / 100); }
                Happen("starving:" + s.Days, "岩切城では兵糧が尽き、逃げ出す兵も出ている。", k.X, k.Y, [k, .. People.Where(u => InArmy(u) && u.Active)]);
            }
            // 外から見える兆し：炊煙が細る・上がらない。これで城の兵糧の見込みが分かる。
            int food = CastleFoodDays;
            // 蓄えが5日分を切ると、粥を薄めて食いつなぐ。兵糧切れが近いので、士気が毎朝少しずつ落ちる（2026-11-25 擬人格の印「士気が22日間60のまま」）。
            if (food <= 5 && s.Store > 0 && men > 0) foreach (var r in Garrison.ToArray()) r.Morale = Math.Max(0, r.Morale - 5);
            if (food <= 14 && s.Store > 0) Happen("castle-food:low", "岩切城の炊煙が細くなった。蓄えが減っているらしい。", k.X, k.Y, [.. People.Where(u => InArmy(u) && u.Active)]);
            if (food <= 5 && s.Store > 0) Happen("castle-food:scarce", "岩切城の炊煙がほとんど上がらない。蓄えは乏しいらしい。", k.X, k.Y, [.. People.Where(u => InArmy(u) && u.Active)]);
            War.Stock["takano"] = Math.Max(0, War.Stock.GetValueOrDefault("takano", 0) - 1); AddSpirit("takano", -1);
            Happen("siege-day:" + s.Days, $"岩切城を攻めて{s.Days}日。{(StockOf("takano") > 0 ? $"本陣の兵糧はあと{StockOf("takano")}日分" : "本陣の兵糧が尽きた")}。", lead.X, lead.Y, [.. AbleArmy]);
            if (CastleMorale <= 0 && Garrison.Any()) { OpenCastle("兵糧が尽きて城兵の士気が折れ、岩切城は門を開いた。"); return; }
        }
        if (Second % (6 * Hour) == 0 && StockOf("takano") <= 0) AddSpirit("takano", -15);
        // 囲みの陣で休む隊は、1時間ごとに士気が5戻る（兵糧があるとき。力攻めの日も、夜に戻る）。
        if (Second % Hour == 0 && StockOf("takano") > 0) foreach (var u in AbleArmy.Where(u => ClashOf(u.Id) == null && !(Daytime(Second) && s.Method == SiegeMethods[0]))) u.Morale = Math.Min(100, u.Morale + 5);
        // 朝5時：城方の朝駆け（兵糧が残り3日を切ったか、勇猛な城主が士気の高いとき）。囲みのいちばん小さい隊へ。
        var able = rebels.Where(r => !Withdrawing(r) && r.Soldiers >= 10 && r.Id != Nobunao).ToArray();
        if (ofDay == 5 * Hour && s.SallyUntil == 0 && s.TalksUntil == 0 && able.Length > 0 && (CastleFoodDays <= 3 || CastleMorale >= 70 && k.Temper == "勇猛"))
        {
            var victim = takano.Where(u => !War.Routed.Contains(u.Id) && Dist(u, castle: true) <= 900_00).OrderBy(u => u.Soldiers).ThenBy(u => u.Id).FirstOrDefault();
            if (victim != null)
            {
                s.SallyUntil = Second + 40 * 60; bool watch = DutyStep("watch") != null && Dist(Player, victim) <= 800_00;
                bool noticed = watch || Roll("sally:" + s.Days) % 100 < ScoutChance(victim);
                foreach (var r in able) { War.Target[r.Id] = victim.Id; if (noticed) War.AmbushUntil.Remove(r.Id); else War.AmbushUntil[r.Id] = Second + 15 * 60; }
                Happen("sally:" + s.Days, "岩切勢が城から朝駆けに出た。", k.X, k.Y, [.. able]);
                flags["sally-morale"] = CastleMorale; flags["sally-men"] = People.Where(r => r.House == Rebel && r.Active && r.Id != Nobunao).Sum(r => r.Soldiers);
                if (watch) Set("sally-spotted");
                if (noticed) Happen("sally-seen:" + s.Days, watch ? $"見回りの{Player.Name}が、城から出る岩切勢に気づいて急を告げた。" : $"{victim.Name}の見張りが、城から出る岩切勢に気づいた。", victim.X, victim.Y, watch ? [victim, Player] : [victim]);
            }
        }
        if (s.SallyUntil > 0 && (Second >= s.SallyUntil || People.Any(r => r.House == Rebel && r.Id != Nobunao && War.Routed.Contains(r.Id) && Facts.Any(f => f.Id.StartsWith("rout:" + r.Id + ":") && f.At >= s.SallyUntil - 40 * 60))))
        {
            s.SallyUntil = 0; var castle = Map.Site(FoeCastle);
            int lost = (int)flags.GetValueOrDefault("sally-men") - People.Where(r => r.House == Rebel && r.Active && r.Id != Nobunao).Sum(r => r.Soldiers);
            foreach (var (aid, tid) in War.Target.ToArray()) if (Person(tid).House == Rebel) War.Target.Remove(aid);
            Happen("sally-end:" + s.Days, $"朝駆けの岩切勢は城へ引き上げ始めた。{(lost > 0 ? $"城方は兵{lost}を失った。" : "打ち合いにならず、城方の兵は減らなかった。")}", k.X, k.Y, [.. People.Where(u => InArmy(u) && u.Active)]);
            foreach (var r in People.Where(r => r.House == Rebel && r.Active && r.Soldiers > 0 && !InFoeCastle(r) && r.Id != Nobunao).ToArray()) { War.Target.Remove(r.Id); Set("withdrawing:" + r.Id); Walk(r, Spot(castle, 3), castle.Id, castle.Name + "へ引き上げている"); } // 崩れた隊も城へ
        }
    }
    // 城方の者たちの行方：討ち取られた者と、捕らえられた者。
    internal string RebelFates()
    {
        var rs = People.Where(r => r.House == Rebel && r.Id != Nobunao).ToArray();
        var slain = rs.Where(r => !r.Active && (r.Doing == "討ち取られた" || Facts.Any(f => f.Id == "slain:" + r.Id))).Select(r => r.Name).ToArray(); var caught = rs.Where(r => r.Active).Select(r => r.Name).ToArray();
        var gone = rs.Where(r => !r.Active && !slain.Contains(r.Name)).Select(r => r.Name).ToArray();
        if (gone.Length > 0) return (slain.Length > 0 ? string.Join("と", slain) + "は討ち取られた。" : "") + (caught.Length > 0 ? string.Join("と", caught) + "は捕らえられた。" : "") + string.Join("と", gone) + "の行方は知れない。";
        return (slain.Length > 0 ? string.Join("と", slain) + "は討ち取られた。" : "") + (caught.Length > 0 ? string.Join("と", caught) + "は捕らえられた。" : "");
    }
    // 城の外から城の内へ歩くときの、門の外の一点（ここを経て入る）。
    (long X, long Y)? ThroughGate(ProtoPerson p, (long X, long Y) to)
    {
        var c = Map.Site(FoeCastle); long cx = c.X * 100L, cy = c.Y * 100L;
        double din = Math.Sqrt(Math.Pow(to.X - cx, 2) + Math.Pow(to.Y - cy, 2)), dme = Math.Sqrt(Math.Pow(p.X - cx, 2) + Math.Pow(p.Y - cy, 2));
        if (din >= CastleRadius || dme <= CastleRadius + 20_00) return null;
        return ProtoMove.Nearest(Map, cx, cy + CastleRadius + 50_00);
    }
    long Dist(ProtoPerson u, bool castle) { var c = Map.Site(FoeCastle); return Dist(u, c.X * 100L, c.Y * 100L); }

    bool BehindGate(ProtoPerson u) => Sheltered(u);
    public int CastleMorale { get { var g = Garrison.ToArray(); /* 崩れて逃げ込んだ隊も城の中身 */ int men = g.Sum(u => u.Soldiers); return men == 0 ? 0 : g.Sum(u => u.Morale * u.Soldiers) / men; } }
    // 城内の隊は崩れない（Fight から呼ぶ）。士気が0になれば門を開く。
    void GarrisonMorale(ProtoPerson[] all)
    {
        if (War.Siege == null || War.Result != "") return;
        if (Garrison.Any() && CastleMorale <= 0) OpenCastle("城兵の士気が折れ、岩切城は門を開いた。");
    }
    // 落城：門が破れた、城兵が尽きた、内応で門が開いた。
    void FallCastle(string text)
    {
        var c = Map.Site(FoeCastle); var k = Person(HeadOf(Rebel)); EndRelief();
        Happen("gate-broken", text, c.X * 100L, c.Y * 100L, [.. Units.Where(u => Dist(u, castle: true) <= 1000_00)]);
        War.Result = "鎮めた"; War.Phase = "終わり";
        Happen("rebellion-crushed", $"岩切城は落ち、岩切勝元の謀叛は鎮まった。{RebelFates()}", k.X, k.Y, [.. People.Where(u => InArmy(u) && u.Active), k]);
        foreach (var r in People.Where(r => r.House == Rebel && r.Active)) { r.Active = false; r.Path = []; r.PathIndex = 0; r.Doing = "捕らえられた"; }
        War.Clashes.Clear(); War.Target.Clear();
    }

    // ---- 攻め手の兵糧：2日ごとに、鷹野城から小荷駄が3日分を運ぶ ----
    const string Konida = "konida";
    void AddKonida() => People.Add(new ProtoPerson { Id = Konida, Name = "小荷駄", House = "", Role = "小荷駄", Home = "takano-castle", Active = false, X = Map.Site("takano-castle").X * 100L, Y = Map.Site("takano-castle").Y * 100L, Doing = "" });
    void Convoy()
    {
        var s = War.Siege!; var kd = People.FirstOrDefault(p => p.Id == Konida); if (kd == null || s.CampAt == 0) return;
        var home = Map.Site("takano-castle"); var camp = CampAt("takano");
        // 朝9時から、前の荷駄から2日たっていれば、鷹野城を出る。護衛の役目があれば、本人が着くまで11時まで待つ。
        bool escortReady = DutyStep("escort") == null || Dist(Player, home.X * 100L, home.Y * 100L) <= 120_00 || Second % Day >= 11 * Hour;
        bool escortHere = DutyStep("escort") != null && Dist(Player, home.X * 100L, home.Y * 100L) <= 120_00;
        if (Second % Day >= (escortHere ? 6 : 9) * Hour && Second % Day < 12 * Hour && Second % 60 == 0 && escortReady && s.ConvoyLeg == "" && DayIndex - s.ConvoyDay >= 2 && camp is { } c0)
        {
            s.ConvoyDay = DayIndex; s.ConvoyLeg = "行き"; kd.Active = true; kd.X = home.X * 100L; kd.Y = home.Y * 100L; kd.Path = []; kd.PathIndex = 0;
            Walk(kd, ProtoMove.Nearest(Map, c0.X, c0.Y + 60_00), "本陣", "兵糧を本陣へ運んでいる"); Set("convoy:" + DayIndex);
        }
        if (s.ConvoyLeg == "行き" && Escorted(kd) && People.Any(b => b.House == "bandit" && b.Active && Dist(b, kd) <= 250_00)) Happen("bandit-eye:" + DayIndex, "道の脇で賊が小荷駄の様子をうかがっていたが、護衛の侍を見て引き下がった。", kd.X, kd.Y, [Player]);
        if (s.ConvoyLeg == "行き" && Flag($"robbed:{Konida}:{DayIndex}")) { s.ConvoyLeg = "帰り"; Walk(kd, (home.X * 100L, home.Y * 100L), home.Id, "鷹野城へ逃げ帰っている"); return; }
        if (s.ConvoyLeg == "行き" && camp is { } c && !kd.Moving && Dist(kd, c.X, c.Y) <= 120_00)
        {
            War.Stock["takano"] = Math.Min(6, War.Stock.GetValueOrDefault("takano", 0) + 3);
            Happen("convoy-in:" + DayIndex, "小荷駄が本陣に兵糧を届けた。", c.X, c.Y, [.. AbleArmy.Where(u => Dist(u, c.X, c.Y) <= 1000_00)]);
            s.ConvoyLeg = "帰り"; Walk(kd, (home.X * 100L, home.Y * 100L), home.Id, "鷹野城へ戻っている");
        }
        else if (s.ConvoyLeg == "行き" && !kd.Moving && camp is { } c1) Walk(kd, ProtoMove.Nearest(Map, c1.X, c1.Y + 60_00), "本陣", "兵糧を本陣へ運んでいる"); // 本陣が移っていれば、移った先へ
        if (s.ConvoyLeg == "帰り" && !kd.Moving) { s.ConvoyLeg = ""; kd.Active = false; }
    }
    // 小荷駄は、護衛の本人が離れたら（80m）待つ（本人が1.5km以内にいるとき）。
    bool KonidaWaits(ProtoPerson p) => p.Id == Konida && War.Siege is { ConvoyLeg: "行き" } && DutyStep("escort") != null && Dist(Player, p) is > 80_00 and < 1500_00;
    // 賊は、侍が付いている荷は襲わない（Core/Proto/ProtoLife.cs の Bandits）。
    bool Escorted(ProtoPerson v) => People.Any(p => p.Active && p.Id != v.Id && (p.Player || p.Samurai && p.Soldiers > 0) && Dist(p, v) <= 100_00);

    // ---- 本人の毎日の役目（兵のないとき） ----
    ProtoStep? DutyStep(string what) => Tasks.FirstOrDefault(t => t.Kind == "duty" && t.State == TaskState.Open && t.Current?.Do == what)?.Current;
    public bool OnDuty => Tasks.Any(t => t.Kind == "duty" && t.State == TaskState.Open);
    void Duties()
    {
        var s = War.Siege!; if (s.CampAt == 0 || !InArmy(Player) || !Player.Active) return;
        bool idle = Player.Soldiers == 0 || War.Routed.Contains(Player.Id);
        // 朝6時5分：総大将から今日の役目。
        long tod = Second % Day;
        if (tod == 7 * Hour + 5 * 60 && DutyChoices.FirstOrDefault() is { } first) { PickDuty(first.Id, quiet: true); Note($"使番が来た。{SiegeLead.Name}から「{first.Title}」の申し付け。"); }
        if (tod == 10 * Hour + 5 * 60 && War.Result == "" && s.TalksUntil == 0 && !(s.ParleyAt >= 0 && Second / Day == s.ParleyAt / Day) && Garrison.Any() && (CastleMorale < 35 && CastleFoodDays <= 8 || CastleMorale < 20) && (CastleFoodDays <= 1 || flags.TryGetValue("parley-hint-day", out var hd) && DayIndex >= hd + 2))
        {
            SendEnvoy("退去");
            if (DutyStep("envoy") != null) Note(Dist(Player, SiegeLead) <= 300_00 ? $"{SiegeLead.Name}「城は弱った。そなたが使者に立ち、城を明け渡すよう勧めてまいれ。」" : $"使番が来た。{SiegeLead.Name}から「勧告の使者」の申し付け。城を明け渡すよう勧めてまいれ、とのこと。");
            return;
        }
        if (tod is 6 * Hour + 5 * 60 or 10 * Hour + 5 * 60 or 14 * Hour + 5 * 60 or 18 * Hour + 5 * 60 && idle && !OnDuty && !Tasks.Any(t => t.Kind == "duty" && t.State == TaskState.Offered))
        {
            var lead = SiegeLead; long dl = Second + 22 * Hour;
            if (Player.Soldiers > 0) { int n = Player.Soldiers; lead.Soldiers += n; Player.Soldiers = 0; Note($"残った兵{n}は{lead.Name}の隊に預けた。"); }
            int open = Enumerable.Range(1, Sectors - 1).Where(i => !s.Forts[i]).OrderByDescending(i => s.Duty.ContainsValue(i)).ThenBy(i => i).FirstOrDefault(); // 隊が築いている持ち場を手伝う（一人で空き地に立たせない）
            bool convoy = tod == 6 * Hour + 5 * 60 && s.ConvoyLeg == "" && DayIndex - s.ConvoyDay >= 2;
            if (!convoy && tod < 14 * Hour && Facts.Any(f => f.Id == "watch-done:" + DayIndex)) { Player.Doing = "見回り明けで休んでいる"; return; } // 見回り明けの朝は休む（小荷駄の護衛だけは出る）
            ProtoTask? t = null;
            if (convoy)
            {
                var esc = Offer(lead.Id, "小荷駄の護衛", "duty", Second + 2 * Hour, dl, 0, new ProtoStep("escort", "鷹野城から本陣まで小荷駄を送る。着きしだい出る", Person: Konida)); var kd = Person(Konida); var hm = Map.Site("takano-castle"); kd.Active = true; kd.X = hm.X * 100L; kd.Y = hm.Y * 100L; kd.Path = []; kd.PathIndex = 0; kd.Doing = "荷を積んでいる";
                ProtoTask other;
                if (open > 0) { var bp = SectorPost(open); other = Offer(lead.Id, "付城の普請", "duty", Second + 2 * Hour, Second + 12 * Hour, 0, new ProtoStep("build", $"{SectorName(open)}で付城の普請を手伝う", X: bp.X, Y: bp.Y)); flags["duty-need:" + other.Id] = Math.Clamp((18 * Hour - tod) / 60 - 60, 60, 6 * 60); }
                else { string[] pool = ["scout", "letter", "lookout"]; other = SiegeDuty(pool[Math.Abs(Roll("duty-pick:" + DayIndex)) % 3], lead); }
                esc.Speech = DutySpeech(esc.Title); other.Speech = DutySpeech(other.Title);
                flags["choice:" + esc.Id] = DayIndex; flags["choice:" + other.Id] = DayIndex;
                Note(Dist(Player, lead) <= 300_00 ? $"{lead.Name}「今日は「{esc.Title}」か「{other.Title}」だ。どちらを務める。」" : $"使番が来た。{lead.Name}から、今日の役目を「{esc.Title}」か「{other.Title}」から選べとのこと。");
                return;
            }
            else if (open == 0 && tod == 6 * Hour + 5 * 60)
            {
                string[] pool = ["scout", "letter", "lookout"]; int r = (int)(Roll("duty-pick:" + DayIndex) % 3);
                var a = SiegeDuty(pool[r], lead); var b = SiegeDuty(pool[(r + 1) % 3], lead);
                flags["choice:" + a.Id] = DayIndex; flags["choice:" + b.Id] = DayIndex;
                Note(Dist(Player, lead) <= 300_00 ? $"{lead.Name}「今日は「{a.Title}」か「{b.Title}」だ。どちらを務める。」" : $"使番が来た。{lead.Name}から、今日の役目を「{a.Title}」か「{b.Title}」から選べとのこと。");
                return;
            }
            else if (open == 0 && tod == 14 * Hour + 5 * 60 && DoneToday() is var done && done.Count < 2)
            {
                // その日まだ務めていないもの（物見・矢文・見張りの順）。
                bool scoutedYesterday = Facts.Any(f => f.Id == "castle-scout:" + (DayIndex - 1));
                var left = new[] { "scout", "letter", "lookout" }.Where(w => !done.Contains(w) && !(w == "scout" && scoutedYesterday)).ToArray(); if (left.Length == 0) left = ["lookout"];
                string what = left[Math.Abs(Roll("pm-duty:" + DayIndex)) % left.Length];
                t = SiegeDuty(what, lead);
            }
            else if (open > 0 && tod < 16 * Hour) { var p = SectorPost(open); t = Offer(lead.Id, "付城の普請", "duty", Second + 2 * Hour, Second + 12 * Hour, 0, new ProtoStep("build", $"{SectorName(open)}で付城の普請を手伝う（18時まで）", X: p.X, Y: p.Y)); }
            else if (tod == 18 * Hour + 5 * 60 && DayIndex % 2 == 0) { var p = SectorPost(3); t = Offer(lead.Id, "夜の見回り", "duty", Second + 2 * Hour, Second + 12 * Hour + 30 * 60, 0, new ProtoStep("watch", "夜（20時〜6時）、岩切城の囲みを見回る", X: p.X, Y: p.Y)); } // 行き先は城の向こう側の持ち場（城の内へは入らない）
            if (t == null) return;
            foreach (var x in Tasks.Where(x => x.Kind == "duty" && x.State == TaskState.Offered && x.Id != t.Id && flags.ContainsKey("choice:" + x.Id)).ToArray()) x.State = TaskState.Expired;
            t.Speech = DutySpeech(t.Title);
            if (t.Title == "付城の普請") flags["duty-need:" + t.Id] = Math.Clamp((18 * Hour - tod) / 60 - 60, 60, 6 * 60);
            t.State = TaskState.Open; flags["duty-day:" + t.Id] = DayIndex; // 軍令なので、申し付けられた時から役目になる（そばにいなくても）
            if (Dist(Player, lead) <= 300_00) Note($"{lead.Name}「{t.Speech}」"); else Note($"使番が来た。{lead.Name}から「{t.Title}」の申し付け。");
        }
        // 委任なら、本人は自分で役目の場所へ向かう（小荷駄の護衛は、小荷駄のそばへ）。
        if (Auto(Player) && Tasks.FirstOrDefault(t => t.Kind == "duty" && t.State == TaskState.Open) is { } go && go.Current is { } gs && (Second % 120 == 0 && !Player.Moving || gs.Do == "escort" && Second % 60 == 0 && Person(Konida).Active))
        {
            var (tx, ty) = gs.Do == "escort" ? (Person(Konida).Active ? (Person(Konida).X + 18_00, Person(Konida).Y + 12_00) : TaskTarget(go)) : TaskTarget(go);
            if (gs.Do == "build") { var c = Map.Site(FoeCastle); double dx = tx - c.X * 100L, dy = ty - c.Y * 100L, len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy)); (tx, ty) = (tx + (long)(dx / len * 60_00), ty + (long)(dy / len * 60_00)); } // 持ち場の外側60m（隊の札に重ならない）
            bool night = Second % Day >= 20 * Hour || Second % Day < 6 * Hour;
            if (gs.Do == "watch" && night && Dist(Player, castle: true) <= 800_00)
            {
                // 見回り：持ち場の外側を、20分ごとに次の持ち場へ歩く。
                if (Second % (20 * 60) == 0) { int i = (Enumerable.Range(0, Sectors).OrderBy(j => Dist(Player, SectorPost(j).X, SectorPost(j).Y)).First() + 1) % Sectors; /* 隣の持ち場へ（城の中を通らない） */ var c = Map.Site(FoeCastle); var sp = SectorPost(i); double dx = sp.X - c.X * 100L, dy = sp.Y - c.Y * 100L, len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy)); Walk(Player, ProtoMove.Nearest(Map, sp.X + (long)(dx / len * 60_00), sp.Y + (long)(dy / len * 60_00)), SectorName(i), "囲みを見回っている"); }
                else if (!Player.Moving) Player.Doing = "囲みを見回っている";
            }
            else if (Dist(Player, tx, ty) > (gs.Do == "watch" ? 500_00 : gs.Do == "escort" ? 20_00 : 60_00)) { var (wx, wy) = RingWay(tx, ty); Walk(Player, ProtoMove.Nearest(Map, wx, wy), TaskPlace(go), go.Title + "に向かっている"); }
            else Player.Doing = go.Title switch { "付城の普請" => "付城の普請を手伝っている", "夜の見回り" => night ? "囲みを見回っている" : "見回りに備えている", "小荷駄の護衛" => "小荷駄について歩いている", "城の物見" => "城の様子を探っている", "矢文" => "矢文を射込んでいる", "付城の見張り" => "付城の櫓から城を見張っている", _ => go.Title };
        }
        // 役目の進み：その場にいた時間を数える。
        foreach (var t in Tasks.Where(t => t.Kind == "duty" && t.State == TaskState.Open).ToArray())
        {
            var st = t.Current; if (st == null) continue;
            bool there = st.Do switch
            {
                "build" => Dist(Player, st.X, st.Y) <= 100_00 && Second % Day is >= 6 * Hour and < 18 * Hour,
                "watch" => Dist(Player, castle: true) <= 800_00 && (Second % Day >= 20 * Hour || Second % Day < 6 * Hour),
                "escort" => Person(Konida).Active && Dist(Player, Person(Konida)) <= 150_00,
                "envoy" => Dist(Player, GatePoint.X, GatePoint.Y) <= 80_00,
                "scout" or "letter" or "lookout" => Dist(Player, st.X, st.Y) <= 100_00 && Daytime(Second),
                _ => false,
            };
            if (there && Second % 60 == 0) flags["duty-min:" + t.Id] = flags.GetValueOrDefault("duty-min:" + t.Id) + 1;
            if (st.Do == "envoy" && there && !Flag("knocked:" + t.Id) && s.TalksUntil == -1) { Set("knocked:" + t.Id); s.TalksUntil = Second + 15 * 60; Happen("envoy-at:" + Second, $"{Player.Name}が使者として岩切城の門を叩いた。門の内から{Person(HeadOf(Rebel)).Name}の家臣が出て口上を聞き、城主に伝えると言って戻った。", Player.X, Player.Y, [Player, Person(HeadOf(Rebel))]); }
            if (st.Do == "envoy" && Flag("knocked:" + t.Id) && s.TalksUntil > 0) Player.Doing = "城門で返事を待っている";
            int mins = (int)flags.GetValueOrDefault("duty-min:" + t.Id);
            bool done = st.Do switch
            {
                "build" => Second % Day == 18 * Hour && mins >= flags.GetValueOrDefault("duty-need:" + t.Id, 6 * 60) || mins >= 30 && Enumerable.Range(0, Sectors).Any(i => s.Forts[i] && SectorPost(i) == (st.X, st.Y)), // 付城が出来上がれば、そこで終わる
                "scout" => mins >= 2 * 60,
                "letter" => mins >= 30,
                "lookout" => mins >= 90,
                "watch" => Second % Day == 6 * Hour && mins >= 4 * 60,
                "escort" => Facts.Any(f => f.Id == "convoy-in:" + DayIndex) && mins >= 20,
                "envoy" => Flag("knocked:" + t.Id) && s.TalksUntil == 0,
                _ => false,
            };
            if (st.Do == "escort" && Flag($"robbed:{Konida}:{DayIndex}")) { t.State = TaskState.Failed; AddFavor(t.Giver, -10); Note("小荷駄を守れなかった。"); continue; }
            if (!done) continue;
            if (st.Do == "lookout")
            {
                int food = CastleFoodDays, roll = (int)(Roll("lookout:" + DayIndex) % 3);
                string said = food > 14 ? "城の蓄えはまだあると言う" : food > 5 ? "城では粥が薄くなったと言う" : "城ではもう米が尽きかけていると言う";
                string seen = roll == 0 ? $"櫓から、城を抜け出そうとした城兵を見つけて捕らえた。{said}。" : roll == 1 ? "城門の内で、城兵が集まって何か話し合っているのが見えた。" : "城に目立った動きは無かった。";
                if (roll == 0) { foreach (var r in Garrison.Where(r => r.Soldiers > 1).Take(1).ToArray()) r.Soldiers--; flags["castle-scout-food"] = food > 14 ? 3 : food > 5 ? 2 : s.Store > 0 ? 1 : 0; }
                Happen("lookout:" + DayIndex, $"{Player.Name}は{SectorAt(st.X, st.Y) ?? "付城"}の櫓で見張った。{seen}", Player.X, Player.Y, [Player, SiegeLead]);
            }
            if (st.Do == "letter")
            {
                // 矢文は調略：読んだ城兵が揺らぎ、城の士気が少し落ちる。日を重ねるほど効き目は薄れる。
                int n = (int)flags.GetValueOrDefault("letter-count"); flags["letter-count"] = n + 1; int drop = Math.Max(2, 6 - n / 3);
                foreach (var r in Garrison.ToArray()) r.Morale = Math.Max(1, r.Morale - drop);
                string[] says = ["降れば命は助ける", "城主一人の謀叛に付き合うことはない", "後詰は来ぬ。沢渡は動かぬ", "降った者には米を分ける", "鷹野は降った者を咎めぬ"];
                int cm = CastleMorale, rr = Math.Abs(Roll("letter-back:" + DayIndex)) % 3;
                string back = cm >= 55 ? new[] { "城からは嘲る声が返ってきた。", "城から矢が一本返ってきた。", "塀の上の城兵は、文を踏みつけて見せた。" }[rr]
                    : cm >= 35 ? new[] { "塀の上で、文を拾う者がいた。", "文を拾った城兵が、仲間と何か囁き合っていた。", "城の内で、文を取り上げる怒鳴り声がした。" }[rr]
                    : new[] { "やがて城兵が一人、塀を越えて降ってきた。", "やがて城兵が二人、門の脇から降ってきた。", "塀の上から、白い布を振る者がいた。" }[rr];
                if (cm < 35) foreach (var r in Garrison.Where(r => r.Soldiers > 1).Take(1).ToArray()) r.Soldiers--;
                Happen("arrow-letter:" + DayIndex, $"{Player.Name}が岩切城へ矢文を射込んだ。「{says[n % says.Length]}」と説く文だ。{back}", Player.X, Player.Y, [Player, SiegeLead]);
            }
            if (st.Do == "scout")
            {
                int food = CastleFoodDays, mor = CastleMorale; string seen = food > 14 ? "炊煙は太く、蓄えはまだ多いらしい" : food > 5 ? "炊煙は細く、蓄えは減っているらしい" : s.Store > 0 ? "炊煙はほとんど上がらず、蓄えは乏しいらしい" : "城兵は痩せ、蓄えは尽きているらしい";
                int was = flags.TryGetValue("castle-scout-morale", out var w) ? (int)w : -1;
                string mood = mor >= 65 ? "塀の上に人影が並び、城兵の声は高い" : mor >= 50 ? "城兵の声はまだ高い" : mor >= 35 ? "城兵の声は低く、塀の上の人影もまばらだ" : mor >= 20 ? "塀の上の人影はまばらで、声も聞こえない" : "塀の上にはほとんど人影が無い";
                int Band(int m) => m >= 65 ? 4 : m >= 50 ? 3 : m >= 35 ? 2 : m >= 20 ? 1 : 0;
                int fb = food > 14 ? 3 : food > 5 ? 2 : s.Store > 0 ? 1 : 0, fwas = flags.TryGetValue("castle-scout-food", out var fw) ? (int)fw : -1;
                string smoke = fwas < 0 || fb == fwas ? "" : fb < fwas ? "炊煙は前より細い。" : "炊煙は前より太い。";
                string than = was < 0 ? "" : smoke + (Band(mor) < Band(was) ? "城兵は前に探った時より沈んでいる。" : Band(mor) > Band(was) ? "城兵は前に探った時より勢いづいている。" : "城兵の様子は前に探った時と変わらない。");
                if (mor < 35 && food <= 8 || mor < 20) { than += "今なら、勧告に耳を貸すかもしれない。"; if (!flags.ContainsKey("parley-hint-day")) flags["parley-hint-day"] = DayIndex; }
                Happen("castle-scout:" + DayIndex, $"{Player.Name}が岩切城を探った。{seen}。{mood}。{than}", Player.X, Player.Y, [Player, SiegeLead]);
                flags["castle-scout-food"] = food > 14 ? 3 : food > 5 ? 2 : s.Store > 0 ? 1 : 0; flags["castle-scout-morale"] = mor;
            }
            if (st.Do == "watch") Happen("watch-done:" + DayIndex, $"{Player.Name}は夜の見回りを終えた。", Player.X, Player.Y, Player);
            Set("duty-done:" + t.Id); Next(t);
        }
    }
    public bool EnvoyWon(string id) => Flag("envoy-won:" + id);
    string DutySpeech(string title) => title switch { "小荷駄の護衛" => "今日は小荷駄が来る。道には賊が出る。鷹野城まで迎えに行って、本陣まで送り届けよ。", "付城の普請" => OpenSectors > 0 ? "囲みに穴がある。付城の普請を手伝え。" : "付城の無い持ち場がある。普請を手伝え。", "城の物見" => "城に近づいて、炊煙と城兵の様子を探ってまいれ。矢の届く所までは寄るな。", "矢文" => "城兵に降るよう説く文を書かせた。城の前から射込んでまいれ。", "付城の見張り" => "付城の櫓に上がり、城の動きを見張れ。抜け出す者がいれば捕らえよ。", _ => "今夜は囲みを見回れ。城から抜ける者、打って出る者に目を光らせよ。" };
    // 攻城の役目を一つ作る（物見・矢文・付城の見張り）。
    ProtoTask SiegeDuty(string what, ProtoPerson lead)
    {
        var sp = ScoutPost; var made = MakeSiegeDuty(what, lead, sp); made.Speech = DutySpeech(made.Title); return made;
    }
    ProtoTask MakeSiegeDuty(string what, ProtoPerson lead, (long X, long Y) sp)
    {
        if (what == "scout") return Offer(lead.Id, "城の物見", "duty", Second + 2 * Hour, Second + 10 * Hour, 0, new ProtoStep("scout", "城に近づいて、城内の様子を2時間探る", X: sp.X, Y: sp.Y));
        if (what == "letter") return Offer(lead.Id, "矢文", "duty", Second + 2 * Hour, Second + 6 * Hour, 0, new ProtoStep("letter", "城の前から、降るよう説く矢文を射込む", X: sp.X, Y: sp.Y));
        int i = 1 + (int)(DayIndex % (Sectors - 1)); var p0 = SectorPost(i); var cc = Map.Site(FoeCastle); double ddx = cc.X * 100L - p0.X, ddy = cc.Y * 100L - p0.Y, dl0 = Math.Max(1, Math.Sqrt(ddx * ddx + ddy * ddy)); var p = ProtoMove.Nearest(Map, p0.X + (long)(ddx / dl0 * 28_00), p0.Y + (long)(ddy / dl0 * 28_00)); // 付城の柵の内、城寄り（櫓のそば）
        return Offer(lead.Id, "付城の見張り", "duty", Second + 2 * Hour, Second + 8 * Hour, 0, new ProtoStep("lookout", $"{SectorName(i)}の付城の櫓から、城を1時間半見張る", X: p.X, Y: p.Y));
    }
    // 今日務めた（または務めている）攻城の役目。
    HashSet<string> DoneToday() => [.. Tasks.Where(t => t.Kind == "duty" && t.State is TaskState.Done or TaskState.Open && flags.GetValueOrDefault("duty-day:" + t.Id, -1) == DayIndex).Select(t => t.Steps[0].Do)];
    // 朝の役目を選ぶ（選ばれなかった方は消える）。
    internal string PickDuty(string id, bool quiet = false)
    {
        var t = Tasks.FirstOrDefault(x => x.Id == id); if (t == null || t.State != TaskState.Offered || !flags.ContainsKey("choice:" + t.Id)) return "";
        foreach (var x in Tasks.Where(x => x.Kind == "duty" && x.State == TaskState.Offered && x.Id != id && flags.ContainsKey("choice:" + x.Id)).ToArray()) x.State = TaskState.Expired;
        t.State = TaskState.Open; flags["duty-day:" + t.Id] = DayIndex; if (!quiet) Note($"今日は「{t.Title}」を務める。");
        return $"{SiegeLead.Name}「{t.Speech}」";
    }
    public IEnumerable<ProtoTask> DutyChoices => Tasks.Where(t => t.Kind == "duty" && t.State == TaskState.Offered && flags.ContainsKey("choice:" + t.Id));
    public int DutiesDone => Tasks.Count(t => t.Kind == "duty" && t.State == TaskState.Done);
    // 果たした攻城の役目の内訳（例：「普請4・見回り6」）。
    public string DutiesText => string.Join("・", Tasks.Where(t => t.Kind == "duty" && t.State == TaskState.Done).GroupBy(t => t.Title switch { "付城の普請" => "普請", "夜の見回り" => "見回り", "小荷駄の護衛" => "護衛", "城の物見" => "物見", "勧告の使者" => "使者", "付城の見張り" => "見張り", _ => t.Title }).OrderByDescending(g => g.Count()).Select(g => g.Key + g.Count()));
    // 攻城での受け持ち（論功行賞の役目の列）：総大将は本陣、付城を築いた隊は付城、ほかは囲み。本人は果たした役目。
    public string SiegeRole(string id) => War.Siege == null ? "" : id == SiegeLead.Id ? "本陣" : War.Siege.Built.GetValueOrDefault(id) is > 0 and var n ? $"付城{n}" : id == Player.Id ? (DutiesDone > 0 ? "役目" : "") : "囲み";

    // ---- 勧告 ----
    public static readonly string[] SurrenderTerms = ["帰参", "退去", "切腹"];
    static string TermsText(string t) => t switch { "帰参" => "所領はそのままで鷹野に戻る", "退去" => "城主は城を明け渡して去る", _ => "城主の切腹で城兵を助ける" };
    public bool Talking => War.Siege is { TalksUntil: not 0 };
    int ParleyScore()
    {
        var s = War.Siege!; var k = Person(HeadOf(Rebel)); int food = CastleFoodDays;
        int hope = s.Relief switch { "出陣" or "向かう" or "着いた" or "使い" => -20, "破れた" or "来ない" => 20, _ => 0 };
        return 20 + (100 - CastleMorale) / 2 + (food <= 3 ? 30 : food <= 7 ? 10 : 0) + (100 - s.Gate) / 5 + (SiegeRatio >= 2 ? 10 : 0) + hope
            + Math.Clamp(Favor(k.Id), -20, 20) + (Flag("persuaded") ? 10 : 0) - (k.Temper == "勇猛" ? 10 : 0);
    }
    string Recommend(string terms)
    {
        var s = War.Siege; if (s == null || !SiegeCouncilOpen || !SurrenderTerms.Contains(terms)) return "";
        var lead = SiegeLead; var heir = Person(Heir);
        if (s.TalksUntil != 0) return $"{lead.Name}「使者はまだ戻らぬ。」";
        if (s.ParleyAt >= 0 && Second / Day == s.ParleyAt / Day) return $"{lead.Name}「今日はもう使者を出した。」";
        if (!CommanderListens) return $"{lead.Name}「勧告など無用。」";
        if (terms == "帰参" && heir.Temper == "勇猛" && SpiritOf("takano") >= 60) return $"{lead.Name}「謀叛人をそのまま許せと申すか。ならぬ。」";
        return SendEnvoy(terms);
    }
    // 勧告の使者を出す（攻城軍議の頼み、または総大将が自分から）。
    string SendEnvoy(string terms)
    {
        var s = War.Siege!; var lead = SiegeLead;
        s.ParleyAt = Second; s.Terms = terms; if (s.Method != SiegeMethods[1]) { s.PrevMethod = s.Method; s.MethodAt = Second; } s.Method = SiegeMethods[1]; Set("siege-decided"); flags.Remove("envoy-mine");
        foreach (var u in People.Where(InArmy)) War.Target.Remove(u.Id);
        Happen("envoy:" + Second, $"鷹野勢は岩切城へ降伏の使者を出すことにした。条件は、{TermsText(terms)}。", lead.X, lead.Y, [.. AbleArmy.Where(u => Dist(u, lead) <= 2500_00)]);
        // 使者は、兵のない本人が務める（城門まで歩く）。本人が兵を率いていれば、ほかの者が行く。
        if (Player.Soldiers == 0 && !OnDuty)
        {
            s.TalksUntil = -1; s.EnvoyBy = Second + 2 * Hour; var g = GatePoint; Set("envoy-mine"); // 本人が使者に立ったときだけ、開城が本人の手柄になる
            var t = Offer(lead.Id, "勧告の使者", "duty", Second + 30 * 60, Second + 2 * Hour, 0, new ProtoStep("envoy", "岩切城の城門へ行き、勧告を伝える", X: g.X, Y: g.Y)); t.State = TaskState.Open;
            return $"{lead.Name}「よかろう。そなたが使者に立て。城門へ行け。」";
        }
        // 本人が行けなければ、兵の少ない者が使者に立って城門まで歩く（誰が行ったか見えるように。2026-11-25 擬人格の印14）。
        var who = People.Where(u => InArmy(u) && u.Active && u.Id != lead.Id && !u.Player && !War.Routed.Contains(u.Id) && ClashOf(u.Id) == null).OrderBy(u => u.Soldiers).ThenBy(u => u.Id).FirstOrDefault();
        if (who != null)
        {
            s.TalksUntil = -1; s.EnvoyBy = Second + 2 * Hour; s.Envoy = who.Id; s.EnvoyMen = who.Soldiers; who.Soldiers = 0; War.Target.Remove(who.Id); // 兵は持ち場に置き、供回りだけで行く
            Happen("envoy-go:" + Second, $"{who.Name}が使者に立ち、岩切城の城門へ向かった。", who.X, who.Y, [.. People.Where(u => InArmy(u) && u.Active)]);
            return $"{lead.Name}「よかろう。{who.Name}を使者に立てる。」";
        }
        s.TalksUntil = Second + 30 * 60;
        return $"{lead.Name}「よかろう。使者を出す。」";
    }
    void SiegeTalks()
    {
        var s = War.Siege!; var k = Person(HeadOf(Rebel)); var lead = SiegeLead;
        // 本人が使者なのに2時間で城門へ着かなければ、別の者が行く。
        if (s.TalksUntil == -1 && s.Envoy == "" && Second >= s.EnvoyBy)
        {
            foreach (var t in Tasks.Where(t => t.Kind == "duty" && t.State == TaskState.Open && t.Current?.Do == "envoy").ToArray()) { t.State = TaskState.Failed; AddFavor(t.Giver, -10); Note($"申し付けられた「勧告の使者」に行かなかった。{lead.Name}の機嫌を損ねた。"); }
            var who = People.Where(u => InArmy(u) && u.Active && u.Id != lead.Id && !u.Player && !War.Routed.Contains(u.Id) && ClashOf(u.Id) == null).OrderBy(u => u.Soldiers).ThenBy(u => u.Id).FirstOrDefault();
            if (who == null) s.TalksUntil = Second + 30 * 60;
            else { s.EnvoyBy = Second + 2 * Hour; s.Envoy = who.Id; s.EnvoyMen = who.Soldiers; who.Soldiers = 0; War.Target.Remove(who.Id); Happen("envoy-go:" + Second, $"使者が来ないので、{who.Name}が代わりに使者に立ち、岩切城の城門へ向かった。", who.X, who.Y, [.. People.Where(u => InArmy(u) && u.Active)]); }
        }
        if (s.TalksUntil > 0 && Second >= s.TalksUntil)
        {
            s.TalksUntil = 0; int score = ParleyScore() + s.Terms switch { "帰参" => 25, "退去" => 5, _ => -20 };
            if (score >= 70 && k.Active) { if (s.Envoy != "") Set("envoy-won:" + s.Envoy); Surrender(s.Terms); return; }
            int food = CastleFoodDays;
            string said = s.Relief is "出陣" or "向かう" or "着いた" ? "後詰は来る。城は渡さぬ。" : food <= 3 ? "まだ降らぬ。だが、長くは保たぬかもしれぬな。" : score >= 55 ? "今は聞けぬ。出直してまいれ。" : "米も矢もまだある。城は渡さぬ。";
            Happen("envoy-back:" + Second, $"岩切勝元は勧告を退けた。「{said}」", lead.X, lead.Y, [.. AbleArmy.Where(u => Dist(u, lead) <= 2500_00), k, Player]);
        }
        if (s.Plot == "密書" && Second >= s.PlotAt)
        {
            var t = Person(s.PlotWith); bool yes = t.Active && t.House == Rebel && PlotWill(t) >= 90;
            s.Plot = yes ? "約束" : "断られた";
            Happen("plot-reply", yes ? $"{t.Name}から、内応を約す返書が届いた。" : $"{t.Name}からの返書は来なかった。", lead.X, lead.Y, lead, Player);
        }
    }
    // 本人でない使者が戻る：置いていった兵を返す。
    void EnvoyBack(bool force = true)
    {
        var s = War.Siege; if (s == null || s.Envoy == "") return;
        var e = Person(s.Envoy);
        if (!force && e.Active && War.Result == "" && Dist(e, SiegePost(e).X, SiegePost(e).Y) > 120_00) return; e.Soldiers += s.EnvoyMen; s.EnvoyMen = 0; s.Envoy = ""; e.Path = []; e.PathIndex = 0;
    }
    void Surrender(string terms)
    {
        EnvoyBack(force: false);
        foreach (var t in Tasks.Where(t => t.Kind == "duty" && t.State == TaskState.Open && t.Current?.Do == "envoy" && Flag("knocked:" + t.Id))) t.State = TaskState.Done; // 使者の役目は、受けた返事で果たした
        var k = Person(HeadOf(Rebel)); EndRelief(); War.Result = "開城させた"; War.Phase = "終わり"; if (Flag("envoy-mine")) Set("parley-won");
        var men = People.Where(r => r.House == Rebel && r.Active && r.Id != k.Id && r.Id != Nobunao).Select(r => r.Name).ToArray(); string rest = men.Length > 0 ? string.Join("と", men) : "";
        string text = terms switch
        {
            "帰参" => $"{k.Name}は勧告を受け、城を開いて鷹野に帰参した。" + (rest != "" ? $"{rest}も従った。" : ""),
            "退去" => $"{k.Name}は勧告を受け、城を明け渡して去った。" + (rest != "" ? $"{rest}は城に残り、鷹野に降った。" : ""),
            _ => $"{k.Name}は城兵の命と引き換えに腹を切り、岩切城は開いた。" + (rest != "" ? $"{rest}は鷹野に降った。" : ""),
        };
        Happen("castle-open", text, k.X, k.Y, [.. People.Where(u => InArmy(u) && u.Active), k]);
        foreach (var r in People.Where(r => r.House == Rebel).ToArray())
        {
            r.Path = []; r.PathIndex = 0;
            if (r.Id == k.Id && terms != "帰参") { r.Active = false; r.Doing = terms == "退去" ? "城を明け渡して去った" : "腹を切った"; continue; }
            r.House = "takano"; r.Doing = "帰参した"; if (r.Id == k.Id) r.Loyalty = Math.Max(r.Loyalty, 55);
        }
        War.Clashes.Clear(); War.Target.Clear();
    }

    // ---- 調略 ----
    ProtoPerson? PlotTarget => People.Where(r => r.House == Rebel && r.Active && r.Id != HeadOf(Rebel) && r.Id != Nobunao && r.Soldiers > 0).OrderBy(r => r.Id).FirstOrDefault();
    int PlotWill(ProtoPerson t) { var s = War.Siege!; return t.Loyalty + Math.Clamp(Favor(t.Id), -20, 20) + (100 - CastleMorale) / 2 + (CastleFoodDays <= 3 ? 15 : 0) + (100 - s.Gate) / 5 + (s.Relief is "破れた" or "来ない" ? 10 : 0); }
    public string PlotStage => War.Siege?.Plot ?? "";
    public bool RumorUsed => War.Siege is { } s && s.RumorDay == DayIndex;
    string Scheme()
    {
        var s = War.Siege; if (s == null || !SiegeCouncilOpen) return "";
        var lead = SiegeLead;
        if (s.Plot == "約束") return PlotSignal();
        if (s.Plot != "") return s.Plot == "密書" ? $"{lead.Name}「返書を待て。」" : $"{lead.Name}「その手はもう使えぬ。」";
        if (PlotTarget is not { } t) return $"{lead.Name}「誘える者がおらぬ。」";
        if (!CommanderListens) return $"{lead.Name}「小細工は要らぬ。」";
        s.Plot = "密書"; s.PlotWith = t.Id; s.PlotAt = Second + 2 * Hour; Set("siege-decided");
        Happen("plot-sent", $"鷹野勢は、城内の{t.Name}へ密書を忍ばせた。", lead.X, lead.Y, lead, Player);
        return $"{lead.Name}「よし、{t.Name}へ密書を忍ばせよ。」";
    }
    // 流言：城内に噂を流して士気を10下げる（1日に一度）。
    string Rumor()
    {
        var s = War.Siege; if (s == null || !SiegeCouncilOpen) return "";
        var lead = SiegeLead;
        if (s.RumorDay == DayIndex) return $"{lead.Name}「今日はもう流した。」";
        if (!CommanderListens) return $"{lead.Name}「小細工は要らぬ。」";
        s.RumorDay = DayIndex; Set("siege-decided");
        int times = (int)flags.GetValueOrDefault("rumor-count"); flags["rumor-count"] = times + 1; int hit = times switch { 0 => 10, 1 => 6, 2 => 3, 3 => 1, _ => 0 };
        foreach (var r in Garrison) r.Morale = Math.Max(0, r.Morale - hit);
        string word = s.Relief is "破れた" or "来ない" ? "後詰は来ぬ" : "城主が城兵を見捨てて落ちる支度をしている";
        Happen("rumor:" + DayIndex, $"鷹野勢は、岩切城の内に「{word}」との噂を流した。", lead.X, lead.Y, lead, Player);
        if (CastleMorale <= 0 && Garrison.Any()) OpenCastle("城兵の士気が折れ、岩切城は門を開いた。");
        return hit == 0 ? $"{lead.Name}「同じ噂は、もう誰も信じぬ。」" : $"{lead.Name}「よし、噂を流させよ。」";
    }
    string PlotSignal()
    {
        var s = War.Siege!; var lead = SiegeLead; var t = Person(s.PlotWith); var k = Person(HeadOf(Rebel)); s.Plot = "済";
        var c = Map.Site(FoeCastle); var near = Units.Where(u => Dist(u, castle: true) <= 1000_00).ToArray();
        if (!t.Active || t.House != Rebel || t.Soldiers <= 0 || PlotWill(t) < 80)
        { Happen("plot-none", $"合図を出したが、{t.Name}は動かなかった。", lead.X, lead.Y, lead, Player, t); return $"{lead.Name}「……動かぬか。」"; }
        int roll = Roll("plot:" + t.Id) % 100 + (Wit(t) - Wit(k)) / 2;
        if (roll >= 75 && CastleMorale < 50)
        {
            foreach (var r in People.Where(r => r.House == Rebel && r.Id != k.Id && r.Id != Nobunao).ToArray()) { r.House = "takano"; r.Doing = "内応して鷹野に付いた"; r.Path = []; r.PathIndex = 0; }
            Set("parley-won"); OpenCastle($"{t.Name}が城内で{k.Name}を捕らえ、岩切城の門を開いた。");
            return $"{lead.Name}「でかした。」";
        }
        if (roll >= 35)
        {
            t.House = "takano"; t.Doing = "内応して鷹野に付いた"; t.Path = []; t.PathIndex = 0; War.Target.Remove(t.Id); War.Clashes.RemoveAll(x => x.A == t.Id || x.B == t.Id);
            FallCastle($"{t.Name}が内応し、岩切城の門を内から開いた。");
            return $"{lead.Name}「城門が開いたぞ。」";
        }
        int cost = Math.Min(k.Soldiers, t.Soldiers / 2); k.Soldiers -= cost; t.Soldiers = 0; t.Active = false; t.Doing = "内応が露見して討たれた";
        Happen("plot-found", $"{t.Name}の内応は露見し、城内で斬り合いになった（岩切勝元の兵{cost}を失わせた）。", c.X * 100L, c.Y * 100L, near);
        return $"{lead.Name}「……露見したか。」";
    }

    // ---- 後詰（沢渡） ----
    // 使いが沢渡城に着くと、沢渡信房が決める（自動の戦争はしない：判断するのは、知らせを受けた当主）。
    // 出すなら半日で支度し、沢渡信直の隊が岩切方として城へ向かう。崩れるか城の決着で、沢渡へ引き上げる。
    void Relief()
    {
        var s = War.Siege!; var lord = Person(Nobufusa); var nao = Person(Nobunao);
        if (s.Relief == "使い" && Second >= s.ReliefAt)
        {
            bool yes = lord.Active && nao.Active && nao.Soldiers > 0 && Roll("relief") % 100 < 60;
            s.Relief = yes ? "出陣" : "来ない"; s.ReliefAt = Second + 12 * Hour;
            var sw = Map.Site("sawatari-castle");
            Happen("relief-decided", yes ? $"{lord.Name}は、岩切城へ後詰を出すと決めた。" : $"{lord.Name}は、岩切城の頼みを断った。", sw.X * 100L, sw.Y * 100L, [lord, nao, Person(HeadOf(Rebel))]);
        }
        if (s.Relief == "出陣" && Second >= s.ReliefAt)
        {
            s.Relief = "向かう"; Set("relief-house"); nao.House = Rebel; War.Routed.Remove(nao.Id); nao.Morale = 100;
            var to = ProtoMove.Nearest(Map, Map.Site(FoeCastle).X * 100L + 600_00, Map.Site(FoeCastle).Y * 100L + 300_00);
            Walk(nao, to, "岩切城", "岩切城の後詰に向かっている");
            var sw = Map.Site("sawatari-castle"); Happen("relief-out", $"{nao.Name}の隊が沢渡城を出て、岩切城へ向かった。", sw.X * 100L, sw.Y * 100L, [nao, lord]);
        }
        if (s.Relief is "向かう" or "着いた")
        {
            if (!nao.Active || nao.Soldiers == 0 || War.Routed.Contains(nao.Id))
            {
                s.Relief = "破れた"; EndRelief();
                Happen("relief-broken", $"沢渡の後詰は破れて退いた。", nao.X, nao.Y, [.. AbleArmy.Where(u => Dist(u, nao) <= 2000_00), nao, Person(HeadOf(Rebel))]);
                return;
            }
            // 着いたら、囲みのいちばん近い隊へ当たる。
            if (Dist(nao, castle: true) <= 1500_00 && !War.Target.ContainsKey(nao.Id))
            {
                var foe = AbleArmy.OrderBy(u => Dist(u, nao)).ThenBy(u => u.Id).FirstOrDefault();
                if (foe != null) { War.Target[nao.Id] = foe.Id; if (s.Relief != "着いた") { s.Relief = "着いた"; Happen("relief-arrive", $"沢渡の後詰の{nao.Name}の隊が、岩切城の囲みに攻めかかった。", nao.X, nao.Y, [.. AbleArmy.Where(u => Dist(u, nao) <= 1500_00), nao]); } }
            }
        }
    }
    // 後詰を沢渡へ戻す（城の決着・後詰が破れたとき）。
    void EndRelief()
    {
        if (!Flag("relief-house")) return; flags.Remove("relief-house");
        var nao = Person(Nobunao); nao.House = "sawatari"; War.Target.Remove(nao.Id); War.Clashes.RemoveAll(c => c.A == nao.Id || c.B == nao.Id); War.Routed.Remove(nao.Id);
        foreach (var (id, tid) in War.Target.ToArray()) if (tid == nao.Id) War.Target.Remove(id);
        if (nao.Active) { var sw = Map.Site("sawatari-castle"); Walk(nao, Spot(sw, 2), sw.Id, "沢渡城へ引き上げている"); }
    }
    public string ReliefState => War.Siege?.Relief ?? "";
    // 画面の色：後詰の沢渡信直は、岩切の側で戦っていても沢渡の色で描く。
    public string ShownHouse(ProtoPerson p) => p.Id == Nobunao && Flag("relief-house") ? "sawatari" : p.House;
    // 画面用：本人が知っている後詰の様子（""・沢渡勢・退けた）。
    public string KnownRelief => Knows(Player, "relief-broken") ? "退けた" : Knows(Player, "relief-arrive") ? "沢渡勢" : "";

    void OpenCastle(string text)
    {
        var k = Person(HeadOf(Rebel)); EndRelief(); War.Result = "開城させた"; War.Phase = "終わり";
        var gave = People.Where(r => r.House == Rebel && r.Active && r.Id != Nobunao).Select(r => r.Name).ToArray();
        Happen("castle-open", text + (gave.Length > 0 ? $"{string.Join("と", gave)}は城を明け渡して降った。" : ""), k.X, k.Y, [.. People.Where(u => InArmy(u) && u.Active && Dist(u, castle: true) <= 1000_00), k]);
        if (Dist(Player, castle: true) > 1000_00) flags["news-open"] = Second + Dist(Player, castle: true) / RunnerSpeed;
        foreach (var r in People.Where(r => r.House == Rebel)) { r.Active = false; r.Path = []; r.PathIndex = 0; r.Doing = "降った"; }
        War.Clashes.Clear(); War.Target.Clear();
    }
}
