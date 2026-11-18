namespace HakutaiKagami.Core.Proto;

// 試作版の世界。一つの時計（秒）で全員が1秒ずつ動く。進める速さを変えても同じ入力から同じ結果になる。
// ・書状は人が実際に運び、宛先の人と同じ拠点にいるときだけ渡せる。
// ・操作する人に見えるのは、近く（350m以内）か同じ拠点にいる人だけ。遠くは知らない。
public sealed class ProtoPerson
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string House { get; init; }
    public required string Role { get; init; }
    public required string Home { get; init; }
    public string[] Visits { get; init; } = [];
    public bool Player { get; init; }
    public long X { get; set; }
    public long Y { get; set; }
    public List<(long X, long Y)> Path { get; set; } = [];
    public int PathIndex { get; set; }
    public string? Goal { get; set; }
    public string Doing { get; set; } = "";
    public bool Moving => PathIndex < Path.Count;
}

public enum LetterState { Carried, Delivered }
public sealed record ProtoLetter(string Id, string From, string To, string Body, long SentAt)
{
    public string Carrier { get; set; } = From;
    public LetterState State { get; set; } = LetterState.Carried;
    public long? DeliveredAt { get; set; }
}

public enum TaskState { Open, Done }
public sealed record ProtoTask(string Id, string Title, string Site, string Person, string? Letter)
{
    public TaskState State { get; set; } = TaskState.Open;
}

public sealed record ProtoEvent(long At, string Text);

public sealed class ProtoWorld
{
    public const long SightCm = 350_00, HandCm = 25_00, Day = 86400;
    public ProtoMap Map { get; }
    public long Second { get; private set; }
    public List<ProtoPerson> People { get; } = [];
    public List<ProtoLetter> Letters { get; } = [];
    public List<ProtoTask> Tasks { get; } = [];
    public List<ProtoEvent> Journal { get; } = [];
    public ProtoPerson Player => People.First(p => p.Player);
    int letterSeq, taskSeq;

    public ProtoWorld(ProtoMap map) : this(map, true) { }
    ProtoWorld(ProtoMap map, bool opening)
    {
        Map = map; Second = map.Data.StartHour * 3600L;
        int i = 0;
        foreach (var d in map.Data.People)
        {
            var home = map.Site(d.Home); var spot = Spot(home, i++);
            People.Add(new ProtoPerson { Id = d.Id, Name = d.Name, House = d.House, Role = d.Role, Home = d.Home, Visits = d.Visits ?? [], Player = d.Player, X = spot.X, Y = spot.Y, Doing = "詰めている" });
        }
        if (opening) Opening();
    }

    public ProtoPerson Person(string id) => People.First(p => p.Id == id);
    public int Hour => (int)(Second % Day / 3600);
    public int Minute => (int)(Second % 3600 / 60);
    public int DayIndex => (int)(Second / Day);
    public static string Clock(long s) => $"{s / Day + 1}日目 {s % Day / 3600}時{s % 3600 / 60:00}分";
    public string SiteName(string id) => Map.Site(id).Name;

    // 拠点の中で人が立つ場所（重ならないように散らす）。
    (long X, long Y) Spot(ProtoSite s, int k)
    {
        double angle = k * 2.399963, r = s.RadiusM * 0.55 * Math.Sqrt((k % 7 + 1) / 7.0);
        return ProtoMove.Nearest(Map, (long)Math.Round((s.X + Math.Cos(angle) * r) * 100), (long)Math.Round((s.Y + Math.Sin(angle) * r) * 100));
    }

    void Note(string text) => Journal.Add(new ProtoEvent(Second, text));

    void Opening()
    {
        var lord = Person("takano-yoshimasa"); var player = Player;
        var letter = Send(lord, "iwakiri-katsumoto", "沢渡家の動きに気をつけ、岩切城の守りを固めよ。");
        letter.Carrier = player.Id;
        Tasks.Add(new ProtoTask("task:" + ++taskSeq, "岩切勝元へ書状を届ける", "iwakiri-castle", "iwakiri-katsumoto", letter.Id));
        Note($"{lord.Name}から、{Person("iwakiri-katsumoto").Name}あての書状を預かった。");
    }

    ProtoLetter Send(ProtoPerson from, string to, string body)
    {
        var l = new ProtoLetter("letter:" + ++letterSeq, from.Id, to, body, Second); Letters.Add(l); return l;
    }

    public string? SiteOf(ProtoPerson p) => Map.SiteAt(p.X, p.Y)?.Id;
    static long Dist(ProtoPerson a, ProtoPerson b) => (long)Math.Sqrt((double)(a.X - b.X) * (a.X - b.X) + (double)(a.Y - b.Y) * (a.Y - b.Y));

    public bool Sees(ProtoPerson viewer, ProtoPerson other)
    {
        if (viewer == other) return true;
        var site = SiteOf(viewer); if (site != null && site == SiteOf(other)) return true;
        return Dist(viewer, other) <= SightCm;
    }
    public IEnumerable<ProtoPerson> Visible => People.Where(p => Sees(Player, p));

    // ---- 操作する人の入力 ----
    public bool MoveTo(long x, long y, string? goal = null)
    {
        var p = Player; var path = ProtoMove.Plan(Map, (p.X, p.Y), (x, y)); if (path == null) return false;
        p.Path = path; p.PathIndex = 0; p.Goal = goal; p.Doing = goal != null ? SiteName(goal) + "へ向かう" : "歩いている"; return true;
    }
    public void Stop() { var p = Player; p.Path = []; p.PathIndex = 0; p.Goal = null; p.Doing = "立ち止まっている"; }

    public (long X, long Y) TaskTarget(ProtoTask t)
    {
        var who = Person(t.Person); var s = Map.Site(t.Site);
        return SiteOf(who) == t.Site ? (who.X, who.Y) : ((long)s.X * 100, (long)s.Y * 100);
    }
    // 任務のボタンに出す到着の見込み（今いる所から歩いた場合の秒数）。
    public long? TaskEta(ProtoTask t)
    {
        var p = Player; var path = ProtoMove.Plan(Map, (p.X, p.Y), TaskTarget(t)); return path == null ? null : ProtoMove.Eta(Map, (p.X, p.Y), path);
    }
    public bool GoToTask(string taskId) { var t = Tasks.First(x => x.Id == taskId); var (x, y) = TaskTarget(t); return MoveTo(x, y, t.Site); }

    // 手の届く所にいる人（話す・渡す）。
    public IEnumerable<ProtoPerson> Nearby => People.Where(p => !p.Player && Dist(Player, p) <= HandCm * 4);
    public IEnumerable<ProtoLetter> Deliverable => Letters.Where(l => l.State == LetterState.Carried && l.Carrier == Player.Id && People.Any(p => p.Id == l.To && Dist(Player, p) <= HandCm * 4));

    public string Hand(string letterId)
    {
        var l = Letters.First(x => x.Id == letterId); var to = Person(l.To);
        if (l.State != LetterState.Carried || l.Carrier != Player.Id || Dist(Player, to) > HandCm * 4) return "渡せません";
        l.State = LetterState.Delivered; l.DeliveredAt = Second;
        var task = Tasks.FirstOrDefault(t => t.Letter == l.Id && t.State == TaskState.Open); if (task != null) task.State = TaskState.Done;
        Note($"{to.Name}に書状を渡した。");
        if (to.Id == "iwakiri-katsumoto")
        {
            var reply = Send(to, "takano-yoshimasa", "守りを固めます。北の峠に沢渡家の者を見たと村の者が申しております。"); reply.Carrier = Player.Id;
            Tasks.Add(new ProtoTask("task:" + ++taskSeq, "鷹野義正へ返書を届ける", "takano-castle", "takano-yoshimasa", reply.Id));
            Note($"{to.Name}から、{Person("takano-yoshimasa").Name}あての返書を預かった。");
            return $"{to.Name}「確かに受け取った。返書を殿に届けてくれ。」";
        }
        return $"{to.Name}「確かに受け取った。」";
    }

    public string Talk(string personId)
    {
        var p = Person(personId); var site = SiteOf(p);
        var where = site != null ? SiteName(site) : "道中";
        return $"{p.Name}（{p.Role}）「{where}で{p.Doing}ところだ。」";
    }

    // ---- 1秒進める ----
    public void Step()
    {
        Second++;
        if (Second % 60 == 0) foreach (var p in People) if (!p.Player) Decide(p);
        foreach (var p in People)
        {
            if (!p.Moving) continue;
            long x = p.X, y = p.Y; int i = p.PathIndex;
            bool arrived = ProtoMove.Step(Map, ref x, ref y, p.Path, ref i);
            p.X = x; p.Y = y; p.PathIndex = i;
            if (arrived) Arrive(p);
        }
        if (Second % 60 == 0) Couriers();
    }
    public void Advance(long seconds) { for (long i = 0; i < seconds; i++) Step(); }

    void Arrive(ProtoPerson p)
    {
        p.Path = []; p.PathIndex = 0; var site = SiteOf(p);
        p.Doing = site == null ? "立ち止まっている" : site == p.Home ? "詰めている" : SiteName(site) + "に来ている";
        if (p.Player && site != null) Note($"{SiteName(site)}に着いた。");
        p.Goal = null;
    }

    // NPC の日課。1分ごとに、今いるべき所を決めて歩き出す。
    void Decide(ProtoPerson p)
    {
        if (p.Moving) return;
        if (p.Id == "shiota-mondo" && Courier(p)) return;
        string want = p.Home;
        if (p.Visits.Length > 0)
        {
            int start = 9 + Hash(p.Id, DayIndex) % 3;
            if (Hour >= start && Hour < start + 4) want = p.Visits[DayIndex % p.Visits.Length];
        }
        if (SiteOf(p) == want) { if (Hour is >= 6 and < 20 && Hash(p.Id, (int)(Second / 1800)) % 6 == 0 && Second % 1800 == 0) Wander(p); return; }
        var target = Spot(Map.Site(want), Hash(p.Id, 0) % 12);
        var path = ProtoMove.Plan(Map, (p.X, p.Y), target); if (path == null) return;
        p.Path = path; p.PathIndex = 0; p.Goal = want; p.Doing = SiteName(want) + "へ向かう";
    }
    void Wander(ProtoPerson p)
    {
        var site = SiteOf(p); if (site == null) return;
        var spot = Spot(Map.Site(site), Hash(p.Id, (int)Second) % 12);
        var path = ProtoMove.Plan(Map, (p.X, p.Y), spot); if (path == null) return;
        p.Path = path; p.PathIndex = 0;
    }
    static int Hash(string s, int k) { unchecked { int h = 17 + k * 31; foreach (char c in s) h = h * 31 + c; return Math.Abs(h % 1000); } }

    // 沢渡家の使番：9時に当主の書状を持って鷹野城へ。渡したら返書を待って戻る。
    bool Courier(ProtoPerson p)
    {
        var carried = Letters.FirstOrDefault(l => l.Carrier == p.Id && l.State == LetterState.Carried);
        if (carried == null && DayIndex == 0 && Hour == 9 && !Letters.Any(l => l.From == "sawatari-nobufusa"))
        {
            carried = Send(Person("sawatari-nobufusa"), "takano-yoshimasa", "国境の村の境目について、話し合いの場を設けたい。"); carried.Carrier = p.Id;
        }
        if (carried == null) return false;
        var to = Person(carried.To); var site = SiteOf(to) ?? to.Home;
        if (SiteOf(p) == site) return true;
        var path = ProtoMove.Plan(Map, (p.X, p.Y), Spot(Map.Site(site), 3)); if (path == null) return false;
        p.Path = path; p.PathIndex = 0; p.Goal = site; p.Doing = "書状を運んでいる"; return true;
    }
    void Couriers()
    {
        foreach (var l in Letters.Where(l => l.State == LetterState.Carried).ToArray())
        {
            var carrier = Person(l.Carrier); if (carrier.Player) continue; var to = Person(l.To);
            if (Dist(carrier, to) > HandCm * 4 && (SiteOf(carrier) == null || SiteOf(carrier) != SiteOf(to))) continue;
            l.State = LetterState.Delivered; l.DeliveredAt = Second; carrier.Doing = "書状を渡した";
            if (Sees(Player, carrier)) Note($"{carrier.Name}が{to.Name}に書状を渡すのを見た。");
        }
    }

    // ---- 保存 ----
    // 世界をまるごと書き出す。読み込むと、続けて進めた結果が保存しなかった場合と同じになる。
    public sealed record SavedPerson(string Id, long X, long Y, long[][] Path, int PathIndex, string? Goal, string Doing);
    public sealed record SavedLetter(string Id, string From, string To, string Body, long SentAt, string Carrier, LetterState State, long? DeliveredAt);
    public sealed record SavedTask(string Id, string Title, string Site, string Person, string? Letter, TaskState State);
    public sealed record Saved(int Schema, long Second, int LetterSeq, int TaskSeq, SavedPerson[] People, SavedLetter[] Letters, SavedTask[] Tasks, ProtoEvent[] Journal);

    public Saved Save() => new(1, Second, letterSeq, taskSeq,
        People.Select(p => new SavedPerson(p.Id, p.X, p.Y, p.Path.Select(q => new[] { q.X, q.Y }).ToArray(), p.PathIndex, p.Goal, p.Doing)).ToArray(),
        Letters.Select(l => new SavedLetter(l.Id, l.From, l.To, l.Body, l.SentAt, l.Carrier, l.State, l.DeliveredAt)).ToArray(),
        Tasks.Select(t => new SavedTask(t.Id, t.Title, t.Site, t.Person, t.Letter, t.State)).ToArray(), Journal.ToArray());

    public static ProtoWorld Load(ProtoMap map, Saved s)
    {
        if (s.Schema != 1) throw new InvalidDataException("試作版の保存の版が違います");
        var w = new ProtoWorld(map, false) { Second = s.Second, letterSeq = s.LetterSeq, taskSeq = s.TaskSeq };
        foreach (var d in s.People)
        {
            var p = w.People.FirstOrDefault(x => x.Id == d.Id) ?? throw new InvalidDataException("保存にある人が地図にいません：" + d.Id);
            p.X = d.X; p.Y = d.Y; p.Path = d.Path.Select(q => (q[0], q[1])).ToList(); p.PathIndex = d.PathIndex; p.Goal = d.Goal; p.Doing = d.Doing;
        }
        foreach (var l in s.Letters) w.Letters.Add(new ProtoLetter(l.Id, l.From, l.To, l.Body, l.SentAt) { Carrier = l.Carrier, State = l.State, DeliveredAt = l.DeliveredAt });
        foreach (var t in s.Tasks) w.Tasks.Add(new ProtoTask(t.Id, t.Title, t.Site, t.Person, t.Letter) { State = t.State });
        w.Journal.AddRange(s.Journal);
        return w;
    }
}
