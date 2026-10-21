using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class ContinuousWorldTrial
{
    bool LoopEnabled => EntryEnabled || Name=="OfficerLoopTrial" || OS.GetCmdlineUserArgs().Contains("--officer-loop");
    Label missionPanel=null!, councilNotice=null!;
    HFlowContainer councilPanel=null!;
    void BuildLoopUI(VBoxContainer col,HFlowContainer bar)
    {
        foreach(var id in new[]{"move","bridge","order","engage","retreat","hold"})buttons[id].Visible=false;
        var row=new HFlowContainer();col.AddChild(row);
        Add(row,"inspect","本人：在所の点検",()=>Require(S.Submit(OfficerLoop.Mission(S.DiagnosticSnapshot(),"request:inspect/"+S.Revision,false),S.Revision)));
        Add(row,"levy","書役へ：徴税40を委任",()=>Require(S.Submit(OfficerLoop.Mission(S.DiagnosticSnapshot(),"request:levy/"+S.Revision,true),S.Revision)));
        Add(row,"council","軍議を開く・閉じる",()=>councilPanel.Visible=!councilPanel.Visible);
        Add(row,"own-line","本隊：地点へ移動",()=>Issue(RegionalAction.Move,65000,-85000,85000,-85000));
        Add(row,"own-engage","本隊：交戦を指示",()=>Issue(RegionalAction.Engage));
        Add(row,"own-return","本隊：帰還",()=>Issue(RegionalAction.Retreat));
        councilPanel=new HFlowContainer{Visible=false};col.AddChild(councilPanel);
        Add(councilPanel,"council-line","別働隊：指定線を保持",()=>Require(S.IssueRegional(OfficerLoop.CouncilLine(S.DiagnosticSnapshot(),"request:council/"+S.Revision),S.Revision)));
        Add(councilPanel,"council-return","別働隊：帰還を伝令",()=>Issue(RegionalAction.Retreat,clerk:true));
        councilNotice=new(){Text="目的：画面奥の線に布陣 / 指示：移動（開戦指示なし） / 報告：受令判断を物理返報（到着報告ではない）"};col.AddChild(councilNotice);
        missionPanel=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(missionPanel);
    }
    static string MissionText(string s)=>s.Replace("本人の履行: ","本人：").Replace("NotStarted","未着手").Replace("Waiting","待機").Replace("Running","作業中").Replace("Completed","完了").Replace("Stopped","中止").Replace("Failed","失敗").Replace("completed","完了の返報").Replace("unavailable","権限・服従条件を満たさず中止");
    void RefreshLoop()
    {
        var w=S.DiagnosticSnapshot();var v=S.GetActorView(ContractFixture.Officer);
        // Only the player's issued intent and their evidence-filtered status are displayed.
        missionPanel.Text="在所の任務："+ (v.Tasks.Length==0?"なし。本人の点検と書役への委任は同時に進められます。":string.Join(" / ",v.Tasks.TakeLast(3).Select(t=>{
            var i=w.Requests.Single(r=>w.Tasks.Single(x=>x.Id==t.Id).RequestId==r.Id).Intent;
            return (i.Action==ActionKind.Transfer?$"徴税{i.Amount}":i.Action==ActionKind.Inspect?"点検":"街道移動")+"："+MissionText(t.KnownStatus);
        })));
        buttons["inspect"].Disabled=w.Region!.Active||OfficerLoop.Busy(w,ContractFixture.Officer)||v.Location.Site!=ContractFixture.Castle;
        buttons["levy"].Disabled=w.Region.Active||v.Location.Site!=ContractFixture.Castle;
        foreach(var id in new[]{"own-line","own-engage","own-return","council-line","council-return"})buttons[id].Disabled=!w.Region.Active;
        councilNotice.Visible=councilPanel.Visible;
        if(councilPanel.Visible)councilNotice.Text="目的：自地域の南側線に布陣 / 指示：移動（開戦指示なし） / 報告：受令判断の物理返報（到着報告ではない）";
    }
    string LoopPose()=>System.Text.Json.JsonSerializer.Serialize(new{Scene=Pose(),Tasks=S.GetActorView(ContractFixture.Officer).Tasks.Select(t=>new{t.Id,t.KnownStatus,t.DeadlineAt}),Panel=missionPanel.Text,Council=councilPanel.Visible,Models=actors.OrderBy(k=>k.Key.Value,StringComparer.Ordinal).Where(k=>k.Value.Visible).Select(k=>new{Id=k.Key.Value,Position=new[]{k.Value.Position.X,k.Value.Position.Y,k.Value.Position.Z},Rotation=new[]{k.Value.Rotation.X,k.Value.Rotation.Y,k.Value.Rotation.Z},k.Value.Lod})});
    void LoopArrange(){Click("inspect");Click("levy");Refresh();}
    void LoopTimeline(long target)
    {
        var events=new long[]{8,9,45,90,180,260,400,410};
        foreach(var at in events.Where(t=>S.Seconds<t&&t<=target))
        {
            Advance(at);
            if(at==8){Click("site");Click("council");}
            if(at==9)Click("council-line");
            if(at==(Case=="report-miss"?45:90)){if(Case=="combat")Click("own-engage");else Click("own-line");}
            if(at==180&&Case!="combat"){if(Case!="refuse")Click("council-return");}
            if(at==260&&Case!="combat"){Click("own-return");}
            if(at==400&&Case=="roundtrip"){Click("site");Click("council");}
            if(at==410&&Case=="roundtrip")Click("inspect");
        }
        Advance(target);Refresh();
    }
    async Task RunLoopChecks()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Output);
            var stages=new long[]{2,5,25,75,210};const long final=430;
            if(Mode.EndsWith("restore"))
            {
                for(int i=0;i<stages.Length;i++)
                {
                    Load(Case+"-s"+i);Refresh();
                    Check(LoopPose()==System.IO.File.ReadAllText(Output+"/"+Case+"-s"+i+"-pose.json"),"tasks/knowledge/models/camera restored "+i);
                    await Capture("restored-s"+i);Click("resume");LoopTimeline(final);await Capture("continued-s"+i);
                }
            }
            else
            {
                LoopArrange();
                var w=S.DiagnosticSnapshot();var unchanged=ContractJson.CanonicalHash(w);
                Check(S.SetRegionalActive(true,S.Revision).Code!=ResultCode.Accepted,"pending civilian duties block deployment in Core");
                Check(unchanged==ContractJson.CanonicalHash(S.DiagnosticSnapshot()),"failed deployment has no world mutation");
                foreach(var i in Enumerable.Range(0,stages.Length))
                {
                    LoopTimeline(stages[i]);Refresh();Save(Case+"-s"+i);
                    System.IO.File.WriteAllText(Output+"/"+Case+"-s"+i+"-pose.json",LoopPose());await Capture("s"+i);
                    System.IO.File.WriteAllText(Output+"/"+Case+"-s"+i+"-rect.json",System.Text.Json.JsonSerializer.Serialize(new[]{surface.GlobalPosition.X,surface.GlobalPosition.Y,surface.Size.X,surface.Size.Y}));
                    if(stages[i]==5&&Case!="refuse")Check(S.GetActorView(ContractFixture.Officer).Tasks.Any(t=>t.Executor==ContractFixture.Clerk&&t.KnownStatus=="発送済・結果未確認"),"levy outcome hidden before physical report");
                }
                LoopTimeline(final);await Capture("final");System.IO.File.WriteAllText(Output+"/"+Case+"-final-rect.json",System.Text.Json.JsonSerializer.Serialize(new[]{surface.GlobalPosition.X,surface.GlobalPosition.Y,surface.Size.X,surface.Size.Y}));w=S.DiagnosticSnapshot();ContractValidation.Validate(w);
                Check(w.Transfers.Length==(Case=="refuse"?0:1),"levy committed once or refused without resource transfer");
                var council=w.Region!.Commands.First(c=>c.Request.Recipient==ContractFixture.Clerk);
                Check(RegionalBattle.Status(w,council,ContractFixture.Officer)==(Case=="refuse"?"refused":Case=="report-miss"?"発送済・結果未確認":"applied"),"council decision physically reported to player");
                if(Case=="roundtrip")Check(!w.Region.Active&&w.Tasks.Last().Execution==ExecutionPhase.Completed,"returned to site and completed next peace duty");
                if(Case=="combat")Check(w.Region.Losses.Length>0,"council and civilian missions share combat world clock");
                Check(w.Seed==OfficerLoop.Create().Seed,"fixed seed preserved");
                var hash=ContractJson.CanonicalHash(w);Click("council");Refresh();Check(hash==ContractJson.CanonicalHash(S.DiagnosticSnapshot()),"opening council never stops time or changes world");Click("council");
            }
            System.IO.File.WriteAllText(Output+"/"+Case+"-"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,ViewportRect=new[]{surface.GlobalPosition.X,surface.GlobalPosition.Y,surface.Size.X,surface.Size.Y}}));
            GD.Print("OFFICER_LOOP_PASSED "+Case);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
    void LoopVideo()
    {
        int f=frame++;
        if(f==0)LoopArrange();
        long target=f<96?(long)(f/16.0):f<192?6+(long)((f-96)/96.0*39):f<384?45+(long)((f-192)/192.0*135):180+(long)((f-384)/192.0*250);
        if(f==264)Save("movie");
        if(f==288){Load("movie");Click("resume");}
        LoopTimeline(Math.Max(S.Seconds,target));
        notice.Text=f<96?"① 点検と徴税の委任。同じ世界時間で並行進行":f<192?"② 返報を受け、野外の軍議から布陣を指示":f<264?"③ 伝令の到着後、別働隊が判断して布陣":f<312?"④ 途中保存・復旧。任務と位置を保持":f<384?"⑤ 本隊の地点移動。受令判断と到着を区別":f<528?"⑥ 帰還命令も伝令で配送":"⑦ 在所へ戻り、次の平時任務へ";
        Refresh();if(f>=575){GD.Print("OFFICER_LOOP_VIDEO_PASSED 576");GetTree().Quit();}
    }
}
