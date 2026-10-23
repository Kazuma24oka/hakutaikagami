using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class NewGameFrontEnd
{
    const int MilitaryCheckpoints=17;
    long ArmyEnd()=>game!.Session.DiagnosticSnapshot().NewMilitary!.Orders.Last().Routes.SelectMany(r=>r).Select(l=>l.EndAt).DefaultIfEmpty(game.Session.Seconds).Max();
    async Task MilitaryStep(int step)
    {
        await Frame();await Frame();var s=game!.Session;
        if(step==1){if(!InitialService)await ServiceStep(1);game.ChooseMilitary(NewMilitaryAction.Request);}
        if(step==2){game.Advance(s.Seconds+2);OpenAllServiceLetters();}
        if(step==3){game.ChooseMilitary(NewMilitaryAction.Accept);game.Advance(s.Seconds+1);Check(s.DiagnosticSnapshot().NewMilitary!.Events.Any(e=>e.Effect==MilitaryEffect.Permission)&&!NewGameMilitary.View(s.DiagnosticSnapshot()).CanPickup,"permission physically effective but sealed notice has not granted known pickup");}
        if(step==4){game.Advance(s.Seconds+1);OpenAllServiceLetters();Check(s.DiagnosticSnapshot().Units.All(u=>u.Commander==s.DiagnosticSnapshot().NewMilitary!.Terms.Principal),"opening permission never transfers command");}
        if(step==5)game.ChooseMilitary(NewMilitaryAction.Pickup);
        if(step==6)game.ChooseMilitary(NewMilitaryAction.Load);
        if(step==7)game.ChooseMilitary(NewMilitaryAction.Deploy);
        if(step==8){var a=NewGameMilitary.Anchor(s.DiagnosticSnapshot());game.ExerciseMilitaryLine(new(a.X+20000,a.Y-12000),new(a.X+20000,a.Y+12000));game.Advance(s.Seconds+2);Check(NewGameMilitary.Moving(s.DiagnosticSnapshot()),"actual terrain-picked line starts multi-unit march");}
        if(step==9){game.Advance(ArmyEnd()+1);Check(!NewGameMilitary.Moving(s.DiagnosticSnapshot())&&s.GetBranchView(s.PlayerControl).Position==FieldWorld.ToWorld(NewGameMilitary.Position(s.DiagnosticSnapshot(),0,s.Seconds)),"same officer's displayed world position equals deployed lead unit");Check(game.MilitaryModelsFit(),"all three placed unit models fit the actual camera");}
        if(step==10){game.ChooseMilitary(NewMilitaryAction.Return);game.Press("service-release");var w=s.DiagnosticSnapshot();var message=w.NewMilitary!.Requests.Last(q=>q.Input.Action==NewMilitaryAction.Return).Message!;var mission=w.Couriers!.Missions.Single(c=>w.Messaging.Attempts.Single(a=>a.Id==c.Attempt).MessageId==message);Check(mission.FirstMile is {Length:>0},"field return request has a real first-mile path to the fixed known home address");game.Advance(mission.ReadyAt+2);Check(!s.DiagnosticSnapshot().NewMilitary!.Events.Any(e=>e.Effect==MilitaryEffect.Returned)&&s.DiagnosticSnapshot().Service!.Events.Any(e=>e.Kind==CareerEventKind.ReleaseWaiting),"offsite return and service departure wait for actual handover");}
        if(step==11)game.ChooseMilitary(NewMilitaryAction.Recall);
        if(step==12){game.Advance(ArmyEnd()+1);Check(NewGameMilitary.AtHome(s.DiagnosticSnapshot())&&NewGameMilitary.Deployed(s.DiagnosticSnapshot()),"all units reach home before actual entry");}
        if(step==13){game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);Check(NewGameMilitary.View(s.DiagnosticSnapshot()).Carry==0,"actual remaining house stock is returned");}
        if(step==14){game.ChooseMilitary(NewMilitaryAction.Report);Check(!s.DiagnosticSnapshot().NewMilitary!.Events.Any(e=>e.Effect==MilitaryEffect.Returned),"sending report is insufficient until lord physically receives it");}
        if(step==15){game.Advance(s.Seconds+3);OpenAllServiceLetters();Check(s.DiagnosticSnapshot().NewMilitary!.Events.Any(e=>e.Effect==MilitaryEffect.Returned)&&NewGameMilitary.View(s.DiagnosticSnapshot()).Status.Contains("返還の通知を確認"),"physical report completes command handover and opened notice confirms it");Check(NewGameService.View(s.DiagnosticSnapshot()).House==null,"service release resumes only after military handover");}
        if(step==16){game.SetDestination("castle");game.Press("depart");game.Advance(s.Seconds+5);Check(s.GetBranchView(s.PlayerControl).Walking,"released same officer can actually resume road travel");}
        game.Press("save");await WaitSave();game.Refresh();
    }
    async Task BeginNewMilitaryFlow()
    {
        Check(ProjectSettings.GetSetting("application/run/main_scene").AsString()=="res://Scenes/NewGameFrontEnd.tscn","military starts from the adopted normal no-scene entry");await Capture("menu");Press("manage");await Frame();await Frame();manager!.ExerciseServiceOfficer(ServiceSite,InitialService);await Capture("registered-officer");PressText(manager,"スタート画面へ");await Frame();Press("new");Press("next");selectionMode.Select(1);selectionMode.EmitSignal(OptionButton.SignalName.ItemSelected,1);registeredRows.Select(0);Press("next");Press("next");Press("confirm");await Frame();string id=chosen.Single().Id;int site=Array.FindIndex(NationalRoutes.Sites,s=>s.Id.Value==ServiceSite);jumpChoice.Select(site);jumpChoice.EmitSignal(OptionButton.SignalName.ItemSelected,site);await Frame();map!.PressSite(ServiceSite);PickRow(id);Press("begin");await Frame();await Frame();Check(game!.Session.PlayerControl.Value==id,"chosen library officer remains the same world actor");File.WriteAllText(Path.Combine(TestOutput,"campaign-instance.txt"),game.Profile.Instance);
        if(FlowCase.StartsWith("dirretry-")){await BeginDirectiveRetryContinuation();return;}if(FlowCase.StartsWith("directive-")){await BeginDirectiveContinuation();return;}if(FlowCase.StartsWith("unitrec-")){await BeginUnitRecipientContinuation();return;}if(FlowCase.StartsWith("saveui-")){await BeginResponsiveSaveContinuation();return;}if(FlowCase.StartsWith("target-")){await BeginKnownTargetContinuation();return;}if(FlowCase.StartsWith("known-")){await BeginRecognitionContinuation();return;}if(FlowCase.StartsWith("repeat-")){await BeginRepeatedExerciseContinuation();return;}if(FlowCase.StartsWith("renewal-")){await BeginMilitaryRenewalContinuation();return;}if(FlowCase.StartsWith("cancelnotice-")){await BeginUnitCancelRecoveryContinuation();return;}if(FlowCase.StartsWith("cancel-")){await BeginUnitCancelContinuation();return;}if(FlowCase.StartsWith("evidence-")){await BeginUnitEvidenceContinuation();return;}if(FlowCase.StartsWith("deadline-")){await BeginUnitDeadlineContinuation();return;}if(FlowCase.StartsWith("command-")){await BeginUnitCommandContinuation();return;}if(FlowCase.StartsWith("held-")){await BeginUnitHeldContinuation();return;}if(FlowCase.StartsWith("reply-")){await BeginUnitReplyContinuation();return;}if(FlowCase.StartsWith("inquiry-")){await BeginUnitInquiryContinuation();return;}if(FlowCase.StartsWith("retry-")){await BeginUnitRetryContinuation();return;}if(FlowCase.StartsWith("rout-")){await BeginRoutContinuation();return;}
        if(FlowCase.StartsWith("withdrawal-")){await BeginWithdrawalContinuation();return;}
        if(FlowCase.StartsWith("combat-")){await BeginCombatContinuation();return;}
        for(int n=0;n<MilitaryCheckpoints;n++){await MilitaryStep(n);game.SaveCheckpoint("military"+n);await Capture("s"+n,true);}
        await Capture("final",true);
    }
    async Task RestoreNewMilitaryFlow()
    {
        Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();
        for(int n=0;n<MilitaryCheckpoints;n++){game!.LoadCheckpoint("military"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"restored exact military stock,unit models and officer pose at"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<MilitaryCheckpoints;next++)await MilitaryStep(next);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same military future produces identical full world from"+n);await Capture("continued-s"+n,true);GD.Print("NEW_MILITARY_RESTORED "+FlowCase+" "+n);}
    }
    async Task RunNewMilitaryChecks()
    {
        if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreNewMilitaryFlow();else await BeginNewMilitaryFlow();
        if(OS.GetCmdlineUserArgs().Contains("--new-military-film")){string dir=Path.Combine(TestOutput,"film");Directory.CreateDirectory(dir);game!.LoadCheckpoint("military8");game.Press("auto");for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"walk-"+n.ToString("D3")+".png"));}game.Press("pause");}
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("NEW_MILITARY_PASSED "+FlowCase);
    }
}
