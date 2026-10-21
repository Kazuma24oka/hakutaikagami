using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class StartSelectionPanel
{
    readonly List<string> checks=new();
    string Output=>ProjectSettings.GlobalizePath("res://../Docs/Design/StartSelectionV1/runtime-local");
    void Need(bool yes,string text){if(!yes)throw new Exception(text);checks.Add(text);}
    async Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    async Task Capture(string name)
    {
        await Frame();if(stage!=null&&play.Visible)Need(stage.HubFits(),"actual started scene fits with navigation header");
        GetViewport().GetTexture().GetImage().SavePng(Output+"/"+name+".png");
        float[] rect=stage!=null&&play.Visible?stage.HubRect():new[]{0f,0f,GetViewportRect().Size.X,GetViewportRect().Size.Y};
        File.WriteAllText(Output+"/"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(rect));
    }
    void Press(Button b)=>b.EmitSignal(Button.SignalName.Pressed);
    void ContinueCreated()
    {
        var s=stage!;if(s.HubSession.Seconds<2)s.HubAdvance(2);
        if(RegionalWork.Inbox(s.HubSession).Any(c=>c.CanOpen))s.HubPress("work-open");
        if(RegionalWork.Inbox(s.HubSession).Any(c=>c.CanDecide))s.HubPress("work-accept");
        if(s.HubSession.Seconds<9)s.HubAdvance(9);
    }
    static Dictionary<string,string> Files(string dir)=>Directory.GetFiles(dir,"*",SearchOption.AllDirectories).ToDictionary(p=>p,p=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p))));
    async void RunSelectionChecks()
    {
        try{
            if(SelectionMode.Contains("-inquiry-")){await RunInquirySelectionChecks();return;}
            if(SelectionMode.Contains("-retry-")){await RunRetrySelectionChecks();return;}
            if(SelectionMode.Contains("-delegation-")){await RunDelegationSelectionChecks();return;}
            if(SelectionMode.Contains("-campaign-")){await RunCampaignSelectionChecks();return;}
            if(SelectionMode.Contains("-retainer-")){await RunRetainerSelectionChecks();return;}
            Directory.CreateDirectory(Output);var tabs=(TabContainer)GetParent();tabs.CurrentTab=2;await Frame();
            if(SelectionMode.EndsWith("restore")){
                ScanProfiles();int index=profiles.FindIndex(p=>p.Profile.Instance==File.ReadAllText(Output+"/origin-instance.txt"));Need(index>=0,"saved creation package is listed after fresh process");saved.Select(index);Press(load);await Frame();
                Need(stage!=null&&stage.HubSession.PlayerControl==PersonalStart.Person,"continue button restores actual personal world");
                for(int n=0;n<5;n++){
                    stage!.HubLoad("selection-s"+n);Need(stage.HubPose()==File.ReadAllText(Output+"/s"+n+"-pose.json"),"initial definition plus live save restores exact personal view "+n);
                    await Capture("restored-s"+n);ContinueCreated();await Capture("continued-s"+n);
                }
                Press(back);int count=profiles.Count;string broken=Path.Combine(Root,new string('0',64),new string('0',32));Directory.CreateDirectory(broken);File.WriteAllText(Path.Combine(broken,"start-profile.json"),"{}");ScanProfiles();
                Need(profiles.Count==count&&message.Text.Contains("読めない")&&File.Exists(Path.Combine(broken,"start-profile.json")),"corrupt metadata is rejected and retained without disturbing valid saves");
            }else{
                await Capture("form");nameInput.Text="";Press(begin);Need(stage==null&&message.Text.Contains("開始できません"),"invalid name cannot create or replace a world");
                nameInput.Text="検証浪人";Press(begin);await Frame();Need(stage!=null&&stage.HubSession.PlayerControl==PersonalStart.Person,"actual name/start button opens independent officer scene");
                string originalDirectory=CurrentDirectory;var originalProfile=ContractJson.Read<SavedStartProfile>(File.ReadAllBytes(Path.Combine(originalDirectory,"start-profile.json")));File.WriteAllText(Output+"/origin-instance.txt",originalProfile.Instance);
                long[] at={1,2,4,6,9};
                for(int n=0;n<at.Length;n++){
                    if(n==2){stage!.HubPress("work-open");stage.HubPress("work-accept");}
                    stage!.HubAdvance(at[n]);Need(stage.HubSession.Seconds==at[n],"actual UI reaches expected checkpoint "+n);stage.HubSession.SetPaused(true);stage.HubSave("selection-s"+n);
                    File.WriteAllText(Output+"/s"+n+"-pose.json",stage.HubPose());await Capture("s"+n);
                }
                stage!.HubPress("save");await Capture("final");var oldFiles=Files(originalDirectory);var same=stage;
                Press(back);stage.HubSession.SetPaused(false);stage.HubSetAutomatic(true);string held=ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot());await Frame();await Frame();await Frame();Need(stage.ProcessMode==ProcessModeEnum.Disabled&&ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot())==held,"hidden personal scene retains world and does not auto-advance");stage.HubSetAutomatic(false);Press(resume);await Frame();Need(ReferenceEquals(stage,same)&&ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot())==held,"return button resumes the same session without rebuilding");
                Press(back);nameInput.Text="別の浪人";Press(begin);Need(confirmation.Visible&&ReferenceEquals(stage,same),"switching a live character requires in-game confirmation");Press(confirmation.GetCancelButton());await Frame();Need(pending==null&&ReferenceEquals(stage,same),"cancel keeps the original world");
                Press(begin);Press(confirmation.GetOkButton());await Frame();Need(stage!=same&&CurrentDirectory!=originalDirectory&&stage!.HubSession.DiagnosticSnapshot().Definitions.Characters.Single(c=>c.Id==PersonalStart.Person).Name=="別の浪人","confirmed new name has a separate run and actual actor name");stage!.HubPress("save");await Capture("second-person");
                Press(back);nameInput.Text="検証浪人";Press(begin);Press(confirmation.GetOkButton());await Frame();Need(CurrentDirectory!=originalDirectory&&stage!.HubSession.Seconds==0,"new playthrough with same name and seed has separate saved progress");var after=Files(originalDirectory);Need(oldFiles.Count==after.Count&&oldFiles.All(p=>after.TryGetValue(p.Key,out var h)&&h==p.Value),"all earlier saved files retain identical bytes");
            }
            File.WriteAllText(Output+"/"+(SelectionMode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(checks));GD.Print("START_SELECTION_PASSED");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
