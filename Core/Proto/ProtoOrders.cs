namespace HakutaiKagami.Core.Proto;

// 試作版の軍議の作戦と役目、総勢での行軍（2026-11-19 ユーザー）。
// ・主君の指示の出陣は、まず城に集まり、総勢が大将に率いられてまとまって戦地（着陣の地）へ進み、着いてから役目ごとに分かれる。
//   出立の遅れは無い。行軍の速さは大将の統率で決まる（60＋統率×0.7 ％）。
// ・作戦を選ぶと役目が出る。正面から当たる＝先手・後詰、釣り野伏＝囮・伏兵・本隊、挟撃＝正面・回り込み。
//   NPC は役目どおりに動く。ただし勇猛な者が囮になると、半々で退かずに戦い続ける。
// ・本人は「指図どおりに動く」を選ぶと、NPC と同じに役目どおり自動で動く。自分で動けばその間は指図から外れる。
// ・作戦は段取りの並び（2026-11-20 ユーザー）。段ごとに成功・失敗が付き、一つでも失敗すれば作戦は崩れる。
//   崩れたあとは作戦なし：自動で動く隊（NPC と、委任にした本人）は気性に合う動きをする（逸る＝見えた敵へ攻めかかる、
//   慎重＝南の丘へ下がって守る、普通＝味方が近くで戦っているか兵が劣らなければ攻める）。
public sealed record ProtoOrder(string Unit, string Name, string Role, string Place, string Text, long X, long Y);
public sealed record ProtoPlanStep(string Text, string State); // State：""（まだ）・"成功"・"失敗"

public sealed partial class ProtoWorld
{
    public static (string Role, string Position)[] RolesOf(string plan) => plan switch
    {
        "釣り野伏" => [("囮", "road-mouth"), ("伏兵", "forest-edge"), ("本隊", "south-hill")],
        "挟撃" => [("正面", "road-mouth"), ("回り込み", "forest-edge")],
        "陣を固める" => [("道の守り", "road-mouth"), ("丘の守り", "south-hill"), ("森の守り", "forest-edge")],
        _ => [("先手", "road-mouth"), ("後詰", "south-hill")],
    };

    // 総大将の決めた役目と陣（Core/Proto/ProtoField.cs の RolesBy・PostFor）に、本人の再提案（役目・陣）を重ねる。
    void AssignRoles(string plan, string? playerRole, string? playerPosition, Dictionary<string, long[]>? posts = null)
    {
        War.Plan = plan; var roles = RolesOf(plan);
        // 前の呼び方：地図の点の名前だけを渡されたら、その点に合う役目にする。
        if (playerRole == null && playerPosition != null) playerRole = roles.FirstOrDefault(r => r.Position == playerPosition).Role;
        var (rh, rk, rm) = RolesBy(plan, playerRole);
        foreach (var (id, r) in new[] { (Heir, rh), (Karou, rk), (Player.Id, rm) }) { War.Roles[id] = r; var at = PostFor(r); SetPost(id, at.X, at.Y); }
        if (playerPosition == "takano-castle") { War.Assign[Player.Id] = "takano-castle"; War.Post.Remove(Player.Id); War.Roles[Player.Id] = ""; }
        else if (playerPosition != null) { var pp = PositionOf(playerPosition); SetPost(Player.Id, pp.X * 100L, pp.Y * 100L); }
        bool lead = HeadOf("takano") == Player.Id;
        foreach (var (id, at) in posts ?? []) if ((lead || id == Player.Id) && War.Post.ContainsKey(id)) SetPost(id, at[0], at[1]);
        // 着陣の地：城から戦場への道の、正面の500m手前。
        (War.StageX, War.StageY) = RoadPoint(1150_00);
    }

    // NPC と同じに自動で動くか（本人は「指図どおりに動く」のときだけ）。
    internal bool Auto(ProtoPerson u) => !u.Player || War.PlayerFollows;
    // 総勢の中にいるか（出立してから着陣まで）。
    public bool InBody(ProtoPerson u) => War.Phase is "行軍" or "合戦" && !War.Split && Flag("marched") && u.House == "takano" && u.Soldiers > 0 && War.Assign.TryGetValue(u.Id, out var a) && a != "takano-castle" && Auto(u) && !War.Routed.Contains(u.Id);
    // 画面で一つの勢として見せるか：城に集まっている間と、総勢で進んでいる間（大将から300m以内）。
    public bool InHost(ProtoPerson u) => War.Phase == "行軍" && !War.Split && u.House == "takano" && u.Soldiers > 0 && War.Assign.TryGetValue(u.Id, out var a) && a != "takano-castle" && Auto(u) && Dist(u, Person(Heir)) <= 300_00;
    public IEnumerable<ProtoPerson> Host => Units.Where(InHost);
    public int MarchPercent => 60 + Person(Heir).Command * 7 / 10;
    public IEnumerable<ProtoPerson> Body => Units.Where(InBody);

    // 総勢での行軍：刻限に大将に率いられて着陣の地へ。大将が着いたら役目ごとに分かれる。
    void MarchTogether(ProtoPerson[] takano)
    {
        if (War.Split || Retreated("takano")) return;
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
            if (!War.PlayerFollows) Note($"出立の刻限になった。{PostOf(Player.Id).Name}へ向かう。");
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
        foreach (var u in body) { var pos = PostOf(u.Id); MarchTo(u, pos); }
    }

    // ---- 作戦の段取り ----
    string? Who(string role) => War.Roles.FirstOrDefault(r => r.Value == role).Key;
    bool Fought(string? id) => id != null && Flag("fought:" + id);
    bool Broke(string? id) => id != null && Facts.Any(f => f.Id.StartsWith("rout:" + id + ":"));
    // 段ごとの確かめ：0＝まだ、1＝成功、2＝失敗。
    (string Text, Func<int> Check)[] StepDefs()
    {
        var steps = new List<(string, Func<int>)>
        {
            ("鷹野城に集まって出立する", () => Flag("marched") ? 1 : 0),
            ("着陣の地まで進み、役目ごとに分かれる", () => War.Split ? 1 : 0),
            ("合図まで陣で待つ", () => Facts.Any(f => f.Id.StartsWith("nukegake:")) ? 2 : War.Signaled ? 1 : 0),
        };
        bool hold = GoalOf("takano") == Goals[1];
        switch (War.Plan)
        {
            case "釣り野伏":
            {
                string? bait = Who("囮"), amb = Who("伏兵");
                steps.Add(("囮が敵に当たる", () => Fought(bait) ? 1 : Broke(bait) ? 2 : 0));
                steps.Add(("囮が退いて、伏兵の前へ誘い込む", () => Fact("bait-stays:" + bait) != null ? 2 : Fact("bait-back:" + bait) != null && Flag("bait-done:" + bait) || Facts.Any(f => f.Id.StartsWith("surprise:" + amb + ":")) ? 1 : Broke(bait) ? 2 : 0));
                steps.Add(("伏兵が敵の横を突く", () => Facts.Any(f => f.Id.StartsWith("surprise:" + amb + ":")) ? 1 : Broke(amb) || flags.TryGetValue("bait-done:" + bait, out var done) && Second >= done + 10 * 60 ? 2 : 0));
                break;
            }
            case "挟撃":
            {
                string? front = Who("正面"), flank = Who("回り込み");
                steps.Add(("正面が敵に当たる", () => Fought(front) ? 1 : Broke(front) ? 2 : 0));
                steps.Add(("回り込みが敵の横を突く", () => Fact("flanker:" + flank) != null && Fought(flank) && ClashOf(flank!) != null ? 1 : Broke(flank) || Broke(front) ? 2 : 0));
                break;
            }
            case "陣を固める":
                steps.Add(("それぞれの陣に着く", () => !War.Signaled ? 0 : PostLost("takano") ? 2 : 1));
                break;
            default:
            {
                string? van = Who("先手");
                steps.Add(("先手が攻めかかる", () => Fought(van) ? 1 : Broke(van) ? 2 : 0));
                string? rear = Who("後詰");
                steps.Add(("後詰が支える", () => Broke(rear) ? 2 : Fought(rear) || War.Result != "" ? 1 : 0));
                break;
            }
        }
        steps.Add((hold ? "陣を守り抜く" : "敵を崩す", () => War.Result is "鎮めた" or "退かせた" || hold && War.Result == "守り抜いた" ? 1 : War.Result != "" ? 2 : 0));
        return [.. steps];
    }
    public IEnumerable<ProtoPlanStep> PlanSteps()
    {
        if (War.Plan == "") yield break;
        var defs = StepDefs();
        for (int i = 0; i < defs.Length; i++) yield return new(defs[i].Text, Flag("plan-ok:" + i) ? "成功" : Flag("plan-ng:" + i) ? "失敗" : "");
    }
    // 作戦の成否：""（進めている）・"成功"・"失敗"。
    public string PlanState => War.Plan == "" ? "" : Flag("plan-failed") ? "失敗" : Flag("plan-done") ? "成功" : "";
    void UpdatePlan()
    {
        if (War.Plan == "" || Flag("plan-failed") || Flag("plan-done")) return;
        var defs = StepDefs();
        for (int i = 0; i < defs.Length; i++)
        {
            if (Flag("plan-ok:" + i)) continue;
            int r = defs[i].Check(); if (r == 0) continue;
            if (r == 1) { Set("plan-ok:" + i); if (i == defs.Length - 1) Set("plan-done"); if (Enumerable.Range(0, defs.Length - 1).All(k => Flag("plan-ok:" + k))) Set("plan-ran"); continue; }
            Set("plan-ng:" + i); Set("plan-failed"); var heir = Person(Heir);
            // 最後の段（敵を崩す）の失敗は合戦の決着なので、別に知らせない。
            if (i < defs.Length - 1) Happen("plan-failed", $"{War.Plan}は崩れた（{defs[i].Text}）。", heir.X, heir.Y, heir, Person(Karou), Player);
            return;
        }
    }

    // 作戦なしの動き：気性に合う動きをする。
    void Freelance(ProtoPerson[] takano, ProtoPerson[] rebels)
    {
        if (War.Phase != "合戦") return;
        bool hold = GoalOf("takano") == Goals[1]; var head = Person(Heir); var foeCamp = Map.Site("iwakiri-castle");
        foreach (var u in takano.Where(Auto))
        {
            flags.Remove("luring:" + u.Id);
            if (War.Target.ContainsKey(u.Id) || ClashOf(u.Id) != null) continue;
            var post = PostOf(u.Id); long px = post.X * 100L, py = post.Y * 100L;
            var seen = rebels.Where(r => InSight(u, r)).OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault();
            bool near = rebels.Any(r => Dist(u, r) <= 2 * Reach);
            bool helped = War.Clashes.Any(c => Dist(u, Person(c.A)) <= 400_00 || Dist(u, Person(c.B)) <= 400_00);
            string did = "", what = "";
            if (hold)
            {
                // 陣を守り抜く：陣の近く（300m）に来た敵だけを相手にし、離れていれば陣へ戻る。勇猛な者だけ、見えた敵を追って出る。
                if (seen != null && (u.Temper == "勇猛" || Dist(seen, px, py) <= 300_00)) { War.Target[u.Id] = seen.Id; what = seen.Id; did = $"{u.Name}は{seen.Name}の兵へ攻めかかった。"; }
                else if (!near && !u.Moving && Dist(u, px, py) > 150_00) { MarchTo(u, post); what = "post"; did = $"{u.Name}は{post.Name}へ戻って守りを固めた。"; }
            }
            else
            {
                // 敵を崩す：攻め続ける。勇猛な者は一人でも、臨機の者は兵が劣らなければ、慎重な者は味方が近くで戦っているときに。そうでなければ大将と合流する。
                // 自分に見えていなくても、味方の誰かに見えている敵は知らせで分かる（大将と勇猛な者はそこへ向かう）。
                if (seen == null && (u.Id == Heir || u.Temper == "勇猛")) seen = rebels.Where(r => takano.Any(t => InSight(t, r))).OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault();
                bool go = seen != null && (u.Temper == "勇猛" || u.Id == Heir || helped || u.Temper == "臨機" && u.Soldiers >= seen.Soldiers && SpiritOf("takano") >= 50); // 臨機は、兵が劣らず全軍士気が半分あれば攻める
                if (go) { War.Target[u.Id] = seen!.Id; what = seen.Id; did = $"{u.Name}は{seen.Name}の兵へ攻めかかった。"; }
                else if (seen == null && !u.Moving && (u.Id == Heir || War.Routed.Contains(Heir) && u.Temper != "慎重") && Dist(u, foeCamp.X * 100L, foeCamp.Y * 100L) > 300_00)
                { Walk(u, ProtoMove.Nearest(Map, foeCamp.X * 100L, foeCamp.Y * 100L), foeCamp.Name, $"{foeCamp.Name}へ兵を進めている"); what = "advance"; did = $"{u.Name}は敵を求めて{foeCamp.Name}へ兵を進めた。"; }
                else if (u.Id != Heir && !near && !u.Moving && Dist(u, head) > 200_00 && !War.Routed.Contains(Heir)) { Walk(u, (head.X, head.Y), head.Name, $"{head.Name}の隊と合流しようとしている"); what = "join"; did = $"{u.Name}は{head.Name}の隊と合流しに向かった。"; }
            }
            if (did != "") Happen($"free:{u.Id}:{what}", did, u.X, u.Y, u); // 同じ動きは一度だけ知らせる
        }
    }

    // 合図のあとの動き（役目ごと）。作戦が崩れたあとは、気性に合う動き。
    void ActOnRoles(ProtoPerson[] takano, ProtoPerson[] rebels)
    {
        UpdatePlan();
        // 作戦が崩れたあとと、作戦の段取りをやり終えたあと（残るのは大目標だけ）は、大目標に戻って動く。
        if (Flag("plan-failed") || Flag("plan-ran")) { Freelance(takano, rebels); return; }
        if (!War.Signaled) return;
        foreach (var u in takano.Where(u => Auto(u) && flags.TryGetValue("attack-at:" + u.Id, out var at) && Second >= at))
        {
            string role = War.Roles.GetValueOrDefault(u.Id, ""); var pos = PostOf(u.Id);
            if (role == "囮") { Bait(u, rebels); continue; }
            if (War.Target.ContainsKey(u.Id)) continue;
            // 守り：陣の近く（300m）に来た敵だけを相手にする。
            if (role.EndsWith("守り"))
            {
                var comer = rebels.Where(r => InSight(u, r) && Dist(r, pos.X * 100L, pos.Y * 100L) <= 300_00).OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault();
                if (comer != null) War.Target[u.Id] = comer.Id;
                else if (!u.Moving && ClashOf(u.Id) == null && Dist(u, pos.X * 100L, pos.Y * 100L) > 150_00) MarchTo(u, pos);
                continue;
            }
            // 伏兵（伏兵の陣）：合図でも出ずに伏せて待つ（ProtoTactics.cs の Ambushers が、目の前に来た敵へ仕掛ける）。
            if (role == "伏兵" || InForest(pos.X * 100L, pos.Y * 100L) && role != "回り込み") continue;
            // 本隊は伏兵が仕掛けるまで、後詰は先手が10分戦い続けるまで、陣に控える。陣の近く（400m）に来た敵は相手にする。
            if (role is "本隊" or "後詰")
            {
                var comer = rebels.Where(r => InSight(u, r) && Dist(r, pos.X * 100L, pos.Y * 100L) <= 400_00).OrderBy(r => Dist(u, r)).ThenBy(r => r.Id).FirstOrDefault();
                if (comer != null) { War.Target[u.Id] = comer.Id; continue; }
                bool go = role == "本隊" ? Facts.Any(f => f.Id.StartsWith("surprise:")) : War.Clashes.Any(c => Second - c.Since >= 10 * 60);
                if (!go) continue;
            }
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
        // 勇猛な者は、半々で退かずに戦い続ける。
        if (u.Temper == "勇猛" && Roll("bait:" + u.Id) % 100 < 50)
        {
            Set("bait-done:" + u.Id);
            Happen("bait-stays:" + u.Id, $"囮の{u.Name}は退かず、そのまま戦い続けた。", u.X, u.Y, u);
            return;
        }
        War.Target.Remove(u.Id); Set("luring:" + u.Id);
        var lure = LurePoint(u, ambusher);
        Walk(u, lure, PostOf(ambusher.Id).Name, "敵を誘って退いている");
        Happen("bait-back:" + u.Id, $"囮の{u.Name}の兵が、{ambusher.Name}の伏せる方へ退き始めた。", u.X, u.Y, u);
    }
    // 囮が退く先：伏兵のすぐ横（60m）を通り過ぎて、その先350m。
    (long X, long Y) LurePoint(ProtoPerson bait, ProtoPerson ambusher)
    {
        double dx = ambusher.X - bait.X, dy = ambusher.Y - bait.Y, len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy)); dx /= len; dy /= len;
        var p = (ambusher.X + (long)(dx * 350_00 + dy * 60_00), ambusher.Y + (long)(dy * 350_00 + dx * 60_00));
        return ProtoMove.Nearest(Map, p.Item1, p.Item2);
    }

    // 委任（作戦どおりに自動で動く。作戦が崩れたあとは気性に合う動き）と、手動。
    string FollowOrders(bool on)
    {
        if (!War.Assign.TryGetValue(Player.Id, out var a)) return "";
        War.PlayerFollows = on; if (!on) return "";
        War.Target.Remove(Player.Id);
        if (Flag("plan-failed")) return "";
        if (War.Split) MarchTo(Player, PostOf(Player.Id));
        else if (Flag("marched")) Walk(Player, (War.StageX, War.StageY), "着陣の地", $"{Person(Heir).Name}勢として兵を進めている");
        return "";
    }
    // 本人が自分で動いたら、指図から外れる（画面から呼ぶ）。
    public void ActOnOwn() => War.PlayerFollows = false;

    // 軍令の一覧（「任務」の窓の軍令のタブ）。
    public string OrderText(string id)
    {
        if (!War.Assign.TryGetValue(id, out var a)) return "";
        var place = PostOf(id).Name; var ambush = War.Roles.FirstOrDefault(r => r.Value == "伏兵");
        return War.Roles.GetValueOrDefault(id, "") switch
        {
            "囮" => ambush.Key != null ? $"敵に当たり、{Person(ambush.Key).Name}の伏せる{PostOf(ambush.Key).Name}の前まで退いて誘い込む" : "敵に当たって誘い込む",
            "伏兵" => $"{place}に伏せ、通り過ぎる敵の横を突く",
            "本隊" => $"{place}に控え、伏兵が仕掛けたら攻めかかる",
            "後詰" => $"{place}に控え、先手が戦い続けていれば加勢する",
            "道の守り" or "丘の守り" or "森の守り" => $"{place}を守る",
            "回り込み" => $"{place}から、ぶつかった敵の横へ回り込む",
            "先手" or "正面" => $"合図で{place}から攻めかかる",
            _ => $"{place}に陣を取る",
        };
    }
    public IEnumerable<ProtoOrder> Orders() => War.Assign.Where(a => Person(a.Key).House == "takano").Select(a =>
    {
        var u = Person(a.Key); var pos = PostOf(a.Key);
        return new ProtoOrder(u.Id, u.Name, War.Roles.GetValueOrDefault(u.Id, ""), pos.Name, OrderText(u.Id), pos.X * 100L, pos.Y * 100L);
    });
}
