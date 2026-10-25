using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    const int WithdrawalCheckpoints=12;
    long PersonalEnd()=>game!.Session.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Journeys.Last().Route.Select(l=>l.EndAt).DefaultIfEmpty(game.Session.Seconds).Max();
    async Task WithdrawalStep(int n){await Frame();var s=game!.Session;if(FlowCase=="withdrawal-search"){await WithdrawalSearchStep(n);return;}
        if(n==1)game.ChooseMilitary(NewMilitaryAction.Handoff);
        if(n==2){game.ChooseMilitary(NewMilitaryAction.Withdraw);game.Advance(s.Seconds+2);var w=s.DiagnosticSnapshot();Check(NewGameMilitary.PersonMoving(w)&&NewGameMilitary.Detached(w),"same selected person follows an independent terrain path");Check(w.ResourceLots.Single(l=>l.Id==NewGameMilitary.Carry).MobileHolder==NewGameMilitary.UnitIds[0],"physical cargo stays with unit, ownership unchanged");}
        if(n==3){game.Advance(PersonalEnd()+1);var w=s.DiagnosticSnapshot();Check(NewGameMilitary.PersonPosition(w,s.Seconds)==NewGameMilitary.Home(w,0)&&!NewGameMilitary.AtHome(w),"personal shelter arrival does not recall units or end sortie");Check(!NewGameMilitary.View(w).CanEnter,"personal arrival alone cannot hand over army");}
        if(n==4){game.ChooseMilitary(NewMilitaryAction.RemoteRecall);game.Advance(s.Seconds+1);Check(!s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Receipts.Any(),"sent retreat order has no effect before physical arrival");}
        if(n==5){game.Advance(s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Dispatches.Last().DueAt+1);Check(s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Receipts.Last().Delivered&&NewGameMilitary.Moving(s.DiagnosticSnapshot()),"unit reception executes after arrival without moving player");Check(NewGameMilitary.PersonalStatus(s.DiagnosticSnapshot()).Contains("未受領"),"sender cannot know unseen receipt before reply");}
        if(n==6){game.Advance(s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Receipts.Last().ReplyDueAt);Check(s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Replies.Last().Received,"physical return courier reaches known sender post");}
        if(n==7){game.Advance(ArmyEnd()+1);Check(NewGameMilitary.AtHome(s.DiagnosticSnapshot())&&NewGameMilitary.Detached(s.DiagnosticSnapshot()),"all troops home but independent person still needs local attachment");}
        if(n==8){game.ChooseMilitary(NewMilitaryAction.Attach);game.ChooseMilitary(NewMilitaryAction.Reclaim);Check(!NewGameMilitary.Detached(s.DiagnosticSnapshot())&&!NewGameMilitary.UnitCarries(s.DiagnosticSnapshot()),"actual local reattachment and cargo handover are separate actions");}
        if(n==9){game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);}
        if(n==10){game.Advance(s.Seconds+3);OpenAllServiceLetters();Check(s.DiagnosticSnapshot().NewMilitary!.Events.Any(e=>e.Effect==MilitaryEffect.Returned),"final report, cargo zero and army entry are required before command handover");}
        if(n==11){game.SetDestination("castle");await PressDeparture();s=game.Session;game.Advance(s.Seconds+3);Check(s.GetBranchView(s.PlayerControl).Walking,"same person continues ordinary travel after handover");}
        game.Press("save");await WaitSave();game.Refresh();if(n==2)Check(game.MilitaryModelsFit(),"army overview contains unit heads at actual viewport aspect");
    }
    async Task WithdrawalSearchStep(int n){var s=game!.Session;
        if(n==1){var a=NewGameMilitary.Anchor(s.DiagnosticSnapshot());game.ExerciseMilitaryLine(new(a.X+30000,a.Y-12000),new(a.X+30000,a.Y+12000));game.ChooseMilitary(NewMilitaryAction.Handoff);}
        if(n==2){game.ChooseMilitary(NewMilitaryAction.Withdraw);game.Advance(s.Seconds+2);}
        if(n==3)game.Advance(PersonalEnd()+1);
        if(n==4){game.ChooseMilitary(NewMilitaryAction.Rejoin);game.Advance(s.Seconds+1);}
        if(n==5){game.Advance(PersonalEnd()+1);Check(!NewGameMilitary.WithdrawalAllowed(s.DiagnosticSnapshot(),new(new("request:check-attach"),NewMilitaryAction.Attach)),"old sighting is not replaced with true moving unit location");}
        if(n==6){game.ChooseMilitary(NewMilitaryAction.PersonalHome);game.Advance(s.Seconds+1);}
        if(n==7){game.Advance(PersonalEnd()+1);Check(NewGameMilitary.PersonPosition(s.DiagnosticSnapshot(),s.Seconds)==NewGameMilitary.Home(s.DiagnosticSnapshot(),0),"missed rejoin does not trap the person away from shelter");}
        if(n==8){var known=s.DiagnosticSnapshot().NewMilitary!.Requests.Last(q=>q.Input.Action==NewMilitaryAction.Form).Input.A!;game.ExercisePersonalMove(known);game.Advance(s.Seconds+1);Check(NewGameMilitary.PersonMoving(s.DiagnosticSnapshot()),"actual ground input chooses own walk destination without a unit command");}
        if(n==9){game.Advance(PersonalEnd()+1);game.ChooseMilitary(NewMilitaryAction.Attach);game.ChooseMilitary(NewMilitaryAction.Reclaim);game.ChooseMilitary(NewMilitaryAction.Recall);game.Advance(ArmyEnd()+1);game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);}
        if(n==10){game.Advance(s.Seconds+3);OpenAllServiceLetters();Check(s.DiagnosticSnapshot().NewMilitary!.Events.Any(e=>e.Effect==MilitaryEffect.Returned),"search, real reunion and local return complete the same loan");}
        if(n==11){game.SetDestination("castle");await PressDeparture();s=game.Session;game.Advance(s.Seconds+3);}
        game.Press("save");await WaitSave();game.Refresh();await Frame();
    }
    async Task BeginWithdrawalContinuation(){for(int n=1;n<=9;n++)await MilitaryStep(n);for(int n=0;n<WithdrawalCheckpoints;n++){await WithdrawalStep(n);game!.SaveCheckpoint("withdrawal"+n);await Capture("s"+n,true);}await Capture("final",true);}
    async Task RestoreWithdrawal(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<WithdrawalCheckpoints;n++){game!.LoadCheckpoint("withdrawal"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"restored independent person/unit/carrier/courier models at"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<WithdrawalCheckpoints;next++)await WithdrawalStep(next);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same complete personal and unit future from"+n);await Capture("continued-s"+n,true);GD.Print("WITHDRAWAL_RESTORED "+n);}}
    async Task RunWithdrawalChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreWithdrawal();else await BeginNewMilitaryFlow();if(OS.GetCmdlineUserArgs().Contains("--withdrawal-film")){game!.LoadCheckpoint("withdrawal2");game.Press("auto");string dir=Path.Combine(TestOutput,"film");Directory.CreateDirectory(dir);for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"withdrawal-"+n.ToString("D3")+".png"));}game.Press("pause");}File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("PERSONAL_WITHDRAWAL_PASSED "+FlowCase);}
}
