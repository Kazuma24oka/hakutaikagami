using System.Text.Json;
using System.Text.Json.Serialization;

namespace HakutaiKagami.Core.Contracts;

// 百代鏡DBの追加の版（2026-11-16、Tools/HakutaiDb/export_1560.py）。v2 の名簿の人物に足す項目だけを持つ。
// v2 そのものは変えない（今の保存が版の印で照合している）。新規の全国開始で一覧（NationalSystems）に入れたものだけが使う。
// 官職・位階：Offices は生涯の記録（時期の無いものを含む）、Held1560 は就いた年の記録が1560年以前のもの（記事の本文の候補を含む）。
public sealed record HakutaiDbOffice(string Kind,string Office,int? From,int? To);
public sealed record HakutaiDbHousePost(string Post,string? House,int? From);
public sealed record HakutaiDbHeldOffice(string Kind,string Office,int From,string Basis);
public sealed record HakutaiDbExtra(string Id)
{
    public string[]? Siblings {get;init;}
    public HakutaiDbOffice[]? Offices {get;init;}
    public HakutaiDbHeldOffice[]? Held1560 {get;init;}
    public string[]? ReligiousNames {get;init;}
    public string? Status {get;init;}
    public int? HeadshipFrom {get;init;}
    // 技能・専門と宗派・信仰の候補（生涯の記録。記事の本文を含む。1560年の時点とは限らない）。
    public string[]? Specialties {get;init;}
    public string[]? Religions {get;init;}
    // 家中職の候補（家老・奉行・城代など。記事の本文。From は同じ文の年、無ければ時期不明。House は記事に書かれた家の名）。
    public HakutaiDbHousePost[]? HousePosts {get;init;}
}
public sealed record HakutaiDbBasedOn(string Edition,string Commit);
public sealed record HakutaiDbSupplementEdition(int Schema,string Id,HakutaiDbBasedOn BasedOn,HakutaiDbSource Source,string Policy,HakutaiDbExtra[] People);

public static class HakutaiDbSupplement
{
    public const string Edition="hakutai-db-1560-supplement-v1";
    static readonly JsonSerializerOptions Options=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    static readonly Lazy<(HakutaiDbSupplementEdition Edition,Dictionary<string,HakutaiDbExtra> People)> data=new(()=>
    {
        using var stream=typeof(HakutaiDbSupplement).Assembly.GetManifestResourceStream("HakutaiDb1560SupplementV1")??throw new InvalidDataException("百代鏡DBの追加の版がありません");
        using var bytes=new MemoryStream();stream.CopyTo(bytes);
        var e=JsonSerializer.Deserialize<HakutaiDbSupplementEdition>(bytes.ToArray(),Options)??throw new InvalidDataException("百代鏡DBの追加の版が空です");
        Validate(e);return (e,e.People.ToDictionary(p=>p.Id,StringComparer.Ordinal));
    });
    public static HakutaiDbSupplementEdition Current=>data.Value.Edition;
    public static HakutaiDbExtra? Person(string id)=>data.Value.People.GetValueOrDefault(id);

    public static void Validate(HakutaiDbSupplementEdition e)
    {
        void Need(bool ok,string why){if(!ok)throw new InvalidDataException("百代鏡DBの追加の版の検査："+why);}
        Need(e.Schema==1&&e.Id==Edition,"版が違います");
        Need(e.BasedOn.Edition==HakutaiDb.Edition1560&&e.BasedOn.Commit==HakutaiDb.Source.Commit,"元の版（v2）と合いません");
        Need(e.People.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()==e.People.Length,"人物が重複しています");
        foreach(var p in e.People)
        {
            Need(HakutaiDb.Person(p.Id) is not null,"名簿にない人物です："+p.Id);
            Need(p.Siblings?.All(s=>s!=p.Id&&HakutaiDb.Person(s) is not null)??true,"兄弟姉妹が名簿の外です："+p.Id);
            Need(p.Offices?.All(o=>o.Kind is "官職" or "位階" or "幕府職"&&o.Office.Length>0)??true,"官位の種類が不正です："+p.Id);
            // 1560年の在職は、就いた年が1560年以前の記録だけ（年の無い記録を在職にしない）。位階は1つまで。
            Need(p.Held1560?.All(h=>h.From<=1560&&p.Offices!=null&&p.Offices.Any(o=>o.Kind==h.Kind&&o.Office==h.Office&&o.From==h.From))??true,"1560年の在職に年の記録がありません："+p.Id);
            Need((p.Held1560?.Count(h=>h.Kind=="位階")??0)<=1,"1560年の位階が2つあります："+p.Id);
        }
    }
}
