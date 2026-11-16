namespace HakutaiKagami.Core.Contracts;
public enum SiegeAction { Begin,Assault,Resolve,Parley,DeliverGate,DeliverKeep,Withdraw }
public sealed record SiegeInput(EntityId Id,SiegeAction Action,EntityId Site,EntityId? Original=null,CastleSection? Section=null);
public sealed record SiegeOrder(SiegeInput Input,EntityId Actor,EntityId Army,EntityId Attacker,EntityId Defender,long At,long Revision,long? Due,int Men,int Damage,int CastleRice,bool Witness,string Outcome);
public sealed record SiegeState(int Schema,string Policy,SiegeOrder[] Orders){public FleetOrder[] Fleets{get;init;}=[];}
public static partial class CastleSiege
{
 public const string Policy="territory-district-siege-food-water-open-gate-v3";
 public static bool Enabled(ContractWorld w)=>(w.NewGame?.TerritoryPolicy==Policy)||NationalSystems.On(w,"CastleSiege");
 internal static SiegeOrder[] Orders(ContractWorld w)=>w.Sieges?.Orders??[];
 internal static bool Before(SiegeOrder o,long at,long rev)=>o.At<at||o.At==at&&o.Revision<rev;
 internal static void Need(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
 public static bool OwnIdUsed(ContractWorld w,EntityId id)=>Orders(w).Any(q=>q.Input.Id==id)||SiegeFleet.Orders(w).Any(o=>o.Input.Id==id);
 static SiegeOrder[] History(ContractWorld w,EntityId root,long at,long rev)=>Orders(w).Where(o=>Before(o,at,rev)&&(o.Input.Id==root||o.Input.Original==root)).ToArray();
 public static bool Closed(SiegeOrder o)=>o.Input.Action is SiegeAction.Withdraw or SiegeAction.DeliverKeep||o.Outcome is "攻囲終了" or "本丸を占有";
 public static SiegeOrder? Active(ContractWorld w,EntityId site,long at,long rev){using var _ccRev=ControlContext.At(rev);var begin=Orders(w).LastOrDefault(o=>Before(o,at,rev)&&o.Input.Site==site&&o.Input.Action==SiegeAction.Begin);return begin==null||Closed(History(w,begin.Input.Id,at,rev)[^1])?null:begin;}
 public static EntityId? OccupierAt(ContractWorld world, EntityId site, long at, long revision)
 {using var _ccRev=ControlContext.At(revision);
     var capture = Orders(world).LastOrDefault(order => Before(order, at, revision) &&
         order.Input.Site == site && order.Outcome == "本丸を占有");
     if (capture == null) return null;
     return CastleHandover.Forces(world, capture.Attacker, site, at, revision) > 0 ? capture.Attacker : null;
 } internal static (long At,int Order,int Change)[] DamageEvents(ContractWorld w,EntityId site,CastleSection section,long at,long rev)=>Orders(w).Where(o=>Before(o,at,rev)&&o.Input.Site==site&&o.Input.Section==section&&o.Input.Action==SiegeAction.Resolve&&o.Damage>0).Select(o=>(o.At,2,o.Damage)).ToArray();
 internal static (long At,long Revision,long Amount)[] RiceTransfers(ContractWorld w,EntityId site,long at,long rev)=>Orders(w).Where(o=>Before(o,at,rev)&&o.Input.Site==site&&o.CastleRice>0).Select(o=>(o.At,o.Revision,-(long)o.CastleRice)).Concat(SiegeFleet.Rice(w,site,at,rev)).ToArray();
 static CastleSection? NextSection(SiegeOrder[] rows){int n=rows.Count(o=>o.Outcome is "区画を確保" or "本丸を占有");return n<4?(CastleSection)n:null;}
 static bool Pending(SiegeOrder[] rows)=>rows[^1].Input.Action==SiegeAction.Assault;
 static bool Agreed(SiegeOrder[] rows)=>rows.LastOrDefault(o=>o.Input.Action==SiegeAction.Parley)?.Outcome=="城兵の助命と退去に合意";
 static SiegeOrder Prepare(ContractWorld w,SiegeInput i,EntityId army,long at,long rev,bool automatic){using var _ccRev=ControlContext.At(rev);Need(Enabled(w)&&EntityId.IsValid(i.Id.Value)&&Enum.IsDefined(i.Action)&&MaterialMarkets.Sites(w).Contains(i.Site),"攻囲の城と原本を確認してください");var c=NewGameMilitary.WorldEngagementBefore(w,army,at,rev-1);var m=c.NewMilitary??throw new InvalidDataException("指揮中の部隊がありません");var owner=m.Terms.Owner;var defender=RegionalDomains.Owner(w,i.Site)??throw new InvalidDataException("対象城の家が不明です");Need(owner!=defender,"自家の城は攻囲できません");int men=CastleHandover.Forces(c,owner,i.Site,at,rev);bool onsite=men>0&&NewGameMilitary.CustodyAt(c,at,rev-1)&&NewGameMilitary.DeployedAt(c,at)&&(ArmyRoadMarch.AssemblySite(c,at)??m.Terms.Site)==i.Site;long carried=NewGameMilitary.CarryAt(c,at,rev-1);bool hostile=FactionStances.Known(c,c.PlayerControl).Any(q=>q.FromOwner==owner&&q.ToOwner==defender&&q.EffectiveFrom<=at&&at<q.Until);var principal=FactionStances.ResolveAuthority(c,c.PlayerControl,owner,at,rev-1);bool localCommand=onsite&&principal!=null;var start=Active(w,i.Site,at,rev);int damage=0,rice=0;long? due=null;string outcome;bool witness=onsite;
 if(i.Action==SiegeAction.Begin){Need(!automatic&&i.Original==null&&i.Section==null&&start==null&&OccupierAt(c,i.Site,at,rev)!=owner&&localCommand&&hostile&&carried>0,"敵対を認識した当主が、現地に展開した部隊と兵糧を確かめて開始してください");outcome="攻囲開始";}
 else{Need(start!=null&&i.Original==start.Input.Id&&army==start.Army,"同じ攻囲の原本が必要です");var rows=History(w,start!.Input.Id,at,rev);var next=NextSection(rows);if(i.Action!=SiegeAction.Resolve)Need(!automatic&&localCommand,"指揮官と部隊が現地に必要です");if(i.Action==SiegeAction.Withdraw){Need(i.Section==null,"撤収に区画は指定しません");outcome="攻囲終了";}
 else if(i.Action==SiegeAction.Assault){Need(!Pending(rows)&&!Agreed(rows)&&hostile&&next!=null&&i.Section==next&&carried>=men*60L,"次の区画と、強襲に必要な携行兵糧を確認してください");due=at+60;outcome="強襲中";}
 else if(i.Action==SiegeAction.Resolve){var order=rows[^1];Need(automatic&&Pending(rows)&&order.Due==at&&i.Id==Resolution(order)&&i.Section==order.Input.Section,"予定された強襲境界ではありません");if(!onsite||!hostile){outcome="攻囲終了";}else if(carried<=0){outcome="兵糧不足で停止";}else{var section=i.Section!.Value;var defense=CastleWorks.Defense(w,i.Site,section,at,rev);int guards=CastleHandover.Forces(c,defender,i.Site,at,rev);long free=Math.Max(0,SiegeFleet.CastleRiceAt(w,i.Site,at,rev)-HouseSalary.Reserved(w,i.Site,at,rev));rice=(int)Math.Min(free,(guards+19)/20);damage=Math.Min(defense.Strength,Math.Max(1,men/3-guards/(rice>0?5:10)));outcome=damage<defense.Strength?"区画が損傷":section==CastleSection.Keep?"本丸を占有":"区画を確保";}}
 else{Need(!Pending(rows)&&i.Section==null,"強襲を終えてから現地で交渉してください");var head=HouseSuccession.Head(w,defender,at,rev-1);bool present=TransportWork.At(w,head,i.Site,at,rev,true);int relief=CastleHandover.Forces(c,defender,i.Site,at,rev);if(i.Action==SiegeAction.Parley){Need(!Agreed(rows)&&present,"現地の守備側代表と条件を協議してください");bool accepts=w.Characters.Single(p=>p.Id==head).WillObey&&relief==0&&(rows.Any(o=>o.Outcome=="区画を確保")||SiegeFleet.CastleRiceAt(w,i.Site,at,rev)<=20);outcome=accepts?"城兵の助命と退去に合意":"守備側は開城を留保";}else{Need(Agreed(rows)&&present&&relief==0,"合意と両者の現地立会い、救援軍の不在が必要です");if(i.Action==SiegeAction.DeliverGate){Need(!rows.Any(o=>o.Input.Action==SiegeAction.DeliverGate),"城門は受領済みです");outcome="城門を受領";}else{Need(i.Action==SiegeAction.DeliverKeep&&rows.Last().Input.Action==SiegeAction.DeliverGate,"先に城門を実際に受け取ってください");outcome="本丸を占有";}}}}
 return new(i,c.PlayerControl,army,owner,defender,at,rev,due,men,damage,rice,witness,outcome);}
 static EntityId Resolution(SiegeOrder q)=>new("request:siege-resolution/"+q.Input.Id.Value.Replace(':','/'));
 static void Add(ContractWorld w,SiegeOrder q){w.Sieges??=new(1,Policy,[]);w.Sieges=w.Sieges with{Orders=[..w.Sieges.Orders,q]};}
 internal static void Apply(ContractWorld w,SiegeInput i){Need(i.Action!=SiegeAction.Resolve,"経過処理は時計が行います");Add(w,Prepare(w,i,w.NewMilitary?.Terms.Army??throw new InvalidDataException("部隊がありません"),w.ElapsedSeconds,w.CommitSequence,false));}
 internal static long? NextBoundary(ContractWorld w,long target)=>Orders(w).Where(o=>o.Due>w.ElapsedSeconds&&o.Due<=target).Select(o=>o.Due).Min();
 internal static void Tick(ContractWorld w){SiegeFleet.Tick(w);foreach(var q in Orders(w).Where(o=>o.Due==w.ElapsedSeconds)){var root=q.Input.Original!.Value;if(Active(w,q.Input.Site,w.ElapsedSeconds,w.CommitSequence+1)==null||History(w,root,w.ElapsedSeconds,w.CommitSequence+1)[^1]!=q)continue;var i=new SiegeInput(Resolution(q),SiegeAction.Resolve,q.Input.Site,root,q.Input.Section);Add(w,Prepare(w,i,q.Army,w.ElapsedSeconds,w.CommitSequence,true));}}
 public static void Validate(ContractWorld w,ContractWorld? prior){if(w.Sieges==null){Need(prior?.Sieges==null,"攻囲記録を消せません");return;}Need(Enabled(w)&&w.Sieges.Schema==1&&w.Sieges.Policy==Policy,"攻囲の版が異なります");SiegeFleet.Validate(w,prior);var os=Orders(w);if(prior?.Sieges!=null)Need(os.Take(prior.Sieges.Orders.Length).SequenceEqual(prior.Sieges.Orders),"過去の攻囲記録が違います");var ids=new HashSet<EntityId>();long rev=0,at=0;foreach(var o in os){Need(ids.Add(o.Input.Id)&&o.Revision>rev&&o.Revision<=w.CommitSequence&&o.At>=at&&o.At<=w.ElapsedSeconds,"攻囲の原本・時点が不正です");Need(o==Prepare(w,o.Input,o.Army,o.At,o.Revision,o.Input.Action==SiegeAction.Resolve),"当時の部隊・兵糧・区画と一致しません");rev=o.Revision;at=o.At;}
 foreach(var assault in os.Where(o=>o.Input.Action==SiegeAction.Assault && o.Due<=w.ElapsedSeconds))
 {
     var next=os.FirstOrDefault(o=>o.Input.Original==assault.Input.Original && o.Revision>assault.Revision);
     Need(next!=null && (next.Input.Action==SiegeAction.Resolve && next.At==assault.Due ||
         next.Input.Action==SiegeAction.Withdraw && next.At<=assault.Due),"強襲の決着または撤収記録がありません");
 }
 }
}
public sealed partial class WorldSession
{
 public CommandResult ManageSiege(SiegeInput i,long revision){using var _ccRev=ControlContext.At(revision);lock(gate){var old=CastleSiege.Orders(state).SingleOrDefault(o=>o.Input.Id==i.Id);if(old!=null)return new(old.Input==i?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"受付済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,i.Id,state.CommitSequence,"表示を更新してください");if(NewGameWork.OwnIdUsed(state,i.Id)||state.Requests.Any(r=>r.Id==i.Id))return new(ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"使用済みの原本です");try{Change(w=>CastleSiege.Apply(w,i));return new(ResultCode.Accepted,i.Id,state.CommitSequence,"攻囲の現地判断を記録しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException){return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,e.Message);}}}
}
