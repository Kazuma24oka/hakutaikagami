namespace HakutaiKagami.Core.Contracts;

public sealed record ImagawaPerson(EntityId Id,string Name,int BirthYear,EntityId? Father,string[] SourceIds);
public sealed record ImagawaPersonnel(int Schema,int WorldYear,EntityId Faction,string FactionName,string HomeCandidate,
    string HomeSource,PersonnelSource[] Sources,ImagawaPerson[] People)
{
    public SetupScenario StartingPlacement(SetupScenario prior,EntityId head)
    {
        if(!People.Any(person=>person.Id==head))
            throw new InvalidDataException("今川家の開始人物から当主を指定してください");
        var site=RegionalCampaign.Site(HomeCandidate).Value;
        if(prior.Castles.Single(c=>c.Id==site).Faction!=null)
            throw new InvalidDataException("駿府に既存の支配家があります");
        var members=People.Select(p=>new SetupPerson(p.Id.Value,p.Name,site,Faction.Value,
            p.Id==head?null:head.Value,false,false,p.BirthYear));
        var result=prior with{
            People=[..prior.People,..members],
            Factions=[..prior.Factions,new(Faction.Value,FactionName,head.Value,site)],
            Castles=prior.Castles.Select(c=>c.Id==site?c with{Faction=Faction.Value}:c).ToArray()
        };
        NewGameSetup.Validate(result);
        return result;
    }

    public static ImagawaPersonnel Read()
    {
        using var stream=typeof(ImagawaPersonnel).Assembly.GetManifestResourceStream("ImagawaPersonnelV1")
            ?? throw new InvalidDataException("今川家の人物資料がありません");
        using var bytes=new MemoryStream();
        stream.CopyTo(bytes);
        return ContractJson.Read<ImagawaPersonnel>(bytes.ToArray());
    }
}
