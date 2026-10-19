using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Simulation;

public static partial class MilitaryFlow
{
    static bool Live(MilitaryUnit u)=>u.Phase is not (MilitaryPhase.Exited or MilitaryPhase.Incapacitated);
    static bool Retreating(MilitaryUnit u)=>u.Phase is MilitaryPhase.Routing or MilitaryPhase.Withdrawing;
    public static void Advance(MilitaryState s,MessageState messages,long tick,IEnumerable<string> characters)
    {
        if(s.Tick+1!=tick)throw new InvalidOperationException("Military step must use the common world tick exactly once.");
        s.Tick=tick;
        foreach(var raw in s.Orders.Where(o=>o.Status==MilitaryOrderStatus.InTransit).ToArray())
        {
            if(!MessageFlow.Knows(messages,OrderMessage(raw.Id),raw.RecipientId))continue;
            var order=raw with{ReceivedAt=MessageFlow.LearnedAt(messages,OrderMessage(raw.Id),raw.RecipientId),Status=MilitaryOrderStatus.WaitingEffect};Replace(s,order);
            var unit=s.Units.Single(u=>u.Id==order.UnitId);
            if(!Permits(s,order.IssuerId,unit,order.Action,tick)||unit.CommanderId!=order.RecipientId)Resolve(s,messages,order,MilitaryOrderStatus.Refused,"到達時の指揮権・指揮官が不一致",tick);
            else if(!unit.WillObey)Resolve(s,messages,order,MilitaryOrderStatus.Refused,"担当者が指示を拒否",tick);
        }
        // Cancellation receipt/effect is causally prior to its target. No arbitrary registration priority.
        var due=s.Orders.Where(o=>o.Status==MilitaryOrderStatus.WaitingEffect&&o.EffectiveAt<=tick)
            .OrderBy(o=>o.Action==MilitaryAction.Cancel?0:1).ThenBy(o=>o.SentAt).ThenBy(o=>o.Id,StringComparer.Ordinal).ToArray();
        var competing=due.Where(o=>o.Action!=MilitaryAction.Cancel).GroupBy(o=>o.UnitId).Where(g=>g.Count()>1).Select(g=>g.Key).ToHashSet();
        foreach(var raw in due)
        {
            var order=s.Orders.Single(o=>o.Id==raw.Id);if(order.Status!=MilitaryOrderStatus.WaitingEffect)continue;
            var unit=s.Units.Single(u=>u.Id==order.UnitId);
            if(order.ExecuteBy<tick){Resolve(s,messages,order,MilitaryOrderStatus.Expired,"履行期限超過",tick);continue;}
            if(!Permits(s,order.IssuerId,unit,order.Action,tick)||unit.CommanderId!=order.RecipientId){Resolve(s,messages,order,MilitaryOrderStatus.Refused,"効力時の指揮権・指揮官が不一致",tick);continue;}
            if(order.Action==MilitaryAction.Cancel)
            {
                var target=s.Orders.Single(o=>o.Id==order.Cancels);
                if(target.Status is MilitaryOrderStatus.Expired or MilitaryOrderStatus.Refused or MilitaryOrderStatus.Cancelled)
                {Resolve(s,messages,order,MilitaryOrderStatus.Applied,"対象は既に停止済み",tick);s.Applied=[..s.Applied,order.Id];continue;}
                Replace(s,target with{Status=MilitaryOrderStatus.Cancelled,ResolvedAt=tick,Reason="取消命令が到達し効力発生"});
                if(unit.ActiveOrder==target.Id)s.Units=s.Units.Select(u=>u.Id==unit.Id?u with{Phase=Live(u)&&!Retreating(u)?MilitaryPhase.Holding:u.Phase,Route=Retreating(u)?u.Route:[],WillFight=false,ActiveOrder=null,MoveBudget=0}:u).ToArray();
                Resolve(s,messages,order,MilitaryOrderStatus.Applied,"対象の既発生損害は巻き戻さず、以後の履行を停止",tick);s.Applied=[..s.Applied,order.Id];continue;
            }
            if(competing.Contains(order.UnitId)){Resolve(s,messages,order,MilitaryOrderStatus.Refused,"同時刻に競合する複数指示",tick);continue;}
            if(!Live(unit)||Retreating(unit)){Resolve(s,messages,order,MilitaryOrderStatus.Refused,"部隊は既に退避・行動不能",tick);continue;}
            var destination=order.Action==MilitaryAction.Withdraw?s.Armies.Single(a=>a.Id==unit.ArmyId).Exit:order.Destination;
            var route=MilitaryMap.Route(unit.Cell,destination);
            s.Units=s.Units.Select(u=>u.Id==unit.Id?u with{Route=route,MoveBudget=0,ActiveOrder=order.Id,WillFight=order.Action==MilitaryAction.Advance,
                Phase=order.Action==MilitaryAction.Withdraw?MilitaryPhase.Withdrawing:route.Length>0?MilitaryPhase.Marching:MilitaryPhase.Holding}:u).ToArray();
            Replace(s,order with{AppliedAt=tick});order=s.Orders.Single(o=>o.Id==order.Id);Resolve(s,messages,order,MilitaryOrderStatus.Applied,"担当者が受領・判断し履行開始",tick);s.Applied=[..s.Applied,order.Id];
        }
        foreach(var order in s.Orders.Where(o=>o.ExecuteBy<tick&&o.Status is MilitaryOrderStatus.InTransit or MilitaryOrderStatus.WaitingEffect).ToArray())
        {
            // No reply may be fabricated by a recipient who has never learned the order.
            if(order.ReceivedAt is null)Replace(s,order with{Status=MilitaryOrderStatus.Expired,ResolvedAt=tick,Reason="到達前に履行期限超過"});
            else Resolve(s,messages,order,MilitaryOrderStatus.Expired,"履行期限超過",tick);
        }
        IngestReports(s,messages,tick);
        MoveAndFight(s,tick);
        Observe(s,characters,tick);
    }
    static void MoveAndFight(MilitaryState s,long tick)
    {
        s.Units=s.Units.Select(u=>u with{Previous=u.Cell}).ToArray();var before=s.Units;
        bool fighting=before.Length==2&&before.All(u=>Live(u)&&!Retreating(u)&&u.WillFight)&&
            s.Hostilities.Any(h=>h.ValidFrom<=tick&&tick<h.ValidUntil&&((h.LeftArmy==before[0].ArmyId&&h.RightArmy==before[1].ArmyId)||(h.LeftArmy==before[1].ArmyId&&h.RightArmy==before[0].ArmyId)))&&
            MilitaryMap.CanSee(before[0].Cell,before[1].Cell)&&MilitaryMap.InContact(before[0].Cell,before[1].Cell);
        if(fighting)
        {
            if(!before.All(u=>u.Phase is MilitaryPhase.Contact or MilitaryPhase.Fighting))
            {s.Units=before.Select(u=>u with{Phase=MilitaryPhase.Contact}).ToArray();return;}
            s.Units=before.Select(u=>
            {
                var enemy=before.Single(e=>e.Id!=u.Id);int loss=Math.Min(u.Soldiers,enemy.AttackPower);
                s.Losses=[..s.Losses,new($"military/{tick}/{u.Id}",tick,u.Id,loss,enemy.MoralePressure)];
                int soldiers=u.Soldiers-loss,morale=Math.Max(0,u.Morale-enemy.MoralePressure);
                var phase=soldiers==0?MilitaryPhase.Incapacitated:morale<=20?MilitaryPhase.Routing:MilitaryPhase.Fighting;
                return u with{Soldiers=soldiers,Morale=morale,Phase=phase,ActionAt=tick,WillFight=phase==MilitaryPhase.Fighting,
                    Route=phase==MilitaryPhase.Routing?MilitaryMap.Route(u.Cell,s.Armies.Single(a=>a.Id==u.ArmyId).Exit):u.Route};
            }).ToArray();return;
        }
        var proposed=new Dictionary<string,MapCell>();
        foreach(var u in before)
        {
            if(!Live(u))continue;
            if(u.Phase is MilitaryPhase.Contact or MilitaryPhase.Fighting)
            {proposed[u.Id]=u.Cell;continue;}
            if(u.Route.Length>0&&u.MoveBudget+1>=MilitaryMap.Cost(u.Route[0]))proposed[u.Id]=u.Route[0];else proposed[u.Id]=u.Cell;
        }
        var rejected=new HashSet<string>();
        foreach(var u in before.Where(Live))
        {
            var next=proposed[u.Id];
            if(next==u.Cell)continue;
            if(proposed.Count(p=>p.Value==next)>1||before.Any(e=>e.Id!=u.Id&&Live(e)&&e.Cell==next&&(proposed[e.Id]==e.Cell||proposed[e.Id]==u.Cell)))rejected.Add(u.Id);
        }
        s.Units=before.Select(u=>
        {
            if(!Live(u))return u;
            if(u.Phase is MilitaryPhase.Contact or MilitaryPhase.Fighting)return u with{Phase=MilitaryPhase.Holding,Route=[],ActiveOrder=null,WillFight=false};
            bool moved=proposed[u.Id]!=u.Cell&&!rejected.Contains(u.Id);MapCell cell=moved?proposed[u.Id]:u.Cell;var route=moved?u.Route.Skip(1).ToArray():u.Route;
            bool exited=Retreating(u)&&cell==s.Armies.Single(a=>a.Id==u.ArmyId).Exit;
            return u with{Cell=cell,Route=route,TravelCells=u.TravelCells+(moved?1:0),MoveBudget=moved||route.Length==0?0:Math.Min(u.MoveBudget+1,MilitaryMap.Cost(route[0])-1),
                Phase=exited?MilitaryPhase.Exited:route.Length==0&&!Retreating(u)?MilitaryPhase.Holding:u.Phase};
        }).ToArray();
    }
}
