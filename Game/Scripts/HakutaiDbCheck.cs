using Godot;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using HakutaiKagami.Core.Contracts;
// スタート画面から「史料：1560年の勢力と人物」を開き、家を選んで当主・本拠・顔ぶれと地図の城を見る。
public partial class HakutaiDbCheck:Node
{
 async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 static void Check(bool condition,string text){if(!condition)throw new InvalidDataException(text);}
 async Task Capture(string output,string name){for(int i=0;i<4;i++)await Frame();if(DisplayServer.GetName()=="headless")return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,name))==Error.Ok,"自前描画 "+name);}
 public override async void _Ready(){try{
  string output=ProjectSettings.GlobalizePath("res://../Docs/Design/HakutaiDbV1/runtime-local/ui");Directory.CreateDirectory(output);
  var entry=new NewGameFrontEnd();AddChild(entry);await Frame();await Frame();
  entry.ReviewDbOpen();await Frame();await Frame();
  Check(entry.ReviewDbText().Contains("人物 "+HakutaiDb.PeopleCount)&&entry.ReviewDbMarks()>500,"一覧と全国の城の点 "+entry.ReviewDbMarks()+" / "+entry.ReviewDbText());Check(entry.ReviewDbStrongMarks()==0,"選ぶ前は強調なし");
  await Capture(output,"overview.png");
  entry.ReviewDbSelect("織田");await Frame();var oda=entry.ReviewDbText();
  Check(oda.Contains("当主：織田信長")&&oda.Contains("本拠：清洲城")&&oda.Contains("〇 柴田勝家　")&&oda.Contains("〇 織田信長　清洲城")&&oda.Contains("石高（推定）")&&oda.Contains("兵の上限")&&!oda.Contains("［"),"織田の当主・本拠・石高・兵の上限・武将一覧（〇△×だけ）");
  Check(entry.ReviewDbStrongMarks()>0,"織田の城を地図で強調");int odaMarks=entry.ReviewDbStrongMarks();
  await Capture(output,"oda.png");
  entry.ReviewDbSelect("今川");await Frame();var imagawa=entry.ReviewDbText();Check(imagawa.Contains("当主：今川義元")&&imagawa.Contains("本拠：今川館"),"今川の当主・本拠");
  entry.ReviewDbSelect("松平（徳川）");await Frame();Check(entry.ReviewDbText().Contains("当主：松平元康"),"1560年の名乗り");await Capture(output,"matsudaira.png");
  var mobHouse=HakutaiDb.Houses.Where(h=>!h.Religious&&HakutaiDbStart.Roster(h.Id).Mobs>0).OrderByDescending(h=>h.Kokudaka).First();entry.ReviewDbSelect(mobHouse.Name);await Frame();Check(entry.ReviewDbText().Contains("× ")&&entry.ReviewDbText().Contains("オリジナル"),"少ない家にモブ武将（×）");await Capture(output,"mobs.png");
  entry.ReviewDbSelect("本願寺");await Frame();Check(entry.ReviewDbText().Contains("顕如"),"寺社の家");
  await Capture(output,"honganji.png");
  File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="スタート画面→史料の画面→織田・今川・本願寺の選択と地図の強調",People=HakutaiDb.PeopleCount,Marks=entry.ReviewDbMarks(),OdaCastles=odaMarks,SourceCommit=HakutaiDb.Source.Commit,ComputerUse="未使用"}));
  GD.Print("HAKUTAI_DB_UI_PASS");GetTree().Quit();
 }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
