using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Simulation;

public enum MilitaryAction { Move, Advance, Withdraw, Cancel }
public enum MilitaryPhase { Holding, Marching, Contact, Fighting, Routing, Withdrawing, Exited, Incapacitated }
public enum MilitaryOrderStatus { InTransit, WaitingEffect, Applied, Refused, Expired, Cancelled }
public enum IntelKind { Own, Sight, Report, Clear }
public sealed record ArmyDefinition(string Id,string OrganizationId,string CommanderId,MapCell Exit);
public sealed record MilitaryHostility(string LeftArmy,string RightArmy,string Basis,long ValidFrom,long ValidUntil);
public sealed record MilitaryGrant(string Id,string ActorId,string UnitId,string ArmyId,string Jurisdiction,string Basis,
    long ValidFrom,long ValidUntil,MilitaryAction[] Actions)
{
    public bool Allows(string actor,MilitaryUnit unit,MilitaryAction action,long tick)=>ActorId==actor&&UnitId==unit.Id&&ArmyId==unit.ArmyId&&
        Jurisdiction==MilitaryFlow.Scope&&ValidFrom<=tick&&tick<ValidUntil&&Actions.Contains(action);
}
public sealed record MilitaryUnit(string Id,string ArmyId,string CommanderId,MapCell Cell,MapCell Previous,int Soldiers,int InitialSoldiers,
    int Morale,int InitialMorale,MilitaryPhase Phase,MapCell[] Route,int MoveBudget=0,int TravelCells=0,bool WillFight=false,bool WillObey=true,
    string? ActiveOrder=null,long? ActionAt=null,int AttackPower=8,int MoralePressure=8);
public sealed record MilitaryOrder(string Id,string IssuerId,string RecipientId,string UnitId,MilitaryAction Action,MapCell Destination,
    string? Cancels,long SentAt,long EffectiveAt,long? ExecuteBy,int InitialDelay,MilitaryOrderStatus Status=MilitaryOrderStatus.InTransit,
    long? ReceivedAt=null,long? AppliedAt=null,long? ResolvedAt=null,string Reason="",string? ResponseBody=null);
public sealed record MilitaryLoss(string Id,long Tick,string UnitId,int Soldiers,int Morale);
public sealed record MilitaryIntel(string Id,string ObserverId,string UnitId,IntelKind Kind,long ObservedAt,long LearnedAt,
    MapCell Cell,int? Soldiers,int? Morale,MilitaryPhase? Phase,bool Present,string? EvidenceMessageId=null,string? SourceIntelId=null,string ArmyId="",string CommanderId="");
public sealed record MilitaryReport(string Id,string SenderId,string RecipientId,string SourceIntelId);
public sealed class MilitaryState
{
    public int Schema {get;set;}=1;
    public string MapId {get;set;}=MilitaryMap.Id;
    public string Rules {get;set;}="military-grid-v1";
    public long StartedAt {get;set;}
    public long Tick {get;set;}
    public ArmyDefinition[] Armies {get;set;}=[];
    public MilitaryHostility[] Hostilities {get;set;}=[];
    public MilitaryUnit[] Units {get;set;}=[];
    public MilitaryGrant[] Grants {get;set;}=[];
    public MilitaryOrder[] Orders {get;set;}=[];
    public MilitaryLoss[] Losses {get;set;}=[];
    public MilitaryIntel[] Intel {get;set;}=[];
    public MilitaryReport[] Reports {get;set;}=[];
    public string[] Applied {get;set;}=[];
}
// A filtered read model: unknown enemies have no row, and stale information has no live position/stats.
public sealed record MilitaryVisibleUnit(string UnitId,string ArmyId,string CommanderId,MapCell Cell,MapCell Previous,
    int? Soldiers,int? Morale,MilitaryPhase? Phase,bool Own,bool Current,bool Present,long ObservedAt,string Evidence,int TravelCells=0,long? ActionAt=null);
public sealed record MilitaryVisibleOrder(string Id,string IssuerId,string UnitId,MilitaryAction Action,string KnownStatus,bool RetryAvailable);
public sealed record MilitaryView(long Tick,string ObserverId,MilitaryVisibleUnit[] Units,MilitaryVisibleOrder[] Orders);

public static partial class MilitaryFlow
{
    public const string Scope="military-grid-v1";
    public static string OrderMessage(string id)=>id+"/military-command";
    public static string ReplyMessage(string id)=>id+"/military-result";
    public static bool Permits(MilitaryState s,string actor,MilitaryUnit unit,MilitaryAction action,long tick)=>s.Grants.Any(g=>g.Allows(actor,unit,action,tick));
    public static MilitaryState Create(WorldSnapshot world,long tick)
    {
        var ids=world.Characters.Select(c=>c.Id).ToArray();string org=world.Definition.Organizations[0].Id;
        return new(){StartedAt=tick,Tick=tick,Armies=[new("army-red",org,ids[0],new(0,3)),new("army-blue",org,ids[1],new(10,3))],
            Hostilities=[new("army-red","army-blue","明示した演習の対抗区分。政治的敵対・離反ではない",tick,long.MaxValue)],Units=[
            new("unit-red","army-red",ids[0],new(1,2),new(1,2),100,100,100,100,MilitaryPhase.Holding,[],AttackPower:12,MoralePressure:15),
            new("unit-blue","army-blue",ids[1],new(9,4),new(9,4),60,60,55,55,MilitaryPhase.Holding,[])],Grants=[
            new("grant-red",ids[0],"unit-red","army-red",Scope,"演習部隊の明示的指揮委任",tick,long.MaxValue,Enum.GetValues<MilitaryAction>()),
            new("grant-blue",ids[1],"unit-blue","army-blue",Scope,"演習部隊の明示的指揮委任",tick,long.MaxValue,Enum.GetValues<MilitaryAction>())]};
    }
    public static void Dispatch(MilitaryState s,MessageState messages,string id,string issuer,string unitId,MilitaryAction action,MapCell destination,
        long tick,int delay=1,long? executeBy=null,long? effectiveAt=null,string? cancels=null)
    {
        if(string.IsNullOrWhiteSpace(id)||delay is <1 or >100||!Enum.IsDefined(action)||executeBy<tick||effectiveAt<tick)throw new ArgumentException("Invalid military order input.");
        var unit=s.Units.Single(u=>u.Id==unitId);var old=s.Orders.SingleOrDefault(o=>o.Id==id);
        if(old is not null)
        {
            if(old.IssuerId!=issuer||old.UnitId!=unitId||old.Action!=action||old.Destination!=destination||old.Cancels!=cancels||old.ExecuteBy!=executeBy||
                old.InitialDelay!=delay||effectiveAt is not null&&old.EffectiveAt!=effectiveAt)throw new InvalidOperationException("Military order ID payload conflict.");
            return;
        }
        if(!Permits(s,issuer,unit,action,tick))throw new UnauthorizedAccessException("No current military unit/army/action grant.");
        if(action==MilitaryAction.Cancel)
        {
            var original=s.Orders.Single(o=>o.Id==cancels);if(original.IssuerId!=issuer||original.UnitId!=unitId||original.Action==MilitaryAction.Cancel)throw new UnauthorizedAccessException("Cannot cancel this order.");
        }
        else
        {
            if(cancels is not null)throw new ArgumentException("Unexpected cancel target.");
            if(action==MilitaryAction.Withdraw&&destination!=s.Armies.Single(a=>a.Id==unit.ArmyId).Exit)throw new ArgumentException("Withdrawal must name this army's exit.");
            MilitaryMap.Route(unit.Cell,destination);
        }
        long effect=effectiveAt??tick;
        if(s.Orders.Any(o=>o.UnitId==unitId&&o.SentAt==tick&&o.Action!=MilitaryAction.Cancel&&action!=MilitaryAction.Cancel&&o.Status is MilitaryOrderStatus.InTransit or MilitaryOrderStatus.WaitingEffect))
            throw new InvalidOperationException("Conflicting simultaneous military orders need explicit cancellation or a later issue time.");
        var order=new MilitaryOrder(id,issuer,unit.CommanderId,unitId,action,destination,cancels,tick,effect,executeBy,delay);s.Orders=[..s.Orders,order];
        MessageFlow.Send(messages,new(OrderMessage(id),id,"military-command",issuer,unit.CommanderId,SnapshotCodec.Write(order),Scope,tick,executeBy),tick,delay);
    }
    static void Replace(MilitaryState s,MilitaryOrder order)=>s.Orders=s.Orders.Select(o=>o.Id==order.Id?order:o).ToArray();
    static void Reply(MilitaryState s,MessageState messages,MilitaryOrder order,long tick)=>MessageFlow.Send(messages,
        new(ReplyMessage(order.Id),order.Id,"military-result",order.RecipientId,order.IssuerId,order.Status+":"+order.Reason,Scope,tick),tick);
    static void Resolve(MilitaryState s,MessageState messages,MilitaryOrder order,MilitaryOrderStatus status,string reason,long tick)
    {order=order with{Status=status,Reason=reason,ResolvedAt=tick,AppliedAt=status==MilitaryOrderStatus.Applied?tick:order.AppliedAt,ResponseBody=status+":"+reason};Replace(s,order);Reply(s,messages,order,tick);}
}
