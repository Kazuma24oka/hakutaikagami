using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

// 現地で本人が目にできる相手へ相談する。依頼・配送・承諾・仕事は既存の任務を使う。
public static class NpcLocalOffers
{
    public static bool IsOffer(ContractWorld w,Intent i)=>NpcJudgment.Enabled(w)&&i.Actor!=i.Executor&&i.Action==ActionKind.Inspect&&i.RequestId.Value.StartsWith("request:npc-routine/",StringComparison.Ordinal);
    internal static bool AvailableAt(ContractWorld w,EntityId actor,long at,long revision)=>!HostageCustody.HeldAt(w,actor,at,revision+1)&&!FieldConvoy.Busy(w,actor,at,revision+1)&&!NpcFreight.Busy(w,actor,at,revision+1)&&!PersonalCompanions.Occupied(w,actor,at,revision)&&!NpcArmies.CommanderBusyAt(w,actor,at,revision);
    internal static int InspectionReference(ContractWorld w, EntityId issuer, EntityId worker, long at)
    {
        if (w.NewGame?.Judgment?.UseInspectionReferences != true) return 0;
        // 開封時刻しか記録しないため、同じ秒の判断へ後から開封を遡及させない。
        bool known = w.Outbox.Any(o => o.Sender == worker && o.Recipient == issuer &&
            o.Kind == "contract-report" && o.Body == "任務完了" && o.At >= at - 30L * 86400 &&
            MessageFlow.LearnedAt(w.Messaging, o.Id.Value, issuer.Value) is long read && read < at &&
            w.Tasks.Any(t => t.Id == o.Task && t.Action == ActionKind.Inspect));
        return known ? 8 : 0;
    }
    static ContractWorld PermissionView(ContractWorld w,long revision){var c=w.ForkTransportLocation();c.CommitSequence=revision;return c;}
    internal static NpcChoiceInput Recipients(ContractWorld w,EntityId issuer,long at,long revision,long choiceAt)
    {using var _ccRev=ControlContext.At(revision);
        var site=PersonalCompanions.PhysicalSiteAt(w,issuer,at,revision+1);
        var candidates=new List<NpcChoiceCandidate>{new(issuer.Value,true,true,-10,new(0,0,0,0,0,0),false,false,false)};
        // 一度も動いていない人（自分が担う依頼・旅・捕縛・人質の記録がない人）の居場所は最初の拠点のまま。全員の居場所を1人ずつ求めると重いので、動いたことのある人だけ求める（結果は同じ）。
        var movers=(w.Captives?.Entries.Length??0)>0||(w.Custody?.Entries.Length??0)>0?null:w.Requests.Select(r=>r.Intent.Executor).Concat(w.Branches!.Journeys.Select(j=>j.Actor)).Append(w.PlayerControl).ToHashSet();
        var homes=movers==null?null:FamilyBirth.People(w).GroupBy(p=>p.Id,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Single().Site,StringComparer.Ordinal);
        ContractWorld? permissions=null; // 権限を見るための写し（相手によらず同じなので1回だけ作る）
        EntityId? SiteOf(EntityId person)=>movers!=null&&!movers.Contains(person)?new(homes![person.Value]):PersonalCompanions.PhysicalSiteAt(w,person,at,revision+1);
        if(site!=null)
        foreach(var person in w.Characters.Where(c=>c.Id!=issuer&&c.Life==LifeState.Adult).OrderBy(c=>c.Id.Value,StringComparer.Ordinal)){
            if(SiteOf(person.Id)!=site)continue;
            // 新版では作業終了と同じ秒の相手を翌秒まで候補に戻さない。
            if((w.NewGame?.Judgment?.EventEdition==HistoricalEventRules.OwariEdition || w.NewGame?.Judgment?.Challenge != null) &&
                w.Tasks.Any(t=>t.Executor==person.Id && t.FinishedAt==at && w.Requests.Any(r=>r.Id==t.RequestId &&
                    r.CommitSequence<=revision && !NewGameNpcRoutine.DiscretionaryPractice(w,r.Intent))))continue;
            bool busy=w.Tasks.Any(t=>t.Executor==person.Id&&(t.FinishedAt==null||t.FinishedAt>=at)&&w.Requests.Any(r=>r.Id==t.RequestId&&r.CreatedAt<=at&&r.CommitSequence<=revision));
            permissions??=PermissionView(w,revision);
            bool permit=WorldSession.Permits(permissions,person.Id,new(new("request:npc-offer-preview"),issuer,person.Id,site.Value,ActionKind.Inspect,null,null,0,null,new(DeadlineCriterion.Completion,at+120,false,null,at+120),at,1,4,1,true,TaskKind.VoluntaryRequest),at);
            candidates.Add(new(person.Id.Value,true,!busy&&permit&&AvailableAt(w,person.Id,at,revision),InspectionReference(w,issuer,person.Id,at),new(0,0,0,0,0,0),false,false,person.Id==w.PlayerControl));
        }
        var settings=w.NewGame!.Judgment!;
        return new(issuer,w.Seed,choiceAt,NpcJudgment.Personality(w,issuer),NpcAspiration.Unspecified,HistoryMode.Fictional,settings.Attention,candidates.ToArray());
    }
    internal static Intent Offer(ContractWorld w,EntityId issuer,EntityId recipient,string id,long at)
    {
        var site=PersonalCompanions.PhysicalSiteAt(w,issuer,at,w.CommitSequence+1)??throw new InvalidDataException("現地に到着してから相談してください");
        return new(new(id),issuer,recipient,site,ActionKind.Inspect,null,null,0,null,new(DeadlineCriterion.Completion,at+120,false,null,at+120),at,1,4,1,true,TaskKind.VoluntaryRequest);
    }
    internal static void Validate(ContractWorld w,Intent intent,long at,long revision)
    {using var _ccRev=ControlContext.At(revision);using var _cc=ControlContext.At(revision);
        var input=NewGameNpcRoutine.Inputs(w,intent.Actor,at,revision);
        var recipient=input.Routine?.Judgment?.Recipient;
        // 過去の作成地点を使い、移動後の現在地で原本を書き換えない。
        var site=PersonalCompanions.PhysicalSiteAt(w,intent.Actor,at,revision+1);
        var expected=new Intent(intent.RequestId,intent.Actor,intent.Executor,site??default,ActionKind.Inspect,null,null,0,null,new(DeadlineCriterion.Completion,at+120,false,null,at+120),at,1,4,1,true,TaskKind.VoluntaryRequest);
        if(!NewGameNpcRoutine.WasActor(w,intent.Actor,at)||NewGameNpcRoutine.Reason(input)!="offer-local-inspection"||recipient!=intent.Executor||site==null||intent!=expected)
            throw new InvalidDataException("現地の既知需要と本人の相手選択に合わない依頼です");
    }
}

public sealed partial class WorldSession
{
    internal void ReceiveNpcLocalOffers(EntityId actor)
    {
        // プレイヤーの開封・承諾は本人の操作に残す。
        if(actor==state.PlayerControl)return;
        foreach(var message in state.Messaging.Messages.Where(m=>m.RecipientId==actor.Value&&state.Requests.Any(r=>r.Id.Value==m.CorrelationId&&NpcLocalOffers.IsOffer(state,r.Intent))&&m.Kind is "contract-order" or "assessment-notice").OrderBy(m=>m.CreatedAt).ThenBy(m=>m.Id,StringComparer.Ordinal).ToArray()){
            if(!MessageFlow.Knows(state.Messaging,message.Id,actor.Value)&&state.Messaging.Attempts.Any(a=>a.MessageId==message.Id&&a.ReceiverId==actor.Value&&a.Status==AttemptStatus.Delivered))Open(actor,new(message.Id),Revision);
        }
        foreach(var task in state.Tasks.Where(t=>t.Executor==actor&&t.Agreement==AgreementPhase.AwaitingAssent&&state.Requests.Any(r=>r.Id==t.RequestId&&NpcLocalOffers.IsOffer(state,r.Intent))).ToArray()){
            bool free=!state.Tasks.Any(t=>t.Id!=task.Id&&t.Executor==actor&&!Terminal(t));
            bool accept=free&&NpcLocalOffers.AvailableAt(state,actor,state.ElapsedSeconds,state.CommitSequence)&&RegionalCampaign.RoadActorPointAt(state,actor,state.ElapsedSeconds)==BranchRoutes.GraphNodes(state).Single(n=>RegionalCampaign.Site(n.Id)==task.Scope).Position&&CharacterRelations.DiagnosticTrait(state,actor,SocialTrait.Duty)+CharacterRelations.DiagnosticTrait(state,actor,SocialTrait.GainSeeking)>=70;
            Assent(task.Id,actor,accept,Revision);
        }
        foreach(var task in state.Tasks.Where(t=>t.Executor==actor&&state.Requests.Any(r=>r.Id==t.RequestId&&NpcLocalOffers.IsOffer(state,r.Intent))).ToArray())
            if(NewGameWork.CanRequest(this,task.Id,actor,state))CollectWorkPayment(new("payment:npc-offer/"+task.Id.Value.Replace(':','/')),task.Id,actor,Revision);
    }
}
