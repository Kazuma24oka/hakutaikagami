using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Appearance;
public partial class OfficerLibraryTrial
{
    OptionButton lordChoice=null!,faceChoice=null!,hairChoice=null!,beardChoice=null!,skinChoice=null!,gearChoice=null!;CheckBox helmetChoice=null!;
    OfficerAppearance currentLook=AppearanceCatalog.Default(1);Officer3DCustomActor lookActor=null!;bool restoringLook;
    readonly List<string?> lordIds=new();
    static readonly float[][] SkinValues={new[]{.87f,.73f,.59f},new[]{.64f,.43f,.28f},new[]{.95f,.85f,.73f}};
    static string KnownSiteForEdit(string site){try{return RegionalCampaign.Site(NewGameWorld.Node(site)).Value;}catch(InvalidDataException){return site;}}
    void BuildEnhanced(GridContainer grid,VBoxContainer left)
    {
        var preview=new SubViewportContainer{Stretch=true,CustomMinimumSize=new(200,120)};left.AddChild(preview);
        var view=new SubViewport{OwnWorld3D=true,Size=new(600,170),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};preview.AddChild(view);var stage=new Node3D();view.AddChild(stage);
        stage.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("343d40"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("e9e7d8"),AmbientLightEnergy=.7f}});stage.AddChild(new DirectionalLight3D{RotationDegrees=new(-50,-30,0),LightEnergy=.9f});
        lookActor=new();stage.AddChild(lookActor);lookActor.RotationDegrees=new(0,20,0);var camera=new Camera3D{Position=new(0,1.0f,4),Projection=Camera3D.ProjectionType.Orthogonal,Size=2.4f,Current=true};stage.AddChild(camera);camera.LookAt(new(0,.9f,0));lookActor.Freeze("idle",0);
        lordChoice=new();grid.AddChild(new Label{Text="直属相手"});grid.AddChild(lordChoice);
        faceChoice=Choice(grid,"顔型",new[]{"基本","壮年"});hairChoice=Choice(grid,"髪型",new[]{"兜の下","束ね髪"});beardChoice=Choice(grid,"髭",new[]{"なし","短い髭"});skinChoice=Choice(grid,"肌の色",new[]{"標準","濃い","淡い"});gearChoice=Choice(grid,"甲冑",new[]{"赤備え","銀の具足"});helmetChoice=new(){Text="兜を着用"};grid.AddChild(new Label{Text="兜"});grid.AddChild(helmetChoice);helmetChoice.Toggled+=_=>UpdateLook();
    }
    OptionButton Choice(Node parent,string label,string[] choices){parent.AddChild(new Label{Text=label});var option=new OptionButton();foreach(var c in choices)option.AddItem(c);parent.AddChild(option);option.ItemSelected+=_=>UpdateLook();return option;}
    void RestoreEnhanced(RegisteredOfficer? p)
    {
        restoringLook=true;currentLook=ContractJson.Copy(p?.Look??AppearanceCatalog.Default(1));lordChoice.Clear();lordIds.Clear();lordChoice.AddItem("場所の所属から決める / 浪人はなし");lordIds.Add(null);
        var candidates=editingGroup==null?library.Ungrouped:library.Groups.Single(g=>g.Id==editingGroup).Officers;
        foreach(var q in candidates.Where(q=>q.Id!=p?.Id)){lordChoice.AddItem(q.Name);lordIds.Add(q.Id);}foreach(var q in NationalPersonnel.Catalog.People){lordChoice.AddItem(q.Name+"（実在）");lordIds.Add(q.Id.Value);}
        int lordAt=lordIds.IndexOf(p?.Lord);if(lordAt<0){lordChoice.AddItem("未登場の直属相手（配置時に要確認）");lordIds.Add(p?.Lord);lordAt=lordIds.Count-1;}lordChoice.Select(lordAt);
        faceChoice.Select(currentLook.Face=="mature_v1"?1:0);hairChoice.Select(currentLook.Hair=="tied_v1"?1:0);beardChoice.Select(currentLook.Beard=="short_v1"?1:0);
        int skinAt=Array.FindIndex(SkinValues,rgb=>rgb.SequenceEqual(currentLook.SkinLinear));skinChoice.Select(skinAt<0?0:skinAt);gearChoice.Select(currentLook.Equipment["torso"]=="red_lamellar"?0:1);helmetChoice.ButtonPressed=currentLook.Enabled["helmet"];
        restoringLook=false;lookActor.Apply(ContractJson.Read<Officer3DCustomLook>(ContractJson.Write(currentLook)));lookActor.Freeze("idle",0);
    }
    void UpdateLook()
    {
        if(restoringLook||lookActor==null)return;
        currentLook.Face=faceChoice.Selected==1?"mature_v1":"v013_face";currentLook.Hair=hairChoice.Selected==1?"tied_v1":"none_under_helmet";currentLook.Beard=beardChoice.Selected==1?"short_v1":"none";
        currentLook.SkinLinear=SkinValues[Math.Max(0,skinChoice.Selected)].ToArray();foreach(var slot in AppearanceCatalog.RedSlots)currentLook.Equipment[slot]=gearChoice.Selected==0?"red_lamellar":"silver";
        currentLook.Enabled["helmet"]=helmetChoice.ButtonPressed;currentLook.Enabled["mask"]=false;lookActor.Apply(ContractJson.Read<Officer3DCustomLook>(ContractJson.Write(currentLook)));lookActor.Freeze("idle",0);
    }
    string? EnhancedLord()=>role.Selected is 0 or 5?null:lordIds[lordChoice.Selected];
    void ResetEnhanced(){role.Select(0);birth.Value=1520;foreach(var n in attributes)n.Value=50;RestoreEnhanced(null);}
    internal void ExerciseSecondDaimyo(){Press(create);personName.Text="鏡山 別家";role.Select(5);factionName.Text="別家";site.Select(Array.IndexOf(SiteIds,"site:castle"));Press(apply);}
    internal void ExerciseMinor(){Press(create);personName.Text="鏡山 幼少";birth.Value=1555;role.Select(0);site.Select(Array.IndexOf(SiteIds,"site:castle"));Press(apply);}
    internal void ExerciseServiceOfficer(string node,bool retainer)
    {
        Press(create);personName.Text="鏡山 宗久";role.Select(retainer?1:0);site.Select(Array.IndexOf(SiteIds,node));if(retainer)lordChoice.Select(lordIds.IndexOf(node=="site:odawara"?"person:ujiyasu":node=="site:castle"?"person:nobunaga":"person:shingen"));skinChoice.Select(1);hairChoice.Select(1);UpdateLook();Press(apply);
    }
    internal OfficerLibrary CurrentLibrary=>ContractJson.Copy(library);
    internal void ExerciseManagement(string output)
    {
        void Assert(bool ok,string text){if(!ok)throw new Exception(text);GD.Print("MANAGEMENT_PASS "+text);}
        Press(create);personName.Text="鏡山 宗久";role.Select(5);factionName.Text="鏡山家";site.Select(Array.IndexOf(SiteIds,"site:castle"));skinChoice.Select(1);hairChoice.Select(1);UpdateLook();Press(apply);Assert(originals.Size.Y>=100&&members.Size.Y>=70,"both original and group member lists remain usable");Assert(library.Ungrouped.Length==1,"created daimyo with shared 3D appearance");string head=library.Ungrouped[0].Id;
        Press(create);personName.Text="鏡山 景綱";role.Select(1);lordChoice.Select(lordIds.IndexOf(head));site.Select(Array.IndexOf(SiteIds,"site:castle"));gearChoice.Select(1);UpdateLook();Press(apply);Assert(library.Ungrouped[1].Lord==head,"created retainer with explicit individual lord");
        groupName.Text="尾張の一門";Press(addGroup);originals.Select(0);Press(copy);originals.Select(1);Press(copy);groupName.Text="尾張の一門・試作";Press(rename);Assert(library.Groups[0].Name==groupName.Text&&library.Groups[0].Officers.Length==2,"group renamed with independent copies");
        members.Select(0);members.EmitSignal(ItemList.SignalName.ItemSelected,0);personName.Text="鏡山 宗久（尾張）";attributes[0].Value=80;Press(apply);Assert(library.Ungrouped[0].Name=="鏡山 宗久"&&library.Ungrouped[0].Attributes.Leadership==50,"group edit preserves original");
        string exportPath=Path.Combine(output,"exported-officers.json");export.EmitSignal(FileDialog.SignalName.FileSelected,exportPath);Assert(ContractJson.CanonicalHash(ContractJson.Read<OfficerLibrary>(File.ReadAllBytes(exportPath)))==ContractJson.CanonicalHash(library),"actual export dialog signal writes all definitions");
        string before=ContractJson.CanonicalHash(library);import.EmitSignal(FileDialog.SignalName.FileSelected,exportPath);Assert(before==ContractJson.CanonicalHash(library)&&before==ContractJson.CanonicalHash(store.Load()),"actual import dialog rejects duplicate IDs without overwrite");
        var extra=new RegisteredOfficer("created:imported-demo",null,"鏡山 正信",1530,new(40,50,50,60),StartTemplate.Ronin,"site:kofu",null,null,AppearanceCatalog.Default(0));string input=Path.Combine(output,"imported-officers.json");File.WriteAllBytes(input,ContractJson.Write(new OfficerLibrary(1,[extra],[])));import.EmitSignal(FileDialog.SignalName.FileSelected,input);Assert(library.Ungrouped.Length==3,"actual import dialog adds independent officer");
        originals.Select(0);originals.EmitSignal(ItemList.SignalName.ItemSelected,0);Assert(ContractJson.CanonicalHash(currentLook)==ContractJson.CanonicalHash(library.Ungrouped[0].Look),"stored appearance restores exactly into live model");
    }
}
