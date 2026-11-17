using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// S07B（2026-11-17）：全国の家の内政。一覧（NationalSystems）に Domestic2 のある新規の全国開始だけ。
// 全国の家の戦略（NationalStrategy.Apply）の月の初めに、家ごとの方針・役職・家計・生産、拠点ごとの災害・徴募・逃散・一揆、寺社の権益、幕府職を進める。
// 遠方の家は日々の細かい仕組みでは眠っている（NpcDormancy）ので、全国の家の内政はこの月ごとの計算で行う。値の単位：米は石、銭は貫。
// ・方針と専門職：当主（操作する人物でない）が毎年1月に、本人の技能と家の状況から方針（農・商・軍備・普請・倹約）を決め、家臣を能力で役に置く
//   （家老・普請奉行・勘定奉行・目付・取次・寺社奉行と、国ごとの郡代、拠点ごとの代官）。欠けた役は月の初めに補う。
// ・家計：10月に拠点の石高×年貢の率×作柄×被害×働き手で米が入り、毎月、町の値と人口で銭が入る。給付・軍勢・救済・普請に出す。
//   同じ順位の要求は按分、緊急の救済は軍勢の分から転用して補償を記録する。赤字が3か月続くと、当主が給付の削減か借銀を決める。
// ・監督：代官→郡代→（目付・勘定奉行）→当主の階層。下の役の怠慢・不正は上の役が見つけ、見つけた知らせが当主に届いた時点で罷免する。
// ・生産：職人集団（鉄砲鍛冶・石工・灰吹の技術者）は史実の拠点に、歴史の設定に従って現れる（史実尊重は年表どおり、準史実は前後2年、仮想は開始の時までのものだけ）。
//   鉄砲は工房の鉄、火薬は港の国から入る硝石が原料で、無ければ作れない。金銀は鉱山の国、軍馬は牧の国だけ。5年持った職人の拠点からは、家の本拠に弟子が分かれる。
// ・災害：台風（西国・7〜9月）・大雪（北国・12〜2月）・地震・8月の作柄（豊作・凶作）を国ごとに世界の種から決め、拠点の田畑・町・城の区画の被害として残す。
//   普請奉行が毎月直す（石工のいる家は城の区画も直せる）。知らせは届いた人だけが知る。
// ・徴募と逃散：出陣の兵は攻める家の拠点の働き手から集め（秋の徴募は不満）、決着の後に討死の分を除いて村に帰る。
//   不満の重い拠点からは働き手が隣の拠点へ逃げ（逃散）、不満が収まると戻る。兵と民を二重に作らない（人口は拠点の人口だけ）。
// ・一揆：拠点ごとに飢え・年貢・徴募・被害・落城・不信から、村が嘆願→労働の拒否→逃散→隣村との連合→一揆へ進む。
//   領主（当主が不在・判断できないときは郡代か家老）が救済・減免・鎮圧を選ぶ。年貢の布告は拠点に届いた時から効き、2年に2度以上変えると不信になる。
//   本願寺の檄は石山から門徒の拠点へ運ばれ、届いた拠点が自分で応じるかを決める。米の余る家は、同盟の家の飢えた拠点へも救済の米を送る（運ぶ途中で減る）。
// ・寺社：大寺社は組織として、領主の家との寄進・安堵・禁制（違反を含む）の関係を持つ。楽市で関・座の権益を侵されると、嘆願→非協力→経済の抵抗→敵の家と結ぶ→武装を段階的に選び、
//   領主が権益を残せば収まる。武装した大寺社は、関係の良い家（または結んだ家）の、その国での合戦に僧兵を出す。
// ・破壊の計略：軍備の方針で戦っている家は、敵の境の拠点へ放火を仕掛け、被害が残る。相手の目付が見つけることがある。
// ・幕府職：1560年（1467年）の在職から始め、在職の将軍が探題・管領代・侍所・政所・御相伴衆・国持衆・准国主・申次衆を補任する。
//   探題は自分の地方の戦を和睦させ、攻めに地方の小さな家が加わる。将軍が1年以上空位なら、京の主で源氏の当主は征夷大将軍を、ほかは関白を奏請する。
public sealed record DomesticGoods(long Iron,long Guns,long Saltpeter,long Powder,long Gold,long Silver,long Horses);
public sealed record DomesticPost(string Post,EntityId Person,string? Place,long Since,bool Corrupt);
public sealed record DomesticHouse(EntityId House,string Policy,int TaxRate,long Rice,long Coin,long Debt,long Owed,int Deficit,int StipendCut,DomesticPost[] Posts,DomesticGoods Goods,long At);
public sealed record DomesticSite(string Site,int Agriculture,int Commerce,int Sections,int Unrest,long Fled,string? FledTo,long Levied,string? LevyCampaign,long Lost,int Distrust,string? Ikki,long Relief);
public sealed record DomesticTemple(string Temple,string Site,EntityId? Lord,int Relation,int Stage,EntityId? Ally,bool Conceded,long At);
public sealed record DomesticCraft(string Id,string Kind,string Name,string Site,long Since,string Origin);
public sealed record DomesticEvent(string Id,string Kind,string? Site,EntityId? House,EntityId? Other,long At,long Revision,long Amount,string Text)
{
    // 書状・米・檄が届く時刻（届くまでは効かない）。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? Arrives {get;init;}
}
public sealed record DomesticRumor(string Id,string Text,long OccurredAt,long LearnedAt,EntityId? House);

public static class Domestic2
{
    public const string Name=NationalSystems.Domestic2;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name)&&NationalStrategy.Enabled(w);
    const long Day=86400;
    public const string Farming="農",Trade="商",Arms="軍備",Building="普請",Thrift="倹約";
    public const string Elder="家老",WorksMagistrate="普請奉行",Treasurer="勘定奉行",Inspector="目付",Envoy="取次",TempleMagistrate="寺社奉行",Intendant="郡代",Deputy="代官";

    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"domestic-v1/{w.Seed}/{key}")))%1000);
    static string Hash(string s)=>ContractJson.Hash(Encoding.UTF8.GetBytes(s))[..12];
    static HistoryMode Mode(ContractWorld w)=>w.NewGame!.Judgment?.History??w.NewGame.History??HistoryMode.SemiHistorical;

    // ---- 拠点の固定の値（城の網の拠点） ----
    sealed record SiteInfo(string Site,string? Province,long Koku,double Lat,double Lon);
    static readonly Lazy<Dictionary<string,SiteInfo>> sites=new(()=>
    {
        var nodes=CastleRoutes.Catalog.Nodes.ToArray();
        string? District(CastleRouteNode n)=>HakutaiDb.Castle(n.Castle) is {District:{} d,Province:{} p}?p+"::"+d:null;
        var count=nodes.Select(District).Where(d=>d!=null).GroupBy(d=>d!,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal);
        var first=nodes.ToDictionary(n=>"site:"+n.Id,n=>{var d=District(n);long koku=d!=null&&HakutaiDb.District(d) is {} x?x.Kokudaka/count[d]:0;
            var province=n.Province??HakutaiDb.Castle(n.Castle)?.Province;
            return new SiteInfo("site:"+n.Id,province!=null?HakutaiDb.Kuni(province):null,koku,n.LatitudeE7/1e7,n.LongitudeE7/1e7);},StringComparer.Ordinal);
        // 国・郡の分からない城は、一番近い国の分かる城の国とし、石高はその国の城の中央値（推定）。
        var known=first.Values.Where(x=>x.Province!=null).ToArray();
        var median=known.Where(x=>x.Koku>0).GroupBy(x=>x.Province!,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Koku).Order().ElementAt(g.Count()/2),StringComparer.Ordinal);
        foreach(var x in first.Values.ToArray())
        {
            var p=x.Province??known.MinBy(k=>Math.Pow(k.Lat-x.Lat,2)+Math.Pow((k.Lon-x.Lon)*0.82,2))?.Province;
            first[x.Site]=x with{Province=p,Koku=x.Koku>0?x.Koku:p!=null?median.GetValueOrDefault(p):0};
        }
        return first;
    });
    public static long Koku(string site)=>sites.Value.TryGetValue(site,out var i)?i.Koku:0;
    static string? Province(string site)=>sites.Value.GetValueOrDefault(site)?.Province;
    // 二つの拠点の間の知らせ・荷の日数（1日＋30kmごとに1日）。
    public static long Days(string a,string b)
    {
        if(!sites.Value.TryGetValue(a,out var x)||!sites.Value.TryGetValue(b,out var y))return 1;
        double dy=(x.Lat-y.Lat)*111,dx=(x.Lon-y.Lon)*91;return 1+(long)(Math.Sqrt(dx*dx+dy*dy)/30);
    }
    static readonly HashSet<string> West=new(["山城","大和","河内","和泉","摂津","伊賀","伊勢","志摩","紀伊","淡路","丹波","丹後","但馬","因幡","伯耆","出雲","石見","隠岐","播磨","美作","備前","備中","備後","安芸","周防","長門","阿波","讃岐","伊予","土佐","筑前","筑後","豊前","豊後","肥前","肥後","日向","大隅","薩摩","壱岐","対馬"],StringComparer.Ordinal);
    static readonly HashSet<string> North=new(["陸奥","出羽","越後","越中","能登","加賀","越前","若狭","飛騨","信濃","佐渡","上野","下野"],StringComparer.Ordinal);
    static readonly HashSet<string> Pasture=new(["甲斐","信濃","陸奥","出羽","上野","下野","武蔵","相模","常陸","下総","上総","安房","飛騨"],StringComparer.Ordinal);
    static readonly Dictionary<string,string> Mines=new(StringComparer.Ordinal){["甲斐"]="gold",["陸奥"]="gold",["伊豆"]="gold",["佐渡"]="gold",["石見"]="silver",["但馬"]="silver"};
    static readonly HashSet<string> SaltpeterPorts=new(["和泉","筑前","肥前","豊後","薩摩"],StringComparer.Ordinal);

    // ---- 職人集団（通説の年。史実の拠点に一番近い城の網の拠点） ----
    sealed record CraftSeed(string Id,string Kind,string Name,string Province,double Lat,double Lon,int Year,int? Until=null);
    static readonly CraftSeed[] Seeds=[
        new("tanegashima","gun","種子島の鉄砲鍛冶","大隅",30.73,131.0,1543),
        new("sakai-gun","gun","堺の鉄砲鍛冶","和泉",34.57,135.48,1544),
        new("kunitomo","gun","国友の鉄砲鍛冶","近江",35.43,136.28,1544),
        new("negoro","gun","根来の鉄砲鍛冶","紀伊",34.29,135.32,1544),
        new("hino","gun","日野の鉄砲鍛冶","近江",35.02,136.25,1555),
        new("ano","stone","穴太衆","近江",35.05,135.86,1500),
        new("iwami","silver","石見の灰吹の技術者","石見",35.11,132.44,1533),
        new("ikuno","silver","生野の灰吹の技術者","但馬",35.17,134.8,1542),
        new("kai-gold","gold","甲斐の金山衆","甲斐",35.85,138.68,1500),
        new("sado","gold","佐渡の金山衆","佐渡",38.04,138.25,1601),
    ];
    static readonly Lazy<Dictionary<string,string>> seedSite=new(()=>{var nodes=CastleRoutes.Catalog.Nodes.ToArray();
        return Seeds.ToDictionary(s=>s.Id,s=>"site:"+nodes.Where(n=>n.Province!=null&&HakutaiDb.Kuni(n.Province)==s.Province).DefaultIfEmpty(nodes.MinBy(n=>Math.Pow(n.LatitudeE7/1e7-s.Lat,2)+Math.Pow(n.LongitudeE7/1e7-s.Lon,2))!)
            .MinBy(n=>Math.Pow(n.LatitudeE7/1e7-s.Lat,2)+Math.Pow(n.LongitudeE7/1e7-s.Lon,2))!.Id,StringComparer.Ordinal);});
    // 職人が現れる年（歴史の設定で変わる。仮想は開始の年までのものだけ）。
    static int? CraftYear(ContractWorld w,CraftSeed s)
    {
        int start=NationalSystems.StartYear(w);
        return Mode(w) switch{HistoryMode.Historical=>s.Year,HistoryMode.SemiHistorical=>s.Year<=start?s.Year:s.Year+Draw(w,"craft-year/"+s.Id)%5-2,_=>s.Year<=start?s.Year:null};
    }

    // ---- 大寺社（武装の有無と僧兵の数・関と座の権益は通説の試作値。百代鏡DBに位置の無い寺社は、通説の所在地の位置） ----
    sealed record TempleSeed(string Name,int Monks,bool Rights,double? Lat=null,double? Lon=null);
    static readonly TempleSeed[] MajorTemples=[
        new("石山本願寺",1500,true,34.685,135.526),new("延暦寺",800,true),new("興福寺",600,true),new("金剛峯寺",600,true),new("根来寺",800,true,34.296,135.335),new("粉河寺",300,false,34.276,135.406),
        new("願証寺",500,false,35.08,136.70),new("本證寺",300,false,34.92,137.06),new("勝興寺",300,false,36.79,137.06),new("平泉寺",400,false),new("英彦山",300,false),
        new("東大寺",0,true),new("石清水八幡宮",0,true,34.88,135.70),new("春日大社",0,true,34.681,135.848),new("伊勢神宮",0,true),new("熱田神宮",0,true),new("多賀大社",0,false)];
    static readonly Lazy<(TempleSeed Seed,HakutaiDbTemple Temple,string Site)[]> temples=new(()=>
    {
        var nodes=CastleRoutes.Catalog.Nodes.ToArray();var rows=new List<(TempleSeed,HakutaiDbTemple,string)>();
        foreach(var t in MajorTemples)
        {
            if(HakutaiDbSupplement.Current.Temples.FirstOrDefault(x=>x.Name==t.Name) is not {} db)continue;
            double? lat=db.LatitudeE7/1e7??t.Lat,lon=db.LongitudeE7/1e7??t.Lon;if(lat==null||lon==null)continue;
            var near=nodes.MinBy(n=>{double dx=(n.LongitudeE7/1e7-lon.Value)*0.82,dy=n.LatitudeE7/1e7-lat.Value;return dx*dx+dy*dy;})!;
            rows.Add((t,db,"site:"+near.Id));
        }
        return rows.ToArray();
    });
    public static string[] TempleNames=>temples.Value.Select(t=>t.Temple.Name).ToArray();
    static int Monks(string temple)=>MajorTemples.FirstOrDefault(t=>t.Name==temple)?.Monks??0;
    static string? HonganjiSite=>temples.Value.FirstOrDefault(t=>t.Temple.Name=="石山本願寺").Site;

    // ---- 幕府職（家柄の役職の1560年・1467年の在職。家の名で引く。1600年は幕府が無い） ----
    public const string Kanrei="管領",KanreiDeputy="管領代",Samurai="侍所所司",Mandokoro="政所執事",KantoKanrei="関東管領",Kubo="古河公方",
        OshuTandai="奥州探題",UshuTandai="羽州探題",KyushuTandai="九州探題",Shoban="御相伴衆",Kunimochi="国持衆",JunKokushu="准国主",Moshitsugi="申次衆",Kampaku="関白",
        Tozama="外様衆",Heyashu="御部屋衆",Hokoshu="奉公衆";
    public const string Sashidashi="指出",Kenchi="検地",CastleBreaking="城割り";
    public static readonly string[] Laws=["喧嘩両成敗","婚姻の許可制","他国との書状の禁止","後継の既定順位"];
    static readonly (string Post,string House,int Year)[] InitialPosts=[
        (Kanrei,"細川",1560),(Mandokoro,"伊勢",1560),(KantoKanrei,"上杉",1560),(Kubo,"足利",1560),(OshuTandai,"伊達",1560),(KyushuTandai,"大友",1560),(UshuTandai,"最上",1560),(Samurai,"京極",1560),
        (Kanrei,"細川",1467),(Mandokoro,"伊勢",1467),(KantoKanrei,"上杉",1467),(Kubo,"足利",1467),(OshuTandai,"大崎",1467),(KyushuTandai,"渋川",1467),(UshuTandai,"最上",1467),(Samurai,"京極",1467)];
    static readonly Dictionary<string,HashSet<string>> TandaiRegions=new(StringComparer.Ordinal){[OshuTandai]=new(["陸奥"],StringComparer.Ordinal),[UshuTandai]=new(["出羽"],StringComparer.Ordinal),
        [KyushuTandai]=new(["筑前","筑後","豊前","豊後","肥前","肥後","日向","大隅","薩摩","壱岐","対馬"],StringComparer.Ordinal)};
    static string HouseName(ContractWorld w,EntityId h)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==h)?.Name??h.Value;
    static string PersonName(ContractWorld w,EntityId p)=>w.Definitions.Characters.SingleOrDefault(c=>c.Id==p)?.Name??p.Value;
    static string SiteName(ContractWorld w,string site)=>w.Definitions.Sites.SingleOrDefault(s=>s.Id.Value==site)?.Name??site;
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NewGameState,Dictionary<string,string>> initialHolders=new();
    // 開始の時の幕府職の在職（職→家）。
    public static Dictionary<string,string> InitialHolders(ContractWorld w)=>initialHolders.GetValue(w.NewGame!,data=>
    {
        int year=NationalSystems.StartYear(w);var map=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var (post,house,y) in InitialPosts.Where(x=>x.Year==year))
            if(data.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal).FirstOrDefault(f=>f.Name.StartsWith(house,StringComparison.Ordinal)&&f.Id!=NationalStart.FactionId(NationalStrategy.Shogun)||post==Kubo&&f.Name.Contains("古河",StringComparison.Ordinal)) is {} f)map[post]=f.Id;
        return map;
    });
    // 今の幕府職の在職（職→家）：開始の在職に、補任（StrategyHonor の職の種類）を重ねる。家が城を失えば職は空く。
    public static Dictionary<string,string> Posts(ContractWorld w,long at)=>Posts(w,w.Strategy?.Honors??[],at);
    static Dictionary<string,string> Posts(ContractWorld w,IEnumerable<StrategyHonor> honors,long at)
    {
        var map=new Dictionary<string,string>(InitialHolders(w),StringComparer.Ordinal);
        foreach(var h in honors.Where(h=>h.At<=at&&ShogunatePost(h.Kind)))map[h.Kind]=h.House.Value;
        return map;
    }
    static bool ShogunatePost(string kind)=>kind is Kanrei or KanreiDeputy or Samurai or Mandokoro or KantoKanrei or OshuTandai or UshuTandai or KyushuTandai;

    // ---- 状態の読み出し ----
    public static DomesticHouse? House(ContractWorld w,EntityId house)=>w.Strategy?.Houses?.FirstOrDefault(h=>h.House==house);
    public static DomesticSite Site(ContractWorld w,string site)=>w.Strategy?.Sites?.FirstOrDefault(x=>x.Site==site)??Calm(site);
    static DomesticSite Calm(string site)=>new(site,0,0,0,0,0,null,0,null,0,0,null,0);
    public static DomesticTemple[] Temples(ContractWorld w)=>w.Strategy?.Temples??[];
    public static DomesticCraft[] Crafts(ContractWorld w)=>w.Strategy?.Crafts??[];
    public static DomesticEvent[] Events(ContractWorld w)=>w.Strategy?.Domestic??[];
    // その拠点の今の年貢の率：その家の布告のうち、拠点に届いたものの最後。
    public static int RateAt(ContractWorld w,string site,string house,long at)
    {
        var last=Events(w).LastOrDefault(e=>e.Kind=="decree"&&e.House?.Value==house&&(e.Site==null||e.Site==Province(site))&&e.At+Days(HomeOf(w,house),site)*Day<=at);
        return last!=null?(int)last.Amount:50;
    }
    static string HomeOf(ContractWorld w,string house)=>w.NewGame!.Start.Factions.FirstOrDefault(f=>f.Id==house)?.Home??"";
    static EntityId[] Members(ContractWorld w,string house)=>FamilyBirth.People(w).Where(p=>p.Faction==house&&w.Characters.SingleOrDefault(c=>c.Id.Value==p.Id)?.Life==LifeState.Adult).Select(p=>new EntityId(p.Id)).ToArray();
    static int Skill(ContractWorld w,EntityId p,DevelopmentSkill s)=>Persons2.On(w)?Persons2.Skill(w,p,s,w.ElapsedSeconds):CharacterDevelopment.StartLevel(w,p,s);
    static bool Alive(ContractWorld w,EntityId p)=>w.Characters.SingleOrDefault(c=>c.Id==p)?.Life==LifeState.Adult;
    static bool PlayerHouse(ContractWorld w,string house)=>HouseSuccession.Head(w,new(house))==w.PlayerControl;

    // ---- 月の初め（NationalStrategy.Apply から） ----
    internal static StrategyState Month(ContractWorld w,StrategyState s,Dictionary<string,string?> owners)
    {
        if(!On(w))return s;
        long now=w.ElapsedSeconds,rev=w.CommitSequence;var date=WorldCalendar.At(w.EpochDate,now);int year=date.Year,month=date.Month;
        var events=(s.Domestic??[]).ToList();int before=events.Count;
        var siteRows=(s.Sites??[]).ToDictionary(x=>x.Site,x=>x,StringComparer.Ordinal);
        DomesticSite Get(string site)=>siteRows.GetValueOrDefault(site)??Calm(site);
        void Put(DomesticSite x){if(x with{Site=""}==Calm(""))siteRows.Remove(x.Site);else siteRows[x.Site]=x;}
        // 一年前より前の記録は、残す種類（一揆・檄・寺社・和睦・奏請・職人・楽市・譲歩）と、家と国ごとの最後の布告だけを残す（記録が増え続けないように）。
        if(month==1)
        {
            var lastDecree=events.Where(e=>e.Kind=="decree").GroupBy(e=>e.House?.Value+"/"+e.Site,StringComparer.Ordinal).Select(g=>g.Last().Id).ToHashSet(StringComparer.Ordinal);
            events=events.Where(e=>now-e.At<365*Day||e.Kind is "ikki" or "call" or "temple" or "mediate" or "petition" or "craft-branch" or "rakuichi" or "concede" or "reform"||lastDecree.Contains(e.Id)||e.Arrives is long a&&a>now).ToList();
        }
        var last=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var e in events)last[e.Kind+"|"+(e.Site??e.House?.Value)]=e.At;
        bool Recent(string kind,string key,long days)=>last.TryGetValue(kind+"|"+key,out var at)&&now-at<days*Day;
        var decrees=events.Where(e=>e.Kind=="decree"&&e.House!=null).GroupBy(e=>e.House!.Value.Value,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.Ordinal);
        int Rate(string site,string house)
        {
            if(!decrees.TryGetValue(house,out var list))return 50;var home=HomeOf(w,house);var province=Province(site);
            for(int i=list.Count-1;i>=0;i--){var e=list[i];if((e.Site==null||e.Site==province)&&e.At+Days(home,site)*Day<=now)return (int)e.Amount;}
            return 50;
        }
        void Add(string kind,string? site,EntityId? house,EntityId? other,long amount,string text,long? arrives=null)
        {
            var e=new DomesticEvent($"domestic:{kind}/{date:yyyyMM}/{Hash(kind+"/"+site+"/"+house?.Value+"/"+other?.Value+"/"+events.Count)}",kind,site,house,other,now,rev,amount,text){Arrives=arrives};
            events.Add(e);last[kind+"|"+(site??house?.Value)]=now;
            if(kind=="decree"&&house is {} hh){if(!decrees.TryGetValue(hh.Value,out var l))decrees[hh.Value]=l=[];l.Add(e);}
        }
        var alive=w.Characters.Where(c=>c.Life==LifeState.Adult).Select(c=>c.Id).ToHashSet();
        // 家中：開始の所属に、登用した人（一覧の Persons2）を一人ずつ足す。
        var recruits=Persons2.On(w)?Persons2.Recruits(w):new Dictionary<string,string?>(StringComparer.Ordinal);
        var byFaction=FamilyBirth.People(w).Select(p=>recruits.TryGetValue(p.Id,out var r)?p with{Faction=r}:p).Where(p=>p.Faction!=null&&alive.Contains(new EntityId(p.Id))).GroupBy(p=>p.Faction!,StringComparer.Ordinal)
            .ToDictionary(g=>g.Key,g=>g.Select(p=>(Id:new EntityId(p.Id),Site:p.Site)).ToArray(),StringComparer.Ordinal);
        var power=NationalStrategy.Powers(w,owners);
        var held=owners.Where(x=>x.Value!=null&&sites.Value.ContainsKey(x.Key)).GroupBy(x=>x.Value!,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Key).OrderBy(x=>x,StringComparer.Ordinal).ToArray(),StringComparer.Ordinal);
        // 届く物は、前の月の初めより後から今までに届いたもの（月の初めごとに一度だけ数える）。
        var pm=date.AddMonths(-1);long prevStart=NationalStrategy.MonthStart(w,pm.Year,pm.Month);
        var lastMonth=now-35*Day;
        var recentCampaigns=s.Campaigns.Where(c=>c.At>=lastMonth&&c.At<now).ToArray();
        var settled=s.Outcomes.Where(o=>o.At>lastMonth&&o.At<=now).ToArray();

        // 職人集団：史実の年に現れ、持ち続けた家の本拠に弟子が分かれる。
        var crafts=(s.Crafts??[]).ToList();
        foreach(var seed in Seeds)if(CraftYear(w,seed) is int y&&y<=year&&!crafts.Any(c=>c.Id==seed.Id))crafts.Add(new(seed.Id,seed.Kind,seed.Name,seedSite.Value[seed.Id],now,"history"));
        foreach(var c in crafts.ToArray())
        {
            if(owners.GetValueOrDefault(c.Site) is not {} lord||now-c.Since<5*365*Day||c.Kind is not("gun" or "stone"))continue;
            var home=HomeOf(w,lord);if(home==""||home==c.Site||owners.GetValueOrDefault(home)!=lord||crafts.Any(x=>x.Site==home&&x.Kind==c.Kind))continue;
            if(Draw(w,$"branch/{c.Id}/{year}")<100){crafts.Add(new(c.Id+"/"+Hash(home),c.Kind,c.Name+"の弟子",home,now,c.Id));Add("craft-branch",home,new(lord),null,0,$"{c.Name}の弟子が{SiteName(w,home)}で仕事を始めた");}
        }

        // 災害と作柄（国ごと）。
        var provinces=sites.Value.Values.Where(x=>x.Province!=null&&owners.ContainsKey(x.Site)).GroupBy(x=>x.Province!,StringComparer.Ordinal).OrderBy(g=>g.Key,StringComparer.Ordinal).ToArray();
        var harvest=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var e in events.Where(e=>e.Kind=="harvest"&&WorldCalendar.At(w.EpochDate,e.At).Year==year))harvest[e.Site!]=(int)e.Amount;
        foreach(var g in provinces)
        {
            string p=g.Key;(string Kind,int Agri,int Town,int Wall)? hit=null;
            if(West.Contains(p)&&month is >=7 and <=9&&Draw(w,$"typhoon/{p}/{date:yyyyMM}")<40)hit=("台風",25,10,0);
            else if(North.Contains(p)&&month is 12 or 1 or 2&&Draw(w,$"snow/{p}/{date:yyyyMM}")<60)hit=("大雪",0,10,0);
            else if(Draw(w,$"quake/{p}/{date:yyyyMM}")<3)hit=("地震",5,20,1);
            if(hit is {} h)
            {
                foreach(var x in g){var d=Get(x.Site);Put(d with{Agriculture=Math.Min(90,d.Agriculture+h.Agri),Commerce=Math.Min(90,d.Commerce+h.Town),Sections=Math.Min(4,d.Sections+h.Wall)});}
                Add("disaster",p,null,null,h.Agri+h.Town,$"{p}で{h.Kind}");
            }
            if(month==8){int r=Draw(w,$"harvest/{p}/{year}");int f=r<120?60:r>=880?120:100;if(f!=100){harvest[p]=f;Add("harvest",p,null,null,f,$"{p}は{(f<100?"凶作":"豊作")}の見込み");}}
        }

        // 徴募：前の月に出陣した家は、その家の拠点の働き手から兵を集める。
        // 同じ月に同じ拠点の働き手を二つの出陣が求めたら、村の衆は分けられないので、固定のくじで一方の出陣に出す。
        var claims=new Dictionary<string,List<StrategyCampaign>>(StringComparer.Ordinal);
        foreach(var c in recentCampaigns)if(held.TryGetValue(c.Attacker.Value,out var mine0))foreach(var site in mine0)(claims.TryGetValue(site,out var l)?l:claims[site]=[]).Add(c);
        foreach(var (site,list) in claims.OrderBy(x=>x.Key,StringComparer.Ordinal))
        {
            var d=Get(site);if(d.LevyCampaign!=null)continue;
            var c=list.Count==1?list[0]:list.Single(x=>x.Id.Value==Award(w,"levy/"+site+"/"+date.ToString("yyyyMM"),list.Select(x=>x.Id.Value).ToArray()));
            if(list.Count>1)Add("award",site,c.Attacker,null,list.Count,$"{SiteName(w,site)}の衆は{list.Count}つの出陣のうち一つに出た（くじ）");
            int count=held[c.Attacker.Value].Length;long per=power.GetValueOrDefault(c.Attacker.Value)/2/Math.Max(1,count);
            bool broken=events.Any(e=>e.Kind=="reform"&&e.House==c.Attacker&&e.Text.EndsWith(CastleBreaking,StringComparison.Ordinal));
            long take=Math.Min(per,Workers(w,site,d)*(broken?4:3)/10);if(take<=0)continue;
            Put(d with{Levied=take,LevyCampaign=c.Id.Value,Distrust=d.Distrust+(month is 9 or 10?1:0)});
        }
        // 決着した出陣の兵は、決着の次の月の初めに、討死の分を除いて村に帰る。
        foreach(var x in siteRows.Values.Where(x=>x.LevyCampaign!=null).ToArray())
        {
            var o=s.Outcomes.FirstOrDefault(o=>o.Campaign.Value==x.LevyCampaign);if(o==null||o.At>=now)continue;
            long lost=o.Battle is {} b&&b.AttackerSoldiers>0?x.Levied*b.AttackerLost/b.AttackerSoldiers:0;
            Put(x with{Levied=0,LevyCampaign=null,Lost=x.Lost+lost});
        }
        // 討死・逃散で減った人口は、月に1%ずつ戻る。
        foreach(var x in siteRows.Values.Where(x=>x.Lost>0).ToArray())Put(x with{Lost=Math.Max(0,x.Lost-Population(w,x.Site)/100)});

        // 家ごと。
        var houses=(s.Houses??[]).ToDictionary(h=>h.House.Value,h=>h,StringComparer.Ordinal);
        var pacts=(s.Pacts??[]).ToList();var honors=(s.Honors??[]).ToList();
        foreach(var f in w.NewGame!.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            if(!held.TryGetValue(f.Id,out var mine)||mine.Length==0)continue;
            var house=new EntityId(f.Id);var head=HouseSuccession.Head(w,house);bool player=head==w.PlayerControl;
            var h=houses.GetValueOrDefault(f.Id)??new(house,Farming,50,0,0,0,0,0,0,[],new(0,0,0,0,0,0,0),now);
            var people=byFaction.GetValueOrDefault(f.Id)??[];var members=people.Select(p=>p.Id).Where(p=>p!=head).ToArray();
            // 役：欠けた役（亡くなった・罷免）を補う。操作する人物の家は本人が決める（ここでは補わない）。
            if(!player&&alive.Contains(head))
            {
                var posts=h.Posts.Where(p=>alive.Contains(p.Person)&&members.Contains(p.Person)).ToList();
                var used=posts.Select(p=>p.Person).ToHashSet();
                void Appoint(string post,string? place,DevelopmentSkill a,DevelopmentSkill b)
                {
                    if(posts.Any(p=>p.Post==post&&p.Place==place))return;
                    var pick=members.Where(p=>!used.Contains(p)).OrderByDescending(p=>Skill(w,p,a)+Skill(w,p,b)).ThenBy(p=>p.Value,StringComparer.Ordinal).FirstOrDefault();
                    if(pick.Value==null)return;posts.Add(new(post,pick,place,now,false));used.Add(pick);
                }
                Appoint(Elder,null,DevelopmentSkill.Adjudication,DevelopmentSkill.TalentAssessment);
                foreach(var p in mine.Select(Province).Where(p=>p!=null).Distinct().OrderBy(p=>p,StringComparer.Ordinal))Appoint(Intendant,p,DevelopmentSkill.Agriculture,DevelopmentSkill.Adjudication);
                Appoint(Treasurer,null,DevelopmentSkill.Finance,DevelopmentSkill.Commerce);
                Appoint(WorksMagistrate,null,DevelopmentSkill.Construction,DevelopmentSkill.Irrigation);
                Appoint(Inspector,null,DevelopmentSkill.IntelligenceGathering,DevelopmentSkill.Adjudication);
                Appoint(Envoy,null,DevelopmentSkill.Negotiation,DevelopmentSkill.Etiquette);
                if(mine.Any(x=>temples.Value.Any(t=>t.Site==x)))Appoint(TempleMagistrate,null,DevelopmentSkill.Etiquette,DevelopmentSkill.Writing);
                foreach(var site in mine)
                {
                    if(posts.Any(p=>p.Post==Deputy&&p.Place==site))continue;
                    var here=people.Where(p=>p.Id!=head&&!used.Contains(p.Id)&&p.Site==site).Select(p=>p.Id).OrderBy(p=>p.Value,StringComparer.Ordinal).FirstOrDefault();
                    if(here.Value!=null){posts.Add(new(Deputy,here,site,now,false));used.Add(here);}
                }
                // 方針（毎年1月か、まだないとき）：当主の得意と家の状況。
                string policy=h.Policy;
                if(month==1||h.At==now)
                {
                    int damaged=mine.Count(x=>Get(x).Agriculture+Get(x).Commerce>=30);
                    policy=h.Deficit>=3?Thrift:damaged*3>=mine.Length?Building:new[]{(Farming,Skill(w,head,DevelopmentSkill.Agriculture)),(Trade,Skill(w,head,DevelopmentSkill.Commerce)),(Arms,Math.Max(Skill(w,head,DevelopmentSkill.Tactics),Skill(w,head,DevelopmentSkill.Drill))),(Building,Skill(w,head,DevelopmentSkill.Construction))}
                        .OrderByDescending(x=>x.Item2).ThenBy(x=>x.Item1,StringComparer.Ordinal).First().Item1;
                    if(posts.FirstOrDefault(p=>p.Post==Elder) is {} elder&&Skill(w,elder.Person,DevelopmentSkill.Finance)>=30&&h.Deficit>0)policy=Thrift;
                    if(policy!=h.Policy||h.At==now)Add("policy",null,house,head,0,$"{HouseName(w,house)}の方針：{policy}");
                }
                // 年貢の布告：方針で率を決め、変えた時は布告が拠点へ運ばれる。2年に2度以上変えると不信。
                int rate=policy is Arms or Thrift?60:policy==Farming?40:50;
                if(rate!=h.TaxRate||h.At==now&&rate!=50)
                {
                    bool often=decrees.TryGetValue(f.Id,out var dl)&&dl.Skip(1).Any(e=>e.Site==null&&now-e.At<2*365*Day);
                    Add("decree",null,house,head,rate,$"{HouseName(w,house)}の年貢の布告：{(rate==40?"四公六民":rate==50?"五公五民":"六公四民")}");
                    if(often)foreach(var x in mine){var d=Get(x);Put(d with{Distrust=d.Distrust+1});}
                }
                h=h with{Policy=policy,TaxRate=rate,Posts=posts.ToArray()};
            }

            // 生産。
            var g=h.Goods;var provs=mine.Select(Province).Where(p=>p!=null).Select(p=>p!).ToHashSet(StringComparer.Ordinal);
            long iron=mine.Sum(x=>(long)(Profile(w,x)?.Workshop??10)/10);
            long guns=0;foreach(var c in crafts.Where(c=>c.Kind=="gun"&&mine.Contains(c.Site))){long n=Math.Min(10,g.Iron+iron);guns+=n;iron-=n;}
            bool port=provs.Overlaps(SaltpeterPorts)||pacts.Any(p=>p.Kind=="alliance"&&p.Broken==null&&p.From<=now&&now<p.Until&&(p.A==house||p.B==house)&&held.GetValueOrDefault((p.A==house?p.B:p.A).Value)?.Any(x=>Province(x) is {} q&&SaltpeterPorts.Contains(q))==true);
            long salt=port&&h.Coin>=100&&crafts.Any(c=>c.Kind=="gun"&&mine.Contains(c.Site))?20:0;
            long powder=Math.Min(20,g.Saltpeter+salt);
            long gold=0,silver=0;foreach(var x in mine)if(Province(x) is {} q&&Mines.TryGetValue(q,out var metal)&&(q!="佐渡"||crafts.Any(c=>c.Id=="sado"))){long n=crafts.Any(c=>c.Kind==metal&&Province(c.Site)==q)?3:1;if(metal=="gold")gold+=n;else silver+=n;}
            long horses=mine.Where(x=>Province(x) is {} q&&Pasture.Contains(q)).Sum(x=>(long)(Profile(w,x)?.Pasture??10)/10);
            // 合戦の後は火薬を使う。
            long used2=settled.Count(o=>{var c=s.Campaigns.Single(c=>c.Id==o.Campaign);return c.Attacker==house||c.Defender==house;})*10;
            g=new(Math.Max(0,g.Iron+iron),g.Guns+guns,g.Saltpeter+salt-powder,Math.Max(0,g.Powder+powder-used2),g.Gold+gold,g.Silver+silver,Math.Min(5000,g.Horses+horses));

            // 家計。
            long rice=h.Rice,coin=h.Coin-salt*5,debt=h.Debt,owed=h.Owed;
            var reforms=events.Where(e=>e.Kind=="reform"&&e.House==house).Select(e=>e.Text.Split('：')[^1]).ToHashSet(StringComparer.Ordinal);
            int measured=reforms.Contains(Kenchi)?110:100;
            long yearly=mine.Sum(x=>Koku(x)*Rate(x,f.Id)/100)*measured/100;
            // 新しく帳面を作る家は、半年分の蔵米と、その2割の銭から始める（推定）。
            if(!houses.ContainsKey(f.Id)){rice=yearly/2;coin=yearly/10;}
            if(month==10)
            {
                foreach(var x in mine){var d=Get(x);long pop=Population(w,x);long work=Math.Max(0,pop-d.Fled-d.Levied-d.Lost+FledIn(siteRows,x));
                    long gain=Koku(x)*measured/100*Rate(x,f.Id)/100*harvest.GetValueOrDefault(Province(x)??"",100)/100*(100-d.Agriculture)/100*work/Math.Max(1,pop);
                    // 一揆の拠点と、惣国（一覧の Cases2）の国の拠点からは年貢を取れない。
                    if(d.Ikki!=null||Cases2.Sokoku(w,Province(x)??"",now))gain=0;
                    if(PostAt(h,Deputy,x) is {} dep)gain=gain*(100+Skill(w,dep.Person,DevelopmentSkill.Agriculture)/10)/100;
                    rice+=gain;}
            }
            long trade=mine.Sum(x=>{var d=Get(x);return d.Ikki!=null?0:(long)(Profile(w,x)?.Commerce??20)*Population(w,x)/200000*(100-d.Commerce)/100;});
            if(h.Policy==Trade)trade=trade*12/10;
            if(Posts(w,now).GetValueOrDefault(Mandokoro)==f.Id)trade=trade*105/100;
            // 地域・時代による権限：1573年までは、守護の家の郡代が国の段銭を取る。
            if(year<=1573&&CentralStanding.On(w))foreach(var p in provs.Where(p=>CentralStanding.IsShugo(w,f.Id,p,now)))trade+=sites.Value.Values.Count(x=>x.Province==p)*2;
            coin+=trade+gold*30+silver*20;
            // 不正：代官・郡代の不正は銭を抜く。上の役が見つけ、知らせが当主に届いた時に罷免する。
            var plist=h.Posts.ToList();
            for(int i=0;i<plist.Count;i++)
            {
                var p=plist[i];if(p.Post is not(Deputy or Intendant))continue;
                int honest=Skill(w,p.Person,DevelopmentSkill.Adjudication)+Skill(w,p.Person,DevelopmentSkill.Finance);
                if(!p.Corrupt&&Draw(w,$"corrupt/{p.Person.Value}/{date:yyyyMM}")<Math.Max(1,5-honest/20-reforms.Count(r=>Laws.Contains(r)))){plist[i]=p=p with{Corrupt=true};}
                if(!p.Corrupt)continue;
                coin-=Math.Max(1,trade/20);
                var over=p.Post==Deputy?plist.FirstOrDefault(x=>x.Post==Intendant&&x.Place==Province(p.Place??"")):plist.FirstOrDefault(x=>x.Post==Inspector)??plist.FirstOrDefault(x=>x.Post==Treasurer);
                int eye=over is {} o?100+Skill(w,o.Person,DevelopmentSkill.IntelligenceGathering)*3+Skill(w,o.Person,DevelopmentSkill.Adjudication)*2:30;
                if(Draw(w,$"find/{p.Person.Value}/{date:yyyyMM}")<eye)
                {
                    string where=p.Place??HomeOf(w,f.Id);long arrive=now+Days(sites.Value.ContainsKey(where)?where:HomeOf(w,f.Id),HomeOf(w,f.Id))*Day;
                    Add("finding",p.Place,house,p.Person,0,$"{(over is {} oo?PersonName(w,oo.Person)+"（"+oo.Post+"）":"当主")}が{PersonName(w,p.Person)}（{p.Post}）の不正を見つけた",arrive);
                    if(!player){plist.RemoveAt(i);i--;Add("dismiss",p.Place,house,p.Person,0,$"{PersonName(w,p.Person)}を{p.Post}から罷免",arrive);}
                }
            }
            h=h with{Posts=plist.ToArray()};

            // 支出の要求：給付・軍勢（同じ順位）、救済（緊急なら軍勢の分から転用）、普請。
            long stipend=yearly*35/1000*(100-h.StipendCut)/100;
            long army=recentCampaigns.Where(c=>c.Attacker==house||c.Defender==house).Sum(_=>power.GetValueOrDefault(f.Id)*15/100);
            var hungry=mine.Where(x=>Get(x).Unrest>=2&&!Recent("relief",x,90)&&Hungry(Get(x),harvest.GetValueOrDefault(Province(x)??"",100),Rate(x,f.Id))).ToArray();
            long relief=hungry.Sum(x=>Population(w,x)/10);
            var (paid,cut)=Allocate(rice,[stipend,army]);
            if(cut*2>stipend+army&&!Recent("allocate",f.Id,365))Add("allocate",null,house,null,cut,$"{HouseName(w,house)}の蔵米が足りず、給付と軍勢の米を按分（不足{cut}石）");
            rice-=paid.Sum();
            long reliefPaid=Math.Min(relief,Math.Max(0,rice));rice-=reliefPaid;
            if(reliefPaid<relief&&paid[1]>0&&hungry.Any(x=>Get(x).Unrest>=4))
            {long divert=Math.Min(paid[1]/2,relief-reliefPaid);reliefPaid+=divert;owed+=divert;Add("divert",null,house,null,divert,$"{HouseName(w,house)}は軍勢の米{divert}石を飢えた村へ回した（後で補う）");}
            if(owed>0&&rice>owed*2){rice-=owed;owed=0;}
            foreach(var x in hungry){var d=Get(x);if(reliefPaid<=0)break;long share=Math.Min(reliefPaid,Population(w,x)/10);reliefPaid-=share;
                // 救済の米は本拠から運ばれる。道のりで遅れ、賊の巣の近くでは一割が失われる。
                long days=Days(HomeOf(w,f.Id),x)+share/500;bool lost=Territory2.IsCamp(w,new(x))||NationalStrategy.Neighbors(x).Any(n=>Territory2.IsCamp(w,new(n)));
                long arrive2=share-(lost?share/10:0)-share*Days(HomeOf(w,f.Id),x)/100;
                Add("relief",x,house,null,arrive2,$"{HouseName(w,house)}が{SiteName(w,x)}へ救済の米を送った",now+days*Day);
                if(lost)Add("loss",x,house,null,share-arrive2,$"{SiteName(w,x)}への救済の米が道中で減った（{share-arrive2}石）");}
            // 普請：普請奉行が被害を直す（城の区画は石工のいる家だけ）。
            if(h.Posts.FirstOrDefault(p=>p.Post==WorksMagistrate) is {} wm&&coin>0)
            {
                int pts=5+Skill(w,wm.Person,DevelopmentSkill.Construction)/10+(h.Policy==Building?5:0);bool stone=crafts.Any(c=>c.Kind=="stone"&&mine.Contains(c.Site));
                var done=new List<string>();int total=0;
                foreach(var x in mine.Where(x=>{var d=Get(x);return d.Agriculture+d.Commerce+d.Sections>0&&d.Unrest<2;}).Take(3))
                {var d=Get(x);int a=Math.Min(d.Agriculture,pts),t=Math.Min(d.Commerce,pts),sc=stone&&d.Sections>0?1:0;if(a+t+sc==0)continue;long cost=(a+t)*20+sc*200;if(coin<cost)break;coin-=cost;
                 Put(d with{Agriculture=d.Agriculture-a,Commerce=d.Commerce-t,Sections=d.Sections-sc});done.Add(SiteName(w,x));total+=a+t+sc;}
                // 普請の知らせは、家ごと・月ごとに一つにまとめる。
                if(done.Count>0)Add("repair",null,house,wm.Person,total,$"{PersonName(w,wm.Person)}（普請奉行）が{string.Join("・",done)}を直した");
            }
            // 赤字の判断：米が負なら銭で買う、銭も無ければ借銀。3か月続けば当主が給付を削る（倹約）か借銀を重ねる。
            if(rice<0){long buy=-rice;coin-=buy;rice=0;}
            // 一年分の年貢を超える蔵米と半年分を超える銭は、家中の諸費（城下の普請・贈答・兵の支度）に毎月1割ずつ払い出す。
            if(rice>yearly)rice-=(rice-yearly)/10;
            if(coin>yearly/2)coin-=(coin-yearly/2)/10;
            int deficit=coin<0?h.Deficit+1:0;
            if(coin<0){long need=-coin;if(g.Gold+g.Silver>0){long sell=Math.Min(need/25+1,g.Gold+g.Silver);coin+=sell*25;g=g with{Gold=Math.Max(0,g.Gold-sell),Silver=Math.Max(0,g.Silver-Math.Max(0,sell-g.Gold))};}
                if(coin<0){debt+=-coin;coin=0;}}
            debt+=debt*2/100;if(debt>0&&coin>debt){coin-=debt;debt=0;}
            int stipendCut=h.StipendCut;
            if(deficit>=3&&!player)
            {
                if(h.Policy==Thrift||debt>yearly){stipendCut=Math.Min(50,stipendCut+20);Add("cut",null,house,head,stipendCut,$"{HouseName(w,house)}は家臣の給付を{stipendCut}%減らした");}
                else Add("borrow",null,house,head,debt,$"{HouseName(w,house)}は商人から借銀（{debt}貫）");
                deficit=0;
            }
            else if(stipendCut>0&&debt==0&&coin>yearly/4&&!player){stipendCut=Math.Max(0,stipendCut-10);Add("raise",null,house,head,stipendCut,$"{HouseName(w,house)}は家臣の給付を戻した");}
            // 改革（毎年1月、操作する人物の家でない）：指出→検地（城5つ以上）、家老の裁許30以上なら分国法の条文を一つずつ、検地の済んだ軍備の家（城8つ以上）は城割り。
            // 改革のたびに、従う家（国人）のうち一つが固定のくじで反発し、その家の拠点の不信が増える。
            if(!player&&month==1&&alive.Contains(head))
            {
                string? pick=null;
                if(!reforms.Contains(Sashidashi)&&h.Policy is Farming or Thrift)pick=Sashidashi;
                else if(reforms.Contains(Sashidashi)&&!reforms.Contains(Kenchi)&&mine.Length>=5)pick=Kenchi;
                else if(h.Posts.FirstOrDefault(p=>p.Post==Elder) is {} el&&Skill(w,el.Person,DevelopmentSkill.Adjudication)>=30&&Laws.FirstOrDefault(l=>!reforms.Contains(l)) is {} law)pick=law;
                else if(reforms.Contains(Kenchi)&&!reforms.Contains(CastleBreaking)&&h.Policy==Arms&&mine.Length>=8)pick=CastleBreaking;
                if(pick!=null)
                {
                    Add("reform",null,house,head,0,$"{HouseName(w,house)}の改革：{pick}");
                    var vassal=held.Keys.Where(v=>v!=f.Id&&NationalStrategy.DbOverlord(w,v)==f.Id).OrderBy(v=>Draw(w,$"reform-resist/{f.Id}/{pick}/{v}")).FirstOrDefault();
                    if(vassal!=null&&Draw(w,$"reform-resist/{f.Id}/{pick}")<400){Add("resist",null,new(vassal),house,0,$"{HouseName(w,new(vassal))}が{HouseName(w,house)}の{pick}に反発");foreach(var x in held[vassal]){var d=Get(x);Put(d with{Distrust=d.Distrust+1});}}
                }
            }
            // 与姓・偏諱：毎年12月、当主が勲功の多い家臣（2度以上、まだ受けていない人）に、名の一字（偏諱）か姓（与姓）を与える。
            if(!player&&month==12&&alive.Contains(head))
            {
                var merit=(s.Merits??[]).Where(m=>m.House==house&&m.Person!=head&&alive.Contains(m.Person)).GroupBy(m=>m.Person).Where(g=>g.Count()>=2&&!honors.Any(x=>x.Person==g.Key&&x.Kind is "偏諱" or "与姓"))
                    .OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key.Value,StringComparer.Ordinal).Select(g=>g.Key).FirstOrDefault();
                if(merit.Value!=null&&NationalStart.DbPerson(head.Value) is {} lordDb)
                {
                    bool kin=NationalStart.DbPerson(merit.Value)?.Surname==lordDb.Surname;
                    if(!kin&&lordDb.Surname is {Length:>0} sur&&Draw(w,$"yosei/{merit.Value}")<300){honors.Add(new(house,merit,"与姓",head,sur,now,rev));Add("yosei",null,house,merit,0,$"{PersonName(w,head)}が{PersonName(w,merit)}に「{sur}」の姓を与えた");}
                    else if(lordDb.Imina is {Length:>=2} imina){honors.Add(new(house,merit,"偏諱",head,imina[..1],now,rev));Add("henki",null,house,merit,0,$"{PersonName(w,head)}が{PersonName(w,merit)}に「{imina[..1]}」の字を与えた");}
                }
            }
            houses[f.Id]=h with{Rice=Math.Max(0,rice),Coin=Math.Max(0,coin),Debt=debt,Owed=owed,Deficit=deficit,StipendCut=stipendCut,Goods=g,At=now};

            // 破壊の計略：軍備の方針で戦っている家は、敵の境の拠点へ放火を仕掛ける。
            if(!player&&h.Policy==Arms)
            {
                var enemy=s.Campaigns.Where(c=>now-c.At<180*Day&&(c.Attacker==house||c.Defender==house)).Select(c=>c.Attacker==house?c.Defender.Value:c.Attacker.Value).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).FirstOrDefault();
                var target=enemy==null?null:mine.SelectMany(x=>NationalStrategy.Neighbors(x)).Where(n=>owners.GetValueOrDefault(n)==enemy&&sites.Value.ContainsKey(n)).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).FirstOrDefault();
                if(target!=null&&Draw(w,$"arson/{f.Id}/{date:yyyyMM}")<200)
                {
                    var d=Get(target);Put(d with{Commerce=Math.Min(90,d.Commerce+25),Sections=Math.Min(4,d.Sections+1)});
                    Add("arson",target,house,new(enemy!),25,$"{SiteName(w,target)}で火の手（{HouseName(w,new(enemy!))}の拠点）");
                    var eh=houses.GetValueOrDefault(enemy!);var insp=eh?.Posts.FirstOrDefault(p=>p.Post==Inspector);
                    if(insp!=null&&Draw(w,$"arson-caught/{f.Id}/{date:yyyyMM}")<200+Skill(w,insp.Person,DevelopmentSkill.IntelligenceGathering)*5)
                        Add("caught",target,new(enemy!),house,0,$"{PersonName(w,insp.Person)}（目付）が放火は{HouseName(w,house)}の仕業と突き止めた");
                }
            }
        }

        // 届いた救済で、村の不満が下がる。
        foreach(var e in events.Where(e=>e.Kind=="relief"&&e.Arrives is long a&&a>prevStart&&a<=now&&e.Site!=null)){var d=Get(e.Site!);Put(d with{Relief=d.Relief+e.Amount,Unrest=Math.Max(0,d.Unrest-2)});}

        // 拠点の村の判断：嘆願→労働の拒否→逃散→連合→一揆。
        var owned=owners.Where(x=>x.Value!=null&&sites.Value.ContainsKey(x.Key)).OrderBy(x=>x.Key,StringComparer.Ordinal).ToArray();
        var captured=settled.Where(o=>o.Captured).Select(o=>s.Campaigns.Single(c=>c.Id==o.Campaign).Target.Value).ToHashSet(StringComparer.Ordinal);
        var stage=owned.ToDictionary(x=>x.Key,x=>Get(x.Key).Unrest,StringComparer.Ordinal);
        foreach(var (site,lord) in owned)
        {
            var d=Get(site);var house=new EntityId(lord!);var h=houses.GetValueOrDefault(lord!);
            int rate=Rate(site,lord!);int hv=harvest.GetValueOrDefault(Province(site)??"",100);
            int grievance=(rate-50)/5+(hv<100?3:0)+d.Agriculture/15+(d.Levied>0&&month is 9 or 10 or 11?2:0)+(captured.Contains(site)?3:0)+d.Distrust-(int)Math.Min(3,d.Relief/Math.Max(1,Population(w,site)/10));
            int target=grievance>=10?5:grievance>=8?4:grievance>=6?3:grievance>=4?2:grievance>=2?1:0;
            int next=d.Ikki!=null?5:target>d.Unrest?d.Unrest+1:target<d.Unrest?d.Unrest-1:d.Unrest;
            if(next==4&&!NationalStrategy.Neighbors(site).Any(n=>stage.GetValueOrDefault(n)>=3))next=3;
            // 嘆願（一段目）は領主への書状なので、本人の家のものだけを記録に残す（遠方の家の嘆願は誰も知らない）。
            if(next>d.Unrest&&(next>=2||PlayerHouse(w,lord!)&&!Recent("unrest",site,365)))
            {
                string text=next switch{1=>"年貢を減じてくだされと嘆願",2=>"普請の労働を拒む",3=>"働き手が逃げ出した（逃散）",4=>"隣の村と結んだ",_=>"一揆を起こした"};
                Add(next==5?"ikki":"unrest",site,house,null,next,$"{SiteName(w,site)}の村が{text}",now+Days(site,HomeOf(w,lord!))*Day);
            }
            long fled=d.Fled;string? to=d.FledTo;
            if(next>=3){var dest=NationalStrategy.Neighbors(site).Where(sites.Value.ContainsKey).OrderBy(n=>stage.GetValueOrDefault(n)).ThenBy(n=>n,StringComparer.Ordinal).FirstOrDefault();
                if(dest!=null){fled=Math.Min(Population(w,site)/2,fled+Population(w,site)/20);to=dest;}}
            else if(next<=1&&fled>0){fled=Math.Max(0,fled-Math.Max(1,fled/3));if(fled==0)to=null;}
            string? ikki=next==5?d.Ikki??"村":null;
            // 領主の対応（一揆と連合）：当主が不在・判断できなければ郡代か家老。操作する人物の家は本人が決める。
            if(next>=4&&h!=null&&!PlayerHouse(w,lord!))
            {
                var head=HouseSuccession.Head(w,house);bool away=SocialWorld.Site(w,head)?.Value!=HomeOf(w,lord!)||MortalSuccession.Incapacitated(w,head,now);
                var proxy=away?h.Posts.FirstOrDefault(p=>p.Post==Intendant&&p.Place==Province(site))??h.Posts.FirstOrDefault(p=>p.Post==Elder):null;
                string who=proxy!=null?$"{PersonName(w,proxy.Person)}（{proxy.Post}、当主に代わり）":PersonName(w,head);
                if(h.Rice>=Population(w,site)/5){houses[lord!]=h=h with{Rice=h.Rice-Population(w,site)/5};next=Math.Max(1,next-3);ikki=null;Add("negotiate",site,house,proxy?.Person??head,Population(w,site)/5,$"{who}が{SiteName(w,site)}の村に米を出して和解");}
                else if(h.Policy is Arms&&power.GetValueOrDefault(lord!)>Population(w,site)){next=0;ikki=null;d=d with{Distrust=d.Distrust+3,Lost=d.Lost+Population(w,site)/20};Add("suppress",site,house,proxy?.Person??head,0,$"{who}が{SiteName(w,site)}の一揆を鎮めた（不信が残る）");}
                else{Add("decree",Province(site),house,proxy?.Person??head,Math.Max(30,rate-10),$"{who}が{Province(site)}の年貢を減じると布告");next=Math.Max(1,next-2);ikki=null;}
            }
            if(next>=3&&(hv<100||d.Agriculture>=30)){d=d with{Lost=d.Lost+Population(w,site)/100};if(!Recent("famine",site,365))Add("famine",site,house,null,Population(w,site)/100,$"{SiteName(w,site)}で飢えて亡くなる者が出た");}
            stage[site]=next;
            Put(d with{Unrest=next,Fled=fled,FledTo=to,Ikki=ikki,Relief=Math.Max(0,d.Relief-Population(w,site)/20),Distrust=Math.Max(0,d.Distrust-(month==1?1:0))});
        }

        // 本願寺の檄：史実（1570年9月、史実尊重・準史実）か、本願寺が武装して領主と敵対した月に、石山から門徒の拠点へ。
        var templeRows=(s.Temples??[]).ToDictionary(t=>t.Temple,t=>t,StringComparer.Ordinal);
        bool honganjiArmed=templeRows.TryGetValue("石山本願寺",out var hg)&&hg.Stage>=5;
        bool historical=Mode(w)!=HistoryMode.Fictional&&year==1570&&month==9;
        if(HonganjiSite is {} ishiyama&&(historical||honganjiArmed&&hg!.At==now)&&!events.Any(e=>e.Kind=="call"&&now-e.At<5*365*Day))
        {
            var enemy=honganjiArmed?hg!.Lord:NationalStrategy.Capital(w);
            Add("call",ishiyama,null,enemy,0,$"本願寺が門徒に檄を発した{(enemy is {} en?$"（{HouseName(w,en)}を仏敵と）":"")}");
        }
        foreach(var call in events.Where(e=>e.Kind=="call").ToArray())
            foreach(var (site,lord) in owned)
            {
                if(Profile(w,site)?.Monto is not >0||events.Any(e=>e.Kind=="answer"&&e.Site==site&&e.Other?.Value==call.Id))continue;
                long arrives=call.At+Days(call.Site!,site)*Day;if(arrives>now)continue;
                var d=Get(site);bool join=(d.Unrest>=2||call.Other?.Value==lord)&&Draw(w,$"answer/{call.Id}/{site}")<600;
                events.Add(new($"domestic:answer/{Hash(call.Id+"/"+site)}","answer",site,new(lord!),new(call.Id),now,rev,join?1:0,$"{SiteName(w,site)}の門徒は檄に{(join?"応じて蜂起した":"応じなかった")}"));
                if(join)Put(d with{Unrest=5,Ikki="本願寺"});
            }

        // 同盟の家への広域救済：米が給付の半年分より多い家は、同盟の家の一揆の拠点へ米を送る。
        foreach(var p in pacts.Where(p=>p.Kind=="alliance"&&p.Broken==null&&p.From<=now&&now<p.Until).OrderBy(p=>p.Id.Value,StringComparer.Ordinal))
            foreach(var (giver,taker) in new[]{(p.A.Value,p.B.Value),(p.B.Value,p.A.Value)})
            {
                if(!houses.TryGetValue(giver,out var gh)||PlayerHouse(w,giver)||!held.TryGetValue(taker,out var theirs))continue;
                var need=theirs.FirstOrDefault(x=>Get(x).Unrest>=3);if(need==null)continue;
                long amount=Population(w,need)/10;if(gh.Rice<amount*6)continue;
                houses[giver]=gh with{Rice=gh.Rice-amount};
                Add("relief",need,new(giver),new(taker),amount,$"{HouseName(w,new(giver))}が同盟の{HouseName(w,new(taker))}の{SiteName(w,need)}へ救済の米を送った",now+Days(HomeOf(w,giver),need)*Day);
            }

        // 寺社。
        foreach(var (seed,temple,site) in temples.Value)
        {
            var lordId=owners.GetValueOrDefault(site);
            var t=templeRows.GetValueOrDefault(temple.Name)??new(temple.Name,site,null,0,0,null,false,now);
            var lord=lordId!=null?new EntityId(lordId):(EntityId?)null;
            if(t.Lord!=lord&&lord is {} nl)
            {
                bool took=captured.Contains(site);var h=houses.GetValueOrDefault(nl.Value);
                if(took){Add("ban",site,nl,null,0,$"{HouseName(w,nl)}が{temple.Name}に禁制を与えた");
                    if(Draw(w,$"violate/{temple.Name}/{date:yyyyMM}")<300){t=t with{Relation=t.Relation-3};Add("violation",site,nl,null,0,$"{HouseName(w,nl)}の兵が{temple.Name}の門前で乱暴（禁制違反）");}}
                if(h?.Policy!=Trade){t=t with{Relation=t.Relation+2};Add("protect",site,nl,null,0,$"{HouseName(w,nl)}が{temple.Name}の寺領を安堵");}
                t=t with{Lord=nl,Stage=0,Conceded=false,At=now};
            }
            if(lord is {} l&&houses.GetValueOrDefault(l.Value) is {} lh&&!PlayerHouse(w,l.Value))
            {
                if(month==1&&lh.Coin>500&&lh.Policy!=Arms){houses[l.Value]=lh=lh with{Coin=lh.Coin-100};t=t with{Relation=t.Relation+1+(lh.Posts.Any(p=>p.Post==TempleMagistrate)?1:0)};Add("donate",site,l,null,100,$"{HouseName(w,l)}が{temple.Name}に寄進");}
                // 楽市（商の方針、1567年から）で関・座の権益を侵す。
                bool infringe=seed.Rights&&lh.Policy==Trade&&year>=1567&&!t.Conceded;
                if(infringe&&!events.Any(e=>e.Kind=="rakuichi"&&e.House==l))Add("rakuichi",null,l,null,0,$"{HouseName(w,l)}が楽市を布告（関・座の特権を認めない）");
                int cap=seed.Monks>0?5:4;
                if(infringe&&t.Relation<5&&t.Stage<cap&&Draw(w,$"temple/{temple.Name}/{date:yyyyMM}")<350)
                {
                    int st=t.Stage+1;EntityId? ally=t.Ally;
                    if(st==4)ally=s.Campaigns.Where(c=>c.Attacker==l||c.Defender==l).OrderByDescending(c=>c.At).Select(c=>c.Attacker==l?c.Defender:c.Attacker).FirstOrDefault(x=>x.Value!=null&&held.ContainsKey(x.Value));
                    if(st==4&&ally?.Value==null){st=3;ally=null;}
                    string text=st switch{1=>"関・座は古来の権と嘆願",2=>"取次・仲裁を断った",3=>"座の商人に商いを止めさせた",4=>$"{HouseName(w,ally!.Value)}と結んだ",_=>"僧兵を集めて敵対した"};
                    t=t with{Stage=st,Ally=ally,At=now};Add("temple",site,l,ally,st,$"{temple.Name}が{text}");
                }
                if(t.Stage>=3){var d=Get(site);Put(d with{Commerce=Math.Min(90,d.Commerce+10)});}
                // 領主が権益の一部を残して収める（寺が強いか、領主が倹約）。
                if(t.Stage>=3&&(power.GetValueOrDefault(l.Value)<seed.Monks*3||lh.Policy==Thrift)){t=t with{Stage=0,Ally=null,Conceded=true,Relation=t.Relation+1,At=now};Add("concede",site,l,null,0,$"{HouseName(w,l)}が{temple.Name}の関・座の権益の一部を残した");}
            }
            templeRows[temple.Name]=t;
        }

        // 幕府職：在職の将軍（操作する人物でない）が補任する。探題は地方の戦を和睦させる。
        if(CentralStanding.On(w)&&SupremeOffice.ShogunAt(w,now,rev) is {} shogun&&shogun!=w.PlayerControl)
        {
            var posts=Posts(w,honors,now);
            void Grant(string kind,string house){var head=HouseSuccession.Head(w,new(house));if(!Alive(w,head))return;honors.Add(new(new(house),head,kind,shogun,null,now,rev));}
            foreach(var (post,region) in TandaiRegions)
            {
                var incumbent=posts.GetValueOrDefault(post);
                if(incumbent!=null&&held.GetValueOrDefault(incumbent)?.Any(x=>Province(x) is {} q&&region.Contains(q))==true)continue;
                var top=held.Where(x=>x.Value.Any(y=>Province(y) is {} q&&region.Contains(q))).OrderByDescending(x=>x.Value.Count(y=>Province(y) is {} q&&region.Contains(q))).ThenBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key).FirstOrDefault();
                if(top!=null&&top!=incumbent)Grant(post,top);
            }
            if(NationalStrategy.Capital(w) is {} cap&&cap.Value!=NationalStart.FactionId(NationalStrategy.Shogun))
            {
                if(posts.GetValueOrDefault(KanreiDeputy)!=cap.Value)Grant(KanreiDeputy,cap.Value);
                if(posts.GetValueOrDefault(Samurai) is not {} sm||!held.ContainsKey(sm))Grant(Samurai,cap.Value);
                if(posts.GetValueOrDefault(Mandokoro) is not {} md||!held.ContainsKey(md))Grant(Mandokoro,cap.Value);
                if(houses.GetValueOrDefault(cap.Value)?.Posts.FirstOrDefault(p=>p.Post==Envoy) is {} envoy&&!honors.Any(x=>x.Kind==Moshitsugi&&x.Person==envoy.Person))honors.Add(new(cap,envoy.Person,Moshitsugi,shogun,null,now,rev));
            }
            foreach(var house in held.Keys.OrderBy(x=>x,StringComparer.Ordinal))
            {
                int shugo=CentralStanding.ShugoProvinces(w,house,now).Length;bool Has(string k)=>honors.Any(x=>x.House.Value==house&&x.Kind==k);
                if(shugo>=2&&!Has(Shoban))Grant(Shoban,house);
                if(shugo>=1&&!Has(Kunimochi))Grant(Kunimochi,house);
                if(shugo==0&&held[house].Length>=8&&!Has(JunKokushu))Grant(JunKokushu,house);
                // 外様衆：畿内の外の、城3つ以上の家で、将軍家に従わない家の当主。御部屋衆：京に城を持つ家の当主。
                bool kinai=held[house].Any(x=>Province(x) is "山城" or "大和" or "河内" or "和泉" or "摂津");
                if(!kinai&&held[house].Length>=3&&NationalStrategy.DbOverlord(w,house)!=NationalStart.FactionId(NationalStrategy.Shogun)&&!Has(Tozama))Grant(Tozama,house);
                if(held[house].Any(NationalStrategy.InKyoto)&&!Has(Heyashu))Grant(Heyashu,house);
            }
            // 探題の和睦：地方の家どうしの戦（二か月前の出陣で、将軍の和睦の無かったもの）を、探題の家が月に15%で一年の和睦にする。
            foreach(var c in s.Campaigns.Where(c=>c.At>=now-65*Day&&c.At<lastMonth&&c.Basis==NationalStrategy.Border))
                foreach(var (post,region) in TandaiRegions)
                {
                    if(Province(c.Target.Value) is not {} q||!region.Contains(q)||posts.GetValueOrDefault(post) is not {} tandai||tandai==c.Attacker.Value||tandai==c.Defender.Value)continue;
                    if(pacts.Any(p=>p.Broken==null&&now<p.Until&&(p.A==c.Attacker&&p.B==c.Defender||p.A==c.Defender&&p.B==c.Attacker))||Draw(w,$"tandai/{c.Id.Value}")>=150)continue;
                    if(PlayerHouse(w,c.Attacker.Value)||PlayerHouse(w,c.Defender.Value))continue;
                    pacts.Add(new(new($"pact:national/{date:yyyyMM}/{Hash(c.Attacker.Value+"/"+c.Defender.Value+"/tandai")}"),c.Attacker,c.Defender,"truce",now,now+365*Day,rev,"tandai:"+post));
                    Add("mediate",c.Target.Value,new(tandai),c.Attacker,0,$"{HouseName(w,new(tandai))}（{post}）が{HouseName(w,c.Attacker)}と{HouseName(w,c.Defender)}を和睦させた");
                }
        }
        // 奏請：将軍が1年以上空位なら、毎年1月に京の主（操作する人物の家でない）が、源氏なら征夷大将軍、ほかは関白を奏請する。
        var shoguns=(s.Shoguns??[]).ToList();
        if(month==1&&CentralStanding.On(w)&&NationalStrategy.Capital(w) is {} holder&&!PlayerHouse(w,holder.Value)&&held.TryGetValue(holder.Value,out var hs)&&hs.Length*10>=owners.Count)
        {
            var head=HouseSuccession.Head(w,holder);var vacant=shoguns.LastOrDefault();
            bool longVacant=SupremeOffice.ShogunAt(w,now,rev) is null&&vacant?.Kind=="vacant"&&now-vacant.At>=365*Day;
            if(longVacant&&Alive(w,head)&&head!=w.PlayerControl)
            {
                bool genji=PersonIdentity.Lineage(w,head)?.Value=="源氏";
                var kyotoSite=hs.FirstOrDefault(NationalStrategy.InKyoto);
                if(genji&&kyotoSite!=null){shoguns.Add(new("installed",holder,head,new(kyotoSite),now,rev));Add("petition",null,holder,head,0,$"{PersonName(w,head)}が征夷大将軍に補任された");}
                else if(!genji&&!honors.Any(x=>x.Kind==Kampaku)){honors.Add(new(holder,head,Kampaku,head,null,now,rev));Add("petition",null,holder,head,0,$"{PersonName(w,head)}が関白に任じられた");}
            }
        }

        var siteArray=siteRows.Values.OrderBy(x=>x.Site,StringComparer.Ordinal).ToArray();
        return s with{Houses=houses.Values.OrderBy(h=>h.House.Value,StringComparer.Ordinal).ToArray(),Sites=siteArray.Length==0?null:siteArray,
            Temples=templeRows.Values.OrderBy(t=>t.Temple,StringComparer.Ordinal).ToArray(),Crafts=crafts.Count==0?null:crafts.ToArray(),
            Domestic=events.Count==0?null:events.ToArray(),Pacts=pacts.Count==0?null:pacts.ToArray(),Honors=honors.Count==0?null:honors.ToArray(),Shoguns=shoguns.Count==0?null:shoguns.ToArray()};
    }
    static DomesticPost? PostAt(DomesticHouse h,string post,string place)=>h.Posts.FirstOrDefault(p=>p.Post==post&&p.Place==place);
    static bool Hungry(DomesticSite d,int harvest,int rate)=>d.Ikki!=null||harvest<100&&rate>=50||d.Agriculture>=30;
    static long FledIn(Dictionary<string,DomesticSite> rows,string site)=>rows.Values.Where(x=>x.FledTo==site).Sum(x=>x.Fled);
    // 拠点の人口（人。Territory2 の村の人口は100分の1の単位）。
    public static long Population(ContractWorld w,string site)=>(long)Territory2.Population(w,new(site))*100;
    static long Workers(ContractWorld w,string site,DomesticSite d)=>Math.Max(0,Population(w,site)/2-d.Fled-d.Levied-d.Lost);
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NewGameState,System.Collections.Concurrent.ConcurrentDictionary<string,SiteProfile?>> profiles=new();
    static SiteProfile? Profile(ContractWorld w,string site)=>profiles.GetValue(w.NewGame!,_=>new(StringComparer.Ordinal)).GetOrAdd(site,x=>Territory2.Profile(w,new(x)));

    // 同じ順位の要求の按分：足りなければ要求の大きさに比例して分ける。返り値は払った額と不足。
    public static (long[] Paid,long Short) Allocate(long available,long[] claims)
    {
        long total=claims.Sum();if(total<=available)return (claims.ToArray(),0);
        var paid=claims.Select(c=>total==0?0:Math.Max(0,available)*c/total).ToArray();
        return (paid,total-paid.Sum());
    }
    // 分けられない物（名物・軍馬の一頭など）の裁定：同じ順位の求め手のうち、固定のくじで一人に渡す。
    public static string Award(ContractWorld w,string item,IReadOnlyList<string> claimants)=>claimants.OrderBy(c=>Draw(w,$"award/{item}/{c}")).ThenBy(c=>c,StringComparer.Ordinal).First();

    // 合戦の兵力（NationalStrategy.Settle から）：鉄砲と火薬・軍馬の効き目、大寺社の僧兵、探題・管領代の加勢。
    internal static (long A,long D) Effective(ContractWorld w,StrategyState s,StrategyCampaign c,long a,long d)
    {
        if(!On(w))return (a,d);
        long Arms(string house,long v)
        {
            if(s.Houses?.FirstOrDefault(h=>h.House.Value==house)?.Goods is not {} g)return v;
            int pct=100+(g.Guns>=200&&g.Powder>=50?15:g.Guns>=50&&g.Powder>=10?10:0)+(g.Horses>=100?5:0);
            return v*pct/100;
        }
        a=Arms(c.Attacker.Value,a);d=Arms(c.Defender.Value,d);
        var province=Province(c.Target.Value);
        foreach(var t in s.Temples??[])
        {
            int monks=Monks(t.Temple);if(Province(t.Site)!=province||monks<=0)continue;
            if(t.Stage>=4&&t.Ally is {} ally){if(ally==c.Attacker)a+=monks;else if(ally==c.Defender)d+=monks;}
            else if(t.Stage>=5&&t.Lord is {} foe){if(foe==c.Attacker)d+=monks;else if(foe==c.Defender)a+=monks;}
            else if(t.Relation>=3&&t.Stage<=1&&t.Lord is {} friend){if(friend==c.Defender)d+=monks;else if(friend==c.Attacker)a+=monks;}
        }
        var posts=Posts(w,w.ElapsedSeconds);
        foreach(var (post,region) in TandaiRegions)if(province!=null&&region.Contains(province)&&posts.GetValueOrDefault(post)==c.Attacker.Value)a=a*110/100;
        if(posts.GetValueOrDefault(KanreiDeputy)==c.Attacker.Value&&NationalStrategy.InKyoto(c.Target.Value))a=a*110/100;
        return (a,d);
    }

    // ---- 知ること ----
    // 噂と書状：家の内政の出来事は、その場所から道のりに応じて遅れて届く。本人の家のことは、役の知らせが届いた時に知る。
    public static DomesticRumor[] Heard(ContractWorld w,EntityId viewer,long at)
    {
        if(!On(w))return [];var rows=new List<DomesticRumor>();
        var mine=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==viewer.Value)?.Faction;
        foreach(var e in Events(w))
        {
            if(e.Kind is "answer")continue;
            string? where=e.Site!=null&&sites.Value.ContainsKey(e.Site)?e.Site:e.Site!=null?sites.Value.Values.FirstOrDefault(x=>x.Province==e.Site)?.Site:e.House is {} h?HomeOf(w,h.Value):null;
            bool own=e.House?.Value==mine||e.House is {} hh&&HouseSuccession.Head(w,hh)==viewer;
            long learned=own&&e.Arrives is long a?a:where!=null&&where!=""?e.At+HistoricalNews.DelayFrom(w,viewer,new(where),e.At):e.At+Day;
            if(own&&e.Kind is "finding" or "dismiss" or "unrest" or "ikki"&&e.Arrives is long a2)learned=a2;
            if(!own&&e.Kind is "finding" or "dismiss" or "allocate" or "divert" or "cut" or "borrow" or "raise" or "policy" or "caught")continue;
            if(learned>at)continue;
            rows.Add(new(e.Id,e.Text,e.At,learned,e.House));
        }
        return rows.OrderBy(r=>r.LearnedAt).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
    }
    internal static IEnumerable<PersonalChronicleRow> ChronicleRows(ContractWorld w,EntityId viewer)=>
        Heard(w,viewer,w.ElapsedSeconds).Select(r=>new PersonalChronicleRow("domestic:"+r.Id,"内政",r.Text,r.OccurredAt,r.LearnedAt,"噂",r.House?.Value));

    // 役ごとの専門の行動（全国の家の月の計算で、その役が行うこと）。
    public static readonly IReadOnlyDictionary<string,string> Duties=new Dictionary<string,string>(StringComparer.Ordinal){
        [Elder]="方針の評定（赤字の家で倹約を進める）・当主の不在時の代理",[Treasurer]="家計の帳簿と郡代の監査",[WorksMagistrate]="災害の被害の普請",[Inspector]="不正と放火の監察",
        [Envoy]="他家との取次（京の主の家は申次衆）",[TempleMagistrate]="寺社への寄進と関係",[Intendant]="国の年貢と代官の監督・一揆の交渉",[Deputy]="拠点の村の差配（農政で収穫が増える）",
        [Kanrei]="将軍の補佐（名目の職）",[KanreiDeputy]="畿内の攻めに加勢が集まる",[Samurai]="京の警固",[Mandokoro]="京の財政（銭の収入が増える）",
        [OshuTandai]="奥州の戦の和睦と加勢",[UshuTandai]="羽州の戦の和睦と加勢",[KyushuTandai]="九州の戦の和睦と加勢",[KantoKanrei]="関東の権威（名目の職）",[Kubo]="関東の権威（名目の職）"};
    // 地域・時代による権限：1573年までは守護の家の郡代が国の段銭を取る。
    public static string[] Authority(ContractWorld w,string house,long at)
    {
        var rows=new List<string>();int year=WorldCalendar.At(w.EpochDate,at).Year;
        if(year<=1573)rows.AddRange(CentralStanding.ShugoProvinces(w,house,at).Select(p=>$"{p}の段銭（守護）"));
        foreach(var (post,h) in Posts(w,at))if(h==house&&Duties.TryGetValue(post,out var d))rows.Add($"{post}：{d}");
        return rows.ToArray();
    }
    // 奉公衆：将軍家の成人（将軍その人を除く）は、将軍直属の武士として奉公衆を名乗る。
    public static bool Hokoshu1(ContractWorld w,EntityId person)=>On(w)&&FamilyBirth.People(w).SingleOrDefault(p=>p.Id==person.Value)?.Faction==NationalStart.FactionId(NationalStrategy.Shogun)&&SupremeOffice.ShogunAt(w,w.ElapsedSeconds,w.CommitSequence)!=person;
    // 家の記録（家系・歴代の当主・家格と職）：本人の家なら、家の帳面の値も出す。
    public static string[] FamilyRecord(ContractWorld w,EntityId viewer,EntityId house)
    {
        if(!On(w))return [];var rows=new List<string>();var head=HouseSuccession.Head(w,house);
        if(PersonIdentity.Lineage(w,head) is {} l)rows.Add($"家系　{l.Value}");
        var heads=new List<EntityId>{new(w.NewGame!.Start.Factions.Single(f=>f.Id==house.Value).Head)};
        foreach(var x in (w.Strategy?.Successions??[]).Where(x=>x.House==house))heads.Add(x.Heir);
        rows.Add("歴代　"+string.Join("・",heads.Distinct().Select(p=>PersonName(w,p))));
        var titles=Posts(w,w.ElapsedSeconds).Where(x=>x.Value==house.Value).Select(x=>x.Key).Concat((w.Strategy?.Honors??[]).Where(h=>h.House==house&&!ShogunatePost(h.Kind)).Select(h=>h.Kind)).Distinct(StringComparer.Ordinal).ToArray();
        if(titles.Length>0)rows.Add("家格　"+string.Join("・",titles));
        if(Logistics2.TreasuresOf(w,house) is {Length:>0} named)rows.Add("名物　"+string.Join("・",named.Select(t=>t.Name)));
        var done=Events(w).Where(e=>e.Kind=="reform"&&e.House==house).Select(e=>e.Text.Split('：')[^1]).ToArray();
        if(done.Length>0)rows.Add("改革　"+string.Join("・",done));
        if(House(w,house) is {} d&&w.NewGame.Start.People.SingleOrDefault(p=>p.Id==viewer.Value)?.Faction==house.Value)
        {
            rows.Add($"方針　{d.Policy}　年貢　{d.TaxRate}%　蔵米{d.Rice}石　銭{d.Coin}貫"+(d.Debt>0?$"　借銀{d.Debt}貫":""));
            rows.Add($"鉄砲{d.Goods.Guns}　火薬{d.Goods.Powder}　軍馬{d.Goods.Horses}　金{d.Goods.Gold}　銀{d.Goods.Silver}");
            if(d.Posts.Length>0)rows.Add("役　"+string.Join("・",d.Posts.Where(p=>p.Post!=Deputy).Select(p=>$"{p.Post}{(p.Place!=null?"（"+p.Place+"）":"")}{PersonName(w,p.Person)}")));
        }
        return rows.ToArray();
    }

    // ---- 口入れ（拠点の仕事の口） ----
    // 本人のいる拠点で、そこにいる実在の人物が出す仕事を一つの口にまとめる（面識のない相手の小口の仕事と、関係のある相手の個別の仕事）。
    // 書状の使い・買付納品（BrokerWork）と、道中の護衛・賊の討伐（GuardJobs）。引き受けはそれぞれの仕組みで行う。
    public sealed record BoardJob(string Kind,EntityId Issuer,string IssuerName,EntityId? Destination,long Fee,bool Personal);
    public static BoardJob[] Board(ContractWorld w,EntityId viewer)
    {
        if(!On(w)||SocialWorld.Site(w,viewer) is null)return [];
        var rows=new List<BoardJob>();var probe=new EntityId("request:board/"+viewer.Value.Replace(':','/'));
        foreach(var personal in new[]{false,true})
            foreach(var kind in new[]{BrokerKind.Letter,BrokerKind.Purchase})
                foreach(var t in BrokerWork.Offers(w,viewer,probe,kind,personal))
                    rows.Add(new(kind==BrokerKind.Letter?"書状の使い":"買付納品",t.Issuer,PersonName(w,t.Issuer),t.Destination,t.Fee,personal));
        if(viewer==w.PlayerControl&&BanditEncounter.Enabled(w))
            foreach(var issuer in GuardJobs.Issuers(w))
            {
                rows.Add(new("道中の護衛",issuer,PersonName(w,issuer),null,80,false));
                if(SocialWorld.Site(w,viewer) is {} here&&NationalStrategy.Neighbors(here.Value).Any(n=>Territory2.IsCamp(w,new(n))))rows.Add(new("賊の討伐",issuer,PersonName(w,issuer),null,80,false));
            }
        foreach(var (issuer,t,fee) in Logistics2.Offers(w,viewer))rows.Add(new("探物："+t.Name,issuer,PersonName(w,issuer),new(t.Site),fee,false));
        return rows.DistinctBy(r=>(r.Kind,r.Issuer,r.Personal)).OrderBy(r=>r.Personal).ThenBy(r=>r.Kind,StringComparer.Ordinal).ThenBy(r=>r.Issuer.Value,StringComparer.Ordinal).ToArray();
    }

    // ---- 本人の家の判断（操作する人物が当主の家） ----
    // 方針・年貢の率・救済・寺社への譲歩を本人が決める。月の初めの計算で使う。
    public static void Decide(ContractWorld w,EntityId actor,string kind,string? target,int value)
    {
        if(!On(w))throw new InvalidDataException("全国の家の内政がない世界です");
        var s=w.Strategy??=new StrategyState(1,NationalStrategy.Policy,[],[]);
        var house=w.NewGame!.Start.Factions.Select(f=>new EntityId(f.Id)).FirstOrDefault(h=>HouseSuccession.Head(w,h)==actor);
        if(house.Value==null)throw new InvalidDataException("当主だけが家の内政を決められます");
        var h=House(w,house)??new(house,Farming,50,0,0,0,0,0,0,[],new(0,0,0,0,0,0,0),w.ElapsedSeconds);
        long now=w.ElapsedSeconds,rev=w.CommitSequence;var events=(s.Domestic??[]).ToList();
        string id=$"domestic:order/{Hash(kind+"/"+target+"/"+value+"/"+now+"/"+events.Count)}";
        switch(kind)
        {
            case "policy" when target is Farming or Trade or Arms or Building or Thrift:
                h=h with{Policy=target};events.Add(new(id,"policy",null,house,actor,now,rev,0,$"{HouseName(w,house)}の方針：{target}"));break;
            case "decree" when value is 30 or 40 or 50 or 60:
                h=h with{TaxRate=value};events.Add(new(id,"decree",target,house,actor,now,rev,value,$"{HouseName(w,house)}の年貢の布告：{value}%"));break;
            case "relief" when target!=null&&NationalStrategy.Owners(w,s).GetValueOrDefault(target)==house.Value&&value>0&&value<=h.Rice:
                h=h with{Rice=h.Rice-value};events.Add(new(id,"relief",target,house,actor,now,rev,value,$"{HouseName(w,house)}が{SiteName(w,target)}へ救済の米を送った"){Arrives=now+Days(HomeOf(w,house.Value),target)*Day});break;
            case "appoint" when target!=null&&Duties.ContainsKey(target.Split('/')[0]):
                var person=new EntityId(target.Split('/')[1]);if(!Members(w,house.Value).Contains(person))throw new InvalidDataException("家中の成人を任じてください");
                h=h with{Posts=[..h.Posts.Where(p=>p.Person!=person),new(target.Split('/')[0],person,target.Split('/').Length>2?target.Split('/')[2]:null,now,false)]};
                events.Add(new(id,"appoint",null,house,person,now,rev,0,$"{PersonName(w,person)}を{target.Split('/')[0]}に任じた"));break;
            case "concede" when target!=null&&(s.Temples??[]).FirstOrDefault(t=>t.Temple==target&&t.Lord==house) is {} t:
                w.Strategy=s=s with{Temples=(s.Temples??[]).Select(x=>x==t?x with{Stage=0,Ally=null,Conceded=true,Relation=x.Relation+1,At=now}:x).ToArray()};
                events.Add(new(id,"concede",t.Site,house,actor,now,rev,0,$"{HouseName(w,house)}が{target}の関・座の権益の一部を残した"));break;
            default:throw new InvalidDataException("家の内政の指示を確認してください");
        }
        w.Strategy=s with{Houses=[..(s.Houses??[]).Where(x=>x.House!=house),h],Domestic=events.ToArray()};
    }

    internal static void Validate(ContractWorld w,StrategyState s,StrategyState? prior)
    {
        if(s.Domestic==null&&s.Houses==null&&s.Sites==null&&s.Temples==null&&s.Crafts==null)return;
        if(!On(w))throw new InvalidDataException("全国の家の内政は一覧に Domestic2 のある世界だけです");
        // 古い記録は毎年1月に間引くので、残っている過去の記録が変わっていないこと、新しい記録が前の最後より後であることを確かめる。
        if(prior?.Domestic is {} old)
        {
            var map=old.ToDictionary(e=>e.Id,StringComparer.Ordinal);long latest=old.Length==0?0:old.Max(e=>e.At);
            foreach(var e in s.Domestic??[])if(map.TryGetValue(e.Id,out var o)?o!=e:e.At<latest)throw new InvalidDataException("過去の内政の記録が変更されています");
        }
        if((s.Domestic??[]).Select(e=>e.Id).Distinct(StringComparer.Ordinal).Count()!=(s.Domestic?.Length??0))throw new InvalidDataException("内政の記録が重複しています");
        var factions=w.NewGame!.Start.Factions.Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var e in s.Domestic??[])if(e.At>w.ElapsedSeconds||e.Revision>w.CommitSequence||e.House is {} h&&!factions.Contains(h.Value))throw new InvalidDataException("内政の記録が不正です");
        foreach(var h in s.Houses??[])if(!factions.Contains(h.House.Value)||h.TaxRate is <30 or >60||h.StipendCut is <0 or >50||h.Rice<0||h.Coin<0||h.Debt<0)throw new InvalidDataException("家の帳面が不正です");
        foreach(var x in s.Sites??[])if(!sites.Value.ContainsKey(x.Site)||x.Unrest is <0 or >5||x.Sections is <0 or >4||x.Fled<0||x.Levied<0)throw new InvalidDataException("拠点の状態が不正です");
    }
}

public sealed partial class WorldSession
{
    // 本人（当主）の家の内政の指示：方針・年貢の布告・救済・役の任命・寺社への譲歩。
    public CommandResult DecideDomestic(string kind,string? target,int value,long revision)
    {
        lock(gate)
        {
            var id=new EntityId("request:domestic/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(kind+"/"+target+"/"+value+"/"+revision))[..16]);
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
            var trial=ContractJson.Read<ContractWorld>(ContractJson.Write(state));
            try{Domestic2.Decide(trial,trial.PlayerControl,kind,target,value);}catch(InvalidDataException e){return new(ResultCode.NotAvailable,id,state.CommitSequence,e.Message);}
            Change(w=>Domestic2.Decide(w,w.PlayerControl,kind,target,value));
            return new(ResultCode.Accepted,id,state.CommitSequence,"指示しました");
        }
    }
}
