namespace HakutaiKagami.Core.Contracts;
public sealed record TrafficExample(string Id,RoadCondition[] Conditions,long At,long ReopensAt,string Goal,string Node);
// Synthetic test schedules only; not historical incidents, never a source for personal route planning.
public static class TrafficExamples
{
 public static TrafficExample Build(string id){if(id is not("road" or "detour" or "port" or "sailed"))throw new ArgumentException("Unknown traffic example");var s=new WorldSession(TrafficWorld.Create());var goal=id is "road" or "detour"?"sakai":"sumoto";s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:fixture",goal),s.Revision);s.AdvanceTo(1);long at;string node;string[] edges;
 if(id is "road" or "detour"){s.AdvanceTo(100);var d=TravelControl.Prepare(s.DiagnosticSnapshot(),TravelControl.Request(s.DiagnosticSnapshot(),"request:fixture-decision","edo"));at=d.EffectiveAt;node=d.Node;edges=id=="road"?BranchRoutes.GraphEdges(s.DiagnosticSnapshot()).Where(e=>e.From==node||e.To==node).Select(e=>e.Id).ToArray():[d.After[d.Before.TakeWhile(l=>l.EndAt<=at).Count()].Edge];goal="edo";}
 else{var l=s.DiagnosticSnapshot().Branches!.Journeys.Single().Legs.First(l=>NationalRoutes.Sea(l.Edge));at=TravelWorld.Departure(s.DiagnosticSnapshot(),l.Edge,l.StartAt)+(id=="sailed"?1:0);node=l.From;edges=[l.Edge];}
 return new(id,edges.SelectMany(e=>new[]{new RoadCondition(at,e,true),new RoadCondition(at+600,e,false)}).ToArray(),at,at+600,goal,node);
 }
}
