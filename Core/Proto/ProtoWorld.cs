namespace HakutaiKagami.Core.Proto;

// 試作版の世界。一つの時計（秒）で全員が1秒ずつ動く。進める速さを変えても同じ入力から同じ結果になる。
// ・書状や荷は人が実際に運び、相手のそばにいるときだけ渡せる。
// ・出来事は、その場にいた人とそれを見た人だけが知る。知ったことは、同じ拠点にいる人どうしで伝わる（噂）。
// ・操作する人に見えるのは、近く（350m以内）か同じ拠点にいる人だけ。遠くは知らない。
// 務め・頼み・事件・勲功は ProtoLife.cs。
public sealed class ProtoPerson
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string House { get; set; } // 謀叛で変わる
    public required string Role { get; set; } // 家督で変わる
    public required string Home { get; init; }
    public string[] Visits { get; init; } = [];
    public bool Player { get; init; }
    public bool Active { get; set; } = true;
    public long X { get; set; }
    public long Y { get; set; }
    public List<(long X, long Y)> Path { get; set; } = [];
    public int PathIndex { get; set; }
    public string? Goal { get; set; }
    public string Doing { get; set; } = "";
    public long HurtUntil { get; set; }
    // 合戦（ProtoWar.cs）：能力・忠誠・気性・兵・士気・技能
    public int Command { get; init; } = 50;
    public int Valor { get; init; } = 50;
    public int Wits { get; init; } = 50;
    public int Loyalty { get; set; } = 70;
    public string Temper { get; init; } = "臨機";
    public int Soldiers { get; set; }
    public int Morale { get; set; } = 100;
    public string[] Skills { get; init; } = [];   // 技能（いくつでも。兵種の適性・物見上手）
    public string Troop { get; init; } = "足軽";    // 兵種（足軽・騎馬）
    public string Tactic { get; init; } = "";       // 戦法（一つだけ。Core/Proto/ProtoArms.cs）
    public bool Moving => PathIndex < Path.Count;
    public bool Samurai => House is "takano" or "sawatari" or "iwakiri";
}

public enum LetterState { Carried, Delivered }
public sealed record ProtoLetter(string Id, string From, string To, string Body, long SentAt, string Tag = "")
{
    public string Carrier { get; set; } = From;
    public LetterState State { get; set; } = LetterState.Carried;
    public long? DeliveredAt { get; set; }
}

public sealed record ProtoEvent(long At, string Text);
public sealed record ProtoFact(string Id, long At, string Text);
// NPC の予定：その時間はその拠点にいる。
public sealed record ProtoAppointment(string Person, string Site, long From, long Until, string Doing);

public sealed partial class ProtoWorld
{
    public const long SightCm = 350_00, HandCm = 100_00, Day = 86400, Hour = 3600;
    public ProtoMap Map { get; }
    public long Second { get; private set; }
    public List<ProtoPerson> People { get; } = [];
    public List<ProtoLetter> Letters { get; } = [];
    public List<ProtoEvent> Journal { get; } = [];
    public List<ProtoFact> Facts { get; } = [];
    public List<ProtoAppointment> Appointments { get; } = [];
    readonly Dictionary<string, HashSet<string>> known = [];
    readonly Dictionary<string, long> flags = [];
    public ProtoPerson Player => People.First(p => p.Player);
    int letterSeq, taskSeq, factSeq;

    public ProtoWorld(ProtoMap map) : this(map, true) { }
    ProtoWorld(ProtoMap map, bool opening)
    {
        Map = map; Second = map.Data.StartHour * Hour;
        int i = 0;
        foreach (var d in map.Data.People)
        {
            var home = map.Site(d.Home); var spot = Spot(home, i++);
            People.Add(new ProtoPerson { Id = d.Id, Name = d.Name, House = d.House, Role = d.Role, Home = d.Home, Visits = d.Visits ?? [], Player = d.Player, X = spot.X, Y = spot.Y, Doing = "詰めている",
                Command = d.Command, Valor = d.Valor, Wits = d.Wits, Loyalty = d.Loyalty, Temper = d.Temper, Soldiers = d.Soldiers, Skills = d.Skills ?? [], Troop = d.Troop, Tactic = d.Tactic });
        }
        AddBandits();
        if (opening) Opening();
    }

    public ProtoPerson Person(string id) => People.First(p => p.Id == id);
    public int HourOfDay => (int)(Second % Day / Hour);
    public int DayIndex => (int)(Second / Day);
    public static string Clock(long s) => $"{s / Day + 1}日目 {s % Day / Hour}時{s % Hour / 60:00}分";
    public static string HourMinute(long s) => $"{s % Day / Hour}時{s % Hour / 60:00}分";
    public string SiteName(string id) => Map.Site(id).Name;
    long At(int day, int hour, int minute = 0) => day * Day + hour * Hour + minute * 60;

    // 拠点の中で人が立つ場所（重ならないように散らす）。
    (long X, long Y) Spot(ProtoSite s, int k)
    {
        double angle = k * 2.399963, r = s.RadiusM * 0.6 * Math.Sqrt((k % 7 + 1) / 7.0);
        return ProtoMove.Nearest(Map, (long)Math.Round((s.X + Math.Cos(angle) * r) * 100), (long)Math.Round((s.Y + Math.Sin(angle) * r) * 100));
    }

    void Note(string text) => Journal.Add(new ProtoEvent(Second, text));

    ProtoLetter Send(ProtoPerson from, string to, string body, string tag = "")
    {
        var l = new ProtoLetter("letter:" + ++letterSeq, from.Id, to, body, Second, tag); Letters.Add(l); return l;
    }

    public string? SiteOf(ProtoPerson p) => Map.SiteAt(p.X, p.Y)?.Id;
    static long Dist(ProtoPerson a, ProtoPerson b) => (long)Math.Sqrt((double)(a.X - b.X) * (a.X - b.X) + (double)(a.Y - b.Y) * (a.Y - b.Y));
    static long Dist(ProtoPerson a, long x, long y) => (long)Math.Sqrt((double)(a.X - x) * (a.X - x) + (double)(a.Y - y) * (a.Y - y));

    public bool Sees(ProtoPerson viewer, ProtoPerson other)
    {
        if (viewer == other) return true;
        if (!other.Active) return false;
        if (viewer.Player && Hostile(viewer.House, other.House) && other.Soldiers > 0) return InSight(viewer, other) || FightingFriend(other); // 敵の隊は見える範囲だけ。味方とぶつかっている敵は見える
        var site = SiteOf(viewer); if (site != null && site == SiteOf(other)) return true;
        return Dist(viewer, other) <= SightCm;
    }
    // 味方の隊とぶつかっている敵の隊は、本人から遠くても見える（2026-11-21 ユーザー「味方部隊と接敵している敵部隊が見えないのも変」）。
    // 味方の隊はいつも見えているので、その目の前の相手も分かる。城内の隊（城という一つの部隊）は別。
    internal bool FightingFriend(ProtoPerson enemy) => !Sheltered(enemy) && War.Clashes.Any(c => c.A == enemy.Id && !Hostile(Player.House, Person(c.B).House) || c.B == enemy.Id && !Hostile(Player.House, Person(c.A).House));
    public IEnumerable<ProtoPerson> Visible => People.Where(p => Sees(Player, p));
    // 合戦の間は、敵の隊とは話せない。
    public IEnumerable<ProtoPerson> Nearby => People.Where(p => !p.Player && p.Active && Dist(Player, p) <= HandCm && !(War.Phase is "行軍" or "合戦" && Hostile(Player.House, p.House)));
    bool Near(ProtoPerson a, ProtoPerson b) => a.Active && b.Active && (Dist(a, b) <= HandCm || SiteOf(a) is { } s && s == SiteOf(b));

    // ---- 知っていること ----
    public bool Knows(ProtoPerson p, string factId) => known.TryGetValue(p.Id, out var set) && set.Contains(factId);
    void Learn(ProtoPerson p, ProtoFact f, string? from = null)
    {
        if (!known.TryGetValue(p.Id, out var set)) known[p.Id] = set = [];
        if (!set.Add(f.Id)) return;
        if (p.Player) Note(from == null ? f.Text : $"{from}から聞いた：{f.Text}");
    }
    // 出来事：関わった人と、その場所が見える所にいた人が知る。
    ProtoFact Happen(string id, string text, long x, long y, params ProtoPerson[] involved)
    {
        var f = Facts.FirstOrDefault(x => x.Id == id) ?? new ProtoFact(id, Second, text);
        if (!Facts.Contains(f)) Facts.Add(f);
        var site = Map.SiteAt(x, y)?.Id;
        foreach (var p in involved) Learn(p, f);
        foreach (var p in People.Where(p => p.Active && !involved.Contains(p) && (Dist(p, x, y) <= SightCm || site != null && SiteOf(p) == site))) Learn(p, f);
        return f;
    }
    public ProtoFact? Fact(string id) => Facts.FirstOrDefault(f => f.Id == id);
    // 噂：同じ拠点にいる人どうしで、知っていることが伝わる（操作する人へは、話したときだけ）。
    void Spread()
    {
        foreach (var group in People.Where(p => p.Active && !p.Player && SiteOf(p) != null).GroupBy(p => SiteOf(p)!))
        {
            var all = group.SelectMany(p => known.TryGetValue(p.Id, out var s) ? s : []).Distinct().ToArray();
            foreach (var p in group) foreach (var id in all) Learn(p, Facts.First(f => f.Id == id));
        }
    }

    // ---- 操作する人の入力 ----
    public int PlayerPercent => (Second < Player.HurtUntil ? 50 : 100) * (Rank >= 1 ? 150 : 100) / 100;
    public bool MoveTo(long x, long y, string? goal = null)
    {
        var p = Player; if (p.House == "takano") (x, y) = OutsideWalls(x, y, p.X, p.Y); // 層を破るまで、敵の城の内へは入れない
        var path = ProtoMove.Plan(Map, (p.X, p.Y), (x, y)); if (path == null) return false;
        p.Path = path; p.PathIndex = 0; p.Goal = goal; p.Doing = goal != null ? goal + "へ向かう" : "歩いている"; return true;
    }
    public void Stop() { var p = Player; p.Path = []; p.PathIndex = 0; p.Goal = null; p.Doing = "立ち止まっている"; }

    // ---- 1秒進める ----
    public void Step()
    {
        Second++;
        bool minute = Second % 60 == 0;
        if (minute) { Life(); foreach (var p in People) if (!p.Player && p.Active) Decide(p); }
        foreach (var p in People)
        {
            if (!p.Moving || !p.Active || Confused(p.Id)) continue; // 混乱している隊は動けない
            long x = p.X, y = p.Y; int i = p.PathIndex;
            bool arrived = ProtoMove.Step(Map, ref x, ref y, p.Path, ref i, (InBody(p) || InReturn(p) ? MarchPercent : p.Player ? PlayerPercent : 100) * (Retreating(p) ? 75 : 100) / 100 * TroopSpeed(p) / 100);
            p.X = x; p.Y = y; p.PathIndex = i;
            if (arrived) Arrive(p);
        }
        if (minute) { Couriers(); Spread(); }
        PlayerSteps();
    }
    public void Advance(long seconds) { for (long i = 0; i < seconds; i++) Step(); }

    void Arrive(ProtoPerson p)
    {
        p.Path = []; p.PathIndex = 0; var site = SiteOf(p);
        if (!p.Player || p.Goal != null || site != null) p.Doing = site == null ? "立ち止まっている" : site == p.Home ? "詰めている" : SiteName(site) + "に来ている";
        if (p.Player && site != null) Note($"{SiteName(site)}に着いた。");
        p.Goal = null;
    }

    void Walk(ProtoPerson p, (long X, long Y) to, string goal, string doing)
    {
        var path = ProtoMove.Plan(Map, (p.X, p.Y), to); if (path == null) return;
        p.Path = path; p.PathIndex = 0; p.Goal = goal; p.Doing = doing;
    }

    // NPC の日課。1分ごとに、今いるべき所を決めて歩き出す。予定（話し合い・務め・市）は日課より先。
    void Decide(ProtoPerson p)
    {
        if (p.Moving) return;
        if (p.House == "bandit") { Bandit(p); return; }
        // 陣を守る（日課で城へ戻らない）。崩れた隊も、本陣で立て直せるうちは日課に戻らない。
        if (War.Phase is "行軍" or "合戦" && War.Assign.ContainsKey(p.Id) && (!War.Routed.Contains(p.Id) || !War.Recovered.Contains(p.Id) && p.Soldiers >= 10)) return;
        if (Courier(p)) return;
        var appointment = Appointments.FirstOrDefault(a => a.Person == p.Id && a.From <= Second && Second < a.Until);
        string want = p.Home; string doing = "詰めている";
        if (appointment != null) { want = appointment.Site; doing = appointment.Doing; }
        else if (p.Visits.Length > 0)
        {
            int start = 9 + Hash(p.Id, DayIndex) % 3;
            if (HourOfDay >= start && HourOfDay < start + 4) { want = p.Visits[DayIndex % p.Visits.Length]; doing = SiteName(want) + "に来ている"; }
        }
        if (SiteOf(p) == want) { if (appointment != null) p.Doing = doing; else if (HourOfDay is >= 6 and < 20 && Second % 1800 == 0 && Hash(p.Id, (int)(Second / 1800)) % 6 == 0) Wander(p); return; }
        Walk(p, Spot(Map.Site(want), Hash(p.Id, 0) % 12), want, SiteName(want) + "へ向かう");
    }
    void Wander(ProtoPerson p)
    {
        var site = SiteOf(p); if (site == null) return;
        var path = ProtoMove.Plan(Map, (p.X, p.Y), Spot(Map.Site(site), Hash(p.Id, (int)Second) % 12)); if (path == null) return;
        p.Path = path; p.PathIndex = 0;
    }
    static int Hash(string s, int k) { unchecked { int h = 17 + k * 31; foreach (char c in s) h = h * 31 + c; return Math.Abs(h % 1000); } }
    int Roll(string key) => Hash(key, (int)(Second / 60));

    // 書状を持つ NPC は宛先の人のいる拠点へ運ぶ。
    bool Courier(ProtoPerson p)
    {
        var carried = Letters.FirstOrDefault(l => l.Carrier == p.Id && l.From != p.Id && l.State == LetterState.Carried); // 書いた本人の手元にあるうちは、まだ誰にも託していない
        if (carried == null) return false;
        var to = Person(carried.To); var site = SiteOf(to) ?? to.Home;
        if (SiteOf(p) == site) return true;
        Walk(p, Spot(Map.Site(site), 3 + Hash(p.Id, 1) % 5), site, "書状を運んでいる"); return true;
    }
    void Couriers()
    {
        foreach (var l in Letters.Where(l => l.State == LetterState.Carried).ToArray())
        {
            var carrier = Person(l.Carrier); if (carrier.Player || carrier.Id == l.From) continue; var to = Person(l.To);
            if (!Near(carrier, to)) continue;
            l.State = LetterState.Delivered; l.DeliveredAt = Second; carrier.Doing = "書状を渡した";
            if (Sees(Player, carrier)) Note($"{carrier.Name}が{to.Name}に書状を渡すのを見た。");
            Delivered(l);
        }
    }

    // ---- 話す ----
    public string Talk(string personId)
    {
        var p = Person(personId);
        // 相手が知っていて自分が知らないことを一つ聞く（新しいものから）。
        var news = Facts.Where(f => Knows(p, f.Id) && !Knows(Player, f.Id)).OrderByDescending(f => f.At).FirstOrDefault();
        if (news != null) { Learn(Player, news, p.Name); return $"{p.Name}「{news.Text}」"; }
        int favor = Favor(p.Id);
        var where = SiteOf(p) is { } s ? SiteName(s) : "道中";
        string mood = p.House == "bandit" ? "……" : favor >= 20 ? "いつもよう働いてくれる。" : favor <= -10 ? "何の用だ。" : p.House == "sawatari" ? "鷹野の者か。" : "";
        return $"{p.Name}（{p.Role}）「{mood}{where}で{p.Doing}ところだ。」";
    }

    // ---- 保存 ----
    // 世界をまるごと書き出す。読み込むと、続けて進めた結果が保存しなかった場合と同じになる。
    public sealed record SavedPerson(string Id, bool Active, long X, long Y, long[][] Path, int PathIndex, string? Goal, string Doing, long HurtUntil, string House = "", int Soldiers = 0, int Morale = 100, int Loyalty = 70, string Role = "");
    public sealed record Saved(int Schema, long Second, int LetterSeq, int TaskSeq, int FactSeq, SavedPerson[] People, ProtoLetter[] Letters, ProtoTask[] Tasks, ProtoEvent[] Journal,
        ProtoFact[] Facts, ProtoAppointment[] Appointments, Dictionary<string, string[]> Known, Dictionary<string, long> Flags, Dictionary<string, int> Favors, int Merit, int Money, int Rank,
        Dictionary<string, string>? Heads = null, ProtoWarState? War = null, ProtoMarker[]? Markers = null, ProtoReport[]? Reports = null, ProtoScout[]? Scouts = null, int ScoutSeq = 0);

    public Saved Save() => new(3, Second, letterSeq, taskSeq, factSeq,
        People.Select(p => new SavedPerson(p.Id, p.Active, p.X, p.Y, p.Path.Select(q => new[] { q.X, q.Y }).ToArray(), p.PathIndex, p.Goal, p.Doing, p.HurtUntil, p.House, p.Soldiers, p.Morale, p.Loyalty, p.Role)).ToArray(),
        Letters.ToArray(), Tasks.Select(t => t.Copy()).ToArray(), Journal.ToArray(), Facts.ToArray(), Appointments.ToArray(),
        known.ToDictionary(k => k.Key, k => k.Value.OrderBy(x => x, StringComparer.Ordinal).ToArray()), new(flags), new(favors), Merit, Money, Rank,
        new(Heads), System.Text.Json.JsonSerializer.Deserialize<ProtoWarState>(System.Text.Json.JsonSerializer.Serialize(War))!, Markers.ToArray(), Reports.ToArray(),
        null, 0);

    public static ProtoWorld Load(ProtoMap map, Saved s)
    {
        if (s.Schema != 3) throw new InvalidDataException("試作版の保存の版が違います");
        var w = new ProtoWorld(map, false) { Second = s.Second, letterSeq = s.LetterSeq, taskSeq = s.TaskSeq, factSeq = s.FactSeq, Merit = s.Merit, Money = s.Money, Rank = s.Rank };
        foreach (var d in s.People)
        {
            var p = w.People.FirstOrDefault(x => x.Id == d.Id) ?? throw new InvalidDataException("保存にある人が地図にいません：" + d.Id);
            if (d.Role != "") p.Role = d.Role;
            p.House = d.House; p.Soldiers = d.Soldiers; p.Morale = d.Morale; p.Loyalty = d.Loyalty;
            p.Active = d.Active; p.X = d.X; p.Y = d.Y; p.Path = d.Path.Select(q => (q[0], q[1])).ToList(); p.PathIndex = d.PathIndex; p.Goal = d.Goal; p.Doing = d.Doing; p.HurtUntil = d.HurtUntil;
        }
        w.Letters.AddRange(s.Letters); w.Tasks.AddRange(s.Tasks.Select(t => t.Copy())); w.Journal.AddRange(s.Journal); w.Facts.AddRange(s.Facts); w.Appointments.AddRange(s.Appointments);
        foreach (var (k, v) in s.Known) w.known[k] = [.. v];
        foreach (var (k, v) in s.Flags) w.flags[k] = v;
        foreach (var (k, v) in s.Favors) w.favors[k] = v;
        foreach (var (k, v) in s.Heads ?? []) w.Heads[k] = v;
        if (s.War != null) w.War = System.Text.Json.JsonSerializer.Deserialize<ProtoWarState>(System.Text.Json.JsonSerializer.Serialize(s.War))!;
        w.Markers.AddRange(s.Markers ?? []); w.Reports.AddRange(s.Reports ?? []);
        return w;
    }
}
