using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class ContinuousWorldTrial
{
    bool EntryEnabled=>CouncilEnabled||Name=="MissionEntryTrial"||OS.GetCmdlineUserArgs().Contains("--mission-entry");
    OptionButton entryPurpose=null!,entryExecutor=null!;
    SpinBox entryQuantity=null!,entryDeadline=null!;
    Label entrySummary=null!;
    HFlowContainer entryRow=null!;
    void BuildEntryUI(VBoxContainer col)
    {
        foreach(var id in new[]{"inspect","levy"})buttons[id].Visible=false;
        var row=new HFlowContainer();entryRow=row;col.AddChild(row);
        row.AddChild(new Label{Text="任務"});entryPurpose=new();entryPurpose.AddItem("点検");entryPurpose.AddItem("徴税");row.AddChild(entryPurpose);
        row.AddChild(new Label{Text="担当"});entryExecutor=new();entryExecutor.AddItem("本人");entryExecutor.AddItem("書役");row.AddChild(entryExecutor);
        row.AddChild(new Label{Text="数量"});entryQuantity=new(){MinValue=1,MaxValue=100,Step=1,Value=40,CustomMinimumSize=new(85,0)};row.AddChild(entryQuantity);
        row.AddChild(new Label{Text="完了期限（今から秒）"});entryDeadline=new(){MinValue=1,MaxValue=3600,Step=1,Value=60,CustomMinimumSize=new(95,0)};row.AddChild(entryDeadline);
        Add(row,"entry-send","この内容で送る",SendEntry);
        entryPurpose.ItemSelected+=_=>RefreshEntry();entryExecutor.ItemSelected+=_=>RefreshEntry();entryQuantity.ValueChanged+=_=>RefreshEntry();entryDeadline.ValueChanged+=_=>RefreshEntry();
        entrySummary=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(entrySummary);
    }
    void SendEntry()
    {
        // Read pending typed SpinBox text as well as stepped values before dispatch.
        entryQuantity.Apply();entryDeadline.Apply();
        var w=S.DiagnosticSnapshot();var purpose=entryPurpose.Selected==0?ActionKind.Inspect:ActionKind.Transfer;
        var intent=MissionEntry.Build(w,"request:entry/"+S.Revision,purpose,entryExecutor.Selected==0?S.PlayerControl:ContractFixture.Clerk,purpose==ActionKind.Transfer?(long)entryQuantity.Value:0,(int)entryDeadline.Value);
        var result=S.Submit(intent,S.Revision);Require(result);
        if(result.Code==ResultCode.Accepted){entryPurpose.Select(0);entryExecutor.Select(0);entryQuantity.Value=40;entryDeadline.Value=60;}
    }
    void RefreshEntry()
    {
        if(entrySummary==null)return;
        var w=S.DiagnosticSnapshot();if(CouncilEnabled){entryRow.Visible=!w.Region!.Active;entrySummary.Visible=!w.Region.Active;}entryQuantity.Editable=entryPurpose.Selected==1;
        entrySummary.Text="送信前："+(entryPurpose.Selected==0?"点検":$"徴税{entryQuantity.Value:0}")+" / "+(entryExecutor.Selected==0?"本人":"書役")+$" / 完了期限は{entryDeadline.Value:0}秒後。作業4秒・報告は配送。数量と秒は試作値です。";
        buttons["entry-send"].Disabled=w.Region!.Active||S.GetActorView(S.PlayerControl).Location.Site!=ContractFixture.Castle;
        missionPanel.Text=string.Join(" / ",S.GetActorView(S.PlayerControl).Tasks.TakeLast(3).Select(t=>{
            var i=w.Requests.Single(r=>w.Tasks.Single(x=>x.Id==t.Id).RequestId==r.Id).Intent;
            return (i.Action==ActionKind.Transfer?$"徴税{i.Amount}":i.Action==ActionKind.Inspect?"点検":"移動")+"・"+(i.Executor==ContractFixture.Clerk?"書役":"本人")+$"・期限{i.Deadline.At}秒："+MissionText(t.KnownStatus);
        }));
        if(w.Tasks.Length==0)missionPanel.Text="在所の任務：なし";
    }
    void ChooseEntry()
    {
        entryPurpose.Select(Case=="self"?0:1);entryExecutor.Select(Case=="self"?0:1);
        entryQuantity.Value=27;entryDeadline.Value=Case=="expiry"?3:37;RefreshEntry();
    }
    string EntryPose()=>System.Text.Json.JsonSerializer.Serialize(new{Scene=LoopPose(),Mission=missionPanel.Text,Draft=entrySummary.Text,Requests=S.DiagnosticSnapshot().Requests.Select(r=>r.Intent),Transfers=S.DiagnosticSnapshot().Transfers});
    async Task EntryCapture(string name)
    {
        await Capture(name);
        System.IO.File.WriteAllText(Output+"/"+Case+"-"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(new[]{missionPanel.GlobalPosition.X,missionPanel.GlobalPosition.Y,Size.X-20,Size.Y-missionPanel.GlobalPosition.Y-10}));
    }
    async Task RunEntryChecks()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Output);long[] stages={1,2,5};const long final=12;
            if(Mode.EndsWith("restore"))
            {
                for(int n=0;n<stages.Length;n++)
                {
                    Load(Case+"-s"+n);Refresh();Check(EntryPose()==System.IO.File.ReadAllText(Output+"/"+Case+"-s"+n+"-pose.json"),"issued contents, knowledge, world and display restored "+n);
                    await EntryCapture("restored-s"+n);Click("resume");Advance(final);Refresh();await EntryCapture("continued-s"+n);
                }
            }
            else
            {
                ChooseEntry();await EntryCapture("draft");Click("entry-send");var w=S.DiagnosticSnapshot();var intent=w.Requests.Single().Intent;
                Check(intent.Amount==(Case=="self"?0:27)&&intent.Deadline.At==(Case=="expiry"?3:37)&&intent.Executor==(Case=="self"?S.PlayerControl:ContractFixture.Clerk),"actual UI sends selected purpose, executor, amount and deadline");
                Check(S.Submit(intent,S.Revision).Code==ResultCode.AlreadyAccepted,"retry uses same task without duplicate effect");
                for(int n=0;n<stages.Length;n++)
                {
                    Advance(stages[n]);Refresh();Save(Case+"-s"+n);System.IO.File.WriteAllText(Output+"/"+Case+"-s"+n+"-pose.json",EntryPose());await EntryCapture("s"+n);
                    if(Case=="success"&&stages[n]==5)Check(S.GetActorView(S.PlayerControl).Tasks.Single().KnownStatus=="発送済・結果未確認","completed levy not leaked before return report");
                }
                Advance(final);Refresh();await EntryCapture("final");w=S.DiagnosticSnapshot();ContractValidation.Validate(w);
                Check(w.Transfers.Length==(Case=="success"?1:0),"expired, refused and inspection duties do not transfer resources");
                if(Case=="success")Check(w.Transfers.Single().Amount==27,"chosen levy quantity applied once");
                if(Case=="expiry")Check(w.Tasks.Single().Execution==ExecutionPhase.Stopped,"completion deadline expires while work is pending");
                Check(!S.GetActorView(S.PlayerControl).Tasks.Single().KnownStatus.Contains("未確認"),"player eventually receives physically delivered report");
                Check(entrySummary.GlobalPosition.Y+entrySummary.Size.Y<Size.Y&&details.GlobalPosition.Y+details.Size.Y<=Size.Y+1,"mission and bottom status fit the actual viewport");
            }
            System.IO.File.WriteAllText(Output+"/"+Case+"-"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks}));
            GD.Print("MISSION_ENTRY_PASSED "+Case);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
