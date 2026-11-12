namespace HakutaiKagami.Core.Contracts;

public sealed record ScaleFootprint(int Sites,int Provinces)
{
    public bool Supports(CourtScale scale)=>scale switch
    {
        CourtScale.Local=>Sites>=1,
        CourtScale.Daimyo=>Sites>=3,
        CourtScale.Great=>Sites>=8 && Provinces>=2,
        CourtScale.Hegemon=>Sites>=15 && Provinces>=4,
        _=>false
    };
}

public static class HouseTerritoryScale
{
    // 記名した出典は HouseScaleV1/provinces.md。未照合の拠点は国数に足さない。
    public static string? Province(EntityId site)=>site.Value switch
    {
        "site:ichijodani"=>"越前","site:nanao"=>"能登","site:hitoyoshi"=>"肥後",
        "site:funai"=>"豊後","site:kofu"=>"甲斐",_=>null
    };

    public static ScaleFootprint Measure(ContractWorld w,EntityId owner,long at,long rev)
    {using var _ccRev=ControlContext.At(rev);
        var sites=w.LandRights.Where(r=>r.Subject==owner && r.Kind=="control" && r.From<=at && at<r.Until)
            .Select(r=>r.Target).Distinct().Where(s=>!CastleAdministration.Blocked(w,s,at,rev)).ToArray();
        return new(sites.Length,sites.Select(Province).OfType<string>().Distinct().Count());
    }
}
