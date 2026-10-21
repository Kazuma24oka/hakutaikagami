using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class ContinuousWorldTrial
{
    bool NoticeEnabled=>Name=="AssessmentNoticeTrial";
    Label noticeBody=null!,noticeResponse=null!;
    OptionButton noticeReply=null!;
    void BuildNoticeUI(VBoxContainer col)
    {
        reviewText.Visible=false;
        var row=new HFlowContainer();col.AddChild(row);
        Add(row,"notice-publish","検証：主君が評価・訂正版を発送",()=>Require(S.PublishAssessment(AssessmentNotice.NextMessageId(S,ReviewTask(),ContractFixture.Lord),ReviewTask(),ContractFixture.Lord,S.Revision)));
        Add(row,"notice-open","届いた評価書状を開封",()=>Require(S.Open(ContractFixture.Officer,AssessmentNotice.Inbox(S,ContractFixture.Officer).Last(c=>c.CanOpen).Message,S.Revision)));
        noticeReply=new();noticeReply.AddItem("受領確認");noticeReply.AddItem("再審査を希望");row.AddChild(noticeReply);
        Add(row,"notice-reply","評価への回答を返送",SendNoticeReply);
        noticeBody=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(noticeBody);
        noticeResponse=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(noticeResponse);
    }
    AssessmentNotice.Card? LatestNotice(){var known=AssessmentNotice.LatestKnown(S,ContractFixture.Officer).LastOrDefault();return known==null?null:AssessmentNotice.Inbox(S,ContractFixture.Officer).Single(c=>c.Known?.Task==known.Task&&c.Known?.Version==known.Version);}
    void SendNoticeReply(){var result=S.RespondAssessment(LatestNotice()!.Message,ContractFixture.Officer,(AssessmentNotice.Reply)noticeReply.Selected,S.Revision);Require(result);if(result.Code==ResultCode.Accepted)noticeReply.Select(0);}
    void RefreshNotice()
    {
        if(noticeBody==null)return;var w=S.DiagnosticSnapshot();var cards=AssessmentNotice.Inbox(S,ContractFixture.Officer);var latest=LatestNotice();
        buttons["notice-open"].Disabled=!cards.Any(c=>c.CanOpen);
        buttons["notice-publish"].Disabled=w.Tasks.Length==0||!AssessmentNotice.CanPublish(S,ReviewTask(),ContractFixture.Lord);
        buttons["notice-reply"].Disabled=latest==null||w.Messaging.Messages.Any(m=>m.Kind==AssessmentNotice.ResponseKind&&m.SenderId==ContractFixture.Officer.Value&&ContractJson.Read<AssessmentNotice.Response>(System.Text.Encoding.UTF8.GetBytes(m.Body)).Notice==latest.Message);
        noticeBody.Text=latest==null?(cards.Length==0?"本人の評価通知：未着":"本人の評価通知：書状到着・未開封（内容未確認）"):
            "本人が開封した評価 第"+latest.Known!.Version+"版（暫定）："+latest.Known.Review.Achievement+" / "+latest.Known.Review.Difference+"\n理由："+latest.Known.Review.Reason+" / "+latest.Known.Review.Liability;
        var replies=AssessmentNotice.KnownResponses(S,ContractFixture.Lord);string replyText="未着・未認識";if(replies.Length>0){var reply=replies.Last();var p=ContractJson.Read<AssessmentNotice.Payload>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==reply.Notice.Value).Body));replyText="第"+p.Version+"版への"+(reply.Reply==AssessmentNotice.Reply.RequestReview?"再審査希望を受領（処分未確定）":"受領確認を受領");}noticeResponse.Text="検証：主君が認識した回答＝"+replyText;
    }
    EntityId NoticeId(int version)=>new(S.DiagnosticSnapshot().Messaging.Messages.Single(m=>m.Kind==AssessmentNotice.Kind&&ContractJson.Read<AssessmentNotice.Payload>(System.Text.Encoding.UTF8.GetBytes(m.Body)).Version==version).Id);
    string NoticePose()=>System.Text.Json.JsonSerializer.Serialize(new{Scene=IncomingPose(),Body=noticeBody.Text,Response=noticeResponse.Text,Inbox=AssessmentNotice.Inbox(S,ContractFixture.Officer),Latest=AssessmentNotice.LatestKnown(S,ContractFixture.Officer),Replies=AssessmentNotice.KnownResponses(S,ContractFixture.Lord)});
    async Task NoticeCapture(string name){await Capture(name);System.IO.File.WriteAllText(Output+"/"+Case+"-"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(new[]{incomingText.GlobalPosition.X,incomingText.GlobalPosition.Y,Size.X-20,Size.Y-incomingText.GlobalPosition.Y-10}));}
    void NoticeTimeline(long target)
    {
        foreach(var at in new long[]{1,2,7,8,9,10,11,12,13,14,16,17,18}.Where(t=>S.Seconds<t&&t<=target)){
            if(S.Paused)Click("resume");Advance(at);Refresh();
            if(at==1){Click("incoming-open");if(S.Paused)Click("resume");}
            if(at==2&&(Case=="own"||Case=="report-miss"))Click("review-interrupt");
            if(at==7){Click("notice-publish");Check(AssessmentNotice.Inbox(S,ContractFixture.Officer).Length==0,"notice contents do not leak while in transit");if(Case=="report-miss")S.DiagnosticTransport((m,t)=>MessageFlow.Fail(m,NoticeId(1).Value));}
            if(at==8){Check(AssessmentNotice.LatestKnown(S,ContractFixture.Officer).Length==0,"closed or lost notice has no visible payload or version");if(Case!="report-miss")Check(S.Paused,"important assessment arrival pauses actual clock");}
            if(at==9&&Case!="report-miss"){Click("notice-open");if(Case=="success"||Case=="expiry"){noticeReply.Select(Case=="expiry"?1:0);Click("notice-reply");Check(AssessmentNotice.KnownResponses(S,ContractFixture.Lord).Length==0,"sending response does not reveal it to assessor");}}
            if(at==10&&(Case=="own"||Case=="report-miss")){reviewReason.Select(1);Click("review-explain");}
            if(at==12&&(Case=="own"||Case=="report-miss")){Click("notice-publish");Check(AssessmentNotice.LatestKnown(S,ContractFixture.Officer).SingleOrDefault()?.Version!=(int?)2,"new revision does not reveal itself on dispatch");}
            if(at==14&&(Case=="own"||Case=="report-miss")){Click("notice-open");Check(AssessmentNotice.LatestKnown(S,ContractFixture.Officer).Single().Version==2,"opening recognizes immutable corrected notice");noticeReply.Select(1);Click("notice-reply");}
            if(at==16&&Case=="report-miss")S.DiagnosticTransport((m,t)=>MessageFlow.Retry(m,NoticeId(1).Value,ContractFixture.Lord.Value,t));
            if(at==18&&Case=="report-miss"){Click("notice-open");Check(AssessmentNotice.LatestKnown(S,ContractFixture.Officer).Single().Version==2,"older physically resent original does not roll back revised view");}
        }
        if(S.Paused&&S.Seconds<target)Click("resume");Advance(target);Refresh();
    }
    async Task RunNoticeChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);long[] stages={7,8,12,18};
            if(Mode.EndsWith("restore")){
                for(int n=0;n<stages.Length;n++){Load(Case+"-s"+n);Check(NoticePose()==System.IO.File.ReadAllText(Output+"/"+Case+"-s"+n+"-pose.json"),"restored sealed notices, revisions, replies, position and 3D "+n);await NoticeCapture("restored-s"+n);NoticeTimeline(20);await NoticeCapture("continued-s"+n);}
            }else{
                Click("incoming-lord");for(int n=0;n<stages.Length;n++){NoticeTimeline(stages[n]);Save(Case+"-s"+n);System.IO.File.WriteAllText(Output+"/"+Case+"-s"+n+"-pose.json",NoticePose());await NoticeCapture("s"+n);}
                NoticeTimeline(20);await NoticeCapture("final");var p=AssessmentNotice.LatestKnown(S,ContractFixture.Officer).Single();
                Check(p.Version==(Case=="own"||Case=="report-miss"?2:1),"recipient only uses highest known version");Check(p.Review.Liability=="責任・処分は未審査","notification and reply never impose automatic punishment");
                Check(AssessmentNotice.KnownResponses(S,ContractFixture.Lord).Length==1,"reply has actually reached assessor");
                Check(noticeResponse.GlobalPosition.Y+noticeResponse.Size.Y<Size.Y&&details.GlobalPosition.Y+details.Size.Y<=Size.Y+1,"notification, response and 3D fit actual viewport");
            }
            System.IO.File.WriteAllText(Output+"/"+Case+"-"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks}));GD.Print("ASSESSMENT_NOTICE_PASSED "+Case);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
