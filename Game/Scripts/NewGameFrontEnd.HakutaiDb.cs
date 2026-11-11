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
    internal void ReviewDbSelect(string house){var list=HakutaiDb.HousesBySize;int i=Enumerable.Range(0,list.Count).First(k=>list[k].Name==house);dbHouses!.Select(i);dbHouses.EmitSignal(ItemList.SignalName.ItemSelected,i);}
    internal void ReviewDbOpen()=>buttons["hakutai-db"].EmitSignal(BaseButton.SignalName.Pressed);
    internal string ReviewDbText()=>dbDetail?.Text??"";
    internal int ReviewDbStrongMarks()=>dbMap?.Marks.Count(m=>m.Strong)??0;
    internal int ReviewDbMarks()=>dbMap?.Marks.Length??0;
    static Color DbColor(string id){uint hash=2166136261;foreach(char c in id)hash=unchecked((hash^c)*16777619);return Color.FromHsv((hash%360)/360f,.55f,.9f);}
    void ShowHakutaiDb()
    {
        var src=HakutaiDb.Source;
        Clear("史料：1560年の勢力と人物",$"戦国DB（版 {src.Commit[..8]}）から書き出した{HakutaiDb.WorldYear}年の見積もりです。所属・置き場所の城・直属の上司・城の持ち主は決め方つきの推定で、史実の判定ではありません。城の位置は公知の地理、持ち主や家の顔ぶれはその年の情勢で、ゲームの中では人物が報告で知ります。");
        var columns=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(0,470)};page.AddChild(columns);
        var houses=HakutaiDb.HousesBySize;
        dbHouses=new ItemList{CustomMinimumSize=new(210,0),SizeFlagsVertical=SizeFlags.ExpandFill};columns.AddChild(dbHouses);
        foreach(var h in houses)dbHouses.AddItem($"{h.Name}（{HakutaiDb.Members(h.Id).Count}人）");
        dbMap=new NewGameMap{ShowPins=false,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};columns.AddChild(dbMap);
        var scroll=new ScrollContainer{CustomMinimumSize=new(380,0),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsVertical=SizeFlags.ExpandFill};columns.AddChild(scroll);
        dbDetail=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(360,0)};scroll.AddChild(dbDetail);
        void Marks(string? selected)
        {
            // 城ごとに、その年の持ち主（なければ本拠・置き場所としている家）。
            var users=HakutaiDb.Houses.SelectMany(h=>HakutaiDb.Members(h.Id).Select(p=>p.Castle).Append(h.Home).OfType<string>().Select(c=>(c,h.Id))).ToLookup(x=>x.c,x=>x.Id);
            dbMap.Marks=HakutaiDb.PublicGeography().Select(place=>{
                    var c=HakutaiDb.Castle(place.Id)!;string owner=c.Owner??users[c.Id].First();
                    return (new GeoCoordinate((int)c.LatitudeE7,(int)c.LongitudeE7),DbColor(HakutaiDb.TopHouse(owner).Id),selected!=null&&(owner==selected||users[c.Id].Contains(selected)));}).ToArray();
            dbMap.QueueRedraw();
        }
        string Name(string? id)=>id==null?"—":HakutaiDb.Person(id) is {} p?p.Name:id;
        string Castle(string? id)=>id==null?"—":HakutaiDb.Castle(id)?.Name??id;
        dbHouses.ItemSelected+=i=>{
            var h=houses[(int)i];var members=HakutaiDb.Members(h.Id);var head=h.Head==null?null:HakutaiDb.Person(h.Head);
            var lines=new System.Collections.Generic.List<string>{
                $"{h.Name}{(h.Religious?"（寺社・所属先）":"")}",
                head==null?"当主：記録なし":$"当主：{head.Name}（{head.Reading ?? "読み不明"}・{(head.Birth is int b?b+"年生":"生年不明")}）／決め方：{h.HeadBasis}",
                "本拠："+Castle(h.Home),
                "主家："+(h.Overlord==null?"なし":HakutaiDb.House(h.Overlord)!.Name+"（最上位 "+HakutaiDb.TopHouse(h.Id).Name+"）")};
            foreach(var g in h.Relations.GroupBy(r=>r.Kind))lines.Add($"{g.Key}：{string.Join("・",g.Select(r=>HakutaiDb.House(r.House)!.Name))}");
            lines.Add($"\n顔ぶれ {members.Count}人（当主・年長の順に最大40人。所属・城は決め方つきの推定）");
            foreach(var p in members.OrderBy(p=>p.Id==h.Head?0:1).ThenBy(p=>p.Birth??9999).ThenBy(p=>p.Id,StringComparer.Ordinal).Take(40))
                lines.Add($"・{p.Name}{(p.Birth is int b2?" "+b2+"年生":"")}　{(p.CastleName??"城不明")}　上司 {Name(p.Superior)}　［{p.HouseBasis}・実在{p.Existence??"?"}］");
            dbDetail.Text=string.Join("\n",lines);Marks(h.Id);
        };
        Marks(null);dbDetail.Text=$"家 {houses.Count}・人物 {HakutaiDb.PeopleCount}・城 {dbMap.Marks.Length}\n左の一覧から家を選ぶと、当主・本拠・関係・顔ぶれと、地図にその家の城を示します。地図の色は主家をたどった一番上の家ごと。";
        Add(page,"back","スタートへ",ShowMenu);
    }
}
