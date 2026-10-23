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
            if(r==null&&at<d.DueAt)Add(UnitCourier(d.Id),d.Origin,d.Route,d.Role==UnitDirectiveCancellationRole?"分隊原本の取消伝令":d.Role==UnitDirectiveRole?"分隊指示の伝令":d.Role==UnitInquiryRole?"部隊への照会伝令":d.Role==CancellationRole?"取消命令の伝令":"撤退命令の伝令");
            else if(r!=null&&at<r.ReplyDueAt)Add(UnitCourier(d.Id),d.Destination,r.ReplyRoute,"戻りの伝令");
        }
        var x=w.NewMilitary?.ReplyRecovery;
        if(x!=null||w.NewMilitary?.CommandHolding!=null||w.NewMilitary?.Cancellation!=null||w.NewMilitary?.UnitDirective?.Holding!=null||w.NewMilitary?.UnitDirective?.Cancellation!=null||w.NewMilitary?.HeldDelivery!=null){
            foreach(var f in (x?.Flights??[]).Where(f=>f.At<=at&&at<f.DueAt))Add(f.Carrier,f.Origin,f.Route,"移動中の伝令");
            foreach(var f in w.NewMilitary?.CommandHolding?.Outcomes.Where(f=>f.Received&&f.At<=at&&at<f.DueAt)??[])Add(f.Carrier,f.Origin,f.Route,"戻りの伝令");
            foreach(var f in w.NewMilitary?.Cancellation?.Notices.Where(f=>f.At<=at&&at<f.DueAt)??[])Add(f.Carrier,f.Origin,f.Route,"戻りの伝令");
            foreach(var f in DirectiveAckFlights(w).Where(f=>f.At<=at&&at<f.DueAt))Add(f.Carrier,f.Origin,f.Route,"分隊原本の遅着返書伝令");
            foreach(var f in w.NewMilitary?.UnitDirective?.Cancellation?.Notices.Where(f=>f.At<=at&&at<f.DueAt)??[])Add(f.Carrier,f.Origin,f.Route,"分隊取消の効力通知伝令");
            foreach(var t in w.NewMilitary?.HeldDelivery?.Transfers.Where(t=>t.At<=at&&at<t.DueAt)??[])Add(t.Carrier,t.Origin,t.Route,"書状を運ぶ伝令");
            foreach(var group in HeldPapers(w,at).GroupBy(h=>h.Holder)){Need(group.Select(h=>h.Position).Distinct().Count()==1,"同じ保持者が異なる場所に存在します");var h=group.First();if(!frames.Any(f=>f.Carrier==h.Holder))Add(h.Holder,h.Position,[],"待機中の伝令",true);}
        }
        Need(frames.Select(f=>f.Carrier).Distinct().Count()==frames.Count,"同じ伝令が複数の経路に存在します");
        return frames.OrderBy(f=>f.Carrier.Value,StringComparer.Ordinal).ToArray();
    }
}
