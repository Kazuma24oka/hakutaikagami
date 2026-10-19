using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

public partial class Officer3DReview : Node3D
{
    Officer3DModel actor = null!;
    Camera3D camera = null!;
    Label status = null!;
    readonly Dictionary<string,CheckButton> toggles = new();
    bool move;
    float direction=1;
    readonly string output = ProjectSettings.GlobalizePath("res://../Art/Officer3D/TrialV1/runtime-local");
    public override void _Ready()
    {
        try { Setup(); if(OS.GetCmdlineUserArgs().Contains("--officer3d-test")) CallDeferred(nameof(StartTests));else if(OS.GetCmdlineUserArgs().Contains("--officer3d-restore-test")) CallDeferred(nameof(RestartRestore)); }
        catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    void Setup()
    {
        DisplayServer.WindowSetSize(new Vector2I(1280,800));
        GetViewport().MeshLodThreshold=0;
        GetViewport().Msaa3D=Viewport.Msaa.Disabled;
        var environment = new Godot.Environment { BackgroundMode=Godot.Environment.BGMode.Color, BackgroundColor=new Color("18232c"), AmbientLightSource=Godot.Environment.AmbientSource.Color, AmbientLightColor=Colors.White, AmbientLightEnergy=.18f, TonemapMode=Godot.Environment.ToneMapper.Linear };
        environment.Sky=new Sky {SkyMaterial=new ProceduralSkyMaterial {SkyTopColor=new Color("627d96"),SkyHorizonColor=new Color("c3c7c5"),GroundBottomColor=new Color("36433c"),GroundHorizonColor=new Color("a0a8a1")}};
        environment.ReflectedLightSource=Godot.Environment.ReflectionSource.Sky;
        AddChild(new WorldEnvironment {Environment=environment});
        AddChild(new DirectionalLight3D {RotationDegrees=new Vector3(-45,-35,0),LightEnergy=.25f,ShadowEnabled=true});
        AddChild(new MeshInstance3D {Mesh=new PlaneMesh {Size=new Vector2(80,80)}, MaterialOverride=new StandardMaterial3D {AlbedoColor=new Color("435345"),Roughness=1}});
        float pitch=Mathf.DegToRad(38);
        camera=new Camera3D {Projection=Camera3D.ProjectionType.Orthogonal,Size=9,Position=new Vector3(0,1+10*Mathf.Sin(pitch),10*Mathf.Cos(pitch)),Current=true,HOffset=1.8f}; AddChild(camera);camera.LookAt(new Vector3(0,1,0));
        actor=new Officer3DModel {Name="Officer"};AddChild(actor);
        var layer=new CanvasLayer();AddChild(layer);
        var panel=new PanelContainer {Position=new Vector2(940,10),CustomMinimumSize=new Vector2(330,770)};layer.AddChild(panel);
        var scroll=new ScrollContainer {CustomMinimumSize=new Vector2(330,770)};panel.AddChild(scroll);
        var box=new VBoxContainer {CustomMinimumSize=new Vector2(310,0)};scroll.AddChild(box);
        box.AddChild(new Label {Text="百代鏡 / 3D武将 技術試作\n立ち絵は別途2Dイラスト"});
        status=new Label {Text="同じ形状・骨格・外見設定を全周で共有",AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(status);
        var views=new HBoxContainer();box.AddChild(views);
        for(int i=0;i<4;i++) {int angle=i*90;Button b=new(){Text=new[]{"正面","右","背面","左"}[i]};views.AddChild(b);b.Pressed+=()=>actor.RotationDegrees=new Vector3(0,angle,0);}
        var motions=new HBoxContainer();box.AddChild(motions);
        foreach(var item in new[]{("idle","待機"),("walk","歩行"),("attack","斬撃")}) {var b=new Button {Text=item.Item2};motions.AddChild(b);b.Pressed+=()=>actor.Play(item.Item1);}
        var slow=new CheckButton {Text="1/4速度"};box.AddChild(slow);slow.Toggled+=value=>actor.Player.SpeedScale=value?.25f:1;
        var movement=new CheckButton {Text="位置移動の試験（Coreと独立）"};box.AddChild(movement);movement.Toggled+=v=>{move=v;actor.Position=Vector3.Zero;actor.Play(v?"walk":"idle");};
        box.AddChild(new Label{Text="カメラ距離（接写は制限）"});var zoom=new HSlider {MinValue=7,MaxValue=14,Value=9,Step=.5};box.AddChild(zoom);zoom.ValueChanged+=v=>camera.Size=(float)v;
        box.AddChild(new Label{Text="肌：顔と体を一括 / 目・眉・口は保護"});
        var skin=new ColorPickerButton {Color=new Color(.87f,.73f,.59f).LinearToSrgb(),EditAlpha=false,CustomMinimumSize=new Vector2(0,32)};box.AddChild(skin);
        skin.ColorChanged+=c=>{var l=c.SrgbToLinear();actor.Appearance.SkinLinear=new[]{l.R,l.G,l.B};actor.Apply(actor.Appearance);};
        var presets=new HBoxContainer();box.AddChild(presets);
        foreach(var item in new[]{("元の肌",new Color(.87f,.73f,.59f)),("濃い肌",new Color(.3f,.16f,.08f))}){var b=new Button{Text=item.Item1};presets.AddChild(b);b.Pressed+=()=>{skin.Color=item.Item2.LinearToSrgb();actor.Appearance.SkinLinear=new[]{item.Item2.R,item.Item2.G,item.Item2.B};actor.Apply(actor.Appearance);};}
        box.AddChild(new Label{Text="装備全体：乗算色（白への置換ではない）"});var gear=new ColorPickerButton {Color=Colors.White,EditAlpha=false,CustomMinimumSize=new Vector2(0,32)};box.AddChild(gear);gear.ColorChanged+=c=>{actor.Appearance.GearSrgb=new[]{c.R,c.G,c.B};actor.Apply(actor.Appearance);};
        box.AddChild(new Label{Text="装備の着脱 / 部位別の乗算色"});
        var labels=new[]{"面頬","刀","下衣・袴","マント","兜","胴","喉輪","肩","籠手","履物","草摺","佩楯","脛当"};
        var slotPickers=new Dictionary<string,ColorPickerButton>();
        for(int i=0;i<Officer3DModel.Slots.Length;i++)
        {
            string slot=Officer3DModel.Slots[i];var row=new HBoxContainer();box.AddChild(row);
            var toggle=new CheckButton{Text=labels[i],ButtonPressed=true,CustomMinimumSize=new Vector2(170,0)};row.AddChild(toggle);toggles[slot]=toggle;
            toggle.Toggled+=v=>{actor.Appearance.Enabled[slot]=v;actor.Apply(actor.Appearance);};
            var color=new ColorPickerButton{Color=Colors.White,EditAlpha=false,CustomMinimumSize=new Vector2(90,0)};row.AddChild(color);
            slotPickers[slot]=color;
            color.ColorChanged+=c=>{actor.Appearance.SlotSrgb[slot]=new[]{c.R,c.G,c.B};actor.Apply(actor.Appearance);};
        }
        var saves=new HBoxContainer();box.AddChild(saves);var save=new Button{Text="外見を保存"};var load=new Button{Text="外見を復旧"};saves.AddChild(save);saves.AddChild(load);
        save.Pressed+=()=>{using var f=FileAccess.Open("user://officer3d-trial-appearance.json",FileAccess.ModeFlags.Write);f.StoreString(actor.Save());status.Text="外見設定を保存しました";};
        load.Pressed+=()=>{try {actor.Restore(FileAccess.GetFileAsString("user://officer3d-trial-appearance.json"));foreach(var s in Officer3DModel.Slots){toggles[s].SetPressedNoSignal(actor.Appearance.Enabled[s]);var rgb=actor.Appearance.SlotSrgb[s];slotPickers[s].Color=new Color(rgb[0],rgb[1],rgb[2]);}var a=actor.Appearance;skin.Color=new Color(a.SkinLinear[0],a.SkinLinear[1],a.SkinLinear[2]).LinearToSrgb();gear.Color=new Color(a.GearSrgb[0],a.GearSrgb[1],a.GearSrgb[2]);status.Text="外見設定を復旧しました";}catch(Exception e){status.Text=e.Message;}};
    }
    public override void _Process(double delta) {if(move){actor.Position+=new Vector3(direction*.7f*(float)delta,0,0);if(Math.Abs(actor.Position.X)>3)direction=-direction;actor.RotationDegrees=new Vector3(0,direction>0?90:-90,0);}}
    async Task Frames(int n=3) {for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    async Task<Image> Capture(string name) {await Frames();var image=GetViewport().GetTexture().GetImage();image.SavePng(output+"/"+name+".png");return image;}
    static void Check(bool condition,string text) {if(!condition)throw new InvalidOperationException(text);GD.Print("OFFICER3D CHECK "+text);}
    public async void RestartRestore()
    {
        try {await Frames(5);var json=FileAccess.GetFileAsString("user://officer3d-trial-test.json");actor.Restore(json);Check(actor.Save()==json,"appearance restored in second Godot process");await Capture("restart-restored");GD.Print("OFFICER3D RESTART_RESTORE_COMPLETE");GetTree().Quit();}
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    static Dictionary<string,int> Compare(Image before,Image after,Image mask,bool skin)
    {
        int inside=0,outside=0,face=0,body=0;
        // The UI is outside the 3D validation region.
        for(int y=0;y<before.GetHeight();y++)for(int x=0;x<930;x++)
        {
            var a=before.GetPixel(x,y);var b=after.GetPixel(x,y);var m=mask.GetPixel(x,y);
            bool changed=a.R!=b.R || a.G!=b.G || a.B!=b.B;
            bool allowed=skin?(m.R>.7f||m.G>.7f):m.B>.7f;
            if(changed) {if(allowed){inside++;if(m.R>.7f)face++;if(m.G>.7f)body++;}else outside++;}
        }
        return new(){{"changed_inside",inside},{"changed_outside",outside},{"face_changed",face},{"body_changed",body}};
    }
    public async void StartTests()
    {
        try
        {
            System.IO.Directory.CreateDirectory(output);await Frames(8);
            Check(actor.Skeleton.GetBoneCount()==73,"one 73-bone skeleton");Check(actor.Clips.Count==3,"three clips");
            var result=new Dictionary<string,object>();var original=actor.Save();
            var uvView=new SubViewport {Size=new Vector2I(1024,1024),OwnWorld3D=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Always};AddChild(uvView);
            var uvRoot=new Node3D();uvView.AddChild(uvRoot);var uvMat=(ShaderMaterial)actor.Materials.Single(m=>m.Role=="skin_face").Material.Duplicate();uvMat.SetShaderParameter("verification_unlit",true);
            uvRoot.AddChild(new MeshInstance3D {Mesh=new QuadMesh {Size=Vector2.One},MaterialOverride=uvMat});var uvCamera=new Camera3D {Projection=Camera3D.ProjectionType.Orthogonal,Size=1,Position=new Vector3(0,0,1),Current=true};uvRoot.AddChild(uvCamera);
            await Frames();var faceBefore=uvView.GetTexture().GetImage();faceBefore.SavePng(output+"/face-paint-before.png");uvMat.SetShaderParameter("debug_mask",1);await Frames();var faceMask=uvView.GetTexture().GetImage();faceMask.SavePng(output+"/face-paint-mask.png");uvMat.SetShaderParameter("debug_mask",0);uvMat.SetShaderParameter("tint",new Vector3(.3f,.16f,.08f)/new Vector3(.97647065f,.90196085f,.84705889f));await Frames();var faceAfter=uvView.GetTexture().GetImage();faceAfter.SavePng(output+"/face-paint-after.png");var uvDiff=Compare(faceBefore,faceAfter,faceMask,true);Check(uvDiff["changed_outside"]==0&&uvDiff["changed_inside"]>0,"painted mouth UV mask protects RGB in actual GPU shader");
            Check(faceMask.GetPixel(462,775).R<.1f && faceBefore.GetPixel(462,775)==faceAfter.GetPixel(462,775),"opaque painted mouth pigment unchanged");
            using(var sourceImage=uvMat.GetShaderParameter("base_texture").As<Texture2D>().GetImage())
            {
                if(sourceImage.IsCompressed())sourceImage.Decompress();var s=sourceImage.GetPixel(100,100).SrgbToLinear();var expected=new Color(s.R*.3f/.97647065f,s.G*.16f/.90196085f,s.B*.08f/.84705889f).LinearToSrgb();var actual=faceAfter.GetPixel(100,100);
                Check(Math.Max(Math.Abs(expected.R-actual.R),Math.Max(Math.Abs(expected.G-actual.G),Math.Abs(expected.B-actual.B)))<2f/255,"actual RGB matches explicit linear tint and sRGB output");
            }
            result["painted_mouth_gpu"]=uvDiff;uvView.QueueFree();await Frames();
            var maskResults=new List<object>();
            var clipping=new List<object>();
            int faceChanges=0,bodyChanges=0;
            foreach(bool bare in new[]{false,true})
            for(int angle=0;angle<360;angle+=90)
            {
                actor.Restore(original);if(bare){foreach(var s in Officer3DModel.Slots){toggles[s].ButtonPressed=false;actor.Appearance.Enabled[s]=false;}actor.Apply(actor.Appearance);}
                actor.RotationDegrees=new Vector3(0,angle,0);actor.Freeze("idle",.5);
                var before=await Capture($"{(bare?"bare":"full")}-{angle}");
                actor.DebugMask(1);var mask=await Capture($"skin-mask-{bare}-{angle}");actor.DebugMask(0);
                if(bare)
                {
                    int clipped=0,skinPixels=0;
                    for(int y=0;y<before.GetHeight();y++)for(int x=0;x<930;x++){var m=mask.GetPixel(x,y);if(m.R>.7f||m.G>.7f){skinPixels++;var p=before.GetPixel(x,y);if(p.R==1||p.G==1||p.B==1)clipped++;}}
                    Check(clipped==0,"default skin channels not clipped "+angle);clipping.Add(new{angle,skinPixels,clipped});
                }
                actor.Appearance.SkinLinear=new[]{.3f,.16f,.08f};actor.Apply(actor.Appearance);
                var after=await Capture($"skin-tan-{bare}-{angle}");var diff=Compare(before,after,mask,true);
                Check(diff["changed_outside"]==0,$"skin protection {bare} {angle}");
                faceChanges+=diff["face_changed"];bodyChanges+=diff["body_changed"];
                maskResults.Add(new{bare,angle,diff});
            }
            Check(maskResults.Count==8 && faceChanges>0 && bodyChanges>0,"four views; face and body actually recolored");result["skin_gpu"]=maskResults;
            result["default_skin_clipping"]=clipping;
            actor.Restore(original);foreach(var s in Officer3DModel.Slots)toggles[s].SetPressedNoSignal(true);actor.Rotation=Vector3.Zero;actor.Freeze("idle",.5);
            var gearBefore=await Capture("gear-before");actor.DebugMask(2);var gearMask=await Capture("gear-mask");actor.DebugMask(0);
            actor.Appearance.GearSrgb=new[]{.4f,.7f,.8f};actor.Apply(actor.Appearance);var gearAfter=await Capture("gear-tinted");var gearDiff=Compare(gearBefore,gearAfter,gearMask,false);Check(gearDiff["changed_inside"]>0&&gearDiff["changed_outside"]==0,"gear protects skin and features");result["gear_gpu"]=gearDiff;
            actor.Restore(original);actor.DebugMask(2,"torso");var torsoMask=await Capture("torso-mask");actor.DebugMask(0);actor.Appearance.SlotSrgb["torso"]=new[]{.2f,.5f,.9f};actor.Apply(actor.Appearance);var torsoAfter=await Capture("torso-tinted");var torsoDiff=Compare(gearBefore,torsoAfter,torsoMask,false);Check(torsoDiff["changed_inside"]>0&&torsoDiff["changed_outside"]==0,"per-slot torso GPU isolation");result["torso_gpu"]=torsoDiff;
            foreach(var slot in Officer3DModel.Slots)
            {
                actor.Restore(original);var prior=actor.Materials.Select(x=>x.Material.GetShaderParameter("tint").AsVector3()).ToArray();actor.Appearance.SlotSrgb[slot]=new[]{.2f,.5f,.9f};actor.Apply(actor.Appearance);
                for(int i=0;i<prior.Length;i++){bool changed=prior[i]!=actor.Materials[i].Material.GetShaderParameter("tint").AsVector3();Check(changed==(actor.Materials[i].Role=="gear_"+slot),"material isolation "+slot+" / "+actor.Materials[i].Role);}
            }
            actor.Restore(original);foreach(var s in Officer3DModel.Slots){toggles[s].ButtonPressed=false;Check(!actor.Meshes["slot_"+s].Visible,"toggle off "+s);actor.Freeze("idle",.5);await Capture("off-"+s+"-idle");actor.Freeze("walk",.6);await Capture("off-"+s+"-walk");toggles[s].ButtonPressed=true;Check(actor.Meshes["slot_"+s].Visible,"toggle restore "+s);}
            var appearance=actor.Save();var other=new Officer3DModel {Position=new Vector3(3,0,0)};AddChild(other);other.Restore(appearance);Check(other.Save()==appearance,"appearance roundtrip to fresh actor");
            Check(actor.Materials.Zip(other.Materials).All(p=>p.First.Material.GetInstanceId()!=p.Second.Material.GetInstanceId()),"actors own independent shader materials");
            actor.Appearance.SkinLinear=new[]{.1f,.1f,.1f};actor.Apply(actor.Appearance);Check(other.Save()==appearance,"actor material independence");other.QueueFree();await Frames();actor.Restore(original);
            using(var file=FileAccess.Open("user://officer3d-trial-test.json",FileAccess.ModeFlags.Write))file.StoreString(appearance);
            actor.Restore(FileAccess.GetFileAsString("user://officer3d-trial-test.json"));Check(actor.Save()==appearance,"disk save roundtrip");
            var motion=new List<object>();int bone=actor.Skeleton.FindBone("J_Bip_L_LowerLeg");Check(bone>=0,"animation probe bone");
            foreach(var id in new[]{"idle","walk","attack"})
            {
                actor.Freeze(id,0);await Frames();var first=Enumerable.Range(0,73).Select(actor.Skeleton.GetBonePoseRotation).ToArray();actor.Freeze(id,.6);await Frames();
                float difference=Enumerable.Range(0,73).Max(i=>first[i].AngleTo(actor.Skeleton.GetBonePoseRotation(i)));Check(difference>.00001f,"sampled skeletal movement "+id);motion.Add(new{id,max_bone_angle_change=difference});
                int root=actor.Skeleton.FindBone("Root");float maxDrift=0;
                for(int sample=0;sample<=20;sample++){actor.Freeze(id,actor.Player.GetAnimation(actor.Clips[id]).Length*sample/20);var p=actor.Skeleton.GetBonePosePosition(root);maxDrift=Math.Max(maxDrift,new Vector2(p.X,p.Z).Length());}
                Check(maxDrift<.0001f,"Root planar translation fixed "+id);
                actor.Player.SpeedScale=1;actor.Play(id);await Frames(40);await Capture("motion-"+id);
            }
            actor.Player.SpeedScale=.25f;actor.Play("walk");await Frames(40);Check(actor.Player.IsPlaying(),"quarter-speed playback");actor.Player.SpeedScale=1;
            actor.Play("attack");await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);Check(actor.Player.CurrentAnimation==actor.Clips["idle"],"attack returns to idle");result["motion"]=motion;
            actor.Restore(original);actor.Freeze("idle",.5);await Capture("review-default");
            for(int frame=0;frame<24;frame++){actor.Freeze("walk",actor.Player.GetAnimation(actor.Clips["walk"]).Length*frame/24);await Capture("walk-frame-"+frame.ToString("D2"));}
            var performance=new List<object>();
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);Engine.MaxFps=0;
            GetChildren().OfType<CanvasLayer>().Single().Visible=false;
            foreach(int count in new[]{1,10,50})
            {
                var extras=new List<Officer3DModel>();camera.Size=count==1?9:count==10?18:32;camera.HOffset=0;
                for(int i=1;i<count;i++){var o=new Officer3DModel{Position=new Vector3((i%10-4)*2,0,(i/10)*2-4)};AddChild(o);extras.Add(o);o.Play("walk");}
                actor.Play("walk");await Frames(15);var sw=Stopwatch.StartNew();await Frames(60);sw.Stop();performance.Add(new{count,frame_mean_ms=sw.Elapsed.TotalMilliseconds/60,estimated_fps=60000/sw.Elapsed.TotalMilliseconds,draw_calls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),primitives=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)});await Capture("actors-"+count);
                foreach(var o in extras)o.QueueFree();await Frames();
            }
            result["performance"]=performance;result["status"]="GPU_AUTOMATION_PASSED_VISUAL_REVIEW_REQUIRED";result["renderer"]=RenderingServer.GetVideoAdapterName();
            result["dotnet_runtime"]=System.Environment.Version.ToString();result["godot"]=Engine.GetVersionInfo()["string"].ToString();result["viewport"]="1280x800";result["performance_conditions"]="Compatibility/OpenGL; vsync off; frame cap off; UI hidden; one directional shadow; fixed sky reflection; automatic LOD disabled via MeshLodThreshold=0";
            System.IO.File.WriteAllText(output+"/results.json",System.Text.Json.JsonSerializer.Serialize(result,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));GD.Print("OFFICER3D GPU_TEST_COMPLETE "+output);GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());System.IO.File.WriteAllText(output+"/failure.txt",e.ToString());GetTree().Quit(1);}
    }
}
