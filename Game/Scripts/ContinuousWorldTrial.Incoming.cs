using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    bool IncomingEnabled=>ReviewEnabled||Name=="IncomingMissionTrial";
    Label incomingText=null!;
    void BuildIncomingUI(VBoxContainer col)
    {
        foreach(var id in new[]{"inspect","levy","depart","site","council","own-line","own-engage","own-return"})buttons[id].Visible=false;
        var row=new HFlowContainer();col.AddChild(row);
        Add(row,"personnel-catalog","収録台帳",OpenPersonnelCatalog);
        Add(row,"incoming-lord","試作：主君が点検命令を発送",()=>SendIncoming(false));
        Add(row,"incoming-clerk","試作：書役が点検依頼を発送",()=>SendIncoming(true));
        Add(row,"incoming-open","届いた書状を開封",()=>{var c=IncomingMission.View(S,ContractFixture.Officer).Last(x=>x.CanOpen);Require(S.Open(ContractFixture.Officer,c.Message,S.Revision));});
        Add(row,"incoming-accept","依頼を受諾",()=>DecideIncoming(true));
        Add(row,"incoming-decline","依頼を辞退",()=>DecideIncoming(false));
        incomingText=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(incomingText);
    }
    void SendIncoming(bool subordinate)=>Require(S.Submit(NationalPersonnel.Build(S.DiagnosticSnapshot(),new(subordinate?"work:clerk-inspect":"work:lord-inspect"),"request:incoming/"+S.Revision,Case=="expiry"?2:60),S.Revision));
    void DecideIncoming(bool accept){var c=IncomingMission.View(S,ContractFixture.Officer).Last(x=>x.CanDecide);Require(S.Assent(c.Task!.Value,ContractFixture.Officer,accept,S.Revision));}
    void RefreshIncoming()
    {
        if(incomingText==null)return;var cards=IncomingMission.View(S,ContractFixture.Officer);
        incomingText.Text=cards.Length==0?"受信任務：なし（未着の内容は表示しません）":string.Join("\n",cards.TakeLast(2).Select(c=>MissionText(c.Text)));
        buttons["incoming-open"].Disabled=!cards.Any(c=>c.CanOpen);
        buttons["incoming-accept"].Disabled=buttons["incoming-decline"].Disabled=!cards.Any(c=>c.CanDecide);
        foreach(var issuer in new[]{ContractFixture.Lord,ContractFixture.Clerk})buttons[issuer==ContractFixture.Lord?"incoming-lord":"incoming-clerk"].Disabled=!NationalPersonnel.DispatcherOptions(S.DiagnosticSnapshot(),issuer).Single().CanDispatch;
    }
    string IncomingPose()=>System.Text.Json.JsonSerializer.Serialize(new{Scene=LoopPose(),Text=incomingText.Text,Cards=IncomingMission.View(S,ContractFixture.Officer)});
    async Task IncomingCapture(string name){await Capture(name);System.IO.File.WriteAllText(Output+"/"+Case+"-"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(new[]{incomingText.GlobalPosition.X,incomingText.GlobalPosition.Y,Size.X-20,Size.Y-incomingText.GlobalPosition.Y-10}));}
    void IncomingFinish(){if(S.Paused)Click("resume");if(Case=="self"||Case=="refuse") {if(IncomingMission.View(S,ContractFixture.Officer).Any(c=>c.CanDecide))Click(Case=="refuse"?"incoming-decline":"incoming-accept");}Advance(12);Refresh();}
    async Task RunIncomingChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);
            if(Mode.EndsWith("restore")){
                for(int n=0;n<3;n++){Load(Case+"-s"+n);Check(IncomingPose()==System.IO.File.ReadAllText(Output+"/"+Case+"-s"+n+"-pose.json"),"restored known letter, task, reasons and 3D "+n);await IncomingCapture("restored-s"+n);if(n<2){if(S.Paused)Click("resume");if(n==0)S.AdvanceTo(1);Click("incoming-open");}IncomingFinish();await IncomingCapture("continued-s"+n);}
            }else{
                Click(Case=="self"||Case=="refuse"?"incoming-clerk":"incoming-lord");
                Check(IncomingMission.View(S,ContractFixture.Officer).Length==0,"in transit contents hidden");
                for(int n=0;n<3;n++){
                    if(n==1){S.AdvanceTo(1);Refresh();Check(S.Paused,"important delivered letter pauses time");Check(IncomingMission.View(S,ContractFixture.Officer).Single().Task==null,"unopened content and task hidden");}
                    if(n==2){Click("incoming-open");Check(buttons["incoming-accept"].Disabled==(Case!="self"&&Case!="refuse"),"binding order cannot be treated as voluntary");}
                    Refresh();Save(Case+"-s"+n);System.IO.File.WriteAllText(Output+"/"+Case+"-s"+n+"-pose.json",IncomingPose());await IncomingCapture("s"+n);
                }
                IncomingFinish();await IncomingCapture("final");var t=S.DiagnosticSnapshot().Tasks.Single();Check(t.Execution==(Case=="refuse"||Case=="expiry"?ExecutionPhase.Stopped:ExecutionPhase.Completed),"chosen decision and expiry govern effect");
                Check(incomingText.GlobalPosition.Y+incomingText.Size.Y<Size.Y,"letter text fits rendered window");
            }
            System.IO.File.WriteAllText(Output+"/"+Case+"-"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks}));GD.Print("INCOMING_MISSION_PASSED "+Case);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
