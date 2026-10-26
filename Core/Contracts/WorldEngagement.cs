namespace HakutaiKagami.Core.Contracts;

// Authoritative simulation input only. Never pass this world-truth snapshot to actor UI.
public sealed record WorldEngagementSnapshot(long At,EngagementUnit[] Units,EngagementMandate[] Mandates,HostilityInterval[] Hostilities);
public static class WorldEngagement
{
    public const string Contract="actual-owned-armies-common-position-scoped-orders-v1";
    public static WorldEngagementSnapshot Snapshot(ContractWorld w)
    {
        if(!NpcArmies.Enabled(w))throw new InvalidDataException("実在軍の明示版が必要です");
        long at=w.ElapsedSeconds;var units=new List<EngagementUnit>();var mandates=new List<EngagementMandate>();
        if(w.NewMilitary is {} own&&NewGameMilitary.Deployed(w)){
            var reports=NewGameMilitary.OwnCombatReport(w,at);
            for(int i=0;i<3;i++){
                var id=NewGameMilitary.UnitIdsFor(w)[i];var u=w.Units.Single(u=>u.Id==id);var p=NewGameMilitary.Position(w,i,at);
                var order=own.Orders.LastOrDefault(o=>o.At<=at&&(o.AffectedUnits==null||o.AffectedUnits.Contains(i)));
                string phase=order?.Action==NewMilitaryAction.Recall?"withdrawing":reports[i].Morale<=25?"routed":"standing";
                units.Add(new(u,p,ContinuousWorld.Height(p),reports[i].Morale,phase,EngagementIntent.SelfDefense,w.Characters.Single(c=>c.Id==u.Commander).WillObey));
            }
            mandates.AddRange(NewGameMilitary.EngagementMandates(w,at));
            foreach(var id in mandates.Where(m=>m.ReceivedAt<=at&&m.EffectiveFrom<=at&&at<m.Until&&(m.CancelledAt==null||at<m.CancelledAt)).SelectMany(m=>m.Units).Distinct()){int n=units.FindIndex(u=>u.Unit.Id==id);if(n>=0)units[n]=units[n] with{Intent=EngagementIntent.Mission};}
        }
        foreach(var m in NpcArmies.Musters(w).Where(m=>m.At<=at)){
            var b=NewGameMilitary.LedgerIdentity(m.Owner,m.Site);var p=FieldWorld.FromWorld(NpcArmies.Position(w,b.Army,at));
            var returned=w.NpcArmies!.Returns.SingleOrDefault(q=>q.Muster==m.Id);
            bool withdrawing=w.NpcArmies.Marches.Where(q=>q.Muster==m.Id&&q.Input.DestinationSite==m.Site).Any(q=>w.Tasks.Single(t=>t.RequestId==q.Input.RequestId).StartedAt<=at);
            bool active=m.Objective!=null&&returned==null;
            foreach(var id in b.Units){var u=w.Units.Single(u=>u.Id==id);int morale=100-WorldClashes.MoraleLost(w,id,at);units.Add(new(u,p,ContinuousWorld.Height(p),morale,withdrawing?"withdrawing":morale<=25?"routed":"standing",active?EngagementIntent.Mission:EngagementIntent.SelfDefense,w.Characters.Single(c=>c.Id==m.Commander).WillObey));}
            if(active){var o=m.Objective!;mandates.Add(new(m.Id,m.Commander,m.Commander,m.Owner,b.Army,o.TargetOwner,b.Units,m.At,m.At,m.At,o.Claim.Until,null));}
        }
        return new(at,units.ToArray(),mandates.Where(m=>!TruceNegotiations.WillNotInitiate(w,m.Owner,m.TargetOwner,at,w.CommitSequence)).ToArray(),FactionStances.Truth(w,at));
    }
    public static EngagementResult Resolve(ContractWorld w)
    {var s=Snapshot(w);return FieldEngagement.Resolve(s.At,s.Units,s.Mandates,s.Hostilities);}
}
