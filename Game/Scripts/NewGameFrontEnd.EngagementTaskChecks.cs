using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    const int EngagementTaskCheckpoints=6;
    async Task EngagementTaskStep(int n)
    {
        if(n==0){game!.ChooseMilitary(NewMilitaryAction.Request);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.ChooseMilitary(NewMilitaryAction.Accept);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.ChooseMilitary(NewMilitaryAction.Pickup);game.ChooseMilitary(NewMilitaryAction.Load);game.ChooseMilitary(NewMilitaryAction.Deploy);game.Press("military-frame");}
        if(n==1){game!.ChooseMilitary(NewMilitaryAction.RequestEngagement);await Frame();await Capture("target-selection",true);Check(game.ReviewEngagementPopupLayout(),"target selection remains a compact bounded popup");game.ChooseEngagementTarget(new("org:hojo"));Check(NewGameMilitary.EngagementMandates(game.Session.DiagnosticSnapshot(),game.Session.Seconds).Length==0,"request creates no permission before issuer receipt");}
        if(n==2){game!.Advance(game.Session.Seconds+2);Check(NewGameMilitary.EngagementMandates(game.Session.DiagnosticSnapshot(),game.Session.Seconds).Single().ReceivedAt==null,"unopened physically delivered permission is not personally effective");}
        if(n==3){OpenAllServiceLetters();game!.Refresh();var w=game.Session.DiagnosticSnapshot();var terms=NewGameMilitary.EngagementMandates(w,w.ElapsedSeconds).Single();Check(terms.ReceivedAt==w.ElapsedSeconds&&terms.TargetOwner==new EntityId("org:hojo"),"normal UI opens the real scoped permission for selected house");Check(w.Units.Sum(u=>u.Soldiers)==100&&w.Units.Select(u=>u.Organization).Distinct().Single()==w.NewMilitary!.Terms.Owner,"conditional permission creates neither enemies nor damage");await Capture("opened-permission",true);game.ChooseMilitary(NewMilitaryAction.CancelEngagement);}
        if(n==4){game!.Advance(game.Session.Seconds+2);Check(NewGameMilitary.EngagementMandates(game.Session.DiagnosticSnapshot(),game.Session.Seconds).Single().CancelledAt==null,"unopened cancellation has not silently affected personal permission");}
        if(n==5){OpenAllServiceLetters();game!.Refresh();Check(NewGameMilitary.EngagementMandates(game.Session.DiagnosticSnapshot(),game.Session.Seconds).Single().CancelledAt==game.Session.Seconds,"opening delivered cancellation ends only its scoped permission");await Capture("opened-cancellation",true);}
        if(game!.WorkScreen!="closed")game.Press("work-close");game.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunEngagementTaskChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<EngagementTaskCheckpoints;n++){game!.LoadCheckpoint("engagementtask"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<EngagementTaskCheckpoints;next++)await EngagementTaskStep(next);await Capture("continued-s"+n,true);}}
        else{await BeginNewMilitaryFlow();for(int n=0;n<EngagementTaskCheckpoints;n++){await EngagementTaskStep(n);game!.SaveCheckpoint("engagementtask"+n);await Capture("s"+n,true);}await Capture("final",true);game!.Press("save");await WaitSave();}
        File.WriteAllText(Path.Combine(TestOutput,restore?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("ENGAGEMENT_TASK_DELIVERY_PASSED "+FlowCase);
    }
}
