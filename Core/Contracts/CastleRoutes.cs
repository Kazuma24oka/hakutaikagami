using HakutaiKagami.Core.World;
namespace HakutaiKagami.Core.Contracts;

// 百代鏡DBの城を拠点にした全国の移動網（Tools/HakutaiDb/build_castle_routes.py）。今の全国の移動網（NationalRoutes）に、
// 城の拠点と推定の連絡路を足したもの。設計：Docs/Proposals/NATIONAL_PLACEMENT_DESIGN_V1.md 第4節1。
// まだ世界には使っていない（旧版の世界と旧保存は今の移動網のまま）。
public sealed record CastleRouteNode(string Id,string Name,int LatitudeE7,int LongitudeE7,string? Province,string Castle);
public sealed record CastleRouteLink(string Id,string From,string To,string LengthMm,int FootSpeed,int CourierSpeed,string Mode,string Basis);
public sealed record CastleRouteCatalog(int Schema,string Id,int WorldYear,string BasedOn,string Database,string DatabaseSource,string Policy,CastleRouteNode[] Nodes,CastleRouteLink[] Edges);

public static class CastleRoutes
{
    public const string Ruleset="castle-routes-1560-v1";
    // 琉球の拠点（今の全国の移動網の首里・那覇・奄美）は城の移動網に入れない（操作できない地域）。
    // 全国の拠点どうしをまっすぐ結んだ陸の推定の連絡路（湖や海を横切る）は使わない。尾張の細かい道と海路は残す。
    static readonly HashSet<string> catalogNodes=NationalRoutes.Catalog.Nodes.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
    static readonly HashSet<string> straightLand=NationalRoutes.Catalog.Edges.Where(e=>e.Mode=="land"&&catalogNodes.Contains(e.From)&&catalogNodes.Contains(e.To)).Select(e=>e.Id).ToHashSet(StringComparer.Ordinal);
    public static BranchEdge[] KeptNationalEdges=>NationalRoutes.Edges.Where(e=>!straightLand.Contains(e.Id)&&!Outside.Contains(e.From)&&!Outside.Contains(e.To)).ToArray();
    public static readonly IReadOnlySet<string> Outside=NationalRoutes.Catalog.Nodes.Where(n=>n.LatitudeE7<289000000&&n.LongitudeE7<1310000000).Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
    static readonly Lazy<CastleRouteCatalog> catalog=new(()=>{var c=Read();Validate(c);return c;});
    public static CastleRouteCatalog Catalog=>ContractJson.Copy(catalog.Value);
    static CastleRouteCatalog Read()
    {
        using var s=typeof(CastleRoutes).Assembly.GetManifestResourceStream("CastleRoutes1560V1")??throw new InvalidDataException("城の移動網がありません");
        using var m=new MemoryStream();s.CopyTo(m);return ContractJson.Read<CastleRouteCatalog>(m.ToArray());
    }
    public static void Validate(CastleRouteCatalog c)
    {
        void Need(bool ok,string why){if(!ok)throw new InvalidDataException("城の移動網の検査："+why);}
        Need(c.Schema==1&&c.Id==Ruleset&&c.WorldYear==1560&&c.BasedOn==NationalRoutes.Ruleset&&c.Database==HakutaiDb.Edition1560&&c.DatabaseSource==HakutaiDb.Source.Commit,"版・元の移動網・元のDB");
        var national=NationalRoutes.Nodes.Select(n=>n.Id).Where(id=>!Outside.Contains(id)).ToHashSet(StringComparer.Ordinal);
        var ids=c.Nodes.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
        Need(ids.Count==c.Nodes.Length&&!ids.Overlaps(national)&&c.Nodes.All(n=>n.Id.StartsWith("c-",StringComparison.Ordinal)&&n.Id.All(ch=>char.IsAsciiLetterLower(ch)||char.IsAsciiDigit(ch)||ch=='-')),"拠点の ID");
        Need(c.Nodes.Select(n=>n.Castle).Order(StringComparer.Ordinal).SequenceEqual(HakutaiDb.PublicGeography().Where(p=>HakutaiDb.Castle(p.Id)!.Region==null).Select(p=>p.Id).Order(StringComparer.Ordinal)),"百代鏡DBの城（琉球を除く）ごとに1つの拠点");
        Need(c.Nodes.All(n=>HakutaiDb.Castle(n.Castle) is {} x&&x.LatitudeE7==n.LatitudeE7&&x.LongitudeE7==n.LongitudeE7),"拠点の位置は城の位置");
        Need(c.Edges.Select(e=>e.Id).Distinct().Count()==c.Edges.Length&&c.Edges.All(e=>e.From!=e.To&&(ids.Contains(e.From)||national.Contains(e.From))&&(ids.Contains(e.To)||national.Contains(e.To))),"道の両端");
        Need(c.Edges.All(e=>long.TryParse(e.LengthMm,out var l)&&l>0&&(e.Mode=="land"&&e.FootSpeed==1000&&e.CourierSpeed==2000||e.Mode=="sea"&&e.FootSpeed==6000&&e.CourierSpeed==8000)&&!string.IsNullOrWhiteSpace(e.Basis)),"道の長さ・速さ・種類");
        // すべての城の拠点が、今の全国の移動網につながる
        var adj=new Dictionary<string,List<string>>(StringComparer.Ordinal);
        void Link(string a,string b){(adj.TryGetValue(a,out var l)?l:adj[a]=[]).Add(b);(adj.TryGetValue(b,out var r)?r:adj[b]=[]).Add(a);}
        foreach(var e in KeptNationalEdges)Link(e.From,e.To);foreach(var e in c.Edges)Link(e.From,e.To);
        var seen=new HashSet<string>(StringComparer.Ordinal){"castle"};var stack=new Stack<string>(seen);
        while(stack.Count>0)foreach(var n in adj.GetValueOrDefault(stack.Pop())??[])if(seen.Add(n))stack.Push(n);
        Need(ids.All(seen.Contains),"つながらない城の拠点がある");
    }
    public static string NodeOf(string castle)=>"c-"+castle.Replace("-","").ToLowerInvariant();
    // 今の全国の拠点に、城の拠点を足したもの（位置は同じ投影）。
    public static BranchNode[] Nodes=>[..NationalRoutes.Nodes.Where(n=>!Outside.Contains(n.Id)),..catalog.Value.Nodes.Select(n=>{var p=JapanGeography.Project(new(n.LatitudeE7,n.LongitudeE7));return new BranchNode(n.Id,new(p.EastMm,p.NorthMm));})];
    public static BranchEdge[] Edges=>[..KeptNationalEdges,..catalog.Value.Edges.Select(e=>new BranchEdge(e.Id,e.From,e.To,long.Parse(e.LengthMm),e.FootSpeed,e.CourierSpeed))];
    public static SiteDefinition[] Sites=>[..NationalRoutes.Sites.Where(s=>!Outside.Contains(s.Id.Value["site:".Length..])),..catalog.Value.Nodes.Select(n=>new SiteDefinition(new("site:"+n.Id),n.Name,n.LatitudeE7,n.LongitudeE7,0))];
}
