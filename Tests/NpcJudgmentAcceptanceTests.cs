using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class NpcJudgmentAcceptanceTests
{
    static void At(WorldSession s,long at)=>NewGameWorldTests.At(s,at);
    static ContractWorld WithSettings(NpcAttention attention,HistoryMode history,ulong seed=20261030,bool shared=false)
    {
        var d=NpcJudgmentTests.Start().NewGame!;
        if(shared){var records=d.Registered.Append(d.Registered[0] with{Id="created:third",Name="候補の浪人"}).ToArray();var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);var request=new WorldSetupRequest(records,[]);d=d with{Registered=records,Start=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request))};}
        return NewGameWorld.Create(d.Start,d.Registered,d.Selected.Value,seed,d.PersonalAuthorityLayout,d.HistoricalCatalog,NpcJudgment.Policy,d.InspectionPolicy,d.WorkPolicy,d.DemandPolicy,
            developmentPolicy:CharacterDevelopment.Policy,companionPolicy:PersonalCompanions.Policy,socialPolicy:CharacterRelations.Policy,judgment:new(NpcJudgment.Policy,history,attention));
    }
    [Theory]
    [InlineData(HistoryMode.Historical,NpcAttention.Beginner)][InlineData(HistoryMode.Historical,NpcAttention.Standard)][InlineData(HistoryMode.Historical,NpcAttention.Equal)]
    [InlineData(HistoryMode.SemiHistorical,NpcAttention.Beginner)][InlineData(HistoryMode.SemiHistorical,NpcAttention.Standard)][InlineData(HistoryMode.SemiHistorical,NpcAttention.Equal)]
    [InlineData(HistoryMode.Fictional,NpcAttention.Beginner)][InlineData(HistoryMode.Fictional,NpcAttention.Standard)][InlineData(HistoryMode.Fictional,NpcAttention.Equal)]
    public void AllSettingsRemainPinnedWithoutAddingRightsOrMoney(HistoryMode history,NpcAttention attention)
    {
        var w=WithSettings(attention,history);var d=w.NewGame!;var store=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"judgment-settings-"+Guid.NewGuid().ToString("N")));
        var profile=store.New(d.Start,d.Registered,d.Selected.Value,w.Seed,d.PersonalAuthorityLayout,d.HistoricalCatalog,NpcJudgment.Policy,d.InspectionPolicy,d.WorkPolicy,d.DemandPolicy,
            developmentPolicy:CharacterDevelopment.Policy,companionPolicy:PersonalCompanions.Policy,socialPolicy:CharacterRelations.Policy,judgment:d.Judgment);
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(NewCampaignStore.Build(store.Read(profile.Instance))));
        var other=WithSettings(NpcAttention.Equal,HistoryMode.Fictional);Assert.Equal(ContractJson.Write(other.Grants),ContractJson.Write(w.Grants));Assert.Equal(ContractJson.Write(other.ResourceLots),ContractJson.Write(w.ResourceLots));
        Assert.Throws<InvalidDataException>(()=>NewCampaignStore.Build(profile with{Judgment=null}));
    }
    [Fact] public void AttentionCanChangeActualSelectedRecipientWhileKeepingFee()
    {
        bool changed=false;
        for(ulong seed=1;seed<=32&&!changed;seed++){
            var a=WithSettings(NpcAttention.Equal,HistoryMode.Fictional,seed,true);var b=WithSettings(NpcAttention.Beginner,HistoryMode.Fictional,seed,true);
            // 主人公自身は発令NPCにしない。地元の登録人物が依頼主になる。
            var issuer=new EntityId("created:routine-0");
            var x=NewGameNpcRoutine.Inputs(a,issuer,0,0).Routine!.Judgment!;var y=NewGameNpcRoutine.Inputs(b,issuer,0,0).Routine!.Judgment!;
            Assert.Equal(x.Choice,y.Choice);
            if(x.Recipient!=y.Recipient){Assert.Equal(b.PlayerControl,y.Recipient);changed=true;}
        }
        Assert.True(changed,"実在する複数候補の相手選びへ難易度が反映されていません");
    }
    [Fact] public void FailedLetterNeverExecutesAndTimeoutDoesNotCreatePayment()
    {
        var s=new WorldSession(NpcJudgmentTests.Start());At(s,1);var w=s.DiagnosticSnapshot();var task=w.Tasks.Single(t=>t.Executor==s.PlayerControl);var letter=w.Messaging.Messages.Single(m=>m.CorrelationId==task.RequestId.Value&&m.Kind=="contract-order");
        s.DiagnosticTransport((m,t)=>MessageFlow.Fail(m,letter.Id));At(s,130);w=s.DiagnosticSnapshot();
        Assert.DoesNotContain(w.Messaging.Knowledge,k=>k.MessageId==letter.Id&&k.CharacterId==s.PlayerControl.Value);Assert.NotEqual(ExecutionPhase.Completed,w.Tasks.Single(t=>t.Id==task.Id).Execution);Assert.False(NewGameWork.Paid(w,task.Id));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void ActualRecoveryPreservesSettingsDecisionsAndSameFuture(int phase)
    {
        var s=new WorldSession(NpcJudgmentTests.Start());At(s,phase switch{0=>1,1=>10,2=>30,3=>3601,4=>86420,_=>172820});s.SetPaused(true);var before=s.DiagnosticSnapshot();
        var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"npc-judgment-recovery-"+Guid.NewGuid().ToString("N")),before.Definitions);Assert.True(store.Save(s,"judgment",before.WorldId).Published);
        var host=new WorldSessionHost(new WorldSession(NpcJudgmentTests.Start()));Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("judgment"),host.Current,host.Current.Revision));
        Assert.Equal(ContractJson.Write(before),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        var output=Environment.GetEnvironmentVariable("HAKUTAI_WORLD_CLASH_QA_DIR");
        if(!string.IsNullOrWhiteSpace(output)){Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,$"npc-judgment-{phase}.before.json"),ContractJson.Write(before));File.WriteAllBytes(Path.Combine(output,$"npc-judgment-{phase}.restored.json"),ContractJson.Write(host.Current.DiagnosticSnapshot()));}
        foreach(var x in new[]{s,host.Current})At(x,x.Seconds+86400);Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
    }
}
