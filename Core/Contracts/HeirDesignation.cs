using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

public sealed record HeirCandidate(EntityId Person,bool Adopted,int? BirthYear);
public sealed record MarriageConnection(EntityId Original,EntityId FirstHouse,EntityId SecondHouse);

public static class HeirDesignation
{
    public static HeirCandidate[] KnownChildren(ContractWorld world,EntityId house)
    {
        var head=world.PlayerControl;
        if(world.NewGame==null || FactionStances.ResolveAuthority(world,head,house,
            world.ElapsedSeconds,world.CommitSequence)==null)return [];
        var result=new List<HeirCandidate>();
        foreach(var child in FamilyBirth.People(world))
        {
            var id=new EntityId(child.Id);
            if((child.BirthParents??[]).Contains(head.Value))
            {
                var birth=world.Births?.SingleOrDefault(b=>b.Child==id);
                if(birth!=null && !FamilyNotice.WitnessedBirth(world,birth,head) &&
                    !world.Messaging.Messages.Any(m=>m.Kind==FamilyNotice.BirthKind &&
                        m.CorrelationId==child.Id && MessageFlow.Knows(world.Messaging,m.Id,head.Value)))continue;
                result.Add(new(id,false,child.BirthYear));
            }
        }
        var entries=world.Adoption?.Entries??[];
        foreach(var entry in entries.Where(e=>e.Input.Action==AdoptionAction.Implement))
        {
            var terms=entries.Single(e=>e.Input.Id==entry.Input.Original).Terms!;
            if(terms.Parent!=head)continue;
            var person=FamilyBirth.People(world).Single(p=>p.Id==terms.Child.Value);
            result.Add(new(terms.Child,true,person.BirthYear));
        }
        return result.Where(c=>!(world.NaturalDeaths??[]).Any(d=>d.Person==c.Person && FamilyNotice.Knows(world,d,head)))
            .OrderBy(c=>c.BirthYear??int.MaxValue).ThenBy(c=>c.Person.Value,StringComparer.Ordinal).ToArray();
    }

    public static IEnumerable<MarriageConnection> WitnessedConnections(ContractWorld world)
    {
        var entries=world.Marriage?.Entries??[];
        foreach(var entry in entries.Where(e=>e.Input.Action==MarriageAction.Implement))
        {
            var terms=entries.Single(e=>e.Input.Id==entry.Input.Original).Terms!;
            if(terms.Houses.Length!=2 || !(terms.First==world.PlayerControl || terms.Second==world.PlayerControl ||
                terms.Houses.Any(h=>h.Representative==world.PlayerControl)))continue;
            yield return new(terms.Id,terms.Houses[0].Owner,terms.Houses[1].Owner);
        }
    }
}
