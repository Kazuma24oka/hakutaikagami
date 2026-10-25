using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    void CheckOldHouseArmy()
    {
        var w=game!.Session.DiagnosticSnapshot();var old=w.OtherMilitaryLedgers!.Single();
        Check(ContractJson.CanonicalHash(old)==File.ReadAllText(Path.Combine(TestOutput,"old-ledger.sha256")),"old owner's actual losses stock orders and letters remain unchanged");
        Check(w.Units.Where(u=>u.Organization==new EntityId("org:takeda")).All(u=>u.Commander==new EntityId("person:shingen")),"former house retains command of its own army");
        Check(w.Units.Select(u=>u.Id).Distinct().Count()==6&&w.ResourceLots.Select(l=>l.Id).Distinct().Count()==w.ResourceLots.Length,"both houses have distinct physical unit and supply identities");
    }
    async Task HouseArmyStep(int n)
    {
        if(n<4){await ServiceDirectionStep(n);if(n==3){var w=game!.Session.DiagnosticSnapshot();File.WriteAllText(Path.Combine(TestOutput,"old-ledger.sha256"),ContractJson.CanonicalHash(w.NewMilitary!));Check(NewGameMilitary.ViewWithAuthority(w).CanRequest,"known new service enables a fresh military discussion after old handover");}}
        if(n==4){
            game!.Press("work-military");game.Press("work-reply");game.ReviewMilitaryWorkAction(NewMilitaryAction.Request);game.Press("work-military-confirm");game.Press("work-reply");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.ChooseMilitary(NewMilitaryAction.Accept);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.ChooseMilitary(NewMilitaryAction.Pickup);game.ChooseMilitary(NewMilitaryAction.Load);
            var w=game.Session.DiagnosticSnapshot();Check(w.NewMilitary!.Terms.Owner==new EntityId("org:hojo")&&NewGameMilitary.TotalStrength(w,w.ElapsedSeconds)==100,"new house grants its own garrison after real offer permission and local pickup");CheckOldHouseArmy();Check(NewGameWork.Balance(w,w.PlayerControl)==1000,"old military supplies never become personal money");await Capture("new-house-pickup",true);
        }
        if(n==5){game!.ChooseMilitary(NewMilitaryAction.Deploy);var a=NewGameMilitary.Anchor(game.Session.DiagnosticSnapshot());game.ExerciseMilitaryLine(new(a.X+20000,a.Y-12000),new(a.X+20000,a.Y+12000));game.Advance(game.Session.Seconds+1);Check(NewGameMilitary.Moving(game.Session.DiagnosticSnapshot()),"new house's units really march to the specified line");CheckOldHouseArmy();await Capture("new-house-marching",true);}
        if(n==6){game!.Advance(ArmyEnd()+1);game.ChooseMilitary(NewMilitaryAction.Recall);game.Advance(ArmyEnd()+1);game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);game.Advance(game.Session.Seconds+3);OpenAllServiceLetters();var w=game.Session.DiagnosticSnapshot();Check(!NewGameMilitary.View(w).Custody&&NewGameMilitary.View(w).Carry==0&&NewGameMilitary.Debriefs(w).Any(d=>d.Principal==new EntityId("person:ujiyasu")),"new house receives actual troops supply and report before its own debrief");CheckOldHouseArmy();game.Press("work-debrief");game.Press("work-reply");await Frame();Check(game.ReviewWorkLayout(),"new house debrief still fits compact conversation");await Capture("new-house-return-feedback",true);}
        if(game!.WorkScreen!="closed")game.Press("work-close");game.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunHouseArmyChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<7;n++){game!.LoadCheckpoint("housearmy"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<7;next++)await HouseArmyStep(next);await Capture("continued-s"+n,true);}}
        else{await BeginRelationsEntry();for(int n=0;n<7;n++){await HouseArmyStep(n);game!.SaveCheckpoint("housearmy"+n);await Capture("s"+n,true);}await Capture("final",true);game!.Press("save");await WaitSave();}
        File.WriteAllText(Path.Combine(TestOutput,restore?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("HOUSEHOLD_MILITARY_PASSED "+FlowCase);
    }
}
