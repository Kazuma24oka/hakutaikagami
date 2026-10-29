namespace HakutaiKagami.Core.Contracts;
public sealed record StoredMilitaryCandidate(EntityId Ledger,EntityId Owner,EntityId Site,EntityId Principal);
public static partial class NewGameMilitary
{
    // Diagnostic projection of the exact ledger owning this unit, never an index in another army.
    public static FieldPoint? ManagedUnitPosition(ContractWorld w,EntityId unit,long at){foreach(var s in LedgerStates(w)){var c=LedgerContext(w,s);int i=Array.IndexOf(UnitIdsFor(c),unit);if(i>=0)return Position(c,i,at);}return null;}
    public const string StoredSuccessionContract="explicit-closed-stored-garrison-current-head-local-reagreement-v1";
    static NewMilitaryTerms StoredTerms(ContractWorld w,EntityId request,EntityId? ledger){
        Need(ledger!=null&&ClosedForSwitch(w),"現在の軍務を返納し、選択した保存台帳を確認してください");var s=LedgerStates(w).SingleOrDefault(s=>s.Binding?.Ledger==ledger);Need(s!=null,"実在する保存済みの駐屯軍台帳が必要です");
        var c=LedgerContext(w,s);var head=HouseSuccession.Head(w,s.Terms.Owner);Need(ClosedForSwitch(c)&&CasePrincipal(c)!=head&&CommunityStewardship.Local(w,w.PlayerControl,s.Terms.Site,w.ElapsedSeconds,w.CommitSequence+1)&&CommunityStewardship.Local(w,head,s.Terms.Site,w.ElapsedSeconds,w.CommitSequence+1)&&MemberOfGarrisonHouse(w,w.PlayerControl,s.Terms.Owner,w.ElapsedSeconds,w.CommitSequence)&&!PersonalLife.Resting(w,w.ElapsedSeconds,w.CommitSequence+1)&&!CastleAdministration.Blocked(w,s.Terms.Site,w.ElapsedSeconds,w.CommitSequence+1)&&(head==w.PlayerControl||w.Characters.Single(x=>x.Id==head).WillObey),"返納済みの同じ軍について現当主と本人が現地で同意してください");
        Need(FactionStances.ResolveAuthority(w,head,s.Terms.Owner,w.ElapsedSeconds,w.CommitSequence)!=null&&w.LandRights.Any(l=>l.Subject==s.Terms.Owner&&l.Target==s.Terms.Site&&l.Kind=="control"&&l.From<=w.ElapsedSeconds&&w.ElapsedSeconds<l.Until),"その駐屯地の実権がありません");
        return new(head,s.Terms.Owner,s.Terms.Site,s.Terms.Army,request);
    }
    static bool CanRequestStored(ContractWorld w,NewMilitaryInput i){try{StoredTerms(w,i.Id,i.TargetLedger);return true;}catch(InvalidDataException){return false;}}
    public static StoredMilitaryCandidate[] StoredSuccessionCandidates(ContractWorld w){var result=new List<StoredMilitaryCandidate>();if(!HouseSuccession.Enabled(w))return [];foreach(var s in LedgerStates(w)){try{var t=StoredTerms(w,new("request:stored-succession-preview"),s.Binding?.Ledger);result.Add(new(s.Binding!.Ledger,t.Owner,t.Site,t.Principal));}catch(InvalidDataException){}}return result.ToArray();}
    public static NewMilitaryInput StoredSuccessionRequest(ContractWorld w,EntityId id,EntityId ledger)=>new(id,NewMilitaryAction.Request){IssuerPolicy=StoredSuccessionContract,TargetLedger=ledger,FromLedger=w.NewMilitary?.Binding?.Ledger==ledger?null:w.NewMilitary?.Binding?.Ledger,LedgerPolicy=LedgerIdentityContract,AuthorityPolicy=AuthorityContract,ObservationPolicy=ObservationContract,CycleContract=RenewalContract};
}
