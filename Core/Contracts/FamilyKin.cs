namespace HakutaiKagami.Core.Contracts;

public static class FamilyKin
{
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
