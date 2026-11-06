namespace HakutaiKagami.Core.Contracts;

public enum PactPurpose { LeaveHouse, WithholdService, RiseTogether }
public sealed record SecretPact(EntityId Id,EntityId Proposer,EntityId Partner,EntityId House,EntityId Site,
    PactPurpose Purpose,bool Accepted,long At,long Revision);

public static class DefectionPacts
{
    public static string Name(PactPurpose purpose)=>purpose switch {
        PactPurpose.LeaveHouse=>"主家を離れる",PactPurpose.WithholdService=>"新たな出撃を見合わせる",
        PactPurpose.RiseTogether=>"共に独立する",_=>throw new ArgumentOutOfRangeException(nameof(purpose))};

    internal static EntityId? HouseAt(ContractWorld w,EntityId person,long at,long rev)=>
        w.Memberships.SingleOrDefault(m=>m.Subject==person && m.From<=at && (m.From<at || m.Until!=long.MaxValue || m.Revision<=rev) &&
            (at<m.Until || at==m.Until && rev<m.Revision))?.Target;

    internal static SecretPact? Negotiate(ContractWorld w,EntityId id,EntityId actor,EntityId partner,PactPurpose purpose,long at,long rev)
    {
        if(!CharacterRelations.Enabled(w) || !Enum.IsDefined(purpose) || actor==partner ||
            Assassination.DeadAt(w,actor,at,rev) || Assassination.DeadAt(w,partner,at,rev))return null;
        var site=PersonalCompanions.SiteAt(w,actor,at,rev);
        var house=HouseAt(w,partner,at,rev);
        if(site==null || house==null || PersonalCompanions.SiteAt(w,partner,at,rev)!=site ||
            HouseSuccession.Head(w,house.Value,at,rev)==partner ||
            purpose==PactPurpose.RiseTogether && (HouseAt(w,actor,at,rev)!=house || HouseSuccession.Head(w,house.Value,at,rev)==actor))return null;
        var people=FamilyBirth.People(w).Where(p=>p.Id==actor.Value || p.Id==partner.Value).ToArray();
        if(people.Length!=2 || people.Any(p=>FamilyKin.LifeAt(w.NewGame!,p,at)!=LifeState.Adult))return null;
        var head=HouseSuccession.Head(w,house.Value,at,rev);
        var loyalty=CharacterRelations.DiagnosticScores(w,partner,head,at,rev);
        var relation=CharacterRelations.DiagnosticScores(w,partner,actor,at,rev);
        int interest=CharacterRelations.DiagnosticTrait(w,partner,SocialTrait.Ambition)+relation.Trust+loyalty.Hostility;
        int reluctance=CharacterRelations.DiagnosticTrait(w,partner,SocialTrait.Duty)+loyalty.Trust+relation.Hostility;
        if(purpose==PactPurpose.WithholdService)reluctance-=10;
        return new(id,actor,partner,house.Value,site.Value,purpose,interest>=reluctance,at,rev+1);
    }

    public static IEnumerable<SecretPact> Known(ContractWorld w,EntityId observer)
    {
        var observed=PactDocuments.Evidence(w,observer).Select(e=>e.Pact).ToHashSet();
        return (w.SecretPacts??[]).Where(p=>p.Proposer==observer || p.Partner==observer || observed.Contains(p.Id));
    }

    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        PactSignals.Validate(w,prior);
        JointIndependence.Validate(w,prior);
        var rows=w.SecretPacts??[];
        var old=prior?.SecretPacts??[];
        if(!rows.Take(old.Length).SequenceEqual(old) || rows.DistinctBy(p=>p.Id).Count()!=rows.Length)
            throw new InvalidDataException("調略の相談原本が重複または変更されています");
        long last=0;
        foreach(var p in rows)
        {
            if(!EntityId.IsValid(p.Id.Value) || p.At<0 || p.At>w.ElapsedSeconds || p.Revision<=last ||
                p.Revision>w.CommitSequence || Negotiate(w,p.Id,p.Proposer,p.Partner,p.Purpose,p.At,p.Revision-1)!=p)
                throw new InvalidDataException("調略の相談相手・所在・条件・返答が一致しません");
            last=p.Revision;
        }
    }

    internal static SecretPact? Prepare(ContractWorld w,EntityId id,EntityId partner,PactPurpose purpose)=>
        Negotiate(w,id,w.PlayerControl,partner,purpose,w.ElapsedSeconds,w.CommitSequence);
}

public sealed partial class WorldSession
{
    public CommandResult NegotiatePact(EntityId id,EntityId partner,PactPurpose purpose,long shown)
    {
        lock(gate)
        {
            var existing=state.SecretPacts?.SingleOrDefault(p=>p.Id==id);
            if(existing!=null)return new(existing.Proposer==state.PlayerControl && existing.Partner==partner && existing.Purpose==purpose
                ?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,id,state.CommitSequence,"記録済みの相談です");
            if(shown!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"現地の状況を確認してください");
            if(!EntityId.IsValid(id.Value) || DefectionPacts.Prepare(state,id,partner,purpose) is not {} pact)
                return new(ResultCode.NotAvailable,id,shown,"現地で会える成人と主家に合う条件を選んでください");
            if((state.SecretPacts??[]).Any(p=>p.Proposer==pact.Proposer && p.Partner==partner && p.House==pact.House &&
                p.Purpose==purpose && (p.Accepted || p.At==pact.At)))
                return new(ResultCode.NotAvailable,id,shown,"同じ条件での相談が記録されています");
            Change(w=>w.SecretPacts=[..w.SecretPacts??[],pact]);
            return new(ResultCode.Accepted,id,state.CommitSequence,pact.Accepted?"相手は条件を受け入れました":"相手は申し出を断りました");
        }
    }
}
