namespace HakutaiKagami.Core.Contracts;
public enum FleetAction { SendSupply, StationWarship, Reach, Unload, SailHome, Dock }
public sealed record FleetInput(EntityId Id,FleetAction Action,EntityId Ship,EntityId Source,EntityId Target,long Cargo=0,EntityId? Original=null);
public sealed record FleetOrder(FleetInput Input,EntityId Actor,EntityId Owner,string Edge,long At,long Revision,long Depart,long Arrive,long Leave,long Home,long Provisions,long Coins,string Outcome);
public static class SiegeFleet
{
 public const long StationTime=86400;
 internal static FleetOrder[] Orders(ContractWorld w)=>w.Sieges?.Fleets??[];
 static bool Before(FleetOrder o,long at,long rev)=>o.At<at||o.At==at&&o.Revision<rev;
 static bool Start(FleetOrder o)=>o.Input.Action is FleetAction.SendSupply or FleetAction.StationWarship;
 static bool Has(ContractWorld w,EntityId root,FleetAction a,long at,long rev)=>Orders(w).Any(o=>o.Input.Original==root&&o.Input.Action==a&&Before(o,at,rev));
 public static bool Busy(ContractWorld w,EntityId ship,long at,long rev)=>Orders(w).Any(o=>Start(o)&&o.Input.Ship==ship&&Before(o,at,rev)&&!Has(w,o.Input.Id,FleetAction.Dock,at,rev));
 public static bool Blocked(ContractWorld w,EntityId target,EntityId beneficiary,long at,long rev)=>Orders(w).Any(o=>o.Input.Action==FleetAction.StationWarship&&o.Input.Target==target&&o.Owner!=beneficiary&&o.Arrive<=at&&at<o.Leave&&Before(o,at,rev)&&Reached(w,o,at,rev)&&!Has(w,o.Input.Id,FleetAction.Dock,at,rev)&&RegionalDomains.Owner(w,target)==beneficiary);
 public static EntityId[] Ports(ContractWorld w,EntityId source)=>NationalRoutes.Catalog.Edges.Where(e=>e.Mode=="sea"&&(e.From==NewGameWorld.Node(source.Value)||e.To==NewGameWorld.Node(source.Value))).Select(e=>RegionalCampaign.Site(e.From==NewGameWorld.Node(source.Value)?e.To:e.From)).Where(s=>RegionalDomains.Owner(w,s)!=null).Distinct().ToArray();
 public static long CastleRiceAt(ContractWorld w,EntityId site,long at,long rev)=>CommunityEconomy.SitesFor(w).Contains(site)?CommunityEconomy.AccountAt(w,site,at,rev).HouseRice:CastleSiege.RiceTransfers(w,site,at,rev).Sum(e=>e.Amount);
 static EntityId Effect(FleetOrder root,FleetAction action)=>new("request:castle-fleet/"+root.Input.Id.Value.Replace(':','/')+"/"+action.ToString().ToLowerInvariant());
 internal static FleetOrder Prepare(ContractWorld w,FleetInput i,EntityId actor,long at,long rev,bool automatic){using var _ccRev=ControlContext.At(rev);void Need(bool b,string why)=>CastleSiege.Need(b,why);Need(CastleSiege.Enabled(w)&&Enum.IsDefined(i.Action)&&EntityId.IsValid(i.Id.Value),"船の原本を確認してください");if(i.Action is FleetAction.SendSupply or FleetAction.StationWarship){Need(!automatic&&i.Original==null&&i.Source!=i.Target&&Ports(w,i.Source).Contains(i.Target),"隣接する既定海路の両港を選んでください");var owner=RegionalDomains.Owner(w,i.Source)??throw new InvalidDataException("港の家が不明です");Need(TransportWork.At(w,actor,i.Source,at,rev,true)&&RegionalDomains.Authority(w,actor,i.Source,at,rev),"現地の港と家の処分権が必要です");var target=RegionalDomains.Owner(w,i.Target)??throw new InvalidDataException("相手港の家が不明です");bool aid=i.Action==FleetAction.SendSupply;if(aid)Need(owner==target&&i.Cargo is >0 and <=1000,"自家の港に運ぶ実在米を指定してください");else Need(owner!=target&&i.Cargo==0&&FactionStances.OwnAt(w,actor,at,rev-1).Any(q=>q.FromOwner==owner&&q.ToOwner==target&&q.EffectiveFrom<=at&&at<q.Until),"敵対を認識した当主の軍船配置が必要です");var ship=PublicWorks.Ships(w).SingleOrDefault(s=>s.Id==i.Ship);Need(ship!=null&&ship.Owner==owner&&ship.Port==i.Source&&ship.Kind==(aid?WorksKind.TradeShip:WorksKind.Warship)&&ship.BuiltAt<=at&&!Busy(w,i.Ship,at,rev),"船が現地に完成し、他の任務に出ていないことを確かめてください");Need(!CraftTrade.Entries(w).Any(e=>e.Input.Action==CraftAction.LaunchVoyage&&e.Input.Reference==i.Ship&&e.Revision<rev&&!CraftTrade.Entries(w).Any(z=>z.Input.Reference==e.Input.Id&&z.Revision<rev&&z.Input.Action is CraftAction.ReturnVoyage or CraftAction.CancelVoyage)),"交易に出ている船は使えません");var a=NewGameWorld.Node(i.Source.Value);var b=NewGameWorld.Node(i.Target.Value);var link=NationalRoutes.Catalog.Edges.Single(e=>e.Mode=="sea"&&(e.From==a&&e.To==b||e.From==b&&e.To==a));long depart=TravelWorld.Departure(w,link.Id,at),duration=(link.LengthMm+link.FootSpeed-1)/link.FootSpeed,arrive=depart+duration,leave=arrive+StationTime,home=leave+duration;Need(w.PublicWorks!.Care.Any(c=>c.Ship==i.Ship&&c.Revision<rev&&c.At<=at&&home<=c.Until),"帰港予定まで船を維持してください");long food=(10*(home-at)+86399)/86400;var acc=CommunityEconomy.AccountAt(w,i.Source,at,rev);Need(acc.Workforce-acc.Reserved>=10&&acc.HouseRice-HouseSalary.Reserved(w,i.Source,at,rev)>=food+i.Cargo&&RegionalDomains.Cash(w,i.Source,at,rev)>=10,"船員十人、航海米・積荷・運航費が不足しています");return new(i,actor,owner,link.Id,at,rev,depart,arrive,leave,home,food,10,"港で積込");}
 Need(automatic&&i.Original!=null,"着船と荷渡しは時計・現地で判定します");var root=Orders(w).Single(o=>o.Input.Id==i.Original&&Start(o));Need(i.Id==Effect(root,i.Action)&&i.Ship==root.Input.Ship&&i.Source==root.Input.Source&&i.Target==root.Input.Target&&i.Cargo==0&&actor==root.Actor&&!Has(w,root.Input.Id,i.Action,at,rev),"航海原本と重複を確認してください");string outcome=i.Action switch{FleetAction.Reach when at==root.Arrive=>RegionalCampaign.BlockedAt(w,root.Edge,root.Depart)?"運休・出港地待機":"対象港へ到着",FleetAction.Unload when root.Input.Action==FleetAction.SendSupply&&at>=root.Arrive&&at<=root.Leave&&Reached(w,root,at,rev+1)&&!Blocked(w,i.Target,root.Owner,at,rev+1)=>"現地納入",FleetAction.SailHome when at==root.Leave=>"帰航開始",FleetAction.Dock when at==root.Home=>"帰港精算",_=>throw new InvalidDataException("未到着・水路封鎖・時点を確認してください")};return root with{Input=i,At=at,Revision=rev,Outcome=outcome,Provisions=0,Coins=0};}
 static bool Reached(ContractWorld w,FleetOrder o,long at,long rev)=>Orders(w).Any(q=>q.Input.Original==o.Input.Id&&q.Input.Action==FleetAction.Reach&&q.Outcome=="対象港へ到着"&&Before(q,at,rev));
 internal static void Add(ContractWorld w,FleetOrder o){w.Sieges??=new(1,CastleSiege.Policy,[]);w.Sieges=w.Sieges with{Fleets=[..Orders(w),o]};}
 internal static bool PrepareNavalDefense(ContractWorld world,EntityId commander,EntityId port,EntityId orderId)
 {
     if (!CastleSiege.Enabled(world)) return false;
     var now = world.ElapsedSeconds;
     var revision = world.CommitSequence;
     if (now % 86400 != 0) return false;
     var owner = RegionalDomains.Owner(world, port);
     var hostile = FactionStances.OwnAt(world, commander, now, revision - 1)
         .Where(claim => claim.FromOwner == owner && claim.EffectiveFrom <= now && now < claim.Until)
         .Select(claim => claim.ToOwner).ToHashSet();
     if (hostile.Count == 0) return false;
     var targets = Ports(world, port).Where(site => RegionalDomains.Owner(world, site) is {} defender && hostile.Contains(defender)).ToArray();
     if (targets.Length == 0) return false;
     var ship = PublicWorks.Ships(world).FirstOrDefault(s => s.Kind == WorksKind.Warship && s.Owner == owner && s.Port == port);
     if (ship == null)
     {
         var kind = PublicWorks.Level(world, port, WorksKind.Port) > 0 ? WorksKind.Warship : WorksKind.Port;
         return TransactionalWorld.TryApply(world, candidate => PublicWorks.Execute(candidate, new(orderId,port,kind), commander));
     }
     if (!ship.Maintained)
         return TransactionalWorld.TryApply(world, candidate => PublicWorks.CareForShip(candidate,orderId,ship.Id,commander));
     if (Busy(world, ship.Id, now, revision)) return false;
     var input = new FleetInput(orderId,FleetAction.StationWarship,ship.Id,port,targets[0]);
     return TransactionalWorld.TryApply(world, candidate => Add(candidate,Prepare(candidate,input,commander,now,candidate.CommitSequence,false)));
 }
 internal static long? NextBoundary(ContractWorld w,long target)
 {
     var times=Orders(w).Where(Start).SelectMany(o=>new[]{o.Depart,o.Arrive,o.Leave,o.Home});
     if (CastleSiege.Enabled(w) && w.FactionStances?.Decisions.Any(d =>
         d.Authority.Issuer != w.PlayerControl && d.Input.Action == FactionStanceAction.DeclareHostile &&
         d.Input.Until > w.ElapsedSeconds) == true)
         times=times.Append((w.ElapsedSeconds / 86400 + 1) * 86400);
     return times.Where(t=>w.ElapsedSeconds<t&&t<=target).Select(t=>(long?)t).Min();
 }
 internal static void Tick(ContractWorld w){long at=w.ElapsedSeconds,rev=w.CommitSequence;var roots=Orders(w).Where(Start).OrderBy(o=>o.Input.Id.Value,StringComparer.Ordinal).ToArray();void Auto(FleetOrder o,FleetAction a){if(Has(w,o.Input.Id,a,at,rev+1))return;var i=new FleetInput(Effect(o,a),a,o.Input.Ship,o.Input.Source,o.Input.Target,Original:o.Input.Id);Add(w,Prepare(w,i,o.Actor,at,rev,true));}
 foreach(var o in roots.Where(o=>o.Arrive==at))Auto(o,FleetAction.Reach);
 foreach(var o in roots.Where(o=>o.Input.Action==FleetAction.SendSupply&&o.Arrive<=at&&at<=o.Leave&&Reached(w,o,at,rev+1)&&!Blocked(w,o.Input.Target,o.Owner,at,rev+1)))Auto(o,FleetAction.Unload);
 foreach(var o in roots.Where(o=>o.Leave==at))Auto(o,FleetAction.SailHome);foreach(var o in roots.Where(o=>o.Home==at))Auto(o,FleetAction.Dock);}
 internal static (long At,long Revision,long Amount)[] Rice(ContractWorld w,EntityId site,long at,long rev){using var _ccRev=ControlContext.At(rev);var rows=new List<(long,long,long)>();foreach(var o in Orders(w).Where(o=>Before(o,at,rev))){if(Start(o)&&o.Input.Source==site)rows.Add((o.At,o.Revision,-o.Provisions-o.Input.Cargo));if(o.Input.Action==FleetAction.Unload&&o.Input.Target==site)rows.Add((o.At,o.Revision,Orders(w).Single(q=>q.Input.Id==o.Input.Original).Input.Cargo));if(o.Input.Action==FleetAction.Dock&&o.Input.Source==site&&!Has(w,o.Input.Original!.Value,FleetAction.Unload,o.At,o.Revision))rows.Add((o.At,o.Revision,Orders(w).Single(q=>q.Input.Id==o.Input.Original).Input.Cargo));}return rows.ToArray();}
 internal static long Cash(ContractWorld w,EntityId site,long at,long rev)=>-Orders(w).Where(o=>Before(o,at,rev)&&o.Input.Source==site).Sum(o=>o.Coins);
 internal static int Crew(ContractWorld w,EntityId site,long at,long rev)=>10*Orders(w).Count(o=>Start(o)&&Before(o,at,rev)&&o.Input.Source==site&&at<o.Home);
 internal static long LaborSeconds(ContractWorld w,EntityId site,long a,long b,long rev)=>10*Orders(w).Where(o=>Start(o)&&o.Revision<rev&&o.Input.Source==site).Sum(o=>Math.Max(0,Math.Min(b,o.Home)-Math.Max(a,o.At)));
 internal static void Validate(ContractWorld w,ContractWorld? prior){var os=Orders(w);if(prior!=null)CastleSiege.Need(os.Take(Orders(prior).Length).SequenceEqual(Orders(prior)),"過去の船便は変更できません");var ids=new HashSet<EntityId>();long at=0,rev=0;foreach(var o in os){CastleSiege.Need(ids.Add(o.Input.Id)&&o.At>=at&&o.At<=w.ElapsedSeconds&&o.Revision>=rev&&o.Revision<=w.CommitSequence&&o.Revision>0&&o==Prepare(w,o.Input,o.Actor,o.At,o.Revision,!Start(o)),"船の所属・荷・航路・着船の原本が不正です");at=o.At;rev=o.Revision;}
 foreach(var root in os.Where(Start)){
  void Due(FleetAction action,long due){if(due<=w.ElapsedSeconds)CastleSiege.Need(os.Count(x=>x.Input.Original==root.Input.Id&&x.Input.Action==action&&x.At==due)==1,"航海の到着・帰航・精算の記録が不足しています");}
  Due(FleetAction.Reach,root.Arrive);Due(FleetAction.SailHome,root.Leave);Due(FleetAction.Dock,root.Home);
  if(root.Input.Action!=FleetAction.SendSupply||!Reached(w,root,w.ElapsedSeconds,w.CommitSequence+1))continue;
  var boundaries=os.Where(x=>x.Input.Action==FleetAction.StationWarship&&x.Input.Target==root.Input.Target).Select(x=>x.Leave).Append(root.Arrive).Distinct().Where(t=>root.Arrive<=t&&t<=root.Leave&&t<=w.ElapsedSeconds).Order();
  var first=boundaries.Where(t=>!Blocked(w,root.Input.Target,root.Owner,t,long.MaxValue)).Select(t=>(long?)t).FirstOrDefault();
  var unload=os.SingleOrDefault(x=>x.Input.Original==root.Input.Id&&x.Input.Action==FleetAction.Unload);
  CastleSiege.Need(unload?.At==first,"封鎖のない最初の到着・荷渡し時点と記録が違います");
 }
 }
}
public sealed partial class WorldSession
{
 public CommandResult ManageSiegeFleet(FleetInput i,long revision){using var _ccRev=ControlContext.At(revision);lock(gate){var old=SiegeFleet.Orders(state).SingleOrDefault(o=>o.Input.Id==i.Id);if(old!=null)return new(old.Input==i?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"受付済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,i.Id,state.CommitSequence,"表示を更新してください");if(NewGameWork.OwnIdUsed(state,i.Id)||state.Requests.Any(r=>r.Id==i.Id))return new(ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"使用済みの原本です");try{Change(w=>SiegeFleet.Add(w,SiegeFleet.Prepare(w,i,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence,false)));return new(ResultCode.Accepted,i.Id,state.CommitSequence,"船員・積荷を確保し出航を手配しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException){return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,e.Message);}}}
}
