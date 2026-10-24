using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    int HouseholdPoints=>FlowCase=="household-west-v2"?3:6;
    async Task HouseholdPoint(int n){game!.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();game.SaveCheckpoint("household"+n);await Capture("s"+n,true);}
    async Task HouseholdStep(int n)
    {
        if(FlowCase=="household-west-v2"){await EditionStep(n);return;}
        var s=game!.Session;
        if(n==1){await PressLocalIntent("work");game.Press("ten");await WaitManualTime();Check(game.Session.GetActorView(game.Session.PlayerControl).Messages.Any(m=>m.KnownBody==null),"historical retainer work arrives sealed through existing delivery");Check(game.Session.DiagnosticSnapshot().Tasks.Single(t=>t.Action==ActionKind.Inspect).Kind==TaskKind.BindingOrder,"known initial lord issues actual binding work");}
        if(n==2){OpenAllServiceLetters();game.Press("ten");await WaitManualTime();Check(game.Session.DiagnosticSnapshot().EffectLedger.Any(e=>e.Result=="completed"),"retainer completes real work before release");}
        if(n==3){Check(game.ButtonEnabled("service-release"),"retainer may request release without fabricated extra army rights");game.Press("service-release");game.Press("ten");await WaitManualTime();}
        if(n==4){OpenAllServiceLetters();game.Press("ten");await WaitManualTime();OpenAllServiceLetters();Check(NewGameService.View(game.Session.DiagnosticSnapshot()).House==null&&game.InfoText.Contains("所属なし"),"same historical retainer becomes ronin only after existing release notice is opened");}
        if(n==5){game.SetDestination("castle");await PressDeparture();game.Press("ten");await WaitManualTime();Check(game.Session.GetBranchView(game.Session.PlayerControl).Walking,"same historical person travels after work and release");}
    }
    async Task BeginHistoricalHousehold()
    {
        if(FlowCase=="household-west-v2"){
            var bytes=File.ReadAllBytes(ProjectSettings.GlobalizePath("res://../Tests/Fixtures/pre-household-start-west-v2.json"));var p=ContractJson.Read<NewCampaignProfile>(bytes);var world=NewCampaignStore.Build(p);Check(world.Characters.Length==5&&p.HistoricalCatalog==HistoricalStartCatalog.Western&&ContractJson.CanonicalHash(world)=="2c13b3e1bc0caf550f595fa25be2b13ac7263d6005c9f0eac0e881844c709639","prior real west-v2 initial world remains exact after adding new household edition");var dir=campaigns.DirectoryFor(p.Instance);Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"start.json"),bytes);campaigns.Saves(p).Save(new WorldSession(world),"manual",world.WorldId,new(0,1));Press("load-list");await Frame();savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==p.Instance));Press("load");await Frame();await Frame();Check(game!.Session.DiagnosticSnapshot().Characters.Length==5,"actual old five-person profile loads without adding historical retainer");File.WriteAllText(Path.Combine(TestOutput,"campaign-instance.txt"),p.Instance);
        }else{
            await BeginPopulationEntry(0,"person:takeda-nobushige");var w=game!.Session.DiagnosticSnapshot();Check(w.Characters.Length==6&&game.Profile.HistoricalCatalog==HistoricalHouseholdCatalog.Id,"actual national map selects sourced historical retainer in explicit six-person edition");Check(NewGameService.View(w).Principal==new EntityId("person:shingen")&&w.Units.Length==0&&w.ResourceLots.Length==0&&!w.Offices.Any(o=>o.Subject==game.Session.PlayerControl),"initial membership and lord do not grant army or office");await Capture("retainer-start",true);
        }
        await HouseholdPoint(0);for(int n=1;n<HouseholdPoints;n++){await HouseholdStep(n);await HouseholdPoint(n);}await Capture("final",true);
    }
    async Task RestoreHistoricalHousehold()
    {
        Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();
        for(int n=0;n<HouseholdPoints;n++){game!.LoadCheckpoint("household"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"same household person and3D after restore"+n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-world.sha256")),"same full household world after restore"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<HouseholdPoints;next++)await HouseholdStep(next);game.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same work/report/release/road future after restart"+n);await Capture("continued-s"+n,true);}
    }
    async Task RunHistoricalHouseholdChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreHistoricalHousehold();else await BeginHistoricalHousehold();File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("HISTORICAL_HOUSEHOLD_PASSED "+FlowCase);}
}
