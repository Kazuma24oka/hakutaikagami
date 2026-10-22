using Godot;
using System;
// Explicit process teardown; this never runs on menu/load/scene replacement.
// Native rendering must remain alive while managed resource handles are released.
public static class GameProcessExit
{
    static bool requested;
    public static async void Request(SceneTree tree,Node scene,int code=0)
    {
        if(requested)return;requested=true;
        try{
            scene.ProcessMode=Node.ProcessModeEnum.Disabled;scene.QueueFree();scene=null!;
            await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            Officer3DCustomActor.ReleaseProcessResources();SceneryVisualAssets.ReleaseProcessResources();
            for(int i=0;i<3;i++){GC.Collect();GC.WaitForPendingFinalizers();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);}
            GD.Print("GAME_PROCESS_EXIT_RELEASED");tree.Quit(code);
        }catch(Exception e){GD.PrintErr(e);tree.Quit(1);}
    }
}
