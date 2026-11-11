using Godot;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;

// The pre-game map uses the same projection and site IDs as the live Core.
// 地図の範囲は今の日本（琉球・小笠原・北方領土を含む）。北方領土の島の形は表示だけ（northern-territories-v1.json）。
// 基準の位置は九州〜東北。引くと全域、寄ると城の一つずつが見分けられる。つかんで動かし、寄り引きはマウスの位置を中心にする。
public partial class NewGameMap : Control
{
    internal Action<string>? SitePicked;internal string? SelectedSite;internal SetupScenario? Setup;
    // 史料の画面で重ねる点（城の位置・色・強調・名前）と線。地図の拠点とは別。
    internal (GeoCoordinate At,Color Color,bool Strong,string Name)[] Marks=[];internal bool ShowPins=true;internal (GeoCoordinate From,GeoCoordinate To,bool Sea)[] Links=[];

    enum Area{Main,Ryukyu,Ezo,Remote}
    sealed record Shape(RoutePoint[] Points,int[] Indices,bool Hole,Area Area,long MinE,long MaxE,long MinN,long MaxN);
    readonly Dictionary<string,Button> pins=new();readonly List<Shape> shapes=new();
    readonly Dictionary<string,RoutePoint> nodeAt=new(StringComparer.Ordinal);
    RoutePoint center=new(0,0);double scale=1e-6;double fullScale=1e-6,homeScale=1e-6,maxScale=1e-6;RoutePoint fullCenter=new(0,0),homeCenter=new(0,0);long boxMinE,boxMaxE,boxMinN,boxMaxN;
    bool dragging;Vector2 dragFrom;RoutePoint dragCenter=new(0,0);bool ready;bool followHome=true;
    // 基準の位置（九州〜東北）と、操作できない地域の名前の位置
    static readonly GeoCoordinate HomeSouthWest=new(309000000,1294000000),HomeNorthEast=new(416000000,1421000000);
    static readonly (GeoCoordinate At,string Name)[] AreaLabels=[(new(263000000,1278500000),"琉球王国")];
    // 国と郡の境界（borders-v1.json、「郡地図 Ver 1.1」を加工して作成）と、経路・国・郡の表示の切り替え。
    sealed record Line(RoutePoint[] Points,long MinE,long MaxE,long MinN,long MaxN);
    readonly List<Line> provinceLines=new(),districtLines=new();readonly List<(RoutePoint At,string Name)> provinceNames=new(),districtNames=new();
    internal bool ShowRoutes=true,ShowProvinces=true,ShowDistricts=true;
    // 琉球の拠点（首里・那覇・奄美）と、そこへの道は描かない。
    static bool Hidden(string node)=>CastleRoutes.Outside.Contains(node);

    static RoutePoint P(GeoCoordinate g){var p=JapanGeography.Project(g);return new(p.EastMm,p.NorthMm);}
    static Area AreaOf(GeoCoordinate[] ring)
    {
        double lat=ring.Average(p=>p.LatitudeE7)/1e7,lon=ring.Average(p=>p.LongitudeE7)/1e7;
        if(lat<28.9&&lon<131.0)return Area.Ryukyu;if(lat>41.35)return Area.Ezo;if(lon>139.5&&lat<31.0||lon>131.0&&lat<27.0)return Area.Remote;return Area.Main;
    }
    void AddPolygon(GeoPolygon polygon,Area? area=null)
    {
        foreach(var ring in new[]{polygon.Outer}.Concat(polygon.Holes)){
            var first=ring[0];var points=ring.Select(p=>new Vector2((p.LongitudeE7-first.LongitudeE7)/1e7f,-(p.LatitudeE7-first.LatitudeE7)/1e7f)).ToArray();var indices=Geometry2D.TriangulatePolygon(points);
            if(indices.Length==0)throw new InvalidDataException("海岸線の三角形化に失敗しました");
            var pts=ring.Select(P).ToArray();shapes.Add(new(pts,indices,ring!=polygon.Outer,area??AreaOf(polygon.Outer),pts.Min(q=>q.EastMm),pts.Max(q=>q.EastMm),pts.Min(q=>q.NorthMm),pts.Max(q=>q.NorthMm)));
        }
    }
    public override void _Ready()
    {
        ClipContents=true;MouseFilter=MouseFilterEnum.Stop;CustomMinimumSize=new(420,320);
        var coast=JapanGeography.Read(System.Text.Encoding.UTF8.GetBytes(Godot.FileAccess.GetFileAsString("res://Data/Geography/japan-map-v1.json")));
        foreach(var polygon in coast.Polygons)AddPolygon(polygon);
        // 北方領土（表示だけ）。形式は海岸線と同じ（E7 の緯度経度の輪）。
        using(var doc=System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://Data/Geography/northern-territories-v1.json"))){
            static GeoCoordinate[] Ring(System.Text.Json.JsonElement r)=>r.EnumerateArray().Select(q=>new GeoCoordinate(q.GetProperty("LatitudeE7").GetInt32(),q.GetProperty("LongitudeE7").GetInt32())).ToArray();
            foreach(var p in doc.RootElement.GetProperty("Polygons").EnumerateArray())AddPolygon(new(Ring(p.GetProperty("Outer")),p.GetProperty("Holes").EnumerateArray().Select(Ring).ToArray()),Area.Ezo);
        }
        using(var doc=System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://Data/Geography/borders-v1.json"))){
            var root=doc.RootElement;RoutePoint At(System.Text.Json.JsonElement q)=>P(new(q[0].GetInt32(),q[1].GetInt32()));
            void Lines(string key,List<Line> into){foreach(var l in root.GetProperty(key).EnumerateArray()){var pts=l.EnumerateArray().Select(At).ToArray();into.Add(new(pts,pts.Min(q=>q.EastMm),pts.Max(q=>q.EastMm),pts.Min(q=>q.NorthMm),pts.Max(q=>q.NorthMm)));}}
            Lines("ProvinceLines",provinceLines);Lines("DistrictLines",districtLines);
            foreach(var x in root.GetProperty("Provinces").EnumerateArray())provinceNames.Add((At(x.GetProperty("Label")),x.GetProperty("Name").GetString()!));
            foreach(var x in root.GetProperty("Districts").EnumerateArray())districtNames.Add((At(x.GetProperty("Label")),x.GetProperty("Name").GetString()!));
        }
        var panel=new PanelContainer{Position=new(6,4)};panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color(0.1f,0.14f,0.17f,0.82f),ContentMarginLeft=6,ContentMarginRight=6,ContentMarginTop=2,ContentMarginBottom=2});AddChild(panel);var toggles=new HBoxContainer();panel.AddChild(toggles);
        void Toggle(string text,Func<bool> get,Action<bool> set){var c=new CheckBox{Text=text,ButtonPressed=get(),FocusMode=FocusModeEnum.None};c.Toggled+=on=>{set(on);QueueRedraw();};toggles.AddChild(c);}
        Toggle("経路",()=>ShowRoutes,v=>ShowRoutes=v);Toggle("国",()=>ShowProvinces,v=>ShowProvinces=v);Toggle("郡",()=>ShowDistricts,v=>ShowDistricts=v);
        foreach(var n in NationalRoutes.Nodes)nodeAt[n.Id]=n.Position;
        foreach(var s in NationalRoutes.Sites){string id=s.Id.Value;var b=new Button{Text="◆",TooltipText=s.Name,CustomMinimumSize=new(20,20),Size=new(20,20)};AddChild(b);pins[id]=b;b.Pressed+=()=>Pick(id);}
        Resized+=()=>{Fit();LayoutPins();QueueRedraw();};ready=true;Fit();Home();
    }
    // 全域・基準の位置・一番寄った倍率（1ドットが40m）を、画面の大きさから決める。
    void Fit()
    {
        if(!ready||Size.X<=0||Size.Y<=0)return;
        var all=shapes.Where(s=>!s.Hole).SelectMany(s=>s.Points).ToArray();
        long minX=all.Min(p=>p.EastMm),maxX=all.Max(p=>p.EastMm),minY=all.Min(p=>p.NorthMm),maxY=all.Max(p=>p.NorthMm);
        fullCenter=new((minX+maxX)/2,(minY+maxY)/2);boxMinE=minX;boxMaxE=maxX;boxMinN=minY;boxMaxN=maxY;fullScale=Math.Min(Size.X/((maxX-minX)*1.06),Size.Y/((maxY-minY)*1.06));
        var a=P(HomeSouthWest);var b=P(HomeNorthEast);homeCenter=new((a.EastMm+b.EastMm)/2,(a.NorthMm+b.NorthMm)/2);
        homeScale=Math.Min(Size.X/(double)Math.Abs(b.EastMm-a.EastMm),Size.Y/(double)Math.Abs(b.NorthMm-a.NorthMm));maxScale=1/40000.0;
        if(followHome){center=homeCenter;scale=homeScale;}scale=Math.Clamp(scale,fullScale,maxScale);
    }
    internal void Pick(string site){SelectedSite=site;SitePicked?.Invoke(site);LayoutPins();QueueRedraw();}
    internal void Home(){followHome=true;Fit();center=homeCenter;scale=homeScale;LayoutPins();QueueRedraw();}
    internal void Nation(){followHome=false;center=fullCenter;scale=fullScale;LayoutPins();QueueRedraw();}
    internal void Focus(string site){followHome=false;center=nodeAt[NationalRoutes.Nodes.Single(n=>RegionalCampaign.Site(n.Id).Value==site).Id];scale=Math.Min(fullScale*12,maxScale);SelectedSite=site;LayoutPins();QueueRedraw();}
    internal double KilometersPerPixel=>1/scale/1e6;
    internal bool InView(GeoCoordinate g)=>new Rect2(Vector2.Zero,Size).HasPoint(Pixel(P(g)));
    internal void ZoomAt(Vector2 at,double factor)
    {
        followHome=false;var before=Unpixel(at);scale=Math.Clamp(scale*factor,fullScale,maxScale);var after=Unpixel(at);
        center=scale<=fullScale*1.0001?fullCenter:new(center.EastMm+(before.EastMm-after.EastMm),center.NorthMm+(before.NorthMm-after.NorthMm));Clamp();LayoutPins();QueueRedraw();
    }
    internal void CenterOn(GeoCoordinate g,double kilometersPerPixel){followHome=false;center=P(g);scale=Math.Clamp(1/(kilometersPerPixel*1e6),fullScale,maxScale);LayoutPins();QueueRedraw();}
    // 地図の外へ行き過ぎないよう、中心を日本の範囲（全域の箱）に収める。
    void Clamp(){center=new(Math.Clamp(center.EastMm,boxMinE,boxMaxE),Math.Clamp(center.NorthMm,boxMinN,boxMaxN));}
    Vector2 Pixel(RoutePoint p)=>new((float)((p.EastMm-center.EastMm)*scale+Size.X/2),(float)(Size.Y/2-(p.NorthMm-center.NorthMm)*scale));
    RoutePoint Unpixel(Vector2 v)=>new(center.EastMm+(long)((v.X-Size.X/2)/scale),center.NorthMm+(long)((Size.Y/2-v.Y)/scale));
    void LayoutPins()
    {
        foreach(var s in NationalRoutes.Sites){var b=pins[s.Id.Value];var pos=Pixel(nodeAt[NewGameWorld.Node(s.Id.Value)]);b.Position=pos-new Vector2(10,10);b.Visible=ShowPins&&!Hidden(NewGameWorld.Node(s.Id.Value))&&new Rect2(new(12,12),Size-new Vector2(24,24)).HasPoint(pos);b.Text=s.Id.Value==SelectedSite?"●":"◆";b.Modulate=Setup?.Castles.SingleOrDefault(c=>c.Id==s.Id.Value)?.Faction is {} faction?FactionColor(faction):new Color("d6c4a2");}
    }
    static Color FactionColor(string id){uint hash=2166136261;foreach(char c in id)hash=unchecked((hash^c)*16777619);return Color.FromHsv((hash%360)/360f,.4f,.95f);}
    static Color LandColor(Area a)=>a switch{Area.Ryukyu=>new Color("9fa58f"),Area.Ezo=>new Color("a3a78f"),Area.Remote=>new Color("a8a48c"),_=>new Color("b5ae8b")};
    public override void _Draw()
    {
        if(!ready)return;var sea=new Color("263c46");DrawRect(new Rect2(Vector2.Zero,Size),sea);var view=new Rect2(new(-40,-40),Size+new Vector2(80,80));
        foreach(var s in shapes){
            var lo=Pixel(new(s.MinE,s.MaxN));var hi=Pixel(new(s.MaxE,s.MinN));if(!new Rect2(lo,hi-lo).Intersects(view))continue;
            var points=s.Points.Select(Pixel).ToArray();
            var color=s.Hole?sea:LandColor(s.Area);for(int i=0;i<s.Indices.Length;i+=3)DrawPrimitive(new[]{points[s.Indices[i]],points[s.Indices[i+1]],points[s.Indices[i+2]]},new[]{color,color,color},Array.Empty<Vector2>());
            DrawPolyline(points.Append(points[0]).ToArray(),s.Area==Area.Main?new Color("d7cead"):new Color("c7c2a6"),1,true);
        }
        void DrawLines(List<Line> list,Color color,float width){foreach(var l in list){var lo=Pixel(new(l.MinE,l.MaxN));var hi=Pixel(new(l.MaxE,l.MinN));if(!new Rect2(lo,hi-lo).Grow(2).Intersects(view))continue;DrawPolyline(l.Points.Select(Pixel).ToArray(),color,width,true);}}
        double kmpp=KilometersPerPixel;
        if(ShowDistricts&&kmpp<2.5)DrawLines(districtLines,new Color("8d8a76"),1);
        if(ShowProvinces)DrawLines(provinceLines,new Color("a8543a"),1.6f);
        if(ShowRoutes)foreach(var e in NationalRoutes.Edges)if(!Hidden(e.From)&&!Hidden(e.To)&&nodeAt.TryGetValue(e.From,out var fa)&&nodeAt.TryGetValue(e.To,out var fb))DrawLine(Pixel(fa),Pixel(fb),new Color(NationalRoutes.Sea(e.Id)?"5e8797":"797e68"),1,true);
        if(ShowRoutes)foreach(var l in Links){var a=Pixel(P(l.From));var b=Pixel(P(l.To));if(view.HasPoint(a)||view.HasPoint(b))DrawLine(a,b,new Color(l.Sea?"5e879780":"6b6f5c90"),1,true);}
        bool names=KilometersPerPixel<0.35;var font=ThemeDB.FallbackFont;var placed=new List<Rect2>{new(new(0,0),new(250,34))};var castleNames=new List<(Vector2,string)>();
        // 名前どうしが重なるときは後の名前を出さない（国→郡→城の順）
        void Label(Vector2 at,string text,int size,Color color){var sz=font.GetStringSize(text,HorizontalAlignment.Left,-1,size);var r=new Rect2(at-new Vector2(sz.X/2,sz.Y*0.75f),sz).Grow(2);if(!view.HasPoint(at)||placed.Any(p=>p.Intersects(r)))return;placed.Add(r);DrawString(font,at-new Vector2(sz.X/2,-sz.Y*0.25f),text,HorizontalAlignment.Left,-1,size,color);}
        foreach(var m in Marks){var q=Pixel(P(m.At));if(!view.HasPoint(q))continue;float r=names?4f:2f;if(m.Strong)DrawCircle(q,r+1.5f,new Color("1b1b1b"));DrawCircle(q,m.Strong?r+0.5f:r,m.Color);if(names&&m.Name.Length>0)castleNames.Add((q+new Vector2(6+m.Name.Length*6.5f,-8),m.Name));}
        if(ShowProvinces&&kmpp<4)foreach(var (at,name) in provinceNames)Label(Pixel(at),name,15,new Color("f3d6b0"));
        if(ShowDistricts&&kmpp<0.8)foreach(var (at,name) in districtNames)Label(Pixel(at),name,12,new Color("9fe0d2"));
        foreach(var (at,name) in castleNames)Label(at,name,13,new Color("fff2d6"));
        foreach(var (at,name) in AreaLabels){var q=Pixel(P(at));if(new Rect2(new(0,30),Size-new Vector2(0,30)).HasPoint(q))DrawString(font,q-new Vector2(30,0),name,HorizontalAlignment.Left,-1,14,new Color("e8e1c5"));}
        if(scale>fullScale*3&&ShowPins)foreach(var s in NationalRoutes.Sites.Where(s=>!Hidden(NewGameWorld.Node(s.Id.Value)))){var p=Pixel(nodeAt[NewGameWorld.Node(s.Id.Value)]);if(new Rect2(new(24,24),Size-new Vector2(48,48)).HasPoint(p))DrawString(font,p+new Vector2(12,-10),s.Name,HorizontalAlignment.Left,-1,15,new Color("fff2d6"));}
    }
    public override void _GuiInput(InputEvent e)
    {
        if(e is InputEventMouseButton m){
            if(m.Pressed&&m.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown){ZoomAt(m.Position,m.ButtonIndex==MouseButton.WheelUp?1.4:1/1.4);AcceptEvent();}
            else if(m.ButtonIndex is MouseButton.Left or MouseButton.Right or MouseButton.Middle){dragging=m.Pressed;if(m.Pressed)followHome=false;dragFrom=m.Position;dragCenter=center;AcceptEvent();}
        }
        else if(e is InputEventMouseMotion mm&&dragging){var d=mm.Position-dragFrom;center=new(dragCenter.EastMm-(long)(d.X/scale),dragCenter.NorthMm+(long)(d.Y/scale));Clamp();LayoutPins();QueueRedraw();AcceptEvent();}
        else if(e is InputEventKey k&&k.Pressed&&k.Keycode==Key.Home){Home();AcceptEvent();}
    }
    internal void SetLayers(bool routes,bool provinces,bool districts){ShowRoutes=routes;ShowProvinces=provinces;ShowDistricts=districts;foreach(var c in GetChildren().OfType<HBoxContainer>().SelectMany(h=>h.GetChildren().OfType<CheckBox>()))c.SetPressedNoSignal(c.Text=="経路"?routes:c.Text=="国"?provinces:districts);QueueRedraw();}
    internal int BorderCounts=>provinceLines.Count*100000+districtLines.Count;
    internal void SetSetup(SetupScenario? value){Setup=value;LayoutPins();QueueRedraw();}
    internal bool SiteVisible(string site)=>pins[site].Visible;
    internal void PressSite(string site)=>pins[site].EmitSignal(Button.SignalName.Pressed);
}
