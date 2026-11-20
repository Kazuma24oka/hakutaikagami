namespace HakutaiKagami.Core.Proto;

// 試作版の合戦のステータスの役割と、兵種・戦法・技能（2026-11-20 ユーザー）。
// ・統率＝総勢の行軍の速さ・受ける打撃を減らす・士気の減りを抑える。武勇＝与える打撃・士気の戻り（鼓舞・立て直し）。
//   知略＝混乱のたまり方（打つ側÷打たれる側）・視野・物見。気性（勇猛・臨機・慎重）は AI の動きにだけ使い、打撃や混乱の計算には使わない。
// ・忠誠＝能力の上げ下げ。データの能力値は「申告の値」で、実際の働きは 1＋（忠誠−50）÷500 倍（0.9〜1.1）。
//   当主（自分の家の主）と本人は、そのままの値。
// ・兵種：隊ごとに一つ（まず足軽と騎馬）。騎馬は野と道で速く（合戦中1.3倍）打撃も強い（1.2倍）が、森と丘では弱い（0.7倍）。
// ・戦法：一人一つ。兵種が合うときだけ使える。騎突＝騎馬の一度きりの打撃（武勇と兵の数で決まる。森の中では使えない）、
//   槍衾＝足軽の続く打撃（5分間1.8倍）、堅陣＝5分間受ける打撃を半分。戦法が無い・使えない者は鼓舞（士気を戻す）。
// ・技能：いくつでも持てる。兵種と同じ名前の技能（適性）を持つと、その兵種の隊を率いたときの打撃が1.15倍、戦法の威力が上がる。
public sealed partial class ProtoWorld
{
    public static readonly string[] Troops = ["足軽", "騎馬"];

    double Zeal(ProtoPerson p) => p.Player || !p.Samurai || HeadOf(p.House) == p.Id ? 1.0 : Math.Clamp(1 + (p.Loyalty - 50) / 500.0, 0.9, 1.1);
    // 実際の働き（忠誠で上下する）。画面に出す数字は申告の値（p.Command など）。
    public int Cmd(ProtoPerson p) => Math.Max(1, (int)Math.Round(p.Command * Zeal(p)));
    public int Val(ProtoPerson p) => Math.Max(1, (int)Math.Round(p.Valor * Zeal(p)));
    public int Wit(ProtoPerson p) => Math.Max(1, (int)Math.Round(p.Wits * Zeal(p)));

    public static bool Apt(ProtoPerson p) => p.Skills.Contains(p.Troop);
    // 戦法：兵種が合うときだけ。合わなければ鼓舞。
    public static string TacticOf(ProtoPerson p) => p.Tactic switch
    {
        "騎突" when p.Troop == "騎馬" => "騎突",
        "槍衾" when p.Troop == "足軽" => "槍衾",
        "堅陣" => "堅陣",
        _ => "鼓舞",
    };
    // 兵種と地形で、与える打撃が変わる。
    double TroopAttack(ProtoPerson a)
    {
        double k = Apt(a) ? 1.15 : 1.0;
        if (a.Troop != "騎馬") return k;
        return k * (TerrainName(a.X, a.Y) is "森" or "丘" ? 0.7 : 1.2);
    }
    // 騎馬は、合戦の間、野と道で速い（総勢で行軍している間は大将の統率で決まる速さのまま）。
    int TroopSpeed(ProtoPerson p) => p.Troop == "騎馬" && p.Soldiers > 0 && War.Phase == "合戦" && !InBody(p) && !War.Routed.Contains(p.Id) && TerrainName(p.X, p.Y) is "野" or "道" ? 130 : 100;

    // 騎突：ぶつかっている相手（いなければ打ち合える所のいちばん近い敵）へ、一度きりの打撃。
    bool Charge(ProtoPerson u)
    {
        if (InForest(u.X, u.Y)) return false;
        var c = ClashOf(u.Id); var foe = c != null ? Person(c.A == u.Id ? c.B : c.A) : null;
        foe ??= Units.Where(e => Hostile(u.House, e.House) && !War.Routed.Contains(e.Id) && Dist(u, e) <= Reach).OrderBy(e => Dist(u, e)).ThenBy(e => e.Id).FirstOrDefault();
        if (foe == null || BehindGate(foe)) return false;
        double blow = u.Soldiers * 0.03 * (Val(u) / 50.0) * (Apt(u) ? 1.3 : 1.0) * FlankOf(foe, u).Item1 / (Cmd(foe) / 50.0);
        if (Buffed("guard", foe.Id)) blow *= 0.5;
        int was = foe.Soldiers, loss = Math.Min(was, Math.Max(1, (int)Math.Round(blow)));
        foe.Soldiers -= loss; foe.Morale = Math.Max(0, foe.Morale - (int)Math.Ceiling(loss * 100.0 / Math.Max(1, was) * Math.Clamp(50.0 / Cmd(foe), 0.6, 1.4)));
        War.Hits.Add(new ProtoHit { To = foe.Id, Loss = loss, Kind = "騎突", At = Second });
        AddChaos(foe, 20, u);
        if (foe.Morale < 20 || foe.Soldiers == 0) Rout(foe);
        return true;
    }
}
