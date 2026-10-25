using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    bool MilitaryWorkOwn=>FlowCase=="milwork-own";
    async Task MilitaryWorkDecision(NewMilitaryAction action)
    {
        var s=game!.Session;game.Press("work-military");game.ReviewMilitaryWorkAction(action);game.Press("work-reply");await Capture("decision-"+action,true);Check(game.ReviewWorkLayout(),"military decision fits compact popup "+action);if(OS.GetCmdlineUserArgs().Contains("--military-work-film"))await TalkHold(.75);
        string before=ContractJson.CanonicalHash(s.DiagnosticSnapshot());game.Press("work-military-confirm");Check(game.ReviewOwnWorkSpeaker&&before==ContractJson.CanonicalHash(s.DiagnosticSnapshot()),"own military reply is uncommitted until continued "+action);
        if(OS.GetCmdlineUserArgs().Contains("--military-work-film"))await TalkHold(1.5);
        game.Press("work-reply");Check(before!=ContractJson.CanonicalHash(s.DiagnosticSnapshot()),"actual existing military input commits "+action);
    }
    async Task MilitaryWorkStep(int n)
    {
        var s=game!.Session;
        if(n==1){await MilitaryWorkDecision(NewMilitaryAction.Request);game.Advance(s.Seconds+2);Check(!game.OffersMilitary(NewMilitaryAction.Accept),"sealed conditions do not enable acceptance");}
        if(n==2){OpenAllServiceLetters();Check(game.OffersMilitary(NewMilitaryAction.Accept),"opened existing offer enables response");game.Press("work-close");}
        if(n==3){await MilitaryWorkDecision(NewMilitaryAction.Accept);game.Advance(s.Seconds+2);Check(!game.OffersMilitary(NewMilitaryAction.Pickup),"sealed permission never enables custody");}
        if(n==4){OpenAllServiceLetters();await Capture("opened-permission",true);Check(game.WorkScreen=="軍務","opened physical military notice uses bottom conversation");if(MilitaryWorkOwn)Check(game.ReviewMilitaryInternalTalk,"own authority uses internal judgement without an invented speaking counterpart or letter label");if(OS.GetCmdlineUserArgs().Contains("--military-work-film"))await TalkHold(2);game.Press("work-close");await MilitaryWorkDecision(NewMilitaryAction.Pickup);}
        if(n==5)await MilitaryWorkDecision(NewMilitaryAction.Load);
        if(n==6){await MilitaryWorkDecision(NewMilitaryAction.Unload);await MilitaryWorkDecision(NewMilitaryAction.Return);await MilitaryWorkDecision(NewMilitaryAction.Report);game.Advance(s.Seconds+3);OpenAllServiceLetters();await Capture("returned-letter",true);if(OS.GetCmdlineUserArgs().Contains("--military-work-film"))await TalkHold(2);game.Press("work-close");Check(NewGameMilitary.View(s.DiagnosticSnapshot()).Status.Contains("返還の通知を確認"),"actual final report received and command return confirmed");Check(s.DiagnosticSnapshot().Units.All(u=>u.Commander==s.DiagnosticSnapshot().NewMilitary!.Terms.Principal)&&game.ReviewPrivateCoins==1000,"command returns to principal without private reward or stock leakage");}
        game.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunMilitaryWorkChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<7;n++){game!.LoadCheckpoint("milwork"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<7;next++)await MilitaryWorkStep(next);await Capture("continued-s"+n,true);}}
        else{
            await BeginPopulationEntry(0,MilitaryWorkOwn?"person:shingen":"person:takeda-nobushige");var s=game!.Session;game.Press("work-military");game.Press("work-reply");string before=ContractJson.CanonicalHash(s.DiagnosticSnapshot());game.Press("work-military-confirm");game.SaveCheckpoint("pending");game.LoadCheckpoint("pending");Check(game.WorkScreen=="closed"&&before==ContractJson.CanonicalHash(s.DiagnosticSnapshot()),"load discards unconfirmed military reply and never executes it");
            for(int n=0;n<7;n++){await MilitaryWorkStep(n);game.SaveCheckpoint("milwork"+n);await Capture("s"+n,true);}
            await Capture("final",true);game.Press("save");await WaitSave();
        }
        File.WriteAllText(Path.Combine(TestOutput,restore?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("MILITARY_WORK_PASSED "+FlowCase);
    }
}
