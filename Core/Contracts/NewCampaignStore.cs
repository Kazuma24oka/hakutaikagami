namespace HakutaiKagami.Core.Contracts;
public sealed record NewCampaignProfile(string Instance,SetupScenario Setup,RegisteredOfficer[] Registered,string Selected,ulong Seed,string InitialHash)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public VoyageExpenseRules? VoyageRules {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? WorldArmyPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? EconomyPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CompanionPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? TrucePolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? InformationPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? StanddownPolicy {get;init;}


    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PersonalAuthorityLayout { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? HistoricalCatalog { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? NpcRoutinePolicy { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? InspectionPolicy { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkPolicy { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? DemandPolicy { get; init; }


}
public sealed record NewCampaignListing(NewCampaignProfile Profile,long Seconds,string Directory);
public sealed class NewCampaignStore
{
    readonly string root;
    public NewCampaignStore(string root)=>this.root=Path.GetFullPath(root);
    static bool Id(string s)=>Guid.TryParseExact(s,"N",out _);
    public string DirectoryFor(string instance){if(!Id(instance))throw new InvalidDataException("開始保存IDが不正です");return Path.Combine(root,instance);}
    public static ContractWorld Build(NewCampaignProfile p){if(!Id(p.Instance))throw new InvalidDataException("開始保存IDが不正です");var w=NewGameWorld.Create(p.Setup,p.Registered,p.Selected,p.Seed,p.PersonalAuthorityLayout,p.HistoricalCatalog,p.NpcRoutinePolicy,p.InspectionPolicy,p.WorkPolicy,p.DemandPolicy,p.VoyageRules,p.WorldArmyPolicy,p.EconomyPolicy,p.CompanionPolicy,p.TrucePolicy,p.InformationPolicy,p.StanddownPolicy);if(ContractJson.CanonicalHash(w)!=p.InitialHash)throw new InvalidDataException("開始定義が変更されています");return w;}
    public NewCampaignProfile New(SetupScenario setup,RegisteredOfficer[] registered,string selected,ulong seed,string? personalAuthorityLayout=null,string? historicalCatalog=null,string? npcRoutinePolicy=null,string? inspectionPolicy=null,string? workPolicy=null,string? demandPolicy=null,VoyageExpenseRules? voyageRules=null,string? worldArmyPolicy=null,string? economyPolicy=null,string? companionPolicy=null,string? trucePolicy=null,string? informationPolicy=null,string? standdownPolicy=null)
    {
        var initial=NewGameWorld.Create(setup,registered,selected,seed,personalAuthorityLayout,historicalCatalog,npcRoutinePolicy,inspectionPolicy,workPolicy,demandPolicy,voyageRules,worldArmyPolicy,economyPolicy,companionPolicy,trucePolicy,informationPolicy,standdownPolicy);var p=new NewCampaignProfile(Guid.NewGuid().ToString("N"),ContractJson.Copy(setup),NewGameWorld.Normalize(registered),selected,seed,ContractJson.CanonicalHash(initial)){PersonalAuthorityLayout=personalAuthorityLayout,HistoricalCatalog=historicalCatalog,NpcRoutinePolicy=npcRoutinePolicy,InspectionPolicy=inspectionPolicy,WorkPolicy=workPolicy,DemandPolicy=demandPolicy,VoyageRules=voyageRules,WorldArmyPolicy=worldArmyPolicy,EconomyPolicy=economyPolicy,CompanionPolicy=companionPolicy,TrucePolicy=trucePolicy,InformationPolicy=informationPolicy,StanddownPolicy=standdownPolicy};
        Directory.CreateDirectory(DirectoryFor(p.Instance));using var stream=new FileStream(Path.Combine(DirectoryFor(p.Instance),"start.json"),FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.Write(ContractJson.Write(p));stream.Flush(true);return p;
    }
    public NewCampaignProfile Read(string instance)
    {
        using var stream=new FileStream(Path.Combine(DirectoryFor(instance),"start.json"),FileMode.Open,FileAccess.Read,FileShare.Read);
        if(stream.Length is <1 or >8388608)throw new InvalidDataException("開始定義の容量が不正です");var bytes=new byte[(int)stream.Length];stream.ReadExactly(bytes);var p=ContractJson.Read<NewCampaignProfile>(bytes);if(p.Instance!=instance)throw new InvalidDataException("開始保存IDが一致しません");Build(p);return p;
    }
    public GenerationSaveStore Saves(NewCampaignProfile p)=>new(Path.Combine(DirectoryFor(p.Instance),"world"),Build(p).Definitions);
    public NewCampaignListing[] List(out int unreadable)
    {
        unreadable=0;var rows=new List<NewCampaignListing>();if(!Directory.Exists(root))return [];
        foreach(var dir in Directory.GetDirectories(root).Where(p=>Id(Path.GetFileName(p))))try{var p=Read(Path.GetFileName(dir));if(!Directory.Exists(Path.Combine(dir,"world","manual")))continue;var load=Saves(p).Prepare("manual");rows.Add(new(p,load.Seconds,dir));}catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or System.Text.Json.JsonException){unreadable++;}
        return rows.OrderByDescending(r=>r.Seconds).ThenBy(r=>r.Profile.Instance,StringComparer.Ordinal).ToArray();
    }
}
