using Godot;
using System;
using System.IO;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    internal void ReviewConfigureClinical(ContractWorld w)
    {
        var d=w.NewGame!;campaigns=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"clinical-ui-"+Guid.NewGuid().ToString("N")));
        Profile=campaigns.New(d.Start,d.Registered,d.Selected.Value,d.Seed,NewGameWorld.CompactPersonalAuthority,d.HistoricalCatalog,NpcJudgment.Policy,LocalInspectionFindings.Policy,WorkFollowup.Policy,LocalWorkDemand.Policy,
            developmentPolicy:CharacterDevelopment.Policy,companionPolicy:PersonalCompanions.Policy,socialPolicy:CharacterRelations.Policy,judgment:d.Judgment,healthPolicy:ClinicalHealth.Policy,previewOnly:true);
        host=new(new WorldSession(w));saves=new(Path.Combine(Path.GetTempPath(),"clinical-ui-save-"+Guid.NewGuid().ToString("N")),w.Definitions);loading=false;
    }
}
public partial class ClinicalHealthCheck:Control
{
    public override async void _Ready()
    {
        try
        {
            var root=ProjectSettings.GlobalizePath("res://../Docs/Design/HealthExpansionV1/runtime-local");
            var w=ContractJson.Read<ContractWorld>(File.ReadAllBytes(Path.Combine(root,"ready.json")));
            var runtime=new NewGameRuntime();runtime.ReviewConfigureClinical(w);AddChild(runtime);
            for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            runtime.ReviewPersonalLifeOpen();
            for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var c=runtime.Session.HealthView(runtime.Session.PlayerControl)!.Conditions.First(x=>!x.Treated);
            runtime.ReviewHealthPress("self:"+c.Id.Value);
            if(runtime.Session.ClinicalView()!.TreatmentUntil is not {} due)throw new InvalidDataException("診療が始まりません");
            if(!runtime.ReviewPersonalLifeText().Contains("診療"))throw new InvalidDataException("診療の表示がありません");
            for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            if(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,"treatment.png"))!=Error.Ok)throw new IOException("描画の保存に失敗しました");
            runtime.ReviewPersonalLifeClose();runtime.Advance(due+1);runtime.ReviewPersonalLifeOpen();
            if(runtime.Session.ClinicalView()!.MedicineExperience!=2||!runtime.Session.HealthView(runtime.Session.PlayerControl)!.Conditions.Any(x=>x.Treated))throw new InvalidDataException("診療完了と表示が一致しません");
            for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            if(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,"completed.png"))!=Error.Ok)throw new IOException("描画の保存に失敗しました");
            File.WriteAllText(Path.Combine(root,"ui-result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Medicine=2,Scope="本人の診療ボタン→共通時計→完了→経験の表示",ComputerUse="未使用"}));
            GD.Print("CLINICAL_HEALTH_UI_PASS");GetTree().Quit();
        }
        catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
