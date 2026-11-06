using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HakutaiKagami.Core.Contracts;

public partial class IndependenceLetterCheck : Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    public override async void _Ready()
    {
        try{
            var file=System.Environment.GetEnvironmentVariable("HAKUTAI_LETTER_FIXTURE")??throw new InvalidDataException("支持要請の入力がありません");
            var w=ContractJson.Read<ContractWorld>(File.ReadAllBytes(file));
            var initial=w.NewGame!;
            var store=new NewCampaignStore(Directory.CreateTempSubdirectory("hakutai-gate-ui-").FullName);
            var profile=store.New(initial.Start,initial.Registered,initial.Selected.Value,initial.Seed,
                inspectionPolicy:initial.InspectionPolicy,workPolicy:initial.WorkPolicy,companionPolicy:initial.CompanionPolicy,
                developmentPolicy:initial.DevelopmentPolicy,socialPolicy:initial.SocialPolicy,comingOfAge:initial.AdultAge);
            var start=new WorldSession(w);start.SetPaused(true);
            if(!store.Saves(profile).Save(start,"manual",w.WorldId).Published)throw new IOException("受領前保存に失敗");
            var game=new NewGameRuntime();game.Configure(store,profile,true);AddChild(game);await Frame();
            var request=IndependenceLetters.Choices(w,w.PlayerControl).Single(i=>i.Action==CourtAction.SeekDeclaredSupport && i.Person.Value=="created:castle-contact");
            game.Press("information-schemes");await Frame();
            game.ReviewSchemePress(request.Id.Value);await Frame();
            var sent=game.Session.DiagnosticSnapshot();
            var letter=sent.Messaging.Messages.Single(m=>m.Kind==IndependenceLetters.Kind);
            if(!sent.Couriers!.Missions.Any(m=>m.Destination=="fork" && sent.Messaging.Attempts.Any(a=>a.MessageId==letter.Id && a.Id==m.Attempt)))
                throw new InvalidDataException("支持要請の使者が既知の城へ出発していません");
            game.ReviewSocialClose();game.ReviewFraction(0);
            var expected=ContractJson.Write(game.Session.DiagnosticSnapshot());var pose=game.Pose();
            game.Press("save");var timer=System.Diagnostics.Stopwatch.StartNew();
            while(game.IsSaving){if(timer.Elapsed.TotalSeconds>120)throw new TimeoutException("支持要請の保存");await Frame();}
            game.Press("load");await Frame();game.ReviewFraction(0);
            if(!expected.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())) || pose!=game.Pose())
                throw new InvalidDataException("支持要請の配送中に世界または3D位置が変化しました");
            GD.Print("INDEPENDENCE_LETTER_UI_PASS");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
