namespace HakutaiKagami.Core.Contracts;

public sealed record ImagawaPerson(EntityId Id,string Name,int BirthYear,EntityId? Father,string[] SourceIds);
public sealed record ImagawaPersonnel(int Schema,int WorldYear,EntityId Faction,string FactionName,string HomeCandidate,
    string HomeSource,PersonnelSource[] Sources,ImagawaPerson[] People)
{
    public static ImagawaPersonnel Read()
    {
        using var stream=typeof(ImagawaPersonnel).Assembly.GetManifestResourceStream("ImagawaPersonnelV1")
            ?? throw new InvalidDataException("今川家の人物資料がありません");
        using var bytes=new MemoryStream();
        stream.CopyTo(bytes);
        return ContractJson.Read<ImagawaPersonnel>(bytes.ToArray());
    }
}
