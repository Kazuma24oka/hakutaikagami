using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class StartSelectionPanel
{
    string LifeCase=>OS.GetCmdlineUserArgs().Contains("--life-long-name")?"long-name":"normal";
    string LifeOutput=>ProjectSettings.GlobalizePath("res://../Docs/Design/LifeEntryV1/runtime-local/"+LifeCase);
    async Task LifeCapture(string name,bool game=true){await Frame();if(game){GetViewport().GetTexture().GetImage().SavePng(LifeOutput+"/diagnostic.png");Need(stage!.HubFits(),"life controls and minimum3D fit actual viewport");File.WriteAllText(LifeOutput+"/"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(stage.HubRect()));File.WriteAllText(LifeOutput+"/"+name+"-world.sha256",ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot()));}GetViewport().GetTexture().GetImage().SavePng(LifeOutput+"/"+name+".png");}
    void LifeContinue(int checkpoint){for(int n=checkpoint+1;n<MilitarySteps;n++){MilitaryStep(n);stage!.HubSession.SetPaused(true);stage.HubRefresh();}CareerAdvance(stage!.HubSession.Seconds+1);}
    async Task RunLifeEntryChecks()
    {
        Directory.CreateDirectory(LifeOutput);var tabs=(TabContainer)GetParent();Need(Simplified&&tabs.CurrentTab==2&&tabs.GetTabCount()==3,"actual candidate opens simplified life tab while retaining old tab order");Need(role.Selected==8&&!role.Visible,"technical trial profiles are hidden; same coherent ronin start selected");await LifeCapture("form",false);
        if(SelectionMode.EndsWith("restore")){
            ScanProfiles();int i=profiles.FindIndex(p=>p.Profile.Instance==File.ReadAllText(LifeOutput+"/origin-instance.txt"));Need(i>=0,"same candidate normal saved profile survives process restart");saved.Select(i);Press(load);await Frame();
            for(int n=0;n<MilitarySteps;n++){stage!.HubLoad("life-s"+n);Need(stage.HubPose()==File.ReadAllText(LifeOutput+"/s"+n+"-pose.json"),"simplified personal information and models restored "+n);Need(ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot())==File.ReadAllText(LifeOutput+"/s"+n+"-world.sha256"),"candidate full world restored "+n);await LifeCapture("restored-s"+n);LifeContinue(n);Need(ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot())==File.ReadAllText(LifeOutput+"/final-world.sha256"),"same candidate continuation yields full world identity "+n);await LifeCapture("continued-s"+n);}
        }else{
            int previous=profiles.Count;nameInput.Text="";nameInput.EmitSignal(LineEdit.SignalName.TextChanged,nameInput.Text);Need(begin.Disabled&&stage==null&&profiles.Count==previous,"empty name cannot start or change existing progress");nameInput.Text=LifeCase=="long-name"?new string('あ',24):"検証人生";nameInput.EmitSignal(LineEdit.SignalName.TextChanged,nameInput.Text);Need(!begin.Disabled,"valid name enables actual start button");Press(begin);await Frame();Need(stage!=null&&stage.LifeView&&CareerMilitary.Enabled(stage.HubSession.DiagnosticSnapshot()),"actual candidate start enables only presentation; same military world");var profile=ContractJson.Read<SavedStartProfile>(File.ReadAllBytes(Path.Combine(CurrentDirectory,"start-profile.json")));Need(ContractJson.CanonicalHash(profile.Package.World)==ContractJson.CanonicalHash(CareerMilitary.Create(nameInput.Text)),"candidate initial world is exactly existing factory output");File.WriteAllText(LifeOutput+"/origin-instance.txt",profile.Instance);
            for(int n=0;n<MilitarySteps;n++){MilitaryStep(n);stage!.HubSession.SetPaused(true);stage.HubRefresh();stage.HubSave("life-s"+n);File.WriteAllText(LifeOutput+"/s"+n+"-pose.json",stage.HubPose());await LifeCapture("s"+n);}LifeContinue(MilitarySteps-1);stage!.HubPress("save");await LifeCapture("final");
            var held=stage;var before=ContractJson.CanonicalHash(held.HubSession.DiagnosticSnapshot());held.HubSetAutomatic(true);held.HubSession.SetPaused(false);before=ContractJson.CanonicalHash(held.HubSession.DiagnosticSnapshot());tabs.CurrentTab=0;await Frame();await Frame();Need(ContractJson.CanonicalHash(held.HubSession.DiagnosticSnapshot())==before,"life world is held while visiting independent old trial tab");tabs.CurrentTab=2;held.HubSetAutomatic(false);Need(ReferenceEquals(held,stage)&&ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot())==before,"returning tab keeps the same life and clock");await Frame();
            var originals=Files(CurrentDirectory);Press(back);await Frame();nameInput.Text="別の人生";Press(begin);Need(confirmation.Visible&&ReferenceEquals(stage,held),"switching existing life requires concrete in-game confirmation");Press(confirmation.GetCancelButton());await Frame();Need(pending==null&&ReferenceEquals(stage,held),"cancel keeps original person");var after=Files(CurrentDirectory);Need(originals.Count==after.Count&&originals.All(p=>after.TryGetValue(p.Key,out var hash)&&hash==p.Value),"all original saved files remain byte-identical after cancel");Press(resume);await Frame();
        }
        File.WriteAllText(LifeOutput+"/"+(SelectionMode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(checks));GD.Print("LIFE_ENTRY_PASSED");GetTree().Quit();
    }
}
