using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;

public sealed record SavedStartProfile(string Instance, StartPackage Package);
public partial class StartSelectionPanel : Control
{
    VBoxContainer form=null!,play=null!;LineEdit nameInput=null!;Label message=null!,intro=null!;Button begin=null!,resume=null!,load=null!,back=null!;
    OptionButton saved=null!,role=null!;ConfirmationDialog confirmation=null!;ContinuousWorldTrial? stage;
    readonly List<(SavedStartProfile Profile,string Directory)> profiles=new();
    (SavedStartProfile Profile,string Directory,bool Load)? pending;
    string SelectionMode=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--start-selection-"))??"";
    bool IsTest=>SelectionMode!=""||OS.GetCmdlineUserArgs().Any(a=>a.StartsWith("--work-hub-"));
    string Root=>IsTest?ProjectSettings.GlobalizePath("res://../Docs/Design/StartSelectionV1/runtime-local/configs/"+Guid.ParseExact(OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--save-key="))[11..],"N").ToString("N")):ProjectSettings.GlobalizePath("user://personal-start-v1/configured");
    string CurrentDirectory="";
    static bool HashDirectory(string value,int length)=>value.Length==length&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public override void _Ready()
    {
        form=new(){SizeFlagsHorizontal=SizeFlags.ExpandFill};AddChild(form);form.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        form.AddChild(new Label{Text="作成武将として始める"});
        form.AddChild(new Label{Text="1560年・清洲から / 成人 / 準史実（既定）"});
        intro=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};form.AddChild(intro);
        form.AddChild(new Label{Text="名前"});nameInput=new(){Text="作成武将",MaxLength=24,PlaceholderText="1～24文字"};form.AddChild(nameInput);
        form.AddChild(new Label{Text="開始時の立場"});role=new();role.AddItem("浪人");role.AddItem("織田家の家臣（作成人物の架空の開始合意）");role.AddItem("織田家の家臣・本隊配置任務（試作兵100）");form.AddChild(role);role.ItemSelected+=_=>RefreshIntro();RefreshIntro();
        begin=new(){Text="新しく始める"};form.AddChild(begin);begin.Pressed+=RequestNew;
        resume=new(){Text="いまの人物へ戻る",Visible=false};form.AddChild(resume);resume.Pressed+=ShowGame;
        form.AddChild(new HSeparator());form.AddChild(new Label{Text="保存した人物の続き"});saved=new();form.AddChild(saved);
        load=new(){Text="選んだ保存を開く"};form.AddChild(load);load.Pressed+=RequestLoad;
        message=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};form.AddChild(message);
        form.AddChild(new Label{Text="陪臣・城主などと史実武将の開始は今後追加します。作成家臣の主従は史実の任命ではありません。",AutowrapMode=TextServer.AutowrapMode.WordSmart,Modulate=new Color("e6c785")});
        play=new(){Visible=false};AddChild(play);play.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        back=new(){Text="開始設定へ戻る（現在の人物は保持）"};play.AddChild(back);back.Pressed+=ShowForm;
        confirmation=new(){Title="現在の進行を切り替えますか",DialogText="画面上の未保存の進行を閉じ、選んだ人物へ切り替えます。保存したデータは残ります。"};AddChild(confirmation);
        confirmation.Confirmed+=OpenPending;confirmation.Canceled+=()=>pending=null;
        ScanProfiles();if(SelectionMode!="")CallDeferred(nameof(RunSelectionChecks));
    }
    void RefreshIntro(){intro.Text=role.Selected==2?"主家の試作兵100で、本隊の配置と帰還を試せます。私有の兵・土地・徴税権は持ちません。":"本人の移動と、現地の仕事を試せます。軍・土地・徴税権は持ちません。";}
    StartPackage NewPackage(string name)
    {
        var w=role.Selected==2?CreatedCampaign.Create(name):role.Selected==1?RetainerStart.Create(name):PersonalStart.Create(name);
        var template=role.Selected==0?StartTemplate.Ronin:StartTemplate.Retainer;var policy=role.Selected==2?CreatedCampaign.Policy:PersonalStart.Policy;
        var choice=StartConfiguration.Describe(w,w.PlayerControl,template,PersonOrigin.Created,policy);
        return new(choice,w,policy);
    }
    static void Validate(SavedStartProfile profile)
    {
        if(profile==null||profile.Package==null||profile.Package.World==null||profile.Package.Selection==null||profile.Package.Policy==null)throw new InvalidDataException("開始データに必要な項目がありません");
        if(!Guid.TryParseExact(profile.Instance,"N",out _)||(!PersonalStart.Enabled(profile.Package.World)&&!RetainerStart.Enabled(profile.Package.World)&&!CreatedCampaign.Enabled(profile.Package.World))||
            !StartConfiguration.Check(profile.Package.World,profile.Package.Selection,profile.Package.Policy).Eligible)
            throw new InvalidDataException("開始データの整合を確認できません");
        var isCampaign=CreatedCampaign.Enabled(profile.Package.World);var isRetainer=RetainerStart.Enabled(profile.Package.World)||isCampaign;
        var personName=profile.Package.World.Definitions.Characters.Single(c=>c.Id==PersonalStart.Person).Name;
        var original=isCampaign?CreatedCampaign.Create(personName,profile.Package.World.Seed,profile.Package.World.CommandMandates!.Single().Until):isRetainer?RetainerStart.Create(personName,profile.Package.World.Seed):PersonalStart.Create(personName,profile.Package.World.Seed);
        if(profile.Package.Selection.Template!=(isRetainer?StartTemplate.Retainer:StartTemplate.Ronin))throw new InvalidDataException("開始時の立場が一致しません");
        var policy=isCampaign?CreatedCampaign.Policy:PersonalStart.Policy;
        if(profile.Package.Selection.Mode!=HistoryMode.SemiHistorical||ContractJson.CanonicalHash(original)!=profile.Package.Selection.WorldHash||ContractJson.CanonicalHash(profile.Package.Policy)!=ContractJson.CanonicalHash(policy))throw new InvalidDataException("この開始定義には対応していません");
    }
    static SavedStartProfile ReadProfile(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,System.IO.FileAccess.Read,FileShare.Read);
        if(stream.Length is <1 or >4194304)throw new InvalidDataException("開始データの容量が不正です");
        var bytes=new byte[(int)stream.Length];stream.ReadExactly(bytes);var profile=ContractJson.Read<SavedStartProfile>(bytes);Validate(profile);return profile;
    }
    void RequestNew()
    {
        try{
            var profile=new SavedStartProfile(Guid.NewGuid().ToString("N"),NewPackage(nameInput.Text));Validate(profile);
            pending=(profile,Path.Combine(Root,profile.Package.Selection.WorldHash,profile.Instance),false);
            if(stage!=null)confirmation.PopupCentered();else OpenPending();
        }catch(Exception e)when(e is InvalidDataException or ArgumentException or IOException or UnauthorizedAccessException or ContractLoadException){message.Text="開始できません："+e.Message;}
    }
    void RequestLoad()
    {
        if(saved.Selected<0||saved.Selected>=profiles.Count)return;var p=profiles[saved.Selected];pending=(p.Profile,p.Directory,true);
        if(stage!=null)confirmation.PopupCentered();else OpenPending();
    }
    void OpenPending()
    {
        if(pending is not {} next)return;ContinuousWorldTrial? fresh=null;
        try{
            Validate(next.Profile);if(next.Load){var savedProfile=ReadProfile(Path.Combine(next.Directory,"start-profile.json"));if(ContractJson.CanonicalHash(savedProfile)!=ContractJson.CanonicalHash(next.Profile))throw new InvalidDataException("開始データが選択後に変わっています");}if(!next.Load){Directory.CreateDirectory(next.Directory);using var stream=new FileStream(Path.Combine(next.Directory,"start-profile.json"),FileMode.CreateNew,System.IO.FileAccess.Write,FileShare.None);var bytes=ContractJson.Write(next.Profile);stream.Write(bytes);stream.Flush(true);}
            fresh=GD.Load<PackedScene>("res://Scenes/PersonalStartTrial.tscn").Instantiate<ContinuousWorldTrial>();
            fresh.StartDefinition=ContractJson.Copy(next.Profile.Package.World);fresh.StartSaveRoot=Path.Combine(next.Directory,"game");fresh.SizeFlagsVertical=SizeFlags.ExpandFill;
            play.AddChild(fresh);if(next.Load)fresh.HubLoad("manual");
            if(stage!=null){stage.ProcessMode=ProcessModeEnum.Disabled;play.RemoveChild(stage);stage.QueueFree();}
            stage=fresh;CurrentDirectory=next.Directory;pending=null;role.Select(CreatedCampaign.Enabled(next.Profile.Package.World)?2:next.Profile.Package.Selection.Template==StartTemplate.Retainer?1:0);RefreshIntro();nameInput.Text=next.Profile.Package.World.Definitions.Characters.Single(c=>c.Id==PersonalStart.Person).Name;ShowGame();
        }catch(Exception e)when(e is InvalidDataException or ArgumentException or IOException or UnauthorizedAccessException or ContractLoadException){if(fresh!=null){play.RemoveChild(fresh);fresh.QueueFree();}message.Text="開けません："+e.Message;pending=null;}
    }
    void ShowGame(){if(stage==null)return;form.Visible=false;play.Visible=true;stage.ProcessMode=ProcessModeEnum.Inherit;}
    void ShowForm(){if(stage!=null)stage.ProcessMode=ProcessModeEnum.Disabled;play.Visible=false;form.Visible=true;resume.Visible=stage!=null;ScanProfiles();}
    void ScanProfiles()
    {
        profiles.Clear();saved.Clear();int unreadable=0;
        string[] Dirs(string path){try{return Directory.GetDirectories(path);}catch(DirectoryNotFoundException){return [];}catch(Exception e)when(e is IOException or UnauthorizedAccessException){unreadable++;return [];}}
        foreach(var hash in Dirs(Root).Where(p=>HashDirectory(Path.GetFileName(p),64)))
            foreach(var dir in Dirs(hash).Where(p=>HashDirectory(Path.GetFileName(p),32))){
                try{
                    var file=Path.Combine(dir,"start-profile.json");if(!File.Exists(file))continue;
                    var profile=ReadProfile(file);
                    if(profile.Instance!=Path.GetFileName(dir)||profile.Package.Selection.WorldHash!=Path.GetFileName(hash))throw new InvalidDataException();
                    var store=new GenerationSaveStore(Path.Combine(dir,"game"),profile.Package.World.Definitions);
                    // An initial definition alone is not saved progress. Do not present it as resumable.
                    if(!Directory.Exists(Path.Combine(dir,"game","manual")))continue;
                    var progress=store.Prepare("manual");
                    profiles.Add((profile,dir));saved.AddItem(profile.Package.World.Definitions.Characters.Single(c=>c.Id==PersonalStart.Person).Name+" / 経過"+progress.Seconds+"秒 / 開始"+File.GetLastWriteTime(file).ToString("MM/dd HH:mm"));
                }catch(Exception e)when(e is InvalidDataException or ArgumentException or IOException or UnauthorizedAccessException or ContractLoadException){unreadable++;}
            }
        saved.Disabled=load.Disabled=profiles.Count==0;message.Text=unreadable>0?"読めない開始データがあります。ファイルは保持しています。":profiles.Count==0?"保存した進行はまだありません。":"保存した人物を選んで再開できます。";
    }
}
