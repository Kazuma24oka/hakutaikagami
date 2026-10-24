using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    PanelContainer workOverlay=null!;Label workTitle=null!,workNames=null!,workSpeech=null!,workDetails=null!,workSummary=null!;
    HFlowContainer workChoices=null!;SubViewport talkView=null!;Node3D talkStage=null!;Label historyText=null!;PanelContainer historyOverlay=null!;
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
        Add(col,"work-details","仕事の内容・報告を見る",ShowKnownWork);
        workOverlay=Overlay();var content=new VBoxContainer();workOverlay.AddChild(content);
        workTitle=new(){ThemeTypeVariation="HeaderLarge"};workTitle.AddThemeFontSizeOverride("font_size",24);content.AddChild(workTitle);
        var scene=new SubViewportContainer{Stretch=true,CustomMinimumSize=new(0,285),SizeFlagsVertical=SizeFlags.ExpandFill};content.AddChild(scene);
        talkView=new(){OwnWorld3D=true,Size=new(900,310),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};scene.AddChild(talkView);talkStage=new();talkView.AddChild(talkStage);
        talkStage.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("25343c"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("ffe9c9"),AmbientLightEnergy=.8f}});
        talkStage.AddChild(new DirectionalLight3D{RotationDegrees=new(-35,-25,0),LightEnergy=1.1f});
        var floor=new MeshInstance3D{Mesh=new PlaneMesh{Size=new(20,20)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("554738")}};talkStage.AddChild(floor);
        var cam=new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal,Size=4.3f,Position=new(0,2.5f,5.5f)};talkStage.AddChild(cam);cam.LookAt(new(0,1,0));
        workNames=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};workNames.AddThemeColorOverride("font_color",new Color("e7c78c"));content.AddChild(workNames);
        workSpeech=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};workSpeech.AddThemeFontSizeOverride("font_size",21);content.AddChild(workSpeech);
        workDetails=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};content.AddChild(workDetails);workChoices=new();content.AddChild(workChoices);
        historyOverlay=Overlay();var historyCol=new VBoxContainer();historyOverlay.AddChild(historyCol);historyCol.AddChild(new Label{Text="履歴 — 本人が確認した用件・書状"});var scroll=new ScrollContainer{SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};historyCol.AddChild(scroll);historyText=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(historyText);var close=new Button{Text="閉じる"};historyCol.AddChild(close);close.Pressed+=()=>historyOverlay.Hide();
    }
    PanelContainer Overlay()
    {
        var p=new PanelContainer{Visible=false,ZIndex=50};AddChild(p);p.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);p.OffsetLeft=60;p.OffsetTop=85;p.OffsetRight=-60;p.OffsetBottom=-55;
        var style=new StyleBoxFlat{BgColor=new Color("19252d"),BorderColor=new Color("b69b66"),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,ContentMarginLeft=24,ContentMarginRight=24,ContentMarginTop=18,ContentMarginBottom=18};p.AddThemeStyleboxOverride("panel",style);return p;
    }
    string PersonName(EntityId id)=>Snapshot().Definitions.Characters.Single(p=>p.Id==id).Name;
    VisibleTask? KnownWork()=>Branch(Session.PlayerControl).Knowledge.Tasks.LastOrDefault(t=>t.Executor==Session.PlayerControl&&t.Issuer!=Session.PlayerControl&&Snapshot().Tasks.Single(p=>p.Id==t.Id).Action==ActionKind.Inspect);
    void TalkPeople(EntityId other,bool present)
    {
        foreach(var a in talkActors.Values)a.Visible=false;
        var w=Snapshot();var ids=present?new[]{w.PlayerControl,other}:new[]{w.PlayerControl};
        for(int n=0;n<ids.Length;n++){if(!talkActors.TryGetValue(ids[n],out var actor)){actor=new();talkStage.AddChild(actor);talkActors.Add(ids[n],actor);}actor.Visible=true;actor.Position=n==0?new(-1.1f,0,.65f):new(1.0f,0,-.4f);actor.RotationDegrees=new(0,n==0?155:-25,0);actor.Apply(ContractJson.Read<Officer3DCustomLook>(ContractJson.Write(NewGameWorld.Look(w.NewGame!,ids[n].Value))));actor.Freeze("idle",0);}
        workNames.Text=PersonName(w.PlayerControl)+"（左手前）　／　"+PersonName(other)+(present?"（右奥）":"（書状の相手・この場には表示しません）");
    }
    void Choice(string id,string text,Action action){Add(workChoices,id,text,action);}
    void ClearChoices(){foreach(Node n in workChoices.GetChildren()){workChoices.RemoveChild(n);n.QueueFree();}}
    void PresentWork(string title,string speech,string details,EntityId other)
    {
        workReadOnly=true;ClearChoices();workTitle.Text=title;workSpeech.Text=speech;workDetails.Text=details;bool present=NearbyDisplay().Any(p=>p.Actor==other)&&Snapshot().Locations.Single(p=>p.Actor==other).Site==Branch(Session.PlayerControl).Knowledge.Location.Site;TalkPeople(other,present);workOverlay.Show();
    }
    void ShowWorkConsultation()
    {
        if(IsSaving||IsClockControlling)return;
        consultation=NewGameWorld.LocalWork(Snapshot(),"request:local-work/"+Session.Revision);var i=consultation;shownWork=null;
        PresentWork("仕事の相談",PersonName(i.Actor)+"：「この地の様子を点検して、結果を知らせてほしい。」", "対象："+NewGameWorld.SiteName(i.Scope.Value)+"の点検\n行うこと：正式な用件を受け取り、開封してから点検。終わると返報を送ります。\n"+(i.Kind==TaskKind.BindingOrder?"現在の主従に基づく命令です。正式受領後は任意依頼の辞退とは扱いが異なります。":"任意の依頼です。正式な内容を開封した後も引受・辞退を選べます。")+"\n引受後は「点検を進める」で時間を進められます。金銭・能力上昇の報酬は未設定。",i.Actor);
        workReadOnly=false;Choice("work-confirm","この仕事を進める",()=>{if(!workOverlay.Visible||consultation==null)return;var fresh=NewGameWorld.LocalWork(Snapshot(),"request:local-work/"+Session.Revision);if(fresh.Actor!=i.Actor||fresh.Scope!=i.Scope||fresh.Kind!=i.Kind)throw new InvalidOperationException("相談相手や状況が変わりました。相談し直してください。");workOverlay.Hide();consultation=null;Submit(fresh);});Choice("work-back","今は相談を終える",()=>{consultation=null;workOverlay.Hide();});
    }
    void ShowKnownWork()
    {
        var t=KnownWork();if(t==null){message.Text="開封して確認した仕事はまだありません。";return;}shownWork=t.Id;RenderKnownWork(t);
    }
    void RenderKnownWork(VisibleTask t)
    {
        bool done=t.KnownStatus.Contains("Completed");bool awaiting=Awaiting()?.Id==t.Id;bool running=t.KnownStatus.Contains("Running");
        string status=done?"点検完了・返報":awaiting?"仕事を引き受けますか":running?"点検を実行中":"仕事の内容";
        string speech=done?PersonName(Session.PlayerControl)+"：「点検を終えました。結果を返報します。」":awaiting?PersonName(t.Issuer)+"：「この地を点検してくれないか。引き受けてもらえるだろうか。」":running?PersonName(Session.PlayerControl)+"：「現地を確認しています。終わり次第、結果を知らせます。」":"確認した用件の状態を表示しています。";
        PresentWork(status,speech,"対象："+NewGameWorld.SiteName(Snapshot().Tasks.Single(p=>p.Id==t.Id).Scope.Value)+"の点検\n"+(done?"成果：本人の点検が完了。既存の仕組みで返報が発送されます。相手の受領・開封は別です。":running?"行動：現地の点検中。時間を進めると作業が進みます。":awaiting?"選択：引き受ける／断る。回答前に仕事は開始しません。":"状態："+ReadableStatus(t.KnownStatus))+"\n"+(t.DeadlineAt is {} due?"期限：開始から"+due+"秒まで":"期限：未確認")+"\n報酬や相手の評価は、この試作ではまだ設定していません。",t.Issuer);
        if(awaiting){Choice("work-assent","引き受ける",()=>{Assent(true);ShowKnownWork();});Choice("work-refuse","今回は断る",()=>{Assent(false);ShowKnownWork();});}
        else if(running)Choice("work-run","点検を進める（最大10秒）",()=>{workOverlay.Hide();RequestManualAdvance(Session.Seconds+10);});
        Choice("work-close",done?"地図へ戻る":"閉じる",()=>workOverlay.Hide());
    }
    static string ReadableStatus(string text)=>text.Replace("Completed","完了").Replace("Running","進行中").Replace("NotStarted","未着手").Replace("Stopped","中断").Replace("Failed","失敗").Replace("Waiting","待機");
    void RefreshWorkExperience()
    {
        if(workSummary==null)return;
        message.Visible=message.Text.StartsWith("操作できません：")||message.Text.Contains("確定できませんでした")||message.Text.Contains("保存できません");
        if(message.Text!=lastObservedMessage){lastObservedMessage=message.Text;if(!string.IsNullOrWhiteSpace(lastObservedMessage)){observedHistory.Add("開始から"+Session.Seconds+"秒　"+lastObservedMessage);if(observedHistory.Count>200)observedHistory.RemoveAt(0);}}
        var t=KnownWork();int unread=Branch(Session.PlayerControl).Knowledge.Messages.Count(m=>m.KnownBody==null);
        workSummary.Text=IsSaving?"保存中です。完了を待ってください。":IsClockControlling?"操作を確認中です。停止で取り消せます。":unread>0?"書状が届いています（"+unread+"通）\n「書状・仕官」で開封して内容を確認。":t==null?"現地で仕事を相談できます。\n相手と内容を確認してから進めます。":t.KnownStatus.Contains("Completed")?"点検が終わりました。\n「仕事の内容・報告を見る」で結果を確認。":t.KnownStatus.Contains("Running")?"現地の点検中\n時間を進めると作業が進みます。":"仕事："+ReadableStatus(t.KnownStatus);
        buttons["work-details"].Disabled=t==null;
        if(workOverlay.Visible&&workReadOnly&&shownWork is {} id){var current=Branch(Session.PlayerControl).Knowledge.Tasks.SingleOrDefault(p=>p.Id==id);if(current!=null&&((current.KnownStatus.Contains("Completed")&&workTitle.Text!="点検完了・返報")||(Awaiting()?.Id==id&&workTitle.Text!="仕事を引き受けますか")))RenderKnownWork(current);}
    }
    object? WorkPose()=>workOverlay?.Visible!=true?null:new{SceneRect=new[]{((Control)talkView.GetParent()).GetGlobalRect().Position.X,((Control)talkView.GetParent()).GetGlobalRect().Position.Y,((Control)talkView.GetParent()).Size.X,((Control)talkView.GetParent()).Size.Y},Viewport=new[]{talkView.Size.X,talkView.Size.Y},Camera=V(talkStage.GetChildren().OfType<Camera3D>().Single().Position),Title=workTitle.Text,Names=workNames.Text,Speech=workSpeech.Text,Details=workDetails.Text,Task=shownWork,Actors=talkStage.GetChildren().OfType<Officer3DCustomActor>().Where(a=>a.Visible).Select(a=>new{Position=V(a.Position),Rotation=V(a.Rotation),Look=a.Appearance,Bones=Enumerable.Range(0,a.Skeleton.GetBoneCount()).Select(i=>{var b=a.Skeleton.GetBoneGlobalPose(i);return new{Origin=V(b.Origin),Basis=new[]{V(b.Basis.X),V(b.Basis.Y),V(b.Basis.Z)}};})})};
    void ShowWorkHistory()
    {
        var k=Branch(Session.PlayerControl).Knowledge;
        historyText.Text="確認済みの用件\n"+string.Join("\n",k.Tasks.Select(t=>PersonName(t.Issuer)+" → "+PersonName(t.Executor)+"　"+ReadableStatus(t.KnownStatus)))+"\n\n届いた書状\n"+string.Join("\n",k.Messages.Select(m=>PersonName(new(m.Sender))+"："+(m.KnownBody==null?"未開封・内容未確認":m.KnownBody.StartsWith("task:")?"用件を確認済み":m.KnownBody)))+"\n\nこの画面での操作履歴（最大200件）\n"+string.Join("\n",observedHistory);historyOverlay.Show();
    }
}
