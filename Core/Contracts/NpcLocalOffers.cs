using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

// 現地で本人が目にできる相手へ相談する。依頼・配送・承諾・仕事は既存の任務を使う。
public static class NpcLocalOffers
{
    public static bool IsOffer(ContractWorld w,Intent i)=>NpcJudgment.Enabled(w)&&i.Actor!=i.Executor&&i.Action==ActionKind.Inspect&&i.RequestId.Value.StartsWith("request:npc-routine/",StringComparison.Ordinal);
    internal static bool AvailableAt(ContractWorld w,EntityId actor,long at,long revision)=>!HostageCustody.HeldAt(w,actor,at,revision+1)&&!FieldConvoy.Busy(w,actor,at,revision+1)&&!NpcFreight.Busy(w,actor,at,revision+1)&&!PersonalCompanions.Occupied(w,actor,at,revision)&&!NpcArmies.CommanderBusyAt(w,actor,at,revision);
    internal static NpcChoiceInput Recipients(ContractWorld w,EntityId issuer,long at,long revision,long choiceAt)
    {
        var site=PersonalCompanions.PhysicalSiteAt(w,issuer,at,revision+1);
        var candidates=new List<NpcChoiceCandidate>{new(issuer.Value,true,true,-10,new(0,0,0,0,0,0),false,false,false)};
        if(site!=null)
        foreach(var person in w.Characters.Where(c=>c.Id!=issuer&&c.Life==LifeState.Adult).OrderBy(c=>c.Id.Value,StringComparer.Ordinal)){
            if(PersonalCompanions.PhysicalSiteAt(w,person.Id,at,revision+1)!=site)continue;
            bool busy=w.Tasks.Any(t=>t.Executor==person.Id&&(t.FinishedAt==null||t.FinishedAt>=at)&&w.Requests.Any(r=>r.Id==t.RequestId&&r.CreatedAt<=at&&r.CommitSequence<=revision));
            var permissions=w.ForkTransportLocation();permissions.CommitSequence=revision;
            bool permit=WorldSession.Permits(permissions,person.Id,new(new("request:npc-offer-preview"),issuer,person.Id,site.Value,ActionKind.Inspect,null,null,0,null,new(DeadlineCriterion.Completion,at+120,false,null,at+120),at,1,4,1,true,TaskKind.VoluntaryRequest),at);
            candidates.Add(new(person.Id.Value,true,!busy&&permit&&AvailableAt(w,person.Id,at,revision),0,new(0,0,0,0,0,0),false,false,person.Id==w.PlayerControl));
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
    {
        var input=NewGameNpcRoutine.Inputs(w,intent.Actor,at,revision);
        var recipient=input.Routine?.Judgment?.Recipient;
        // 過去の作成地点を使い、移動後の現在地で原本を書き換えない。
        var site=PersonalCompanions.PhysicalSiteAt(w,intent.Actor,at,revision+1);
        var expected=new Intent(intent.RequestId,intent.Actor,intent.Executor,site??default,ActionKind.Inspect,null,null,0,null,new(DeadlineCriterion.Completion,at+120,false,null,at+120),at,1,4,1,true,TaskKind.VoluntaryRequest);
        if(!NewGameNpcRoutine.IsActor(w,intent.Actor)||NewGameNpcRoutine.Reason(input)!="offer-local-inspection"||recipient!=intent.Executor||site==null||intent!=expected)
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
            if(NewGameWork.CanRequest(this,task.Id,actor))CollectWorkPayment(new("payment:npc-offer/"+task.Id.Value.Replace(':','/')),task.Id,actor,Revision);
    }
}
