namespace HakutaiKagami.Core.Contracts;
public sealed record LocalInspectionNeed(bool Needed,string Reason,string[] MissingEdges,string[] StaleEdges,EntityId[] PendingRequests);
// Decisions read the previous completed second, so later knowledge gained at the
// same clock value cannot rewrite the historical reason for an accepted offer.
public static class LocalWorkDemand
{
    public const string Policy="issuer-known-local-road-records-3600sec-no-duplicate-v1";
    public const long RecordAge=3600;
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.DemandPolicy==Policy;
    public static LocalInspectionNeed At(ContractWorld w,EntityId issuer,EntityId site,long at,long revision)
    {
        if(!Enabled(w))return new(true,"legacy-unrestricted",[],[],[]);
        string node=NewGameWorld.Node(site.Value);var edges=BranchRoutes.GraphEdges(w).Where(e=>(e.From==node||e.To==node)&&!NationalRoutes.Sea(e.Id)).OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray();
        if(edges.Length==0)return new(false,"unmapped",[],[],[]);
        var pending=w.Requests.Where(r=>r.CreatedAt<=at&&r.CommitSequence<=revision&&r.Intent.Actor==issuer&&r.Intent.Scope==site&&r.Intent.Action==ActionKind.Inspect).Where(r=>{var t=w.Tasks.Single(t=>t.RequestId==r.Id);string report=WorldSession.Child("message",t.Id,"report").Value;return !w.Messaging.Knowledge.Any(k=>k.CharacterId==issuer.Value&&k.MessageId==report&&k.LearnedAt<at)||w.Outbox.Where(o=>o.Task==t.Id&&o.Recipient==issuer&&o.Kind=="route-observation").Any(o=>!w.Messaging.Knowledge.Any(k=>k.CharacterId==issuer.Value&&k.MessageId==o.Id.Value&&k.LearnedAt<at));}).Select(r=>r.Id).OrderBy(id=>id.Value,StringComparer.Ordinal).ToArray();
        var records=w.Evidence.Where(e=>e.Observer==issuer&&e.ObservedAt!=null&&e.RecordedAt<at&&w.Knowledge.Any(k=>k.Character==issuer&&k.Evidence==e.Id&&k.LearnedAt<at)).ToArray();
        var missing=new List<string>();var stale=new List<string>();
        foreach(var edge in edges){var last=records.Where(e=>e.Claim=="route-open:"+edge.Id||e.Claim=="route-block:"+edge.Id).OrderByDescending(e=>e.ObservedAt).ThenByDescending(e=>e.RecordedAt).ThenBy(e=>e.Id.Value,StringComparer.Ordinal).FirstOrDefault();if(last==null)missing.Add(edge.Id);else if(at-last.ObservedAt!.Value>=RecordAge)stale.Add(edge.Id);}
        return new(pending.Length==0&&(missing.Count>0||stale.Count>0),pending.Length>0?"await-report":missing.Count>0?"missing-record":stale.Count>0?"old-record":"records-current",missing.ToArray(),stale.ToArray(),pending);
    }
    public static NearbyActor[] Candidates(ContractWorld w,bool requireNeed=true)
    {
        var site=w.Locations.Single(l=>l.Actor==w.PlayerControl);if(site.Kind!=LocationKind.AtSite)return [];
        var lord=NewGameService.View(w).Principal;
        return RegionalCampaign.Nearby(w,w.PlayerControl).OrderBy(p=>p.Actor==lord?0:1).Where(p=>w.Characters.Single(c=>c.Id==p.Actor).Life==LifeState.Adult&&w.Locations.Single(l=>l.Actor==p.Actor).Site==site.Site).Where(p=>!requireNeed||At(w,p.Actor,site.Site!.Value,w.ElapsedSeconds,w.CommitSequence).Needed).ToArray();
    }
}
