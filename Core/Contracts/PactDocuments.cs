namespace HakutaiKagami.Core.Contracts;

public sealed record PactSeizure(EntityId Document,EntityId Pact,EntityId From,EntityId Observer,
    EntityId Capture,EntityId Site,long At,long Revision);

public static class PactDocuments
{
    const string Kind="secret-pact";

    static IEnumerable<(PersonalObject Paper,EntityId Pact,PactSeizure[] Seizures)> Trace(ContractWorld w,long revision)
    {using var _ccRev=ControlContext.At(revision);
        if(w.SecretPacts==null)yield break;
        var captures=InformationSchemes.Events(w).Where(e=>e.Input.Action==SchemeAction.CaughtInEncounter &&
            e.Revision<=revision).OrderBy(e=>e.Revision).ToArray();
        foreach(var pact in w.SecretPacts.Where(p=>p.Accepted && p.Revision<=revision))
        foreach(var signer in new[]{pact.Proposer,pact.Partner})
        {
            var id=InformationSchemes.Id(Kind,pact.Id.Value+"/"+signer.Value);
            var holder=signer;
            var site=pact.Site;
            var seizures=new List<PactSeizure>();
            foreach(var capture in captures)
            {
                if(capture.Revision<=pact.Revision || capture.Actor!=holder)continue;
                var next=capture.Input.Subject;
                seizures.Add(new(id,pact.Id,holder,next,capture.Input.Id,capture.Site,capture.At,capture.Revision));
                holder=next;
                site=capture.Site;
            }
            yield return (new(id,"署名した密約の控え",Kind,1,signer,holder,site,null,false,
                ["署名時に本人が保管",..seizures.Select(s=>"捕縛による押収 "+s.Capture.Value)]),pact.Id,seizures.ToArray());
        }
    }

    internal static IEnumerable<PersonalObject> Papers(ContractWorld w,long revision)=>
        Trace(w,revision).Select(t=>t.Paper);

    public static IEnumerable<PactSeizure> Evidence(ContractWorld w,EntityId observer)=>
        Trace(w,w.CommitSequence).SelectMany(t=>t.Seizures).Where(s=>s.Observer==observer);
}
