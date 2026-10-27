using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    OptionButton workReplyChoice=null!;EntityId[] workReplyIds=[];bool renderingWorkReply;OptionButton findingRoad=null!;InspectedRoad[] shownRoads=[];PanelContainer workDecision=null!;PanelContainer[] portraitPanels=[];SubViewport[] portraitViews=[];Node3D[] portraitStages=[];bool workDecisionOpen,ownWorkSpeaker;Action? pendingWorkReply;
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
        Add(toolbar,"trade","米の取引",ShowRegionalTrade);Add(toolbar,"history","履歴",ShowWorkHistory);Add(toolbar,"economy","地域の帳簿",ShowLocalEconomy);Add(toolbar,"companions","同行者・配下",ShowCompanions);Add(toolbar,"truce-negotiation","不戦交渉",ShowTruceNegotiation);Add(toolbar,"private-contact","私的な情報交換",ShowPrivateContact);Add(toolbar,"secret-cooperation","秘密の協力",ShowSecretCooperation);
        var col=(VBoxContainer)buttons["work"].GetParent();
        workSummary=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(0,82)};col.AddChild(workSummary);col.MoveChild(workSummary,1);
        Add(col,"work-receipts","返答・未受領の報酬",()=>ShowWorkAssessment());Add(col,"work-self","自分で陸路を調べる（報酬なし）",()=>Submit(NewGameWorld.Inspect(Snapshot(),"request:inspection/"+Session.Revision)));
        buttons["inspect"].Hide();buttons["inspect"]=buttons["work-self"];Add(col,"work-retry","不達と分かった返報を再送",RetryKnownWorkLetter);
        Add(col,"work-details","仕事の内容・報告を見る",()=>{if(unreadIds.Length>0)OpenSelectedMail();else ShowKnownWork();});Add(col,"work-run","点検を進める",()=>RequestManualAdvance(Session.Seconds+10));
        var workArea=new PanelContainer();workArea.AddThemeStyleboxOverride("panel",WorkBox("213038","465b65",14));col.AddChild(workArea);col.MoveChild(workArea,0);operationPanels[workArea]=0;
        var workStack=new VBoxContainer();workStack.AddThemeConstantOverride("separation",10);workArea.AddChild(workStack);workStack.AddChild(new Label{Text="仕事"});
        workSummary.Reparent(workStack);workSummary.CustomMinimumSize=new(0,46);
        foreach(var id in new[]{"work-details","work-run","work-receipts","work-retry","work","work-self"}){buttons[id].Reparent(workStack);buttons[id].CustomMinimumSize=new(0,42);}
        col.GetChildren().OfType<Label>().First().Text="移動";
        workBackdrop=new(){Visible=false,Color=new Color(0,0,0,.85f),ZIndex=49};AddChild(workBackdrop);workBackdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        workOverlay=new(){Visible=false,ZIndex=50};AddChild(workOverlay);workOverlay.AddThemeStyleboxOverride("panel",WorkBox("17252f","a28b61",12));
        var band=new HBoxContainer();band.AddThemeConstantOverride("separation",14);workOverlay.AddChild(band);
        var left=BuildTalkPortrait();band.AddChild(left.Panel);
        var words=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};words.AddThemeConstantOverride("separation",6);band.AddChild(words);
        var header=new HBoxContainer();words.AddChild(header);
        workNames=new(){SizeFlagsHorizontal=SizeFlags.ExpandFill};workNames.AddThemeColorOverride("font_color",new Color("efce92"));workNames.AddThemeFontSizeOverride("font_size",18);header.AddChild(workNames);
        workTitle=new();workTitle.AddThemeFontSizeOverride("font_size",14);workTitle.AddThemeColorOverride("font_color",new Color("a8b7be"));header.AddChild(workTitle);
        Add(header,"work-close","×",CloseBottomTalk);buttons["work-back"]=buttons["work-close"];
        workSpeech=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsVertical=SizeFlags.ExpandFill};workSpeech.AddThemeFontSizeOverride("font_size",21);words.AddChild(workSpeech);
        var footer=new HBoxContainer();words.AddChild(footer);workPhase=new(){SizeFlagsHorizontal=SizeFlags.ExpandFill};workPhase.AddThemeColorOverride("font_color",new Color("99aeb9"));footer.AddChild(workPhase);
        Add(footer,"work-reply","返答を選ぶ",AdvanceBottomTalk);buttons["work-reply"].CustomMinimumSize=new(130,32);
        var right=BuildTalkPortrait();band.AddChild(right.Panel);portraitPanels=[left.Panel,right.Panel];portraitViews=[left.View,right.View];portraitStages=[left.Stage,right.Stage];talkView=left.View;talkStage=left.Stage;workSpeechBox=workOverlay;
        workDecision=new(){Visible=false,ZIndex=51};AddChild(workDecision);workDecision.AddThemeStyleboxOverride("panel",WorkBox("1d2b33","a28b61",10));
        var decision=new VBoxContainer();decision.AddThemeConstantOverride("separation",6);workDecision.AddChild(decision);
        var caption=new HBoxContainer();decision.AddChild(caption);workPrompt=new(){SizeFlagsHorizontal=SizeFlags.ExpandFill};workPrompt.AddThemeColorOverride("font_color",new Color("efce92"));caption.AddChild(workPrompt);
        workCardHeading=new();workCardHeading.AddThemeColorOverride("font_color",new Color("a8b7be"));caption.AddChild(workCardHeading);
        workDetails=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};decision.AddChild(workDetails);
        findingRoad=new(){Visible=false,CustomMinimumSize=new(0,36)};decision.AddChild(findingRoad);
        workReplyChoice=new(){Visible=false,CustomMinimumSize=new(0,36)};decision.AddChild(workReplyChoice);workReplyChoice.ItemSelected+=n=>{if(!renderingWorkReply&&n>=0&&n<workReplyIds.Length)ShowWorkAssessment(workReplyIds[n]);};
        workChoices=new();workChoices.AddThemeConstantOverride("separation",6);decision.AddChild(workChoices);
        BuildMilitaryWork(workStack);BuildSupplyWork(workStack);BuildTransportWork(workStack);BuildWorkDuty(workStack);BuildWorkRelations(workStack);Resized+=LayoutBottomTalk;LayoutBottomTalk();
        historyOverlay=Overlay();var historyCol=new VBoxContainer();historyOverlay.AddChild(historyCol);historyCol.AddChild(new Label{Text="履歴 — 本人が確認した用件・書状"});var scroll=new ScrollContainer{SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};historyCol.AddChild(scroll);historyText=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(historyText);var close=new Button{Text="閉じる"};historyCol.AddChild(close);close.Pressed+=()=>{historyOverlay.Hide();RefreshWorkExperience();};
    }
    PanelContainer Overlay()
    {
        var p=new PanelContainer{Visible=false,ZIndex=50};AddChild(p);p.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);p.OffsetLeft=28;p.OffsetTop=28;p.OffsetRight=-28;p.OffsetBottom=-28;
        var style=new StyleBoxFlat{BgColor=new Color("19252d"),BorderColor=new Color("b69b66"),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,ContentMarginLeft=24,ContentMarginRight=24,ContentMarginTop=18,ContentMarginBottom=18};p.AddThemeStyleboxOverride("panel",style);return p;
    }
    static StyleBoxFlat WorkBox(string background,string border,int padding)=>new(){BgColor=new Color(background),BorderColor=new Color(border),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6,ContentMarginLeft=padding,ContentMarginRight=padding,ContentMarginTop=padding,ContentMarginBottom=padding};
    string PersonName(EntityId id)=>Snapshot().Definitions.Characters.Single(p=>p.Id==id).Name;
    VisibleTask? KnownWork()=>Branch(Session.PlayerControl).Knowledge.Tasks.LastOrDefault(t=>t.Executor==Session.PlayerControl&&Snapshot().Tasks.Single(p=>p.Id==t.Id).Action==ActionKind.Inspect);
    (PanelContainer Panel,SubViewport View,Node3D Stage) BuildTalkPortrait()
    {
        var panel=new PanelContainer{CustomMinimumSize=new(94,106),SizeFlagsVertical=SizeFlags.ShrinkCenter};panel.AddThemeStyleboxOverride("panel",WorkBox("22323c","89754e",2));
        var holder=new SubViewportContainer{Stretch=true,CustomMinimumSize=new(88,100)};panel.AddChild(holder);
        var viewport=new SubViewport{OwnWorld3D=true,Size=new(112,128),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};holder.AddChild(viewport);var stage=new Node3D();viewport.AddChild(stage);
        stage.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("24333b"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("ffe9c9"),AmbientLightEnergy=.8f}});
        stage.AddChild(new DirectionalLight3D{RotationDegrees=new(-35,-25,0),LightEnergy=1.1f});
        var camera=new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal,Size=.65f,Position=new(0,1.42f,3)};stage.AddChild(camera);return(panel,viewport,stage);
    }
    void LayoutBottomTalk()
    {
        if(workOverlay==null||workDecision==null)return;float height=Math.Clamp(Size.Y*.22f,154,190);float width=Math.Min(Size.X-36,Math.Max(580,Size.X*.56f));
        workOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);workOverlay.OffsetLeft=18;workOverlay.OffsetRight=-18;workOverlay.OffsetTop=-height-18;workOverlay.OffsetBottom=-18;
        workDecision.AnchorLeft=workDecision.AnchorRight=workDecision.AnchorTop=workDecision.AnchorBottom=1;workDecision.OffsetLeft=-width-18;workDecision.OffsetRight=-18;workDecision.OffsetTop=-height-18-8-(NewGameWork.Enabled(Snapshot())?275:findingRoad?.Visible==true?260:200);workDecision.OffsetBottom=-height-18-8;
    }
    void CloseBottomTalk(){consultation=null;pendingWorkReply=null;workDecisionOpen=false;workDecision.Hide();workOverlay.Hide();}
    void AdvanceBottomTalk()
    {
        if(!workOverlay.Visible||historyOverlay.Visible)return;
        if(pendingWorkReply is {} action){pendingWorkReply=null;action();return;}
        workDecisionOpen=!workDecisionOpen;workDecision.Visible=workDecisionOpen;buttons["work-reply"].Text=workDecisionOpen?"台詞に戻る":shownWork is {} id&&KnownWork()?.KnownStatus.Contains("Completed")==true?"結果を見る":"返答を選ぶ";
    }
    void SetTalkSpeaker(EntityId speaker)
    {
        ownWorkSpeaker=speaker==Session.PlayerControl;workNames.Text=PersonName(speaker)+(ownWorkSpeaker?"  ／  あなた":"");
        for(int n=0;n<portraitPanels.Length;n++)portraitPanels[n].Modulate=n==(ownWorkSpeaker?0:1)?Colors.White:new Color(.42f,.42f,.42f,1);
    }
    void TalkPeople(EntityId other,bool present)
    {
        foreach(var a in talkActors.Values)a.Visible=false;var w=Snapshot();var ids=new[]{w.PlayerControl,other};
        for(int n=0;n<ids.Length;n++){if(!talkActors.TryGetValue(ids[n],out var actor)){actor=new();portraitStages[n].AddChild(actor);talkActors.Add(ids[n],actor);}actor.Visible=true;actor.Position=Vector3.Zero;actor.Rotation=Vector3.Zero;actor.Apply(ContractJson.Read<Officer3DCustomLook>(ContractJson.Write(NewGameWorld.Look(w.NewGame!,ids[n].Value))));actor.Freeze("idle",0);}
    }
    void Choice(string id,string text,Action action)
    {
        Add(workChoices,id,text,()=>{if(id is "work-service-release" or "work-debrief-next" or "work-followup-move" or "work-contact-apply" or "work-contact-accept" or "work-contact-decline" or "transport-next" or "work-duty-accept" or "work-confirm" or "work-assent" or "work-refuse" or "work-pay" or "work-received" or "work-military-confirm" or "work-supply-confirm" or "transport-accept" or "transport-collect" or "transport-deliver" or "transport-report" or "transport-pay" or "transport-cancel" or "transport-return"){pendingWorkReply=action;workDecisionOpen=false;workDecision.Hide();SetTalkSpeaker(Session.PlayerControl);workSpeech.Text=text+"。";workPhase.Text="あなたの返答";buttons["work-reply"].Text="続ける";}else action();});
        var button=buttons[id];button.CustomMinimumSize=new(0,42);button.Alignment=HorizontalAlignment.Left;button.AddThemeFontSizeOverride("font_size",18);button.AddThemeStyleboxOverride("normal",WorkBox("3e382a","bda068",8));button.AddThemeStyleboxOverride("hover",WorkBox("655338","eed39c",8));button.AddThemeStyleboxOverride("focus",WorkBox("655338","eed39c",8));
    }
    void ClearChoices(){foreach(Node n in workChoices.GetChildren()){workChoices.RemoveChild(n);n.QueueFree();}}
    void PresentWork(string title,string speech,string details,EntityId other,EntityId speaker,string phase,string prompt)
    {
        workReadOnly=true;pendingWorkReply=null;workDecisionOpen=false;workDecision.Hide();ClearChoices();findingRoad.Hide();workReplyChoice.Hide();militaryWorkChoice?.Hide();workContactChoice?.Hide();workCardHeading.Show();shownRoads=[];LayoutBottomTalk();workTitle.Text=title;workSpeech.Text=speech;workDetails.Text=FormatWorkDetails(details);workPhase.Text=phase.Split('/')[0].Trim();workPrompt.Text=prompt;buttons["work-reply"].Text=phase.StartsWith("報告")?"結果を見る":"返答を選ぶ";
        bool present=NearbyDisplay().Any(p=>p.Actor==other)&&Snapshot().Locations.Single(p=>p.Actor==other).Site==Branch(Session.PlayerControl).Knowledge.Location.Site;
        TalkPeople(other,present);portraitPanels[1].Visible=other!=Session.PlayerControl;SetTalkSpeaker(speaker);if(!present&&speaker!=Session.PlayerControl)workNames.Text+="  ／  書状";
        workOverlay.Show();
    }
    string FormatWorkDetails(string text)=>NewGameWork.Enabled(Snapshot())?text:text.Replace("\n","　");
    string WorkTerms(EntityId scope,long? deadline,bool consultationPreview=false,TaskKind? kind=null,long? fixedFee=null)
    {
        string due=deadline is {} at?consultationPreview?"受取手続きから120秒以内":Math.Max(0,at-Session.Seconds)+"秒以内":"未確認";
        string reward=!NewGameWork.Enabled(Snapshot())?"提示なし":(fixedFee??(kind==TaskKind.VoluntaryRequest||consultationPreview&&WorkDuty.Active(Snapshot())?.Site==scope?20:0))>0?"銭20文（返答開封後・現地で受取）":"別払いなし";return "場所　"+NewGameWorld.SiteName(scope.Value)+"\n期限　"+due+"\n報酬　"+reward;
    }
    void ShowWorkConsultation()
    {
        if(IsSaving||IsClockControlling)return;
        if(LocalWorkDemand.Enabled(Snapshot())&&LocalWorkDemand.Candidates(Snapshot()).Length==0){var peer=LocalWorkDemand.Candidates(Snapshot(),false).FirstOrDefault();if(peer==null)return;shownWork=null;consultation=null;PresentWork("現地の相談","今は新しく頼む点検の用件はない。また立ち寄ってくれ。","自主点検や移動、軍務はそれぞれの判断から進められます。",peer.Actor,peer.Actor,"相談","次の行動");workCardHeading.Text="新しい依頼なし";Choice("work-no-demand","地図へ戻る",CloseBottomTalk);return;}
        consultation=NewGameWorld.LocalWork(Snapshot(),"request:local-work/"+Session.Revision);var i=consultation;shownWork=null;
        PresentWork("現地の点検", "この拠点から出る陸路を点検し、通れるか知らせてほしい。",WorkTerms(i.Scope,i.Deadline.At,true,i.Kind),i.Actor,i.Actor,"相談  /  用件を受け取る前","あなたの返答");
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
        if(running){CloseBottomTalk();operationTabs.CurrentTab=0;return;}
        if(done){
            var report=Snapshot().Outbox.SingleOrDefault(o=>o.Task==t.Id&&o.Sender==Session.PlayerControl&&o.Kind=="contract-report");
            PresentWork("点検の報告","点検を終えました。結果をお送りします。","点検　　完了\n返報　　"+(report?.Sent==true?"発送済み":"発送待ち")+"\n相手の確認　未確認",t.Issuer,Session.PlayerControl,"報告  /  仕事の結果","次の行動");
            workCardHeading.Text=NewGameWorld.SiteName(task.Scope.Value)+"の陸路";
            shownRoads=LocalInspectionFindings.View(Session,Session.PlayerControl,t.Id);findingRoad.Clear();
            if(shownRoads.Length>0){
                workSpeech.Text="陸路の点検を終えました。通行状況は確認時点の記録です。";
                workDetails.Text="確認：開始から"+shownRoads[0].ObservedAt+"秒　／　完了返報："+(report?.Sent==true?"発送済み":"発送待ち");
                foreach(var row in shownRoads){findingRoad.AddItem(NewGameWorld.SiteName("site:"+row.Destination)+"　"+(row.LengthMm/1000000d).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"km　"+(row.Blocked?"通行不可（確認時）":"通行可（確認時）"));findingRoad.SetItemDisabled(findingRoad.ItemCount-1,row.Blocked);}
                int available=Array.FindIndex(shownRoads,r=>!r.Blocked);findingRoad.Select(available);findingRoad.Show();LayoutBottomTalk();
                Choice("work-road","選んだ道の行先を指定",()=>{int n=findingRoad.Selected;if(n<0||n>=shownRoads.Length||shownRoads[n].Blocked||Branch(Session.PlayerControl).Knowledge.Location.Site!=task.Scope)return;SetDestination(shownRoads[n].Destination);CloseBottomTalk();operationTabs.CurrentTab=0;});buttons["work-road"].Disabled=available<0||Branch(Session.PlayerControl).Knowledge.Location.Site!=task.Scope;
            }else workDetails.Text="この保存には陸路の点検記録がありません。完了返報："+(report?.Sent==true?"発送済み":"発送待ち");
            if(t.Issuer==Session.PlayerControl){workTitle.Text="点検記録";workDetails.Text=shownRoads.Length>0?"確認：開始から"+shownRoads[0].ObservedAt+"秒　／　自分の行動判断用の記録":"この拠点の陸路は、まだ点検対象が設定されていません。";}Choice("work-finish","地図へ戻る",CloseBottomTalk);return;
        }
        if(awaiting){
            PresentWork("点検を引き受けますか","この拠点から出る陸路を点検してくれないか。",WorkTerms(task.Scope,t.DeadlineAt,false,task.Kind,NewGameWork.Reward(Snapshot(),task)),t.Issuer,t.Issuer,"引受  /  返答すると仕事が始まります","あなたの返答");
            workCardHeading.Text="依頼の要点";Choice("work-assent","引き受けよう",()=>{if(Awaiting()?.Id!=t.Id)throw new InvalidOperationException("用件が変わりました。確認し直してください。");Assent(true);ShowKnownWork();});Choice("work-refuse","今回は断ろう",()=>{if(Awaiting()?.Id!=t.Id)throw new InvalidOperationException("用件が変わりました。確認し直してください。");Assent(false);ShowKnownWork();});return;
        }
        bool declined=task.Agreement==AgreementPhase.Declined;
        PresentWork(declined?"依頼を辞退しました":"仕事の確認",declined?"今回は引き受けないことにしました。":"この仕事の状況を確認しよう。","状態　　"+ReadableStatus(t.KnownStatus),t.Issuer,Session.PlayerControl,"確認","次の行動");
        workCardHeading.Text=NewGameWorld.SiteName(task.Scope.Value)+"の点検";Choice("work-finish","地図へ戻る",()=>workOverlay.Hide());
    }
    static string ReadableStatus(string text)=>text.Replace("Completed","完了").Replace("Running","進行中").Replace("NotStarted","未着手").Replace("Stopped","中断").Replace("Failed","失敗").Replace("Waiting","待機");
    void RefreshWorkExperience()
    {
        buttons["secret-cooperation"].Visible=PrivateStanddown.Enabled(Snapshot());buttons["secret-cooperation"].Disabled=IsSaving||IsClockControlling||IsDeparting;
        buttons["private-contact"].Visible=PrivateInformation.Enabled(Snapshot());buttons["private-contact"].Disabled=IsSaving||IsClockControlling||IsDeparting;
        buttons["truce-negotiation"].Visible=TruceNegotiations.Enabled(Snapshot())&&OwnPoliticalHouse(Snapshot())!=null;buttons["truce-negotiation"].Disabled=IsSaving||IsClockControlling||IsDeparting;
        buttons["companions"].Visible=PersonalCompanions.Enabled(Snapshot());buttons["companions"].Disabled=IsSaving||IsClockControlling||IsDeparting;
        buttons["trade"].Visible=Session.RegionalTradeView()!=null;buttons["trade"].Disabled=IsSaving||IsClockControlling||IsDeparting;buttons["economy"].Visible=Session.LocalEconomyView()!=null||Session.CommunityEconomyView()!=null;buttons["economy"].Disabled=IsSaving||IsClockControlling||IsDeparting;
        buttons["work-contact"].Visible=SelectWorkContact()!=null;
        buttons["work-contact"].Disabled=IsSaving||IsClockControlling||IsDeparting;
        buttons["work-duty"].Visible=WorkDuty.Candidate(Snapshot())!=null||WorkDuty.Active(Snapshot())!=null;
        if(workSummary==null)return;workBackdrop.Visible=historyOverlay.Visible;workDecision.Visible=workOverlay.Visible&&workDecisionOpen&&!historyOverlay.Visible;
        message.Visible=message.Text.StartsWith("操作できません：")||message.Text.Contains("確定できませんでした")||message.Text.Contains("保存できません");
        if(message.Text!=lastObservedMessage){lastObservedMessage=message.Text;if(!string.IsNullOrWhiteSpace(lastObservedMessage)){observedHistory.Add("開始から"+Session.Seconds+"秒　"+lastObservedMessage);if(observedHistory.Count>200)observedHistory.RemoveAt(0);}}
        var t=KnownWork();int unread=Branch(Session.PlayerControl).Knowledge.Messages.Count(m=>m.KnownBody==null);
        workSummary.Text=IsSaving?"保存中です。完了を待ってください。":IsClockControlling?"操作を確認中です。停止で取り消せます。":unread>0?"書状が届きました（"+unread+"通）":t==null?"現地で仕事を相談できます。\n相手と内容を確認してから進めます。":t.KnownStatus.Contains("Completed")?"点検完了\n次は、点検結果を確認する。":t.KnownStatus.Contains("Running")?"進行中："+NewGameWorld.SiteName(Snapshot().Tasks.Single(p=>p.Id==t.Id).Scope.Value)+"の点検\n期限：残り"+Math.Max(0,(t.DeadlineAt??Session.Seconds)-Session.Seconds)+"秒":"仕事："+ReadableStatus(t.KnownStatus);
        buttons["work-details"].Disabled=t==null&&unread==0;buttons["work-details"].Text=unread>0?"届いた書状を開く":t?.KnownStatus.Contains("Completed")==true?"点検結果を見る":"仕事の内容を確認";
        bool running=t?.KnownStatus.Contains("Running")==true;
        buttons["work-details"].Visible=!running&&(t!=null||unread>0);buttons["work"].Visible=t==null||t.KnownStatus.Contains("Completed")||t.KnownStatus.Contains("Stopped")||t.KnownStatus.Contains("Failed");
        buttons["work-run"].Visible=running;buttons["work-run"].Disabled=IsClockControlling||IsSaving;
        buttons["accept"].Visible=buttons["decline"].Visible=false;
        bool idle=!Snapshot().Tasks.Any(p=>p.Executor==Session.PlayerControl&&p.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed));buttons["work-self"].Visible=idle;buttons["work-self"].Disabled=!idle||Branch(Session.PlayerControl).Knowledge.Location.Kind!=LocationKind.AtSite||IsSaving||IsClockControlling;
        buttons["work-retry"].Visible=KnownFailedWorkLetters().Length>0;buttons["work-retry"].Disabled=IsSaving||IsClockControlling;
        bool peer=NearbyDisplay().Any(p=>Snapshot().Characters.Single(c=>c.Id==p.Actor).Life==LifeState.Adult&&Snapshot().Locations.Single(l=>l.Actor==p.Actor).Site==Branch(Session.PlayerControl).Knowledge.Location.Site);bool mapped=Branch(Session.PlayerControl).Knowledge.Location.Site is {} here&&NewGameWork.ScopeSupported(Snapshot(),here);buttons["work-self"].Disabled|=!mapped;buttons["work-self"].TooltipText=mapped?"自分の判断に使う陸路の記録を得ます。":"この拠点の陸路は点検対象が未設定です。";buttons["work"].Disabled=!idle||!peer||!mapped||IsSaving||IsClockControlling;buttons["work"].TooltipText=!mapped?"この拠点の陸路は点検対象が未設定です。別の拠点で相談してください。":peer?"現地の相手と依頼条件を相談します。":"この拠点には相談できる相手がいません。自主点検はできます。";
        buttons["work-receipts"].Visible=NewGameWork.Enabled(Snapshot())&&AssessmentNotice.LatestKnown(Session,Session.PlayerControl).Length>0;buttons["work-receipts"].Disabled=IsSaving||IsClockControlling;
        RefreshMilitaryWork();RefreshSupplyWork();RefreshTransportWork();RefreshVoyageTravel();RefreshWorkCycleGuidance(t,unread);if(NewGameWork.Enabled(Snapshot()))workSummary.Text+="\n私財：銭"+NewGameWork.Balance(Snapshot(),Session.PlayerControl)+"文"+(VoyageExpense.Enabled(Snapshot())?" ／船賃"+Snapshot().NewGame!.VoyageRules!.FarePerPassage+"文/区間":"");
        if(workOverlay.Visible&&workReadOnly&&shownWork is {} id){var current=Branch(Session.PlayerControl).Knowledge.Tasks.SingleOrDefault(p=>p.Id==id);if(current!=null&&Awaiting()?.Id==id&&!ownWorkSpeaker)workDetails.Text=FormatWorkDetails(WorkTerms(Snapshot().Tasks.Single(p=>p.Id==id).Scope,current.DeadlineAt,false,Snapshot().Tasks.Single(p=>p.Id==id).Kind,NewGameWork.Reward(Snapshot(),Snapshot().Tasks.Single(p=>p.Id==id))));if(current!=null&&((current.KnownStatus.Contains("Completed")&&workTitle.Text is not ("点検の報告" or "点検記録"))||(Awaiting()?.Id==id&&workTitle.Text!="点検を引き受けますか")))RenderKnownWork(current);}
    }
    internal bool ReviewRunningWorkLayout()
    {
        var action=buttons["work-run"];var summary=workSummary.GetGlobalRect();var rect=action.GetGlobalRect();
        return !workOverlay.Visible&&action.Visible&&action.GetParent()==workSummary.GetParent()&&rect.Position.Y>=summary.End.Y&&rect.Position.Y-summary.End.Y<60&&rect.End.Y<=GetViewportRect().Size.Y;
    }
    internal bool ReviewWorkLayout()
    {
        if(!workOverlay.Visible)return false;var screen=GetViewportRect();var band=workOverlay.GetGlobalRect();
        if(band.Size.Y>screen.Size.Y*.26f||band.Position.Y<screen.Size.Y*.72f||workBackdrop.Visible)return false;
        if(workSpeech.GetLineCount()*workSpeech.GetLineHeight()>workSpeech.Size.Y+1)return false;
        if(!workDecision.Visible)return true;var decision=workDecision.GetGlobalRect();if(decision.End.Y>=band.Position.Y||decision.Position.Y<0)return false;
        if((band.Area+decision.Area)/screen.Area>.43f)return false;
        float bottom=militaryWorkChoice.Visible?militaryWorkChoice.GetGlobalRect().End.Y:workReplyChoice.Visible?workReplyChoice.GetGlobalRect().End.Y:findingRoad.Visible?findingRoad.GetGlobalRect().End.Y:workDetails.GetGlobalRect().End.Y;
        foreach(var button in workChoices.GetChildren().OfType<Button>()){var rect=button.GetGlobalRect();if(rect.Position.Y<bottom||rect.Size.Y<40||rect.End.Y>decision.End.Y-8)return false;bottom=rect.End.Y;}
        return workDetails.GetLineCount()*workDetails.GetLineHeight()<=workDetails.Size.Y+1;
    }
    internal string[] ReviewFindingEdges=>shownRoads.Select(r=>r.Edge).ToArray();
    internal string? ReviewFindingDestination=>findingRoad.Selected>=0?shownRoads[findingRoad.Selected].Destination:null;
    internal bool ReviewOwnWorkSpeaker=>ownWorkSpeaker&&portraitPanels[0].Modulate==Colors.White&&portraitPanels[1].Modulate!=Colors.White;
    object? WorkPose()=>workOverlay?.Visible!=true?(object)new{Mode="map",Summary=workSummary.Text,SupplyNeeded=buttons["work-supply"].Visible,SupplyNeedText=supplyWorkNeed.Visible?supplyWorkNeed.Text:null,RunVisible=buttons["work-run"].Visible,DetailsVisible=buttons["work-details"].Visible,DetailsText=buttons["work-details"].Text}:new{Band=TalkRect(workOverlay),DecisionVisible=workDecision.Visible,Decision=workDecision.Visible?TalkRect(workDecision):null,OwnSpeaker=ownWorkSpeaker,Title=workTitle.Text,Phase=workPhase.Text,Prompt=workPrompt.Text,Card=workCardHeading.Text,Choices=workChoices.GetChildren().OfType<Button>().Select(b=>b.Text),Names=workNames.Text,Speech=workSpeech.Text,Details=workDetails.Text,Task=shownWork,Roads=shownRoads,SelectedRoad=findingRoad.Visible?findingRoad.Selected:-1,ReplyChoices=workReplyChoice.Visible?workReplyIds.Select(id=>id.Value).ToArray():[],SelectedReply=workReplyChoice.Visible?workReplyChoice.Selected:-1,Contact=workContactChoice?.Visible==true?workContactIds[workContactChoice.Selected].Value:null,MilitaryChoices=militaryWorkChoice.Visible?militaryWorkActions.Select(a=>a.ToString()).ToArray():[],SelectedMilitary=militaryWorkChoice.Visible?militaryWorkChoice.Selected:-1,Portraits=portraitStages.Select((stage,n)=>new{Rect=TalkRect(portraitPanels[n]),Size=new[]{portraitViews[n].Size.X,portraitViews[n].Size.Y},Camera=V(stage.GetChildren().OfType<Camera3D>().Single().Position),CameraSize=stage.GetChildren().OfType<Camera3D>().Single().Size,Modulate=portraitPanels[n].Modulate.ToHtml(),Actors=stage.GetChildren().OfType<Officer3DCustomActor>().Where(a=>a.Visible).Select(a=>new{Position=V(a.Position),Rotation=V(a.Rotation),Look=a.Appearance,Bones=Enumerable.Range(0,a.Skeleton.GetBoneCount()).Select(i=>{var bone=a.Skeleton.GetBoneGlobalPose(i);return new{Origin=V(bone.Origin),Basis=new[]{V(bone.Basis.X),V(bone.Basis.Y),V(bone.Basis.Z)}};})})})};
    static float[] TalkRect(Control control){var rect=control.GetGlobalRect();return[rect.Position.X,rect.Position.Y,rect.Size.X,rect.Size.Y];}
    void ShowWorkHistory()
    {
        workDecisionOpen=false;workDecision.Hide();var k=Branch(Session.PlayerControl).Knowledge;
        historyText.Text="確認済みの用件\n"+string.Join("\n",k.Tasks.Select(t=>PersonName(t.Issuer)+" → "+PersonName(t.Executor)+"　"+ReadableStatus(t.KnownStatus)))+"\n\n届いた書状\n"+string.Join("\n",k.Messages.Select(m=>PersonName(new(m.Sender))+"："+(m.KnownBody==null?"未開封・内容未確認":m.KnownBody.StartsWith("task:")?"用件を確認済み":ReadableWorkLetter(m))))+"\n\nこの画面での操作履歴（最大200件）\n"+string.Join("\n",observedHistory);historyOverlay.Show();
    }
}
