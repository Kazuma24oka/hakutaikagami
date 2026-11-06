using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public class GuardianshipTests
{
    [Fact]
    public void AdultSiblingsMakeTheirOwnClaimsWithoutGivingThePlayerRemoteKnowledge()
    {
        var elder=NewGameWorldTests.Officer("created:elder",StartTemplate.Daimyo) with{BirthYear=1430};
        var children=Enumerable.Range(1,8).Select(i=>NewGameWorldTests.Officer("created:sibling-"+i) with{BirthParents=[elder.Id]}).ToArray();
        var setup=NewGameWorldTests.Setup([elder,..children],[new(elder.Id,"org:elder",[elder.Site],false)]);
        var world=NewGameWorld.Create(setup.World,setup.Registered,"person:shingen",0,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,
            socialPolicy:CharacterRelations.Policy,comingOfAge:15);
        const long death=12830400;
        world.ElapsedSeconds=death-1;
        var s=new WorldSession(world);NewGameWorldTests.At(s,death);
        var after=s.DiagnosticSnapshot();
        var claims=after.HouseClaims??[];
        Assert.True(claims.Length>=2);
        Assert.Equal(claims.Length,claims.DistinctBy(c=>c.Claimant).Count());
        Assert.Empty(Guardianship.Known(after,s.PlayerControl));
        var opening=Assert.Single(Guardianship.Known(after,claims[0].Claimant)).Opening;
        Assert.Empty(Guardianship.WitnessedClaims(after,opening,s.PlayerControl));
        Assert.Equal(claims.Length,Guardianship.WitnessedClaims(after,opening,claims[0].Claimant).Count());
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(after)));
        NewGameWorldTests.At(s,death+1);NewGameWorldTests.At(restored,death+1);
        Assert.Equal(claims.Length,restored.DiagnosticSnapshot().HouseClaims!.Length);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LimitedGuardianAuthorityBeginsOnKnownDeathAndEndsAtComingOfAge(bool present)
    {
        var head=NewGameWorldTests.Officer("created:elder",StartTemplate.Daimyo) with{BirthYear=1430};
        var child=NewGameWorldTests.Officer("created:minor",site:present?head.Site:"site:kyoto") with{BirthYear=1546,BirthParents=[head.Id]};
        var retainers=Enumerable.Range(1,8).Select(i=>NewGameWorldTests.Officer("created:guardian-"+i,
            StartTemplate.Retainer,head.Site,head.Id)).ToArray();
        EntityId house=new("org:elder");
        var setup=NewGameWorldTests.Setup([head,child,..retainers],[new(head.Id,house.Value,[head.Site],false)]);
        var w=NewGameWorld.Create(setup.World,setup.Registered,head.Id,0,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,
            socialPolicy:CharacterRelations.Policy,comingOfAge:15);
        var session=new WorldSession(w);
        Assert.Equal(ResultCode.Accepted,session.DesignateHeir(house,new(child.Id),session.Revision).Code);
        var paper=Assert.Single(session.DiagnosticSnapshot().HeirNominations!);
        foreach(var r in retainers)
            if(session.AgreeGuardian(paper,new(r.Id),session.Revision).Code==ResultCode.Accepted)break;
        w=session.DiagnosticSnapshot();
        var mandate=Assert.Single(w.GuardianMandates!);
        var lands=ContractJson.Write(w.LandRights);var units=ContractJson.Write(w.Units);
        Assert.False(MinorGuardians.MayKeepLedger(w,mandate));
        const long death=12830400;
        w.ElapsedSeconds=death-1;
        session=new WorldSession(w);NewGameWorldTests.At(session,death);
        w=session.DiagnosticSnapshot();
        Assert.True(MinorGuardians.MayKeepLedger(w,mandate));
        Assert.Equal(mandate.Guardian,GuardianLedger.Latest(w,mandate)!.Holder);
        Assert.Equal(present?paper.Heir:paper.Head,HouseSuccession.Head(w,house));
        Assert.Equal(present?1:0,w.MinorityAccessions?.Length??0);
        Assert.Equal(paper.Head,HouseSuccession.Head(w,house,death-1,w.CommitSequence-1));
        Assert.Null(FactionStances.ResolveAuthority(w,paper.Heir,house,death,w.CommitSequence));
        Assert.Equal(lands,ContractJson.Write(w.LandRights));Assert.Equal(units,ContractJson.Write(w.Units));
        Assert.Null(FactionStances.ResolveAuthority(w,mandate.Guardian,house,death,w.CommitSequence));
        foreach(var attempt in w.Messaging.Attempts.Where(a=>a.DueAt>session.Seconds).OrderBy(a=>a.DueAt))NewGameWorldTests.At(session,attempt.DueAt);
        w=session.DiagnosticSnapshot();w.ElapsedSeconds=mandate.Until-1;
        session=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.True(MinorGuardians.MayKeepLedger(session.DiagnosticSnapshot(),mandate));
        NewGameWorldTests.At(session,mandate.Until);
        var adult=session.DiagnosticSnapshot();
        Assert.False(MinorGuardians.MayKeepLedger(adult,mandate));
        Assert.False(GuardianLedger.Latest(adult,mandate)!.Returned);
        Assert.Equal(present,FactionStances.ResolveAuthority(adult,paper.Heir,house,mandate.Until,adult.CommitSequence)!=null);
        var loaded=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(adult)));
        NewGameWorldTests.At(session,mandate.Until+1);NewGameWorldTests.At(loaded,mandate.Until+1);
        Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(loaded.DiagnosticSnapshot()));
        var ledger=GuardianLedger.Latest(loaded.DiagnosticSnapshot(),mandate)!;
        Assert.Equal(present,ledger.Returned);
        Assert.Equal(present?paper.Heir:mandate.Guardian,ledger.Holder);
        Assert.Equal(LifeState.Adult,adult.Characters.Single(c=>c.Id.Value==child.Id).Life);
        Assert.Equal(mandate,Assert.Single(new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(adult))).DiagnosticSnapshot().GuardianMandates!));
    }
}
