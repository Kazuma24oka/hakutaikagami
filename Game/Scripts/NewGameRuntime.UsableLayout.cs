using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    TabContainer operationTabs=null!;
    string? restoredOperationPanel;
    string OperationPanel=>operationTabs==null?restoredOperationPanel??"peace":operationTabs.CurrentTab switch{1=>"military",2=>"mail",_=>"peace"};
    void RestoreOperationPanel(string? panel){restoredOperationPanel=panel;if(operationTabs!=null)operationTabs.CurrentTab=panel switch{"military"=>1,"mail"=>2,"peace"=>0,_=>Snapshot().NewMilitary==null?0:1};}
    NearbyActor[]? cachedNearby;
    readonly Dictionary<Control,int> operationPanels=new();
    NearbyActor[] NearbyDisplay(){Snapshot();return cachedNearby??=RegionalCampaign.Nearby(cachedWorld!,Session.PlayerControl);}
    void BuildUsableLayout(VBoxContainer root,HFlowContainer toolbar)
    {
        var children=root.GetChildren().OfType<Control>().ToArray();
        var warning=children.OfType<Label>().Single(l=>l.Text.StartsWith("地形・城郭は既存"));
        var militaryRows=children.Where(c=>c!=toolbar&&c!=serviceBar&&c!=info&&c!=message&&c!=surface&&c!=overview&&c!=tasks&&c!=warning).ToArray();
        var content=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill,SizeFlagsHorizontal=SizeFlags.ExpandFill};root.AddChild(content);root.MoveChild(content,root.GetChildCount()-2);
        var world=new VBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill,SizeFlagsHorizontal=SizeFlags.ExpandFill};content.AddChild(world);surface.Reparent(world);overview.Reparent(world);
        operationTabs=new(){CustomMinimumSize=new(400,0),SizeFlagsVertical=SizeFlags.ExpandFill};content.AddChild(operationTabs);
        VBoxContainer Panel(string name){var scroll=new ScrollContainer{Name=name,SizeFlagsVertical=SizeFlags.ExpandFill,SizeFlagsHorizontal=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};operationTabs.AddChild(scroll);var col=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(col);return col;}
        var peace=Panel("移動・仕事");var military=Panel("軍務");var mail=Panel("書状・仕官");
        void Move(Control control,VBoxContainer target,int index){control.Reparent(target);control.SizeFlagsHorizontal=SizeFlags.ExpandFill;operationPanels[control]=index;}
        peace.AddChild(new Label{Text="本人の行先と現地での判断",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        Move(destination,peace,0);foreach(var key in new[]{"depart","arrive","inspect","work","accept","decline"})Move(buttons[key],peace,0);
        military.AddChild(new Label{Text="許可・引受・配置・返報",AutowrapMode=TextServer.AutowrapMode.WordSmart});foreach(var row in militaryRows)Move(row,military,1);
        mail.AddChild(new Label{Text="本人に届いた書状",AutowrapMode=TextServer.AutowrapMode.WordSmart});Move(unreadChoice,mail,2);Move(buttons["open"],mail,2);Move(tasks,mail,2);Move(serviceBar,mail,2);
        void FitOptions(Node node){foreach(var child in node.GetChildren()){if(child is OptionButton option){option.FitToLongestItem=false;option.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;option.CustomMinimumSize=new(Math.Min(350,option.CustomMinimumSize.X),option.CustomMinimumSize.Y);}FitOptions(child);}}
        FitOptions(operationTabs);
        message.AutowrapMode=TextServer.AutowrapMode.WordSmart;info.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        foreach(var b in buttons.Values)b.MouseFilter=MouseFilterEnum.Stop;
        operationTabs.CurrentTab=restoredOperationPanel switch{"military"=>1,"mail"=>2,"peace"=>0,_=>Snapshot().NewMilitary==null?0:1};
    }
    void ShowOperation(Control control)
    {
        if(operationTabs==null)return;
        for(Node? n=control;n!=null;n=n.GetParent())if(n is Control c&&operationPanels.TryGetValue(c,out int index)){operationTabs.CurrentTab=index;return;}
    }
    void ResolveWorldLabels()
    {
        // Text positions only: never move a unit, its marker or its world anchor.
        // Sorting and integer pixels keep restore images deterministic.
        var placed=new List<Rect2>();
        foreach(var pair in worldTextLabels.Where(p=>p.Value.Visible).OrderBy(p=>p.Value.Position.Y).ThenBy(p=>p.Key,StringComparer.Ordinal))
        {
            var label=pair.Value;var size=label.GetCombinedMinimumSize();var p=label.Position;p.X=Mathf.Clamp(p.X,4,Math.Max(4,view.Size.X-size.X-4));p.Y=Mathf.Clamp(p.Y,4,Math.Max(4,view.Size.Y-size.Y-4));
            for(int attempt=0;attempt<placed.Count+1;attempt++){var rect=new Rect2(p,size+new Vector2(4,4));var hit=placed.Where(r=>r.Intersects(rect)).ToArray();if(hit.Length==0)break;p.Y=Math.Max(4,hit.Min(r=>r.Position.Y)-size.Y-5);}
            label.Position=new(Mathf.Round(p.X),Mathf.Round(p.Y));placed.Add(new(label.Position,size+new Vector2(4,4)));
        }
    }
}
