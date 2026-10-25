using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    bool DebriefLoss=>FlowCase=="debrief-loss";
    async Task DebriefStep(int n)
    {
        if(n==0){game!.ChooseMilitary(NewMilitaryAction.Request);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.ChooseMilitary(NewMilitaryAction.Accept);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.ChooseMilitary(NewMilitaryAction.Pickup);game.ChooseMilitary(NewMilitaryAction.Load);game.ChooseMilitary(NewMilitaryAction.Deploy);}
        if(n==1){
            if(DebriefLoss){game!.ChooseMilitary(NewMilitaryAction.CombatPermit);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.ChooseMilitary(NewMilitaryAction.CombatStart);game.Advance(game.Session.Seconds+2);game.ChooseMilitary(NewMilitaryAction.CombatEnd);}
            else {var a=NewGameMilitary.Anchor(game!.Session.DiagnosticSnapshot());game.ExerciseMilitaryLine(new(a.X+20000,a.Y-12000),new(a.X+20000,a.Y+12000));game.Advance(ArmyEnd()+1);}
            game!.ChooseMilitary(NewMilitaryAction.Recall);game.Advance(ArmyEnd()+1);game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);game.Advance(game.Session.Seconds+3);Check(NewGameMilitary.Debriefs(game.Session.DiagnosticSnapshot()).Length==0,"unopened actual return notice does not reveal feedback");}
        if(n==2){OpenAllServiceLetters();game!.Press("work-debrief");game.Press("work-reply");await Frame();var d=NewGameMilitary.Debriefs(game.Session.DiagnosticSnapshot()).Single();Check((d.Losses>0)==DebriefLoss,"received final report selects correct next duty without blame");Check(game.ReviewWorkLayout(),"debrief and next duty fit compact conversation");await Capture("return-feedback",true);game.Press("work-close");}
        if(n==3){game!.Press("work-debrief");game.Press("work-reply");game.Press("work-debrief-next");game.Press("work-reply");game.Press("work-close");Check(NewGameMilitary.Cases(game.Session.DiagnosticSnapshot()).Length==2&&!NewGameMilitary.View(game.Session.DiagnosticSnapshot()).CanPickup,"debrief choice creates real next case, not instant permission");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();}
        if(n==4){game!.ChooseMilitary(NewMilitaryAction.Accept);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.ChooseMilitary(NewMilitaryAction.Pickup);game.ChooseMilitary(NewMilitaryAction.Load);game.ChooseMilitary(NewMilitaryAction.Deploy);var w=game.Session.DiagnosticSnapshot();int before=NewGameMilitary.TotalStrength(w,game.Session.Seconds);var d=NewGameMilitary.Debriefs(w).Single();Check(before==d.Returned,"next case retains returned strength without resurrection");game.Press("work-followup-line");game.Press("work-reply");await Frame();await Capture("next-duty",true);game.Press("work-followup-move");game.Press("work-reply");Check(NewGameMilitary.Moving(game.Session.DiagnosticSnapshot()),"agreed next-duty line starts actual unit movement");game.Advance(ArmyEnd()+1);var current=game.Session.DiagnosticSnapshot();Check(NewGameMilitary.Position(current,0,game.Session.Seconds).X==NewGameMilitary.Anchor(current).X+(DebriefLoss?10000:25000),"actual3D unit reaches the outcome-specific line");await Frame();await Capture("next-duty-position",true);}
        if(game!.WorkScreen!="closed")game.Press("work-close");game.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunDebriefChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<5;n++){game!.LoadCheckpoint("debrief"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<5;next++)await DebriefStep(next);await Capture("continued-s"+n,true);}}
        else{await BeginDutyEntry();for(int n=0;n<5;n++){await DebriefStep(n);game!.SaveCheckpoint("debrief"+n);await Capture("s"+n,true);}await Capture("final",true);game!.Press("save");await WaitSave();}
        GD.Print("MILITARY_DEBRIEF_PASSED "+FlowCase);
    }
}
