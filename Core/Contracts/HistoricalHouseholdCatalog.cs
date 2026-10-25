namespace HakutaiKagami.Core.Contracts;
public sealed record HistoricalHouseholdPerson(EntityId Id,string Name,EntityId Faction,EntityId Lord,string HomeCandidate,string[] SourceIds,string Certainty);
public sealed record HistoricalHouseholdEdition(int Schema,string Id,string BaseEdition,int WorldYear,string Policy,PersonnelSource[] Sources,HistoricalHouseholdPerson[] People);
public static class HistoricalHouseholdCatalog
{
    public const string Id="historical-start-household-v3";
    static readonly HistoricalHouseholdEdition edition=Read();
    public static HistoricalHouseholdEdition Edition=>ContractJson.Copy(edition);
    static HistoricalHouseholdEdition Read(){using var stream=typeof(HistoricalHouseholdCatalog).Assembly.GetManifestResourceStream("HistoricalHouseholdV3")??throw new InvalidDataException("Missing household edition");using var bytes=new MemoryStream();stream.CopyTo(bytes);var c=ContractJson.Read<HistoricalHouseholdEdition>(bytes.ToArray());Validate(c);return c;}
    public static void Validate(HistoricalHouseholdEdition c)
    {
        void Need(bool ok){if(!ok)throw new InvalidDataException("Invalid historical household provenance");}
        Need(c.Schema==1&&c.Id==Id&&c.BaseEdition==HistoricalStartCatalog.Western&&c.WorldYear==1560&&!string.IsNullOrWhiteSpace(c.Policy));
        Need(c.Sources.Length>0&&c.Sources.Select(s=>s.Id).Distinct().Count()==c.Sources.Length&&c.Sources.All(s=>Uri.TryCreate(s.Url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&DateOnly.TryParseExact(s.CheckedOn,"yyyy-MM-dd",out _)&&!string.IsNullOrWhiteSpace(s.Fact)));
        var baseline=NewGameWorld.Scenario(HistoricalStartCatalog.Western);Need(c.People.Length==1&&c.People.All(p=>EntityId.IsValid(p.Id.Value)&&!baseline.People.Any(old=>old.Id==p.Id.Value)&&!string.IsNullOrWhiteSpace(p.Name)&&!string.IsNullOrWhiteSpace(p.Certainty)&&NationalRoutes.Nodes.Any(n=>n.Id==p.HomeCandidate)&&baseline.People.Any(l=>l.Id==p.Lord.Value&&l.Faction==p.Faction.Value)&&p.SourceIds.Length>0&&p.SourceIds.Distinct().Count()==p.SourceIds.Length&&p.SourceIds.All(id=>c.Sources.Any(s=>s.Id==id))));
    }
    public static SetupScenario Scenario(){var baseline=NewGameWorld.Scenario(HistoricalStartCatalog.Western);var added=edition.People.Select(p=>new SetupPerson(p.Id.Value,p.Name,RegionalCampaign.Site(p.HomeCandidate).Value,p.Faction.Value,p.Lord.Value,false,false,null,null,StartTemplate.Retainer));var result=baseline with{People=[..baseline.People,..added]};NewGameSetup.Validate(result);return result;}
}
