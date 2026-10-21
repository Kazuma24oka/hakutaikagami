using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class ContinuousWorldTrial
{
    bool ReviewEnabled=>NoticeEnabled||Name=="MissionReviewTrial";
    Label reviewWarning=null!,reviewText=null!;
    OptionButton reviewReason=null!;
    EntityId ReviewTask()=>S.DiagnosticSnapshot().Tasks.Last(t=>t.Action==ActionKind.Inspect).Id;
    void BuildReviewUI(VBoxContainer col)
    {
        missionPanel.Visible=false;
        var row=new HFlowContainer();col.AddChild(row);
        Add(row,"review-interrupt","任務と異なる中断を試す",()=>Require(S.InterruptMission(S.GetActorView(ContractFixture.Officer).Tasks.Last(t=>MissionReview.CanInterrupt(S,t.Id,ContractFixture.Officer)).Id,ContractFixture.Officer,S.Revision)));
        reviewReason=new();reviewReason.AddItem("自己判断による中断");reviewReason.AddItem("危険回避と本人申告");row.AddChild(reviewReason);
        Add(row,"review-explain","中断理由を返送",SendReviewExplanation);
        reviewWarning=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(reviewWarning);
        reviewText=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(reviewText);
    }
    void SendReviewExplanation()
    {
        var w=S.DiagnosticSnapshot();var id=S.GetActorView(ContractFixture.Officer).Tasks.Last(t=>w.Tasks.Single(x=>x.Id==t.Id).InternalReason=="executor-interrupted"&&!w.Messaging.Messages.Any(m=>m.Kind==MissionReview.ExplanationKind&&m.CorrelationId==w.Tasks.Single(x=>x.Id==t.Id).RequestId.Value)).Id;
        var result=S.ExplainMission(id,ContractFixture.Officer,(MissionReview.Explanation)reviewReason.Selected,S.Revision);Require(result);
        if(result.Code==ResultCode.Accepted)reviewReason.Select(0);
    }
    void RefreshReview()
    {
        if(reviewText==null)return;var v=S.GetActorView(ContractFixture.Officer);var w=S.DiagnosticSnapshot();
        reviewWarning.Text=MissionReview.Warning(S,ContractFixture.Officer);
        buttons["review-interrupt"].Disabled=!v.Tasks.Any(t=>MissionReview.CanInterrupt(S,t.Id,ContractFixture.Officer));
        buttons["review-explain"].Disabled=!v.Tasks.Any(t=>w.Tasks.Single(x=>x.Id==t.Id).InternalReason=="executor-interrupted"&&!w.Messaging.Messages.Any(m=>m.Kind==MissionReview.ExplanationKind&&m.CorrelationId==w.Tasks.Single(x=>x.Id==t.Id).RequestId.Value));
        var reviews=MissionReview.View(S,ContractFixture.Lord);
        reviewText.Text="検証用・主君の直近審査（主人公へは未通知）："+(reviews.Length==0?"審査対象なし":string.Join(" / ",reviews.TakeLast(1).Select(c=>c.Achievement+" / "+c.Difference+"\n理由："+c.Reason+" / "+c.Liability)));
    }
    string ReviewPose()=>System.Text.Json.JsonSerializer.Serialize(new{Scene=IncomingPose(),Warning=reviewWarning.Text,Review=reviewText.Text,Cards=MissionReview.View(S,ContractFixture.Lord)});
    async Task ReviewCapture(string name){await Capture(name);System.IO.File.WriteAllText(Output+"/"+Case+"-"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(new[]{incomingText.GlobalPosition.X,incomingText.GlobalPosition.Y,Size.X-20,Size.Y-incomingText.GlobalPosition.Y-10}));}
    void ReviewTimeline(long target)
    {
        if(S.Paused)Click("resume");
        foreach(var at in new long[]{1,2,4,5}.Where(t=>S.Seconds<t&&t<=target)){
            if(at==1)S.AdvanceTo(at);else Advance(at);
            Refresh();
            if(at==1){Check(!MissionReview.CanInterrupt(S,ReviewTask(),ContractFixture.Officer),"unopened orders do not enable deviation");Click("incoming-open");if(S.Paused)Click("resume");Check(!buttons["review-interrupt"].Disabled,"actual UI warns and permits known personal interruption");}
            if(at==2&&Case=="own")Click("review-interrupt");
            if(at==4&&Case=="own"){Check(MissionReview.View(S,ContractFixture.Lord).Single().Reason.Contains("未認識"),"reason unknown until separately sent and delivered");reviewReason.Select(1);Click("review-explain");Check(MissionReview.View(S,ContractFixture.Lord).Single().Reason.Contains("未認識"),"sending reason does not reveal it to assessor");}
            if(at==5&&Case=="report-miss"){var id=S.DiagnosticSnapshot().Messaging.Messages.Single(m=>m.Kind=="contract-report").Id;S.DiagnosticTransport((m,t)=>MessageFlow.Fail(m,id));}
        }
        Advance(target);Refresh();
    }
    async Task RunReviewChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);long[] stages={1,2,4,7};
            if(Mode.EndsWith("restore")){
                for(int n=0;n<stages.Length;n++){Load(Case+"-s"+n);Check(ReviewPose()==System.IO.File.ReadAllText(Output+"/"+Case+"-s"+n+"-pose.json"),"restored task, explanation, actor knowledge and 3D "+n);await ReviewCapture("restored-s"+n);ReviewTimeline(12);await ReviewCapture("continued-s"+n);}
            }else{
                Click("incoming-lord");Check(!reviewWarning.Text.Contains("既知任務との不一致"),"in transit assignment never leaks into warning");
                for(int n=0;n<stages.Length;n++){
                    ReviewTimeline(stages[n]);if(stages[n]==2)Check(MissionReview.View(S,ContractFixture.Lord).Single().Achievement=="結果未確認","actual assessor UI does not see result before report");
                    Save(Case+"-s"+n);System.IO.File.WriteAllText(Output+"/"+Case+"-s"+n+"-pose.json",ReviewPose());await ReviewCapture("s"+n);
                }
                ReviewTimeline(12);await ReviewCapture("final");var c=MissionReview.View(S,ContractFixture.Lord).Single();var t=S.DiagnosticSnapshot().Tasks.Single();
                Check(c.Liability=="責任・処分は未審査","no automatic punishment or merit fault score");
                Check(t.Execution==(Case=="own"||Case=="expiry"?ExecutionPhase.Stopped:ExecutionPhase.Completed),"physical task outcome preserved independently of review");
                if(Case=="report-miss")Check(c.Achievement=="結果未確認"&&c.Evidence.Length==0,"lost report leaves assessor uninformed despite completed truth");
                else Check(c.Achievement!="結果未確認","report physically recognized by assessor");
                if(Case=="own")Check(c.Reason.Contains("危険の事実は未確認")&&c.Evidence.Length==2,"reason remains unverified claim and has distinct delivery evidence");
                Check(reviewText.GlobalPosition.Y+reviewText.Size.Y<Size.Y&&details.GlobalPosition.Y+details.Size.Y<=Size.Y+1,"review, viewport and bottom status fit actual window");
            }
            System.IO.File.WriteAllText(Output+"/"+Case+"-"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks}));GD.Print("MISSION_REVIEW_PASSED "+Case);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
