using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
public partial class NewGameRuntime
{
    AcceptDialog? verbDialog;
    readonly Dictionary<string,List<Control>> verbControls=new();
    internal bool ReviewNotebookHasGoals=>verbControls.GetValueOrDefault("notebook")?.Contains(buttons["personal-life"])==true;
    string VerbFor(string id)=>id is "merit-rewards" or "renown-world" or "chronicle" or "play-guide" or "personal-life" or "salary" or "history"||id.Contains("goal")?"notebook":id.Contains("delegate")||id.Contains("steward")||id.Contains("convoy")||id.Contains("freight")?"entrust":id.Contains("mail")||id.Contains("treaty")||id.Contains("truce")||id.Contains("inquir")?"write":id is "household-court" or "information-schemes" or "social-assets" or "social-world" or "temple-world"||id.Contains("talk")||id.Contains("private")||id.Contains("marriage")||id.Contains("adoption")||id.Contains("companion")||id.Contains("village")||id.Contains("career")?"meet":"list";
    void BuildVerbNotebook(VBoxContainer root,HFlowContainer toolbar)
    {
        var keep=new[]{"menu","ten","auto","pause","rotate","near","nation","save","load","time-next","time-settings","history-summary"}.ToHashSet();
        var storage=new VBoxContainer{Visible=false};root.AddChild(storage);
        foreach(var p in buttons.ToArray())if(p.Value.GetParent()==toolbar&&!keep.Contains(p.Key)){
            string group=VerbFor(p.Key);if(!verbControls.ContainsKey(group))verbControls[group]=new();verbControls[group].Add(p.Value);p.Value.Reparent(storage);
        }
        var dock=new HFlowContainer();root.AddChild(dock);
        void Open(string key,string title){RequestClockPause(true);verbDialog?.Hide();if(verbDialog!=null){foreach(var c in verbControls.Values.SelectMany(x=>x))if(verbDialog.IsAncestorOf(c))c.Reparent(storage);verbDialog.QueueFree();}verbDialog=new(){Title=title,Exclusive=false};AddChild(verbDialog);var scroll=new ScrollContainer{CustomMinimumSize=new(600,360)};verbDialog.AddChild(scroll);var box=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(box);box.AddChild(new Label{Text=key=="meet"?"本人が知っている相手との現地相談。会えるかは各用件で確認します。":key=="write"?"書状の到着・開封・効力は別です。現在分かっている条件で判断します。":key=="entrust"?"任せられる範囲は、本人の権限と相手の同意で決まります。":"本人の目標と経緯。未確認の結果は確定情報として扱いません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});foreach(var c in verbControls.Values.SelectMany(x=>x).Where(c=>key=="list"||verbControls.GetValueOrDefault(key)?.Contains(c)==true).ToArray())c.Reparent(box);if(box.GetChildCount()==1)box.AddChild(new Label{Text="今この入口で使える用件はありません。予備の一覧と右側の操作も利用できます。"});verbDialog.PopupCentered(new(640,430));}
        Add(dock,"verb-meet","会う",()=>Open("meet","会う：現地で相談"));
        Add(dock,"verb-write","書く",()=>{operationTabs.CurrentTab=2;Open("write","書く：書状と交渉");});
        Add(dock,"verb-entrust","任せる",()=>Open("entrust","任せる：権限と担当"));
        Add(dock,"verb-move","動く",()=>{operationTabs.CurrentTab=0;verbDialog?.Hide();});
        Add(dock,"verb-council","評定",()=>{courtCategory=2;ShowHouseholdCourt();});
        Add(dock,"verb-notebook","手帳",()=>Open("notebook","手帳：目標と年代記"));
        Add(dock,"verb-list","…一覧",()=>Open("list","予備の操作一覧"));
    }
}
