using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

// 位階・官職・幕府職の表（BD-54、2026-11-12。Docs/Proposals/RANKS_AND_OFFICES_V1.md の「使用する職の一覧」、Tools/Ranks/build_ranks.py）。
// 名乗りは根拠ごとに分ける：百代鏡DBに記録された1560年の名乗り（db-1560）と、朝廷の返書を開封した正式な叙任（court）。
public sealed record TitleEntry(string Id,string Name,string Kind,int Order,string? Rank,string Grantor,bool Warrior,string Certainty,string? Province);
public sealed record TitleCatalog(int Schema,string Id,string Source,TitleEntry[] Titles);
public sealed record HeldTitle(string Name,string? Title,string Basis);

public static class CentralTitles
{
    public const string DbBasis="db-1560";
    public const string CourtBasis="court";
    public const int NoRank=15; // 正六位（六位以下と無位をまとめた段）
    static readonly Lazy<TitleCatalog> catalog=new(()=>
    {
        using var s=typeof(CentralTitles).Assembly.GetManifestResourceStream("RanksOfficesV1")??throw new InvalidDataException("位階・官職の表がありません");
        using var m=new MemoryStream();s.CopyTo(m);var c=ContractJson.Read<TitleCatalog>(m.ToArray());Validate(c);return c;
    });
    static readonly Lazy<Dictionary<string,TitleEntry>> byId=new(()=>catalog.Value.Titles.ToDictionary(t=>t.Id,StringComparer.Ordinal));
    static readonly Lazy<Dictionary<string,TitleEntry>> byName=new(()=>catalog.Value.Titles.Where(t=>t.Kind!="hyakkan").GroupBy(t=>t.Name,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.Ordinal));
    public static IReadOnlyList<TitleEntry> Titles=>catalog.Value.Titles;
    public static TitleEntry? Title(string id)=>byId.Value.GetValueOrDefault(id);
    public static TitleEntry? ByName(string name)=>byName.Value.GetValueOrDefault(name);
    static void Validate(TitleCatalog c)
    {
        if(c.Schema!=1||c.Id!="ranks-offices-v1"||c.Titles.Select(t=>t.Id).Distinct(StringComparer.Ordinal).Count()!=c.Titles.Length)throw new InvalidDataException("位階・官職の表の版または ID が不正です");
        var ranks=c.Titles.Where(t=>t.Kind=="rank").Select(t=>t.Name).ToHashSet(StringComparer.Ordinal);
        if(ranks.Count!=15||c.Titles.Any(t=>t.Rank!=null&&!ranks.Contains(t.Rank)))throw new InvalidDataException("位階・官職の表の相当位階が不正です");
    }
    static int RankOrder(string rank)=>catalog.Value.Titles.Single(t=>t.Kind=="rank"&&t.Name==rank).Order;

    // 本人の名乗り（DBの1560年の名乗りと、開封した朝廷の返書）。
    public static HeldTitle[] Held(ContractWorld w,EntityId person)
    {
        var rows=new List<HeldTitle>();
        if(NationalStart.DbPerson(person.Value)?.Kanto is {Length:>0} kanto)rows.Add(new(kanto,ByName(kanto)?.Id,DbBasis));
        var events=HouseholdCourt.Events(w);
        foreach(var e in events.Where(e=>e.Input.Action==CourtAction.IssueCourtRank&&e.Input.Person==person&&MessageFlow.Knows(w.Messaging,CentralRank.ReplyId(e),person.Value)))
        {var id=CentralRank.RequestedTitle(events,e)??"rank:14";rows.Add(new(Title(id)!.Name,id,CourtBasis));}
        return rows.ToArray();
    }
    // 朝廷が記録している正式な位階の序列（返書の開封と関係なく、審議した時点の真実）。なければ正六位。
    internal static int FormalRank(ContractWorld w,EntityId person,long at,long rev)
    {
        int best=NoRank;
        var events=HouseholdCourt.Events(w);
        foreach(var e in events.Where(e=>e.Input.Action==CourtAction.IssueCourtRank&&e.Input.Person==person&&(e.At<at||e.At==at&&e.Revision<rev)))
        {
            var t=Title(CentralRank.RequestedTitle(events,e)??"rank:14");
            if(t?.Kind=="rank")best=Math.Min(best,t.Order);else if(t?.Rank is {} r)best=Math.Min(best,RankOrder(r));
        }
        return best;
    }
    static bool FormallyHeldByOther(ContractWorld w,EntityId person,string title,long at,long rev)
    {
        var events=HouseholdCourt.Events(w);
        return events.Any(e=>e.Input.Action==CourtAction.IssueCourtRank&&e.Input.Person!=person&&(e.At<at||e.At==at&&e.Revision<rev)&&CentralRank.RequestedTitle(events,e)==title);
    }

    // 朝廷の審議：位階は今の一つ上だけ。受領名・京官は相当の位階に届いていること。正式な任官は1つの職に1人。
    internal static bool Eligible(ContractWorld w,EntityId person,EntityId house,string title,long at,long rev)
    {
        if(Title(title) is not {} t||t.Grantor!="court"||t.Kind is not("rank" or "kokushi" or "kyokan"))return false;
        if(HouseSuccession.Head(w,house,at,rev)!=person)return false;
        int now=FormalRank(w,person,at,rev);
        if(t.Kind=="rank")return t.Order==now-1&&t.Order>=6; // 従三位より上は大臣・納言とあわせて（後の工程）
        if(t.Rank==null||t.Order==0)return false; // 相当位階のない職（摂関・蔵人など）は申し出られない
        return now<=RankOrder(t.Rank)&&!FormallyHeldByOther(w,person,title,at,rev);
    }
    // 申し出の候補（画面用）：一つ上の位階、家の城がある国の受領名、武家に多い京官のうち、今の位階で届くもの。
    public static TitleEntry[] Requestable(ContractWorld w,EntityId person,EntityId house)
    {
        long at=w.ElapsedSeconds,rev=w.CommitSequence+1;
        var provinces=w.NewGame!.Start.Castles.Where(c=>c.Faction==house.Value).Select(c=>CastleRoutes.Catalog.Nodes.FirstOrDefault(n=>"site:"+n.Id==c.Id)?.Province).Where(p=>p!=null).ToHashSet(StringComparer.Ordinal);
        return catalog.Value.Titles.Where(t=>(t.Kind=="rank"||t.Kind=="kokushi"&&t.Province!=null&&provinces.Contains(t.Province)||t.Kind=="kyokan"&&t.Warrior)&&Eligible(w,person,house,t.Id,at,rev))
            .OrderBy(t=>t.Kind=="rank"?0:t.Kind=="kokushi"?1:2).ThenBy(t=>t.Order).ToArray();
    }
}
