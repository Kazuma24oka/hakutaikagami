using Godot;
using System;
using System.IO;

public partial class ImagawaStartCheck : Node
{
    public override async void _Ready()
    {
        try
        {
            foreach (var fromMap in new[] { false, true })
            {
                var entry = new NewGameFrontEnd();
                AddChild(entry);
                await entry.CheckImagawaStart(Path.Combine(Path.GetTempPath(),
                    "imagawa-" + Guid.NewGuid().ToString("N")), fromMap);
                entry.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            GD.Print("IMAGAWA_START_PASS");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PrintErr(error);
            GetTree().Quit(1);
        }
    }
}
