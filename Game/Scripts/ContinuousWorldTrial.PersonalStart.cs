using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    internal ContractWorld? StartDefinition {get;set;}
    internal string? StartSaveRoot {get;set;}
    bool PersonalSelectionCheck=>PersonalStartEnabled&&OS.GetCmdlineUserArgs().Any(a=>a.StartsWith("--start-selection-"));
    bool PresenceStartEnabled=>StartDefinition!=null&&FieldPresence.Enabled(StartDefinition);
    bool SupplyStartEnabled=>StartDefinition!=null&&FieldSupply.Enabled(StartDefinition);
    bool EngagementStartEnabled=>StartDefinition!=null&&EngagementMission.Enabled(StartDefinition);
    bool CampaignStartEnabled=>StartDefinition!=null&&CreatedCampaign.Enabled(StartDefinition);
    bool RetainerStartEnabled=>StartDefinition!=null?(RetainerStart.Enabled(StartDefinition)||CreatedCampaign.Enabled(StartDefinition)):Name=="RetainerStartTrial";
    bool PersonalStartEnabled=>StartDefinition!=null||Name=="PersonalStartTrial"||RetainerStartEnabled;
    long PersonalArrival=>S.DiagnosticSnapshot().Branches!.Journeys.Single().Legs[^1].EndAt;
    void PersonalContinue()
    {
        if(Case=="journey"){
            if(S.Seconds<PersonalArrival)WorkAdvance(PersonalArrival);
            if(S.Seconds<PersonalArrival+1)WorkAdvance(PersonalArrival+1);
        }else if(S.Seconds<2)WorkAdvance(2);
        if(RegionalWork.Inbox(S).Any(c=>c.CanOpen))Click("work-open");
        if(RegionalWork.Inbox(S).Any(c=>c.CanDecide))Click("work-accept");
        long end=Case=="journey"?PersonalArrival+9:9;
        if(S.Seconds<end)WorkAdvance(end);
    }
    async Task PersonalFrame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    async Task PersonalCapture(string name)
    {
        await PersonalFrame();Check(HubFits(),"created-person UI fits actual viewport with 3D area");
        GetViewport().GetTexture().GetImage().SavePng(Output+"/"+name+".png");
        System.IO.File.WriteAllText(Output+"/"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(HubRect()));
    }
    async Task RunPersonalStartChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);await PersonalFrame();
            Check(S.PlayerControl==PersonalStart.Person,"actual UI controls independent created person");
            if(Mode.EndsWith("restore")){
                for(int n=0;n<5;n++){
                    Load("personal-s"+n);Check(WorkPose()==System.IO.File.ReadAllText(Output+"/s"+n+"-pose.json"),"personal identity, sealed knowledge, model pose and terrain restored "+n);
                    await PersonalCapture("restored-s"+n);PersonalContinue();await PersonalCapture("continued-s"+n);
                }
            }else{
                if(Case=="journey"){
                    var i=TravelWorld.Move(S.DiagnosticSnapshot(),"request:personal-ui-travel","kofu");Require(S.Submit(i,S.Revision));WorkAdvance(1);Refresh();
                }
                long[] stages=Case=="journey"?new[]{1L,1000,PersonalArrival,PersonalArrival+1,PersonalArrival+6}:new[]{1L,2,4,6,9};
                for(int n=0;n<5;n++){
                    if(Case!="journey"&&n==2){Click("work-open");Click("work-accept");}
                    if(Case=="journey"&&n==4){Click("work-open");Click("work-accept");}
                    WorkAdvance(stages[n]);S.SetPaused(true);Refresh();Save("personal-s"+n);
                    System.IO.File.WriteAllText(Output+"/s"+n+"-pose.json",WorkPose());await PersonalCapture("s"+n);
                }
                PersonalContinue();await PersonalCapture("final");var w=S.DiagnosticSnapshot();
                Check(w.Tasks.Any(t=>t.Executor==PersonalStart.Person&&t.Action==ActionKind.Inspect&&t.Execution==ExecutionPhase.Completed),"created person completes local task through actual buttons");
                Check(w.Units.All(u=>u.Commander!=PersonalStart.Person)&&w.Grants.Where(g=>g.Agent==PersonalStart.Person).All(g=>!g.Actions.Contains(ActionKind.Transfer)),"no inherited army or tax rights");
                Check(S.SetRegionalActive(true,S.Revision).Code==ResultCode.NotAvailable,"peace role cannot activate fixture armies");
            }
            System.IO.File.WriteAllText(Output+"/"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(checks));GD.Print("PERSONAL_START_PASSED");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
