using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class ClinicalHealthTests
{
    static ContractWorld Start(ulong seed=42,bool clinical=true,bool retainer=false)
    {
        var d=PersonalHealthTests.Start().NewGame!;
        var registered=retainer?d.Registered.Select(p=>p.Id=="created:routine-0"?p with{Role=StartTemplate.Retainer,Lord="person:nobunaga"}:p).ToArray():d.Registered;
        var request=new WorldSetupRequest(registered,[]);var raw=NewGameWorld.Scenario(d.HistoricalCatalog);var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        return NewGameWorld.Create(setup,registered,d.Selected.Value,seed,NewGameWorld.CompactPersonalAuthority,d.HistoricalCatalog,NpcJudgment.Policy,LocalInspectionFindings.Policy,WorkFollowup.Policy,LocalWorkDemand.Policy,
            developmentPolicy:CharacterDevelopment.Policy,companionPolicy:PersonalCompanions.Policy,socialPolicy:CharacterRelations.Policy,
            judgment:new(NpcJudgment.Policy,HistoryMode.SemiHistorical,NpcAttention.Standard){HealthEdition=PersonalHealth.Edition},healthPolicy:clinical?ClinicalHealth.Policy:null);
    }
    static (ContractWorld,HealthCondition) Illness()
    {
        var basis=Start();(ulong Seed,HealthCondition C)? best=null;
        for(ulong seed=1;seed<=100;seed++)
        {
            basis.NewGame=basis.NewGame! with{Seed=seed};
            var c=PersonalHealth.Conditions(basis,basis.PlayerControl,30*86400).FirstOrDefault(c=>c.Severity==1&&c.OnsetAt>0);
            if(c!=null&&(best==null||c.OnsetAt<best.Value.C.OnsetAt))best=(seed,c);
        }
        Assert.NotNull(best);return(Start(best.Value.Seed),best.Value.C);
    }
    static void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
    [Fact]public void DelayedTreatmentRestoresTheSameFutureWithoutDuplicateCare()
    {
        var (w,c)=Illness();var s=new WorldSession(w);NewGameWorldTests.At(s,c.OnsetAt);
        var export=Environment.GetEnvironmentVariable("HAKUTAI_CLINICAL_QA_DIR");if(!string.IsNullOrEmpty(export)){Directory.CreateDirectory(export);File.WriteAllBytes(Path.Combine(export,"ready.json"),ContractJson.Write(s.DiagnosticSnapshot()));}
        var input=new HealthCareInput(new("request:clinical-first"),s.PlayerControl,c.Id,s.PlayerControl);
        Good(s.ManageHealth(input,s.PlayerControl,s.Revision));
        Assert.Empty(s.DiagnosticSnapshot().Health!.Care);Assert.Equal(0,s.ClinicalView()!.MedicineExperience);
        Assert.False(PersonalHealth.Can(s.DiagnosticSnapshot(),s.PlayerControl,HealthLimit.Travel,s.Seconds));
        Assert.Equal(ResultCode.AlreadyAccepted,s.ManageHealth(input,s.PlayerControl,s.Revision).Code);
        long due=s.Seconds+ClinicalHealth.TreatmentSeconds;NewGameWorldTests.At(s,due-1);s.SetPaused(true);
        var snap=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"clinical-"+Guid.NewGuid().ToString("N")),snap.Definitions);
        Assert.True(store.Save(s,"manual",snap.WorldId).Published);
        var host=new WorldSessionHost(new WorldSession(w));Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("manual"),host.Current,host.Current.Revision));
        foreach(var x in new[]{s,host.Current}){NewGameWorldTests.At(x,due+1);x.SetPaused(true);}
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        Assert.Single(s.DiagnosticSnapshot().Health!.Care);Assert.Equal(2,s.ClinicalView()!.MedicineExperience);
        Assert.True(PersonalHealth.RecoverAt(s.DiagnosticSnapshot(),c)<c.OnsetAt+c.BaseSeconds);
        Assert.Null(ClinicalHealth.View(s.DiagnosticSnapshot(),new("person:other"),s.PlayerControl));
        var forged=s.DiagnosticSnapshot();forged.Health=forged.Health! with{Care=[forged.Health.Care[0] with{At=due-1}]};
        Assert.Throws<InvalidDataException>(()=>new WorldSession(forged));
    }
    [Fact]public void LegacyHealthNeverGainsClinicalStateAndRoundTripsByteForByte()
    {
        var w=Start(clinical:false);var bytes=ContractJson.Write(w);Assert.Null(w.Health!.Clinic);
        Assert.Equal(bytes,ContractJson.Write(new WorldSession(w).DiagnosticSnapshot()));
        Assert.DoesNotContain("healthPolicy",System.Text.Encoding.UTF8.GetString(bytes));
        w.Health=w.Health with{Clinic=new(1,ClinicalHealth.Policy,[],[])};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Fact]public void ActualWalkingIntervalsProduceFatigueAndWaitingReducesIt()
    {
        var w=Start();w.Branches=w.Branches! with{Journeys=[new(new("task:fatigue"),w.PlayerControl,[new("road","a","b",0,21600)])]};
        Assert.Equal(24,ClinicalHealth.Fatigue(w,w.PlayerControl,21600));
        Assert.Equal(16,ClinicalHealth.Fatigue(w,w.PlayerControl,36000));
        Assert.Equal(0,ClinicalHealth.Fatigue(w,w.PlayerControl,172800));
    }
    [Fact]public void BattleOfficerIdentityIsFrozenAndTheSameDayNeverRollsTwice()
    {
        var w=Start();var unit=new EntityId("unit:clinical");var me=w.PlayerControl;
        w.Units=[new(unit,new("army:clinical"),me,new("org:oda"),100,0)];
        var result=new EngagementResult(1,[],[new(1,unit,1,1)]);
        OfficerWound[]? wound=null;
        for(ulong seed=1;seed<=100;seed++){w.Seed=seed;wound=OfficerBattleHealth.Resolve(w,result);if(wound!.Length>0)break;}
        Assert.Single(wound!);Assert.Equal(me,wound![0].Person);
        var step=new WorldClashStep(new("army:clinical"),0,1,1,result){OfficerWounds=wound};
        w.WorldClashes=new(1,WorldClashes.Policy,[step]);
        Assert.Empty(OfficerBattleHealth.Resolve(w,result with{At=2})!);
        w.Units=[w.Units[0] with{Commander=new("person:replacement")}];
        Assert.Equal(me,OfficerBattleHealth.Conditions(w).Single().Person);
        var round=ContractJson.Read<WorldClashStep>(ContractJson.Write(step));Assert.Equal(wound[0],round.OfficerWounds![0]);
    }
    [Fact]public void SealedHealthReportsOnlyExposeTheDatedObservedSnapshot()
    {
        var w=Start(retainer:true);var s=new WorldSession(w);var recipient=s.HealthReportRecipients().First();
        var input=new ClinicalReportInput(new("request:clinical-report"),s.PlayerControl,recipient);
        Good(s.ReportHealth(input,s.PlayerControl,s.Revision));
        var sent=s.DiagnosticSnapshot();var report=sent.Health!.Clinic!.Reports.Single();var letter=ClinicalHealth.LetterId(report);
        Assert.Empty(ClinicalHealth.KnownReports(sent,recipient));
        Assert.Equal(ResultCode.AlreadyAccepted,s.ReportHealth(input,s.PlayerControl,s.Revision).Code);
        // 配送機構の到達と開封を分けて、観測者が移動してもスナップショットのままか確かめる。
        var attempt=sent.Messaging.Attempts.Single(a=>a.MessageId==letter);
        NewGameWorldTests.At(s,attempt.DueAt);
        Assert.Empty(ClinicalHealth.KnownReports(s.DiagnosticSnapshot(),recipient));
        Good(s.Open(recipient,new(letter),s.Revision));
        Assert.Single(ClinicalHealth.KnownReports(s.DiagnosticSnapshot(),recipient));
        Assert.Equal(report.Cards,ClinicalHealth.KnownReports(s.DiagnosticSnapshot(),recipient).Single().Cards);
        var forged=s.DiagnosticSnapshot();var old=forged.Health!.Clinic!.Reports.Single();
        forged.Health=forged.Health with{Clinic=forged.Health.Clinic with{Reports=[old with{Cards=[new(new("condition:forged"),"病","熱","危篤",0,false,"")]}]}};
        Assert.Throws<InvalidDataException>(()=>new WorldSession(forged));
    }

}
