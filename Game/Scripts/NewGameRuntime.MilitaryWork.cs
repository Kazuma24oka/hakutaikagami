using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    OptionButton militaryWorkChoice=null!;NewMilitaryAction[] militaryWorkActions=[];bool drawingMilitaryWork;long? militaryWorkDeadline;
    void BuildMilitaryWork(Node parent)
    {
        BuildMilitaryDebrief(parent);
        Add(parent,"work-military","軍務を相談・確認",()=>ShowMilitaryWork());
        militaryWorkChoice=new(){Visible=false,CustomMinimumSize=new(0,36),FitToLongestItem=false,TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis};
        workDetails.GetParent().AddChild(militaryWorkChoice);workDetails.GetParent().MoveChild(militaryWorkChoice,workDetails.GetIndex()+1);
        militaryWorkChoice.ItemSelected+=n=>{if(!drawingMilitaryWork&&n>=0&&n<militaryWorkActions.Length)MilitaryWorkDetails(militaryWorkActions[n]);};
    }
    void RefreshMilitaryWork()
    {
        RefreshMilitaryDebrief();var w=Snapshot();buttons["work-military"].Visible=NewGameWork.Enabled(w)&&(militaryActions.Length>0||w.NewMilitary!=null);
        buttons["work-military"].Disabled=IsSaving||IsClockControlling;
        var v=NewGameMilitary.ViewWithAuthority(w);buttons["work-military"].Text=v.CanAccept?"軍務条件を確認・回答する":v.CanPickup?"許可済み・兵を引き受ける":w.NewMilitary!=null?"軍務の状況・次の行動":"軍務を相談・確認";
    }
    void MilitaryWorkDetails(NewMilitaryAction action)
    {
        var w=Snapshot();workDetails.Text=action switch{
            NewMilitaryAction.Request=>"申出だけでは兵の指揮は移りません。条件と許可を確認します。",
            NewMilitaryAction.Accept=>"回答を発送します。実際の許可を開封するまで兵は引き受けられません。",
            NewMilitaryAction.Pickup=>"許可を確認した兵の指揮を現地で引き受けます。所有は主家に残ります。",
            NewMilitaryAction.Load=>"主家の兵糧を携行します。本人の私財には入りません。",
            NewMilitaryAction.Unload=>"携行している主家兵糧の残りを現地で全量返します。",
            NewMilitaryAction.Report=>"自隊の記録を返報します。発送と相手の受領は別です。",
            NewMilitaryAction.Return=>"指揮返還の手続きを始めます。帰還・兵糧返納・返報の確認が必要です。",
            NewMilitaryAction.Inquire=>"許可・返還の状況を照会します。返書が届いてから内容を確認します。",
            _=>"部隊の行動は軍務の地図操作で指定します。"};
        if(action==NewMilitaryAction.Accept&&militaryWorkDeadline is {} due)workDetails.Text="回答期限：開始から"+due+"秒まで。許可の開封と兵の引受は別です。";
        foreach(var b in workChoices.GetChildren().OfType<Button>())if(buttons.TryGetValue("work-military-confirm",out var confirm)&&b==confirm)b.Text=MilitaryActionNameForActor(w,action);
        workSpeech.Text=MilitaryActionNameForActor(w,action)+"か、状況を確認しよう。";
    }
    void ShowMilitaryWork(EntityId? letter=null)
    {
        if(IsSaving||IsClockControlling)return;var debrief=letter is {} received?NewGameMilitary.Debriefs(Snapshot()).SingleOrDefault(d=>d.Notice==received):null;if(debrief!=null){ShowMilitaryDebrief(debrief.Case);return;}RefreshMilitary();shownWork=null;
        var w=Snapshot();var status=NewGameMilitary.ViewWithAuthority(w);militaryWorkDeadline=Branch(Session.PlayerControl).Knowledge.Messages.Where(k=>k.KnownBody!=null&&w.Messaging.Messages.Any(m=>m.Id==k.Id.Value&&m.Kind=="new-military-notice"&&w.NewMilitary!=null&&w.NewMilitary.Events.Any(e=>e.Message==m.Id))).Select(k=>ContractJson.Read<MilitaryNotice>(System.Text.Encoding.UTF8.GetBytes(k.KnownBody!))).Where(n=>n.Effect==MilitaryEffect.Offered).Select(n=>n.Deadline).DefaultIfEmpty(null).Max();EntityId other=Session.PlayerControl,speaker=Session.PlayerControl;
        string speech=status.Status,phase="軍務の判断";
        // A speaking issuer is shown only for a letter this person actually opened.
        if(letter is {} id){var known=Branch(Session.PlayerControl).Knowledge.Messages.Single(m=>m.Id==id);if(known.KnownBody==null)throw new InvalidOperationException("未開封の軍務書状です。");
            var m=w.Messaging.Messages.Single(m=>m.Id==id.Value);if(m.Kind!="new-military-notice")throw new InvalidOperationException("軍務通知ではありません。");
            if(w.NewMilitary?.Events.Any(e=>e.Message==m.Id)!=true){PresentWork("以前の主家の軍務書状","確認済みの書状を記録として見返します。現在の主家への返答ではありません。","",new(m.SenderId),Session.PlayerControl,"過去の書状","確認した記録");Choice("work-military-history-back","地図へ戻る",CloseBottomTalk);return;}
            var notice=ContractJson.Read<MilitaryNotice>(System.Text.Encoding.UTF8.GetBytes(known.KnownBody));other=speaker=new(m.SenderId);phase="確認済みの書状";
            speech=notice.Effect switch{MilitaryEffect.EngagementPermission=>EngagementLetterSpeech(w,notice),MilitaryEffect.EngagementRejected=>"その条件では交戦任務を許可できない。",MilitaryEffect.EngagementCancelled=>"先の交戦任務を取り消す。この書状の開封から攻撃許可は終了する。",MilitaryEffect.Offered=>"軍務の条件を示す。引き受けるなら返答してほしい。",MilitaryEffect.Permission=>"軍務を許可する。兵と兵糧の引受は現地で行うように。",MilitaryEffect.Returned=>"指揮の返還を確認した。この軍務は終了とする。",_=>"軍務への返答を届ける。確認した状況をもとに判断してほしい。"};
            if(speaker==Session.PlayerControl){phase="自家の軍務判断";speech=notice.Effect switch{MilitaryEffect.EngagementPermission=>EngagementLetterSpeech(w,notice),MilitaryEffect.EngagementRejected=>"交戦任務の条件を満たしていない。",MilitaryEffect.EngagementCancelled=>"交戦任務の取消を確定した。",MilitaryEffect.Offered=>"自家の軍務条件を確認。準備を確定しよう。",MilitaryEffect.Permission=>"自家の軍務の準備が整った。現地で兵と兵糧を引き受けよう。",MilitaryEffect.Returned=>"兵と兵糧の返納を確認。この軍務を終えよう。",_=>"自家の軍務の記録を確認しよう。"};}
        }
        PresentWork("軍務",speech,"",other,speaker,phase,"次の行動");workCardHeading.Text="兵・兵糧と私財は別";if(letter==null)workPhase.Text=status.Status;
        var supported=new[]{NewMilitaryAction.Request,NewMilitaryAction.Accept,NewMilitaryAction.Pickup,NewMilitaryAction.Load,NewMilitaryAction.Unload,NewMilitaryAction.Report,NewMilitaryAction.Return,NewMilitaryAction.Inquire};
        militaryWorkActions=militaryActions.Where(supported.Contains).ToArray();drawingMilitaryWork=true;militaryWorkChoice.Clear();foreach(var a in militaryWorkActions)militaryWorkChoice.AddItem(MilitaryActionNameForActor(w,a));militaryWorkChoice.Visible=militaryWorkActions.Length>1;
        if(militaryWorkActions.Length>0){militaryWorkChoice.Select(0);MilitaryWorkDetails(militaryWorkActions[0]);if(letter!=null)workSpeech.Text=speech;
            var expected=Session;long revision=Session.Revision;Choice("work-military-confirm",MilitaryActionNameForActor(w,militaryWorkActions[0]),()=>{if(!ReferenceEquals(Session,expected)||Session.Revision!=revision)throw new InvalidOperationException("進行が変わりました。軍務を確認し直してください。");int n=militaryWorkChoice.Selected;if(n<0||n>=militaryWorkActions.Length)return;var selected=militaryWorkActions[n];RefreshMilitary();if(!militaryActions.Contains(selected))throw new InvalidOperationException("軍務の状況が変わりました。確認し直してください。");CloseBottomTalk();ChooseMilitary(selected);operationTabs.CurrentTab=0;});}
        else workDetails.Text=status.Status;
        drawingMilitaryWork=false;Choice("work-military-map","部隊の地図操作へ",()=>{CloseBottomTalk();operationTabs.CurrentTab=1;});LayoutBottomTalk();
    }
    internal bool ReviewMilitaryInternalTalk=>workPhase.Text=="自家の軍務判断"&&ownWorkSpeaker&&!portraitPanels[1].Visible;
    internal void ReviewMilitaryWorkAction(NewMilitaryAction action){int n=Array.IndexOf(militaryWorkActions,action);if(n<0)throw new InvalidOperationException("会話で選べない軍務："+action);militaryWorkChoice.Select(n);militaryWorkChoice.EmitSignal(OptionButton.SignalName.ItemSelected,n);}
}
