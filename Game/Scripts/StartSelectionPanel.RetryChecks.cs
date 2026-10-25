using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class StartSelectionPanel
{
    string RetryOutput=>ProjectSettings.GlobalizePath("res://../Docs/Design/DelegationTransportV1/runtime-local");
    void RetryStep(int n)
    {
        var child=stage!;
        if(n==0){child.HubPress("delegate-line");child.HubPress("delegate-focus");child.HubSession.DiagnosticTransport((m,t)=>MessageFlow.Fail(m,DelegationCommand.Message));}
        if(n==1){DelegationAdvance(MessageFlow.Latest(child.HubSession.DiagnosticSnapshot().Messaging,DelegationCommand.Message).DueAt);Need(!MessageFlow.FailureKnown(child.HubSession.DiagnosticSnapshot().Messaging,MessageFlow.Latest(child.HubSession.DiagnosticSnapshot().Messaging,DelegationCommand.Message).Id,child.HubSession.PlayerControl.Value),"physical loss stays unknown before failure return");}
        if(n==2){var w=child.HubSession.DiagnosticSnapshot();var failure=w.Messaging.Messages.Single(m=>m.Kind=="delivery-failure"&&m.Body==MessageFlow.Latest(w.Messaging,DelegationCommand.Message).Id);DelegationAdvance(DelegationDue(failure.Id));}
        if(n==3){child.HubPress("delegate-retry");Need(MessageFlow.Latest(child.HubSession.DiagnosticSnapshot().Messaging,DelegationCommand.Message).Number==2,"real button sends second physical attempt");}
        if(n==4){DelegationAdvance(MessageFlow.Latest(child.HubSession.DiagnosticSnapshot().Messaging,DelegationCommand.Message).DueAt);DelegationAdvance(DelegationDue(DelegationAck));DelegationAdvance(Math.Max(child.HubSession.Seconds,DelegationArrive));DelegationAdvance(DelegationDue(child.HubSession.DiagnosticSnapshot().Delegation!.Arrivals.Single().Message));Need(child.HubSession.DiagnosticSnapshot().Delegation!.Plans.Length==1,"resend has exactly one business execution");}
    }
    void RetryContinue(int n){for(int i=n+1;i<5;i++)RetryStep(i);DelegationAdvance(stage!.HubSession.Seconds+1);}
    async Task RetryCapture(string name){await Frame();Need(stage!.HubFits(),"retry controls and3D fit actual viewport");GetViewport().GetTexture().GetImage().SavePng(RetryOutput+"/"+name+".png");File.WriteAllText(RetryOutput+"/"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(stage.HubRect()));}
    async Task RunRetrySelectionChecks()
    {
        Directory.CreateDirectory(RetryOutput);((TabContainer)GetParent()).CurrentTab=2;await Frame();
        if(SelectionMode.EndsWith("restore")){ScanProfiles();int i=profiles.FindIndex(p=>p.Profile.Instance==File.ReadAllText(RetryOutput+"/origin-instance.txt"));Need(i>=0,"retry profile survives process restart");saved.Select(i);Press(load);await Frame();for(int n=0;n<5;n++){stage!.HubLoad("retry-s"+n);Need(stage.HubPose()==File.ReadAllText(RetryOutput+"/s"+n+"-pose.json"),"retry routes and displayed knowledge exact restore "+n);await RetryCapture("restored-s"+n);RetryContinue(n);await RetryCapture("continued-s"+n);}}
        else{role.Select(6);nameInput.Text="検証再送";Press(begin);await Frame();var profile=ContractJson.Read<SavedStartProfile>(File.ReadAllBytes(Path.Combine(CurrentDirectory,"start-profile.json")));File.WriteAllText(RetryOutput+"/origin-instance.txt",profile.Instance);for(int n=0;n<5;n++){RetryStep(n);stage!.HubSession.SetPaused(true);stage.HubSave("retry-s"+n);File.WriteAllText(RetryOutput+"/s"+n+"-pose.json",stage.HubPose());await RetryCapture("s"+n);}RetryContinue(4);stage!.HubPress("save");await RetryCapture("final");}
        File.WriteAllText(RetryOutput+"/"+(SelectionMode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(checks));GD.Print("DELEGATION_RETRY_PASSED");GetTree().Quit();
    }
}
