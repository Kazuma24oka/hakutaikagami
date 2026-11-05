using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    internal Func<long,long>? AdvanceTargetTestHook {get;set;}
    internal Action<double>? TerrainBuildTestHook {get;set;}
    void ChangeAdvanceSpeed(int value)
    {
        if(IsSaving||IsClockControlling||value==speed)return;
        if(!WorldTimeControl.Speeds.Contains(value))throw new ArgumentOutOfRangeException(nameof(value));
        CancelAdvancePrefetch();advanceClockActive=false;speed=value;
    }
    internal void ReviewSelectSpeed(int value)
    {
        int index=Array.IndexOf(WorldTimeControl.Speeds,value);
        if(index<0)throw new ArgumentOutOfRangeException(nameof(value));
        speedChoice.Select(index);speedChoice.EmitSignal(OptionButton.SignalName.ItemSelected,index);
    }
    internal int ReviewSpeed=>speed;
}
