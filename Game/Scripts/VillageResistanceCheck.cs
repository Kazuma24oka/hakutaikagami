using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class VillageResistanceCheck : Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public override async void _Ready()
    {
        try
        {
            var fixture=System.Environment.GetEnvironmentVariable("HAKUTAI_VILLAGE_RESISTANCE_FIXTURE")??throw new InvalidDataException("村の実戦保存を指定してください");
            var w=ContractJson.Read<ContractWorld>(File.ReadAllBytes(fixture));
            var start=w.NewGame!;
            var store=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"village-ui-"+Guid.NewGuid().ToString("N")));
            var profile=store.New(start.Start,start.Registered,start.Selected.Value,start.Seed,inspectionPolicy:LocalInspectionFindings.Policy,
                workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy,worldArmyPolicy:NpcArmies.CompactCombatPolicy);
            var session=new WorldSession(w);session.SetPaused(true);
            Check(store.Saves(profile).Save(session,"manual",w.WorldId).Published,"初期保存");
            var game=new NewGameRuntime();game.Configure(store,profile,true);AddChild(game);await Frame();
            PopupPanel Panel()=>game.GetChildren().OfType<PopupPanel>().Single(p=>p.Visible);
            async Task Open(){game.Press("village");await Frame();}
            bool Shows(string label)=>Panel().FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains(label));
            await Open();Check(Shows("蜂起に加わる"),"村代表の蜂起回答");
            var decision=Panel().FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text.Contains("鎮圧する"));
            decision.EmitSignal(Button.SignalName.Pressed);await Frame();
            Check(Shows("不信"),"鎮圧後の村代表");Panel().Hide();await Frame();
            var pose=game.Pose();game.SaveCheckpoint("aftermath");game.LoadCheckpoint("aftermath");
            Check(game.Pose()==pose,"復旧後の配置");await Open();Check(Shows("不信"),"復旧後の相談");
            Check(!Panel().FindChildren("*","Button",true,false).OfType<Button>().Any(b=>b.Text.Contains("鎮圧する")),"重複鎮圧");
            GD.Print("VILLAGE_RESISTANCE_UI_PASS");GetTree().Quit();
        }
        catch(Exception ex){GD.PrintErr(ex);GetTree().Quit(1);}
    }
}
