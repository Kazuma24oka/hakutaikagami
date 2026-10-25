using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public sealed record EngagementTaskRequest(string Contract,EntityId TargetOwner,long EffectiveFrom,long Until);
public sealed record EngagementTaskTerms(EntityId Original,EntityId Principal,EntityId Agent,EntityId Owner,
    EntityId Army,EntityId TargetOwner,EntityId[] Units,long IssuedAt,long EffectiveFrom,long Until,
    MilitaryIssuerAuthorityBasis Authority,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] FactionStanceClaim? Hostility=null);

public static partial class NewGameMilitary
{
    // Permission is conditional on independently established hostility. It neither declares war
    // nor spawns enemies. A sealed permission/cancellation affects this actor only upon opening.
    public const string EngagementTaskContract="real-issuer-scoped-conditional-engagement-personal-opening-v1";
    public static bool CanRequestEngagement(ContractWorld w)=>w.NewMilitary!=null&&CurrentAuthorityCase(w)&&
        CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)&&CurrentPlacementAuthority(w)&&
        !Returning(w,w.ElapsedSeconds,w.CommitSequence)&&
        !CaseRequests(w).Any(q=>q.Input.Action==NewMilitaryAction.RequestEngagement);
    static NewMilitaryRequest? EngagementOriginalRequest(ContractWorld w,EntityId id)=>
        CaseRequests(w).SingleOrDefault(q=>q.Input.Id==id&&q.Input.Action==NewMilitaryAction.RequestEngagement);
    public static bool CanCancelEngagement(ContractWorld w,EntityId id)=>w.NewMilitary!=null&&
        EngagementOriginalRequest(w,id)!=null&&CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)&&
        !CaseRequests(w).Any(q=>q.Input.Action==NewMilitaryAction.CancelEngagement);
    static void ValidateEngagementInput(ContractWorld w,NewMilitaryInput i)
    {
        Need((i.Action==NewMilitaryAction.RequestEngagement)==(i.Engagement!=null),"交戦任務の操作と原本条件が違います");
        Need((i.Action==NewMilitaryAction.CancelEngagement)==(i.EngagementOriginal!=null),"交戦取消の操作と原本が違います");
        if(i.Engagement is {} x)Need(x.Contract==EngagementTaskContract&&EntityId.IsValid(x.TargetOwner.Value)&&
            w.NewMilitary!=null&&x.TargetOwner!=w.NewMilitary.Terms.Owner&&
            w.Definitions.Organizations.Any(o=>o.Id==x.TargetOwner)&&x.EffectiveFrom>=w.ElapsedSeconds&&
            x.Until>x.EffectiveFrom,"対象勢力・効力・期限が不正です");
        if(i.EngagementOriginal is {} id)Need(EntityId.IsValid(id.Value)&&EngagementOriginalRequest(w,id)!=null,"同じ軍務案件の交戦原本ではありません");
    }
    static EngagementTaskTerms EngagementTermsFor(ContractWorld w,string source)
    {
        var s=w.NewMilitary!;var q=CaseRequests(w).Single(q=>q.Message==source);var x=q.Input.Engagement!;
        var authority=NewGameMilitaryAuthority.Resolve(w,s.Terms.Principal,w.PlayerControl,s.Terms.Owner,s.Terms.Site);
        Need(authority!=null,"主君の実在の発令権限がありません");
        return new(q.Input.Id,s.Terms.Principal,w.PlayerControl,s.Terms.Owner,s.Terms.Army,x.TargetOwner,
            [..UnitIdsFor(w)],w.ElapsedSeconds,Math.Max(w.ElapsedSeconds,x.EffectiveFrom),x.Until,authority,FactionStances.ClaimAt(w,s.Terms.Owner,x.TargetOwner,w.ElapsedSeconds,w.CommitSequence));
    }
    static void UpdateEngagementTasks(ContractWorld w)
    {
        var s=w.NewMilitary;if(s==null)return;
        foreach(var q in CaseRequests(w).Where(q=>q.Input.Action is NewMilitaryAction.RequestEngagement or NewMilitaryAction.CancelEngagement&&q.Message!=null&&MessageFlow.Knows(w.Messaging,q.Message,s.Terms.Principal.Value)).ToArray()){
            if(q.Input.Action==NewMilitaryAction.RequestEngagement){
                if(Has(w,MilitaryEffect.EngagementPermission)||Has(w,MilitaryEffect.EngagementRejected))continue;
                bool valid=!Has(w,MilitaryEffect.EngagementCancelled)&&CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)&&!Returning(w,w.ElapsedSeconds,w.CommitSequence)&&
                    q.Input.Engagement!.Until>w.ElapsedSeconds&&MilitaryBoundAt(w,w.ElapsedSeconds,w.CommitSequence)&&
                    NewGameMilitaryAuthority.Resolve(w,s.Terms.Principal,w.PlayerControl,s.Terms.Owner,s.Terms.Site)!=null;
                Announce(w,valid?MilitaryEffect.EngagementPermission:MilitaryEffect.EngagementRejected,q.Message!);
            }else if(!Has(w,MilitaryEffect.EngagementCancelled))Announce(w,MilitaryEffect.EngagementCancelled,q.Message!);
        }
    }
    public static EngagementMandate[] EngagementMandates(ContractWorld w,long at)
    {
        Need(at>=0&&at<=w.ElapsedSeconds,"交戦任務は確定した世界時点からのみ参照します");
        if(w.NewMilitary==null||!CustodyAt(w,at,w.CommitSequence)||!MilitaryBoundAt(w,at,w.CommitSequence))return [];
        var e=CaseEvents(w,at).SingleOrDefault(e=>e.Effect==MilitaryEffect.EngagementPermission);
        if(e==null)return [];
        var t=Read(w,e).Engagement??throw new InvalidDataException("交戦許可の原本条件がありません");
        long? received=CaseEvents(w,at).Where(n=>Read(w,n).Effect==MilitaryEffect.EngagementPermission&&Read(w,n).Engagement?.Original==t.Original).SelectMany(n=>w.NewMilitary.Openings.Where(o=>o.Message==n.Message&&o.At<=at)).OrderBy(o=>o.At).FirstOrDefault()?.At;
        long? cancelled=CaseEvents(w,at).Where(n=>Read(w,n).Effect==MilitaryEffect.EngagementCancelled).SelectMany(n=>w.NewMilitary.Openings.Where(o=>o.Message==n.Message&&o.At<=at)).OrderBy(o=>o.At).FirstOrDefault()?.At;
        return [new(t.Original,t.Principal,t.Agent,t.Owner,t.Army,t.TargetOwner,[..t.Units],t.IssuedAt,received,t.EffectiveFrom,t.Until,cancelled)];
    }
    public static string EngagementKnownStatus(ContractWorld w)
    {
        if(w.NewMilitary==null)return "";
        var known=CaseEvents(w).Where(e=>Known(w,e,w.ElapsedSeconds,w.CommitSequence)).Select(e=>Read(w,e)).ToArray();
        if(known.Any(e=>e.Effect==MilitaryEffect.EngagementCancelled))return "交戦任務の取消を開封・攻撃許可終了";
        if(known.Any(e=>e.Effect==MilitaryEffect.EngagementRejected))return "交戦任務は許可されませんでした";
        var permission=known.FirstOrDefault(e=>e.Effect==MilitaryEffect.EngagementPermission);
        if(permission!=null){var t=permission.Engagement!;return w.ElapsedSeconds>=t.Until?"交戦任務の期限切れ":w.ElapsedSeconds<t.EffectiveFrom?"交戦任務を開封・効力時刻待ち":"対象勢力への交戦任務を開封（敵対・接触は別条件）";}
        return CaseRequests(w).Any(q=>q.Input.Action==NewMilitaryAction.RequestEngagement)?"交戦任務の相談を発送・結果未確認":"";
    }
}
