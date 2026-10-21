using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    bool TaskEntryMode=>NoticeEnabled&&OS.GetCmdlineUserArgs().Any(a=>a.StartsWith("--task-entry-"));
    TabContainer EntryTabs()=>(TabContainer)GetParent().GetParent();
    async Task VerifyTaskEntryHost()
    {
        Check(GetTree().CurrentScene is Main,"normal project Main is the actual root, not a separately opened trial");
        var tabs=EntryTabs();Check(tabs.GetTabControl(tabs.CurrentTab)==GetParent(),"actual native tab selection opens this session");
        Check(GetParent().GetChildCount()==1,"one lazy session instance per tab");
        string legacy=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(Output+"/"+Case+"-host.json")).RootElement.GetProperty("Legacy").GetString()!;
        Check(((Main)GetTree().CurrentScene).TaskEntryLegacyFingerprint==legacy,"new session initialization preserves old foundation state");
        await Capture("entry-open");if(Case=="success")await VerifyPersonnelCatalog();await VerifyTaskEntryRoundtrip();
    }
    async Task VerifyTaskEntryRoundtrip()
    {
        var tabs=EntryTabs();int index=tabs.GetTabIdxFromControl((Control)GetParent());var root=(Main)GetTree().CurrentScene;
        string legacy=root.TaskEntryLegacyFingerprint;string before=ContractJson.CanonicalHash(S.DiagnosticSnapshot());var original=S;
        tabs.CurrentTab=0;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        Check(root.TaskEntryLegacyFingerprint==legacy,"returning to original tab preserves foundation state");
        tabs.CurrentTab=index;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        Check(ReferenceEquals(S,original)&&GetParent().GetChildCount()==1&&ContractJson.CanonicalHash(S.DiagnosticSnapshot())==before,"tab roundtrip preserves session identity, tasks, clock and knowledge");
    }
}
