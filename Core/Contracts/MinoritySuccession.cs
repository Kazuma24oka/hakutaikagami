namespace HakutaiKagami.Core.Contracts;

public sealed record MinorityAccession(EntityId House,EntityId Previous,EntityId Child,EntityId Site,long Mandate,long At,long Revision);

public static class MinoritySuccession
{
    static EntityId Key(string kind,MinorityAccession row)=>InformationSchemes.Id(kind,row.House.Value+"/"+row.Child.Value+"/"+row.Mandate);
    internal static EntityId Office(MinorityAccession row)=>Key("minority-office",row);

    internal static void AddMemberships(ContractWorld w,List<TimedRelation> rows)
    {
        foreach(var a in w.MinorityAccessions??[])
            if(!rows.Any(m=>m.Subject==a.Child && m.Target==a.House && m.From<=a.At && a.At<m.Until))
                rows.Add(new(Key("minority-membership",a),a.Child,a.House,"membership",NewGameWorld.Basis,a.At,long.MaxValue,a.Revision));
    }

    internal static void Tick(ContractWorld w)
    {
        foreach(var m in w.GuardianMandates??[])
        {
            if((w.MinorityAccessions??[]).Any(a=>a.Mandate==m.Revision) || !MinorGuardians.MayKeepLedger(w,m))continue;
            var p=w.HeirNominations!.Single(p=>p.Revision==m.Nomination);
            if(HouseSuccession.Head(w,p.House)!=p.Head || SocialWorld.Site(w,p.Heir)!=m.Site ||
                w.Memberships.Any(r=>r.Subject==p.Heir && r.Target!=p.House && r.From<=w.ElapsedSeconds && w.ElapsedSeconds<r.Until) ||
                (w.HouseClaims??[]).Any(c=>c.House==p.House && c.Deceased==p.Head && c.Claimant!=p.Heir))continue;
            w.MinorityAccessions=[..w.MinorityAccessions??[],new(p.House,p.Head,p.Heir,m.Site,m.Revision,w.ElapsedSeconds,w.CommitSequence)];
        }
    }

    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        var rows=w.MinorityAccessions??[];var old=prior?.MinorityAccessions??[];
        if(!rows.Take(old.Length).SequenceEqual(old) || rows.DistinctBy(a=>(a.House,a.Previous)).Count()!=rows.Length)
            throw new InvalidDataException("未成年の家督承継が重複または改変されています");
        foreach(var a in rows)
        {
            var m=w.GuardianMandates?.SingleOrDefault(m=>m.Revision==a.Mandate);
            var p=w.HeirNominations?.SingleOrDefault(p=>p.Revision==m?.Nomination);
            if(m==null || p==null || a.Site!=m.Site || a.Child!=p.Heir || a.Previous!=p.Head || a.House!=p.House ||
                a.At<m.At || a.At>=m.Until || a.At>w.ElapsedSeconds || a.Revision<=m.Revision || a.Revision>w.CommitSequence ||
                HouseSuccession.Head(w,a.House,a.At,a.Revision-1)!=a.Previous ||
                !Assassination.DeadAt(w,a.Previous,a.At,a.Revision) || Assassination.DeadAt(w,a.Child,a.At,a.Revision) ||
                !TransportWork.At(w,a.Child,a.Site,a.At,a.Revision,true) || !TransportWork.At(w,m.Guardian,a.Site,a.At,a.Revision,true))
                throw new InvalidDataException("未成年の家督承継の所在地・旧当主・後見原本が一致しません");
        }
    }
}
