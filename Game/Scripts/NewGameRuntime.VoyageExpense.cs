using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    Label voyageHint=null!;
    void BuildVoyageTravel()
    {
        var col=buttons["depart"].GetParent();voyageHint=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(voyageHint);
        Add(col,"depart-walk","陸路だけで出発（船を使わない）",()=>{try{if(TryArmyDeparture())return;RequestDeparture(TravelWorld.Move(Snapshot(),"request:walk-ui/"+Session.Revision,destinationNodes[destination.Selected].Id,true));}catch(System.IO.InvalidDataException){message.Text="操作できません：この行先への陸路はありません。陸続きの行先を選んでください。";}});
        Add(col,"voyage-cancel","出航を取りやめ、港に留まる",()=>{Submit(Session.ChangeTravel(TravelControl.Request(Snapshot(),"request:cancel-boarding-ui/"+Session.Revision,null),Session.Revision));});
        foreach(var id in new[]{"depart-walk","voyage-cancel"})buttons[id].CustomMinimumSize=new(0,42);
    }
    void RefreshVoyageTravel()
    {
        if(voyageHint==null)return;var w=Snapshot();bool enabled=VoyageExpense.Enabled(w);voyageHint.Visible=buttons["depart-walk"].Visible=enabled;
        bool busy=IsSaving||IsClockControlling||IsDeparting;buttons["depart-walk"].Disabled=busy||TravelWorld.Active(w)||w.Locations.Single(l=>l.Actor==Session.PlayerControl).Kind!=LocationKind.AtSite;
        buttons["voyage-cancel"].Visible=enabled&&TravelControl.PortWaiting(w,Session.PlayerControl);buttons["voyage-cancel"].Disabled=busy;
        if(!enabled)return;var support=w.NewGame?.Judgment?.Support ?? DecisionSupport.Brief;long fee=w.NewGame!.VoyageRules!.FarePerPassage;string text=$"徒歩は無料。船賃は出航時に1区間{fee}文。\n私財から支払い、預り金は使いません。";
        var refused=w.VoyageExpenses?.Refusals.LastOrDefault();var latest=w.Tasks.LastOrDefault(t=>t.Executor==Session.PlayerControl&&t.Action==ActionKind.Move);
        if(refused!=null&&latest?.Id==refused.Task&&latest.Execution==ExecutionPhase.Stopped)text="船賃が足りず港で待機中。" + (support switch {
            DecisionSupport.Independent => "",
            DecisionSupport.Detailed => $"\n船で進むには私財から{fee}文必要です。陸路は徒歩で進めます。船を使うなら仕事で旅費を得てから出発できます。",
            _ => "\n陸路の行先や別の仕事を選べます。"
        });
        else if(w.VoyageExpenses?.Payments.LastOrDefault() is {} paid)text+=$"\n直近の船賃：{paid.Amount}文支払済み。";
        if (Session.ArmyRoadView() is {} army)
        {
            string supply = army.Marching ? "行軍中" :
                army.LocalRicePrice.HasValue ? $"現地で購入可能 {army.MaxPurchaseKg}kg（1kg {army.LocalRicePrice}文）。" :
                "この集合地では兵糧を購入できません。";
            text = $"携行米 {army.CarryKg}kg / 私財 {army.PrivateMoney}文。\n{supply} ";
            if (support != DecisionSupport.Independent) text += "指揮返還は元の家で行います。";
            if (support == DecisionSupport.Detailed && !army.Marching && army.MaxPurchaseKg > 0)
                text += $" 1kg積むと私財が{army.LocalRicePrice}文減り、携行米が1kg増えます。";
            buttons["depart"].Text = "部隊を率いて行先へ";
        }
        else buttons["depart"].Text = "行先へ出発";
        voyageHint.Text=text;
    }
    internal string ReviewVoyageHint=>voyageHint?.Text??"";
}
