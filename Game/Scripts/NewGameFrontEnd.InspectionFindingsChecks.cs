using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    async Task RunInspectionFindingsChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");bool film=OS.GetCmdlineUserArgs().Contains("--work-conversation-film");
        await RunWorkConversationChecks();
        game!.LoadCheckpoint("s1");game.Press("work-details");game.Press("work-reply");await Frame();await Frame();
        var s=game.Session;var w=s.DiagnosticSnapshot();var task=w.Tasks.Last(t=>t.Executor==s.PlayerControl&&t.Issuer!=s.PlayerControl&&t.Action==ActionKind.Inspect);
        var roads=LocalInspectionFindings.View(s,s.PlayerControl,task.Id);
        Check(roads.Length>0&&roads.Select(r=>r.Edge).SequenceEqual(game.ReviewFindingEdges),"popup renders only personally observed local roads");
        Check(roads.All(r=>r.Direct&&r.ObservedAt==task.FinishedAt),"actual completion timestamp retained");
        Check(game.ReviewWorkLayout(),"actual result popup fits both small and normal viewport");
        await Capture(restore?"restored-result":"result",true);if(film)await TalkHold(3);
        if(restore){game.LoadCheckpoint("s2");await Capture("restored-s2",true);game.Advance(game.Session.Seconds+3);await Capture("continued-s2",true);}
        else{
            string destination=game.ReviewFindingDestination!;string hash=ContractJson.CanonicalHash(s.DiagnosticSnapshot());game.Press("work-road");
            Check(hash==ContractJson.CanonicalHash(s.DiagnosticSnapshot())&&!s.GetBranchView(s.PlayerControl).Walking,"choosing observed road destination never starts movement");await Capture("destination",true);if(film)await TalkHold(2);
            game.Press("depart");await WaitDeparture();Check(game.LastDepartureResult==ResultCode.Accepted,"observed destination enters real existing movement");
            game.Advance(game.Session.Seconds+3);Check(game.Session.GetBranchView(game.Session.PlayerControl).Walking,"selected officer is walking toward observed destination");
            game.ReviewFraction(0);game.SaveCheckpoint("s2");await Capture("s2",true);if(film)await TalkHold(3);game.Advance(game.Session.Seconds+3);await Capture("movement-final",true);
        }
        File.WriteAllText(Path.Combine(TestOutput,restore?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("INSPECTION_FINDINGS_PASSED "+FlowCase);
    }
}
