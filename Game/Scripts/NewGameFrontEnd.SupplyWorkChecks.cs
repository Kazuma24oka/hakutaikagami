using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    bool SupplyFilm=>OS.GetCmdlineUserArgs().Contains("--supply-work-film");
    async Task SupplyHold(double t){if(SupplyFilm)await TalkHold(t);}
    async Task SupplyWorkStep(int n)
    {
        var s=game!.Session;
        if(n==0){Check(!game.ReviewSupplyWorkNeeded,"no supply work before any military authority");game.ChooseMilitary(NewMilitaryAction.Request);game.Advance(s.Seconds+2);OpenAllServiceLetters();game.Press("work-close");game.ChooseMilitary(NewMilitaryAction.Accept);game.Advance(s.Seconds+2);Check(!game.ReviewSupplyWorkNeeded,"sealed permission alone creates no supply work");}
        if(n==1){OpenAllServiceLetters();game.Press("work-close");await MilitaryWorkDecision(NewMilitaryAction.Pickup);Check(game.ReviewSupplyWorkNeeded,"actual custody and empty carried food create a visible preparation job");await Capture("need",true);await SupplyHold(1);}
        if(n==2){
            game.Press("work-supply");game.Press("work-reply");await Capture("terms",true);Check(game.ReviewWorkLayout(),"supply ownership and amount fit compact popup");await SupplyHold(2);
            string before=ContractJson.CanonicalHash(s.DiagnosticSnapshot());game.Press("work-supply-confirm");Check(game.ReviewOwnWorkSpeaker&&before==ContractJson.CanonicalHash(s.DiagnosticSnapshot()),"choosing supply reply does not move stock");game.SaveCheckpoint("supply-pending");game.LoadCheckpoint("supply-pending");s=game.Session;Check(game.WorkScreen=="closed"&&before==ContractJson.CanonicalHash(s.DiagnosticSnapshot())&&game.ReviewSupplyWorkNeeded,"load discards pending reply and preserves stock");
            game.Press("work-supply");game.Press("work-reply");game.Press("work-supply-confirm");await SupplyHold(1);game.Press("work-reply");var w=s.DiagnosticSnapshot();Check(NewGameMilitary.View(w).Carry==100*86400L&&w.ResourceLots.Where(l=>l.Kind=="rice").Sum(l=>l.Quantity)==NewGameMilitary.InitialStock&&game.ReviewPrivateCoins==1000&&w.Transfers.Length==0,"existing load moves actual house food without minting stock or personal money");Check(!game.ReviewSupplyWorkNeeded,"prepared job disappears after actual load");await Capture("received",true);await SupplyHold(2);game.Press("work-supply-done");
        }
        if(n==3){await MilitaryWorkDecision(NewMilitaryAction.Unload);Check(game.ReviewSupplyWorkNeeded,"unloading actual food restores need while duty is active");await Capture("unloaded",true);await SupplyHold(1);}
        if(n==4){game.ChooseMilitary(NewMilitaryAction.Return);Check(!game.ReviewSupplyWorkNeeded,"returning duty never advertises another load");game.ChooseMilitary(NewMilitaryAction.Report);game.Advance(s.Seconds+3);OpenAllServiceLetters();game.Press("work-close");Check(!game.ReviewSupplyWorkNeeded&&NewGameMilitary.View(s.DiagnosticSnapshot()).Status.Contains("返還の通知を確認"),"completed handover clears preparation need");}
        if(n==5){game.SetDestination("suwa");game.Press("depart");await WaitDeparture();game.Advance(game.Session.Seconds+3);Check(!game.ReviewSupplyWorkNeeded,"travelling character receives no local supply offer");}
        game.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunSupplyWorkChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<6;n++){game!.LoadCheckpoint("supply"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<6;next++)await SupplyWorkStep(next);await Capture("continued-s"+n,true);}}
        else{await BeginPopulationEntry(0,FlowCase=="supply-own"?"person:shingen":"person:takeda-nobushige");for(int n=0;n<6;n++){await SupplyWorkStep(n);game!.SaveCheckpoint("supply"+n);await Capture("s"+n,true);}await Capture("final",true);game!.Press("save");await WaitSave();}
        File.WriteAllText(Path.Combine(TestOutput,restore?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("SUPPLY_WORK_PASSED "+FlowCase);
    }
}
