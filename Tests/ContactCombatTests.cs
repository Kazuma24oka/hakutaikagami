using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class ContactCombatTests
{
    static ContactParticipant Unit(string id,int side=0,long x=0,long height=0,int soldiers=40,int morale=100,string phase="standing")=>new(new("unit:"+id),side,new(x,0),height,soldiers,morale,phase);
    [Fact]public void PeaceDoesNotAuthorizeDamage()=>Assert.Empty(ContactCombat.Resolve(1,false,[Unit("a"),Unit("b",1)]));
    [Fact]public void AlliesDoNotDamageEachOther()=>Assert.Empty(ContactCombat.Resolve(1,true,[Unit("a"),Unit("b")]));
    [Theory][InlineData(4500,3000,true)][InlineData(4501,3000,false)][InlineData(4500,3001,false)][InlineData(0,-3000,true)][InlineData(0,-3001,false)]
    public void ContactBoundaries(long distance,long height,bool contact)=>Assert.Equal(contact,ContactCombat.Resolve(1,true,[Unit("a"),Unit("b",1,distance,height)]).Length==2);
    [Fact]public void MutualDestructionIsSimultaneous(){var hits=ContactCombat.Resolve(4,true,[Unit("a",soldiers:1),Unit("b",1,soldiers:1)]);Assert.Equal(2,hits.Length);Assert.All(hits,h=>{Assert.Equal(1,h.Soldiers);Assert.Equal(4,h.At);});}
    [Fact]public void PermutingSnapshotDoesNotChangeEffects(){ContactParticipant[] u=[Unit("a"),Unit("b",1),Unit("c",1,soldiers:100)];Assert.Equal(ContactCombat.Resolve(1,true,u).OrderBy(h=>h.Unit.Value),ContactCombat.Resolve(1,true,u.Reverse().ToArray()).OrderBy(h=>h.Unit.Value));}
    [Theory][InlineData("routed")][InlineData("withdrawing")]
    public void RetreatingUnitDoesNotAttackButCanBeHit(string phase){var hits=ContactCombat.Resolve(1,true,[Unit("a"),Unit("b",1,phase:phase)]);Assert.Single(hits);Assert.Equal(new EntityId("unit:b"),hits[0].Unit);}
    [Fact]public void ZeroStrengthCannotAttackOrReceiveDamage()=>Assert.Empty(ContactCombat.Resolve(1,true,[Unit("a"),Unit("b",1,soldiers:0)]));
    [Fact]public void LowMoraleDoesNotChangeSoldierCountOrLife(){var u=Unit("a",morale:3);var hit=ContactCombat.Resolve(1,true,[u,Unit("b",1)])[0];Assert.Equal(3,hit.Morale);Assert.Equal(4,hit.Soldiers);Assert.Equal(40,u.Soldiers);}
    [Fact]public void MultipleOpponentsContributeOnce(){var hit=ContactCombat.Resolve(1,true,[Unit("a"),Unit("b",1),Unit("c",1,soldiers:30)])[0];Assert.Equal(7,hit.Soldiers);Assert.Equal(16,hit.Morale);}
    [Fact]public void DamageIsCappedWithoutIntegerOverflow(){var hit=ContactCombat.Resolve(1,true,new[]{Unit("a",soldiers:12)}.Concat(Enumerable.Range(0,11).Select(i=>Unit("e"+i,1,soldiers:int.MaxValue))).ToArray())[0];Assert.Equal(12,hit.Soldiers);Assert.Equal(88,hit.Morale);}
    [Fact]public void ExtremeCoordinatesCannotWrapIntoContact()=>Assert.Empty(ContactCombat.Resolve(1,true,[Unit("a",x:long.MinValue),Unit("b",1,x:long.MaxValue)]));
    [Theory][InlineData(2700,3600,true)][InlineData(2701,3600,false)]public void CircularContactBoundary(long x,long y,bool near){var enemy=Unit("b",1) with{Position=new(x,y)};Assert.Equal(near,ContactCombat.Resolve(1,true,[Unit("a"),enemy]).Length==2);}
    [Fact]public void ExtremeHeightsCannotOverflowIntoContact()=>Assert.Empty(ContactCombat.Resolve(1,true,[Unit("a",height:long.MinValue),Unit("b",1,height:long.MaxValue)]));
    [Fact]public void SnapshotIsImmutableAndReproducible(){ContactParticipant[] u=[Unit("a"),Unit("b",1)];var first=ContactCombat.Resolve(2,true,u);Assert.Equal(first,ContactCombat.Resolve(2,true,u));Assert.Equal(40,u[0].Soldiers);}
    [Fact]public void ExistingFormulaMatchesAcrossSeededSnapshots(){var rng=new Random(20261022);for(int sample=0;sample<250;sample++){var units=Enumerable.Range(0,12).Select(i=>Unit("r"+i,i%3,rng.Next(-12000,12001),rng.Next(-4000,4001),rng.Next(0,101),rng.Next(0,101),i%5==0?"routed":"standing")).ToArray();var expected=new List<RegionalLoss>();foreach(var u in units.Where(u=>u.Soldiers>0)){var enemies=units.Where(v=>v.Soldiers>0&&v.Side!=u.Side&&v.Phase is not ("routed" or "withdrawing")&&RegionalGround.Distance(u.Position,v.Position)<=4500&&Math.Abs(u.Height-v.Height)<=3000).ToArray();if(enemies.Length>0)expected.Add(new(1,u.Unit,Math.Min(u.Soldiers,enemies.Sum(e=>Math.Max(1,e.Soldiers/10))),Math.Min(u.Morale,enemies.Length*8)));}Assert.Equal(expected,ContactCombat.Resolve(1,true,units));}}
    [Theory][InlineData("id")][InlineData("duplicate")][InlineData("soldiers")][InlineData("morale")][InlineData("phase")][InlineData("time")]
    public void InvalidSnapshotIsRejected(string kind){var a=Unit("a");var b=Unit("b",1);a=kind switch{"id"=>a with{Unit=default},"soldiers"=>a with{Soldiers=-1},"morale"=>a with{Morale=101},"phase"=>a with{Phase=""},_=>a};if(kind=="duplicate")b=b with{Unit=a.Unit};Assert.Throws<InvalidDataException>(()=>ContactCombat.Resolve(kind=="time"?-1:1,true,[a,b]));}
}
