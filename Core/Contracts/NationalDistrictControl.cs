namespace HakutaiKagami.Core.Contracts;

public sealed record NationalDistrictOwner(string House,string Faction,bool Estimated);
public sealed record NationalDistrictAssignment(string Id,string SourceDistrictId,string Province,string Name,string Status,bool NameInferred,NationalDistrictOwner[] Owners);
public sealed record NationalDistrictShare(string House,long Kokudaka);
public sealed record NationalDistrictRecord(string Id,string? Owner,string? OwnerBasis,NationalDistrictShare[] Shares);
public sealed record NationalDistrictControlState(int Schema,string Policy,NationalDistrictRecord[] Districts);

public static class NationalDistrictControl
{
    public const string Edition="hakutai-national-district-control-v1";
    public const string Policy="national-district-control-v1";
    internal static string ProvinceKey(string province)=>HakutaiDb.Kuni(province) switch
    {
        "羽前" or "羽後"=>"出羽",
        "陸前" or "陸中" or "岩代" or "磐城"=>"陸奥",
        var name=>name
    };
    static readonly string[] DirectionalPrefixes=["北","南","東","西","上","下","中"];

    public static NationalDistrictAssignment? Resolve(string id,string province,string name)
    {
        if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(province)||string.IsNullOrWhiteSpace(name))return null;
        var sameProvince=HakutaiDb.Districts.Where(d=>ProvinceKey(d.Province)==ProvinceKey(province)).ToArray();
        var exact=sameProvince.Where(d=>d.Name==name).ToArray();
        var inferred=false;
        HakutaiDbDistrict? district=exact.Length==1?exact[0]:null;
        if(district==null&&exact.Length==0&&DirectionalPrefixes.Any(p=>name.StartsWith(p,StringComparison.Ordinal)))
        {
            var candidates=sameProvince.Where(d=>d.Name==name[1..]).ToArray();
            if(candidates.Length==1){district=candidates[0];inferred=true;}
        }
        if(district==null)return null;
        var owners=district.Owner is {} owner
            ?new[]{owner}
            :district.Shares.Select(s=>s.House).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if(owners.Length==0)return null;
        var resolved=owners.Select(h=>new NationalDistrictOwner(h,HakutaiDb.TopHouse(h).Id,inferred||district.OwnerBasis?.StartsWith("推定",StringComparison.Ordinal)==true)).ToArray();
        return new(id,district.Id,province,name,owners.Length>1?"並立":inferred?"郡名から推定":district.OwnerBasis?.StartsWith("推定",StringComparison.Ordinal)==true?"支配を推定":"記録",inferred,resolved);
    }

    public static NationalDistrictAssignment? Resolve(NationalDistrictControlState state,string id,string province,string name)
    {
        if(state.Schema!=1||state.Policy!=Policy)throw new InvalidDataException("郡支配の版が違います");
        var match=Resolve(id,province,name);if(match==null)return null;
        var source=state.Districts.SingleOrDefault(d=>d.Id==match.SourceDistrictId);if(source==null)return null;
        var houses=source.Owner is {} owner?new[]{owner}:source.Shares.Select(s=>s.House).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if(houses.Length==0)return null;
        return new(id,source.Id,province,name,houses.Length>1?"並立":match.NameInferred?"郡名から推定":source.OwnerBasis?.StartsWith("推定",StringComparison.Ordinal)==true?"支配を推定":"記録",match.NameInferred,
            houses.Select(h=>new NationalDistrictOwner(h,HakutaiDb.TopHouse(h).Id,match.NameInferred||source.OwnerBasis?.StartsWith("推定",StringComparison.Ordinal)==true)).ToArray());
    }

    public static NationalDistrictControlState Initial()=>new(1,Policy,HakutaiDb.Districts.OrderBy(d=>d.Id,StringComparer.Ordinal).Select(d=>new NationalDistrictRecord(d.Id,d.Owner,d.OwnerBasis,d.Shares.OrderBy(s=>s.House,StringComparer.Ordinal).Select(s=>new NationalDistrictShare(s.House,s.Kokudaka)).ToArray())).ToArray());
    public static bool Enabled(ContractWorld w)=>w.NewGame?.DistrictControlPolicy==Policy;
    public static void Validate(ContractWorld w)
    {
        void Need(bool ok,string reason){if(!ok)throw new InvalidDataException("全国の郡支配："+reason);}
        if(!Enabled(w)){Need(w.DistrictControl==null,"対象外の開始に郡支配を混在できません");return;}
        Need(NationalStart.IsNational(w.NewGame!.HistoricalCatalog)&&w.NewGame.NetworkPolicy==WorldNetwork.CastlePolicy,"全国1560年開始だけで使用できます");
        Need(w.DistrictControl is {Schema:1,Policy:Policy}&&ContractJson.CanonicalHash(w.DistrictControl)==ContractJson.CanonicalHash(Initial()),"開始時の支配・並立・根拠が1560年の百代鏡DBと一致しません");
    }
}
