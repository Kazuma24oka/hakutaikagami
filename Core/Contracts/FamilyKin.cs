namespace HakutaiKagami.Core.Contracts;

public static class FamilyKin
{
    public const int ComingOfAge=15;
    internal static LifeState LifeAt(NewGameState data,SetupPerson person,long at)
    {
        if(NewGameWorld.CanPlay(data.Start,person,data.AdultAge))return LifeState.Adult;
        if(data.AdultAge!=ComingOfAge || person.BirthYear is not int born)return LifeState.Minor;
        return WorldCalendar.At(data.Start.EpochDate,at).Year-born>=ComingOfAge ? LifeState.Adult : LifeState.Minor;
    }
    internal static long? NextAdulthood(ContractWorld world,long target)
    {
        if(world.NewGame is not {AdultAge:ComingOfAge} data)return null;
        var epoch=DateOnly.ParseExact(data.Start.EpochDate,"yyyy-MM-dd");
        var minors=world.Characters.Where(c=>c.Life==LifeState.Minor).Select(c=>c.Id.Value).ToHashSet();
        return FamilyBirth.People(world).Where(p=>minors.Contains(p.Id) && p.BirthYear is >=1 and <=9984)
            .Select(p=>(long)(new DateOnly(p.BirthYear!.Value+ComingOfAge,1,1).DayNumber-epoch.DayNumber)*86400)
            .Where(t=>world.ElapsedSeconds<t && t<=target).Select(t=>(long?)t).Min();
    }
    internal static void Mature(ContractWorld world)
    {
        if(world.NewGame is not {AdultAge:ComingOfAge} data || world.ElapsedSeconds%86400!=0)return;
        var date=WorldCalendar.At(data.Start.EpochDate,world.ElapsedSeconds);
        if(date.Month!=1 || date.Day!=1)return;
        var mature=FamilyBirth.People(world).Where(p=>p.BirthYear==date.Year-ComingOfAge &&
            world.Characters.Any(c=>c.Id.Value==p.Id && c.Life==LifeState.Minor)).ToArray();
        if(mature.Length==0)return;
        var ids=mature.Select(p=>p.Id).ToHashSet(StringComparer.Ordinal);
        world.Characters=world.Characters.Select(c=>ids.Contains(c.Id.Value)
            ? c with{Life=LifeState.Adult,Revision=world.CommitSequence}:c).ToArray();
        foreach(var person in mature)FamilyNotice.AnnounceMaturity(world,person);
    }
    internal static void Validate(SetupPerson[] people)
    {
        if(!people.Any(p=>p.BirthParents!=null))return;
        var persons=people.ToDictionary(p=>p.Id,StringComparer.Ordinal);
        var children=people.ToDictionary(p=>p.Id,_=>new List<string>(),StringComparer.Ordinal);
        var remaining=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var child in people)
        {
            var parents=child.BirthParents??[];
            if(parents.Length>2 || parents.Distinct(StringComparer.Ordinal).Count()!=parents.Length)
                throw new InvalidDataException("実親は重複しない二人までです");
            remaining[child.Id]=parents.Length;
            foreach(var id in parents)
            {
                if(id==null || !persons.TryGetValue(id,out var parent) || parent.Id==child.Id ||
                    parent.BirthYear is int older && child.BirthYear is int younger && older>=younger)
                    throw new InvalidDataException("実親の人物または親子の生年を確認してください");
                children[id].Add(child.Id);
            }
        }
        var ready=new Queue<string>(remaining.Where(k=>k.Value==0).Select(k=>k.Key));
        int visited=0;
        while(ready.TryDequeue(out var person))
        {
            visited++;
            foreach(var child in children[person])
                if(--remaining[child]==0)ready.Enqueue(child);
        }
        if(visited!=people.Length)throw new InvalidDataException("実親の系譜が循環しています");
    }
}
