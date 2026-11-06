namespace HakutaiKagami.Core.Contracts;

public sealed record IndependenceDeclaration(EntityId Pact,EntityId Founder,EntityId FormerHouse,EntityId House,
    EntityId Site,long At,long Revision)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? CheckGarrison {get;init;}
}

public static class JointIndependence
{
    internal static IndependenceDeclaration? Declaration(ContractWorld w,SecretPact p)=>
        w.Declarations?.SingleOrDefault(d=>d.Founder==p.Proposer && d.FormerHouse==p.House && p.Revision<d.Revision);

    public sealed record LocalGuard(EntityId Castle,EntityId FormerHouse,bool FormerTroopsPresent,bool ControlsCastle);
    public static LocalGuard? ObserveOwnCastle(ContractWorld w,EntityId observer)
    {
        var declaration=w.Declarations?.LastOrDefault(d=>d.Founder==observer && d.CheckGarrison==true);
        if(declaration==null || IndependenceGate.MeetingSite(w,observer)!=declaration.Site ||
            Assassination.DeadAt(w,observer,w.ElapsedSeconds,w.CommitSequence))return null;
        var site=declaration.Site;
        bool formerTroops=CastleHandover.HasGarrison(w,declaration.FormerHouse,site,w.ElapsedSeconds,w.CommitSequence+1);
        bool controls=w.LandRights.Any(r=>r.Subject==declaration.House && r.Target==site &&
            r.Kind=="control" && r.From<=w.ElapsedSeconds && w.ElapsedSeconds<r.Until);
        return new(site,declaration.FormerHouse,formerTroops,controls);
    }

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

    internal static bool CommandedCastle(ContractWorld w,EntityId person,EntityId site,long at,long rev)
    {
        var latest=w.Castellan?.Entries.LastOrDefault(e=>e.Input.Site==site && e.Revision<rev &&
            e.Input.Action is CastellanAction.Appoint or CastellanAction.Reauthorize or CastellanAction.Release);
        return latest!=null ? CastleAdministration.Grant(w,site,at,rev)?.Agent==person :
            w.Offices.Any(o=>o.Subject==person && o.Target==site && o.Kind=="castellan" && o.From<=at && at<o.Until);
    }
    internal static TimedRelation[] CastleControl(ContractWorld w,TimedRelation[] original)
    {
        if(w.Declarations==null)return original;
        var rows=original.ToList();
        foreach(var d in w.Declarations)
        {
            Transfer(d,d.Founder,d.Site,d.At,d.Revision);
            var receipt=HouseholdCourt.Events(w).SingleOrDefault(e=>e.Input.Action==CourtAction.ReceiveIndependentKeep && e.Input.Reference==d.Pact);
            if(receipt!=null)Transfer(d,d.Founder,d.Site,receipt.At,receipt.Revision,true);
            foreach(var response in w.PactResponses??[])
            {
                if(!response.Acted)continue;
                var p=w.SecretPacts!.Single(p=>p.Id==response.Pact);
                if(p.Purpose==PactPurpose.RiseTogether && Declaration(w,p)==d &&
                    PersonalCompanions.SiteAt(w,p.Partner,response.At,response.Revision-1) is {} site)
                    Transfer(d,p.Partner,site,response.At,response.Revision);
            }
        }
        return rows.ToArray();

        void Transfer(IndependenceDeclaration d,EntityId person,EntityId site,long at,long rev,bool delivered=false)
        {
            var index=rows.FindIndex(row=>row.Subject==d.FormerHouse && row.Target==site && row.Kind=="control" &&
                row.From<=at && at<row.Until);
            if(index<0 || CastleAdministration.Blocked(w,site,at,rev))return;
            if(!CommandedCastle(w,person,site,at,rev) && !delivered)return;
            if(d.CheckGarrison==true && CastleHandover.HasGarrison(w,d.FormerHouse,site,at,rev))return;
            var former=rows[index];
            rows[index]=former with{Until=at,Revision=rev};
            rows.Add(former with{Id=InformationSchemes.Id("castle-control",d.Pact.Value+"/"+site.Value),
                Subject=d.House,From=at,Revision=rev});
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
            if(d.CheckGarrison==false || p==null || !p.Accepted || p.Purpose!=PactPurpose.RiseTogether || p.Proposer!=d.Founder ||
                p.House!=d.FormerHouse || p.Revision>=d.Revision || d.Revision>w.CommitSequence ||
                d.At<p.At || d.At>w.ElapsedSeconds || d.House!=InformationSchemes.Id("independent-house",p.Id.Value) ||
                DefectionPacts.HouseAt(w,d.Founder,d.At,d.Revision-1)!=d.FormerHouse ||
                PersonalCompanions.SiteAt(w,d.Founder,d.At,d.Revision-1)!=d.Site ||
                Assassination.DeadAt(w,d.Founder,d.At,d.Revision-1) || !PactSignals.Sent(w,p))
                throw new InvalidDataException("独立宣言の本人・所在・旧所属・密約が一致しません");
        }
    }
}
