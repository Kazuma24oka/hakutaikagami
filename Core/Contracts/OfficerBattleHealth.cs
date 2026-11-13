using System.Security.Cryptography;
using System.Text;
namespace HakutaiKagami.Core.Contracts;
public sealed record OfficerWound(EntityId Unit,EntityId Person,HealthPart Part,int Severity,long At);
public static class OfficerBattleHealth
{
    // 同じ人物・同じ日には同じ危険判定を使い、刻みや再送で抽選を増やさない。
    internal static OfficerWound[]? Resolve(ContractWorld w,EngagementResult result)
    {
        if(!ClinicalHealth.Enabled(w))return null;
        var seen=(w.WorldClashes?.Steps??[]).SelectMany(s=>s.OfficerWounds??[]).Where(x=>x.At/86400==result.At/86400).Select(x=>x.Person).ToHashSet();
        var wounds=new List<OfficerWound>();
        foreach(var loss in result.Losses.Where(l=>l.Soldiers>0).OrderBy(l=>l.Unit.Value,StringComparer.Ordinal))
        {
            var unit=w.Units.SingleOrDefault(u=>u.Id==loss.Unit);if(unit==null)continue;
            var person=HouseholdDetachment.Appointment(w,unit.Id,result.At,w.CommitSequence+1)?.Actor??unit.Commander;
            if(seen.Contains(person))continue;
            var d=SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"officer-wound-v2/{w.Seed}/{person.Value}/{result.At/86400}")));
            if(d[0]%100>=15)continue;
            seen.Add(person);wounds.Add(new(unit.Id,person,(HealthPart)(d[1]%3),1+d[2]%3,result.At));
        }
        return wounds.ToArray();
    }
    internal static void Validate(ContractWorld context,WorldClashStep step,EngagementResult result)
    {
        if(ContractJson.CanonicalHash(step.OfficerWounds)!=ContractJson.CanonicalHash(Resolve(context,result)))throw new InvalidDataException("交戦時の武将の負傷を再現できません");
    }
    internal static IEnumerable<HealthCondition> Conditions(ContractWorld w)
    {
        if(!ClinicalHealth.Enabled(w))yield break;
        foreach(var x in (w.WorldClashes?.Steps??[]).SelectMany(s=>s.OfficerWounds??[]))
        {
            string source=FormattableString.Invariant($"battle-health-v2/{x.Person.Value}/{x.At}");
            long days=x.Severity switch{1=>3,2=>14,_=>45};
            yield return new(new("condition:"+ContractJson.Hash(Encoding.UTF8.GetBytes(source))[..24]),x.Person,HealthKind.Injury,x.Part,x.Severity,x.At,days*86400);
        }
    }
}
