namespace HakutaiKagami.Core.Contracts;

public sealed record PersonnelSource(string Id,string Title,string Url,string CheckedOn,string Fact);
public sealed record PersonnelSeed(EntityId Id,string Name,EntityId Faction,string HomeCandidate,string[] SourceIds,string Certainty);
public sealed record FactionSeed(EntityId Id,string Name,string[] SourceIds,string TerritoryStatus);
public sealed record PeaceWorkSeed(EntityId Id,string Title,EntityId Issuer,EntityId Recipient,EntityId Site,bool Subordinate,string Basis);
public sealed record PersonnelCatalog(int Schema,int WorldYear,string Policy,PersonnelSource[] Sources,PersonnelSeed[] People,FactionSeed[] Factions,PeaceWorkSeed[] Work);
public sealed record PersonnelCoverage(string Node,string Name,int People,string Status);
public sealed record PeaceWorkOption(EntityId Id,string Title,bool CanDispatch,string Reason);

// Scenario reference data is not a character's observation, ownership, office or AI state.
public static class NationalPersonnel
{
    static readonly PersonnelCatalog catalog=Read();
    static PersonnelCatalog Read()
    {
        using var stream=typeof(NationalPersonnel).Assembly.GetManifestResourceStream("NationalPersonnelV1")??throw new InvalidDataException("Missing personnel catalog");
        using var memory=new MemoryStream();stream.CopyTo(memory);var c=ContractJson.Read<PersonnelCatalog>(memory.ToArray());ValidateCatalog(c);return c;
    }
    public static PersonnelCatalog Catalog=>ContractJson.Copy(catalog);
    public static string Version{get;}=ContractJson.CanonicalHash(catalog);
    public static void ValidateCatalog(PersonnelCatalog c)
    {
        void Need(bool ok){if(!ok)throw new InvalidDataException("Invalid national personnel provenance or identity");}
        bool Sources(string[] ids)=>ids.Length>0&&ids.Distinct().Count()==ids.Length&&ids.All(id=>c.Sources.Any(s=>s.Id==id));
        Need(c.Schema==1&&c.WorldYear==1560&&!string.IsNullOrWhiteSpace(c.Policy));
        Need(c.Sources.Select(s=>s.Id).Distinct().Count()==c.Sources.Length&&c.Sources.All(s=>Uri.TryCreate(s.Url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&DateOnly.TryParseExact(s.CheckedOn,"yyyy-MM-dd",out _)&&!string.IsNullOrWhiteSpace(s.Fact)));
        Need(c.People.Length==TravelWorld.People.Length&&c.Factions.Length==TravelWorld.People.Select(p=>p.House).Distinct().Count()&&c.People.Select(p=>p.Id).Distinct().Count()==c.People.Length&&c.Factions.Select(f=>f.Id).Distinct().Count()==c.Factions.Length);
        Need(c.Factions.All(f=>Sources(f.SourceIds)&&TravelWorld.People.Any(p=>"org:"+p.House==f.Id.Value&&p.HouseName==f.Name)&&!string.IsNullOrWhiteSpace(f.TerritoryStatus)));
        Need(c.People.All(p=>Sources(p.SourceIds)&&c.Factions.Any(f=>f.Id==p.Faction)&&NationalRoutes.Nodes.Any(n=>n.Id==p.HomeCandidate)&&TravelWorld.People.Any(t=>"person:"+t.Id==p.Id.Value&&t.Name==p.Name&&"org:"+t.House==p.Faction.Value&&t.Home==p.HomeCandidate)&&!string.IsNullOrWhiteSpace(p.Certainty)));
        Need(c.Work.Length==2&&c.Work.Select(w=>w.Id).Distinct().Count()==2&&c.Work.Select(w=>w.Subordinate).Distinct().Count()==2);
        Need(c.Work.All(w=>w.Id==new EntityId(w.Subordinate?"work:clerk-inspect":"work:lord-inspect")&&w.Issuer==(w.Subordinate?ContractFixture.Clerk:ContractFixture.Lord)&&w.Recipient==ContractFixture.Officer&&w.Site==ContractFixture.Castle&&!string.IsNullOrWhiteSpace(w.Basis)));
    }
    public static PersonnelCoverage[] Coverage()=>NationalRoutes.Nodes.Select(n=>{
        int count=catalog.People.Count(p=>p.HomeCandidate==n.Id);
        return new PersonnelCoverage(n.Id,NationalRoutes.Sites.Single(s=>s.Id==RegionalCampaign.Site(n.Id)).Name,count,count==0?"人物・勢力未収録":"本拠候補を部分収録・領域未収録");
    }).ToArray();
    // This is an explicit dispatcher test view, not the recipient's knowledge of a pending job.
    // Generic failure avoids exposing another person's location, authority or private motivation.
    public static PeaceWorkOption[] DispatcherOptions(ContractWorld w,EntityId issuer)=>catalog.Work.Where(t=>t.Issuer==issuer).Select(t=>
        new PeaceWorkOption(t.Id,t.Title,Available(w,t),Available(w,t)?"発送可能（試作設定）":"現在は発送できません")).ToArray();
    static bool Available(ContractWorld w,PeaceWorkSeed t)
    {
        if(!IncomingMission.Enabled(w)||w.PlayerControl!=t.Recipient||w.Region?.Active!=false)return false;
        bool AtSite(EntityId actor)=>w.Locations.Any(l=>l.Actor==actor&&l.Kind==LocationKind.AtSite&&l.Site==t.Site);
        if(!AtSite(t.Issuer)||!AtSite(t.Recipient)||OfficerLoop.Busy(w,t.Recipient))return false;
        EntityId subject=t.Subordinate?t.Issuer:t.Recipient,target=t.Subordinate?t.Recipient:t.Issuer;
        if(!HouseholdService.BondsAt(w,w.ElapsedSeconds,w.CommitSequence+1).Any(b=>b.Subject==subject&&b.Target==target&&b.From<=w.ElapsedSeconds&&w.ElapsedSeconds<b.Until))return false;
        var i=IncomingMission.Build(w,"request:personnel-preview",t.Subordinate);
        return WorldSession.Permits(w,t.Issuer,i,w.ElapsedSeconds)&&WorldSession.Permits(w,t.Recipient,i,w.ElapsedSeconds);
    }
    public static Intent Build(ContractWorld w,EntityId work,string request,int due=60)
    {
        var t=catalog.Work.SingleOrDefault(t=>t.Id==work);
        if(t==null||!Available(w,t))throw new InvalidDataException("現在は発送できません");
        return IncomingMission.Build(w,request,t.Subordinate,due);
    }
}
