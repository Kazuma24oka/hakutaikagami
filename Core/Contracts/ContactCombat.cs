namespace HakutaiKagami.Core.Contracts;

// Pure seconds-domain calculation. Callers own permission, observation, movement,
// application of the returned effects, and the distinction between troops and people.
public sealed record ContactParticipant(EntityId Unit,int Side,FieldPoint Position,long Height,
    int Soldiers,int Morale,string Phase);
public static class ContactCombat
{
    public const long RadiusMm=4500,HeightToleranceMm=3000;
    static bool Near(FieldPoint a,FieldPoint b){var x=(System.Numerics.BigInteger)a.X-b.X;var y=(System.Numerics.BigInteger)a.Y-b.Y;return x*x+y*y<=RadiusMm*RadiusMm;}
    public static RegionalLoss[] Resolve(long at,bool hostile,IReadOnlyList<ContactParticipant> units)
    {
        if(at<0||units.Any(u=>!EntityId.IsValid(u.Unit.Value)||u.Position==null||u.Soldiers<0||u.Morale is <0 or >100||string.IsNullOrEmpty(u.Phase))||units.Select(u=>u.Unit).Distinct().Count()!=units.Count)
            throw new InvalidDataException("Invalid simultaneous contact snapshot");
        if(!hostile)return [];
        var losses=new List<RegionalLoss>();
        foreach(var unit in units.Where(u=>u.Soldiers>0))
        {
            // Every contribution reads the same pre-effect snapshot. A unit lost
            // in this second still contributes this second; input order cannot decide victory.
            var enemies=units.Where(e=>e.Soldiers>0&&e.Side!=unit.Side&&e.Phase is not ("routed" or "withdrawing")
                &&Near(unit.Position,e.Position)
                &&System.Numerics.BigInteger.Abs((System.Numerics.BigInteger)unit.Height-e.Height)<=HeightToleranceMm).ToArray();
            if(enemies.Length==0)continue;
            long damage=enemies.Sum(e=>(long)Math.Max(1,e.Soldiers/10));
            losses.Add(new(at,unit.Unit,(int)Math.Min(unit.Soldiers,damage),(int)Math.Min(unit.Morale,(long)enemies.Length*8)));
        }
        return losses.ToArray();
    }
}
