using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

public sealed record HeirNomination(EntityId Head,EntityId House,EntityId Heir,long At,long Revision);
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

    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        HeirPledges.Validate(w);
        var papers=w.HeirNominations??[];
        var existing=prior?.HeirNominations??[];
        if(!papers.Take(existing.Length).SequenceEqual(existing))throw new InvalidDataException("既存の後継指名状が変更されています");
        long previousRevision=0,previousTime=0;
        foreach(var paper in papers)
        {
            if(paper.Revision<=previousRevision || paper.Revision>w.CommitSequence ||
                paper.At<previousTime || paper.At>w.ElapsedSeconds || w.NewGame==null ||
                Assassination.DeadAt(w,paper.Head,paper.At,paper.Revision) ||
                FactionStances.ResolveAuthority(w,paper.Head,paper.House,paper.At,paper.Revision)==null ||
                !FamilyConsentRules.ParentLinks(w,paper.At,paper.Revision).Contains((paper.Head,paper.Heir)))
                throw new InvalidDataException("後継指名の時点・当主の権限・親子関係が一致しません");
            previousRevision=paper.Revision;
            previousTime=paper.At;
        }
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

public sealed partial class WorldSession
{
    public CommandResult DesignateHeir(EntityId house,EntityId heir,long shownRevision)
    {
        lock(gate)
        {
            var actor=state.PlayerControl;
            var existing=state.HeirNominations?.LastOrDefault(p=>p.House==house && p.Head==actor);
            if(existing?.Heir==heir && existing.Revision==shownRevision+1)
                return new(ResultCode.AlreadyAccepted,heir,state.CommitSequence,"この指名は記録済みです");
            if(shownRevision!=state.CommitSequence)
                return new(ResultCode.StateChanged,heir,state.CommitSequence,"候補を再確認してください");
            if(existing?.Heir==heir || !HeirDesignation.KnownChildren(state,house).Any(c=>c.Person==heir) ||
                Assassination.DeadAt(state,actor,state.ElapsedSeconds,shownRevision))
                return new(ResultCode.NotAvailable,heir,state.CommitSequence,"この候補を新たに指名できません");
            Change(w=>w.HeirNominations=[..w.HeirNominations??[],new(actor,house,heir,w.ElapsedSeconds,w.CommitSequence)]);
            return new(ResultCode.Accepted,heir,state.CommitSequence,"後継の指名状を記しました");
        }
    }
}
