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
        var people=FamilyBirth.People(w).Where(p=>!(w.Births??[]).Any(b=>b.Child.Value==p.Id && (b.At>death.At || b.At==death.At && b.Revision>death.Revision))).ToArray();
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

    public static bool WitnessedBirth(ContractWorld w,FamilyBirthRecord birth,EntityId observer)=>
        (birth.FirstParent==observer || birth.SecondParent==observer) &&
        TransportWork.At(w,observer,birth.Site,birth.At,birth.Revision,true);

    public const string BirthKind="family-birth-notice";

    internal static void AnnounceBirth(ContractWorld w,FamilyBirthRecord birth)
    {
        var child=FamilyBirth.Person(w,birth);
        var people=FamilyBirth.People(w).Where(p=>p.Id!=child.Id &&
            !Assassination.DeadAt(w,new(p.Id),birth.At,birth.Revision)).ToArray();
        var parents=people.Where(p=>child.BirthParents!.Contains(p.Id)).ToArray();
        bool Present(SetupPerson p)=>TransportWork.At(w,new(p.Id),birth.Site,birth.At,birth.Revision,true);
        var witness=people.Where(p=>Present(p) && w.Characters.Any(c=>c.Id.Value==p.Id && c.Life==LifeState.Adult))
            .OrderByDescending(p=>parents.Contains(p)).ThenBy(p=>p.Id,StringComparer.Ordinal).FirstOrDefault();
        if(witness==null)return;
        foreach(var relative in people.Where(p=>!Present(p) &&
            (parents.Contains(p) || parents.Any(parent=>(parent.BirthParents??[]).Contains(p.Id)))))
        {
            var id=InformationSchemes.Id("family-birth",child.Id+"/"+relative.Id);
            var letter=new MessageEnvelope(id.Value,child.Id,BirthKind,witness.Id,relative.Id,
                child.Name+"が生まれました。",relative.Site,birth.At);
            MessageFlow.Send(w.Messaging,letter,birth.At,1);
            w.SealedMessages=[..w.SealedMessages,id];
        }
        RegionalCampaign.Bind(w);
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

    public const string MaturityKind="family-coming-of-age";
    public static bool WitnessedMaturity(ContractWorld w,SetupPerson person,EntityId observer)
    {
        if(w.NewGame is not {AdultAge:FamilyKin.ComingOfAge} data || person.BirthYear is not int born ||
            born>9984 || NewGameWorld.CanPlay(data.Start,person,data.AdultAge))return false;
        long at=(long)(new DateOnly(born+FamilyKin.ComingOfAge,1,1).DayNumber-
            DateOnly.ParseExact(data.Start.EpochDate,"yyyy-MM-dd").DayNumber)*86400;
        if(at<0 || at>w.ElapsedSeconds || Assassination.DeadAt(w,new(person.Id),at,0) ||
           Assassination.DeadAt(w,observer,at,0))return false;
        if(person.Id==observer.Value)return true;
        var point=RegionalCampaign.ActorPointAt(w,new(person.Id),at);
        var node=BranchRoutes.GraphNodes(w).FirstOrDefault(n=>n.Position==point);
        if(node==null)return false;
        var site=RegionalCampaign.Site(node.Id);
        return TransportWork.At(w,new(person.Id),site,at,0,true) && TransportWork.At(w,observer,site,at,0,true);
    }

    internal static void AnnounceMaturity(ContractWorld w,SetupPerson child)
    {
        var actor=new EntityId(child.Id);
        var site=SocialWorld.Site(w,actor);
        foreach(var parentId in child.BirthParents??[])
        {
            var parent=new EntityId(parentId);
            if(Assassination.DeadAt(w,parent,w.ElapsedSeconds,w.CommitSequence) ||
                site is {} local && TransportWork.At(w,parent,local,w.ElapsedSeconds,w.CommitSequence,true))continue;
            string id=InformationSchemes.Id("coming-of-age",child.Id+"/"+parentId).Value;
            var address=FamilyBirth.People(w).Single(p=>p.Id==parentId).Site;
            var letter=new MessageEnvelope(id,child.Id,MaturityKind,child.Id,parentId,
                child.Name+"より、元服のご報告を申し上げます。",address,w.ElapsedSeconds);
            MessageFlow.Send(w.Messaging,letter,w.ElapsedSeconds,1);
            w.SealedMessages=[..w.SealedMessages,new(id)];
        }
        RegionalCampaign.Bind(w);
    }

    internal static string? Address(ContractWorld w,MessageEnvelope message)=>message.Kind is Kind or MaturityKind or BirthKind or HeirPledges.RequestKind or HeirPledges.ReplyKind or PactSignals.Call or PactSignals.Reply or HouseScaleNotices.Kind or HouseReforms.Proposal or HouseReforms.Answer or HouseReforms.TransferOrder
        ? NewGameWorld.Node(FamilyBirth.People(w).Single(p=>p.Id==message.RecipientId).Site) : null;
}
