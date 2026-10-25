using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
// Approved default entry; legacy Main and its saves remain available.
public partial class WorkHubTrial:Control
{
    [Export] public bool LifeFirst {get;set;}
    TabContainer tabs=null!;StartSelectionPanel selection=null!;Control npcSlot=null!,legacySlot=null!;
    ContinuousWorldTrial npc=null!;Main? legacy;
    readonly List<string> checks=new();
    string Mode=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--work-hub-"))??"";
    string Output=>ProjectSettings.GlobalizePath((OS.GetCmdlineUserArgs().Contains("--startup-entry")?"res://../Docs/Design/WorkHubStartupV1/runtime-local":"res://../Docs/Design/WorkHubV1/runtime-local"));
    public override void _Ready()
    {
        try{
            var oldFlags=new[]{"--smoke-test","--tax-smoke-test","--shared-protocol-smoke-test","--deadline-smoke-test","--late-smoke-test","--delivery-smoke-test","--officer-motion-smoke-test","--officer-motion-review","--legacy-officer-2d"};
            if(OS.GetCmdlineUserArgs().Any(a=>oldFlags.Contains(a)||a.StartsWith("--task-entry-")||a.StartsWith("--officer-world-")||a.StartsWith("--military-grid-")||a.StartsWith("--formation-world-"))){CallDeferred(nameof(OpenLegacyScene));return;}
            Theme=new Theme{DefaultFontSize=15,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
            var col=new VBoxContainer();AddChild(col);col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            col.AddChild(new Label{Text=LifeFirst?"百代鏡 / 武将の人生":"百代鏡 / 試作の選択"});
            col.AddChild(new Label{Text=LifeFirst?"一人の武将として同じ世界で生きる試作です。地形・城郭は開発中の仮表現です。":"各タブは別の試作世界・保存先です。以前の保存は「従来の検証」で開けます。",Modulate=new Color("e6c785")});
            tabs=new TabContainer{SizeFlagsVertical=SizeFlags.ExpandFill};col.AddChild(tabs);
            npcSlot=new Control{Name="地域NPCの仕事"};legacySlot=new Control{Name="従来の検証"};tabs.AddChild(npcSlot);tabs.AddChild(legacySlot);selection=new(){Name=LifeFirst?"武将の人生":"作成武将で開始",Simplified=LifeFirst,ProcessMode=ProcessModeEnum.Disabled};tabs.AddChild(selection);
            npc=GD.Load<PackedScene>("res://Scenes/NpcWorkTrial.tscn").Instantiate<ContinuousWorldTrial>();npcSlot.AddChild(npc);
            tabs.TabChanged+=i=>{
                if(i==1&&legacy==null){legacy=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<Main>();legacySlot.AddChild(legacy);}
                npc.ProcessMode=i==0?ProcessModeEnum.Inherit:ProcessModeEnum.Disabled;selection.ProcessMode=i==2?ProcessModeEnum.Inherit:ProcessModeEnum.Disabled;
                if(legacy!=null)legacy.ProcessMode=i==1?ProcessModeEnum.Inherit:ProcessModeEnum.Disabled;
            };
            if(LifeFirst)tabs.CurrentTab=2;
            if(Mode!="")CallDeferred(nameof(RunHubChecks));
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
    void OpenLegacyScene(){if(GetTree().ChangeSceneToFile("res://Scenes/Main.tscn")!=Error.Ok)throw new Exception("Legacy entry unavailable");}
    void Check(bool yes,string text){if(!yes)throw new Exception(text);checks.Add(text);}
    async Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    async Task Capture(string name){await Frame();Check(npc.HubFits(),"actual regional UI and minimum 3D height fit new launcher");GetViewport().GetTexture().GetImage().SavePng(Output+"/"+name+".png");System.IO.File.WriteAllText(Output+"/"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(npc.HubRect()));}
    async Task Roundtrip()
    {
        var session=npc.HubSession;string before=ContractJson.CanonicalHash(session.DiagnosticSnapshot());tabs.CurrentTab=1;await Frame();
        Check(legacy!=null&&npc.ProcessMode==ProcessModeEnum.Disabled,"actual legacy tab lazily creates old Main and suspends hidden trial");
        var old=legacy!;string fingerprint=old.TaskEntryLegacyFingerprint;
        tabs.CurrentTab=0;await Frame();
        Check(ReferenceEquals(session,npc.HubSession)&&ContractJson.CanonicalHash(npc.HubSession.DiagnosticSnapshot())==before,"native tab roundtrip preserves current NPC session and world");
        Check(ReferenceEquals(old,legacy)&&old.TaskEntryLegacyFingerprint==fingerprint&&old.ProcessMode==ProcessModeEnum.Disabled,"legacy session and foundation state preserved while hidden");
    }
    void ContinueNpc()
    {
        if(npc.HubSession.Seconds<2)npc.HubAdvance(2);
        if(RegionalWork.Inbox(npc.HubSession).Any(c=>c.CanOpen))npc.HubPress("work-open");
        if(RegionalWork.Inbox(npc.HubSession).Any(c=>c.CanDecide))npc.HubPress("work-accept");
        if(npc.HubSession.Seconds<9)npc.HubAdvance(9);npc.HubAdvance(3602);
    }
    async void RunHubChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);await Frame();Check(tabs.CurrentTab==0&&legacy==null&&legacySlot.GetChildCount()==0,"new launcher starts local NPC profile and leaves old Main lazy");
            if(Mode.EndsWith("restore")){
                for(int n=0;n<5;n++){npc.HubLoad("hub-s"+n);Check(npc.HubPose()==System.IO.File.ReadAllText(Output+"/s"+n+"-pose.json"),"launcher restores NPC memory, known messages and 3D "+n);await Roundtrip();await Capture("restored-s"+n);ContinueNpc();await Capture("continued-s"+n);}
            }else{
                await Capture("startup");long[] at={1,2,4,6,9};
                for(int n=0;n<at.Length;n++){
                    if(n==2){npc.HubPress("work-open");npc.HubPress("work-accept");}
                    npc.HubAdvance(at[n]);await Roundtrip();npc.HubSave("hub-s"+n);System.IO.File.WriteAllText(Output+"/s"+n+"-pose.json",npc.HubPose());await Capture("s"+n);
                }
                ContinueNpc();await Roundtrip();await Capture("final");Check(npc.HubSession.DiagnosticSnapshot().Tasks.Count(t=>t.Action==ActionKind.Inspect)==2,"autonomous work and sealed repeat run inside candidate launcher");
            }
            System.IO.File.WriteAllText(Output+"/"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks}));GD.Print("WORK_HUB_PASSED");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
