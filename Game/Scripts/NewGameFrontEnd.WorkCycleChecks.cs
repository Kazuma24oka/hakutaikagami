using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    bool WorkCycleBinding=>FlowCase=="workcycle-binding";
    async Task RunWorkCycleChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore"),film=OS.GetCmdlineUserArgs().Contains("--work-conversation-film");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();await Frame();for(int n=0;n<3;n++){game!.LoadCheckpoint("s"+n);if(n==0)game.Press("work-receipts");await Capture("restored-s"+n,true);if(n==0)await FinishWorkCycleReply();if(n==1){game.Press("work-receipts");game.Press("work-reply");if(game.ButtonEnabled("work-received")){game.Press("work-received");game.Press("work-reply");}game.Press("work-self");await WaitDeparture();game.Press("work-run");await WaitManualTime();}if(n==2)game.Advance(game.Session.Seconds+3);await Capture("continued-s"+n,true);}}
        else{
            await BeginPopulationEntry(0,WorkCycleBinding?"person:takeda-nobushige":"person:shingen");Check(game!.ReviewPrivateCoins==1000,"declared new-game private coins shown");await Capture("choices",true);if(film)await TalkHold(1);
            game.Press("work");game.Press("work-reply");await Capture("offer",true);Check(game.ReviewWorkLayout(),"fee and deadline fit compact reply popup");if(film)await TalkHold(2);game.Press("work-confirm");game.Press("work-reply");await WaitDeparture();game.Press("ten");await WaitManualTime();game.Press("work-details");
            if(!WorkCycleBinding){game.Press("work-reply");game.Press("work-assent");game.Press("work-reply");}if(FlowCase=="workcycle-return"){game.Advance(game.Session.Seconds+4);game.SetDestination("suwa");game.Press("depart");await WaitDeparture();game.Press("ten");await WaitManualTime();Check(AssessmentNotice.Inbox(game.Session,game.Session.PlayerControl).Length==0,"departed officer is not followed by a reply to hidden current location");await Capture("reply-away",true);game.Press("arrive");await WaitManualTime();game.SetDestination("kofu");game.Press("depart");await WaitDeparture();game.Press("ten");await WaitManualTime();game.Press("arrive");await WaitManualTime();Check(game.Session.DiagnosticSnapshot().Messaging.Attempts.Count(a=>game.Session.DiagnosticSnapshot().Messaging.Messages.Any(m=>m.Id==a.MessageId&&m.Kind==AssessmentNotice.Kind))==2,"assessor learns real failure then resends original reply at actual local return");}else if(FlowCase=="workcycle-failed"){game.Advance(game.Session.Seconds+4);var note=game.Session.DiagnosticSnapshot().Outbox.Single(o=>o.Kind=="route-observation"&&o.Sender==game.Session.PlayerControl);game.Session.DiagnosticTransport((mail,at)=>HakutaiKagami.Core.Messaging.MessageFlow.Fail(mail,note.Id.Value));game.Press("ten");await WaitManualTime();Check(AssessmentNotice.LatestKnown(game.Session,game.Session.PlayerControl).Length==0&&game.Session.DiagnosticSnapshot().Debts.Length==0,"lost report cannot produce a fee or imaginary reply");await Capture("known-failure",true);game.Press("work-retry");game.Press("ten");await WaitManualTime();}else{game.Press("work-run");await WaitManualTime();}
            Check(game.Session.GetActorView(game.Session.PlayerControl).Messages.Any(m=>m.KnownBody==null),"physical reply arrives sealed");Check(AssessmentNotice.LatestKnown(game.Session,game.Session.PlayerControl).Length==0,"reply contents hidden before opening");await Capture("sealed",true);if(film)await TalkHold(1);game.Press("work-details");Check(game.WorkScreen=="依頼主からの返答","opened actual letter appears as issuer dialogue");game.ReviewFraction(0);game.SaveCheckpoint("s0");await Capture("s0",true);await FinishWorkCycleReply(film);await Capture("final",true);
            // s1: receipt/payment confirmed, before choosing a second kind of work.
            game.LoadCheckpoint("s1");await Capture("s1",true);game.Press("work-receipts");game.Press("work-reply");game.Press("work-received");game.Press("work-reply");game.Press("work-self");await WaitDeparture();game.Press("work-run");await WaitManualTime();await Capture("self-complete",true);if(film)await TalkHold(2);
            var own=game.Session.DiagnosticSnapshot().Tasks.Last(t=>t.Executor==game.Session.PlayerControl);Check(own.Issuer==game.Session.PlayerControl&&own.Execution==ExecutionPhase.Completed,"autonomous inspection is a distinct real existing job without payer");game.SetDestination("suwa");game.Press("depart");await WaitDeparture();game.Advance(game.Session.Seconds+3);game.ReviewFraction(0);game.SaveCheckpoint("s2");await Capture("s2",true);if(film)await TalkHold(3);game.Advance(game.Session.Seconds+3);await Capture("travel-final",true);game.Press("save");await WaitSave();
        }
        File.WriteAllText(Path.Combine(TestOutput,restore?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("WORK_CYCLE_PASSED "+FlowCase);
    }
    async Task FinishWorkCycleReply(bool film=false)
    {
        game!.Press("work-reply");await Capture("reply-choices",true);Check(game.ReviewWorkLayout(),"actual notice list,fee and choices fit the compact popup");if(film)await TalkHold(3);
        if(!WorkCycleBinding){string before=ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());game.Press("work-pay");Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"own payment reply does not transfer money until continued");if(film)await TalkHold(1);game.Press("work-reply");Check(game.ReviewPrivateCoins==1020&&game.Session.DiagnosticSnapshot().Transfers.Length==1,"actual local transfer increases only personal coins once");await Capture("paid",true);if(film)await TalkHold(2);}
        else Check(game.ReviewPrivateCoins==1000&&game.Session.DiagnosticSnapshot().Transfers.Length==0,"binding duty invents no additional private fee");
        game.Press("work-close");game.ReviewFraction(0);game.SaveCheckpoint("s1");
    }
}
