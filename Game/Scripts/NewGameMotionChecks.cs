using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameMotionChecks : Control
{
    async Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    public override async void _Ready()
    {
        try
        {
            string label=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--motion-label="))?.Split('=')[1]??"baseline";
            string root=ProjectSettings.GlobalizePath("res://../Docs/Design/MotionResponseV1/runtime-local/");Directory.CreateDirectory(root);
            bool film=OS.GetCmdlineUserArgs().Contains("--motion-film");bool capped=OS.GetCmdlineUserArgs().Contains("--motion-capped");Engine.MaxFps=capped?60:0;DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Theme=new Theme{DefaultFontSize=16,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
            var profile=ContractJson.Read<NewCampaignProfile>(System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath("res://../Docs/Design/MotionResponseV1/baseline-start.json")));
            var store=new NewCampaignStore(Path.Combine(root,Guid.NewGuid().ToString("N")));var run=store.New(profile.Setup,profile.Registered,profile.Selected,profile.Seed);
            var game=new NewGameRuntime();game.Configure(store,run,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            await Frame();await Frame();game.SetDestination("fork");game.Press("depart");game.Press("auto");
            if(film)
            {
                string dir=Path.Combine(root,"film");Directory.CreateDirectory(dir);
                for(int n=0;n<480;n++){if(n==240)game.Press("pause");if(n==300)game.Press("rotate");if(n==360)game.Press("save");if(n==420)game.Press("load");await Frame();GetViewport().GetTexture().GetImage().SavePng(Path.Combine(dir,"frame-"+n.ToString("D4")+".png"));}
                GD.Print("MOTION_FILM_PASSED 480");GetTree().Quit();return;
            }
            var clock=Stopwatch.StartNew();while(clock.Elapsed.TotalSeconds<2)await Frame();
            long allocated=GC.GetTotalAllocatedBytes(false);int gc0=GC.CollectionCount(0);var intervals=new List<double>();var cpu=new List<double>();double last=clock.Elapsed.TotalMilliseconds;double until=last+10000;
            while(clock.Elapsed.TotalMilliseconds<until){await Frame();double now=clock.Elapsed.TotalMilliseconds;intervals.Add(now-last);last=now;cpu.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess)*1000);}
            game.Press("pause");long allocation=GC.GetTotalAllocatedBytes(false)-allocated;var order=intervals.Order().ToArray();double P(double p)=>order[(int)Math.Min(order.Length-1,Math.Ceiling(p*order.Length)-1)];
            var responses=new List<double>();for(int i=0;i<12;i++){var timer=Stopwatch.StartNew();game.Press("rotate");await Frame();responses.Add(timer.Elapsed.TotalMilliseconds);}
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,label+".png"));
            var actor=Officer3DCustomActor.Descendants(game).OfType<Officer3DCustomActor>().First();var branch=game.Session.GetBranchView(game.Session.PlayerControl);double metres=new Vector2(branch.NextPosition.EastMm-branch.Position.EastMm,branch.NextPosition.NorthMm-branch.Position.NorthMm).Length()/1000;
            var result=new{Label=label,Capped=capped,FixedCapture=false,Resolution=new[]{(int)GetViewportRect().Size.X,(int)GetViewportRect().Size.Y},Frames=intervals.Count,MeanFrameMs=intervals.Average(),MedianMs=P(.5),P95Ms=P(.95),P99Ms=P(.99),WorstMs=order.Last(),Over33=intervals.Count(x=>x>33.334),Over50=intervals.Count(x=>x>50),MeanProcessMs=cpu.Average(),AllocatedBytes=allocation,Gen0Collections=GC.CollectionCount(0)-gc0,ButtonToDrawMs=responses,WalkStride=actor.WalkStride,ActorScale=actor.Scale.Z,ClipSeconds=actor.Player.GetAnimation(actor.Clips["walk"]).Length,MetresPerWorldSecond=metres,IntervalsMs=intervals,ProcessMs=cpu,Seconds=game.Session.Seconds,Renderer=RenderingServer.GetRenderingDevice()==null?"Compatibility/OpenGL":"RenderingDevice",Device=RenderingServer.GetVideoAdapterName()};
            var boundaries=new List<object>();game.Press("pause");
            for(int n=0;n<5;n++)
            {
                game.Advance(game.Session.Seconds+(n==0?1:85));game.Refresh();await Frame();var w=game.Session.DiagnosticSnapshot();var v=game.Session.GetBranchView(w.PlayerControl);var center=FieldWorld.FromWorld(v.Position);var terrain=Officer3DCustomActor.Descendants(game).OfType<ContinuousTerrainRenderer>().Single();var tile=ContinuousWorld.TileAt(center);var origin=ContinuousWorld.Point(tile.Node,tile.X*ContinuousWorld.Tile,tile.Y*ContinuousWorld.Tile);
                var expect=new Vector3((origin.X-center.X)/1000f,0,-(origin.Y-center.Y)/1000f);if(terrain.Position.DistanceTo(expect)>.0001f||!terrain.TileIds.SequenceEqual(ContinuousWorld.VisibleTiles(center,game.Span).Select(t=>t.Id)))throw new Exception("retained terrain coverage/origin differs from Core projection");
                if(v.Walking){double step=NewGameRuntime.VisualDistance(w,w.PlayerControl,1)-NewGameRuntime.VisualDistance(w,w.PlayerControl,0);double dx=v.NextPosition.EastMm-v.Position.EastMm,dy=v.NextPosition.NorthMm-v.Position.NorthMm;if(Math.Abs(step-Math.Sqrt(dx*dx+dy*dy))>3)throw new Exception("animation distance differs from actual interpolated movement");}
                game.SaveCheckpoint("motion-boundary");string hash=ContractJson.CanonicalHash(w),pose=game.Pose();game.Advance(game.Session.Seconds+4);game.LoadCheckpoint("motion-boundary");await Frame();if(hash!=ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())||pose!=game.Pose())throw new Exception("cached view changed across restore");
                boundaries.Add(new{Seconds=w.ElapsedSeconds,Tile=tile.Id,World=hash,Restored=true});GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,label+"-boundary"+n+".png"));
            }
            System.IO.File.WriteAllText(Path.Combine(root,label+"-boundaries.json"),System.Text.Json.JsonSerializer.Serialize(boundaries));
            System.IO.File.WriteAllText(Path.Combine(root,label+".json"),System.Text.Json.JsonSerializer.Serialize(result));GD.Print("MOTION_BENCH_PASSED "+label+" "+intervals.Count+"frames");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
