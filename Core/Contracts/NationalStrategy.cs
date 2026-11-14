using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

// 全国の家の戦略 V1（BD-01・BD-03、2026-11-12）。版（NewGameState.StrategyPolicy）のある全国開始の世界だけで動く。
// 月の初めに、操作する人物の家でない各家の当主が、城の移動網で境を接する家との強さ（兵の上限×今の城の数の割合）を比べて出陣を決める。
// 史実の事件は、前提（当事者の家と当主・境の城）がそろったときに当主がその判断を選びやすくなる重みで、結果は強制しない。
// 遠方の出陣は翌月の初めに簡単な計算で決着し、勝てばその城の支配が移る。結果は境の城から当事者の当主へ書状で届く。
// 操作する人物の家への出陣と、操作する人物から30km以内の城への出陣は、実際の軍勢の工程まで行わない。
public sealed record StrategyCampaign(EntityId Id,EntityId Attacker,EntityId Defender,EntityId Target,long At,long Revision,string Basis)
{
    // 実際の軍勢で行う出陣（操作する人物の近くの城・本人の家への出陣。一覧の StrategyRealArmies）。攻める家の当主が敵対を表明し、NPC の軍が目的の城へ出陣し、着いて留まり終えたときに決着する。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public bool? Real {get;init;}
}
public sealed record StrategyOutcome(EntityId Campaign,long At,long Revision,bool Captured)
{
    // 守る側の同盟の家で、援軍に出た家（同盟の義務）。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public EntityId[]? Allies {get;init;}
    // 遠方の合戦の計算（StrategyBattles、W4）。強さの比のくじで決めた旧保存では持たない。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public StrategyBattle? Battle {get;init;}
    // 実際の軍勢の出陣で、退いた攻め手の軍の大将を守る側が捕らえたとき（一覧の StrategyCapture）。拘束は捕縛の記録と同じに扱う（BattleCaptivity）。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public BattleCapture? Capture {get;init;}
    // 再興の挙兵を後援した家（一覧の RestorationSupport）。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public EntityId[]? Supporters {get;init;}
}
// 史実の出陣のその月の結果：fired（出陣した）・premise（前提が崩れて不発）・declined（前提はそろったが当主が選ばなかった）。
public sealed record StrategyHistoryRecord(string Event,long At,long Revision,string Result);
// 家どうしの約束：alliance（同盟。共通の強い隣への備え）・truce（和睦。将軍の仲裁）。破られたら Broken に時刻。
public sealed record StrategyPact(EntityId Id,EntityId A,EntityId B,string Kind,long From,long Until,long Revision,string Basis)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? Broken {get;init;}
}
// 京の主：山城の城の過半を押さえた家（上洛）。押さえる家が変わった月に記録する。
public sealed record StrategyCapital(EntityId House,long At,long Revision);
// 包囲網：同じ強い家を相手にした同盟が三つ以上重なったときの、その家への包囲網（加わる家）。
public sealed record StrategyCoalition(EntityId Id,EntityId Target,EntityId[] Members,long At,long Revision)
{
    // 操作する人物が当主の家で、包囲する相手と境を接する家への誘い（書状の ID）。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public StrategyInvite[]? Invites {get;init;}
}
// 包囲網への誘い。答え（Joined）は当主が書状を開いた後に一度だけ。返書が誘った家の当主へ届く。
public sealed record StrategyInvite(EntityId House,EntityId Head,EntityId Letter,EntityId Sender)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public bool? Joined {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? AnsweredAt {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? AnsweredRevision {get;init;}
}
// 将軍：vacant（将軍家が城をすべて失った）・installed（京の主が将軍家の成人を擁立し、山城の城を一つ渡した）。
public sealed record StrategyShogun(string Kind,EntityId? Patron,EntityId? Shogun,EntityId? Site,long At,long Revision);
public sealed record StrategySuccession(EntityId House,EntityId Previous,EntityId Heir,long At,long Revision);
// 家の滅亡：最後の城を失った家と、その城・落とした家（StrategyRestoration の版だけ）。
public sealed record StrategyFall(EntityId House,EntityId Site,EntityId By,long At,long Revision);
// 従属の離反：従っていた家（DBの Overlord）から離れた家（StrategyDefection の版だけ）。
public sealed record StrategyDefection(EntityId House,EntityId From,long At,long Revision);
public sealed record StrategyState(int Schema,string Policy,StrategyCampaign[] Campaigns,StrategyOutcome[] Outcomes)
{
    // 当主が亡くなった家の家督（全国の家の戦略の版だけ）。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public StrategySuccession[]? Successions {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public StrategyHistoryRecord[]? History {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public StrategyPact[]? Pacts {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public StrategyCapital[]? Capitals {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public StrategyShogun[]? Shoguns {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public StrategyCoalition[]? Coalitions {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public StrategyFall[]? Falls {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public StrategyDefection[]? Defections {get;init;}
}
public sealed record HistoricalCampaign(string Id,string Name,string Attacker,string Defender,string? AttackerHead,int Year,int Month,string Source);

public static class NationalStrategy
{
    public const string Policy="national-strategy-v1";
    public const string LetterKind="strategy-outcome";
    public const string Border="border";
    public static bool Enabled(ContractWorld w)=>w.NewGame?.StrategyPolicy==Policy;
    public static void ValidatePolicy(NewGameState data){if(data.StrategyPolicy is not(null or Policy))throw new InvalidDataException("全国の戦略の版が不正です");if(data.StrategyPolicy!=null&&!WorldNetwork.Castle(data))throw new InvalidDataException("全国の戦略は城の移動網の世界だけで使えます");}

    // 史実の出陣（公知の通説。年と月は事件の起きた頃）。DBの家と、そのとき出陣を決めた当主（当主が代わっている見込みの後年の事件は条件にしない）。
    public static readonly HistoricalCampaign[] History=[
        new("okehazama-1560","尾張への出陣（桶狭間の戦い）","G-000123","G-000131","P-005312",1560,5,"通説：永禄3年5月、今川義元が尾張へ出陣"),
        new("odawara-1561","小田原城攻め","G-000134","G-000104","P-005249",1561,3,"通説：永禄4年3月、長尾景虎が小田原城を囲む"),
        new("kawanakajima-1561","第四次川中島の戦い","G-000134","G-000106","P-005249",1561,9,"通説：永禄4年9月"),
        new("mino-1567","稲葉山城の攻略","G-000131","G-000117","P-005235",1567,8,"通説：永禄10年8月、織田信長が稲葉山城を落とす"),
        new("kannonji-1568","上洛と観音寺城の攻略","G-000131","G-000150","P-005235",1568,9,"通説：永禄11年9月"),
        new("suruga-1568","駿河侵攻","G-000106","G-000123","P-005247",1568,12,"通説：永禄11年12月、武田信玄が駿河へ攻め込む"),
        new("gassantoda-1565","月山富田城攻め","G-000188","G-000196","P-005270",1565,4,"通説：永禄8年4月から包囲、9年11月に開城"),
        new("anegawa-1570","浅井攻め（姉川の戦い）","G-000131","G-000152","P-005235",1570,6,"通説：元亀元年6月"),
        new("ichijodani-1573","朝倉攻め","G-000131","G-000147","P-005235",1573,8,"通説：天正元年8月、一乗谷が落ちる"),
        new("odani-1573","小谷城攻め","G-000131","G-000152","P-005235",1573,8,"通説：天正元年8月から9月"),
        new("koshu-1582","甲州征伐","G-000131","G-000106","P-005235",1582,2,"通説：天正10年2月"),
        new("mimikawa-1578","日向への出陣（耳川の戦い）","G-000210","G-000236","P-005322",1578,10,"通説：天正6年10月から11月"),
        new("okitanawate-1584","島原への出陣（沖田畷の戦い）","G-000236","G-000223",null,1584,3,"通説：天正12年3月"),
        new("takaya-1560","河内への出陣（高屋城）","G-000199","G-000160","P-004257",1560,10,"通説：永禄3年10月、三好長慶が河内を平定"),
        new("eiroku-1565","永禄の変","G-000199","G-000154",null,1565,5,"通説：永禄8年5月、三好方が将軍足利義輝を討つ"),
        new("moribe-1561","美濃への出陣（森部の戦い）","G-000131","G-000117","P-005235",1561,5,"通説：永禄4年5月"),
        new("shogunjizo-1561","京への出陣（将軍山城）","G-000150","G-000199",null,1561,7,"通説：永禄4年7月、六角が三好を攻める"),
        new("moji-1561","門司城の戦い","G-000188","G-000210","P-005270",1561,10,"通説：永禄4年10月"),
        new("kyokoji-1562","教興寺の戦い","G-000199","G-000160",null,1562,5,"通説：永禄5年5月"),
        new("kokufudai-1564","第二次国府台の戦い","G-000104","G-000080",null,1564,1,"通説：永禄7年1月"),
        new("kakegawa-1568","遠江への出陣（掛川城）","G-000126","G-000123",null,1568,12,"通説：永禄11年12月、徳川家康が遠江へ攻め込む"),
        new("ogochi-1569","伊勢への出陣（大河内城）","G-000131","G-000167","P-005235",1569,8,"通説：永禄12年8月"),
        new("imayama-1570","今山の戦い","G-000210","G-000223",null,1570,8,"通説：元亀元年8月"),
        new("ishiyama-1570","石山合戦","G-000131","G-000242","P-005235",1570,9,"通説：元亀元年9月から"),
        new("kizakibaru-1572","木崎原の戦い","G-000235","G-000236",null,1572,5,"通説：元亀3年5月"),
        new("mikatagahara-1572","遠江への出陣（三方ヶ原の戦い）","G-000106","G-000126","P-005247",1572,10,"通説：元亀3年10月から12月"),
        new("iwamura-1572","岩村城攻め","G-000106","G-000131","P-005247",1572,11,"通説：元亀3年11月"),
        new("nagashima-1574","長島への出陣","G-000131","G-000242","P-005235",1574,7,"通説：天正2年7月"),
        new("shimantogawa-1575","四万十川の戦い","G-000206","G-000207",null,1575,9,"通説：天正3年9月"),
        new("nanao-1577","七尾城攻め","G-000134","G-000143","P-005249",1577,9,"通説：天正5年9月"),
        new("tedorigawa-1577","手取川の戦い","G-000134","G-000131","P-005249",1577,9,"通説：天正5年9月"),
        new("tottori-1581","鳥取城攻め","G-000131","G-000188","P-005235",1581,6,"通説：天正9年6月から"),
        new("takamatsu-1582","備中高松城攻め","G-000131","G-000188","P-005235",1582,4,"通説：天正10年4月から"),
        new("suriagehara-1589","摺上原の戦い","G-000037","G-000040",null,1589,6,"通説：天正17年6月"),
    ];

    static readonly Lazy<Dictionary<string,string[]>> adjacency=new(BuildAdjacency);
    static Dictionary<string,string[]> BuildAdjacency()
    {
        var castles=CastleRoutes.Catalog.Nodes.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
        var edges=CastleRoutes.Edges.SelectMany(e=>new[]{(e.From,e.To),(e.To,e.From)}).GroupBy(x=>x.Item1,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Item2).Distinct(StringComparer.Ordinal).ToArray(),StringComparer.Ordinal);
        var result=new Dictionary<string,string[]>(StringComparer.Ordinal);
        foreach(var c in castles)
        {
            var near=new HashSet<string>(StringComparer.Ordinal);
            foreach(var n in edges.GetValueOrDefault(c)??[])
            {
                if(castles.Contains(n)){near.Add(n);continue;}
                foreach(var m in edges.GetValueOrDefault(n)??[])if(m!=c&&castles.Contains(m))near.Add(m);
            }
            result["site:"+c]=near.Select(x=>"site:"+x).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        return result;
    }
    public static string[] Neighbors(string site)=>adjacency.Value.GetValueOrDefault(site)??[];

    // その時点の城の支配（開始時の持ち主に、出陣の結果を順に当てる）。
    public static EntityId? Owner(ContractWorld w,EntityId site,long at,long rev)
    {using var _ccRev=ControlContext.At(rev);
        var start=w.NewGame!.Start.Castles.SingleOrDefault(c=>c.Id==site.Value)?.Faction;
        EntityId? owner=start==null?null:new(start);
        if(w.Strategy is not {} s)return owner;
        foreach(var (Site,House,At,Revision) in Changes(s))
        {
            if(At>at||At==at&&Revision>rev)continue;
            if(Site==site.Value)owner=new(House);
        }
        return owner;
    }
    // 城の持ち主が変わる出来事（落城と、擁立で渡した城）を版の順に。
    static IEnumerable<(string Site,string House,long At,long Revision)> Changes(StrategyState s)=>
        s.Outcomes.Where(o=>o.Captured).Select(o=>{var c=s.Campaigns.Single(c=>c.Id==o.Campaign);return (c.Target.Value,c.Attacker.Value,o.At,o.Revision);})
        .Concat((s.Shoguns??[]).Where(x=>x.Kind=="installed"&&x.Site!=null).Select(x=>(x.Site!.Value.Value,NationalStart.FactionId(Shogun),x.At,x.Revision)))
        .OrderBy(x=>x.Item4).ThenBy(x=>x.Item3);
    internal static Dictionary<string,string?> Owners(ContractWorld w,StrategyState? s)
    {
        var map=w.NewGame!.Start.Castles.ToDictionary(c=>c.Id,c=>c.Faction,StringComparer.Ordinal);
        if(s!=null){foreach(var (site,house,_,_) in Changes(s))map[site]=house;Independent(w,map);return map;}
        if(s!=null)foreach(var o in s.Outcomes.Where(o=>o.Captured)){var c=s.Campaigns.Single(c=>c.Id==o.Campaign);map[c.Target.Value]=c.Attacker.Value;}
        return map;
    }

    // 独立を宣言した家が押さえている城（一覧の StrategyIndependence を持つ世界だけ）：家の戦略では、元の家の城に数えず、出陣の目的にもしない。
    // 独立した家は戦略の家に入らない（その家の出陣・同盟は今は行わない）。
    static void Independent(ContractWorld w,Dictionary<string,string?> map)
    {
        if(!NationalSystems.On(w,"StrategyIndependence"))return;long at=w.ElapsedSeconds;
        foreach(var d in w.Declarations??[])
            if(map.ContainsKey(d.Site.Value)&&w.LandRights.Any(r=>r.Subject==d.House&&r.Target==d.Site&&r.Kind=="control"&&r.From<=at&&at<r.Until))map[d.Site.Value]=null;
    }
    static long MonthStart(ContractWorld w,int year,int month)
    {
        var epoch=DateOnly.ParseExact(w.EpochDate,"yyyy-MM-dd",CultureInfo.InvariantCulture);
        return ((long)new DateOnly(year,month,1).DayNumber-epoch.DayNumber)*86400;
    }
    // at より後の最初の月の初め。
    static long NextMonthStart(ContractWorld w,long at)
    {
        var d=WorldCalendar.At(w.EpochDate,at);var next=MonthStart(w,d.Year,d.Month);
        if(next<=at){var m=d.AddMonths(1);next=MonthStart(w,m.Year,m.Month);}
        return next;
    }
    internal static bool IsMonthStart(ContractWorld w,long at){var d=WorldCalendar.At(w.EpochDate,at);return MonthStart(w,d.Year,d.Month)==at;}
    internal static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;long next=NextMonthStart(w,w.ElapsedSeconds);
        return next<=target?next:null;
    }

    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"national-strategy-v1/{w.Seed}/{key}")))%1000);
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string,long> caps=new(StringComparer.Ordinal);
    internal static long Cap(string faction)=>caps.GetOrAdd(faction,f=>NationalStart.DbHouse(f) is {} db?HakutaiDbStart.Roster(db).SoldierCap:0);
    static Dictionary<string,long> Powers(ContractWorld w,Dictionary<string,string?> owners,Dictionary<string,int> initial)
    {
        var lost=Losses(w);if(lost.Count==0)return Powers(owners,initial);
        return Powers(owners,initial).ToDictionary(x=>x.Key,x=>Math.Max(50L,x.Value-lost.GetValueOrDefault(x.Key)),StringComparer.Ordinal);
    }
    static Dictionary<string,long> Powers(Dictionary<string,string?> owners,Dictionary<string,int> initial)
    {
        var now=owners.Values.Where(v=>v!=null).GroupBy(v=>v!,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal);
        return initial.Keys.ToDictionary(f=>f,f=>{int start=initial[f];return Math.Max(50L,start==0?0:Cap(f)*now.GetValueOrDefault(f)/start);},StringComparer.Ordinal);
    }
    static long Strength(ContractWorld w,string faction,Dictionary<string,string?> owners,Dictionary<string,int> initial)=>Math.Max(50L,(initial.GetValueOrDefault(faction) is int start&&start>0?Cap(faction)*owners.Values.Count(v=>v==faction)/start:0)-Losses(w).GetValueOrDefault(faction));
    // 合戦の損害を家の兵力に残す（全国の開始の一覧の StrategyLosses を持つ世界だけ）。遠方の合戦で失った兵は、その日から180日かけて同じ割合で戻る。
    // 守る側の損害は、守る家と援軍の家で 2:1 の割合で分ける（援軍は兵力の半分で出るため）。
    public const long LossRecoverySeconds=180L*86400;
    internal static Dictionary<string,long> Losses(ContractWorld w)
    {
        var result=new Dictionary<string,long>(StringComparer.Ordinal);if(w.Strategy is not {} s||!NationalSystems.On(w,"StrategyLosses"))return result;
        long now=w.ElapsedSeconds;
        foreach(var o in s.Outcomes){if(o.Battle is not {} b||now-o.At>=LossRecoverySeconds)continue;var c=s.Campaigns.Single(c=>c.Id==o.Campaign);
            long Left(long lost)=>lost*(LossRecoverySeconds-(now-o.At))/LossRecoverySeconds;
            void Add(string f,long n){if(n>0)result[f]=checked(result.GetValueOrDefault(f)+n);}
            if(c.Basis!=Restoration)Add(c.Attacker.Value,Left(b.AttackerLost));
            var allies=o.Allies??[];long weight=2+allies.Length;Add(c.Defender.Value,Left(b.DefenderLost)*2/weight);foreach(var a in allies)Add(a.Value,Left(b.DefenderLost)/weight);}
        return result;
    }
    static bool Allied(StrategyState s,string a,string b)
    {
        var x=NationalStart.DbHouse(a) is {} da?HakutaiDb.House(da):null;var y=NationalStart.DbHouse(b) is {} db?HakutaiDb.House(db):null;
        bool Left(string house,string? overlord)=>overlord!=null&&StrategyDefections.Left(s,house,FactionId(overlord));
        return x?.Overlord!=null&&x.Overlord==y?.Id&&!Left(a,x.Overlord)||y?.Overlord!=null&&y.Overlord==x?.Id&&!Left(b,y.Overlord)||x?.Overlord!=null&&x.Overlord==y?.Overlord&&!Left(a,x.Overlord)&&!Left(b,y!.Overlord);
    }
    static string FactionId(string house)=>NationalStart.FactionId(house);
    // 決着した出陣の後、同じ二つの家は三か月は互いに出陣しない（戦いの疲れ。和睦の交渉は後の工程）。
    public const string Shogun="G-000154";
    public const string Jouraku="jouraku",Restoration="restoration";
    // 山城の城（移動網の国が山城国）。
    static readonly Lazy<HashSet<string>> kyoto=new(()=>CastleRoutes.Catalog.Nodes.Where(n=>n.Province=="山城国").Select(n=>"site:"+n.Id).ToHashSet(StringComparer.Ordinal));
    public static bool InKyoto(string site)=>kyoto.Value.Contains(site);
    // 今の京の主（山城の城の過半を押さえる家。なければ null）。
    public static EntityId? Capital(ContractWorld w)=>w.Strategy?.Capitals is {Length:>0} rows?rows[^1].House:null;
    static string? CapitalHolder(Dictionary<string,string?> owners)
    {
        var held=kyoto.Value.Select(x=>owners.GetValueOrDefault(x)).Where(x=>x!=null).GroupBy(x=>x!,StringComparer.Ordinal).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key,StringComparer.Ordinal).FirstOrDefault();
        return held!=null&&held.Count()*2>kyoto.Value.Count?held.Key:null;
    }
    static bool Bound(StrategyPact[] pacts,string a,string b,long now)=>pacts.Any(p=>p.Broken==null&&p.From<=now&&now<p.Until&&(p.A.Value==a&&p.B.Value==b||p.A.Value==b&&p.B.Value==a));
    // 同盟：共通の強い隣を持ち、互いに境を接する二つの家は、月に20%で二年の同盟を結ぶ。
    // 将軍の仲裁：三か月で同じ相手に城を二つ以上奪われた家があれば、将軍が和睦を勧め、攻めた側は自分が相手の3倍より強くなければ受ける（一年）。
    static List<StrategyPact> Diplomacy(ContractWorld w,StrategyState s,Dictionary<string,string?> owners,Dictionary<string,long> power,HashSet<string> playerHouse,List<(StrategyPact Pact,string Sender)> letters)
    {
        var pacts=(s.Pacts??[]).ToList();long now=w.ElapsedSeconds;var date=WorldCalendar.At(w.EpochDate,now);
        var houses=w.NewGame!.Start.Factions.Select(f=>f.Id).Where(f=>!playerHouse.Contains(f)&&owners.ContainsValue(f)).OrderBy(f=>f,StringComparer.Ordinal).ToArray();
        var byOwner=owners.Where(x=>x.Value!=null).GroupBy(x=>x.Value!,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Key).ToArray(),StringComparer.Ordinal);
        var borders=houses.ToDictionary(f=>f,f=>(byOwner.GetValueOrDefault(f)??[]).SelectMany(Neighbors).Select(n=>owners.GetValueOrDefault(n)).Where(o=>o!=null&&o!=f).Select(o=>o!).ToHashSet(StringComparer.Ordinal),StringComparer.Ordinal);
        var strength=houses.ToDictionary(f=>f,f=>power.GetValueOrDefault(f,50),StringComparer.Ordinal);
        foreach(var a in houses)foreach(var b in borders[a].Where(b=>string.CompareOrdinal(a,b)<0&&strength.ContainsKey(b)).OrderBy(b=>b,StringComparer.Ordinal))
        {
            if(Bound(pacts.ToArray(),a,b,now)||s.Campaigns.Any(c=>c.At>now-180*86400&&(c.Attacker.Value==a&&c.Defender.Value==b||c.Attacker.Value==b&&c.Defender.Value==a)))continue;
            var threat=borders[a].Intersect(borders[b]).Where(c=>strength.TryGetValue(c,out var x)&&x>strength[a]&&x>strength[b]).OrderBy(c=>c,StringComparer.Ordinal).FirstOrDefault();
            if(threat==null||Draw(w,$"alliance/{a}/{b}/{date:yyyy-MM}")>=200)continue;
            var pact=new StrategyPact(new($"pact:national/{date:yyyyMM}/{ContractJson.Hash(Encoding.UTF8.GetBytes(a+"/"+b))[..12]}"),new(a),new(b),"alliance",now,now+2*365*86400L,w.CommitSequence,"threat:"+threat);
            pacts.Add(pact);letters.Add((pact,""));
        }
        var shogun=NationalStart.FactionId(Shogun);
        if(w.NewGame.Start.Factions.Any(f=>f.Id==shogun)&&HouseSuccession.Head(w,new(shogun)) is var sh&&w.Characters.SingleOrDefault(c=>c.Id==sh)?.Life==LifeState.Adult)
            foreach(var g in s.Outcomes.Where(o=>o.Captured&&o.At>now-90*86400).Select(o=>s.Campaigns.Single(c=>c.Id==o.Campaign)).GroupBy(c=>(c.Attacker.Value,c.Defender.Value)).Where(g=>g.Count()>=2).OrderBy(g=>g.Key.Item1,StringComparer.Ordinal).ThenBy(g=>g.Key.Item2,StringComparer.Ordinal))
            {
                var (a,d)=g.Key;if(playerHouse.Contains(a)||playerHouse.Contains(d)||Bound(pacts.ToArray(),a,d,now)||a==shogun||d==shogun)continue;
                long x=power.GetValueOrDefault(a,50),y=power.GetValueOrDefault(d,50);if(x>=3*y)continue;
                var pact=new StrategyPact(new($"pact:national/{date:yyyyMM}/{ContractJson.Hash(Encoding.UTF8.GetBytes(a+"/"+d+"/truce"))[..12]}"),new(a),new(d),"truce",now,now+365*86400L,w.CommitSequence,"mediation:"+shogun);
                pacts.Add(pact);letters.Add((pact,sh.Value));
            }
        return pacts;
    }
    internal static bool Resting(StrategyState s,string a,string b,long now)=>s.Outcomes.Any(o=>o.At>now-90*86400&&s.Campaigns.Single(c=>c.Id==o.Campaign) is var c&&(c.Attacker.Value==a&&c.Defender.Value==b||c.Attacker.Value==b&&c.Defender.Value==a));
    static readonly Lazy<Dictionary<string,HashSet<string>>> vassalsOf=new(()=>HakutaiDb.Houses.Where(h=>h.Overlord!=null).GroupBy(h=>NationalStart.FactionId(h.Overlord!),StringComparer.Ordinal)
        .ToDictionary(g=>g.Key,g=>g.Select(h=>NationalStart.FactionId(h.Id)).ToHashSet(StringComparer.Ordinal),StringComparer.Ordinal));
    static HashSet<string> Vassals(string faction)=>vassalsOf.Value.GetValueOrDefault(faction)??[];
    static HashSet<string> Vassals(StrategyState s,string faction)=>Vassals(faction).Where(v=>!StrategyDefections.Left(s,v,faction)).ToHashSet(StringComparer.Ordinal);
    internal static string? DbOverlord(string faction)=>NationalStart.DbHouse(faction) is {} d&&HakutaiDb.House(d)?.Overlord is {} o?FactionId(o):null;
    static HistoryMode Mode(ContractWorld w)=>w.NewGame!.Judgment?.History??w.NewGame.History??HistoryMode.SemiHistorical;
    // 操作する人物から30km以内の城（月の初めに一度だけ数える）。
    internal static HashSet<string> NearPlayerSites(ContractWorld w)
    {
        var p=RegionalCampaign.ActorPointAt(w,w.PlayerControl,w.ElapsedSeconds);double r=(double)NpcDormancy.RadiusMm*NpcDormancy.RadiusMm;
        return BranchRoutes.GraphNodes(w).Where(n=>{long de=n.Position.EastMm-p.EastMm,dn=n.Position.NorthMm-p.NorthMm;return (double)de*de+(double)dn*dn<=r;}).Select(n=>"site:"+n.Id).ToHashSet(StringComparer.Ordinal);
    }

    internal static void Apply(ContractWorld w)
    {
        if(!Enabled(w)||!IsMonthStart(w,w.ElapsedSeconds))return;
        var s=w.Strategy??new StrategyState(1,Policy,[],[]);
        var date=WorldCalendar.At(w.EpochDate,w.ElapsedSeconds);
        s=s with{Successions=Succeed(w,s)};w.Strategy=s;
        var outcomes=s.Outcomes.ToList();
        // 前の月の出陣を決着させる。
        foreach(var c in s.Campaigns.Where(c=>!s.Outcomes.Any(o=>o.Campaign==c.Id)&&c.At<w.ElapsedSeconds))
        {
            if(c.Real==true&&RealArmy(w,c) is {} army&&!NpcArmies.ReturnedAt(w,army,w.ElapsedSeconds,w.CommitSequence))continue; // 実際の軍勢は着いてから決着する（ResolveArmies）
            outcomes.Add(Settle(w,s,outcomes,c));
        }
        s=StrategyRestoration.RecordFalls(w,s,s with{Outcomes=outcomes.ToArray()});
        {var holder=CapitalHolder(Owners(w,s));var last=s.Capitals is {Length:>0} cs?cs[^1].House.Value:null;
         if(holder!=null&&holder!=last){s=s with{Capitals=[..s.Capitals??[],new(new(holder),w.ElapsedSeconds,w.CommitSequence)]};if(last!=null)CapitalLetter(w,holder);}}
        s=ShogunTurn(w,s);
        if(StrategyDefections.Enabled(w)){var before=(s.Defections??[]).Length;s=StrategyDefections.Decide(w,s,Powers(w,Owners(w,s),InitialCounts(w)),date);
            if(NationalSystems.On(w,"DefectionLetters"))foreach(var d in (s.Defections??[]).Skip(before))DefectionLetter(w,d);}
        var owners=Owners(w,s);var initial=InitialCounts(w);var campaigns=s.Campaigns.ToList();var power=Powers(w,owners,initial);var near=NearPlayerSites(w);bool realArmies=NationalSystems.On(w,"StrategyRealArmies");
        var player=w.PlayerControl;var playerHouse=w.NewGame!.Start.Factions.Where(f=>HouseSuccession.Head(w,new(f.Id))==player).Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);
        var playerMember=w.NewGame.Start.People.SingleOrDefault(p=>p.Id==player.Value)?.Faction;if(playerMember!=null)playerHouse.Add(playerMember);
        var mode=Mode(w);var history=(s.History??[]).ToList();
        // その月の史実の出陣で、攻める家が存在しないか当主が操作する人物の家などで判断に入らないものは、前提が崩れた不発として残す。
        foreach(var e in History.Where(e=>e.Year==date.Year&&e.Month==date.Month))
        {
            var f=NationalStart.FactionId(e.Attacker);var head=w.NewGame.Start.Factions.Any(x=>x.Id==f)?HouseSuccession.Head(w,new(f)):default;
            if(mode==HistoryMode.Fictional||head.Value==null||playerHouse.Contains(f)||w.Characters.SingleOrDefault(c=>c.Id==head)?.Life!=LifeState.Adult||!owners.ContainsValue(f))
                history.Add(new(e.Id,w.ElapsedSeconds,w.CommitSequence,mode==HistoryMode.Fictional?"declined":"premise"));
        }
        var pactLetters=new List<(StrategyPact Pact,string Sender)>();var pacts=Diplomacy(w,s,owners,power,playerHouse,pactLetters);
        foreach(var f in w.NewGame.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            if(playerHouse.Contains(f.Id))continue;
            var head=HouseSuccession.Head(w,new(f.Id));if(w.Characters.SingleOrDefault(c=>c.Id==head)?.Life!=LifeState.Adult)continue;
            var own=owners.Where(x=>x.Value==f.Id).Select(x=>x.Key).ToArray();if(own.Length==0)continue;
            // 境の城は、自分の城と、自分に従う家（DBの従属先が自分の家）の城の隣。
            var vassals=Vassals(s,f.Id);var front=owners.Where(x=>x.Value==f.Id||x.Value!=null&&vassals.Contains(x.Value)).Select(x=>x.Key).ToArray();
            long mine=power.GetValueOrDefault(f.Id,50);
            var targets=front.SelectMany(Neighbors).Distinct(StringComparer.Ordinal)
                .Select(t=>(Site:t,Owner:owners.GetValueOrDefault(t))).Where(t=>t.Owner!=null&&t.Owner!=f.Id&&!vassals.Contains(t.Owner!)&&(realArmies||!playerHouse.Contains(t.Owner!))&&!Allied(s,f.Id,t.Owner!)&&!Resting(s,f.Id,t.Owner!,w.ElapsedSeconds)&&!Bound(pacts.ToArray(),f.Id,t.Owner!,w.ElapsedSeconds)&&(realArmies||!near.Contains(t.Site))).ToArray();
            // 破約：約束の相手が3倍以上弱ければ、当主は月に3%で約束を破って攻める。
            var bound=front.SelectMany(Neighbors).Distinct(StringComparer.Ordinal).Select(t=>(Site:t,Owner:owners.GetValueOrDefault(t)))
                .Where(t=>t.Owner!=null&&t.Owner!=f.Id&&!playerHouse.Contains(t.Owner!)&&Bound(pacts.ToArray(),f.Id,t.Owner!,w.ElapsedSeconds)&&!near.Contains(t.Site))
                .Select(t=>(t,Ratio:mine*100/power.GetValueOrDefault(t.Owner!,50))).Where(x=>x.Ratio>=300).OrderByDescending(x=>x.Ratio).ThenBy(x=>x.t.Site,StringComparer.Ordinal).ToArray();
            if(targets.Length==0&&bound.Length==0)continue;
            string? basis=null;(string Site,string? Owner) pick=default;
            if(mode!=HistoryMode.Fictional)
                foreach(var e in History.Where(e=>e.Year==date.Year&&e.Month==date.Month&&NationalStart.FactionId(e.Attacker)==f.Id))
                {
                    var defender=NationalStart.FactionId(e.Defender);var known=targets.Where(t=>t.Owner==defender).OrderBy(t=>t.Site,StringComparer.Ordinal).ToArray();
                    if(known.Length==0||e.AttackerHead!=null&&NationalStart.DbPerson(head.Value)?.Id!=e.AttackerHead){history.Add(new(e.Id,w.ElapsedSeconds,w.CommitSequence,"premise"));continue;} // 前提が崩れていれば不発
                    if(basis==null&&Draw(w,"history/"+e.Id+"/"+f.Id)<(mode==HistoryMode.Historical?900:500)){basis="history:"+e.Id;pick=known[0];history.Add(new(e.Id,w.ElapsedSeconds,w.CommitSequence,"fired"));}
                    else history.Add(new(e.Id,w.ElapsedSeconds,w.CommitSequence,"declined"));
                }
            if(basis==null&&bound.Length>0&&Draw(w,$"betray/{f.Id}/{date:yyyy-MM}")<30)
            {
                var victim=bound[0].t;basis="betrayal";pick=victim;
                for(int n=0;n<pacts.Count;n++)if(pacts[n].Broken==null&&(pacts[n].A.Value==f.Id&&pacts[n].B.Value==victim.Owner||pacts[n].B.Value==f.Id&&pacts[n].A.Value==victim.Owner))pacts[n]=pacts[n] with{Broken=w.ElapsedSeconds};
            }
            // 上洛：自分が京の主でなく、境に山城の城があり、相手の2倍以上強い家は、月に30%でその城を目指す。
            if(basis==null&&Capital(w)?.Value!=f.Id&&targets.Where(t=>InKyoto(t.Site)&&mine>=2*power.GetValueOrDefault(t.Owner!,50)).OrderBy(t=>t.Site,StringComparer.Ordinal).FirstOrDefault() is {Site:not null} kyotoTarget&&Draw(w,$"jouraku/{f.Id}/{date:yyyy-MM}")<300)
            {basis=Jouraku;pick=kyotoTarget;}
            if(basis==null&&targets.Length==0)continue;
            if(basis==null)
            {
                var best=targets.Select(t=>(t,Ratio:mine*100/power.GetValueOrDefault(t.Owner!,50))).OrderByDescending(x=>x.Ratio).ThenBy(x=>x.t.Site,StringComparer.Ordinal).First();
                if(best.Ratio<150||Draw(w,$"border/{f.Id}/{date:yyyy-MM}")>=60)continue;
                basis=Border;pick=best.t;
            }
            var id=new EntityId($"campaign:national/{date:yyyyMM}/{ContractJson.Hash(Encoding.UTF8.GetBytes(f.Id))[..12]}");
            // 操作する人物の近くの城・本人の家への出陣は、実際の軍勢で行う：当主が相手の家へ敵対を表明し、NPC の軍がその城へ出陣する（表明できなければ出陣しない）。
            bool real=realArmies&&(near.Contains(pick.Site!)||playerHouse.Contains(pick.Owner!));
            if(real&&!TransactionalWorld.TryApply(w,c=>FactionStances.Apply(c,new(DeclarationId(id),FactionStanceAction.DeclareHostile,new(f.Id),new(pick.Owner!),c.ElapsedSeconds,checked(c.ElapsedSeconds+RealDays*86400L)),head)))continue; // 表明ごとに版を進める
            campaigns.Add(new(id,new(f.Id),new(pick.Owner!),new(pick.Site!),w.ElapsedSeconds,w.CommitSequence,basis){Real=real?true:null});
        }
        if(StrategyRestoration.Enabled(w))campaigns.AddRange(StrategyRestoration.Uprisings(w,s,owners,playerHouse,near,campaigns,date));
        var coalitions=Coalitions(w,s,pacts,out var formed);
        w.Strategy=s with{Campaigns=campaigns.ToArray(),History=history.Count==0?null:history.ToArray(),Pacts=pacts.Count==0?null:pacts.ToArray(),Coalitions=coalitions};
        foreach(var c in formed){CoalitionLetters(w,c);InviteThePlayer(w,c);}
        foreach(var (pact,sender) in pactLetters)PactLetters(w,pact,sender);
    }
    // 実際の軍勢の出陣：敵対の表明の ID は出陣の ID から決まり、NPC の軍はその表明を根拠に出る（軍の目的の Claim.Declaration）。表明は30日。
    public const int RealDays=30;
    internal static EntityId DeclarationId(EntityId campaign)=>new("decision:strategy-army/"+ContractJson.Hash(Encoding.UTF8.GetBytes(campaign.Value))[..24]);
    static NpcArmyMuster? RealArmy(ContractWorld w,StrategyCampaign c){var d=DeclarationId(c.Id);return NpcArmies.Musters(w).FirstOrDefault(m=>m.Objective?.Claim.Declaration==d);}
    // NPC の軍が目指す城：その表明が実際の軍勢の出陣のものなら、出陣の城（決着の前後を問わない。検査でも使う）。
    internal static EntityId? RealTarget(ContractWorld w,EntityId declaration)=>w.Strategy?.Campaigns.FirstOrDefault(c=>c.Real==true&&DeclarationId(c.Id)==declaration)?.Target;
    internal static bool RealOpen(ContractWorld w,EntityId declaration)=>w.Strategy is {} s&&s.Campaigns.Any(c=>c.Real==true&&DeclarationId(c.Id)==declaration&&!s.Outcomes.Any(o=>o.Campaign==c.Id));
    // 実際の軍勢の決着（区切りごと）：軍が目的の城に着いて留まり終えたら、月の初めと同じ計算で決着する。大将が捕らえられた・兵がいない・着く前に帰った軍は退いたものとする。
    internal static void ResolveArmies(ContractWorld w)
    {
        if(w.Strategy is not {} s||!NationalSystems.On(w,"StrategyRealArmies"))return;
        var outcomes=s.Outcomes.ToList();bool changed=false;
        foreach(var c in s.Campaigns.Where(c=>c.Real==true).ToArray())
        {
            if(outcomes.Any(o=>o.Campaign==c.Id)||RealArmy(w,c) is not {} m)continue;
            if(NpcArmies.Broken(w,m)){Letters(w,c,false);outcomes.Add(new(c.Id,w.ElapsedSeconds,w.CommitSequence,false));changed=true;continue;}
            if(NpcArmies.HoldEnd(w,m) is {} end&&w.ElapsedSeconds>=end){outcomes.Add(Settle(w,s,outcomes,c));changed=true;}
        }
        if(changed)w.Strategy=StrategyRestoration.RecordFalls(w,s,s with{Outcomes=outcomes.ToArray()});
    }
    // 実際の軍勢の出陣が退いたとき、攻め手の軍が崩れていれば（遠方の合戦の計算のないときは常に）、守る家が軍の大将を捕らえる（35%、人物と日の固定のくじ）。
    internal static BattleCapture? CaptureLeader(ContractWorld w,StrategyCampaign c,StrategyBattle? battle,int? roll=null)
    {
        if(c.Real!=true||!NationalSystems.On(w,"StrategyCapture")||RealArmy(w,c) is not {} m||battle is {AttackerBroke:false})return null;
        var leader=m.Commander;long at=w.ElapsedSeconds;
        if(Assassination.DeadAt(w,leader,at,w.CommitSequence)||BattleCaptivity.HeldAt(w,leader,at,w.CommitSequence+1)||leader==w.PlayerControl)return null;
        if((roll??Draw(w,"capture/"+c.Id.Value+"/"+leader.Value))>=350)return null; // roll は検査用
        var holder=HouseSuccession.Head(w,c.Defender);var capture=new BattleCapture(leader,c.Attacker,c.Defender,holder,c.Target,at);
        BattleCaptivity.StopCaptured(w,[capture]);return capture;
    }
    // 出陣の決着（強さと同盟の援軍、遠方の合戦の計算）。月の初めの決着と、実際の軍勢が着いたときの決着で同じものを使う。
    internal static StrategyOutcome Settle(ContractWorld w,StrategyState s,List<StrategyOutcome> outcomes,StrategyCampaign c)
    {
            var owners0=Owners(w,s with{Outcomes=outcomes.ToArray()});var initial0=InitialCounts(w);
            long a=c.Basis==Restoration?StrategyRestoration.Strength(c.Attacker.Value):Strength(w,c.Attacker.Value,owners0,initial0),d=Strength(w,c.Defender.Value,owners0,initial0);
            // 同盟の義務：守る側と出陣の時点で同盟していた家（攻める側と約束のない家）は、兵力の半分で援軍に出る。
            var allies=(s.Pacts??[]).Where(p=>p.Kind=="alliance"&&p.Broken==null&&p.From<=c.At&&c.At<p.Until&&(p.A==c.Defender||p.B==c.Defender)).Select(p=>p.A==c.Defender?p.B:p.A)
                .Where(x=>x!=c.Attacker&&!Bound((s.Pacts??[]).ToArray(),x.Value,c.Attacker.Value,c.At)).Distinct().OrderBy(x=>x.Value,StringComparer.Ordinal).ToArray();
            foreach(var ally in allies)d=checked(d+Strength(w,ally.Value,owners0,initial0)/2);
            // 再興の挙兵への後援（一覧の RestorationSupport）：落とした家に一年以内に城を奪われた家（今も城を持つ家）は、兵力の半分で後援する。
            var supporters=c.Basis==Restoration&&NationalSystems.On(w,"RestorationSupport")?s.Outcomes.Concat(outcomes).Where(o=>o.Captured&&o.At>w.ElapsedSeconds-365*86400L).Select(o=>s.Campaigns.Single(x=>x.Id==o.Campaign)).Where(x=>x.Attacker==c.Defender&&x.Defender!=c.Attacker&&x.Basis!=Restoration&&owners0.ContainsValue(x.Defender.Value)).Select(x=>x.Defender).Distinct().OrderBy(x=>x.Value,StringComparer.Ordinal).ToArray():[];
            foreach(var x in supporters)a=checked(a+Strength(w,x.Value,owners0,initial0)/2);
            bool held=owners0.GetValueOrDefault(c.Target.Value)==c.Defender.Value;
            var battle=StrategyBattles.Enabled(w)?StrategyBattles.Fight(w,c.Id,a,d):null;
            bool captured=held&&(battle!=null?StrategyBattles.Falls(battle):Draw(w,"outcome/"+c.Id.Value)<1000*a/(a+d));
            Letters(w,c,captured);
            return new(c.Id,w.ElapsedSeconds,w.CommitSequence,captured){Allies=allies.Length==0?null:allies,Battle=battle,Capture=captured?null:CaptureLeader(w,c,battle),Supporters=supporters.Length==0?null:supporters};
    }
    // 当主が亡くなった家（操作する人物の家を除く）の家督：成人した実子の年長者、いなければ家中の筆頭格の成人。
    static StrategySuccession[]? Succeed(ContractWorld w,StrategyState s)
    {
        var rows=(s.Successions??[]).ToList();var people=FamilyBirth.People(w).ToArray();var life=w.Characters.ToDictionary(c=>c.Id.Value,c=>c.Life,StringComparer.Ordinal);
        foreach(var f in w.NewGame!.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            var head=HouseSuccession.Head(w,new(f.Id));if(life.GetValueOrDefault(head.Value)!=LifeState.Dead||head==w.PlayerControl)continue;
            var heir=people.Where(p=>(p.BirthParents??[]).Contains(head.Value)&&life.GetValueOrDefault(p.Id)==LifeState.Adult).OrderBy(p=>p.BirthYear??NationalStart.Life(p.Id)?.Born??int.MaxValue).ThenBy(p=>p.Id,StringComparer.Ordinal).FirstOrDefault()
                ??people.Where(p=>p.Faction==f.Id&&life.GetValueOrDefault(p.Id)==LifeState.Adult).OrderBy(p=>p.Lord==null?0:1).ThenBy(p=>p.Id,StringComparer.Ordinal).FirstOrDefault();
            if(heir==null)continue;
            rows.Add(new(new(f.Id),head,new(heir.Id),w.ElapsedSeconds,w.CommitSequence));
        }
        return rows.Count==0?null:rows.ToArray();
    }
    static Dictionary<string,int> InitialCounts(ContractWorld w)=>w.NewGame!.Start.Castles.Where(c=>c.Faction!=null).GroupBy(c=>c.Faction!,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal);

    // 結果の書状：境の城にいる人物から、双方の当主へ。城に誰もいなければ書状はない（後で噂で知る）。
    static void Letters(ContractWorld w,StrategyCampaign c,bool captured)
    {
        var sender=w.Locations.Where(l=>l.Kind==LocationKind.AtSite&&l.Site==c.Target&&w.Characters.Single(x=>x.Id==l.Actor).Life==LifeState.Adult).Select(l=>l.Actor).OrderBy(x=>x.Value,StringComparer.Ordinal).FirstOrDefault();
        if(sender.Value==null)return;
        string castle=w.Definitions.Sites.SingleOrDefault(s=>s.Id==c.Target)?.Name??c.Target.Value;
        string attacker=w.Definitions.Organizations.Single(o=>o.Id==c.Attacker).Name,defender=w.Definitions.Organizations.Single(o=>o.Id==c.Defender).Name;
        string body=captured?$"{castle}は{attacker}の手に落ちました。":$"{attacker}の軍勢は{castle}を落とせずに退きました。";
        // 史実の出陣は、年代記に残るときの名で知らせる。
        if(History.SingleOrDefault(e=>"history:"+e.Id==c.Basis) is {} named)body=named.Name+"　"+body;
        // 当事者の二家と、落ちた（または攻められた）城の隣に城を持つ家の当主へ届く（噂より確かな近隣の知らせ）。
        var owners=Owners(w,w.Strategy);
        var neighbours=Neighbors(c.Target.Value).Select(n=>owners.GetValueOrDefault(n)).Where(o=>o!=null).Select(o=>new EntityId(o!));
        foreach(var house in new[]{c.Attacker,c.Defender}.Concat(neighbours).Distinct().ToArray())
        {
            var head=HouseSuccession.Head(w,house);if(head==sender||w.Characters.Single(x=>x.Id==head).Life!=LifeState.Adult)continue;
            var site=FamilyBirth.People(w).Single(p=>p.Id==head.Value).Site;
            string id=$"message:strategy/{ContractJson.Hash(Encoding.UTF8.GetBytes(c.Id.Value+"/"+house.Value))[..16]}";
            MessageFlow.Send(w.Messaging,new MessageEnvelope(id,c.Id.Value,LetterKind,sender.Value,head.Value,body,site,w.ElapsedSeconds),w.ElapsedSeconds,1);
            w.SealedMessages=[..w.SealedMessages,new(id)];
        }
        RegionalCampaign.Bind(w);
    }

    // 将軍家が城をすべて失えば空位。空位の間、京の主（将軍家でない）が将軍家の生きている成人を擁立し、自分の山城の城を一つ渡す。
    static StrategyState ShogunTurn(ContractWorld w,StrategyState s)
    {
        var shogun=NationalStart.FactionId(Shogun);if(!w.NewGame!.Start.Factions.Any(f=>f.Id==shogun))return s;
        var owners=Owners(w,s);var rows=(s.Shoguns??[]).ToList();var last=rows.LastOrDefault();
        bool holds=owners.ContainsValue(shogun);
        if(!holds&&last?.Kind!="vacant")rows.Add(new("vacant",null,null,null,w.ElapsedSeconds,w.CommitSequence));
        else if(!holds&&last?.Kind=="vacant"&&Capital(w) is {} patron&&patron.Value!=shogun)
        {
            var candidate=FamilyBirth.People(w).Where(p=>p.Faction==shogun&&w.Characters.SingleOrDefault(c=>c.Id.Value==p.Id)?.Life==LifeState.Adult).OrderBy(p=>p.Lord==null?0:1).ThenBy(p=>p.Id,StringComparer.Ordinal).FirstOrDefault();
            var site=owners.Where(x=>x.Value==patron.Value&&InKyoto(x.Key)).Select(x=>x.Key).OrderBy(x=>x,StringComparer.Ordinal).FirstOrDefault();
            if(candidate!=null&&site!=null)rows.Add(new("installed",patron,new(candidate.Id),new(site),w.ElapsedSeconds,w.CommitSequence));
        }
        return rows.Count==(s.Shoguns?.Length??0)?s:s with{Shoguns=rows.ToArray()};
    }
    // 同じ強い家（同盟の根拠 threat:X）を相手にした、今も続く同盟の家が三つ以上で、まだその家への包囲網がなければ作る。
    static StrategyCoalition[]? Coalitions(ContractWorld w,StrategyState s,List<StrategyPact> pacts,out List<StrategyCoalition> formed)
    {
        var rows=(s.Coalitions??[]).ToList();formed=[];long now=w.ElapsedSeconds;var date=WorldCalendar.At(w.EpochDate,now);
        foreach(var g in pacts.Where(p=>p.Kind=="alliance"&&p.Broken==null&&p.From<=now&&now<p.Until&&p.Basis.StartsWith("threat:",StringComparison.Ordinal)).GroupBy(p=>p.Basis[7..],StringComparer.Ordinal).OrderBy(g=>g.Key,StringComparer.Ordinal))
        {
            var members=g.SelectMany(p=>new[]{p.A,p.B}).Distinct().OrderBy(x=>x.Value,StringComparer.Ordinal).ToArray();
            if(members.Length<3||rows.Any(c=>c.Target.Value==g.Key&&now-c.At<2*365*86400L))continue;
            var c=new StrategyCoalition(new($"coalition:national/{date:yyyyMM}/{ContractJson.Hash(Encoding.UTF8.GetBytes(g.Key))[..12]}"),new(g.Key),members,now,w.CommitSequence);
            rows.Add(c);formed.Add(c);
        }
        return rows.Count==0?null:rows.ToArray();
    }
    static void CoalitionLetters(ContractWorld w,StrategyCoalition c)
    {
        string target=w.Definitions.Organizations.Single(o=>o.Id==c.Target).Name;var first=HouseSuccession.Head(w,c.Members[0]);
        foreach(var house in c.Members)
        {
            var head=HouseSuccession.Head(w,house);var sender=house==c.Members[0]?HouseSuccession.Head(w,c.Members[1]):first;
            if(head==sender||w.Characters.Single(x=>x.Id==head).Life!=LifeState.Adult||w.Characters.Single(x=>x.Id==sender).Life!=LifeState.Adult)continue;
            string id=$"message:strategy-coalition/{ContractJson.Hash(Encoding.UTF8.GetBytes(c.Id.Value+"/"+house.Value))[..16]}";
            MessageFlow.Send(w.Messaging,new MessageEnvelope(id,c.Id.Value,LetterKind,sender.Value,head.Value,$"{target}に備え、{c.Members.Length}家で手を結びたく存じます。",FamilyBirth.People(w).Single(x=>x.Id==head.Value).Site,w.ElapsedSeconds),w.ElapsedSeconds,1);
            w.SealedMessages=[..w.SealedMessages,new(id)];
        }
        RegionalCampaign.Bind(w);
    }
    public const string InviteKind="strategy-invite",InviteReplyKind="strategy-invite-reply";
    // 操作する人物が当主の家が、包囲する相手の城と境を接し、相手と約束がなければ、包囲網の最初の家の当主から誘いの書状が届く。
    static void InviteThePlayer(ContractWorld w,StrategyCoalition c)
    {
        var s=w.Strategy!;var owners=Owners(w,s);var player=w.PlayerControl;
        var target=c.Target.Value;var targetSites=owners.Where(x=>x.Value==target).Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
        var invites=new List<StrategyInvite>();
        foreach(var f in w.NewGame!.Start.Factions.Where(f=>HouseSuccession.Head(w,new(f.Id))==player&&f.Id!=target&&!c.Members.Any(m=>m.Value==f.Id)).OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            bool borders=owners.Where(x=>x.Value==f.Id).Any(x=>Neighbors(x.Key).Any(targetSites.Contains));
            if(!borders||Bound(s.Pacts??[],f.Id,target,w.ElapsedSeconds))continue;
            var sender=HouseSuccession.Head(w,c.Members[0]);if(w.Characters.Single(x=>x.Id==sender).Life!=LifeState.Adult||w.Characters.Single(x=>x.Id==player).Life!=LifeState.Adult)continue;
            string name=w.Definitions.Organizations.Single(o=>o.Id==c.Target).Name;
            string id=$"message:strategy-invite/{ContractJson.Hash(Encoding.UTF8.GetBytes(c.Id.Value+"/"+f.Id))[..16]}";
            MessageFlow.Send(w.Messaging,new MessageEnvelope(id,c.Id.Value,InviteKind,sender.Value,player.Value,$"{name}に備え、{c.Members.Length}家で手を結んだ。貴家も加わられたい。",FamilyBirth.People(w).Single(x=>x.Id==player.Value).Site,w.ElapsedSeconds),w.ElapsedSeconds,1);
            w.SealedMessages=[..w.SealedMessages,new(id)];
            invites.Add(new(new(f.Id),player,new(id),sender));
        }
        if(invites.Count==0)return;
        w.Strategy=s with{Coalitions=s.Coalitions!.Select(x=>x.Id==c.Id?x with{Invites=[..x.Invites??[],..invites]}:x).ToArray()};
        RegionalCampaign.Bind(w);
    }
    // 操作する人物が開いた、まだ答えていない誘い。
    public static (StrategyCoalition Coalition,StrategyInvite Invite)[] OpenInvites(ContractWorld w)=>
        (w.Strategy?.Coalitions??[]).SelectMany(c=>(c.Invites??[]).Select(i=>(c,i)))
        .Where(x=>x.i.Joined==null&&x.i.Head==w.PlayerControl&&HouseSuccession.Head(w,x.i.House)==w.PlayerControl&&MessageFlow.Knows(w.Messaging,x.i.Letter.Value,w.PlayerControl.Value)).ToArray();
    // 誘いに答える：加わるなら包囲網の家に加わり、各家と同盟（二年）を結ぶ。返書が誘った家の当主へ届く。
    internal static void Answer(ContractWorld w,EntityId letter,bool join)
    {
        var (c,i)=OpenInvites(w).SingleOrDefault(x=>x.Invite.Letter==letter);
        if(c==null)throw new InvalidDataException("答えられる誘いではありません");
        var s=w.Strategy!;long now=w.ElapsedSeconds;var date=WorldCalendar.At(w.EpochDate,now);
        var answered=i with{Joined=join,AnsweredAt=now,AnsweredRevision=w.CommitSequence};
        var coalition=c with{Invites=c.Invites!.Select(x=>x.Letter==letter?answered:x).ToArray(),Members=join?[..c.Members,i.House]:c.Members};
        var pacts=(s.Pacts??[]).ToList();
        if(join)foreach(var m in c.Members)pacts.Add(new(new($"pact:national/{date:yyyyMM}/{ContractJson.Hash(Encoding.UTF8.GetBytes(m.Value+"/"+i.House.Value+"/coalition"))[..12]}"),m,i.House,"alliance",now,now+2*365*86400L,w.CommitSequence,"threat:"+c.Target.Value));
        w.Strategy=s with{Coalitions=s.Coalitions!.Select(x=>x.Id==c.Id?coalition:x).ToArray(),Pacts=pacts.Count==0?null:pacts.ToArray()};
        string reply=$"message:strategy-invite-reply/{ContractJson.Hash(Encoding.UTF8.GetBytes(letter.Value))[..16]}";
        MessageFlow.Send(w.Messaging,new MessageEnvelope(reply,c.Id.Value,InviteReplyKind,w.PlayerControl.Value,i.Sender.Value,join?"お誘いに応じ、手を結ぶ。":"このたびのお誘いは辞退する。",FamilyBirth.People(w).Single(x=>x.Id==i.Sender.Value).Site,now),now,1);
        w.SealedMessages=[..w.SealedMessages,new(reply)];
        RegionalCampaign.Bind(w);
    }
    // 京の主が変わったら、将軍から新しい京の主の当主へ書状が届く（将軍家が京の主なら送らない）。
    // 離反を主家へ知らせる（一覧の DefectionLetters を持つ世界だけ）：離れた家の当主から主家の当主へ、書状が実際に運ばれて届く。
    internal static void DefectionLetter(ContractWorld w,StrategyDefection d)
    {
        var sender=HouseSuccession.Head(w,d.House);var head=HouseSuccession.Head(w,d.From);
        if(sender==head||w.Characters.Single(x=>x.Id==sender).Life!=LifeState.Adult||w.Characters.Single(x=>x.Id==head).Life!=LifeState.Adult)return;
        string house=w.Definitions.Organizations.Single(o=>o.Id==d.House).Name;
        string id=$"message:strategy-defection/{ContractJson.Hash(Encoding.UTF8.GetBytes(d.House.Value+"/"+d.From.Value+"/"+w.ElapsedSeconds))[..16]}";
        MessageFlow.Send(w.Messaging,new MessageEnvelope(id,"defection:"+d.House.Value,LetterKind,sender.Value,head.Value,$"{house}は、今後は御家の指図を受けず、自らの判断で家を保ちます。",FamilyBirth.People(w).Single(x=>x.Id==head.Value).Site,w.ElapsedSeconds),w.ElapsedSeconds,1);
        w.SealedMessages=[..w.SealedMessages,new(id)];RegionalCampaign.Bind(w);
    }
    static void CapitalLetter(ContractWorld w,string holder)
    {
        var shogun=NationalStart.FactionId(Shogun);if(holder==shogun||!w.NewGame!.Start.Factions.Any(f=>f.Id==shogun))return;
        var sender=HouseSuccession.Head(w,new(shogun));var head=HouseSuccession.Head(w,new(holder));
        if(sender==head||w.Characters.Single(x=>x.Id==sender).Life!=LifeState.Adult||w.Characters.Single(x=>x.Id==head).Life!=LifeState.Adult)return;
        string name=w.Definitions.Organizations.Single(o=>o.Id.Value==holder).Name;
        string id=$"message:strategy-capital/{ContractJson.Hash(Encoding.UTF8.GetBytes(holder+"/"+w.ElapsedSeconds))[..16]}";
        MessageFlow.Send(w.Messaging,new MessageEnvelope(id,"capital:"+holder,LetterKind,sender.Value,head.Value,$"{name}の上洛を賀す。京の守りを頼む。",FamilyBirth.People(w).Single(x=>x.Id==head.Value).Site,w.ElapsedSeconds),w.ElapsedSeconds,1);
        w.SealedMessages=[..w.SealedMessages,new(id)];RegionalCampaign.Bind(w);
    }
    // 約束の知らせ：同盟は互いの当主へ（相手の当主から）、和睦は将軍から双方の当主へ。
    static void PactLetters(ContractWorld w,StrategyPact p,string shogun)
    {
        string a=w.Definitions.Organizations.Single(o=>o.Id==p.A).Name,b=w.Definitions.Organizations.Single(o=>o.Id==p.B).Name;
        foreach(var (house,other) in new[]{(p.A,p.B),(p.B,p.A)})
        {
            var head=HouseSuccession.Head(w,house);var sender=shogun!=""?new EntityId(shogun):HouseSuccession.Head(w,other);
            if(head==sender||w.Characters.Single(x=>x.Id==head).Life!=LifeState.Adult||w.Characters.Single(x=>x.Id==sender).Life!=LifeState.Adult)continue;
            string body=p.Kind=="alliance"?$"{a}と{b}の同盟を結びたく存じます。":$"将軍の仲裁により、{a}と{b}は和睦いたすべし。";
            string id=$"message:strategy-pact/{ContractJson.Hash(Encoding.UTF8.GetBytes(p.Id.Value+"/"+house.Value))[..16]}";
            MessageFlow.Send(w.Messaging,new MessageEnvelope(id,p.Id.Value,LetterKind,sender.Value,head.Value,body,FamilyBirth.People(w).Single(x=>x.Id==head.Value).Site,w.ElapsedSeconds),w.ElapsedSeconds,1);
            w.SealedMessages=[..w.SealedMessages,new(id)];
        }
        RegionalCampaign.Bind(w);
    }
    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        if(w.Strategy is not {} s){if(Enabled(w)&&prior?.Strategy!=null)throw new InvalidDataException("全国の戦略の記録が消えています");return;}
        if(!Enabled(w)||s.Schema!=1||s.Policy!=Policy)throw new InvalidDataException("全国の戦略の版が不正です");
        if(prior?.Strategy is {} old&&(!s.Campaigns.Take(old.Campaigns.Length).SequenceEqual(old.Campaigns)||!s.Outcomes.Take(old.Outcomes.Length).SequenceEqual(old.Outcomes)))throw new InvalidDataException("過去の出陣の記録が変更されています");
        var factions=w.NewGame!.Start.Factions.Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);
        if(s.Campaigns.Select(c=>c.Id).Distinct().Count()!=s.Campaigns.Length||s.Outcomes.Select(o=>o.Campaign).Distinct().Count()!=s.Outcomes.Length)throw new InvalidDataException("出陣の記録が重複しています");
        foreach(var c in s.Campaigns)
            if(c.Attacker==c.Defender||!factions.Contains(c.Attacker.Value)||!factions.Contains(c.Defender.Value)||c.At<0||c.At>w.ElapsedSeconds||c.Revision<0||c.Revision>w.CommitSequence||!IsMonthStart(w,c.At)||
                !(c.Basis==Border||c.Basis=="betrayal"||c.Basis==Jouraku||c.Basis==Restoration&&StrategyRestoration.Enabled(w)||c.Basis.StartsWith("history:",StringComparison.Ordinal)&&History.Any(e=>"history:"+e.Id==c.Basis))||Owner(w,c.Target,c.At,c.Revision)!=c.Defender)
                throw new InvalidDataException("出陣の記録が不正です");
        foreach(var x in s.Successions??[])if(!factions.Contains(x.House.Value)||x.At>w.ElapsedSeconds||x.Revision>w.CommitSequence||!IsMonthStart(w,x.At)||w.Characters.SingleOrDefault(c=>c.Id==x.Previous)?.Life!=LifeState.Dead||!w.Characters.Any(c=>c.Id==x.Heir))throw new InvalidDataException("家督の記録が不正です");
        if(prior?.Strategy?.Successions is {} oldSuccessions&&!(s.Successions??[]).Take(oldSuccessions.Length).SequenceEqual(oldSuccessions))throw new InvalidDataException("過去の家督の記録が変更されています");
        foreach(var p in s.Pacts??[])if(p.A==p.B||!factions.Contains(p.A.Value)||!factions.Contains(p.B.Value)||p.Kind is not("alliance" or "truce")||p.From>w.ElapsedSeconds||p.Until<=p.From||!IsMonthStart(w,p.From)||p.Revision>w.CommitSequence)throw new InvalidDataException("家どうしの約束の記録が不正です");
        if(prior?.Strategy?.Pacts is {} oldPacts&&!(s.Pacts??[]).Take(oldPacts.Length).Select(p=>p with{Broken=null}).SequenceEqual(oldPacts.Select(p=>p with{Broken=null})))throw new InvalidDataException("過去の約束の記録が変更されています");
        foreach(var o in s.Outcomes){var c=s.Campaigns.SingleOrDefault(c=>c.Id==o.Campaign);if(c==null||o.At<=c.At||o.At>w.ElapsedSeconds||o.Revision>w.CommitSequence||!IsMonthStart(w,o.At)&&c.Real!=true)throw new InvalidDataException("出陣の結果が不正です");if(o.Capture is {} cap&&(c.Real!=true||o.Captured||cap.At!=o.At||cap.FromOwner!=c.Attacker||cap.CaptorOwner!=c.Defender||cap.Site!=c.Target||RealArmy(w,c)?.Commander!=cap.Person))throw new InvalidDataException("出陣の捕縛の記録が不正です");StrategyBattles.Validate(w,o);}
        StrategyRestoration.Validate(w,s,prior?.Strategy);StrategyDefections.Validate(w,s,prior?.Strategy);
    }
}
