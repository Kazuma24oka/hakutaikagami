namespace HakutaiKagami.Core.Contracts;

public sealed record MilitaryAuthorityProof(EntityId Case,MilitaryEffect Effect,long At,long Revision,MilitaryIssuerAuthorityBasis Source);
public sealed record MilitaryScopedGrant(EntityId Case,EntityId Owner,CommandMandate Mandate,long FromRevision,long? UntilRevision);
public static partial class NewGameMilitary
{
    // Pure trace builder. It does not install grants, change old saves or create troops.
    public static MilitaryScopedGrant[] BuildAuthorityScopes(ContractWorld w,MilitaryAuthorityProof[] proofs)
    {
        Need(w.NewMilitary!=null&&proofs!=null&&proofs.All(p=>p!=null&&p.Source!=null),"軍務と発令根拠が必要です");var s=w.NewMilitary!;
        Need(proofs.Select(p=>(p.Case,p.Effect)).Distinct().Count()==proofs.Length,"発令根拠が重複しています");
        foreach(var p in proofs){
            Need(p.Effect is MilitaryEffect.Offered or MilitaryEffect.Permission&&s.Events.Any(e=>e.Effect==p.Effect&&e.At==p.At&&e.Revision==p.Revision&&EventCase(w,e)==p.Case),"実際の軍務提示・許可のない発令根拠です");
            var prior=BeforeState(w,p.At,p.Revision-1);prior.CommitSequence=p.Revision;
            var expected=NewGameMilitaryAuthority.Resolve(prior,s.Terms.Principal,w.PlayerControl,s.Terms.Owner,s.Terms.Site);
            Need(expected!=null&&expected==p.Source,"許可時点の主君・所属・役職・城の権限を再現できません");
        }
        return DeriveAuthorityScopes(w,proofs);
    }
    static MilitaryScopedGrant[] DeriveAuthorityScopes(ContractWorld w,MilitaryAuthorityProof[] proofs)
    {
        var s=w.NewMilitary!;
        var grants=new List<MilitaryScopedGrant>();
        foreach(var p in proofs.Where(p=>p.Effect==MilitaryEffect.Permission)){
            var pickup=s.Events.SingleOrDefault(e=>e.Effect==MilitaryEffect.PickedUp&&EventCase(w,e)==p.Case);if(pickup==null)continue;
            var end=s.Events.SingleOrDefault(e=>e.Effect==MilitaryEffect.Returned&&EventCase(w,e)==p.Case);
            var mandate=new CommandMandate(new("mandate:new-military/"+Hash(p.Case.Value)),p.Source.Issuer,p.Source.Agent,s.Terms.Army,[..UnitIds],[RegionalAction.Move,RegionalAction.Hold,RegionalAction.Retreat],NewGameWorld.Basis,pickup.At,end?.At??long.MaxValue);
            grants.Add(new(p.Case,p.Source.Owner,mandate,pickup.Revision,end?.Revision));
        }
        return grants.OrderBy(g=>g.Mandate.From).ThenBy(g=>g.FromRevision).ToArray();
    }
    public static bool ScopedAuthorityAllows(MilitaryScopedGrant grant,EntityId militaryCase,EntityId actor,EntityId army,EntityId unit,RegionalAction action,long at,long revision)=>grant.Case==militaryCase&&grant.Mandate.Agent==actor&&grant.Mandate.Army==army&&grant.Mandate.Units.Contains(unit)&&grant.Mandate.Actions.Contains(action)&&(grant.Mandate.From<at||grant.Mandate.From==at&&grant.FromRevision<=revision)&&(at<grant.Mandate.Until||at==grant.Mandate.Until&&grant.UntilRevision is {} end&&revision<end);
}
