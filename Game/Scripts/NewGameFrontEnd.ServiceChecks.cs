using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    bool InitialService=>FlowCase.StartsWith("held-")||FlowCase.StartsWith("reply-")&&FlowCase!="reply-ronin"||FlowCase.StartsWith("inquiry-")&&FlowCase!="inquiry-ronin"||FlowCase.StartsWith("retry-")&&FlowCase!="retry-ronin"||FlowCase.StartsWith("rout-")&&FlowCase!="rout-ronin"||FlowCase.StartsWith("withdrawal-")&&FlowCase!="withdrawal-ronin"||FlowCase.StartsWith("combat-")&&FlowCase!="combat-ronin"||FlowCase=="service-retainer"||FlowCase.StartsWith("military-")&&FlowCase!="military-ronin";
    string ServiceSite=>FlowCase.Contains("odawara")?"site:odawara":"site:kofu";
    void OpenAllServiceLetters(){for(int n=0;n<12&&game!.Session.GetActorView(game.Session.PlayerControl).Messages.Any(m=>m.KnownBody==null);n++)game.Press("open");}
    void ServiceStep(int step)
    {
        if(step==1){if(!InitialService){game!.Press("service-apply");game.Advance(game.Session.Seconds+2);Check(!game.ButtonEnabled("service-accept"),"unopened conditions cannot be accepted");OpenAllServiceLetters();Check(game.ButtonEnabled("service-accept"),"opened real offer enables acceptance");game.Press("service-accept");game.Advance(game.Session.Seconds+1);Check(NewGameService.BoundAt(game.Session.DiagnosticSnapshot(),NewGameService.LocalPrincipals(game.Session.DiagnosticSnapshot()).Single(),game.Session.Seconds,game.Session.Revision),"actual service starts on physical acceptance");Check(NewGameService.View(game.Session.DiagnosticSnapshot()).House==null,"before opening agreement the character still knows no affiliation");game.Advance(game.Session.Seconds+1);OpenAllServiceLetters();}else game!.Advance(4);}
        if(step==2){game!.Press("work");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();Check(game.Session.DiagnosticSnapshot().Tasks.Single(t=>t.Action==ActionKind.Inspect).Kind==TaskKind.BindingOrder,"same officer's known lord issues a distinct binding work order");}
        if(step==3){Check(game!.ButtonEnabled("service-release"),"known current service permits a release request");game.Press("service-release");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();Check(game.Session.DiagnosticSnapshot().Service!.Events.Any(e=>e.Kind==CareerEventKind.ReleaseWaiting),"release waits for a physically received work report");}
        if(step==4){game!.Advance(game.Session.Seconds+12);OpenAllServiceLetters();Check(NewGameService.View(game.Session.DiagnosticSnapshot()).House==null&&game.InfoText.Contains("所属なし"),"released same person is shown as ronin only after notice");}
        if(step==5){game!.SetDestination("castle");game.Press("depart");game.Advance(game.Session.Seconds+5);}
        if(step==6){game!.Press("arrive");Check(game.Session.DiagnosticSnapshot().Locations.Single(l=>l.Actor==game.Session.PlayerControl).Site==new EntityId("site:castle"),"same released person actually arrives at new site");}
        game!.Press("save");game.Refresh();
    }
    async Task BeginServiceFlow()
    {
        Check(ProjectSettings.GetSetting("application/run/main_scene").AsString()=="res://Scenes/NewGameFrontEnd.tscn","normal no-scene startup uses adopted frontend");await Capture("menu");Press("manage");await Frame();await Frame();manager!.ExerciseServiceOfficer(ServiceSite,InitialService);await Capture("registered-officer");PressText(manager,"スタート画面へ");await Frame();Press("new");Press("next");selectionMode.Select(1);selectionMode.EmitSignal(OptionButton.SignalName.ItemSelected,1);registeredRows.Select(0);Press("next");Press("next");Press("confirm");await Frame();string id=chosen.Single().Id;int site=Array.FindIndex(NationalRoutes.Sites,s=>s.Id.Value==ServiceSite);jumpChoice.Select(site);jumpChoice.EmitSignal(OptionButton.SignalName.ItemSelected,site);await Frame();map!.PressSite(ServiceSite);PickRow(id);Press("begin");await Frame();await Frame();Check(game!.Session.PlayerControl.Value==id,"normal menu begins the chosen created person");File.WriteAllText(Path.Combine(TestOutput,"campaign-instance.txt"),game.Profile.Instance);
        for(int n=0;n<=5;n++){ServiceStep(n);game.SaveCheckpoint("service"+n);await Capture("s"+n,true);}
        ServiceStep(6);await Capture("final",true);
        var same=game;string hash=ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());game.Press("menu");Press("legacy");await Frame();await Capture("legacy-entry");Press("back");Press("continue");await Frame();Check(ReferenceEquals(same,game)&&hash==ContractJson.CanonicalHash(game!.Session.DiagnosticSnapshot()),"old trial entry roundtrip retains same person's world and time");
    }
    async Task RestoreServiceFlow()
    {
        Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();
        for(int n=0;n<=5;n++){game!.LoadCheckpoint("service"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"restored identical officer pose at"+n);await Capture("restored-s"+n,true);for(int step=n+1;step<=6;step++)ServiceStep(step);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same future yields same full world from"+n);await Capture("continued-s"+n,true);}
    }
    async Task RunServiceChecks()
    {
        if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreServiceFlow();else await BeginServiceFlow();
        if(OS.GetCmdlineUserArgs().Contains("--service-film")){string dir=Path.Combine(TestOutput,"film");Directory.CreateDirectory(dir);game!.LoadCheckpoint("service5");game.Press("auto");for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"walk-"+n.ToString("D3")+".png"));}game.Press("pause");}
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("SERVICE_ENTRY_PASSED "+FlowCase);
    }
}
