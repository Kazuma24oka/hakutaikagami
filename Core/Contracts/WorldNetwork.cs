namespace HakutaiKagami.Core.Contracts;

// 新規ゲームの世界ごとの移動網の版（設計 Docs/Proposals/NATIONAL_PLACEMENT_DESIGN_V1.md 第4節1）。
// 版を持たない世界（今までの開始と旧保存）は今の全国の移動網（NationalRoutes）のまま。
// 城の移動網には、従来のv1と地形コストを加えた新規全国開始用v2がある。
public static class WorldNetwork
{
    public const string CastlePolicy=TerrainCastleRoutes.Ruleset;
    public static bool Castle(NewGameState? data)=>data?.NetworkPolicy is CastleRoutes.Ruleset or TerrainCastleRoutes.Ruleset;
    static bool Terrain(NewGameState? data)=>data?.NetworkPolicy==TerrainCastleRoutes.Ruleset;
    public static bool Castle(ContractWorld w)=>Castle(w.NewGame);
    static bool Terrain(ContractWorld w)=>Terrain(w.NewGame);
    public static void ValidatePolicy(string? policy){if(policy is not(null or CastleRoutes.Ruleset or TerrainCastleRoutes.Ruleset))throw new InvalidDataException("移動網の版が不正です");}

    // 城の移動網は大きいので一度だけ作る（中身は変えない）。
    static readonly Lazy<SiteDefinition[]> castleSites=new(()=>CastleRoutes.Sites);
    static readonly Lazy<BranchNode[]> castleNodes=new(()=>CastleRoutes.Nodes);
    static readonly Lazy<BranchEdge[]> castleEdges=new(()=>CastleRoutes.Edges);
    static readonly Lazy<BranchEdge[]> terrainCastleEdges=new(()=>TerrainCastleRoutes.Edges);
    static readonly Lazy<Dictionary<EntityId,SiteDefinition>> castleSiteIndex=new(()=>castleSites.Value.ToDictionary(s=>s.Id));
    static readonly Lazy<Dictionary<string,BranchNode>> castleNodeIndex=new(()=>castleNodes.Value.ToDictionary(n=>n.Id,StringComparer.Ordinal));

    // 城の移動網の世界の拠点か（世界の定義の拠点は城の移動網と同じであることを検査済み）。毎回一覧をなめない。
    public static bool HasCastleWorldSite(EntityId site)=>castleSiteIndex.Value.ContainsKey(site);
    public static SiteDefinition[] Sites(ContractWorld w)=>Castle(w)?castleSites.Value:NationalRoutes.Sites;
    public static SiteDefinition[] Sites(NewGameState data)=>Castle(data)?castleSites.Value:NationalRoutes.Sites;
    public static BranchNode[] Nodes(ContractWorld w)=>Castle(w)?castleNodes.Value:NationalRoutes.Nodes;
    public static BranchEdge[] Edges(ContractWorld w)=>Terrain(w)?terrainCastleEdges.Value:Castle(w)?castleEdges.Value:NationalRoutes.Edges;

    // 拠点の名前・位置を ID で引く（どの版の世界の拠点でも。今の全国の拠点は今までどおり NationalRoutes から）。
    public static SiteDefinition Site(EntityId id)=>NationalRoutes.Sites.FirstOrDefault(s=>s.Id==id)??(castleSiteIndex.Value.TryGetValue(id,out var s)?s:throw new InvalidOperationException("収録されていない拠点です"));
    public static BranchNode? Node(string id)=>NationalRoutes.Nodes.FirstOrDefault(n=>n.Id==id)??castleNodeIndex.Value.GetValueOrDefault(id);
    // 今の全国の拠点を拠点 ID で引く索引（一覧の先にあるものを優先する FirstOrDefault と同じ結果）。
    static readonly Lazy<Dictionary<string,BranchNode>> nationalBySite=new(()=>{var d=new Dictionary<string,BranchNode>(StringComparer.Ordinal);foreach(var n in NationalRoutes.Nodes)d.TryAdd(RegionalCampaign.Site(n.Id).Value,n);return d;});
    public static BranchNode? NodeOfSite(string site)=>nationalBySite.Value.GetValueOrDefault(site)??(site.StartsWith("site:c-",StringComparison.Ordinal)?castleNodeIndex.Value.GetValueOrDefault(site["site:".Length..]):null);

    // 開始配置に城の拠点を足す（持ち主なし。DBの家の配置は全国開始の版で入れる）。
    public static SetupScenario WithCastles(SetupScenario s)=>s with{Castles=[..s.Castles.Where(c=>!CastleRoutes.Outside.Contains(c.Id["site:".Length..])),..CastleRoutes.Catalog.Nodes.Select(n=>new SetupCastle("site:"+n.Id,null))]};
}
