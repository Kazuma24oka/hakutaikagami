namespace HakutaiKagami.Core.Proto;

// 試作版の合戦（2026-11-18 ユーザー「関ケ原の陣取り」「軍議で誰がどこに陣取るか、いつ仕掛けるかを決める」
// 「抜け駆け・配置につかない・相手に先にとられていた」「相手の位置がわからない、諜報次第で見える」）。Docs/CORE_LOOP.md。
// 流れ：当主の死 → 家督 → 訃報（書状） → 城主が知って判断（忠誠しだいで謀叛。プレイヤーは説き伏せられる） → 手切れの書状
//       → 軍議（陣・出立・合図の時刻） → 行軍（遅れ・先取り・抜け駆け） → 合図の狼煙 → 合戦（兵・地形・能力・挟み撃ち・士気・敗走）。
// 見えない敵：敵の隊は、本人の見える範囲（地形と知略で変わる）に入ったときだけ見える。味方が見た敵は、知らせが届くまで遅れて
// 「最後に見た場所と時刻」の印になる。物見を出すと、見て戻ってきたときに印が増える。
public sealed record ProtoMarker(string Unit, long X, long Y, long SeenAt, string By);
public sealed record ProtoReport(string Unit, long X, long Y, long SeenAt, long ArriveAt, string By);
public sealed class ProtoScout
{
    public string Id { get; set; } = "";
    public string Target { get; set; } = "";
    public long X { get; set; }
    public long Y { get; set; }
    public long[][] Path { get; set; } = [];
    public int PathIndex { get; set; }
    public string Phase { get; set; } = "行き"; // 行き・帰り・済
    public List<ProtoMarker> Seen { get; set; } = [];
}
public sealed class ProtoWarState
{
    public string Phase { get; set; } = ""; // 軍議待ち・行軍・合戦・終わり
    public long CouncilAt { get; set; }
    public long DepartAt { get; set; }
    public long SignalAt { get; set; }
    public bool Signaled { get; set; }
    public bool PlayerAtCouncil { get; set; }
    public Dictionary<string, string> Assign { get; set; } = [];
    public Dictionary<string, string> Target { get; set; } = []; // 抜け駆け・合図の後に向かう敵
    public List<string> Routed { get; set; } = [];
    public string Result { get; set; } = "";
}
public sealed record ProtoCouncilView(string Commander, string[] Opinions, (string Id, string Name)[] Positions, string[] Departures, string[] Signals);

public sealed partial class ProtoWorld
{
    public Dictionary<string, string> Heads { get; } = [];
    public ProtoWarState War { get; private set; } = new();
    public List<ProtoMarker> Markers { get; } = [];
    public List<ProtoReport> Reports { get; } = [];
    public List<ProtoScout> Scouts { get; } = [];
    int scoutSeq;
    const string Rebel = "iwakiri";
    public static readonly string[] Departures = ["今夜のうちに出る", "夜明けに出る"];
    public static readonly string[] Signals = ["卯の刻（6時）に狼煙", "辰の刻（8時）に狼煙"];

    public ProtoPosition PositionOf(string id) => Map.Data.Positions!.First(p => p.Id == id);
    public string HeadOf(string house) => Heads.TryGetValue(house, out var h) ? h : Map.Data.Houses.First(x => x.Id == house).Head;
    static bool Hostile(string a, string b) => a != b && (a == Rebel && b == "takano" || a == "takano" && b == Rebel);
    public IEnumerable<ProtoPerson> Units => People.Where(p => p.Active && p.Soldiers > 0 && p.House != "bandit");

    // 見える範囲（cm）：地形（丘は広く、森は狭い）と知略と技能で変わる。
    public long SightOf(ProtoPerson p)
    {
        double r = 350_00;
        var g = Map.GroundAtCm(p.X, p.Y);
        if (g == Ground.Mountain) r *= 1.8; else if (g == Ground.Forest) r *= 0.6;
        r += (p.Wits - 50) * 4_00;
        if (p.Skills.Contains("物見上手")) r += 150_00;
        return (long)r;
    }
    bool InSight(ProtoPerson viewer, ProtoPerson target) => Dist(viewer, target) <= SightOf(viewer);

    // ---- 毎分 ----
    void Wars()
    {
        int day = DayIndex;
        var lord = Person(Lord);
        if (day == 2 && HourOfDay == 5 && !Flag("lord-dead"))
        {
            Set("lord-dead"); lord.Active = false; lord.Path = []; lord.PathIndex = 0;
            Heads["takano"] = Heir; Person(Heir).Role = "当主";
            var heir = Person(Heir); var castle = Map.Site("takano-castle");
            Happen("lord-dead", "鷹野義正が病で亡くなり、鷹野正之が家を継いだ。", castle.X * 100L, castle.Y * 100L, heir, Person(Karou));
            var notice = Send(heir, Katsumoto, "父義正が身罷った。これよりは正之が鷹野の家を継ぐ。変わらぬ忠勤を頼む。", "death-notice");
            Offer(Heir, "岩切城へ訃報を届ける", "death-notice", At(2, 9), At(2, 18), 3,
                new ProtoStep("hand", "岩切勝元に訃報を渡す", "iwakiri-castle", Katsumoto, Letter: notice.Id), new ProtoStep("meet", "鷹野正之に報告する", "takano-castle", Heir));
        }
        if (day == 2 && HourOfDay == 10 && !Flag("invite-sent"))
        {
            Set("invite-sent");
            var l = Send(Person(Nobufusa), Katsumoto, "鷹野の若殿では先が知れぬ。沢渡と結べば、岩切の地は安堵いたそう。", "invite"); l.Carrier = Shiota;
        }
        var katsumoto = Person(Katsumoto);
        if (flags.TryGetValue("katsumoto-knows", out var knew) && !Flag("katsumoto-decided") && Second >= knew + 2 * Hour)
        {
            Set("katsumoto-decided");
            int loyalty = katsumoto.Loyalty + (Favor(Katsumoto) >= 10 ? 10 : 0);
            if (loyalty < 50) RaiseRebellion(katsumoto);
            else Happen("katsumoto-loyal", "岩切勝元は新しい当主に従うと誓った。", katsumoto.X, katsumoto.Y, katsumoto);
        }
        // 軍議の刻限に城にいれば呼ばれて、その場で謀叛を知る。いなければ知らないまま（大将が決める）。
        if (War.Phase == "軍議待ち" && SiteOf(Player) == "takano-castle" && (Second >= War.CouncilAt || SiteOf(Person(Heir)) == "takano-castle") && Fact("defiance") is { } defiance && !Knows(Player, "defiance"))
        { Learn(Player, defiance, Person(Heir).Name); Note($"{Person(Heir).Name}に軍議へ呼ばれた。"); }
        if (War.Phase == "軍議待ち" && Second >= War.CouncilAt + 30 * 60 && War.Assign.Count == 0) DecideCouncil(null, null, null); // 本人が来なければ大将が決める
        if (War.Phase is "行軍" or "合戦") Battle();
        // 謀叛方は夜明け前（4時）に陣へ出る。鷹野が夜明けに出れば丘は先に取られる
        if (Flag("rebellion") && !Flag("rebels-out") && HourOfDay == 4)
        {
            Set("rebels-out");
            War.Assign[Katsumoto] = "south-hill"; War.Assign[Iguchi] = "road-mouth";
            foreach (var id in new[] { Katsumoto, Iguchi }) { var p = Person(id); if (p.House == Rebel) MarchTo(p, PositionOf(War.Assign[id])); }
        }
    }

    void RaiseRebellion(ProtoPerson katsumoto)
    {
        Set("rebellion"); katsumoto.House = Rebel; Heads[Rebel] = Katsumoto;
        var iguchi = Person(Iguchi); if (iguchi.Loyalty < 70) iguchi.House = Rebel;
        Appointments.RemoveAll(a => a.Person == Iguchi && iguchi.House == Rebel);
        Happen("rebellion", "岩切勝元が謀叛し、岩切城に籠もった。", katsumoto.X, katsumoto.Y, katsumoto, iguchi);
        var l = Send(katsumoto, Heir, "若殿に仕える気はない。これより岩切は鷹野と手を切る。", "defiance"); l.Carrier = Iguchi;
        Appointments.RemoveAll(a => a.Person == Katsumoto || a.Person == Iguchi);
    }

    // ---- 説き伏せる ----
    bool CanPersuade => !Flag("katsumoto-decided") && !Flag("persuaded") && Knows(Player, "lord-dead") && flags.ContainsKey("katsumoto-knows") && Near(Player, Person(Katsumoto));
    string Persuade()
    {
        if (!CanPersuade) return "今は話せない";
        Set("persuaded"); var k = Person(Katsumoto); int add = Rank >= 1 || Merit >= 6 ? 20 : 10; k.Loyalty += add; AddFavor(Katsumoto, 5);
        Happen("persuaded", $"{Player.Name}が岩切勝元に、新しい当主に従うよう説いた。", k.X, k.Y, Player, k);
        return add >= 20 ? $"{k.Name}「……そなたがそこまで言うなら、考え直そう。」" : $"{k.Name}「……考えておこう。」";
    }

    // ---- 軍議 ----
    // 本人・大将・家老が城にそろえばすぐ開く（刻限を過ぎれば、そろわなくても開く）。
    public bool CouncilOpen => War.Phase == "軍議待ち" && War.Assign.Count == 0 && SiteOf(Player) == "takano-castle" && Knows(Player, "defiance")
        && (Second >= War.CouncilAt || SiteOf(Person(Heir)) == "takano-castle" && SiteOf(Person(Karou)) == "takano-castle");
    public ProtoCouncilView Council()
    {
        var heir = Person(Heir); var karou = Person(Karou);
        var ops = new List<string>
        {
            $"{heir.Name}（大将）「夜明けを待たずに道の口から一気に攻めかかる。岩切の者どもに時を与えるな。」",
            $"{karou.Name}「南の丘を先に押さえれば、城から出る敵を見下ろせます。勝元は用心深い男、夜明けには丘へ出てまいりましょう。今夜のうちに丘へ。」",
        };
        if (Knows(Player, "invite")) ops.Add($"{Player.Name}は、沢渡家が勝元を誘っていたことを知っている。");
        return new(heir.Name, [.. ops], Map.Data.Positions!.Select(p => (p.Id, p.Name)).ToArray(), Departures, Signals);
    }
    // 本人が出ていれば本人の意見（自分の陣・出立・合図）が通る。出ていなければ大将の考えで決まる。
    public string DecideCouncil(string? playerPosition, string? departure, string? signal)
    {
        if (War.Phase != "軍議待ち" || War.Assign.Count > 0) return "";
        bool present = playerPosition != null; War.PlayerAtCouncil = present;
        string dep = departure ?? Departures[1], sig = signal ?? Signals[0];
        War.Assign[Heir] = "road-mouth"; War.Assign[Karou] = "south-hill"; War.Assign[Player.Id] = playerPosition ?? "forest-edge";
        long night = At(DayIndex, 21), dawn = (Second / Day + 1) * Day + 4 * Hour;
        War.DepartAt = dep == Departures[0] ? Math.Max(Second + 10 * 60, Math.Min(night, Second + Hour)) : dawn;
        War.SignalAt = (dawn / Day) * Day + (sig == Signals[0] ? 6 : 8) * Hour;
        War.Phase = "行軍";
        // 兵を預かり、出陣の任務を受ける。軍議の任務は、軍議に加わって済む。
        Player.Soldiers = 30; Player.Morale = 100;
        foreach (var t in Tasks.Where(t => t.Kind == "council" && t.State == TaskState.Open && present)) Next(t);
        var mine = PositionOf(War.Assign[Player.Id]);
        Note($"{Person(Heir).Name}から兵30を預かった。");
        if (mine.Id != "takano-castle")
        {
            var muster = Offer(Heir, "出陣：" + mine.Name, "muster", Second, War.SignalAt, 2, new ProtoStep("visit", mine.Name + "に陣を取る", X: mine.X * 100L, Y: mine.Y * 100L));
            muster.State = TaskState.Open; muster.Speech = $"そなたは{mine.Name}に陣を取れ。{dep}。合図は{sig}じゃ。";
            Note($"{Person(Heir).Name}「{muster.Speech}」");
        }
        var where = string.Join("、", War.Assign.Select(a => $"{Person(a.Key).Name}は{PositionOf(a.Value).Name}"));
        var castle = Map.Site("takano-castle");
        Happen("council", $"軍議で決まった：{where}。{dep}。{sig}。", castle.X * 100L, castle.Y * 100L, Person(Heir), Person(Karou), Player);
        return present ? $"{Person(Heir).Name}「それでいこう。」" : "";
    }

    void MarchTo(ProtoPerson p, ProtoPosition pos)
    {
        var to = ProtoMove.Nearest(Map, pos.X * 100L, pos.Y * 100L);
        var path = ProtoMove.Plan(Map, (p.X, p.Y), to); if (path == null) return;
        p.Path = path; p.PathIndex = 0; p.Goal = pos.Name; p.Doing = pos.Name + "へ兵を進めている";
    }

    // ---- 行軍と合戦 ----
    void Battle()
    {
        var takano = Units.Where(u => u.House == "takano" && War.Assign.ContainsKey(u.Id) && !War.Routed.Contains(u.Id)).ToArray();
        var rebels = Units.Where(u => u.House == Rebel && !War.Routed.Contains(u.Id)).ToArray();
        // 出立：統率が低いほど遅れて出る。
        foreach (var u in takano)
        {
            if (u.Player || Flag("departed:" + u.Id)) continue;
            long delay = (100 - u.Command) / 2 * 60;
            if (Second >= War.DepartAt + delay)
            {
                Set("departed:" + u.Id); var pos = PositionOf(War.Assign[u.Id]);
                if (pos.Id != "takano-castle") MarchTo(u, pos);
            }
        }
        if (Second == War.DepartAt) Note($"出立の刻限になった。{PositionOf(War.Assign[Player.Id]).Name}へ向かう。");
        // 先に取られていた：着く前に敵がその陣にいれば、手前で止まって知らせる。
        foreach (var u in takano.Where(u => !u.Player && u.Moving && !War.Target.ContainsKey(u.Id)))
        {
            var pos = PositionOf(War.Assign[u.Id]); long px = pos.X * 100L, py = pos.Y * 100L;
            var holder = rebels.FirstOrDefault(r => Dist(r, px, py) <= 150_00);
            if (holder != null && Dist(u, px, py) <= 400_00 && InSight(u, holder) && !Flag("taken:" + pos.Id))
            {
                Set("taken:" + pos.Id); u.Path = []; u.PathIndex = 0; u.Doing = pos.Name + "の手前で止まっている";
                Happen("taken:" + pos.Id, $"{pos.Name}は、すでに{holder.Name}の兵に押さえられていた。", u.X, u.Y, u);
            }
        }
        // 謀叛方も、向かう陣が鷹野に押さえられていれば手前で止まる。
        foreach (var r in rebels.Where(r => r.Moving && War.Assign.ContainsKey(r.Id) && !War.Target.ContainsKey(r.Id)))
        {
            var pos = PositionOf(War.Assign[r.Id]); long px = pos.X * 100L, py = pos.Y * 100L;
            var holder = takano.Concat(new[] { Player }).FirstOrDefault(t => t.Soldiers > 0 && Dist(t, px, py) <= 150_00);
            if (holder != null && Dist(r, px, py) <= 400_00 && InSight(r, holder) && !Flag("taken-by-takano:" + pos.Id))
            {
                Set("taken-by-takano:" + pos.Id); r.Path = []; r.PathIndex = 0; r.Doing = pos.Name + "の手前で止まっている";
                Happen("taken-by-takano:" + pos.Id, $"{r.Name}の兵は、{pos.Name}がすでに鷹野に押さえられているのを見て、手前で止まった。", r.X, r.Y, r);
            }
        }
        // 合図の狼煙：見える所（2.5km）の味方はすぐ、遠い味方は知らせが届いてから仕掛ける。
        if (!War.Signaled && Second >= War.SignalAt)
        {
            War.Signaled = true; War.Phase = "合戦"; var heir = Person(Heir);
            Happen("signal", $"{heir.Name}の陣から合図の狼煙が上がった。", heir.X, heir.Y, heir);
            foreach (var u in takano) { long d = Dist(u, heir); flags["attack-at:" + u.Id] = d <= 2500_00 ? Second : Second + d / 200; }
            if (Dist(Player, heir) <= 2500_00) Learn(Player, Fact("signal")!);
        }
        // 抜け駆け：逸る者は、合図の前でも敵が見えると仕掛けることがある。
        foreach (var u in takano.Where(u => !u.Player && !War.Signaled && u.Temper == "逸る" && !War.Target.ContainsKey(u.Id)))
        {
            var seen = rebels.Where(r => InSight(u, r)).OrderBy(r => Dist(u, r)).FirstOrDefault();
            if (seen != null && Roll("nukegake:" + u.Id) % 100 < 30)
            {
                War.Target[u.Id] = seen.Id; War.Phase = "合戦";
                Happen("nukegake:" + u.Id, $"{u.Name}が合図を待たずに{seen.Name}の兵へ仕掛けた。", u.X, u.Y, u);
            }
        }
        // 丘の陣は守る（上ってくる敵を高い所から打つ）。ほかの陣は合図で攻めかかる。
        foreach (var u in takano.Where(u => !u.Player && War.Signaled && War.Assign[u.Id] != "south-hill" && !War.Target.ContainsKey(u.Id) && flags.TryGetValue("attack-at:" + u.Id, out var at) && Second >= at))
        {
            var target = rebels.OrderBy(r => Dist(u, r)).FirstOrDefault(); if (target != null) War.Target[u.Id] = target.Id;
        }
        // 謀叛方は、合戦になったら見えている敵に向かう。
        foreach (var r in rebels.Where(r => War.Phase == "合戦" && !War.Target.ContainsKey(r.Id)))
        {
            var seen = takano.Concat(new[] { Player }).Where(t => t.Soldiers > 0 && InSight(r, t)).OrderBy(t => Dist(r, t)).FirstOrDefault();
            if (seen != null) War.Target[r.Id] = seen.Id;
        }
        // 向かう：相手が動けば道筋を引き直す（5分ごと）。
        foreach (var (id, targetId) in War.Target.ToArray())
        {
            var u = Person(id); var t = Person(targetId);
            if (War.Routed.Contains(id)) continue;
            if (War.Routed.Contains(targetId) || !t.Active) { War.Target.Remove(id); continue; }
            if (Dist(u, t) > 60_00 && (!u.Moving || Second % 300 == 0)) Walk(u, (t.X, t.Y), t.Name, $"{t.Name}の兵へ攻めかかっている");
        }
        if (War.Phase == "合戦") Fight(takano.Concat(rebels).Concat(Player.Soldiers > 0 && !War.Routed.Contains(Player.Id) ? new[] { Player } : []).Distinct().ToArray());
        // 終わり
        var liveRebels = Units.Where(u => u.House == Rebel && !War.Routed.Contains(u.Id)).ToArray();
        // 本人は、戦いに加わっていなければ数えない（本人が後ろにいるだけでは合戦は続かない）。
        var liveTakano = Units.Where(u => u.House == "takano" && War.Assign.ContainsKey(u.Id) && !War.Routed.Contains(u.Id) && (!u.Player || Flag("fought:" + u.Id))).ToArray();
        if (Flag("rebels-out") && liveRebels.Length == 0 && War.Result == "")
        {
            War.Result = "鎮めた"; War.Phase = "終わり"; var k = Person(Katsumoto);
            Happen("rebellion-crushed", "岩切勝元の謀叛は鎮まった。勝元と井口は捕らえられた。", k.X, k.Y, Person(Heir), Person(Karou), k);
            foreach (var r in People.Where(r => r.House == Rebel)) { r.Active = false; r.Path = []; r.PathIndex = 0; r.Doing = "捕らえられた"; }
            if (Flag("fought:" + Player.Id)) { Merit += 10; Note("謀叛の討伐で働いた。勲功10。"); }
        }
        else if (War.Phase == "合戦" && liveTakano.Length == 0 && War.Result == "")
        {
            War.Result = "退けられた"; War.Phase = "終わり"; var h = Person(Heir);
            Happen("expedition-failed", "鷹野の討手は岩切勢に退けられた。", h.X, h.Y, h, Person(Karou));
            foreach (var r in People.Where(r => r.House == Rebel && r.Active)) Walk(r, Spot(Map.Site("iwakiri-castle"), 3), "iwakiri-castle", "岩切城へ引き上げている");
        }
    }

    // 1分ごとの打ち合い：兵 × 武勇 × 地形（高い所・森）× 挟み撃ち ÷ 相手の統率。士気が20を切ると敗走。
    void Fight(ProtoPerson[] all)
    {
        var hits = new Dictionary<string, double>();
        foreach (var a in all)
        {
            var foes = all.Where(b => Hostile(a.House, b.House) && Dist(a, b) <= 80_00 && !War.Routed.Contains(b.Id)).ToArray();
            if (foes.Length == 0 || War.Routed.Contains(a.Id)) continue;
            Set("fought:" + a.Id);
            var b = foes.OrderBy(f => Dist(a, f)).First();
            double atk = a.Soldiers * 0.012 * (a.Valor / 50.0) * (a.Morale / 100.0 + 0.5);
            long rise = Map.HeightAtCm(a.X, a.Y) - Map.HeightAtCm(b.X, b.Y);
            if (rise > 10_00) atk *= 1.3;        // 高い所から打ち下ろす
            else if (rise < -10_00) atk *= 0.7;  // 坂の下から攻め上る
            if (Map.GroundAtCm(b.X, b.Y) == Ground.Forest) atk *= 0.8;                      // 森に籠もる相手
            if (all.Count(x => Hostile(x.House, b.House) && Dist(x, b) <= 80_00) >= 2) atk *= 1.3; // 挟み撃ち
            if (a.Skills.Contains("槍衾") && a.Moving == false) atk *= 1.15;
            atk /= b.Command / 50.0;
            hits[b.Id] = hits.GetValueOrDefault(b.Id) + atk;
            a.Doing = $"{b.Name}の兵と戦っている";
        }
        foreach (var (id, h) in hits)
        {
            var b = Person(id); int loss = Math.Max(1, (int)Math.Round(h));
            int before = b.Soldiers; b.Soldiers = Math.Max(0, b.Soldiers - loss);
            b.Morale = Math.Max(0, b.Morale - (int)Math.Ceiling(loss * 100.0 / Math.Max(1, before) * 1.5));
            if (b.Morale < 20 || b.Soldiers == 0) Rout(b);
        }
    }

    void Rout(ProtoPerson b)
    {
        if (War.Routed.Contains(b.Id)) return;
        War.Routed.Add(b.Id); War.Target.Remove(b.Id);
        Happen("rout:" + b.Id, $"{b.Name}の兵が崩れて退いた（残り{b.Soldiers}）。", b.X, b.Y, b);
        if (b.Player) { Stop(); Note("兵が崩れた。これ以上は戦えない。"); return; }
        var home = b.House == Rebel ? "iwakiri-castle" : b.Home;
        Walk(b, Spot(Map.Site(home), 2), home, "敗走している");
    }

    // ---- 本人が攻めかかる・退く ----
    string Attack(string id)
    {
        var e = Person(id); if (!InSight(Player, e) || Player.Soldiers <= 0) return "攻められない";
        War.Target[Player.Id] = id;
        if (War.Phase == "行軍")
        {
            War.Phase = "合戦"; AddFavor(Heir, -5);
            Happen("nukegake:" + Player.Id, $"{Player.Name}が合図を待たずに{e.Name}の兵へ仕掛けた。", Player.X, Player.Y, Player);
        }
        MoveTo(e.X, e.Y, e.Name); Player.Doing = e.Name + "の兵へ攻めかかっている";
        return $"{e.Name}の兵へ攻めかかる。";
    }
    string Withdraw()
    {
        War.Target.Remove(Player.Id);
        var pos = War.Assign.TryGetValue(Player.Id, out var a) ? PositionOf(a) : PositionOf("takano-castle");
        MoveTo(pos.X * 100L, pos.Y * 100L, pos.Name); return pos.Name + "まで兵を退く。";
    }

    // ---- 見えない敵：見える範囲・味方の知らせ・物見 ----
    void Watch()
    {
        if (War.Phase is not ("行軍" or "合戦")) return;
        var me = Player;
        foreach (var e in Units.Where(e => Hostile(me.House, e.House)))
        {
            if (InSight(me, e)) Mark(new ProtoMarker(e.Id, e.X, e.Y, Second, "自分の目"));
            foreach (var f in Units.Where(f => f.House == me.House && !f.Player && InSight(f, e)))
                if (Second % 300 == 0) Reports.Add(new ProtoReport(e.Id, e.X, e.Y, Second, Second + Dist(f, me) / 200, f.Name));
        }
        foreach (var r in Reports.Where(r => r.ArriveAt <= Second).ToArray()) { Mark(new ProtoMarker(r.Unit, r.X, r.Y, r.SeenAt, r.By + "の知らせ")); Reports.Remove(r); }
    }
    void Mark(ProtoMarker m)
    {
        var old = Markers.FirstOrDefault(x => x.Unit == m.Unit);
        if (old != null && old.SeenAt >= m.SeenAt) return;
        if (old != null) Markers.Remove(old);
        Markers.Add(m);
    }
    public bool SeesEnemy(ProtoPerson e) => !Hostile(Player.House, e.House) || InSight(Player, e);

    public bool CanScout => War.Phase is "行軍" or "合戦" && Player.Soldiers >= 5 && Scouts.All(s => s.Phase == "済");
    string SendScout(string positionId)
    {
        if (!CanScout) return "今は物見を出せない";
        var pos = PositionOf(positionId); var to = ProtoMove.Nearest(Map, pos.X * 100L, pos.Y * 100L);
        var path = ProtoMove.Plan(Map, (Player.X, Player.Y), to); if (path == null) return "そこへは行けない";
        Scouts.Add(new ProtoScout { Id = "scout:" + ++scoutSeq, Target = positionId, X = Player.X, Y = Player.Y, Path = path.Select(p => new[] { p.X, p.Y }).ToArray() });
        return $"{pos.Name}へ物見を出した。";
    }
    void MoveScouts()
    {
        foreach (var s in Scouts.Where(s => s.Phase != "済"))
        {
            long x = s.X, y = s.Y; int i = s.PathIndex; var path = s.Path.Select(p => (p[0], p[1])).ToList();
            bool arrived = ProtoMove.Step(Map, ref x, ref y, path, ref i, 130); s.X = x; s.Y = y; s.PathIndex = i;
            if (Second % 60 == 0)
                foreach (var e in Units.Where(e => Hostile(Player.House, e.House)))
                    if (Math.Sqrt((double)(e.X - s.X) * (e.X - s.X) + (double)(e.Y - s.Y) * (e.Y - s.Y)) <= 600_00) s.Seen.Add(new ProtoMarker(e.Id, e.X, e.Y, Second, "物見"));
            if (!arrived) continue;
            if (s.Phase == "行き")
            {
                s.Phase = "帰り"; var back = ProtoMove.Plan(Map, (s.X, s.Y), (Player.X, Player.Y));
                s.Path = back?.Select(p => new[] { p.X, p.Y }).ToArray() ?? []; s.PathIndex = 0;
            }
            else if (Math.Sqrt((double)(Player.X - s.X) * (Player.X - s.X) + (double)(Player.Y - s.Y) * (Player.Y - s.Y)) <= 100_00 || s.Path.Length == 0)
            {
                s.Phase = "済"; foreach (var m in s.Seen) Mark(m);
                var names = s.Seen.Select(m => Person(m.Unit).Name).Distinct().ToArray();
                Note(names.Length == 0 ? $"物見が戻った。{PositionOf(s.Target).Name}のあたりに敵は見えなかった。" : $"物見が戻った。{string.Join("・", names)}の兵を見たという。");
            }
            else { var back = ProtoMove.Plan(Map, (s.X, s.Y), (Player.X, Player.Y)); s.Path = back?.Select(p => new[] { p.X, p.Y }).ToArray() ?? []; s.PathIndex = 0; }
        }
    }

    // 書状が届いたときの合戦まわりの続き（ProtoLife.Delivered から呼ぶ）。
    void WarDelivered(ProtoLetter l)
    {
        var to = Person(l.To);
        switch (l.Tag)
        {
            case "death-notice": flags["katsumoto-knows"] = Second; Learn(to, Fact("lord-dead")!); break;
            case "invite": to.Loyalty -= 15; Happen("invite", "沢渡家が岩切勝元に、鷹野から離れるよう誘いをかけた。", to.X, to.Y, to, Person(l.Carrier)); break;
            case "defiance":
            {
                Happen("defiance", "岩切勝元から手切れの書状が届いた。謀叛である。", to.X, to.Y, to, Person(Karou));
                War = new ProtoWarState { Phase = "軍議待ち", CouncilAt = Second + Hour };
                Offer(Heir, "軍議に出る", "council", War.CouncilAt + 30 * 60, War.CouncilAt + 30 * 60, 1, new ProtoStep("attend", "鷹野城の広間で軍議に加わる", "takano-castle"));
                break;
            }
        }
    }
}
