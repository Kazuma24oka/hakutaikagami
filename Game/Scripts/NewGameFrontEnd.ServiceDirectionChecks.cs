using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    async Task ServiceDirectionStep(int n)
    {
        if(n==0){await RelationsStep(1);await RelationsStep(2);await RelationsStep(3);}
        if(n==1){
            game!.ChooseMilitary(NewMilitaryAction.Request);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.ChooseMilitary(NewMilitaryAction.Accept);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.ChooseMilitary(NewMilitaryAction.Pickup);game.ChooseMilitary(NewMilitaryAction.Load);
            if(FlowCase=="housearmy-loss"){game.ChooseMilitary(NewMilitaryAction.Deploy);game.ChooseMilitary(NewMilitaryAction.CombatPermit);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.ChooseMilitary(NewMilitaryAction.CombatStart);game.Advance(game.Session.Seconds+2);game.ChooseMilitary(NewMilitaryAction.CombatEnd);game.ChooseMilitary(NewMilitaryAction.Recall);game.Advance(ArmyEnd()+1);game.ChooseMilitary(NewMilitaryAction.Enter);Check(NewGameMilitary.TotalStrength(game.Session.DiagnosticSnapshot(),game.Session.Seconds)<100,"old house has actual exercise losses before release");}
            game.Press("work-close");
            game.Press("work-contact");game.Press("work-reply");game.Press("work-contact-service");game.Press("work-reply");await Frame();Check(game.ReviewWorkLayout(),"continuing or requesting release fits compact conversation");await Capture("service-choice",true);string h=ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());game.Press("work-service-continue");Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==h,"choosing to continue changes no service records");game.Press("work-reply");game.Press("work-contact-service");game.Press("work-reply");game.Press("work-service-release");game.Press("work-reply");Check(NewGameService.View(game.Session.DiagnosticSnapshot()).House!=null,"spoken release request does not instantly erase membership");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();await Frame();Check(game.Session.DiagnosticSnapshot().Service!.Events.Any(e=>e.Kind==CareerEventKind.ReleaseWaiting)&&NewGameMilitary.View(game.Session.DiagnosticSnapshot()).Custody,"actual principal reply waits for real military handover");await Capture("handover-wait",true);}
        if(n==2){game!.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);game.Advance(game.Session.Seconds+3);OpenAllServiceLetters();await Frame();var w=game.Session.DiagnosticSnapshot();Check(NewGameService.View(w).House==null&&!NewGameMilitary.View(w).Custody&&NewGameMilitary.View(w).Carry==0,"actual troop/food/report handover completes service and known release");Check(game.InfoText.Contains("浪人")&&NewGameWork.Balance(w,w.PlayerControl)==1000&&WorkRelations.Known(w).Length==1,"same officer becomes ronin while keeping wallet and known relationship");await Capture("known-release",true);}
        if(n==3){if(game!.WorkScreen!="closed")game.Press("work-close");game.SetDestination("odawara");await PressDeparture();game.Advance(game.Session.Seconds+3);for(int hop=0;hop<8&&TravelWorld.Active(game.Session.DiagnosticSnapshot());hop++){game.Press("arrive");await WaitManualTime();OpenAllServiceLetters();if(game.WorkScreen!="closed")game.Press("work-close");}await Capture("other-house-arrival",true);Check(game.Session.DiagnosticSnapshot().Locations.Single(l=>l.Actor==game.Session.PlayerControl).Site==new EntityId("site:odawara"),"released same officer really reaches another house's site");game.Press("service-apply");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.Press("service-accept");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();var w=game.Session.DiagnosticSnapshot();Check(NewGameService.View(w).Principal==new EntityId("person:ujiyasu")&&w.Units.All(u=>u.Organization==new EntityId("org:takeda")&&u.Commander!=w.PlayerControl),"new service uses actual agreement and never takes old house troops");Check(WorkRelations.Known(w).Any(c=>c.Other==new EntityId("person:shingen")&&!c.Local),"old known work relationship remains a memory after moving");await Capture("new-service",true);}
        if(game!.WorkScreen!="closed")game.Press("work-close");game.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunServiceDirectionChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<4;n++){game!.LoadCheckpoint("serviceway"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<4;next++)await ServiceDirectionStep(next);await Capture("continued-s"+n,true);}}
        else{await BeginRelationsEntry();for(int n=0;n<4;n++){await ServiceDirectionStep(n);game!.SaveCheckpoint("serviceway"+n);await Capture("s"+n,true);}await Capture("final",true);game!.Press("save");await WaitSave();}
        GD.Print("SERVICE_DIRECTION_PASSED "+FlowCase);
    }
}
