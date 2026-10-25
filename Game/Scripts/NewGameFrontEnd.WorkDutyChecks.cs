using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    async Task BeginDutyEntry()
    {
        Check(ProjectSettings.GetSetting("application/run/main_scene").AsString()=="res://Scenes/NewGameFrontEnd.tscn","normal no-scene startup uses adopted frontend");await Capture("menu");Press("manage");await Frame();await Frame();manager!.ExerciseServiceOfficer("site:kofu",true);await Capture("registered-officer");PressText(manager,"スタート画面へ");await Frame();Press("new");Press("next");selectionMode.Select(1);selectionMode.EmitSignal(OptionButton.SignalName.ItemSelected,1);registeredRows.Select(0);Press("next");Press("next");Press("confirm");await Frame();string id=chosen.Single().Id;int site=Array.FindIndex(NationalRoutes.Sites,s=>s.Id.Value==ServiceSite);jumpChoice.Select(site);jumpChoice.EmitSignal(OptionButton.SignalName.ItemSelected,site);await Frame();map!.PressSite(ServiceSite);PickRow(id);Press("begin");await Frame();await Frame();Check(game!.Session.PlayerControl.Value==id,"normal menu begins the chosen created person");File.WriteAllText(Path.Combine(TestOutput,"campaign-instance.txt"),game.Profile.Instance);
    }
    async Task DutyStep(int n)
    {
        if(n==1){await PressLocalIntent("work");game!.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.Press("ten");await WaitManualTime();game.Press("work-details");await Frame();Check(WorkDuty.Candidate(game.Session.DiagnosticSnapshot())!=null,"opened lord recognition enables duty offer");game.Press("work-close");game.Press("work-duty");await Frame();game.Press("work-reply");await Frame();await Capture("duty-offer",true);game.Press("work-duty-later");Check(WorkDuty.Active(game.Session.DiagnosticSnapshot())==null,"postponing has no role or money effect");game.Press("work-duty");game.Press("work-reply");game.Press("work-duty-accept");game.Press("work-reply");Check(game.InfoText.Contains(WorkDuty.Title)&&game.ReviewPrivateCoins==1000,"explicit agreement changes role display without giving money or command");}
        if(n==2){game!.Advance(game.Session.Seconds+LocalWorkDemand.RecordAge+1);OpenAllServiceLetters();await PressLocalIntent("work");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.Press("ten");await WaitManualTime();var w=game.Session.DiagnosticSnapshot();var task=w.Tasks.Last(t=>t.Executor==w.PlayerControl&&t.Action==ActionKind.Inspect);Check(task.Kind==TaskKind.BindingOrder&&NewGameWork.Reward(w,task)==20,"new lord order freezes agreed fee20");game.Press("work-details");game.Press("work-reply");game.Press("work-pay");game.Press("work-reply");game.Press("work-close");Check(game.ReviewPrivateCoins==1020,"actual duty fee paid from existing lord wallet");await Frame();await Capture("duty-paid",true);}
        if(n==3){game!.Press("service-release");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();Check(WorkDuty.Active(game.Session.DiagnosticSnapshot())==null&&!game.InfoText.Contains(WorkDuty.Title)&&game.ReviewPrivateCoins==1020,"release ends duty display without undoing earned money");}
        game!.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunDutyChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<4;n++){game!.LoadCheckpoint("duty"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<4;next++)await DutyStep(next);await Capture("continued-s"+n,true);}}
        else{await BeginDutyEntry();for(int n=0;n<4;n++){await DutyStep(n);game!.SaveCheckpoint("duty"+n);await Capture("s"+n,true);}await Capture("final",true);game!.Press("save");await WaitSave();}
        GD.Print("WORK_DUTY_PASSED "+FlowCase);
    }
}
