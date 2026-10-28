using Godot;
using System;
using System.Linq;
using System.Globalization;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    string SupplySiteName(ContractWorld w,EntityId id)=>w.Definitions.Sites.FirstOrDefault(s=>s.Id==id)?.Name??"集合拠点";
    string CampSiteLine(ContractWorld w,CampConvoyCard card){if(card.RoadDestination is {} site)return "街道の集合拠点："+SupplySiteName(w,site);var h=NewGameMilitary.Home(w,0);return $"集合地点：拠点から東{(card.Camp.X-h.X)/1000m:0}m・北{(card.Camp.Y-h.Y)/1000m:0}m";}
    void AppendRoadSupply(VBoxContainer box,ContractWorld w,long shown,Action<CampConvoyInput> run)
    {
        if(!RoadConvoy.Enabled(w))return;box.AddChild(new Label{Text="街道で集合拠点へ補給"});box.AddChild(new Label{Text="元の家で担当家臣と米・集合拠点を合意します。30日以内に本人と同じ部隊が集合し、空き容量分だけ受け取ります。残りは家臣が保持し、取消受領・期限後は実帰還して返納します。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        var local=Session.MilitaryEconomyView();if(local==null||!local.CanFund)return;var people=NpcFreight.Candidates(w,local.Site);var nodes=NationalRoutes.Nodes.Where(n=>RegionalCampaign.Site(n.Id)!=local.Site).ToArray();if(people.Length==0||nodes.Length==0){box.AddChild(new Label{Text="元の拠点で担当できる直属家臣が必要です。"});return;}
        var who=new OptionButton();foreach(var person in people)who.AddItem(PersonName(person));box.AddChild(who);var where=new OptionButton();foreach(var n in nodes)where.AddItem(SupplySiteName(w,RegionalCampaign.Site(n.Id)));box.AddChild(where);var kg=new SpinBox{MinValue=1,MaxValue=Math.Max(1,Math.Min(100,local.HouseFreeKg)),Step=1,Value=Math.Max(1,Math.Min(10,local.HouseFreeKg)),Suffix=" kg：家の未引当米"};box.AddChild(kg);var button=new Button{Text="この家臣・集合拠点・米で街道補給を合意",Disabled=local.HouseFreeKg<=0};box.AddChild(button);button.Pressed+=()=>run(new(new("request:road-supply-ui/"+shown),CampConvoyAction.Dispatch,Carrier:people[who.Selected],RiceKg:(long)kg.Value){DestinationSite=RegionalCampaign.Site(nodes[where.Selected].Id)});
    }
    void AppendSupplyReport(VBoxContainer box,ContractWorld w,CampConvoyCard card)
    {if(card.Report is not {} report)return;var date=DateTime.ParseExact(w.EpochDate,"yyyy-MM-dd",CultureInfo.InvariantCulture).AddSeconds(report.ObservedAt);box.AddChild(new Label{Text=$"{date:yyyy/MM/dd HH:mm}の開封報告：{report.Statement}\n当時の荷 {report.Cargo}kg / 累計受渡し {report.SupplyDeliveredKg??0}kg（現在の荷とは限りません）",AutowrapMode=TextServer.AutowrapMode.WordSmart});}
}
