namespace HakutaiKagami.Core.Proto;

// 試作版の合戦の作り直し（2026-11-19 ユーザー「今の戦闘は何をやっているかよく分からない」→ 組み合わせ案で試作）。Docs/CORE_LOOP.md。
// 骨組み：要所と総士気（新生・戦国下天禄の勝ち負けの決まり）。陣を持つ側の総士気が上がり、隊が崩れると下がる。0で総崩れ。
// 見え方：ぶつかった隊どうしの押し合い（HoI4 の戦いのゲージ）と、狭い所では一度に戦える隊の数が限られる（戦場の幅）。
// 手応え：本人の隊がぶつかっている間だけ、構え（白兵・射撃・防御）・戦法・「かかれ」（気力を使う一押し、半熟英雄のボタン）。
// 立て直し：崩れた隊は本陣へ戻り、しばらく休むと一度だけ戦いに戻れる（戦国下天禄の再出撃）。
// その1分に受けた打撃（地図に数字で出す）。Kind はいちばん悪い当たり方（正面・側面・背後）、奇襲なら Surprise。
public sealed class ProtoHit
{
    public string To { get; set; } = "";
    public int Loss { get; set; }
    public string Kind { get; set; } = "正面";
    public bool Surprise { get; set; }
    public long At { get; set; }
}
public sealed class ProtoClash
{
    public string A { get; set; } = "";   // 鷹野方
    public string B { get; set; } = "";   // 岩切方
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
        if (War.Post.ContainsKey(Heir) && !War.Routed.Contains(Heir)) { var pos = PostOf(Heir); return (pos.X * 100L, pos.Y * 100L, pos.Name); }
        var c = Map.Site("takano-castle"); return (c.X * 100L, c.Y * 100L, c.Name);
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
        var hits = new Dictionary<string, double>(); var dealt = new Dictionary<(string, string), double>(); var rear = new HashSet<string>(); var side = new HashSet<string>(); var shaken = new HashSet<string>();
        War.Hits.Clear();
        foreach (var u in all) if (!War.StartSoldiers.ContainsKey(u.Id)) War.StartSoldiers[u.Id] = u.Soldiers;
        var before = all.ToDictionary(u => u.Id, u => u.Soldiers);
        bool Live(ProtoPerson x) => !War.Routed.Contains(x.Id);
        ProtoPerson? Foe(ProtoPerson a) => all.Where(b => Hostile(a.House, b.House) && Live(b) && Dist(a, b) <= Reach).OrderBy(b => Dist(a, b)).ThenBy(b => b.Id).FirstOrDefault();
        foreach (var a in all.Where(Live))
        {
            var b = Foe(a); if (b == null) continue;
            if (Retreating(a)) continue; // 下がっている隊は打ち返さない（打たれるだけ）
            Set("fought:" + a.Id);
            double atk = a.Soldiers * 0.006 * (a.Valor / 50.0) * (a.Morale / 100.0 + 0.5) * (SpiritOf(Side(a.House)) / 200.0 + 0.5);
            long rise = Map.HeightAtCm(a.X, a.Y) - Map.HeightAtCm(b.X, b.Y);
            if (rise > 10_00) atk *= 1.3; else if (rise < -10_00) atk *= 0.7;
            if (Map.GroundAtCm(b.X, b.Y) == Ground.Forest) atk *= 0.8;
            // 向き：側面1.5倍・背後2倍（Core/Proto/ProtoTactics.cs）。追っていて隊形が崩れた相手は1.25倍。伏せからの奇襲は1.5倍。
            var (flank, kind) = FlankOf(b, a); atk *= flank;
            if (kind != "正面" && !Flag($"flank:{a.Id}:{b.Id}:{kind}")) { Set($"flank:{a.Id}:{b.Id}:{kind}"); Happen($"flank:{a.Id}:{b.Id}:{kind}:{Second}", $"{a.Name}の兵が{b.Name}の{kind}を突いた。", b.X, b.Y, a, b); }
            if (kind == "背後") rear.Add(b.Id); else if (kind == "側面") side.Add(b.Id);
            if (Buffed("surprise", a.Id)) shaken.Add(b.Id);
            if (b.Moving) atk *= Retreating(b) ? 1.5 : 1.25; // 下がる隊は打たれやすい
            if (a.Moving) atk *= 0.4; // 歩きながら（追いながら）では打ちにくい
            if (Buffed("surprise", a.Id)) atk *= 1.5;
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
        // ぶつかり合いの記録（誰と誰が打ち合っているか）。
        foreach (var ((x, y), _) in dealt)
        {
            var (a, b) = Person(x).House == Rebel ? (y, x) : (x, y);
            var c = War.Clashes.FirstOrDefault(k => k.A == a && k.B == b);
            if (c == null)
            {
                c = new ProtoClash { A = a, B = b, Since = Second }; War.Clashes.Add(c);
                // 伏せていた隊（10分以内に伏せていた）が仕掛けたなら奇襲。
                // どちらが先に打ったかに関わらず、伏せていた側が仕掛けた側。
                var (att, def) = War.HiddenAt.ContainsKey(y) && !War.HiddenAt.ContainsKey(x) ? (Person(y), Person(x)) : (Person(x), Person(y));
                if (War.HiddenAt.TryGetValue(att.Id, out var hid) && Second - hid <= 5 * 60 && !Buffed("surprise", att.Id))
                {
                    War.Until["surprise:" + att.Id] = Second + 5 * 60; def.Morale = Math.Max(0, def.Morale - 30); War.HiddenAt.Remove(att.Id);
                    Happen($"surprise:{att.Id}:{Second}", $"{att.Name}の兵が森から{def.Name}の兵へ奇襲をかけた。", def.X, def.Y, att, def);
                }
            }
        }
        foreach (var c in War.Clashes.ToArray())
        {
            if (!dealt.ContainsKey((c.A, c.B)) && !dealt.ContainsKey((c.B, c.A))) { War.Clashes.Remove(c); continue; }
        }
        Pursue(all, hits);
        foreach (var (id, h) in hits)
        {
            var b = Person(id); int loss = Math.Max(1, (int)Math.Round(h));
            int was = b.Soldiers; b.Soldiers = Math.Max(0, b.Soldiers - loss);
            War.Hits.Add(new ProtoHit { To = id, Loss = was - b.Soldiers, Kind = War.Routed.Contains(id) ? "追い討ち" : rear.Contains(id) ? "背後" : side.Contains(id) ? "側面" : "正面", Surprise = shaken.Contains(id), At = Second });
            b.Morale = Math.Max(0, b.Morale - (int)Math.Ceiling(loss * 100.0 / Math.Max(1, was) * 1.0 * (b.Player && War.Stance == "防御" ? 0.5 : 1)) - (rear.Contains(id) ? 5 : side.Contains(id) ? 2 : 0) - (shaken.Contains(id) ? 4 : 0)); // 背後5・側面2・奇襲を受けている間4、毎分さらに下がる
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
            if (War.Post.ContainsKey(id) && Dist(u, PostOf(id).X * 100L, PostOf(id).Y * 100L) > 150_00) MarchTo(u, PostOf(id));
        }
    }

    // ---- 戦法・構え・かかれ ----
    string UseTactic(ProtoPerson u)
    {
        if (GaugeOf(u.Id) < 100 || War.Routed.Contains(u.Id)) return "まだ使えない";
        War.Gauge[u.Id] = 0; var name = TacticOf(u);
        switch (name)
        {
            case "突撃": War.Until["charge:" + u.Id] = Second + 3 * 60; break;
            case "槍衾": case "堅陣": War.Until["guard:" + u.Id] = Second + 5 * 60; break;
            default: u.Morale = Math.Min(100, u.Morale + 25); break;
        }
        Happen($"tactic:{u.Id}:{Second}", $"{u.Name}が戦法「{name}」を使った。", u.X, u.Y, u);
        return $"戦法「{name}」。";
    }
    string SetStance(string s) { if (!Stances.Contains(s)) return ""; War.Stance = s; return $"構え：{s}"; }
    public bool CanKakare => ClashOf(Player.Id) != null && War.Ki >= 25 && !War.Routed.Contains(Player.Id);
    string Kakare()
    {
        if (!CanKakare) return "今は強攻できない";
        War.Ki -= 25; War.Until["kakare:" + Player.Id] = Second + 3 * 60;
        return "強攻をかけた。";
    }
}
