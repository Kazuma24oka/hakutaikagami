using Godot;
using System;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    const long SupplyWorkDay=100*86400L;
    Label supplyWorkNeed=null!;
    void BuildSupplyWork(VBoxContainer stack)
    {
        supplyWorkNeed=new(){Visible=false,AutowrapMode=TextServer.AutowrapMode.WordSmart};stack.AddChild(supplyWorkNeed);
        Add(stack,"work-supply","兵糧を受け取る（軍務）",ShowSupplyWork);buttons["work-supply"].CustomMinimumSize=new(0,42);
    }
    bool NeedsSupplyWork()=>NewGameWork.Enabled(Snapshot())&&Array.IndexOf(militaryActions,NewMilitaryAction.Load)>=0&&NewGameMilitary.ViewWithAuthority(Snapshot()).Carry<SupplyWorkDay;
    void RefreshSupplyWork()
    {
        bool needed=NeedsSupplyWork();supplyWorkNeed.Visible=buttons["work-supply"].Visible=needed;buttons["work-supply"].Disabled=IsSaving||IsClockControlling||IsDeparting;
        if(needed)supplyWorkNeed.Text="軍務の準備：携行兵糧が100人換算で1日分未満です。";
    }
    void ShowSupplyWork()
    {
        if(IsSaving||IsClockControlling||IsDeparting)return;RefreshMilitary();if(!NeedsSupplyWork())return;
        shownWork=null;var source=Session;long revision=Session.Revision;
        PresentWork("軍務の兵糧準備","出発に備え、主家の兵糧を現地で受け取ろう。","引受　100人の1日分\n所有　主家（携行するだけで私財にはならない）\n別払いの報酬はありません。",Session.PlayerControl,Session.PlayerControl,"本人の準備","次の行動");workCardHeading.Text="現地での引受";
        Choice("work-supply-confirm","主家の兵糧を受け取ろう",()=>{
            if(!ReferenceEquals(Session,source)||Session.Revision!=revision)throw new InvalidOperationException("状況が変わりました。兵糧の準備を確認し直してください。");RefreshMilitary();if(!NeedsSupplyWork())throw new InvalidOperationException("現在は現地で兵糧を引き受けられません。");
            CloseBottomTalk();ChooseMilitary(NewMilitaryAction.Load);operationTabs.CurrentTab=0;
            PresentWork("兵糧の引受","主家の兵糧を携行分へ移した。部隊の行動を選ぼう。","主家の在庫から引受済み。所有は主家のままです。",Session.PlayerControl,Session.PlayerControl,"引受済み","次の行動");workCardHeading.Text="軍務の準備";
            Choice("work-supply-field","部隊の地図操作へ",()=>{CloseBottomTalk();operationTabs.CurrentTab=1;});Choice("work-supply-done","仕事へ戻る",CloseBottomTalk);
        });
        Choice("work-supply-later","今は受け取らず戻る",CloseBottomTalk);
    }
    internal bool ReviewSupplyWorkNeeded=>buttons["work-supply"].Visible&&NeedsSupplyWork();
}
