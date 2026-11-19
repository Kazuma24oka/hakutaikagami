namespace HakutaiKagami.Core.Proto;

// 試作版の軍議の作戦と役目、総勢での行軍（2026-11-19 ユーザー）。
// ・主君の指示の出陣は、まず城に集まり、総勢が大将に率いられてまとまって戦地（着陣の地）へ進み、着いてから役目ごとに分かれる。
//   出立の遅れは無い。行軍の速さは大将の統率で決まる（60＋統率×0.7 ％）。
// ・作戦を選ぶと役目が出る。正面から当たる＝先手・後詰、釣り野伏＝囮・伏兵・本隊、挟撃＝正面・回り込み。
//   NPC は役目どおりに動く。ただし逸る者が囮になると、半々で退かずに戦い続ける。
// ・本人は「指図どおりに動く」を選ぶと、NPC と同じに役目どおり自動で動く。自分で動けばその間は指図から外れる。
public sealed record ProtoOrder(string Unit, string Name, string Role, string Place, string Text, long X, long Y);

public sealed partial class ProtoWorld
{
    public static (string Role, string Position)[] RolesOf(string plan) => plan switch
    {
        "釣り野伏" => [("囮", "road-mouth"), ("伏兵", "forest-edge"), ("本隊", "south-hill")],
        "挟撃" => [("正面", "road-mouth"), ("回り込み", "forest-edge")],
        _ => [("先手", "road-mouth"), ("後詰", "south-hill")],
    };

    void AssignRoles(string plan, string? playerRole, string? playerPosition)
    {
        War.Plan = plan; var roles = RolesOf(plan);
        // 陣だけを渡されたら、その陣の役目にする。
        playerRole ??= roles.FirstOrDefault(r => r.Position == playerPosition).Role;
        string heir, karou, me;
        switch (plan)
        {
            case "釣り野伏": karou = "伏兵"; (heir, me) = playerRole == "囮" ? ("本隊", "囮") : ("囮", playerRole ?? "伏兵"); break;
            case "挟撃": heir = "正面"; karou = "回り込み"; me = playerRole ?? "回り込み"; break;
            default: heir = "先手"; karou = "後詰"; me = playerRole ?? ""; break;
        }
        string Pos(string role) => roles.First(r => r.Role == role).Position;
        War.Roles[Heir] = heir; War.Assign[Heir] = Pos(heir);
        War.Roles[Karou] = karou; War.Assign[Karou] = Pos(karou);
        War.Roles[Player.Id] = me; War.Assign[Player.Id] = me != "" ? Pos(me) : playerPosition ?? "forest-edge";
        // 着陣の地：城から道の口への道の、道の口の500m手前。
        var castle = Map.Site("takano-castle"); var mouth = PositionOf("road-mouth");
        var path = ProtoMove.Plan(Map, (castle.X * 100L, castle.Y * 100L), ProtoMove.Nearest(Map, mouth.X * 100L, mouth.Y * 100L));
        (War.StageX, War.StageY) = (mouth.X * 100L, mouth.Y * 100L);
        if (path != null)
        {
            double left = 0;
            for (int i = path.Count - 1; i > 0; i--)
            {
                // 道の点は間が長いことがあるので、区間の途中のちょうど500m手前の所を取る。
                double seg = Math.Sqrt(Math.Pow(path[i].X - path[i - 1].X, 2) + Math.Pow(path[i].Y - path[i - 1].Y, 2));
                if (left + seg >= 500_00 && seg > 0)
                {
                    double t = (500_00 - left) / seg;
                    (War.StageX, War.StageY) = ProtoMove.Nearest(Map, path[i].X + (long)((path[i - 1].X - path[i].X) * t), path[i].Y + (long)((path[i - 1].Y - path[i].Y) * t)); break;
                }
                left += seg;
            }
        }
    }

    // NPC と同じに自動で動くか（本人は「指図どおりに動く」のときだけ）。
    internal bool Auto(ProtoPerson u) => !u.Player || War.PlayerFollows;
    // 総勢の中にいるか（出立してから着陣まで）。
    public bool InBody(ProtoPerson u) => War.Phase is "行軍" or "合戦" && !War.Split && Flag("marched") && u.House == "takano" && u.Soldiers > 0 && War.Assign.TryGetValue(u.Id, out var a) && a != "takano-castle" && Auto(u) && !War.Routed.Contains(u.Id);
    public int MarchPercent => 60 + Person(Heir).Command * 7 / 10;
    public IEnumerable<ProtoPerson> Body => Units.Where(InBody);

    // 総勢での行軍：刻限に大将に率いられて着陣の地へ。大将が着いたら役目ごとに分かれる。
    void MarchTogether(ProtoPerson[] takano)
    {
        if (War.Split) return;
        var heir = Person(Heir);
        // 出立の前に城へ集まる。刻限に皆がそろっていれば出る（そろわなくても、刻限から2時間で出る）。
        var castle = Map.Site("takano-castle"); long cx = castle.X * 100L, cy = castle.Y * 100L;
        var called = takano.Where(u => Auto(u) && War.Assign[u.Id] != "takano-castle").ToArray();
        if (!Flag("marched"))
            foreach (var u in called.Where(u => !u.Moving && Dist(u, cx, cy) > 300_00)) Walk(u, Spot(castle, 2), castle.Id, "出陣のため鷹野城へ集まっている");
        if (!Flag("marched") && Second >= War.DepartAt && (called.All(u => Dist(u, cx, cy) <= 300_00) || Second >= War.DepartAt + 2 * Hour))
        {
            Set("marched");
            foreach (var u in Body) Walk(u, (War.StageX, War.StageY), "着陣の地", $"{heir.Name}勢として兵を進めている");
            Happen("marched", $"{heir.Name}勢が鷹野城を出た。", heir.X, heir.Y, heir, Person(Karou), Player);
            if (!War.PlayerFollows) Note($"出立の刻限になった。{PositionOf(War.Assign[Player.Id]).Name}へ向かう。");
        }
        if (!Flag("marched")) return;
        // 遅れた者は大将を追う。
        foreach (var u in Body.Where(u => u.Id != Heir && !u.Moving && Dist(u, War.StageX, War.StageY) > 150_00)) Walk(u, (War.StageX, War.StageY), "着陣の地", $"{heir.Name}勢を追っている");
        bool arrived = Dist(heir, War.StageX, War.StageY) <= 100_00 || War.Phase == "合戦";
        if (!arrived && !heir.Moving) Walk(heir, (War.StageX, War.StageY), "着陣の地", $"{heir.Name}勢として兵を進めている");
        if (!arrived) return;
        var body = takano.Where(u => Auto(u) && War.Assign[u.Id] != "takano-castle").ToArray();
        War.Split = true;
        Happen("split", $"{heir.Name}勢は着陣し、役目ごとに分かれた。", heir.X, heir.Y, heir, Person(Karou), Player);
        foreach (var u in body) { var pos = PositionOf(War.Assign[u.Id]); MarchTo(u, pos); }
    }

    // 合図のあとの動き（役目ごと）。
    void ActOnRoles(ProtoPerson[] takano, ProtoPerson[] rebels)
    {
        if (!War.Signaled) return;
        foreach (var u in takano.Where(u => Auto(u) && flags.TryGetValue("attack-at:" + u.Id, out var at) && Second >= at))
        {
            string role = War.Roles.GetValueOrDefault(u.Id, ""); var pos = PositionOf(War.Assign[u.Id]);
            if (role == "囮") { Bait(u, rebels); continue; }
            if (War.Target.ContainsKey(u.Id)) continue;
            // 伏兵（伏兵の陣）：合図でも出ずに伏せて待つ（ProtoTactics.cs の Ambushers が、目の前に来た敵へ仕掛ける）。
            if (role == "伏兵" || pos.Kind == "伏兵" && role != "回り込み") continue;
            // 丘の陣は、鷹野が持っている間は守る（上ってくる敵を高い所から打つ）。
            if (pos.Id == "south-hill" && War.Holder.GetValueOrDefault("south-hill") != Rebel) continue;
            // 回り込み：正面がぶつかるのを待ってから、ぶつかっている敵の横へ出る。
            if (role == "回り込み")
            {
                var engaged = War.Clashes.Select(c => Person(c.B)).Where(b => !War.Routed.Contains(b.Id)).OrderBy(b => Dist(u, b)).FirstOrDefault();
                if (engaged != null) { War.Target[u.Id] = engaged.Id; Happen("flanker:" + u.Id, $"{u.Name}の兵が{engaged.Name}の横へ回り込みにかかった。", u.X, u.Y, u); }
                continue;
            }
            var target = rebels.OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault(); if (target != null) War.Target[u.Id] = target.Id;
        }
    }

    // 囮：敵に当たり、1分打ち合ったら伏兵の前へ退く。伏兵が仕掛けたら（または退ききったら）向き直って挟む。
    void Bait(ProtoPerson u, ProtoPerson[] rebels)
    {
        if (Flag("bait-done:" + u.Id) || War.Routed.Contains(u.Id)) return;
        var ambusher = War.Roles.Where(r => r.Value == "伏兵").Select(r => Person(r.Key)).FirstOrDefault(k => k.Active && k.Soldiers > 0);
        if (Flag("luring:" + u.Id))
        {
            bool sprung = ambusher != null && Facts.Any(f => f.Id.StartsWith("surprise:" + ambusher.Id));
            if (!sprung && u.Moving) return;
            flags.Remove("luring:" + u.Id); Set("bait-done:" + u.Id);
            var back = rebels.Where(r => Dist(u, r) <= 500_00).OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault();
            if (back != null) { War.Target[u.Id] = back.Id; Happen("bait-turn:" + u.Id, $"{u.Name}の兵が向き直り、{back.Name}の兵へ攻めかかった。", u.X, u.Y, u); }
            return;
        }
        if (!War.Target.ContainsKey(u.Id))
        {
            var target = rebels.OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault(); if (target != null) War.Target[u.Id] = target.Id;
            return;
        }
        if (ClashOf(u.Id) == null) { flags.Remove("bait-clash:" + u.Id); return; }
        if (!flags.TryGetValue("bait-clash:" + u.Id, out var since)) { flags["bait-clash:" + u.Id] = Second; return; }
        if (Second - since < 60) return;
        if (ambusher == null) { Set("bait-done:" + u.Id); return; }
        // 逸る者は、半々で退かずに戦い続ける。
        if (u.Temper == "逸る" && Roll("bait:" + u.Id) % 100 < 50)
        {
            Set("bait-done:" + u.Id);
            Happen("bait-stays:" + u.Id, $"囮の{u.Name}は退かず、そのまま戦い続けた。", u.X, u.Y, u);
            return;
        }
        War.Target.Remove(u.Id); Set("luring:" + u.Id);
        var lure = LurePoint(u, ambusher);
        Walk(u, lure, PositionOf(War.Assign[ambusher.Id]).Name, "敵を誘って退いている");
        Happen("bait-back:" + u.Id, $"囮の{u.Name}の兵が、{ambusher.Name}の伏せる方へ退き始めた。", u.X, u.Y, u);
    }
    // 囮が退く先：伏兵のすぐ横（60m）を通り過ぎて、その先350m。
    (long X, long Y) LurePoint(ProtoPerson bait, ProtoPerson ambusher)
    {
        double dx = ambusher.X - bait.X, dy = ambusher.Y - bait.Y, len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy)); dx /= len; dy /= len;
        var p = (ambusher.X + (long)(dx * 350_00 + dy * 60_00), ambusher.Y + (long)(dy * 350_00 + dx * 60_00));
        return ProtoMove.Nearest(Map, p.Item1, p.Item2);
    }

    // 「指図どおりに動く」。
    string FollowOrders(bool on)
    {
        if (!War.Assign.TryGetValue(Player.Id, out var a)) return "";
        War.PlayerFollows = on; if (!on) return "自分の判断で動く。";
        War.Target.Remove(Player.Id);
        if (War.Split) MarchTo(Player, PositionOf(a));
        else if (Flag("marched")) Walk(Player, (War.StageX, War.StageY), "着陣の地", $"{Person(Heir).Name}勢として兵を進めている");
        return $"指図どおりに動く：{OrderText(Player.Id)}。";
    }
    // 本人が自分で動いたら、指図から外れる（画面から呼ぶ）。
    public void ActOnOwn() => War.PlayerFollows = false;

    // 軍令の一覧（「任務」の窓の軍令のタブ）。
    public string OrderText(string id)
    {
        if (!War.Assign.TryGetValue(id, out var a)) return "";
        var place = PositionOf(a).Name; var ambush = War.Roles.FirstOrDefault(r => r.Value == "伏兵");
        return War.Roles.GetValueOrDefault(id, "") switch
        {
            "囮" => ambush.Key != null ? $"敵に当たり、{Person(ambush.Key).Name}の伏せる{PositionOf(War.Assign[ambush.Key]).Name}の前まで退いて誘い込む" : "敵に当たって誘い込む",
            "伏兵" => $"{place}に伏せ、通り過ぎる敵の横を突く",
            "本隊" or "後詰" => $"{place}を守る",
            "回り込み" => $"{place}から、ぶつかった敵の横へ回り込む",
            "先手" or "正面" => $"合図で{place}から攻めかかる",
            _ => $"{place}に陣を取る",
        };
    }
    public IEnumerable<ProtoOrder> Orders() => War.Assign.Where(a => Person(a.Key).House == "takano").Select(a =>
    {
        var u = Person(a.Key); var pos = PositionOf(a.Value);
        return new ProtoOrder(u.Id, u.Name, War.Roles.GetValueOrDefault(u.Id, ""), pos.Name, OrderText(u.Id), pos.X * 100L, pos.Y * 100L);
    });
}
