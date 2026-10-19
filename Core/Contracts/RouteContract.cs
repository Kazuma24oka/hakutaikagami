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
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    public static ContractWorld Create()
    {
        var w=ContractFixture.Create();w.WorldId=new("world:route-fixture");
        w.Definitions=w.Definitions with{Id=new("scenario:route-fixture"),Ruleset=Ruleset,Version=Version};
        ContractValidation.Validate(w);return w;
    }
    public static long Offset(LocationState l)=>l.Kind==LocationKind.AtSite?
        l.Site==ContractFixture.Castle?0:l.Site==ContractFixture.Village?Length:throw new InvalidDataException("Unknown route site"):
        l.Kind==LocationKind.Traveling&&l.TerrainRevision==Version?l.ProgressMm:throw new InvalidDataException("Unsupported route location");
    public static RoutePoint Point(long offset)
    {
        if(offset<0||offset>Length)throw new InvalidDataException("Route progress out of range");
        foreach(var leg in Legs){if(offset<=leg.LengthMm)return new(leg.From.EastMm+(leg.To.EastMm-leg.From.EastMm)*offset/leg.LengthMm,
            leg.From.NorthMm+(leg.To.NorthMm-leg.From.NorthMm)*offset/leg.LengthMm);offset-=leg.LengthMm;}
        throw new InvalidDataException("Route has no endpoint");
    }
    public static int Travel(long from,long to,bool courier)
    {
        if(from<0||to<0||from>Length||to>Length)throw new InvalidDataException("Invalid route endpoints");
        long low=Math.Min(from,to),high=Math.Max(from,to),start=0,total=0;
        foreach(var leg in Legs){var distance=Math.Max(0,Math.Min(high,start+leg.LengthMm)-Math.Max(low,start));
            var speed=courier?leg.CourierMmPerSecond:leg.FootMmPerSecond;
            total=checked(total+(distance+speed-1)/speed);start+=leg.LengthMm;}
        return checked((int)Math.Max(1,total));
    }
    public static long DistanceAt(long elapsed)
    {
        if(elapsed<0)throw new ArgumentOutOfRangeException(nameof(elapsed));
        long distance=0;
        foreach(var leg in Legs){long duration=(leg.LengthMm+leg.FootMmPerSecond-1)/leg.FootMmPerSecond;
            if(elapsed<duration)return distance+Math.Min(leg.LengthMm,checked(elapsed*leg.FootMmPerSecond));
            elapsed-=duration;distance+=leg.LengthMm;}
        return distance;
    }
    public static int Segment(long offset)=>offset<Legs[0].LengthMm?0:1;
    public static int Courier(ContractWorld w,EntityId sender,EntityId receiver)=>Travel(
        Offset(w.Locations.Single(l=>l.Actor==sender)),Offset(w.Locations.Single(l=>l.Actor==receiver)),true);
    public static Intent Order(ContractWorld w,string id,ActionKind action=ActionKind.Move,EntityId? executor=null)
    {
        var i=ContractFixture.Order(id,action,action==ActionKind.Transfer?40:0,executor:executor,deadline:1000);
        return i with{DeliverySeconds=Courier(w,i.Actor,i.Executor),WorkSeconds=action==ActionKind.Move?Travel(0,Length,false):4,ReportSeconds=1};
    }
    public static void Validate(ContractWorld w)
    {
        if(w.Definitions.Version!=Version||w.Definitions.Id!=new EntityId("scenario:route-fixture")||
            w.Definitions.Sites.Length!=2||!w.Definitions.Sites.Select(s=>s.Id).ToHashSet().SetEquals(new[]{ContractFixture.Castle,ContractFixture.Village}))
            throw new InvalidDataException("Route dependency changed; explicit migration required");
        if(w.Locations.Single(l=>l.Actor==ContractFixture.Lord).Site!=ContractFixture.Castle)throw new InvalidDataException("Fixture issuer address must remain at castle");
        foreach(var r in w.Requests){var i=r.Intent;
            if(i.Actor!=ContractFixture.Lord||i.Executor==ContractFixture.Lord||i.ReportSeconds!=1||
                i.WorkSeconds!=(i.Action==ActionKind.Move?Travel(0,Length,false):4)||
                i.Action==ActionKind.Move&&(i.Scope!=ContractFixture.Castle||i.DestinationSite!=ContractFixture.Village))throw new InvalidDataException("Route request rules mismatch");}
        foreach(var a in w.Actions){var task=w.Tasks.Single(t=>t.Id==a.Task);
            if(a.Until-a.StartedAt!=(task.Action==ActionKind.Move?Travel(0,Length,false):4)||
                a.Phase==ExecutionPhase.Running&&a.Progress!=w.ElapsedSeconds-a.StartedAt)throw new InvalidDataException("Route action timing mismatch");}
        foreach(var l in w.Locations){var offset=Offset(l);_ = Point(offset);
            if(l.Kind==LocationKind.Traveling){var t=w.Tasks.SingleOrDefault(t=>l.Route==WorldSession.Child("route",t.Id,"corridor"));
                if(t==null||t.Executor!=l.Actor||t.Action!=ActionKind.Move||l.SegmentIndex!=Segment(offset))throw new InvalidDataException("Route ownership/segment mismatch");
                var a=w.Actions.Single(a=>a.Task==t.Id);
                if(offset!=DistanceAt(a.Progress)||a.Until-a.StartedAt!=Travel(0,Length,false))throw new InvalidDataException("Route time/distance mismatch");}}
    }
    public static void ValidateOrder(ContractWorld w,Intent i)
    {
        // Addresses are a fixed site for this first corridor fixture; interception is not implemented.
        if(i.Actor!=ContractFixture.Lord||i.Executor==ContractFixture.Lord||w.Locations.Single(l=>l.Actor==i.Actor).Kind!=LocationKind.AtSite||
            w.Locations.Single(l=>l.Actor==i.Executor).Kind!=LocationKind.AtSite||
            i.DeliverySeconds!=Courier(w,i.Actor,i.Executor)||i.ReportSeconds!=1||
            i.WorkSeconds!=(i.Action==ActionKind.Move?Travel(0,Length,false):4)||
            i.Action==ActionKind.Move&&(i.Scope!=ContractFixture.Castle||i.DestinationSite!=ContractFixture.Village))
            throw new InvalidDataException("Timing/route must come from registered rules");
    }
}
