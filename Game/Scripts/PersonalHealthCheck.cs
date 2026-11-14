using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
// 通常開始から、賊との戦いで負傷し、暮らしの画面で状態を見て手当し、保存復旧後に回復する一巡。
public partial class PersonalHealthCheck:Node
{
 async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 async Task Until(Func<bool> done,string label){var t=Stopwatch.StartNew();while(!done()){if(t.Elapsed.TotalSeconds>300)throw new TimeoutException(label);await Frame();}}
 static void Check(bool condition,string text){if(!condition)throw new InvalidDataException(text);}
 async Task Capture(string output,string name){if(DisplayServer.GetName()=="headless")return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,name))==Error.Ok,"自前描画 "+name);}
 public override async void _Ready(){try{
  string output=RecordPath.Globalize("res://../Docs/Design/PersonalHealthV1/runtime-local/ui");Directory.CreateDirectory(output);
  var entry=new NewGameFrontEnd();AddChild(entry);await Frame();var start=entry.CreateBanditReview(Path.Combine(Path.GetTempPath(),"health-ui-"+Guid.NewGuid().ToString("N")));entry.QueueFree();await Frame();
  Check(start.Profile.Judgment?.HealthEdition==PersonalHealth.Edition,"新規開始に健康の版が付く");
  var game=new NewGameRuntime();game.Configure(start.Store,start.Profile,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Until(()=>!game.IsSaving,"開始保存");
  var me=game.Session.PlayerControl;Check(game.Session.HealthView(me) is {Own:true} v0&&v0.Conditions.Length==0,"開始時は傷病なし");
  game.Press("bandit-encounter");await Frame();game.ReviewBanditPress("encounter");await Frame();game.ReviewBanditPress("fight");await Frame();game.ReviewSocialClose();game.Advance(40);game.ReviewFraction(0);
  var w=game.Session.DiagnosticSnapshot();Check(BanditEncounter.Current(w)==null,"接触戦の終了");
  var injury=PersonalHealth.Active(w,me,w.ElapsedSeconds).FirstOrDefault(c=>c.Kind==HealthKind.Injury);Check(injury!=null,"接触戦の負傷が傷病になる");
  game.ReviewPersonalLifeOpen();await Frame();var text=game.ReviewPersonalLifeText();Check(text.Contains("体調：負傷")&&text.Contains("見立てでは"),"暮らしの画面に本人の状態と見立て");
  await Capture(output,"injured.png");
  long before=PersonalHealth.RecoverAt(w,injury!);game.ReviewHealthPress("self:"+injury!.Id.Value);await Frame();
  w=game.Session.DiagnosticSnapshot();long after=PersonalHealth.RecoverAt(w,injury);Check(after<before&&after>w.ElapsedSeconds,"手当で経過が縮み、即時には治らない");
  Check(game.Session.HealthView(me)!.Conditions.Single(c=>c.Id==injury.Id).Treated,"手当済みの表示");
  game.ReviewPersonalLifeOpen();await Frame();await Capture(output,"treated.png");game.ReviewPersonalLifeClose();
  game.Press("save");await Until(()=>!game.IsSaving,"手当後の保存");var bytes=ContractJson.Write(game.Session.DiagnosticSnapshot());game.Press("load");await Frame();game.ReviewFraction(0);
  Check(bytes.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())),"保存復旧で同じ世界");
  game.Advance(after+60);w=game.Session.DiagnosticSnapshot();Check(PersonalHealth.Active(w,me,w.ElapsedSeconds).All(c=>c.Id!=injury.Id),"回復");
  game.ReviewPersonalLifeOpen();await Frame();await Capture(output,"recovered.png");game.ReviewPersonalLifeClose();
  File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="通常開始・賊との戦いの負傷・暮らしの画面の状態と見立て・手当・保存復旧・回復",Severity=injury.Severity,Part=injury.Part.ToString(),RecoverBefore=before,RecoverAfter=after,ComputerUse="未使用"}));
  GD.Print("PERSONAL_HEALTH_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();
 }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
