using HakutaiKagami.Core.Messaging;
using HakutaiKagami.Core.World;
namespace HakutaiKagami.Core.Contracts;

public sealed record CourierLeg(string Edge, RoutePoint From, RoutePoint To, long LengthMm, long StartAt, long EndAt);
public sealed record CourierMission(string Attempt, string Destination, long Appointment, RoutePoint Origin, CourierLeg[] Legs,
    string[] KnownBlocked, long ReadyAt, bool Obstructed=false);
public sealed record RoadCondition(long At,string Edge,bool Blocked);
public sealed record RoadSighting(EntityId Actor,string Edge,bool Blocked,long At,RoutePoint Position);
public sealed record CourierState(CourierMission[] Missions,RoadCondition[] Conditions,RoadSighting[] Sightings,string[] InitialBlocked);
public sealed record NearbyActor(EntityId Actor,RoutePoint Position,RoutePoint Heading,long DistanceMm,bool Walking,RoutePoint NextPosition,long StepDistanceMm);
// A sender can see dispatch plans, but live coordinates require a local sighting.
public sealed record CourierView(string Attempt,string Message,string Destination,long? DispatchedAt,long? PlannedAt,
    RoutePoint? Position,string Status,RoutePoint? Heading,long? DistanceMm,RoutePoint? NextPosition,long? StepDistanceMm);

public static class RegionalCampaign
{
    public const string Ruleset="owari-campaign-v1";
    static readonly RegionalBranchCatalog expansion=ReadExpansion();
    public static string Version{get;}=ContractJson.CanonicalHash(new[]{RegionalBranch.Version,ContractJson.CanonicalHash(Expansion),"physical-couriers-v1","known-address-retry-v1","scheduled-conditions-v1"});
    public static RegionalBranchCatalog Expansion=>ContractJson.Copy(expansion);
    static RegionalBranchCatalog ReadExpansion(){using var stream=typeof(RegionalCampaign).Assembly.GetManifestResourceStream("RegionalExpansionV1")??throw new InvalidDataException("Missing expansion");using var memory=new MemoryStream();stream.CopyTo(memory);return ContractJson.Read<RegionalBranchCatalog>(memory.ToArray());}
    public static BranchNode[] Nodes=>[..RegionalBranch.Nodes,..expansion.Nodes.Select(n=>{var p=JapanGeography.Project(new(n.Stop.LatitudeE7,n.Stop.LongitudeE7));return new BranchNode(n.Id,new(p.EastMm,p.NorthMm));})];
    public static BranchEdge[] Edges=>[..RegionalBranch.Edges,..expansion.Edges];
    public static SiteDefinition[] Sites=>[..RegionalBranch.Sites,..expansion.Nodes.Select(n=>new SiteDefinition(new("site:"+n.Id),n.Stop.Name,n.Stop.LatitudeE7,n.Stop.LongitudeE7,0))];
    public static EntityId Site(string node)=>node=="castle"?ContractFixture.Castle:node=="village"?ContractFixture.Village:new("site:"+node);
    public static string Destination(ContractWorld w,EntityId site)=>BranchRoutes.GraphNodes(w).Single(n=>Site(n.Id)==site).Id;
    public static Intent Move(ContractWorld w,string id,string destination,EntityId? executor=null){var i=RegionalBranch.Order(w,id,executor:executor);return i with{DestinationSite=Site(destination),WorkSeconds=checked((int)BranchRoutes.Duration(BranchRoutes.Plan("castle",destination,BranchRoutes.KnownBlocked(w,i.Executor),w:w),w:w))};}
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset||RiverRoads.Enabled(w)||HistoricalRoads.Enabled(w)||NationalRoutes.Enabled(w)||TravelWorld.Enabled(w);
    public static ContractWorld Create(RoadCondition[]? conditions=null,bool initiallyBlocked=true)
    {
        var w=RegionalControl.Create();w.WorldId=new("world:regional-campaign-v1");
        w.Definitions=w.Definitions with{Id=new("scenario:regional-campaign-v1"),Ruleset=Ruleset,Version=Version,Sites=Sites};
        w.Branches=w.Branches! with{BlockedEdges=initiallyBlocked?["pass"]:[]};
        w.Couriers=new([],conditions??[],[],w.Branches.BlockedEdges);
        var org=w.Definitions.Organizations[0].Id;w.Definitions=w.Definitions with{Armies=[new(new("army:officer"),org,ContractFixture.Officer),new(new("army:clerk"),org,ContractFixture.Clerk)]};
        w.Units=[new(new("unit:officer"),new("army:officer"),ContractFixture.Officer,org,100,0),new(new("unit:clerk"),new("army:clerk"),ContractFixture.Clerk,org,100,0)];ContractValidation.Validate(w);return w;
    }
    static RoutePoint Node(ContractWorld w,string id)=>BranchRoutes.GraphNodes(w).Single(n=>n.Id==id).Position;
    static long Seconds(long mm,int speed)=>Math.Max(1,checked((mm+speed-1)/speed));
    static RoutePoint ActorPoint(ContractWorld w,EntityId actor)=>BranchRoutes.View(w,new(actor,w.CommitSequence,w.ElapsedSeconds,w.Locations.Single(l=>l.Actor==actor),[],[],[])).Position;
    static CourierLeg[] FromNode(ContractWorld w,string from,string to,string[] blocked,long at)
    {
        var list=new List<CourierLeg>();
        foreach(var id in BranchRoutes.Plan(from,to,blocked,true,w,at)){
            var e=BranchRoutes.GraphEdges(w).Single(e=>e.Id==id);var next=e.From==from?e.To:e.From;
            var end=TravelWorld.End(w,e,at,true);list.Add(new(id,Node(w,from),Node(w,next),e.LengthMm,at,end));from=next;at=end;
        }return list.ToArray();
    }
    static CourierLeg[] Route(ContractWorld w,EntityId sender,string to,string[] blocked,long at,RoutePoint? overrideOrigin=null)
    {
        var point=overrideOrigin??ActorPoint(w,sender);var node=BranchRoutes.GraphNodes(w).SingleOrDefault(n=>n.Position==point);
        if(node!=null)return FromNode(w,node.Id,to,blocked,at);
        // A report sent along a road starts at its actual partial progress, never at a teleported endpoint.
        var j=w.Branches!.Journeys.Last(j=>j.Actor==sender);var task=w.Tasks.Single(t=>t.Id==j.Task);var time=task.FinishedAt??w.ElapsedSeconds;
        var leg=j.Legs.FirstOrDefault(l=>time<l.EndAt)??j.Legs[^1];var edge=BranchRoutes.GraphEdges(w).Single(e=>e.Id==leg.Edge);
        var progress=Math.Clamp(checked((time-TravelWorld.Departure(w,leg.Edge,leg.StartAt))*edge.FootSpeed),0,edge.LengthMm);
        var options=new List<CourierLeg[]>();
        foreach(var option in new[]{(leg.From,progress),(leg.To,edge.LengthMm-progress)})try{
            var end=checked(at+Seconds(option.Item2,edge.CourierSpeed));
            var head=new CourierLeg(edge.Id,point,Node(w,option.Item1),option.Item2,at,end);
            options.Add([head,..FromNode(w,option.Item1,to,blocked,end)]);
        }catch(InvalidDataException){}
        return options.OrderBy(p=>p[^1].EndAt).ThenBy(p=>string.Join('/',p.Select(l=>l.Edge)),StringComparer.Ordinal).FirstOrDefault()??throw new InvalidDataException("No known courier route");
    }
    static long End(CourierLeg[] legs,long sent)=>legs.Length==0?sent:legs[^1].EndAt;
    static CourierMission New(ContractWorld w,MessageAttempt attempt,string destination,long appointment,RoutePoint? origin=null)
    {
        var m=w.Messaging.Messages.Single(m=>m.Id==attempt.MessageId);var known=BranchRoutes.KnownBlocked(w,new(m.SenderId));
        CourierLeg[] legs;try{legs=Route(w,new(m.SenderId),destination,known,attempt.SentAt,origin);}catch(InvalidDataException){return new(attempt.Id,destination,appointment,origin??ActorPoint(w,new(m.SenderId)),[],known,attempt.SentAt+1,true);}
        return new(attempt.Id,destination,appointment,origin??ActorPoint(w,new(m.SenderId)),legs,known,Math.Max(attempt.SentAt+1,Math.Max(appointment,End(legs,attempt.SentAt))));
    }
    static void Put(ContractWorld w,CourierMission c)
    {
        w.Couriers=w.Couriers! with{Missions=w.Couriers.Missions.Where(x=>x.Attempt!=c.Attempt).Append(c).ToArray()};
        w.Messaging.Attempts=w.Messaging.Attempts.Select(a=>a.Id==c.Attempt?a with{DueAt=c.ReadyAt,FailOnArrival=a.FailOnArrival||c.Obstructed}:a).ToArray();
    }
    public static void Bind(ContractWorld w)
    {
        foreach(var a in w.Messaging.Attempts.Where(a=>!w.Couriers!.Missions.Any(c=>c.Attempt==a.Id)).ToArray()){
            var m=w.Messaging.Messages.Single(m=>m.Id==a.MessageId);string destination=TravelWorld.Enabled(w)&&m.SenderId==m.RecipientId?TravelWorld.SelfAddress(w,m.SenderId,a.SentAt):"castle";long appointment=a.SentAt+1;RoutePoint? origin=null;
            var t=w.Tasks.SingleOrDefault(t=>WorldSession.Child("message",t.Id,"order").Value==m.Id);
            if(t?.Action is ActionKind.Cancel or ActionKind.Redirect){var target=w.Tasks.Single(x=>x.Id==w.Requests.Single(r=>r.Id==t.RequestId).Intent.TargetTask);destination=Meeting(w,target);appointment=Appointment(w,target);}
            if(m.Kind=="delivery-failure")origin=Position(w,w.Couriers!.Missions.Single(c=>c.Attempt==m.Body),a.SentAt);
            Put(w,New(w,a,destination,appointment,origin));
        }
        Arrivals(w);
    }
    public static string Meeting(ContractWorld w,TaskState target){var r=w.Requests.Single(r=>r.Id==target.RequestId);return r.Intent.DestinationSite==new EntityId("site:inabayama")?"inabayama":"fork";}
    public static long Appointment(ContractWorld w,TaskState target){var r=w.Requests.Single(r=>r.Id==target.RequestId);return Meeting(w,target)=="fork"?MovementControl.Appointment(w,target):checked(Math.Max(r.CreatedAt+r.Intent.DeliverySeconds,r.Intent.EffectiveAt)+r.Intent.WorkSeconds);}
    static long ControlDelay(ContractWorld w,TaskState target)=>Math.Max(Appointment(w,target),w.ElapsedSeconds+BranchRoutes.Duration(BranchRoutes.Plan("castle",Meeting(w,target),BranchRoutes.KnownBlocked(w,ContractFixture.Lord),true,w),true,w))-w.ElapsedSeconds;
    public static Intent Control(ContractWorld w,string id,EntityId target,ActionKind action,bool important=true)
    {var t=w.Tasks.Single(t=>t.Id==target);return RegionalControl.Order(w,id,target,action,Math.Max(w.ElapsedSeconds,Appointment(w,t)),important) with{DeliverySeconds=checked((int)ControlDelay(w,t))};}
    public static void ValidateOrder(ContractWorld w,Intent i)
    {
        if(i.Action is not(ActionKind.Cancel or ActionKind.Redirect)){
            if(i.Action!=ActionKind.Move){RegionalBranch.ValidateOrder(w,i);return;}
            if(i.Actor!=ContractFixture.Lord||i.Executor==ContractFixture.Lord||i.Scope!=ContractFixture.Castle||i.DeliverySeconds!=1||i.ReportSeconds!=1||w.Locations.Single(l=>l.Actor==i.Executor).Site!=ContractFixture.Castle||i.DestinationSite==ContractFixture.Castle||
                i.WorkSeconds!=BranchRoutes.Duration(BranchRoutes.Plan("castle",Destination(w,i.DestinationSite!.Value),BranchRoutes.KnownBlocked(w,i.Executor),w:w),w:w))throw new InvalidDataException("Invalid regional destination");return;
        }
        var target=w.Tasks.Single(t=>t.Id==i.TargetTask);
        if(target.Action!=ActionKind.Move||i.Actor!=ContractFixture.Lord||i.Executor!=target.Executor||i.Scope!=target.Scope||i.WorkSeconds!=4||i.ReportSeconds!=1||
            i.DeliverySeconds!=ControlDelay(w,target)||
            i.Action==ActionKind.Redirect&&i.DestinationSite!=ContractFixture.Castle)throw new InvalidDataException("Invalid campaign control");
    }
    public static RoutePoint Position(ContractWorld w,CourierMission c,long at)
    {
        if(c.Legs.Length==0)return c.Origin;
        var leg=c.Legs.FirstOrDefault(l=>at<l.EndAt)??c.Legs[^1];var departure=TravelWorld.Departure(w,leg.Edge,leg.StartAt,leg.LengthMm==BranchRoutes.GraphEdges(w).Single(e=>e.Id==leg.Edge).LengthMm);var elapsed=Math.Clamp(at-departure,0,leg.EndAt-departure);
        var duration=leg.EndAt-departure;
        return new(leg.From.EastMm+(leg.To.EastMm-leg.From.EastMm)*elapsed/duration,leg.From.NorthMm+(leg.To.NorthMm-leg.From.NorthMm)*elapsed/duration);
    }
    public static long? NextBoundary(ContractWorld w,long target)=>w.Couriers!.Missions
        .Where(c=>w.Messaging.Attempts.Any(a=>a.Id==c.Attempt&&a.Status==AttemptStatus.InTransit))
        .SelectMany(c=>c.Legs.SelectMany(l=>new[]{l.StartAt,l.EndAt,TravelWorld.Departure(w,l.Edge,l.StartAt,l.LengthMm==BranchRoutes.GraphEdges(w).Single(e=>e.Id==l.Edge).LengthMm)}))
        .Concat(w.Couriers.Conditions.Select(c=>c.At)).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    public static void ApplyConditions(ContractWorld w)
    {
        foreach(var c in w.Couriers!.Conditions.Where(c=>c.At==w.ElapsedSeconds))
            w.Branches=w.Branches! with{BlockedEdges=c.Blocked?w.Branches.BlockedEdges.Append(c.Edge).Distinct().Order().ToArray():w.Branches.BlockedEdges.Where(e=>e!=c.Edge).ToArray()};
    }
    public static void Arrivals(ContractWorld w)
    {
        foreach(var old in w.Couriers!.Missions.ToArray()){
            var a=w.Messaging.Attempts.Single(a=>a.Id==old.Attempt);if(a.Status!=AttemptStatus.InTransit)continue;var c=old;
            // Truth is checked at an edge entrance only. Crossings already begun finish their edge.
            for(int guard=0;guard<=BranchRoutes.GraphEdges(w).Length;guard++){
                var index=Array.FindIndex(c.Legs,l=>l.StartAt==w.ElapsedSeconds);
                if(index<0||!w.Branches!.BlockedEdges.Contains(c.Legs[index].Edge))break;
                var leg=c.Legs[index];var n=BranchRoutes.GraphNodes(w).SingleOrDefault(n=>n.Position==leg.From);if(n==null)break;
                var known=c.KnownBlocked.Append(leg.Edge).Distinct().Order().ToArray();
                try{var tail=FromNode(w,n.Id,c.Destination,known,w.ElapsedSeconds);c=c with{Legs=[..c.Legs.Take(index),..tail],KnownBlocked=known,ReadyAt=Math.Max(w.ElapsedSeconds+1,Math.Max(c.Appointment,End(tail,w.ElapsedSeconds)))};Put(w,c);}
                catch(InvalidDataException){c=c with{Legs=c.Legs.Take(index).ToArray(),KnownBlocked=known,Obstructed=true,ReadyAt=w.ElapsedSeconds+1};w.Couriers=w.Couriers! with{Missions=w.Couriers.Missions.Select(x=>x.Attempt==c.Attempt?c:x).ToArray()};w.Messaging.Attempts=w.Messaging.Attempts.Select(x=>x.Id==a.Id?x with{DueAt=c.ReadyAt,FailOnArrival=true}:x).ToArray();break;}
            }
            a=w.Messaging.Attempts.Single(a=>a.Id==old.Attempt);if(a.DueAt!=w.ElapsedSeconds)continue;
            var m=w.Messaging.Messages.Single(m=>m.Id==a.MessageId);
            if(ActorPoint(w,new(a.ReceiverId))!=Position(w,c,w.ElapsedSeconds))w.Messaging.Attempts=w.Messaging.Attempts.Select(x=>x.Id==a.Id?x with{FailOnArrival=true}:x).ToArray();
        }
    }
    public static void Failures(ContractWorld w)
    {
        foreach(var a in w.Messaging.Attempts.Where(a=>a.FailureAt==w.ElapsedSeconds).ToArray()){
            var m=w.Messaging.Messages.Single(m=>m.Id==a.MessageId);if(m.Kind=="delivery-failure")continue;
            var id="message:courier-failure/"+a.Id.Replace(':','/');
            MessageFlow.ReportFailure(w.Messaging,id,a.Id,m.SenderId,w.ElapsedSeconds,1);
        }
    }
    public static void Retry(ContractWorld w,EntityId actor,string message,string destination,long appointment)
    {
        if(!Enabled(w))throw new InvalidOperationException("Regional campaign required");var m=w.Messaging.Messages.Single(m=>m.Id==message);
        if(m.SenderId!=actor.Value||!BranchRoutes.GraphNodes(w).Any(n=>n.Id==destination)||appointment<w.ElapsedSeconds)throw new InvalidDataException("Unknown delivery address");
        // A destination is selected from the original known mission; it is never inferred from recipient truth.
        var task=w.Tasks.Single(t=>WorldSession.Child("message",t.Id,"order").Value==message);
        if(task.Action is not(ActionKind.Cancel or ActionKind.Redirect))throw new InvalidDataException("Unsupported retry address");
        var target=w.Tasks.Single(t=>t.Id==w.Requests.Single(r=>r.Id==task.RequestId).Intent.TargetTask);var goal=Destination(w,w.Requests.Single(r=>r.Id==target.RequestId).Intent.DestinationSite!.Value);
        if(destination!="castle"&&destination!=Meeting(w,target)&&destination!=goal)throw new InvalidDataException("Address is not in the known original mission");
        MessageFlow.Retry(w.Messaging,message,actor.Value,w.ElapsedSeconds,1);
        var a=MessageFlow.Latest(w.Messaging,message);Put(w,New(w,a,destination,appointment));
    }
    static long LegDistance(ContractWorld w,CourierLeg l,long at){var departure=TravelWorld.Departure(w,l.Edge,l.StartAt,l.LengthMm==BranchRoutes.GraphEdges(w).Single(e=>e.Id==l.Edge).LengthMm);return at>=l.EndAt?l.LengthMm:at<=departure?0:l.LengthMm*(at-departure)/(l.EndAt-departure);}
    public static CourierView[] Views(ContractWorld w,EntityId observer)
    {
        if(!Enabled(w))return [];var own=ActorPoint(w,observer);
        return w.Couriers!.Missions.Select(c=>{
            var a=w.Messaging.Attempts.Single(a=>a.Id==c.Attempt);var m=w.Messaging.Messages.Single(m=>m.Id==a.MessageId);var p=Position(w,c,w.ElapsedSeconds);
            // Local 80m sight radius is a prototype coefficient, not global sender telemetry.
            bool seen=Math.Abs(p.EastMm-own.EastMm)<=80000&&Math.Abs(p.NorthMm-own.NorthMm)<=80000&&a.Status==AttemptStatus.InTransit;
            if(m.Kind=="delivery-failure"&&!seen&&!MessageFlow.Knows(w.Messaging,m.Id,observer.Value))return null;
            if(m.SenderId!=observer.Value&&!seen&&!MessageFlow.Knows(w.Messaging,m.Id,observer.Value))return null;
            var status=MessageFlow.Knows(w.Messaging,m.Id,observer.Value)?"受領済":seen?(w.ElapsedSeconds<End(c.Legs,a.SentAt)?"移動を目撃":"待機を目撃"):MessageFlow.FailureKnown(w.Messaging,a.Id,observer.Value)?"失敗報告を受領":"発送済・結果未確認";
            var leg=c.Legs.FirstOrDefault(l=>w.ElapsedSeconds<l.EndAt)??c.Legs.LastOrDefault();
            if(seen&&leg!=null&&w.ElapsedSeconds<TravelWorld.Departure(w,leg.Edge,leg.StartAt,leg.LengthMm==BranchRoutes.GraphEdges(w).Single(e=>e.Id==leg.Edge).LengthMm))status="出航待ちを目撃";
            var distance=c.Legs.Sum(l=>LegDistance(w,l,w.ElapsedSeconds));
            var nextDistance=c.Legs.Sum(l=>LegDistance(w,l,w.ElapsedSeconds+1));
            bool informed=MessageFlow.Knows(w.Messaging,m.Id,observer.Value)||m.SenderId==observer.Value&&m.Kind!="delivery-failure";
            var opaque=ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(c.Attempt));
            return new CourierView(informed?c.Attempt:"courier:seen/"+opaque,informed?m.Id:"message:unread/"+opaque,informed?c.Destination:"届け先未確認",informed?a.SentAt:null,informed?Math.Max(c.Appointment,a.SentAt+1):null,seen?p:null,status,seen&&leg!=null?new(leg.To.EastMm-leg.From.EastMm,leg.To.NorthMm-leg.From.NorthMm):null,seen?(informed?distance:checked(w.ElapsedSeconds*2000)):null,seen?Position(w,c,w.ElapsedSeconds+1):null,seen?nextDistance-distance:null);
        }).OfType<CourierView>().ToArray();
    }
    public static NearbyActor[] Nearby(ContractWorld w,EntityId observer)
    {
        if(!Enabled(w))throw new InvalidOperationException("Regional campaign required");var own=ActorPoint(w,observer);
        return w.Characters.Where(c=>c.Id!=observer).Select(c=>BranchRoutes.View(w,new(c.Id,w.CommitSequence,w.ElapsedSeconds,w.Locations.Single(l=>l.Actor==c.Id),[],[],[])))
            .Where(v=>Math.Abs(v.Position.EastMm-own.EastMm)<=80000&&Math.Abs(v.Position.NorthMm-own.NorthMm)<=80000)
            .Select(v=>new NearbyActor(v.Knowledge.Observer,v.Position,v.Heading,v.DistanceMm,v.Walking,v.NextPosition,v.StepDistanceMm)).ToArray();
    }
    public static void InspectRoad(ContractWorld w,EntityId actor,string edge)
    {
        if(!Enabled(w))throw new InvalidOperationException("Regional campaign required");var e=BranchRoutes.GraphEdges(w).Single(e=>e.Id==edge);var p=ActorPoint(w,actor);
        if(p!=Node(w,e.From)&&p!=Node(w,e.To))throw new InvalidOperationException("Road inspection requires presence at an endpoint");
        bool blocked=w.Branches!.BlockedEdges.Contains(edge);var id=new EntityId("evidence:road/"+actor.Value.Replace(':','/')+"/"+edge+"/"+w.ElapsedSeconds);
        if(w.Evidence.Any(x=>x.Id==id))return;
        w.Couriers=w.Couriers! with{Sightings=[..w.Couriers.Sightings,new(actor,edge,blocked,w.ElapsedSeconds,p)]};
        var claim=(blocked?"route-block:":"route-open:")+edge;
        w.Evidence=[..w.Evidence,new(id,actor,actor,claim,w.ElapsedSeconds,w.ElapsedSeconds,null,null)];w.Knowledge=[..w.Knowledge,new(actor,id,w.ElapsedSeconds)];
        if(actor!=ContractFixture.Lord)MessageFlow.Send(w.Messaging,new("message:"+id.Value[9..],actor.Value,"route-observation",actor.Value,ContractFixture.Lord.Value,claim,ContractFixture.Castle.Value,w.ElapsedSeconds),w.ElapsedSeconds,1);
    }
    public static RoutePoint ActorPointAt(ContractWorld w,EntityId actor,long at)
    {
        var j=w.Branches!.Journeys.LastOrDefault(j=>j.Actor==actor&&j.Legs[0].StartAt<=at);if(j==null)return Node(w,TravelWorld.Enabled(w)?TravelWorld.InitialNode(actor):"castle");
        var task=w.Tasks.Single(t=>t.Id==j.Task);var time=Math.Min(at,task.FinishedAt??at);var leg=j.Legs.FirstOrDefault(l=>time<l.EndAt)??j.Legs[^1];var edge=BranchRoutes.GraphEdges(w).Single(e=>e.Id==leg.Edge);
        var progress=Math.Clamp((time-TravelWorld.Departure(w,leg.Edge,leg.StartAt))*edge.FootSpeed,0,edge.LengthMm);var a=Node(w,leg.From);var b=Node(w,leg.To);
        return new(a.EastMm+(b.EastMm-a.EastMm)*progress/edge.LengthMm,a.NorthMm+(b.NorthMm-a.NorthMm)*progress/edge.LengthMm);
    }
    public static bool BlockedAt(ContractWorld w,string edge,long at)
    {
        var last=w.Couriers!.Conditions.Where(c=>c.Edge==edge&&c.At<=at).OrderByDescending(c=>c.At).FirstOrDefault();
        return last?.Blocked??w.Couriers!.InitialBlocked.Contains(edge);
    }
    public static void Validate(ContractWorld w)
    {
        if(w.Couriers==null)throw new InvalidDataException("Missing physical courier module");
        var cs=w.Couriers;var edges=BranchRoutes.GraphEdges(w);
        if(cs.InitialBlocked.Distinct().Count()!=cs.InitialBlocked.Length||cs.InitialBlocked.Any(id=>!edges.Any(e=>e.Id==id))||!w.Branches!.BlockedEdges.ToHashSet().SetEquals(edges.Where(e=>BlockedAt(w,e.Id,w.ElapsedSeconds)).Select(e=>e.Id)))throw new InvalidDataException("Road truth differs from recorded conditions");
        if(cs.Conditions.Any(c=>c.At<1||!edges.Any(e=>e.Id==c.Edge))||cs.Conditions.Select(c=>(c.At,c.Edge)).Distinct().Count()!=cs.Conditions.Length)throw new InvalidDataException("Invalid condition schedule");
        foreach(var sight in cs.Sightings){var edge=edges.Single(e=>e.Id==sight.Edge);if(sight.At>w.ElapsedSeconds||sight.Position!=ActorPointAt(w,sight.Actor,sight.At)||sight.Blocked!=BlockedAt(w,edge.Id,sight.At)||sight.Position!=Node(w,edge.From)&&sight.Position!=Node(w,edge.To))throw new InvalidDataException("Invalid road sighting");}
        if(cs.Missions.Select(c=>c.Attempt).Distinct().Count()!=cs.Missions.Length||cs.Missions.Length!=w.Messaging.Attempts.Length)throw new InvalidDataException("Missing/duplicate courier");
        foreach(var c in cs.Missions){var a=w.Messaging.Attempts.Single(a=>a.Id==c.Attempt);var message=w.Messaging.Messages.Single(m=>m.Id==a.MessageId);
            var origin=message.Kind=="delivery-failure"?Position(w,cs.Missions.Single(x=>x.Attempt==message.Body),a.SentAt):ActorPointAt(w,new(message.SenderId),a.SentAt);
            if(c.Origin!=origin)throw new InvalidDataException("Courier origin lacks physical dispatch");
            if(message.Kind=="contract-order"){
                var task=w.Tasks.Single(t=>WorldSession.Child("message",t.Id,"order").Value==message.Id);
                if(task.Action is ActionKind.Cancel or ActionKind.Redirect){var target=w.Tasks.Single(t=>t.Id==w.Requests.Single(r=>r.Id==task.RequestId).Intent.TargetTask);var goal=Destination(w,w.Requests.Single(r=>r.Id==target.RequestId).Intent.DestinationSite!.Value);
                    if(a.Number==1&&(c.Destination!=Meeting(w,target)||c.Appointment!=Appointment(w,target))||a.Number>1&&(c.Appointment<a.SentAt||c.Destination!="castle"&&c.Destination!=Meeting(w,target)&&c.Destination!=goal))throw new InvalidDataException("Courier address is not justified by original known mission");}
                else if(c.Destination!=(TravelWorld.Enabled(w)&&message.SenderId==message.RecipientId?TravelWorld.SelfAddress(w,message.SenderId,a.SentAt):"castle")||c.Appointment!=a.SentAt+1)throw new InvalidDataException("Ordinary order left its original address");
            }
            if(a.LateDueAt!=null)throw new InvalidDataException("Recovered late copies require a registered physical recovery route; timer-only late arrival is not supported");
            if(!BranchRoutes.GraphNodes(w).Any(n=>n.Id==c.Destination)||c.ReadyAt!=a.DueAt||c.Appointment<0||c.KnownBlocked.Any(id=>!edges.Any(e=>e.Id==id)))throw new InvalidDataException("Invalid courier binding");
            for(int i=0;i<c.Legs.Length;i++){var l=c.Legs[i];var edge=edges.Single(e=>e.Id==l.Edge);
                if(l.LengthMm<0||l.LengthMm>edge.LengthMm||l.EndAt!=TravelWorld.End(w,edge,l.StartAt,true,l.LengthMm)||l.StartAt<0||i==0&&(l.StartAt!=a.SentAt||l.From!=c.Origin)||i>0&&(l.StartAt!=c.Legs[i-1].EndAt||l.From!=c.Legs[i-1].To))throw new InvalidDataException("Invalid courier chronology");
                var ea=Node(w,edge.From);var eb=Node(w,edge.To);if(l.To!=ea&&l.To!=eb||i>0&&(l.From!=ea&&l.From!=eb||l.LengthMm!=edge.LengthMm))throw new InvalidDataException("Courier left its road");
                if(i==0){decimal dx=eb.EastMm-ea.EastMm,dy=eb.NorthMm-ea.NorthMm,x=l.From.EastMm-ea.EastMm,y=l.From.NorthMm-ea.NorthMm;
                    var f=(x*dx+y*dy)/(dx*dx+dy*dy);var length=(l.To==ea?f:1-f)*edge.LengthMm;
                    if(f<0||f>1||Math.Abs(x*dy-y*dx)>Math.Abs(dx)+Math.Abs(dy)||Math.Abs(length-l.LengthMm)>3)throw new InvalidDataException("Invalid courier partial-road distance");}
            }
            if(!c.Obstructed&&(c.ReadyAt<End(c.Legs,a.SentAt)||c.ReadyAt<c.Appointment||c.Legs.Length>0&&c.Legs[^1].To!=Node(w,c.Destination)))throw new InvalidDataException("Delivery before physical arrival");
            if(a.Status==AttemptStatus.Delivered&&a.FinishedAt<c.ReadyAt)throw new InvalidDataException("Early courier delivery");
        }
    }
}
