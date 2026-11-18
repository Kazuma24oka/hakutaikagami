namespace HakutaiKagami.Core.Proto;

// 試作版の「生きている世界」（2026-11-18 ユーザー「その3つを入れる形で進めて」）。
// 1. やることが湧き続ける：当主は毎朝7時に務めを出す（見回り・年貢・寄進・立ち会い・賊の討伐）。村や町の人も頼みごとをする。
//    頼みは、頼む人のそばにいるときだけ受けたり断ったりできる。受けなかった務めは家中の誰かが代わりに行く。
// 2. 自分がいなくても世界が動く：国境の境目の争い（書状 → 話し合い → こじれれば杭打ち → 小競り合い）、峠の賊（商人を襲う → 噂 →
//    城主の報告 → 討伐の務め）、三日に一度の市。出来事は人の口で伝わる。
// 3. やったことが返ってくる：勲功で褒美と役目（馬廻になると馬で速く歩ける）。頼んだ人との仲。期限に遅れると叱られる。
public enum TaskState { Offered, Open, Done, Failed, Declined, Expired }

// Do：visit（その拠点か地点へ行く）・meet（会って報告する）・hand（書状を渡す）・take（品を受け取る）・give（品を渡す）・fight（賊と戦う）・parley（談判する）・attend（話し合いに居合わせる）
public sealed record ProtoStep(string Do, string Text, string? Site = null, string? Person = null, long X = 0, long Y = 0, string? Letter = null);

public sealed class ProtoTask
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Giver { get; set; } = "";
    public string Kind { get; set; } = "";
    public List<ProtoStep> Steps { get; set; } = [];
    public int Step { get; set; }
    public TaskState State { get; set; }
    public long OfferUntil { get; set; }
    public long Deadline { get; set; }
    public int Merit { get; set; }
    public int Pay { get; set; }
    public string? Goods { get; set; }
    public ProtoStep? Current => Step < Steps.Count ? Steps[Step] : null;
    public ProtoTask Copy() => new() { Id = Id, Title = Title, Giver = Giver, Kind = Kind, Steps = [.. Steps], Step = Step, State = State, OfferUntil = OfferUntil, Deadline = Deadline, Merit = Merit, Pay = Pay, Goods = Goods };
}

public sealed record ProtoAction(string Id, string Text);

public sealed partial class ProtoWorld
{
    public List<ProtoTask> Tasks { get; } = [];
    readonly Dictionary<string, int> favors = [];
    public int Merit { get; private set; }
    public int Money { get; private set; }
    public int Rank { get; private set; }
    public static readonly string[] RankNames = ["近習", "馬廻", "組頭"];
    public string RankName => RankNames[Rank];
    public int Favor(string id) => favors.TryGetValue(id, out var v) ? v : 0;
    void AddFavor(string id, int d) => favors[id] = Math.Clamp(Favor(id) + d, -100, 100);
    bool Flag(string key) => flags.ContainsKey(key);
    void Set(string key) => flags[key] = Second;

    // 峠の賊の隠れ家と、峠道で待ち伏せる所。見回りで行く峠。
    static readonly (long X, long Y) Camp = (1300_00, 1290_00), Ambush = (2122_00, 1044_00), Pass = (1340_00, 1700_00);
    const string Lord = "takano-yoshimasa", Karou = "kosaka-shigetada", Heir = "takano-masayuki", Katsumoto = "iwakiri-katsumoto", Iguchi = "iguchi-yashichi",
        Nobufusa = "sawatari-nobufusa", Nobunao = "sawatari-nobunao", Ono = "ono-tanomo", Shiota = "shiota-mondo", Tahei = "tahei", Sakuemon = "sakuemon", Echigoya = "echigoya", Jion = "jion";

    void AddBandits()
    {
        for (int i = 1; i <= 3; i++)
            People.Add(new ProtoPerson { Id = "bandit-" + i, Name = "賊", House = "bandit", Role = "賊", Home = "", Active = false, X = Camp.X + i * 900, Y = Camp.Y + i * 500, Doing = "潜んでいる" });
    }

    ProtoTask Offer(string giver, string title, string kind, long until, long deadline, int merit, params ProtoStep[] steps)
    {
        var t = new ProtoTask { Id = "task:" + ++taskSeq, Title = title, Giver = giver, Kind = kind, Steps = [.. steps], State = TaskState.Offered, OfferUntil = until, Deadline = deadline, Merit = merit };
        Tasks.Add(t);
        var g = Person(giver); if (Near(Player, g)) Note($"{g.Name}から頼まれごと：{title}");
        return t;
    }

    void Opening()
    {
        var lord = Person(Lord);
        var letter = Send(lord, Katsumoto, "沢渡家の動きに気をつけ、岩切城の守りを固めよ。", "opening"); letter.Carrier = Player.Id;
        var t = Offer(Lord, "岩切城への使い", "letter", Second, At(0, 18), 3, new ProtoStep("hand", "岩切勝元に書状を渡す", "iwakiri-castle", Katsumoto, Letter: letter.Id));
        t.State = TaskState.Open; Journal.Clear();
        Note($"{lord.Name}から、{Person(Katsumoto).Name}あての書状を預かった。");
        Offer(Echigoya, "東の村へ荷を届ける", "errand", At(0, 18), At(1, 18), 0,
            new ProtoStep("give", "作右衛門に荷を渡す", "east-village", Sakuemon), new ProtoStep("meet", "越後屋から駄賃を受け取る", "town", Echigoya)).Pay = 200;
    }

    // ---- 毎分の世界の動き ----
    void Life()
    {
        int day = DayIndex, hour = HourOfDay; long dayStart = At(day, 0);
        if (Second == dayStart + 60) Morning(day);
        if (Second == At(day, 7) && day >= 1) Duties(day);
        foreach (var t in Tasks)
        {
            if (t.State == TaskState.Offered && Second >= t.OfferUntil) { t.State = TaskState.Expired; Stand(t); }
            else if (t.State == TaskState.Open && Second > t.Deadline) Fail(t);
        }
        Bandits(day, hour);
        Dispute(day);
        Wars();
        Watch();
        Rewards();
        foreach (var p in People.Where(p => !p.Player && p.Active && p.HurtUntil > Second)) p.Doing = "手傷を負って休んでいる";
    }

    // 日の始め：市の日（三日に一度）は庄屋と商人が町に集まる。
    void Morning(int day)
    {
        if (day % 3 == 2)
        {
            foreach (var id in new[] { Tahei, Sakuemon, Echigoya }) Appointments.Add(new(id, "town", At(day, 9), At(day, 13), "市に来ている"));
            Happen($"market:{day}", "今日は市場町に市が立つ。", 2270_00, 2120_00, Person(Echigoya));
        }
        if (day >= 2 && day % 2 == 0 && !Tasks.Any(t => t.Kind == "errand" && t.State is TaskState.Offered or TaskState.Open))
            Offer(Echigoya, "東の村へ荷を届ける", "errand", At(day, 18), At(day + 1, 18), 0,
                new ProtoStep("give", "作右衛門に荷を渡す", "east-village", Sakuemon), new ProtoStep("meet", "越後屋から駄賃を受け取る", "town", Echigoya)).Pay = 200;
    }

    // 朝7時、当主が城にいれば務めを出す。11時までに受けなければ家中の誰かが行く。
    void Duties(int day)
    {
        var lord = Person(HeadOf("takano")); var Lord = lord.Id; if (!lord.Active || SiteOf(lord) != "takano-castle") return;
        long until = At(day, 11), deadline = At(day, 19);
        Offer(Lord, "西の村と峠の見回り", "patrol", until, deadline, 3,
            new ProtoStep("visit", "西の村へ行く", "west-village"), new ProtoStep("visit", "峠へ行く", X: Pass.X, Y: Pass.Y), new ProtoStep("meet", "鷹野義正に報告する", "takano-castle", Lord));
        if (day % 2 == 1)
            Offer(Lord, "西の村の年貢を受け取る", "tax", until, deadline, 3,
                new ProtoStep("take", "太兵衛から年貢を受け取る", "west-village", Tahei), new ProtoStep("give", "小坂重忠に年貢を渡す", "takano-castle", Karou));
        else
            Offer(Lord, "円照寺へ寄進を届ける", "temple", until, deadline, 2,
                new ProtoStep("take", "小坂重忠から寄進の銭を受け取る", "takano-castle", Karou), new ProtoStep("give", "慈恩に寄進の銭を渡す", "temple", Jion));
        if (Flag("talk-set") && !Flag("talk-done") && Knows(lord, "talk-set"))
            Offer(Lord, "市場町の話し合いに立ち会う", "attend", At(day, 9, 30), At(day, 13), 4,
                new ProtoStep("attend", "12時まで市場町の話し合いに居合わせる", "town"), new ProtoStep("meet", "鷹野義正に報告する", "takano-castle", Lord));
        if (Knows(lord, "bandits-reported") && People.Any(p => p.House == "bandit" && p.Active) && !Tasks.Any(t => t.Kind == "bandits" && t.State is TaskState.Offered or TaskState.Open))
            Offer(Lord, "峠道の賊を討つ", "bandits", At(day, 12), At(day + 1, 19), 8,
                new ProtoStep("fight", "峠道の賊と戦う", Person: "bandit-1", X: Ambush.X, Y: Ambush.Y), new ProtoStep("meet", "鷹野義正に報告する", "takano-castle", Lord));
    }

    // 受けなかった務めは家中の者が代わりに行く（姿が見える）。
    void Stand(ProtoTask t)
    {
        switch (t.Kind)
        {
            case "patrol": Appointments.Add(new(Heir, "west-village", Second, Second + 2 * Hour, "見回りに来ている")); break;
            case "tax": Appointments.Add(new(Iguchi, "west-village", Second, Second + 2 * Hour, "年貢を受け取りに来ている")); break;
            case "dispute-reply": { var l = Letters.First(x => x.Tag == "dispute-reply"); l.Carrier = Karou; break; }
            case "death-notice": { var l = Letters.First(x => x.Tag == "death-notice"); l.Carrier = Karou; break; }
        }
    }

    void Fail(ProtoTask t)
    {
        t.State = TaskState.Failed; Merit = Math.Max(0, Merit - 2); AddFavor(t.Giver, -15);
        Note($"「{t.Title}」の期限に間に合わなかった。{Person(t.Giver).Name}の機嫌を損ねた。");
    }

    void Complete(ProtoTask t)
    {
        t.State = TaskState.Done; Merit += t.Merit; Money += t.Pay; AddFavor(t.Giver, 10);
        Note($"「{t.Title}」を果たした。" + (t.Merit > 0 ? $"勲功{t.Merit}。" : "") + (t.Pay > 0 ? $"銭{t.Pay}文。" : ""));
    }

    void Next(ProtoTask t)
    {
        t.Step++;
        if (t.Step >= t.Steps.Count) Complete(t);
    }

    // 勲功がたまると、当主のそばに行ったときに褒美と役目をもらう。
    void Rewards()
    {
        var lord = Person(HeadOf("takano")); var Lord = lord.Id; if (!Near(Player, lord)) return;
        if (Rank == 0 && Merit >= 8) { Rank = 1; Money += 500; Note($"{lord.Name}から、褒美に銭500文と馬を賜り、馬廻に取り立てられた。"); }
        else if (Rank == 1 && Merit >= 20) { Rank = 2; Money += 1000; Note($"{lord.Name}から、褒美に銭1000文を賜り、組頭に取り立てられた。"); }
        if (favors.TryGetValue(Lord, out var f) && f <= -15 && !Flag("scolded:" + DayIndex)) { Set("scolded:" + DayIndex); Note($"{lord.Name}「務めを怠るでない。」"); }
    }

    // ---- 峠の賊 ----
    void Bandits(int day, int hour)
    {
        var bandits = People.Where(p => p.House == "bandit").ToArray();
        if (day >= 1 && !Flag("bandits-came")) { Set("bandits-came"); foreach (var b in bandits) b.Active = true; }
        foreach (var b in bandits.Where(b => b.Active))
            foreach (var v in People.Where(v => v.Active && !v.Player && !v.Samurai && v.House != "bandit" && SiteOf(v) == null && Dist(v, b) <= 60_00))
            {
                if (Flag($"robbed:{v.Id}:{day}")) continue; Set($"robbed:{v.Id}:{day}");
                Happen($"robbery:{v.Id}:{day}", $"{v.Name}が峠道で賊に荷を奪われた。", v.X, v.Y, [v, .. bandits]);
                v.Doing = "荷を奪われた";
            }
        // 岩切城主は、賊の話を聞いたら殿へ知らせを送る。
        var katsumoto = Person(Katsumoto);
        if (!Flag("bandits-letter") && Facts.Any(f => f.Id.StartsWith("robbery:") && Knows(katsumoto, f.Id)))
        {
            Set("bandits-letter");
            var l = Send(katsumoto, Lord, "峠道に賊が出て、商人が荷を奪われております。討手を出していただきたい。", "bandits"); l.Carrier = Iguchi;
        }
        _ = hour;
    }

    void Bandit(ProtoPerson b)
    {
        if (!b.Active) return;
        int k = b.Id[^1] - '0'; bool day = HourOfDay is >= 8 and < 17;
        var (x, y) = day ? Ambush : Camp; var spot = ProtoMove.Nearest(Map, x + k * 1500 - 3000, y + (k % 2) * 1800 - 900);
        if (Dist(b, spot.X, spot.Y) < 500) { b.Doing = day ? "峠道で待ち伏せている" : "隠れ家に潜んでいる"; return; }
        Walk(b, spot, "", day ? "峠道へ向かう" : "隠れ家へ戻る");
    }

    // ---- 国境の境目の争い ----
    void Dispute(int day)
    {
        if (day == 0 && HourOfDay == 9 && !Flag("dispute-sent"))
        {
            Set("dispute-sent");
            var l = Send(Person(Nobufusa), Lord, "国境の村の境目について、話し合いの場を設けたい。", "dispute"); l.Carrier = Shiota;
        }
        if (Flag("talk-set") && !Flag("talk-done") && Second >= flags["talk-at"] + 2 * Hour)
        {
            Set("talk-done"); var town = Map.Site("town"); bool present = SiteOf(Player) == "town";
            var who = new[] { Person(Karou), Person(Ono) };
            if (present)
            {
                Happen("agreement", "市場町の話し合いで、境目は今のままとすることで話がついた。", town.X * 100L, town.Y * 100L, [.. who, Player]);
                foreach (var t in Tasks.Where(t => t.Kind == "attend" && t.State == TaskState.Open && t.Current?.Do == "attend")) Next(t);
            }
            else
            {
                Happen("talk-failed", "市場町の話し合いはこじれた。沢渡家は境目を譲らないと言っている。", town.X * 100L, town.Y * 100L, who);
                long d = day + 1;
                Appointments.Add(new(Nobunao, "west-village", d * Day + 8 * Hour, d * Day + 15 * Hour, "境目に杭を打っている"));
                Appointments.Add(new(Shiota, "west-village", d * Day + 8 * Hour, d * Day + 15 * Hour, "境目に杭を打っている"));
                flags["stakes-day"] = d;
            }
        }
        if (flags.TryGetValue("stakes-day", out var sd) && day == sd && !Flag("stakes"))
        {
            var nobunao = Person(Nobunao);
            if (SiteOf(nobunao) == "west-village")
            {
                Set("stakes"); var v = Map.Site("west-village");
                Happen("stakes", "沢渡の者が西の村の境目に杭を打っている。", v.X * 100L, v.Y * 100L, nobunao, Person(Tahei));
                Offer(Tahei, "沢渡の者を西の村から追い返す", "stakes", At(day, 15), At(day, 15), 5,
                    new ProtoStep("parley", "沢渡信直と談判する", "west-village", Nobunao), new ProtoStep("meet", "太兵衛に知らせる", "west-village", Tahei));
            }
        }
        // 談判がなければ、翌日に鷹野家が人を出して小競り合いになる。
        if (Flag("stakes") && !Flag("stakes-removed") && !Flag("skirmish-set") && Second >= At((int)flags["stakes-day"], 15))
        {
            Set("skirmish-set"); long d = flags["stakes-day"] + 1;
            foreach (var id in new[] { Heir, Iguchi }.Where(id => Person(id).House == "takano")) Appointments.Add(new(id, "west-village", d * Day + 9 * Hour, d * Day + 12 * Hour, "沢渡の者と睨み合っている"));
            foreach (var id in new[] { Nobunao, Shiota }) Appointments.Add(new(id, "west-village", d * Day + 9 * Hour, d * Day + 12 * Hour, "境目に杭を打っている"));
            flags["skirmish-at"] = d * Day + 10 * Hour + 30 * 60;
        }
        if (flags.TryGetValue("skirmish-at", out var sa) && Second >= sa && !Flag("skirmish"))
        {
            Set("skirmish"); var heir = Person(Heir); var v = Map.Site("west-village");
            if (SiteOf(heir) != "west-village") return; // 合戦などで来られなかったら小競り合いは起きない
            heir.HurtUntil = Second + 2 * Day;
            Happen("skirmish", "西の村で鷹野家と沢渡家の小競り合いがあり、鷹野正之が手傷を負った。", v.X * 100L, v.Y * 100L, heir, Person(Iguchi), Person(Nobunao), Person(Shiota));
        }
    }

    // 書状が届いたときに起きること。
    void Delivered(ProtoLetter l)
    {
        var to = Person(l.To); var from = Person(l.From);
        switch (l.Tag)
        {
            case "dispute":
            {
                Happen("dispute", "沢渡家が、国境の村の境目について話し合いを求めてきた。", to.X, to.Y, to, Person(l.Carrier));
                var reply = Send(to, Nobufusa, "明日の10時、市場町にて話し合おう。", "dispute-reply");
                var t = Offer(Lord, "沢渡城へ返書を届ける", "dispute-reply", Second + 3 * Hour, At(DayIndex, 20), 3,
                    new ProtoStep("hand", "沢渡信房に返書を渡す", "sawatari-castle", Nobufusa, Letter: reply.Id), new ProtoStep("meet", "鷹野義正に報告する", "takano-castle", Lord));
                _ = t; break;
            }
            case "dispute-reply":
            {
                long talk = At(DayIndex + 1, 10); flags["talk-at"] = talk; Set("talk-set");
                Happen("talk-set", $"明日の10時から市場町で、境目の話し合いをする。", to.X, to.Y, to, Person(l.Carrier));
                Appointments.Add(new(Karou, "town", talk - 30 * 60, talk + 2 * Hour + 30 * 60, "話し合いに来ている"));
                Appointments.Add(new(Ono, "town", talk - 30 * 60, talk + 2 * Hour + 30 * 60, "話し合いに来ている"));
                // 小坂が運んだなら、戻って殿に伝える。本人が運んだなら、報告で伝わる。
                break;
            }
            case "bandits":
                Happen("bandits-reported", "岩切城から、峠道に賊が出て商人が荷を奪われたと知らせが来た。", to.X, to.Y, to, Person(l.Carrier));
                break;
            case "opening":
            {
                var reply = Send(to, Lord, "守りを固めます。北の峠のあたりで怪しい者を見たと村の者が申しております。", "opening-reply"); reply.Carrier = Player.Id;
                var t = Tasks.First(x => x.Kind == "letter");
                t.Steps.Add(new ProtoStep("hand", "鷹野義正に返書を渡す", "takano-castle", Lord, Letter: reply.Id));
                Note($"{to.Name}から、{Person(Lord).Name}あての返書を預かった。");
                break;
            }
            default: WarDelivered(l); break;
        }
        _ = from;
    }

    // ---- 操作する人ができること ----
    public IEnumerable<ProtoTask> OpenTasks => Tasks.Where(t => t.State == TaskState.Open);
    public IEnumerable<ProtoTask> OffersHere => Tasks.Where(t => t.State == TaskState.Offered && Near(Player, Person(t.Giver)));

    public IEnumerable<ProtoAction> Actions()
    {
        if (CouncilOpen) yield return new("council:open", "軍議に加わる");
        if (CanPersuade) yield return new("persuade:katsumoto", "説き伏せる：岩切勝元");
        if (CanScout) foreach (var pos in Map.Data.Positions!.Where(p => p.Id != "takano-castle")) yield return new("scout:" + pos.Id, "物見を出す：" + pos.Name);
        foreach (var t in OffersHere) { yield return new("accept:" + t.Id, "受ける：" + t.Title); yield return new("decline:" + t.Id, "断る：" + t.Title); }
        foreach (var t in OpenTasks)
        {
            var s = t.Current; if (s == null) continue;
            if (s.Do == "fight") { if (People.Any(b => b.House == "bandit" && b.Active && Dist(Player, b) <= 120_00)) yield return new("step:" + t.Id, "賊と戦う"); continue; }
            if (s.Person == null || s.Do is "visit" or "attend") continue;
            var p = Person(s.Person); if (!Near(Player, p)) continue;
            yield return new("step:" + t.Id, s.Text);
        }
    }

    public string Do(string action)
    {
        var (kind, id) = (action[..action.IndexOf(':')], action[(action.IndexOf(':') + 1)..]);
        if (kind == "talk") return Talk(id);
        if (kind == "persuade") return Persuade();
        if (kind == "scout") return SendScout(id);
        if (kind == "council") return "";
        var t = Tasks.First(x => x.Id == id); var giver = Person(t.Giver);
        switch (kind)
        {
            case "accept":
                if (t.State != TaskState.Offered || !Near(Player, giver)) return "今は受けられない";
                t.State = TaskState.Open;
                if (t.Kind == "dispute-reply") Letters.First(l => l.Id == t.Steps[0].Letter).Carrier = Player.Id;
                if (t.Kind == "errand") t.Goods = "荷";
                Note($"{giver.Name}の頼み「{t.Title}」を引き受けた。");
                return $"{giver.Name}「頼んだぞ。」";
            case "decline":
                if (t.State != TaskState.Offered) return "";
                t.State = TaskState.Declined; AddFavor(t.Giver, -3); Stand(t);
                return $"{giver.Name}「そうか。」";
            case "step": return StepAction(t);
        }
        return "";
    }

    string StepAction(ProtoTask t)
    {
        var s = t.Current; if (t.State != TaskState.Open || s == null) return "";
        if (s.Do == "fight") return Fight(t);
        var p = Person(s.Person!); if (!Near(Player, p)) return "そばにいない";
        string reply;
        switch (s.Do)
        {
            case "hand":
            {
                var l = Letters.First(x => x.Id == s.Letter); l.State = LetterState.Delivered; l.DeliveredAt = Second;
                reply = $"{p.Name}「確かに受け取った。」"; Note($"{p.Name}に書状を渡した。");
                Delivered(l); Next(t); return reply;
            }
            case "take": t.Goods = s.Text.Contains("年貢") ? "年貢" : s.Text.Contains("寄進") ? "寄進の銭" : "荷"; reply = $"{p.Name}「これを頼む。」"; Note($"{p.Name}から{t.Goods}を受け取った。"); break;
            case "give": reply = $"{p.Name}「確かに。」"; Note($"{p.Name}に{t.Goods}を渡した。"); t.Goods = null; AddFavor(p.Id, 5); break;
            case "parley": reply = Parley(t, p); if (reply.StartsWith('!')) return reply[1..]; break;
            default:
                // 報告：自分の知っていることが相手に伝わる。
                foreach (var f in Facts.Where(f => Knows(Player, f.Id)).ToArray()) Learn(p, f);
                reply = p.Role == "商人" ? $"{p.Name}「助かりました。駄賃です。」" : p.Role == "庄屋" ? $"{p.Name}「ありがとうございます。」" : $"{p.Name}「ご苦労であった。」"; break;
        }
        Next(t); return reply;
    }

    string Fight(ProtoTask t)
    {
        var bandits = People.Where(b => b.House == "bandit" && b.Active).ToArray();
        if (!bandits.Any(b => Dist(Player, b) <= 120_00)) return "賊は見えない";
        int power = Math.Min(85, 45 + Merit * 2 + Rank * 10 - (Second < Player.HurtUntil ? 25 : 0));
        if (Roll("fight") % 100 < power)
        {
            foreach (var b in bandits) { b.Active = false; b.Path = []; b.PathIndex = 0; }
            Happen("bandits-gone", $"{Player.Name}が峠道の賊を追い払った。", Player.X, Player.Y, Player);
            Next(t); return "賊は散り散りに逃げていった。";
        }
        Player.HurtUntil = Second + Day;
        Happen("player-hurt:" + DayIndex, $"{Player.Name}が峠道の賊に手傷を負わされた。", Player.X, Player.Y, Player);
        return "賊に押し返され、手傷を負った。";
    }

    string Parley(ProtoTask t, ProtoPerson nobunao)
    {
        bool ok = Rank >= 1 || Merit >= 6 || Roll("parley") % 100 < 40;
        if (!ok)
        {
            Happen("parley-failed", $"{Player.Name}が沢渡信直と談判したが、聞き入れられなかった。", Player.X, Player.Y, Player, nobunao);
            t.State = TaskState.Failed; AddFavor(t.Giver, -5); return "!沢渡信直「鷹野の若造が何を言う。」";
        }
        Set("stakes-removed");
        Appointments.RemoveAll(a => a.Site == "west-village" && (a.Person == Nobunao || a.Person == Shiota) && a.Until > Second);
        Happen("stakes-removed", $"{Player.Name}の談判で、沢渡の者は西の村の杭を抜いて引き上げた。", Player.X, Player.Y, Player, nobunao, Person(Tahei));
        return $"{nobunao.Name}「……今日のところは引こう。」";
    }

    // 見回り（その場所へ行く）と立ち会い（その場所にいる）は、着けば進む。
    void PlayerSteps()
    {
        foreach (var t in Tasks)
        {
            if (t.State != TaskState.Open) continue; var s = t.Current; if (s?.Do != "visit") continue;
            bool there = s.Site != null ? SiteOf(Player) == s.Site : Dist(Player, s.X, s.Y) <= 60_00;
            if (!there) continue;
            if (s.Site == null)
            {
                var seen = People.Where(b => b.House == "bandit" && b.Active && Dist(Player, b) <= SightCm).ToArray();
                if (seen.Length > 0) Happen("bandits-seen:" + DayIndex, "峠のあたりで怪しい者たちを見た。", Player.X, Player.Y, Player);
            }
            Next(t);
        }
    }

    // ---- 任務へ向かう ----
    public (long X, long Y) TaskTarget(ProtoTask t)
    {
        var s = t.Current ?? t.Steps[^1];
        if (s.X != 0 || s.Y != 0) return (s.X, s.Y);
        if (s.Person != null)
        {
            var who = Person(s.Person);
            if (who.Active && (Sees(Player, who) || s.Site != null && SiteOf(who) == s.Site)) return (who.X, who.Y);
        }
        var site = Map.Site(s.Site ?? Person(s.Person!).Home); return (site.X * 100L, site.Y * 100L);
    }
    public string TaskPlace(ProtoTask t) { var s = t.Current ?? t.Steps[^1]; return s.Site != null ? SiteName(s.Site) : s.Do == "fight" ? "峠道" : "峠"; }
    public long? TaskEta(ProtoTask t)
    {
        var p = Player; var path = ProtoMove.Plan(Map, (p.X, p.Y), TaskTarget(t)); return path == null ? null : ProtoMove.Eta(Map, (p.X, p.Y), path, percent: PlayerPercent);
    }
    public bool GoToTask(string taskId) { var t = Tasks.First(x => x.Id == taskId); var (x, y) = TaskTarget(t); return MoveTo(x, y, TaskPlace(t)); }
}
