using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

public enum Historicity { Attested, Uncertain, Fictional }
// 開始時に登場する武将（DBの人物か、オリジナルのモブ武将）。
public sealed record HakutaiDbOfficer(string Id,string Name,string House,Historicity Historicity,string? Person,int Rank);
public sealed record HakutaiDbRoster(string House,long Kokudaka,long MoneyKan,int Quota,int SoldierCap,HakutaiDbOfficer[] Officers,int DbMembers,int Mobs,int LeftOut);

// 百代鏡DBからの開始配置の決まり（Docs/Decisions/2026-11-12_HAKUTAI_DB_START_RULES.md）。
// 登場人数と兵の上限は、支配する郡の石高と金銭収入（石に換算）から。数値は試作値。
public static class HakutaiDbStart
{
    public const string Policy="hakutai-db-start-rules-v1";
    public const int OfficersPerTenThousandKoku=2;
    public const int SoldiersPerTenThousandKoku=250;
    public const int MinimumOfficers=3;
    public const int KokuPerKan=2;

    // 実在性の表示：〇（確度A〜C）△（確度D）×（確度E・オリジナルのモブ）。
    public static Historicity Of(HakutaiDbPerson p)=>p.Existence switch{"A" or "B" or "C"=>Historicity.Attested,"E"=>Historicity.Fictional,_=>Historicity.Uncertain};
    public static string Mark(Historicity h)=>h switch{Historicity.Attested=>"〇",Historicity.Uncertain=>"△",_=>"×"};

    // 郡ごとの金銭収入は戦国DBにまだない。町・港・市の収入の決まりを作るまでは0（石高だけで数える）。
    public static long MoneyKan(string house)=>0;
    public static long IncomeKoku(HakutaiDbHouse h)=>h.Kokudaka+MoneyKan(h.Id)*KokuPerKan;
    public static int Quota(HakutaiDbHouse h)=>h.Religious?HakutaiDb.Members(h.Id).Count:Math.Max(MinimumOfficers,(int)Math.Round(IncomeKoku(h)*OfficersPerTenThousandKoku/10000d,MidpointRounding.AwayFromZero));
    public static int SoldierCap(HakutaiDbHouse h)=>(int)(IncomeKoku(h)*SoldiersPerTenThousandKoku/10000);

    // 残す順：当主 → 1560年の所属が記録による人 → 推定（主君・苗字と国から）→ 推定（国の支配者から）。
    // 推定の所属で、家の国（支配する郡・本拠の国）の外の人は後ろ。同じ段では 〇 → △ → ×、記録の多い順、ID の順。
    static int Tier(HakutaiDbHouse h,HakutaiDbPerson p,ISet<string> provinces)
    {
        if(p.Id==h.Head)return 0;
        int t=p.HouseBasis is "勢力" or "勢力（父の家）" or "勢力（主君の時期から）"?1:p.HouseBasis.Contains("国の支配者")?3:2;
        // 国は後の活動地のこともあるので、推定の所属の人だけに使う（記録の所属の柴田勝家＝越前などは下げない）。
        return t==1||p.Province is null or "国不明" or "明"||provinces.Count==0||provinces.Contains(p.Province)?t:t+3;
    }
    static ISet<string> Provinces(HakutaiDbHouse h)
    {
        var set=HakutaiDb.Districts.Where(d=>d.Owner==h.Id).Select(d=>d.Province.TrimEnd('国').Replace('驒','騨')).ToHashSet(StringComparer.Ordinal);
        if(h.Home is {} home&&HakutaiDb.Castle(home)?.Province is {} pv)set.Add(pv.TrimEnd('国').Replace('驒','騨'));
        return set;
    }

    public static HakutaiDbRoster Roster(string houseId)
    {
        var h=HakutaiDb.House(houseId)??throw new KeyNotFoundException(houseId);int quota=Quota(h);
        var members=HakutaiDb.Members(houseId);
        var provinces=Provinces(h);var kept=members.OrderBy(p=>Tier(h,p,provinces)).ThenBy(p=>(int)Of(p)).ThenByDescending(p=>p.Records).ThenBy(p=>p.Id,StringComparer.Ordinal).Take(quota)
            .Select((p,i)=>new HakutaiDbOfficer(p.Id,p.StartName,houseId,Of(p),p.Id,i)).ToList();
        int mobs=h.Religious?0:Math.Max(0,quota-kept.Count);
        var used=kept.Select(o=>o.Name).ToHashSet();for(int i=0;i<mobs;i++){var m=Mob(h,i,kept.Count,used);used.Add(m.Name);kept.Add(m);}
        return new(houseId,h.Kokudaka,MoneyKan(houseId),quota,SoldierCap(h),kept.ToArray(),members.Count,mobs,Math.Max(0,members.Count-quota));
    }

    // オリジナルのモブ武将：家の人に多い苗字（なければ家の名前）と、よくある通称から、家と番号で決まる名前。
    static readonly string[] Given=["源三郎","弥兵衛","与十郎","孫六","新左衛門","彦七","甚五郎","勘助","半兵衛","市之丞","藤八","喜兵衛","太郎左衛門","清蔵","庄九郎","伝右衛門","作兵衛","又四郎","権之助","久内"];
    static HakutaiDbOfficer Mob(HakutaiDbHouse h,int n,int rank,ISet<string> used)
    {
        var surnames=HakutaiDb.Members(h.Id).Select(p=>p.Surname).OfType<string>().Where(s=>s.Length is >=1 and <=3).GroupBy(s=>s).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key,StringComparer.Ordinal).Select(g=>g.Key).Take(6).ToArray();
        string house=h.Name.Split('（')[0].TrimEnd('氏','家','衆');
        if(surnames.Length==0)surnames=[house.Length is >=1 and <=3?house:"某"];
        uint x=BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(h.Id+"/mob/"+n)),0);
        string name="";
        for(int k=0;k<surnames.Length*Given.Length+1;k++){name=surnames[(x+k/Given.Length)%surnames.Length]+Given[(x/7+k)%Given.Length];if(!used.Contains(name))break;}
        if(used.Contains(name))name+=n;
        return new("mob:"+h.Id+"/"+n,name,h.Id,Historicity.Fictional,null,rank);
    }
}
