using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

// 傷病死：危篤の傷病の峠で命を落とし、自然死と同じ死亡の流れ（訃報・後継）に乗る（BD-06・BD-07、2026-11-13）。
public sealed class HealthMortalityTests
{
    static ContractWorld Start(ulong seed,bool clinical=true)
    {
        var d=PersonalHealthTests.Start().NewGame!;
        var request=new WorldSetupRequest(d.Registered,[]);var raw=NewGameWorld.Scenario(d.HistoricalCatalog);var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        return NewGameWorld.Create(setup,d.Registered,d.Selected.Value,seed,NewGameWorld.CompactPersonalAuthority,d.HistoricalCatalog,NpcJudgment.Policy,LocalInspectionFindings.Policy,WorkFollowup.Policy,LocalWorkDemand.Policy,
            developmentPolicy:CharacterDevelopment.Policy,companionPolicy:PersonalCompanions.Policy,socialPolicy:CharacterRelations.Policy,
            judgment:new(NpcJudgment.Policy,HistoryMode.SemiHistorical,NpcAttention.Standard){HealthEdition=PersonalHealth.Edition},healthPolicy:clinical?ClinicalHealth.Policy:null);
    }
    // 本人以外で、最初の数か月に命を落とす危篤の病を持つ固定シードを探す。
    static (ulong Seed,HealthCondition C) FatalIllness()
    {
        var basis=Start(1);(ulong Seed,HealthCondition C)? best=null;
        for(ulong seed=1;seed<=400;seed++)
        {
            basis.NewGame=basis.NewGame! with{Seed=seed};
            foreach(var p in PersonalHealth.Living(basis).Where(p=>p!=basis.PlayerControl))
                for(long k=0;k<2;k++)
                    if(PersonalHealth.Illness(basis,p,k) is {Severity:3} c&&c.OnsetAt>0&&HealthMortality.Fatal(basis,c)&&(best==null||c.OnsetAt<best.Value.C.OnsetAt))best=(seed,c);
        }
        return best??throw new Xunit.Sdk.XunitException("命を落とす危篤の病が見つかりません");
    }

    [Fact]
    public void ACriticalIllnessKillsAtItsCrisisThroughTheNaturalDeathFlowAndRestoresTheSameFuture()
    {
        var (seed,c)=FatalIllness();long crisis=HealthMortality.CrisisAt(c);
        Assert.Equal(43200,crisis%86400);Assert.True(crisis>=c.OnsetAt+7*86400);
        var s=new WorldSession(Start(seed));
        NewGameWorldTests.At(s,crisis-1);s.SetPaused(true);
        var before=s.DiagnosticSnapshot();
        Assert.DoesNotContain(before.NaturalDeaths??[],d=>d.Person==c.Person);
        Assert.NotEqual(LifeState.Dead,before.Characters.Single(x=>x.Id==c.Person).Life);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(before)));
        foreach(var x in new[]{s,restored}){NewGameWorldTests.At(x,crisis+1);x.SetPaused(true);}
        var w=s.DiagnosticSnapshot();
        var death=Assert.Single(w.NaturalDeaths!,d=>d.Person==c.Person);
        Assert.Equal(crisis,death.At);Assert.Equal(c.Id,death.Condition);
        Assert.Equal(LifeState.Dead,w.Characters.Single(x=>x.Id==c.Person).Life);
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(restored.DiagnosticSnapshot()));
        // 原因の傷病を書き換えた保存は読めない。
        var forged=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        forged.NaturalDeaths=forged.NaturalDeaths!.Select(d=>d.Person==c.Person?d with{Condition=new("condition:forged")}:d).ToArray();
        Assert.Throws<InvalidDataException>(()=>new WorldSession(forged));
    }

    [Fact]
    public void LegacyHealthAndTheRiskRuleStayBounded()
    {
        var (seed,c)=FatalIllness();
        var legacy=Start(seed,clinical:false);
        Assert.False(HealthMortality.Enabled(legacy));Assert.Null(HealthMortality.Next(legacy,HealthMortality.CrisisAt(c)));
        var w=Start(seed);
        Assert.InRange(HealthMortality.Risk(w,c),250,800);
        Assert.False(HealthMortality.Fatal(w,c with{Severity=2}));
    }
}
