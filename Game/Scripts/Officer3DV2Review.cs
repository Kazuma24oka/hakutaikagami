using HakutaiKagami.Core.Simulation;
using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json;

public partial class Officer3DV2Review : Node
{
    Officer3DStage stage=null!;TaxPanel session=null!;
    readonly string output=ProjectSettings.GlobalizePath("res://../Art/Officer3D/TrialV2/runtime-local");
    readonly Dictionary<string,object> results=new();
    public override void _Ready()
    {
        try {Officer3DV2Gate.Require(!OS.GetCmdlineUserArgs().Contains("--v2-test")&&!OS.GetCmdlineUserArgs().Contains("--v2-restore"));}catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(2);return;}
        DisplayServer.WindowSetSize(new Vector2I(1440,1000));
        var theme=new Theme{DefaultFontSize=18,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo"}}};
        var root=new Control{Theme=theme};AddChild(root);root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        session=new TaxPanel{Visible=false};root.AddChild(session);session.Initialize(FileAccess.GetFileAsString("res://Data/prototype.json"), OS.GetCmdlineUserArgs().Any(a=>a=="--v2-test"||a=="--v2-restore") ? ProjectSettings.GlobalizePath("user://officer3d-v2-test-world.json") : null);
        stage=new Officer3DStage();root.AddChild(stage);stage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);stage.Initialize(session);
        if(OS.GetCmdlineUserArgs().Contains("--v2-test"))CallDeferred(nameof(Test));
        else if(OS.GetCmdlineUserArgs().Contains("--v2-restore"))CallDeferred(nameof(RestoreTest));
    }
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);GD.Print("V2 CHECK "+message);}
    async Task Frames(int n=4){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    async Task<Image> Capture(string name,bool whole=false){await Frames();var i=(whole?GetViewport():stage.View).GetTexture().GetImage();i.SavePng(output+"/"+name+".png");return i;}
    static (int Inside,int Outside) Difference(Image before,Image after,Image mask)
    {int inside=0,outside=0;for(int y=0;y<before.GetHeight();y++)for(int x=0;x<before.GetWidth();x++){var a=before.GetPixel(x,y);var b=after.GetPixel(x,y);if(a.R==b.R&&a.G==b.G&&a.B==b.B)continue;var m=mask.GetPixel(x,y);if(m.R>.5||m.G>.5||m.B>.5)inside++;else outside++;}return(inside,outside);}
    public async void RestoreTest()
    {
        try{stage.SetProcess(false);session.Simulation.SetPaused(true);stage.RestoreLooks(FileAccess.GetFileAsString("user://officer3d-v2-test-looks.json"));Check(stage.SaveLooks()==FileAccess.GetFileAsString("user://officer3d-v2-test-looks.json"),"looks restored in separate process");session.InvokeFrom3D("load");for(int i=0;i<4;i++)session.InvokeFrom3D("advance");Check(session.Simulation.SaveJson()==FileAccess.GetFileAsString("user://officer3d-v2-test-world-expected.json"),"world restored in separate process and continues identically");stage.Synchronize();await Capture("restart",true);GD.Print("V2_RESTORE_COMPLETE");GetTree().Quit();}catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    public async void Test()
    {
        try
        {
            System.IO.Directory.CreateDirectory(output);stage.SetProcess(false);stage.AutomaticLod=false;((SubViewportContainer)stage.View.GetParent()).Stretch=false;stage.View.Size=new Vector2I(960,600);
            var actor=stage.Actors["character-002"];
            foreach(var a in stage.Actors.Values)a.Visible=false;actor.Visible=true;actor.Position=Vector3.Zero;actor.Rotation=Vector3.Zero;
            stage.Camera.Size=5;stage.Camera.Position=new Vector3(0,3.2f,6);stage.Camera.LookAt(new Vector3(0,1,0));
            Officer3DStage.Preset(actor,"commander");actor.Freeze("idle",.4);
            // Silhouette comparison against the original at identical camera, pose and projected size.
            var source=new Officer3DModel();stage.World.AddChild(source);source.Position=Vector3.Zero;source.Freeze("idle",.4);
            var clipTransfer=new Dictionary<string,object>();foreach(var clip in new[]{"idle","walk","attack"}){double originalLength=source.Player.GetAnimation(source.Clips[clip]).Length,convertedLength=actor.Player.GetAnimation(actor.Clips[clip]).Length;Check(Math.Abs(originalLength-convertedLength)<.002,"clip duration preserved "+clip);float maxAngle=0,maxTranslation=0;for(int sample=0;sample<20;sample++){double t=originalLength*sample/20;source.Freeze(clip,t);actor.Freeze(clip,t);for(int b=0;b<source.Skeleton.GetBoneCount();b++){int other=actor.Skeleton.FindBone(source.Skeleton.GetBoneName(b));if(other<0)throw new InvalidOperationException("Missing shared bone");maxAngle=Math.Max(maxAngle,source.Skeleton.GetBonePoseRotation(b).AngleTo(actor.Skeleton.GetBonePoseRotation(other)));maxTranslation=Math.Max(maxTranslation,source.Skeleton.GetBonePosePosition(b).DistanceTo(actor.Skeleton.GetBonePosePosition(other)));}}Check(maxAngle<.02f&&maxTranslation<.002f,"clip pose preserved "+clip+" angle="+maxAngle+" translation="+maxTranslation);clipTransfer[clip]=new{maxAngle,maxTranslation,originalLength,convertedLength};}results["clip_transfer"]=clipTransfer;source.Freeze("idle",.4);actor.Freeze("idle",.4);
            var environment=stage.World.GetChildren().OfType<WorldEnvironment>().Single().Environment;var background=environment.BackgroundColor;environment.BackgroundColor=Colors.Black;
            var scenery=stage.World.GetChildren().OfType<MeshInstance3D>().ToArray();foreach(var m in scenery)m.Visible=false;
            var flat=new Shader{Code="shader_type spatial; render_mode unshaded,cull_disabled; uniform sampler2D tex; void fragment(){if(texture(tex,UV).a<0.5)discard; ALBEDO=vec3(1.0);}"};
            var originalMaterials=new List<(MeshInstance3D Mesh,int Surface,Material Material)>();
            foreach(var m in source.Meshes.Values.Concat(actor.Meshes.Values))for(int i=0;i<m.Mesh.GetSurfaceCount();i++){var material=(ShaderMaterial)m.GetSurfaceOverrideMaterial(i);originalMaterials.Add((m,i,material));var silhouette=new ShaderMaterial{Shader=flat};silhouette.SetShaderParameter("tex",material.GetShaderParameter("base_texture"));m.SetSurfaceOverrideMaterial(i,silhouette);}
            var silhouetteScores=new Dictionary<string,double>();
            foreach(int angle in new[]{0,90,180,270})
            {
                actor.Visible=false;source.Visible=true;source.RotationDegrees=new(0,angle,0);var baseline=await Capture("silhouette-source-"+angle);
                source.Visible=false;actor.Visible=true;actor.RotationDegrees=new(0,angle,0);actor.Freeze("idle",.4);
                foreach(var lod in new[]{"near","mid","far"}){actor.SetLod(lod);var candidate=await Capture("silhouette-"+lod+"-"+angle);int intersection=0,union=0;for(int y=0;y<baseline.GetHeight();y++)for(int x=0;x<baseline.GetWidth();x++){var a=baseline.GetPixel(x,y);var b=candidate.GetPixel(x,y);bool aa=a.R>.95f&&a.G>.95f&&a.B>.95f,bb=b.R>.95f&&b.G>.95f&&b.B>.95f;if(aa&&bb)intersection++;if(aa||bb)union++;}double score=(double)intersection/union;silhouetteScores[lod+"-"+angle]=score;Check(score>(lod=="near"?.94:lod=="mid"?.85:.65),"silhouette "+lod+" "+angle+" IoU="+score);}
            }
            foreach(var item in originalMaterials)item.Mesh.SetSurfaceOverrideMaterial(item.Surface,item.Material);source.Free();foreach(var m in scenery)m.Visible=true;environment.BackgroundColor=background;actor.Rotation=Vector3.Zero;actor.SetLod("near");results["silhouette_iou_at_200px"]=silhouetteScores;
            foreach(var level in new[]{"near","mid","far"})
            {
                actor.SetLod(level);
                foreach(var clip in new[]{"idle","walk","attack"}){actor.Freeze(clip,.4);await Capture("lod-"+level+"-"+clip);}
                foreach(var slot in Officer3DActor.Slots){actor.Appearance.Enabled[slot]=false;actor.Apply(actor.Appearance);actor.Freeze("walk",.4);await Capture("off-"+level+"-"+slot);actor.Appearance.Enabled[slot]=true;actor.Apply(actor.Appearance);}
            }
            actor.SetLod("near");actor.Freeze("idle",.4);
            foreach(var (mat,_) in actor.Materials)mat.SetShaderParameter("verification_unlit",true);
            var isolation=new Dictionary<string,object>();
            for(int angle=0;angle<360;angle+=90)
            {
                actor.RotationDegrees=new(0,angle,0);
                foreach(var category in new[]{"cloth","lacquer","metal"})
                {
                    var before=await Capture($"color-{angle}-{category}-before");int channel=Array.IndexOf(new[]{"cloth","lacquer","metal"},category)+1;
                    foreach(var (mat,_) in actor.Materials)mat.SetShaderParameter("category_mask",channel);
                    var mask=await Capture($"color-{angle}-{category}-mask");foreach(var (mat,_) in actor.Materials)mat.SetShaderParameter("category_mask",0);
                    actor.Appearance.MaterialSrgb[category]=new float[]{.2f,.7f,.35f};actor.Apply(actor.Appearance);var after=await Capture($"color-{angle}-{category}-after");var diff=Difference(before,after,mask);
                    Check(diff.Inside>10&&diff.Outside==0,$"{category} {angle}: changed {diff.Inside}, outside {diff.Outside}");isolation[$"{category}-{angle}"]=new{diff.Inside,diff.Outside};
                    actor.Appearance.MaterialSrgb[category]=new float[]{1,1,1};actor.Apply(actor.Appearance);
                }
                foreach(var slot in Officer3DActor.Slots)actor.Appearance.Enabled[slot]=false;actor.Apply(actor.Appearance);
                var skinBefore=await Capture("skin-"+angle+"-before");actor.DebugMask(1);var skinMask=await Capture("skin-"+angle+"-mask");actor.DebugMask(0);actor.Appearance.SkinLinear=new float[]{.3f,.16f,.08f};actor.Apply(actor.Appearance);var skinAfter=await Capture("skin-"+angle+"-after");var sd=Difference(skinBefore,skinAfter,skinMask);Check(sd.Inside>10&&sd.Outside==0,"skin protection "+angle);isolation["skin-"+angle]=new{sd.Inside,sd.Outside};actor.Appearance.SkinLinear=new float[]{.87f,.73f,.59f};foreach(var slot in Officer3DActor.Slots)actor.Appearance.Enabled[slot]=true;actor.Apply(actor.Appearance);
            }
            results["color_isolation"]=isolation;foreach(var (mat,_) in actor.Materials)mat.SetShaderParameter("verification_unlit",false);
            Officer3DStage.Preset(actor,"veteran");actor.Rotation=Vector3.Zero;
            var variants=new Dictionary<string,object>();
            foreach(var level in new[]{"near","mid","far"}){actor.SetLod(level);foreach(var angle in new[]{0,90,180,270}){actor.RotationDegrees=new(0,angle,0);foreach(var clip in new[]{"idle","walk","attack"}){actor.Freeze(clip,.4);await Capture($"veteran-{level}-{angle}-{clip}");}}}
            Check(actor.Meshes["hair_tied"].Visible&&actor.Meshes["beard_short"].Visible&&actor.Meshes["face_veteran"].Visible&&!actor.Meshes["face"].Visible,"veteran parts replace default face and attach to shared rig");
            foreach(var clip in new[]{"idle","walk","attack"})
            {
                actor.Freeze(clip,0);var rotations=Enumerable.Range(0,actor.Skeleton.GetBoneCount()).Select(actor.Skeleton.GetBonePoseRotation).ToArray();float max=0,drift=0;
                for(int sample=0;sample<20;sample++){actor.Freeze(clip,actor.Player.GetAnimation(actor.Clips[clip]).Length*sample/20);for(int i=0;i<rotations.Length;i++)max=Math.Max(max,rotations[i].AngleTo(actor.Skeleton.GetBonePoseRotation(i)));var p=actor.Skeleton.GetBonePosePosition(actor.Skeleton.FindBone("Root"));drift=Math.Max(drift,new Vector2(p.X,p.Z).Length());}
                Check(max>.01f&&drift<.0001f,"shared clip and in-place root "+clip);variants[clip]=new{max_rotation=max,planar_drift=drift};
            }
            foreach(var clip in new[]{"idle","walk","attack"}){actor.Play(clip);await Frames(12);Check(actor.Player.CurrentAnimationPosition>0,"actual playback "+clip);}actor.Play("attack");await ToSignal(GetTree().CreateTimer(1.6),SceneTreeTimer.SignalName.Timeout);Check(actor.Player.CurrentAnimation.ToString().EndsWith("idle"),"attack returns to idle");
            results["variant_motion"]=variants;results["walk_stride_m"] = actor.WalkStride;var looks=stage.SaveLooks();stage.RestoreLooks(looks);Check(looks==stage.SaveLooks(),"all part IDs/colors roundtrip");
            using(var f=FileAccess.Open("user://officer3d-v2-test-looks.json",FileAccess.ModeFlags.Write))f.StoreString(looks);
            // All LODs keep the same skeleton object, animation cursor, and appearance.
            var boneId=actor.Skeleton.GetInstanceId();actor.Freeze("walk",.35);string saved=actor.Save();
            foreach(var l in new[]{"near","mid","far","near"}){actor.SetLod(l);Check(actor.Skeleton.GetInstanceId()==boneId&&actor.Save()==saved&&Math.Abs(actor.Player.CurrentAnimationPosition-.35)<.0001,"LOD keeps rig/appearance/clip "+l);}
            actor.UpdateLod(150);Check(actor.Lod=="mid","near exit");actor.UpdateLod(170);Check(actor.Lod=="mid","LOD hysteresis");actor.UpdateLod(190);Check(actor.Lod=="near","near enter");actor.UpdateLod(50);Check(actor.Lod=="far","far enter");actor.UpdateLod(70);Check(actor.Lod=="far","far hysteresis");actor.UpdateLod(90);Check(actor.Lod=="mid","far exit");
            // Unknown IDs/schema and invalid colors fail before changing live state.
            string validLook=actor.Save();foreach(var bad in new[]{validLook.Replace("angular_jaw","unknown_face"),validLook.Replace("\"Schema\": 2","\"Schema\": 9")}){bool rejected=false;try{actor.Restore(bad);}catch{rejected=true;}Check(rejected&&actor.Save()==validLook,"invalid appearance rejected atomically");}
            // Existing transaction handlers drive both views. No rendering operation may alter Core JSON.
            session.InvokeFrom3D("reset");session.InvokeFrom3D("submit");var sim=session.Simulation;string stable=sim.SaveJson();
            stage.Synchronize();await Frames(30);Check(stable==sim.SaveJson(),"rendering does not advance Core time or RNG");
            session.InvokeFrom3D("pause");long paused=sim.Tick;stage.Automatic=true;stage.SetProcess(true);await Frames(30);stage.SetProcess(false);Check(sim.Tick==paused&&stage.Fraction==0,"pause freezes display and session");
            session.InvokeFrom3D("pause");session.InvokeFrom3D("advance");Check(sim.Tick==1&&sim.Orders[0].Phase==TaxPhase.Received,"existing command delivery drives game scene");
            string checkpoint=sim.SaveJson();session.InvokeFrom3D("save");var restored=TaxSimulation.LoadJson(checkpoint);for(int i=0;i<4;i++){sim.AdvanceOneTick();restored.AdvanceOneTick();}Check(sim.SaveJson()==restored.SaveJson()&&sim.TreasuryRice==160,"save restore produces same result / collection once");
            string expected=sim.SaveJson();using(var f=FileAccess.Open("user://officer3d-v2-test-world-expected.json",FileAccess.ModeFlags.Write))f.StoreString(expected);
            session.InvokeFrom3D("load");Check(session.Simulation.Tick==1,"3D load handler restores saved shared world");for(int i=0;i<4;i++)session.InvokeFrom3D("advance");Check(session.Simulation.SaveJson()==expected,"3D save/load UI preserves deterministic continuation");
            stage.Synchronize();foreach(var a in stage.Actors.Values)a.Visible=true;stage.Camera.Size=10;stage.Camera.Position=new(0,8,10);stage.Camera.LookAt(Vector3.Zero);await Capture("game-world",true);
            session.InvokeFrom3D("reset");session.InvokeFrom3D("submit");stage.Automatic=true;
            for(int f=0;f<24;f++){stage._Process(1.5/24);await Capture("delivery-frame-"+f.ToString("D2"),true);}stage.Automatic=false;
            results["integration"]="shared TaxPanel simulation, render immutability, pause, arrival, collection once, deterministic restore passed";
            // Comparable scene and render settings across source / authored detail levels.
            var performance=new Dictionary<string,object>();foreach(var a in stage.Actors.Values)a.Visible=false;
            stage.View.Size=new Vector2I(1280,800);stage.Camera.Size=9;float pitch=Mathf.DegToRad(38);stage.Camera.Position=new(0,1+10*Mathf.Sin(pitch),10*Mathf.Cos(pitch));stage.Camera.LookAt(new Vector3(0,1,0));stage.Camera.HOffset=1.8f;
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);Engine.MaxFps=0;
            foreach(var kind in new[]{"source","near","mid","far"})foreach(var count in new[]{1,10,50})
            {
                var models=new List<Node3D>();for(int i=0;i<count;i++){Node3D a=kind=="source"?new Officer3DModel():new Officer3DActor();stage.World.AddChild(a);a.Position=new((i%10-4.5f)*.7f,0,(i/10-2)*.7f);if(a is Officer3DActor v){v.SetLod(kind);v.Play("walk");}else ((Officer3DModel)a).Play("walk");models.Add(a);}
                await Frames(15);var times=new List<double>();for(int i=0;i<60;i++){var t=Stopwatch.StartNew();await Frames(1);times.Add(t.Elapsed.TotalMilliseconds);}
                var mean=times.Average();performance[$"{kind}-{count}"]=new{mean_frame_ms=mean,fps=1000/mean,draw_calls=stage.View.GetRenderInfo(Viewport.RenderInfoType.Visible,Viewport.RenderInfo.DrawCallsInFrame),primitives=stage.View.GetRenderInfo(Viewport.RenderInfoType.Visible,Viewport.RenderInfo.PrimitivesInFrame)};await Capture($"perf-{kind}-{count}");foreach(var a in models)a.Free();await Frames();
            }
            results["performance"]=performance;results["hardware"]=RenderingServer.GetVideoAdapterName();results["viewport"]="1280x800 GL Compatibility, shadows, source lighting, 15 warmup + 60 samples, 50 unique positions";
            stage.Camera.HOffset=0;stage.Camera.Size=8;stage.Camera.Position=new(0,4,7);stage.Camera.LookAt(new Vector3(0,1,0));foreach(var a in stage.Actors.Values){a.Visible=true;a.SetLod("near");}stage.Synchronize();await Capture("review",true);
            System.IO.File.WriteAllText(output+"/results.json",JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));GD.Print("V2_TEST_COMPLETE");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
