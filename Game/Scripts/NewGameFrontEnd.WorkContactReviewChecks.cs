using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    async Task ContactReviewStep(int n)
    {
        if(n<4)await ServiceDirectionStep(n);
        if(n==4){
            var prior=game!.Session.DiagnosticSnapshot();Check(WorkProgression.Recognized(prior,new("person:ujiyasu"))==0&&WorkDuty.Active(prior)==null,"new lord does not inherit former lord's merit or duty");
            await PressLocalIntent("work");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.Press("work-run");await WaitManualTime();game.Press("work-details");game.Press("work-close");game.Press("work-contact");game.Press("work-reply");await Frame();var rows=WorkRelations.Known(game.Session.DiagnosticSnapshot());Check(rows.Length==2&&rows.Single(c=>c.Other==new EntityId("person:ujiyasu")).Local&&!rows.Single(c=>c.Other==new EntityId("person:shingen")).Local,"two known contacts distinguish current audience from old memory");Check(game.ReviewWorkLayout(),"contact selection fits compact header");await Capture("current-lord-contact",true);
            game.ReviewSelectWorkContact("person:shingen");game.Press("work-reply");await Frame();Check(game.ReviewRemoteContact&&game.ReviewOwnWorkSpeaker,"remote contact only shows own memory and no live service or military choices");await Capture("former-lord-memory",true);
            game.ReviewSelectWorkContact("person:ujiyasu");game.Press("work-reply");game.Press("work-contact-duty");game.Press("work-reply");game.Press("work-duty-accept");game.Press("work-reply");var w=game.Session.DiagnosticSnapshot();Check(WorkDuty.Active(w)!.Principal==new EntityId("person:ujiyasu")&&w.WorkCycle!.Duties!.Length==2,"new duty is a distinct agreement with new lord, not old duty transfer");Check(WorkProgression.Recognized(w,new("person:shingen"))==1&&WorkProgression.Recognized(w,new("person:ujiyasu"))==1,"both issuer-specific recognized jobs remain separate");game.Press("work-contact");game.Press("work-reply");game.Press("work-contact-duty");game.Press("work-reply");await Frame();await Capture("new-lord-duty",true);game.Press("work-close");}
        if(game!.WorkScreen!="closed")game.Press("work-close");game.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunContactReviewChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<5;n++){game!.LoadCheckpoint("contactreview"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<5;next++)await ContactReviewStep(next);await Capture("continued-s"+n,true);}}
        else{await BeginRelationsEntry();for(int n=0;n<5;n++){await ContactReviewStep(n);game!.SaveCheckpoint("contactreview"+n);await Capture("s"+n,true);}await Capture("final",true);game!.Press("save");await WaitSave();}
        GD.Print("CONTACT_REVIEW_PASSED "+FlowCase);
    }
}
