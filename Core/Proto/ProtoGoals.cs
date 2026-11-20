namespace HakutaiKagami.Core.Proto;

// 試作版の合戦の大目標と、軍としての撤退条件（2026-11-20 ユーザー）。
// ・大目標（何を果たせば勝ちか）＝「敵を崩す」「陣を守り抜く」。中目標＝作戦、小目標＝役目（Core/Proto/ProtoOrders.cs）。
//   作戦が崩れたあとの自動の動きは、大目標に戻って決める（気性は、どれだけ無理をするかの加減だけ）。
// ・撤退条件は、選ぶものではなく状況で初めから決まっている。軍議では足せるが、減らせない。
//   どれかを満たすと退き貝が鳴り、狼煙と同じに届く（見える所はすぐ、遠い隊は知らせが届いてから）。逸る者は、戦っている最中だと半々で従わない。
// ・敵（岩切勢）も同じに大目標と撤退条件を持つ。
public sealed record ProtoRetreatRule(string Id, string Text, bool Added, bool Met);

public sealed partial class ProtoWorld
{
    public static readonly string[] Goals = ["敵を崩す", "陣を守り抜く"];
    public static readonly string[] RetreatAdds = ["足さない", "全軍士気が半分を切る", "昼までに決まらない", "作戦が崩れる"];
    static readonly string[] AddIds = ["", "spirit-half", "noon", "plan"];
    public static string[] PlansOf(string goal) => goal == Goals[1] ? ["陣を固める", "釣り野伏"] : Plans;
    public string GoalOf(string side) => War.Goals.GetValueOrDefault(side, Goals[0]);

    IEnumerable<ProtoPerson> SideUnits(string side) => People.Where(u => u.Active && u.House != "bandit" && Side(u.House) == side && (side == Rebel ? u.House == Rebel : War.Assign.ContainsKey(u.Id) && War.Assign[u.Id] != "takano-castle"));
    static string Foe(string side) => side == Rebel ? "takano" : Rebel;

    // 状況から撤退条件を決める（軍議・敵の出陣のとき）。added：足す条件（RetreatAdds のどれか）。
    void SetRetreatRules(string side, string? added)
    {
        var rules = new List<string> { "head", "spirit0", GoalOf(side) == Goals[1] ? "lost-post" : "dusk" };
        int mine = SideUnits(side).Sum(u => u.Soldiers), theirs = SideUnits(Foe(side)).Sum(u => u.Soldiers);
        War.StartMen[side] = mine;
        if (theirs > 0 && mine * 10 < theirs * 7) rules.Add("half-men");            // 兵が敵よりかなり少ない
        if (Person(HeadOf(side)).Soldiers > 0) rules.Add("lord");                    // 当主が自ら出ている：立て直しを待たず、崩れたら退く
        int a = Array.IndexOf(RetreatAdds, added ?? ""); if (a > 0 && !(side == Rebel && AddIds[a] == "plan")) { rules.Add(AddIds[a]); War.RetreatAdded[side] = AddIds[a]; }
        War.RetreatRules[side] = rules;
    }
    string RuleText(string side, string id) => id switch
    {
        "head" => "大将の隊が崩れて立て直せない",
        "spirit0" => "全軍士気が尽きる",
        "lost-post" => "守る陣を取られて半刻取り返せない",
        "dusk" => "日が傾いても決まらない",
        "half-men" => "兵が半分を切る",
        "lord" => $"{Person(HeadOf(side)).Name}の隊が崩れる",
        "spirit-half" => RetreatAdds[1],
        "noon" => RetreatAdds[2],
        _ => RetreatAdds[3],
    };
    bool RuleMet(string side, string id)
    {
        if (War.Phase != "合戦") return false;
        var head = Person(HeadOf(side));
        return id switch
        {
            "head" => !head.Active || !StillFights(head),
            "spirit0" => SpiritOf(side) <= 0,
            "lost-post" => flags.TryGetValue("post-lost:" + side, out var since) && Second - since >= 30 * 60,
            "dusk" => War.Signaled && Second >= War.SignalAt + 8 * Hour,
            "half-men" => SideUnits(side).Sum(u => u.Soldiers) * 2 < War.StartMen.GetValueOrDefault(side),
            "lord" => War.Routed.Contains(head.Id),
            "spirit-half" => SpiritOf(side) < 50,
            "noon" => Second >= War.SignalAt / Day * Day + 12 * Hour,
            _ => Flag("plan-failed"),
        };
    }
    public IEnumerable<ProtoRetreatRule> RetreatRules(string side = "takano") => War.RetreatRules.GetValueOrDefault(side, []).Select(id =>
        new ProtoRetreatRule(id, RuleText(side, id), War.RetreatAdded.GetValueOrDefault(side) == id, War.RetreatBy.GetValueOrDefault(side) == id));
    public bool Retreated(string side) => War.RetreatBy.ContainsKey(side);
    internal bool Withdrawing(ProtoPerson u) => Flag("withdrawing:" + u.Id);

    // 毎分：撤退条件を確かめ、退き貝を鳴らし、届いた隊から本陣へ退く。退ききったら決着。
    void Retreats()
    {
        if (War.Phase != "合戦" || War.Result != "") return;
        foreach (var side in new[] { "takano", Rebel })
        {
            // 守る陣を敵に取られているか（取られ始めた時刻を持つ）。
            bool lost = PostLost(side);
            if (!lost) flags.Remove("post-lost:" + side); else if (!flags.ContainsKey("post-lost:" + side)) flags["post-lost:" + side] = Second;
            if (!Retreated(side) && !Retreated(Foe(side)) && War.RetreatRules.GetValueOrDefault(side, []).FirstOrDefault(id => RuleMet(side, id)) is { } met)
            {
                War.RetreatBy[side] = met; War.RetreatAt[side] = Second; var head = Person(HeadOf(side));
                // 総崩れで皆が崩れているなら、退き貝を鳴らすまでもない（決着の決まりに任せる）。
                Happen("retreat:" + side, $"{HouseName(side)}の陣で退き貝が鳴った（{RuleText(side, met)}）。", head.X, head.Y, [.. SideUnits(side).Where(u => !u.Player && Dist(u, head) <= 2500_00)]);
                foreach (var u in SideUnits(side)) { long d = Dist(u, head); flags["retreat-at:" + u.Id] = d <= 2500_00 ? Second : Second + d / 200; }
            }
            if (!Retreated(side)) continue;
            var camp = Map.Site(side == Rebel ? "iwakiri-castle" : "takano-castle"); var leader = HeadOf(side);
            foreach (var u in SideUnits(side).Where(u => u.Soldiers > 0 && !War.Routed.Contains(u.Id) && !Flag("retreat-heard:" + u.Id) && flags.TryGetValue("retreat-at:" + u.Id, out var at) && Second >= at))
            {
                Set("retreat-heard:" + u.Id);
                if (u.Player) { if (Fact("retreat:" + side) is { } f) Learn(u, f); Note($"退き貝が聞こえた。{HouseName(side)}は兵を引く。"); }
                if (!Auto(u)) continue;
                // 逸る者は、戦っている最中だと半々で従わない。
                if (u.Temper == "逸る" && u.Id != leader && ClashOf(u.Id) != null && Roll("stays:" + u.Id) % 100 < 50)
                { Set("stays:" + u.Id); Happen("stays:" + u.Id, $"{u.Name}は退き貝を聞いても退かず、戦い続けた。", u.X, u.Y, u); continue; }
                War.Target.Remove(u.Id); flags.Remove("luring:" + u.Id); Set("withdrawing:" + u.Id);
                Walk(u, Spot(camp, 3), camp.Id, camp.Name + "へ兵を引いている");
            }
            // 退く隊は、歩みが止まっていれば歩き直す。
            foreach (var u in SideUnits(side).Where(u => Withdrawing(u) && !u.Moving && !War.Routed.Contains(u.Id) && Dist(u, camp.X * 100L, camp.Y * 100L) > 300_00)) Walk(u, Spot(camp, 3), camp.Id, camp.Name + "へ兵を引いている");
            // 退く隊がみな敵から600m離れたら（または半刻たったら）決着。それまでは追われる。
            bool heard = SideUnits(side).Where(u => u.Soldiers > 0 && !War.Routed.Contains(u.Id)).All(u => Flag("retreat-heard:" + u.Id));
            bool clear = heard && !SideUnits(side).Any(u => Withdrawing(u) && !War.Routed.Contains(u.Id) && Units.Any(e => Hostile(u.House, e.House) && !War.Routed.Contains(e.Id) && Dist(u, e) <= 600_00));
            if (!clear && Second < War.RetreatAt[side] + 30 * 60) continue;
            var h = Person(HeadOf(side)); War.Phase = "終わり";
            if (side == Rebel)
            {
                War.Result = "退かせた"; Happen("withdrawn:" + side, "岩切勢は兵を引き、岩切城へ退いた。", h.X, h.Y, Person(Heir), Person(Karou), h);
                if (Flag("fought:" + Player.Id)) { Merit += 5; Note("岩切勢を退かせた。勲功5。"); }
            }
            else { War.Result = "退いた"; Happen("withdrawn:" + side, "鷹野勢は兵を引いた。", h.X, h.Y, h, Person(Karou)); }
            GoHome(); return;
        }
        // 陣を守り抜く：合図から三刻（6時間）、守る陣を一つも取られていなければ守り抜いた。
        if (GoalOf("takano") == Goals[1] && War.Signaled && Second >= War.SignalAt + 6 * Hour && !flags.ContainsKey("post-lost:takano"))
        {
            var h = Person(Heir); War.Result = "守り抜いた"; War.Phase = "終わり";
            Happen("held", "鷹野勢は陣を守り抜いた。", h.X, h.Y, h, Person(Karou));
            if (Flag("fought:" + Player.Id)) { Merit += 5; Note("陣を守り抜いた。勲功5。"); }
            GoHome();
        }
    }
    // 決着のあと、戦場に残っている隊は本陣へ引き上げる（本人は、委任のときだけ）。
    void GoHome()
    {
        foreach (var side in new[] { "takano", Rebel })
        {
            var camp = Map.Site(side == Rebel ? "iwakiri-castle" : "takano-castle");
            foreach (var u in SideUnits(side).Where(u => u.Soldiers > 0 && Auto(u) && !War.Routed.Contains(u.Id))) Walk(u, Spot(camp, 3), camp.Id, camp.Name + "へ引き上げている");
        }
    }
}
