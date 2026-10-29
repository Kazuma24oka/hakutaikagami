namespace HakutaiKagami.Core.Contracts;
public static partial class NewGameMilitary
{
    public const string IssuerSuccessionContract="explicit-returned-same-garrison-case-issuer-succession-v1";
    // The first loan's immutable terms remain the provenance of the lifetime ledger.
    // A later case may select a successor; all historical calls use the original case time.
    public static EntityId CasePrincipal(ContractWorld w,long? at=null,long? revision=null)
    {
        var s=w.NewMilitary??throw new InvalidDataException("軍台帳がありません");long t=at??w.ElapsedSeconds,r=revision??w.CommitSequence;
        var q=s.Requests.LastOrDefault(q=>q.Input.Action==NewMilitaryAction.Request&&q.Input.IssuerPolicy==IssuerSuccessionContract&&Before(q.At,q.Revision,t,r));
        return q==null?s.Terms.Principal:HouseSuccession.Head(w,s.Terms.Owner,q.At,q.Revision);
    }
    static EntityId PrincipalOf(ContractWorld w,NewMilitaryState s,long? at=null,long? rev=null){var c=w.ForkMilitaryLedger();c.NewMilitary=s;return CasePrincipal(c,at,rev);}
    static EntityId CaseIssuer(ContractWorld w,EntityId? militaryCase){var q=militaryCase==null?CaseStart(w):w.NewMilitary!.Requests.Single(q=>q.Input.Id==militaryCase);return q==null?CasePrincipal(w):CasePrincipal(w,q.At,q.Revision);}
    public static bool CanAssumeReturnedGarrison(ContractWorld w)
    {
        if(!HouseSuccession.Enabled(w)||w.NewMilitary is not {} s||s.Binding==null||CasePrincipal(w)==HouseSuccession.Head(w,s.Terms.Owner)||!ClosedForSwitch(w)||PersonalLife.Resting(w,w.ElapsedSeconds,w.CommitSequence+1)||CastleAdministration.Blocked(w,s.Terms.Site,w.ElapsedSeconds,w.CommitSequence+1))return false;
        var head=HouseSuccession.Head(w,s.Terms.Owner);return CommunityStewardship.Local(w,w.PlayerControl,s.Terms.Site,w.ElapsedSeconds,w.CommitSequence+1)&&CommunityStewardship.Local(w,head,s.Terms.Site,w.ElapsedSeconds,w.CommitSequence+1)&&MemberOfGarrisonHouse(w,w.PlayerControl,s.Terms.Owner,w.ElapsedSeconds,w.CommitSequence)&&NewGameMilitaryAuthority.Resolve(w,head,head,s.Terms.Owner,s.Terms.Site)!=null;
    }
    static bool MemberOfGarrisonHouse(ContractWorld w,EntityId actor,EntityId owner,long at,long rev)=>w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult)&&w.Memberships.Any(m=>m.Subject==actor&&m.Target==owner&&m.Kind=="membership"&&m.From<=at&&(at<m.Until||at==m.Until&&rev<m.Revision));
    internal static bool SuccessorMilitaryAgreement(ContractWorld w,EntityId issuer,EntityId agent,EntityId owner,EntityId site){if(!HouseSuccession.Enabled(w)||w.NewMilitary is not {} s||agent!=w.PlayerControl||s.Terms.Owner!=owner||s.Terms.Site!=site||CasePrincipal(w)!=issuer||HouseSuccession.Head(w,owner)!=issuer||!MemberOfGarrisonHouse(w,agent,owner,w.ElapsedSeconds,w.CommitSequence))return false;return s.Requests.Any(q=>q.Input.Action==NewMilitaryAction.Request&&q.Input.IssuerPolicy==IssuerSuccessionContract&&Before(q.At,q.Revision,w.ElapsedSeconds,w.CommitSequence));}
    static bool SuccessorMilitaryCase(ContractWorld w,long at,long rev){var c=w.ForkMilitaryLedger();c.ElapsedSeconds=at;c.CommitSequence=rev;return c.NewMilitary is {} s&&SuccessorMilitaryAgreement(c,CasePrincipal(c),w.PlayerControl,s.Terms.Owner,s.Terms.Site);}
    public static string? IssuerPolicyForRequest(ContractWorld w)=>CanAssumeReturnedGarrison(w)?IssuerSuccessionContract:null;
    static void CheckIssuerSuccessionInput(ContractWorld w,NewMilitaryInput i)
    {
        if(i.IssuerPolicy==null)return;
        Need(i.IssuerPolicy==IssuerSuccessionContract&&i.Action==NewMilitaryAction.Request&&i.AuthorityPolicy==AuthorityContract&&i.LedgerPolicy==LedgerIdentityContract&&i.CycleContract==RenewalContract&&i.FromLedger==null&&CanAssumeReturnedGarrison(w),"返納済みの同じ駐屯軍について現当主と本人が現地で再確認してください");
    }
}
