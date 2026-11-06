using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class SubmissionDemandCheck : Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public override async void _Ready()
    {
        try
        {
            var fixture=System.Environment.GetEnvironmentVariable("HAKUTAI_SUBMISSION_FIXTURE")??throw new InvalidDataException("外交試験用の保存を指定してください");
            var w=ContractJson.Read<ContractWorld>(File.ReadAllBytes(fixture));
            var start=w.NewGame!;
            var store=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"submission-ui-"+Guid.NewGuid().ToString("N")));
            var profile=store.New(start.Start,start.Registered,start.Selected.Value,start.Seed,trucePolicy:TruceNegotiations.Policy,worldArmyPolicy:NpcArmies.CompactCombatPolicy);
            var session=new WorldSession(w);session.SetPaused(true);
            Check(store.Saves(profile).Save(session,"manual",w.WorldId).Published,"初期保存");
            var game=new NewGameRuntime();game.Configure(store,profile,true);AddChild(game);await Frame();
            PopupPanel Panel()=>game.GetChildren().OfType<PopupPanel>().Single(p=>p.Visible);
            Button Find(string text)=>Panel().FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==text);
            game.Press("truce-negotiation");await Frame();
            Find("独立条項を選んで正式条約を交渉").EmitSignal(Button.SignalName.Pressed);await Frame();
            Find("従属を勧告する").ButtonPressed=true;await Frame();
            var send=Find("この条件で従属を勧告する");Check(send.IsVisibleInTree()&&!send.Disabled,"勧告の発送ボタン");
            send.EmitSignal(Button.SignalName.Pressed);await Frame();
            var offer=game.Session.DiagnosticSnapshot().Treaties!.Papers.Single();
            Check(offer.Terms.Submission is {GuaranteeLands:true,MusterMen:100},"本領と軍役の原本");
            Check(game.Session.TreatyView().Single().Stage.Contains("返答待ち"),"開封前の表示");
            var pose=game.Pose();game.SaveCheckpoint("inflight");game.LoadCheckpoint("inflight");
            Check(game.Pose()==pose && game.Session.DiagnosticSnapshot().Treaties!.Papers.Single()==offer,"配送中の復旧");
            GD.Print("SUBMISSION_DEMAND_UI_PASS");GetTree().Quit();
        }
        catch(Exception ex){GD.PrintErr(ex);GetTree().Quit(1);}
    }
}
