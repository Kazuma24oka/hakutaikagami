using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

[Trait("Tier","Heavy")]public sealed class PersonalHealthTests
{
    static readonly EntityId Friend=new("created:routine-0");
    internal static ContractWorld Start(ulong? seed=null,int? health=PersonalHealth.Edition)
    {
        var old=NpcContinuingTests.Start(companions:true);var d=old.NewGame!;
        return NewGameWorld.Create(d.Start,d.Registered,d.Selected.Value,seed??old.Seed,NewGameWorld.CompactPersonalAuthority,d.HistoricalCatalog,NpcJudgment.Policy,LocalInspectionFindings.Policy,WorkFollowup.Policy,LocalWorkDemand.Policy,
            developmentPolicy:CharacterDevelopment.Policy,companionPolicy:PersonalCompanions.Policy,socialPolicy:CharacterRelations.Policy,
            judgment:new(NpcJudgment.Policy,HistoryMode.SemiHistorical,NpcAttention.Standard){HealthEdition=health});
    }
    static void At(WorldSession s,long at)=>NewGameWorldTests.At(s,at);
    static void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.Code+":"+r.VisibleReason);
    const long Horizon=120L*86400;
    // 固定シードの病から、指定の重さ以上の最初の病を持つ開始を探す。
    static (ContractWorld World,HealthCondition Illness) WithIllness(EntityId person,int severity)
    {
        // 進める時間を短くするため、最も早い発症の開始を選ぶ。
        var basis=Start();(ContractWorld World,HealthCondition Illness)? best=null;
        for(ulong seed=1;seed<=300;seed++)
        {
            var w=ContractJson.Copy(basis);w.NewGame=w.NewGame! with{Seed=seed};
            var c=PersonalHealth.Conditions(w,person,Horizon).FirstOrDefault(c=>c.Kind==HealthKind.Illness&&c.Severity>=severity&&c.OnsetAt>100);
            if(c!=null&&(best==null||c.OnsetAt<best.Value.Illness.OnsetAt))best=(Start(seed),c);
        }
        return best??throw new InvalidOperationException("no seeded illness");
    }

    [Fact]public void OldSettingsNeverGainHealthAndForgedStateIsRejected()
    {
        var old=Start(health:null);Assert.Null(old.Health);Assert.Empty(PersonalHealth.Conditions(old,old.PlayerControl,Horizon));
        old.Health=new(1,PersonalHealth.Policy,[]);Assert.Throws<InvalidDataException>(()=>new WorldSession(old));
        var w=Start();Assert.NotNull(w.Health);w.NewGame=w.NewGame! with{Judgment=w.NewGame.Judgment! with{HealthEdition=9}};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }

    [Fact]public void SeededIllnessIsStableAcrossAdvancePartitions()
    {
        var (w,c)=WithIllness(new EntityId("person:nobunaga"),1);
        Assert.Equal(PersonalHealth.Conditions(w,c.Person,Horizon).Select(x=>(x.Id,x.OnsetAt,x.Severity)),PersonalHealth.Conditions(ContractJson.Copy(w),c.Person,Horizon).Select(x=>(x.Id,x.OnsetAt,x.Severity)));
        var a=new WorldSession(w);var b=new WorldSession(ContractJson.Copy(w));a.SetPaused(false);b.SetPaused(false);
        At(a,c.OnsetAt+3600);foreach(long t in new[]{c.OnsetAt-1,c.OnsetAt,c.OnsetAt+3600})At(b,t);
        var x=a.DiagnosticSnapshot();var y=b.DiagnosticSnapshot();
        Assert.Equal(x.Requests.Select(r=>(r.Id,r.CreatedAt,r.Intent.Action)),y.Requests.Select(r=>(r.Id,r.CreatedAt,r.Intent.Action)));
        Assert.Equal(ContractJson.Write(x.Development),ContractJson.Write(y.Development));
        Assert.Equal(PersonalHealth.Active(x,c.Person,x.ElapsedSeconds).Select(h=>h.Id),PersonalHealth.Active(y,c.Person,y.ElapsedSeconds).Select(h=>h.Id));
    }

    [Fact]public void FeverBlocksPracticeUntilRecoveryButKeepsOtherRecords()
    {
        var (w,c)=WithIllness(Start().PlayerControl,2);var s=new WorldSession(w);
        At(s,c.OnsetAt+60);
        var limits=PersonalHealth.Limits(s.DiagnosticSnapshot(),s.PlayerControl,s.Seconds);Assert.Contains(HealthLimit.Practice,limits);Assert.Contains(HealthLimit.Judgment,limits);
        var blocked=CharacterDevelopment.Practice(s.DiagnosticSnapshot(),"request:health-fever-practice",DevelopmentSkill.Tactics);
        var r=s.Submit(blocked,s.Revision);Assert.NotEqual(ResultCode.Accepted,r.Code);
        var view=s.HealthView(s.PlayerControl)!;Assert.True(view.Own);Assert.Contains(view.Conditions,x=>x.Id==c.Id&&x.Outlook.StartsWith("見立て"));Assert.Contains(PersonalHealth.Reason(HealthLimit.Practice),view.Limits);
        At(s,PersonalHealth.RecoverAt(s.DiagnosticSnapshot(),c)+1);
        Assert.DoesNotContain(PersonalHealth.Active(s.DiagnosticSnapshot(),s.PlayerControl,s.Seconds),x=>x.Id==c.Id);
    }

    [Fact]public void CareShortensTheRemainingCourseWithoutInstantRecovery()
    {
        var (w,c)=WithIllness(Start().PlayerControl,1);var s=new WorldSession(w);At(s,c.OnsetAt+60);
        long before=PersonalHealth.RecoverAt(s.DiagnosticSnapshot(),c);
        var care=new HealthCareInput(new("request:health-self-care"),s.PlayerControl,c.Id,s.PlayerControl);
        Good(s.ManageHealth(care,s.PlayerControl,s.Revision));
        long after=PersonalHealth.RecoverAt(s.DiagnosticSnapshot(),c);
        Assert.True(after<before&&after>s.Seconds);
        Assert.Equal(ResultCode.AlreadyAccepted,s.ManageHealth(care,s.PlayerControl,s.Revision).Code);
        Assert.Equal(ResultCode.NotAvailable,s.ManageHealth(care with{Id=new("request:health-second-care")},s.PlayerControl,s.Revision).Code);
        Assert.True(s.HealthView(s.PlayerControl)!.Conditions.Single(x=>x.Id==c.Id).Treated);
    }

    [Fact]public void OthersSeeOnlyWhatIsVisibleLocallyAndNeverTheOutlook()
    {
        var (w,c)=WithIllness(Friend,1);var s=new WorldSession(w);At(s,c.OnsetAt+60);var x=s.DiagnosticSnapshot();
        var view=PersonalHealth.View(x,x.PlayerControl,Friend)!;
        bool together=SocialWorld.Site(x,x.PlayerControl) is {} site&&SocialWorld.Site(x,Friend)==site;
        Assert.Equal(together,view.Observed);Assert.False(view.Own);Assert.Empty(view.Limits);
        Assert.All(view.Conditions,card=>Assert.Equal("",card.Outlook));
        if(!together)Assert.Empty(view.Conditions);
    }

    [Fact]public void IllNpcDoesNotChooseBlockedPractice()
    {
        var (w,c)=WithIllness(Friend,2);var s=new WorldSession(w);At(s,PersonalHealth.RecoverAt(w,c)-60);
        var x=s.DiagnosticSnapshot();
        Assert.DoesNotContain(x.Requests,r=>r.Intent.Executor==Friend&&r.Intent.Action==ActionKind.Train&&r.CreatedAt>=c.OnsetAt&&r.CreatedAt<PersonalHealth.RecoverAt(x,c));
        Assert.DoesNotContain(x.Tasks,t=>t.Executor==Friend&&t.Action==ActionKind.Train&&t.Execution==ExecutionPhase.Running);
    }

    [Fact]public void ForgedCareRecordsAreRejected()
    {
        var (w,c)=WithIllness(Start().PlayerControl,1);var s=new WorldSession(w);At(s,c.OnsetAt+60);
        Good(s.ManageHealth(new(new("request:health-forge-base"),s.PlayerControl,c.Id,s.PlayerControl),s.PlayerControl,s.Revision));
        var x=s.DiagnosticSnapshot();var e=x.Health!.Care[0];
        var early=ContractJson.Copy(x);early.Health=early.Health! with{Care=[e with{At=c.OnsetAt-10}]};Assert.Throws<InvalidDataException>(()=>new WorldSession(early));
        var unknown=ContractJson.Copy(x);unknown.Health=unknown.Health! with{Care=[e with{Input=e.Input with{Condition=new("condition:missing")}}]};Assert.Throws<InvalidDataException>(()=>new WorldSession(unknown));
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void SavedIllnessAndCareRestoreTheSameFuture(int phase)
    {
        var (w,c)=WithIllness(Start().PlayerControl,1);var s=new WorldSession(w);
        At(s,phase==0?c.OnsetAt-60:c.OnsetAt+60);
        if(phase==2)Good(s.ManageHealth(new(new("request:health-save-care"),s.PlayerControl,c.Id,s.PlayerControl),s.PlayerControl,s.Revision));
        s.SetPaused(true);var snap=s.DiagnosticSnapshot();
        var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"health-"+Guid.NewGuid().ToString("N")),snap.Definitions);
        Assert.True(store.Save(s,"health",snap.WorldId).Published);
        var host=new WorldSessionHost(new WorldSession(Start(w.NewGame!.Seed)));
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("health"),host.Current,host.Current.Revision));
        Assert.Equal(ContractJson.Write(snap),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        long end=PersonalHealth.RecoverAt(snap,c)+3600;foreach(var x in new[]{s,host.Current})At(x,end);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
    }
}
