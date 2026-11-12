using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;
// 史料の画面：百代鏡DB（戦国DBの版から書き出した1560年）の家・人物・城を、決め方つきで見る。開始前の参照で、
// ゲームの中の人物の知識ではない（遠方の情勢は報告で知る）。
public partial class NewGameFrontEnd
{
    ItemList? dbHouses;Label? dbDetail;NewGameMap? dbMap;
    internal void ReviewDbSelect(string house){var list=HakutaiDb.Houses.OrderByDescending(HakutaiDbStart.Quota).ThenBy(h=>h.Id,StringComparer.Ordinal).ToArray();int i=Enumerable.Range(0,list.Length).First(k=>list[k].Name==house);dbHouses!.Select(i);dbHouses.EmitSignal(ItemList.SignalName.ItemSelected,i);}
    internal void ReviewDbOpen()=>buttons["hakutai-db"].EmitSignal(BaseButton.SignalName.Pressed);
    internal NewGameMap? ReviewDbMap()=>dbMap;
    internal string ReviewDbText()=>dbDetail?.Text??"";
    internal int ReviewDbStrongMarks()=>dbMap?.Marks.Count(m=>m.Strong)??0;
    internal int ReviewDbMarks()=>dbMap?.Marks.Length??0;
    static Color DbColor(string id){uint hash=2166136261;foreach(char c in id)hash=unchecked((hash^c)*16777619);return Color.FromHsv((hash%360)/360f,.55f,.9f);}
    void ShowHakutaiDb()
    {
        var src=HakutaiDb.Source;
        Clear("1560年の勢力と人物","");
        var columns=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(0,470)};page.AddChild(columns);
        var houses=HakutaiDb.Houses.OrderByDescending(HakutaiDbStart.Quota).ThenBy(h=>h.Id,StringComparer.Ordinal).ToArray();
        dbHouses=new ItemList{CustomMinimumSize=new(210,0),SizeFlagsVertical=SizeFlags.ExpandFill};columns.AddChild(dbHouses);
        foreach(var h in houses)dbHouses.AddItem($"{h.Name}（{HakutaiDbStart.Quota(h)}人）");
        dbMap=new NewGameMap{ShowPins=false,ShowControl=true,ControlState=NationalDistrictControl.Initial(),SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};columns.AddChild(dbMap);
        var scroll=new ScrollContainer{CustomMinimumSize=new(380,0),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsVertical=SizeFlags.ExpandFill};columns.AddChild(scroll);
        dbDetail=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(360,0)};scroll.AddChild(dbDetail);
        void Marks(string? selected)
        {
            // 城ごとに、その年の持ち主（なければ本拠・置き場所としている家）。
            var users=HakutaiDb.Houses.SelectMany(h=>HakutaiDb.Members(h.Id).Select(p=>p.Castle).Append(h.Home).OfType<string>().Select(c=>(c,h.Id))).ToLookup(x=>x.c,x=>x.Id);
            dbMap.Marks=HakutaiDb.PublicGeography().Select(place=>{
                    var c=HakutaiDb.Castle(place.Id)!;string owner=c.Owner??users[c.Id].First();
                    return (new GeoCoordinate((int)c.LatitudeE7,(int)c.LongitudeE7),DbColor(HakutaiDb.TopHouse(owner).Id),selected!=null&&(owner==selected||users[c.Id].Contains(selected)),c.Name);}).ToArray();
            dbMap.QueueRedraw();
        }
        string Castle(string? id)=>id==null?"—":HakutaiDb.Castle(id)?.Name??id;
        dbHouses.ItemSelected+=i=>{
            var h=houses[(int)i];var roster=HakutaiDbStart.Roster(h.Id);var head=h.Head==null?null:HakutaiDb.Person(h.Head);
            var districts=HakutaiDb.Districts.Where(d=>d.Shares.Any(x=>x.House==h.Id)).ToArray();
            var lines=new System.Collections.Generic.List<string>{
                $"{h.Name}",
                head==null?"当主：記録なし":$"当主：{head.StartName}",
                "本拠："+Castle(h.Home),
                "主家："+(h.Overlord==null?"なし":HakutaiDb.House(h.Overlord)!.Name+"（最上位 "+HakutaiDb.TopHouse(h.Id).Name+"）")};
            foreach(var g in h.Relations.GroupBy(r=>r.Kind))lines.Add($"{g.Key}：{string.Join("・",g.Select(r=>HakutaiDb.House(r.House)!.Name))}");
            lines.Add($"\n郡 {districts.Length}（{string.Join("・",districts.Take(12).Select(d=>d.Name))}{(districts.Length>12?" ほか":"")}）");
            lines.Add($"石高 {roster.Kokudaka/10000d:0.#}万石{(roster.MoneyKan>0?"　金銭収入 "+roster.MoneyKan+"貫文":"")}");
            lines.Add($"兵の上限 {roster.SoldierCap:#,0}人");
            lines.Add($"武将 {roster.Officers.Length}人");
            lines.Add("\n武将一覧");
            foreach(var o in roster.Officers)lines.Add($"{HakutaiDbStart.Mark(o.Historicity)} {o.Name}　{Castle(o.Castle)}");
            dbDetail.Text=string.Join("\n",lines);Marks(h.Id);
        };
        var at=CastleRoutes.Catalog.Nodes.ToDictionary(n=>n.Id,n=>new GeoCoordinate(n.LatitudeE7,n.LongitudeE7));foreach(var n in NationalRoutes.Catalog.Nodes)at[n.Id]=new(n.LatitudeE7,n.LongitudeE7);
        dbMap.Links=CastleRoutes.Catalog.Edges.Where(e=>at.ContainsKey(e.From)&&at.ContainsKey(e.To)).Select(e=>(at[e.From],at[e.To],e.Mode=="sea")).ToArray();
        Marks(null);dbDetail.Text="";
        Add(page,"back","スタートへ",ShowMenu);
    }
}
