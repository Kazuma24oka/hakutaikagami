using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

// 画面の検査（2026-11-12）：操作する人物が死ぬと死去の画面が出て、知っていた成人の子で続けられる。保存から開き直しても同じ人物で続く。
public partial class ControlSuccessionCheck:Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public override async void _Ready()
    {
        try
        {
            var front=new NewGameFrontEnd();AddChild(front);await Frame();
            var start=front.CreateSecurityReview(Path.Combine(Path.GetTempPath(),"succession-ui-"+Guid.NewGuid().ToString("N")),true);
            front.QueueFree();await Frame();
            // 高齢の親（操作する人物）と、成人の子の二人を登録して始める。
            RegisteredOfficer Officer(string id,int born,string? parent)=>new(id,null,id=="created:elder"?"鏡山 宗玄":"鏡山 宗久",born,new(60,50,70,40),StartTemplate.Ronin,"site:castle",null,null,HakutaiKagami.Core.Appearance.AppearanceCatalog.Default(1)){BirthParents=parent==null?null:[parent]};
            var records=NewGameWorld.Normalize([Officer("created:elder",1430,null),Officer("created:test",1530,"created:elder")]);
            var raw=NewGameWorld.Scenario();var request=new WorldSetupRequest(records,[]);var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
            var child=setup.People.Single(p=>p.Id=="created:test");
            var profile=start.Store.New(setup,records,"created:elder",0,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,comingOfAge:15,controlPolicy:ControlSuccession.Policy);
            var s=new WorldSession(NewCampaignStore.Build(profile));
            while(!ControlSuccession.Awaiting(s.DiagnosticSnapshot())&&s.Seconds<366L*86400){s.SetPaused(false);s.AdvanceTo(Math.Min(s.Seconds+30L*86400,366L*86400));}
            var dead=s.DiagnosticSnapshot();Check(ControlSuccession.Awaiting(dead),"一年のうちに死去しません");
            Check(ControlSuccession.Candidates(dead).Contains(new EntityId(child.Id)),"子が候補にいません");
            start.Store.Saves(profile).Save(s,"manual",dead.WorldId);
            var game=new NewGameRuntime();game.Configure(start.Store,profile,true);AddChild(game);await Frame();game.Refresh();await Frame();
            var popup=game.GetChildren().OfType<PopupPanel>().SingleOrDefault(p=>p.Visible);Check(popup!=null,"死去の画面が出ません");
            var labels=popup!.FindChildren("*","Label",true,false).OfType<Label>().Select(l=>l.Text).ToArray();
            Check(labels.Any(t=>t.EndsWith("死去")),"死去の表示がありません");
            var output=RecordPath.Globalize("res://../Docs/Design/ControlSuccessionV1/runtime-local/ui");Directory.CreateDirectory(output);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Check(ViewportCapture.Save(popup.GetViewport(),Path.Combine(output,"death.png"))==Error.Ok,"死去の画面の描画");
            var name=dead.Definitions.Characters.Single(c=>c.Id.Value==child.Id).Name;
            popup.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==name+"で続ける").EmitSignal(Button.SignalName.Pressed);
            await Frame();
            Check(game.Session.PlayerControl==new EntityId(child.Id),"子へ引き継げません");
            game.SaveCheckpoint("succession");game.LoadCheckpoint("succession");await Frame();
            Check(game.Session.PlayerControl==new EntityId(child.Id),"開き直すと引き継ぎが失われます");
            Check(!game.GetChildren().OfType<PopupPanel>().Any(p=>p.Visible&&p.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.EndsWith("死去"))),"引き継いだ後も死去の画面が残っています");
            File.WriteAllText(Path.Combine(output,"result.json"),"{\"Status\":\"PASSED\"}");
            GD.Print("CONTROL_SUCCESSION_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();
        }
        catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}
