using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

// A presentation-independent adapter onto existing tasks, couriers and regional commands.
// No parallel mission state, evaluation truth, resource ownership or world clock.
public static class OfficerLoop
{
    public const string Ruleset = "officer-peace-war-loop-v1";
    public const string Contract = "at-castle-inspect-or-levy40-4s-delivery1-report1-main-lock-council-line-v1";
    public static bool Enabled(ContractWorld w) => w.Definitions.Ruleset == Ruleset || RegionalWork.Enabled(w) || MissionEntry.Enabled(w) || IncomingMission.Enabled(w);
    public static bool Terminal(TaskState t) => t.Execution is ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed;
    public static bool Busy(ContractWorld w, EntityId actor) => w.Tasks.Any(t => t.Executor == actor && !Terminal(t) && (!CreatedCampaign.Enabled(w)||((t.Issuer==actor||t.Agreement==AgreementPhase.Active)&&!(t.Action==ActionKind.Inspect&&t.Execution==ExecutionPhase.Waiting&&!w.Locations.Any(l=>l.Actor==actor&&l.Kind==LocationKind.AtSite&&l.Site==t.Scope)))));
    public static ContractWorld Create(bool refuses = false)
    {
        var w = ContinuousWorld.Create(refuses: refuses);
        RegionalBattle.SetActive(w, false);
        w.WorldId = new("world:officer-peace-war-loop-v1");
        w.Definitions = w.Definitions with { Id = new("scenario:officer-peace-war-loop-v1"), Ruleset = Ruleset };
        SetSiteLocations(w);
        w.Definitions = w.Definitions with { Version = RegionalBattle.Version(w) };
        ContractValidation.Validate(w);
        return w;
    }
    static void AtCastle(ContractWorld w, EntityId actor)
    {
        var l = w.Locations.Single(l => l.Actor == actor);
        if (l.Kind != LocationKind.AtSite || l.Site != ContractFixture.Castle)
            throw new InvalidDataException("清洲の在所で行う任務です");
    }
    public static Intent Mission(ContractWorld w, string id, bool delegateLevy)
    {
        if (!Enabled(w)) throw new InvalidDataException("Officer loop rules required");
        long deadline = checked(w.ElapsedSeconds + 3600);
        return new(new(id), w.PlayerControl, delegateLevy ? ContractFixture.Clerk : w.PlayerControl,
            ContractFixture.Castle, delegateLevy ? ActionKind.Transfer : ActionKind.Inspect,
            delegateLevy ? ContractFixture.Source : null, delegateLevy ? ContractFixture.Treasury : null,
            delegateLevy ? 40 : 0, null, new(DeadlineCriterion.Completion, deadline, false, null, deadline),
            w.ElapsedSeconds, 1, 4, 1, false, TaskKind.BindingOrder);
    }
    internal static void ValidateOrder(ContractWorld w, Intent i)
    {
        if (i.Action == ActionKind.Move)
        {
            AtCastleOrOtherSite(w, i.Actor);
            if (Busy(w, i.Actor)) throw new InvalidDataException("本人の任務を終えてから出発してください");
            TravelWorld.ValidateOrder(w, i);
            return;
        }
        if (RegionalWork.Enabled(w)){RegionalWork.ValidateOrder(w,i);return;}
        if (IncomingMission.Enabled(w)) IncomingMission.ValidateOrder(w,i);
        else if (MissionEntry.Enabled(w)) MissionEntry.ValidateOrder(w,i);
        else if (i != Mission(w, i.RequestId.Value, i.Action == ActionKind.Transfer))
            throw new InvalidDataException("Unsupported mission purpose, limits or report condition");
        AtCastle(w, i.Actor); AtCastle(w, i.Executor);
        if (Busy(w, i.Executor)) throw new InvalidDataException("担当者は別の任務に拘束されています");
    }
    static void AtCastleOrOtherSite(ContractWorld w, EntityId actor)
    {
        if (w.Locations.Single(l => l.Actor == actor).Kind != LocationKind.AtSite)
            throw new InvalidDataException("拠点への到着が必要です");
    }
    internal static void Transition(ContractWorld w, bool active)
    {
        if (!Enabled(w)) return;
        if(NpcWork.Enabled(w)&&!CreatedCampaign.Enabled(w)&&active)throw new InvalidDataException("NPC work profile is a peace-only trial");
        if (active)
        {
            if(CreatedCampaign.Enabled(w)&&!CreatedCampaign.CanDeploy(w))throw new InvalidDataException("本隊は清洲で待機しています。任務と返報を終えてから出陣してください");
            if (new[]{w.PlayerControl, ContractFixture.Clerk}.Any(a => Busy(w, a)) ||
                !CreatedCampaign.Enabled(w)&&w.Messaging.Attempts.Any(a => a.Status == AttemptStatus.InTransit &&
                    w.Messaging.Messages.Any(m => m.Id == a.MessageId && m.Kind.StartsWith("contract-", StringComparison.Ordinal))))
                throw new InvalidDataException("任務と返報が済んでから野外へ出てください");
        }
        else if (w.Region!.Units.Any(u => (CreatedCampaign.Enabled(w)?u.Commander==w.PlayerControl:u.Side==0) && (u.Position != u.Home || u.Route.Any(l => l.EndAt > w.ElapsedSeconds))))
            throw new InvalidDataException("自隊と別働隊の帰還が必要です");
    }
    internal static void SetSiteLocations(ContractWorld w)
    {
        if (!w.Region!.Active)
            w.Locations = w.Locations.Select(l => l.Actor == ContractFixture.Clerk ?
                new LocationState(l.Actor, LocationKind.AtSite, ContractFixture.Castle, null, 0, 0, null, null, null, null, w.CommitSequence) : l).ToArray();
    }
    public static RegionalRequest CouncilLine(ContractWorld w, string id)
    {
        if (!Enabled(w) || !w.Region!.Active) throw new InvalidDataException("野外の軍議で指示します");
        var n = w.Atlas!.Anchor;
        return new(new(id), w.PlayerControl, ContractFixture.Clerk, RegionalAction.Move,
            ContinuousWorld.Point(n, 95000, -65000), ContinuousWorld.Point(n, 115000, -65000));
    }
}
