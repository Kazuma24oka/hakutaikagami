namespace HakutaiKagami.Core.Contracts;

public sealed record HistoricalStartPerson(EntityId Id,string Name,EntityId Faction,string FactionName,string HomeCandidate,string[] SourceIds,string Certainty);
public sealed record HistoricalStartEdition(int Schema,string Id,int WorldYear,string Policy,PersonnelSource[] Sources,HistoricalStartPerson[] People);
// Stable, closed editions: null is the original three-person scenario. Unknown ids never fall back.
public static class HistoricalStartCatalog
{
    public const string Western="historical-start-west-v2";
    public const string Current=HistoricalHouseholdCatalog.Id;
    static readonly HistoricalStartEdition current=Read();
    public static HistoricalStartEdition Edition=>ContractJson.Copy(current);
    static HistoricalStartEdition Read()
    {
        using var stream=typeof(HistoricalStartCatalog).Assembly.GetManifestResourceStream("HistoricalStartWestV2")??throw new InvalidDataException("Missing historical start edition");using var bytes=new MemoryStream();stream.CopyTo(bytes);var c=ContractJson.Read<HistoricalStartEdition>(bytes.ToArray());Validate(c);return c;
    }
    public static void Validate(HistoricalStartEdition c)
    {
        void Need(bool ok){if(!ok)throw new InvalidDataException("Invalid historical start edition provenance");}
        Need(c.Schema==1&&c.Id==Western&&c.WorldYear==1560&&!string.IsNullOrWhiteSpace(c.Policy));
        Need(c.Sources.Length>0&&c.Sources.Select(s=>s.Id).Distinct().Count()==c.Sources.Length&&c.Sources.All(s=>Uri.TryCreate(s.Url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&DateOnly.TryParseExact(s.CheckedOn,"yyyy-MM-dd",out _)&&!string.IsNullOrWhiteSpace(s.Fact)));
        Need(c.People.Length==2&&c.People.Select(p=>p.Id).Distinct().Count()==2&&c.People.Select(p=>p.Faction).Distinct().Count()==2);
        Need(c.People.All(p=>EntityId.IsValid(p.Id.Value)&&EntityId.IsValid(p.Faction.Value)&&!string.IsNullOrWhiteSpace(p.Name)&&!string.IsNullOrWhiteSpace(p.FactionName)&&!string.IsNullOrWhiteSpace(p.Certainty)&&NationalRoutes.Nodes.Any(n=>n.Id==p.HomeCandidate)&&p.SourceIds.Length>0&&p.SourceIds.Distinct().Count()==p.SourceIds.Length&&p.SourceIds.All(id=>c.Sources.Any(s=>s.Id==id))));
    }
    public static SetupScenario Scenario(string edition)
    {
        if(edition==HistoricalHouseholdCatalog.Id)return HistoricalHouseholdCatalog.Scenario();
        if(edition!=Western)throw new InvalidDataException("Unknown historical start edition; original save retained");
        var legacy=NewGameWorld.Scenario();var added=current.People.Select(p=>new SetupPerson(p.Id.Value,p.Name,RegionalCampaign.Site(p.HomeCandidate).Value,p.Faction.Value,null,false,false)).ToArray();
        var factions=legacy.Factions.Concat(current.People.Select(p=>new SetupFaction(p.Faction.Value,p.FactionName,p.Id.Value,RegionalCampaign.Site(p.HomeCandidate).Value))).ToArray();
        var scenario=legacy with{People=[..legacy.People,..added],Factions=factions,Castles=legacy.Castles.Select(c=>c.Faction==null?c with{Faction=factions.SingleOrDefault(f=>f.Home==c.Id)?.Id}:c).ToArray()};NewGameSetup.Validate(scenario);return scenario;
    }
}
