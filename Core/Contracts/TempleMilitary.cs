namespace HakutaiKagami.Core.Contracts;
public enum TempleMilitaryAction { RequestDefense, EndDefense, Observe, Attack, Withdraw }
public sealed record TempleMilitaryInput(EntityId Id,TempleMilitaryAction Action,EntityId Site);
public sealed record TempleMilitaryOrder(TempleMilitaryInput Input,EntityId Actor,EntityId Army,EntityId Owner,long At,long Revision,bool Accepted,bool Hostile);
public static class TempleMilitary
{
 public static readonly EntityId Site=new("site:sakai");
 public static EntityId Unit=>new("unit:temple-militia/sakai");
 public static EntityId Army=>new("army:temple-militia/sakai");
 public static TempleMilitaryOrder[] Orders(ContractWorld w)=>w.Battlefield?.Temples??[];
 public static bool Armed(ContractWorld w,EntityId site)=>BattlefieldRules.Enabled(w)&&site==Site;
 public static FieldPoint Position(ContractWorld w){var p=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>n.Id=="sakai").Position);return new(p.X+3000,p.Y);}
 static bool Before(TempleMilitaryOrder o,long at,long rev)=>o.At<at||o.At==at&&o.Revision<rev;
 static bool Broken(ContractWorld w,EntityId actor,long at,long rev)=>SocialWorld.Ledger(w).Where(e=>e.Actor==actor&&e.Site==Site&&e.At<=at&&e.Revision<rev&&e.Input.Action is SocialWorldAction.Protect or SocialWorldAction.BreakProtection).LastOrDefault()?.Input.Action==SocialWorldAction.BreakProtection;
 static void Need(bool b,string why){if(!b)throw new InvalidDataException(why);}
 public static TempleMilitaryOrder? Aid(ContractWorld w,long at,long rev)=>Orders(w).Where(o=>Before(o,at,rev)&&o.Accepted&&o.Input.Action is TempleMilitaryAction.RequestDefense or TempleMilitaryAction.EndDefense).LastOrDefault() is {Input.Action:TempleMilitaryAction.RequestDefense} a&&at<a.At+86400&&!Broken(w,a.Actor,at,rev)?a:null;
 public static bool KnownHostile(ContractWorld w,long at,long rev)=>Orders(w).Where(o=>Before(o,at,rev)&&o.Input.Action==TempleMilitaryAction.Observe).LastOrDefault()?.Hostile==true;
 public static bool Attacking(ContractWorld w,long at,long rev)=>Orders(w).Where(o=>Before(o,at,rev)&&o.Input.Action is TempleMilitaryAction.Attack or TempleMilitaryAction.Withdraw).LastOrDefault() is {Input.Action:TempleMilitaryAction.Attack} a&&at<a.At+3600;
 public static int Strength(ContractWorld w,long at)=>20-WorldClashes.SoldiersLost(w,Unit,at);
 static TempleMilitaryOrder Prepare(ContractWorld w,TempleMilitaryInput i,long at,long rev,EntityId actor,EntityId army,EntityId owner){
 Need(Armed(w,i.Site)&&Enum.IsDefined(i.Action),"この寺社は武装協力の対象ではありません");var c=NewGameMilitary.WorldEngagementBefore(w,army,at,rev-1);Need(c.NewMilitary?.Terms.Owner==owner&&actor==ControlSuccession.PlayerAtRevision(w,rev)&&NewGameMilitary.CustodyAt(c,at,rev),"引受済みの軍と指揮権が必要です");bool field=NewGameMilitary.Deployed(c);bool local=field?RegionalGround.Distance(NewGameMilitary.PersonPosition(c,at),Position(c))<=80000:TransportWork.At(w,actor,i.Site,at,rev,true);Need(local,"本人と軍が寺社の現地にいる必要があります");bool hostile=Broken(w,actor,at,rev),accepted=true;
 if(i.Action==TempleMilitaryAction.RequestDefense){Need(!field&&Aid(w,at,rev)==null&&Strength(c,at)>0,"現地で重複なく協力を相談してください");accepted=!hostile&&SocialWorld.Ledger(w).Any(e=>e.Actor==actor&&e.Site==Site&&e.Input.Action==SocialWorldAction.Protect&&e.At<=at&&e.Revision<rev);if(accepted)Need(CommunityEconomy.Enabled(w)&&CommunityEconomy.AccountAt(w,Site,at,rev).HouseRice-HouseSalary.Reserved(w,Site,at,rev)>=12,"一日の協力兵20人分の米12kgが不足しています");}
 if(i.Action==TempleMilitaryAction.EndDefense)Need(Aid(w,at,rev)?.Army==army,"終了する協力がありません");
 if(i.Action==TempleMilitaryAction.Attack)Need(field&&KnownHostile(w,at,rev)&&hostile&&Strength(c,at)>0,"武装し、本人が敵対を確認した寺社だけが対象です");
 if(i.Action==TempleMilitaryAction.Withdraw)Need(Attacking(w,at,rev),"取りやめる攻撃がありません");
 return new(i,actor,army,owner,at,rev,accepted,i.Action==TempleMilitaryAction.Observe&&hostile);
 }
 internal static void Apply(ContractWorld w,TempleMilitaryInput i){var t=w.NewMilitary?.Terms??throw new InvalidDataException("引受中の軍がありません");var o=Prepare(w,i,w.ElapsedSeconds,w.CommitSequence,w.PlayerControl,t.Army,t.Owner);w.Battlefield??=new(1,BattlefieldRules.Policy,[]);w.Battlefield=w.Battlefield with{Temples=[..Orders(w),o]};}
 internal static (long At,long Revision,long Amount)[] RiceTransfers(ContractWorld w,EntityId site,long at,long rev)=>Orders(w).Where(o=>o.Input.Site==site&&o.Input.Action==TempleMilitaryAction.RequestDefense&&o.Accepted&&Before(o,at,rev)).Select(o=>(o.At,o.Revision,-12L)).ToArray();
 internal static WorldEngagementSnapshot Adapt(ContractWorld w,WorldEngagementSnapshot s){if(!BattlefieldRules.Enabled(w)||w.NewMilitary==null)return s;var point=Position(w);if(!s.Units.Any(u=>u.Unit.Army==w.NewMilitary.Terms.Army&&RegionalGround.Distance(u.Position,point)<=80000))return s;var temple=SocialWorld.Temple(w,Site);var cohort=new UnitState(Unit,Army,temple.Representative,temple.Id,Strength(w,s.At),0);int morale=Math.Max(0,100-WorldClashes.MoraleLost(w,Unit,s.At));var u=new EngagementUnit(cohort,point,ContinuousWorld.Height(point),morale,morale<=25?"routed":"standing",EngagementIntent.SelfDefense,true);var units=s.Units.ToArray();if(Attacking(w,s.At,w.CommitSequence+1)&&Broken(w,w.PlayerControl,s.At,w.CommitSequence+1))units=units.Select(x=>x.Unit.Army==w.NewMilitary.Terms.Army?x with{Intent=EngagementIntent.DeliberateUnauthorizedAttack,Target=Unit}:x).ToArray();return s with{Units=[..units,u]};}
 internal static bool Defends(ContractWorld w,EngagementUnit defender,EngagementUnit target)=>defender.Unit.Id==Unit&&Aid(w,w.ElapsedSeconds,w.CommitSequence+1) is {} a&&target.Unit.Army==a.Army;
 internal static void Validate(ContractWorld w,ContractWorld? prior){if(prior?.Battlefield?.Temples is {} old)Need(Orders(w).Take(old.Length).SequenceEqual(old),"寺社合意の原本を変更できません");long rev=0,at=0;var ids=new HashSet<EntityId>();foreach(var o in Orders(w)){Need(EntityId.IsValid(o.Input.Id.Value)&&ids.Add(o.Input.Id)&&o.Revision>rev&&o.Revision<=w.CommitSequence&&o.At>=at&&o.At<=w.ElapsedSeconds,"寺社の入力順序が不正です");Need(o==Prepare(w,o.Input,o.At,o.Revision,o.Actor,o.Army,o.Owner),"当時の寺社の条件と判断が一致しません");at=o.At;rev=o.Revision;}}
}
public sealed partial class WorldSession
{
 public CommandResult ManageTempleMilitary(TempleMilitaryInput i,long revision){lock(gate){var old=TempleMilitary.Orders(state).SingleOrDefault(o=>o.Input.Id==i.Id);if(old!=null)return new(old.Input==i?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"受付済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,i.Id,state.CommitSequence,"表示を更新してください");if(NewGameWork.OwnIdUsed(state,i.Id)||state.Requests.Any(r=>r.Id==i.Id))return new(ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"使用済みの原本です");try{Change(w=>TempleMilitary.Apply(w,i));return new(ResultCode.Accepted,i.Id,state.CommitSequence,TempleMilitary.Orders(state).Last().Accepted?"現地の寺社との判断を記録しました":"寺社は保護の合意がないため協力を留保しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException){return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,e.Message);}}}
}
