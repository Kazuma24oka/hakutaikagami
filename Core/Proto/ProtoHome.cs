namespace HakutaiKagami.Core.Proto;

// 試作版の帰陣と論功行賞（2026-11-20 ユーザーの印「謀叛が鎮まったら急に軍団解散するのも微妙。城に戻るまで維持して、戻ってから論功行賞」
// 「敗戦時とか自部隊が壊滅しても論功行賞からハブられないように」）。
// ・決着しても軍はその場で解かない。勝ったとき・引き分けのときは大将のもとに集まり（半刻まで待つ）、総勢で鷹野城へ戻る
//   （行きと同じ速さ・同じ一つの凸）。負けて退くときは、集まらずにそのまま城へ。
// ・大将が城に着くと論功行賞。本人が城にいれば開く。いなければ1日待ち、過ぎたらいないまま済ませて勲功だけ届く。
// ・出陣した者はみな名を呼ばれる：負けても、隊が崩れても、兵を失っても外さない。
//   勲功は、役目の見込み（RoleMerit）・役目を果たしたか・勝ち負けで決まる。
// ・済むと、本人は預かった兵を返す。
public sealed record ProtoAward(string Unit, string Name, string Role, bool Did, int Start, int Now, bool Broken, int Merit);

public sealed partial class ProtoWorld
{
    bool InArmy(ProtoPerson u) => u.House == "takano" && War.Assign.TryGetValue(u.Id, out var a) && a != "takano-castle";
    // 決着してから論功行賞が済むまで（軍はまだ解かれていない）。
    public bool Returning => War.Phase == "終わり" && War.Result != "" && !Flag("awards-done") && !Flag("war-merit") && People.Any(InArmy);
    // 総勢として城へ戻っているか（画面で一つの勢に見せ、行軍の速さで進む）。
    public bool InReturn(ProtoPerson u) => Returning && Flag("home-march") && !Flag("awards-ready") && InArmy(u) && u.Soldiers > 0 && Auto(u) && !War.Routed.Contains(u.Id) && Dist(u, Person(Heir)) <= 300_00;
    public string AwardsHostId => AwardsHost.Id;
    ProtoPerson AwardsHost => Person(Heir).Active ? Person(Heir) : Person(Karou);

    // 毎分：集まる → 総勢で戻る → 論功行賞。
    void Homecoming()
    {
        if (!Returning) return;
        var heir = Person(Heir); var host = AwardsHost; var castle = Map.Site(AwardsSite); long cx = castle.X * 100L, cy = castle.Y * 100L;
        var army = People.Where(u => InArmy(u) && u.Active).ToArray();
        if (!Flag("awards-ready"))
        {
            var able = army.Where(u => Auto(u) && u.Soldiers > 0 && !War.Routed.Contains(u.Id)).ToArray();
            bool won = War.Result is "鎮めた" or "開城させた" or "退かせた" or "守り抜いた" or "引き分け", leads = won && able.Contains(heir);
            if (!Flag("home-gather")) { Set("home-gather"); if (leads) { heir.Path = []; heir.PathIndex = 0; heir.Doing = "兵をまとめている"; } }
            if (!Flag("home-march"))
            {
                if (leads && !able.All(u => Dist(u, heir) <= 200_00) && Second < flags["home-gather"] + 30 * 60)
                {
                    foreach (var u in able.Where(u => u.Id != Heir && Dist(u, heir) > 150_00 && (!u.Moving || u.Goal != heir.Name))) Walk(u, (heir.X, heir.Y), heir.Name, $"{heir.Name}のもとへ集まっている");
                    return;
                }
                Set("home-march");
                foreach (var u in army.Where(u => Auto(u))) Walk(u, Spot(castle, 3), castle.Id, castle.Name + "へ引き上げている");
                if (leads) Happen("home-march", $"{heir.Name}勢は兵をまとめ、{castle.Name}へ引き上げた。", heir.X, heir.Y, able);
                if (InArmy(Player) && !Auto(Player)) Note($"{host.Name}勢は{castle.Name}へ引き上げる。");
            }
            foreach (var u in army.Where(u => Auto(u) && !u.Moving && SiteOf(u) != castle.Id)) Walk(u, Spot(castle, 3), castle.Id, castle.Name + "へ引き上げている");
            if (SiteOf(host) != castle.Id && Second < flags["home-gather"] + 8 * Hour) return;
            Set("awards-ready");
            Happen("awards", $"{castle.Name}で論功行賞が行われる。", cx, cy, [.. army.Where(u => SiteOf(u) == castle.Id)]);
            if (!InArmy(Player)) { Set("awards-done"); return; }
            var t = Offer(host.Id, "論功行賞に出る", "awards", Second, Second + 2 * Day, 0, new ProtoStep("attend", $"{castle.Name}の広間で論功行賞に出る", castle.Id));
            t.State = TaskState.Open;
            if (SiteOf(Player) != castle.Id) Note($"{castle.Name}で論功行賞がある。");
        }
        else if (Second >= flags["awards-ready"] + Day) TakeAwards(true);
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
        .Select(u => new ProtoAward(u.Id, u.Name, War.Roles.GetValueOrDefault(u.Id, ""), DidRole(u.Id), War.StartSoldiers.GetValueOrDefault(u.Id, u.Soldiers), u.Soldiers, War.Routed.Contains(u.Id), WarMerit(u.Id)));

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
    // 勲功：役目を果たせば見込みどおり、果たせなければ半分。勝ちで上乗せ、負けは戦っていれば見込みの四半分。
    public int WarMerit(string id)
    {
        int see = RoleMerit(War.Roles.GetValueOrDefault(id, "")), m = DidRole(id) ? see : see / 2;
        if (id == Player.Id && Flag("parley-won")) m += 8; // 説いて城を開かせた
        return Math.Max(0, War.Result switch { "鎮めた" or "開城させた" => m + 4, "退かせた" or "守り抜いた" => m + 2, _ => Flag("fought:" + id) ? see / 4 : 0 });
    }

    // 論功行賞を受ける。absent：本人がいないまま済ませる。
    string TakeAwards(bool absent = false)
    {
        if (!Flag("awards-ready") || Flag("awards-done") || !absent && !AwardsOpen) return "";
        Set("awards-done");
        int m = WarMerit(Player.Id), back = Player.Soldiers; Merit += m; Player.Soldiers = 0;
        foreach (var t in Tasks.Where(t => t.Kind == "awards" && t.State == TaskState.Open).ToArray()) t.State = TaskState.Done;
        Note(absent ? $"論功行賞は済んでいた。合戦の働きで勲功{m}。" : $"合戦の働きで勲功{m}。");
        if (back > 0) Note($"預かった兵{back}を返した。");
        return "";
    }
}
