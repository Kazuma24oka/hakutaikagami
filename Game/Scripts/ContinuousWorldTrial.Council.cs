using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class ContinuousWorldTrial
{
    bool CouncilEnabled=>Name=="CouncilEntryTrial"||OS.GetCmdlineUserArgs().Contains("--war-council");
    OptionButton councilRecipient=null!;
    FieldPoint? councilStart,councilA,councilB;
    bool councilArmed;
    Node3D councilLines=null!;
    string councilGeometry="";
    int drawnCouncilLines;
    EntityId CouncilRecipient=>councilRecipient.Selected==0?S.PlayerControl:ContractFixture.Clerk;
    void BuildCouncilUI()
    {
        buttons["council-line"].Visible=false;buttons["council-return"].Visible=false;
        councilPanel.AddChild(new Label{Text="次の命令先"});councilRecipient=new();councilRecipient.AddItem("本隊（本人）");councilRecipient.AddItem("別働隊");councilRecipient.Select(1);councilPanel.AddChild(councilRecipient);
        Add(councilPanel,"council-draw","作戦線を引く",()=>{councilArmed=!councilArmed;councilStart=null;drag=null;});
        Add(councilPanel,"council-send","この線で命令",SendCouncil);
        Add(councilPanel,"council-back","選んだ部隊へ帰還指示",()=>{Require(S.IssueRegional(new(new("request:council-back/"+S.Revision),S.PlayerControl,CouncilRecipient,RegionalAction.Retreat,new(0,0),new(0,0)),S.Revision));});
        councilRecipient.ItemSelected+=_=>Refresh();councilNotice.AutowrapMode=TextServer.AutowrapMode.WordSmart;
    }
    void ClearCouncilDraft(){councilA=null;councilB=null;councilStart=null;councilArmed=false;drag=null;if(councilRecipient!=null)councilRecipient.Select(1);}
    bool ValidCouncilLine()=>councilA!=null&&councilB!=null&&RegionalGround.Distance(councilA,councilB)>=10000;
    void SendCouncil()
    {
        if(!ValidCouncilLine()){notice.Text="10m以上の作戦線を下書きしてください";return;}
        var r=CouncilInput.Line(S.DiagnosticSnapshot(),"request:council-input/"+S.Revision,CouncilRecipient,councilA!,councilB!);
        var result=S.IssueRegional(r,S.Revision);Require(result);if(result.Code==ResultCode.Accepted)ClearCouncilDraft();
    }
    bool CouncilGroundInput(InputEvent e)
    {
        if(e is not InputEventMouseButton m||m.ButtonIndex!=MouseButton.Left||!councilPanel.Visible)return false;
        if(!councilArmed||!S.DiagnosticSnapshot().Region!.Active||span>240){councilStart=null;notice.Text="野外の詳細表示で「作戦線を引く」を選んでください";return true;}
        var pixel=m.Position*new Vector2(viewport.Size.X/surface.Size.X,viewport.Size.Y/surface.Size.Y);var p=Pick(pixel);
        if(m.Pressed)councilStart=p;
        else
        {
            if(councilStart is {} a&&p is {} b){councilA=a;councilB=b;notice.Text=ValidCouncilLine()?"下書きです。「この線で命令」を押すまで部隊は動きません":"線が短すぎます。10m以上で引き直してください";}
            councilStart=null;
        }
        Refresh();return true;
    }
    string LineLabel(FieldPoint a,FieldPoint b)
    {
        var anchor=S.DiagnosticSnapshot().Atlas!.Anchor;var x=ContinuousWorld.Local(a,anchor);var y=ContinuousWorld.Local(b,anchor);
        return $"({x.X/1000.0:0},{x.Y/1000.0:0})→({y.X/1000.0:0},{y.Y/1000.0:0})m";
    }
    void DrawCouncilLine(FieldPoint a,FieldPoint b,string color)
    {
        int segments=(int)Math.Clamp(RegionalGround.Distance(a,b)/2000,1,256);var mesh=new ImmediateMesh();mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        Vector3 V(int n){var p=new FieldPoint(a.X+(b.X-a.X)*n/segments,a.Y+(b.Y-a.Y)*n/segments);return P(p)+Vector3.Up;}
        for(int n=0;n<segments;n++){mesh.SurfaceAddVertex(V(n));mesh.SurfaceAddVertex(V(n+1));}mesh.SurfaceEnd();
        councilLines.AddChild(new MeshInstance3D{Mesh=mesh,MaterialOverride=Mat(color)});
        foreach(var p in new[]{a,b})councilLines.AddChild(new MeshInstance3D{Mesh=new SphereMesh{Radius=.8f,Height=1.6f,RadialSegments=8,Rings=4},Position=P(p)+Vector3.Up,MaterialOverride=Mat(color)});
        drawnCouncilLines++;
    }
    void RefreshCouncil()
    {
        if(councilRecipient==null||world==null)return;
        var w=S.DiagnosticSnapshot();if(!councilPanel.Visible){councilArmed=false;councilStart=null;}
        var issued=w.Region!.Commands.Where(c=>c.Request.Actor==w.PlayerControl&&c.Request.Action==RegionalAction.Move).TakeLast(4).ToArray();
        buttons["council-draw"].Text=councilArmed?"線の入力を終了":"作戦線を引く";
        buttons["council-draw"].Disabled=!w.Region.Active;buttons["council-send"].Disabled=!w.Region.Active||!ValidCouncilLine();buttons["council-back"].Disabled=!w.Region.Active;
        councilNotice.Visible=councilPanel.Visible||issued.Length>0||councilA!=null;
        councilNotice.Text=councilA is {} a&&councilB is {} b?"未送信："+(CouncilRecipient==w.PlayerControl?"本隊 ":"別働隊 ")+LineLabel(a,b)+" / 青い線は下書き。送信まで実行されません。":
            issued.LastOrDefault() is {} c?"送信済："+(c.Request.Recipient==w.PlayerControl?"本隊 ":"別働隊 ")+LineLabel(c.Request.A,c.Request.B)+" / "+(RegionalBattle.Status(w,c,w.PlayerControl)=="applied"?"指示を適用":Readable(RegionalBattle.Status(w,c,w.PlayerControl)))+(c.Request.Recipient==w.PlayerControl?"（本人の指示）":"（受令判断の返報・到着報告ではありません）"):
            "担当者を選び、地面に10m以上の線を下書きして送信。線は布陣目標で、実際の進路ではありません。";
        if(councilLines==null){councilLines=new();world.AddChild(councilLines);}councilLines.Visible=w.Region.Active&&span<=240;
        string key=System.Text.Json.JsonSerializer.Serialize(new{center.X,center.Y,Issued=issued.Select(c=>c.Request),DraftA=councilA,DraftB=councilB});if(key==councilGeometry)return;councilGeometry=key;
        foreach(var child in councilLines.GetChildren())child.Free();drawnCouncilLines=0;
        foreach(var order in issued)DrawCouncilLine(order.Request.A,order.Request.B,"edc66c");
        if(councilA is {} da&&councilB is {} db)DrawCouncilLine(da,db,"78dce8");
    }
    string CouncilPose()=>System.Text.Json.JsonSerializer.Serialize(new{Scene=LoopPose(),Summary=councilNotice.Text,Recipient=councilRecipient.Selected,DraftA=councilA,DraftB=councilB,Armed=councilArmed,drawnCouncilLines,Commands=S.DiagnosticSnapshot().Region!.Commands.Select(c=>c.Request)});
    async Task CouncilCapture(string name)
    {
        await Capture(name);
        System.IO.File.WriteAllText(Output+"/"+Case+"-"+name+"-rect.json",System.Text.Json.JsonSerializer.Serialize(new[]{councilPanel.GlobalPosition.X,councilPanel.GlobalPosition.Y,Size.X-20,Size.Y-councilPanel.GlobalPosition.Y-10}));
    }
    void TestDrawCouncilLine(FieldPoint a,FieldPoint b)
    {
        Vector2 Pixel(FieldPoint p){var v=camera.UnprojectPosition(P(p));return v*new Vector2(surface.Size.X/viewport.Size.X,surface.Size.Y/viewport.Size.Y);}
        foreach(var step in new[]{(a,true),(b,false)})surface.EmitSignal(Control.SignalName.GuiInput,new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=step.Item2,Position=Pixel(step.Item1)});
    }
    async Task RunCouncilChecks()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Output);const long final=250;
            if(Mode.EndsWith("restore"))
            {
                var stages=System.Text.Json.JsonSerializer.Deserialize<long[]>(System.IO.File.ReadAllText(Output+"/"+Case+"-stages.json"))!;
                for(int i=0;i<stages.Length;i++)
                {
                    Load(Case+"-s"+i);Check(CouncilPose()==System.IO.File.ReadAllText(Output+"/"+Case+"-s"+i+"-pose.json"),"issued recipient, line, visibility, models and delivery restored "+i);
                    await CouncilCapture("restored-s"+i);Click("resume");Advance(final);Refresh();await CouncilCapture("continued-s"+i);
                }
            }
            else
            {
                Click("site");Click("council");follow=false;span=160;
                bool own=Case=="own",blocked=Case=="blocked";
                var a=ContinuousWorld.Point("castle",blocked?45000:own?65000:95000,blocked?85000:own?-85000:-65000);
                var b=ContinuousWorld.Point("castle",a.X-ContinuousWorld.Origin("castle").X+20000,a.Y-ContinuousWorld.Origin("castle").Y);
                center=new((a.X+b.X)/2,(a.Y+b.Y)/2);Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                var before=ContractJson.CanonicalHash(S.DiagnosticSnapshot());councilRecipient.Select(own?0:1);councilRecipient.EmitSignal(OptionButton.SignalName.ItemSelected,(long)councilRecipient.Selected);
                TestDrawCouncilLine(a,b);Check(before==ContractJson.CanonicalHash(S.DiagnosticSnapshot()),"unarmed council drag cannot accidentally issue a direct own-force order");
                Click("council-draw");TestDrawCouncilLine(a,a with{X=a.X+5000});Check(buttons["council-send"].Disabled&&before==ContractJson.CanonicalHash(S.DiagnosticSnapshot()),"short draft is disabled without any order or world mutation");TestDrawCouncilLine(a,b);
                Check(councilA!=null&&councilB!=null&&RegionalGround.Distance(councilA,a)<20&&RegionalGround.Distance(councilB,b)<20,"actual rendered terrain picking preserves chosen line endpoints");
                Check(before==ContractJson.CanonicalHash(S.DiagnosticSnapshot()),"recipient selection, line drawing and preview never dispatch or pause world");
                Check(!buttons["council-send"].Disabled&&drawnCouncilLines==1,"valid draft is visible and send button enabled");await CouncilCapture("draft");
                var positions=S.DiagnosticSnapshot().Region!.Units.Select(u=>u.Position).ToArray();var chosenA=councilA;var chosenB=councilB;Click("council-send");var w=S.DiagnosticSnapshot();var command=w.Region!.Commands.Single();
                Check(command.Request.Recipient==(own?S.PlayerControl:ContractFixture.Clerk)&&command.Request.A==chosenA&&command.Request.B==chosenB,"UI sends exact chosen recipient and picked line");
                Check(councilA==null&&drawnCouncilLines==1,"accepted line is reconstructed from saved command rather than draft");
                Check(S.IssueRegional(command.Request,S.Revision).Code==ResultCode.AlreadyAccepted,"same request does not duplicate order or courier");
                if(!own)Check(positions.SequenceEqual(w.Region.Units.Select(u=>u.Position))&&RegionalBattle.Status(w,command,S.PlayerControl)=="発送済・結果未確認","recipient stays still and result stays unknown before delivery");
                long due=own?1:w.Region.Deliveries.Single().DueAt;long[] stages={0,Math.Max(1,due-1),due+1};System.IO.File.WriteAllText(Output+"/"+Case+"-stages.json",System.Text.Json.JsonSerializer.Serialize(stages));
                for(int i=0;i<stages.Length;i++)
                {
                    Advance(stages[i]);Refresh();Save(Case+"-s"+i);System.IO.File.WriteAllText(Output+"/"+Case+"-s"+i+"-pose.json",CouncilPose());await CouncilCapture("s"+i);
                    if(!own&&i==2)Check(RegionalBattle.Status(S.DiagnosticSnapshot(),S.DiagnosticSnapshot().Region!.Commands.Single(),S.PlayerControl)=="発送済・結果未確認","recipient decision cannot leak before physical return report");
                }
                Advance(final);Refresh();await CouncilCapture("final");w=S.DiagnosticSnapshot();ContractValidation.Validate(w);
                string expected=Case=="refuse"?"refused":Case=="blocked"?"blocked-hold":"applied";
                Check(RegionalBattle.Status(w,w.Region!.Commands.Single(),S.PlayerControl)==expected,"physical report or own recognition has correct visible outcome");
                if(Case is "clerk" or "own")Check(w.Region.Units.Where(u=>u.Commander==command.Request.Recipient).All(u=>u.Position==u.Goal),"chosen formation reached without changing unrelated commander");
                else Check(positions.SequenceEqual(w.Region.Units.Select(u=>u.Position)),"refusal and unwalkable goal do not move troops");
                Check(!w.Region.Hostile,"drawing and formation never authorize war");
                Check(details.GlobalPosition.Y+details.Size.Y<=Size.Y+1,"council and bottom status fit actual viewport");
            }
            System.IO.File.WriteAllText(Output+"/"+Case+"-"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks}));GD.Print("COUNCIL_INPUT_PASSED "+Case);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
