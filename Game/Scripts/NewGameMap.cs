using Godot;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;

// The pre-game map uses the same projection and site IDs as the live Core.
public partial class NewGameMap : Control
{
    internal Action<string>? SitePicked;internal string? SelectedSite;internal SetupScenario? Setup;
    readonly Dictionary<string,Button> pins=new();readonly List<(GeoCoordinate[] Ring,int[] Indices,bool Hole)> rings=new();JapanCatalog coast=null!;RoutePoint center=new(0,0);double zoom=1;double scale;double breadth=2400000000,depth=2900000000;
    public override void _Ready(){ClipContents=true;MouseFilter=MouseFilterEnum.Stop;CustomMinimumSize=new(420,320);coast=JapanGeography.Read(System.Text.Encoding.UTF8.GetBytes(Godot.FileAccess.GetFileAsString("res://Data/Geography/japan-map-v1.json")));foreach(var polygon in coast.Polygons)foreach(var ring in new[]{polygon.Outer}.Concat(polygon.Holes)){var first=ring[0];var points=ring.Select(p=>new Vector2((p.LongitudeE7-first.LongitudeE7)/1e7f,-(p.LatitudeE7-first.LatitudeE7)/1e7f)).ToArray();var indices=Geometry2D.TriangulatePolygon(points);if(indices.Length==0)throw new InvalidDataException("海岸線の三角形化に失敗しました");rings.Add((ring,indices,ring!=polygon.Outer));}foreach(var s in NationalRoutes.Sites){string id=s.Id.Value;var b=new Button{Text="◆",TooltipText=s.Name,CustomMinimumSize=new(20,20),Size=new(20,20)};AddChild(b);pins[id]=b;b.Pressed+=()=>Pick(id);}Resized+=()=>{LayoutPins();QueueRedraw();};Nation();}
    internal void Pick(string site){SelectedSite=site;SitePicked?.Invoke(site);LayoutPins();QueueRedraw();}
    internal void Nation(){var all=coast.Polygons.SelectMany(p=>p.Outer).Select(JapanGeography.Project).ToArray();long minX=all.Min(p=>p.EastMm),maxX=all.Max(p=>p.EastMm),minY=all.Min(p=>p.NorthMm),maxY=all.Max(p=>p.NorthMm);center=new((minX+maxX)/2,(minY+maxY)/2);breadth=(maxX-minX)*1.12;depth=(maxY-minY)*1.12;zoom=1;LayoutPins();QueueRedraw();}
    internal void Focus(string site){var p=NationalRoutes.Nodes.Single(n=>RegionalCampaign.Site(n.Id).Value==site).Position;center=p;zoom=12;SelectedSite=site;LayoutPins();QueueRedraw();}
    Vector2 Pixel(RoutePoint p)=>new((float)((p.EastMm-center.EastMm)*scale+Size.X/2),(float)(Size.Y/2-(p.NorthMm-center.NorthMm)*scale));
    void LayoutPins(){scale=Math.Min(Size.X/breadth,Size.Y/depth)*zoom;foreach(var s in NationalRoutes.Sites){var b=pins[s.Id.Value];var pos=Pixel(NationalRoutes.Nodes.Single(n=>RegionalCampaign.Site(n.Id)==s.Id).Position);b.Position=pos-new Vector2(10,10);b.Visible=new Rect2(new(12,12),Size-new Vector2(24,24)).HasPoint(pos);b.Text=s.Id.Value==SelectedSite?"●":"◆";b.Modulate=Setup?.Castles.SingleOrDefault(c=>c.Id==s.Id.Value)?.Faction is {} faction?FactionColor(faction):new Color("d6c4a2");}}
    static Color FactionColor(string id){uint hash=2166136261;foreach(char c in id)hash=unchecked((hash^c)*16777619);return Color.FromHsv((hash%360)/360f,.4f,.95f);}
    public override void _Draw(){if(coast==null)return;DrawRect(new Rect2(Vector2.Zero,Size),new Color("263c46"));foreach(var (ring,indices,hole) in rings){var points=ring.Select(g=>{var p=JapanGeography.Project(g);return Pixel(new(p.EastMm,p.NorthMm));}).ToArray();var color=new Color(hole?"263c46":"b5ae8b");for(int i=0;i<indices.Length;i+=3)DrawPrimitive(new[]{points[indices[i]],points[indices[i+1]],points[indices[i+2]]},new[]{color,color,color},Array.Empty<Vector2>());DrawPolyline(points.Append(points[0]).ToArray(),new Color("d7cead"),1,true);}foreach(var e in NationalRoutes.Edges){var a=Pixel(NationalRoutes.Nodes.Single(n=>n.Id==e.From).Position);var b=Pixel(NationalRoutes.Nodes.Single(n=>n.Id==e.To).Position);DrawLine(a,b,new Color(NationalRoutes.Sea(e.Id)?"5e8797":"797e68"),1,true);}if(zoom>3)foreach(var s in NationalRoutes.Sites){var p=Pixel(NationalRoutes.Nodes.Single(n=>RegionalCampaign.Site(n.Id)==s.Id).Position);if(new Rect2(new(24,24),Size-new Vector2(48,48)).HasPoint(p))DrawString(ThemeDB.FallbackFont,p+new Vector2(12,-10),s.Name,HorizontalAlignment.Left,-1,15,new Color("fff2d6"));}DrawString(ThemeDB.FallbackFont,new(16,25),zoom>1?"地域の拠点を選択 / ホイールで拡大・縮小":"全国の拠点 / 地域を選んで拡大",HorizontalAlignment.Left,-1,16,new Color("f4e6c7"));}
    public override void _GuiInput(InputEvent e){if(e is InputEventMouseButton m&&m.Pressed&&m.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown){zoom=Math.Clamp(zoom*(m.ButtonIndex==MouseButton.WheelUp?1.6:1/1.6),1,80);LayoutPins();QueueRedraw();AcceptEvent();}}
    internal void SetSetup(SetupScenario? value){Setup=value;LayoutPins();QueueRedraw();}
    internal bool SiteVisible(string site)=>pins[site].Visible;
    internal void PressSite(string site)=>pins[site].EmitSignal(Button.SignalName.Pressed);
}
