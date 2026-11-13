using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// 傷病死（BD-06・BD-07）。危篤（重さ3）の傷病は、発症から7日を過ぎた最初の正午に峠を迎え、
// 固定シード・年齢・それまでの手当で生死が決まる。死亡は自然死と同じ記録（NaturalDeaths）に原因の傷病を付けて残し、
// 訃報・後見・家督の判断は既存の死亡の流れがそのまま扱う。診療の版（ClinicalHealth）を持つ新規開始だけで有効。
public static class HealthMortality
{
    const long Day=86400,Window=30*Day,Crisis=7*Day;
    public static bool Enabled(ContractWorld w)=>ClinicalHealth.Enabled(w);

    public static long CrisisAt(HealthCondition c)
    {
        long t=checked(c.OnsetAt+Crisis),noon=t/Day*Day+Day/2;
        return noon<t?noon+Day:noon;
    }
    static int Age(ContractWorld w,EntityId person,long at)
    {
        var p=FamilyBirth.People(w).FirstOrDefault(p=>p.Id==person.Value);
        return p?.BirthYear is int born?WorldCalendar.At(w.EpochDate,at).Year-born:30;
    }
    // 峠の時点までに手当を受けていれば危険は三分の一。手当の記録は峠より前のものだけを見る。
    public static int Risk(ContractWorld w,HealthCondition c)
    {
        long at=CrisisAt(c);
        int risk=(c.Kind==HealthKind.Injury?200:250)+Math.Max(0,Age(w,c.Person,at)-50)*10;
        risk=Math.Min(risk,800);
        return PersonalHealth.Treatment(w,c) is {} care&&care.At<at?risk/3:risk;
    }
    public static bool Fatal(ContractWorld w,HealthCondition c)
    {
        if(c.Severity<3||PersonalHealth.RecoverAt(w,c)<=CrisisAt(c))return false;
        var d=SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture,$"health-death-v1/{w.NewGame!.Seed}/{c.Id.Value}")));
        return BinaryPrimitives.ReadUInt32LittleEndian(d)%1000<Risk(w,c);
    }
    // 峠が (from, until] にある危篤の傷病。
    static IEnumerable<HealthCondition> Crises(ContractWorld w,long from,long until)
    {
        foreach(var c in PersonalHealth.EventConditions(w))
            if(c.Severity>=3&&CrisisAt(c) is var at&&at>from&&at<=until)yield return c;
        long first=Math.Max(0,(from-Crisis-Day)/Window),last=Math.Max(0,until-Crisis)/Window;
        var people=PersonalHealth.Living(w).ToArray();
        for(long k=first;k<=last;k++)
            foreach(var p in people)
                if(PersonalHealth.Illness(w,p,k) is {Severity:>=3} c&&CrisisAt(c) is var at&&at>from&&at<=until)yield return c;
    }
    internal static long? Next(ContractWorld w,long target)
    {
        if(!Enabled(w)||target<=w.ElapsedSeconds)return null;
        long? best=null;
        foreach(var c in Crises(w,w.ElapsedSeconds,target))
            if(!Assassination.DeadAt(w,c.Person,CrisisAt(c),long.MaxValue))best=Math.Min(best??long.MaxValue,CrisisAt(c));
        return best;
    }
    // 今の時刻が峠で、命を落とす傷病。同じ人に二つ重なれば、識別子の順で先の傷病を原因にする。
    internal static IEnumerable<(EntityId Person,EntityId Condition)> Due(ContractWorld w)
    {
        if(!Enabled(w))yield break;
        foreach(var g in Crises(w,w.ElapsedSeconds-1,w.ElapsedSeconds).Where(c=>Fatal(w,c)).GroupBy(c=>c.Person).OrderBy(g=>g.Key.Value,StringComparer.Ordinal))
            yield return (g.Key,g.OrderBy(c=>c.Id.Value,StringComparer.Ordinal).First().Id);
    }
    internal static bool Matches(ContractWorld w,NaturalDeath d)
    {
        if(!Enabled(w)||d.Condition is not {} id)return false;
        var c=PersonalHealth.Conditions(w,d.Person,d.At).FirstOrDefault(c=>c.Id==id);
        return c!=null&&CrisisAt(c)==d.At&&Fatal(w,c);
    }
}
