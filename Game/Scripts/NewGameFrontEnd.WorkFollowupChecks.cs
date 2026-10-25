using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    async Task FollowupStep(int n)
    {
        if(n==1){await ProgressionStep(1);await ProgressionStep(2);await ProgressionStep(3);Check(game!.Session.DiagnosticSnapshot().NewGame!.WorkPolicy==WorkFollowup.Policy,"normal entry opts into versioned outcome rule");}
        if(n==2){await ProgressionStep(4);game!.Press("work-transport");game.Press("work-reply");game.Press("transport-next");game.Press("work-reply");await Frame();game.Press("work-reply");await Frame();var j=TransportWork.Jobs(game.Session.DiagnosticSnapshot()).Last();Check(j.Handover.Terms.Amount==300&&j.Handover.Terms.Fee==40,"successful physical200 delivery earns actual next300/40 original");Check(game.ReviewWorkLayout(),"changed next job and reason fit compact dialogue");await Capture("larger-next-job",true);game.Press("work-close");}
        if(n==3){await TransportChoice("transport-accept");await TransportChoice("transport-collect");await TransportChoice("transport-cancel");Check(TransportWork.OfferAt(game!.Session.DiagnosticSnapshot(),new("job:unreturned"))==null,"unreturned300 prevents a parallel job");}
        if(n==4){await TransportChoice("transport-return");game!.Press("work-transport");game.Press("work-reply");game.Press("transport-next");game.Press("work-reply");await Frame();game.Press("work-reply");await Frame();var w=game.Session.DiagnosticSnapshot();var jobs=TransportWork.Jobs(w);var j=jobs.Last();Check(j.Handover.Terms.Amount==100&&j.Handover.Terms.Fee==20&&jobs.Length==3,"return leads to a distinct small restart original");Check(game.ReviewPrivateCoins==1050&&w.ResourceLots.Single(l=>l.Id==TransportWork.Cargo(jobs[1].Handover.Terms.Id)).Quantity==0,"returned300 cannot become fee or private money");Check(jobs[0].Handover.Terms.Amount==200&&TransportHandover.Validate(jobs[0].Handover).Phase==HandoverPhase.Paid,"earlier delivered contract remains unchanged");Check(game.ReviewWorkLayout(),"small restart explanation fits compact dialogue");await Capture("small-restart",true);game.Press("work-close");}
        game!.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunFollowupChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<5;n++){game!.LoadCheckpoint("followup"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<5;next++)await FollowupStep(next);await Capture("continued-s"+n,true);}}
        else{await BeginTransportEntry();for(int n=0;n<5;n++){await FollowupStep(n);game!.SaveCheckpoint("followup"+n);await Capture("s"+n,true);}await Capture("final",true);game!.Press("save");await WaitSave();}
        GD.Print("WORK_FOLLOWUP_PASSED "+FlowCase);
    }
}
