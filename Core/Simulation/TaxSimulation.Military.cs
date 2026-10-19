using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Simulation;

public sealed partial class TaxSimulation
{
    public bool HasFormationMap=>state.Officers?.Military?.Rules==MilitaryFlow.FormationRules;
    public FormationPlan[] FormationPlansFor(string observer)=>SnapshotCodec.Copy((state.Officers?.Military?.FormationPlans??[]).Where(p=>p.IssuerId==observer).ToArray());
    public void StartFormationMap()=>Mutate(s=>
    {
        if(s.Officers is null||s.Officers.Military is not null||s.Officers.Battle is not null)throw new InvalidOperationException("既存の演習を保持しています。初期化後に布陣演習を配置してください。");
        s.Officers.Military=MilitaryFlow.CreateFormation(s.World,s.Tick);MilitaryFlow.Observe(s.Officers.Military,s.World.Characters.Select(c=>c.Id),s.Tick);
    });
    public void SendFormation(string id,string issuer,MapCell start,MapCell end,int[] delays)=>Mutate(s=>MilitaryFlow.DispatchFormation(s.Officers?.Military??throw new InvalidOperationException(),s.Messaging,id,issuer,start,end,delays,s.Tick));
    public bool HasMilitaryMap=>state.Officers?.Military is not null;
    public bool HasLegacyField=>state.Officers?.Battle is not null;
    public HakutaiKagami.Core.Appearance.OfficerAppearance AppearanceFor(string id)=>SnapshotCodec.Copy(state.Officers?.Looks[id]??throw new InvalidOperationException("No officer appearance binding."));
    // Explicit diagnostic snapshot. Renderers must use MilitaryViewFor instead for battlefield data.
    public MilitaryState? MilitaryDiagnostic=>state.Officers?.Military is null?null:SnapshotCodec.Copy(state.Officers.Military);
    public MilitaryView MilitaryViewFor(string observer)
    {
        if(!state.World.Characters.Any(c=>c.Id==observer))throw new ArgumentException("Unknown observer.");
        return MilitaryFlow.View(state.Officers?.Military??throw new InvalidOperationException("No military map."),state.Messaging,observer,state.Tick);
    }
    public void StartMilitaryMap()=>Mutate(s=>
    {
        if(s.Officers is null||s.Officers.Military is not null||s.Officers.Battle is not null)throw new InvalidOperationException("Reset the session before starting another field scenario.");
        s.Officers.Military=MilitaryFlow.Create(s.World,s.Tick);MilitaryFlow.Observe(s.Officers.Military,s.World.Characters.Select(c=>c.Id),s.Tick);
    });
    public void SendMilitary(string id,string issuer,string unit,MilitaryAction action,MapCell destination,int delay=1,long? executeBy=null,long? effectiveAt=null,string? cancels=null)=>Mutate(s=>
        MilitaryFlow.Dispatch(s.Officers?.Military??throw new InvalidOperationException("No military map."),s.Messaging,id,issuer,unit,action,destination,s.Tick,delay,executeBy,effectiveAt,cancels));
    public void RetryMilitary(string id,string issuer,int delay=1)=>Mutate(s=>
    {
        var c=s.Officers?.Military??throw new InvalidOperationException();var o=c.Orders.Single(o=>o.Id==id);var u=c.Units.Single(u=>u.Id==o.UnitId);
        if(o.Status is not (MilitaryOrderStatus.InTransit or MilitaryOrderStatus.WaitingEffect)||!MilitaryFlow.Permits(c,issuer,u,o.Action,s.Tick))throw new UnauthorizedAccessException("Military retry is unavailable for this actor/order.");
        MessageFlow.Retry(s.Messaging,MilitaryFlow.OrderMessage(id),issuer,s.Tick,delay);
    });
    public void RetryMilitaryResult(string id,string responder,int delay=1)=>Mutate(s=>
    {
        var o=s.Officers?.Military?.Orders.Single(o=>o.Id==id)??throw new InvalidOperationException();
        if(o.RecipientId!=responder||o.ResponseBody is null)throw new UnauthorizedAccessException("No resolved military response owned by this responder.");
        MessageFlow.Retry(s.Messaging,MilitaryFlow.ReplyMessage(id),responder,s.Tick,delay);
    });
    public void RetryMilitaryIntel(string id,string sender,int delay=1)=>Mutate(s=>
    {
        var r=s.Officers?.Military?.Reports.Single(r=>r.Id==id)??throw new InvalidOperationException();if(r.SenderId!=sender)throw new UnauthorizedAccessException();
        MessageFlow.Retry(s.Messaging,id+"/military-intel",sender,s.Tick,delay);
    });
    public void RevokeMilitaryGrant(string id)=>Mutate(s=>{var c=s.Officers?.Military??throw new InvalidOperationException();if(!c.Grants.Any(g=>g.Id==id))throw new ArgumentException();c.Grants=c.Grants.Where(g=>g.Id!=id).ToArray();});
    public void SetMilitaryWillingness(string unit,bool willing)=>Mutate(s=>
    {var c=s.Officers?.Military??throw new InvalidOperationException();if(!c.Units.Any(u=>u.Id==unit))throw new ArgumentException();c.Units=c.Units.Select(u=>u.Id==unit?u with{WillObey=willing}:u).ToArray();});
    public void ReassignMilitaryCommander(string unitId,string commander,string armyCommander)=>Mutate(s=>
    {
        var c=s.Officers?.Military??throw new InvalidOperationException();var u=c.Units.Single(u=>u.Id==unitId);
        if(!s.World.Characters.Any(x=>x.Id==commander)||c.Armies.Single(a=>a.Id==u.ArmyId).CommanderId!=armyCommander)throw new UnauthorizedAccessException();
        c.Units=c.Units.Select(x=>x.Id==unitId?x with{CommanderId=commander}:x).ToArray();
        // Appointment never invents or transfers command grants or political membership.
    });
    public void SendMilitaryIntel(string id,string sender,string recipient,string sourceIntel,int delay=1)=>Mutate(s=>
        MilitaryFlow.SendIntel(s.Officers?.Military??throw new InvalidOperationException(),s.Messaging,id,sender,recipient,sourceIntel,s.Tick,delay));
    public void SendKnownMilitaryIntel(string id,string sender,string recipient,string unit,int delay=1)=>Mutate(s=>
    {
        var c=s.Officers?.Military??throw new InvalidOperationException();var known=MilitaryFlow.LatestIntel(c,sender,unit)??throw new UnauthorizedAccessException("No learned battlefield observation.");
        MilitaryFlow.SendIntel(c,s.Messaging,id,sender,recipient,known.Id,s.Tick,delay);
    });
}
