using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    bool SelectedInspectionCompleted(){var w=game!.Session.DiagnosticSnapshot();return w.Tasks.Any(t=>t.Executor==w.PlayerControl&&t.Action==ActionKind.Inspect&&t.Execution==ExecutionPhase.Completed&&w.EffectLedger.Any(e=>e.Task==t.Id&&e.Result=="completed"));}
    int RoutinePoints=>FlowCase is "routine-cycle" or "routine-legacy-v3"?3:2;
    async Task RoutinePoint(int n){game!.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();game.SaveCheckpoint("routine"+n);await Capture("s"+n,true);}
    async Task RoutineStep(int n)
    {
        if(FlowCase=="routine-legacy-v3"){await EditionStep(n);Check(game!.Session.DiagnosticSnapshot().NpcWork==null&&game.Profile.NpcRoutinePolicy==null,"pre-routine six-person save never receives NPC behavior retroactively");return;}
        if(n==1){var expected=new WorldSession(game!.Session.DiagnosticSnapshot());expected.SetPaused(false);expected.AdvanceTo(expected.Seconds+10);expected.SetPaused(true);game.Press("ten");await WaitManualTime();Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"actual manual-time worker runs same shared NPC Core transactions");}
        else game!.Advance(3610); // Core continuity check;not a claim of3600seconds of recorded automatic UI playback.
        var w=game!.Session.DiagnosticSnapshot();int count=5;Check(w.Requests.Length==count&&w.Tasks.Length==count&&w.Tasks.All(t=>t.Executor!=w.PlayerControl&&t.Execution==ExecutionPhase.Completed),"five historical NPCs finish own initial jobs without unsolicited repeats or fabricated player jobs");Check(game.Session.GetActorView(w.PlayerControl).Messages.Length==0,"other NPC private reports never appear as player letters");Check(w.NpcWork!=null&&w.NewGame!.NpcRoutinePolicy==NewGameNpcRoutine.Policy&&w.NewGame.Registered.All(p=>!w.NpcWork.Decisions.Any(d=>d.Actor.Value==p.Id)),"pinned routine policy excludes synthetic registered load actors");Check(w.NpcWork!.Decisions.Select(d=>d.Actor).Distinct().Count()==5,"all five authored non-player actors participate");File.WriteAllText(Path.Combine(TestOutput,"routine-"+n+".json"),System.Text.Json.JsonSerializer.Serialize(new{w.ElapsedSeconds,NpcActors=5,Requests=count,Decisions=w.NpcWork.Decisions.Length,SyntheticRegistered=w.NewGame!.Registered.Length,Scope="Five authored NPC self-inspections only;synthetic registered actors remain static;not national autonomous or military AI load. Cycle jump uses existing Core QA advance,not recorded wall-clock playback."}));
    }
    async Task RunNpcRoutineChecks()
    {
        if(OS.GetCmdlineUserArgs().Contains("--new-game-restore")){
            Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<RoutinePoints;n++){game!.LoadCheckpoint("routine"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"NPC same selected-person3D after restore"+n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-world.sha256")),"NPC same full world/decision memory after restore"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<RoutinePoints;next++)await RoutineStep(next);game.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"NPC same known-result waiting outcome after restart"+n);await Capture("continued-s"+n,true);}
        }else if(FlowCase=="routine-legacy-v3"){
            var bytes=File.ReadAllBytes(ProjectSettings.GlobalizePath("res://../Tests/Fixtures/pre-routine-household-v3.json"));var p=ContractJson.Read<NewCampaignProfile>(bytes);var world=NewCampaignStore.Build(p);Check(p.NpcRoutinePolicy==null&&ContractJson.CanonicalHash(world)=="bc1ca5126363089b885fce3dbbb17f8620ad2b45032862d8a8f9f886eac95477","real prior six-person initial profile remains exact");var dir=campaigns.DirectoryFor(p.Instance);Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"start.json"),bytes);campaigns.Saves(p).Save(new WorldSession(world),"manual",world.WorldId,new(0,1));Press("load-list");await Frame();savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==p.Instance));Press("load");await Frame();await Frame();File.WriteAllText(Path.Combine(TestOutput,"campaign-instance.txt"),p.Instance);await RoutinePoint(0);for(int n=1;n<RoutinePoints;n++){await RoutineStep(n);await RoutinePoint(n);}await Capture("final",true);
        }else{await BeginPopulationEntry(FlowCase=="routine-1000"?1000:0);Check(game!.Profile.NpcRoutinePolicy==NewGameNpcRoutine.Policy,"normal new entry saves explicit NPC routine policy");await RoutinePoint(0);for(int n=1;n<RoutinePoints;n++){await RoutineStep(n);await RoutinePoint(n);}await Capture("final",true);}
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("NEW_GAME_NPC_ROUTINE_PASSED "+FlowCase);
    }
}
