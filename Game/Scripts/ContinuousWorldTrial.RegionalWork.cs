using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    bool RegionalWorkEnabled=>NpcWorkEnabled||Name=="RegionalWorkTrial";
    string WorkNode=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--node="))?[7..]??"castle";
    OptionButton workDispatcher=null!;
    Label workText=null!,workRole=null!;
    void BuildRegionalWorkUI(VBoxContainer col)
    {
        foreach(var id in new[]{"inspect","levy","council","own-line","own-engage","own-return"})buttons[id].Visible=false;
        missionPanel.Visible=false;councilNotice.Visible=false;
        var row=new HFlowContainer();col.AddChild(row);workDispatcher=new();row.AddChild(workDispatcher);
        Add(row,"work-send","検証：現地人物から点検依頼を発送",()=>{
            var issuer=new EntityId(workDispatcher.GetItemMetadata(workDispatcher.Selected).AsString());
            Require(S.Submit(RegionalWork.Build(S.DiagnosticSnapshot(),"request:regional-work/"+S.Revision,issuer,Case=="expiry"?2:60),S.Revision));
        });
        Add(row,"work-open","届いた書状を開封",()=>Require(S.Open(ContractFixture.Officer,RegionalWork.Inbox(S).Last(c=>c.CanOpen).Message,S.Revision)));
        Add(row,"work-accept","受諾",()=>WorkConsent(true));Add(row,"work-decline","辞退",()=>WorkConsent(false));
        workRole=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(workRole);
        workText=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(workText);
    }
    void WorkConsent(bool yes)=>Require(S.Assent(RegionalWork.Inbox(S).Last(c=>c.CanDecide).Task!.Value,ContractFixture.Officer,yes,S.Revision));
    void RefreshRegionalWork()
    {
        councilNotice.Visible=false;
        var nearby=RegionalWork.NearbyDispatchers(S);var w=S.DiagnosticSnapshot();
        string selected=workDispatcher.ItemCount>0?workDispatcher.GetItemMetadata(workDispatcher.Selected).AsString():"";
        workDispatcher.Clear();foreach(var id in nearby){workDispatcher.AddItem(w.Definitions.Characters.Single(p=>p.Id==id).Name);workDispatcher.SetItemMetadata(workDispatcher.ItemCount-1,id.Value);if(id.Value==selected)workDispatcher.Select(workDispatcher.ItemCount-1);}
        buttons["work-send"].Disabled=nearby.Length==0||!RegionalWork.CanDispatch(w,new(workDispatcher.GetItemMetadata(workDispatcher.Selected).AsString()));
        var inbox=RegionalWork.Inbox(S);buttons["work-open"].Disabled=!inbox.Any(c=>c.CanOpen);buttons["work-accept"].Disabled=buttons["work-decline"].Disabled=!inbox.Any(c=>c.CanDecide);
        workRole.Text=nearby.Length==0?"現地で会える依頼元：なし。拠点へ移動してください。":string.Join(" / ",nearby.Select(id=>{var role=RegionalWork.Roles.Single(r=>r.Person==id);return w.Definitions.Characters.Single(p=>p.Id==id).Name+"："+role.Position;}));
        workText.Text=inbox.Length==0?"本人の受信：なし（未着の内容は表示しません）":string.Join("\n",inbox.TakeLast(2).Select(c=>MissionText(c.Text)));
        workText.Text+="\n任意点検は試作設定。史実の具体的命令・主従関係・徴税権は追加していません。";
        // One actor at the authoritative player point; NPC and player share a site anchor and are not artificially displaced.
        var key=ContractFixture.Officer;
        if(!actors.TryGetValue(key,out var model)){model=new();world.AddChild(model);model.Apply(Officer3DCustomActor.Preset("red_guard"));actors[key]=model;}
        if(NpcWorkEnabled){buttons["work-send"].Visible=false;workDispatcher.Visible=false;buttons["site"].Visible=false;details.Text="平時の地域仕事 / 軍議・戦闘は従来の検証に残しています（別世界）";if(notice.Text.StartsWith("地面を左ドラッグ"))notice.Text="行先を選んで移動。時間を進めると、現地で任意の依頼が届きます。";}
        var view=S.GetBranchView(key);model.Visible=span<=240&&!w.Region!.Active;model.Scale=Vector3.One*2;model.Position=P(Current());model.Rotation=new(0,Mathf.Atan2(view.Heading.EastMm,-view.Heading.NorthMm),0);model.SetLod("mid");
        string clip=view.Walking?"walk":"idle";model.Freeze(clip,S.Seconds%model.Player.GetAnimation(model.Clips[clip]).Length);if(model.Visible)playerMarker.Visible=false;
    }
    string WorkPose()=>System.Text.Json.JsonSerializer.Serialize(new{Scene=LoopPose(),workText.Text,Role=workRole.Text,Inbox=RegionalWork.Inbox(S),Position=S.GetBranchView(ContractFixture.Officer).Position,Models=actors.Where(p=>p.Value.Visible).Select(p=>new{p.Key,p.Value.Position,p.Value.Rotation,p.Value.Lod})});
    long WorkAt=>WorkNode=="castle"?0:S.DiagnosticSnapshot().Branches!.Journeys.First().Legs[^1].EndAt;
    void WorkAdvance(long at){if(S.Paused)Click("resume");Advance(at);Refresh();}
    void WorkArrive(){if(TravelWorld.Active(S.DiagnosticSnapshot()))WorkAdvance(WorkAt);}
    void WorkContinue()
    {
        WorkArrive();long at=WorkAt;
        if(!S.DiagnosticSnapshot().Tasks.Any(t=>t.Action==ActionKind.Inspect))Click("work-send");
        if(S.Seconds<at+1)WorkAdvance(at+1);
        if(RegionalWork.Inbox(S).Any(c=>c.CanOpen))Click("work-open");
        if(RegionalWork.Inbox(S).Any(c=>c.CanDecide)&&Case!="expiry")Click(Case=="refuse"?"work-decline":"work-accept");
        WorkAdvance(at+8);
    }
    async Task WorkCapture(string name){await Capture(name);System.IO.File.WriteAllText(Output+"/"+Case+"-"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(new[]{0,workRole.GlobalPosition.Y,Size.X,Size.Y-workRole.GlobalPosition.Y}));}
    async Task RunRegionalWorkChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);
            if(Mode.EndsWith("restore")){
                for(int n=0;n<5;n++){Load(Case+"-s"+n);Check(WorkPose()==System.IO.File.ReadAllText(Output+"/"+Case+"-s"+n+"-pose.json"),"restored travel, consent, knowledge, position and actual 3D "+n);await WorkCapture("restored-s"+n);WorkContinue();await WorkCapture("continued-s"+n);}
            }else{
                Check(RegionalWork.Roles.Any(r=>r.Node==WorkNode),"sourced regional work node");
                if(WorkNode!="castle"){
                    destination.Select(Array.FindIndex(NationalRoutes.Nodes,n=>n.Id==WorkNode));Click("depart");WorkAdvance(1);
                    var leg=S.DiagnosticSnapshot().Branches!.Journeys.Single().Legs[0];WorkAdvance(Math.Max(1,(leg.StartAt+leg.EndAt)/2));
                    Check(TravelWorld.Active(S.DiagnosticSnapshot())&&RegionalWork.NearbyDispatchers(S).Length==0,"actual road travel cannot reveal remote dispatcher");
                }
                for(int n=0;n<5;n++){
                    if(n==1)WorkArrive();
                    if(n==2){Click("work-send");Check(RegionalWork.Inbox(S).Length==0,"undelivered job contents hidden");WorkAdvance(WorkAt+1);Check(S.Paused&&RegionalWork.Inbox(S).Single().Task==null,"arrival pauses but unopened job remains hidden");}
                    if(n==3){Click("work-open");if(Case!="expiry")Click(Case=="refuse"?"work-decline":"work-accept");if(Case=="success")WorkAdvance(WorkAt+2);}
                    if(n==4)WorkAdvance(WorkAt+(Case=="success"?5:Case=="expiry"?3:2));
                    Save(Case+"-s"+n);System.IO.File.WriteAllText(Output+"/"+Case+"-s"+n+"-pose.json",WorkPose());await WorkCapture("s"+n);
                }
                WorkContinue();await WorkCapture("final");var w=S.DiagnosticSnapshot();var task=w.Tasks.Last(t=>t.Action==ActionKind.Inspect);
                Check(task.Execution==(Case=="success"?ExecutionPhase.Completed:ExecutionPhase.Stopped),"chosen consent or deadline governs execution");
                Check(task.Report==ReportingPhase.KnownByRecipient,"physical report reached original local issuer");
                Check(w.Couriers!.Missions.Where(c=>w.Messaging.Attempts.Single(a=>a.Id==c.Attempt).MessageId=="message:"+task.Id.Value.Replace(':','/')+"/report").All(c=>c.Destination==WorkNode),"return stays at fixed scope instead of default Kiyosu");
                Check(S.GetBranchView(ContractFixture.Officer).Position==NationalRoutes.Nodes.Single(n=>n.Id==WorkNode).Position,"personal journey reaches authoritative regional node");
                Check(workText.GlobalPosition.Y+workText.Size.Y<Size.Y&&details.GlobalPosition.Y+details.Size.Y<=Size.Y+1,"work text and actual 3D fit viewport");
            }
            System.IO.File.WriteAllText(Output+"/"+Case+"-"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks}));GD.Print("REGIONAL_WORK_PASSED "+WorkNode+" "+Case);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
