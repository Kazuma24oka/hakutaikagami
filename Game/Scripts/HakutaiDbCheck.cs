using Godot;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;
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
  Check(entry.ReviewDbMarks()>500,"全国の城の点 "+entry.ReviewDbMarks());Check(entry.ReviewDbStrongMarks()==0,"選ぶ前は強調なし");
  await Capture(output,"overview.png");
  var map=entry.ReviewDbMap()!;GeoCoordinate G(double lat,double lon)=>new((int)(lat*1e7),(int)(lon*1e7));
  Check(map.InView(G(31.6,130.55))&&map.InView(G(38.27,140.87))&&!map.InView(G(26.21,127.68)),"基準の位置は九州〜東北（琉球は外）");
  map.Nation();await Frame();Check(map.InView(G(26.21,127.68))&&map.InView(G(45.0,147.8))&&map.InView(G(24.45,122.95)),"引くと琉球・北方領土・与那国まで入る");await Capture(output,"map-full.png");
  map.CenterOn(G(35.2,136.84),0.05);await Frame();Check(map.KilometersPerPixel<0.35,"寄ると城の名前が出る倍率まで寄れる");await Capture(output,"map-castles.png");
  map.ZoomAt(new Godot.Vector2(10,10),1e-6);await Frame();Check(map.InView(G(26.21,127.68)),"一番引いた所で止まる");
  map.Home();await Frame();Check(map.BorderCounts/100000>100&&map.BorderCounts%100000>1000,"国と郡の境界の線");await Capture(output,"map-provinces.png");
  map.CenterOn(G(35.15,136.85),0.5);await Frame();await Capture(output,"map-districts.png");
  map.CenterOn(G(33.0,130.5),0.4);await Frame();await Capture(output,"map-tamana.png");map.CenterOn(G(37.3,140.35),0.4);await Frame();await Capture(output,"map-iwase.png");
  map.SetLayers(false,true,false);await Frame();await Capture(output,"map-provinces-only.png");map.SetLayers(true,true,true);
  map.Home();await Frame();
  entry.ReviewDbSelect("織田");await Frame();var oda=entry.ReviewDbText();
  Check(oda.Contains("当主：織田信長")&&oda.Contains("本拠：清洲城")&&oda.Contains("〇 柴田勝家　")&&oda.Contains("〇 織田信長　清洲城")&&oda.Contains("石高 ")&&oda.Contains("兵の上限")&&!oda.Contains("［"),"織田の当主・本拠・石高・兵の上限・武将一覧（〇△×だけ）");
  Check(entry.ReviewDbStrongMarks()>0,"織田の城を地図で強調");int odaMarks=entry.ReviewDbStrongMarks();
  await Capture(output,"oda.png");
  entry.ReviewDbSelect("今川");await Frame();var imagawa=entry.ReviewDbText();Check(imagawa.Contains("当主：今川義元")&&imagawa.Contains("本拠：今川館"),"今川の当主・本拠");
  entry.ReviewDbSelect("松平（徳川）");await Frame();Check(entry.ReviewDbText().Contains("当主：松平元康"),"1560年の名乗り");await Capture(output,"matsudaira.png");
  var mobHouse=HakutaiDb.Houses.Where(h=>!h.Religious&&HakutaiDbStart.Roster(h.Id).Mobs>0).OrderByDescending(h=>h.Kokudaka).First();entry.ReviewDbSelect(mobHouse.Name);await Frame();Check(entry.ReviewDbText().Contains("× "),"少ない家にモブ武将（×）");await Capture(output,"mobs.png");
  entry.ReviewDbSelect("本願寺");await Frame();Check(entry.ReviewDbText().Contains("顕如"),"寺社の家");
  await Capture(output,"honganji.png");
  File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="スタート画面→史料の画面→織田・今川・本願寺の選択と地図の強調",People=HakutaiDb.PeopleCount,Marks=entry.ReviewDbMarks(),OdaCastles=odaMarks,SourceCommit=HakutaiDb.Source.Commit,ComputerUse="未使用"}));
  GD.Print("HAKUTAI_DB_UI_PASS");GetTree().Quit();
 }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
