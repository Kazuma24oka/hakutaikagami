using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
// S11：帳面（書状・人物・判断・約束・家督・軍勢）と、会話の差し替え口・文字の大きさ・キーでの送り。
public partial class NewGameRuntime
{
    PopupPanel? ledgerPopup;TabContainer? ledgerTabs;string? letterCategory;EntityId? bookPerson;
    internal static int TextSize=Math.Clamp(LoadTextSize(),16,30);
    static int LoadTextSize(){var c=new ConfigFile();return c.Load("user://hakutai-ui.cfg")==Error.Ok?(int)c.GetValue("ui","text_size",21):21;}
    static void SaveTextSize(){var c=new ConfigFile();c.SetValue("ui","text_size",TextSize);c.Save("user://hakutai-ui.cfg");}
    void BuildLedger(HFlowContainer bar){if(Screens2.On(Snapshot()))Add(bar,"ledger","帳面",()=>ShowLedger(0));}
    internal void ShowLedger(int tab)
    {
        if(ledgerPopup!=null){ledgerPopup.Hide();RemoveChild(ledgerPopup);ledgerPopup.QueueFree();ledgerPopup=null;}var w=Snapshot();long revision=Session.Revision;
        var popup=new PopupPanel{Exclusive=true};AddChild(popup);ledgerPopup=popup;
        var panel=new PanelContainer{CustomMinimumSize=new(760,520)};panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",12));popup.AddChild(panel);
        var tabs=new TabContainer();panel.AddChild(tabs);ledgerTabs=tabs;
        VBoxContainer Page(string title){var scroll=new ScrollContainer{Name=title,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};tabs.AddChild(scroll);var box=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};box.AddThemeConstantOverride("separation",6);scroll.AddChild(box);return box;}
        Label Text(VBoxContainer box,string text,string? color=null){var l=new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart};l.AddThemeFontSizeOverride("font_size",TextSize-4);if(color!=null)l.AddThemeColorOverride("font_color",new Color(color));box.AddChild(l);return l;}
        void Act(Container row,string label,bool disabled,Func<CommandResult> run,int reopen)
        {
            var b=new Button{Text=label,Disabled=disabled};row.AddChild(b);
            b.Pressed+=()=>{var r=run();message.Text=r.VisibleReason;Refresh();ShowLedger(reopen);};
        }
        string When(long at)=>WorldCalendar.Display(w.EpochDate,at);

        // 書状
        var letters=Page("書状");var filter=new OptionButton();filter.AddItem("すべて");foreach(var c in Screens2.Categories)filter.AddItem(c);letters.AddChild(filter);
        filter.Select(letterCategory==null?0:Array.IndexOf(Screens2.Categories,letterCategory)+1);
        filter.ItemSelected+=i=>{letterCategory=i==0?null:Screens2.Categories[i-1];ShowLedger(0);};
        foreach(var r in Session.Letters(letterCategory).Take(60))
        {
            string state=r.Incoming?(r.Opened?"開封":"未開封"):r.Failed?"不達":"送った";
            Text(letters,$"{When(r.At)}　{r.Category}・{r.Kind}　{(r.Incoming?r.Sender+" より":r.Recipient+" へ")}　{state}",r.Incoming&&!r.Opened?"efce92":r.Failed?"e08a7a":null);
            if(r.Body!=null)Text(letters,r.Body,"a8b7be");
        }

        // 人物
        var people=Page("人物");var who=new OptionButton();people.AddChild(who);
        var known=new List<EntityId>{w.PlayerControl};known.AddRange(Session.CorpsCommanders());
        if(Screens2.HouseOf(w,w.PlayerControl) is {} house)known.AddRange(FamilyBirth.People(w).Where(p=>p.Faction==house).Select(p=>new EntityId(p.Id)).Where(p=>!known.Contains(p)).Take(30));
        foreach(var p in known)who.AddItem(PersonName(p));var shown=bookPerson is {} bp&&known.Contains(bp)?bp:w.PlayerControl;who.Select(known.IndexOf(shown));
        who.ItemSelected+=i=>{bookPerson=known[(int)i];ShowLedger(1);};
        if(Session.PersonBook(shown) is {} book)
        {
            Text(people,(book.Title.Length>0?book.Title+"　":"")+book.Name,"efce92");
            Text(people,string.Join("　",book.Abilities.Select(a=>a.Name+" "+(a.Value?.ToString()??a.Confidence))));
            foreach(var f in book.Fields)Text(people,f.Field+"　"+string.Join("　",f.Skills.Select(s=>s.Name+" "+(s.Value?.ToString()??s.Confidence))));
            if(book.Origins.Length>0)Text(people,"出自　"+string.Join("・",book.Origins));
            foreach(var g in book.Growth.Take(6))Text(people,When(g.At)+"　"+g.Text,"a8b7be");
            if(book.Health is {} h)foreach(var c in h.Conditions)Text(people,$"{c.Part}の{c.Kind}　{c.Severity}　{c.Outlook}",c.Severity is "重傷" or "危篤"?"e08a7a":null);
            if(book.Care.Any(c=>!c.Can))Text(people,string.Join("　",book.Care.Select(c=>c.Action+(c.Can?"":"×"+c.Reason))));
            // 傷病のある本人は、頼まれた務めを返還・代理・延期で返上できる（一覧の Finish2）。
            if(book.Own&&Finish2.On(w)&&book.Health?.Conditions.Length>0)
                foreach(var t in w.Tasks.Where(t=>t.Executor==w.PlayerControl&&t.Issuer!=w.PlayerControl&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed)))
                {
                    var row=new HBoxContainer();people.AddChild(row);row.AddChild(new Label{Text=Screens2.Address(w,w.PlayerControl,t.Issuer)+"の務め"});
                    foreach(var kind in Finish2.ReliefKinds)Act(row,kind,false,()=>Session.RequestRelief(t.Id,kind,Session.Revision),1);
                }
            if(book.Education is {} e)
            {
                Text(people,e.Plan);if(e.Teacher!=null||e.Learning!=null)Text(people,"師 "+(e.Teacher??"なし")+"　"+(e.Learning??""));
                if(e.Interrupted!=null)Text(people,"中断　"+e.Interrupted,"e08a7a");foreach(var x in e.Reports.TakeLast(3))Text(people,x,"a8b7be");if(e.Reflection!=null)Text(people,e.Reflection);
            }
        }

        // 判断
        var judge=Page("判断");
        foreach(var c in Session.Crises())
        {
            Text(judge,c.Kind+"　"+c.What+(c.Warning.Length>0?"　"+c.Warning:""),"efce92");
            foreach(var o in c.Options)Text(judge,string.Join("　",new[]{o.Name,o.Preparation,o.Time,o.Cost,o.Basis,o.Uncertainty}.Where(x=>x.Length>0)));
        }
        foreach(var o in Session.HistoryOpportunities())
        {
            var row=new HBoxContainer();judge.AddChild(row);row.AddChild(new Label{Text=Screens2.KindName(Screens2.HistoryKind)});
            Act(row,"応じる",false,()=>Session.AnswerHistoryOpportunity(o.Letter,true,Session.Revision),2);Act(row,"見送る",false,()=>Session.AnswerHistoryOpportunity(o.Letter,false,Session.Revision),2);
        }

        // 約束
        var promises=Page("約束");var (housePromises,privatePromises)=Session.Promises();
        Text(promises,"家の約束","efce92");foreach(var p in housePromises)Text(promises,$"{p.Kind}　{p.With}　{p.Terms}　{(p.Until is long u?When(u)+"まで":"")}　{p.State}");
        Text(promises,"私の約束","efce92");foreach(var p in privatePromises)Text(promises,$"{p.Kind}　{p.With}　{p.Terms}　{(p.Until is long u?When(u)+"まで":"")}　{p.State}");

        // 家督
        var heirs=Page("家督");
        if(Session.SuccessionBook() is {} sb)
        {
            Text(heirs,sb.HouseName+"　当主 "+sb.HeadName+(sb.Guardian!=null?"　後見 "+sb.Guardian:""),"efce92");
            foreach(var c in sb.Candidates)
            {
                var row=new HBoxContainer();heirs.AddChild(row);
                row.AddChild(new Label{Text=$"{c.Name}　{(c.Age is int a?a+"歳":"")}{(c.Adopted?"　養子":"")}{(c.ComingOfAgeYear is int y?"　元服 "+y+"年":"")}{(c.Designated?"　後継":"")}"});
                if(sb.ViewerIsHead&&!c.Designated)Act(row,"後継に指名",false,()=>Session.DesignateHeir(sb.House,c.Person,Session.Revision),4);
            }
            if(sb.CanRetire){var row=new HBoxContainer();heirs.AddChild(row);var b=new Button{Text="隠居"};row.AddChild(b);b.Pressed+=()=>{popup.Hide();ShowRetire();};}
        }

        // 軍勢
        var armies=Page("軍勢");
        foreach(var a in Session.HouseArmies())
        {
            var row=new HBoxContainer();armies.AddChild(row);
            row.AddChild(new Label{Text=$"{a.Line}　{a.Leader}　{(a.Soldiers>0?"兵 約"+a.Soldiers+"　":"")}{a.Objective}　{(a.Priority is int p?"優先 "+p+"　":"")}{a.State}"});
            if(a.State=="命じた")Act(row,"取り消す",false,()=>Session.CancelCorpsOrder(new(a.Id),Session.Revision),5);
        }
        var provinces=Session.CorpsProvinces();var commanders=Session.CorpsCommanders();
        if(provinces.Length>0&&commanders.Length>0)
        {
            var form=new HBoxContainer();armies.AddChild(form);var province=new OptionButton();foreach(var p in provinces)province.AddItem(p);form.AddChild(province);
            var leader=new OptionButton();foreach(var c in commanders)leader.AddItem(PersonName(c));form.AddChild(leader);
            var priority=new SpinBox{MinValue=1,MaxValue=9,Value=1,Prefix="優先"};form.AddChild(priority);
            Act(form,"出陣を命じる",false,()=>Session.OrderCorps(provinces[province.Selected],commanders[leader.Selected],(int)priority.Value,Session.Revision),5);
        }

        // 文字の大きさ
        var view=Page("表示");var size=new HBoxContainer();view.AddChild(size);size.AddChild(new Label{Text="文字"});
        var spin=new SpinBox{MinValue=16,MaxValue=30,Value=TextSize};size.AddChild(spin);spin.ValueChanged+=v=>{TextSize=(int)v;SaveTextSize();workSpeech?.AddThemeFontSizeOverride("font_size",TextSize);};

        tabs.CurrentTab=Math.Clamp(tab,0,tabs.GetTabCount()-1);popup.PopupCentered();
    }

    // ---- 拠点（城・町）の形と名前 ----
    // 近くの画面に、見えている範囲の拠点を簡単な形（台と屋根）と名前で描く。絵の束ができたら差し替える。
    readonly System.Collections.Generic.Dictionary<string,Node3D> siteMarkers=new();
    void RefreshSiteMarkers(ContractWorld w,FieldPoint center)
    {
        foreach(var m in siteMarkers.Values)m.Visible=false;
        if(nationMode)return;double reach=Span*1.6*1000;
        foreach(var n in BranchRoutes.GraphNodes(w))
        {
            var fp=FieldWorld.FromWorld(n.Position);double dx=fp.X-center.X,dy=fp.Y-center.Y;if(dx*dx+dy*dy>reach*reach)continue;
            if(!siteMarkers.TryGetValue(n.Id,out var marker))
            {
                marker=new Node3D();stage.AddChild(marker);siteMarkers[n.Id]=marker;string name=NewGameWorld.SiteName(RegionalCampaign.Site(n.Id).Value);bool castle=name.EndsWith("城");
                var stone=new StandardMaterial3D{AlbedoColor=new Color(castle?"cfc6b4":"b89a6e")};var roof=new StandardMaterial3D{AlbedoColor=new Color(castle?"3d4650":"6b4a32")};
                marker.AddChild(new MeshInstance3D{Mesh=new BoxMesh{Size=castle?new Vector3(5,2.4f,5):new Vector3(4,1.6f,3)},MaterialOverride=stone,Position=new Vector3(5,castle?1.2f:.8f,-8)});
                marker.AddChild(new MeshInstance3D{Mesh=new PrismMesh{Size=castle?new Vector3(5.6f,2,5.6f):new Vector3(4.4f,1.4f,3.4f)},MaterialOverride=roof,Position=new Vector3(5,castle?3.4f:2.3f,-8)});
                if(castle)marker.AddChild(new MeshInstance3D{Mesh=new BoxMesh{Size=new Vector3(2.4f,1.6f,2.4f)},MaterialOverride=stone,Position=new Vector3(5,5.2f,-8)});
                marker.AddChild(new Label3D{Text=name,Position=new Vector3(5,castle?8:5,-8),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=40,PixelSize=.03f,Modulate=new Color("f2dfb4"),OutlineSize=8,NoDepthTest=true});
            }
            WorldArmyProjection.Apply(marker,fp,center);marker.Visible=true;
        }
    }

    // ---- 重い欄の描き直しの間引き ----
    // 自動進行の間は、右の欄・城の印などの重い描き直しを3秒に一度にする。止まっている間や操作の後は毎回描き直す。
    bool heavyUi=true;readonly System.Diagnostics.Stopwatch heavyClock=System.Diagnostics.Stopwatch.StartNew();long heavySeconds=-1;
    bool HeavyUiDue()
    {
        bool running=automatic&&!Session.Paused;
        if(running&&heavySeconds>=0&&heavyClock.Elapsed.TotalSeconds<3)return false;
        heavyClock.Restart();heavySeconds=Session.Seconds;return true;
    }

    // ---- 会話の差し替え口 ----
    // 立ち絵と背景は、絵の束に名前の合う絵があればそれを使い、無ければ今の仮の絵（3Dの武将・ドット絵）のまま。
    TextureRect?[] portraitSlots=[];
    void ApplyStage(EntityId other,string title)
    {
        if(!Screens2.On(Snapshot())||portraitPanels.Length==0)return;
        if(portraitSlots.Length!=portraitPanels.Length)portraitSlots=new TextureRect?[portraitPanels.Length];
        var ids=new[]{Session.PlayerControl,other};
        for(int n=0;n<portraitPanels.Length;n++)
        {
            var stage=Screens2.StageFor(ids[n],title);
            portraitSlots[n]??=new TextureRect{ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,CustomMinimumSize=new(88,100)};
            if(portraitSlots[n]!.GetParent()==null)portraitPanels[n].AddChild(portraitSlots[n]);
            bool has=ResourceLoader.Exists(stage.Portrait);portraitSlots[n]!.Visible=has;if(has)portraitSlots[n]!.Texture=GD.Load<Texture2D>(stage.Portrait);
        }
        var back=Screens2.StageFor(other,title).Background;
        if(ResourceLoader.Exists(back))workOverlay.AddThemeStyleboxOverride("panel",new StyleBoxTexture{Texture=GD.Load<Texture2D>(back),ContentMarginLeft=12,ContentMarginRight=12,ContentMarginTop=12,ContentMarginBottom=12});
        else workOverlay.AddThemeStyleboxOverride("panel",WorkBox("17252f","a28b61",12));
        workSpeech.AddThemeFontSizeOverride("font_size",TextSize);
    }
    // 動画用：本人に一番近いほかの拠点を行き先にして出発する。
    internal bool ReviewDepartNearest()
    {
        var me=Branch(Session.PlayerControl).Position;var pick=destinationNodes.Where(n=>n.Position!=me).OrderBy(n=>Math.Pow(n.Position.EastMm-me.EastMm,2)+Math.Pow(n.Position.NorthMm-me.NorthMm,2)).FirstOrDefault();
        if(pick==null)return false;SetDestination(pick.Id);if(!ButtonEnabled("depart"))return false;Press("depart");return true;
    }
    internal string StagePortrait(int n)=>n<portraitSlots.Length&&portraitSlots[n]?.Visible==true?"絵":"仮";
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if(e is not InputEventKey{Pressed:true,Echo:false} k||workOverlay?.Visible!=true)return;
        if(k.Keycode is Key.Enter or Key.KpEnter or Key.Space){AdvanceBottomTalk();GetViewport().SetInputAsHandled();}
        else if(k.Keycode==Key.Escape){CloseBottomTalk();GetViewport().SetInputAsHandled();}
    }
    // 検査用：帳面の文字と、押す。
    internal string ReviewLedgerText()=>ledgerPopup==null?"":string.Join("\n",Walk(ledgerPopup).OfType<Label>().Select(l=>l.Text));
    internal void ReviewLedgerPress(string label)
    {
        var b=Walk(ledgerPopup!).OfType<Button>().FirstOrDefault(b=>b.Text==label&&b.IsVisibleInTree())??throw new System.IO.InvalidDataException(label+" のボタンがありません");
        if(b.Disabled)throw new System.IO.InvalidDataException(label+" が押せません");b.EmitSignal(BaseButton.SignalName.Pressed);
    }
    internal int ReviewLedgerTab{get=>ledgerTabs?.CurrentTab??-1;set{if(ledgerTabs!=null)ledgerTabs.CurrentTab=value;}}
    internal void ReviewTalkKey(Key key)=>_UnhandledKeyInput(new InputEventKey{Keycode=key,Pressed=true});
    internal bool ReviewTalkOpen=>workOverlay?.Visible==true;
    internal void ReviewPresent(EntityId other)=>PresentWork("書状","帳面の確かめ","",other,other,"相談","返答");
}
