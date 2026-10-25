using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    async Task ProgressionStep(int n)
    {
        if(n==0){var offer=TransportWork.OfferAt(game!.Session.DiagnosticSnapshot(),new("job:before-evaluation"));Check(offer?.Amount==100&&offer.Fee==20,"before results, only basic100/20 consignment offered");}
        if(n==1){await PressLocalIntent("work");game!.Press("ten");await WaitManualTime();game.Press("work-details");game.Press("work-reply");game.Press("work-assent");game.Press("work-reply");game.Press("work-run");await WaitManualTime();var w=game.Session.DiagnosticSnapshot();var issuer=w.Tasks.Last(t=>t.Action==ActionKind.Inspect&&t.Executor==w.PlayerControl).Issuer;Check(WorkProgression.Recognized(w,issuer)==0&&TransportWork.OfferAt(w,new("job:sealed-preview"))!.Amount==100,"unopened assessment cannot leak improved work");}
        if(n==2){game!.Press("work-details");await Frame();await Capture("recognition",true);game.Press("work-reply");game.Press("work-pay");game.Press("work-reply");Check(game.ReviewPrivateCoins==1020,"same actual work payment remains20");game.Press("work-reply");game.Press("work-next-transport");game.Press("work-reply");await Frame();Check(TransportWork.Jobs(game.Session.DiagnosticSnapshot()).Last().Handover.Terms.Amount==200,"recognized work actually unlocks200/30 job through dialogue");await Capture("next-job",true);game.Press("work-close");}
        if(n==3){await TransportChoice("transport-accept");await TransportChoice("transport-collect");var w=game!.Session.DiagnosticSnapshot();var j=TransportWork.Jobs(w).Last();Check(w.ResourceLots.Single(l=>l.Id==TransportWork.Cargo(j.Handover.Terms.Id)).Quantity==200&&game.ReviewPrivateCoins==1020,"new larger job moves real200 without paying carrier early");}
        if(n==4){await VoyageDepart("suwa");game!.Press("arrive");await WaitManualTime();await TransportChoice("transport-deliver");await VoyageDepart("kofu");game.Press("arrive");await WaitManualTime();await TransportChoice("transport-report");await TransportChoice("transport-pay");Check(game.ReviewPrivateCoins==1050,"recognized larger consignment really pays30 after local return report");}
        game!.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunProgressionChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<5;n++){game!.LoadCheckpoint("progression"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<5;next++)await ProgressionStep(next);await Capture("continued-s"+n,true);}}
        else{await BeginTransportEntry();for(int n=0;n<5;n++){await ProgressionStep(n);game!.SaveCheckpoint("progression"+n);await Capture("s"+n,true);}await Capture("final",true);game!.Press("save");await WaitSave();}
        GD.Print("WORK_PROGRESSION_PASSED "+FlowCase);
    }
}
