using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

public static class Officer3DV3Validation
{
    static Officer3DCreator ui=null!;
    static readonly List<string> captures=new();
    static readonly Dictionary<string,object> result=new();
    static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);GD.Print("V3 CHECK "+message);}
    static async Task Frames(int n=3){for(int i=0;i<n;i++)await ui.ToSignal(ui.GetTree(),SceneTree.SignalName.ProcessFrame);await ui.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    static async Task<Image> Capture(string name,bool screen=false){await Frames();var image=(screen?ui.GetViewport():ui.View).GetTexture().GetImage();Check(!captures.Contains(name),"capture "+name);captures.Add(name);Check(image.SavePng(ui.Output+"/"+name+".png")==Error.Ok,"save "+name);return image;}
    static void Pose(Officer3DCustomActor a,string clip,double fraction){a.Freeze(clip,a.Player.GetAnimation(a.Clips[clip]).Length*fraction);}
    static (int Inside,int Outside) Difference(Image a,Image b,Image mask){int inside=0,outside=0;for(int y=0;y<a.GetHeight();y++)for(int x=0;x<a.GetWidth();x++){var aa=a.GetPixel(x,y);var bb=b.GetPixel(x,y);if(aa.R==bb.R&&aa.G==bb.G&&aa.B==bb.B)continue;var m=mask.GetPixel(x,y);if(Math.Max(m.R,Math.Max(m.G,m.B))>.8)inside++;else outside++;}return(inside,outside);}
    public static async Task Restore(Officer3DCreator creator)
    {
        ui=creator;
        try{ui.AutoLod=false;ui.SetProcess(false);ui.LoadButton.EmitSignal(Button.SignalName.Pressed);string expected=FileAccess.GetFileAsString("user://officer3d-v3-test-expected.json");Check(ui.SaveRoster()==expected,"separate process roster / part IDs / colors restored");await Capture("restart",true);GD.Print("V3_RESTORE_COMPLETE");ui.GetTree().Quit();}catch(Exception e){GD.PushError(e.ToString());ui.GetTree().Quit(1);}
    }
    public static async Task Run(Officer3DCreator creator)
    {
        ui=creator;
        try
        {
            System.IO.Directory.CreateDirectory(ui.Output);ui.AutoLod=false;ui.SetProcess(false);((SubViewportContainer)ui.View.GetParent()).Stretch=false;ui.View.Size=new(960,720);ui.Paused=true;ui.Actor.Player.SpeedScale=0;var a=ui.Actor;
            // Exact source clips and named shared rig survive geometry changes.
            var reference=new Officer3DModel{Visible=false};ui.World.AddChild(reference);var transfer=new Dictionary<string,object>();
            foreach(var clip in new[]{"idle","walk","attack"}){double length=reference.Player.GetAnimation(reference.Clips[clip]).Length;Check(length==a.Player.GetAnimation(a.Clips[clip]).Length,"original clip duration "+clip);float rotation=0,translation=0;for(int i=0;i<20;i++){double t=length*i/20;reference.Freeze(clip,t);a.Freeze(clip,t);for(int b=0;b<73;b++){int n=a.Skeleton.FindBone(reference.Skeleton.GetBoneName(b));if(n<0)throw new InvalidOperationException("bone missing");rotation=Math.Max(rotation,reference.Skeleton.GetBonePoseRotation(b).AngleTo(a.Skeleton.GetBonePoseRotation(n)));translation=Math.Max(translation,reference.Skeleton.GetBonePosePosition(b).DistanceTo(a.Skeleton.GetBonePosePosition(n)));}}Check(rotation<.02&&translation<.002,"original clip pose "+clip);transfer[clip]=new{rotation,translation,length};}
            reference.Free();result["source_clip_transfer"]=transfer;
            // Three LODs, all views and all motions for each registered visual preset.
            foreach(var preset in new[]{"commander","veteran","red_guard"})foreach(var level in new[]{"near","mid","far"})
            {
                var look=Officer3DCustomActor.Preset(preset);if(preset=="red_guard")look.Enabled["helmet"]=true;a.Apply(look);a.SetLod(level);
                foreach(int angle in new[]{0,90,180,270}){a.RotationDegrees=new(0,angle,0);foreach(var clip in new[]{"idle","walk","attack"}){Pose(a,clip,.35);await Capture($"preset-{preset}-{level}-{angle}-{clip}");}}
            }
            a.SetLod("near");a.Rotation=Vector3.Zero;
            // Skin and material colors are global semantics, independent of direction and equipment family.
            var colors=new Dictionary<string,object>();
            foreach(var family in new[]{"silver","red_lamellar"})foreach(int angle in new[]{0,90,180,270})
            {
                var look=Officer3DCustomActor.Preset("commander");foreach(var slot in Officer3DCustomActor.RedSlots)look.Equipment[slot]=family;a.Apply(look);Pose(a,"idle",.35);a.RotationDegrees=new(0,angle,0);foreach(var (mat,_) in a.Materials)mat.SetShaderParameter("verification_unlit",true);
                foreach(var category in new[]{"cloth","lacquer","metal"})
                {
                    string key=$"{family}-{angle}-{category}";var before=await Capture("color-"+key+"-before");foreach(var (mat,_) in a.Materials)mat.SetShaderParameter("category_mask",Array.IndexOf(new[]{"cloth","lacquer","metal"},category)+1);var mask=await Capture("color-"+key+"-mask");foreach(var (mat,_) in a.Materials)mat.SetShaderParameter("category_mask",0);look.MaterialSrgb[category]=new float[]{.2f,.7f,.35f};a.Apply(look);var after=await Capture("color-"+key+"-after");var d=Difference(before,after,mask);Check(d.Inside>0&&d.Outside==0,"material isolation "+key+" changed="+d.Inside+" outside="+d.Outside);colors[key]=new{d.Inside,d.Outside};look.MaterialSrgb[category]=new float[]{1,1,1};a.Apply(look);
                }
                foreach(var (mat,_) in a.Materials)mat.SetShaderParameter("verification_unlit",false);
            }
            result["material_color_isolation"]=colors;
            var skin=new Dictionary<string,object>();a.Apply(Officer3DCustomActor.Preset("veteran"));foreach(var slot in Officer3DCustomActor.Slots)a.Appearance.Enabled[slot]=false;a.Apply(a.Appearance);
            foreach(var (mat,_) in a.Materials)mat.SetShaderParameter("verification_unlit",true);
            foreach(int angle in new[]{0,90,180,270}){a.RotationDegrees=new(0,angle,0);Pose(a,"idle",.35);var before=await Capture("skin-"+angle+"-before");a.DebugMask(1);var mask=await Capture("skin-"+angle+"-mask");a.DebugMask(0);a.Appearance.SkinLinear=new float[]{.3f,.16f,.08f};a.Apply(a.Appearance);var after=await Capture("skin-"+angle+"-after");var d=Difference(before,after,mask);Check(d.Inside>0&&d.Outside==0,"shared skin protection "+angle);skin[angle.ToString()]=new{d.Inside,d.Outside};a.Appearance.SkinLinear=new float[]{.87f,.73f,.59f};a.Apply(a.Appearance);}
            result["skin_isolation"]=skin;foreach(var (mat,_) in a.Materials)mat.SetShaderParameter("verification_unlit",false);
            // All-off images retain the base. Each slot is individually swapped and removed during motion.
            foreach(var level in new[]{"near","mid","far"}){a.SetLod(level);a.Apply(Officer3DCustomActor.Preset("commander"));foreach(var slot in Officer3DCustomActor.Slots)a.Appearance.Enabled[slot]=false;a.Apply(a.Appearance);foreach(int angle in new[]{0,90,180,270}){a.RotationDegrees=new(0,angle,0);Pose(a,"walk",.35);await Capture("base-"+level+"-"+angle);}}
            a.SetLod("near");a.Rotation=Vector3.Zero;
            foreach(var slot in Officer3DCustomActor.Slots)
            {
                var look=Officer3DCustomActor.Preset("commander");look.Enabled[slot]=false;a.Apply(look);Pose(a,"walk",.35);await Capture("off-"+slot);
                if(Officer3DCustomActor.RedSlots.Contains(slot)){look.Enabled[slot]=true;look.Equipment[slot]="red_lamellar";a.Apply(look);foreach(var clip in new[]{"idle","walk","attack"}){Pose(a,clip,.35);await Capture("swap-"+slot+"-"+clip);}}
            }
            // Deformation sweeps: different bend samples for neck / shoulder / wrist / waist / legs.
            a.Apply(Officer3DCustomActor.Preset("red_guard"));a.Appearance.Enabled["helmet"]=true;a.Apply(a.Appearance);
            foreach(var clip in new[]{"walk","attack"})for(int i=0;i<12;i++){Pose(a,clip,i/12.0);await Capture("joint-"+clip+"-"+i.ToString("D2"));}
            // LOD changes retain identity, bone objects, phase and appearance IDs.
            a.Freeze("walk",.35);string identity=a.Save();ulong rig=a.Skeleton.GetInstanceId();foreach(var level in new[]{"near","mid","far","near"}){a.SetLod(level);Check(a.Save()==identity&&a.Skeleton.GetInstanceId()==rig&&Math.Abs(a.Player.CurrentAnimationPosition-.35)<.0001,"LOD preserves live rig and appearance "+level);}
            a.UpdateLod(150);Check(a.Lod=="mid","LOD near exit");a.UpdateLod(170);Check(a.Lod=="mid","LOD hysteresis");a.UpdateLod(190);Check(a.Lod=="near","LOD near entry");a.UpdateLod(50);Check(a.Lod=="far","LOD far entry");a.UpdateLod(90);Check(a.Lod=="mid","LOD far exit");
            a.SetLod("near");a.Play("attack");a.Player.SpeedScale=1;await ui.ToSignal(ui.GetTree().CreateTimer(1.6),SceneTreeTimer.SignalName.Timeout);Check(a.Player.CurrentAnimation.ToString().EndsWith("idle"),"attack returns to idle");a.Player.SpeedScale=0;
            // Real UI signal handlers, without native mouse/keyboard input.
            ui.NewButton.EmitSignal(Button.SignalName.Pressed);ui.NameEdit.Text="交換検証の武将";ui.NameEdit.EmitSignal(LineEdit.SignalName.TextChanged,"交換検証の武将");ui.FaceChoice.Select(1);ui.FaceChoice.EmitSignal(OptionButton.SignalName.ItemSelected,1L);
            foreach(var slot in Officer3DCustomActor.RedSlots){ui.EquipmentChoices[slot].Select(2);ui.EquipmentChoices[slot].EmitSignal(OptionButton.SignalName.ItemSelected,2L);Check(ui.Actor.Appearance.Equipment[slot]=="red_lamellar"&&ui.Actor.Appearance.Enabled[slot],"UI equipment choice "+slot);}
            Check(!ui.Actor.Meshes["hair_tied"].Visible&&!ui.Actor.Meshes["beard_short"].Visible,"helmet and mask hide covered hair/beard");ui.EquipmentChoices["mask"].EmitSignal(OptionButton.SignalName.ItemSelected,0L);ui.EquipmentChoices["helmet"].EmitSignal(OptionButton.SignalName.ItemSelected,0L);Check(ui.Actor.Meshes["hair_tied"].Visible&&ui.Actor.Meshes["beard_short"].Visible,"removal restores hair and beard");
            ui.Change(look=>{look.SkinLinear=new float[]{.6f,.38f,.23f};look.MaterialSrgb["cloth"]=new float[]{.4f,.6f,1};return look;});ui.SaveButton.EmitSignal(Button.SignalName.Pressed);string saved=ui.SaveRoster();Check(System.IO.File.ReadAllText(ui.StoragePath)==saved,"UI saves full roster atomically");ui.NewButton.EmitSignal(Button.SignalName.Pressed);ui.LoadButton.EmitSignal(Button.SignalName.Pressed);Check(ui.SaveRoster()==saved&&ui.Roster.Officers.Count==3,"UI restores roster / names / parts / colors");
            foreach(string bad in new[]{saved.Replace("mature_v1","missing_face"),saved.Replace("red_lamellar","missing_armor"),saved.Replace("\"Schema\": 1","\"Schema\": 777")}){bool rejected=false;try{ui.LoadRoster(bad);}catch{rejected=true;}Check(rejected&&ui.SaveRoster()==saved,"invalid roster rejected without partial changes");}
            using(var f=FileAccess.Open("user://officer3d-v3-test-expected.json",FileAccess.ModeFlags.Write))f.StoreString(saved);
            result["persistence"]="GUI signals save/load 3 officers; invalid IDs/schema rejected atomically; separate process checked by runner";
            ui.OfficerChoice.Select(1);ui.OfficerChoice.EmitSignal(OptionButton.SignalName.ItemSelected,1L);ui.Paused=true;ui.Actor.Freeze("idle",.35);ui.Camera.Size=3.3f;((SubViewportContainer)ui.View.GetParent()).Stretch=true;await Capture("creator-ui",true);((SubViewportContainer)ui.View.GetParent()).Stretch=false;
            // Resource sharing and per-instance color independence with mixed source/new parts.
            var perf=new Dictionary<string,object>();DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);Engine.MaxFps=0;ui.View.Size=new(1280,800);ui.Paused=false;
            foreach(var count in new[]{1,10,50})foreach(var lod in new[]{"near","mid","far"})
            {
                ui.Rebuild(count);ui.AutoLod=false;ui.SetProcess(false);foreach(var model in ui.Models){model.SetLod(lod);model.Play("walk");model.Player.SpeedScale=1;}
                if(count>1){var x=ui.Models[0];var y=ui.Models[1];Check(x.Meshes["slot_torso"].Mesh.GetInstanceId()==y.Meshes["slot_torso"].Mesh.GetInstanceId(),"geometry resource shared");Check(x.Meshes["alt_torso"].Mesh.GetInstanceId()==y.Meshes["alt_torso"].Mesh.GetInstanceId(),"new armor geometry resource shared");Check(!x.Materials.Select(m=>m.Material.GetInstanceId()).Intersect(y.Materials.Select(m=>m.Material.GetInstanceId())).Any(),"all material instances independent");var otherUniforms=y.Materials.Select(m=>m.Material.GetShaderParameter("lacquer_tint")).ToArray();string untouched=y.Save();x.Appearance.MaterialSrgb["lacquer"]=new float[]{.3f,.5f,.8f};x.Apply(x.Appearance);Check(y.Save()==untouched,"color edit does not change other officer");Check(y.Materials.Select((m,index)=>m.Material.GetShaderParameter("lacquer_tint").Equals(otherUniforms[index])).All(v=>v),"other officer live material uniforms unchanged");}
                await Frames(15);var ms=new List<double>();for(int frame=0;frame<60;frame++){var timer=Stopwatch.StartNew();await Frames(1);ms.Add(timer.Elapsed.TotalMilliseconds);}
                double avg=ms.Average();perf[lod+"-"+count]=new{average_ms=avg,fps=1000/avg,draw_calls=ui.View.GetRenderInfo(Viewport.RenderInfoType.Visible,Viewport.RenderInfo.DrawCallsInFrame),primitives=ui.View.GetRenderInfo(Viewport.RenderInfoType.Visible,Viewport.RenderInfo.PrimitivesInFrame)};await Capture("mixed-"+lod+"-"+count);
            }
            result["performance"]=perf;result["hardware"]=RenderingServer.GetVideoAdapterName();result["measurement"]="1280x800 GL Compatibility, 15 warmup +60 samples, shadows, unique positions, mixed presets / per-slot mixes / color variants; 50 displayed instances of 3 saved officers, not 50 authored unique characters";
            ui.OfficerChoice.Select(1);ui.OfficerChoice.EmitSignal(OptionButton.SignalName.ItemSelected,1L);ui.Rebuild(1);ui.Paused=true;ui.Actor.Player.SpeedScale=0;ui.Actor.Freeze("idle",.35);ui.RefreshEditor();((SubViewportContainer)ui.View.GetParent()).Stretch=true;await Capture("review",true);
            result["raw_capture_names"]=captures.ToArray();System.IO.File.WriteAllText(ui.Output+"/results.json",JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));GD.Print("V3_TEST_COMPLETE");ui.GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());ui.GetTree().Quit(1);}
    }
}
