using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Appearance;
public partial class NewGameFrontEnd
{
    bool VoyagePoor=>FlowCase=="voyage-poor";
    bool VoyageCancel=>FlowCase=="voyage-cancel";
    VoyageExpenseRules VoyageRulesForEntry=>OS.GetCmdlineUserArgs().Any(x=>x.StartsWith("--new-game-test-key="))&&VoyagePoor?VoyageExpense.Standard with{FarePerPassage=1000}:VoyageExpense.Standard;
    string VoyageIsland=>"iki";
    string VoyageMainland=>NationalRoutes.Edges.Where(e=>NationalRoutes.Sea(e.Id)&&(e.From==VoyageIsland||e.To==VoyageIsland)).Select(e=>e.From==VoyageIsland?e.To:e.From).First(n=>NationalRoutes.Edges.Any(e=>!NationalRoutes.Sea(e.Id)&&(e.From==n||e.To==n)));
    long CurrentBoardAt(){var w=game!.Session.DiagnosticSnapshot();var j=w.Branches!.Journeys.Last();var l=j.Legs.First(l=>NationalRoutes.Sea(l.Edge)&&l.EndAt>game.Session.Seconds);return TravelWorld.Departure(w,l.Edge,l.StartAt);}
    async Task BeginVoyageEntry()
    {
        var p=new RegisteredOfficer("created:voyager",null,"渡海の検証武将",1520,new(50,50,50,50),StartTemplate.Ronin,RegionalCampaign.Site(VoyageIsland).Value,null,null,AppearanceCatalog.Default(1));
        new OfficerLibraryStore(LibraryRoot).Save(new(1,[],[new("group:voyage","渡海検証・架空武将",[p])]),null);ShowMenu();Press("new");Press("next");selectionMode.Select(2);selectionMode.EmitSignal(OptionButton.SignalName.ItemSelected,2);groupChoice.Select(0);Press("next");Press("next");Press("confirm");await Frame();map!.PressSite(RegionalCampaign.Site(VoyageIsland).Value);PickRow(p.Id);Press("begin");await Frame();await Frame();
        File.WriteAllText(Path.Combine(TestOutput,"campaign-instance.txt"),game!.Profile.Instance);Check(game.Profile.VoyageRules?.FarePerPassage==(VoyagePoor?1000:10),"actual normal start profile pins standard10 or declared high-price1000 diagnostic rules");
    }
    async Task VoyageDepart(string node,bool walk=false)
    {
        game!.SetDestination(node);game.Press(walk?"depart-walk":"depart");await WaitDeparture();Check(game.LastDepartureError==null,"actual route publication succeeds");game.Advance(game.Session.Seconds+3);
    }
    string LandNeighbor(){var e=BranchRoutes.GraphEdges(game!.Session.DiagnosticSnapshot()).First(e=>!NationalRoutes.Sea(e.Id)&&(e.From==VoyageMainland||e.To==VoyageMainland));return e.From==VoyageMainland?e.To:e.From;}
    string LocalWorkGoal()
    {
        var w=game!.Session.DiagnosticSnapshot();string here=NewGameWorld.Node(w.Locations.Single(l=>l.Actor==w.PlayerControl).Site!.Value.Value);var reachable=new HashSet<string>{here};bool changed=true;while(changed){changed=false;foreach(var e in BranchRoutes.GraphEdges(w).Where(e=>!NationalRoutes.Sea(e.Id))){if(reachable.Contains(e.From))changed|=reachable.Add(e.To);if(reachable.Contains(e.To))changed|=reachable.Add(e.From);}}
        return w.NewGame!.Start.Factions.Where(f=>reachable.Contains(NewGameWorld.Node(f.Home))).OrderBy(f=>f.Home,StringComparer.Ordinal).Select(f=>NewGameWorld.Node(f.Home)).First();
    }
    async Task VoyageEarn()
    {
        string goal=LocalWorkGoal();var site=game!.Session.DiagnosticSnapshot().Locations.Single(l=>l.Actor==game.Session.PlayerControl).Site!.Value;if(site!=RegionalCampaign.Site(goal)){await VoyageDepart(goal,true);game.Press("arrive");await WaitManualTime();}
        long before=game.ReviewPrivateCoins;await PressLocalIntent("work");game.Press("ten");await WaitManualTime();game.Press("work-details");game.Press("work-reply");game.Press("work-assent");game.Press("work-reply");game.Press("work-run");await WaitManualTime();game.Press("work-details");game.Press("work-reply");game.Press("work-pay");game.Press("work-reply");Check(game.ReviewPrivateCoins==before+20,"same traveller actually earns20 through ordinary conversation and local payment after voyage");game.Press("work-close");
    }
    async Task VoyageStep(int n)
    {
        if(n==1){await VoyageDepart(VoyageMainland);game!.Advance(CurrentBoardAt()-1);Check(TravelControl.PortWaiting(game.Session.DiagnosticSnapshot(),game.Session.PlayerControl)&&game.ReviewPrivateCoins==1000&&game.Session.DiagnosticSnapshot().VoyageExpenses==null,"actual waiting screen shows no boarding charge");}
        if(VoyageCancel&&n>=2){if(n==2)game!.Press("voyage-cancel");if(n==3)game!.Advance(game.Session.Seconds+10000);Check(game!.ReviewPrivateCoins==1000&&game.Session.DiagnosticSnapshot().VoyageExpenses==null,"ordinary cancel button prevents charge now and after departure time");game.ReviewFraction(0);game.Refresh();await Frame();return;}
        if(n==2){game!.Advance(CurrentBoardAt());Check(game.ReviewPrivateCoins==(VoyagePoor?0:990)&&game.Session.DiagnosticSnapshot().VoyageExpenses!.Payments.Length==1,"actual boarding charges private wallet exactly once");}
        if(n==3){game!.Press("arrive");await WaitManualTime();Check(game.Session.DiagnosticSnapshot().Locations.Single(l=>l.Actor==game.Session.PlayerControl).Site==RegionalCampaign.Site(VoyageMainland),"same traveller physically arrives at mainland port");}
        if(n==4){if(VoyagePoor){await VoyageDepart(VoyageIsland);game!.Advance(CurrentBoardAt()+1);Check(game.ReviewPrivateCoins==0&&game.Session.DiagnosticSnapshot().VoyageExpenses!.Refusals.Length==1&&game.ReviewVoyageHint.Contains("船賃が足りず"),"actual no-money screen stops at port without using another wallet");}else await VoyageDepart(LandNeighbor(),true);}
        if(n==5){if(VoyagePoor)await VoyageDepart(LandNeighbor(),true);else{game!.Press("arrive");await WaitManualTime();}}
        if(n==6){if(VoyagePoor){game!.Press("arrive");await WaitManualTime();Check(game.ReviewPrivateCoins==0,"zero coins did not prevent walking");}await VoyageEarn();}
        if(n==7){game!.Press("service-apply");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.Press("service-accept");game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.Press("work-close");Check(NewGameService.View(game.Session.DiagnosticSnapshot()).House!=null,"same travelling officer enters actual recognized local service");}
        if(n==8){game!.ChooseMilitary(NewMilitaryAction.Request);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.Press("work-close");game.ChooseMilitary(NewMilitaryAction.Accept);game.Advance(game.Session.Seconds+2);OpenAllServiceLetters();game.Press("work-close");game.ChooseMilitary(NewMilitaryAction.Pickup);game.ChooseMilitary(NewMilitaryAction.Load);game.ChooseMilitary(NewMilitaryAction.Deploy);var a=NewGameMilitary.Anchor(game.Session.DiagnosticSnapshot());game.ExerciseMilitaryLine(new(a.X+20000,a.Y-12000),new(a.X+20000,a.Y+12000));game.Advance(game.Session.Seconds+2);Check(NewGameMilitary.Moving(game.Session.DiagnosticSnapshot()),"same officer actually commands moving3D troops after travelling and earning");}
        if(n==9){game!.Advance(ArmyEnd()+1);game.ChooseMilitary(NewMilitaryAction.Recall);game.Advance(ArmyEnd()+1);game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Return);game.ChooseMilitary(NewMilitaryAction.Report);game.Advance(game.Session.Seconds+3);OpenAllServiceLetters();game.Press("work-close");Check(NewGameMilitary.View(game.Session.DiagnosticSnapshot()).Status.Contains("返還の通知を確認")&&game.ReviewPrivateCoins==(VoyagePoor?20:1010),"same officer returns command and preserves personal voyage/work balance");game.Press("work-receipts");game.Press("work-close");}
        game!.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunVoyageChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");int count=VoyageCancel?4:10;
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<count;n++){game!.LoadCheckpoint("voyage"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<count;next++)await VoyageStep(next);await Capture("continued-s"+n,true);}}
        else{await BeginVoyageEntry();for(int n=0;n<count;n++){await VoyageStep(n);game!.SaveCheckpoint("voyage"+n);await Capture("s"+n,true);}await Capture("final",true);game!.Press("save");await WaitSave();}
        File.WriteAllText(Path.Combine(TestOutput,restore?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("VOYAGE_EXPENSE_PASSED "+FlowCase);
    }
}
