namespace HakutaiKagami.Core.Contracts;
public static partial class PersonalCompanions
{
    public const string PrivateInspectionPolicy="local-companions-private20-single-inspection-paid-contact-v2";
    public static bool Supported(string? policy)=>policy is Policy or PrivateInspectionPolicy or PersonalServicePolicy or RetainerReleasePolicy;
    static bool CommissionContact(ContractWorld w,EntityId person,long at,long rev)=>SingleInspectionEnabled(w)&&person!=w.PlayerControl&&w.PrivateInformation?.Orders.Any(q=>q.Input.Action==InformationAction.Pay&&q.Input.Person==person&&(q.At<at||q.At==at&&q.Revision<rev))==true;
    static bool CanCommission(ContractWorld w,EntityId person,long at,long rev)=>CommissionContact(w,person,at,rev)&&w.Characters.Single(c=>c.Id==person) is {Life:LifeState.Adult,WillObey:true}&&!w.Offices.Any(o=>o.Subject==person&&o.From<=at&&at<o.Until)&&!NpcArmies.CommanderBusyAt(w,person,at,rev)&&SiteAt(w,person,at,rev)!=null&&!Occupied(w,person,at,rev-1);
}
