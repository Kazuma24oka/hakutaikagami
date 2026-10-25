using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    readonly List<string> assertions=new();
    bool HistoricalCase=>FlowCase is "historical" or "corrupt";
    string FlowCase=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--new-game-case="))?.Split('=')[1]??"group";
    void Check(bool ok,string text){if(!ok){if(game!=null){Directory.CreateDirectory(TestOutput);File.WriteAllText(Path.Combine(TestOutput,"failure-pose.json"),game.Pose());ViewportCapture.Save(GetViewport(),Path.Combine(TestOutput,"failure.png"));}throw new Exception(text);}assertions.Add(text);}
    async Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    async Task WaitSave(){var deadline=DateTime.UtcNow.AddSeconds(120);while(game!.IsSaving){if(DateTime.UtcNow>deadline)throw new TimeoutException("actual UI save completion");await Frame();}Check(game.LastSaveError==null,"actual UI save completed before continuing");}
    void Press(string id){Check(buttons.TryGetValue(id,out var b)&&!b.Disabled,"enabled actual frontend button: "+id);b!.EmitSignal(Button.SignalName.Pressed);}
    static IEnumerable<Node> Children(Node n){foreach(Node child in n.GetChildren()){yield return child;foreach(var x in Children(child))yield return x;}}
    void PressText(Node node,string text)=>Children(node).OfType<Button>().Single(b=>b.Text==text).EmitSignal(Button.SignalName.Pressed);
    async Task Capture(string name,bool play=false)
    {
        if(play&&!guideReviewed&&OS.GetCmdlineUserArgs().Contains("--play-guide-check"))await ReviewPlayGuide();
        foreach(var control in Children(this).OfType<Control>())control.MouseFilter=MouseFilterEnum.Ignore;
        await Frame();await Frame();if(game!=null&&play)game.Refresh();await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(TestOutput,name+".png"));
        if(play){var rect=game!.RenderRect;var info=game.InfoRect;Check(rect.Size.Y>=300&&rect.Position.Y+rect.Size.Y<=GetViewportRect().Size.Y,"actual 3D surface fits viewport "+name);File.WriteAllText(Path.Combine(TestOutput,name+"-rect.json"),System.Text.Json.JsonSerializer.Serialize(new{Surface=new[]{rect.Position.X,rect.Position.Y,rect.Size.X,rect.Size.Y},Info=new[]{info.Position.X,info.Position.Y,info.Size.X,info.Size.Y}}));File.WriteAllText(Path.Combine(TestOutput,name+"-pose.json"),game.Pose());File.WriteAllText(Path.Combine(TestOutput,name+"-world.sha256"),ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()));File.WriteAllText(Path.Combine(TestOutput,name+"-info.txt"),game.InfoText);}
    }
    async Task GameStep(int n)
    {
        if(n==1){game!.Press("rotate");game.SetDestination(NewGameWorld.InitialNode(game.Session.DiagnosticSnapshot(),game.Session.PlayerControl)=="castle"?"fork":"castle");await PressDeparture();game.Advance(game.Session.Seconds+5);}
        if(n==2){game!.Press("arrive");await WaitManualTime();}
        if(n==3){await PressLocalIntent("inspect");game!.Advance(game.Session.Seconds+2);}
        if(n==4)game!.Advance(game.Session.Seconds+10);
        game!.Press("save");await WaitSave();game.Refresh();
    }
    async Task ContinueFrom(int point){for(int n=point+1;n<=4;n++)await GameStep(n);}
    void PickRow(string id){int index=Array.FindIndex(visiblePeople,p=>p.Id==id);Check(index>=0,"selected person exists in actual filtered list");personRows.Select(index);personRows.EmitSignal(ItemList.SignalName.ItemSelected,index);}
    async Task SetupFlow()
    {
        await Capture("menu");
        if(FlowCase=="corrupt")Check(registryReadError!=null&&notice.Text.Contains("保持"),"corrupt library is kept while menu and historical starts remain usable");
        else{
Press("manage");await Frame();await Frame();string io=Path.Combine(DataRoot,"qa-io");Directory.CreateDirectory(io);manager!.ExerciseManagement(io);if(FlowCase=="collision")manager.ExerciseSecondDaimyo();if(FlowCase=="minor")manager.ExerciseMinor();await Capture("management");PressText(manager,"スタート画面へ");await Frame();
        Check(registry.Ungrouped.Length==(FlowCase is "collision" or "minor"?4:3)&&registry.Groups[0].Officers.Length==2,"saved independent library feeds new-game selection");
        }
        Press("new");await Capture("conditions");Press("next");selectionMode.Select(HistoricalCase?0:FlowCase is "individual" or "collision" or "minor"?1:2);selectionMode.EmitSignal(OptionButton.SignalName.ItemSelected,selectionMode.Selected);
        if(FlowCase is "individual" or "collision" or "minor"){registeredRows.Select(0,false);registeredRows.Select(1,false);if(FlowCase is "collision" or "minor")registeredRows.Select(3,false);}if(selectionMode.Selected==2)groupChoice.Select(0);await Capture("registered-selection");Press("next");await Frame();
        if(FlowCase=="collision"){
            Check(buttons["next"].Disabled&&previewText.Text.Contains("同じ城"),"conflicting daimyo claims disable confirmation in real GUI");await Capture("collision");placementPerson.Select(2);placementPerson.EmitSignal(OptionButton.SignalName.ItemSelected,2);placementSite.Select(Array.FindIndex(NationalRoutes.Sites,s=>s.Id.Value=="site:odawara"));Press("change-site");await Frame();Check(!buttons["next"].Disabled,"editing only this start resolves collision");
            var stored=new OfficerLibraryStore(LibraryRoot).Load();Check(stored.Ungrouped[3].Site=="site:castle","temporary placement correction does not overwrite registered source");
        }
        if(chosen.Any(p=>p.Role==StartTemplate.Daimyo)){
            int index=Array.FindIndex(NewGameWorld.Scenario().Castles,c=>c.Id=="site:kofu");territory.Select(index,false);territory.EmitSignal(ItemList.SignalName.MultiSelected,index,true);Check(!buttons["next"].Disabled,"multiple territories accepted in actual placement view");
            if(FlowCase=="absorb"){incorporate.ButtonPressed=true;incorporate.EmitSignal(BaseButton.SignalName.Toggled,true);}
        }
        await Capture("placement");Check(!buttons["next"].Disabled,"review enabled only after placement validation");Press("next");await Capture("world-review");Press("confirm");await Frame();
        Check(confirmed!=null,"world fixed before selecting player");
        string wanted=HistoricalCase?"person:shingen":chosen.First(p=>p.Role==StartTemplate.Daimyo).Id;
        string at=confirmed!.People.Single(p=>p.Id==wanted).Site;int jump=Array.FindIndex(NationalRoutes.Sites,s=>s.Id.Value==at);jumpChoice.Select(jump);jumpChoice.EmitSignal(OptionButton.SignalName.ItemSelected,jump);await Frame();Check(map!.SiteVisible(at),"actual castle pin visible after regional jump");map.PressSite(at);await Capture("person-map");
        if(FlowCase=="minor"){PickRow(chosen.Single(p=>p.BirthYear==1555).Id);Check(buttons["begin"].Disabled&&personText.Text.Contains("18歳"),"minor may appear but cannot be selected for play");await Capture("minor-disabled");}
        PickRow(wanted);Check(visiblePeople.All(p=>p.Site==at),"map castle selection filters same world residents");Press("all-people");sortChoice.Select(1);sortChoice.EmitSignal(OptionButton.SignalName.ItemSelected,1);PickRow(wanted);Press("locate");await Capture("person-list");
        Press("begin");await Frame();await Frame();Check(game!=null&&game.Session.PlayerControl.Value==wanted,"selected historical/registered person starts actual Core session");
        var start=game!.Session.DiagnosticSnapshot();Check(ContractJson.CanonicalHash(start.NewGame!.Start)==ContractJson.CanonicalHash(confirmed),"confirmed affiliations and territory are unchanged in runtime");
        File.WriteAllText(Path.Combine(TestOutput,"campaign-instance.txt"),game.Profile.Instance);
        if(!HistoricalCase){
            await PressLocalIntent("work");game!.Advance(game.Session.Seconds+2);var waiting=game.Session.DiagnosticSnapshot().Tasks.Single(t=>t.Kind==TaskKind.VoluntaryRequest);Check(waiting.StartedAt==null&&!game.Session.GetActorView(game.Session.PlayerControl).Tasks.Any(t=>t.Id==waiting.Id),"sealed local offer hides content and does not execute");await Capture("work-sealed",true);
            game.Press("open");await Capture("work-open",true);game.Press("accept");game.Advance(game.Session.Seconds+10);var done=game.Session.DiagnosticSnapshot().Tasks.Single(t=>t.Id==waiting.Id);Check(done.Execution==ExecutionPhase.Completed&&done.Report==ReportingPhase.KnownByRecipient,"local voluntary work requires opening/assent and physical return report");await Capture("work-complete",true);
        }

        string beforeMap=ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());game.Press("nation");await Capture("runtime-nation");game.Press("near");Check(beforeMap==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"in-game national map and returning to3D never change world");
        if(FlowCase=="corrupt")Check(File.ReadAllText(Path.Combine(LibraryRoot,"officers.json"))=="broken","beginning a life never overwrites corrupt registered data");
        await GameStep(0);game.SaveCheckpoint("s0");await Capture("s0",true);
        for(int n=1;n<=3;n++){await GameStep(n);game.SaveCheckpoint("s"+n);await Capture("s"+n,true);}
        await GameStep(4);await Capture("final",true);Check(game.Session.DiagnosticSnapshot().Tasks.Any(t=>t.Executor==game.Session.PlayerControl&&t.Action==ActionKind.Inspect&&t.Execution==ExecutionPhase.Completed),"same selected officer completes actual inspection with effects and messages");
        var same=game;game.Press("menu");await Frame();string before=ContractJson.CanonicalHash(same.Session.DiagnosticSnapshot());Press("settings");Press("large");Press("standard");Press("back");Press("continue");Check(ReferenceEquals(game,same)&&before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"settings/menu roundtrip keeps same world and clock");
        game.Press("menu");await Frame();Press("load-list");await Frame();Check(savedRows.Any(r=>r.Profile.Instance==same.Profile.Instance),"normal load list contains saved same-person campaign");await Capture("load-list");Press("load");Check(switching.Visible&&ReferenceEquals(game,same),"replacing current life asks actual in-game confirmation");switching.GetCancelButton().EmitSignal(Button.SignalName.Pressed);await Frame();Check(ReferenceEquals(game,same)&&pendingStart==null,"cancel leaves current person and world intact");Press("load");switching.GetOkButton().EmitSignal(Button.SignalName.Pressed);await Frame();Check(game!.Profile.Instance==same.Profile.Instance&&ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"normal load dialog reopens exact final world");await Capture("loaded",true);
    }
    async Task RestoreFlow()
    {
        Press("load-list");await Frame();string instance=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));int index=Array.FindIndex(savedRows,r=>r.Profile.Instance==instance);Check(index>=0,"campaign appears in load list after process restart");savedList.Select(index);Press("load");await Frame();
        for(int n=0;n<=3;n++){game!.LoadCheckpoint("s"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"restored 3D pose/appearance/person at checkpoint "+n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-world.sha256")),"restored all Core state at checkpoint "+n);await Capture("restored-s"+n,true);await ContinueFrom(n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same continuation yields same complete world "+n);await Capture("continued-s"+n,true);}
    }
    async Task CaptureFilm()
    {
        string folder=Path.Combine(TestOutput,"film");Directory.CreateDirectory(folder);game!.LoadCheckpoint("s1");
        game.Press("auto");for(int n=0;n<48;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(folder,"walk-"+n.ToString("D3")+".png"));}game.Press("pause");
        game.LoadCheckpoint("s2");await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(folder,"arrival.png"));game.Load();await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(folder,"reopened.png"));
        GD.Print("NEW_GAME_FILM_FRAMES_PASSED 50");
    }
    async void RunFlowChecks()
    {
        try{
            Directory.CreateDirectory(TestOutput);GD.Print("NEW_GAME_QA_BEGIN "+FlowCase);await Frame();await Frame();if(FlowCase.StartsWith("serviceway-")){await RunServiceDirectionChecks();QuitGracefully();return;}if(FlowCase.StartsWith("debrief-")){await RunDebriefChecks();QuitGracefully();return;}if(FlowCase.StartsWith("relations-")){await RunRelationsChecks();QuitGracefully();return;}if(FlowCase.StartsWith("followup-")){await RunFollowupChecks();QuitGracefully();return;}if(FlowCase.StartsWith("duty-")){await RunDutyChecks();QuitGracefully();return;}if(FlowCase.StartsWith("progression-")){await RunProgressionChecks();QuitGracefully();return;}if(FlowCase.StartsWith("voyage-")){await RunVoyageChecks();QuitGracefully();return;}if(FlowCase.StartsWith("transport-")){await RunTransportWorkChecks();QuitGracefully();return;}if(FlowCase.StartsWith("supply-")){await RunSupplyWorkChecks();QuitGracefully();return;}if(FlowCase.StartsWith("demand-")){await RunDemandChecks();QuitGracefully();return;}if(FlowCase.StartsWith("milwork-")){await RunMilitaryWorkChecks();QuitGracefully();return;}if(FlowCase.StartsWith("workcycle-")){await RunWorkCycleChecks();QuitGracefully();return;}if(FlowCase.StartsWith("findings-")){await RunInspectionFindingsChecks();QuitGracefully();return;}if(FlowCase.StartsWith("worktalk-")){await RunWorkConversationChecks();QuitGracefully();return;}if(FlowCase.StartsWith("playreview-")){await RunPlayReviewChecks();QuitGracefully();return;}if(FlowCase.StartsWith("routine-")){await RunNpcRoutineChecks();QuitGracefully();return;}if(FlowCase.StartsWith("household-")){await RunHistoricalHouseholdChecks();QuitGracefully();return;}if(FlowCase.StartsWith("edition-")){await RunHistoricalEditionChecks();QuitGracefully();return;}if(FlowCase.StartsWith("local-")){await RunLocalIntentChecks();QuitGracefully();return;}if(FlowCase.StartsWith("depart-")){await RunDepartureChecks();QuitGracefully();return;}if(FlowCase.StartsWith("lease-")){await RunLeaseChecks();QuitGracefully();return;}if(FlowCase.StartsWith("manual-")){await RunManualTimeChecks();QuitGracefully();return;}if(FlowCase.StartsWith("batch-")){await RunBatchChecks();QuitGracefully();return;}if(FlowCase.StartsWith("savecapture-")){await RunSaveCaptureChecks();QuitGracefully();return;}if(FlowCase.StartsWith("clock-")){await RunClockChecks();QuitGracefully();return;}if(FlowCase.StartsWith("prefetch-")){await RunPrefetchChecks();QuitGracefully();return;}if(FlowCase.StartsWith("capacity-")){await RunCapacityChecks();QuitGracefully();return;}if(FlowCase.StartsWith("authority-")){await RunAuthorityChecks();QuitGracefully();return;}if(FlowCase.StartsWith("dircancelheld-")){await RunDirectiveCancelHoldingChecks();QuitGracefully();return;}if(FlowCase.StartsWith("dircancelretry-")){await RunDirectiveCancelRetryChecks();QuitGracefully();return;}if(FlowCase.StartsWith("dircancel-")){await RunDirectiveCancellationChecks();QuitGracefully();return;}if(FlowCase.StartsWith("dirheld-")){await RunDirectiveHoldingChecks();QuitGracefully();return;}if(FlowCase.StartsWith("dirretry-")){await RunDirectiveRetryChecks();QuitGracefully();return;}if(FlowCase.StartsWith("directive-")){await RunDirectiveChecks();QuitGracefully();return;}if(FlowCase.StartsWith("unitrec-")){await RunUnitRecipientChecks();QuitGracefully();return;}if(FlowCase.StartsWith("saveui-")){await RunResponsiveSaveChecks();QuitGracefully();return;}if(FlowCase.StartsWith("target-")){await RunKnownTargetChecks();QuitGracefully();return;}if(FlowCase.StartsWith("known-")){await RunRecognitionChecks();QuitGracefully();return;}if(FlowCase.StartsWith("repeat-")){await RunRepeatedExerciseChecks();QuitGracefully();return;}if(FlowCase.StartsWith("renewal-")){await RunMilitaryRenewalChecks();QuitGracefully();return;}if(FlowCase.StartsWith("cancelnotice-")){await RunUnitCancelRecoveryChecks();QuitGracefully();return;}if(FlowCase.StartsWith("cancel-")){await RunUnitCancelChecks();QuitGracefully();return;}if(FlowCase.StartsWith("evidence-")){await RunUnitEvidenceChecks();QuitGracefully();return;}if(FlowCase.StartsWith("deadline-")){await RunUnitDeadlineChecks();QuitGracefully();return;}if(FlowCase.StartsWith("command-")){await RunUnitCommandChecks();QuitGracefully();return;}if(FlowCase.StartsWith("held-")){await RunUnitHeldChecks();QuitGracefully();return;}if(FlowCase.StartsWith("reply-")){await RunUnitReplyChecks();QuitGracefully();return;}if(FlowCase.StartsWith("inquiry-")){await RunUnitInquiryChecks();QuitGracefully();return;}if(FlowCase.StartsWith("retry-")){await RunUnitRetryChecks();QuitGracefully();return;}if(FlowCase.StartsWith("rout-")){await RunRoutChecks();QuitGracefully();return;}if(FlowCase.StartsWith("withdrawal-")){await RunWithdrawalChecks();QuitGracefully();return;}if(FlowCase.StartsWith("combat-")){await RunCombatChecks();QuitGracefully();return;}if(FlowCase.StartsWith("military-")){await RunNewMilitaryChecks();QuitGracefully();return;}if(FlowCase.StartsWith("service-")){await RunServiceChecks();QuitGracefully();return;}if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreFlow();else{await SetupFlow();if(OS.GetCmdlineUserArgs().Contains("--new-game-film"))await CaptureFilm();}
            File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("NEW_GAME_FLOW_PASSED "+FlowCase);QuitGracefully();
        }catch(Exception e){GD.PrintErr(e);QuitGracefully(1);}
    }
}
