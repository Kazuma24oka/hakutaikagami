using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class StartSelectionPanel
{
    string RetainerCase=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--retainer-case="))?[16..]??"binding";
    string RetainerOutput=>ProjectSettings.GlobalizePath("res://../Docs/Design/RetainerStartV1/runtime-local/"+RetainerCase);
    long Arrival=>stage!.HubSession.DiagnosticSnapshot().Branches!.Journeys.Single().Legs[^1].EndAt;
    async Task RetainerCapture(string name)
    {
        await Frame();Need(stage!.HubFits(),"retainer UI and minimum 3D area fit actual window");GetViewport().GetTexture().GetImage().SavePng(RetainerOutput+"/"+name+".png");
        File.WriteAllText(RetainerOutput+"/"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(stage.HubRect()));
    }
    void RetainerContinue()
    {
        var child=stage!;if(RetainerCase=="journey"){
            if(child.HubSession.Seconds<Arrival)child.HubAdvance(Arrival);
            if(child.HubSession.Seconds<Arrival+1)child.HubAdvance(Arrival+1);
        }else if(child.HubSession.Seconds<2)child.HubAdvance(2);
        if(child.HubCanPress("work-open"))child.HubPress("work-open");
        if(child.HubCanPress("work-accept"))child.HubPress("work-accept");
        if(RetainerCase=="interrupt"){
            if(child.HubCanPress("retainer-interrupt"))child.HubPress("retainer-interrupt");
            if(child.HubCanPress("retainer-explain"))child.HubPress("retainer-explain");
        }
        long end=RetainerCase=="journey"?Arrival+9:9;if(child.HubSession.Seconds<end)child.HubAdvance(end);
    }
    async Task RunRetainerSelectionChecks()
    {
        Directory.CreateDirectory(RetainerOutput);((TabContainer)GetParent()).CurrentTab=2;await Frame();
        if(SelectionMode.EndsWith("restore")){
            ScanProfiles();int index=profiles.FindIndex(p=>p.Profile.Instance==File.ReadAllText(RetainerOutput+"/origin-instance.txt"));Need(index>=0,"retainer save is listed from its original start package");saved.Select(index);Press(load);await Frame();
            for(int n=0;n<5;n++){
                stage!.HubLoad("retainer-s"+n);Need(stage.HubPose()==File.ReadAllText(RetainerOutput+"/s"+n+"-pose.json"),"retainer role/knowledge/model restored "+n);await RetainerCapture("restored-s"+n);RetainerContinue();await RetainerCapture("continued-s"+n);
            }
        }else{
            role.Select(1);nameInput.Text="検証家臣";Press(begin);await Frame();Need(stage!=null&&RetainerStart.Enabled(stage.HubSession.DiagnosticSnapshot()),"actual role dropdown starts retainer profile");
            var p=ContractJson.Read<SavedStartProfile>(File.ReadAllBytes(Path.Combine(CurrentDirectory,"start-profile.json")));Need(p.Package.Selection.Template==StartTemplate.Retainer,"persisted selection records retainer template");File.WriteAllText(RetainerOutput+"/origin-instance.txt",p.Instance);
            if(RetainerCase=="journey"){stage!.HubDestination("kofu");stage.HubPress("depart");stage.HubAdvance(1);}
            long[] at=RetainerCase=="journey"?new[]{1L,1000,Arrival,Arrival+1,Arrival+6}:RetainerCase=="interrupt"?new[]{1L,2,3,4,6}:new[]{1L,2,4,6,9};
            for(int n=0;n<5;n++){
                if(RetainerCase!="journey"&&n==2){stage!.HubPress("work-open");if(RetainerCase=="interrupt"){stage.HubPress("retainer-interrupt");stage.HubPress("retainer-explain");}}
                if(RetainerCase=="journey"&&n==4){stage!.HubPress("work-open");stage.HubPress("work-accept");}
                stage!.HubAdvance(at[n]);Need(stage.HubSession.Seconds==at[n],"retainer actual UI reaches checkpoint "+n);stage.HubSession.SetPaused(true);stage.HubSave("retainer-s"+n);File.WriteAllText(RetainerOutput+"/s"+n+"-pose.json",stage.HubPose());await RetainerCapture("s"+n);
            }
            RetainerContinue();stage!.HubPress("save");await RetainerCapture("final");var w=stage.HubSession.DiagnosticSnapshot();var task=w.Tasks.Last(t=>t.Action==ActionKind.Inspect);
            Need(task.Executor==PersonalStart.Person&&task.Kind==(RetainerCase=="journey"?TaskKind.VoluntaryRequest:TaskKind.BindingOrder),"direct lord order and other-house request remain distinct");
            Need(task.Execution==(RetainerCase=="interrupt"?ExecutionPhase.Stopped:ExecutionPhase.Completed),"actual task outcome matches chosen player action");
            if(RetainerCase=="interrupt"){var review=MissionReview.View(stage.HubSession,RetainerStart.Lord).Single();Need(review.Reason.Contains("自己判断")&&review.Liability=="責任・処分は未審査","physical reason reaches lord without automatic punishment");}
            Need(w.Units.All(u=>u.Commander!=PersonalStart.Person)&&w.LandRights.All(l=>l.Subject!=PersonalStart.Person),"membership never creates an army or land rights");
        }
        File.WriteAllText(RetainerOutput+"/"+(SelectionMode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(checks));GD.Print("RETAINER_START_PASSED");GetTree().Quit();
    }
}
