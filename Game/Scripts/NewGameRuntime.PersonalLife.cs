using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowPersonalLife()
    {
        if(Session==null||Session.PersonalLifeView() is not {} v)return;
        var popup=new PopupPanel();AddChild(popup);
        var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};popup.AddChild(scroll);scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);var box=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};box.AddThemeConstantOverride("separation",10);scroll.AddChild(box);
        void Text(string text)=>box.AddChild(new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string text,Action run,bool disabled=false){var b=new Button{Text=text,Disabled=disabled,CustomMinimumSize=new Vector2(0,36)};box.AddChild(b);b.Pressed+=run;}
        var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Done(CommandResult r){if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();if(r.Code is ResultCode.Accepted or ResultCode.AlreadyAccepted)ShowPersonalLife();}
        long shown=Session.Revision;EntityId Id(string tag)=>new("request:personal-life-ui/"+tag+"/"+shown);
        void Life(LifeAction action,LifeGoal goal=LifeGoal.WorkPay,long target=0)=>Done(Session.ManageLife(new(Id(action.ToString()),action,v.Site!.Value,action==LifeAction.CancelRest?v.ActiveRest:null,goal,target),Session.PlayerControl,shown));
        Text($"{PersonName(Session.PlayerControl)} — {v.Role}\n私財 {v.PrivateMoney}文 ／ 給付からの私有米 {v.PrivateRice}kg\n給付の未受領 {v.StipendReceivable}kg ／ 自分が支払う未払い {v.StipendPayable}kg");
        Text(v.Burden);Text($"食事で実消費した米 {v.MealRiceConsumed}kg ／ 記録した休養 {v.RestSeconds/60}分");
        if(v.ActiveRest!=null){Text($"休息中：残り {Math.Max(0,v.RestUntil!.Value-Session.Seconds)/60}分。世界時刻は通常の進行操作で進みます。");Button("ここで休息を中止する（宿代・経過時間は戻りません）",()=>Life(LifeAction.CancelRest));}
        else if(v.Site!=null){
            Text("食事・休息は自分で選べます。金欠でも徒歩移動と仕事探しを続けられます。");
            Button("私有米1kgを食事に使う",()=>Life(LifeAction.EatPrivateRice),v.PrivateRice<1);
            if(Session.RegionalTradeView() is {} market){var cargo=market.Deals.LastOrDefault(d=>d.Commodity==null&&d.Amount is >=1 and <=3&&d.Phase is TradePhase.Offered or TradePhase.Accepted or TradePhase.Carrying);if(cargo==null){Button($"現地の食事用米1kgを見積る（現在 {market.BuyPrice}文）",()=>Done(Session.TradeRice(new(Id("meal-quote"),TradeAction.Quote,v.Site.Value,Destination:v.Site,Amount:1),Session.PlayerControl,shown)),market.RiceAvailable<1);}else{var action=cargo.Phase==TradePhase.Offered?TradeAction.Accept:cargo.Phase==TradePhase.Accepted?TradeAction.Collect:TradeAction.Consume;var label=action==TradeAction.Accept?$"見積の{cargo.Amount}kg・{cargo.PurchasePrice*cargo.Amount}文を了承":action==TradeAction.Collect?"現物を受け取り、私財で支払う":$"携行中の米{cargo.Amount}kgを食事に使う";Button(label,()=>Done(Session.TradeRice(new(Id("meal-"+action),action,v.Site.Value,cargo.Id),Session.PlayerControl,shown)));}}
            Button("野外で無料の休息を始める（4時間）",()=>Life(LifeAction.RestOutside),!v.CanRest);Button("宿で休息を始める（10文・2時間）",()=>Life(LifeAction.RestInn),!v.CanRest||v.PrivateMoney<PersonalLife.InnCost);
            Text("当面の目標（設定後の実受領を数えます。達成後も続けられます）");
            foreach(var pair in new[]{(LifeGoal.WorkPay,"仕事報酬を20文受け取る"),(LifeGoal.ReceiveStipend,"扶持米を20kg受け取る"),(LifeGoal.PayStipend,"家臣へ扶持米20kgを渡す")})Button(pair.Item2,()=>Life(LifeAction.SetGoal,pair.Item1,20));
        }
        else Text("道中です。食事・休息や目標の変更は拠点に着いてから選べます。");
        foreach(var g in v.Goals.Reverse().Take(3)){string label=g.Goal==LifeGoal.WorkPay?"仕事報酬（文）":g.Goal==LifeGoal.ReceiveStipend?"扶持の実受領（kg）":"扶持の実支払（kg）";Text($"{label} {g.Progress}/{g.Target} — {(g.Achieved?"達成済み":g==v.Goals.Last()?"現在の目標":"変更前の目標")}");}
        Text("休養の記録は治療・能力上昇を意味しません。給付相談・兵糧・家の資金はそれぞれの操作で扱います。");Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(620,580));
    }
}
