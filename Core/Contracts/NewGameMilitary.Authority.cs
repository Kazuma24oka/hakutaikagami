namespace HakutaiKagami.Core.Contracts;
public sealed record NewMilitaryAuthorityState(string Contract,long At,long Revision,MilitaryAuthorityProof[] Proofs,MilitaryScopedGrant[] Grants);
public static partial class NewGameMilitary
{
    public const string AuthorityContract="explicit-real-issuer-source-case-unit-placement-only-mandate-v1";
    // Own starting office/claimed territory is personal starting knowledge; this is not a remote truth lookup.
    public static bool CanRequestOwnAuthority(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame!.Start.Factions.SingleOrDefault(f=>f.Head==w.PlayerControl.Value) is {} f&&w.Locations.Any(l=>l.Actor==w.PlayerControl&&l.Kind==LocationKind.AtSite&&w.NewGame.Start.Castles.Any(c=>c.Id==l.Site!.Value.Value&&c.Faction==f.Id));
    static bool OwnAuthorityCase(ContractWorld w,long at,long rev)=>w.NewMilitary is {} s&&CaseStart(w,at,rev) is {} q&&q.Input.AuthorityPolicy==AuthorityContract&&s.Terms.Principal==w.PlayerControl&&w.NewGame!.Start.Factions.Any(f=>f.Head==w.PlayerControl.Value&&f.Id==s.Terms.Owner.Value);
    static bool MilitaryBoundAt(ContractWorld w,long at,long rev)=>NewGameService.BoundAt(w,w.NewMilitary!.Terms.Principal,at,rev)||OwnAuthorityCase(w,at,rev);
    static bool MilitaryPrincipalKnown(ContractWorld w,long at,long rev)=>NewGameService.View(w,at,rev).Principal==w.NewMilitary!.Terms.Principal||OwnAuthorityCase(w,at,rev);
    public static NewMilitaryView ViewWithAuthority(ContractWorld w){var v=View(w);if(CanSwitchToCurrentHouse(w))return v with{CanRequest=true,CanAccept=false,CanPickup=false,Status="旧主家の軍務は返納済み。今の主家に新たに軍務を相談できます"};return w.NewMilitary==null&&CanRequestOwnAuthority(w)?v with{CanRequest=true,Status="自家の軍務を準備し、現地で兵と兵糧を引き受けます"}:v;}
    public static string AuthorityKnownStatus(ContractWorld w)
    {
        if(w.NewMilitary==null)return CanRequestOwnAuthority(w)?"自家の軍務を準備できます。兵の現地引受は別です。":"";
        if(!CurrentAuthorityCase(w))return "";var v=View(w);bool ended=CaseEvents(w).Any(e=>Known(w,e,w.ElapsedSeconds,w.CommitSequence)&&Read(w,e).Effect==MilitaryEffect.Returned);
        if(ended)return "指揮返還を確認。この案件の配置指揮は終了しています。";
        if(v.Returning)return "指揮返還願い発送・結果未確認。兵と兵糧の所有は主家に残ります。";
        var owner=w.Definitions.Organizations.Single(p=>p.Id==w.NewMilitary.Terms.Owner).Name;
        if(v.Custody)return "兵・兵糧の所有："+owner+"／確認した指揮：配置・待機・帰還（本隊・左右分隊）";
        if(v.CanPickup)return "軍務許可を確認・現地引受前。配置指揮はまだ引き受けていません。";
        if(v.CanAccept)return "軍務条件を確認・本人の回答前。配置指揮はまだありません。";
        return v.Status;
    }
    // A decision of the same issuer/agent is local processing, not a courier to an empty castle.
    // Only the explicit new own-head policy uses this path; unit letters and other people still travel.
    static bool HandlesLedgerInternalAuthorityMessage(ContractWorld w,string id){var s=w.NewMilitary;if(s==null||s.Terms.Principal!=w.PlayerControl||s.Authority==null)return false;var m=w.Messaging.Messages.SingleOrDefault(m=>m.Id==id);return m!=null&&m.Kind.StartsWith("new-military-",StringComparison.Ordinal)&&m.SenderId==w.PlayerControl.Value&&m.RecipientId==w.PlayerControl.Value&&s.Requests.Any(q=>q.Input.Action==NewMilitaryAction.Request&&q.Input.AuthorityPolicy==AuthorityContract&&q.Input.Id.Value==m.CorrelationId);}
    static bool AuthorityCase(ContractWorld w,EntityId militaryCase)=>w.NewMilitary?.Requests.Any(q=>q.Input.Id==militaryCase&&q.Input.Action==NewMilitaryAction.Request&&q.Input.AuthorityPolicy==AuthorityContract)==true;
    static bool CurrentAuthorityCase(ContractWorld w)=>CaseStart(w) is {} q&&AuthorityCase(w,q.Input.Id);
    static void EnableAuthority(ContractWorld w,NewMilitaryInput i){if(i.AuthorityPolicy!=AuthorityContract||w.NewMilitary!.Authority!=null)return;w.NewMilitary=w.NewMilitary with{Authority=new(AuthorityContract,w.ElapsedSeconds,w.CommitSequence,[],[])};}
    static bool IssuerAuthorityAvailable(ContractWorld w)=>!CurrentAuthorityCase(w)||NewGameMilitaryAuthority.Resolve(w,w.NewMilitary!.Terms.Principal,w.PlayerControl,w.NewMilitary.Terms.Owner,w.NewMilitary.Terms.Site)!=null;
    static void RecordAuthorityProof(ContractWorld w,MilitaryEffect effect)
    {
        if(effect is not(MilitaryEffect.Offered or MilitaryEffect.Permission)||!CurrentAuthorityCase(w))return;
        var s=w.NewMilitary!;var source=NewGameMilitaryAuthority.Resolve(w,s.Terms.Principal,w.PlayerControl,s.Terms.Owner,s.Terms.Site);Need(source!=null,"発令者の実際の役職・城の権限がありません");var x=s.Authority!;
        w.NewMilitary=s with{Authority=x with{Proofs=[..x.Proofs,new(CaseStart(w)!.Input.Id,effect,w.ElapsedSeconds,w.CommitSequence,source)]}};
    }
    static void SyncAuthority(ContractWorld w){if(w.NewMilitary?.Authority is not {} x)return;w.NewMilitary=w.NewMilitary with{Authority=x with{Grants=DeriveAuthorityScopes(w,x.Proofs)}};}
    static NewMilitaryAuthorityState? AuthorityBefore(NewMilitaryAuthorityState? x,long at,long rev)=>x==null||!Before(x.At,x.Revision,at,rev)?null:x with{Proofs=x.Proofs.Where(p=>Before(p.At,p.Revision,at,rev)).ToArray(),Grants=[]};
    static bool CurrentPlacementAuthority(ContractWorld w)=>!CurrentAuthorityCase(w)||w.NewMilitary!.Authority!.Grants.Any(g=>ScopedAuthorityAllows(g,CaseStart(w)!.Input.Id,w.PlayerControl,w.NewMilitary.Terms.Army,UnitIdsFor(w)[0],RegionalAction.Move,w.ElapsedSeconds,w.CommitSequence));
    static void ValidateAuthority(ContractWorld w,bool replay)
    {
        var s=w.NewMilitary!;var x=s.Authority;var first=s.Requests.FirstOrDefault(q=>q.Input.AuthorityPolicy==AuthorityContract);Need((x!=null)==(first!=null),"実在発令根拠の方針と台帳が一致しません");if(x==null)return;
        Need(x.Contract==AuthorityContract&&x.At==first!.At&&x.Revision==first.Revision&&x.Proofs!=null&&x.Grants!=null&&x.Proofs.All(p=>p!=null&&p.Source!=null),"実在発令根拠の版・開始・項目が不正です");
        Need(w.Messaging.Attempts.Where(a=>HandlesInternalAuthorityMessage(w,a.MessageId)).All(a=>a.Number==1&&a.DueAt==a.SentAt+1&&a.LateDueAt==null&&a.FailureAt==null&&!a.FailOnArrival&&a.Status!=HakutaiKagami.Core.Messaging.AttemptStatus.Lost),"本人内部の軍務処理を伝令の不達・遅着に置き換えています");
        var events=s.Events.Where(e=>e.Effect is MilitaryEffect.Offered or MilitaryEffect.Permission&&AuthorityCase(w,EventCase(w,e))).ToArray();
        Need(x.Proofs.Length==events.Length&&x.Proofs.Select(p=>(p.Case,p.Effect)).Distinct().Count()==x.Proofs.Length&&x.Proofs.All(p=>AuthorityCase(w,p.Case)&&events.Any(e=>e.Effect==p.Effect&&e.At==p.At&&e.Revision==p.Revision&&EventCase(w,e)==p.Case)),"実際の軍務提示・許可の根拠が欠落・重複しています");
        Need(Same(x.Grants,DeriveAuthorityScopes(w,x.Proofs)),"案件・部隊・配置操作・指揮終了の範囲が不正です");
        if(replay)Need(Same(x.Grants,BuildAuthorityScopes(w,x.Proofs)),"発令者の実在の権限を許可時点から再現できません");
    }
}
