namespace HakutaiKagami.Core.Proto;

// 試作版の攻城（2026-11-20 ユーザー「攻城戦から」）。
// ・始まり：野の戦いで岩切勢が退き貝で岩切城へ退ききると、決着にせず攻城になる（鷹野の大目標が「敵を崩す」で、戦える隊が残っているとき。
//   「陣を守り抜く」なら、退かせた所で決着）。
//   鷹野勢は城の前（道の550m手前）に陣を敷き、そこで攻め方の軍議。総大将が案を出し、本人がいれば再提案できる（聞き入れるかは心証しだい）。
// ・力攻め：皆で城の兵へ攻めかかる。城門（100）が残っている間、城の兵が受ける打撃は4割5分・与える打撃は1.15倍。
//   城門は攻め手の兵の数と武勇で削れ、0になると城の利が消えて、岩切の全軍士気が下がる。城の兵がみな崩れれば落城。
// ・囲む：城を三方から囲んで（550m）兵糧を断つ。城の兵糧は3日、攻め手の兵糧は2日（城には蓄えがある。待つだけでは攻め手が先に尽きる）。
//   毎朝6時に1日ぶん減り、長陣で鷹野の全軍士気も下がる。尽きた側は6時間ごとに全軍士気が下がる。
//   城方は毎朝5時、囲みのいちばん小さい隊へ朝駆けに出る（慎重な城主は兵糧が尽きるまで出ない）。見張りが気づかなければ奇襲になる。
// ・開城を説く：本人が城の近く（700m）で勝元に説く。城の全軍士気が低い・兵糧が乏しい（外からは見えない）・城門が破れている・
//   勝元の心証が良いほど聞き入れる。同じ日に二度は説けない。
// ・終わり：落城（鎮めた）・開城（開城させた）・鷹野が退く（退いた。撤退条件は野の戦いと同じ。日が傾いても、は攻城では数えない）。
//   落城・開城のときは岩切城へ入り、そこで論功行賞（Core/Proto/ProtoHome.cs）。
public sealed class ProtoSiege
{
    public long Since { get; set; }
    public long CampAt { get; set; }      // 城の前に陣を敷いた時刻（0＝まだ）
    public string Method { get; set; } = ""; // 力攻め・囲む（""＝まだ決まっていない）
    public int Gate { get; set; } = 100;
    public double GateHits { get; set; }
    public int Food { get; set; } = 3;    // 城の兵糧（日）
    public int Supply { get; set; } = 2;  // 攻め手の兵糧（日）
    public int Days { get; set; }
    public long SallyUntil { get; set; }
    public long ParleyAt { get; set; } = -1;
}

public sealed partial class ProtoWorld
{
    public static readonly string[] SiegeMethods = ["力攻め", "囲む"];
    const string FoeCastle = "iwakiri-castle";
    bool InFoeCastle(ProtoPerson u) => Dist(u, castle: true) <= 150_00; // 城の内（城の真ん中から150m）
    IEnumerable<ProtoPerson> AbleArmy => People.Where(u => InArmy(u) && u.Active && u.Soldiers > 0 && !War.Routed.Contains(u.Id));
    ProtoPerson SiegeLead => AbleArmy.OrderByDescending(u => u.Id == Heir).ThenByDescending(u => u.Id == Karou).ThenBy(u => u.Id).FirstOrDefault() ?? Person(Heir);
    (long X, long Y) SiegeCamp => RoadPoint(550_00);

    // 岩切勢が城へ退ききった：攻城を始める。
    bool CanBesiege => War.Siege == null && GoalOf("takano") == Goals[0] && AbleArmy.Any() && Units.Any(u => u.House == Rebel && StillFights(u)); // 崩れて城へ逃げ込んだ隊も、城で立て直せるなら籠もる
    void StartSiege()
    {
        var castle = Map.Site(FoeCastle); var k = Person(HeadOf(Rebel));
        War.Siege = new ProtoSiege { Since = Second };
        War.RetreatBy.Remove(Rebel); War.RetreatAt.Remove(Rebel); War.RetreatRules[Rebel] = []; War.Goals[Rebel] = Goals[1];
        foreach (var r in People.Where(r => r.House == Rebel))
        {
            foreach (var f in new[] { "retreat-heard:", "withdrawing:", "retreat-at:", "stays:" }) flags.Remove(f + r.Id);
            War.Target.Remove(r.Id); War.Post[r.Id] = [castle.X * 100L, castle.Y * 100L];
        }
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
    public string SiegeSpeech => $"{SiegeLead.Name}「岩切城は、{MethodText(SiegeProposal)}。」";
    // 攻め方の軍議：陣を敷いてから半刻のあいだ、本人が総大将のそば（300m）にいれば加われる。
    public bool SiegeCouncilOpen => War.Siege is { Method: "", CampAt: > 0 } && War.Result == "" && InArmy(Player) && Dist(Player, SiegeLead) <= 300_00;
    string DecideSiege(string? want)
    {
        var s = War.Siege; if (s == null || s.Method != "" || War.Result != "") return "";
        if (want != null && !SiegeCouncilOpen) return "";
        string prop = SiegeProposal; bool present = want != null && SiegeMethods.Contains(want), refused = present && want != prop && !CommanderListens;
        s.Method = present && !refused ? want! : prop; var lead = SiegeLead;
        foreach (var u in People.Where(InArmy)) War.Target.Remove(u.Id);
        Happen("siege-method", $"軍議で決まった：岩切城は、{MethodText(s.Method)}。", lead.X, lead.Y, [.. AbleArmy.Where(u => Dist(u, lead) <= 2500_00)]);
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
        if (s.Method == "")
        {
            var camp = SiegeCamp; var lead = SiegeLead;
            foreach (var u in auto.Where(u => !u.Moving && ClashOf(u.Id) == null && !War.Target.ContainsKey(u.Id) && Dist(u, camp.X, camp.Y) > 150_00)) Walk(u, camp, "攻城の陣", "岩切城の前へ兵を進めている");
            if (s.CampAt == 0 && Dist(lead, camp.X, camp.Y) <= 150_00) { s.CampAt = Second; Happen("siege-camp", $"{lead.Name}勢は岩切城の前に陣を敷いた。", lead.X, lead.Y, [.. AbleArmy.Where(u => Dist(u, lead) <= 2500_00)]); }
            if (s.CampAt > 0 && Second >= s.CampAt + 30 * 60) DecideSiege(null);
        }
        else if (s.Method == SiegeMethods[0])
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
        SiegeDays(takano, rebels);
    }

    void SiegeDays(ProtoPerson[] takano, ProtoPerson[] rebels)
    {
        var s = War.Siege!; var k = Person(HeadOf(Rebel)); var lead = SiegeLead; long ofDay = Second % Day;
        // 朝6時：兵糧が1日ぶん減る。長陣で攻め手の全軍士気も下がる。
        if (ofDay == 6 * Hour && Second > s.Since + Hour)
        {
            s.Days++; s.Food = Math.Max(0, s.Food - 1); s.Supply = Math.Max(0, s.Supply - 1); AddSpirit("takano", -5);
            Happen("siege-day:" + s.Days, $"岩切城を攻めて{s.Days}日。{(s.Supply > 0 ? $"兵糧はあと{s.Supply}日ぶん" : "兵糧が尽きた")}。", lead.X, lead.Y, [.. AbleArmy]);
        }
        // 尽きた側は、6時間ごとに全軍士気が下がる。城は、兵糧が尽きて士気が落ちきると開く。
        if (Second % (6 * Hour) == 0 && Second > s.Since + Hour)
        {
            if (s.Supply <= 0) AddSpirit("takano", -15);
            if (s.Food <= 0) { if (SpiritOf(Rebel) <= 30) { OpenCastle("兵糧が尽き、岩切城は門を開いた。"); return; } AddSpirit(Rebel, -10); }
        }
        // 朝5時：城方の朝駆け。囲みのいちばん小さい隊へ。
        var able = rebels.Where(r => !Withdrawing(r) && r.Soldiers >= 10).ToArray();
        if (ofDay == 5 * Hour && Second > s.Since + Hour && s.SallyUntil == 0 && able.Length > 0 && (s.Food <= 0 || SpiritOf(Rebel) >= 50 && k.Temper != "慎重")) // 慎重な城主は、兵糧が尽きるまで出ない
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
    // 城門が残っている間、城の兵は崩れない・不意を突かれない・騎突を受けない。
    bool BehindGate(ProtoPerson u) => War.Siege is { Gate: > 0 } && u.House == Rebel && InFoeCastle(u);
    void WearGate()
    {
        var s = War.Siege; if (s == null || s.Gate <= 0 || s.GateHits < 1) return;
        int d = (int)s.GateHits; s.GateHits -= d; s.Gate = Math.Max(0, s.Gate - d);
        if (s.Gate > 0) return;
        var c = Map.Site(FoeCastle);
        Happen("gate-broken", "岩切城の門が破られた。", c.X * 100L, c.Y * 100L, [.. Units.Where(u => Dist(u, castle: true) <= 1000_00)]);
        AddSpirit(Rebel, -20); AddSpirit("takano", 10);
    }

    // ---- 開城を説く ----
    public bool CanParley => War.Siege is { } s && War.Result == "" && InArmy(Player) && ClashOf(Player.Id) == null && Dist(Player, castle: true) <= 700_00 && Person(HeadOf(Rebel)).Active && (s.ParleyAt < 0 || Second / Day != s.ParleyAt / Day);
    int ParleyScore()
    {
        var s = War.Siege!; var k = Person(HeadOf(Rebel));
        return 30 + (100 - Math.Min(100, SpiritOf(Rebel))) / 2 + (s.Food <= 1 ? 30 : s.Food <= 2 ? 10 : 0) + (s.Gate <= 0 ? 15 : s.Gate <= 50 ? 5 : 0)
            + Math.Clamp(Favor(k.Id), -20, 20) + (Flag("persuaded") ? 10 : 0) - (k.Temper == "勇猛" ? 10 : 0);
    }
    string Parley()
    {
        if (!CanParley) return "今は説けない";
        var s = War.Siege!; var k = Person(HeadOf(Rebel)); s.ParleyAt = Second; int score = ParleyScore();
        Happen("parley:" + Second, $"{Player.Name}が岩切勝元に、城を開くよう説いた。", Player.X, Player.Y, Player, k);
        if (score >= 70)
        {
            Set("parley-won"); OpenCastle($"{Player.Name}の説得で、岩切勝元は城を開いた。");
            return $"{k.Name}「……もはやこれまでか。城は開く。兵の命は助けてくれ。」";
        }
        // 断り方で、城の内の様子がうかがえる。
        return s.Food <= 1 ? $"{k.Name}「……まだ降らぬ。だが、長くは保たぬかもしれぬな。」" : score >= 55 ? $"{k.Name}「……今は聞けぬ。出直してまいれ。」" : $"{k.Name}「米も矢もまだある。帰って若殿に伝えよ、城は渡さぬと。」";
    }
    void OpenCastle(string text)
    {
        var k = Person(HeadOf(Rebel)); War.Result = "開城させた"; War.Phase = "終わり";
        Happen("castle-open", text, k.X, k.Y, [.. People.Where(u => InArmy(u) && u.Active), k]);
        foreach (var r in People.Where(r => r.House == Rebel)) { r.Active = false; r.Path = []; r.PathIndex = 0; r.Doing = "降った"; }
        War.Clashes.Clear(); War.Target.Clear();
    }
}
