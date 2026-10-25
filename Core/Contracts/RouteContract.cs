namespace HakutaiKagami.Core.Contracts;

public sealed record RoutePoint(long EastMm,long NorthMm);
public sealed record RouteLeg(string Id,RoutePoint From,RoutePoint To,long LengthMm,int FootMmPerSecond,int CourierMmPerSecond);
public sealed record RouteRules(RouteLeg[] Legs,int HandlingSeconds,int WorkSeconds,string Algorithm);
public sealed record RouteActorView(ActorView Knowledge,RoutePoint Position,long DistanceMm,bool Walking,long? ArrivalAt);

// Registered synthetic geography. Version is a hash of every geometric/timing input,
// saved in DefinitionPack so a changed catalog cannot silently reinterpret a save.
public static class RouteContract
{
    public const string Ruleset="common-route-fixture-v1";
    public static RouteLeg[] Legs => [new("road",new(0,0),new(40000,0),40000,2000,4000),
        new("pass",new(40000,0),new(40000,80000),80000,1000,2000)];
    public static string Version { get; }=ContractJson.CanonicalHash(new RouteRules(Legs,1,4,"corridor-integer-ceiling-v1"));
    public static long Length=>Legs.Sum(l=>l.LengthMm);
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset||RegionalRoute.Enabled(w);
    public static RouteLeg[] Geometry(ContractWorld? w=null)=>w!=null&&RegionalRoute.Enabled(w)?RegionalRoute.Legs:Legs;
    public static long Total(ContractWorld? w=null)=>Geometry(w).Sum(l=>l.LengthMm);
    public static string Revision(ContractWorld? w=null)=>w!=null&&RegionalRoute.Enabled(w)?RegionalRoute.Version:Version;
    public static ContractWorld Create()
    {
        var w=ContractFixture.Create();w.WorldId=new("world:route-fixture");
        w.Definitions=w.Definitions with{Id=new("scenario:route-fixture"),Ruleset=Ruleset,Version=Version};
        ContractValidation.Validate(w);return w;
    }
    public static long Offset(LocationState l,ContractWorld? w=null)=>l.Kind==LocationKind.AtSite?
        l.Site==ContractFixture.Castle?0:l.Site==ContractFixture.Village?Total(w):throw new InvalidDataException("Unknown route site"):
        l.Kind==LocationKind.Traveling&&l.TerrainRevision==Revision(w)?l.ProgressMm:throw new InvalidDataException("Unsupported route location");
    public static RoutePoint Point(long offset,ContractWorld? w=null)=>Point(offset,Geometry(w));
    public static RoutePoint Point(long offset,RouteLeg[] legs)
    {
        if(offset<0||offset>legs.Sum(l=>l.LengthMm))throw new InvalidDataException("Route progress out of range");
        foreach(var leg in legs){if(offset<=leg.LengthMm)return new(leg.From.EastMm+(leg.To.EastMm-leg.From.EastMm)*offset/leg.LengthMm,
            leg.From.NorthMm+(leg.To.NorthMm-leg.From.NorthMm)*offset/leg.LengthMm);offset-=leg.LengthMm;}
        throw new InvalidDataException("Route has no endpoint");
    }
    public static int Travel(long from,long to,bool courier,ContractWorld? w=null)
    {
        if(from<0||to<0||from>Total(w)||to>Total(w))throw new InvalidDataException("Invalid route endpoints");
        long low=Math.Min(from,to),high=Math.Max(from,to),start=0,total=0;
        foreach(var leg in Geometry(w)){var distance=Math.Max(0,Math.Min(high,start+leg.LengthMm)-Math.Max(low,start));
            var speed=courier?leg.CourierMmPerSecond:leg.FootMmPerSecond;
            total=checked(total+(distance+speed-1)/speed);start+=leg.LengthMm;}
        return checked((int)Math.Max(1,total));
    }
    public static long DistanceAt(long elapsed,ContractWorld? w=null)
    {
        if(elapsed<0)throw new ArgumentOutOfRangeException(nameof(elapsed));
        long distance=0;
        foreach(var leg in Geometry(w)){long duration=(leg.LengthMm+leg.FootMmPerSecond-1)/leg.FootMmPerSecond;
            if(elapsed<duration)return distance+Math.Min(leg.LengthMm,checked(elapsed*leg.FootMmPerSecond));
            elapsed-=duration;distance+=leg.LengthMm;}
        return distance;
    }
    public static int Segment(long offset,ContractWorld? w=null)=>Segment(offset,Geometry(w));
    public static int Segment(long offset,RouteLeg[] legs){for(int i=0;i<legs.Length-1;i++){if(offset<legs[i].LengthMm)return i;offset-=legs[i].LengthMm;}return legs.Length-1;}
    public static int Courier(ContractWorld w,EntityId sender,EntityId receiver)=>Travel(
        Offset(w.Locations.Single(l=>l.Actor==sender),w),Offset(w.Locations.Single(l=>l.Actor==receiver),w),true,w);
    public static Intent Order(ContractWorld w,string id,ActionKind action=ActionKind.Move,EntityId? executor=null)
    {
        var i=ContractFixture.Order(id,action,action==ActionKind.Transfer?40:0,executor:executor,deadline:RegionalRoute.Enabled(w)?1000000:1000);
        return i with{DeliverySeconds=Courier(w,i.Actor,i.Executor),WorkSeconds=action==ActionKind.Move?Travel(0,Total(w),false,w):4,ReportSeconds=1};
    }
    public static void Validate(ContractWorld w)
    {
        if(RegionalRoute.Enabled(w))RegionalRoute.ValidateSites(w);
        if(w.Definitions.Version!=Revision(w)||w.Definitions.Id!=new EntityId(RegionalRoute.Enabled(w)?"scenario:owari-route-v1":"scenario:route-fixture")||
            (!RegionalRoute.Enabled(w)&&(w.Definitions.Sites.Length!=2||!w.Definitions.Sites.Select(s=>s.Id).ToHashSet().SetEquals(new[]{ContractFixture.Castle,ContractFixture.Village}))))
            throw new InvalidDataException("Route dependency changed; explicit migration required");
        if(w.Locations.Single(l=>l.Actor==ContractFixture.Lord).Site!=ContractFixture.Castle)throw new InvalidDataException("Fixture issuer address must remain at castle");
        foreach(var r in w.Requests){var i=r.Intent;
            if(i.Actor!=ContractFixture.Lord||i.Executor==ContractFixture.Lord||i.ReportSeconds!=1||
                i.WorkSeconds!=(i.Action==ActionKind.Move?Travel(0,Total(w),false,w):4)||
                i.Action==ActionKind.Move&&(i.Scope!=ContractFixture.Castle||i.DestinationSite!=ContractFixture.Village))throw new InvalidDataException("Route request rules mismatch");}
        foreach(var a in w.Actions){var task=w.Tasks.Single(t=>t.Id==a.Task);
            if(a.Until-a.StartedAt!=(task.Action==ActionKind.Move?Travel(0,Total(w),false,w):4)||
                a.Phase==ExecutionPhase.Running&&a.Progress!=w.ElapsedSeconds-a.StartedAt)throw new InvalidDataException("Route action timing mismatch");}
        foreach(var l in w.Locations){var offset=Offset(l,w);_ = Point(offset,w);
            if(l.Kind==LocationKind.Traveling){var t=w.Tasks.SingleOrDefault(t=>l.Route==WorldSession.Child("route",t.Id,"corridor"));
                if(t==null||t.Executor!=l.Actor||t.Action!=ActionKind.Move||l.SegmentIndex!=Segment(offset,w))throw new InvalidDataException("Route ownership/segment mismatch");
                var a=w.Actions.Single(a=>a.Task==t.Id);
                if(offset!=DistanceAt(a.Progress,w)||a.Until-a.StartedAt!=Travel(0,Total(w),false,w))throw new InvalidDataException("Route time/distance mismatch");}}
    }
    public static void ValidateOrder(ContractWorld w,Intent i)
    {
        // Fixed addresses; never restart a regional trip from the destination as if it were at the origin.
        if(RegionalRoute.Enabled(w)&&i.Action==ActionKind.Move&&w.Locations.Single(l=>l.Actor==i.Executor).Site!=ContractFixture.Castle)throw new InvalidDataException("Regional departure must be at Kiyosu");
        if(i.Actor!=ContractFixture.Lord||i.Executor==ContractFixture.Lord||w.Locations.Single(l=>l.Actor==i.Actor).Kind!=LocationKind.AtSite||
            w.Locations.Single(l=>l.Actor==i.Executor).Kind!=LocationKind.AtSite||
            i.DeliverySeconds!=Courier(w,i.Actor,i.Executor)||i.ReportSeconds!=1||
            i.WorkSeconds!=(i.Action==ActionKind.Move?Travel(0,Total(w),false,w):4)||
            i.Action==ActionKind.Move&&(i.Scope!=ContractFixture.Castle||i.DestinationSite!=ContractFixture.Village))
            throw new InvalidDataException("Timing/route must come from registered rules");
    }
}
