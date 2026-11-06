using Godot;
using HakutaiKagami.Core.Contracts;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class CoalitionCheck : Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    async Task Saved(NewGameRuntime game)
    {
        var timer=Stopwatch.StartNew();
        while(game.IsSaving){if(timer.Elapsed.TotalSeconds>120)throw new TimeoutException("盟約の保存");await Frame();}
    }
    static void Require(bool condition,string reason){if(!condition)throw new InvalidDataException(reason);}
    public override async void _Ready()
    {
        try{
            var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
            var founder=raw.Factions.First(f=>raw.People.Any(p=>p.Id==f.Head && p.Site==f.Home));
            var store=new NewCampaignStore(Directory.CreateTempSubdirectory("hakutai-coalition-ui-").FullName);
            var profile=store.New(raw,[],founder.Head,42,historicalCatalog:HistoricalStartCatalog.Current,
                inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,
                worldArmyPolicy:NpcArmies.CompactCombatPolicy,trucePolicy:TruceNegotiations.Policy);
            var game=new NewGameRuntime();game.Configure(store,profile,false);AddChild(game);
            game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Saved(game);
            async Task<VBoxContainer> Open(){
                game.ShowTreatyNegotiation();await Frame();
                var popup=game.GetChildren().OfType<PopupPanel>().Single(p=>p.Visible);
                var toggle=popup.FindChildren("*","CheckButton",true,false).OfType<CheckButton>().Single(b=>b.Text=="包囲網の書状");
                toggle.ButtonPressed=true;await Frame();
                return (VBoxContainer)toggle.GetParent().GetChild(toggle.GetIndex()+1);
            }
            var panel=await Open();var options=panel.GetChildren().OfType<OptionButton>().ToArray();
            options[0].Select(1);options[0].EmitSignal(OptionButton.SignalName.ItemSelected,1);options[1].Select((int)CoalitionPurpose.Both);
            var recipients=panel.GetChildren().OfType<CheckBox>().Where(b=>b.Visible).Take(2).ToArray();
            Require(recipients.Length==2,"招請先の選択");foreach(var b in recipients)b.ButtonPressed=true;
            var names=recipients.Select(b=>b.Text).ToArray();var enemy=options[0].GetItemText(1);
            panel.GetChildren().OfType<Button>().Single(b=>b.Text=="選んだ各家に招請状を発送").EmitSignal(BaseButton.SignalName.Pressed);
            await Frame();
            var snapshot=game.Session.DiagnosticSnapshot();
            var invite=snapshot.Court!.Events.Single(e=>e.Input.Action==CourtAction.ProposeCoalition).Input.Coalition!;
            string NameOf(EntityId id)=>snapshot.Definitions.Organizations.Single(o=>o.Id==id).Name;
            Require(NameOf(invite.Target)==enemy && names.SequenceEqual(invite.Invitees.Select(NameOf)) && invite.Purpose==CoalitionPurpose.Both,"画面で選んだ条件と原本");
            Require(snapshot.Messaging.Messages.Count(m=>m.Kind==CoalitionPacts.Invitation)==2,"二家へ個別に発送");
            foreach(var popup in game.GetChildren().OfType<PopupPanel>().Where(p=>p.Visible))popup.Hide();
            game.ReviewFraction(0);var pose=game.Pose();game.Press("save");await Saved(game);game.Press("load");await Frame();game.ReviewFraction(0);
            Require(ContractJson.Write(snapshot).SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())) && pose==game.Pose(),"発送後の保存・3D配置");
            panel=await Open();
            Require(panel.GetChildren().OfType<Label>().Any(l=>l.Text.Contains("発送先：") && names.All(n=>l.Text.Contains(n)) && l.Text.Contains(enemy)),"復旧後の外交画面に原本の対象・宛先を表示");
            GD.Print("COALITION_UI_PASS");GetTree().Quit();
        }catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}