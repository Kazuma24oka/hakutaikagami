using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class NewGameFrontEnd
{
    const int UnitInquiryCheckpoints=12;
    TroopDispatch CurrentInquiry()=>game!.Session.DiagnosticSnapshot().NewMilitary!.UnitInquiry!.Dispatches.Last();
    TroopReceipt CurrentInquiryReply()=>game!.Session.DiagnosticSnapshot().NewMilitary!.UnitInquiry!.Receipts.Last();
    UnitPostInquiryAnswer CurrentInquiryQuote(){var w=game!.Session.DiagnosticSnapshot();return ContractJson.Read<UnitPostInquiryAnswer>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==CurrentInquiryReply().ReplyMessage).Body));}
    async Task UnitInquiryStep(int n){await Frame();var s=game!.Session;
        if(n==1){var a=NewGameMilitary.Anchor(s.DiagnosticSnapshot());game.ExerciseMilitaryLine(new(a.X+20000,a.Y-12000),new(a.X+20000,a.Y+12000));game.Advance(ArmyEnd()+1);}
        if(n==2){game.ChooseMilitary(NewMilitaryAction.Recall);game.ChooseMilitary(NewMilitaryAction.Handoff);game.ChooseMilitary(NewMilitaryAction.Withdraw);game.Press("military-frame");}
        if(n==3)game.Advance(Math.Max(PersonalEnd(),ArmyEnd())+1);
        if(n==4)game.ChooseMilitary(NewMilitaryAction.RemoteRecall);
        if(n==5){var d=s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Dispatches.Single();game.Advance(d.DueAt);Check(!MessageFlow.FailureKnown(s.DiagnosticSnapshot().Messaging,d.Attempt,s.PlayerControl.Value),"initial failure is not known before its physical return");}
        if(n==6)game.ChooseMilitary(NewMilitaryAction.InquireUnitHome);
        if(n==7){game.Advance(CurrentInquiry().DueAt);Check(!CurrentInquiryReply().Applied&&NewGameMilitary.UnitInquiryStatus(s.DiagnosticSnapshot()).Contains("未受領"),"receiver writes a quote without executing an order or revealing the return early");}
        if(n==8){game.Advance(CurrentInquiryReply().ReplyDueAt);var w=s.DiagnosticSnapshot();var d=w.NewMilitary!.Withdrawal!.Dispatches.Single();var quote=CurrentInquiryQuote();Check(!quote.Originals.Single().Received&&quote.Soldiers==40,"delivered inquiry quotes only the reception unit's own record and soldiers");Check(!MessageFlow.FailureKnown(w.Messaging,d.Attempt,s.PlayerControl.Value)&&!NewGameMilitary.UnitRetryAllowed(w,NewMilitaryAction.RetryRecallHome),"unreceived quote does not fabricate original failure-report receipt or enable retry");}
        if(n==9){game.Advance(s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Receipts.Single().ReplyDueAt);Check(NewGameMilitary.UnitRetryAllowed(s.DiagnosticSnapshot(),NewMilitaryAction.RetryRecallHome),"the actual failure report now enables retry independently of the quote");game.ChooseMilitary(NewMilitaryAction.RetryRecallHome);}
        if(n==10){var d=s.DiagnosticSnapshot().NewMilitary!.UnitRetry!.Dispatches.Last();game.Advance(d.DueAt);game.Advance(s.DiagnosticSnapshot().NewMilitary!.UnitRetry!.Receipts.Last().ReplyDueAt);game.ChooseMilitary(NewMilitaryAction.InquireUnitHome);game.Advance(CurrentInquiry().DueAt);game.Advance(CurrentInquiryReply().ReplyDueAt);var w=s.DiagnosticSnapshot();Check(CurrentInquiryQuote().Originals.Single() is{Received:true,Applied:true},"a later physical query reports the original's actual receipt and single effect");Check(!MessageFlow.Knows(w.Messaging,d.Message,w.PlayerControl.Value)&&w.Messaging.Receipts.Count(r=>r.MessageId==d.Message)==1,"quoted content never creates an extra original receipt or forwarded-original knowledge");}
        if(n==11){game.ChooseMilitary(NewMilitaryAction.Attach);game.ChooseMilitary(NewMilitaryAction.Reclaim);game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);game.Advance(s.Seconds+3);OpenAllServiceLetters();Check(s.DiagnosticSnapshot().NewMilitary!.Events.Any(e=>e.Effect==MilitaryEffect.Returned),"same person completes physical cargo return and command handover");}
        game.Press("save");await WaitSave();game.Refresh();
    }
    async Task BeginUnitInquiryContinuation(){for(int n=1;n<=7;n++)await MilitaryStep(n);for(int n=0;n<UnitInquiryCheckpoints;n++){await UnitInquiryStep(n);game!.SaveCheckpoint("unitinquiry"+n);await Capture("s"+n,true);}await Capture("final",true);}
    async Task RestoreUnitInquiry(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<UnitInquiryCheckpoints;n++){game!.LoadCheckpoint("unitinquiry"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"actual inquiry/original/retry model pose restored at"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<UnitInquiryCheckpoints;next++)await UnitInquiryStep(next);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same quoted records and formal receipts future from"+n);await Capture("continued-s"+n,true);GD.Print("UNIT_INQUIRY_RESTORED "+n);}}
    async Task RunUnitInquiryChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreUnitInquiry();else await BeginNewMilitaryFlow();if(OS.GetCmdlineUserArgs().Contains("--unit-inquiry-film")){game!.LoadCheckpoint("unitinquiry6");game.Press("auto");string dir=Path.Combine(TestOutput,"film");Directory.CreateDirectory(dir);for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"inquiry-"+n.ToString("D3")+".png"));}game.Press("pause");}File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("UNIT_INQUIRY_PASSED "+FlowCase);}
}
