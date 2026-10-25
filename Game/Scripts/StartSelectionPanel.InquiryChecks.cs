using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class StartSelectionPanel
{
    string InquiryOutput=>ProjectSettings.GlobalizePath("res://../Docs/Design/DelegationInquiryV1/runtime-local");
    string InquiryMessage=>stage!.HubSession.DiagnosticSnapshot().Messaging.Messages.Single(m=>m.Kind==DelegationInquiry.QueryKind).Id;
    string InquiryReply=>stage!.HubSession.DiagnosticSnapshot().Messaging.Messages.Single(m=>m.Kind==DelegationInquiry.ReplyKind).Id;
    void InquiryStep(int n)
    {
        var child=stage!;
        if(n==0){child.HubPress("delegate-line");child.HubPress("delegate-focus");DelegationAdvance(DelegationDue(DelegationCommand.Message));DelegationAdvance(DelegationDue(DelegationAck));DelegationAdvance(Math.Max(child.HubSession.Seconds,DelegationArrive));var report=child.HubSession.DiagnosticSnapshot().Delegation!.Arrivals.Single().Message;child.HubSession.DiagnosticTransport((m,t)=>MessageFlow.Fail(m,report));DelegationAdvance(DelegationDue(report));Need(DelegationMission.View(child.HubSession,child.HubSession.PlayerControl).Single().Progress=="到達の返報は未確認","lost original arrival stays unknown");}
        if(n==1)child.HubPress("delegate-query");
        if(n==2){DelegationAdvance(DelegationDue(InquiryMessage));Need(DelegationInquiry.View(child.HubSession,child.HubSession.PlayerControl).Single().Progress==null,"inquiry answer remains private until physically returned");}
        if(n==3)DelegationAdvance(DelegationDue(InquiryReply)-1);
        if(n==4){DelegationAdvance(DelegationDue(InquiryReply));var w=child.HubSession.DiagnosticSnapshot();var arrival=w.Delegation!.Arrivals.Single();Need(DelegationMission.View(child.HubSession,child.HubSession.PlayerControl).Single().Progress=="配置到達の申告","quoted self report recovers arrival information");Need(!MessageFlow.Knows(w.Messaging,arrival.Message,child.HubSession.PlayerControl.Value)&&!w.Messaging.Receipts.Any(r=>r.MessageId==arrival.Message),"inquiry does not forge original formal receipt");Need(w.Delegation.Plans.Length==1,"inquiry never reexecutes placement");}
    }
    void InquiryContinue(int n){for(int i=n+1;i<5;i++)InquiryStep(i);DelegationAdvance(stage!.HubSession.Seconds+1);}
    async Task InquiryCapture(string name){await Frame();GetViewport().GetTexture().GetImage().SavePng(InquiryOutput+"/diagnostic.png");Need(stage!.HubFits(),"inquiry controls and3D fit actual viewport");GetViewport().GetTexture().GetImage().SavePng(InquiryOutput+"/"+name+".png");File.WriteAllText(InquiryOutput+"/"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(stage.HubRect()));}
    async Task RunInquirySelectionChecks()
    {
        Directory.CreateDirectory(InquiryOutput);((TabContainer)GetParent()).CurrentTab=2;await Frame();
        if(SelectionMode.EndsWith("restore")){ScanProfiles();int i=profiles.FindIndex(p=>p.Profile.Instance==File.ReadAllText(InquiryOutput+"/origin-instance.txt"));Need(i>=0,"inquiry normal saved profile survives process restart");saved.Select(i);Press(load);await Frame();for(int n=0;n<5;n++){stage!.HubLoad("inquiry-s"+n);Need(stage.HubPose()==File.ReadAllText(InquiryOutput+"/s"+n+"-pose.json"),"inquiry envelope and displayed knowledge exact restore "+n);await InquiryCapture("restored-s"+n);InquiryContinue(n);await InquiryCapture("continued-s"+n);}}
        else{role.Select(6);nameInput.Text="検証照会";Press(begin);await Frame();var profile=ContractJson.Read<SavedStartProfile>(File.ReadAllBytes(Path.Combine(CurrentDirectory,"start-profile.json")));File.WriteAllText(InquiryOutput+"/origin-instance.txt",profile.Instance);for(int n=0;n<5;n++){InquiryStep(n);stage!.HubSession.SetPaused(true);stage.HubSave("inquiry-s"+n);File.WriteAllText(InquiryOutput+"/s"+n+"-pose.json",stage.HubPose());await InquiryCapture("s"+n);}InquiryContinue(4);stage!.HubPress("save");await InquiryCapture("final");}
        File.WriteAllText(InquiryOutput+"/"+(SelectionMode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(checks));GD.Print("DELEGATION_INQUIRY_PASSED");GetTree().Quit();
    }
}
