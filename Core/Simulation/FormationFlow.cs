using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Simulation;

public sealed record FormationSlot(string UnitId,MapCell Destination,string OrderId,int Delay);
public sealed record FormationPlan(string Id,string IssuerId,MapCell Start,MapCell End,long SentAt,FormationSlot[] Slots);
public static partial class MilitaryFlow
{
    public const string FormationRules="formation-line-v1";
    public static MilitaryState CreateFormation(WorldSnapshot world,long tick)
    {
        var ids=world.Characters.Select(c=>c.Id).ToArray();
        return new(){Rules=FormationRules,StartedAt=tick,Tick=tick,
            Armies=[new("army-formation",world.Definition.Organizations[0].Id,ids[0],new(0,3))],
            Units=new[]{ids[1],ids[0],ids[2]}.Select((id,i)=>new MilitaryUnit("formation-"+i,"army-formation",id,new(1,1+i*2),new(1,1+i*2),100,100,100,100,MilitaryPhase.Holding,[])).ToArray(),
            Grants=ids.Select((id,i)=>new MilitaryGrant("formation-grant-"+i,ids[0],"formation-"+i,"army-formation",Scope,"軍議による演習布陣の指揮委任",tick,long.MaxValue,[MilitaryAction.Move,MilitaryAction.Cancel])).ToArray()};
    }
    public static MapCell[] FormationPositions(MapCell start,MapCell end)
    {
        if(!MilitaryMap.Contains(start)||!MilitaryMap.Contains(end)||start.Distance(end)<4)throw new ArgumentException("布陣線は地図内で4区画以上の長さが必要です。");
        var cells=Enumerable.Range(0,3).Select(i=>new MapCell((int)Math.Round(start.X+(end.X-start.X)*i/2.0,MidpointRounding.AwayFromZero),(int)Math.Round(start.Z+(end.Z-start.Z)*i/2.0,MidpointRounding.AwayFromZero))).ToArray();
        if(cells.Distinct().Count()!=3||cells.Any(c=>!MilitaryMap.Walkable(c)))throw new ArgumentException("担当位置が川・岩や地図外にかかっています。");
        return cells;
    }
    public static void DispatchFormation(MilitaryState s,MessageState messages,string id,string issuer,MapCell start,MapCell end,int[] delays,long tick)
    {
        if(s.Rules!=FormationRules)throw new InvalidOperationException("布陣試作を配置してください。");
        if(string.IsNullOrWhiteSpace(id)||delays.Length!=3||delays.Any(d=>d is <1 or >100))throw new ArgumentException("Invalid formation transmission input.");
        var cells=FormationPositions(start,end);var units=s.Units.OrderBy(u=>u.Id,StringComparer.Ordinal).ToArray();
        var slots=units.Select((u,i)=>new FormationSlot(u.Id,cells[i],id+"/unit/"+i,delays[i])).ToArray();var plan=new FormationPlan(id,issuer,start,end,tick,slots);
        var old=s.FormationPlans.SingleOrDefault(p=>p.Id==id);
        if(old is not null){if(old.IssuerId!=issuer||old.Start!=start||old.End!=end||!old.Slots.SequenceEqual(slots))throw new InvalidOperationException("Formation ID payload conflict.");return;}
        if(s.FormationPlans.Length!=0)throw new InvalidOperationException("この試作では一つの作戦を検証します。新しい線は演習を初期化して指定してください。");
        foreach(var slot in slots)
        {
            var unit=units.Single(u=>u.Id==slot.UnitId);
            if(!Permits(s,issuer,unit,MilitaryAction.Move,tick))throw new UnauthorizedAccessException("布陣対象すべての指揮権が必要です。");
            MilitaryMap.Route(unit.Cell,slot.Destination,slots.Where(x=>x.UnitId!=unit.Id).Select(x=>x.Destination));
        }
        s.FormationPlans=[plan];
        foreach(var slot in slots)Dispatch(s,messages,slot.OrderId,issuer,slot.UnitId,MilitaryAction.Move,slot.Destination,tick,slot.Delay);
    }
    static MapCell[] CommandRoute(MilitaryState s,MilitaryUnit unit,MilitaryOrder order)
    {
        var plan=s.FormationPlans.SingleOrDefault(p=>p.Slots.Any(x=>x.OrderId==order.Id));
        return MilitaryMap.Route(unit.Cell,order.Destination,plan?.Slots.Where(x=>x.UnitId!=unit.Id).Select(x=>x.Destination));
    }
    static void ReportFormationArrivals(MilitaryState s,MessageState messages,long tick)
    {
        foreach(var slot in s.FormationPlans.SelectMany(p=>p.Slots))
        {
            var order=s.Orders.Single(o=>o.Id==slot.OrderId);var unit=s.Units.Single(u=>u.Id==slot.UnitId);
            if(s.FormationArrivals.Contains(order.Id)||order.Status!=MilitaryOrderStatus.Applied||unit.ActiveOrder!=order.Id||unit.Cell!=slot.Destination||unit.Route.Length>0)continue;
            var intel=LatestIntel(s,unit.CommanderId,unit.Id)!;
            SendIntel(s,messages,"formation-arrival/"+order.Id,unit.CommanderId,order.IssuerId,intel.Id,tick,1);
            s.FormationArrivals=[..s.FormationArrivals,order.Id];
        }
    }
    static void ValidateFormations(MilitaryState s,MessageState messages,long tick)
    {
        static void Check(bool ok){if(!ok)throw new InvalidDataException("Invalid immutable formation plan/arrival.");}
        Check(s.FormationPlans.Length<=(s.Rules==FormationRules?1:0));
        foreach(var p in s.FormationPlans)
        {
            var positions=FormationPositions(p.Start,p.End);Check(p.SentAt>=s.StartedAt&&p.SentAt<=tick&&p.Slots.Length==3&&p.Slots.Select(x=>x.UnitId).Distinct().Count()==3);
            for(int i=0;i<3;i++){var slot=p.Slots[i];Check(slot.Destination==positions[i]&&slot.Delay is >=1 and <=100&&slot.OrderId==p.Id+"/unit/"+i&&s.Orders.Any(o=>o.Id==slot.OrderId&&o.IssuerId==p.IssuerId&&o.UnitId==slot.UnitId&&o.Destination==slot.Destination&&o.InitialDelay==slot.Delay&&o.SentAt==p.SentAt&&o.Action==MilitaryAction.Move));}
        }
        Check(s.FormationArrivals.Distinct().Count()==s.FormationArrivals.Length);
        foreach(var id in s.FormationArrivals)
        {
            var slot=s.FormationPlans.SelectMany(p=>p.Slots).SingleOrDefault(x=>x.OrderId==id);Check(slot is not null);
            var report=s.Reports.SingleOrDefault(r=>r.Id=="formation-arrival/"+id);Check(report is not null);
            var source=s.Intel.SingleOrDefault(i=>i.Id==report!.SourceIntelId);var order=s.Orders.Single(o=>o.Id==id);
            Check(source is not null&&source.Kind==IntelKind.Own&&source.ObserverId==order.RecipientId&&source.UnitId==slot!.UnitId&&source.Cell==slot.Destination&&source.ObservedAt>=order.AppliedAt&&source.Phase==MilitaryPhase.Holding&&messages.Messages.Any(m=>m.Id==report!.Id+"/military-intel"));
        }
    }
}
