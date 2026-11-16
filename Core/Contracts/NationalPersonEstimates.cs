using System.Security.Cryptography;

namespace HakutaiKagami.Core.Contracts;

public sealed record NationalOfficerAbilities(int Leadership,int Valor,int Intelligence,int Politics,int Charm);
public sealed record NationalOfficerAbilityBasis(string Leadership,string Valor,string Intelligence,string Politics,string Charm);
public sealed record NationalOfficerEstimate(string Id,int BirthYear,string BirthKind,string BirthBasis,string BirthConfidence,
    NationalOfficerAbilities Abilities,NationalOfficerAbilityBasis AbilityBasis,string AbilityConfidence,bool BirthParentAccepted,string BirthParentBasis);
public sealed record NationalOfficerEstimateCatalog(int Schema,string Policy,string SourceEdition,string SourceCommit,string SourceSha256,
    Dictionary<string,string> Rules,NationalOfficerEstimate[] People);

public static class NationalPersonEstimates
{
    public const string Policy="national-person-data-v1";
    static readonly Lazy<NationalOfficerEstimateCatalog> catalog=new(Read);
    static readonly Lazy<Dictionary<string,NationalOfficerEstimate>> byId=new(()=>catalog.Value.People.ToDictionary(p=>p.Id,StringComparer.Ordinal));
    static NationalOfficerEstimateCatalog Read()
    {
        using var stream=typeof(NationalPersonEstimates).Assembly.GetManifestResourceStream("NationalPersonEstimatesV1")??throw new InvalidDataException("全国人物推定表がありません");
        using var bytes=new MemoryStream();stream.CopyTo(bytes);
        var result=ContractJson.Read<NationalOfficerEstimateCatalog>(bytes.ToArray());Validate(result);return result;
    }
    static string DatabaseResourceSha256()
    {
        using var stream=typeof(HakutaiDb).Assembly.GetManifestResourceStream("HakutaiDb1560V2")??throw new InvalidDataException("全国人物推定表の元DBがありません");
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    public static NationalOfficerEstimateCatalog Catalog=>ContractJson.Copy(catalog.Value);
    public static NationalOfficerEstimate ForDbPerson(string id)=>byId.Value.GetValueOrDefault(id)??throw new InvalidDataException("全国人物推定表に人物がありません："+id);
    public static bool Enabled(ContractWorld w)=>w.NewGame?.NationalPersonDataPolicy==Policy;
    public static void ValidateWorld(ContractWorld w,bool compareInitialData)
    {
        void Need(bool ok,string why){if(!ok)throw new InvalidDataException("全国人物データ："+why);}
        if(!Enabled(w))return;
        Need(w.NewGame!.HistoricalCatalog is NationalStart.CourtEdition or NationalStart.PersonEdition or NationalStart.RevenueEdition&&w.NewGame.DistrictControlPolicy==NationalDistrictControl.Policy&&CharacterDevelopment.IsPolicy(w.NewGame.DevelopmentPolicy),"全国開始・能力表示の版が違います");
        if(!compareInitialData)return;
        foreach(var person in w.NewGame.Start.People)
        {
            if(NationalStart.DbPerson(person.Id) is not{} db)continue;
            var estimate=ForDbPerson(db.Id);var a=person.Attributes;
            Need(person.BirthYear==estimate.BirthYear&&a!=null&&a.Leadership==estimate.Abilities.Leadership&&a.Valor==estimate.Abilities.Valor&&a.Intelligence==estimate.Abilities.Intelligence&&a.Politics==estimate.Abilities.Politics&&a.Charm==estimate.Abilities.Charm,"初期の生年・能力が根拠表と違います："+db.Id);
        }
    }
    public static void Validate(NationalOfficerEstimateCatalog c)
    {
        void Need(bool ok,string why){if(!ok)throw new InvalidDataException("全国人物推定表の検査："+why);}
        var source=HakutaiDb.Houses.SelectMany(h=>HakutaiDb.Members(h.Id)).ToDictionary(p=>p.Id,StringComparer.Ordinal);
        Need(c.Schema==1&&c.Policy==Policy&&c.SourceEdition==HakutaiDb.Edition1560&&c.SourceCommit==HakutaiDb.Source.Commit&&c.People.Length==source.Count,"版・対象DB");
        Need(c.SourceSha256.Length==64&&c.SourceSha256.All(Uri.IsHexDigit)&&c.SourceSha256==DatabaseResourceSha256(),"元DBのハッシュ");
        Need(c.People.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()==c.People.Length&&source.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(c.People.Select(p=>p.Id)),"人物IDの欠落または重複");
        foreach(var estimate in c.People)
        {
            var db=source[estimate.Id];
            if(db.Birth is int known)Need(estimate.BirthYear==known&&estimate.BirthKind=="explicit"&&estimate.BirthConfidence=="source-explicit","明記生年の保持："+db.Id);
            else Need(estimate.BirthYear is >=1400 and <=1560 && estimate.BirthKind.StartsWith("estimated-",StringComparison.Ordinal) && (estimate.BirthConfidence is "estimate-low" or "estimate-very-low") && !string.IsNullOrWhiteSpace(estimate.BirthBasis),"推定生年の区別："+db.Id);
            if(db.Birth==null&&db.Death is int death)Need(estimate.BirthYear<=death-16,"没年より前の推定生年："+db.Id);
            var values=estimate.Abilities;
            Need(new[]{values.Leadership,values.Valor,values.Intelligence,values.Politics,values.Charm}.All(v=>v is >=1 and <=100),"能力の範囲："+db.Id);
            Need(estimate.AbilityConfidence=="estimate-very-low"&&new[]{estimate.AbilityBasis.Leadership,estimate.AbilityBasis.Valor,estimate.AbilityBasis.Intelligence,estimate.AbilityBasis.Politics,estimate.AbilityBasis.Charm}.All(s=>!string.IsNullOrWhiteSpace(s)),"能力根拠の層："+db.Id);
            Need(values.Charm==50&&estimate.AbilityBasis.Charm.Contains("交渉値は転用しない",StringComparison.Ordinal),"魅力と旧交渉の分離："+db.Id);
            if(db.Father is{} fatherId && source.TryGetValue(fatherId,out var father))
            {
                var fatherEstimate=c.People.Single(p=>p.Id==father.Id);
                Need(estimate.BirthParentAccepted==(fatherEstimate.BirthYear<estimate.BirthYear),"親子年代に基づくリンク判定："+db.Id);
                Need(!string.IsNullOrWhiteSpace(estimate.BirthParentBasis),"親子リンクの根拠："+db.Id);
            }
        }
    }
}
