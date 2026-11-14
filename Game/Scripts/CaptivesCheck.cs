using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    internal void ReviewConfigureCaptives(ContractWorld w)
    {
        var d=w.NewGame!;campaigns=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"captives-ui-"+Guid.NewGuid().ToString("N")));
        Profile=campaigns.New(d.Start,d.Registered,d.Selected.Value,d.Seed,d.PersonalAuthorityLayout,d.HistoricalCatalog,d.NpcRoutinePolicy,d.InspectionPolicy,d.WorkPolicy,worldArmyPolicy:d.WorldArmyPolicy,previewOnly:true);
        host=new(new WorldSession(w));saves=new(Path.Combine(Path.GetTempPath(),"captives-ui-save-"+Guid.NewGuid().ToString("N")),w.Definitions);loading=false;
    }
    internal void ReviewCaptivesOpen()=>ShowCaptives();
    internal string ReviewStatusText()=>string.Join(" ",Walk(this).OfType<Label>().Select(l=>l.Text));
    static IEnumerable<Node> Walk(Node n){yield return n;foreach(var c in n.GetChildren())foreach(var x in Walk(c))yield return x;}
    internal string ReviewCaptivesText()=>captivesPopup==null?"":string.Join("\n",Walk(captivesPopup).OfType<Label>().Select(l=>l.Text));
    internal void ReviewCaptivesPress(string label)
    {
        var button=Walk(captivesPopup!).OfType<Button>().FirstOrDefault(b=>b.Text==label)??throw new InvalidDataException(label+" のボタンがありません");
        if(button.Disabled)throw new InvalidDataException(label+" が押せません");button.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
public partial class CaptivesCheck:Control
{
    public override async void _Ready()
    {
        var root=RecordPath.Globalize("res://../Docs/Design/BattleCaptivityV1/runtime-local");
        try
        {
            var w=ContractJson.Read<ContractWorld>(File.ReadAllBytes(Path.Combine(root,"ready.json")));
            var runtime=new NewGameRuntime();runtime.ReviewConfigureCaptives(w);AddChild(runtime);
            for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var captive=runtime.Session.Captives().Single();var name=w.Definitions.Characters.Single(c=>c.Id==captive.Person).Name;
            runtime.ReviewCaptivesOpen();for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(!runtime.ReviewCaptivesText().Contains(name)||!runtime.ReviewCaptivesText().Contains("留置"))throw new InvalidDataException("捕虜の表示がありません");
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            if(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,"captives.png"))!=Error.Ok)throw new IOException("描画の保存に失敗しました");
            runtime.ReviewCaptivesPress("身代金");for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(runtime.Session.Ransoms().Single().Stage!="返事待ち"||!runtime.ReviewCaptivesText().Contains("身代金 500文　返事待ち"))throw new InvalidDataException("身代金の表示が一致しません");
            runtime.ReviewCaptivesPress("登用");for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var entry=runtime.Session.DiagnosticSnapshot().Captives!.Entries.Last();if(entry.Input.Action!=CaptiveAction.Recruit)throw new InvalidDataException("登用が記録されません");
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            if(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,"after.png"))!=Error.Ok)throw new IOException("描画の保存に失敗しました");
            // 本人が捕らえられた世界：画面に捕らわれの表示と願いの欄があり、500文で願うと解放される。
            var held=ContractJson.Read<ContractWorld>(File.ReadAllBytes(Path.Combine(root,"held.json")));
            var second=new NewGameRuntime();second.ReviewConfigureCaptives(held);runtime.QueueFree();AddChild(second);
            for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            second.ReviewCaptivesOpen();for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(!second.ReviewCaptivesText().Contains("捕らわれ"))throw new InvalidDataException("捕らわれの表示がありません");
            if(!second.ReviewStatusText().Contains("捕らわれ・"))throw new InvalidDataException("上の欄に捕らわれの所在がありません");
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            if(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,"held.png"))!=Error.Ok)throw new IOException("描画の保存に失敗しました");
            second.ReviewCaptivesPress("解放を願う");for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(second.Session.MyCaptivity()!=null||second.ReviewCaptivesText().Contains("捕らわれ"))throw new InvalidDataException("願いで解放されません");
            File.WriteAllText(Path.Combine(root,"ui-result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Recruited=entry.Accepted,Scope="捕虜の一覧→身代金の書状→登用の誘い、本人の捕らわれ→解放の願い（ゲーム内の入力）",ComputerUse="未使用"}));
            GetTree().Quit(0);
        }
        catch(Exception e)
        {
            File.WriteAllText(Path.Combine(root,"ui-result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="FAILED",Error=e.ToString()}));GetTree().Quit(1);
        }
    }
}
