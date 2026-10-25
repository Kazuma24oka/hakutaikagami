using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    const int CombatCheckpoints=9;
    async Task CombatStep(int n){await Frame();var s=game!.Session;
        if(n==1)game.ChooseMilitary(NewMilitaryAction.CombatPermit);
        if(n==2){game.Advance(s.Seconds+2);Check(!NewGameMilitary.CanStartCombat(s.DiagnosticSnapshot()),"sealed combat permit cannot authorize known attack");OpenAllServiceLetters();Check(NewGameMilitary.CanStartCombat(s.DiagnosticSnapshot()),"opened explicit synthetic permission enables contact");}
        if(n==3)game.ChooseMilitary(NewMilitaryAction.CombatStart);
        if(n==4){game.Advance(s.Seconds+1);Check(NewGameMilitary.TotalStrength(s.DiagnosticSnapshot(),s.Seconds)==90,"simultaneous first-second own loss is ten");}
        if(n==5){game.Advance(s.Seconds+3);game.ChooseMilitary(NewMilitaryAction.Report);Check(s.DiagnosticSnapshot().NewMilitary!.Reports.Last().Soldiers==NewGameMilitary.TotalStrength(s.DiagnosticSnapshot(),s.Seconds),"physical own report freezes dispatched remaining strength");}
        if(n==6){game.ChooseMilitary(NewMilitaryAction.CombatEnd);game.ChooseMilitary(NewMilitaryAction.Recall);game.Advance(ArmyEnd()+1);}
        if(n==7){game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);}
        if(n==8){game.Advance(s.Seconds+3);OpenAllServiceLetters();Check(s.DiagnosticSnapshot().NewMilitary!.Events.Any(e=>e.Effect==MilitaryEffect.Returned),"losses survive return and lord receives final report before command handover");Check(s.DiagnosticSnapshot().Characters.Single(c=>c.Id==s.PlayerControl).Life==LifeState.Adult,"troop loss never deletes the player officer");}
        game.Press("save");await WaitSave();game.Refresh();
    }
    async Task BeginCombatContinuation(){for(int n=1;n<=7;n++)await MilitaryStep(n);for(int n=0;n<CombatCheckpoints;n++){await CombatStep(n);game!.SaveCheckpoint("combat"+n);await Capture("s"+n,true);}await Capture("final",true);}
    async Task RestoreCombat(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<CombatCheckpoints;n++){game!.LoadCheckpoint("combat"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"combat pose/restored exact own ledger at"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<CombatCheckpoints;next++)await CombatStep(next);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same complete combat future from"+n);await Capture("continued-s"+n,true);GD.Print("COMBAT_RESTORED "+n);}}
    async Task RunCombatChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreCombat();else await BeginNewMilitaryFlow();if(OS.GetCmdlineUserArgs().Contains("--contact-film")){game!.LoadCheckpoint("combat3");game.Press("auto");string dir=Path.Combine(TestOutput,"film");Directory.CreateDirectory(dir);for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"contact-"+n.ToString("D3")+".png"));}game.Press("pause");}File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("CONTACT_COMBAT_PASSED "+FlowCase);}
}
