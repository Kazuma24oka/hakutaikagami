using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

public sealed record NaturalDeath(EntityId Person,long At,long Revision);

public static class FamilyMortality
{
    // 史料で生没年と病没を確認した人物だけを自然死判定へ接続する。
    static (int Born,int Died)? HistoricalLife(string id)=>id switch
    {
        "person:yoshishige"=>(1530,1587),
        _=>null
    };

    // 登録武将には史実の没年がない。固定シードと年齢から年ごとの発生と日を決める。
    internal static long? DateInYear(NewGameState data,SetupPerson person,int year)
    {
        var historical=person.Created?null:HistoricalLife(person.Id);
        if((person.BirthYear??historical?.Born) is not int born || year-born<50)return null;
        int age=year-born;
        int risk=Math.Clamp((age-49)*(age-49)/6+10,0,1000);
        var key=FormattableString.Invariant($"natural-death-v1/{data.Seed}/{person.Id}/{year}");
        var digest=SHA256.HashData(Encoding.UTF8.GetBytes(key));
        if(historical is {} life && data.Judgment?.History==HistoryMode.Historical)
        {
            if(year!=life.Died)return null;
        }
        else
        {
            if(historical is {} known && data.Judgment?.History==HistoryMode.SemiHistorical)
                risk=Math.Min(1000,risk+Math.Max(0,200-40*Math.Abs(known.Died-year)));
            if(BinaryPrimitives.ReadUInt32LittleEndian(digest)%1000>=risk)return null;
        }
        int days=DateTime.IsLeapYear(year)?366:365;
        int day=(int)(BinaryPrimitives.ReadUInt32LittleEndian(digest.AsSpan(4))%(uint)days);
        var epoch=DateOnly.ParseExact(data.Start.EpochDate,"yyyy-MM-dd",CultureInfo.InvariantCulture);
        return ((long)new DateOnly(year,1,1).DayNumber-epoch.DayNumber+day)*86400+43200;
    }

    internal static long? Next(ContractWorld w,long target)
    {
        if(w.NewGame is not {AdultAge:FamilyKin.ComingOfAge} data)return null;
        int first=WorldCalendar.At(w.EpochDate,w.ElapsedSeconds).Year,last=WorldCalendar.At(w.EpochDate,target).Year;
        long? earliest=null;
        foreach(var person in FamilyBirth.People(w).Where(p=>(p.BirthYear??HistoricalLife(p.Id)?.Born)<=last-50))
        {
            if(w.Characters.Single(c=>c.Id.Value==person.Id).Life==LifeState.Dead)continue;
            for(int year=first;year<=last;year++)
                if(DateInYear(data,person,year) is {} at && w.ElapsedSeconds<at && at<=target)
                {earliest=Math.Min(earliest??at,at);break;}
        }
        return earliest;
    }

    internal static void Apply(ContractWorld w)
    {
        if(w.NewGame is not {AdultAge:FamilyKin.ComingOfAge} data || w.ElapsedSeconds%86400!=43200)return;
        int year=WorldCalendar.At(w.EpochDate,w.ElapsedSeconds).Year;
        foreach(var person in FamilyBirth.People(w).Where(p=>DateInYear(data,p,year)==w.ElapsedSeconds))
        {
            EntityId id=new(person.Id);
            if(w.Characters.Single(c=>c.Id==id).Life==LifeState.Dead)continue;
            var death=new NaturalDeath(id,w.ElapsedSeconds,w.CommitSequence);
            w.NaturalDeaths=[..w.NaturalDeaths??[],death];
            w.Characters=w.Characters.Select(c=>c.Id==id?c with{Life=LifeState.Dead,Revision=w.CommitSequence}:c).ToArray();
            foreach(var task in w.Tasks.Where(t=>t.Executor==id && t.Execution==ExecutionPhase.Running).ToArray())
                WorldSession.Stop(w,task,"本人の死亡により行動終了");
            FamilyNotice.Send(w,death);
        }
    }

    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        var rows=w.NaturalDeaths??[];
        if(prior?.NaturalDeaths is {} old && !rows.Take(old.Length).SequenceEqual(old))
            throw new InvalidDataException("過去の自然死記録が変更されています");
        FamilyNotice.Validate(w);
        if(rows.Length==0)return;
        if(w.NewGame is not {AdultAge:FamilyKin.ComingOfAge} data || rows.DistinctBy(d=>d.Person).Count()!=rows.Length)
            throw new InvalidDataException("自然死の記録が不正です");
        foreach(var d in rows)
        {
            var p=FamilyBirth.People(w).SingleOrDefault(p=>p.Id==d.Person.Value);
            if(p==null || d.At<0 || d.At>w.ElapsedSeconds || d.Revision<=0 || d.Revision>w.CommitSequence ||
                DateInYear(data,p,WorldCalendar.At(w.EpochDate,d.At).Year)!=d.At ||
                InformationSchemes.Events(w).Any(e=>e.Input.Action==SchemeAction.FatalEncounter && e.Input.Subject==d.Person))
                throw new InvalidDataException("自然死の時刻または死因が一致しません");
        }
    }
}
