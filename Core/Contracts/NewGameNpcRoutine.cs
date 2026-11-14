namespace HakutaiKagami.Core.Contracts;
// Versioned personal work using the existing task, transport and clock contracts.
public static partial class NewGameNpcRoutine
{
    public const string Policy="historical-own-site-first-inspection-v1";
    public const int MaximumActors=8;
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&Supported(w.NewGame?.NpcRoutinePolicy);
    internal static EntityId[] Actors(ContractWorld w)
    {
        if(!Enabled(w))return [];
        var adults=Continuing(w)?w.Characters.Where(c=>c.Life==LifeState.Adult).Select(c=>c.Id.Value).ToHashSet(StringComparer.Ordinal):null;
        // 休眠の版のある世界では、今の区切りで起きている人物だけ（NpcDormancy）。
        return w.NewGame!.Start.People.Where(p=>p.Id!=w.PlayerControl.Value&&(adults!=null?adults.Contains(p.Id):!p.Created)).Select(p=>new EntityId(p.Id)).Where(NpcDormancy.AwakeAt(w,w.ElapsedSeconds)).OrderBy(p=>p.Value,StringComparer.Ordinal).ToArray();
    }
    public static bool IsActor(ContractWorld w,EntityId actor)=>Enabled(w)&&actor!=w.PlayerControl&&w.NewGame!.Start.People.Any(p=>p.Id==actor.Value&&(Continuing(w)?w.Characters.Single(c=>c.Id==actor).Life==LifeState.Adult:!p.Created));
    // 保存の検証用：その時点で担い手だったか。後で亡くなっても生前の行動は正しいまま。
    internal static bool WasActor(ContractWorld w,EntityId actor,long at)=>IsActor(w,actor)||Enabled(w)&&Continuing(w)&&actor!=w.PlayerControl&&w.NewGame!.Start.People.Any(p=>p.Id==actor.Value)&&w.Characters.Single(c=>c.Id==actor).Life==LifeState.Dead&&!Assassination.DeadAt(w,actor,at,long.MaxValue);
    static ContractWorld LifeAtDecision(ContractWorld world,long at,long revision)
    {using var _ccRev=ControlContext.At(revision);
        var player=world.PlayerControl;
        if(player==world.PlayerControl&&!world.Characters.Any(c=>c.Life==LifeState.Dead && !Assassination.DeadAt(world,c.Id,at,revision)))return world;
        var history=world.ForkTransportLocation();history.PlayerControl=player;
        history.Characters=world.Characters.Select(c=>c.Life==LifeState.Dead && !Assassination.DeadAt(world,c.Id,at,revision)
            ? c with{Life=NewGameWorld.CanPlay(world.NewGame!.Start,FamilyBirth.People(world).Single(p=>p.Id==c.Id.Value))?LifeState.Adult:LifeState.Minor} : c).ToArray();
        if(history.Schemes!=null)history.Schemes=history.Schemes with{Events=history.Schemes.Events.Where(e=>e.At<at || e.At==at && e.Revision<=revision).ToArray()};
        return history;
    }
    public static int BoundaryCost(ContractWorld w)=>2*Actors(w).Length;
    public static NpcWorkInputs Inputs(ContractWorld w,EntityId actor,long at,long revision)
    {using var _ccRev=ControlContext.At(revision);
        if(NpcJudgment.Enabled(w))return NpcJudgment.Apply(w,actor,at,revision,PurposeInputs(w,actor,at,revision));
        if(PurposeEnabled(w))return PurposeInputs(w,actor,at,revision);
        if(Continuing(w))return ContinuingInputs(w,actor,at,revision);
        var own=w.Requests.Where(r=>r.CreatedAt<=at&&r.CommitSequence<=revision&&r.Intent.Actor==actor&&(r.Intent.Executor==actor||LocalWorkDemand.Enabled(w))&&r.Intent.Action==ActionKind.Inspect).OrderBy(r=>r.CreatedAt).ThenBy(r=>r.Id.Value,StringComparer.Ordinal).ToArray();
        bool known=true;string? result=null;
        if(own.Length>0){var task=w.Tasks.Single(t=>t.RequestId==own[^1].Id);string report=WorldSession.Child("message",task.Id,"report").Value;known=w.Messaging.Knowledge.Any(k=>k.CharacterId==actor.Value&&k.MessageId==report&&k.LearnedAt<=at);if(known)result=w.Messaging.Messages.Single(m=>m.Id==report).Body;}
        if(PrivateStanddown.Enabled(w)||PersonalCompanions.SingleInspectionEnabled(w)){
            var historical=w.ForkTransportLocation();var requests=w.Requests.Where(r=>r.CreatedAt<at||r.CreatedAt==at&&r.CommitSequence<=revision).ToArray();historical.Branches=w.Branches! with{Journeys=w.Branches!.Journeys.Where(j=>requests.Any(r=>w.Tasks.Any(t=>t.Id==j.Task&&t.RequestId==r.Id))).ToArray()};
            var point=RegionalCampaign.RoadActorPointAt(historical,actor,at);var node=BranchRoutes.GraphNodes(w).FirstOrDefault(n=>n.Position==point);bool moving=requests.Any(r=>r.Intent.Executor==actor&&r.Intent.Action==ActionKind.Move&&w.Tasks.Any(t=>t.RequestId==r.Id&&(t.FinishedAt==null||t.FinishedAt>at)));historical.Locations=w.Locations.Select(l=>l.Actor==actor?l with{Kind=node!=null&&!moving?LocationKind.AtSite:LocationKind.Traveling,Site=node!=null&&!moving?RegionalCampaign.Site(node.Id):null}:l).ToArray();w=historical;
        }
        var place=w.Locations.Single(l=>l.Actor==actor);bool local=place.Kind==LocationKind.AtSite&&place.Site!=null;long ready=0;
        bool permit=!FieldConvoy.Busy(w,actor,at,revision+1)&&!NpcFreight.Busy(w,actor,at,revision+1)&&!PersonalCompanions.Occupied(w,actor,at,revision)&&!NpcArmies.CommanderBusyAt(w,actor,at,revision)&&IsActor(w,actor)&&w.Characters.Single(c=>c.Id==actor).Life==LifeState.Adult&&local&&WorldSession.Permits(w,actor,NewGameWorld.InspectFor(w,actor,"request:npc-routine-preview",at),at);
        return new(local,permit,own.Select(r=>w.Tasks.Single(t=>t.RequestId==r.Id).Id).ToArray(),known,result,ready,at>=ready);
    }
    public static string Reason(NpcWorkInputs i)=>i.Routine?.Purpose!=null?PurposeReason(i):i.Routine!=null?ContinuingReason(i):!i.LocalWitness?"not-at-own-site":!i.InspectionPermit?"no-personal-permit":!i.LastReportKnown?"await-own-report":i.OwnTasks.Length>0?"own-result-known":"inspect-own-site";
    // 検査で並列を止める（同じ答えになることを比べるため）。
    [ThreadStatic] internal static bool Sequential;
    public static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;var times=new List<long>();
        long soonest=checked(w.ElapsedSeconds+1); // これより早い区切りは無いので、見つかったら残りの人物を計算しない
        var actors=Actors(w).ToArray();
        // 人ごとの最後の判断（判断の記録を人ごとに頭から探さない）。
        var lastOf=new Dictionary<EntityId,NpcWorkDecision>();foreach(var d in w.NpcWork!.Decisions)lastOf[d.Actor]=d;
        List<long> For(EntityId actor)
        {
            var found=new List<long>();var i=Inputs(w,actor,w.ElapsedSeconds,w.CommitSequence);var last=lastOf.GetValueOrDefault(actor);
            if(last==null||last.InputHash!=ContractJson.CanonicalHash(i)){found.Add(soonest);return found;}
            if((PurposeEnabled(w)?PurposeBoundary(w,actor,i):ContinuingBoundary(w,i)) is {} next)found.Add(next);
            if(NpcDormancy.Enabled(w))found.AddRange(InputChanges(w,actor,last.InputHash));
            return found;
        }
        // 多くの人を並べて求める（人ごとに世界を読むだけの計算で、答えは最も早い時刻なので、順によらず同じ）。
        if(actors.Length>=32&&!Sequential)
        {
            var rows=new List<long>?[actors.Length];long? rev=ControlContext.Revision,time=ControlContext.Time;
            Parallel.For(0,actors.Length,new ParallelOptions{MaxDegreeOfParallelism=Math.Max(1,Math.Min(8,Environment.ProcessorCount))},(n,loop)=>{
                if(loop.ShouldExitCurrentIteration)return;
                using var _=ControlContext.Restore(rev,time);var r=For(actors[n]);rows[n]=r;if(r.Contains(soonest))loop.Stop();});
            if(rows.Any(r=>r!=null&&r.Contains(soonest)))return soonest<=target?soonest:null;
            foreach(var r in rows)if(r!=null)times.AddRange(r);
        }
        else foreach(var actor in actors){var r=For(actor);times.AddRange(r);if(r.Contains(soonest))break;}
        return times.Where(t=>t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    }
    // 休眠の版の世界：次の区切りまで世界は変わらないので、判断の入力が変わるのは時刻だけによる。
    // 1秒後（直前に知った記録が使えるようになる時刻）と記録が古くなる時刻のうち、入力が変わる時刻を区切りにする。
    // これで、進める細かさによらず同じ時刻に判断する（v108）。
    static IEnumerable<long> InputChanges(ContractWorld w,EntityId actor,string? lastHash)
    {
        long now=w.ElapsedSeconds;var candidates=new List<long>{checked(now+1)};
        if(PersonalCompanions.PhysicalSiteAt(w,actor,now,w.CommitSequence+1) is {} site&&LocalWorkDemand.NextStale(w,actor,site,now) is {} stale)candidates.Add(stale);
        foreach(var t in candidates.Distinct())if(ContractJson.CanonicalHash(Inputs(w,actor,t,w.CommitSequence))!=lastHash)yield return t;
    }
    internal static void TakeTurns(WorldSession session)=>session.TakeNewGameNpcTurns();
    public static void Validate(ContractWorld w,ContractWorld? trustedPrior=null)
    {
        if(!Enabled(w)||w.NpcWork==null||Actors(w).Length>(Continuing(w)?ContinuingMaximumActors:MaximumActors))throw new InvalidDataException("NPC routine policy/module mismatch");var ds=w.NpcWork.Decisions;if(ds.Select(d=>d.Id).Distinct().Count()!=ds.Length)throw new InvalidDataException("Duplicate NPC routine decision");
        // 休眠の版の世界（v110）：検査済みの前の状態と同じ判断（先頭から同じ並び・同じ中身）は、過去の入力の計算をやり直さない。
        // 記録は追記だけで、入力は判断の時点の番号までの記録から求めるので、検査済みの判断の答えは変わらない（他の仕組みの trustedPrior と同じ考え方）。
        int verified=0;
        if(NpcDormancy.Enabled(w)&&trustedPrior?.NpcWork is {} prior&&prior.Decisions.Length<=ds.Length&&prior.Decisions.Length>0&&ContractJson.Write(prior.Decisions).AsSpan().SequenceEqual(ContractJson.Write(ds[..prior.Decisions.Length])))verified=prior.Decisions.Length;
        var requestsById=w.Requests.ToLookup(r=>r.Id);
        for(int n=0;n<ds.Length;n++){var d=ds[n];using var _cc=ControlContext.At(d.InputRevision);bool replay=n>=verified;var historical=replay?LifeAtDecision(w,d.At,d.InputRevision):w;if(d.Id!=new EntityId("decision:npc-work/"+(n+1))||replay&&(!IsActor(historical,d.Actor)||!NpcDormancy.Awake(w,d.Actor,d.At))||d.At<0||d.At>w.ElapsedSeconds||d.InputRevision<0||d.InputRevision>w.CommitSequence||n>0&&d.At<ds[n-1].At||d.InputHash!=ContractJson.CanonicalHash(d.Inputs)||replay&&d.InputHash!=ContractJson.CanonicalHash(Inputs(historical,d.Actor,d.At,d.InputRevision))||d.Reason!=Reason(d.Inputs))throw new InvalidDataException("NPC routine lacks its own historical inputs");
            if(d.Request is {} id){if((d.Reason!="inspect-own-site"&&(!PurposeEnabled(w)||d.Reason!="practice-own-skill")&&(!NpcJudgment.Enabled(w)||d.Reason!="offer-local-inspection"))||id!=new EntityId("request:npc-routine/"+d.Actor.Value.Replace(':','/')+"/"+(d.Inputs.OwnTasks.Length+1)))throw new InvalidDataException("Invalid NPC routine request identity");if(d.Result is ResultCode.Accepted or ResultCode.AlreadyAccepted){var request=requestsById[id].Single();if(PurposeEnabled(w)&&(request.Intent.Action!=(d.Reason=="practice-own-skill"?ActionKind.Train:ActionKind.Inspect)||d.Reason=="practice-own-skill"&&request.Intent.TrainingSkill!=d.Inputs.Routine!.Purpose!.Skill))throw new InvalidDataException("NPC purpose dispatch differs from chosen action");if(request.CreatedAt!=d.At||request.Intent.Actor!=d.Actor||request.Intent.Executor!=(d.Reason=="offer-local-inspection"?d.Inputs.Routine!.Judgment!.Recipient:d.Actor))throw new InvalidDataException("NPC routine dispatch differs from decision");}}
            else if(d.Reason is "inspect-own-site" or "practice-own-skill" or "offer-local-inspection"||d.Result!=ResultCode.NotAvailable)throw new InvalidDataException("NPC routine wait has dispatch result");
        }
    }
}
