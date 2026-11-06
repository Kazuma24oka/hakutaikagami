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

    // 当主が知る防衛条約と、現に視認できる同盟部隊への攻撃を出陣の根拠にする。
    // 別の場所で同じ家が損害を受けても、この観測には加えない。
    static bool WitnessedAttackOnAlly(ContractWorld w, EntityId head, EntityId owner, EntityId army)
    {
        if (!TreatyNegotiations.Enabled(w)) return false;
        var allies = TreatyNegotiations.Known(w,head)
            .Where(f => (f.Terms.A == owner || f.Terms.B == owner) &&
                TreatyNegotiations.ClauseUsable(w,head,f.Acceptance,TreatyClause.Defense,w.ElapsedSeconds,w.CommitSequence))
            .Select(f => f.Terms.A == owner ? f.Terms.B : f.Terms.A).ToHashSet();
        if (allies.Count == 0) return false;
        var observer = FieldWorld.FromWorld(RegionalCampaign.ActorPoint(w,head));
        long range = BattlefieldRules.SightRange(w,observer,w.ElapsedSeconds);
        var visible = WorldEngagement.Snapshot(w).Units.Where(u => RegionalGround.Distance(observer,u.Position) <= range).ToArray();
        var attackers = visible.Where(u => u.Unit.Army == army).Select(u => u.Unit.Id).ToHashSet();
        var targets = visible.Where(u => allies.Contains(u.Unit.Organization)).Select(u => u.Unit.Id).ToHashSet();
        return w.WorldClashes!.Steps[^1].Result.Strikes.Any(s => attackers.Contains(s.Attacker) &&
            targets.Contains(s.Target) && s.Basis is EngagementBasis.Mission or EngagementBasis.UnauthorizedAttack);
    }

    static long AvailableSoldiers(ContractWorld w, EntityId head, EntityId owner, EntityId home, bool atHome)
    {
        if (atHome && MilitaryEconomy.CanMuster(w,home,w.ElapsedSeconds,w.CommitSequence+1))
            return CastleHandover.Forces(w,owner,home,w.ElapsedSeconds,w.CommitSequence+1);

        // 遠征中の当主は同行する兵と携行兵糧だけを数える。
        var muster = NpcArmies.Musters(w).SingleOrDefault(m => m.Owner == owner && m.Commander == head &&
            !w.NpcArmies!.Returns.Any(r => r.Muster == m.Id));
        if (muster == null) return 0;
        var binding = NewGameMilitary.LedgerIdentity(owner,muster.Site);
        long soldiers = w.Units.Where(u => u.Army == binding.Army).Sum(u => u.Soldiers);
        long carried = w.ResourceLots.SingleOrDefault(l => l.Id == binding.Carry && l.MobileHolder == head)?.Quantity ?? 0;
        return carried >= soldiers * 86400L ? soldiers : 0;
    }

    internal static IEnumerable<(FactionStanceInput Decision, EntityId Head)> Choices(ContractWorld world)
    {
        if (world.WorldClashes?.Steps.LastOrDefault() is not {} step || step.At != world.ElapsedSeconds ||
            step.Result.Strikes.Length == 0 || !CharacterRelations.Enabled(world) || !MilitaryEconomy.Enabled(world) ||
            !NpcArmies.Enabled(world) || !NpcArmies.Musters(world).Any()) yield break;
        long at=world.ElapsedSeconds;
        foreach (var house in HouseSuccession.Factions(world))
        {
            var owner=new EntityId(house.Id);
            var head=new EntityId(house.Head);
            long rev=world.CommitSequence;
            if (head==world.PlayerControl ||
                FactionStances.ResolveAuthority(world,head,owner,at,rev) is not {} authority) continue;
            var position=BranchRoutes.GraphNodes(world).Single(n=>RegionalCampaign.Site(n.Id)==authority.Home).Position;
            bool atHome=SocialWorld.Site(world,head)==authority.Home;
            long defenders=AvailableSoldiers(world,head,owner,authority.Home,atHome);
            if (defenders<=0) continue;
            foreach (var sight in WitnessedAttacks(world,head).Where(s=>s.Owner!=owner))
            {
                if (!world.NewGame!.Start.Factions.Any(f=>f.Id==sight.Owner.Value) ||
                    TruceNegotiations.WillNotInitiate(world,owner,sight.Owner,at,rev) ||
                    FactionStances.ClaimAt(world,owner,sight.Owner,at,rev)!=null) continue;
                bool homeDefense=atHome && sight.Position==position;
                if (!homeDefense && !WitnessedAttackOnAlly(world,head,owner,sight.Army)) continue;
                int ambition=CharacterRelations.DiagnosticTrait(world,head,SocialTrait.Ambition);
                int caution=CharacterRelations.DiagnosticTrait(world,head,SocialTrait.Caution);
                if (defenders*((homeDefense?150L:100L)+ambition)<sight.Soldiers*(100L+caution)) continue;
                var id=HouseholdCourt.Id(homeDefense?"border-defense":"ally-campaign",owner.Value+"/"+sight.Army.Value);
                if (FactionStances.OwnIdUsed(world,id)) continue;
                var decision=new FactionStanceInput(id,FactionStanceAction.DeclareHostile,owner,sight.Owner,checked(at+1),checked(at+7*86400));
                if (FactionStances.OverlapsDeclaration(world.FactionStances?.Decisions ?? [],decision)) continue;
                yield return (decision,head);
                break;
            }
        }
    }
}

public sealed partial class WorldSession
{
    // 観測元の戦闘と、その後の政治判断は別々の版で確定する。
    void ConsiderObservedWar()
    {
        foreach (var choice in NpcWarCouncil.Choices(state).ToArray())
            Change(w=>FactionStances.Apply(w,choice.Decision,choice.Head));
    }
}
