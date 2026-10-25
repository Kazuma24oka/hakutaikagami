using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

// An explicit, data-only integration screen. Does not replace the ordinary 3D game entry.
public partial class CommonContractTrial : Control
{
    private WorldSessionHost host=null!;
    private GenerationSaveStore store=null!;
    private WorldSession Session=>host.Current;
    private EntityId observer=ContractFixture.Officer;
    private Label status=null!,notice=null!,details=null!;
    private OptionButton perspective=null!;
    private bool failed;
    public Dictionary<string,Button> Actions {get;}=new();
    private readonly List<string> checks=new();
    private bool IsTest=>OS.GetCmdlineUserArgs().Contains("--common-contract-check");
    private bool CaptureEnabled=>OS.GetCmdlineUserArgs().Contains("--common-contract-capture");
    private string Output=>ProjectSettings.GlobalizePath("res://../Docs/Design/CommonContractV1/runtime-local/godot");
    public override void _Ready()
    {
        host=new(new(ContractFixture.Create()));
        Theme=new Theme{DefaultFontSize=19,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
        var slotRoot=IsTest?"user://common-contract-v1-test-"+Guid.NewGuid().ToString("N"):"user://common-contract-v1-manual";
        store=new(ProjectSettings.GlobalizePath(slotRoot),ContractFixture.Create().Definitions);
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach(var edge in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+edge,24);
        var column=new VBoxContainer();column.AddThemeConstantOverride("separation",14);margin.AddChild(column);
        var title=new Label{Text="百代鏡 / 共通Core・保存復旧の検証"};title.AddThemeFontSizeOverride("font_size",27);column.AddChild(title);
        column.AddChild(new Label{Text="秒基準の架空地域・検証入力。通常ゲーム、全国地理、製品の任務画面とは別の入口です。",AutowrapMode=TextServer.AutowrapMode.WordSmart,Modulate=new Color("dbc590")});
        var bar=new HFlowContainer();column.AddChild(bar);
        Add(bar,"reset","初期化",()=>{host=new(new(ContractFixture.Create()));notice.Text="新しい検証セッションです。通常保存は変更していません。";});
        Add(bar,"dispatch","① 重要任務を発送",()=>Result(Session.Submit(ContractFixture.Order("request:ui-prepare",important:true),Session.Revision)));
        Add(bar,"advance","10秒進める",()=>{var r=Session.AdvanceTo(checked(Session.Seconds+10));notice.Text=$"{r.ReachedAt}秒まで進行 / {r.StopReason}";});
        Add(bar,"open","② 奉行が開封",()=>{var letter=Session.GetActorView(ContractFixture.Officer).Messages.FirstOrDefault(m=>m.Sealed);if(letter==null){notice.Text="未開封の手元の書状はありません。";return;}Result(Session.Open(ContractFixture.Officer,letter.Id,Session.Revision));});
        Add(bar,"pause","停止 / 再開",()=>Session.SetPaused(!Session.Paused));
        Add(bar,"parallel","③ 本人移動＋書役へ委任",()=>{
            Result(Session.Submit(ContractFixture.Order("request:ui-delegate",ActionKind.Transfer,40,executor:ContractFixture.Clerk),Session.Revision));
            Result(Session.Submit(ContractFixture.Order("request:ui-move",ActionKind.Move),Session.Revision));
        });
        Add(bar,"save","世代保存",()=>{var r=store.Save(Session,"manual",new("world:common-fixture"));notice.Text=r.Published?"保存済み: "+r.GenerationId:"保存しませんでした。このスロットには新しい版があります。";});
        Add(bar,"load","保存から復旧",()=>{var candidate=store.Prepare("manual");var current=Session;var result=host.CommitLoad(candidate,current,current.Revision);notice.Text="復旧: "+result+" / 全体停止で再開";});
        perspective=new OptionButton();bar.AddChild(perspective);perspective.AddItem("奉行の認識");perspective.AddItem("城主の認識");perspective.AddItem("書役の認識");
        perspective.ItemSelected+=i=>{observer=i==0?ContractFixture.Officer:i==1?ContractFixture.Lord:ContractFixture.Clerk;Refresh();};
        status=new Label();column.AddChild(status);notice=new Label{Text="①発送 → 時刻を進める → 到達で停止 → ②開封 → 再開。本人と主君の認識を切り替えて確認できます。",AutowrapMode=TextServer.AutowrapMode.WordSmart};column.AddChild(notice);
        var scroll=new ScrollContainer{SizeFlagsVertical=SizeFlags.ExpandFill};column.AddChild(scroll);
        details=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=SizeFlags.ExpandFill};details.AddThemeFontSizeOverride("font_size",17);scroll.AddChild(details);
        Refresh();if(IsTest)CallDeferred(MethodName.RunChecks);
    }
    private void Add(Container parent,string id,string text,Action action)
    {
        var b=new Button{Text=text};parent.AddChild(b);Actions.Add(id,b);
        b.Pressed+=()=>{try{action();Refresh();}catch(Exception e){failed=true;notice.Text="検証操作を実行できませんでした。";GD.PrintErr(e.ToString());}};
    }
    private void Result(CommandResult r)=>notice.Text=r.VisibleReason+" / "+r.Code;
    private void Refresh()
    {
        // Ordinary screen data comes only from the observer's filtered read model.
        var view=Session.GetActorView(observer);
        var selected=observer==ContractFixture.Officer?0:observer==ContractFixture.Lord?1:2;
        if(perspective.Selected!=selected)perspective.Select(selected);
        status.Text=$"世界時刻 {view.ElapsedSeconds} 秒 / {(Session.Paused?"停止中":"進行可能")} / 版 {view.Revision}";
        var location=view.Location.Kind==LocationKind.AtSite?view.Location.Site?.Value:$"経路途中 {view.Location.ProgressMm}mm（検証距離）";
        details.Text=$"【{observer.Value}の認識】\n所在: {location}\n\n【認識している任務】\n"+
            string.Join("\n",view.Tasks.Select(t=>$"{t.Id}: {t.KnownStatus}"))+"\n\n【手元に届いた書状】\n"+
            string.Join("\n",view.Messages.Select(m=>$"{m.Id}: {(m.Sealed?"未開封・本文非表示":m.KnownBody)}"))+"\n\n【知識の出典】\n"+
            string.Join("\n",view.Evidence.Select(e=>$"{e.RecordedAt}秒 / {e.Claim}"));
    }
    private void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);checks.Add(reason);}
    private void Click(string id)=>Actions[id].EmitSignal(Button.SignalName.Pressed);
    private async Task Capture(string name)
    {
        if(!CaptureEnabled)return;
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        System.IO.Directory.CreateDirectory(Output);
        if(GetViewport().GetTexture().GetImage().SavePng(Output+"/"+name+".png")!=Error.Ok)throw new System.IO.IOException("GPU capture failed.");
    }
    private async void RunChecks()
    {
        try{
            Click("dispatch");Check(!failed&&Session.Seconds==0,"dispatch does not advance time");
            Click("advance");Check(Session.Seconds==2&&Session.Paused,"important arrival pauses at its boundary");
            Check(Session.GetActorView(ContractFixture.Officer).Messages.Single().KnownBody==null,"sealed body hidden in actual view model");
            await Capture("sealed");
            Click("open");Click("pause");Session.AdvanceTo(8);Refresh();
            observer=ContractFixture.Lord;Refresh();Check(details.Text.Contains("任務完了"),"lord screen learns report only after arrival");
            Click("parallel");Session.AdvanceTo(12);Refresh();Click("save");
            var before=Session.GetActorView(ContractFixture.Officer);Check(before.Location.Kind==LocationKind.Traveling,"officer screen has intermediate route progress");
            observer=ContractFixture.Officer;Refresh();await Capture("in-progress");
            Click("advance");Click("load");
            Check(Session.Seconds==12&&Session.Paused,"UI restores saved revision paused without legacy clock conversion");
            Check(Session.GetActorView(ContractFixture.Officer).Location.ProgressMm==before.Location.ProgressMm,"UI restore retains physical progress");
            Refresh();await Capture("restored");
            Click("pause");Session.AdvanceTo(16);observer=ContractFixture.Lord;Refresh();
            Check(Session.GetActorView(observer).Tasks.Count(t=>t.KnownStatus=="任務完了")==3,"three reports visible to lord after loop");
            Check(!failed,"actual button callbacks completed without exceptions");
            await Capture("complete");
            System.IO.Directory.CreateDirectory(Output);
            System.IO.File.WriteAllText(Output+"/ui-checks.json",System.Text.Json.JsonSerializer.Serialize(new{status="PASSED",checks,screen=details.Text,seconds=Session.Seconds},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            GD.Print("COMMON_CONTRACT_UI_COMPLETE "+checks.Count);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e.ToString());GetTree().Quit(1);}
    }
}
