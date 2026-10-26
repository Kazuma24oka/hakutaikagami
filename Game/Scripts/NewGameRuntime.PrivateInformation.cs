using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowPrivateContact()
    {
        var w=Snapshot();var people=RegionalCampaign.Nearby(w,w.PlayerControl).Where(p=>p.Actor!=w.PlayerControl).Select(p=>p.Actor).ToArray();var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var box=new VBoxContainer{CustomMinimumSize=new(490,250)};panel.AddChild(box);box.AddChild(new Label{Text="現地で私的に情報交換を打診する"});box.AddChild(new Label{Text="相手が自分で見た道の情報だけを相談します。\n所属変更や内応の約束を買う操作ではありません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        if(people.Length==0)box.AddChild(new Label{Text="現地に相談相手がいません。"});else{var person=new OptionButton();foreach(var id in people)person.AddItem(w.Definitions.Characters.Single(c=>c.Id==id).Name);box.AddChild(person);var ask=new Button{Text="情報提供の条件を聞く"};box.AddChild(ask);long shown=Session.Revision;ask.Pressed+=()=>{popup.Hide();try{var id=new EntityId("request:private-info-ui/"+shown);Submit(Session.ContactForInformation(new(id,people[person.Selected],InformationAction.Ask),Session.PlayerControl,shown));ShowInformationQuote(id);}catch(Exception e){message.Text="操作できません："+e.Message;}Refresh();};}
        foreach(var card in Session.PrivateInformationView().Where(c=>c.Stage.Contains("未払い"))){var review=new Button{Text="未精算の情報を確認（"+card.Fee+"文）"};box.AddChild(review);review.Pressed+=()=>{popup.Hide();var row=Branch(Session.PlayerControl).Knowledge.Messages.Single(m=>m.KnownBody!=null&&Snapshot().Messaging.Messages.Any(p=>p.Id==m.Id.Value&&p.Kind==PrivateInformation.Kind)&&ContractJson.Read<InformationPayload>(System.Text.Encoding.UTF8.GetBytes(m.KnownBody)).Quote==card.Quote);ShowPrivateInformationLetter(row.Id);};}
        var close=new Button{Text="地図へ戻る"};box.AddChild(close);close.Pressed+=()=>popup.Hide();popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(550,300));
    }
    void ShowInformationQuote(EntityId id)
    {
        var card=Session.PrivateInformationView().Single(c=>c.Quote==id);var world=Snapshot();var quote=world.PrivateInformation!.Quotes.Single(q=>q.Id==id);long observed=quote.Sources.Max(source=>world.Evidence.Single(e=>e.Id==source).ObservedAt!.Value);long shown=Session.Revision;PresentWork("私的な情報交換","私が実際に見た道の話なら、情報料"+card.Fee+"文で話そう。","最も新しい観測でも、開始から"+observed+"秒時点の記録です。新しい話とは限りません。届いて開いた後、現地で支払ってください。",card.Person,card.Person,"現地で聞いた条件","個人間の情報交換");
        if(card.CanAccept)Choice("private-info-accept","その条件で情報を受け取る",()=>{Submit(Session.ContactForInformation(new(new("request:private-info-accept/"+shown),card.Person,InformationAction.Accept,id),Session.PlayerControl,shown));CloseBottomTalk();});Choice("private-info-later","今回は見送る",CloseBottomTalk);LayoutBottomTalk();
    }
    void ShowPrivateInformationLetter(EntityId id)
    {
        var known=Branch(Session.PlayerControl).Knowledge.Messages.Single(m=>m.Id==id);if(known.KnownBody==null)throw new InvalidOperationException("未開封の私的情報です");var payload=ContractJson.Read<InformationPayload>(System.Text.Encoding.UTF8.GetBytes(known.KnownBody));var card=Session.PrivateInformationView().Single(c=>c.Quote==payload.Quote);long shown=Session.Revision;var text=string.Join("\n",payload.Facts.Select(f=>{bool blocked=f.Claim.StartsWith("route-block:",StringComparison.Ordinal);string edge=f.Claim[(blocked?12:11)..];var road=BranchRoutes.GraphEdges(Snapshot()).Single(e=>e.Id==edge);return NewGameWorld.SiteName(RegionalCampaign.Site(road.From).Value)+"―"+NewGameWorld.SiteName(RegionalCampaign.Site(road.To).Value)+"："+(blocked?"通行できなかった":"通行できた")+"（開始から"+f.ObservedAt+"秒の観測）";}));PresentWork("相手から受け取った道の情報",text,"相手の観測当時の情報です。現在も同じとは限りません。",payload.Speaker,payload.Speaker,"提供者の観測","受領した記録");if(card.CanPay)Choice("private-info-pay","情報料"+card.Fee+"文を私財から支払う",()=>{Submit(Session.ContactForInformation(new(new("request:private-info-pay/"+shown),card.Person,InformationAction.Pay,card.Quote),Session.PlayerControl,shown));CloseBottomTalk();});Choice("private-info-map","地図へ戻る",CloseBottomTalk);LayoutBottomTalk();
    }
}
