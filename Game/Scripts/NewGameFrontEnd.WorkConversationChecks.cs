using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    bool WorkVoluntary=>FlowCase is "worktalk-voluntary" or "worktalk-small";
    async Task TalkHold(double seconds){for(int n=0;n<(int)(seconds*30);n++)await Frame();}
    async Task WorkAcceptAndRun()
    {
        if(WorkVoluntary){Check(game!.WorkScreen=="点検を引き受けますか","real voluntary decision is visible");game.Press("work-assent");Check(game.WorkScreen=="closed"&&game.WorkSummary.Contains("点検"),"assent starts actual task");await Capture("running",true);Check(game.ReviewRunningWorkLayout(),"current job and next actual action are grouped together on map");await TalkHold(2);}
        Check(game!.WorkScreen=="closed"&&game.WorkSummary.Contains("点検"),"known real execution shown");game.Press("work-run");await WaitManualTime();game.Press("work-details");
        Check(game.WorkScreen=="点検の報告","completion shown from player knowledge");
        var w=game.Session.DiagnosticSnapshot();var t=w.Tasks.Last(t=>t.Executor==w.PlayerControl&&t.Issuer!=w.PlayerControl&&t.Action==ActionKind.Inspect);Check(t.Execution==ExecutionPhase.Completed&&w.Outbox.Any(o=>o.Task==t.Id&&o.Kind=="contract-report"),"actual selected work completes and generates actual report");
    }
    async Task RunWorkConversationChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        bool film=OS.GetCmdlineUserArgs().Contains("--work-conversation-film");


        if(restore){
            Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();await Frame();
            game!.LoadCheckpoint("s0");game.Press("work-details");await Capture("restored-s0",true);await WorkAcceptAndRun();await Capture("continued-s0",true);
            game.LoadCheckpoint("s1");game.Press("work-details");await Capture("restored-s1",true);await Capture("continued-s1",true);
        }else{
            await BeginPopulationEntry(0,WorkVoluntary?"person:shingen":"person:takeda-nobushige");
            string before=ContractJson.CanonicalHash(game!.Session.DiagnosticSnapshot());game.Press("work");Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.WorkScreen=="現地の点検","consultation previews without issuing any task or changing clock");await Capture("consultation",true);Check(game.ReviewWorkLayout(),"speaker,card and vertical replies fit the actual viewport without overlap");await TalkHold(4);game.Press("work-back");Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"leaving consultation does not create a command");game.Press("work");game.Press("work-confirm");await WaitDeparture();Check(game.LastLocalIntentResult==ResultCode.Accepted,"confirmation submits existing real work exactly once");
            game.Press("ten");await WaitManualTime();Check(game.Session.GetActorView(game.Session.PlayerControl).Messages.Any(m=>m.KnownBody==null),"sealed delivered letter exists");Check(game.WorkScreen=="closed"&&!game.WorkSummary.Contains("完了"),"sealed content not leaked into work screen");await Capture("sealed",true);await TalkHold(1);game.Press("work-details");
            Check(game.WorkScreen==(WorkVoluntary?"点検を引き受けますか":"closed"),"opening shows actual voluntary or binding state");game.SaveCheckpoint("s0");await Capture("s0",true);if(WorkVoluntary)Check(game.ReviewWorkLayout(),"assent choices fit the actual viewport");await TalkHold(3);
            if(WorkVoluntary&&!film){
                string hash=ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());game.Press("work-close");game.Press("history");Check(game.WorkHistoryVisible&&hash==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"history is view only");await Capture("history",true);game.LoadCheckpoint("s0");game.Press("work-details");
                game.Press("work-refuse");Check(game.Session.DiagnosticSnapshot().Tasks.Any(t=>t.Executor==game.Session.PlayerControl&&t.Agreement==AgreementPhase.Declined),"actual voluntary refusal is respected");await Capture("declined",true);game.LoadCheckpoint("s0");game.Press("work-details");
            }
            await WorkAcceptAndRun();
            Check(game.WorkScreen=="点検の報告","actual result card visible");game.ReviewFraction(0);game.SaveCheckpoint("s1");await Capture("s1",true);await Capture("final",true);await TalkHold(4);game.Press("work-close");game.Press("save");await WaitSave();
        }
        File.WriteAllText(Path.Combine(TestOutput,restore?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("WORK_CONVERSATION_PASSED "+FlowCase);
    }
}
