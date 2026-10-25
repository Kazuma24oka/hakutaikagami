using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class NewGameFrontEnd
{
    Label? demandCaption;
    bool DemandFilm=>OS.GetCmdlineUserArgs().Contains("--demand-film");
    async Task DemandHold(double t){if(DemandFilm)await TalkHold(t);}
    async Task StartDemandJob()
    {
        game!.Press("work");game.Press("work-reply");await Capture("offer-"+game.Session.Seconds,true);Check(game.ReviewWorkLayout(),"needed work terms fit compact conversation");await DemandHold(1.5);game.Press("work-confirm");game.Press("work-reply");await WaitDeparture();game.Press("ten");await WaitManualTime();game.Press("work-details");game.Press("work-reply");game.Press("work-assent");game.Press("work-reply");
    }
    async Task FinishDemandJob()
    {
        var s=game!.Session;
        if(FlowCase=="demand-failed"&&s.Seconds<10){game.Advance(s.Seconds+4);var paper=s.DiagnosticSnapshot().Outbox.First(o=>o.Kind=="route-observation"&&o.Sender==s.PlayerControl);s.DiagnosticTransport((m,t)=>MessageFlow.Fail(m,paper.Id.Value));game.Press("ten");await WaitManualTime();game.Press("work");game.Press("work-reply");Check(game.WorkScreen=="現地の相談","lost original does not create a duplicate offer");await Capture("missing-original",true);game.Press("work-close");game.Press("work-retry");game.Press("ten");await WaitManualTime();}
        else {game.Press("work-run");await WaitManualTime();}
        s=game.Session;game.Press("work-details");Check(game.WorkScreen=="依頼主からの返答","same physical reply opens after originals arrive");game.Press("work-reply");await DemandHold(1.5);game.Press("work-pay");game.Press("work-reply");game.Press("work-close");
        Check(!LocalWorkDemand.Candidates(s.DiagnosticSnapshot()).Any(),"received current road records satisfy nearby issuer");game.Press("work");game.Press("work-reply");Check(game.WorkScreen=="現地の相談"&&game.ReviewWorkLayout(),"explicit local consultation explains no new job without leaking records");await Capture("no-new-job-"+s.Seconds,true);await DemandHold(2);game.Press("work-close");
    }
    async Task DemandStep(int n)
    {
        var s=game!.Session;if(demandCaption!=null)demandCaption.Text=n switch{1=>"検証録画：必要な点検を引き受ける",2=>"検証録画：返報を受け取ると、新しい依頼は出ない",3=>"検証録画：約1時間進めて、記録の更新需要を確認",4=>"検証録画：新しい需要の点検をもう一度実行",_=>"検証録画：拠点で仕事を相談"};
        if(n==1){await StartDemandJob();s=game.Session;Check(s.DiagnosticSnapshot().Requests.Count(r=>r.Intent.Actor==new EntityId("person:takeda-nobushige")&&r.Intent.Action==ActionKind.Inspect)==1,"NPC does not duplicate the inspection already issued to this player");}
        if(n==2)await FinishDemandJob();
        if(n==3){if(FlowCase=="demand-travel"){game.SetDestination("castle");game.Press("depart");await WaitDeparture();game.Press("ten");await WaitManualTime();for(int guard=0;game.Session.GetBranchView(game.Session.PlayerControl).Knowledge.Location.Kind!=LocationKind.AtSite&&guard<32;guard++){game.Press("arrive");await WaitManualTime();}Check(game.Session.GetBranchView(game.Session.PlayerControl).Knowledge.Location.Site==new EntityId("site:castle"),"same officer actually travels to another lord's site before consulting");await Capture("other-site",true);}if(game.Session.Seconds<3610)game.Advance(3610);s=game.Session;Check(LocalWorkDemand.Candidates(s.DiagnosticSnapshot()).Length>0,"old known record creates a new real need");game.Press("work");game.Press("work-reply");Check(game.WorkScreen=="現地の点検","same consultation offers inspection again only after record ages");await Capture("record-aged",true);await DemandHold(2);game.Press("work-close");}
        if(n==4){await StartDemandJob();await FinishDemandJob();s=game.Session;Check(game.ReviewPrivateCoins==1040&&s.DiagnosticSnapshot().Transfers.Length==2,"two real distinct completed needs pay exactly two conserved fees");}
        game.ReviewFraction(0);game.Refresh();await Frame();
    }
    async Task RunDemandChecks()
    {
        bool restore=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
        if(restore){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<5;n++){game!.LoadCheckpoint("demand"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<5;next++)await DemandStep(next);await Capture("continued-s"+n,true);}}
        else {await BeginPopulationEntry(0,"person:shingen");Check(game!.Profile.DemandPolicy==LocalWorkDemand.Policy,"normal entry pins new demand profile");if(DemandFilm){demandCaption=new(){Position=new(28,116),ZIndex=70};demandCaption.AddThemeColorOverride("font_color",new Color("ffe4a6"));AddChild(demandCaption);}for(int n=0;n<5;n++){await DemandStep(n);game.SaveCheckpoint("demand"+n);await Capture("s"+n,true);}await Capture("final",true);game.Press("save");await WaitSave();}
        File.WriteAllText(Path.Combine(TestOutput,restore?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("LOCAL_DEMAND_PASSED "+FlowCase);
    }
}
