namespace HakutaiKagami.Core.Proto;

// 試作版の帰陣と論功行賞（2026-11-20 ユーザーの印「謀叛が鎮まったら急に軍団解散するのも微妙。城に戻るまで維持して、戻ってから論功行賞」
// 「敗戦時とか自部隊が壊滅しても論功行賞からハブられないように」）。
// ・決着しても軍はその場で解かない。本陣があれば皆が本陣に集まり（半刻まで待つ）、陣を畳んで（半刻）から、総勢で戻る
//   （行きと同じ速さ・同じ一つの凸）。本陣が無ければ大将のもとに集まる。退き貝で退いたときは、もう畳んであるのでそのまま城へ（Core/Proto/ProtoCamp.cs）。
// ・大将が城に着くと論功行賞。本人が城にいれば開く。いなければ1日待ち、過ぎたらいないまま済ませて勲功だけ届く。
// ・出陣した者はみな名を呼ばれる：負けても、隊が崩れても、兵を失っても外さない。
//   勲功は、役目の見込み（RoleMerit）・役目を果たしたか・勝ち負けで決まる。
// ・済むと、本人は預かった兵を返す。
public sealed record ProtoAward(string Unit, string Name, string Role, bool Did, int Start, int Now, bool Broken, int Merit, int Deeds);

public sealed partial class ProtoWorld
{
    // 出陣した鷹野勢（軍議で役目をもらった者）。降って鷹野に戻った岩切の者は入れない。
    bool InArmy(ProtoPerson u) => u.House == "takano" && War.Roles.ContainsKey(u.Id) && War.Assign.TryGetValue(u.Id, out var a) && a != "takano-castle";
    // 決着してから論功行賞が済むまで（軍はまだ解かれていない）。
    public bool Returning => War.Phase == "終わり" && War.Result != "" && !Flag("awards-done") && !Flag("war-merit") && People.Any(InArmy);
    // 総勢として城へ戻っているか（画面で一つの勢に見せ、行軍の速さで進む）。
    public bool InReturn(ProtoPerson u) => Returning && Flag("home-march") && !Flag("awards-ready") && InArmy(u) && u.Soldiers > 0 && Auto(u) && !War.Routed.Contains(u.Id) && Dist(u, Person(Heir)) <= 300_00;
    public string AwardsHostId => AwardsHost.Id;
    ProtoPerson AwardsHost => Person(Heir).Active ? Person(Heir) : Person(Karou);

    // 毎分：集まる → 総勢で戻る → 論功行賞。
    void Homecoming()
    {
        if (War.Result != "")
        {
            EndRelief(); UpdatePlan(); EnvoyBack(force: Flag("home-march"));
            foreach (var t in Tasks.Where(t => t.Kind == "duty" && t.State is TaskState.Open or TaskState.Offered)) t.State = TaskState.Expired; // 決着したら、攻城の役目は解く
        } // どんな決着でも、後詰は沢渡へ引き上げる。作戦の最後の段（敵を崩す）も決着で決まる
        if (!Returning) return;
        var heir = Person(Heir); var host = AwardsHost; var castle = Map.Site(AwardsSite); long cx = castle.X * 100L, cy = castle.Y * 100L;
        var army = People.Where(u => InArmy(u) && u.Active).ToArray();
        if (!Flag("awards-ready"))
        {
            var able = army.Where(u => Auto(u) && u.Soldiers > 0 && !War.Routed.Contains(u.Id)).ToArray();
            bool won = War.Result is "鎮めた" or "開城させた" or "退かせた" or "守り抜いた" or "引き分け", leads = won && able.Contains(heir);
            UpdatePacking(); // 移している最中なら、移し終えるまで待つ（集まる先は移す先）
            // 兵を失った者（委任の本人など）も軍の一員：集まる間は本陣（無ければ大将）へ、戻る間は総大将について歩く（先に城へ入って待たない。2026-11-21 ユーザーの印）。
            foreach (var u in army.Where(u => Auto(u) && u.Soldiers == 0 && u.Id != host.Id))
            {
                if (u.Moving && Second % 120 != 0) continue;
                if (Flag("home-march") && SiteOf(host) == castle.Id) { if (SiteOf(u) != castle.Id && !u.Moving) Walk(u, Spot(castle, Hash(u.Id, 0) % 12), castle.Id, castle.Name + "へ引き上げている"); continue; } // 総大将が城に入れば一緒に入る
                (long X, long Y) to = Flag("home-march") ? (host.X, host.Y) : (CampAt("takano") ?? (host.X, host.Y)); // 移している間は元の本陣で待つ
                if (Dist(u, to.X, to.Y) > 60_00) Walk(u, ProtoMove.Nearest(Map, to.X - 30_00, to.Y + 40_00), host.Name, Flag("home-march") ? $"{host.Name}勢について引き上げている" : $"{host.Name}勢に加わっている");
                else if (!u.Moving) u.Doing = Flag("home-march") ? $"{host.Name}勢について引き上げている" : $"{host.Name}勢に加わっている";
            }
            var hq = CampAt("takano") is { } c0 && !CampLost("takano") ? (CampMoveTarget ?? c0) : ((long X, long Y)?)null;
            if (!Flag("home-gather")) { Set("home-gather"); if (leads && hq == null) { heir.Path = []; heir.PathIndex = 0; heir.Doing = "兵をまとめている"; } }
            if (!Flag("home-march"))
            {
                // 本陣があれば、皆が本陣に集まって陣を畳む（負けて崩れていても）。無ければ、勝ったときだけ大将のもとに集まる。
                if (hq is { } h0)
                {
                    var all = army.Where(u => Auto(u) && u.Soldiers > 0).ToArray();
                    if (!Flag("camp-pack") && !all.All(u => Dist(u, h0.X, h0.Y) <= 200_00) && Second < flags["home-gather"] + 30 * 60)
                    {
                        foreach (var u in all.Where(u => Dist(u, h0.X, h0.Y) > 150_00 && (!u.Moving || u.Goal != "本陣"))) Walk(u, AroundCamp(h0, u.Id), "本陣", "本陣へ集まっている");
                        return;
                    }
                    StartPacking(); UpdatePacking();
                    if (CampMoving) { foreach (var u in all.Where(u => !u.Moving)) u.Doing = "本陣を移し終えるのを待っている"; return; }
                    if (!Flag("camp-packed")) { foreach (var u in all.Where(u => !u.Moving)) u.Doing = "本陣を畳んでいる"; return; }
                }
                else if (leads && !able.All(u => Dist(u, heir) <= 200_00) && Second < flags["home-gather"] + 30 * 60)
                {
                    foreach (var u in able.Where(u => u.Id != Heir && Dist(u, heir) > 150_00 && (!u.Moving || u.Goal != heir.Name))) Walk(u, (heir.X, heir.Y), heir.Name, $"{heir.Name}のもとへ集まっている");
                    return;
                }
                Set("home-march");
                foreach (var u in army.Where(u => Auto(u) && u.Soldiers > 0)) Walk(u, Spot(castle, Hash(u.Id, 0) % 12), castle.Id, castle.Name + "へ引き上げている"); // 崩れていた隊も、兵が残っていれば一緒に
                if (leads) Happen("home-march", $"{heir.Name}勢は兵をまとめ、{castle.Name}へ引き上げた。", heir.X, heir.Y, able);
                if (InArmy(Player) && !Auto(Player)) Note($"{host.Name}勢は{castle.Name}へ引き上げる。");
            }
            foreach (var u in army.Where(u => Auto(u) && u.Soldiers > 0 && !u.Moving && SiteOf(u) != castle.Id)) Walk(u, Spot(castle, Hash(u.Id, 0) % 12), castle.Id, castle.Name + "へ引き上げている");
            if (SiteOf(host) != castle.Id && Second < flags["home-gather"] + 8 * Hour) return;
            Set("awards-ready");
            Happen("awards", $"{castle.Name}で論功行賞が行われる。", cx, cy, [.. army.Where(u => SiteOf(u) == castle.Id || u.Player && InArmy(u))]);
            if (!InArmy(Player)) { Set("awards-done"); return; }
            var t = Offer(host.Id, "論功行賞に出る", "awards", Second, Second + 2 * Day, 0, new ProtoStep("attend", $"{castle.Name}の広間で論功行賞に出る", castle.Id));
            t.State = TaskState.Open;
        }
        else
        {
            // 論功行賞を待つ間、兵のない者も城の中へ入る。
            foreach (var u in army.Where(u => Auto(u) && u.Soldiers == 0 && !u.Moving && SiteOf(u) != castle.Id)) Walk(u, Spot(castle, Hash(u.Id, 0) % 12), castle.Id, castle.Name + "へ引き上げている");
            if (Second >= flags["awards-ready"] + Day) TakeAwards(true);
        }
    }

    public bool AwardsReady => Flag("awards-ready");
    public bool AwardsOpen => Flag("awards-ready") && !Flag("awards-done") && SiteOf(Player) == AwardsSite;
    // 論功行賞の場所：城を落とした・開かせたときは、その城に入って行う。ほかは鷹野城へ戻ってから。
    public string AwardsSite => War.Siege != null && War.Result is "鎮めた" or "開城させた" ? FoeCastle : "takano-castle";
    public string AwardsSpeech => AwardsHost.Name + "「" + War.Result switch
    {
        "鎮めた" => War.Siege != null ? "岩切城は落ちた。皆、よう働いた。" : "謀叛は鎮まった。皆、よう働いた。",
        "開城させた" => "岩切城は開いた。皆、よう働いた。",
        "退かせた" or "守り抜いた" => "岩切勢を退けた。皆、よう働いた。",
        "引き分け" => "決着はつかなんだ。次こそ討つ。",
        _ => "この度は不覚を取った。次に備えよ。",
    } + "」";
    // 出陣した者みな（大将・家老・本人の順）。
    public IEnumerable<ProtoAward> Awards() => People.Where(InArmy).OrderBy(u => u.Id == Heir ? 0 : u.Player ? 2 : 1).ThenBy(u => u.Id)
        .Select(u => new ProtoAward(u.Id, u.Name, War.Roles.GetValueOrDefault(u.Id, ""), DidRole(u.Id), War.StartSoldiers.GetValueOrDefault(u.Id, u.Soldiers), u.Soldiers, War.Routed.Contains(u.Id), WarMerit(u.Id), Deeds(u.Id)));

    // 役目を果たしたか。
    bool DidRole(string id)
    {
        bool fought = Flag("fought:" + id);
        return War.Roles.GetValueOrDefault(id, "") switch
        {
            "囮" => Fact("bait-back:" + id) != null || fought && Facts.Any(f => f.Id.StartsWith("surprise:")),
            "伏兵" or "回り込み" => Facts.Any(f => f.Id.StartsWith("surprise:" + id + ":") || f.Id.StartsWith("flank:" + id + ":")),
            _ => fought,
        };
    }
    // 戦果：与えた打撃を、預かった兵の数で割ったもの（0〜6）。同じ兵で同じ役目でも、働きが違えば勲功が変わる（2026-11-25 ユーザー）。
    public int Deeds(string id) => (int)Math.Min(6, Math.Round(War.Dealt.GetValueOrDefault(id) * 10 / Math.Max(1, War.StartSoldiers.GetValueOrDefault(id, 0))));
    // 勲功：役目を果たせば見込みどおり、果たせなければ半分。戦果を足す。勝ちで上乗せ、負けは戦っていれば見込みの四半分。
    // 本人は、攻城の毎日の役目（1つにつき1。小さな務めなので、合戦の役目より軽い）と一番乗り（6）も足す。兵を失ったこと自体では減らさない。
    public bool Ichiban => Flag("ichiban");
    public int WarMerit(string id)
    {
        int see = RoleMerit(War.Roles.GetValueOrDefault(id, "")), m = (DidRole(id) ? see : see / 2) + Deeds(id);
        if (id == Player.Id && Flag("parley-won")) m += 8; // 説いて城を開かせた
        if (id == Player.Id) m += Math.Min(8, DutiesDone / 3) + (Flag("ichiban") ? 6 : 0) + (Flag("sally-spotted") ? 3 : 0);
        if (War.Siege != null) m += Math.Min(6, War.Siege.Built.GetValueOrDefault(id) * 3); // 付城を築いた
        if (Flag("envoy-won:" + id)) m += 6; // 使者に立って城を開かせた
        return Math.Max(0, War.Result switch { "鎮めた" or "開城させた" => m + 4, "退かせた" or "守り抜いた" => m + 2, _ => Flag("fought:" + id) ? see / 4 + Deeds(id) : 0 });
    }

    // 論功行賞を受ける。absent：本人がいないまま済ませる。
    string TakeAwards(bool absent = false)
    {
        if (!Flag("awards-ready") || Flag("awards-done") || !absent && !AwardsOpen) return "";
        Set("awards-done");
        int m = WarMerit(Player.Id), back = Player.Soldiers; Merit += m; Player.Soldiers = 0;
        foreach (var t in Tasks.Where(t => t.Kind == "awards" && t.State == TaskState.Open).ToArray()) t.State = TaskState.Done;
        string what = War.Siege != null ? "攻城の働き" : "合戦の働き";
        Note(absent ? $"論功行賞は済んでいた。{what}で勲功{m}。" : $"{what}で勲功{m}。");
        if (back > 0) Note($"預かった兵{back}を返した。");
        return "";
    }
}
