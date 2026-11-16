using Godot;
using System.Collections.Generic;
using System.Linq;
using HakutaiKagami.Core.Contracts;
// 出陣で2番目・3番目の部隊を任せる家臣を選ぶ（PlayerUnitLeaders）。
public partial class NewGameRuntime
{
    void ShowUnitLeaders()
    {
        var w=Snapshot();var candidates=NewGameMilitary.UnitLeaderCandidates(w);if(candidates.Length==0)return;
        var popup=new PopupPanel{Exclusive=true};AddChild(popup);
        var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);
        var box=new VBoxContainer{CustomMinimumSize=new(360,0)};panel.AddChild(box);box.AddThemeConstantOverride("separation",8);
        var chosen=new List<EntityId>();var go=new Button{Text="出陣",Disabled=true};
        foreach(var p in candidates)
        {
            var check=new CheckBox{Text=PersonName(p)};box.AddChild(check);
            check.Toggled+=on=>{if(on){if(chosen.Count>=2){check.SetPressedNoSignal(false);return;}chosen.Add(p);}else chosen.Remove(p);go.Disabled=chosen.Count==0;};
        }
        var row=new HBoxContainer();box.AddChild(row);row.AddChild(go);
        go.Pressed+=()=>{long revision=Session.Revision;popup.Hide();Span=90;nationMode=false;Submit(Session.DecideNewMilitary(new(new("request:new-military-ui/"+revision),NewMilitaryAction.Deploy){Leaders=[..chosen]},Session.PlayerControl,revision));Refresh();};
        var close=new Button{Text="閉じる"};row.AddChild(close);close.Pressed+=()=>popup.Hide();
        popup.Size=new Vector2I(380,1);popup.PopupCentered(new Vector2I(380,1));
    }
}
