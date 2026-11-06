namespace HakutaiKagami.Core.Contracts;

public sealed record PersonSighting(EntityId Original,EntityId Person,EntityId Site,long ObservedAt);

public static class Assassination
{
    public static PersonSighting[] Sightings(ContractWorld world,EntityId observer) =>
        InformationSchemes.Events(world)
            .Where(e=>e.Actor==observer && e.Input.Action==SchemeAction.ObservePerson)
            .Select(e=>new PersonSighting(e.Input.Id,e.Input.Subject,e.Site,e.At)).ToArray();

    public static bool DeadAt(ContractWorld w,EntityId person,long at,long rev) =>
        InformationSchemes.Events(w).Any(e=>e.Input.Action==SchemeAction.FatalEncounter && e.Input.Subject==person &&
            (e.At<at || e.At==at && e.Revision<=rev));

    internal static SchemeAction Clash(ContractWorld world,SchemeInput input,EntityId actor,EntityId site,long at,long rev)
    {
        var contact=InformationSchemes.Events(world).SingleOrDefault(e=>e.Input.Id==input.Reference &&
            e.Input.Action==SchemeAction.SeekContact && e.Actor==actor && e.Input.Subject==input.Subject && e.Site==site && e.Revision<rev);
        if(contact==null || input.Person!=actor || input.Destination!=null || input.Topic!=SchemeTopic.ForeignContact || DeadAt(world,actor,at,rev-1) ||
            DeadAt(world,input.Subject,at,rev-1) || !TransportWork.At(world,input.Subject,site,at,rev,true) ||
            !TransportWork.At(world,actor,site,at,rev,true))
            throw new InvalidDataException("接触した対象を現地で再確認してください");
        if(InformationSchemes.Events(world).Any(e=>IsConclusion(e.Input.Action)&&e.Actor==actor&&e.Input.Subject==input.Subject&&e.At/86400==at/86400&&e.Revision<rev))
            throw new InvalidDataException("今日は同じ対象との接触戦を終えています");
        var start=world.NewGame!.Start;
        var attacker=start.People.Single(p=>p.Id==actor.Value);
        var target=start.People.Single(p=>p.Id==input.Subject.Value);
        int guards=LocalSecurity.GuardsAt(world,site,at,rev);
        bool headquarters=HouseSuccession.Factions(world,at,rev-1).Any(f=>f.Head==target.Id && f.Home==site.Value);
        int odds=Math.Clamp(42+((attacker.Attributes?.Valor??50)-(target.Attributes?.Valor??50))/2-10*guards-(headquarters?40:0),2,85);
        var key=FormattableString.Invariant($"{world.NewGame.Seed}/{actor.Value}/{target.Id}/{site.Value}/{at/86400}/encounter");
        var draw=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return draw[0]*100/256<odds?SchemeAction.FatalEncounter:draw[1]*100/256<15+Math.Min(50,guards*10)?SchemeAction.CaughtInEncounter:SchemeAction.EscapedEncounter;
    }

    public static bool IsConclusion(SchemeAction action)=>action is SchemeAction.FatalEncounter or SchemeAction.CaughtInEncounter or SchemeAction.EscapedEncounter;

    internal static void Execute(ContractWorld world,SchemeInput input,EntityId actor,EntityId site)
    {
        var outcome=Clash(world,input,actor,site,world.ElapsedSeconds,world.CommitSequence);
        InformationSchemes.Add(world,input,actor,site);
        var result=input with {Id=InformationSchemes.Id("encounter",input.Id.Value),Action=outcome,Reference=input.Id};
        InformationSchemes.Add(world,result,actor,site);
        if(outcome==SchemeAction.FatalEncounter)
            world.Characters=world.Characters.Select(c=>c.Id==input.Subject?c with{Life=LifeState.Dead,Revision=world.CommitSequence}:c).ToArray();
    }    internal static void CheckContact(ContractWorld world,SchemeInput input,EntityId actor,EntityId site,long at,long revision)
    {
        if (!CastleSiege.Enabled(world) || DeadAt(world,actor,at,revision-1) || DeadAt(world,input.Subject,at,revision-1) || input.Subject==actor || input.Person!=actor || input.Destination!=null ||
            input.Topic!=SchemeTopic.ForeignContact || !world.Characters.Any(c=>c.Id==input.Subject))
            throw new InvalidDataException("現地で確かめる相手を指定してください");
        if (!TransportWork.At(world,actor,site,at,revision,true))
            throw new InvalidDataException("自分が現地に到着してから行動してください");
        if (input.Action==SchemeAction.SeekContact)
        {
            var sight=InformationSchemes.Events(world).SingleOrDefault(e=>e.Input.Id==input.Reference && e.Input.Action==SchemeAction.ObservePerson &&
                e.Actor==actor && e.Input.Subject==input.Subject && e.Site==site && e.Revision<revision && e.At<=at);
            if (sight==null) throw new InvalidDataException("この場所で対象を確認した情報が必要です");
        }
        else if(input.Reference!=null)throw new InvalidDataException("直接の目撃に別の原本は指定できません");
        if (!TransportWork.At(world,input.Subject,site,at,revision,true))
            throw new InvalidDataException("現地で対象を見つけられませんでした");
    }
}