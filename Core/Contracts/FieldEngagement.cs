namespace HakutaiKagami.Core.Contracts;

public enum EngagementIntent { Hold, Mission, SelfDefense, DeliberateUnauthorizedAttack }
public enum EngagementBasis { None, Mission, ImmediateDefense, UnauthorizedAttack }
// Authoritative, already validated world facts, not a player-command payload or a grant creator.
// The world adapter must verify issuer authority and the original order/receipt/cancellation.
public sealed record EngagementMandate(EntityId Original,EntityId Principal,EntityId Agent,EntityId Owner,
    EntityId Army,EntityId TargetOwner,EntityId[] Units,long IssuedAt,long? ReceivedAt,long EffectiveFrom,
    long Until,long? CancelledAt);
public sealed record HostilityInterval(EntityId FromOwner,EntityId ToOwner,long From,long Until);
public sealed record EngagementUnit(UnitState Unit,FieldPoint Position,long Height,int Morale,string Phase,
    EngagementIntent Intent,bool WillFight,EntityId? Target=null);
public sealed record EngagementStrike(EntityId Attacker,EntityId Target,EngagementBasis Basis,EntityId? Original);
public sealed record EngagementResult(long At,EngagementStrike[] Strikes,RegionalLoss[] Losses);

public static class FieldEngagement
{
    public const string Contract="received-scoped-mission-directed-hostility-immediate-defense-explicit-unauthorized-contact-v1";
    static bool Near(EngagementUnit a,EngagementUnit b)
    {
        System.Numerics.BigInteger x=(System.Numerics.BigInteger)a.Position.X-b.Position.X,y=(System.Numerics.BigInteger)a.Position.Y-b.Position.Y;
        return x*x+y*y<=(System.Numerics.BigInteger)ContactCombat.RadiusMm*ContactCombat.RadiusMm&&System.Numerics.BigInteger.Abs((System.Numerics.BigInteger)a.Height-b.Height)<=ContactCombat.HeightToleranceMm;
    }
    static bool CanAct(EngagementUnit u)=>u.WillFight&&u.Unit.Soldiers>0&&u.Phase is not ("routed" or "withdrawing");
    static bool Valid(EngagementMandate m,EngagementUnit a,EngagementUnit b,long at)=>m.Agent==a.Unit.Commander&&m.Owner==a.Unit.Organization&&m.Army==a.Unit.Army&&m.TargetOwner==b.Unit.Organization&&m.Units.Contains(a.Unit.Id)&&m.IssuedAt<=at&&m.ReceivedAt is {} received&&received<=at&&m.EffectiveFrom<=at&&at<m.Until&&(m.CancelledAt==null||at<m.CancelledAt);
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool value,string message){if(!value)throw new InvalidDataException(message);}
    public static EngagementResult Resolve(long at,IReadOnlyList<EngagementUnit> units,IReadOnlyList<EngagementMandate> mandates,IReadOnlyList<HostilityInterval> hostilities)
    {
        Need(at>=0&&units!=null&&mandates!=null&&hostilities!=null,"交戦入力が不正です");
        Need(units!.All(x=>x!=null&&x.Unit!=null&&x.Position!=null&&EntityId.IsValid(x.Unit.Id.Value)&&EntityId.IsValid(x.Unit.Army.Value)&&EntityId.IsValid(x.Unit.Organization.Value)&&EntityId.IsValid(x.Unit.Commander.Value)&&x.Unit.Soldiers>=0&&x.Unit.Soldiers<=int.MaxValue&&x.Morale is >=0 and <=100&&!string.IsNullOrWhiteSpace(x.Phase)&&Enum.IsDefined(x.Intent)&&(x.Target==null||EntityId.IsValid(x.Target.Value.Value)))&&units.Select(x=>x.Unit.Id).Distinct().Count()==units.Count,"部隊スナップショットが不正です");
        Need(mandates!.All(m=>m!=null&&EntityId.IsValid(m.Original.Value)&&EntityId.IsValid(m.Principal.Value)&&EntityId.IsValid(m.Agent.Value)&&EntityId.IsValid(m.Owner.Value)&&EntityId.IsValid(m.Army.Value)&&EntityId.IsValid(m.TargetOwner.Value)&&m.Owner!=m.TargetOwner&&m.Units!=null&&m.Units.Length>0&&m.Units.All(i=>EntityId.IsValid(i.Value))&&m.Units.Distinct().Count()==m.Units.Length&&m.IssuedAt>=0&&m.EffectiveFrom>=m.IssuedAt&&m.Until>m.EffectiveFrom&&(m.ReceivedAt==null||m.ReceivedAt>=m.IssuedAt)&&(m.CancelledAt==null||m.CancelledAt>=m.IssuedAt))&&mandates.Select(m=>m.Original).Distinct().Count()==mandates.Count,"命令原本・時点・範囲が不正です");
        Need(hostilities!.All(h=>h!=null&&EntityId.IsValid(h.FromOwner.Value)&&EntityId.IsValid(h.ToOwner.Value)&&h.FromOwner!=h.ToOwner&&h.From>=0&&h.Until>h.From),"敵対関係の時点が不正です");
        var strikes=new List<EngagementStrike>();
        // First establish actual incoming attacks. A nearby hostile unit alone is not an attack.
        foreach(var a in units.OrderBy(u=>u.Unit.Id.Value,StringComparer.Ordinal).Where(CanAct))foreach(var b in units.OrderBy(u=>u.Unit.Id.Value,StringComparer.Ordinal)){
            if(a.Unit.Id==b.Unit.Id||a.Unit.Organization==b.Unit.Organization||b.Unit.Soldiers==0||(a.Target is {} target&&target!=b.Unit.Id)||!Near(a,b))continue;
            if(a.Intent==EngagementIntent.DeliberateUnauthorizedAttack&&a.Target==b.Unit.Id){strikes.Add(new(a.Unit.Id,b.Unit.Id,EngagementBasis.UnauthorizedAttack,null));continue;}
            if(a.Intent!=EngagementIntent.Mission||!hostilities.Any(h=>h.FromOwner==a.Unit.Organization&&h.ToOwner==b.Unit.Organization&&h.From<=at&&at<h.Until))continue;
            var order=mandates.Where(m=>Valid(m,a,b,at)).OrderByDescending(m=>m.IssuedAt).ThenBy(m=>m.Original.Value,StringComparer.Ordinal).FirstOrDefault();
            if(order!=null)strikes.Add(new(a.Unit.Id,b.Unit.Id,EngagementBasis.Mission,order.Original));
        }
        var incoming=strikes.ToArray();
        foreach(var a in units.Where(u=>CanAct(u)&&u.Intent==EngagementIntent.SelfDefense))foreach(var hit in incoming.Where(s=>s.Target==a.Unit.Id))strikes.Add(new(a.Unit.Id,hit.Attacker,EngagementBasis.ImmediateDefense,null));
        // Read all contributions from the same pre-effect state. Neither input order nor animation decides a hit.
        var losses=new List<RegionalLoss>();
        foreach(var target in units.OrderBy(u=>u.Unit.Id.Value,StringComparer.Ordinal)){
            var attackers=strikes.Where(s=>s.Target==target.Unit.Id).Select(s=>s.Attacker).Distinct().ToArray();if(attackers.Length==0)continue;
            var participants=new List<ContactParticipant>{new(target.Unit.Id,0,target.Position,target.Height,(int)target.Unit.Soldiers,target.Morale,target.Phase)};
            participants.AddRange(attackers.Select(id=>{var a=units.Single(u=>u.Unit.Id==id);return new ContactParticipant(id,1,a.Position,a.Height,(int)a.Unit.Soldiers,a.Morale,a.Phase);}));
            losses.Add(ContactCombat.Resolve(at,true,participants).Single(l=>l.Unit==target.Unit.Id));
        }
        return new(at,strikes.OrderBy(s=>s.Attacker.Value,StringComparer.Ordinal).ThenBy(s=>s.Target.Value,StringComparer.Ordinal).ToArray(),losses.ToArray());
    }
}
