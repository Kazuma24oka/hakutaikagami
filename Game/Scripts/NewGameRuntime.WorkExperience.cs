using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    ColorRect workBackdrop=null!;PanelContainer workOverlay=null!;Label workTitle=null!,workNames=null!,workSpeech=null!,workDetails=null!,workSummary=null!;
    VBoxContainer workChoices=null!;Label workPrompt=null!,workCardHeading=null!,workPhase=null!;PanelContainer workSpeechBox=null!;SubViewport talkView=null!;Node3D talkStage=null!;Label historyText=null!;PanelContainer historyOverlay=null!;
    readonly Dictionary<EntityId,Officer3DCustomActor> talkActors=new();
    readonly List<string> observedHistory=new();string lastObservedMessage="";Intent? consultation;EntityId? shownWork;bool workReadOnly;
    internal string WorkScreen=>workOverlay?.Visible==true?workTitle.Text:"closed";
    internal string WorkSummary=>workSummary.Text;
    internal bool WorkHistoryVisible=>historyOverlay.Visible;
    void BuildWorkExperience(HFlowContainer toolbar)
    {
        message.Visible=false;tasks.Visible=false;
        Add(toolbar,"history","履歴",ShowWorkHistory);
        var col=(VBoxContainer)buttons["work"].GetParent();
        workSummary=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(0,82)};col.AddChild(workSummary);col.MoveChild(workSummary,1);
        Add(col,"work-details","仕事の内容・報告を見る",()=>{if(unreadIds.Length>0)OpenSelectedMail();else ShowKnownWork();});Add(col,"work-run","点検を進める",()=>RequestManualAdvance(Session.Seconds+10));
        var workArea=new PanelContainer();workArea.AddThemeStyleboxOverride("panel",WorkBox("213038","465b65",14));col.AddChild(workArea);col.MoveChild(workArea,0);operationPanels[workArea]=0;
        var workStack=new VBoxContainer();workStack.AddThemeConstantOverride("separation",10);workArea.AddChild(workStack);workStack.AddChild(new Label{Text="仕事"});
        workSummary.Reparent(workStack);workSummary.CustomMinimumSize=new(0,46);
        foreach(var id in new[]{"work-details","work-run","work"}){buttons[id].Reparent(workStack);buttons[id].CustomMinimumSize=new(0,42);}
        col.GetChildren().OfType<Label>().First().Text="移動";
        workBackdrop=new(){Visible=false,Color=new Color(0,0,0,.85f),ZIndex=49};AddChild(workBackdrop);workBackdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        workOverlay=Overlay();
        var content=new VBoxContainer();content.AddThemeConstantOverride("separation",16);workOverlay.AddChild(content);
        var header=new HBoxContainer();content.AddChild(header);
        workPhase=new(){SizeFlagsHorizontal=SizeFlags.ExpandFill};workPhase.AddThemeColorOverride("font_color",new Color("bcae91"));header.AddChild(workPhase);
        Add(header,"work-close","閉じる",()=>{consultation=null;workOverlay.Hide();});buttons["work-back"]=buttons["work-close"];
        var columns=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};columns.AddThemeConstantOverride("separation",28);content.AddChild(columns);
        var visual=new VBoxContainer{CustomMinimumSize=new(330,0),SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsStretchRatio=.9f};columns.AddChild(visual);
        var scene=new SubViewportContainer{Stretch=true,CustomMinimumSize=new(310,340),SizeFlagsVertical=SizeFlags.ExpandFill};visual.AddChild(scene);
        talkView=new(){OwnWorld3D=true,Size=new(420,420),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};scene.AddChild(talkView);talkStage=new();talkView.AddChild(talkStage);
        talkStage.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("24333b"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("ffe9c9"),AmbientLightEnergy=.8f}});
        talkStage.AddChild(new DirectionalLight3D{RotationDegrees=new(-35,-25,0),LightEnergy=1.1f});
        talkStage.AddChild(new MeshInstance3D{Mesh=new PlaneMesh{Size=new(20,20)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("4a4338")}});
        var cam=new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal,Size=4.3f,Position=new(0,2.5f,5.5f)};talkStage.AddChild(cam);cam.LookAt(new(0,1,0));
        var decision=new VBoxContainer{CustomMinimumSize=new(420,0),SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsStretchRatio=1.15f};decision.AddThemeConstantOverride("separation",16);columns.AddChild(decision);
        workTitle=new();workTitle.AddThemeFontSizeOverride("font_size",26);decision.AddChild(workTitle);
        workSpeechBox=new();workSpeechBox.AddThemeStyleboxOverride("panel",WorkBox("23333e","526776",16));decision.AddChild(workSpeechBox);
        var speechCol=new VBoxContainer();speechCol.AddThemeConstantOverride("separation",9);workSpeechBox.AddChild(speechCol);
        workNames=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};workNames.AddThemeColorOverride("font_color",new Color("e7c78c"));speechCol.AddChild(workNames);
        workSpeech=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(0,78)};workSpeech.AddThemeFontSizeOverride("font_size",22);speechCol.AddChild(workSpeech);
        var card=new PanelContainer();card.AddThemeStyleboxOverride("panel",WorkBox("1c292f","46564f",16));decision.AddChild(card);
        var cardCol=new VBoxContainer();cardCol.AddThemeConstantOverride("separation",12);card.AddChild(cardCol);
        workCardHeading=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};workCardHeading.AddThemeFontSizeOverride("font_size",19);cardCol.AddChild(workCardHeading);
        workDetails=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};cardCol.AddChild(workDetails);
        workPrompt=new();workPrompt.AddThemeColorOverride("font_color",new Color("e7c78c"));decision.AddChild(workPrompt);
        workChoices=new();workChoices.AddThemeConstantOverride("separation",10);decision.AddChild(workChoices);
        decision.AddChild(new Control{SizeFlagsVertical=SizeFlags.ExpandFill});
        historyOverlay=Overlay();var historyCol=new VBoxContainer();historyOverlay.AddChild(historyCol);historyCol.AddChild(new Label{Text="履歴 — 本人が確認した用件・書状"});var scroll=new ScrollContainer{SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};historyCol.AddChild(scroll);historyText=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(historyText);var close=new Button{Text="閉じる"};historyCol.AddChild(close);close.Pressed+=()=>{historyOverlay.Hide();RefreshWorkExperience();};
    }
    PanelContainer Overlay()
    {
        var p=new PanelContainer{Visible=false,ZIndex=50};AddChild(p);p.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);p.OffsetLeft=28;p.OffsetTop=28;p.OffsetRight=-28;p.OffsetBottom=-28;
        var style=new StyleBoxFlat{BgColor=new Color("19252d"),BorderColor=new Color("b69b66"),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,ContentMarginLeft=24,ContentMarginRight=24,ContentMarginTop=18,ContentMarginBottom=18};p.AddThemeStyleboxOverride("panel",style);return p;
    }
    static StyleBoxFlat WorkBox(string background,string border,int padding)=>new(){BgColor=new Color(background),BorderColor=new Color(border),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6,ContentMarginLeft=padding,ContentMarginRight=padding,ContentMarginTop=padding,ContentMarginBottom=padding};
    string PersonName(EntityId id)=>Snapshot().Definitions.Characters.Single(p=>p.Id==id).Name;
    VisibleTask? KnownWork()=>Branch(Session.PlayerControl).Knowledge.Tasks.LastOrDefault(t=>t.Executor==Session.PlayerControl&&t.Issuer!=Session.PlayerControl&&Snapshot().Tasks.Single(p=>p.Id==t.Id).Action==ActionKind.Inspect);
    void TalkPeople(EntityId other,bool present)
    {
        foreach(var a in talkActors.Values)a.Visible=false;
        var w=Snapshot();var ids=present?new[]{w.PlayerControl,other}:new[]{w.PlayerControl};
        for(int n=0;n<ids.Length;n++){if(!talkActors.TryGetValue(ids[n],out var actor)){actor=new();talkStage.AddChild(actor);talkActors.Add(ids[n],actor);}actor.Visible=true;actor.Position=n==0?new(-.75f,0,.5f):new(.65f,0,-.45f);actor.RotationDegrees=new(0,n==0?155:-25,0);actor.Apply(ContractJson.Read<Officer3DCustomLook>(ContractJson.Write(NewGameWorld.Look(w.NewGame!,ids[n].Value))));actor.Freeze("idle",0);}

    }
    void Choice(string id,string text,Action action){Add(workChoices,id,text,action);var button=buttons[id];button.CustomMinimumSize=new(0,54);button.Alignment=HorizontalAlignment.Left;button.AddThemeFontSizeOverride("font_size",20);button.AddThemeStyleboxOverride("normal",WorkBox("3e382a","bda068",14));button.AddThemeStyleboxOverride("hover",WorkBox("655338","eed39c",14));button.AddThemeStyleboxOverride("focus",WorkBox("655338","eed39c",14));}
    void ClearChoices(){foreach(Node n in workChoices.GetChildren()){workChoices.RemoveChild(n);n.QueueFree();}}
    void PresentWork(string title,string speech,string details,EntityId other,EntityId speaker,string phase,string prompt)
    {
        workReadOnly=true;ClearChoices();workTitle.Text=title;workSpeech.Text=speech;workDetails.Text=details;workPhase.Text=phase;workPrompt.Text=prompt;
        bool present=NearbyDisplay().Any(p=>p.Actor==other)&&Snapshot().Locations.Single(p=>p.Actor==other).Site==Branch(Session.PlayerControl).Knowledge.Location.Site;
        TalkPeople(other,present);workNames.Text=PersonName(speaker)+(speaker==Session.PlayerControl?"  ／  あなた":present?"  ／  相手の話":"  ／  書状");
        workOverlay.Show();
    }
    string WorkTerms(EntityId scope,long? deadline,bool consultationPreview=false)
    {
        string due=deadline is {} at?consultationPreview?"受取手続きから120秒以内":Math.Max(0,at-Session.Seconds)+"秒以内":"未確認";
        return "場所　　"+NewGameWorld.SiteName(scope.Value)+"\n期限　　"+due+"\n報酬　　提示なし";
    }
    void ShowWorkConsultation()
    {
        if(IsSaving||IsClockControlling)return;
        consultation=NewGameWorld.LocalWork(Snapshot(),"request:local-work/"+Session.Revision);var i=consultation;shownWork=null;
        PresentWork("現地の点検", "この地の様子を点検して、結果を知らせてほしい。",WorkTerms(i.Scope,i.Deadline.At,true),i.Actor,i.Actor,"相談  /  用件を受け取る前","あなたの返答");
        workCardHeading.Text=i.Kind==TaskKind.BindingOrder?"主君からの用件":"依頼の要点";
        workReadOnly=false;Choice("work-confirm","内容を確認しよう",()=>{if(!workOverlay.Visible||consultation==null)return;var fresh=NewGameWorld.LocalWork(Snapshot(),"request:local-work/"+Session.Revision);if(fresh.Actor!=i.Actor||fresh.Scope!=i.Scope||fresh.Kind!=i.Kind)throw new InvalidOperationException("相談相手や状況が変わりました。相談し直してください。");workOverlay.Hide();consultation=null;Submit(fresh);});
        Choice("work-back-choice","今は見送ろう",()=>{consultation=null;workOverlay.Hide();});
    }
    void ShowKnownWork()
    {
        var t=KnownWork();if(t==null){message.Text="開封して確認した仕事はまだありません。";return;}shownWork=t.Id;RenderKnownWork(t);
    }
    void RenderKnownWork(VisibleTask t)
    {
        bool done=t.KnownStatus.Contains("Completed");bool awaiting=Awaiting()?.Id==t.Id;bool running=t.KnownStatus.Contains("Running");var task=Snapshot().Tasks.Single(p=>p.Id==t.Id);
        if(running){workOverlay.Hide();operationTabs.CurrentTab=0;return;}
        if(done){
            var report=Snapshot().Outbox.SingleOrDefault(o=>o.Task==t.Id&&o.Sender==Session.PlayerControl&&o.Kind=="contract-report");
            PresentWork("点検の報告","点検を終えました。結果をお送りします。","点検　　完了\n返報　　"+(report?.Sent==true?"発送済み":"発送待ち")+"\n相手の確認　未確認",t.Issuer,Session.PlayerControl,"報告  /  仕事の結果","次の行動");
            workCardHeading.Text=NewGameWorld.SiteName(task.Scope.Value)+"の点検結果";Choice("work-finish","報告を終えて地図へ戻る",()=>workOverlay.Hide());return;
        }
        if(awaiting){
            PresentWork("点検を引き受けますか","この地を点検してくれないか。引き受けてもらえるだろうか。",WorkTerms(task.Scope,t.DeadlineAt),t.Issuer,t.Issuer,"引受  /  返答すると仕事が始まります","あなたの返答");
            workCardHeading.Text="依頼の要点";Choice("work-assent","引き受けよう",()=>{Assent(true);ShowKnownWork();});Choice("work-refuse","今回は断ろう",()=>{Assent(false);ShowKnownWork();});return;
        }
        bool declined=task.Agreement==AgreementPhase.Declined;
        PresentWork(declined?"依頼を辞退しました":"仕事の確認",declined?"今回は引き受けないことにしました。":"この仕事の状況を確認しよう。","状態　　"+ReadableStatus(t.KnownStatus),t.Issuer,Session.PlayerControl,"確認","次の行動");
        workCardHeading.Text=NewGameWorld.SiteName(task.Scope.Value)+"の点検";Choice("work-finish","地図へ戻る",()=>workOverlay.Hide());
    }
    static string ReadableStatus(string text)=>text.Replace("Completed","完了").Replace("Running","進行中").Replace("NotStarted","未着手").Replace("Stopped","中断").Replace("Failed","失敗").Replace("Waiting","待機");
    void RefreshWorkExperience()
    {
        if(workSummary==null)return;workBackdrop.Visible=workOverlay.Visible||historyOverlay.Visible;
        message.Visible=message.Text.StartsWith("操作できません：")||message.Text.Contains("確定できませんでした")||message.Text.Contains("保存できません");
        if(message.Text!=lastObservedMessage){lastObservedMessage=message.Text;if(!string.IsNullOrWhiteSpace(lastObservedMessage)){observedHistory.Add("開始から"+Session.Seconds+"秒　"+lastObservedMessage);if(observedHistory.Count>200)observedHistory.RemoveAt(0);}}
        var t=KnownWork();int unread=Branch(Session.PlayerControl).Knowledge.Messages.Count(m=>m.KnownBody==null);
        workSummary.Text=IsSaving?"保存中です。完了を待ってください。":IsClockControlling?"操作を確認中です。停止で取り消せます。":unread>0?"書状が届きました（"+unread+"通）":t==null?"現地で仕事を相談できます。\n相手と内容を確認してから進めます。":t.KnownStatus.Contains("Completed")?"点検完了\n次は、結果を報告する。":t.KnownStatus.Contains("Running")?"進行中："+NewGameWorld.SiteName(Snapshot().Tasks.Single(p=>p.Id==t.Id).Scope.Value)+"の点検\n期限：残り"+Math.Max(0,(t.DeadlineAt??Session.Seconds)-Session.Seconds)+"秒":"仕事："+ReadableStatus(t.KnownStatus);
        buttons["work-details"].Disabled=t==null&&unread==0;buttons["work-details"].Text=unread>0?"届いた書状を開く":t?.KnownStatus.Contains("Completed")==true?"点検の結果を報告する":"仕事の内容を確認";
        bool running=t?.KnownStatus.Contains("Running")==true;
        buttons["work-details"].Visible=!running&&(t!=null||unread>0);buttons["work"].Visible=t==null||t.KnownStatus.Contains("Completed")||t.KnownStatus.Contains("Stopped")||t.KnownStatus.Contains("Failed");
        buttons["work-run"].Visible=running;buttons["work-run"].Disabled=IsClockControlling||IsSaving;
        buttons["accept"].Visible=buttons["decline"].Visible=false;
        if(workOverlay.Visible&&workReadOnly&&shownWork is {} id){var current=Branch(Session.PlayerControl).Knowledge.Tasks.SingleOrDefault(p=>p.Id==id);if(current!=null&&((current.KnownStatus.Contains("Completed")&&workTitle.Text!="点検の報告")||(Awaiting()?.Id==id&&workTitle.Text!="点検を引き受けますか")))RenderKnownWork(current);}
    }
    internal bool ReviewRunningWorkLayout()
    {
        var action=buttons["work-run"];var summary=workSummary.GetGlobalRect();var rect=action.GetGlobalRect();
        return !workOverlay.Visible&&action.Visible&&action.GetParent()==workSummary.GetParent()&&rect.Position.Y>=summary.End.Y&&rect.Position.Y-summary.End.Y<60&&rect.End.Y<=GetViewportRect().Size.Y;
    }
    internal bool ReviewWorkLayout()
    {
        if(!workOverlay.Visible)return false;
        var screen=GetViewportRect();var scene=((Control)talkView.GetParent()).GetGlobalRect();var speech=workSpeechBox.GetGlobalRect();var detail=workDetails.GetGlobalRect();var prompt=workPrompt.GetGlobalRect();var choices=workChoices.GetChildren().OfType<Button>().ToArray();
        if(scene.End.X+12>speech.Position.X||speech.End.Y>detail.Position.Y||detail.End.Y>prompt.Position.Y||choices.Length==0)return false;
        float bottom=prompt.End.Y;
        foreach(var button in choices){var rect=button.GetGlobalRect();if(rect.Position.Y<bottom||rect.Size.Y<50||rect.End.Y>screen.End.Y-28||rect.End.X>screen.End.X-28)return false;bottom=rect.End.Y;}
        return workSpeech.GetLineCount()*workSpeech.GetLineHeight()<=workSpeech.Size.Y+1&&workDetails.GetLineCount()*workDetails.GetLineHeight()<=workDetails.Size.Y+1;
    }
    object? WorkPose()=>workOverlay?.Visible!=true?(object)new{Mode="map",Summary=workSummary.Text,RunVisible=buttons["work-run"].Visible,DetailsVisible=buttons["work-details"].Visible,DetailsText=buttons["work-details"].Text}:new{SceneRect=new[]{((Control)talkView.GetParent()).GetGlobalRect().Position.X,((Control)talkView.GetParent()).GetGlobalRect().Position.Y,((Control)talkView.GetParent()).Size.X,((Control)talkView.GetParent()).Size.Y},Viewport=new[]{talkView.Size.X,talkView.Size.Y},Camera=V(talkStage.GetChildren().OfType<Camera3D>().Single().Position),Title=workTitle.Text,Phase=workPhase.Text,Prompt=workPrompt.Text,Card=workCardHeading.Text,Choices=workChoices.GetChildren().OfType<Button>().Select(b=>b.Text),Names=workNames.Text,Speech=workSpeech.Text,Details=workDetails.Text,Task=shownWork,Actors=talkStage.GetChildren().OfType<Officer3DCustomActor>().Where(a=>a.Visible).Select(a=>new{Position=V(a.Position),Rotation=V(a.Rotation),Look=a.Appearance,Bones=Enumerable.Range(0,a.Skeleton.GetBoneCount()).Select(i=>{var b=a.Skeleton.GetBoneGlobalPose(i);return new{Origin=V(b.Origin),Basis=new[]{V(b.Basis.X),V(b.Basis.Y),V(b.Basis.Z)}};})})};
    void ShowWorkHistory()
    {
        var k=Branch(Session.PlayerControl).Knowledge;
        historyText.Text="確認済みの用件\n"+string.Join("\n",k.Tasks.Select(t=>PersonName(t.Issuer)+" → "+PersonName(t.Executor)+"　"+ReadableStatus(t.KnownStatus)))+"\n\n届いた書状\n"+string.Join("\n",k.Messages.Select(m=>PersonName(new(m.Sender))+"："+(m.KnownBody==null?"未開封・内容未確認":m.KnownBody.StartsWith("task:")?"用件を確認済み":m.KnownBody)))+"\n\nこの画面での操作履歴（最大200件）\n"+string.Join("\n",observedHistory);historyOverlay.Show();
    }
}
