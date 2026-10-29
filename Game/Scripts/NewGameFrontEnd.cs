using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd : Control
{
    VBoxContainer page=null!;Label heading=null!,notice=null!;Control canvas=null!;ConfirmationDialog switching=null!;
    readonly Dictionary<string,Button> buttons=new();OfficerLibrary registry=new(1,[],[]);OfficerLibraryTrial? manager;NewGameRuntime? game;
    NewCampaignStore campaigns=null!;RegisteredOfficer[] chosen=[];WorldSetupPreview? preview;SetupScenario? confirmed;
    readonly Dictionary<string,DaimyoPlacement> claims=new();ItemList registeredRows=null!,territory=null!,personRows=null!;OptionButton selectionMode=null!,groupChoice=null!,daimyoChoice=null!,sortChoice=null!;CheckBox incorporate=null!;LineEdit search=null!;Label previewText=null!,siteText=null!,personText=null!;NewGameMap? map;
    string? filterSite,selected;string? registryReadError;SetupPerson[] visiblePeople=[];NewCampaignListing[] savedRows=[];ItemList savedList=null!;
    Action? pendingStart;bool refreshingClaims;OptionButton jumpChoice=null!,placementPerson=null!,placementSite=null!;
    string? DemandForEntry=>TestKey==null||FlowCase.StartsWith("demand-")||FlowCase.StartsWith("supply-")?LocalWorkDemand.Policy:null;
    string? TestKey=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--new-game-test-key="))?.Split('=')[1];
    string TestOutput=>ProjectSettings.GlobalizePath("res://../Docs/Design/"+(FlowCase.StartsWith("engagementtask-")?"EngagementTaskDeliveryV1":FlowCase.StartsWith("housearmy-")?"HouseholdMilitaryV1":FlowCase.StartsWith("contactreview-")?"WorkContactReviewV1":FlowCase.StartsWith("serviceway-")?"ServiceDirectionV1":FlowCase.StartsWith("debrief-")?"MilitaryDebriefV1":FlowCase.StartsWith("relations-")?"WorkRelationsV1":FlowCase.StartsWith("followup-")?"WorkFollowupV1":FlowCase.StartsWith("duty-")?"WorkDutyV1":FlowCase.StartsWith("progression-")?"WorkProgressionV1":FlowCase.StartsWith("voyage-")?"TravelExpenseV1":FlowCase.StartsWith("transport-")?"TransportWorkV1":FlowCase.StartsWith("supply-")?"SupplyWorkV1":FlowCase.StartsWith("demand-")?"LocalWorkDemandV1":FlowCase.StartsWith("milwork-")?"MilitaryWorkV1":FlowCase.StartsWith("workcycle-")?"WorkCycleV1":FlowCase.StartsWith("findings-")?"InspectionFindingsV1":FlowCase.StartsWith("worktalk-")?"BottomDialogueV1":FlowCase.StartsWith("playreview-")?"PlayReviewV1":FlowCase.StartsWith("routine-")?"NewGameNpcRoutineV1":FlowCase.StartsWith("household-")?"HistoricalHouseholdV1":FlowCase.StartsWith("edition-")?"HistoricalStartEditionV1":FlowCase.StartsWith("local-")?"ResponsiveLocalIntentV1":FlowCase.StartsWith("depart-")?"ResponsiveDepartureV1":FlowCase.StartsWith("lease-")?"PreparationQueueV1":FlowCase.StartsWith("manual-")?"ManualAdvanceV1":FlowCase.StartsWith("batch-")?"BatchedAdvanceV1":FlowCase.StartsWith("savecapture-")?"SaveCaptureV1":FlowCase.StartsWith("clock-")?"ResponsiveClockV1":FlowCase.StartsWith("prefetch-")?"PreparedAdvanceV1":FlowCase.StartsWith("capacity-")?"PlayableLoopV1":FlowCase.StartsWith("authority-")?"MilitaryAuthorityV1":FlowCase.StartsWith("dircancelheld-")?"UnitDirectiveCancelHoldingV1":FlowCase.StartsWith("dircancelretry-")?"UnitDirectiveCancelRetryV1":FlowCase.StartsWith("dircancel-")?"UnitDirectiveCancellationV1":FlowCase.StartsWith("dirheld-")?"UnitDirectiveHoldingV1":FlowCase.StartsWith("dirretry-")?"UnitDirectiveRetryV1":FlowCase.StartsWith("directive-")?"UnitDirectiveV1":FlowCase.StartsWith("unitrec-")?"UnitRecipientV1":FlowCase.StartsWith("saveui-")?"ResponsiveSaveV1":FlowCase.StartsWith("target-")?"KnownMilitaryTargetV1":FlowCase.StartsWith("known-")?"RecognizedMilitaryV1":FlowCase.StartsWith("repeat-")?"RepeatedExerciseV1":FlowCase.StartsWith("renewal-")?"MilitaryRenewalV1":FlowCase.StartsWith("cancelnotice-")?"UnitCancellationRecoveryV1":FlowCase.StartsWith("cancel-")?"UnitCancellationV1":FlowCase.StartsWith("evidence-")?"UnitSenderEvidenceV1":FlowCase.StartsWith("deadline-")?"UnitOrderDeadlineV1":FlowCase.StartsWith("command-")?"UnitCommandHoldingV1":FlowCase.StartsWith("held-")?"UnitHeldDeliveryV1":FlowCase.StartsWith("reply-")?"UnitReplyRecoveryV1":FlowCase.StartsWith("inquiry-")?"UnitPostInquiryV1":FlowCase.StartsWith("retry-")?"UnitPostRecoveryV1":FlowCase.StartsWith("rout-")?"RoutRegroupV1":FlowCase.StartsWith("withdrawal-")?"PersonalWithdrawalV1":FlowCase.StartsWith("combat-")?"ContactCombatV1":FlowCase.StartsWith("military-")?"NewGameMilitaryV1":FlowCase.StartsWith("service-")?"ServiceEntryV1":"NewGameFlowV1")+"/runtime-local/"+FlowCase);
    string DataRoot=>TestKey is {} key?Path.Combine(TestOutput,Guid.ParseExact(key,"N").ToString("N")):ProjectSettings.GlobalizePath("user://new-game-v1");
    string LibraryRoot=>TestKey!=null?Path.Combine(DataRoot,"library"):ProjectSettings.GlobalizePath("user://officer-library-v1");
    const ulong Seed=20261022;
    SystemFont? ownedFont;
    public override void _ExitTree(){ownedFont?.Dispose();ownedFont=null;}
    void QuitGracefully(int code=0,[System.Runtime.CompilerServices.CallerMemberName]string caller=""){GD.Print("NEW_GAME_EXIT_REQUEST case="+FlowCase+" code="+code+" caller="+caller);GameProcessExit.Request(GetTree(),this,code);}
    public override void _Notification(int what){if(what==NotificationWMCloseRequest&&!GetTree().AutoAcceptQuit)QuitGracefully();}
    public override void _Ready()
    {
        // Explicit legacy verification flags preserve the former harness entry. Normal play uses this menu.
        var args=OS.GetCmdlineUserArgs();
        if(args.Any(a=>a.StartsWith("--start-selection-")||a.StartsWith("--work-hub-")||a.StartsWith("--task-entry-")||a is "--startup-entry" or "--personnel-catalog" or "--smoke-test" or "--tax-smoke-test" or "--shared-protocol-smoke-test")){
            using var legacyScene=GD.Load<PackedScene>("res://Scenes/WorkHubTrial.tscn");var legacy=legacyScene.Instantiate<Control>();AddChild(legacy);legacy.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);return;
        }

        GetTree().AutoAcceptQuit=false;
        Theme=new Theme{DefaultFontSize=16,DefaultFont=ownedFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
        var bg=new ColorRect{Color=new Color("17232b"),MouseFilter=MouseFilterEnum.Ignore};AddChild(bg);bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        canvas=new Control();AddChild(canvas);canvas.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        switching=new(){Title="現在の人生を切り替えますか",DialogText="現在の保存済み進行は残ります。画面上の未保存の進行を閉じ、選んだ人物へ切り替えます。"};AddChild(switching);switching.Confirmed+=()=>{var action=pendingStart;pendingStart=null;Guard(()=>action?.Invoke());};switching.Canceled+=()=>pendingStart=null;
        campaigns=new(Path.Combine(DataRoot,"campaigns"));ShowMenu();if(TestKey!=null)CallDeferred(nameof(RunFlowChecks));
    }
    void Guard(Action action){try{action();}catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException){if(notice!=null)notice.Text="確認してください："+e.Message;GD.Print("FRONTEND_REJECT "+e.Message);}}
    void Clear(string title,string help)
    {
        buttons.Clear();foreach(Node n in canvas.GetChildren()){canvas.RemoveChild(n);n.QueueFree();}canvas.Visible=true;if(game!=null)game.Suspend();
        var margin=new MarginContainer();canvas.AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);foreach(var side in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+side,24);
        page=new VBoxContainer();margin.AddChild(page);page.AddThemeConstantOverride("separation",10);heading=new(){Text=title,Modulate=new Color("e6c785")};heading.AddThemeFontSizeOverride("font_size",28);var headline=new HBoxContainer();page.AddChild(headline);heading.SizeFlagsHorizontal=SizeFlags.ExpandFill;headline.AddChild(heading);if(title!="百代鏡")Add(headline,"home","スタートへ",ShowMenu);page.AddChild(new Label{Text=help,AutowrapMode=TextServer.AutowrapMode.WordSmart});notice=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart,Modulate=new Color("edc89d")};page.AddChild(notice);
    }
    Button Add(Node parent,string id,string text,Action action){var b=new Button{Text=text,CustomMinimumSize=new(0,34)};parent.AddChild(b);buttons[id]=b;b.Pressed+=()=>Guard(action);return b;}
    HBoxContainer Bar()=>new HBoxContainer();
    void ReadRegistry(){registryReadError=null;try{registry=new OfficerLibraryStore(LibraryRoot).Load();}catch(FileNotFoundException){registry=new(1,[],[]);}catch(DirectoryNotFoundException){registry=new(1,[],[]);}catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or System.Text.Json.JsonException){registry=new(1,[],[]);registryReadError="登録データを読めません。元ファイルは保持しています。保存した人生のロードや、実在武将だけの開始は利用できます。";}}
    static string DisplaySite(string site){try{return NewGameWorld.SiteName(site);}catch(InvalidDataException){return "未収録の登場場所（管理画面で選び直してください）";}}
    void ShowMenu()
    {
        Clear("百代鏡","一人の武将として、時代の中を生きる。 / 新しいゲームの入口");ReadRegistry();if(registryReadError!=null)notice.Text=registryReadError;
        var columns=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};page.AddChild(columns);var menu=new VBoxContainer{CustomMinimumSize=new(300,0)};columns.AddChild(menu);
        Add(menu,"new","新規ゲーム",Conditions);Add(menu,"load-list","ロード",ShowLoads);Add(menu,"manage","武将作成・管理",ShowManagement);Add(menu,"settings","設定",Settings);
        if(game!=null)Add(menu,"continue","いまの武将へ戻る",()=>{canvas.Visible=false;game.Resume();});
        Add(menu,"legacy","以前の試作と保存",Legacy);Add(menu,"quit","終了",()=>QuitGracefully());
        var overview=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};columns.AddChild(overview);overview.AddChild(new Label{Text="1560年 / 全国の拠点から、一人を選ぶ"});var nation=new NewGameMap{SizeFlagsVertical=SizeFlags.ExpandFill};overview.AddChild(nation);nation.SitePicked=site=>{notice.Text=NewGameWorld.SiteName(site)+" / 新規ゲームで、ここから武将を探せます。";nation.Focus(site);};
        menu.AddChild(new Label{Text=(registryReadError==null?"登録武将 "+registry.Ungrouped.Length+"人\nグループ "+registry.Groups.Length+"組":"登録データ：読み込み不可（保持中）")+"\n\n作成・編集は新規ゲームと独立。\n開始した世界には後から上書きされません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        page.AddChild(new Label{Text="人物・勢力は部分収録。領土・役職は開始配置の試作設定です。通常起動と旧保存は元の入口に保持しています。",AutowrapMode=TextServer.AutowrapMode.WordSmart,Modulate=new Color("c7b687")});
    }
    void ShowManagement()
    {
        Clear("武将作成・管理","元の登録とシナリオ別の複製を、開始とは独立して管理します。");manager=new OfficerLibraryTrial{Enhanced=true,StorageOverride=LibraryRoot,Closed=()=>{ReadRegistry();ShowMenu();},SizeFlagsVertical=SizeFlags.ExpandFill};page.AddChild(manager);
    }
    void Conditions(){chosen=[];claims.Clear();preview=null;confirmed=null;selected=null;Clear("① 開始条件","開始する時代とシナリオを選びます。");page.AddChild(new Label{Text="開始年：1560年\nシナリオ：人物・拠点の開始配置試作\n歴史設定：準史実\n固定Seed：20261022"});page.AddChild(new Label{Text="初回の製品方針は1560年開始です。追加年代は収録後に追加します。\n史料付きの人物名・所属と、検証用の領土・役職設定は区別しています。",AutowrapMode=TextServer.AutowrapMode.WordSmart});Add(page,"next","登録武将の選択へ",ChooseRegistered);Add(page,"back","スタートへ",ShowMenu);}
    void ChooseRegistered()
    {
        ReadRegistry();confirmed=null;preview=null;Clear("② 登録武将を選ぶ","個別に複数人を選ぶか、一つのグループを選びます。登場させず実在武将だけでも始められます。");
        selectionMode=new();foreach(var text in new[]{"登場させない","グループなしから個別に選ぶ（複数可）","グループを選ぶ"})selectionMode.AddItem(text);page.AddChild(selectionMode);if(registryReadError!=null){notice.Text=registryReadError;selectionMode.SetItemDisabled(1,true);selectionMode.SetItemDisabled(2,true);}
        registeredRows=new(){SelectMode=ItemList.SelectModeEnum.Multi,SizeFlagsVertical=SizeFlags.ExpandFill};page.AddChild(registeredRows);foreach(var p in registry.Ungrouped)registeredRows.AddItem(p.Name+" / "+DisplaySite(p.Site)+" / "+NewGameWorld.RoleName(p.Role));
        groupChoice=new();foreach(var g in registry.Groups)groupChoice.AddItem(g.Name+"（"+g.Officers.Length+"人）");page.AddChild(groupChoice);groupChoice.Disabled=registry.Groups.Length==0;
        selectionMode.ItemSelected+=_=>UpdateSelectionMode();UpdateSelectionMode();
        Add(page,"next","配置の確認へ",()=>{chosen=NewGameWorld.Normalize(selectionMode.Selected==0?[]:selectionMode.Selected==1?NewGameSetup.Select(registry,registeredRows.GetSelectedItems().Select(i=>registry.Ungrouped[i].Id).ToArray(),null):NewGameSetup.Select(registry,[],registry.Groups[groupChoice.Selected].Id));claims.Clear();foreach(var p in chosen.Where(p=>p.Role==StartTemplate.Daimyo))claims[p.Id]=new(p.Id,"org:registered/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(p.Id)),[p.Site],false);Placements();});Add(page,"back","開始条件へ",Conditions);
    }
    void UpdateSelectionMode(){registeredRows.Visible=selectionMode.Selected==1;groupChoice.Visible=selectionMode.Selected==2;}
    void Placements()
    {
        confirmed=null;Clear("③ 配置・勢力を確認","登場場所と新しい領土、旧大名の処遇を確認します。競合がある場合は、ここで修正します。");
        if(chosen.Length>0){var edits=new HBoxContainer();page.AddChild(edits);placementPerson=new();foreach(var p in chosen)placementPerson.AddItem(p.Name);edits.AddChild(placementPerson);placementSite=new();foreach(var s in NationalRoutes.Sites)placementSite.AddItem(s.Name);edits.AddChild(placementSite);void ShowSite(){placementSite.Select(Array.FindIndex(NationalRoutes.Sites,s=>s.Id.Value==chosen[placementPerson.Selected].Site));}placementPerson.ItemSelected+=_=>ShowSite();ShowSite();Add(edits,"change-site","この開始だけ登場場所を変更",()=>{int index=placementPerson.Selected;var old=chosen[index];string site=NationalRoutes.Sites[placementSite.Selected].Id.Value;chosen[index]=old with{Site=site};if(claims.TryGetValue(old.Id,out var d))claims[old.Id]=d with{Castles=d.Castles.Where(c=>c!=old.Site).Append(site).Distinct().ToArray()};Placements();});}
        var split=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};page.AddChild(split);map=new NewGameMap{Setup=NewGameWorld.Scenario(HistoricalStartCatalog.Current),SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};split.AddChild(map);
        var side=new VBoxContainer{CustomMinimumSize=new(380,0),SizeFlagsVertical=SizeFlags.ExpandFill};split.AddChild(side);daimyoChoice=new();foreach(var p in chosen.Where(p=>p.Role==StartTemplate.Daimyo))daimyoChoice.AddItem(p.Name+" / "+p.FactionName);side.AddChild(daimyoChoice);
        territory=new(){SelectMode=ItemList.SelectModeEnum.Multi,SizeFlagsVertical=SizeFlags.ExpandFill};side.AddChild(territory);foreach(var c in NewGameWorld.Scenario(HistoricalStartCatalog.Current).Castles){var owner=NewGameWorld.Scenario(HistoricalStartCatalog.Current).Factions.SingleOrDefault(f=>f.Id==c.Faction);territory.AddItem(NewGameWorld.SiteName(c.Id)+" / "+(owner?.Name??"勢力未設定"));}
        incorporate=new(){Text="旧大名も家臣として取り込む"};side.AddChild(incorporate);side.AddChild(new Label{Text="旧勢力に領土が残れば移転。\n全領土を失った旧大名と随行家臣は、\n初期値では主従を保ったまま浪人になります。"});
        previewText=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};side.AddChild(previewText);
        territory.MultiSelected+=(i,on)=>{if(!refreshingClaims)CaptureClaims();};incorporate.Toggled+=_=>{if(!refreshingClaims)CaptureClaims();};daimyoChoice.ItemSelected+=_=>LoadClaims();
        map.SitePicked=site=>{if(daimyoChoice.ItemCount==0){notice.Text=NewGameWorld.SiteName(site)+" / 登録大名はいません。";return;}var entries=NewGameWorld.Scenario(HistoricalStartCatalog.Current).Castles;int i=Array.FindIndex(entries,c=>c.Id==site);if(territory.IsSelected(i))territory.Deselect(i);else territory.Select(i,false);CaptureClaims();};
        Add(page,"next","配置結果を最終確認",ReviewWorld);Add(page,"back","登録武将を選び直す",ChooseRegistered);
        if(daimyoChoice.ItemCount>0){daimyoChoice.Select(0);LoadClaims();}else{territory.Visible=false;incorporate.Visible=false;daimyoChoice.Visible=false;UpdatePreview();}
    }
    void LoadClaims(){refreshingClaims=true;territory.DeselectAll();var p=chosen.Where(p=>p.Role==StartTemplate.Daimyo).ToArray()[daimyoChoice.Selected];var d=claims[p.Id];var all=NewGameWorld.Scenario(HistoricalStartCatalog.Current).Castles;for(int i=0;i<all.Length;i++)if(d.Castles.Contains(all[i].Id))territory.Select(i,false);incorporate.ButtonPressed=d.IncorporateOldHead;refreshingClaims=false;UpdatePreview();}
    void CaptureClaims(){var p=chosen.Where(p=>p.Role==StartTemplate.Daimyo).ToArray()[daimyoChoice.Selected];var all=NewGameWorld.Scenario(HistoricalStartCatalog.Current).Castles;claims[p.Id]=claims[p.Id] with{Castles=territory.GetSelectedItems().Select(i=>all[i].Id).ToArray(),IncorporateOldHead=incorporate.ButtonPressed};UpdatePreview();}
    WorldSetupRequest Request()=>new(chosen,claims.Values.OrderBy(c=>c.Officer,StringComparer.Ordinal).ToArray());
    void UpdatePreview()
    {
        preview=NewGameSetup.Preview(NewGameWorld.Scenario(HistoricalStartCatalog.Current),Request());string[] errors=preview.Errors;
        if(preview.IsEligible())try{var first=preview.World!.People.First(p=>NewGameWorld.CanPlay(preview.World,p));NewGameWorld.Create(preview.World,chosen,first.Id,Seed,NewGameWorld.CompactPersonalAuthority,HistoricalStartCatalog.Current,NewGameNpcRoutine.Policy,LocalInspectionFindings.Policy,WorkFollowup.Policy,DemandForEntry,VoyageRulesForEntry);}catch(InvalidDataException e){errors=[e.Message];}
        buttons["next"].Disabled=errors.Length>0;previewText.Text=errors.Length>0?"配置の競合・不整合：\n"+string.Join("\n",errors):"配置の競合：なし\n登録武将："+chosen.Length+"人\n新勢力："+claims.Count+"勢力\n"+string.Join("\n",preview.Changes.Select(c=>NewGameWorld.Scenario(HistoricalStartCatalog.Current).People.Single(p=>p.Id==c.Person).Name+" → "+(c.Kind=="relocated"?"残った城へ移転":c.Kind=="incorporated"?"新勢力の家臣":"随行家臣と浪人")));
        if(map!=null){map.SetSetup(preview.World??NewGameWorld.Scenario(HistoricalStartCatalog.Current));}
    }
    void ReviewWorld()
    {
        UpdatePreview();if(buttons["next"].Disabled||preview==null)throw new InvalidDataException("配置を修正してください");var reviewed=preview;
        Clear("④ 世界を確定","この内容で世界を作った後、操作する武将を選びます。");var summary=reviewed.World!;page.AddChild(new Label{Text="登録武将 "+chosen.Length+"人 / 新勢力 "+claims.Count+"勢力 / 配置の競合 なし"});var nation=new NewGameMap{Setup=summary,SizeFlagsVertical=SizeFlags.ExpandFill};page.AddChild(nation);
        Add(page,"confirm","この世界で確定",()=>{confirmed=NewGameSetup.Confirm(NewGameWorld.Scenario(HistoricalStartCatalog.Current),Request(),reviewed);ChoosePerson();});Add(page,"back","配置を調整する",Placements);
    }
    void ChoosePerson()
    {
        if(confirmed==null)throw new InvalidDataException("世界を確定してください");Clear("⑤ 操作する武将を選ぶ","全国マップの地域・拠点から探すか、一覧で検索・並べ替えできます。登録武将も同じ住人として選べます。史実人物は一部収録で、開始位置・主従には試作用の設定を含みます。");filterSite=null;
        var filters=new HBoxContainer();page.AddChild(filters);Add(filters,"nation","全国へ",()=>map!.Nation());Add(filters,"all-people","全武将の一覧",()=>{filterSite=null;RefreshPeople();});search=new(){PlaceholderText="武将名で検索",SizeFlagsHorizontal=SizeFlags.ExpandFill};filters.AddChild(search);search.TextChanged+=_=>RefreshPeople();sortChoice=new();foreach(var s in new[]{"名前順","統率順","武勇順","知略順","政務順"})sortChoice.AddItem(s);filters.AddChild(sortChoice);sortChoice.ItemSelected+=_=>RefreshPeople();jumpChoice=new(){CustomMinimumSize=new(150,0)};foreach(var s in NationalRoutes.Sites)jumpChoice.AddItem(s.Name);filters.AddChild(jumpChoice);jumpChoice.ItemSelected+=i=>{string site=NationalRoutes.Sites[(int)i].Id.Value;map!.Focus(site);map.Pick(site);};
        var split=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};page.AddChild(split);map=new NewGameMap{Setup=confirmed,SizeFlagsVertical=SizeFlags.ExpandFill,SizeFlagsHorizontal=SizeFlags.ExpandFill};split.AddChild(map);var side=new VBoxContainer{CustomMinimumSize=new(360,0)};split.AddChild(side);siteText=new(){Text="全武将"};side.AddChild(siteText);personRows=new(){SizeFlagsVertical=SizeFlags.ExpandFill};side.AddChild(personRows);personRows.ItemSelected+=i=>SelectPerson(visiblePeople[(int)i]);personText=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};side.AddChild(personText);
        map.SitePicked=site=>{filterSite=site;siteText.Text=NewGameWorld.SiteName(site)+"にいる武将";map.Focus(site);RefreshPeople();};
        Add(side,"locate","選んだ武将を地図で見る",()=>{if(selected!=null)map.Focus(confirmed.People.Single(p=>p.Id==selected).Site);});Add(page,"begin","この武将で始める",RequestBegin);Add(page,"back","配置をやり直す",Placements);RefreshPeople();
    }
    void RefreshPeople()
    {
        var rows=confirmed!.People.Where(p=>(filterSite==null||p.Site==filterSite)&&(string.IsNullOrWhiteSpace(search.Text)||p.Name.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)));
        int Value(SetupPerson p)=>p.Attributes==null?int.MinValue:sortChoice.Selected switch{1=>p.Attributes.Leadership,2=>p.Attributes.Valor,3=>p.Attributes.Intelligence,_=>p.Attributes.Politics};
        visiblePeople=(sortChoice.Selected==0?rows.OrderBy(p=>p.Name,StringComparer.CurrentCulture):rows.OrderByDescending(Value).ThenBy(p=>p.Name,StringComparer.CurrentCulture)).ToArray();personRows.Clear();foreach(var p in visiblePeople)personRows.AddItem(p.Name+" / "+(p.Created?"登録":"実在")+(p.Attributes==null?" / 能力未採録":" / 統率"+p.Attributes.Leadership)+(NewGameWorld.CanPlay(confirmed,p)?"":" / 未成年"));
        int at=Array.FindIndex(visiblePeople,p=>p.Id==selected);if(at>=0){personRows.Select(at);SelectPerson(visiblePeople[at]);}else{selected=null;buttons["begin"].Disabled=true;personText.Text=visiblePeople.Length==0?"この条件に該当する武将はいません。":"武将を選んでください。";}
    }
    void SelectPerson(SetupPerson p){selected=p.Id;buttons["begin"].Disabled=!NewGameWorld.CanPlay(confirmed!,p);personText.Text=p.Name+"\n所在："+NewGameWorld.SiteName(p.Site)+"\n"+(p.Faction==null?"所属なし":confirmed!.Factions.Single(f=>f.Id==p.Faction).Name)+"\n"+(p.Lord==null?"直属相手なし":"直属："+confirmed!.People.Single(q=>q.Id==p.Lord).Name)+(NewGameWorld.CanPlay(confirmed!,p)?"":"\nこの候補では18歳以上から操作可能です。");}
    void RequestBegin(){if(confirmed==null||selected==null)throw new InvalidDataException("世界と操作人物を選んでください");string person=selected;Action start=()=>{var p=campaigns.New(confirmed,chosen,person,Seed,NewGameWorld.CompactPersonalAuthority,HistoricalStartCatalog.Current,NewGameNpcRoutine.Policy,LocalInspectionFindings.Policy,WorkFollowup.Policy,DemandForEntry,VoyageRulesForEntry,NpcArmies.CompactCombatPolicy,CastleHandover.Policy,PersonalCompanions.RetainerReleasePolicy,TruceNegotiations.Policy,PrivateInformation.Policy,PrivateStanddown.Policy);OpenGame(p,false);};if(game!=null){pendingStart=start;switching.PopupCentered();}else start();}
    void OpenGame(NewCampaignProfile profile,bool load)
    {
        var fresh=new NewGameRuntime{MenuRequested=ShowMenu};fresh.Configure(campaigns,profile,load);AddChild(fresh);fresh.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);if(game!=null){RemoveChild(game);game.QueueFree();}game=fresh;canvas.Visible=false;fresh.Resume();
    }
    void ShowLoads()
    {
        Clear("ロード","保存した人物の人生を選んで再開します。以前の試作の保存は、元の入口から開けます。");savedRows=campaigns.List(out int unreadable);savedList=new(){SizeFlagsVertical=SizeFlags.ExpandFill};page.AddChild(savedList);foreach(var row in savedRows){var p=row.Profile.Setup.People.Single(p=>p.Id==row.Profile.Selected);savedList.AddItem(p.Name+" / 開始地："+NewGameWorld.SiteName(p.Site)+" / 経過"+row.Seconds+"秒");}if(savedRows.Length>0)savedList.Select(0);notice.Text=unreadable>0?"読めない保存は保持しています（"+unreadable+"件）。":savedRows.Length==0?"保存した人生はまだありません。":"";
        Add(page,"load","選んだ保存を開く",()=>{var index=savedList.GetSelectedItems();if(index.Length!=1)throw new InvalidDataException("保存を選んでください");var p=savedRows[index[0]].Profile;Action start=()=>OpenGame(campaigns.Read(p.Instance),true);if(game!=null){pendingStart=start;switching.PopupCentered();}else start();}).Disabled=savedRows.Length==0;Add(page,"back","スタートへ",ShowMenu);
    }
    void Settings(){Clear("設定","画面内の文字の大きさを調整できます。世界の状態や時間は変わりません。");Add(page,"standard","標準の文字",()=>Theme.DefaultFontSize=16);Add(page,"large","大きめの文字",()=>Theme.DefaultFontSize=18);Add(page,"back","スタートへ",ShowMenu);}
    void Legacy(){Clear("以前の試作と保存","この中の各タブは、別の試作世界と保存先です。新しい開始の人物とは混ざりません。");Add(page,"back","スタートへ",ShowMenu);var hub=GD.Load<PackedScene>("res://Scenes/WorkHubTrial.tscn").Instantiate<Control>();hub.SizeFlagsVertical=SizeFlags.ExpandFill;page.AddChild(hub);}
}
