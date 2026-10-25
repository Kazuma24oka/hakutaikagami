using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    string EditionPerson=>FlowCase=="edition-otomo"?"person:yoshishige":FlowCase=="edition-shimazu"?"person:takahisa":"person:nobunaga";
    async Task EditionPoint(int n){game!.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();game.SaveCheckpoint("edition"+n);await Capture("s"+n,true);}
    async Task EditionStep(int n)
    {
        if(n==1){await CompleteLocal("inspect");game!.Press("ten");await WaitManualTime();Check(SelectedInspectionCompleted(),"same western/legacy person completes existing inspection effects");}
        if(n==2){game!.SetDestination(FlowCase=="edition-otomo"?"hita":FlowCase=="edition-shimazu"?"hitoyoshi":"fork");await PressDeparture();game.Press("ten");await WaitManualTime();Check(game.Session.GetBranchView(game.Session.PlayerControl).Walking,"same versioned person enters existing road movement");}
    }
    async Task BeginHistoricalEditionFlow()
    {
        if(FlowCase=="edition-legacy"){
            byte[] bytes=File.ReadAllBytes(ProjectSettings.GlobalizePath("res://../Tests/Fixtures/pre-catalog-start-v1.json"));var p=ContractJson.Read<NewCampaignProfile>(bytes);var world=NewCampaignStore.Build(p);Check(world.Characters.Length==3&&p.HistoricalCatalog==null&&ContractJson.CanonicalHash(world)=="29a0594ab45c0f14e9ed9ec472929618e0a191f0f4ec8277a4f4edcd4f0fec73","actual pre-upgrade profile rebuilds unchanged three-person world");string folder=campaigns.DirectoryFor(p.Instance);Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"start.json"),bytes);campaigns.Saves(p).Save(new WorldSession(world),"manual",world.WorldId,new(0,1));Press("load-list");await Frame();savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==p.Instance));Press("load");await Frame();await Frame();Check(game!.Profile.HistoricalCatalog==null&&game.Session.DiagnosticSnapshot().Characters.Length==3,"new executable opens original profile without adopting current five-person roster");File.WriteAllText(Path.Combine(TestOutput,"campaign-instance.txt"),p.Instance);
        }else{
            await BeginPopulationEntry(0,EditionPerson);Check(game!.Profile.HistoricalCatalog==HistoricalStartCatalog.Current&&game.Session.DiagnosticSnapshot().Characters.Length==NewGameWorld.Scenario(HistoricalStartCatalog.Current).People.Length,"normal map/list entry pins current explicit edition in actual profile");await Capture("western-start",true);
        }
        await EditionPoint(0);for(int n=1;n<3;n++){await EditionStep(n);await EditionPoint(n);}await Capture("final",true);
    }
    async Task RestoreHistoricalEditionFlow()
    {
        Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();
        for(int n=0;n<3;n++){game!.LoadCheckpoint("edition"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"edition exact saved3D restored"+n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-world.sha256")),"edition exact saved world restored"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<3;next++)await EditionStep(next);game.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"edition exact inspection/travel future after restart"+n);await Capture("continued-s"+n,true);}
    }
    async Task RunHistoricalEditionChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreHistoricalEditionFlow();else await BeginHistoricalEditionFlow();File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("HISTORICAL_START_EDITION_PASSED "+FlowCase);}
}
