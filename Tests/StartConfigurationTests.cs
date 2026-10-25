using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class StartConfigurationTests
{
    static readonly EntityId Created = new("person:created/test");
    static readonly StartBudgetPolicy Policy = new(1000,25,10,1,[new("rice","fixture-rice-unit",2)]);
    static ContractWorld World(StartTemplate role = StartTemplate.Ronin)
    {
        var w=ContractFixture.Create();w.PlayerControl=Created;
        w.Definitions=w.Definitions with {Characters=[..w.Definitions.Characters,new(Created,"作成武将（試験）")]};
        w.Characters=[..w.Characters,new(Created,LifeState.Adult,new("appearance:fixture"),0)];
        w.Locations=[..w.Locations,new(Created,LocationKind.AtSite,ContractFixture.Castle,null,0,0,null,null,null,null,0)];
        EntityId basis=new("basis:membership"),house=w.Definitions.Organizations[0].Id;
        TimedRelation R(string id,EntityId a,EntityId b,string kind)=>new(new("start:"+id),a,b,kind,basis,0,long.MaxValue,0);
        if(role!=StartTemplate.Ronin)w.Memberships=[..w.Memberships,R("member",Created,house,"membership")];
        if(role is StartTemplate.Retainer or StartTemplate.Subretainer)w.ServiceBonds=[..w.ServiceBonds,R("service",Created,ContractFixture.Lord,"service")];
        if(role==StartTemplate.Subretainer)w.ServiceBonds=[..w.ServiceBonds,R("upper",ContractFixture.Lord,ContractFixture.Clerk,"service")];
        if(role==StartTemplate.Castellan)w.Offices=[..w.Offices,R("office",Created,ContractFixture.Castle,"castellan")];
        if(role is StartTemplate.LocalLord or StartTemplate.Daimyo){w.Offices=[..w.Offices,R("head",Created,ContractFixture.Castle,"house-head")];w.LandRights=[R("land",Created,ContractFixture.Castle,"control")];}
        if(role==StartTemplate.Daimyo)w.ServiceBonds=[..w.ServiceBonds,R("follower",ContractFixture.Clerk,Created,"service")];
        ContractValidation.Validate(w);return w;
    }
    static StartSelection Choice(ContractWorld w,StartTemplate role=StartTemplate.Ronin,StartBudgetPolicy? policy=null)=>StartConfiguration.Describe(w,Created,role,PersonOrigin.Created,policy??Policy);
    static StartCheck Check(ContractWorld w,StartTemplate role=StartTemplate.Ronin,StartBudgetPolicy? policy=null)=>StartConfiguration.Check(w,Choice(w,role,policy),policy??Policy);
    [Theory]
    [InlineData(StartTemplate.Ronin)] [InlineData(StartTemplate.Retainer)] [InlineData(StartTemplate.Subretainer)]
    [InlineData(StartTemplate.Castellan)] [InlineData(StartTemplate.LocalLord)] [InlineData(StartTemplate.Daimyo)]
    public void SixSyntheticRolesAreValidatedWithoutCreatingRights(StartTemplate role)
    {var w=World(role);var before=ContractJson.CanonicalHash(w);Assert.True(Check(w,role).Eligible);Assert.Equal(before,ContractJson.CanonicalHash(w));}
    [Theory]
    [InlineData(HistoryMode.Historical)] [InlineData(HistoryMode.SemiHistorical)] [InlineData(HistoryMode.Fictional)]
    public void ModesDoNotChangeRightsOrSeed(HistoryMode mode)
    {var w=World();var c=Choice(w) with{Mode=mode};Assert.True(StartConfiguration.Check(w,c,Policy).Eligible);Assert.Equal(20261020UL,c.Seed);Assert.DoesNotContain(w.Grants,g=>g.Agent==Created);}
    [Fact] public void AdultAndIdentityAreRequired(){var w=World();w.Characters=w.Characters.Select(c=>c.Id==Created?c with{Life=LifeState.Minor}:c).ToArray();Assert.False(Check(w).Eligible);}
    [Fact] public void RankNameNeverGrantsMissingRights(){var w=World();foreach(var role in Enum.GetValues<StartTemplate>().Where(r=>r!=StartTemplate.Ronin))Assert.False(Check(w,role).Eligible);Assert.Empty(w.LandRights);}
    [Fact] public void ExpiredMembershipCannotSupplyRetainerStatus(){var w=World(StartTemplate.Retainer);w.Memberships=w.Memberships.Select(m=>m.Subject==Created?m with{From=1,Until=10}:m).ToArray();Assert.False(Check(w,StartTemplate.Retainer).Eligible);}
    [Fact] public void UpperLordNeededForSubretainer(){Assert.False(Check(World(StartTemplate.Retainer),StartTemplate.Subretainer).Eligible);}
    [Fact] public void ServiceCyclesAndDanglingReferencesAreRejected(){var w=World(StartTemplate.Retainer);w.ServiceBonds=[..w.ServiceBonds,new(new("start:cycle"),ContractFixture.Lord,Created,"service",new("basis:service"),0,long.MaxValue,0)];Assert.False(Check(w,StartTemplate.Retainer).Eligible);w=World();w.Locations[0]=w.Locations[0] with{Site=new("site:absent")};Assert.False(Check(w).Eligible);}
    [Fact] public void DefinitionSeedAndContentAreBound(){var w=World();var c=Choice(w);Assert.False(StartConfiguration.Check(w,c with{Seed=0},Policy).Eligible);Assert.False(StartConfiguration.Check(w,c with{DefinitionVersion="other"},Policy).Eligible);w.Characters[0]=w.Characters[0] with{Revision=1};Assert.False(StartConfiguration.Check(w,c,Policy).Eligible);}
    [Fact] public void InvalidEnumsAreRejected(){var w=World();var c=Choice(w);Assert.False(StartConfiguration.Check(w,c with{Template=(StartTemplate)99},Policy).Eligible);Assert.False(StartConfiguration.Check(w,c with{Mode=(HistoryMode)99},Policy).Eligible);Assert.False(StartConfiguration.Check(w,c with{Origin=(PersonOrigin)99},Policy).Eligible);}
    [Fact] public void HistoricalNamesDoNotInventAuthority(){var w=NpcWork.Create();var c=StartConfiguration.Describe(w,new("person:nobunaga"),StartTemplate.Daimyo,PersonOrigin.Historical,Policy);Assert.False(StartConfiguration.Check(w,c,Policy).Eligible);Assert.Contains(StartConfiguration.Check(w,c,Policy).Reasons,r=>r.Contains("詳細採録前"));}
    [Fact] public void CreatedIdMustNotReplaceHistoricalOrFixturePerson(){var w=World();var c=Choice(w) with{Person=ContractFixture.Officer};Assert.False(StartConfiguration.Check(w,c,Policy).Eligible);}
    [Fact] public void HouseholdAssetsAndFollowersConsumeCreatedBudget(){var w=World(StartTemplate.Daimyo);w.ResourceLots=w.ResourceLots.Select(l=>l.Id==ContractFixture.Treasury?l with{Quantity=100,InitialQuantity=100}:l).ToArray();var check=Check(w,StartTemplate.Daimyo);Assert.True(check.Eligible);Assert.Equal(235L,check.CreationCost);var low=Policy with{Budget=234};Assert.False(Check(w,StartTemplate.Daimyo,low).Eligible);}
    [Fact] public void UnknownResourceUnitAndArithmeticOverflowFailClosed(){var w=World();w.ResourceLots=[..w.ResourceLots,new(new("lot:personal"),"coin",Created,Created,ContractFixture.Castle,"unknown-unit",1,1,0)];Assert.False(Check(w).Eligible);w.ResourceLots[^1]=w.ResourceLots[^1] with{Kind="rice",Unit="fixture-rice-unit",Quantity=long.MaxValue,InitialQuantity=long.MaxValue};Assert.False(Check(w).Eligible);}
    [Fact] public void ForeignCommandIsNotGrantedByTemplate(){var w=World();var org=w.Definitions.Organizations[0].Id;w.Definitions=w.Definitions with{Armies=[new(new("army:start"),org,Created)]};w.Units=[new(new("unit:start"),new("army:start"),Created,org,100,0)];Assert.False(Check(w).Eligible);}
    [Fact] public void MissingOrDuplicatePolicyDoesNotMutateWorld(){var w=World();var hash=ContractJson.CanonicalHash(w);Assert.False(StartConfiguration.Check(w,Choice(w),Policy with{Prices=null!}).Eligible);var duplicate=Policy with{Prices=[Policy.Prices[0],Policy.Prices[0]]};Assert.False(Check(w,policy:duplicate).Eligible);Assert.Equal(hash,ContractJson.CanonicalHash(w));}
    [Fact] public void StrictPackageRoundtripPreservesChoiceAndWorld(){var w=World(StartTemplate.Daimyo);var p=new StartPackage(Choice(w,StartTemplate.Daimyo),w,Policy);var bytes=ContractJson.Write(p);var loaded=ContractJson.Read<StartPackage>(bytes);Assert.Equal(bytes,ContractJson.Write(loaded));Assert.True(StartConfiguration.Check(loaded.World,loaded.Selection,loaded.Policy).Eligible);var bad=System.Text.Encoding.UTF8.GetString(bytes).Replace("\"PolicyHash\":","\"Unknown\":0,\"PolicyHash\":");Assert.ThrowsAny<Exception>(()=>ContractJson.Read<StartPackage>(System.Text.Encoding.UTF8.GetBytes(bad)));}
    [Fact] public void SoldiersAreChargedSeparatelyFromFollowers(){var w=World(StartTemplate.Daimyo);var org=w.Definitions.Organizations[0].Id;w.Definitions=w.Definitions with{Armies=[new(new("army:start"),org,Created)]};w.Units=[new(new("unit:start"),new("army:start"),Created,org,100,0)];Assert.Equal(135L,Check(w,StartTemplate.Daimyo).CreationCost);Assert.False(Check(w,StartTemplate.Daimyo,Policy with{Budget=134}).Eligible);Assert.True(Check(w,StartTemplate.Daimyo,Policy with{Budget=135}).Eligible);}
    [Fact] public void RoninCannotBeginWithTaxGrant(){var w=World();w.Grants=[..w.Grants,w.Grants.First(g=>g.Actions.Contains(ActionKind.Transfer)) with{Id=new("grant:created-tax"),Agent=Created}];Assert.False(Check(w).Eligible);}

}
