using Godot;
using System;
public partial class NewGameRuntime
{
    internal Func<long,long>? AdvanceTargetTestHook {get;set;}
    void ChangeAdvanceSpeed(int value)
    {
        if(IsSaving||IsClockControlling||value==speed)return;
        if(value is not(1 or 10 or 60 or 360))throw new ArgumentOutOfRangeException(nameof(value));
        CancelAdvancePrefetch();advanceClockActive=false;speed=value;
    }
    internal void ReviewSelectSpeed(int value)
    {
        int index=Array.IndexOf(new[]{1,10,60,360},value);
        if(index<0)throw new ArgumentOutOfRangeException(nameof(value));
        speedChoice.Select(index);speedChoice.EmitSignal(OptionButton.SignalName.ItemSelected,index);
    }
    internal int ReviewSpeed=>speed;
}
