namespace HakutaiKagami.Core.Contracts;

// 百代鏡DBの一つの開始年の版（2026-11-17、S03）。家・人物・城の持ち主・郡の支配はその年の見積もり。
public sealed class HakutaiDbEra
{
    public readonly HakutaiDbEdition Edition;
    public readonly Dictionary<string,HakutaiDbPerson> People;
    public readonly Dictionary<string,HakutaiDbHouse> Houses;
    public readonly Dictionary<string,HakutaiDbCastle> Castles;
    public readonly Dictionary<string,HakutaiDbPerson[]> Members;
    public readonly Dictionary<string,HakutaiDbDistrict> Districts;
    public HakutaiDbEra(HakutaiDbEdition e)
    {
        Edition=e;People=e.People.ToDictionary(p=>p.Id,StringComparer.Ordinal);Houses=e.Houses.ToDictionary(h=>h.Id,StringComparer.Ordinal);
        Castles=e.Castles.ToDictionary(c=>c.Id,StringComparer.Ordinal);
        Districts=e.Districts.ToDictionary(d=>d.Id,StringComparer.Ordinal);
        Members=e.People.GroupBy(p=>p.House,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
    }
    public int Year=>Edition.WorldYear;
    public HakutaiDbPerson? Person(string id)=>People.GetValueOrDefault(id);
    public HakutaiDbHouse? House(string id)=>Houses.GetValueOrDefault(id);
    public HakutaiDbCastle? Castle(string id)=>Castles.GetValueOrDefault(id);
    public HakutaiDbDistrict? District(string id)=>Districts.GetValueOrDefault(id);
    public IReadOnlyList<HakutaiDbHouse> HouseList=>Edition.Houses;
    public IReadOnlyList<HakutaiDbDistrict> DistrictList=>Edition.Districts;
    public IReadOnlyList<HakutaiDbPerson> MembersOf(string house)=>Members.TryGetValue(house,out var m)?m:[];
    public IReadOnlyList<HakutaiDbCastle> CastlesOf(string house)=>Edition.Castles.Where(c=>c.Owner==house).OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray();
}
