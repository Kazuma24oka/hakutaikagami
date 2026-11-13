using Godot;
using System;
using System.IO;
using System.Linq;
using HakutaiKagami.Core.Contracts;

public partial class NewGameRuntime
{
    internal void ReviewConfigureRevenue(ContractWorld world)
    {
        campaigns=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"national-revenue-ui-"+Guid.NewGuid().ToString("N")));
        Profile=NationalStart.New(campaigns,world.NewGame!.Start,world.PlayerControl.Value,world.Seed,null,true);
        host=new WorldSessionHost(new WorldSession(world));saves=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"national-revenue-ui-saves-"+Guid.NewGuid().ToString("N")),world.Definitions);loading=false;
    }
}
public partial class NationalDistrictRevenueCheck:Control
{
    public override async void _Ready()
    {
        try
        {
            var input=System.Environment.GetEnvironmentVariable("HAKUTAI_REVENUE_SNAPSHOT")??throw new InvalidDataException("検査用保存が未指定です");
            var world=ContractJson.Read<ContractWorld>(File.ReadAllBytes(input));
            if(!NationalDistrictRevenue.Enabled(world))throw new InvalidDataException("全国v4保存ではありません");
            var runtime=new NewGameRuntime();runtime.ReviewConfigureRevenue(world);AddChild(runtime);
            for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            runtime.Press("treasury");
            for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var labels=runtime.FindChildren("*","Label",true,false).Cast<Label>().Select(l=>l.Text).ToArray();
            if(labels.Any(t=>t.StartsWith("操作できません",StringComparison.Ordinal)))throw new InvalidDataException("操作時例外が表示されています");
            var view=runtime.Session.HouseTreasuryView()??throw new InvalidDataException("現地公金が表示されません");
            if(!labels.Any(t=>t.StartsWith("郡収入 ",StringComparison.Ordinal))||!labels.Any(t=>t.Contains($"家の資金 {view.HouseCash}文",StringComparison.Ordinal)))throw new InvalidDataException("郡収入と公金残高の表示不一致");
            var output=ProjectSettings.GlobalizePath("res://../Docs/Design/NationalDistrictRevenueV1/runtime-local/ui");Directory.CreateDirectory(output);
            if(DisplayServer.GetName()=="headless")throw new InvalidDataException("描画のある検査が必要です");
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            var picture=Path.Combine(output,"treasury.png");
            if(GetViewport().GetTexture().GetImage().SavePng(picture)!=Error.Ok)throw new IOException("描画を保存できません");
            File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",HouseCash=view.HouseCash,PrivateCash=view.PrivateCash,Scope="ゲーム内部の公金ボタン入力と自前描画",Screenshot=picture,ComputerUse="未使用"},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            GD.Print("NATIONAL_REVENUE_UI_PASS");GetTree().Quit();
        }
        catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
