namespace HakutaiKagami.Core.Proto;

// 試作版の本陣（2026-11-21 ユーザー）。野戦も攻城も、軍には本陣がある。
// ・場所：城が近い側は城が本陣（岩切勢は岩切城）。遠征する側（鷹野勢）は、軍議で決めた所に本陣を置く。総勢はまず本陣の地へ進み、着いた時から本陣になる
//   （着くまでは、出てきた城が本陣の扱い）。攻城になれば、城の前の陣へ本陣を進める。
// ・兵糧は本陣に置く。指示と兵糧は、本陣から CampRange（1,000m）まで届く。
//   指示が届かない隊：役目の動き・退き貝を受け取れない（最後の指図のまま。勇猛な者は見えた敵に自分で当たる）。退き貝は、届く所に戻るか半刻たつと気づく。
//   兵糧が届かない隊：手持ちの兵糧で2日は保つ（2026-11-21 ユーザー「範囲外にでた兵も数日は士気と兵力を保てる」）。それを過ぎると、10分ごとに士気が1下がり、30分ごとに兵が2％（最低1）減る。
//   本陣の兵糧が無いとき（尽きた・取られた）は、どの隊も補給を受けられず、手持ちの半日ぶんを過ぎると減り始める。
// ・敗走：崩れた隊は本陣（城）へ退いて収容される。半刻休むと、残った兵の7割で戦いに戻る（何度でも。兵が10以上残っていれば）。
// ・本陣を取られる：敵の隊が本陣（60m）に踏み込み、味方の隊がそば（150m）にいないと取られる。兵糧を失い、収容していた兵は半分が散り、全軍士気が30下がる。
//   そのあとは、指示は総大将の隊から届き、崩れた隊は出てきた城まで退く。劣勢の岩切勢は、手薄な本陣が見えると突きに来る。
// ・軍議は本陣で行う：野戦の軍議は出陣の前の城で、攻城の軍議は本陣（300m）で。
public sealed partial class ProtoWorld
{
    public const long CampRange = 1000_00;
    // 隊が手持ちで保つ時間：範囲の外に出てから2日。本陣に兵糧が無いときは半日。
    public const long Carried = 2 * Day, CarriedNoStock = 12 * Hour;

    // 本陣の場所（まだ置いていなければ null）。岩切勢は岩切城。
    public (long X, long Y)? CampAt(string side)
    {
        if (side == Rebel) { if (!Flag("rebellion")) return null; var c = Map.Site(FoeCastle); return (c.X * 100L, c.Y * 100L); }
        return Flag("camp-set") && War.Camp.TryGetValue("takano", out var at) ? (at[0], at[1]) : null;
    }
    // 軍議で決めた、本陣を置く所（置く前でも分かる。画面用）。
    public (long X, long Y)? CampPlan => War.Camp.TryGetValue("takano", out var at) ? (at[0], at[1]) : null;
    public bool CampLost(string side) => Flag("camp-lost:" + side);
    public int StockOf(string side) => side == Rebel ? War.Siege?.Food ?? 3 : War.Siege?.Supply ?? War.Stock.GetValueOrDefault("takano", 3);

    // 指示が届くか：本陣から CampRange。本陣を取られたあとは、総大将の隊から。本陣を置く前は、いつでも届く。
    public bool Ordered(ProtoPerson u)
    {
        string side = Side(u.House); if (CampAt(side) is not { } c) return true;
        if (CampLost(side)) { var head = Person(HeadOf(side)); return head.Active && Dist(u, head) <= CampRange; }
        return Dist(u, c.X, c.Y) <= CampRange;
    }
    // 兵糧が届くか：本陣に兵糧があり、本陣から CampRange。
    public bool Supplied(ProtoPerson u)
    {
        string side = Side(u.House); if (CampAt(side) is not { } c) return true;
        return !CampLost(side) && StockOf(side) > 0 && Dist(u, c.X, c.Y) <= CampRange;
    }

    // 手持ちの兵糧の残り（秒）。届いている間は null。
    public long? CarriedLeft(ProtoPerson u)
    {
        if (Supplied(u)) return null; string side = Side(u.House); bool none = CampLost(side) || StockOf(side) <= 0;
        return Math.Max(0, (none ? CarriedNoStock : Carried) - (Second - War.OutSince.GetValueOrDefault(u.Id, Second)));
    }
    // 毎分（行軍と合戦の間）：本陣を置く・進める、兵糧の届かない隊が痩せる、本陣を取られる、劣勢の岩切勢が本陣を突く。
    void Camps(ProtoPerson[] takano, ProtoPerson[] rebels)
    {
        var heir = Person(Heir);
        // 総勢が本陣の地に着いたら、本陣を置く。
        if (!Flag("camp-set") && War.Camp.TryGetValue("takano", out var plan) && War.Split)
        { Set("camp-set"); Happen("camp-set", $"{heir.Name}勢は本陣を置いた。", plan[0], plan[1], [.. People.Where(u => InArmy(u) && u.Active)]); }
        // 攻城になって城の前に陣を敷いたら、本陣をそこへ進める。
        if (War.Siege is { CampAt: > 0 } && Flag("camp-set") && !CampLost("takano") && !Flag("camp-moved"))
        { Set("camp-moved"); var sc = SiegeCamp; War.Camp["takano"] = [sc.X, sc.Y]; Happen("camp-moved", $"{heir.Name}勢は本陣を岩切城の前へ進めた。", sc.X, sc.Y, [.. People.Where(u => InArmy(u) && u.Active)]); }

        // 兵糧の届かない隊は、じわじわ士気と兵が減る（10分ごと）。城内の隊は、城の兵糧の決まり（Core/Proto/ProtoSiege.cs）。
        foreach (var u in takano.Concat(rebels).Where(u => u.Soldiers > 0 && !Sheltered(u)))
        {
            if (Supplied(u)) { War.OutSince.Remove(u.Id); continue; }
            if (!War.OutSince.TryGetValue(u.Id, out var since)) { War.OutSince[u.Id] = since = Second; }
            string side = Side(u.House); bool none = CampLost(side) || StockOf(side) <= 0;
            if (Second % 600 != 0 || Second - since < (none ? CarriedNoStock : Carried)) continue;
            u.Morale = Math.Max(0, u.Morale - 1); if (Second % 1800 == 0) u.Soldiers = Math.Max(0, u.Soldiers - Math.Max(1, u.Soldiers / 50));
            if (!Flag("hungry:" + u.Id)) { Set("hungry:" + u.Id); Happen("hungry:" + u.Id, $"{u.Name}の隊は兵糧が届かず、兵が痩せ始めた。", u.X, u.Y, u); if (u.Player) Note("兵糧が届かない。兵が痩せ始めた。"); }
        }

        // 本陣を取られる（鷹野の本陣。岩切の本陣は城なので、落城・開城がそれに当たる）。
        if (CampAt("takano") is { } camp && !CampLost("takano"))
        {
            var taker = rebels.Where(r => !War.Routed.Contains(r.Id) && !Sheltered(r) && Dist(r, camp.X, camp.Y) <= 60_00).OrderBy(r => r.Id).FirstOrDefault();
            bool guarded = People.Any(u => InArmy(u) && u.Active && u.Soldiers > 0 && !War.Routed.Contains(u.Id) && Dist(u, camp.X, camp.Y) <= 150_00);
            if (taker != null && !guarded)
            {
                Set("camp-lost:takano"); War.Stock["takano"] = 0; if (War.Siege != null) War.Siege.Supply = 0;
                foreach (var u in People.Where(u => InArmy(u) && War.Routed.Contains(u.Id) && Dist(u, camp.X, camp.Y) <= 150_00)) { u.Soldiers /= 2; War.AtCamp.Remove(u.Id); }
                Happen("camp-lost:takano", $"{taker.Name}の兵が鷹野勢の本陣を奪った。兵糧は失われた。", camp.X, camp.Y, [.. People.Where(u => InArmy(u) && u.Active), taker]);
                AddSpirit("takano", -30); AddSpirit(Rebel, 10); flags.Remove("raid:" + taker.Id);
            }
            // 劣勢の岩切勢は、手薄な本陣が見えていれば突きに来る（一隊だけ）。
            else if (War.Phase == "合戦" && SpiritOf(Rebel) < SpiritOf("takano") && !guarded && !rebels.Any(r => Flag("raid:" + r.Id)))
            {
                var raider = rebels.Where(r => !War.Routed.Contains(r.Id) && !Sheltered(r) && !Withdrawing(r) && ClashOf(r.Id) == null && r.Temper != "慎重" && Dist(r, camp.X, camp.Y) <= SightOf(r) * 2).OrderBy(r => Dist(r, camp.X, camp.Y)).ThenBy(r => r.Id).FirstOrDefault();
                if (raider != null) { Set("raid:" + raider.Id); War.Target.Remove(raider.Id); Walk(raider, camp, "鷹野の本陣", "鷹野の本陣を突きに向かっている"); Happen("raid:" + raider.Id, $"{raider.Name}の兵が、手薄な鷹野の本陣へ向かった。", raider.X, raider.Y, raider); }
            }
        }
    }
}
