using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace HakutaiKagami.Core.Contracts;

// 百代鏡DB：戦国DBの特定の版から一方向に書き出した、1560年の人物・家・城（Tools/HakutaiDb/export_1560.py）。
// 戦国DBの ID をそのまま使う。所属・置き場所・上司・城の持ち主は機械の見積もりで、決め方（Basis）を値ごとに持つ。
// 知識の区分（BD-20）：城の名前と位置は公知の地理、家の顔ぶれ・城の持ち主・家どうしの関係はその年の情勢で、
// ゲームの中では人物が報告で知るもの。ここは開発用の史料で、人物が信じている内容ではない。
public sealed record HakutaiDbSource(string Repository,string Commit,string[] Tags,string? SqliteBuiltAt,string? CatalogVersion);
public sealed record HakutaiDbRelation(string House,string Kind);
public sealed record HakutaiDbHouse(string Id,string Name,string? Head,string? Home,bool Religious,string? Overlord,HakutaiDbRelation[] Relations,string HeadBasis,long Kokudaka);
public sealed record HakutaiDbName(string Name,int? From,string Step);
public sealed record HakutaiDbPerson(string Id,string Name,string? Reading,string? Surname,string? Imina,string? Tsusho,string? Kanto,string? Sex,
    int? Birth,int? Death,string? Father,string StartName,string StartNameBasis,HakutaiDbName[] NameHistory,int Records,string? Existence,string Registration,string? Role,string? Province,
    string House,string HouseBasis,string? Castle,string? CastleName,string? CastleBasis,string? Superior,string? SuperiorBasis);
public sealed record HakutaiDbCastle(string Id,string Name,string? Province,string? District,long LatitudeE7,long LongitudeE7,string PositionBasis,string? Owner,string? Holder,string? OwnerBasis);
// 郡：1560年の支配の見積もりと石高の推定（慶長3年の国の石高を郡の面積で按分）。
public sealed record HakutaiDbDistrict(string Id,string Province,string Name,string? Owner,string? OwnerBasis,long Kokudaka,string KokudakaBasis);
public sealed record HakutaiDbEdition(int Schema,string Id,int WorldYear,HakutaiDbSource Source,string Policy,HakutaiDbHouse[] Houses,HakutaiDbPerson[] People,HakutaiDbCastle[] Castles,HakutaiDbDistrict[] Districts);

public enum HakutaiDbKnowledge { PublicGeography, YearSituation }
// 公知の地理：城の名前・国・位置だけ（持ち主を含まない）。
public sealed record HakutaiDbPlace(string Id,string Name,string? Province,long LatitudeE7,long LongitudeE7,string PositionBasis);
// その年の情勢：持ち主と城主の名前。報告で知るもので、遠方の今の真実ではない。
public sealed record HakutaiDbSituation(string Castle,int Year,string? Owner,string? Holder,string? Basis);

public static class HakutaiDb
{
    public const string Edition1560="hakutai-db-1560-v2";
    public const string Policy="hakutai-db-from-sengokudb-v1";
    // 既存の開始人物・家と百代鏡DBの対応（同じ人・家であることだけ。値や配置は既存の開始版のまま）。
    public static readonly IReadOnlyDictionary<string,string> GameLinks=new Dictionary<string,string>(StringComparer.Ordinal){
        ["person:nobunaga"]="P-005235",["person:shingen"]="P-005247",["person:ujiyasu"]="P-005580",
        ["person:imagawa-yoshimoto"]="P-005312",["person:imagawa-ujizane"]="P-005546",["person:ashikaga-yoshiteru"]="P-005253",
        ["person:yamashina-tokitsugu"]="P-010391",["person:yoshishige"]="P-005322",["person:takahisa"]="P-005627",
        ["person:takeda-nobushige"]="P-005486",["person:hosokawa-fujitaka"]="P-007110",
        ["org:oda"]="G-000131",["org:takeda"]="G-000106",["org:hojo"]="G-000104",["org:imagawa"]="G-000123",
        ["org:otomo"]="G-000210",["org:shimazu"]="G-000236",["org:central-bakufu"]="G-000154",["org:ishiyama-honganji"]="G-000242"};

    sealed class Index
    {
        public readonly HakutaiDbEdition Edition;
        public readonly Dictionary<string,HakutaiDbPerson> People;
        public readonly Dictionary<string,HakutaiDbHouse> Houses;
        public readonly Dictionary<string,HakutaiDbCastle> Castles;
        public readonly Dictionary<string,HakutaiDbPerson[]> Members;
        public readonly Dictionary<string,HakutaiDbDistrict> Districts;
        public Index(HakutaiDbEdition e)
        {
            Edition=e;People=e.People.ToDictionary(p=>p.Id,StringComparer.Ordinal);Houses=e.Houses.ToDictionary(h=>h.Id,StringComparer.Ordinal);
            Castles=e.Castles.ToDictionary(c=>c.Id,StringComparer.Ordinal);
            Districts=e.Districts.ToDictionary(d=>d.Id,StringComparer.Ordinal);
            Members=e.People.GroupBy(p=>p.House,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
        }
    }
    static readonly Lazy<Index> index=new(()=>{var e=Read();Validate(e);return new Index(e);});

    static readonly JsonSerializerOptions Options=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static HakutaiDbEdition Parse(byte[] json)=>JsonSerializer.Deserialize<HakutaiDbEdition>(json,Options)??throw new InvalidDataException("百代鏡DBが空です");
    static HakutaiDbEdition Read()
    {
        using var stream=typeof(HakutaiDb).Assembly.GetManifestResourceStream("HakutaiDb1560V2")??throw new InvalidDataException("百代鏡DBがありません");
        using var bytes=new MemoryStream();stream.CopyTo(bytes);return Parse(bytes.ToArray());
    }

    public static void Validate(HakutaiDbEdition e)
    {
        void Need(bool ok,string why){if(!ok)throw new InvalidDataException("百代鏡DBの検査："+why);}
        Need(e.Schema==1&&e.Id==Edition1560&&e.WorldYear==1560&&!string.IsNullOrWhiteSpace(e.Policy),"版・年");
        Need(e.Source.Repository=="SengokuDB"&&Regex.IsMatch(e.Source.Commit,"^[0-9a-f]{40}$"),"元の戦国DBの版");
        Need(e.People.Length>0&&e.Houses.Length>0&&e.Castles.Length>0&&e.Districts.Length>0,"空の表");
        Need(e.Districts.Select(d=>d.Id).Distinct().Count()==e.Districts.Length&&e.Districts.All(d=>d.Kokudaka>=0&&(d.Owner==null||e.Houses.Any(h=>h.Id==d.Owner)&&d.OwnerBasis!=null)),"郡");
        Need(e.Houses.All(h=>h.Kokudaka==e.Districts.Where(d=>d.Owner==h.Id).Sum(d=>d.Kokudaka)),"家の石高は支配する郡の合計");
        var people=e.People.Select(p=>p.Id).ToHashSet(StringComparer.Ordinal);var houses=e.Houses.Select(h=>h.Id).ToHashSet(StringComparer.Ordinal);var castles=e.Castles.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        Need(people.Count==e.People.Length&&houses.Count==e.Houses.Length&&castles.Count==e.Castles.Length,"IDの重複");
        Need(e.People.All(p=>Regex.IsMatch(p.Id,"^P-\\d{6}$"))&&e.Castles.All(c=>Regex.IsMatch(c.Id,"^L-\\d{6}$"))&&e.Houses.All(h=>Regex.IsMatch(h.Id,"^(G-\\d{6}|SITE:.+)$")),"IDの形");
        foreach(var p in e.People){
            Need(!string.IsNullOrWhiteSpace(p.Name)&&!string.IsNullOrWhiteSpace(p.Registration)&&!string.IsNullOrWhiteSpace(p.HouseBasis),"人物の名前・登録状態："+p.Id);
            Need(houses.Contains(p.House),"人物の家："+p.Id);
            Need(p.Castle==null||castles.Contains(p.Castle)&&p.CastleBasis!=null,"人物の城："+p.Id);
            Need(p.Superior==null||people.Contains(p.Superior)&&p.Superior!=p.Id&&p.SuperiorBasis!=null,"人物の上司："+p.Id);
            Need(p.Father==null||people.Contains(p.Father)&&p.Father!=p.Id,"人物の父："+p.Id);
            Need((p.Birth==null||p.Birth<=e.WorldYear)&&(p.Death==null||p.Death>=e.WorldYear)&&(p.Birth==null||p.Death==null||p.Birth<=p.Death),"人物の生没年が開始年と合わない："+p.Id);
            Need(p.Existence is null or "A" or "B" or "C" or "D" or "E","実在の確度："+p.Id);
            Need(!string.IsNullOrWhiteSpace(p.StartName)&&!string.IsNullOrWhiteSpace(p.StartNameBasis)&&p.Records>=0,"開始時の名前："+p.Id);
            Need(p.NameHistory.All(n=>!string.IsNullOrWhiteSpace(n.Name)&&(n.From==null||n.From>=1454&&n.From<=1650)),"名乗りの変化："+p.Id);
        }
        var byId=e.People.ToDictionary(p=>p.Id,StringComparer.Ordinal);
        foreach(var h in e.Houses){
            Need(!string.IsNullOrWhiteSpace(h.Name)&&!string.IsNullOrWhiteSpace(h.HeadBasis),"家の名前："+h.Id);
            Need(h.Head==null||people.Contains(h.Head)&&byId[h.Head].House==h.Id,"当主はその家の人："+h.Id);
            Need(h.Home==null||castles.Contains(h.Home),"家の本拠："+h.Id);
            Need(h.Overlord==null||houses.Contains(h.Overlord)&&h.Overlord!=h.Id,"主家："+h.Id);
            Need(h.Relations.All(r=>houses.Contains(r.House)&&r.House!=h.Id&&!string.IsNullOrWhiteSpace(r.Kind)),"家どうしの関係："+h.Id);
        }
        foreach(var c in e.Castles){
            Need(!string.IsNullOrWhiteSpace(c.Name)&&!string.IsNullOrWhiteSpace(c.PositionBasis),"城の名前："+c.Id);
            Need(c.LatitudeE7 is >=240000000 and <=460000000&&c.LongitudeE7 is >=1220000000 and <=1460000000,"城の位置が日本の外："+c.Id);
            Need(c.Owner==null||houses.Contains(c.Owner)&&c.OwnerBasis!=null,"城の持ち主："+c.Id);
        }
        Need(GameLinks.All(l=>l.Key.StartsWith("person:",StringComparison.Ordinal)?people.Contains(l.Value):houses.Contains(l.Value)),"既存の開始人物・家との対応");
        Need(GameLinks.Where(l=>l.Key.StartsWith("person:",StringComparison.Ordinal)).Select(l=>l.Value).Distinct().Count()==GameLinks.Count(l=>l.Key.StartsWith("person:",StringComparison.Ordinal)),"対応の重複");
    }

    public static IReadOnlyList<HakutaiDbDistrict> Districts=>index.Value.Edition.Districts;
    public static HakutaiDbDistrict? District(string id)=>index.Value.Districts.GetValueOrDefault(id);
    public static HakutaiDbSource Source=>index.Value.Edition.Source;
    public static int WorldYear=>index.Value.Edition.WorldYear;
    public static int PeopleCount=>index.Value.People.Count;
    public static HakutaiDbPerson? Person(string id)=>index.Value.People.GetValueOrDefault(id);
    public static HakutaiDbHouse? House(string id)=>index.Value.Houses.GetValueOrDefault(id);
    public static HakutaiDbCastle? Castle(string id)=>index.Value.Castles.GetValueOrDefault(id);
    // その年の持ち主がその家の城（城主不明で郡の支配者の城としたものを含む）。ID の順。
    public static IReadOnlyList<HakutaiDbCastle> CastlesOf(string house)=>index.Value.Edition.Castles.Where(c=>c.Owner==house).OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray();
    public static string Kuni(string? province)=>(province??"").TrimEnd('国').Replace('驒','騨');
    public static IReadOnlyList<HakutaiDbPerson> Members(string house)=>index.Value.Members.TryGetValue(house,out var m)?m:[];
    public static IReadOnlyList<HakutaiDbHouse> Houses=>index.Value.Edition.Houses;
    // 大きい家（顔ぶれの多い順）から。同数は ID 順で固定。
    public static IReadOnlyList<HakutaiDbHouse> HousesBySize=>index.Value.Edition.Houses.OrderByDescending(h=>Members(h.Id).Count).ThenBy(h=>h.Id,StringComparer.Ordinal).ToArray();
    public static IReadOnlyList<HakutaiDbPerson> Named(string name)=>index.Value.Edition.People.Where(p=>p.Name==name).ToArray();
    public static HakutaiDbPerson? ForGame(string gameId)=>GameLinks.TryGetValue(gameId,out var id)?Person(id):null;

    // 主家をたどった一番上の家（従属の連なり）。輪になっていれば止める。
    public static HakutaiDbHouse TopHouse(string house)
    {
        var seen=new HashSet<string>(StringComparer.Ordinal);var h=House(house)??throw new KeyNotFoundException(house);
        while(h.Overlord!=null&&seen.Add(h.Id)&&House(h.Overlord) is {} up)h=up;
        return h;
    }

    // 知識の区分：地理は公知、持ち主は年の情勢。
    public static HakutaiDbKnowledge KnowledgeOf(string field)=>field is nameof(HakutaiDbCastle.Name) or nameof(HakutaiDbCastle.Province) or nameof(HakutaiDbCastle.LatitudeE7) or nameof(HakutaiDbCastle.LongitudeE7) or nameof(HakutaiDbCastle.PositionBasis)?HakutaiDbKnowledge.PublicGeography:HakutaiDbKnowledge.YearSituation;
    public static IReadOnlyList<HakutaiDbPlace> PublicGeography()=>index.Value.Edition.Castles.Select(c=>new HakutaiDbPlace(c.Id,c.Name,c.Province,c.LatitudeE7,c.LongitudeE7,c.PositionBasis)).ToArray();
    public static HakutaiDbSituation? Situation(string castle)=>Castle(castle) is {} c?new(c.Id,WorldYear,c.Owner,c.Holder,c.OwnerBasis):null;
    public static int? Age(HakutaiDbPerson p,int year)=>p.Birth is int b?year-b:null;
    // その年の名乗り：名乗り始めた年が分かる名前のうち、その年までの最後のもの。開始年は書き出しで決めた名前。
    public static string NameAt(HakutaiDbPerson p,int year)
    {
        if(year==WorldYear)return p.StartName;
        var adult=p.NameHistory.Where(n=>n.Step!="幼名").ToArray();
        var dated=adult.Where(n=>n.From is int f&&f<=year).ToArray();
        return dated.Length>0&&dated[^1].From>=WorldYear?dated[^1].Name:p.StartName;
    }
    // 開始年より後に、年の分かる改名（ゲームの中の改名の出来事の候補）。自動では起こさない。
    public static IReadOnlyList<HakutaiDbName> LaterRenames(HakutaiDbPerson p)=>p.NameHistory.Where(n=>n.Step!="幼名"&&n.From is int f&&f>WorldYear&&n.Name!=p.StartName).ToArray();
}
