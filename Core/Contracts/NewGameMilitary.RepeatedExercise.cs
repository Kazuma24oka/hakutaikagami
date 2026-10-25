namespace HakutaiKagami.Core.Contracts;
public sealed record ExerciseStateView(EntityId? Episode,RegionalLoss[] Losses,OwnUnitReport[] Own,OwnUnitReport[] Opponents);
public static partial class NewGameMilitary
{
 public const string RepeatedExerciseContract="versioned-repeatable-synthetic-contact-lifetime-strength-morale-supply-episode-v1";
 static EntityId[] OpponentIds(NewMilitaryRequest start)=>start.Input.ExercisePolicy==RepeatedExerciseContract?Enumerable.Range(0,3).Select(i=>new EntityId("unit:exercise/"+Hash(start.Input.Id.Value)+"/"+i)).ToArray():ExerciseIds;
 static bool RepeatedExercise(ContractWorld w)=>w.NewMilitary?.Requests.Any(q=>q.Input.ExercisePolicy==RepeatedExerciseContract)==true;
 static NewMilitaryRequest[] ExerciseStarts(ContractWorld w)=>w.NewMilitary?.Requests.Where(q=>q.Input.Action==NewMilitaryAction.CombatStart).ToArray()??[];
 static NewMilitaryRequest? ExerciseAt(ContractWorld w,long at,long rev)=>ExerciseStarts(w).LastOrDefault(q=>Before(q.At,q.Revision,at,rev));
 static EntityId ExerciseRecordKey(ContractWorld w,long at,long rev){var q=ExerciseAt(w,at,rev);Need(q!=null,"演習記録の起点がありません");return q.Input.Id;}
 static NewRoutState RoutScope(ContractWorld w,NewRoutState s){if(!RepeatedExercise(w))return s;var start=CombatStart(w);if(start==null)return s with{Retreats=[],Arrivals=[],Regroups=[]};bool Match(long at,long rev)=>ExerciseRecordKey(w,at,rev)==start.Input.Id;return s with{Retreats=s.Retreats.Where(r=>Match(r.At,r.Revision)).ToArray(),Arrivals=s.Arrivals.Where(r=>Match(r.At,r.Revision)).ToArray(),Regroups=s.Regroups.Where(r=>Match(r.At,r.Revision)).ToArray()};}
 public static int ExerciseNumber(ContractWorld w)=>ExerciseStarts(w).Length;
 public static bool ExerciseVisible(ContractWorld w)=>CombatStart(w) is {} q&&CaseStart(w,q.At,q.Revision)?.Input.Id==CaseStart(w)?.Input.Id;
 public static bool CanStartCombat(ContractWorld w,string? policy){if(policy==null)return CanStartCombat(w);if(policy!=RepeatedExerciseContract||!CombatReady(w)||CombatActive(w)||RoutActive(w))return false;var permit=Event(w,MilitaryEffect.CombatPermission)!;if(CombatStarted(w)&&Read(w,permit).Combat?.Contract!=RepeatedExerciseContract)return false;var own=ExerciseState(w,w.ElapsedSeconds).Own;return own.Any(u=>u.Soldiers>0)&&own.Where(u=>u.Soldiers>0).All(u=>u.Morale>25);}
 public static ExerciseStateView ExerciseState(ContractWorld w,long at)
 {
  if(!RepeatedExercise(w)){var hits=CombatLosses(w,at);var own=OwnCombatReport(w,at);var other=Enumerable.Range(0,3).Select(i=>new OwnUnitReport(ExerciseIds[i],Strength[i]-hits.Where(h=>h.Unit==ExerciseIds[i]).Sum(h=>h.Soldiers),hits.Where(h=>h.Unit==ExerciseIds[i]).Sum(h=>h.Soldiers),100-hits.Where(h=>h.Unit==ExerciseIds[i]).Sum(h=>h.Morale))).ToArray();return new(ExerciseAt(w,at,w.CommitSequence)?.Input.Id,hits,own,other);}
  var effects=new List<RegionalLoss>();int[] ownMen=[..Strength],ownMorale=[100,100,100],enemyMen=[..Strength],enemyInitial=[..Strength],enemyMorale=[100,100,100];var groups=(w.NewMilitary?.Rout?.Regroups??[]).Where(g=>g.At<=at).OrderBy(g=>g.At).ThenBy(g=>g.Revision).ToArray();int cursor=0;void Regroup(long until){while(cursor<groups.Length&&groups[cursor].At<=until){var g=groups[cursor++];ownMorale[g.Unit]=g.Morale;}}
  EntityId? episode=null;EntityId[] enemyIds=ExerciseIds;
  foreach(var start in ExerciseStarts(w).Where(q=>q.At<=at)){
   Regroup(start.At);episode=start.Input.Id;enemyIds=OpponentIds(start);enemyInitial=[..ownMen];enemyMen=[..enemyInitial];enemyMorale=[100,100,100];long until=Math.Min(at,CombatUntil(w,start));
   for(long t=start.At+1;t<=until;t++){
    Regroup(t-1);var inputs=new List<ContactParticipant>();for(int i=0;i<3;i++){var p=Position(w,i,t);var origin=Position(w,i,start.At);var enemy=new FieldPoint(origin.X+3000,origin.Y);inputs.Add(new(UnitIdsFor(w)[i],0,p,ContinuousWorld.Height(p),ownMen[i],ownMorale[i],ownMorale[i]<=25?"routed":"standing"));inputs.Add(new(enemyIds[i],1,enemy,ContinuousWorld.Height(enemy),enemyMen[i],enemyMorale[i],enemyMorale[i]<=25?"routed":"standing"));}
    var hits=ContactCombat.Resolve(t,true,inputs);effects.AddRange(hits);foreach(var h in hits){int i=Array.IndexOf(UnitIdsFor(w),h.Unit);if(i>=0){ownMen[i]-=h.Soldiers;ownMorale[i]-=h.Morale;}else{i=Array.IndexOf(enemyIds,h.Unit);Need(i>=0,"演習相手のIDが不明です");enemyMen[i]-=h.Soldiers;enemyMorale[i]-=h.Morale;}}Regroup(t);
   }
  }
  Regroup(at);var ownReports=Enumerable.Range(0,3).Select(i=>new OwnUnitReport(UnitIdsFor(w)[i],ownMen[i],Strength[i]-ownMen[i],ownMorale[i])).ToArray();var enemyReports=Enumerable.Range(0,3).Select(i=>new OwnUnitReport(enemyIds[i],enemyMen[i],enemyInitial[i]-enemyMen[i],enemyMorale[i])).ToArray();return new(episode,effects.ToArray(),ownReports,enemyReports);
 }
}
