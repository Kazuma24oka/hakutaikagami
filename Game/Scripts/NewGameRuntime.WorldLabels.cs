using Godot;
using System;
using System.Collections.Generic;
public partial class NewGameRuntime
{
    CanvasLayer? worldTextLayer;
    readonly SortedDictionary<string,Label> worldTextLabels=new(StringComparer.Ordinal);
    Label WorldLabel(string key,int size,Color? tint=null)
    {
        if(worldTextLayer==null){worldTextLayer=new();view.AddChild(worldTextLayer);}
        var label=new Label{MouseFilter=MouseFilterEnum.Ignore};label.AddThemeFontOverride("font",GetThemeDefaultFont());label.AddThemeFontSizeOverride("font_size",size);label.AddThemeColorOverride("font_color",tint??Colors.White);label.AddThemeColorOverride("font_outline_color",Colors.Black);label.AddThemeConstantOverride("outline_size",3);worldTextLayer.AddChild(label);worldTextLabels.Add(key,label);foreach(var item in worldTextLabels.Values)item.MoveToFront();return label;
    }
    // A restored Label3D produced a seven-pixel text-edge difference.
    // Render text at integer viewport pixels; keep the actual world anchor unchanged.
    void PlaceWorldLabel(Label label,Vector3 anchor){var p=camera.UnprojectPosition(anchor);label.Position=new(Mathf.Round(p.X-label.GetCombinedMinimumSize().X/2),Mathf.Round(p.Y));}
}
