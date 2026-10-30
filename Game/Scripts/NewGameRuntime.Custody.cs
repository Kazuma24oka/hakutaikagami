using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowCustody()
    {
        var world=Snapshot();if(!HostageCustody.Enabled(world))return;
        var popup=new PopupPanel();AddChild(popup);var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};popup.AddChild(scroll);scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);var box=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(box);
        void Text(string text)=>box.AddChild(new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string text,Action action){var b=new Button{Text=text,CustomMinimumSize=new Vector2(0,34)};box.AddChild(b);b.Pressed+=action;}
        Text("人質の現地預かり・返還");Text("当人と両家の現地確認が必要です。本人・受入先の返答と、実際の受入を分けます。返還期限を過ぎても自動返送や処刑はしません。捕虜の拘束・護送・交換はまだ扱いません。");
        var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);long revision=Session.Revision;
        void Act(CustodyAction action,EntityId? person=null,EntityId? receiver=null,EntityId? original=null,int days=0,string? guarantee=null){var r=Session.ManageCustody(new(new("request:custody-ui/"+Guid.NewGuid().ToString("N")),action,person,receiver,original,days,guarantee),Session.PlayerControl,revision);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowCustody();}
        foreach(var visit in HostageCustody.Visits(world))Button(PersonName(visit.Person)+"と保証原本の預かり先へ同行する",()=>{var result=Session.ManageCompanion(new(new("request:custody-visit/"+Guid.NewGuid().ToString("N")),visit.Person,CompanionAction.VisitCustodySite,Destination:visit.Destination),Session.PlayerControl,revision);if(result.Code!=ResultCode.Accepted){feedback.Text=result.VisibleReason;return;}popup.Hide();Refresh();});
        foreach(var card in HostageCustody.View(world)){
            Text(PersonName(card.Person)+" ／ "+card.Status);Text("最終確認："+TreatyDate(card.LastObservedAt)+" ／ 返還期限："+TreatyDate(card.Until));Text("受入先："+PersonName(card.Custodian)+" ／ "+card.Treatment);Text("保証内容："+card.Guarantee);
            foreach(var meal in HostageCustody.Meals(world).Where(m=>m.Original==card.Id))Text(TreatyDate(meal.ObservedAt)+" 食事提供："+(meal.RiceKg==1?"家の米1kgを実消費":"未履行（自由在庫不足）。米の引当は解除していません"));
            if(HostageCustody.CanRequestMeal(world,card.Id))Button("現地で食事の提供を確認する（試作1kg・一日間隔）",()=>Act(CustodyAction.RequestMeal,original:card.Id));
            if(card.CanAdmit)Button("現地で受入を実施する",()=>Act(CustodyAction.Admit,original:card.Id));
            if(card.CanReconfirm)Button("当主交代後の両家の代表者を現地で再確認する",()=>Act(CustodyAction.Reconfirm,original:card.Id));
            if(card.CanRelease)Button("現地で解放・返還を受ける",()=>Act(CustodyAction.Release,original:card.Id));
            if(card.CanWithdraw)Button("受入前の相談を取り下げる",()=>Act(CustodyAction.Withdraw,original:card.Id));
        }
        var candidates=HostageCustody.Candidates(world);if(candidates.Length==0)Text("現地で相談できる成人の直属登録家臣はいません。");
        else{
            var person=new OptionButton();box.AddChild(person);foreach(var id in candidates)person.AddItem(PersonName(id));
            var houses=world.NewGame!.Start.Factions.Where(f=>f.Head!=Session.PlayerControl.Value).ToArray();var receiver=new OptionButton();box.AddChild(receiver);foreach(var house in houses)receiver.AddItem(house.Name);
            Text("返還までの日数（1〜7日）");var days=new SpinBox{MinValue=1,MaxValue=7,Step=1,Value=1};box.AddChild(days);
            var promise=new LineEdit{PlaceholderText="保証する内容を記入",MaxLength=200};box.AddChild(promise);
            if(houses.Length>0)Button("本人と受入先へ現地で相談する",()=>Act(CustodyAction.Propose,candidates[person.Selected],new(houses[receiver.Selected].Id),days:(int)days.Value,guarantee:promise.Text));
        }
        Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(630,600));
    }
}
