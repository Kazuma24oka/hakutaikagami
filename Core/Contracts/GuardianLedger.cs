namespace HakutaiKagami.Core.Contracts;

public sealed record GuardianLedgerTransfer(long Mandate,EntityId Holder,bool Returned,long At,long Revision);

public static class GuardianLedger
{
    public static GuardianLedgerTransfer? Latest(ContractWorld w,GuardianMandate m)=>
        w.GuardianLedger?.LastOrDefault(t=>t.Mandate==m.Revision);

    public static IEnumerable<GuardianLedgerTransfer> Witnessed(ContractWorld w,EntityId observer)
    {
        foreach(var t in w.GuardianLedger??[])
        {
            var mandate=w.GuardianMandates!.Single(m=>m.Revision==t.Mandate);
            if(t.Holder==observer || !Assassination.DeadAt(w,observer,t.At,t.Revision) &&
                TransportWork.At(w,observer,mandate.Site,t.At,t.Revision,true))yield return t;
        }
    }

    internal static void Tick(ContractWorld w)
    {
        MinoritySuccession.Tick(w);
        foreach(var m in w.GuardianMandates??[])
        {
            var held=Latest(w,m);
            if(held?.Returned==true)continue;
            var paper=w.HeirNominations!.Single(p=>p.Revision==m.Nomination);
            if(held==null)
            {
                if(!MinorGuardians.MayKeepLedger(w,m))continue;
                w.GuardianLedger=[..w.GuardianLedger??[],new(m.Revision,m.Guardian,false,w.ElapsedSeconds,w.CommitSequence)];
            }
            else if(w.ElapsedSeconds>m.Until && CanReturn(w,m,paper,w.ElapsedSeconds,w.CommitSequence))
                w.GuardianLedger=[..w.GuardianLedger!,new(m.Revision,paper.Heir,true,w.ElapsedSeconds,w.CommitSequence)];
        }
    }

    static bool CanReturn(ContractWorld w,GuardianMandate m,HeirNomination p,long at,long revision)=>
        !Assassination.DeadAt(w,m.Guardian,at,revision) && !Assassination.DeadAt(w,p.Heir,at,revision) &&
        TransportWork.At(w,m.Guardian,m.Site,at,revision,true) && TransportWork.At(w,p.Heir,m.Site,at,revision,true) &&
        FamilyConsentRules.IndividualAccepts(w,m.Guardian,p.Heir,at,revision-1);

    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        MinoritySuccession.Validate(w,prior);
        var rows=w.GuardianLedger??[];
        var old=prior?.GuardianLedger??[];
        if(!rows.Take(old.Length).SequenceEqual(old))throw new InvalidDataException("後見帳簿の受渡し履歴が変わっています");
        var previous=new Dictionary<long,GuardianLedgerTransfer>();
        foreach(var t in rows)
        {
            var m=w.GuardianMandates?.SingleOrDefault(m=>m.Revision==t.Mandate);
            if(m==null || t.At<m.At || t.At>w.ElapsedSeconds || t.Revision<=m.Revision || t.Revision>w.CommitSequence)
                throw new InvalidDataException("後見の合意原本と帳簿受渡し時点が一致しません");
            var paper=w.HeirNominations!.Single(p=>p.Revision==m.Nomination);
            if(!previous.TryGetValue(t.Mandate,out var first))
            {
                var opening=Guardianship.Openings(w).SingleOrDefault(o=>o.Deceased==paper.Head && o.House==paper.House);
                if(t.Returned || t.Holder!=m.Guardian || opening==null || t.At<opening.At || t.At>=m.Until ||
                    !TransportWork.At(w,m.Guardian,m.Site,t.At,t.Revision,true) || Assassination.DeadAt(w,m.Guardian,t.At,t.Revision))
                    throw new InvalidDataException("後見人による帳簿受領が不正です");
            }
            else if(first.Returned || !t.Returned || t.Holder!=paper.Heir || t.At<=m.Until || t.Revision<=first.Revision ||
                !CanReturn(w,m,paper,t.At,t.Revision))
                throw new InvalidDataException("帳簿の現地返還に必要な所在・意思・期限が一致しません");
            previous[t.Mandate]=t;
        }
    }
}
