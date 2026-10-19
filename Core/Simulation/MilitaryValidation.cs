using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Simulation;

public static partial class MilitaryFlow
{
    public static void Validate(MilitaryState s,WorldSnapshot world,MessageState messages,long tick)
    {
        static void Check(bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
        var ids=world.Characters.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);var orgs=world.Definition.Organizations.Select(o=>o.Id).ToHashSet(StringComparer.Ordinal);
        bool formation=s.Rules==FormationRules;int unitCount=formation?3:2,armyCount=formation?1:2;
        Check(s.Schema==1&&s.MapId==MilitaryMap.Id&&(s.Rules==Scope||formation)&&s.StartedAt>=0&&s.StartedAt<=tick&&s.Tick==tick,"Military schema/map/common clock mismatch.");
        Check(s.Armies.Length==armyCount&&s.Armies.Select(a=>a.Id).Distinct().Count()==armyCount&&s.Armies.All(a=>!string.IsNullOrWhiteSpace(a.Id)&&orgs.Contains(a.OrganizationId)&&ids.Contains(a.CommanderId)&&MilitaryMap.Walkable(a.Exit)),"Invalid army/owner/commander/exit.");
        Check(s.Units.Length==unitCount&&s.Units.Select(u=>u.Id).Distinct().Count()==unitCount&&s.Units.Select(u=>u.CommanderId).Distinct().Count()==unitCount&&s.Units.Where(Live).Select(u=>u.Cell).Distinct().Count()==s.Units.Count(Live),"Invalid/overlapping military units or duplicate field commander.");
        Check(!formation||s.Hostilities.Length==0&&s.Losses.Length==0&&s.Orders.All(o=>o.Action is MilitaryAction.Move or MilitaryAction.Cancel),"Formation fixture has no combat rules.");
        Check(s.Hostilities.Length<=1,"Duplicate exercise hostility relation.");
        foreach(var h in s.Hostilities)Check(h.LeftArmy!=h.RightArmy&&s.Armies.Any(a=>a.Id==h.LeftArmy)&&s.Armies.Any(a=>a.Id==h.RightArmy)&&!string.IsNullOrWhiteSpace(h.Basis)&&h.ValidFrom>=0&&h.ValidUntil>h.ValidFrom,"Invalid explicit hostility/exercise relation.");
        foreach(var u in s.Units)
        {
            Check(s.Armies.Any(a=>a.Id==u.ArmyId)&&ids.Contains(u.CommanderId)&&!string.IsNullOrWhiteSpace(u.Id)&&Enum.IsDefined(u.Phase)&&MilitaryMap.Walkable(u.Cell)&&MilitaryMap.Walkable(u.Previous)&&u.Cell.Distance(u.Previous)<=1,"Broken unit references/position.");
            Check(u.InitialSoldiers is >0 and <=10000&&u.Soldiers>=0&&u.Soldiers<=u.InitialSoldiers&&u.InitialMorale is >=1 and <=100&&u.Morale is >=0 and <=100&&u.AttackPower is >0 and <=100&&u.MoralePressure is >0 and <=100&&u.TravelCells>=0&&u.MoveBudget>=0,"Invalid military quantities.");
            Check(u.Soldiers+s.Losses.Where(l=>l.UnitId==u.Id).Sum(l=>l.Soldiers)==u.InitialSoldiers&&u.Morale==Math.Max(0,u.InitialMorale-s.Losses.Where(l=>l.UnitId==u.Id).Sum(l=>l.Morale)),"Military casualty/morale ledger mismatch.");
            Check((u.Soldiers==0)==(u.Phase==MilitaryPhase.Incapacitated)&&u.ActionAt==s.Losses.Where(l=>l.UnitId==u.Id).Select(l=>(long?)l.Tick).Max(),"Military action/effect mismatch.");
            Check(u.Phase!=MilitaryPhase.Exited||u.Cell==s.Armies.Single(a=>a.Id==u.ArmyId).Exit,"Unit disappeared before retreat destination.");
            var previous=u.Cell;foreach(var step in u.Route){Check(MilitaryMap.Walkable(step)&&previous.Distance(step)==1,"Noncontiguous or impassable saved route.");previous=step;}
            Check(u.Route.Length<=MilitaryMap.Width*MilitaryMap.Height&&(u.Route.Length==0?u.MoveBudget==0:u.MoveBudget<MilitaryMap.Cost(u.Route[0])),"Invalid path progress.");
            Check(u.ActiveOrder is null||s.Orders.Any(o=>o.Id==u.ActiveOrder&&o.UnitId==u.Id&&o.AppliedAt!=null),"Unknown active order.");
            if(u.Route.Length>0)
            {
                var destination=Retreating(u)?s.Armies.Single(a=>a.Id==u.ArmyId).Exit:s.Orders.Single(o=>o.Id==u.ActiveOrder).Destination;
                var expected=Retreating(u)?MilitaryMap.Route(u.Cell,destination):CommandRoute(s,u,s.Orders.Single(o=>o.Id==u.ActiveOrder));
                Check(u.Route[^1]==destination&&!u.Route.Contains(u.Cell)&&u.Route.Distinct().Count()==u.Route.Length&&u.Route.Sum(MilitaryMap.Cost)==expected.Sum(MilitaryMap.Cost),"Saved path diverges from its active command/exit or has invalid cost.");
            }
        }
        Check(s.Grants.Select(g=>g.Id).Distinct().Count()==s.Grants.Length,"Duplicate military grant.");
        foreach(var g in s.Grants)Check(!string.IsNullOrWhiteSpace(g.Id)&&ids.Contains(g.ActorId)&&s.Units.Any(u=>u.Id==g.UnitId&&u.ArmyId==g.ArmyId)&&g.Jurisdiction==Scope&&!string.IsNullOrWhiteSpace(g.Basis)&&g.ValidFrom>=0&&g.ValidUntil>g.ValidFrom&&g.Actions.Length>0&&g.Actions.Distinct().Count()==g.Actions.Length&&g.Actions.All(Enum.IsDefined),"Invalid actor/unit/army/jurisdiction/basis/period/action grant.");
        Check(s.Orders.Length<=256&&s.Orders.Select(o=>o.Id).Distinct().Count()==s.Orders.Length&&s.Applied.Distinct().Count()==s.Applied.Length,"Duplicate/excessive military commands/effects.");
        foreach(var o in s.Orders)
        {
            Check(ids.Contains(o.IssuerId)&&ids.Contains(o.RecipientId)&&s.Units.Any(u=>u.Id==o.UnitId)&&Enum.IsDefined(o.Action)&&Enum.IsDefined(o.Status)&&o.SentAt>=s.StartedAt&&o.SentAt<=tick&&o.EffectiveAt>=o.SentAt&&(o.ExecuteBy is null||o.ExecuteBy>=o.SentAt)&&o.InitialDelay is >=1 and <=100,"Invalid military command identity/time.");
            var envelope=messages.Messages.SingleOrDefault(m=>m.Id==OrderMessage(o.Id));Check(envelope is not null&&envelope.Kind=="military-command"&&envelope.SenderId==o.IssuerId&&envelope.RecipientId==o.RecipientId&&envelope.Scope==Scope&&envelope.CreatedAt==o.SentAt&&envelope.DeliverBy==o.ExecuteBy,"Military/common transmission mismatch.");
            var sent=SnapshotCodec.Read<MilitaryOrder>(envelope!.Body);
            Check(sent==o with{Status=MilitaryOrderStatus.InTransit,ReceivedAt=null,AppliedAt=null,ResolvedAt=null,Reason="",ResponseBody=null},"Military logical payload was changed after dispatch.");
            Check(o.ReceivedAt is null||o.ReceivedAt==MessageFlow.LearnedAt(messages,OrderMessage(o.Id),o.RecipientId),"Effect before personal order recognition.");
            Check((o.ResolvedAt is null||o.ResolvedAt>=o.SentAt&&o.ResolvedAt<=tick)&&
                (o.Status!=MilitaryOrderStatus.InTransit||o.ReceivedAt is null&&o.ResolvedAt is null&&o.ResponseBody is null)&&
                (o.Status!=MilitaryOrderStatus.WaitingEffect||o.ReceivedAt!=null&&o.AppliedAt is null&&o.ResolvedAt is null)&&
                (o.Status!=MilitaryOrderStatus.Applied||o.AppliedAt!=null&&o.ResolvedAt!=null&&o.ResponseBody!=null)&&
                (o.Status!=MilitaryOrderStatus.Refused||o.ReceivedAt!=null&&o.ResolvedAt!=null&&o.ResponseBody!=null)&&
                (o.Status is not (MilitaryOrderStatus.Expired or MilitaryOrderStatus.Cancelled)||o.ResolvedAt!=null),"Military command lifecycle mismatch.");
            Check(s.Applied.Contains(o.Id)==(o.AppliedAt!=null)&&(o.AppliedAt is null||o.ReceivedAt<=o.AppliedAt&&o.AppliedAt>=o.EffectiveAt&&o.AppliedAt<=tick&&(o.ExecuteBy is null||o.AppliedAt<=o.ExecuteBy)),"Military effect/receipt/deadline mismatch.");
            if(o.ResponseBody is not null)Check(messages.Messages.Any(m=>m.Id==ReplyMessage(o.Id)&&m.Body==o.ResponseBody&&m.SenderId==o.RecipientId&&m.RecipientId==o.IssuerId)&&o.ReceivedAt!=null,"Military result reply provenance mismatch.");
            Check(o.Action==MilitaryAction.Cancel?s.Orders.Any(t=>t.Id==o.Cancels&&t.UnitId==o.UnitId&&t.IssuerId==o.IssuerId&&t.Action!=MilitaryAction.Cancel):o.Cancels is null&&MilitaryMap.Walkable(o.Destination),"Invalid military target/cancellation reference.");
        }
        Check(s.Losses.Select(l=>l.Id).Distinct().Count()==s.Losses.Length&&s.Losses.All(l=>l.Id==$"military/{l.Tick}/{l.UnitId}"&&s.Units.Any(u=>u.Id==l.UnitId)&&l.Tick>s.StartedAt&&l.Tick<=tick&&l.Soldiers>0&&l.Morale>0),"Invalid military effect ledger.");
        Check(s.Intel.Select(i=>i.Id).Distinct().Count()==s.Intel.Length,"Duplicate military observations.");
        foreach(var i in s.Intel)
        {
            Check(ids.Contains(i.ObserverId)&&ids.Contains(i.CommanderId)&&s.Armies.Any(a=>a.Id==i.ArmyId)&&s.Units.Any(u=>u.Id==i.UnitId)&&Enum.IsDefined(i.Kind)&&MilitaryMap.Walkable(i.Cell)&&i.ObservedAt>=s.StartedAt&&i.ObservedAt<=i.LearnedAt&&i.LearnedAt<=tick&&
                (i.Soldiers is null||i.Soldiers is >=0 and <=10000)&&(i.Morale is null||i.Morale is >=0 and <=100)&&(i.Phase is null||Enum.IsDefined(i.Phase.Value)),"Invalid military knowledge/time/facts.");
            if(i.Kind==IntelKind.Report)
            {
                var source=s.Intel.SingleOrDefault(x=>x.Id==i.SourceIntelId);Check(source is not null&&i.EvidenceMessageId!=null&&MessageFlow.Knows(messages,i.EvidenceMessageId,i.ObserverId)&&i.LearnedAt==MessageFlow.LearnedAt(messages,i.EvidenceMessageId,i.ObserverId)&&
                    i.ObservedAt==source.ObservedAt&&i.Cell==source.Cell&&i.Soldiers==source.Soldiers&&i.Morale==source.Morale&&i.Phase==source.Phase&&i.Present==source.Present&&i.ArmyId==source.ArmyId&&i.CommanderId==source.CommanderId,"Military report cannot invent or refresh source observations.");
            }
            else Check(i.EvidenceMessageId is null&&i.SourceIntelId is null&&i.ObservedAt==i.LearnedAt,"Direct observation has report provenance.");
        }
        Check(s.Reports.Select(r=>r.Id).Distinct().Count()==s.Reports.Length,"Duplicate battlefield reports.");
        foreach(var r in s.Reports)
        {
            var source=s.Intel.Single(i=>i.Id==r.SourceIntelId);Check(source.ObserverId==r.SenderId&&ids.Contains(r.RecipientId)&&messages.Messages.Any(m=>m.Id==r.Id+"/military-intel"&&m.Kind=="military-intel"&&m.SenderId==r.SenderId&&m.RecipientId==r.RecipientId&&m.Body==SnapshotCodec.Write(source)&&m.CreatedAt>=source.LearnedAt),"Battle report was not known by its sender.");
        }
        ValidateFormations(s,messages,tick);
    }
}
