using Godot;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    EntityId? KnownRetainerTask()=>S.GetActorView(S.PlayerControl).Tasks.LastOrDefault(t=>MissionReview.CanInterrupt(S,t.Id,S.PlayerControl))?.Id;
    EntityId? OwnInterruptedTask()
    {
        var w=S.DiagnosticSnapshot();var own=S.GetActorView(S.PlayerControl).Tasks.Select(t=>t.Id).ToHashSet();
        return w.Tasks.LastOrDefault(t=>t.Executor==S.PlayerControl&&own.Contains(t.Id)&&t.InternalReason=="executor-interrupted"&&
            !w.Messaging.Messages.Any(m=>m.Kind==MissionReview.ExplanationKind&&m.CorrelationId==t.RequestId.Value))?.Id;
    }
    void BuildRetainerUI(HFlowContainer row)
    {
        Add(row,"retainer-interrupt","本人判断で命令を中断",()=>Require(S.InterruptMission(KnownRetainerTask()!.Value,S.PlayerControl,S.Revision)));
        Add(row,"retainer-explain","自己判断の理由を返送",()=>Require(S.ExplainMission(OwnInterruptedTask()!.Value,S.PlayerControl,MissionReview.Explanation.OwnDecision,S.Revision)));
    }
    void RefreshRetainerUI()
    {
        buttons["retainer-interrupt"].Disabled=KnownRetainerTask()==null;buttons["retainer-explain"].Disabled=OwnInterruptedTask()==null;
    }
}
