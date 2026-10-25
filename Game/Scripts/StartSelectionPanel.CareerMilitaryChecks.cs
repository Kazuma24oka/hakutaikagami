using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class StartSelectionPanel
{
    const int MilitarySteps=17;
    string MilitaryOutput=>ProjectSettings.GlobalizePath("res://../Docs/Design/CareerMilitaryV1/runtime-local");
    void MilitaryStep(int n)
    {
        var child=stage!;var s=child.HubSession;
        switch(n){
        case 0:Need(!CareerMilitary.Custody(s.DiagnosticSnapshot())&&!child.HubCanPress("site")&&!child.HubCanPress("supply-pack"),"ronin has neither command nor supply controls");break;
        case 1:CareerPress("career-apply");CareerAdvance(CareerDue(CareerOwnMessage)+1);CareerPress("career-open");CareerPress("career-accept");CareerAdvance(CareerDue(CareerOwnMessage)+1);CareerPress("career-open");CareerPress("work-open");CareerAdvance(s.Seconds+5);Need(!child.HubCanPress("site")&&!child.HubCanPress("supply-pack"),"service alone grants no army/supply");break;
        case 2:CareerPress("military-request");break;
        case 3:CareerAdvance(s.Seconds+2);Need(!child.HubCanPress("military-accept"),"unopened military offer is not known");break;
        case 4:CareerPress("military-open");Need(child.HubCanPress("military-accept"),"opened conditions permit answer");break;
        case 5:CareerPress("military-accept");break;
        case 6:CareerAdvance(CareerDue(s.DiagnosticSnapshot().Military!.Requests.Last().Message!));Need(!CareerMilitary.Custody(s.DiagnosticSnapshot())&&!child.HubCanPress("military-pickup"),"principal assent is not known permission or local pickup");break;
        case 7:CareerAdvance(CareerDue(s.DiagnosticSnapshot().Military!.Events.Single(e=>e.Effect==MilitaryEffect.Permission).Message));CareerPress("military-open");Need(!child.HubCanPress("site"),"opened permission still requires local pickup");break;
        case 8:{var p=s.GetBranchView(s.PlayerControl).Position;CareerPress("military-pickup");Need(s.GetBranchView(s.PlayerControl).Position==p,"local custody leaves body position unchanged");break;}
        case 9:{var p=s.GetBranchView(s.PlayerControl).Position;CareerPress("supply-pack");CareerPress("site");Need(s.GetBranchView(s.PlayerControl).Position==p,"deployment at anchor leaves body position unchanged");break;}
        case 10:CareerPress("own-line");CareerAdvance(s.Seconds+10);Need(s.DiagnosticSnapshot().Region!.Commands.Last().Result=="applied","actual formation line applies within granted placement rights");break;
        case 11:CareerPress("own-return");CareerAdvance(s.Seconds+1);CareerAdvance(CampaignEnd+1);CareerPress("site");Need(FieldSupply.Carried(s.DiagnosticSnapshot()).Quantity<FieldSupply.Capacity,"real march consumes house-owned carried food");break;
        case 12:CareerPress("field-report");CareerPress("military-return");CareerAdvance(s.Seconds+2);Need(CareerMilitary.Custody(s.DiagnosticSnapshot())&&s.DiagnosticSnapshot().Military!.Events.Any(e=>e.Effect==MilitaryEffect.Waiting),"received handover request waits while carried stock remains");break;
        case 13:{var p=s.GetBranchView(s.PlayerControl).Position;CareerPress("supply-return");CareerAdvance(s.Seconds+1);Need(!CareerMilitary.Custody(s.DiagnosticSnapshot())&&s.GetBranchView(s.PlayerControl).Position==p,"lord retakes command only after local return, stock and received report without teleport");Need(CareerMilitary.View(s,s.PlayerControl).Custody,"actual handback does not leak before its notice");break;}
        case 14:CareerAdvance(s.Seconds+2);while(child.HubCanPress("military-open"))CareerPress("military-open");Need(!CareerMilitary.View(s,s.PlayerControl).Custody&&!child.HubCanPress("supply-pack"),"known handback removes personal command and supply controls");break;
        case 15:CareerPress("career-release");CareerAdvance(CareerDue(CareerOwnMessage)+2);while(child.HubCanPress("career-open"))CareerPress("career-open");Need(Career.View(s,s.PlayerControl).Role.StartsWith("浪人"),"same officer leaves service after handover");break;
        case 16:child.HubDestination("kofu");CareerPress("depart");CareerAdvance(s.Seconds+2);Need(TravelWorld.Active(s.DiagnosticSnapshot()),"same life starts road travel after army return and resignation");break;
        }
    }
    void MilitaryContinue(int checkpoint){for(int n=checkpoint+1;n<MilitarySteps;n++){MilitaryStep(n);stage!.HubSession.SetPaused(true);}CareerAdvance(stage!.HubSession.Seconds+1);}
    async Task MilitaryCapture(string name){await Frame();ViewportCapture.Save(GetViewport(),MilitaryOutput+"/diagnostic.png");Need(stage!.HubFits(),"military controls and minimum3D fit actual viewport");ViewportCapture.Save(GetViewport(),MilitaryOutput+"/"+name+".png");File.WriteAllText(MilitaryOutput+"/"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(stage.HubRect()));File.WriteAllText(MilitaryOutput+"/"+name+"-world.sha256",ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot()));}
    async Task RunMilitarySelectionChecks()
    {
        Directory.CreateDirectory(MilitaryOutput);((TabContainer)GetParent()).CurrentTab=2;await Frame();
        if(SelectionMode.EndsWith("restore")){ScanProfiles();int i=profiles.FindIndex(p=>p.Profile.Instance==File.ReadAllText(MilitaryOutput+"/origin-instance.txt"));Need(i>=0,"same-life military normal saved profile survives process restart");saved.Select(i);Press(load);await Frame();for(int n=0;n<MilitarySteps;n++){stage!.HubLoad("military-s"+n);Need(stage.HubPose()==File.ReadAllText(MilitaryOutput+"/s"+n+"-pose.json"),"same personal UI/location/models restored "+n);Need(ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot())==File.ReadAllText(MilitaryOutput+"/s"+n+"-world.sha256"),"full world restored "+n);await MilitaryCapture("restored-s"+n);MilitaryContinue(n);Need(ContractJson.CanonicalHash(stage.HubSession.DiagnosticSnapshot())==File.ReadAllText(MilitaryOutput+"/final-world.sha256"),"same continuation yields identical full world "+n);await MilitaryCapture("continued-s"+n);}}
        else{role.Select(8);nameInput.Text="検証平戦継続";Press(begin);await Frame();Need(stage!=null&&CareerMilitary.Enabled(stage.HubSession.DiagnosticSnapshot()),"normal role selection creates same-life military profile: "+message.Text);var profile=ContractJson.Read<SavedStartProfile>(File.ReadAllBytes(Path.Combine(CurrentDirectory,"start-profile.json")));File.WriteAllText(MilitaryOutput+"/origin-instance.txt",profile.Instance);for(int n=0;n<MilitarySteps;n++){MilitaryStep(n);stage!.HubSession.SetPaused(true);stage.HubSave("military-s"+n);File.WriteAllText(MilitaryOutput+"/s"+n+"-pose.json",stage.HubPose());await MilitaryCapture("s"+n);}MilitaryContinue(MilitarySteps-1);stage!.HubPress("save");await MilitaryCapture("final");var w=stage.HubSession.DiagnosticSnapshot();Need(w.PlayerControl==profile.Package.Selection.Person&&w.WorldId==profile.Package.World.WorldId&&w.Seed==profile.Package.World.Seed,"one person/world/seed across service, work, sortie, custody return and resignation");Need(!w.Units.Any(u=>u.Commander==w.PlayerControl)&&!w.ResourceLots.Any(l=>l.Owner==w.PlayerControl)&&!w.Region!.Hostile,"house retains assets; no unauthorized engagement");}
        File.WriteAllText(MilitaryOutput+"/"+(SelectionMode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(checks));GD.Print("MILITARY_PASSED");GetTree().Quit();
    }
}
