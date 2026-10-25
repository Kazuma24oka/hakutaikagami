namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    internal void MusterNpcArmy(EntityId id,EntityId commander,EntityId owner,NpcArmyObjective objective)
    {lock(gate){Change(w=>NpcArmies.Muster(w,id,commander,owner,objective));}}
    internal void ReturnNpcArmy(EntityId muster)
    {lock(gate){Change(w=>NpcArmies.Return(w,muster));}}
}
