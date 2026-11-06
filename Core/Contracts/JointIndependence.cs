namespace HakutaiKagami.Core.Contracts;

public sealed record IndependenceDeclaration(EntityId Pact,EntityId Founder,EntityId FormerHouse,EntityId House,
    EntityId Site,long At,long Revision);

public static class JointIndependence
{
    internal static IndependenceDeclaration? Declaration(ContractWorld w,SecretPact p)=>
        w.Declarations?.SingleOrDefault(d=>d.Founder==p.Proposer && d.FormerHouse==p.House && p.Revision<d.Revision);

    internal static SetupFaction Faction(ContractWorld w,IndependenceDeclaration d)=>
        new(d.House.Value,FamilyBirth.People(w).Single(p=>p.Id==d.Founder.Value).Name+"家",d.Founder.Value,d.Site.Value);

    internal static TimedRelation[] Relations(ContractWorld w,TimedRelation[] original,string kind)
    {
        if(w.Declarations==null)return original;
        var rows=original.ToList();
        foreach(var d in w.Declarations)
        {
            Join(d.Founder,d.At,d.Revision,d);
            foreach(var response in w.PactResponses??[])
            {
                if(!response.Acted)continue;
                var p=w.SecretPacts!.Single(p=>p.Id==response.Pact);
                if(p.Purpose==PactPurpose.RiseTogether && Declaration(w,p)==d)Join(p.Partner,response.At,response.Revision,d);
            }
        }
        return rows.ToArray();

        void Join(EntityId person,long at,long revision,IndependenceDeclaration declaration)
        {
            var former=kind=="membership"?declaration.FormerHouse:HouseSuccession.Head(w,declaration.FormerHouse,at,revision-1);
            for(int i=0;i<rows.Count;i++)
            {
                var row=rows[i];
                if(row.Subject==person && row.Target==former && row.From<=at && at<row.Until)
                    rows[i]=row with{Until=at,Revision=revision};
            }
            if(kind=="service" && person==declaration.Founder)return;
            var target=kind=="membership"?declaration.House:declaration.Founder;
            rows.Add(new(InformationSchemes.Id("independent-"+kind,declaration.Pact.Value+"/"+person.Value),
                person,target,kind,NewGameWorld.Basis,at,long.MaxValue,revision));
        }
    }

    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        var rows=w.Declarations??[];
        if(!rows.Take(prior?.Declarations?.Length??0).SequenceEqual(prior?.Declarations??[]) ||
            rows.DistinctBy(d=>d.House).Count()!=rows.Length)throw new InvalidDataException("独立宣言が重複・変更されています");
        foreach(var d in rows)
        {
            var p=w.SecretPacts?.SingleOrDefault(p=>p.Id==d.Pact);
            if(p==null || !p.Accepted || p.Purpose!=PactPurpose.RiseTogether || p.Proposer!=d.Founder ||
                p.House!=d.FormerHouse || p.Revision>=d.Revision || d.Revision>w.CommitSequence ||
                d.At<p.At || d.At>w.ElapsedSeconds || d.House!=InformationSchemes.Id("independent-house",p.Id.Value) ||
                DefectionPacts.HouseAt(w,d.Founder,d.At,d.Revision-1)!=d.FormerHouse ||
                PersonalCompanions.SiteAt(w,d.Founder,d.At,d.Revision-1)!=d.Site ||
                Assassination.DeadAt(w,d.Founder,d.At,d.Revision-1) || !PactSignals.Sent(w,p))
                throw new InvalidDataException("独立宣言の本人・所在・旧所属・密約が一致しません");
        }
    }
}
