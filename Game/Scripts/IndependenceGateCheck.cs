using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HakutaiKagami.Core.Contracts;

public partial class IndependenceGateCheck : Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    public override async void _Ready()
    {
        try{
            var file=System.Environment.GetEnvironmentVariable("HAKUTAI_INDEPENDENCE_UI_FIXTURE")??throw new InvalidDataException("現地立会いの入力がありません");
            var w=ContractJson.Read<ContractWorld>(File.ReadAllBytes(file));
            var initial=w.NewGame!;
            var store=new NewCampaignStore(Directory.CreateTempSubdirectory("hakutai-gate-ui-").FullName);
            var profile=store.New(initial.Start,initial.Registered,initial.Selected.Value,initial.Seed,
                inspectionPolicy:initial.InspectionPolicy,workPolicy:initial.WorkPolicy,companionPolicy:initial.CompanionPolicy,
                developmentPolicy:initial.DevelopmentPolicy,socialPolicy:initial.SocialPolicy,comingOfAge:initial.AdultAge);
            var start=new WorldSession(w);start.SetPaused(true);
            if(!store.Saves(profile).Save(start,"manual",w.WorldId).Published)throw new IOException("受領前保存に失敗");
            var game=new NewGameRuntime();game.Configure(store,profile,true);AddChild(game);await Frame();
            var keep=IndependenceGate.Available(w,w.PlayerControl).Single(i=>i.Action==CourtAction.ReceiveIndependentKeep);
            game.Press("information-schemes");await Frame();
            game.ReviewSchemePress(keep.Id.Value);await Frame();
            var guard=JointIndependence.ObserveOwnCastle(game.Session.DiagnosticSnapshot(),w.PlayerControl);
            if(guard is not {ControlsCastle:true,FormerTroopsPresent:false})throw new InvalidDataException("本丸の受領結果が不一致");
            if(!game.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.IsVisibleInTree() && l.Text=="この城の支配は自家に移りました。"))
                throw new InvalidDataException("支配移転の現地表示がありません");
            game.ReviewSocialClose();game.ReviewFraction(0);
            var expected=ContractJson.Write(game.Session.DiagnosticSnapshot());var pose=game.Pose();
            game.Press("save");var timer=System.Diagnostics.Stopwatch.StartNew();
            while(game.IsSaving){if(timer.Elapsed.TotalSeconds>120)throw new TimeoutException("本丸受領の保存");await Frame();}
            game.Press("load");await Frame();game.ReviewFraction(0);
            if(!expected.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())) || pose!=game.Pose())
                throw new InvalidDataException("本丸の受領後に世界または3D位置が変化しました");
            GD.Print("INDEPENDENCE_GATE_UI_PASS");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
