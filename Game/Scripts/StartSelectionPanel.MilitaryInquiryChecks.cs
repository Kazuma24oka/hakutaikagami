using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class StartSelectionPanel
{
    const int MilitaryInquirySteps=6;
    string MilitaryInquiryCase=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--military-inquiry-case="))?.Split('=',2)[1]??"permission";
    string MilitaryInquiryOutput=>ProjectSettings.GlobalizePath("res://../Docs/Design/MilitaryInquiryV1/runtime-local/"+MilitaryInquiryCase);
    string MilitaryFaultOriginal=>MilitaryInquiryCase=="retry"?stage!.HubSession.DiagnosticSnapshot().Military!.Requests.Single(q=>q.Input.Action==MilitaryAction.Request).Message!:stage!.HubSession.DiagnosticSnapshot().Military!.Events.Single(e=>e.Effect==(MilitaryInquiryCase=="offer"?MilitaryEffect.Offered:MilitaryInquiryCase=="permission"?MilitaryEffect.Permission:MilitaryEffect.Returned)).Message;
    string MilitaryStatusLetter=>stage!.HubSession.DiagnosticSnapshot().Military!.Events.Last(e=>e.Effect==MilitaryEffect.StatusSent).Message;
    void MilitaryFaultSetup()
    {
        MilitaryStep(1);MilitaryStep(2);
        if(MilitaryInquiryCase=="offer")CareerAdvance(CareerDue(stage!.HubSession.DiagnosticSnapshot().Military!.Requests.Last().Message!));
        else if(MilitaryInquiryCase!="retry"){
            for(int n=3;n<=6;n++)MilitaryStep(n);
            if(MilitaryInquiryCase=="handback"){for(int n=7;n<=13;n++)MilitaryStep(n);while(stage!.HubCanPress("military-open"))CareerPress("military-open");}
        }
        stage!.HubSession.DiagnosticTransport((m,t)=>MessageFlow.Fail(m,MilitaryFaultOriginal));stage.HubAdvance(stage.HubSession.Seconds);
    }
    void MilitaryInquiryStep(int n)
    {
        var child=stage!;var s=child.HubSession;
        if(n==0){MilitaryFaultSetup();Need(!child.HubCanPress("military-retry"),"failure not yet personally known does not show retry");return;}
        if(MilitaryInquiryCase=="retry"){
            if(n==1){CareerAdvance(CareerDue(MilitaryFaultOriginal)+1);Need(child.HubCanPress("military-retry"),"physical failure report enables own original retry");}
            if(n==2){CareerPress("military-retry");Need(s.DiagnosticSnapshot().Messaging.Attempts.Count(a=>a.MessageId==MilitaryFaultOriginal)==2,"actual retry button reuses original logical message");}
            if(n==3){CareerAdvance(CareerDue(MilitaryFaultOriginal)+1);s.DiagnosticRecoverLocalCourier(MilitaryFaultOriginal,1);child.HubAdvance(s.Seconds);Need(s.DiagnosticSnapshot().CourierRecoveries?.Single().Arrival==null,"saved original recovery has an explicit physical path and pending local arrival");}
            if(n==4){CareerAdvance(s.DiagnosticSnapshot().CourierRecoveries!.Single().DueAt);Need(s.DiagnosticSnapshot().Military!.Events.Count(e=>e.Effect==MilitaryEffect.Offered)==1&&s.DiagnosticSnapshot().Messaging.Receipts.Count(r=>r.MessageId==MilitaryFaultOriginal)==1,"recovered original and resend reach principal but offer/receipt are singular");}
            if(n==5){CareerPress("military-open");Need(CareerMilitary.View(s,s.PlayerControl).CanAccept,"single physically received offer can be answered");}
            return;
        }
        if(n==1){CareerAdvance(s.Seconds+2);Need(!MessageFlow.Knows(s.DiagnosticSnapshot().Messaging,MilitaryFaultOriginal,s.PlayerControl.Value),"lost original remains unknown");}
        if(n==2)CareerPress("career-status");
        if(n==3){CareerAdvance(CareerDue(CareerOwnMessage));Need(s.GetActorView(s.PlayerControl).Messages.All(m=>m.Id.Value!=MilitaryStatusLetter),"status conclusion is not exposed before physical arrival");}
        if(n==4){CareerAdvance(CareerDue(MilitaryStatusLetter));Need(s.GetActorView(s.PlayerControl).Messages.Single(m=>m.Id.Value==MilitaryStatusLetter).KnownBody==null,"physically arrived status remains sealed");var v=CareerMilitary.View(s,s.PlayerControl);Need(MilitaryInquiryCase=="offer"?!v.CanAccept:MilitaryInquiryCase=="permission"?!v.CanPickup:v.Custody,"unopened status cannot reveal grant/end");}
        if(n==5){CareerPress("military-open");var v=CareerMilitary.View(s,s.PlayerControl);Need(MilitaryInquiryCase=="offer"?v.CanAccept:MilitaryInquiryCase=="permission"?v.CanPickup:!v.Custody,"actual status opening restores personal known offer/permission/end");var w=s.DiagnosticSnapshot();Need(!MessageFlow.Knows(w.Messaging,MilitaryFaultOriginal,s.PlayerControl.Value)&&!w.Messaging.Receipts.Any(r=>r.MessageId==MilitaryFaultOriginal),"citation does not fabricate original personal knowledge or formal receipt");}
    }
    void MilitaryInquiryContinue(int checkpoint)
    {
        for(int n=checkpoint+1;n<MilitaryInquirySteps;n++){MilitaryInquiryStep(n);stage!.HubSession.SetPaused(true);}
        if(MilitaryInquiryCase is "offer" or "retry"){MilitaryStep(5);CareerAdvance(CareerDue(stage!.HubSession.DiagnosticSnapshot().Military!.Requests.Last().Message!)+1);CareerPress("military-open");}
        int start=MilitaryInquiryCase=="handback"?14:8;for(int n=start;n<MilitarySteps;n++){MilitaryStep(n);stage!.HubSession.SetPaused(true);}CareerAdvance(stage!.HubSession.Seconds+1);
    }
    async Task MilitaryInquiryCapture(string name){await Frame();GetViewport().GetTexture().GetImage().SavePng(MilitaryInquiryOutput+"/diagnostic.png");Need(stage!.HubFits(),"military inquiry controls and minimum3D fit actual viewport");GetViewport().GetTexture().GetImage().SavePng(MilitaryInquiryOutput+"/"+name+".png");File.WriteAllText(MilitaryInquiryOutput+"/"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(stage.HubRect()));File.WriteAllText(MilitaryInquiryOutput+"/"+name+"-world.sha256",ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot()));}
    async Task RunMilitaryInquiryChecks()
    {
        Need(new[]{"offer","permission","handback","retry"}.Contains(MilitaryInquiryCase),"supported military inquiry fixture");Directory.CreateDirectory(MilitaryInquiryOutput);((TabContainer)GetParent()).CurrentTab=2;await Frame();
        if(SelectionMode.EndsWith("restore")){ScanProfiles();int i=profiles.FindIndex(p=>p.Profile.Instance==File.ReadAllText(MilitaryInquiryOutput+"/origin-instance.txt"));Need(i>=0,"same military inquiry profile survives process restart");saved.Select(i);Press(load);await Frame();for(int n=0;n<MilitaryInquirySteps;n++){stage!.HubLoad("military-inquiry-s"+n);Need(stage.HubPose()==File.ReadAllText(MilitaryInquiryOutput+"/s"+n+"-pose.json"),"personal knowledge/location/models restored "+n);Need(ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot())==File.ReadAllText(MilitaryInquiryOutput+"/s"+n+"-world.sha256"),"full world restored "+n);await MilitaryInquiryCapture("restored-s"+n);MilitaryInquiryContinue(n);Need(ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot())==File.ReadAllText(MilitaryInquiryOutput+"/final-world.sha256"),"same future actions and checkpoint pauses give full-world identity "+n);await MilitaryInquiryCapture("continued-s"+n);}}
        else{role.Select(8);nameInput.Text="検証軍務回復/"+MilitaryInquiryCase;Press(begin);await Frame();Need(stage!=null&&CareerMilitary.Enabled(stage.HubSession.DiagnosticSnapshot()),"actual normal role starts same military world");var profile=ContractJson.Read<SavedStartProfile>(File.ReadAllBytes(Path.Combine(CurrentDirectory,"start-profile.json")));File.WriteAllText(MilitaryInquiryOutput+"/origin-instance.txt",profile.Instance);for(int n=0;n<MilitaryInquirySteps;n++){MilitaryInquiryStep(n);stage!.HubSession.SetPaused(true);stage.HubSave("military-inquiry-s"+n);File.WriteAllText(MilitaryInquiryOutput+"/s"+n+"-pose.json",stage.HubPose());await MilitaryInquiryCapture("s"+n);}MilitaryInquiryContinue(MilitaryInquirySteps-1);stage!.HubPress("save");await MilitaryInquiryCapture("final");var w=stage.HubSession.DiagnosticSnapshot();Need(w.PlayerControl==profile.Package.Selection.Person&&w.WorldId==profile.Package.World.WorldId&&!w.Units.Any(u=>u.Commander==w.PlayerControl),"same life completes military return/resignation after loss recovery");}
        File.WriteAllText(MilitaryInquiryOutput+"/"+(SelectionMode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(checks));GD.Print("MILITARY_INQUIRY_PASSED");GetTree().Quit();
    }
}
