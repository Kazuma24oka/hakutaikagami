namespace HakutaiKagami.Core.Contracts;
public static partial class RegionalCampaign
{
    static NearbyActor[] NearbyNewGame(ContractWorld w,EntityId observer,RoutePoint own)
    {
        var moving=w.Branches!.Journeys.Select(j=>j.Actor).ToHashSet();
        var locations=w.Locations.ToDictionary(l=>l.Actor);
        var start=w.NewGame!.Start.People.ToDictionary(p=>p.Id,p=>p.Site);
        var sites=BranchRoutes.GraphNodes(w).ToDictionary(n=>Site(n.Id).Value,n=>n.Position);
        var result=new List<NearbyActor>();
        foreach(var c in w.Characters.Where(c=>c.Id!=observer))
        {
            NearbyActor row;
            // A new-game stationary person without a travel record is at the
            // validated initial site. Do not construct the same national graph
            // twice per person. Deployed player and all travelers keep the
            // authoritative existing sampling path.
            if(!moving.Contains(c.Id)&&HostageCustody.ReturnJourney(w,c.Id,w.ElapsedSeconds,w.CommitSequence+1)==null&&!(c.Id==w.PlayerControl&&NewGameMilitary.Deployed(w))){var p=sites[start[c.Id.Value]];row=new(c.Id,p,new(1,0),0,false,p,0);}
            else{var v=BranchRoutes.View(w,new(c.Id,w.CommitSequence,w.ElapsedSeconds,locations[c.Id],[],[],[]));row=new(c.Id,v.Position,v.Heading,v.DistanceMm,v.Walking,v.NextPosition,v.StepDistanceMm);}
            long range=BattlefieldRules.Enabled(w)?BattlefieldRules.SightRange(w,FieldWorld.FromWorld(own),w.ElapsedSeconds):80000;if(Math.Abs(row.Position.EastMm-own.EastMm)<=range&&Math.Abs(row.Position.NorthMm-own.NorthMm)<=range)result.Add(row);
        }
        return result.ToArray();
    }
}
