using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Appearance;
public partial class NewGameFrontEnd
{
    async Task BeginTransportEntry()
    {
        var receiver=new RegisteredOfficer("created:transport-recipient",null,"運搬検証の受取人",1520,new(50,50,50,50),StartTemplate.Retainer,"site:suwa","person:shingen",null,AppearanceCatalog.Default(1));
        new OfficerLibraryStore(LibraryRoot).Save(new(1,[],[new("group:transport","運搬検証の架空受取人",[receiver])]),null);
        ShowMenu();Press("new");Press("next");selectionMode.Select(2);selectionMode.EmitSignal(OptionButton.SignalName.ItemSelected,2);groupChoice.Select(0);Press("next");Press("next");Press("confirm");await Frame();map!.PressSite(NewGameWorld.Scenario(HistoricalStartCatalog.Current).People.Single(p=>p.Id=="person:shingen").Site);PickRow("person:shingen");Press("begin");await Frame();await Frame();
        File.WriteAllText(Path.Combine(TestOutput,"campaign-instance.txt"),game!.Profile.Instance);
        Check(game.Session.DiagnosticSnapshot().NewGame!.Registered.Single().Id==receiver.Id,"actual normal group setup imported declared synthetic recipient, not historical evidence");
    }
    async Task TransportChoice(string choice)
    {
        game!.Press("work-transport");game.Press("work-reply");string hash=ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());game.Press(choice);Check(hash==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"own spoken choice is not executed before continuing "+choice);game.Press("work-reply");await Frame();game.Press("work-close");
    }
    async Task TransportMilitaryChecks()
    {
        game!.ChooseMilitary(NewMilitaryAction.Request);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.Press("work-close");game.ChooseMilitary(NewMilitaryAction.Accept);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.Press("work-close");game.ChooseMilitary(NewMilitaryAction.Pickup);game.ChooseMilitary(NewMilitaryAction.Load);
        var w=game.Session.DiagnosticSnapshot();Check(game.ReviewPrivateCoins==1000&&w.ResourceLots.Single(l=>l.Id==TransportWork.Cargo(TransportWork.Jobs(w).Single().Handover.Terms.Id)).Quantity==100&&NewGameMilitary.View(w).Carry>0,"same world keeps private wallet, borrowed purse and military food distinct");
        game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Return);game.ChooseMilitary(NewMilitaryAction.Report);game.Advance(game.Session.Seconds+3);OpenAllServiceLetters();game.Press("work-close");
    }
    async Task TransportStep(int n)
    {
        if(FlowCase=="transport-cancel"&&n>=3){if(n==3)await TransportChoice("transport-cancel");if(n==4)await TransportChoice("transport-return");if(n>=4){var w=game!.Session.DiagnosticSnapshot();Check(NewGameWork.Balance(w,TransportWork.Jobs(w).Single().Handover.Terms.Issuer)==1000&&game.ReviewPrivateCoins==1000,"actual local cancellation and return restore source coins without fee");}game!.ReviewFraction(0);game.Refresh();await Frame();return;}
        if(n==0){game!.Press("work-transport");game.Press("work-reply");await Frame();await Frame();Check(game.ReviewWorkLayout(),"transport fixed terms fit compact dialogue");await Capture("terms",true);game.Press("work-close");}
        if(n==1)await TransportChoice("transport-accept");
        if(n==2){await TransportChoice("transport-collect");var w=game!.Session.DiagnosticSnapshot();Check(NewGameWork.Balance(w,w.PlayerControl)==1000&&w.ResourceLots.Single(l=>l.Id==TransportWork.Cargo(TransportWork.Jobs(w).Single().Handover.Terms.Id)).Quantity==100,"actual pickup keeps purse separate from private wallet");if(FlowCase=="transport-military")await TransportMilitaryChecks();}
        if(n==3){game!.SetDestination("suwa");game.Press("depart");await WaitDeparture();game.Advance(game.Session.Seconds+3);Check(game.Session.DiagnosticSnapshot().Locations.Single(l=>l.Actor==game.Session.PlayerControl).Kind==LocationKind.Traveling,"real road journey carries purse");}
        if(n==4){game!.Press("arrive");await WaitManualTime();await TransportChoice("transport-deliver");Check(TransportHandover.Validate(TransportWork.Jobs(game.Session.DiagnosticSnapshot()).Single().Handover).Phase==HandoverPhase.Delivered,"actual local recipient receives purse before report");}
        if(n==5){game!.SetDestination("kofu");game.Press("depart");await WaitDeparture();game.Advance(game.Session.Seconds+3);game.Press("arrive");await WaitManualTime();await TransportChoice("transport-report");Check(game.Session.DiagnosticSnapshot().Debts.Any(d=>d.Id==TransportWork.Debt(TransportWork.Jobs(game.Session.DiagnosticSnapshot()).Single().Handover.Terms.Id)&&d.Outstanding==20),"actual return report creates unpaid fee only once");}
        if(n==6){await TransportChoice("transport-pay");Check(game!.ReviewPrivateCoins==1020,"actual local fee credited once to private wallet");}
        game!.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunTransportWorkChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<7;n++){game!.LoadCheckpoint("transport"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<7;next++)await TransportStep(next);await Capture("continued-s"+n,true);}}
        else{await BeginTransportEntry();for(int n=0;n<7;n++){await TransportStep(n);game!.SaveCheckpoint("transport"+n);await Capture("s"+n,true);}await Capture("final",true);game!.Press("save");await WaitSave();}
        File.WriteAllText(Path.Combine(TestOutput,restore?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("TRANSPORT_WORK_PASSED "+FlowCase);
    }
}
