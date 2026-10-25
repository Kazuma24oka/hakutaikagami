using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class FieldEngagementTests
{
    static EngagementUnit A=>new(new(new("unit:a"),new("army:a"),new("person:a"),new("org:a"),40,0),new(0,0),0,100,"standing",EngagementIntent.Mission,true);
    static EngagementUnit B=>new(new(new("unit:b"),new("army:b"),new("person:b"),new("org:b"),30,0),new(3000,0),0,100,"standing",EngagementIntent.SelfDefense,true);
    static EngagementMandate Order=>new(new("order:attack"),new("person:head"),A.Unit.Commander,A.Unit.Organization,A.Unit.Army,B.Unit.Organization,[A.Unit.Id],0,2,2,10,null);
    static HostilityInterval Hostile=>new(A.Unit.Organization,B.Unit.Organization,0,20);
    static EngagementResult Run(long at=3,EngagementUnit? a=null,EngagementUnit? b=null,EngagementMandate? order=null,bool hostile=true)=>FieldEngagement.Resolve(at,[a??A,b??B],[order??Order],hostile?[Hostile]:[]);
    [Fact] public void NeutralityIsNotOverriddenByAnAttackOrder()=>Assert.Empty(Run(hostile:false).Losses);
    [Fact] public void HostilityWithoutReceivedMissionDoesNotCauseDamage()=>Assert.Empty(Run(order:Order with{ReceivedAt=null}).Losses);
    [Theory][InlineData(0)][InlineData(1)][InlineData(10)][InlineData(11)]public void OrderOnlyActsWithinReceivedEffectiveInterval(long at)=>Assert.Empty(Run(at).Losses);
    [Fact] public void CancellationTakesEffectAtItsExactWorldSecond(){Assert.NotEmpty(Run(4,order:Order with{CancelledAt=5}).Losses);Assert.Empty(Run(5,order:Order with{CancelledAt=5}).Losses);}
    [Fact] public void AuthorityCannotBeReusedAcrossArmyOwnerCommanderOrTarget(){foreach(var m in new[]{Order with{Army=new("army:other")},Order with{Owner=new("org:other")},Order with{Agent=new("person:other")},Order with{TargetOwner=new("org:other")},Order with{Units=[new("unit:other")]}})Assert.Empty(Run(order:m).Losses);}
    [Fact] public void WillingnessIsIndependentFromAnOtherwiseValidOrder()=>Assert.Empty(Run(a:A with{WillFight=false}).Losses);
    [Fact] public void SelfDefenseRequiresAnActualIncomingAttack()=>Assert.Empty(Run(a:A with{Intent=EngagementIntent.SelfDefense}).Losses);
    [Fact] public void ExplicitUnauthorizedAttackRemainsDistinctFromMissionAndDoesNotCreateWar(){var r=Run(a:A with{Intent=EngagementIntent.DeliberateUnauthorizedAttack,Target=B.Unit.Id},hostile:false);Assert.Contains(r.Strikes,s=>s.Basis==EngagementBasis.UnauthorizedAttack);Assert.Contains(r.Strikes,s=>s.Basis==EngagementBasis.ImmediateDefense);Assert.DoesNotContain(r.Strikes,s=>s.Basis==EngagementBasis.Mission);}
    [Theory][InlineData("withdrawing")][InlineData("routed")]public void RetreatingUnitDoesNotInitiateAttack(string phase)=>Assert.Empty(Run(a:A with{Phase=phase}).Losses);
    [Fact] public void ContactAndHeightArePhysicalConditions(){Assert.Empty(Run(b:B with{Position=new(5000,0)}).Losses);Assert.Empty(Run(b:B with{Height=4000}).Losses);}
    [Fact] public void SnapshotOrderCannotChooseWinner(){var a=FieldEngagement.Resolve(3,[A,B],[Order],[Hostile]);var b=FieldEngagement.Resolve(3,[B,A],[Order],[Hostile]);Assert.Equal(ContractJson.Write(a),ContractJson.Write(b));Assert.Equal(3,a.Losses.Single(x=>x.Unit==A.Unit.Id).Soldiers);Assert.Equal(4,a.Losses.Single(x=>x.Unit==B.Unit.Id).Soldiers);}
    [Fact] public void SameOwnerNeverBecomesAnEnemyThroughTheExplicitAttackChoice()=>Assert.Empty(Run(a:A with{Intent=EngagementIntent.DeliberateUnauthorizedAttack,Target=B.Unit.Id},b:B with{Unit=B.Unit with{Organization=A.Unit.Organization}}).Losses);
    [Fact] public void RestoredFactsProduceTheSameResult(){var units=ContractJson.Read<EngagementUnit[]>(ContractJson.Write(new[]{A,B}));var orders=ContractJson.Read<EngagementMandate[]>(ContractJson.Write(new[]{Order}));var h=ContractJson.Read<HostilityInterval[]>(ContractJson.Write(new[]{Hostile}));Assert.Equal(ContractJson.Write(Run()),ContractJson.Write(FieldEngagement.Resolve(3,units,orders,h)));}
    [Fact] public void RepeatingCalculationDoesNotMutateWorldFacts(){var u=new[]{A,B};var h=ContractJson.CanonicalHash(u);var first=FieldEngagement.Resolve(3,u,[Order],[Hostile]);Assert.Equal(ContractJson.Write(first),ContractJson.Write(FieldEngagement.Resolve(3,u,[Order],[Hostile])));Assert.Equal(h,ContractJson.CanonicalHash(u));}
    [Fact] public void UnauthorizedAttackRequiresOneExplicitTarget()
    {
        var c=B with{Unit=B.Unit with{Id=new("unit:c"),Army=new("army:c"),Organization=new("org:c")},Intent=EngagementIntent.Hold};
        var r=FieldEngagement.Resolve(3,[A with{Intent=EngagementIntent.DeliberateUnauthorizedAttack,Target=B.Unit.Id},B,c],[],[]);
        Assert.DoesNotContain(r.Strikes,s=>s.Target==c.Unit.Id);
        Assert.Empty(FieldEngagement.Resolve(3,[A with{Intent=EngagementIntent.DeliberateUnauthorizedAttack},B],[],[]).Strikes);
    }
    [Fact] public void ExpiredOrderDoesNotRemoveImmediateSelfDefense()
    {
        var r=FieldEngagement.Resolve(10,[A with{Intent=EngagementIntent.SelfDefense},B with{Intent=EngagementIntent.DeliberateUnauthorizedAttack,Target=A.Unit.Id}],[Order],[]);
        Assert.Contains(r.Strikes,s=>s.Attacker==A.Unit.Id&&s.Basis==EngagementBasis.ImmediateDefense&&s.Original==null);
    }
    [Fact] public void HostilityIsDirectedAndExpiresExactly()
    {
        Assert.Empty(FieldEngagement.Resolve(3,[A,B],[Order],[new(B.Unit.Organization,A.Unit.Organization,0,20)]).Strikes);
        Assert.Empty(FieldEngagement.Resolve(3,[A,B],[Order],[Hostile with{Until=3}]).Strikes);
    }
    [Fact] public void FutureReceiptAndFutureEffectiveTimeCannotBeUsedEarly()
    {
        Assert.Empty(Run(order:Order with{ReceivedAt=4}).Strikes);
        Assert.Empty(Run(order:Order with{EffectiveFrom=4}).Strikes);
    }
    [Fact] public void DuplicateOriginalCannotMultiplyDamage()
    {
        Assert.Throws<InvalidDataException>(()=>FieldEngagement.Resolve(3,[A,B],[Order,Order],[Hostile]));
        var r=FieldEngagement.Resolve(3,[A,B],[Order,Order with{Original=new("order:resend")}],[Hostile]);
        Assert.Equal(ContractJson.Write(Run()),ContractJson.Write(r));
    }
    [Theory][InlineData("duplicate-unit")][InlineData("soldiers")][InlineData("morale")][InlineData("intent")][InlineData("receipt")][InlineData("scope")][InlineData("time")]
    public void InvalidFactsCannotReachDamageCalculation(string kind)
    {
        var a=kind switch{"soldiers"=>A with{Unit=A.Unit with{Soldiers=long.MaxValue}},"morale"=>A with{Morale=-1},"intent"=>A with{Intent=(EngagementIntent)999},_=>A};
        var b=kind=="duplicate-unit"?B with{Unit=B.Unit with{Id=A.Unit.Id}}:B;
        var o=kind switch{"receipt"=>Order with{ReceivedAt=-1},"scope"=>Order with{Units=[]},_=>Order};
        Assert.Throws<InvalidDataException>(()=>FieldEngagement.Resolve(kind=="time"?-1:3,[a,b],[o],[Hostile]));
    }
    [Fact] public void NullCollectionsAreRejectedBeforeEnumeration()
    {
        Assert.Throws<InvalidDataException>(()=>FieldEngagement.Resolve(3,null!,[],[]));
        Assert.Throws<InvalidDataException>(()=>FieldEngagement.Resolve(3,[],null!,[]));
        Assert.Throws<InvalidDataException>(()=>FieldEngagement.Resolve(3,[],[],null!));
    }
    [Fact] public void DamageUsesExistingContactCombatRules()
    {
        var random=new Random(20261025);
        for(int i=0;i<100;i++){
            var a=A with{Unit=A.Unit with{Soldiers=random.Next(1,1000)},Morale=random.Next(1,101)};
            var b=B with{Unit=B.Unit with{Soldiers=random.Next(1,1000)},Morale=random.Next(1,101)};
            var expected=ContactCombat.Resolve(3,true,[new(a.Unit.Id,0,a.Position,a.Height,(int)a.Unit.Soldiers,a.Morale,a.Phase),new(b.Unit.Id,1,b.Position,b.Height,(int)b.Unit.Soldiers,b.Morale,b.Phase)]);
            Assert.Equal(expected,Run(a:a,b:b).Losses);
        }
    }
    [Fact]public void MissionStillDefendsAgainstAnAttackerOutsideItsMissionTarget()
    {
        var r=FieldEngagement.Resolve(3,[A,B with{Intent=EngagementIntent.DeliberateUnauthorizedAttack,Target=A.Unit.Id}],[Order with{TargetOwner=new("org:elsewhere")}],[]);Assert.Contains(r.Strikes,s=>s.Attacker==A.Unit.Id&&s.Target==B.Unit.Id&&s.Basis==EngagementBasis.ImmediateDefense);
    }
    [Fact]public void MissionAndImmediateDefenseCannotCountTheSameReciprocalHitTwice()
    {
        var r=Run(b:B with{Intent=EngagementIntent.DeliberateUnauthorizedAttack,Target=A.Unit.Id});Assert.Single(r.Strikes,s=>s.Attacker==A.Unit.Id&&s.Target==B.Unit.Id);Assert.Equal(4,r.Losses.Single(l=>l.Unit==B.Unit.Id).Soldiers);
    }
    [Fact]public void ExpiredMissionRetainsImmediateDefenseWithoutMissionAuthority()
    {
        var r=Run(at:10,b:B with{Intent=EngagementIntent.DeliberateUnauthorizedAttack,Target=A.Unit.Id});Assert.Contains(r.Strikes,s=>s.Attacker==A.Unit.Id&&s.Basis==EngagementBasis.ImmediateDefense);Assert.DoesNotContain(r.Strikes,s=>s.Basis==EngagementBasis.Mission);
    }

}
