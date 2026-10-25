using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    async Task BeginRelationsEntry()
    {
        Check(ProjectSettings.GetSetting("application/run/main_scene").AsString()=="res://Scenes/NewGameFrontEnd.tscn","normal no-scene startup uses adopted frontend");await Capture("menu");Press("manage");await Frame();await Frame();manager!.ExerciseServiceOfficer("site:kofu",false);await Capture("registered-officer");PressText(manager,"スタート画面へ");await Frame();Press("new");Press("next");selectionMode.Select(1);selectionMode.EmitSignal(OptionButton.SignalName.ItemSelected,1);registeredRows.Select(0);Press("next");Press("next");Press("confirm");await Frame();string id=chosen.Single().Id;int site=Array.FindIndex(NationalRoutes.Sites,s=>s.Id.Value==ServiceSite);jumpChoice.Select(site);jumpChoice.EmitSignal(OptionButton.SignalName.ItemSelected,site);await Frame();map!.PressSite(ServiceSite);PickRow(id);Press("begin");await Frame();await Frame();Check(game!.Session.PlayerControl.Value==id,"normal menu begins the chosen created person");File.WriteAllText(Path.Combine(TestOutput,"campaign-instance.txt"),game.Profile.Instance);
    }
    async Task RelationsStep(int n)
    {
        if(n==1){await PressLocalIntent("work");game!.Press("ten");await WaitManualTime();game.Press("work-details");game.Press("work-reply");game.Press("work-assent");game.Press("work-reply");game.Press("work-run");await WaitManualTime();var w=game!.Session.DiagnosticSnapshot();var issuer=w.Tasks.Last(t=>t.Action==ActionKind.Inspect&&t.Executor==w.PlayerControl).Issuer;Check(WorkRelations.Facts(w,issuer,w.PlayerControl).Length==1&&WorkRelations.Known(w).Length==0,"issuer knows fulfillment but unopened statement does not leak social facts");}
        if(n==2){game!.Press("work-details");game.Press("work-close");game.Press("work-contact");game.Press("work-reply");await Frame();Check(game.ReviewWorkLayout(),"known relationship and working choices fit compact conversation");await Capture("work-connection",true);game.Press("work-contact-apply");game.Press("work-reply");Check(NewGameService.View(game.Session.DiagnosticSnapshot()).House==null,"relationship conversation does not itself create service");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.Press("work-close");game.Press("work-contact");game.Press("work-reply");await Frame();await Capture("service-conditions",true);game.Press("work-contact-accept");game.Press("work-reply");game.Advance(game.Session.Seconds+1);var w=game.Session.DiagnosticSnapshot();var issuer=w.Service!.Cases.Last().Principal;Check(NewGameService.BoundAt(w,issuer,game.Session.Seconds,game.Session.Revision)&&NewGameService.View(w).House==null,"physical agreement precedes knowledge of membership");game.Advance(game.Session.Seconds+1);OpenAllServiceLetters();game.Press("work-close");Check(NewGameService.View(game.Session.DiagnosticSnapshot()).Principal==issuer&&game.Session.DiagnosticSnapshot().Units.Length==0,"same created ronin becomes retainer only through actual agreement and known notice");}
        if(n==3){game!.Press("work-contact");game.Press("work-reply");game.Press("work-contact-duty");game.Press("work-reply");game.Press("work-duty-accept");game.Press("work-reply");Check(game.InfoText.Contains(WorkDuty.Title),"known work relationship connects same retainer to actual accepted duty");}
        if(n==4){game!.Press("work-contact");game.Press("work-reply");game.Press("work-contact-military");game.Press("work-reply");game.Press("work-military-confirm");game.Press("work-reply");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();await Frame();Check(NewGameMilitary.View(game.Session.DiagnosticSnapshot()).CanAccept&&!NewGameMilitary.View(game.Session.DiagnosticSnapshot()).CanPickup,"social consultation only requests real military terms, not command permission");await Capture("military-consultation",true);game.Press("work-close");}
        game!.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunRelationsChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<5;n++){game!.LoadCheckpoint("relations"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<5;next++)await RelationsStep(next);await Capture("continued-s"+n,true);}}
        else{await BeginRelationsEntry();for(int n=0;n<5;n++){await RelationsStep(n);game!.SaveCheckpoint("relations"+n);await Capture("s"+n,true);}await Capture("final",true);game!.Press("save");await WaitSave();}
        GD.Print("WORK_RELATIONS_PASSED "+FlowCase);
    }
}
