using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Appearance;
public partial class NewGameFrontEnd
{
    int CapacityCount=>FlowCase=="capacity-1000"?1000:100;
    async Task BeginPopulationEntry(int count,string selectedPerson="person:nobunaga")
    {
        var rows=Enumerable.Range(0,count).Select(i=>new RegisteredOfficer("created:capacity-"+i.ToString("D5"),null,"測定専用"+i.ToString("D5"),1520,new(50,50,50,50),StartTemplate.Ronin,NationalRoutes.Sites[i%NationalRoutes.Sites.Length].Id.Value,null,null,AppearanceCatalog.Default(1))).ToArray();
        // A declared imported library fixture, then the ordinary group-selection
        // screens. This is not a claim that these are sourced historical NPCs.
        new OfficerLibraryStore(LibraryRoot).Save(new(1,[],[new("group:capacity","測定専用・架空武将"+count+"人",rows)]),null);
        ShowMenu();await Capture("menu");Press("new");Press("next");selectionMode.Select(2);selectionMode.EmitSignal(OptionButton.SignalName.ItemSelected,2);groupChoice.Select(0);Press("next");await Capture("placement");Press("next");await Capture("world-review");Press("confirm");await Frame();
        map!.PressSite(NewGameWorld.Scenario(HistoricalStartCatalog.Current).People.Single(p=>p.Id==selectedPerson).Site);PickRow(selectedPerson);Press("begin");await Frame();await Frame();
        var w=game!.Session.DiagnosticSnapshot();Check(w.Characters.Length==count+NewGameWorld.Scenario(HistoricalStartCatalog.Current).People.Length&&w.NewGame!.Registered.Length==count,"declared imported synthetic group and versioned historical people enter the actual normal UI");Check(w.NewGame!.PersonalAuthorityLayout==NewGameWorld.CompactPersonalAuthority&&w.Grants.Length==2*w.Characters.Length,"actual new entry adopts exact compact authority layout");
        Check(NationalRoutes.Nodes.Length==108&&NationalRoutes.Edges.Length==118,"runtime108/118 includes regional7/7 plus national catalog101/111");File.WriteAllText(Path.Combine(TestOutput,"campaign-instance.txt"),game.Profile.Instance);
    }
    async Task BeginCapacityFlow()
    {
        int count=CapacityCount;await BeginPopulationEntry(count);
        game!.Press("save");await WaitSave();game.Refresh();game.SaveCheckpoint("s0");await Capture("s0",true);
        for(int n=1;n<=3;n++){await GameStep(n);game.SaveCheckpoint("s"+n);await Capture("s"+n,true);}await GameStep(4);await Capture("final",true);
        Check(game.Session.DiagnosticSnapshot().Tasks.Any(t=>t.Action==ActionKind.Inspect&&t.Execution==ExecutionPhase.Completed),"same selected historical actor moves and completes inspection in synthetic population world");
        var samples=new System.Collections.Generic.List<double>();var click=Stopwatch.StartNew();game.Press("auto");double autoClickMs=click.Elapsed.TotalMilliseconds;await WaitClock();double autoCompleteMs=click.Elapsed.TotalMilliseconds;long autoStart=game.Session.Seconds;
        var clock=Stopwatch.StartNew();double prior=0;
        for(int n=0;n<1000&&(samples.Count<120||clock.Elapsed.TotalSeconds<3);n++){await Frame();double now=clock.Elapsed.TotalMilliseconds;samples.Add(now-prior);prior=now;}
        double observedSeconds=clock.Elapsed.TotalSeconds;long autoElapsed=game.Session.Seconds-autoStart;click.Restart();game.Press("pause");double pauseClickMs=click.Elapsed.TotalMilliseconds;await WaitClock();double pauseCompleteMs=click.Elapsed.TotalMilliseconds;
        Check(samples.Count>=120&&observedSeconds>=3&&autoElapsed>=2,"actual automatic observation includes multiple world-second updates and excludes click cost");
        File.WriteAllText(Path.Combine(TestOutput,"population-observation.json"),System.Text.Json.JsonSerializer.Serialize(new{SyntheticRegisteredPeople=count,HistoricalPeople=NewGameWorld.Scenario(HistoricalStartCatalog.Current).People.Length,RuntimeNodes=108,RuntimeEdges=118,Frames=samples,AutoClickMilliseconds=autoClickMs,PauseClickMilliseconds=pauseClickMs,AutoCompletionMilliseconds=autoCompleteMs,PauseCompletionMilliseconds=pauseCompleteMs,ObservationSeconds=observedSeconds,AutoElapsedSeconds=autoElapsed,PayloadBytes=ContractJson.Write(game.Session.DiagnosticSnapshot()).Length,Scope="Selected actor only; stationary synthetic people; not nationwide NPC AI or simultaneous armies. Click operations are measured separately from at least3seconds of automatic simulation/render frames; observations are not a real-time certification."}));
        game.LoadCheckpoint("s3");await GameStep(4);
    }
    async Task RunCapacityChecks()
    {
        if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreFlow();else await BeginCapacityFlow();
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("NEW_GAME_CAPACITY_PASSED "+FlowCase);
    }
}
