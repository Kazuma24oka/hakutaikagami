namespace HakutaiKagami.Core.Proto;

// 試作版の合戦の作り直し（2026-11-19 ユーザー「今の戦闘は何をやっているかよく分からない」→ 組み合わせ案で試作）。Docs/CORE_LOOP.md。
// 骨組み：要所と総士気（新生・戦国下天禄の勝ち負けの決まり）。陣を持つ側の総士気が上がり、隊が崩れると下がる。0で総崩れ。
// 見え方：ぶつかった隊どうしの押し合い（HoI4 の戦いのゲージ）と、狭い所では一度に戦える隊の数が限られる（戦場の幅）。
// 手応え：本人の隊がぶつかっている間だけ、構え（白兵・射撃・防御）・戦法・「かかれ」（気力を使う一押し、半熟英雄のボタン）。
// 立て直し：崩れた隊は本陣へ戻り、しばらく休むと一度だけ戦いに戻れる（戦国下天禄の再出撃）。
public sealed class ProtoClash
{
    public string A { get; set; } = "";   // 鷹野方
    public string B { get; set; } = "";   // 岩切方
    public double Push { get; set; }      // -100〜100。正なら鷹野方が押している
    public long Since { get; set; }
}

public sealed partial class ProtoWorld
{
    public static readonly string[] Stances = ["白兵", "射撃", "防御"];
    // 戦法：武将の技能から決まる。ゲージがたまると使える（NPC はたまれば自分で使う）。
    public static string TacticOf(ProtoPerson p) => p.Skills.Contains("先駆け") ? "突撃" : p.Skills.Contains("槍衾") ? "槍衾" : p.Skills.Contains("城構え") ? "堅陣" : "鼓舞";
    public int SpiritOf(string house) => War.Spirit.TryGetValue(house, out var s) ? s : 100;
    public ProtoClash? ClashOf(string id) => War.Clashes.FirstOrDefault(c => c.A == id || c.B == id);
    public int GaugeOf(string id) => War.Gauge.TryGetValue(id, out var g) ? g : 0;
    bool Buffed(string kind, string id) => War.Until.TryGetValue(kind + ":" + id, out var t) && Second < t;
    string Side(string house) => house == Rebel ? Rebel : "takano";

    // 本陣：鷹野は大将の陣（大将が崩れていれば城）、岩切は岩切城。
    (long X, long Y, string Name) CampOf(ProtoPerson p)
    {
        if (p.House == Rebel) { var s = Map.Site("iwakiri-castle"); return (s.X * 100L, s.Y * 100L, s.Name); }
        if (War.Assign.TryGetValue(Heir, out var a) && !War.Routed.Contains(Heir)) { var pos = PositionOf(a); return (pos.X * 100L, pos.Y * 100L, pos.Name); }
        var c = Map.Site("takano-castle"); return (c.X * 100L, c.Y * 100L, c.Name);
    }

    // ---- 要所：その場にいるのが一つの家だけなら、その家が持つ。10分ごとに持つ側の総士気が上がり、相手が下がる ----
    void HoldKeypoints()
    {
        foreach (var pos in Map.Data.Positions!.Where(p => p.Kind != ""))
        {
            var here = Units.Where(u => !War.Routed.Contains(u.Id) && Dist(u, pos.X * 100L, pos.Y * 100L) <= 150_00).Select(u => Side(u.House)).Distinct().ToArray();
            if (here.Length == 1 && War.Holder.GetValueOrDefault(pos.Id) != here[0])
            {
                War.Holder[pos.Id] = here[0];
                if (War.Phase == "合戦") Happen($"hold:{pos.Id}:{Second}", $"{HouseName(here[0])}が{pos.Name}を押さえた。", pos.X * 100L, pos.Y * 100L);
            }
            if (War.Phase == "合戦" && Second % 600 == 0 && War.Holder.TryGetValue(pos.Id, out var h))
            { AddSpirit(h, 3); AddSpirit(h == Rebel ? "takano" : Rebel, -3); }
        }
    }
    // まだ戦える：崩れていないか、本陣で立て直せる（兵10以上・まだ一度も立て直していない）。
    bool StillFights(ProtoPerson u) => !War.Routed.Contains(u.Id) || u.Active && u.Soldiers >= 10 && !War.Recovered.Contains(u.Id);
    string HouseName(string side) => side == Rebel ? "岩切勢" : "鷹野勢";
    void AddSpirit(string side, int d)
    {
        if (War.Result != "") return;
        int now = Math.Clamp(SpiritOf(side) + d, 0, 150); War.Spirit[side] = now;
        if (now > 0 || Flag("collapse:" + side)) return;
        Set("collapse:" + side);
        var head = Person(HeadOf(side));
        Happen("collapse:" + side, $"{HouseName(side)}の総士気が尽き、総崩れになった。", head.X, head.Y, head);
        foreach (var u in Units.Where(u => Side(u.House) == side).ToArray()) { if (!War.Recovered.Contains(u.Id)) War.Recovered.Add(u.Id); Rout(u); } // 総崩れの後は立て直さない
    }

    // ---- 1分ごとの打ち合い ----
    // 兵 × 武勇 × 総士気 × 地形（高い所・森・射撃の要所）× 挟み撃ち × 戦法と構え ÷ 相手の統率。
    // 狭い要所では、一度に戦えるのはその幅の数の隊だけ（ほかは控える）。
    void Fight(ProtoPerson[] all)
    {
        if (War.Spirit.Count == 0) { War.Spirit["takano"] = 100; War.Spirit[Rebel] = 100; }
        var hits = new Dictionary<string, double>(); var dealt = new Dictionary<(string, string), double>(); var rear = new HashSet<string>();
        var before = all.ToDictionary(u => u.Id, u => u.Soldiers);
        bool Live(ProtoPerson x) => !War.Routed.Contains(x.Id);
        ProtoPerson? Foe(ProtoPerson a) => all.Where(b => Hostile(a.House, b.House) && Live(b) && Dist(a, b) <= 80_00).OrderBy(b => Dist(a, b)).ThenBy(b => b.Id).FirstOrDefault();
        foreach (var a in all.Where(Live))
        {
            var b = Foe(a); if (b == null) continue;
            var area = Map.Data.Positions!.FirstOrDefault(p => p.Width > 0 && Dist(a, p.X * 100L, p.Y * 100L) <= 200_00);
            if (area != null)
            {
                var line = all.Where(x => x.House == a.House && Live(x) && Dist(x, area.X * 100L, area.Y * 100L) <= 200_00 && Foe(x) != null)
                    .OrderBy(x => Dist(x, Foe(x)!)).ThenBy(x => x.Id).ToList();
                if (line.IndexOf(a) >= area.Width) { a.Doing = $"{area.Name}で控えている"; continue; }
            }
            Set("fought:" + a.Id);
            double atk = a.Soldiers * 0.006 * (a.Valor / 50.0) * (a.Morale / 100.0 + 0.5) * (SpiritOf(Side(a.House)) / 200.0 + 0.5);
            long rise = Map.HeightAtCm(a.X, a.Y) - Map.HeightAtCm(b.X, b.Y);
            if (rise > 10_00) atk *= 1.3; else if (rise < -10_00) atk *= 0.7;
            if (Map.GroundAtCm(b.X, b.Y) == Ground.Forest) atk *= 0.8;
            // 向き：側面1.5倍・背後2倍（Core/Proto/ProtoTactics.cs）。追っていて隊形が崩れた相手は1.25倍。伏せからの奇襲は1.5倍。
            var (flank, kind) = FlankOf(b, a); atk *= flank;
            if (kind != "正面" && !Flag($"flank:{a.Id}:{b.Id}:{kind}")) { Set($"flank:{a.Id}:{b.Id}:{kind}"); Happen($"flank:{a.Id}:{b.Id}:{kind}:{Second}", $"{a.Name}の兵が{b.Name}の{kind}を突いた。", b.X, b.Y, a, b); }
            if (kind == "背後") rear.Add(b.Id);
            if (b.Moving) atk *= 1.25;
            if (Buffed("surprise", a.Id)) atk *= 1.5;
            if (area is { Kind: "射撃" } && Dist(a, area.X * 100L, area.Y * 100L) <= 150_00) atk *= 1.2;
            if (Buffed("charge", a.Id)) atk *= 1.8;
            if (Buffed("kakare", a.Id)) atk *= 1.6;
            if (a.Player) atk *= War.Stance switch { "射撃" => 0.7, "防御" => 0.3, _ => 1.0 };
            if (Buffed("guard", b.Id)) atk *= 0.5;
            if (b.Player) atk *= War.Stance switch { "射撃" => 0.6, "防御" => 0.5, _ => 1.0 };
            atk /= b.Command / 50.0;
            hits[b.Id] = hits.GetValueOrDefault(b.Id) + atk; dealt[(a.Id, b.Id)] = dealt.GetValueOrDefault((a.Id, b.Id)) + atk;
            a.Doing = $"{b.Name}の兵と戦っている";
            War.Gauge[a.Id] = Math.Min(100, GaugeOf(a.Id) + (a.Player ? 12 : 6));
            if (a.Player) War.Ki = Math.Min(100, War.Ki + 3);
        }
        // 押し合い：同じ1分に互いが失った割合の差で動く。押し込まれ続けると士気が下がる。
        foreach (var ((x, y), _) in dealt)
        {
            var (a, b) = Person(x).House == Rebel ? (y, x) : (x, y);
            var c = War.Clashes.FirstOrDefault(k => k.A == a && k.B == b);
            if (c == null)
            {
                c = new ProtoClash { A = a, B = b, Since = Second }; War.Clashes.Add(c);
                // 伏せていた隊（10分以内に伏せていた）が仕掛けたなら奇襲。
                var (att, def) = (Person(x), Person(y));
                if (War.HiddenAt.TryGetValue(att.Id, out var hid) && Second - hid <= 10 * 60 && !Buffed("surprise", att.Id))
                {
                    War.Until["surprise:" + att.Id] = Second + 5 * 60; def.Morale = Math.Max(0, def.Morale - 15); War.HiddenAt.Remove(att.Id);
                    Happen($"surprise:{att.Id}:{Second}", $"{att.Name}の兵が森から{def.Name}の兵へ奇襲をかけた。", def.X, def.Y, att, def);
                }
            }
        }
        foreach (var c in War.Clashes.ToArray())
        {
            if (!dealt.ContainsKey((c.A, c.B)) && !dealt.ContainsKey((c.B, c.A))) { War.Clashes.Remove(c); continue; }
            double toB = dealt.GetValueOrDefault((c.A, c.B)) / Math.Max(1, before.GetValueOrDefault(c.B)), toA = dealt.GetValueOrDefault((c.B, c.A)) / Math.Max(1, before.GetValueOrDefault(c.A));
            c.Push = Math.Clamp(c.Push + (toB - toA) * 900, -100, 100);
            if (c.Push >= 60) Person(c.B).Morale = Math.Max(0, Person(c.B).Morale - 2);
            if (c.Push <= -60) Person(c.A).Morale = Math.Max(0, Person(c.A).Morale - 2);
        }
        Pursue(all, hits);
        foreach (var (id, h) in hits)
        {
            var b = Person(id); int loss = Math.Max(1, (int)Math.Round(h));
            int was = b.Soldiers; b.Soldiers = Math.Max(0, b.Soldiers - loss);
            b.Morale = Math.Max(0, b.Morale - (int)Math.Ceiling(loss * 100.0 / Math.Max(1, was) * 1.0 * (b.Player && War.Stance == "防御" ? 0.5 : 1)) - (rear.Contains(id) ? 3 : 0));
            if (War.Routed.Contains(id) && b.Soldiers == 0 && b.Active && !b.Player) { b.Active = false; b.Doing = "討ち取られた"; Happen($"slain:{id}", $"{b.Name}は追い討ちを受けて討ち取られた。", b.X, b.Y, b); }
        }
        foreach (var u in all.Where(u => Live(u) && (u.Morale < 20 || u.Soldiers == 0)).ToArray()) Rout(u);
        // NPC は戦法のゲージがたまれば自分で使う。
        foreach (var u in all.Where(u => !u.Player && Live(u) && GaugeOf(u.Id) >= 100)) UseTactic(u);
        War.Clashes.RemoveAll(c => War.Routed.Contains(c.A) || War.Routed.Contains(c.B));
    }

    internal void Rout(ProtoPerson b)
    {
        if (War.Routed.Contains(b.Id)) return;
        War.Routed.Add(b.Id); War.Target.Remove(b.Id); War.AtCamp.Remove(b.Id);
        Happen($"rout:{b.Id}:{Second}", $"{b.Name}の兵が崩れて退いた（残り{b.Soldiers}）。", b.X, b.Y, b);
        AddSpirit(Side(b.House), b.Id == HeadOf(Side(b.House)) ? -40 : -15);
        if (CutOff(b)) { Annihilate(b); if (!b.Player) return; }
        var camp = CampOf(b);
        if (b.Player) { Stop(); Note($"兵が崩れた。{camp.Name}の本陣まで退けば立て直せる。"); return; }
        Walk(b, (camp.X, camp.Y), camp.Name, "本陣へ退いている");
    }

    // ---- 立て直し：本陣で30分休むと、兵が10以上残っていれば一度だけ戦いに戻る ----
    void Regroup()
    {
        foreach (var id in War.Routed.ToArray())
        {
            var u = Person(id); if (!u.Active || u.Soldiers < 10 || War.Recovered.Contains(id)) continue;
            var camp = CampOf(u);
            if (Dist(u, camp.X, camp.Y) > 120_00) { War.AtCamp.Remove(id); continue; }
            if (!War.AtCamp.TryGetValue(id, out var since)) { War.AtCamp[id] = Second; continue; }
            if (Second - since < 30 * 60) continue;
            War.Routed.Remove(id); War.Recovered.Add(id); War.AtCamp.Remove(id); u.Morale = 60;
            Happen($"regroup:{id}", $"{u.Name}の兵が{camp.Name}で立て直し、再び戦いに戻った（兵{u.Soldiers}）。", u.X, u.Y, u);
            AddSpirit(Side(u.House), 5);
            if (u.Player) { Note("兵を立て直した。"); continue; }
            if (War.Assign.TryGetValue(id, out var a) && Dist(u, PositionOf(a).X * 100L, PositionOf(a).Y * 100L) > 150_00) MarchTo(u, PositionOf(a));
        }
    }

    // ---- 戦法・構え・かかれ ----
    string UseTactic(ProtoPerson u)
    {
        if (GaugeOf(u.Id) < 100 || War.Routed.Contains(u.Id)) return "まだ使えない";
        War.Gauge[u.Id] = 0; var name = TacticOf(u);
        switch (name)
        {
            case "突撃": War.Until["charge:" + u.Id] = Second + 3 * 60; Shove(u, 20); break;
            case "槍衾": case "堅陣": War.Until["guard:" + u.Id] = Second + 5 * 60; break;
            default: u.Morale = Math.Min(100, u.Morale + 25); Shove(u, 10); break;
        }
        Happen($"tactic:{u.Id}:{Second}", $"{u.Name}が戦法「{name}」を使った。", u.X, u.Y, u);
        return $"戦法「{name}」。";
    }
    void Shove(ProtoPerson u, double d) { var c = ClashOf(u.Id); if (c != null) c.Push = Math.Clamp(c.Push + (c.A == u.Id ? d : -d), -100, 100); }
    string SetStance(string s) { if (!Stances.Contains(s)) return ""; War.Stance = s; return $"構え：{s}"; }
    public bool CanKakare => ClashOf(Player.Id) != null && War.Ki >= 25 && !War.Routed.Contains(Player.Id);
    string Kakare()
    {
        if (!CanKakare) return "今はかかれない";
        War.Ki -= 25; War.Until["kakare:" + Player.Id] = Second + 3 * 60; Shove(Player, 15);
        return "かかれ！";
    }
}
