namespace HakutaiKagami.Core.Contracts;
public sealed record UnitCourierFrame(EntityId Carrier,FieldPoint Position,FieldPoint Next,string Purpose,bool Holding);
public static partial class NewGameMilitary
{
    // Recorded physical legs only. No recipient GPS or invented character location.
    public static UnitCourierFrame[] CourierFrames(ContractWorld w,long at)
    {
        var frames=new List<UnitCourierFrame>();
        void Add(EntityId carrier,FieldPoint origin,FieldLeg[] route,string purpose,bool holding=false){frames.Add(new(carrier,RegionalGround.At(route,origin,at),RegionalGround.At(route,origin,at+1),purpose,holding));}
        foreach(var d in UnitDispatches(w)){
            if(at<d.At)continue;
            var r=UnitReceipts(w).SingleOrDefault(r=>r.Id==d.Id&&r.At<=at);
            if(r==null&&at<d.DueAt)Add(UnitCourier(d.Id),d.Origin,d.Route,d.Role==UnitInquiryRole?"本隊への照会伝令":"撤退命令の伝令");
            else if(r!=null&&at<r.ReplyDueAt)Add(UnitCourier(d.Id),d.Destination,r.ReplyRoute,d.Role==UnitInquiryRole?"本隊の照会返書":"部隊からの返書");
        }
        var x=w.NewMilitary?.ReplyRecovery;
        if(x!=null){
            foreach(var f in x.Flights.Where(f=>f.At<=at&&at<f.DueAt))Add(f.Carrier,f.Origin,f.Route,f.Kind==UnitReplyLeg.FailureReport?"返書の不達を本隊へ報告":"返書の再送伝令");
            foreach(var group in x.Holdings.Where(h=>h.At<=at).GroupBy(h=>h.Holder)){Need(group.Select(h=>h.Position).Distinct().Count()==1,"同じ保持者が異なる場所に存在します");var h=group.First();if(!frames.Any(f=>f.Carrier==h.Holder))Add(h.Holder,h.Position,[],"書状を預かる伝令",true);}
        }
        Need(frames.Select(f=>f.Carrier).Distinct().Count()==frames.Count,"同じ伝令が複数の経路に存在します");
        return frames.OrderBy(f=>f.Carrier.Value,StringComparer.Ordinal).ToArray();
    }
}
