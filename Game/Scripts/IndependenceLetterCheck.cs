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
            var allied=System.Environment.GetEnvironmentVariable("HAKUTAI_ALLIED_GATE_FIXTURE");
            var file=allied??System.Environment.GetEnvironmentVariable("HAKUTAI_LETTER_FIXTURE")??throw new InvalidDataException("支持要請の入力がありません");
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
            if(!sent.Couriers!.Missions.Any(m=>m.Destination==(allied==null?"fork":"castle") && sent.Messaging.Attempts.Any(a=>a.MessageId==letter.Id && a.Id==m.Attempt)))
                throw new InvalidDataException("支持要請の使者が既知の城へ出発していません");
            game.ReviewSocialClose();
            async Task SaveLoad()
            {
                game.Session.SetPaused(true);game.ReviewFraction(0);
                var expected=ContractJson.Write(game.Session.DiagnosticSnapshot());var pose=game.Pose();
                game.Press("save");var timer=System.Diagnostics.Stopwatch.StartNew();
                while(game.IsSaving){if(timer.Elapsed.TotalSeconds>120)throw new TimeoutException("書状の保存");await Frame();}
                game.Press("load");await Frame();game.ReviewFraction(0);
                if(!expected.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())) || pose!=game.Pose())
                    throw new InvalidDataException("配送中の復旧で世界または3D位置が変化しました");
            }
            void Advance(long at)
            {
                while(game.Session.Seconds<at){
                    var before=game.Session.Seconds;game.Session.SetPaused(false);game.Session.AdvanceTo(at);
                    if(game.Session.Seconds<=before)throw new InvalidDataException("使者の移動が停止");
                }
                game.Session.SetPaused(true);
            }
            bool Visible(string value)=>game.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.IsVisibleInTree() && l.Text==value);
            long Arrival(ContractWorld state,string id)=>state.Couriers!.Missions.Single(m=>state.Messaging.Attempts.Any(a=>a.MessageId==id && a.Id==m.Attempt)).ReadyAt+1;
            await SaveLoad();
            Advance(Arrival(sent,letter.Id));
            if(allied!=null){
                var recipient=request.Person;
                for(int i=0;i<5;i++){
                    var snapshot=game.Session.DiagnosticSnapshot();
                    if(snapshot.Court!.Events.Any(e=>e.Input.Action==CourtAction.ReceiveIndependentGate && e.Actor==recipient))break;
                    Advance(game.Session.Seconds+1);
                }
                var gate=game.Session.DiagnosticSnapshot();
                if(!gate.Court!.Events.Any(e=>e.Input.Action==CourtAction.ReceiveIndependentGate && e.Actor==recipient) ||
                    JointIndependence.ObserveOwnCastle(gate,recipient) is not {ControlsCastle:false})
                    throw new InvalidDataException("城門受領時の旧主家の支配が不一致");
                await SaveLoad();
                Advance(game.Session.Seconds+1);
                var keep=game.Session.DiagnosticSnapshot();
                if(JointIndependence.ObserveOwnCastle(keep,recipient) is not {ControlsCastle:true} ||
                    JointIndependence.ObserveOwnCastle(keep,w.PlayerControl)?.Castle==request.Site)
                    throw new InvalidDataException("現地の本丸受領と遠方の観測範囲が不一致");
            }
            var returning=game.Session.DiagnosticSnapshot();
            var reply=returning.Messaging.Messages.Single(m=>m.Kind==IndependenceLetters.Reply);
            await SaveLoad();
            game.Press("information-schemes");await Frame();
            if(Visible(reply.Body) || IndependenceLetters.Delivered(game.Session.DiagnosticSnapshot(),w.PlayerControl).Any(l=>l.Id==reply.Id))
                throw new InvalidDataException("返書の到着前に回答が判明");
            game.ReviewSocialClose();Advance(Arrival(returning,reply.Id));
            game.Press("information-schemes");await Frame();
            if(Visible(reply.Body))throw new InvalidDataException("開封前に返書本文を表示");
            game.ReviewSchemePress(reply.Id);await Frame();
            if(!Visible(reply.Body))throw new InvalidDataException("開封後に返書本文が表示されません");
            GD.Print(allied==null?"INDEPENDENCE_LETTER_UI_PASS":"ALLIED_GATE_UI_PASS");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
