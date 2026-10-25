using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void BuildMilitaryDebrief(Node parent)
    {
        Add(parent,"work-debrief","帰還後の返答を見る",()=>ShowMilitaryDebrief());
        Add(parent,"work-followup-line","次の軍務の配置を確認",ShowMilitaryFollowupLine);
    }
    void RefreshMilitaryDebrief()
    {
        var w=Snapshot();buttons["work-debrief"].Visible=NewGameMilitary.Debriefs(w).Length>0;
        buttons["work-debrief"].Disabled=IsSaving||IsClockControlling;
        buttons["work-followup-line"].Visible=NewGameMilitary.FollowupBasis(w)!=null&&NewGameMilitary.View(w).Custody&&NewGameMilitary.Deployed(w);
        buttons["work-followup-line"].Disabled=IsSaving||IsClockControlling||NewGameMilitary.Moving(w);
    }
    void ShowMilitaryDebrief(EntityId? militaryCase=null)
    {
        var d=NewGameMilitary.Debriefs(Snapshot()).LastOrDefault(x=>militaryCase==null||x.Case==militaryCase);if(d==null)return;shownWork=null;
        string speech=d.Losses>0?"帰還と兵糧の返納、返報を確認した。兵の損耗は報告どおり受け取る。次は兵を立て直し、近場で隊列を確かめよう。":"帰還と兵糧の返納、返報を確認した。今回の返報では兵の損耗はない。次の布陣についても相談してほしい。";
        PresentWork("帰還後の返答",speech,$"帰還兵 {d.Returned}／引受時 {d.Started}、今回の損耗 {d.Losses}。消費兵糧 {d.Used}人秒分。\n次の相談：{d.Next}。損耗の理由・責任・戦功は別に確認します。",d.Principal,d.Principal,"確認済みの返答","次の働き方");workCardHeading.Text="返報と現地返納に基づく";
        if(Snapshot().NewMilitary?.Terms.Principal==d.Principal&&NewGameMilitary.Cases(Snapshot()).Any(c=>c.Id==d.Case)&&NewGameMilitary.ViewWithAuthority(Snapshot()).CanRequest)Choice("work-debrief-next",d.Losses>0?"兵を立て直す次の軍務を相談しよう":"次の布陣の軍務を相談しよう",()=>{CloseBottomTalk();ChooseMilitary(NewMilitaryAction.Request);ShowMilitaryWork();});
        Choice("work-debrief-back","地図へ戻る",CloseBottomTalk);
    }
    void ShowMilitaryFollowupLine()
    {
        var w=Snapshot();var d=NewGameMilitary.FollowupBasis(w);var line=NewGameMilitary.FollowupLine(w);if(d==null||line.Length!=2)return;shownWork=null;
        PresentWork("次の軍務の配置",d.Losses>0?"今回は近場で隊列を確かめよう。兵の損耗をなかったことにはせず、残った兵で並ぶ。":"前回の帰還を踏まえ、今回は少し先の線に並ぼう。",d.Next+"\n示された配置線へ移動します。別の線を自分で指定することもできます。",d.Principal,Session.PlayerControl,"配置の判断","部隊を動かしますか");
        var source=Session;long revision=Session.Revision;
        Choice("work-followup-move","示された線へ向かおう",()=>{if(!ReferenceEquals(Session,source)||Session.Revision!=revision)throw new InvalidOperationException("状況が変わりました。配置を確認し直してください。");CloseBottomTalk();ExerciseMilitaryLine(line[0],line[1]);});
        Choice("work-followup-back","いったん地図へ戻る",CloseBottomTalk);
    }
}
