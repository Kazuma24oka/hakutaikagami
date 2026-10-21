using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class StartSelectionPanel
{
    string CampaignCase=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--campaign-case="))?[16..]??"deployment";
    string CampaignOutput=>ProjectSettings.GlobalizePath("res://../Docs/Design/CreatedCampaignV1/runtime-local/"+CampaignCase);
    long CampaignEnd=>stage!.HubSession.DiagnosticSnapshot().Region!.Units.Where(u=>u.Commander==stage.HubSession.PlayerControl).SelectMany(u=>u.Route).Select(l=>l.EndAt).DefaultIfEmpty(stage.HubSession.Seconds).Max();
    async Task CampaignCapture(string name){await Frame();GetViewport().GetTexture().GetImage().SavePng(CampaignOutput+"/diagnostic.png");Need(stage!.HubFits(),"campaign controls and minimum 3D area fit actual window");GetViewport().GetTexture().GetImage().SavePng(CampaignOutput+"/"+name+".png");File.WriteAllText(CampaignOutput+"/"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(stage.HubRect()));}
    void CampaignStep(int n){var child=stage!;
        if(n==0&&CampaignCase=="supplies")child.HubPress("supply-pack");
        if(n==0)child.HubAdvance(CampaignCase=="incoming"?2:1);
        if(n==1){if(CampaignCase!="incoming"){child.HubAdvance(2);child.HubPress("work-open");if(CampaignCase=="dispatch"){child.HubPress("retainer-interrupt");child.HubAdvance(3);}}if(CampaignCase is "deployment" or "combat" or "supplies")child.HubAdvance(9);child.HubPress("site");if(CampaignCase=="dispatch")child.HubPress("retainer-explain");if(CampaignCase=="incoming")child.HubPress("work-open");}
        if(n==2){child.HubPress(CampaignCase is "combat" or "supplies"?"own-engage":"own-line");child.HubAdvance(CampaignCase is "combat" or "supplies"?60:child.HubSession.Seconds+10);if(CampaignCase is "combat" or "supplies")Need(child.HubSession.DiagnosticSnapshot().Region!.Losses.Length>0,"actual 3D engagement applies contact losses");}
        if(n==3){if(CampaignCase is "combat" or "supplies")child.HubPress("field-report");child.HubPress("own-return");child.HubAdvance(child.HubSession.Seconds+1);}
        if(n==4){child.HubAdvance(CampaignEnd+1);child.HubPress("site");if(CampaignCase=="supplies"){Need(FieldSupply.Carried(child.HubSession.DiagnosticSnapshot()).Quantity<FieldSupply.Capacity,"carried food consumed in shared world clock");}child.HubAdvance(child.HubSession.Seconds+6);}
    }
    void CampaignContinue(int checkpoint){for(int n=checkpoint+1;n<5;n++)CampaignStep(n);var child=stage!;if(CampaignCase=="supplies"){child.HubPress("supply-return");Need(FieldSupply.Carried(child.HubSession.DiagnosticSnapshot()).Quantity==0,"actual return button returns remaining food");}if(child.HubCanPress("work-open"))child.HubPress("work-open");if(child.HubCanPress("work-accept"))child.HubPress("work-accept");if(child.HubSession.Seconds<4000)child.HubAdvance(4000);}
    async Task RunCampaignSelectionChecks()
    {
        Directory.CreateDirectory(CampaignOutput);((TabContainer)GetParent()).CurrentTab=2;await Frame();
        if(SelectionMode.EndsWith("restore")){
            ScanProfiles();int index=profiles.FindIndex(p=>p.Profile.Instance==File.ReadAllText(CampaignOutput+"/origin-instance.txt"));Need(index>=0,"campaign original start package listed");saved.Select(index);Press(load);await Frame();
            for(int n=0;n<5;n++){stage!.HubLoad("campaign-s"+n);Need(stage.HubPose()==File.ReadAllText(CampaignOutput+"/s"+n+"-pose.json"),"same person position knowledge and scene restored "+n);await CampaignCapture("restored-s"+n);CampaignContinue(n);await CampaignCapture("continued-s"+n);}
        }else{
            role.Select(CampaignCase=="supplies"?4:CampaignCase=="combat"?3:2);nameInput.Text="検証配置家臣";Press(begin);await Frame();Need(stage!=null&&CreatedCampaign.Enabled(stage.HubSession.DiagnosticSnapshot()),"actual dropdown starts limited command profile");var profile=ContractJson.Read<SavedStartProfile>(File.ReadAllBytes(Path.Combine(CurrentDirectory,"start-profile.json")));File.WriteAllText(CampaignOutput+"/origin-instance.txt",profile.Instance);string world=stage!.HubSession.DiagnosticSnapshot().WorldId.Value;
            for(int n=0;n<5;n++){
                CampaignStep(n);
                Need(stage.HubSession.DiagnosticSnapshot().WorldId.Value==world,"peace and field share world identity "+n);stage.HubSession.SetPaused(true);stage.HubSave("campaign-s"+n);File.WriteAllText(CampaignOutput+"/s"+n+"-pose.json",stage.HubPose());await CampaignCapture("s"+n);
            }
            CampaignContinue(4);stage.HubPress("save");await CampaignCapture("final");Need(!stage.HubSession.DiagnosticSnapshot().Region!.Active,"actual buttons return same officer to garrison");
        }
        File.WriteAllText(CampaignOutput+"/"+(SelectionMode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(checks));GD.Print("CREATED_CAMPAIGN_PASSED");GetTree().Quit();
    }
}
