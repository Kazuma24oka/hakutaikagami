using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class StartSelectionPanel
{
    string CareerCase=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--career-case="))?[14..]??"release";
    int CareerSteps=>CareerCase=="refuse"?3:CareerCase=="decline"?5:7;
    string CareerOutput=>ProjectSettings.GlobalizePath("res://../Docs/Design/CareerV1/runtime-local/"+CareerCase);
    void CareerAdvance(long at){while(stage!.HubSession.Seconds<at){long before=stage.HubSession.Seconds;stage.HubAdvance(at);Need(stage.HubSession.Seconds>before,"career common clock advances");}}
    void CareerPress(string id){Need(stage!.HubCanPress(id),"actual enabled/visible career action "+id);stage.HubPress(id);}
    string CareerNotice(CareerEventKind kind)=>stage!.HubSession.DiagnosticSnapshot().Career!.Events.Last(e=>e.Kind==kind).Message;
    long CareerDue(string id)=>MessageFlow.Latest(stage!.HubSession.DiagnosticSnapshot().Messaging,id).DueAt;
    string CareerOwnMessage=>stage!.HubSession.DiagnosticSnapshot().Career!.Requests.Last().Message;
    void CareerStep(int n)
    {
        var child=stage!;
        if(n==0){CareerPress("career-apply");Need(child.HubSession.DiagnosticSnapshot().Career!.Requests.Length==1,"application is one physical business request");return;}
        if(n==1){CareerAdvance(CareerDue(child.HubSession.DiagnosticSnapshot().Career!.Requests.First().Message)+1);Need(Career.View(child.HubSession,child.HubSession.PlayerControl).Role.StartsWith("浪人"),"unopened offer or refusal does not change known role");Need(!Career.View(child.HubSession,child.HubSession.PlayerControl).Cases.Single().CanAccept,"unopened terms cannot be accepted");return;}
        if(CareerCase=="refuse"){CareerPress("career-open");Need(Career.View(child.HubSession,child.HubSession.PlayerControl).CanApply,"refusal opened without gaining rights");return;}
        if(n==2){CareerPress("career-open");Need(Career.View(child.HubSession,child.HubSession.PlayerControl).Cases.Single().CanAccept,"opened conditions and receipt deadline are available before answering");return;}
        if(n==3){CareerPress(CareerCase=="decline"?"career-decline":"career-accept");Need(!child.HubSession.DiagnosticSnapshot().Memberships.Any(m=>m.Subject==child.HubSession.PlayerControl),"clicking assent is not principal receipt/effect");return;}
        if(CareerCase=="decline"){CareerAdvance(CareerDue(CareerOwnMessage)+1);CareerPress("career-open");Need(Career.View(child.HubSession,child.HubSession.PlayerControl).CanApply,"decline physically confirmed without service");return;}
        if(n==4){CareerAdvance(CareerDue(CareerOwnMessage));Need(Career.ActiveAt(child.HubSession.DiagnosticSnapshot(),RetainerStart.Lord,child.HubSession.Seconds),"mutual assent effective after principal receives it");Need(Career.View(child.HubSession,child.HubSession.PlayerControl).Role.StartsWith("浪人"),"unreceived confirmation does not reveal true membership");return;}
        if(n==5){CareerAdvance(CareerDue(CareerNotice(CareerEventKind.Joined)));CareerPress("career-open");Need(Career.View(child.HubSession,child.HubSession.PlayerControl).Role.StartsWith("織田家所属"),"known agreement changes personal role");CareerPress("work-open");if(child.HubCanPress("work-accept"))CareerPress("work-accept");CareerAdvance(child.HubSession.Seconds+5);if(CareerCase=="release"){CareerPress("career-release");CareerAdvance(CareerDue(CareerOwnMessage));}Need(!Career.ActiveAt(child.HubSession.DiagnosticSnapshot(),RetainerStart.Lord,child.HubSession.Seconds),"own received reports permit agreed release or declared training end");Need(Career.View(child.HubSession,child.HubSession.PlayerControl).Role.StartsWith("織田家所属"),"end of service stays unknown before notice opening");return;}
        string end=CareerNotice(CareerCase=="release"?CareerEventKind.Released:CareerEventKind.Dismissed);CareerAdvance(CareerDue(end));CareerPress("career-open");Need(Career.View(child.HubSession,child.HubSession.PlayerControl).Role.StartsWith("浪人"),"end notice changes known role without ending life");
        if(CareerCase=="journey"){child.HubDestination("kofu");CareerPress("depart");CareerAdvance(child.HubSession.Seconds+2);Need(TravelWorld.Active(child.HubSession.DiagnosticSnapshot()),"same officer begins actual road journey after dismissal");}
    }
    void CareerContinue(int checkpoint)
    {
        for(int n=checkpoint+1;n<CareerSteps;n++){CareerStep(n);stage!.HubSession.SetPaused(true);}
        if(CareerCase=="journey"){
            long arrival=stage!.HubSession.DiagnosticSnapshot().Branches!.Journeys.Last().Legs[^1].EndAt;CareerAdvance(arrival+1);CareerPress("work-open");if(stage.HubCanPress("work-accept"))CareerPress("work-accept");CareerAdvance(stage.HubSession.Seconds+5);
            Need(stage.HubSession.DiagnosticSnapshot().Tasks.Last(t=>t.Action==ActionKind.Inspect).Kind==TaskKind.VoluntaryRequest,"same ronin continues voluntary work in another region");
        }else CareerAdvance(stage!.HubSession.Seconds+1);
    }
    async Task CareerCapture(string name){await Frame();GetViewport().GetTexture().GetImage().SavePng(CareerOutput+"/diagnostic.png");Need(stage!.HubFits(),"career controls and minimum3D fit actual viewport");GetViewport().GetTexture().GetImage().SavePng(CareerOutput+"/"+name+".png");File.WriteAllText(CareerOutput+"/"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(stage.HubRect()));}
    async Task RunCareerSelectionChecks()
    {
        Directory.CreateDirectory(CareerOutput);((TabContainer)GetParent()).CurrentTab=2;await Frame();
        if(SelectionMode.EndsWith("restore")){ScanProfiles();int i=profiles.FindIndex(p=>p.Profile.Instance==File.ReadAllText(CareerOutput+"/origin-instance.txt"));Need(i>=0,"career normal saved profile survives process restart");saved.Select(i);Press(load);await Frame();for(int n=0;n<CareerSteps;n++){stage!.HubLoad("career-s"+n);Need(stage.HubPose()==File.ReadAllText(CareerOutput+"/s"+n+"-pose.json"),"career exact personal text/location/models restore "+n);File.WriteAllText(CareerOutput+"/restored-s"+n+"-world.sha256",ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot()));Need(ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot())==File.ReadAllText(CareerOutput+"/s"+n+"-world.sha256"),"career full world restore "+n);await CareerCapture("restored-s"+n);CareerContinue(n);File.WriteAllText(CareerOutput+"/continued-s"+n+"-world.sha256",ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot()));Need(ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot())==File.ReadAllText(CareerOutput+"/final-world.sha256"),"same future actions and save pauses give identical full world "+n);await CareerCapture("continued-s"+n);}}
        else{role.Select(7);nameInput.Text="検証人生/"+CareerCase;Press(begin);await Frame();Need(stage!=null&&Career.Enabled(stage.HubSession.DiagnosticSnapshot()),"actual career role selection starts one continuing life");var profile=ContractJson.Read<SavedStartProfile>(File.ReadAllBytes(Path.Combine(CurrentDirectory,"start-profile.json")));File.WriteAllText(CareerOutput+"/origin-instance.txt",profile.Instance);for(int n=0;n<CareerSteps;n++){CareerStep(n);stage!.HubSession.SetPaused(true);stage.HubSave("career-s"+n);File.WriteAllText(CareerOutput+"/s"+n+"-pose.json",stage.HubPose());File.WriteAllText(CareerOutput+"/s"+n+"-world.sha256",ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot()));await CareerCapture("s"+n);}CareerContinue(CareerSteps-1);File.WriteAllText(CareerOutput+"/final-world.sha256",ContractJson.CanonicalHash(stage!.HubSession.DiagnosticSnapshot()));stage.HubPress("save");await CareerCapture("final");var w=stage.HubSession.DiagnosticSnapshot();Need(w.PlayerControl==profile.Package.Selection.Person&&w.WorldId==profile.Package.World.WorldId&&w.Seed==profile.Package.World.Seed,"same person/world/seed across career history");Need(!w.Units.Any(u=>u.Commander==w.PlayerControl)&&!w.ResourceLots.Any(l=>l.Owner==w.PlayerControl),"career never transfers armies or private property");}
        File.WriteAllText(CareerOutput+"/"+(SelectionMode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(checks));GD.Print("CAREER_PASSED");GetTree().Quit();
    }
}
