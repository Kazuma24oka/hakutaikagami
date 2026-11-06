using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public class GuardianshipTests
{
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
        Assert.False(MinorGuardians.MayKeepLedger(w,mandate));
        const long death=12830400;
        w.ElapsedSeconds=death-1;
        session=new WorldSession(w);NewGameWorldTests.At(session,death);
        w=session.DiagnosticSnapshot();
        Assert.True(MinorGuardians.MayKeepLedger(w,mandate));
        Assert.Equal(mandate.Guardian,GuardianLedger.Latest(w,mandate)!.Holder);
        Assert.Null(FactionStances.ResolveAuthority(w,mandate.Guardian,house,death,w.CommitSequence));
        foreach(var attempt in w.Messaging.Attempts.Where(a=>a.DueAt>session.Seconds).OrderBy(a=>a.DueAt))NewGameWorldTests.At(session,attempt.DueAt);
        w=session.DiagnosticSnapshot();w.ElapsedSeconds=mandate.Until-1;
        session=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.True(MinorGuardians.MayKeepLedger(session.DiagnosticSnapshot(),mandate));
        NewGameWorldTests.At(session,mandate.Until);
        var adult=session.DiagnosticSnapshot();
        Assert.False(MinorGuardians.MayKeepLedger(adult,mandate));
        Assert.False(GuardianLedger.Latest(adult,mandate)!.Returned);
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
