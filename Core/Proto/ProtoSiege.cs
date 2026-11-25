namespace HakutaiKagami.Core.Proto;

// 試作版の攻城（2026-11-20〜21 ユーザーの整理）。
// ・始まり：岩切の隊が一つでも岩切城へ逃げ込んだら（崩れて・退き貝で）、その時から攻城（鷹野の大目標が「敵を崩す」で、戦える隊が残っているとき）。
// ・城は一つの部隊：兵力（城内の隊の兵）・士気・兵糧・耐久を持つ。城内に入った隊は城の中身になり、個別には戦わず、狙われない。崩れていた隊も城の兵に戻る。
//   城の守りは城内でいちばん高い統率、打ち返しはいちばん高い武勇で決まる。
//   部隊と違う決まり：①動かない。②耐久が残っている間は城の利（城が受ける打撃は5割5分。城門の前で戦えるのは一部なので与える打撃は6割5分）。
//   ③士気が切れても敗走しない（城の内では士気の下がりが半分）。
// ・城の負け：耐久（100）が0で落城／城の兵糧（3日）が0で開城／城の士気が20を切ると開城／城の兵が尽きれば落城。
//   城に層は無い（2026-11-21 ユーザー「層の話はなかったことに」）。城の見た目は、もとの模型を4倍にしたもの（城の内は真ん中から190m）。
// ・包囲：攻め手が城のそば（900m）にいる間は包囲。兵の数は問わない。包囲されている間、城は兵糧を補給できず（毎朝6時に1日ぶん減る）、
//   城の士気が6時間ごとに下がる。攻め手がみな離れれば包囲は解ける。
// ・攻め手の兵糧は本陣に置く（3日。Core/Proto/ProtoCamp.cs）。本陣を取られると失う。尽きると6時間ごとに全軍士気が下がり、隊もじわじわ痩せる。
// ・攻城の軍議：本陣（300m）にいれば、いつでも開ける。続ける行動は 包囲 と 力攻め（総大将が聞き入れるかは心証しだい）。
//   本人が言わなければ、陣を敷いて半刻で総大将が決める（勇猛な総大将は力攻め）。軍議の中で、調略と勧告も打てる（総大将が認めたとき）。
// ・力攻め：城門（城の南）に寄せて城と戦う（城門から110m以内）。与えた打撃は城内の隊に兵の数で割り振られ、城は城内の兵の合計で打ち返す。
//   耐久は攻め手の兵の数と武勇で削れる。城が立っている間、城の内へは入れない。城から打って出た隊（朝駆け）だけが、野の隊として個別に戦う。
// ・朝駆け：城方は毎朝5時、囲みのいちばん小さい隊へ打って出る（慎重な城主は兵糧が残り1日になるまで出ない）。見張りが気づかなければ奇襲。
// ・勧告：軍議で条件を選んで、降伏の使者を出す（1日に一度。総大将が認めたとき）。出すと包囲になり、返事が戻るまで（半刻）攻めない・城方も打って出ない。
//   条件は 帰参（所領はそのままで鷹野に戻る）・退去（城主は城を明け渡して去る）・切腹（城主の切腹で城兵を助ける）。
//   受けるかは城主が決める：城の全軍士気・兵糧（外からは見えない）・削れた耐久・勝元の心証に、条件の重さを足し引きする。
//   勇猛な総大将は、押しているうち（全軍士気60以上・兵糧2日以上）は「帰参」を認めない。
// ・調略：軍議で、城内の武将（城主でない者）へ密書を忍ばせる（総大将が認めたとき）。2時間で返事。内応を約すかは、その武将が決める
//   （鷹野への忠誠・本人への心証・城の全軍士気・兵糧・削れた耐久）。約束があれば、軍議で「内応の合図」を出せる。
//   合図のとき、動くかどうかをもう一度その武将が決める。動けば、城主を生け捕って開城／内から城門を開けて落城／露見して城内で斬り合い、のどれか。
// ・終わり：落城（鎮めた）・開城（開城させた）・鷹野が退く（退いた）。落城・開城のときは岩切城へ入って論功行賞（Core/Proto/ProtoHome.cs）。
public sealed class ProtoSiege
{
    public long Since { get; set; }
    public long CampAt { get; set; }      // 城の前に陣を敷いた時刻（0＝まだ）
    public string Method { get; set; } = "包囲"; // 力攻め・包囲（基本は包囲から）
    public string PrevMethod { get; set; } = "包囲"; // 変える前の攻め方（使番が着くまでは、こちらで動く）
    public long MethodAt { get; set; }
    public bool Ringed { get; set; }      // 包囲されている（攻め手が城のそばにいる）
    public int Gate { get; set; } = 100; // 城の耐久
    public double GateHits { get; set; }
    public int Food { get; set; } = 3;    // 城の兵糧（日）
    public int Supply { get; set; } = 3;  // 攻め手の兵糧（日）
    public int Days { get; set; }
    public long SallyUntil { get; set; }
    public long ParleyAt { get; set; } = -1; // 最後に勧告の使者を出した時刻
    public string Terms { get; set; } = "";  // 勧告の条件（交渉中）
    public long TalksUntil { get; set; }     // 使者の返事が戻る時刻（0＝交渉していない）
    public string Plot { get; set; } = "";   // 調略：""・密書・約束・断られた・済
    public string PlotWith { get; set; } = "";
    public long PlotAt { get; set; }
}

public sealed partial class ProtoWorld
{
    public static readonly string[] SiegeMethods = ["力攻め", "包囲"];
    const string FoeCastle = "iwakiri-castle";
    bool InFoeCastle(ProtoPerson u) => Dist(u, castle: true) <= 180_00; // 城の内（城の真ん中から180m。いちばん外の層は150m）
    // 層の名前（外から。三層なら 三の丸・二の丸・本丸）と、層の外周までの遠さ（cm）。
    // 城の内：城の真ん中からの遠さ（cm）。もとの模型を4倍にした大きさに合わせる。
    public const long CastleRadius = 190_00;
    // 城門：城の南の辺（模型の門の側）。城の兵は、その内に並ぶ。
    public (long X, long Y) GatePoint { get { var c = Map.Site(FoeCastle); return (c.X * 100L, c.Y * 100L + CastleRadius - 40_00); } }
    // 城の兵を今の層の城門に並べる（層を失ったとき・攻城の始まり）。
    void PlaceGarrison(bool atOnce)
    {
        var s = War.Siege!; var g = GatePoint; var c = Map.Site(FoeCastle); double dx = g.X - c.X * 100L, dy = g.Y - c.Y * 100L, len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        int i = 0;
        foreach (var r in People.Where(r => r.House == Rebel && r.Active).OrderByDescending(r => r.Id == HeadOf(Rebel)).ThenBy(r => r.Id))
        {
            int side = i == 0 ? 0 : (i % 2 == 1 ? 1 : -1) * ((i + 1) / 2); i++;
            long x = g.X + (long)(-dy / len * side * 45_00), y = g.Y + (long)(dx / len * side * 45_00);
            War.Post[r.Id] = [x, y];
            if (atOnce && r.Soldiers > 0 && !Withdrawing(r) && InFoeCastle(r) && !War.Routed.Contains(r.Id)) { r.X = x; r.Y = y; r.Path = []; r.PathIndex = 0; r.Doing = "城を守っている"; }
        }
    }
    // 城が立っている（謀叛のあと、落ちるまで）。
    bool CastleStands => Flag("rebellion") && War.Result == "" && War.Phase is "行軍" or "合戦";
    bool InWalls(ProtoPerson u) => Dist(u, castle: true) <= CastleRadius + 10_00; // 城の内
    // 城内の隊：城という一つの部隊の中身。個別には戦わず、狙われない。狙いを持って打って出る隊は別。
    // 攻城の前は、城へ逃げ込んだ隊（崩れた・退き貝で退いた）と、まだ城を出ていない隊だけ。城のそばに陣を張って戦っている隊は、野の隊のまま。
    public bool Sheltered(ProtoPerson u) => u.House == Rebel && u.Active && CastleStands && InWalls(u) && !War.Target.ContainsKey(u.Id)
        && (War.Siege != null || War.Routed.Contains(u.Id) || Withdrawing(u) || !Flag("rebels-out"));
    public IEnumerable<ProtoPerson> Garrison => People.Where(r => r.Soldiers > 0 && Sheltered(r));
    // 攻め手が寄せる所：今の層の城門の外60m。隊ごとに横へ50mずつずらす。城と戦えるのは城門から110m以内。
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
    // 城との打ち合い（Fight から呼ぶ）：今の層の城門に寄せた攻め手と、城（城内の兵の合計）。
    void CastleFight(ProtoPerson[] all, Dictionary<string, double> hits, Dictionary<(string, string), double> dealt)
    {
        var s = War.Siege; if (s == null || War.Result != "" || Talking) return;
        var g = Garrison.ToArray(); int men = g.Sum(u => u.Soldiers); if (men == 0) return;
        var gate = GatePoint;
        var att = all.Where(a => InArmy(a) && a.Soldiers > 0 && !War.Routed.Contains(a.Id) && !Retreating(a) && Dist(a, gate.X, gate.Y) <= AssaultReach).ToArray(); if (att.Length == 0) return;
        var lord = g.OrderByDescending(u => u.Id == HeadOf(Rebel)).ThenBy(u => u.Id).First(); double into = 0; int morale = CastleMorale;
        // 城内の武将の能力が、城の能力になる：守りは城内でいちばん高い統率、打ち返しはいちばん高い武勇。
        int castleCmd = g.Max(Cmd), castleVal = g.Max(Val);
        foreach (var a in att)
        {
            Set("fought:" + a.Id);
            double atk = a.Soldiers * 0.006 * (Val(a) / 50.0) * TroopAttack(a) * (a.Morale / 100.0 + 0.5) * (SpiritOf("takano") / 200.0 + 0.5) * 0.55 / (castleCmd / 50.0);
            if (Confused(a.Id)) atk *= 0.5; if (Buffed("spear", a.Id)) atk *= Apt(a) ? 2.0 : 1.8;
            into += atk; s.GateHits += a.Soldiers * 0.02 * (Val(a) / 50.0);
            dealt[(a.Id, lord.Id)] = dealt.GetValueOrDefault((a.Id, lord.Id)) + atk;
            a.Doing = "岩切城の兵と戦っている"; War.Gauge[a.Id] = Math.Min(100, GaugeOf(a.Id) + (a.Player ? 12 : 6));
        }
        foreach (var u in g) { hits[u.Id] = hits.GetValueOrDefault(u.Id) + into * u.Soldiers / men; Set("fought:" + u.Id); }
        double back = men * 0.006 * (castleVal / 50.0) * (morale / 100.0 + 0.5) * (SpiritOf(Rebel) / 200.0 + 0.5) * 0.65; int total = att.Sum(a => a.Soldiers); // 城門の前に出て戦えるのは城の兵の一部
        foreach (var a in att)
        {
            double h = back * a.Soldiers / total / (Cmd(a) / 50.0); if (Buffed("guard", a.Id)) h *= 0.5; if (Confused(a.Id)) h *= 1.5;
            hits[a.Id] = hits.GetValueOrDefault(a.Id) + h; dealt[(lord.Id, a.Id)] = dealt.GetValueOrDefault((lord.Id, a.Id)) + h;
        }
    }
    // 包囲：攻め手が城のそば（900m）にいる間。兵の数は問わない。
    bool Besieging => AbleArmy.Any(u => Dist(u, castle: true) <= 900_00);
    IEnumerable<ProtoPerson> AbleArmy => People.Where(u => InArmy(u) && u.Active && u.Soldiers > 0 && !War.Routed.Contains(u.Id));
    ProtoPerson SiegeLead => AbleArmy.OrderByDescending(u => u.Id == Heir).ThenByDescending(u => u.Id == Karou).ThenBy(u => u.Id).FirstOrDefault() ?? Person(Heir);
    (long X, long Y) SiegeCamp => RoadPoint(550_00);

    // 岩切勢が城へ退ききった：攻城を始める。
    bool CanBesiege => War.Siege == null && GoalOf("takano") == Goals[0] && AbleArmy.Any() && Units.Any(u => u.House == Rebel && StillFights(u)); // 崩れて城へ逃げ込んだ隊も、城で立て直せるなら籠もる
    void StartSiege()
    {
        var castle = Map.Site(FoeCastle); var k = Person(HeadOf(Rebel));
        War.Siege = new ProtoSiege { Since = Second, Supply = War.Stock.GetValueOrDefault("takano", 3) }; // 攻め手の兵糧は本陣のもの
        War.RetreatBy.Remove(Rebel); War.RetreatAt.Remove(Rebel); War.RetreatRules[Rebel] = []; War.Goals[Rebel] = Goals[1];
        foreach (var r in People.Where(r => r.House == Rebel))
        {
            foreach (var f in new[] { "retreat-heard:", "withdrawing:", "retreat-at:", "stays:" }) flags.Remove(f + r.Id);
            War.Target.Remove(r.Id);
        }
        PlaceGarrison(false);
        foreach (var u in People.Where(InArmy)) War.Target.Remove(u.Id);
        // 攻め手の撤退条件に「兵糧が尽きて士気が保てない」が加わる（総崩れになる前に囲みを解く）。
        if (War.RetreatRules.TryGetValue("takano", out var rules) && !rules.Contains("supply")) rules.Add("supply");
        // 城に入った兵は気を取り直す。
        AddSpirit(Rebel, 15); foreach (var r in People.Where(r => r.House == Rebel && r.Active && r.Soldiers > 0 && InWalls(r))) r.Morale = Math.Max(r.Morale, 60);
        Happen("siege", "岩切勢は岩切城に籠もった。", k.X, k.Y, [.. AbleArmy, k]);
    }

    public int GateOf => War.Siege?.Gate ?? 0;
    public string SiegeProposal
    {
        get
        {
            var heir = Person(Heir); int mine = AbleArmy.Sum(u => u.Soldiers), theirs = Units.Where(u => u.House == Rebel).Sum(u => u.Soldiers);
            return heir.Temper == "勇猛" ? SiegeMethods[0] : heir.Temper == "慎重" ? SiegeMethods[1] : mine >= theirs * 2 ? SiegeMethods[0] : SiegeMethods[1];
        }
    }
    static string MethodText(string m) => m == SiegeMethods[0] ? "力攻めで落とす" : "囲んで兵糧を断つ";
    public string SiegeSpeech => $"{SiegeLead.Name}「岩切城は、{MethodText(Flag("siege-decided") ? War.Siege!.Method : SiegeProposal)}。」";
    // 画面用：城の内で守っている隊か。本人の囲みの陣（城を押したときの行き先）。
    public bool Garrisoned(ProtoPerson u) => Sheltered(u);
    // 画面用：城が攻め手と打ち合っているか。
    public bool CastleFighting => War.Siege != null && War.Clashes.Any(c => Sheltered(Person(c.A)) || Sheltered(Person(c.B)));
    public (long X, long Y)? PlayerSiegePost => War.Siege != null && War.Result == "" && InArmy(Player) ? SiegePost(Player) : null;
    // 攻城の軍議：本陣（300m）で、いつでも開ける（本陣を取られたあとは、総大将のそば）。
    public bool SiegeCouncilOpen => War.Siege != null && War.Result == "" && InArmy(Player)
        && (answering || (CampAt("takano") is { } hq && !CampLost("takano") ? Dist(Player, hq.X, hq.Y) <= 300_00 || CampMoveTarget is { } mv && Dist(Player, mv.X, mv.Y) <= 300_00 : Dist(Player, SiegeLead) <= 300_00));
    string DoSiege(string id) => id == "調略" ? Scheme() : id.StartsWith("勧告:") ? Recommend(id[3..]) : DecideSiege(id);
    string DecideSiege(string? want)
    {
        var s = War.Siege; if (s == null || War.Result != "") return "";
        if (want != null && !SiegeCouncilOpen) return "";
        if (want == SiegeMethods[0] && s.TalksUntil > 0) return $"{SiegeLead.Name}「使者が戻るまでは攻めぬ。」";
        string prop = Flag("siege-decided") ? s.Method : SiegeProposal; bool present = want != null && SiegeMethods.Contains(want), refused = present && want != prop && !CommanderListens;
        string next = present && !refused ? want! : prop; var lead = SiegeLead; Set("siege-decided");
        if (next == s.Method && !present) return "";
        if (next != s.Method) { s.PrevMethod = s.Method; s.MethodAt = Second; }
        s.Method = next;
        foreach (var u in People.Where(InArmy)) War.Target.Remove(u.Id);
        Happen("siege-method:" + Second, $"軍議で決まった：岩切城は、{MethodText(s.Method)}。", lead.X, lead.Y, [.. AbleArmy.Where(u => Dist(u, lead) <= 2500_00)]);
        return !present ? "" : refused ? $"{lead.Name}「いや、{MethodText(prop)}。」" : want == prop ? $"{lead.Name}「うむ。」" : $"{lead.Name}「よかろう。{MethodText(s.Method)}。」";
    }

    // 囲むときの陣：道の側（総大将）と、城をはさんで左右。城から450m。
    (long X, long Y) SiegePost(ProtoPerson u)
    {
        var castle = Map.Site(FoeCastle); long cx = castle.X * 100L, cy = castle.Y * 100L; var camp = SiegeCamp;
        var order = People.Where(InArmy).OrderByDescending(x => x.Id == Heir).ThenBy(x => x.Id).Select(x => x.Id).ToList(); int i = order.IndexOf(u.Id);
        if (i <= 0) return camp;
        double a = Math.Atan2(camp.Y - cy, camp.X - cx) + (i % 2 == 1 ? 1 : -1) * Math.PI * 0.6 * ((i + 1) / 2);
        return ProtoMove.Nearest(Map, cx + (long)(Math.Cos(a) * 550_00), cy + (long)(Math.Sin(a) * 550_00));
    }

    // 毎分（合戦の間、攻城になっていれば）：陣を敷く・攻め方ごとの動き・兵糧・朝駆け・開城。
    void SiegeMoves(ProtoPerson[] takano, ProtoPerson[] rebels)
    {
        var s = War.Siege!; var castle = Map.Site(FoeCastle); long cx = castle.X * 100L, cy = castle.Y * 100L;
        UpdatePlan();
        // 朝駆けから引き上げた隊は、城へ入ったら元に戻る。
        foreach (var r in rebels.Where(r => Withdrawing(r) && InFoeCastle(r))) { flags.Remove("withdrawing:" + r.Id); r.Path = []; r.PathIndex = 0; r.Doing = "城を守っている"; }
        // 城へ逃げ込んだ隊は、城の兵に戻る（崩れていても、城という部隊の中身になる）。
        foreach (var r in People.Where(r => r.House == Rebel && r.Active && r.Soldiers > 0 && InWalls(r) && War.Routed.Contains(r.Id)).ToArray())
        { War.Routed.Remove(r.Id); War.AtCamp.Remove(r.Id); r.Morale = Math.Max(r.Morale, 40); r.Path = []; r.PathIndex = 0; r.Doing = "城を守っている"; }
        var auto = takano.Where(u => Auto(u) && !War.Routed.Contains(u.Id)).ToArray();
        // 城内の隊を狙っていた者は、狙いを外す（城の中の隊は個別には狙えない）。
        foreach (var (id, tid) in War.Target.ToArray()) if (Person(id).House != Rebel && Sheltered(Person(tid))) War.Target.Remove(id);
        {
            // 陣を敷いたら知らせる。半刻たっても本人が何も言わなければ、総大将が攻め方を決める。
            var camp = SiegeCamp; var lead = SiegeLead;
            if (s.CampAt == 0 && Dist(lead, camp.X, camp.Y) <= 150_00) { s.CampAt = Second; Happen("siege-camp", $"{lead.Name}勢は岩切城の前に陣を敷いた。", lead.X, lead.Y, [.. AbleArmy.Where(u => Dist(u, lead) <= 2500_00)]); }
            if (s.CampAt > 0 && Second >= s.CampAt + 30 * 60 && !Flag("siege-decided") && s.TalksUntil == 0) DecideSiege(null);
            // 城の兵は、今の層の城門に並ぶ（朝駆けに出ている間は別）。
            foreach (var r in rebels.Where(r => !Withdrawing(r) && !War.Target.ContainsKey(r.Id) && !r.Moving && ClashOf(r.Id) == null && War.Post.TryGetValue(r.Id, out var at) && Dist(r, at[0], at[1]) > 30_00)) MarchTo(r, PostOf(r.Id));
            // 包囲の完成：囲みの中の兵が足りているか。
            bool ring = Besieging;
            if (ring != s.Ringed) { s.Ringed = ring; Happen($"ring:{ring}:{Second}", ring ? "鷹野勢は岩切城を囲んだ。" : "岩切城の囲みが解けた。", lead.X, lead.Y, [.. People.Where(u => InArmy(u) && u.Active)]); }
        }
        // 攻め方は使番が運ぶ：着いた隊から新しい攻め方で動く。
        string MethodFor(ProtoPerson u) => Second >= s.MethodAt + (RunnerAt(u) - Second) ? s.Method : s.PrevMethod;
        var storm = auto.Where(u => MethodFor(u) == SiegeMethods[0]).ToArray(); var hold = auto.Except(storm).ToArray();
        {
            foreach (var u in storm.Where(u => !War.Target.ContainsKey(u.Id)))
            {
                // 城の外にいる敵（打って出た隊・まだ入っていない隊）が見えていれば、そちらに当たる。いなければ城門へ寄せる。
                var outside = rebels.Where(r => !Sheltered(r) && !Withdrawing(r) && (InSight(u, r) || ClashOf(r.Id) != null)).OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault();
                if (outside != null) { War.Target[u.Id] = outside.Id; continue; }
                var at = AssaultPoint(u);
                if (!u.Moving && Dist(u, at.X, at.Y) > 40_00) Walk(u, at, "城門", "岩切城の城門へ寄せている");
            }
        }
        {
            foreach (var u in hold)
            {
                // 城の中の敵は追わない。城から出てきた敵（見えている、または味方が戦っている）には当たる。
                if (War.Target.TryGetValue(u.Id, out var tid) && ClashOf(u.Id) == null && Dist(Person(tid), cx, cy) <= 250_00) War.Target.Remove(u.Id);
                if (War.Target.ContainsKey(u.Id)) continue;
                var outside = rebels.Where(r => !Sheltered(r) && !Withdrawing(r) && Dist(r, cx, cy) > 250_00 && (InSight(u, r) || ClashOf(r.Id) != null)).OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault();
                if (outside != null) { War.Target[u.Id] = outside.Id; continue; }
                var post = SiegePost(u);
                if (!u.Moving && ClashOf(u.Id) == null && Dist(u, post.X, post.Y) > 100_00) Walk(u, post, "囲みの陣", "岩切城を囲んでいる");
            }
        }
        SiegeTalks();
        if (War.Result != "") return;
        SiegeDays(takano, rebels);
    }

    void SiegeDays(ProtoPerson[] takano, ProtoPerson[] rebels)
    {
        var s = War.Siege!; var k = Person(HeadOf(Rebel)); var lead = SiegeLead; long ofDay = Second % Day;
        // 朝6時：兵糧が1日ぶん減る。長陣で攻め手の全軍士気も下がる。
        if (ofDay == 6 * Hour && Second > s.Since + Hour)
        {
            s.Days++; if (s.Ringed) s.Food = Math.Max(0, s.Food - 1); else s.Food = Math.Min(3, s.Food + 1); // 囲まれている間は補給できない。囲みが解ければ運び込める
            s.Supply = Math.Max(0, s.Supply - 1); AddSpirit("takano", -5);
            Happen("siege-day:" + s.Days, $"岩切城を攻めて{s.Days}日。{(s.Supply > 0 ? $"兵糧はあと{s.Supply}日ぶん" : "兵糧が尽きた")}。", lead.X, lead.Y, [.. AbleArmy]);
        }
        // 尽きた側は、6時間ごとに全軍士気が下がる。城は、兵糧が尽きて士気が落ちきると開く。
        if (Second % (6 * Hour) == 0 && Second > s.Since + Hour)
        {
            if (s.Supply <= 0) AddSpirit("takano", -15);
            // 囲まれている間、城の士気はだんだん下がる。
            if (s.Ringed) { AddSpirit(Rebel, -2); foreach (var r in Garrison) r.Morale = Math.Max(0, r.Morale - 3); if (CastleMorale < 20 && Garrison.Any()) { OpenCastle("城兵の士気が尽き、岩切城は門を開いた。"); return; } }
            if (s.Food <= 0) { OpenCastle("兵糧が尽き、岩切城は門を開いた。"); return; } // 城の兵糧が尽きたら、城の負け
        }
        // 朝5時：城方の朝駆け。囲みのいちばん小さい隊へ。
        var able = rebels.Where(r => !Withdrawing(r) && r.Soldiers >= 10).ToArray();
        if (ofDay == 5 * Hour && Second > s.Since + Hour && s.SallyUntil == 0 && s.TalksUntil == 0 && able.Length > 0 && (s.Food <= 1 || SpiritOf(Rebel) >= 50 && k.Temper != "慎重")) // 慎重な城主は、兵糧が尽きるまで出ない
        {
            var victim = takano.Where(u => !War.Routed.Contains(u.Id) && Dist(u, castle: true) <= 900_00).OrderBy(u => u.Soldiers).ThenBy(u => u.Id).FirstOrDefault();
            if (victim != null)
            {
                s.SallyUntil = Second + 40 * 60; bool noticed = Roll("sally:" + s.Days) % 100 < ScoutChance(victim);
                foreach (var r in able) { War.Target[r.Id] = victim.Id; if (noticed) War.AmbushUntil.Remove(r.Id); else War.AmbushUntil[r.Id] = Second + 15 * 60; }
                Happen("sally:" + s.Days, "岩切勢が城から朝駆けに出た。", k.X, k.Y, [.. able]);
                if (noticed) Happen("sally-seen:" + s.Days, $"{victim.Name}の見張りが、城から出る岩切勢に気づいた。", victim.X, victim.Y, victim);
            }
        }
        // 朝駆けは、40分たつか、どれかの隊が崩れたら城へ引き上げる。
        if (s.SallyUntil > 0 && (Second >= s.SallyUntil || People.Any(r => r.House == Rebel && War.Routed.Contains(r.Id) && Facts.Any(f => f.Id.StartsWith("rout:" + r.Id + ":") && f.At >= s.SallyUntil - 40 * 60))))
        {
            s.SallyUntil = 0; var castle = Map.Site(FoeCastle);
            foreach (var r in rebels.Where(r => !InFoeCastle(r))) { War.Target.Remove(r.Id); Set("withdrawing:" + r.Id); Walk(r, Spot(castle, 3), castle.Id, castle.Name + "へ引き上げている"); }
        }
    }
    long Dist(ProtoPerson u, bool castle) { var c = Map.Site(FoeCastle); return Dist(u, c.X * 100L, c.Y * 100L); }

    // 城内の隊は崩れない・不意を突かれない・騎突を受けない。
    bool BehindGate(ProtoPerson u) => Sheltered(u);
    public int CastleMorale { get { var g = Garrison.Where(u => !War.Routed.Contains(u.Id)).ToArray(); int men = g.Sum(u => u.Soldiers); return men == 0 ? 0 : g.Sum(u => u.Morale * u.Soldiers) / men; } }
    // 城の士気が切れたら（20を切ったら）、敗走の代わりに門を開く（Fight から呼ぶ）。
    void GarrisonMorale(ProtoPerson[] all)
    {
        if (War.Siege == null || War.Result != "") return;
        // 城の士気＝城の内の隊の士気を、兵の数で重みを付けてならしたもの。個々の隊は20より下がらない（崩れない）。
        var inside = all.Where(u => BehindGate(u) && u.Soldiers > 0 && !War.Routed.Contains(u.Id)).ToArray(); if (inside.Length == 0) return;
        foreach (var u in inside) u.Morale = Math.Max(u.Morale, 10);
        if (CastleMorale < 20) LoseLayer(true);
    }
    // 城が落ちる。byMorale：士気が切れて開城。そうでなければ、耐久が尽きて落城。
    void LoseLayer(bool byMorale)
    {
        var c = Map.Site(FoeCastle);
        if (byMorale) { OpenCastle("城兵の士気が尽き、岩切城は門を開いた。"); return; }
        var k = Person(HeadOf(Rebel));
        Happen("gate-broken", "岩切城の守りが破られた。", c.X * 100L, c.Y * 100L, [.. Units.Where(u => Dist(u, castle: true) <= 1000_00)]);
        War.Result = "鎮めた"; War.Phase = "終わり";
        Happen("rebellion-crushed", "岩切城は落ち、岩切勝元の謀叛は鎮まった。勝元と井口は捕らえられた。", k.X, k.Y, [.. People.Where(u => InArmy(u) && u.Active), k]);
        foreach (var r in People.Where(r => r.House == Rebel)) { r.Active = false; r.Path = []; r.PathIndex = 0; r.Doing = "捕らえられた"; }
        War.Clashes.Clear(); War.Target.Clear();
    }
    void WearGate()
    {
        var s = War.Siege; if (s == null || s.Gate <= 0 || s.GateHits < 1) return;
        int d = (int)s.GateHits; s.GateHits -= d; s.Gate = Math.Max(0, s.Gate - d);
        if (s.Gate > 0) return;
        LoseLayer(false); // 耐久が尽きた
    }

    // ---- 勧告 ----
    public static readonly string[] SurrenderTerms = ["帰参", "退去", "切腹"];
    static string TermsText(string t) => t switch { "帰参" => "所領はそのままで鷹野に戻る", "退去" => "城主は城を明け渡して去る", _ => "城主の切腹で城兵を助ける" };
    public bool Talking => War.Siege is { TalksUntil: > 0 };
    int ParleyScore()
    {
        var s = War.Siege!; var k = Person(HeadOf(Rebel));
        return 30 + (100 - Math.Min(100, SpiritOf(Rebel))) / 2 + (s.Food <= 1 ? 30 : s.Food <= 2 ? 10 : 0) + (100 - s.Gate) / 5
            + Math.Clamp(Favor(k.Id), -20, 20) + (Flag("persuaded") ? 10 : 0) - (k.Temper == "勇猛" ? 10 : 0);
    }
    string Recommend(string terms)
    {
        var s = War.Siege; if (s == null || !SiegeCouncilOpen || !SurrenderTerms.Contains(terms)) return "";
        var lead = SiegeLead; var heir = Person(Heir);
        if (s.TalksUntil > 0) return $"{lead.Name}「使者はまだ戻らぬ。」";
        if (s.ParleyAt >= 0 && Second / Day == s.ParleyAt / Day) return $"{lead.Name}「今日はもう使者を出した。」";
        if (!CommanderListens) return $"{lead.Name}「勧告など無用。」";
        if (terms == "帰参" && heir.Temper == "勇猛" && SpiritOf("takano") >= 60 && s.Supply >= 2) return $"{lead.Name}「謀叛人をそのまま許せと申すか。ならぬ。」";
        s.ParleyAt = Second; s.Terms = terms; s.TalksUntil = Second + 30 * 60; if (s.Method != SiegeMethods[1]) { s.PrevMethod = s.Method; s.MethodAt = Second; } s.Method = SiegeMethods[1]; Set("siege-decided"); Set("envoy-mine");
        foreach (var u in People.Where(InArmy)) War.Target.Remove(u.Id);
        Happen("envoy:" + Second, $"鷹野勢は岩切城へ降伏の使者を出した（{TermsText(terms)}）。", lead.X, lead.Y, [.. AbleArmy.Where(u => Dist(u, lead) <= 2500_00), Person(HeadOf(Rebel))]);
        return $"{lead.Name}「よかろう。使者を出す。」";
    }
    // 毎分：使者の返事と、密書の返事。
    void SiegeTalks()
    {
        var s = War.Siege!; var k = Person(HeadOf(Rebel)); var lead = SiegeLead;
        if (s.TalksUntil > 0 && Second >= s.TalksUntil)
        {
            s.TalksUntil = 0; int score = ParleyScore() + s.Terms switch { "帰参" => 25, "退去" => 5, _ => -20 };
            if (score >= 70 && k.Active) { Surrender(s.Terms); return; }
            // 断り方で、城の内の様子がうかがえる。
            string said = s.Food <= 1 ? "まだ降らぬ。だが、長くは保たぬかもしれぬな。" : score >= 55 ? "今は聞けぬ。出直してまいれ。" : "米も矢もまだある。城は渡さぬ。";
            Happen("envoy-back:" + Second, $"岩切勝元は勧告を退けた。「{said}」", lead.X, lead.Y, [.. AbleArmy.Where(u => Dist(u, lead) <= 2500_00), k]);
        }
        if (s.Plot == "密書" && Second >= s.PlotAt)
        {
            var t = Person(s.PlotWith); bool yes = t.Active && t.House == Rebel && PlotWill(t) >= 90;
            s.Plot = yes ? "約束" : "断られた";
            Happen("plot-reply", yes ? $"{t.Name}から、内応を約す返書が届いた。" : $"{t.Name}からの返書は来なかった。", lead.X, lead.Y, lead, Player);
        }
    }
    // 勧告を受けて城を開く。条件ごとに、城主と城内の者の行く末が変わる。
    void Surrender(string terms)
    {
        var k = Person(HeadOf(Rebel)); War.Result = "開城させた"; War.Phase = "終わり"; if (Flag("envoy-mine")) Set("parley-won");
        string text = terms switch
        {
            "帰参" => $"{k.Name}は勧告を受け、城を開いて鷹野に帰参した。",
            "退去" => $"{k.Name}は勧告を受け、城を明け渡して去った。",
            _ => $"{k.Name}は城兵の命と引き換えに腹を切り、岩切城は開いた。",
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
    ProtoPerson? PlotTarget => People.Where(r => r.House == Rebel && r.Active && r.Id != HeadOf(Rebel) && r.Soldiers > 0).OrderBy(r => r.Id).FirstOrDefault();
    // 内応する気（90以上で約す、80以上で合図のとき動く）：鷹野への忠誠・本人への心証・城の全軍士気・兵糧・失った層。
    int PlotWill(ProtoPerson t) { var s = War.Siege!; return t.Loyalty + Math.Clamp(Favor(t.Id), -20, 20) + (100 - Math.Min(100, SpiritOf(Rebel))) / 2 + (s.Food <= 1 ? 15 : 0) + (100 - s.Gate) / 5; }
    // 軍議で出せる手（画面用）：調略の今の段。
    public string PlotStage => War.Siege?.Plot ?? "";
    string Scheme()
    {
        var s = War.Siege; if (s == null || !SiegeCouncilOpen) return "";
        var lead = SiegeLead;
        if (s.Plot == "約束") return PlotSignal();
        if (s.Plot != "") return s.Plot == "密書" ? $"{lead.Name}「返書を待て。」" : $"{lead.Name}「その手はもう使えぬ。」";
        if (PlotTarget is not { } t) return $"{lead.Name}「誘える者がおらぬ。」";
        if (!CommanderListens) return $"{lead.Name}「小細工は要らぬ。」";
        s.Plot = "密書"; s.PlotWith = t.Id; s.PlotAt = Second + 2 * Hour; Set("siege-decided"); // 本人が軍議で手を打ったので、総大将は今の攻め方のまま待つ
        Happen("plot-sent", $"鷹野勢は、城内の{t.Name}へ密書を忍ばせた。", lead.X, lead.Y, lead, Player);
        return $"{lead.Name}「よし、{t.Name}へ密書を忍ばせよ。」";
    }
    // 内応の合図：動くかどうかは、その武将がもう一度決める。
    string PlotSignal()
    {
        var s = War.Siege!; var lead = SiegeLead; var t = Person(s.PlotWith); var k = Person(HeadOf(Rebel)); s.Plot = "済";
        var c = Map.Site(FoeCastle); var near = Units.Where(u => Dist(u, castle: true) <= 1000_00).ToArray();
        if (!t.Active || t.House != Rebel || t.Soldiers <= 0 || PlotWill(t) < 80)
        { Happen("plot-none", $"合図を出したが、{t.Name}は動かなかった。", lead.X, lead.Y, lead, Player, t); return $"{lead.Name}「……動かぬか。」"; }
        int roll = Roll("plot:" + t.Id) % 100 + (Wit(t) - Wit(k)) / 2;
        if (roll >= 75 && CastleMorale < 50)
        {
            foreach (var r in People.Where(r => r.House == Rebel && r.Id != k.Id).ToArray()) { r.House = "takano"; r.Doing = "内応して鷹野に付いた"; r.Path = []; r.PathIndex = 0; }
            Set("parley-won"); OpenCastle($"{t.Name}が城内で{k.Name}を捕らえ、岩切城の門を開いた。");
            return $"{lead.Name}「でかした。」";
        }
        if (roll >= 35)
        {
            Happen("plot-gate", $"{t.Name}が内応し、城門を内から開いた。", c.X * 100L, c.Y * 100L, near);
            t.House = "takano"; t.Doing = "内応して鷹野に付いた"; t.Path = []; t.PathIndex = 0; War.Target.Remove(t.Id); War.Clashes.RemoveAll(x => x.A == t.Id || x.B == t.Id);
            LoseLayer(false);
            return $"{lead.Name}「城門が開いたぞ。」";
        }
        // 露見：城内で斬り合いになり、内応した隊は潰れ、城主の隊も兵を失う。
        int cost = Math.Min(k.Soldiers, t.Soldiers / 2); k.Soldiers -= cost; t.Soldiers = 0; t.Active = false; t.Doing = "内応が露見して討たれた";
        Happen("plot-found", $"{t.Name}の内応は露見し、城内で斬り合いになった（岩切勝元の兵{cost}を失わせた）。", c.X * 100L, c.Y * 100L, near);
        return $"{lead.Name}「……露見したか。」";
    }

    void OpenCastle(string text)
    {
        var k = Person(HeadOf(Rebel)); War.Result = "開城させた"; War.Phase = "終わり";
        Happen("castle-open", text, k.X, k.Y, [.. People.Where(u => InArmy(u) && u.Active), k]);
        foreach (var r in People.Where(r => r.House == Rebel)) { r.Active = false; r.Path = []; r.PathIndex = 0; r.Doing = "降った"; }
        War.Clashes.Clear(); War.Target.Clear();
    }
}
