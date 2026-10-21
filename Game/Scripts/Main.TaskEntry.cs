using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
public partial class Main
{
    TabContainer taskEntryTabs=null!;
    Control taskEntrySlot=null!;
    public string TaskEntryLegacyFingerprint=>System.Text.Json.JsonSerializer.Serialize(new{world.Tick,world.Characters,world.Castles,world.Authorities,world.Resources});
    void AddTaskEntry(TabContainer tabs)
    {
        taskEntryTabs=tabs;taskEntrySlot=new Control{Name="武将の任務"};tabs.AddChild(taskEntrySlot);
        int index=tabs.GetTabIdxFromControl(taskEntrySlot);
        tabs.TabChanged+=selected=>{
            if(selected!=index||taskEntrySlot.GetChildCount()!=0)return;
            var entry=GD.Load<PackedScene>("res://Scenes/AssessmentNoticeTrial.tscn").Instantiate<Control>();taskEntrySlot.AddChild(entry);
        };
        if(OS.GetCmdlineUserArgs().Any(a=>a.StartsWith("--task-entry-")))CallDeferred(nameof(BeginTaskEntryCheck));
    }
    async void BeginTaskEntryCheck()
    {
        try{
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            if(taskEntryTabs.CurrentTab!=0||taskEntrySlot.GetChildCount()!=0)throw new Exception("Normal startup must keep original tab and lazily create the new session");
            var titles=Enumerable.Range(0,taskEntryTabs.GetTabCount()).Select(i=>taskEntryTabs.GetTabTitle(i)).ToArray();
            if(titles[0]!="武将・城"||titles[1]!="徴税・保存検証"||titles[2]!="応答・代理・共通検証"||titles[^1]!="武将の任務")throw new Exception("Legacy tab positions changed");
            string output=ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--personnel-catalog")?"res://../Docs/Design/NationalPersonnelV1/runtime-local":"res://../Docs/Design/TaskEntryV1/runtime-local");System.IO.Directory.CreateDirectory(output);
            string c=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--case="))[7..];
            if(GetViewport().GetTexture().GetImage().SavePng(output+"/"+c+"-startup.png")!=Error.Ok)throw new Exception("Startup capture failed");
            System.IO.File.WriteAllText(output+"/"+c+"-host.json",System.Text.Json.JsonSerializer.Serialize(new{Titles=titles,Lazy=true,Legacy=TaskEntryLegacyFingerprint,Main=GetTree().CurrentScene==this}));
            taskEntryTabs.CurrentTab=taskEntryTabs.GetTabIdxFromControl(taskEntrySlot);
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
