namespace HakutaiKagami.Core.Contracts;

// 自領で現認した交戦は領地防衛の根拠になる。通知に対する返答とは別に当主が判断する。
public static class NpcWarCouncil
{
    public static IEnumerable<NpcArmySight> WitnessedAttacks(ContractWorld w, EntityId observer)
    {
        if (w.WorldClashes?.Steps.LastOrDefault() is not {} battle || battle.At != w.ElapsedSeconds) yield break;
        var attackers=battle.Result.Strikes.Where(s=>s.Basis is EngagementBasis.Mission or EngagementBasis.UnauthorizedAttack)
            .Select(s=>s.Attacker).ToHashSet();
        foreach(var visible in NpcArmies.Sights(w,observer))
            if(visible.Attacking && w.Units.Any(u=>u.Army==visible.Army && attackers.Contains(u.Id))) yield return visible;
    }

    internal static void Consider(ContractWorld world)
    {
        if (world.WorldClashes?.Steps.LastOrDefault() is not {} step || step.At != world.ElapsedSeconds ||
            step.Result.Strikes.Length == 0 || !CharacterRelations.Enabled(world) || !MilitaryEconomy.Enabled(world) ||
            !NpcArmies.Enabled(world) || !NpcArmies.Musters(world).Any()) return;
        long at=world.ElapsedSeconds;
        foreach (var house in HouseSuccession.Factions(world))
        {
            var owner=new EntityId(house.Id);
            var head=new EntityId(house.Head);
            long rev=world.CommitSequence;
            if (head==world.PlayerControl || SocialWorld.Site(world,head) is not {} site ||
                FactionStances.ResolveAuthority(world,head,owner,at,rev) is not {} authority ||
                authority.Home!=site || !MilitaryEconomy.CanMuster(world,site,at,rev+1)) continue;
            var position=BranchRoutes.GraphNodes(world).Single(n=>RegionalCampaign.Site(n.Id)==site).Position;
            foreach (var sight in WitnessedAttacks(world,head).Where(s=>s.Owner!=owner && s.Position==position))
            {
                if (!world.NewGame!.Start.Factions.Any(f=>f.Id==sight.Owner.Value) ||
                    TruceNegotiations.WillNotInitiate(world,owner,sight.Owner,at,rev) ||
                    FactionStances.ClaimAt(world,owner,sight.Owner,at,rev)!=null) continue;
                long defenders=CastleHandover.Forces(world,owner,site,at,rev+1);
                int ambition=CharacterRelations.DiagnosticTrait(world,head,SocialTrait.Ambition);
                int caution=CharacterRelations.DiagnosticTrait(world,head,SocialTrait.Caution);
                if (defenders<=0 || defenders*(150L+ambition)<sight.Soldiers*(100L+caution)) continue;
                var id=HouseholdCourt.Id("border-defense",owner.Value+"/"+sight.Army.Value);
                if (FactionStances.OwnIdUsed(world,id)) continue;
                var decision=new FactionStanceInput(id,FactionStanceAction.DeclareHostile,owner,sight.Owner,at,checked(at+7*86400));
                TransactionalWorld.TryApply(world,copy=>FactionStances.Apply(copy,decision,head));
                break;
            }
        }
    }
}
