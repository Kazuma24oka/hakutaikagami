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
 async Task Click(CheckBox check){var position=check.GetGlobalRect().GetCenter();GetViewport().PushInput(new InputEventMouseButton{Position=position,GlobalPosition=position,ButtonIndex=MouseButton.Left,Pressed=true},true);await Frame();GetViewport().PushInput(new InputEventMouseButton{Position=position,GlobalPosition=position,ButtonIndex=MouseButton.Left,Pressed=false},true);await Frame();}
 static void Check(bool condition,string text){if(!condition)throw new InvalidDataException(text);}
 async Task Capture(string output,string name){for(int i=0;i<4;i++)await Frame();if(DisplayServer.GetName()=="headless")return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,name))==Error.Ok,"自前描画 "+name);}
 public override async void _Ready(){try{
  string output=RecordPath.Globalize("res://../Docs/Design/HakutaiDbV1/runtime-local/ui");Directory.CreateDirectory(output);
  var entry=new NewGameFrontEnd();AddChild(entry);await Frame();await Frame();
  entry.ReviewDbOpen();await Frame();await Frame();
  Check(entry.ReviewDbMarks()>500,"全国の城の点 "+entry.ReviewDbMarks());Check(entry.ReviewDbStrongMarks()==0,"選ぶ前は強調なし");
  var map=entry.ReviewDbMap()!;Check(map.ReviewControlTextureReady&&map.ReviewControlPixelCount>100000,"郡支配色が実際に生成される");var controlHash=ContractJson.CanonicalHash(map.ControlState!);var controlToggle=map.FindChildren("*","CheckBox",true,false).OfType<CheckBox>().Single(c=>c.Text=="勢力");await Click(controlToggle);Check(!map.ShowControl&&ContractJson.CanonicalHash(map.ControlState!)==controlHash,"勢力表示を消しても郡支配データは変わらない");await Click(controlToggle);Check(map.ShowControl&&ContractJson.CanonicalHash(map.ControlState!)==controlHash,"勢力表示を戻しても郡支配データは変わらない");await Capture(output,"control.png");await Capture(output,"overview.png");
  GeoCoordinate G(double lat,double lon)=>new((int)(lat*1e7),(int)(lon*1e7));
  Check(map.InView(G(31.6,130.55))&&map.InView(G(38.27,140.87))&&!map.InView(G(26.21,127.68)),"基準の位置は九州〜東北（琉球は外）");
  map.Nation();await Frame();Check(map.InView(G(26.21,127.68))&&map.InView(G(45.0,147.8))&&map.InView(G(24.45,122.95)),"引くと琉球・北方領土・与那国まで入る");await Capture(output,"map-full.png");
  map.CenterOn(G(35.2,136.84),0.05);await Frame();Check(map.KilometersPerPixel<0.35,"寄ると城の名前が出る倍率まで寄れる");await Capture(output,"map-castles.png");
  map.ZoomAt(new Godot.Vector2(10,10),1e-6);await Frame();Check(map.InView(G(26.21,127.68)),"一番引いた所で止まる");
  map.Home();await Frame();Check(map.BorderCounts/100000>100&&map.BorderCounts%100000>1000,"国と郡の境界の線");using(var bd=System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://Data/Geography/borders-v2.json"))){var ck=bd.RootElement.GetProperty("Checks");Check(ck.GetProperty("OpenEnds").GetInt64()==0&&ck.GetProperty("UnassignedLand").GetInt64()==0,"陸地はすべて国・郡に割り当てられ、境界線の端はすべて海岸かほかの線に届く");}await Capture(output,"map-provinces.png");
  map.CenterOn(G(34.68,135.47),0.15);await Frame();await Capture(output,"map-osaka.png");map.CenterOn(G(35.17,136.88),0.008);await Frame();Check(map.KilometersPerPixel<0.01,"1ドット10mより寄れる");await Capture(output,"map-closest.png");map.CenterOn(G(35.15,136.85),0.5);await Frame();await Capture(output,"map-districts.png");
  map.CenterOn(G(33.0,130.5),0.4);await Frame();await Capture(output,"map-tamana.png");map.CenterOn(G(37.3,140.35),0.4);await Frame();await Capture(output,"map-iwase.png");map.CenterOn(G(35.25,136.1),0.25);await Frame();await Capture(output,"map-biwa.png");map.CenterOn(G(35.45,133.0),0.12);await Frame();await Capture(output,"map-izumo.png");map.CenterOn(G(34.4,129.33),0.08);await Frame();await Capture(output,"map-tsushima.png");map.CenterOn(G(35.6,140.1),0.6);await Frame();await Capture(output,"map-kanto.png");
  map.SetLayers(false,true,false,false);await Frame();await Capture(output,"map-provinces-only.png");map.SetLayers(true,true,true,true);Check(map.TerrainCounts/10000>4000&&map.TerrainCounts%10000>500,"川と湖");map.CenterOn(G(36.2,138.2),1.2);await Frame();await Capture(output,"map-terrain.png");
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
  File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="スタート画面→史料の画面→1560年郡支配色・織田・今川・本願寺の選択と地図の強調",People=HakutaiDb.PeopleCount,Marks=entry.ReviewDbMarks(),ControlPixels=map.ReviewControlPixelCount,OdaCastles=odaMarks,SourceCommit=HakutaiDb.Source.Commit,ComputerUse="未使用"}));
  GD.Print("HAKUTAI_DB_UI_PASS");GetTree().Quit();
 }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
