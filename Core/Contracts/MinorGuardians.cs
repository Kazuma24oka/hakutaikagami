namespace HakutaiKagami.Core.Contracts;

public sealed record GuardianMandate(long Nomination,EntityId Guardian,EntityId Site,string Duty,long Until,long At,long Revision);

public static class MinorGuardians
{
    public const string KeepLedger="house-ledger-custody";

    static long? End(ContractWorld w,EntityId child)
    {
        var data=w.NewGame!;
        var person=FamilyBirth.People(w).Single(p=>p.Id==child.Value);
        if(data.AdultAge!=FamilyKin.ComingOfAge || person.BirthYear is not (>=1 and <=9984))return null;
        return (long)(new DateOnly(person.BirthYear.Value+FamilyKin.ComingOfAge,1,1).DayNumber-
            DateOnly.ParseExact(w.EpochDate,"yyyy-MM-dd").DayNumber)*86400;
    }

    internal static GuardianMandate? Agree(ContractWorld w,HeirNomination paper,EntityId guardian,long at,long revision)
    {
        if(!CharacterRelations.Enabled(w) || guardian==paper.Heir || guardian==paper.Head ||
            FactionStances.ResolveAuthority(w,paper.Head,paper.House,at,revision)==null ||
            End(w,paper.Heir) is not {} until || until<=at ||
            Assassination.DeadAt(w,paper.Heir,at,revision) || Assassination.DeadAt(w,guardian,at,revision) ||
            !w.Memberships.Any(m=>m.Subject==guardian && m.Target==paper.House && m.From<=at && at<m.Until))return null;
        var person=FamilyBirth.People(w).Single(p=>p.Id==guardian.Value);
        if(FamilyKin.LifeAt(w.NewGame!,person,at)!=LifeState.Adult)return null;
        var site=new EntityId(w.NewGame!.Start.Factions.Single(f=>f.Id==paper.House.Value).Home);
        if(!TransportWork.At(w,paper.Head,site,at,revision,true) || !TransportWork.At(w,guardian,site,at,revision,true) ||
            !FamilyConsentRules.IndividualAccepts(w,guardian,paper.Head,at,revision))return null;
        return new(paper.Revision,guardian,site,KeepLedger,until,at,revision+1);
    }

    public static bool MayKeepLedger(ContractWorld w,GuardianMandate m)
    {
        if(!(w.GuardianMandates??[]).Contains(m) || w.ElapsedSeconds>=m.Until || w.ElapsedSeconds<m.At)return false;
        var paper=w.HeirNominations!.Single(p=>p.Revision==m.Nomination);
        var opening=Guardianship.Known(w,m.Guardian).SingleOrDefault(c=>c.Opening.House==paper.House && c.Opening.Deceased==paper.Head);
        return opening!=null && w.HeirNominations!.Last(p=>p.House==paper.House && p.Revision<opening.Opening.Revision)==paper &&
            !Assassination.DeadAt(w,paper.Heir,w.ElapsedSeconds,w.CommitSequence) &&
            !Assassination.DeadAt(w,m.Guardian,w.ElapsedSeconds,w.CommitSequence) &&
            SocialWorld.Site(w,m.Guardian)==m.Site;
    }

    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        var rows=w.GuardianMandates??[];
        var old=prior?.GuardianMandates??[];
        if(!rows.Take(old.Length).SequenceEqual(old) || rows.DistinctBy(m=>m.Nomination).Count()!=rows.Length)
            throw new InvalidDataException("後見原本の変更または重複です");
        GuardianLedger.Validate(w,prior);
        long previous=0;
        foreach(var m in rows)
        {
            var paper=w.HeirNominations?.LastOrDefault(p=>p.Revision<m.Revision && p.House==
                w.HeirNominations.SingleOrDefault(n=>n.Revision==m.Nomination)?.House);
            if(paper?.Revision!=m.Nomination || m.Revision<=previous || m.Revision>w.CommitSequence || m.At<paper.At ||
                m.At>w.ElapsedSeconds || Agree(w,paper,m.Guardian,m.At,m.Revision-1)!=m)
                throw new InvalidDataException("後見の範囲・期限・本人承諾と指名状が一致しません");
            previous=m.Revision;
        }
    }
}

public sealed partial class WorldSession
{
    public CommandResult AgreeGuardian(HeirNomination paper,EntityId guardian,long shown)
    {
        lock(gate)
        {
            if(state.PlayerControl!=paper.Head || state.HeirNominations?.LastOrDefault(p=>p.House==paper.House)!=paper)
                return new(ResultCode.NotAvailable,guardian,state.CommitSequence,"現当主の指名状が必要です");
            var old=state.GuardianMandates?.SingleOrDefault(m=>m.Nomination==paper.Revision);
            if(old!=null)return new(old.Guardian==guardian?ResultCode.AlreadyAccepted:ResultCode.NotAvailable,guardian,state.CommitSequence,"この指名の後見合意は記録済みです");
            if(shown!=state.CommitSequence)return new(ResultCode.StateChanged,guardian,state.CommitSequence,"表示を更新してください");
            if(MinorGuardians.Agree(state,paper,guardian,state.ElapsedSeconds,shown) is not {} mandate)
                return new(ResultCode.NotAvailable,guardian,shown,"本拠での成人家臣の承諾と未成年の候補が必要です");
            Change(w=>w.GuardianMandates=[..w.GuardianMandates??[],mandate]);
            return new(ResultCode.Accepted,guardian,state.CommitSequence,"元服までの家の帳簿保管について合意しました");
        }
    }
}
