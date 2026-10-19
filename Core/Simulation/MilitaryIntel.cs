using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Simulation;

public static partial class MilitaryFlow
{
    public static MilitaryIntel? LatestIntel(MilitaryState s,string observer,string unit)=>s.Intel.Where(i=>i.ObserverId==observer&&i.UnitId==unit)
        .OrderByDescending(i=>i.ObservedAt).ThenByDescending(i=>i.Kind is IntelKind.Own or IntelKind.Sight or IntelKind.Clear).ThenByDescending(i=>i.LearnedAt).ThenBy(i=>i.Id,StringComparer.Ordinal).FirstOrDefault();
    static MapCell ObserverCell(MilitaryState s,string observer)=>s.Units.FirstOrDefault(u=>u.CommanderId==observer)?.Cell??new MapCell(0,6);
    public static void Observe(MilitaryState s,IEnumerable<string> characters,long tick)
    {
        foreach(var observer in characters.Order(StringComparer.Ordinal))
        {
            var location=ObserverCell(s,observer);
            foreach(var unit in s.Units)
            {
                bool own=unit.CommanderId==observer,visible=Live(unit)&&MilitaryMap.CanSee(location,unit.Cell);
                string id=$"observation/{tick}/{observer}/{unit.Id}";if(s.Intel.Any(i=>i.Id==id))continue;
                if(own||visible)
                    s.Intel=[..s.Intel,new(id,observer,unit.Id,own?IntelKind.Own:IntelKind.Sight,tick,tick,unit.Cell,unit.Soldiers,unit.Morale,unit.Phase,Live(unit),ArmyId:unit.ArmyId,CommanderId:unit.CommanderId)];
                else
                {
                    var previous=LatestIntel(s,observer,unit.Id);
                    // The absence at a formerly known cell is observable; a hidden new location is not.
                    if(previous is not null&&previous.Present&&MilitaryMap.CanSee(location,previous.Cell))
                        s.Intel=[..s.Intel,new(id,observer,unit.Id,IntelKind.Clear,tick,tick,previous.Cell,null,null,null,false,ArmyId:previous.ArmyId,CommanderId:previous.CommanderId)];
                }
            }
        }
    }
    public static void SendIntel(MilitaryState s,MessageState messages,string id,string sender,string recipient,string sourceIntel,long tick,int delay)
    {
        var source=s.Intel.Single(i=>i.Id==sourceIntel);if(source.ObserverId!=sender)throw new UnauthorizedAccessException("Sender has not learned that battlefield fact.");
        var old=s.Reports.SingleOrDefault(r=>r.Id==id);var report=new MilitaryReport(id,sender,recipient,sourceIntel);
        if(old is not null){if(old!=report)throw new InvalidOperationException("Report ID payload conflict.");return;}
        if(s.Reports.Length>=256)throw new InvalidOperationException("Fixture report limit reached.");
        MessageFlow.Send(messages,new(id+"/military-intel",id,"military-intel",sender,recipient,SnapshotCodec.Write(source),Scope,tick),tick,delay);s.Reports=[..s.Reports,report];
    }
    static void IngestReports(MilitaryState s,MessageState messages,long tick)
    {
        foreach(var report in s.Reports)
        {
            string mid=report.Id+"/military-intel";if(!MessageFlow.Knows(messages,mid,report.RecipientId)||s.Intel.Any(i=>i.Id=="received/"+mid))continue;
            var fact=s.Intel.Single(i=>i.Id==report.SourceIntelId);long learned=MessageFlow.LearnedAt(messages,mid,report.RecipientId)!.Value;
            s.Intel=[..s.Intel,new("received/"+mid,report.RecipientId,fact.UnitId,IntelKind.Report,fact.ObservedAt,learned,fact.Cell,fact.Soldiers,fact.Morale,fact.Phase,fact.Present,mid,fact.Id,fact.ArmyId,fact.CommanderId)];
        }
    }
    public static MilitaryView View(MilitaryState s,MessageState messages,string observer,long tick)
    {
        var units=new List<MilitaryVisibleUnit>();
        foreach(var u in s.Units)
        {
            bool own=u.CommanderId==observer;var known=LatestIntel(s,observer,u.Id);if(known is null)continue;
            bool current=known.ObservedAt==tick&&known.Kind is IntelKind.Own or IntelKind.Sight;
            var prior=current?s.Intel.LastOrDefault(i=>i.ObserverId==observer&&i.UnitId==u.Id&&i.ObservedAt==tick-1&&i.Kind==IntelKind.Sight&&i.Present):null;
            // Every enemy field below comes from the knowledge record, never live enemy state.
            units.Add(new(u.Id,known.ArmyId,known.CommanderId,known.Cell,own?u.Previous:prior?.Cell??known.Cell,known.Soldiers,known.Morale,known.Phase,
                own,current,known.Present,known.ObservedAt,known.Kind.ToString(),own?u.TravelCells:0,own?u.ActionAt:null));
        }
        var orders=s.Orders.Where(o=>o.IssuerId==observer||MessageFlow.Knows(messages,OrderMessage(o.Id),observer)).Select(o=>
        {
            string text="発送済・履行結果は未確認";
            if(MessageFlow.Knows(messages,ReplyMessage(o.Id),observer))text=messages.Messages.Single(m=>m.Id==ReplyMessage(o.Id)).Body;
            else if(o.ExecuteBy<tick)text="期限経過・履行結果は未確認";
            var latest=MessageFlow.Latest(messages,OrderMessage(o.Id));bool failed=MessageFlow.FailureKnown(messages,latest.Id,observer);
            if(failed&&!MessageFlow.Knows(messages,ReplyMessage(o.Id),observer))text="配送失敗の報告を受領";
            bool retry=failed&&o.IssuerId==observer&&latest.Number<MessageFlow.MaxAttempts&&(o.ExecuteBy is null||o.ExecuteBy>tick)&&!MessageFlow.Knows(messages,ReplyMessage(o.Id),observer);
            return new MilitaryVisibleOrder(o.Id,o.IssuerId,o.UnitId,o.Action,text,retry);
        }).ToArray();
        return new(tick,observer,units.ToArray(),orders);
    }
}
