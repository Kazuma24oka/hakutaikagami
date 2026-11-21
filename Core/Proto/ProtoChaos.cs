namespace HakutaiKagami.Core.Proto;

// 試作版の伏兵・奇襲・混乱と、合戦の勲功（2026-11-20 ユーザー）。
// ・伏兵：敵に見つかっていない隊。森の中は（動いていても）1分、森の外は5分、見つからずにいると伏兵になる。
//   森の中の隊は80m（見る側の知略で増減、物見上手は+30m）まで近づかないと見つからない。森の外は、敵の見える範囲に入れば見つかる。
//   見つかったあと・森を出たあとも、3分の間は伏兵の利が残る。
// ・奇襲：伏兵（か、利が残っている間）の隊が敵にぶつかると起きる。ぶつかった瞬間に3分ぶんの打撃を一度に与え、相手の混乱を30上げる。
// ・混乱：打たれるとたまる（毎分 正面2・側面6・背後10、二隊以上に打たれていれば+4、下がりながら打たれていれば+4。打たれていなければ毎分−5）。
//   たまる量は「打つ側の知略÷打たれる側の知略」倍。100になると3分のあいだ混乱する：動けない・向きを変えられない・戦法を使えない・
//   与える打撃は半分・受ける打撃は1.5倍・士気の下がりは1.5倍。終わると0からたまり直す。気性は計算に使わない。
// ・勲功：役目ごとの見込み（軍議の戦場図に出す）。決まるのは城へ戻ってからの論功行賞（Core/Proto/ProtoHome.cs）。
public sealed partial class ProtoWorld
{
    public const int UnseenForest = 60, UnseenOpen = 5 * 60, AmbushGrace = 3 * 60, ChaosTime = 3 * 60;

    public bool InAmbush(string id) => War.Ambush.Contains(id);
    bool CanSurprise(string id) => InAmbush(id) || War.AmbushUntil.GetValueOrDefault(id) >= Second;
    public int ChaosOf(string id) => War.Chaos.GetValueOrDefault(id);
    public bool Confused(string id) => War.ChaosUntil.GetValueOrDefault(id) > Second;
    // 森の伏兵を見つける遠さ（cm）。
    long SpotRange(ProtoPerson viewer) => 80_00 + (Wit(viewer) - 50) * 1_00 + (viewer.Skills.Contains("物見上手") ? 30_00 : 0);

    // 毎分：見つかっているかを確かめ、伏兵になる・解ける。
    void Stealth(ProtoPerson[] field)
    {
        foreach (var u in field)
        {
            bool live = u.Soldiers > 0 && !War.Routed.Contains(u.Id);
            // 打ち合った隊は、そのあと5分は見つかったまま（打ち合いの切れ目でまた伏兵にならない）。
            if (ClashOf(u.Id) != null) flags["clashed:" + u.Id] = Second;
            bool seen = !live || War.Siege != null && InFoeCastle(u) || Spotted(u.Id) || flags.TryGetValue("clashed:" + u.Id, out var cl) && Second - cl < 5 * 60 || field.Any(e => Hostile(u.House, e.House) && !War.Routed.Contains(e.Id) && InSight(e, u));
            if (seen) War.UnseenSince.Remove(u.Id); else if (!War.UnseenSince.ContainsKey(u.Id)) War.UnseenSince[u.Id] = Second;
            bool now = !seen && Second - War.UnseenSince[u.Id] >= (InForest(u.X, u.Y) ? UnseenForest : UnseenOpen);
            if (now) { if (!InAmbush(u.Id)) War.Ambush.Add(u.Id); }
            else if (War.Ambush.Remove(u.Id) && live) War.AmbushUntil[u.Id] = Second + AmbushGrace;
        }
    }
    // ---- 物見（自動。2026-11-20 ユーザー「自部隊に知略高い武将がいれば自動でたまにとってくる」） ----
    // 隊はいつも周りを探っている。5分ごとに、知略に応じた確率で、少し遠く（ScoutFar。物見上手は+100m）の森の中の敵に気づく。
    // 気づかれた隊は5分のあいだ森の中でも隠れられず、伏兵も解ける。味方が見つけた敵は、知らせが届いてから印になる。
    public const long ScoutFar = 250_00;
    public int ScoutChance(ProtoPerson p) => Math.Clamp(Wit(p) - 30, 0, 60);
    bool Spotted(string id) => flags.TryGetValue("spotted:" + id, out var at) && Second - at < 5 * 60;
    void AutoScout(ProtoPerson[] field)
    {
        if (Second % 300 != 0) return;
        foreach (var v in field.Where(v => v.Soldiers > 0 && !War.Routed.Contains(v.Id) && !Confused(v.Id)).OrderBy(v => v.Id))
        {
            long far = ScoutFar + (v.Skills.Contains("物見上手") ? 100_00 : 0);
            foreach (var t in field.Where(t => Hostile(v.House, t.House) && !War.Routed.Contains(t.Id) && Hidden(t) && !InSight(v, t) && Dist(v, t) <= far).OrderBy(t => t.Id))
            {
                if (Roll($"scout:{v.Id}:{t.Id}") % 100 >= ScoutChance(v)) continue;
                flags["spotted:" + t.Id] = Second; FoundByScout(t.Id);
                Happen($"spotted:{t.Id}:{Second}", $"{v.Name}の物見が、森に潜む{t.Name}の兵を見つけた。", v.X, v.Y, v);
                if (Side(v.House) == "takano")
                {
                    if (v.Player) Mark(new ProtoMarker(t.Id, t.X, t.Y, Second, "物見"));
                    else Reports.Add(new ProtoReport(t.Id, t.X, t.Y, Second, Second + Dist(v, Player) / 200, v.Name));
                }
                else if (t.Player) Note("敵の物見に見つかった。");
            }
        }
    }
    // 物見に見られた隊は見つかる。
    void FoundByScout(string id)
    {
        War.UnseenSince.Remove(id);
        if (War.Ambush.Remove(id)) War.AmbushUntil[id] = Second + AmbushGrace;
    }

    // 混乱をためる。by：打った側（知略の比で増減）。
    void AddChaos(ProtoPerson b, int amount, ProtoPerson? by)
    {
        if (amount == 0 || Confused(b.Id) || War.Routed.Contains(b.Id) || b.Soldiers <= 0 || Sheltered(b)) return; // 城内の隊は混乱しない
        int add = amount > 0 ? Math.Max(1, (int)Math.Round(amount * (double)(by != null ? Wit(by) : 50) / Wit(b))) : amount;
        int now = Math.Clamp(ChaosOf(b.Id) + add, 0, 100); War.Chaos[b.Id] = now;
        if (now < 100) return;
        War.Chaos[b.Id] = 0; War.ChaosUntil[b.Id] = Second + ChaosTime;
        Happen($"chaos:{b.Id}:{Second}", $"{b.Name}の兵が混乱した。", b.X, b.Y, b);
        if (b.Player) Note("兵が混乱した。しばらく動けない。");
    }
    void ClearChaos(string id) { War.Chaos.Remove(id); War.ChaosUntil.Remove(id); War.Ambush.Remove(id); War.AmbushUntil.Remove(id); War.UnseenSince.Remove(id); }

    // ---- 勲功 ----
    public static int RoleMerit(string role) => role switch { "囮" or "先手" or "正面" => 12, "伏兵" or "回り込み" => 10, _ => 6 };
}
