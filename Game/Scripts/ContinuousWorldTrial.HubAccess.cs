using Godot;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    bool WorkHubCheck=>OS.GetCmdlineUserArgs().Any(a=>a.StartsWith("--work-hub-")||a.StartsWith("--start-selection-"));
    internal void HubSetAutomatic(bool value){automatic=value;}
    internal WorldSession HubSession=>S;
    internal string HubPose()=>NpcPose();
    internal void HubPress(string id)=>Click(id);
    internal void HubAdvance(long at)=>WorkAdvance(at);
    internal void HubSave(string slot)=>Save(slot);
    internal void HubLoad(string slot)=>Load(slot);
    internal float[] HubRect()=>new[]{0,workRole.GlobalPosition.Y,GetViewportRect().Size.X,GetViewportRect().Size.Y-workRole.GlobalPosition.Y};
    internal bool HubFits()=>details.GlobalPosition.Y+details.Size.Y<=GetViewportRect().Size.Y+1&&surface.Size.Y>=280;
}
