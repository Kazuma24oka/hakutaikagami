using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class OfficerLibraryTrial : Control
{
    OfficerLibrary library=new(1,[],[]);OfficerLibraryStore store=null!;string? savedHash,editing,editingGroup;
    ItemList originals=null!,groups=null!,members=null!;LineEdit personName=null!,groupName=null!,factionName=null!;
    SpinBox birth=null!;readonly SpinBox[] attributes=new SpinBox[5];OptionButton role=null!,site=null!;Label status=null!,editorTitle=null!;
    Button apply=null!,create=null!,copy=null!,addGroup=null!,rename=null!;FileDialog import=null!,export=null!;
    internal bool Enhanced;internal string? StorageOverride;internal Action? Closed;
    string[] SiteIds=>Enhanced?NationalRoutes.Sites.Select(s=>s.Id.Value).ToArray():new[]{"castle:kiyosu","castle:kofu","castle:odawara"};
    string? TestKey=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--library-test-key="))?.Split('=')[1];
    string Output=>ProjectSettings.GlobalizePath("res://../Docs/Design/NewGameSetupV1/runtime-local/library");
    string Root=>StorageOverride??(TestKey is {} key?Path.Combine(Output,Guid.ParseExact(key,"N").ToString("N")):ProjectSettings.GlobalizePath("user://officer-library-v1"));
    SystemFont? ownedFont;
    public override void _ExitTree(){ownedFont?.Dispose();ownedFont=null;}
    public override void _Ready()
    {
        Theme=new Theme{DefaultFontSize=Enhanced?GetThemeDefaultFontSize():16,DefaultFont=ownedFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);foreach(var key in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+key,16);
        var root=new VBoxContainer();margin.AddChild(root);root.AddChild(new Label{Text="百代鏡 / 登録武将の管理"});
        root.AddChild(new Label{Text="開始とは独立した登録画面の試作です。ここでの編集は、開始済みの世界へ影響しません。",Modulate=new Color("e6c785")});
        var body=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};root.AddChild(body);
        var left=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsStretchRatio=6};body.AddChild(left);left.AddChild(new Label{Text="グループなし / 元の登録武将"});originals=new(){SizeFlagsVertical=SizeFlags.ExpandFill};left.AddChild(originals);originals.ItemSelected+=i=>Edit(library.Ungrouped[(int)i],null);
        var right=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsStretchRatio=4};body.AddChild(right);right.AddChild(new Label{Text="シナリオ別のグループ / 独立した複製"});
        groups=new(){CustomMinimumSize=new(0,80)};right.AddChild(groups);groups.ItemSelected+=_=>RefreshMembers();groupName=new(){PlaceholderText="グループ名",MaxLength=40};right.AddChild(groupName);
        var buttons=new HBoxContainer();right.AddChild(buttons);addGroup=Button(buttons,"グループ追加",AddGroup);rename=Button(buttons,"選択グループを改名",Rename);copy=Button(right,"左の武将を選択グループへ複製",Copy);
        members=new(){SizeFlagsVertical=SizeFlags.ExpandFill};right.AddChild(members);members.ItemSelected+=i=>{var g=CurrentGroup();if(g!=null)Edit(g.Officers[(int)i],g.Id);};
        root.AddChild(new HSeparator());editorTitle=new(){Text="新しい武将"};root.AddChild(editorTitle);
        var grid=new GridContainer{Columns=4};root.AddChild(grid);
        personName=Field(grid,"名前");birth=Number(grid,"生年",1,9999,1520);role=new();grid.AddChild(new Label{Text="立場"});grid.AddChild(role);foreach(var title in new[]{"浪人","家臣","陪臣","城主","国人領主","大名"})role.AddItem(title);
        site=new();grid.AddChild(new Label{Text="登場希望の場所"});grid.AddChild(site);foreach(var title in Enhanced?NationalRoutes.Sites.Select(s=>s.Name).ToArray():new[]{"清洲","甲府","小田原"})site.AddItem(title);
        string[] names={"統率","武勇","知略","政務","交渉"};for(int n=0;n<5;n++)attributes[n]=Number(grid,names[n],0,int.MaxValue,50);
        factionName=Field(grid,"大名の勢力名");root.AddChild(new Label{Text="所属・領土・主従は新規ゲームの配置時に確認します。",AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=SizeFlags.ExpandFill});
        if(Enhanced){root.GetChild<Control>(0).Visible=false;root.GetChild<Control>(1).Visible=false;originals.CustomMinimumSize=new(0,110);members.CustomMinimumSize=new(0,80);BuildEnhanced(grid,left);}
        var actions=new HBoxContainer();root.AddChild(actions);create=Button(actions,"新規入力",()=>{editing=editingGroup=null;if(Enhanced)ResetEnhanced();personName.Text="";factionName.Text="";editorTitle.Text="新しい武将";});apply=Button(actions,"登録して保存",Apply);Button(actions,"インポート",()=>import.PopupCenteredRatio());Button(actions,"書き出し",()=>export.PopupCenteredRatio());
        if(Closed!=null)Button(actions,"スタート画面へ",()=>Closed());
        status=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};root.AddChild(status);
        root.AddChild(new Label{Text=Enhanced?"登録は新規ゲームで選んだときだけ登場します。グループの編集は他の複製へ影響しません。":"外見・直属相手の指定と全拠点選択、ゲーム開始への接続は後続です。数値の製品上限は未確定です。",AutowrapMode=TextServer.AutowrapMode.WordSmart,Modulate=new Color("e6c785")});
        import=new(){FileMode=FileDialog.FileModeEnum.OpenFile,Access=FileDialog.AccessEnum.Filesystem,Filters=new[]{"*.json ; 武将ライブラリ"},UseNativeDialog=false};AddChild(import);
        import.FileSelected+=path=>Safe(()=>Persist(OfficerLibraryStore.Import(library,ContractJson.Read<OfficerLibrary>(File.ReadAllBytes(path)))));
        export=new(){FileMode=FileDialog.FileModeEnum.SaveFile,Access=FileDialog.AccessEnum.Filesystem,Filters=new[]{"*.json ; 武将ライブラリ"},UseNativeDialog=false};AddChild(export);
        export.FileSelected+=path=>Safe(()=>{using var stream=new FileStream(path,FileMode.CreateNew,System.IO.FileAccess.Write,FileShare.None);stream.Write(ContractJson.Write(library));stream.Flush(true);status.Text="書き出しました。既存ファイルは上書きしません。";});
        store=new(Root);try{library=store.Load();savedHash=ContractJson.CanonicalHash(library);status.Text="登録データを読み込みました。";}catch(FileNotFoundException){status.Text="登録武将はまだありません。";}catch(DirectoryNotFoundException){status.Text="登録武将はまだありません。";}catch(Exception e){status.Text="読めないデータを保持しています："+e.Message;apply.Disabled=addGroup.Disabled=copy.Disabled=rename.Disabled=true;}
        Refresh();if(Enhanced)RestoreEnhanced(null);if(TestKey!=null)CallDeferred(nameof(RunChecks));
    }
    Button Button(Node parent,string text,Action action){var b=new Button{Text=text};parent.AddChild(b);b.Pressed+=()=>Safe(action);return b;}
    LineEdit Field(Node parent,string title){parent.AddChild(new Label{Text=title});var v=new LineEdit{MaxLength=40,SizeFlagsHorizontal=SizeFlags.ExpandFill};parent.AddChild(v);return v;}
    SpinBox Number(Node parent,string title,double min,double max,double value){parent.AddChild(new Label{Text=title});var v=new SpinBox{MinValue=min,MaxValue=max,Value=value,Step=1};parent.AddChild(v);return v;}
    void Safe(Action action){try{action();}catch(Exception e)when(e is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or ContractLoadException){status.Text="保存・編集できません："+e.Message;}}
    OfficerGroup? CurrentGroup(){var selected=groups.GetSelectedItems();return selected.Length==1?library.Groups[selected[0]]:null;}
    void Refresh(){string? previous=CurrentGroup()?.Id;originals.Clear();foreach(var p in library.Ungrouped)originals.AddItem(p.Name);groups.Clear();foreach(var g in library.Groups)groups.AddItem(g.Name+"（"+g.Officers.Length+"人）");int at=Array.FindIndex(library.Groups,g=>g.Id==previous);if(at>=0)groups.Select(at);else if(library.Groups.Length>0)groups.Select(0);RefreshMembers();}
    void RefreshMembers(){members.Clear();var g=CurrentGroup();if(g!=null)foreach(var p in g.Officers)members.AddItem(p.Name);}
    void Edit(RegisteredOfficer p,string? group){editing=p.Id;editingGroup=group;personName.Text=p.Name;birth.Value=p.BirthYear;role.Select((int)p.Role);site.Select(Array.IndexOf(SiteIds,Enhanced?KnownSiteForEdit(p.Site):p.Site));factionName.Text=p.FactionName??"";int[] values={p.Attributes.Leadership,p.Attributes.Valor,p.Attributes.Intelligence,p.Attributes.Politics,p.Attributes.Negotiation??50};for(int n=0;n<5;n++)attributes[n].Value=values[n];if(Enhanced)RestoreEnhanced(p);editorTitle.Text=group==null?"元の登録武将を編集":"グループ内の複製を編集（元の登録は変わりません）";}
    void Persist(OfficerLibrary next){string hash=store.Save(next,savedHash);library=next;savedHash=hash;Refresh();status.Text="保存しました。開始済みの世界と他の複製は変更しません。";}
    void Apply()
    {
        if(string.IsNullOrWhiteSpace(personName.Text))throw new InvalidDataException("名前を入力してください");
        if(site.Selected<0)throw new InvalidDataException("場所を選んでください");
        var next=ContractJson.Copy(library);var old=editing==null?null:editingGroup==null?next.Ungrouped.Single(p=>p.Id==editing):next.Groups.Single(g=>g.Id==editingGroup).Officers.Single(p=>p.Id==editing);
        var p=new RegisteredOfficer(editing??"created:"+Guid.NewGuid().ToString("N"),old?.CopiedFrom,personName.Text.Trim(),(int)birth.Value,new((int)attributes[0].Value,(int)attributes[1].Value,(int)attributes[2].Value,(int)attributes[3].Value){Negotiation=old?.Attributes.Negotiation==null&&old!=null&&(int)attributes[4].Value==50?null:(int)attributes[4].Value},(StartTemplate)role.Selected,SiteIds[site.Selected],old?.Lord,string.IsNullOrWhiteSpace(factionName.Text)?null:factionName.Text.Trim(),old?.Look);
        if(editingGroup==null)next=next with{Ungrouped=old==null?next.Ungrouped.Append(p).ToArray():next.Ungrouped.Select(x=>x.Id==p.Id?p:x).ToArray()};else next=next with{Groups=next.Groups.Select(g=>g.Id==editingGroup?g with{Officers=g.Officers.Select(x=>x.Id==p.Id?p:x).ToArray()}:g).ToArray()};
        if(Enhanced){p=p with{Lord=EnhancedLord(),Look=ContractJson.Copy(currentLook)};if(editingGroup==null)next=next with{Ungrouped=next.Ungrouped.Select(x=>x.Id==p.Id?p:x).ToArray()};else next=next with{Groups=next.Groups.Select(g=>g.Id==editingGroup?g with{Officers=g.Officers.Select(x=>x.Id==p.Id?p:x).ToArray()}:g).ToArray()};}
        Persist(next);editing=p.Id;
    }
    void AddGroup(){if(string.IsNullOrWhiteSpace(groupName.Text))throw new InvalidDataException("グループ名を入力してください");Persist(library with{Groups=library.Groups.Append(new("group:"+Guid.NewGuid().ToString("N"),groupName.Text.Trim(),[])).ToArray()});}
    void Rename(){var g=CurrentGroup()??throw new InvalidDataException("グループを選んでください");if(string.IsNullOrWhiteSpace(groupName.Text))throw new InvalidDataException("グループ名を入力してください");Persist(library with{Groups=library.Groups.Select(x=>x.Id==g.Id?x with{Name=groupName.Text.Trim()}:x).ToArray()});}
    void Copy(){var indices=originals.GetSelectedItems();var g=CurrentGroup();if(indices.Length!=1||g==null)throw new InvalidDataException("元の武将と複製先グループを選んでください");Persist(NewGameSetup.CopyToGroup(library,library.Ungrouped[indices[0]].Id,g.Id,"created:"+Guid.NewGuid().ToString("N")));}
    async Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    void Check(bool yes,string text){if(!yes)throw new Exception(text);GD.Print("PASS "+text);}
    void Press(Button b)=>b.EmitSignal(Godot.Button.SignalName.Pressed);
    async void RunChecks()
    {
        try{
            Directory.CreateDirectory(Output);await Frame();bool restore=OS.GetCmdlineUserArgs().Contains("--library-restore");
            if(restore){Check(library.Ungrouped.Length==1&&library.Groups.Length==1,"saved definitions reopened in another process");Check(library.Ungrouped[0].Name=="鏡山 宗久"&&library.Groups[0].Officers[0].Name=="九州の宗久","group edit did not change original across restart");groups.Select(0);RefreshMembers();members.Select(0);members.EmitSignal(ItemList.SignalName.ItemSelected,0);}
            else{
                Press(create);personName.Text="鏡山 宗久";Press(apply);Check(library.Ungrouped.Length==1,"actual register/save button stores officer");
                groupName.Text="九州の浪人衆";Press(addGroup);originals.Select(0);Press(copy);Check(library.Groups[0].Officers.Length==1,"actual copy button creates independent group member");
                members.Select(0);members.EmitSignal(ItemList.SignalName.ItemSelected,0);personName.Text="九州の宗久";attributes[0].Value=80;Press(apply);Check(library.Ungrouped[0].Name=="鏡山 宗久"&&library.Ungrouped[0].Attributes.Leadership==50,"group edit keeps original name and attributes");
                string before=ContractJson.CanonicalHash(library);personName.Text="";Press(apply);Check(before==ContractJson.CanonicalHash(library)&&ContractJson.CanonicalHash(store.Load())==before,"invalid input keeps memory and saved library");personName.Text="九州の宗久";
            }
            await Frame();Check(originals.GetGlobalRect().Size.X>groups.GetGlobalRect().Size.X,"originals occupy larger left pane");GetViewport().GetTexture().GetImage().SavePng(Path.Combine(Output,restore?"restored.png":"edited.png"));
            File.WriteAllText(Path.Combine(Output,restore?"restored.sha256":"edited.sha256"),ContractJson.CanonicalHash(library));
            GD.Print("OFFICER_LIBRARY_PASSED");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
