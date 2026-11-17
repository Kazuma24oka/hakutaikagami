using Godot;
using System;
using System.IO;
using System.Linq;
using HakutaiKagami.Core.Contracts;

// S11 の画面の一巡：帳面の各面をゲームの中の入力で開いて押し、描画を画像に書き出す。会話の差し替え口とキーでの送りも確かめる。
public partial class NewGameRuntime
{
    internal void ReviewConfigureScreens(ContractWorld world)
    {
        campaigns=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"screens2-ui-"+Guid.NewGuid().ToString("N")));
        Profile=NationalStart.New(campaigns,world.NewGame!.Start,world.PlayerControl.Value,world.Seed,null,true);
        host=new WorldSessionHost(new WorldSession(world));saves=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"screens2-ui-saves-"+Guid.NewGuid().ToString("N")),world.Definitions);loading=false;
    }
}
public partial class Screens2Check:Control
{
    public override async void _Ready()
    {
        var root=RecordPath.Globalize("res://../Docs/Design/ScreensV1/runtime-local");
        try
        {
            var w=ContractJson.Read<ContractWorld>(File.ReadAllBytes(Path.Combine(root,"ready.json")));
            var runtime=new NewGameRuntime();runtime.ReviewConfigureScreens(w);AddChild(runtime);
            async System.Threading.Tasks.Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            async System.Threading.Tasks.Task Shot(string name){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);if(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,name+".png"))!=Error.Ok)throw new IOException("描画の保存に失敗しました");}
            void Need(bool ok,string what){if(!ok)throw new InvalidDataException(what+"\n"+runtime.ReviewLedgerText());}
            await Frames(8);
            if(!runtime.ButtonEnabled("ledger"))throw new InvalidDataException("帳面のボタンがありません");
            runtime.Press("ledger");await Frames(4);
            string[] tabs=["書状","人物","判断","約束","家督","軍勢","表示"];
            for(int n=0;n<tabs.Length;n++){runtime.ShowLedger(n);await Frames(3);Need(runtime.ReviewLedgerTab==n,tabs[n]+"の面が開きません");await Shot("ledger-"+n);}
            runtime.ShowLedger(0);await Frames(2);Need(runtime.ReviewLedgerText().Contains("出陣の上申")==runtime.Session.Letters().Any(l=>l.Kind=="出陣の上申"),"書状の帳面（道中の書状は出さない）");
            runtime.ShowLedger(1);await Frames(2);Need(runtime.ReviewLedgerText().Contains("軍事")&&runtime.ReviewLedgerText().Contains("教養・育成"),"人物帳の6分野");
            runtime.ShowLedger(2);await Frames(2);Need(!runtime.ReviewLedgerText().Contains("出陣の機会"),"届いていない機会を判断に出さない");
            runtime.ShowLedger(3);await Frames(2);Need(runtime.ReviewLedgerText().Contains("家の約束")&&runtime.ReviewLedgerText().Contains("私の約束"),"約束の二つの欄");
            runtime.ShowLedger(4);await Frames(2);Need(runtime.ReviewLedgerText().Contains("当主"),"家督の帳面");
            runtime.ShowLedger(5);await Frames(2);runtime.ReviewLedgerPress("出陣を命じる");await Frames(3);
            Need(runtime.Session.HouseArmies().Any(a=>a.State=="命じた"),"軍団への出陣の命");
            runtime.ShowLedger(5);await Frames(2);Need(runtime.ReviewLedgerText().Contains("命じた"),"軍勢の帳面");await Shot("ledger-ordered");
            // 会話：差し替え口（絵が無ければ仮の絵）とキーでの送り・閉じ。
            var other=runtime.Session.CorpsCommanders().First();
            runtime.ReviewPresent(other);await Frames(3);Need(runtime.ReviewTalkOpen&&runtime.StagePortrait(1)=="仮","会話の差し替え口");await Shot("talk");
            runtime.ReviewTalkKey(Key.Escape);await Frames(2);Need(!runtime.ReviewTalkOpen,"Esc で会話を閉じる");
            File.WriteAllText(Path.Combine(root,"ui-result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="帳面の7面を開いて描画、届いていない史実の機会を出さない、軍団に出陣を命じる、会話の差し替え口とEscで閉じる（ゲーム内の入力）",ComputerUse="不使用"}));
            GD.Print("SCREENS2_UI_PASS");GetTree().Quit(0);
        }
        catch(Exception e)
        {
            Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"ui-result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="FAILED",Error=e.ToString()}));GD.PrintErr(e.ToString());GetTree().Quit(1);
        }
    }
}
