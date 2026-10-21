using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class StartSelectionPanel
{
    string ReturnRetryOutput=>ProjectSettings.GlobalizePath("res://../Docs/Design/DelegationReturnRetryV1/runtime-local");
    string LatestQuestion=>stage!.HubSession.DiagnosticSnapshot().Messaging.Messages.Last(m=>m.Kind==DelegationInquiry.QueryKind).Id;
    string LatestAnswer=>stage!.HubSession.DiagnosticSnapshot().Messaging.Messages.Last(m=>m.Kind==DelegationInquiry.ReplyKind).Id;
    string OriginalReturn=>stage!.HubSession.DiagnosticSnapshot().Delegation!.Arrivals.Single().Message;
    long OriginalReturnDue=>MessageFlow.Latest(stage!.HubSession.DiagnosticSnapshot().Messaging,OriginalReturn).DueAt;
    void ReturnRetryStep(int n)
    {
        var child=stage!;
        if(n==0){InquiryStep(0);child.HubPress("delegate-query");DelegationAdvance(DelegationDue(LatestQuestion));DelegationAdvance(DelegationDue(LatestAnswer));var w=child.HubSession.DiagnosticSnapshot();Need(!MessageFlow.Knows(w.Messaging,OriginalReturn,child.HubSession.PlayerControl.Value),"ordinary inquiry quotes but does not resend original");Need(DelegationInquiry.View(child.HubSession,child.HubSession.PlayerControl).Single().RetriedCount==null,"old inquiry body has no resend instruction");}
        if(n==1)child.HubPress("delegate-resend-return");
        if(n==2){DelegationAdvance(DelegationDue(LatestQuestion));Need(MessageFlow.Latest(child.HubSession.DiagnosticSnapshot().Messaging,OriginalReturn).Number==2,"recipient sends one physical retry after personally known failure and received request");Need(DelegationInquiry.View(child.HubSession,child.HubSession.PlayerControl).Last().RetriedCount==null,"retry acceptance reply stays unknown until physically returned");}
        if(n==3)DelegationAdvance(OriginalReturnDue-1);
        if(n==4){DelegationAdvance(Math.Max(OriginalReturnDue,DelegationDue(LatestAnswer)));var w=child.HubSession.DiagnosticSnapshot();Need(MessageFlow.Knows(w.Messaging,OriginalReturn,child.HubSession.PlayerControl.Value)&&w.Messaging.Receipts.Any(r=>r.MessageId==OriginalReturn),"actual original retry now has its own formal receipt");Need(DelegationInquiry.View(child.HubSession,child.HubSession.PlayerControl).Last().OriginalReceived,"personal display distinguishes actual original receipt from quotation");Need(DelegationInquiry.View(child.HubSession,child.HubSession.PlayerControl).Last().RetriedCount==1,"delivered self reply reports exactly one accepted original resend");Need(w.Delegation!.Plans.Length==1&&w.Delegation.Arrivals.Length==1,"resending original report never reexecutes placement or creates another report");}
    }
    void ReturnRetryContinue(int n){for(int i=n+1;i<5;i++)ReturnRetryStep(i);DelegationAdvance(stage!.HubSession.Seconds+1);}
    async Task ReturnRetryCapture(string name){await Frame();GetViewport().GetTexture().GetImage().SavePng(ReturnRetryOutput+"/diagnostic.png");Need(stage!.HubFits(),"return retry controls and3D fit actual viewport");GetViewport().GetTexture().GetImage().SavePng(ReturnRetryOutput+"/"+name+".png");File.WriteAllText(ReturnRetryOutput+"/"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(stage.HubRect()));}
    async Task RunReturnRetrySelectionChecks()
    {
        Directory.CreateDirectory(ReturnRetryOutput);((TabContainer)GetParent()).CurrentTab=2;await Frame();
        if(SelectionMode.EndsWith("restore")){ScanProfiles();int i=profiles.FindIndex(p=>p.Profile.Instance==File.ReadAllText(ReturnRetryOutput+"/origin-instance.txt"));Need(i>=0,"original retry normal save survives process restart");saved.Select(i);Press(load);await Frame();for(int n=0;n<5;n++){stage!.HubLoad("return-retry-s"+n);Need(stage.HubPose()==File.ReadAllText(ReturnRetryOutput+"/s"+n+"-pose.json"),"original retry and actual knowledge exact restore "+n);await ReturnRetryCapture("restored-s"+n);ReturnRetryContinue(n);await ReturnRetryCapture("continued-s"+n);}}
        else{role.Select(6);nameInput.Text="検証原返報の再送";Press(begin);await Frame();var profile=ContractJson.Read<SavedStartProfile>(File.ReadAllBytes(Path.Combine(CurrentDirectory,"start-profile.json")));File.WriteAllText(ReturnRetryOutput+"/origin-instance.txt",profile.Instance);for(int n=0;n<5;n++){ReturnRetryStep(n);stage!.HubSession.SetPaused(true);stage.HubSave("return-retry-s"+n);File.WriteAllText(ReturnRetryOutput+"/s"+n+"-pose.json",stage.HubPose());await ReturnRetryCapture("s"+n);}ReturnRetryContinue(4);stage!.HubPress("save");await ReturnRetryCapture("final");}
        File.WriteAllText(ReturnRetryOutput+"/"+(SelectionMode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(checks));GD.Print("DELEGATION_RETURN_RETRY_PASSED");GetTree().Quit();
    }
}
