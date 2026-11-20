namespace HakutaiKagami.Core.Proto;

// 試作版の合戦（2026-11-18 ユーザー「関ケ原の陣取り」「軍議で誰がどこに陣取るか、いつ仕掛けるかを決める」
// 「抜け駆け・配置につかない・相手に先にとられていた」「相手の位置がわからない、諜報次第で見える」）。Docs/CORE_LOOP.md。
// 流れ：当主の死 → 家督 → 訃報（書状） → 城主が知って判断（忠誠しだいで謀叛。プレイヤーは説き伏せられる） → 手切れの書状
//       → 軍議（陣・出立・合図の時刻） → 行軍（遅れ・先取り・抜け駆け） → 合図の狼煙 → 合戦（兵・地形・能力・挟み撃ち・士気・敗走）。
// 見えない敵：敵の隊は、本人の見える範囲（地形と知略で変わる）に入ったときだけ見える。味方が見た敵は、知らせが届くまで遅れて
// 「最後に見た場所と時刻」の印になる。隊はいつも周りを探っていて、知略しだいで森の伏兵にも気づく（Core/Proto/ProtoChaos.cs の AutoScout）。
public sealed record ProtoMarker(string Unit, long X, long Y, long SeenAt, string By);
public sealed record ProtoReport(string Unit, long X, long Y, long SeenAt, long ArriveAt, string By);
// 前の保存に残っている手動の物見（2026-11-20 に自動へ変えた。読むだけで使わない）。
public sealed class ProtoScout
{
    public string Id { get; set; } = "";
    public string Target { get; set; } = "";
    public long X { get; set; }
    public long Y { get; set; }
    public long[][] Path { get; set; } = [];
    public int PathIndex { get; set; }
    public string Phase { get; set; } = "行き"; // 行き・帰り・消えた（敵に見つかった。刻限に「戻らない」と分かる）・済
    public long TX { get; set; }
    public long TY { get; set; }
    public int Men { get; set; }
    public long NoteAt { get; set; }
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
    public Dictionary<string, string> Assign { get; set; } = [];   // 陣を持つ隊（"field"＝戦場に陣、"takano-castle"＝城に残る）
    public Dictionary<string, long[]> Post { get; set; } = [];      // 隊ごとの陣（地図の上の一点、cm。Core/Proto/ProtoField.cs）
    public Dictionary<string, string> Target { get; set; } = []; // 抜け駆け・合図の後に向かう敵
    public List<string> Routed { get; set; } = [];
    public string Result { get; set; } = "";
    // 要所・総士気・押し合い・采配（Core/Proto/ProtoClash.cs）
    public Dictionary<string, int> Spirit { get; set; } = [];
    public Dictionary<string, string> Holder { get; set; } = [];
    public List<ProtoClash> Clashes { get; set; } = [];
    public string Stance { get; set; } = "白兵";
    public int Ki { get; set; } = 50;
    public Dictionary<string, int> Gauge { get; set; } = [];
    public Dictionary<string, long> Until { get; set; } = [];
    public Dictionary<string, long> AtCamp { get; set; } = [];
    public List<string> Recovered { get; set; } = [];
    // 向き・伏せ（Core/Proto/ProtoTactics.cs）
    public Dictionary<string, double[]> Facing { get; set; } = [];
    public Dictionary<string, string> FacingFoe { get; set; } = [];
    public Dictionary<string, long> HiddenAt { get; set; } = [];
    // 伏兵・混乱（Core/Proto/ProtoChaos.cs）
    public Dictionary<string, long> UnseenSince { get; set; } = [];
    public List<string> Ambush { get; set; } = [];
    public Dictionary<string, long> AmbushUntil { get; set; } = [];
    public Dictionary<string, int> Chaos { get; set; } = [];
    public Dictionary<string, long> ChaosUntil { get; set; } = [];
    public Dictionary<string, string> HoldCand { get; set; } = []; // 要所 → 「家|居始めた時刻」
    public List<ProtoHit> Hits { get; set; } = []; // その1分の打撃（地図に出す）
    // 作戦と役目・総勢での行軍（Core/Proto/ProtoOrders.cs）
    public string Plan { get; set; } = "";
    public Dictionary<string, string> Roles { get; set; } = [];
    public bool Split { get; set; }          // 着陣して役目ごとに分かれたか
    public long StageX { get; set; }
    public long StageY { get; set; }
    public bool PlayerFollows { get; set; }  // 本人が「指図どおりに動く」を選んでいる
    public Dictionary<string, int> StartSoldiers { get; set; } = [];
    // 大目標と撤退条件（Core/Proto/ProtoGoals.cs）。鍵は側（takano・iwakiri）。
    public Dictionary<string, string> Goals { get; set; } = [];
    public Dictionary<string, List<string>> RetreatRules { get; set; } = [];
    public Dictionary<string, string> RetreatAdded { get; set; } = [];
    public Dictionary<string, string> RetreatBy { get; set; } = [];   // 満たした条件（退き貝が鳴った）
    public Dictionary<string, long> RetreatAt { get; set; } = [];
    public Dictionary<string, int> StartMen { get; set; } = []; // 合戦に入ったときの兵の数（右下の窓に「今/元」で出す）
}
public sealed record ProtoCouncilView(string Commander, string[] Opinions, (string Id, string Name)[] Positions, string[] Departures, string[] Signals, string[] Plans, string[] Goals, string[] RetreatAdds);

public sealed partial class ProtoWorld
{
    public Dictionary<string, string> Heads { get; } = [];
    public ProtoWarState War { get; private set; } = new();
    public List<ProtoMarker> Markers { get; } = [];
    public List<ProtoReport> Reports { get; } = [];
    const string Rebel = "iwakiri";
    public static readonly string[] Departures = ["今夜のうちに出る", "夜明けに出る"];
    public static readonly string[] Signals = ["卯の刻（6時）に狼煙", "辰の刻（8時）に狼煙"];
    // 作戦（2026-11-19 ユーザー「釣り野伏」）：正面は大将が道の口・家老が南の丘。釣り野伏は家老が森の北の縁に伏せ、大将は南の丘で勝元を引きつける（囮は本人）。
    public static readonly string[] Plans = ["正面から当たる", "釣り野伏", "挟撃"];

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
    // 森の中の隊は、80m（見る側の知略と技能で増減）まで近づかないと見えない（Core/Proto/ProtoChaos.cs）。
    internal bool InSight(ProtoPerson viewer, ProtoPerson target) => Dist(viewer, target) <= (War.Phase is "行軍" or "合戦" && Hostile(viewer.House, target.House) && Hidden(target) ? SpotRange(viewer) : SightOf(viewer));

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
            PlaceRebels();
            // 岩切勢の大目標：鷹野が夜のうちに出ていれば（陣を取られているかもしれない）討って出る。まだ出ていなければ陣を守り抜く。
            War.Goals[Rebel] = flags.TryGetValue("marched", out var went) && went < Second ? Goals[0] : Goals[1];
            SetRetreatRules(Rebel, katsumoto.Temper == "慎重" ? RetreatAdds[1] : null);
            foreach (var id in new[] { Katsumoto, Iguchi }) { var p = Person(id); if (p.House == Rebel) MarchTo(p, PostOf(id)); }
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
            $"{heir.Name}（大将）「夜明けを待たずに、城への道から一気に攻めかかる。岩切の者どもに時を与えるな。」",
            $"{karou.Name}「城の南の丘を先に押さえれば、城から出る敵を見下ろせます。勝元は用心深い男、夜明けには丘へ出てまいりましょう。今夜のうちに丘へ。」",
        };
        if (Knows(Player, "invite")) ops.Add($"{Player.Name}は、沢渡家が勝元を誘っていたことを知っている。");
        return new(heir.Name, [.. ops], Map.Data.Positions!.Select(p => (p.Id, p.Name)).ToArray(), Departures, Signals, Plans, Goals, RetreatAdds);
    }
    // 本人が出ていれば本人の意見（自分の陣・出立・合図）が通る。出ていなければ大将の考えで決まる。
    // 本人が出ていれば本人の意見（作戦・自分の役目・出立・合図）が通る。出ていなければ大将の考えで決まる。
    // role：自分の役目（作戦の役目のどれか）。playerPosition だけを渡すと、その陣の役目になる（前の呼び方）。
    // goal：大目標。retreat：足す撤退条件（RetreatAdds）。出ていなければ大将が決める（慎重な大将は「全軍士気が半分を切る」を足す）。
    // まず総大将が全員の陣と役目を決める（Proposal）。本人が出ていれば再提案できる：自分の陣（posts）と役目はいつも通り、
    // 軍全体に関わる分（大目標・作戦・出立・合図・足す撤退条件）は総大将が聞き入れたときだけ通る。総大将が本人なら全員の陣を決められる。
    // playerPosition：前の呼び方（地図の点の名前で自分の陣を渡す）。
    public string DecideCouncil(string? playerPosition, string? departure, string? signal, string? plan = null, string? role = null, string? goal = null, string? retreat = null, Dictionary<string, long[]>? posts = null)
    {
        if (War.Phase != "軍議待ち" || War.Assign.Count > 0) return "";
        bool present = playerPosition != null || role != null || posts != null; War.PlayerAtCouncil = present;
        var prop = Proposal(); bool refused = false;
        if (present && !CommanderListens)
        {
            refused = (goal ?? prop.Goal) != prop.Goal || (plan ?? prop.Plan) != prop.Plan || (departure ?? prop.Departure) != prop.Departure || (signal ?? prop.Signal) != prop.Signal || (retreat ?? prop.RetreatAdd) != prop.RetreatAdd;
            if (refused) { goal = prop.Goal; plan = prop.Plan; departure = prop.Departure; signal = prop.Signal; retreat = prop.RetreatAdd; if (role != null && !RolesOf(plan).Any(r => r.Role == role)) role = null; }
        }
        if (!present) retreat = prop.RetreatAdd;
        string dep = departure ?? Departures[1], sig = signal ?? Signals[0];
        War.Goals["takano"] = goal ?? Goals[0];
        AssignRoles(plan ?? PlansOf(War.Goals["takano"])[0], role, playerPosition, posts);
        long night = At(DayIndex, 21), dawn = (Second / Day + 1) * Day + 4 * Hour;
        War.DepartAt = dep == Departures[0] ? Math.Max(Second + 10 * 60, Math.Min(night, Second + Hour)) : dawn;
        War.SignalAt = (dawn / Day) * Day + (sig == Signals[0] ? 6 : 8) * Hour;
        War.Phase = "行軍";
        // 兵を預かり、出陣の任務を受ける。軍議の任務は、軍議に加わって済む。
        Player.Soldiers = 30; Player.Morale = 100;
        SetRetreatRules("takano", retreat);
        foreach (var t in Tasks.Where(t => t.Kind == "council" && t.State == TaskState.Open && present)) Next(t);
        var mine = PostOf(Player.Id);
        Note($"{Person(Heir).Name}から兵30を預かった。");
        if (mine.Id != "takano-castle")
        {
            var muster = Offer(Heir, "出陣：" + mine.Name, "muster", Second, War.SignalAt, 2, new ProtoStep("visit", mine.Name + "に陣を取る", X: mine.X * 100L, Y: mine.Y * 100L));
            muster.State = TaskState.Open; muster.Speech = $"そなたは{OrderText(Player.Id)}。{dep}。合図は{sig}じゃ。";
            Note($"{Person(Heir).Name}「{muster.Speech}」");
        }
        var where = string.Join("、", War.Assign.Select(a => $"{Person(a.Key).Name}は{(War.Roles.GetValueOrDefault(a.Key, "") is { Length: > 0 } r ? r + "（" + PostOf(a.Key).Name + "）" : PostOf(a.Key).Name)}"));
        var castle = Map.Site("takano-castle");
        Happen("council", $"軍議で決まった：{War.Goals["takano"]}。{War.Plan}。{where}。{dep}。{sig}。", castle.X * 100L, castle.Y * 100L, Person(Heir), Person(Karou), Player);
        return !present ? "" : refused ? $"{Person(Heir).Name}「作戦はわしが決める。そなたの陣はそれでよい。」" : $"{Person(Heir).Name}「それでいこう。」";
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
        // 退き貝で退いている隊は、作戦にも狙いにも加えない（敵からは見えるので、追われる）。
        var allTakano = takano; takano = takano.Where(u => !Withdrawing(u)).ToArray();
        MarchTogether(takano);
        // 先に取られていた：着く前に敵がその陣にいれば、手前で止まって知らせる。
        foreach (var u in takano.Where(u => Auto(u) && War.Split && u.Moving && !War.Target.ContainsKey(u.Id) && !Flag("luring:" + u.Id)))
        {
            var pos = PostOf(u.Id); long px = pos.X * 100L, py = pos.Y * 100L;
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
            var pos = PostOf(r.Id); long px = pos.X * 100L, py = pos.Y * 100L;
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
        // 大将は自分の決めた合図を破らない。
        foreach (var u in takano.Where(u => !u.Player && u.Id != Heir && War.Split && !War.Signaled && u.Temper == "逸る" && !War.Target.ContainsKey(u.Id)))
        {
            var seen = rebels.Where(r => InSight(u, r)).OrderBy(r => Dist(u, r)).FirstOrDefault();
            if (seen != null && Roll("nukegake:" + u.Id) % 100 < 30)
            {
                War.Target[u.Id] = seen.Id; War.Phase = "合戦";
                Happen("nukegake:" + u.Id, $"{u.Name}が合図を待たずに{seen.Name}の兵へ仕掛けた。", u.X, u.Y, u);
            }
        }
        ActOnRoles(takano, rebels);
        // 謀叛方は、合戦になったら見えている敵に向かう。陣を守り抜くつもりなら、自分の陣の近く（400m）に来た敵だけを相手にし、離れたら陣へ戻る。
        bool rebelsHold = GoalOf(Rebel) == Goals[1];
        foreach (var r in rebels.Where(r => War.Phase == "合戦" && !War.Target.ContainsKey(r.Id) && !Withdrawing(r)))
        {
            var post = War.Post.ContainsKey(r.Id) ? PostOf(r.Id) : null;
            var seen = allTakano.Concat(new[] { Player }).Where(t => t.Soldiers > 0 && InSight(r, t) && (!rebelsHold || post == null || Dist(t, post.X * 100L, post.Y * 100L) <= 400_00)).OrderBy(t => Dist(r, t)).FirstOrDefault();
            if (seen != null) War.Target[r.Id] = seen.Id;
            else if (rebelsHold && post != null && !r.Moving && ClashOf(r.Id) == null && Dist(r, post.X * 100L, post.Y * 100L) > 150_00) MarchTo(r, post);
            // 討って出るつもりで、合図から半刻たっても敵が見えなければ、鷹野の来る道を進んで敵を探す。
            else if (!rebelsHold && War.Signaled && Second >= War.SignalAt + 30 * 60 && !r.Moving && ClashOf(r.Id) == null && r.Id == HeadOf(Rebel))
            { var to = RoadPoint(1150_00); if (Dist(r, to.X, to.Y) > 300_00) Walk(r, to, "鷹野勢の来る道", "敵を探して兵を進めている"); }
        }
        // 向かう：相手が動けば道筋を引き直す（5分ごと）。
        foreach (var (id, targetId) in War.Target.ToArray())
        {
            var u = Person(id); var t = Person(targetId);
            if (War.Routed.Contains(id)) continue;
            if (!t.Active || !KeepsChasing(u, t)) { War.Target.Remove(id); continue; }
            // 相手の手前の間合い（50m）まで進んで止まり、向かい合う（相手の位置に重ならない）。相手が動いたら1分ごとに引き直す。
            // 下がっていく相手には、手前で止まらず追いすがる：相手の今いる所を目指し、20秒ごとに引き直す（途中で立ち止まって待たない。2026-11-19 ユーザーの印）。
            if (Retreating(t)) { if (!u.Moving || Second % 20 == 0) Walk(u, (t.X, t.Y), t.Name, $"{t.Name}の兵を追っている"); }
            else if (Dist(u, t) > Contact + 20_00) { if (!u.Moving || Second % 60 == 0) Walk(u, ContactPoint(u, t), t.Name, $"{t.Name}の兵へ攻めかかっている"); }
            else if (u.Moving && Auto(u)) { u.Path = []; u.PathIndex = 0; }
        }
        if (War.Phase == "合戦") Regroup();
        var field = Units.Where(u => u.House == Rebel || u.House == "takano" && War.Assign.ContainsKey(u.Id)).ToArray();
        // 打ち合える所に敵がいる隊は、その場で止まって戦う（追いかけながら重なっていかない）。本人の歩みは本人が決める。
        if (War.Phase == "合戦") foreach (var u in field.Where(u => Auto(u) && !Flag("luring:" + u.Id) && !Withdrawing(u) && u.Moving && !War.Routed.Contains(u.Id) && field.Any(e => Hostile(u.House, e.House) && !War.Routed.Contains(e.Id) && !Retreating(e) && Dist(u, e) <= Reach)))
            { u.Path = []; u.PathIndex = 0; }
        Spread(field); UpdateFacing(field); AutoScout(field); Stealth(field);
        if (War.Phase == "合戦") { Ambushers(); Fight(field); }
        // 終わり
        var liveRebels = Units.Where(u => u.House == Rebel && StillFights(u)).ToArray();
        // 本人は、戦いに加わっていなければ数えない（本人が後ろにいるだけでは合戦は続かない）。
        var liveTakano = Units.Where(u => u.House == "takano" && War.Assign.ContainsKey(u.Id) && (u.Player ? !War.Routed.Contains(u.Id) && Flag("fought:" + u.Id) : StillFights(u))).ToArray();
        if (Flag("rebels-out") && liveRebels.Length == 0 && War.Result == "")
        {
            War.Result = "鎮めた"; War.Phase = "終わり"; var k = Person(Katsumoto);
            Happen("rebellion-crushed", "岩切勝元の謀叛は鎮まった。勝元と井口は捕らえられた。", k.X, k.Y, Person(Heir), Person(Karou), k);
            foreach (var r in People.Where(r => r.House == Rebel)) { r.Active = false; r.Path = []; r.PathIndex = 0; r.Doing = "捕らえられた"; }
        }
        else if (War.Phase == "合戦" && War.Result == "" && Second >= War.SignalAt + 8 * Hour)
        {
            // 日が傾いても決まらなければ、両方とも兵を引く。
            War.Result = "引き分け"; War.Phase = "終わり"; var h = Person(Heir);
            Happen("draw", "日が傾き、鷹野勢も岩切勢も兵を引いた。", h.X, h.Y, h);
            foreach (var r in People.Where(r => r.House == Rebel && r.Active)) Walk(r, Spot(Map.Site("iwakiri-castle"), 3), "iwakiri-castle", "岩切城へ引き上げている");
        }
        else if (War.Phase == "合戦" && liveTakano.Length == 0 && War.Result == "")
        {
            War.Result = "退けられた"; War.Phase = "終わり"; var h = Person(Heir);
            Happen("expedition-failed", "鷹野の討手は岩切勢に退けられた。", h.X, h.Y, h, Person(Karou));
            foreach (var r in People.Where(r => r.House == Rebel && r.Active)) Walk(r, Spot(Map.Site("iwakiri-castle"), 3), "iwakiri-castle", "岩切城へ引き上げている");
        }
        Retreats();
        if (War.Result != "") { UpdatePlan(); AwardWarMerit(); } // 決着で最後の段と勲功が決まる
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
        var cp = ContactPoint(Player, e); MoveTo(cp.X, cp.Y, e.Name); Player.Doing = e.Name + "の兵へ攻めかかっている";
        return $"{e.Name}の兵へ攻めかかる。";
    }
    string Withdraw()
    {
        War.Target.Remove(Player.Id);
        var pos = PostOf(Player.Id);
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
