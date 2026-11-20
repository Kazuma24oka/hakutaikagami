namespace HakutaiKagami.Core.Proto;

// 試作版の攻城（2026-11-20 ユーザーの整理）。
// ・始まり：野の戦いで岩切勢が岩切城へ退ききると、決着にせず攻城になる（鷹野の大目標が「敵を崩す」で、戦える隊が残っているとき）。
// ・城も一つの部隊：兵力（城内の隊の兵）・士気・兵糧・耐久を持つ。部隊と違う決まりは3つだけ。
//   ①動かない。②耐久が残っている層は地形の効果（城の兵が受ける打撃は4割5分・与える打撃は1.15倍）。
//   ③士気が切れても敗走しない。一層捨てて内へ退く（本丸で切れたら開城）。城の内では士気の下がりが半分で、内の層へ退くと70まで戻る。
// ・層：城の規模で数が決まる（岩切城は三の丸・二の丸・本丸の三層）。層ごとに耐久100。城の兵は今の層の城門に立ち、攻め手は城門へ寄せる。
//   耐久が0になるか、城の兵の士気が切れると、その層を失って内の層へ退く。最後の層を耐久で失えば落城、士気で失えば開城。
// ・基本は包囲から：攻城になると、まず城を三方から囲む（550m）。囲みの中（800m）の兵が「層の数×50」以上いれば包囲が完成し、
//   完成している間だけ、毎朝6時に城の兵糧（3日）が1日ぶん減る。足りなければ城は補給できて、兵糧は減らない。0になれば開城。
// ・攻め手の兵糧は総大将の隊が持ち、皆がそれを食べる（3日）。総大将の隊が崩れると兵糧を失う。尽きると6時間ごとに全軍士気が下がる。
// ・攻城の軍議：城に取りついた隊（800m）はいつでも開ける。包囲・力攻めを選ぶ（総大将が聞き入れるかは心証しだい）。
//   本人が言わなければ、陣を敷いて半刻で総大将が決める（勇猛な総大将は力攻め）。軍議の中で、調略と勧告も打てる（総大将が認めたとき）。
// ・力攻め：皆で城門の兵へ攻めかかる。耐久は攻め手の兵の数と武勇で削れる。
// ・朝駆け：城方は毎朝5時、囲みのいちばん小さい隊へ打って出る（慎重な城主は兵糧が残り1日になるまで出ない）。見張りが気づかなければ奇襲。
// ・勧告：軍議で条件を選んで、降伏の使者を出す（1日に一度。総大将が認めたとき）。出すと包囲になり、返事が戻るまで（半刻）攻めない・城方も打って出ない。
//   条件は 帰参（所領はそのままで鷹野に戻る）・退去（城主は城を明け渡して去る）・切腹（城主の切腹で城兵を助ける）。
//   受けるかは城主が決める：城の全軍士気・兵糧（外からは見えない）・失った層・勝元の心証に、条件の重さを足し引きする。
//   勇猛な総大将は、押しているうち（全軍士気60以上・兵糧2日以上）は「帰参」を認めない。
// ・調略：軍議で、城内の武将（城主でない者）へ密書を忍ばせる（総大将が認めたとき）。2時間で返事。内応を約すかは、その武将が決める
//   （鷹野への忠誠・本人への心証・城の全軍士気・兵糧・失った層）。約束があれば、軍議で「内応の合図」を出せる。
//   合図のとき、動くかどうかをもう一度その武将が決める。動けば、城主を生け捕って開城／内から城門を開けて一層失わせる／露見して城内で斬り合い、のどれか。
// ・終わり：落城（鎮めた）・開城（開城させた）・鷹野が退く（退いた）。落城・開城のときは岩切城へ入って論功行賞（Core/Proto/ProtoHome.cs）。
public sealed class ProtoSiege
{
    public long Since { get; set; }
    public long CampAt { get; set; }      // 城の前に陣を敷いた時刻（0＝まだ）
    public string Method { get; set; } = "包囲"; // 力攻め・包囲（基本は包囲から）
    public int Layers { get; set; } = 3;  // 層の数（城の規模）
    public int Layer { get; set; }        // 今守っている層（0＝いちばん外。Layers に達したら城は落ちている）
    public bool Ringed { get; set; }      // 包囲が完成している
    public int Gate { get; set; } = 100; // 今の層の耐久
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
    public static int LayersOf(string site) => site == FoeCastle ? 3 : 1;
    // 層の名前（外から。三層なら 三の丸・二の丸・本丸）と、層の外周までの遠さ（cm）。
    public static string LayerName(int layers, int layer) => (layers - layer) switch { <= 1 => "本丸", 2 => "二の丸", _ => "三の丸" };
    public static long LayerRadius(int layers, int layer) => 50_00L * (layers - layer);
    // 今の層の城門（攻め手の陣の側）。城の兵はここに立つ。
    public (long X, long Y) GatePoint(int layer)
    {
        var c = Map.Site(FoeCastle); long cx = c.X * 100L, cy = c.Y * 100L; var camp = SiegeCamp; int layers = War.Siege?.Layers ?? LayersOf(FoeCastle);
        double dx = camp.X - cx, dy = camp.Y - cy, len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy)); long r = LayerRadius(layers, Math.Min(layer, layers - 1)) - 15_00;
        return (cx + (long)(dx / len * r), cy + (long)(dy / len * r));
    }
    // 城の兵を今の層の城門に並べる（層を失ったとき・攻城の始まり）。
    void PlaceGarrison(bool atOnce)
    {
        var s = War.Siege!; var g = GatePoint(s.Layer); var c = Map.Site(FoeCastle); double dx = g.X - c.X * 100L, dy = g.Y - c.Y * 100L, len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        int i = 0;
        foreach (var r in People.Where(r => r.House == Rebel && r.Active).OrderByDescending(r => r.Id == HeadOf(Rebel)).ThenBy(r => r.Id))
        {
            int side = i == 0 ? 0 : (i % 2 == 1 ? 1 : -1) * ((i + 1) / 2); i++;
            long x = g.X + (long)(-dy / len * side * 45_00), y = g.Y + (long)(dx / len * side * 45_00);
            War.Post[r.Id] = [x, y];
            if (atOnce && r.Soldiers > 0 && !Withdrawing(r) && InFoeCastle(r) && !War.Routed.Contains(r.Id)) { r.X = x; r.Y = y; r.Path = []; r.PathIndex = 0; r.Doing = "城を守っている"; }
        }
    }
    public IEnumerable<ProtoPerson> Garrison => People.Where(r => r.House == Rebel && r.Active && r.Soldiers > 0 && InFoeCastle(r));
    public int RingNeed => (War.Siege?.Layers ?? 0) * 50;
    public int RingHave => AbleArmy.Where(u => Dist(u, castle: true) <= 800_00).Sum(u => u.Soldiers);
    IEnumerable<ProtoPerson> AbleArmy => People.Where(u => InArmy(u) && u.Active && u.Soldiers > 0 && !War.Routed.Contains(u.Id));
    ProtoPerson SiegeLead => AbleArmy.OrderByDescending(u => u.Id == Heir).ThenByDescending(u => u.Id == Karou).ThenBy(u => u.Id).FirstOrDefault() ?? Person(Heir);
    (long X, long Y) SiegeCamp => RoadPoint(550_00);

    // 岩切勢が城へ退ききった：攻城を始める。
    bool CanBesiege => War.Siege == null && GoalOf("takano") == Goals[0] && AbleArmy.Any() && Units.Any(u => u.House == Rebel && StillFights(u)); // 崩れて城へ逃げ込んだ隊も、城で立て直せるなら籠もる
    void StartSiege()
    {
        var castle = Map.Site(FoeCastle); var k = Person(HeadOf(Rebel));
        War.Siege = new ProtoSiege { Since = Second, Layers = LayersOf(FoeCastle) };
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
        AddSpirit(Rebel, 15); foreach (var r in People.Where(r => r.House == Rebel && r.Active && r.Soldiers > 0)) r.Morale = Math.Max(r.Morale, 60);
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
    public bool Garrisoned(ProtoPerson u) => BehindGate(u);
    public (long X, long Y)? PlayerSiegePost => War.Siege != null && War.Result == "" && InArmy(Player) ? SiegePost(Player) : null;
    // 攻城の軍議：城に取りついた隊（800m）は、いつでも開ける。
    public bool SiegeCouncilOpen => War.Siege != null && War.Result == "" && InArmy(Player) && Dist(Player, castle: true) <= 800_00;
    string DecideSiege(string? want)
    {
        var s = War.Siege; if (s == null || War.Result != "") return "";
        if (want != null && !SiegeCouncilOpen) return "";
        if (want == SiegeMethods[0] && s.TalksUntil > 0) return $"{SiegeLead.Name}「使者が戻るまでは攻めぬ。」";
        string prop = Flag("siege-decided") ? s.Method : SiegeProposal; bool present = want != null && SiegeMethods.Contains(want), refused = present && want != prop && !CommanderListens;
        string next = present && !refused ? want! : prop; var lead = SiegeLead; Set("siege-decided");
        if (next == s.Method && !present) return "";
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
        var auto = takano.Where(u => Auto(u) && !War.Routed.Contains(u.Id)).ToArray();
        // 兵を失った本人も、委任なら総大将について行く（軍議や説得に加われるように）。
        if (InArmy(Player) && Auto(Player) && Player.Soldiers == 0 && !Player.Moving && Dist(Player, SiegeLead) > 200_00) Walk(Player, (SiegeLead.X, SiegeLead.Y), SiegeLead.Name, $"{SiegeLead.Name}の陣へ向かっている");
        {
            // 陣を敷いたら知らせる。半刻たっても本人が何も言わなければ、総大将が攻め方を決める。
            var camp = SiegeCamp; var lead = SiegeLead;
            if (s.CampAt == 0 && Dist(lead, camp.X, camp.Y) <= 150_00) { s.CampAt = Second; Happen("siege-camp", $"{lead.Name}勢は岩切城の前に陣を敷いた。", lead.X, lead.Y, [.. AbleArmy.Where(u => Dist(u, lead) <= 2500_00)]); }
            if (s.CampAt > 0 && Second >= s.CampAt + 30 * 60 && !Flag("siege-decided") && s.TalksUntil == 0) DecideSiege(null);
            // 城の兵は、今の層の城門に並ぶ（朝駆けに出ている間は別）。
            foreach (var r in rebels.Where(r => !Withdrawing(r) && !War.Target.ContainsKey(r.Id) && !r.Moving && ClashOf(r.Id) == null && War.Post.TryGetValue(r.Id, out var at) && Dist(r, at[0], at[1]) > 30_00)) MarchTo(r, PostOf(r.Id));
            // 包囲の完成：囲みの中の兵が足りているか。
            bool ring = RingHave >= RingNeed;
            if (ring != s.Ringed) { s.Ringed = ring; Happen($"ring:{ring}:{Second}", ring ? "岩切城の包囲が成った。" : "兵が足りず、岩切城の包囲が破れた。", lead.X, lead.Y, [.. AbleArmy.Where(u => Dist(u, lead) <= 2500_00)]); }
            // 兵糧は総大将の隊が持つ。崩れると失う。
            if (War.Routed.Contains(Heir) && s.Supply > 0 && !Flag("supply-lost")) { Set("supply-lost"); s.Supply = 0; Happen("supply-lost", "総大将の隊が崩れ、鷹野勢は兵糧を失った。", lead.X, lead.Y, [.. AbleArmy]); }
        }
        if (s.Method == SiegeMethods[0])
        {
            foreach (var u in auto.Where(u => !War.Target.ContainsKey(u.Id)))
            { var t = rebels.OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault(); if (t != null) War.Target[u.Id] = t.Id; }
        }
        else
        {
            foreach (var u in auto)
            {
                // 城の中の敵は追わない。城から出てきた敵（見えている、または味方が戦っている）には当たる。
                if (War.Target.TryGetValue(u.Id, out var tid) && ClashOf(u.Id) == null && Dist(Person(tid), cx, cy) <= 250_00) War.Target.Remove(u.Id);
                if (War.Target.ContainsKey(u.Id)) continue;
                var outside = rebels.Where(r => !Withdrawing(r) && Dist(r, cx, cy) > 250_00 && (InSight(u, r) || ClashOf(r.Id) != null)).OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault();
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
            s.Days++; if (s.Ringed) s.Food = Math.Max(0, s.Food - 1); // 包囲が完成していなければ、城は補給できる
            s.Supply = Math.Max(0, s.Supply - 1); AddSpirit("takano", -5);
            Happen("siege-day:" + s.Days, $"岩切城を攻めて{s.Days}日。{(s.Supply > 0 ? $"兵糧はあと{s.Supply}日ぶん" : "兵糧が尽きた")}。", lead.X, lead.Y, [.. AbleArmy]);
        }
        // 尽きた側は、6時間ごとに全軍士気が下がる。城は、兵糧が尽きて士気が落ちきると開く。
        if (Second % (6 * Hour) == 0 && Second > s.Since + Hour)
        {
            if (s.Supply <= 0) AddSpirit("takano", -15);
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

    // 力攻め：城門が残っている間、城の兵は打たれにくく、打ちやすい。攻め手の打撃で城門が削れる（Fight から呼ぶ）。
    double CastleShield(ProtoPerson attacker, ProtoPerson defender)
    {
        var s = War.Siege; if (s == null || s.Gate <= 0) return 1;
        if (defender.House == Rebel && InFoeCastle(defender))
        {
            s.GateHits += attacker.Soldiers * 0.02 * (Val(attacker) / 50.0);
            return 0.45;
        }
        return attacker.House == Rebel && InFoeCastle(attacker) ? 1.15 : 1;
    }
    // 耐久が残っている間、城の兵は崩れない・不意を突かれない・騎突を受けない。
    bool BehindGate(ProtoPerson u) => War.Siege is { Gate: > 0 } s && s.Layer < s.Layers && War.Result == "" && u.House == Rebel && InFoeCastle(u) && !Withdrawing(u);
    public int CastleMorale { get { var g = Garrison.Where(u => !War.Routed.Contains(u.Id)).ToArray(); int men = g.Sum(u => u.Soldiers); return men == 0 ? 0 : g.Sum(u => u.Morale * u.Soldiers) / men; } }
    // 城の士気が切れたら（20を切ったら）、敗走の代わりに一層捨てて内へ退く（Fight から呼ぶ）。
    void GarrisonMorale(ProtoPerson[] all)
    {
        if (War.Siege == null || War.Result != "") return;
        // 城の士気＝城の内の隊の士気を、兵の数で重みを付けてならしたもの。個々の隊は20より下がらない（崩れない）。
        var inside = all.Where(u => BehindGate(u) && u.Soldiers > 0 && !War.Routed.Contains(u.Id)).ToArray(); if (inside.Length == 0) return;
        foreach (var u in inside) u.Morale = Math.Max(u.Morale, 10);
        if (CastleMorale < 20) LoseLayer(true);
    }
    // 層を失う。byMorale：士気が切れて捨てた（最後の層なら開城）。耐久が尽きた（最後の層なら落城）。
    void LoseLayer(bool byMorale)
    {
        var s = War.Siege!; var c = Map.Site(FoeCastle); string name = LayerName(s.Layers, s.Layer); s.Layer++;
        if (s.Layer >= s.Layers)
        {
            if (byMorale) { OpenCastle("城兵の士気が尽き、岩切城は門を開いた。"); return; }
            var k = Person(HeadOf(Rebel));
            Happen("gate-broken", $"岩切城の{name}が破られた。", c.X * 100L, c.Y * 100L, [.. Units.Where(u => Dist(u, castle: true) <= 1000_00)]);
            War.Result = "鎮めた"; War.Phase = "終わり";
            Happen("rebellion-crushed", "岩切城は落ち、岩切勝元の謀叛は鎮まった。勝元と井口は捕らえられた。", k.X, k.Y, [.. People.Where(u => InArmy(u) && u.Active), k]);
            foreach (var r in People.Where(r => r.House == Rebel)) { r.Active = false; r.Path = []; r.PathIndex = 0; r.Doing = "捕らえられた"; }
            War.Clashes.Clear(); War.Target.Clear(); return;
        }
        string next = LayerName(s.Layers, s.Layer); s.Gate = 100; s.GateHits = 0;
        Happen($"layer:{s.Layer}", byMorale ? $"岩切勢は士気が保てず、{name}を捨てて{next}へ退いた。" : $"岩切城の{name}が破られた。岩切勢は{next}へ退いた。", c.X * 100L, c.Y * 100L, [.. Units.Where(u => Dist(u, castle: true) <= 1000_00)]);
        AddSpirit(Rebel, -10); AddSpirit("takano", 5);
        // 内の層で立て直す（士気は70まで戻る）。城門の内へ下がるので、ぶつかり合いはいったん切れる。
        foreach (var r in Garrison) { r.Morale = Math.Max(r.Morale, 70); War.Chaos.Remove(r.Id); War.ChaosUntil.Remove(r.Id); }
        War.Clashes.RemoveAll(k => Person(k.A).House == Rebel && InFoeCastle(Person(k.A)) || Person(k.B).House == Rebel && InFoeCastle(Person(k.B)));
        PlaceGarrison(true);
    }
    void WearGate()
    {
        var s = War.Siege; if (s == null || s.Gate <= 0 || s.GateHits < 1) return;
        int d = (int)s.GateHits; s.GateHits -= d; s.Gate = Math.Max(0, s.Gate - d);
        if (s.Gate > 0) return;
        LoseLayer(false); // 今の層の耐久が尽きた
    }

    // ---- 勧告 ----
    public static readonly string[] SurrenderTerms = ["帰参", "退去", "切腹"];
    static string TermsText(string t) => t switch { "帰参" => "所領はそのままで鷹野に戻る", "退去" => "城主は城を明け渡して去る", _ => "城主の切腹で城兵を助ける" };
    public bool Talking => War.Siege is { TalksUntil: > 0 };
    int ParleyScore()
    {
        var s = War.Siege!; var k = Person(HeadOf(Rebel));
        return 30 + (100 - Math.Min(100, SpiritOf(Rebel))) / 2 + (s.Food <= 1 ? 30 : s.Food <= 2 ? 10 : 0) + 10 * s.Layer
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
        s.ParleyAt = Second; s.Terms = terms; s.TalksUntil = Second + 30 * 60; s.Method = SiegeMethods[1]; Set("siege-decided"); Set("envoy-mine");
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
    int PlotWill(ProtoPerson t) { var s = War.Siege!; return t.Loyalty + Math.Clamp(Favor(t.Id), -20, 20) + (100 - Math.Min(100, SpiritOf(Rebel))) / 2 + (s.Food <= 1 ? 15 : 0) + 10 * s.Layer; }
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
            Happen("plot-gate", $"{t.Name}が内応し、{LayerName(s.Layers, s.Layer)}の城門を内から開いた。", c.X * 100L, c.Y * 100L, near);
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
