using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

public static class FamilyNotice
{
    public const string Kind="family-death-notice";
    static string LetterId(NaturalDeath death,string recipient)=>InformationSchemes.Id("obituary",death.Person.Value+"/"+death.At+"/"+recipient).Value;

    static EntityId? SiteAtDeath(ContractWorld world,NaturalDeath death)
    {
        var point=RegionalCampaign.ActorPointAt(world,death.Person,death.At);
        var node=BranchRoutes.GraphNodes(world).FirstOrDefault(n=>n.Position==point);
        if(node==null)return null;
        var site=RegionalCampaign.Site(node.Id);
        return TransportWork.At(world,death.Person,site,death.At,death.Revision,true)?site:null;
    }

    static bool Witness(ContractWorld w,NaturalDeath death,EntityId person,EntityId place)=>
        person!=death.Person && !Assassination.DeadAt(w,person,death.At,death.Revision) &&
        TransportWork.At(w,person,place,death.At,death.Revision,true);

    public static bool Knows(ContractWorld w,NaturalDeath death,EntityId observer)=>
        (w.NaturalDeaths?.Contains(death)??false) && (SiteAtDeath(w,death) is {} site && Witness(w,death,observer,site) ||
        MessageFlow.Knows(w.Messaging,LetterId(death,observer.Value),observer.Value));

    static IEnumerable<MessageEnvelope> Letters(ContractWorld w,NaturalDeath death)
    {
        if(SiteAtDeath(w,death) is not {} site)yield break;
        var people=w.NewGame!.Start.People;
        var deceased=people.Single(p=>p.Id==death.Person.Value);
        var sender=people.Where(p=>Witness(w,death,new(p.Id),site)).OrderBy(p=>p.Id,StringComparer.Ordinal).FirstOrDefault();
        if(sender==null)yield break;
        foreach(var person in people.Where(p=>(deceased.BirthParents??[]).Contains(p.Id) ||
            (p.BirthParents??[]).Contains(deceased.Id) || p.Lord==deceased.Id || deceased.Lord==p.Id))
        {
            var recipient=new EntityId(person.Id);
            if(Witness(w,death,recipient,site) || Assassination.DeadAt(w,recipient,death.At,death.Revision))continue;
            string id=LetterId(death,person.Id);
            yield return new MessageEnvelope(id,death.Person.Value,Kind,sender.Id,person.Id,
                deceased.Name+"殿が亡くなられました。",person.Site,death.At);
        }
    }

    internal static void Validate(ContractWorld w)
    {
        var actual=w.Messaging.Messages.Where(m=>m.Kind==Kind).OrderBy(m=>m.Id,StringComparer.Ordinal).ToArray();
        var expected=(w.NaturalDeaths??[]).SelectMany(d=>Letters(w,d)).OrderBy(m=>m.Id,StringComparer.Ordinal);
        if(!actual.SequenceEqual(expected) || actual.Any(m=>!w.SealedMessages.Contains(new EntityId(m.Id))))
            throw new InvalidDataException("訃報の差出人・宛先・死亡原本・封書が一致しません");
    }

    internal static void Send(ContractWorld w,NaturalDeath death)
    {
        foreach(var letter in Letters(w,death))
        {
            MessageFlow.Send(w.Messaging,letter,death.At,1);
            w.SealedMessages=[..w.SealedMessages,new(letter.Id)];
        }
        RegionalCampaign.Bind(w);
    }

    internal static string? Address(ContractWorld w,MessageEnvelope message)=>message.Kind==Kind
        ? NewGameWorld.Node(w.NewGame!.Start.People.Single(p=>p.Id==message.RecipientId).Site) : null;
}
