using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record CombatAuthorization(EntityId Principal,EntityId Agent,EntityId Target,EntityId Basis,long From,long Until,string Contract);
public static partial class NewGameMilitary
{
    // Explicit synthetic exercise only. This grants no authority to attack a historical house.
    public const string CombatContract="explicit-synthetic-local-contact-simultaneous-loss-morale-person-seconds-v1";
    static CombatAuthorization CombatTerms(ContractWorld w,long from)=>new(w.NewMilitary!.Terms.Principal,w.PlayerControl,new("exercise:local-contact"),w.NewMilitary.Terms.Case,from,from+CombatPermitDuration,CombatContract);
    public const int CombatDuration=30,CombatPermitDuration=120;
    public static readonly EntityId[] ExerciseIds=[new("unit:exercise/0"),new("unit:exercise/1"),new("unit:exercise/2")];
    static NewMilitaryRequest? CombatStart(ContractWorld w)=>w.NewMilitary?.Requests.SingleOrDefault(q=>q.Input.Action==NewMilitaryAction.CombatStart);
    public static bool CombatStarted(ContractWorld w)=>CombatStart(w)!=null;
    static long CombatUntil(ContractWorld w,NewMilitaryRequest start)=>Math.Min(Math.Min(Math.Min(start.At+CombatDuration,Event(w,MilitaryEffect.CombatPermission)!.At+CombatPermitDuration),w.NewMilitary!.Requests.FirstOrDefault(q=>q.Revision>start.Revision&&q.Input.Action is NewMilitaryAction.CombatEnd or NewMilitaryAction.Recall or NewMilitaryAction.Enter)?.At??long.MaxValue),w.NewMilitary.Orders.FirstOrDefault(o=>o.Revision>start.Revision&&o.Action==NewMilitaryAction.Recall)?.At??long.MaxValue);
    public static bool CombatActive(ContractWorld w)=>CombatStart(w) is {} start&&w.ElapsedSeconds<CombatUntil(w,start);
    public static bool CanStartCombat(ContractWorld w)=>w.NewMilitary is {} s&&CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)&&Deployed(w)&&!Detached(w)&&!Moving(w)&&!Returning(w,w.ElapsedSeconds,w.CommitSequence)&&CombatStart(w)==null&&Event(w,MilitaryEffect.CombatPermission) is {} permit&&(Known(w,permit,w.ElapsedSeconds,w.CommitSequence)||s.Events.Where(e=>e.Effect==MilitaryEffect.StatusSent&&Known(w,e,w.ElapsedSeconds,w.CommitSequence)).Any(e=>Read(w,e).Effect==MilitaryEffect.CombatPermission&&Read(w,e).EffectiveAt==permit.At))&&w.ElapsedSeconds<permit.At+CombatPermitDuration&&View(w).Carry>0&&Enumerable.Range(0,3).All(i=>{var p=Position(w,i,w.ElapsedSeconds);return RegionalGround.Walkable(new(p.X+3000,p.Y),atlas:true);});
    public static FieldPoint ExercisePosition(ContractWorld w,int i){var p=Position(w,i,CombatStart(w)!.At);return new(p.X+3000,p.Y);}
    public static RegionalLoss[] CombatLosses(ContractWorld w,long at)
    {
        if(CombatStart(w) is not {} start||at<=start.At)return [];
        long until=Math.Min(at,CombatUntil(w,start));var effects=new List<RegionalLoss>();int[] own=[..Strength],other=[..Strength],ownMorale=[100,100,100],otherMorale=[100,100,100];
        for(long t=start.At+1;t<=until;t++){
            var inputs=new List<ContactParticipant>();for(int i=0;i<3;i++){
                var p=Position(w,i,t);var enemy=ExercisePosition(w,i);
                inputs.Add(new(UnitIds[i],0,p,ContinuousWorld.Height(p),own[i],ownMorale[i],ownMorale[i]<=25?"routed":"standing"));
                inputs.Add(new(ExerciseIds[i],1,enemy,ContinuousWorld.Height(enemy),other[i],otherMorale[i],otherMorale[i]<=25?"routed":"standing"));
            }
            var hits=ContactCombat.Resolve(t,true,inputs);effects.AddRange(hits);
            foreach(var h in hits){int i=Array.IndexOf(UnitIds,h.Unit);if(i>=0){own[i]-=h.Soldiers;ownMorale[i]-=h.Morale;}else{i=Array.IndexOf(ExerciseIds,h.Unit);other[i]-=h.Soldiers;otherMorale[i]-=h.Morale;}}
        }
        return effects.ToArray();
    }
    public static int StrengthAt(ContractWorld w,int i,long at)=>Strength[i]-CombatLosses(w,at).Where(l=>l.Unit==UnitIds[i]).Sum(l=>l.Soldiers);
    public static int TotalStrength(ContractWorld w,long at)=>Strength.Sum()-CombatLosses(w,at).Where(l=>UnitIds.Contains(l.Unit)).Sum(l=>l.Soldiers);
    public static OwnUnitReport[] OwnCombatReport(ContractWorld w,long at){var hits=CombatLosses(w,at);return Enumerable.Range(0,3).Select(i=>new OwnUnitReport(UnitIds[i],Strength[i]-hits.Where(l=>l.Unit==UnitIds[i]).Sum(l=>l.Soldiers),hits.Where(l=>l.Unit==UnitIds[i]).Sum(l=>l.Soldiers),w.NewMilitary?.Rout?.Regroups.FirstOrDefault(g=>g.Unit==i&&g.At<=at)?.Morale??100-hits.Where(l=>l.Unit==UnitIds[i]).Sum(l=>l.Morale))).ToArray();}
}
