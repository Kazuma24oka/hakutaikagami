using Godot;
using System;
public partial class NpcPurposeEntryCheck : Node
{
    public override void _Ready()
    {
        var entry=new NewGameFrontEnd();
        try{entry.VerifyPurposePreview();GetTree().Quit();}
        catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
        finally{entry.Free();}
    }
}
