namespace HakutaiKagami.Core.Contracts;

public sealed record NationalFactionRelation(string SubjectHouse,string? SubjectFaction,string OverlordHouse,string? OverlordFaction,string Basis);
public sealed record NationalFactionRelationsState(int Schema,string Policy,NationalFactionRelation[] Relations);

public static class NationalFactionRelations
{
    public const string Policy="national-faction-relations-v1";
    const string Basis="hakutai-db-1560-v2:Overlord";

    public static NationalFactionRelationsState Initial(SetupScenario start)
    {
        var factions=start.Factions.Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);
        return new(1,Policy,HakutaiDb.Houses.Where(h=>h.Overlord!=null).OrderBy(h=>h.Id,StringComparer.Ordinal).Select(h=>new NationalFactionRelation(
            h.Id,factions.Contains(NationalStart.FactionId(h.Id))?NationalStart.FactionId(h.Id):null,
            h.Overlord!,factions.Contains(NationalStart.FactionId(h.Overlord!))?NationalStart.FactionId(h.Overlord!):null,Basis)).ToArray());
    }

    public static bool Enabled(ContractWorld w)=>NationalDistrictControl.Enabled(w);

    // A character sees only the direct ties of their own house. Missing database entries are not shown as proof of no relationship.
    public static string? DisplayFor(ContractWorld w,EntityId character)
    {
        if(!Enabled(w)||w.FactionRelations is not {Schema:1,Policy:Policy} state||w.NewGame is not {} game)return null;
        var person=game.Start.People.SingleOrDefault(p=>p.Id==character.Value);if(person?.Faction is not {} own)return null;
        var names=game.Start.Factions.ToDictionary(f=>f.Id,f=>f.Name,StringComparer.Ordinal);
        var items=new List<string>();
        foreach(var r in state.Relations.Where(r=>r.SubjectFaction==own||r.OverlordFaction==own).OrderBy(r=>r.SubjectHouse,StringComparer.Ordinal))
        {
            if(r.SubjectFaction==own&&r.OverlordFaction is {} overlord&&names.TryGetValue(overlord,out var overlordName))items.Add("主家・"+overlordName);
            if(r.OverlordFaction==own&&r.SubjectFaction is {} subject&&names.TryGetValue(subject,out var subjectName))items.Add("従属・"+subjectName);
        }
        return items.Count==0?null:string.Join(" / ",items.Distinct(StringComparer.Ordinal));
    }

    public static void Validate(ContractWorld w)
    {
        void Need(bool ok,string reason){if(!ok)throw new InvalidDataException("全国の勢力関係："+reason);}
        if(!Enabled(w)){Need(w.FactionRelations==null,"対象外の開始に勢力関係を混在できません");return;}
        Need(NationalStart.IsNational(w.NewGame!.HistoricalCatalog)&&w.NewGame.NetworkPolicy==WorldNetwork.CastlePolicy,"1560年全国開始だけで使用できます");
        Need(w.FactionRelations is {Schema:1,Policy:Policy}&&ContractJson.CanonicalHash(w.FactionRelations)==ContractJson.CanonicalHash(Initial(w.NewGame.Start)),"百代鏡DBの1560年従属関係と開始台帳が一致しません");
    }
}
