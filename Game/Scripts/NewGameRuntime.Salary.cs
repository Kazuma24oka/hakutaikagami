using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowSalary()
    {
        var v=Session.SalaryView();if(v==null){message.Text="拠点で待遇と給付を確認してください。";return;}long shown=Session.Revision;var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var scroll=new ScrollContainer{CustomMinimumSize=new(500,350),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);var box=new VBoxContainer{CustomMinimumSize=new(490,0)};scroll.AddChild(box);string Name(EntityId id)=>Snapshot().Definitions.Characters.Single(c=>c.Id==id).Name;
        box.AddChild(new Label{Text="家の支給と待遇 — "+NewGameWorld.SiteName(v.Site.Value)});box.AddChild(new Label{Text=$"本人の受領済み私有米 {v.PrivateRice}kg（家や軍の備蓄とは別）"});var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Run(SalaryAction action,EntityId person,EntityId? contract=null,long amount=0,EntityId? preparation=null){var result=Session.ManageSalary(new(new("request:salary-ui/"+shown),action,person,v.Site,contract,amount,preparation),Session.PlayerControl,shown);if(result.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=result.VisibleReason;return;}popup.Hide();Refresh();ShowSalary();}
        var amount=new SpinBox{MinValue=1,MaxValue=600,Step=1,Value=20,Suffix=" kg"};box.AddChild(amount);
        if(v.People.Length>0){var people=new OptionButton();foreach(var person in v.People)people.AddItem(Name(person));box.AddChild(people);var propose=new Button{Text="三回分の月初給付を相談する（上の値を月額にする）"};box.AddChild(propose);propose.Pressed+=()=>Run(SalaryAction.Propose,v.People[people.Selected],amount:(long)amount.Value);}
        else box.AddChild(new Label{Text="今ここに、主君と直属家臣として相談できる相手はいません。"});
        var quote=v.Quotes.LastOrDefault(q=>!v.Contracts.Any(c=>c.Id==q.Id)&&q.Site==v.Site);if(quote!=null){box.AddChild(new Label{Text=quote.Reply+"（月額"+quote.Monthly+"kg）",AutowrapMode=TextServer.AutowrapMode.WordSmart});if(quote.Agreed&&Session.Seconds<=quote.AcceptBy){var confirm=new Button{Text="この返答と給付条件を確認する（相談から1日以内）"};box.AddChild(confirm);confirm.Pressed+=()=>Run(SalaryAction.Confirm,quote.Employer==Session.PlayerControl?quote.Employee:quote.Employer,quote.Id);}}
        if(v.Contracts.Length>0){var select=new OptionButton();foreach(var c in v.Contracts)select.AddItem(Name(c.Employer)+" → "+Name(c.Employee));box.AddChild(select);var status=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(status);SalaryCard Current()=>v.Contracts[select.Selected];EntityId Other(SalaryCard c)=>c.Employer==Session.PlayerControl?c.Employee:c.Employer;
            Button Add(string title,SalaryAction action,Func<long> quantity,Func<EntityId?> preparation){var b=new Button{Text=title};box.AddChild(b);b.Pressed+=()=>{var c=Current();Run(action,Other(c),c.Id,quantity(),preparation());};return b;}
            EntityId? Ready(){var c=Current();return Snapshot().Salary!.Entries.LastOrDefault(e=>e.Input.Contract==c.Id&&e.Input.Action==SalaryAction.Prepare)?.Input.Id;}
            var prepare=Add("上の量を支払う準備をする（1日の引当）",SalaryAction.Prepare,()=>(long)amount.Value,()=>null);var receive=Add("用意された米を現地で受け渡す",SalaryAction.Receive,()=>0,Ready);var cancel=Add("支払準備を取りやめる（未払いは残る）",SalaryAction.CancelPrepared,()=>0,Ready);var end=Add("将来分の給付約束を終了する（過去の未払いは残る）",SalaryAction.End,()=>0,()=>null);
            void Update(){var c=Current();bool local=v.Site==c.Site&&Session.GetNearbyActors(Session.PlayerControl).Any(n=>n.Actor==Other(c));status.Text=$"{c.Status}\n月額 {c.Monthly}kg　期日到来 {c.Due}kg　受領済み {c.Paid}kg\n未払い {c.Owed}kg　支払準備 {c.Prepared}kg"+(c.LocalResponse==null?"\n相手の現在の意向は未確認。": "\n"+c.LocalResponse);prepare.Disabled=!local||c.Owed==0||c.Prepared>0||amount.Value>c.Owed;receive.Disabled=cancel.Disabled=!local||c.Prepared==0;end.Disabled=!local||Snapshot().Salary?.Terms.FirstOrDefault(t=>t.Id==c.Id)?.EndedAt!=null;}
            select.ItemSelected+=_=>Update();amount.ValueChanged+=_=>Update();Update();
        }
        var reload=new Button{Text="条件を読み直す"};box.AddChild(reload);reload.Pressed+=()=>{popup.Hide();ShowSalary();};var close=new Button{Text="地図へ戻る"};box.AddChild(close);close.Pressed+=()=>popup.Hide();popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(560,500));
    }
}
