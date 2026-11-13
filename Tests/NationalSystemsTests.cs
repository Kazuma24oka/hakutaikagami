using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// 全国の開始で、地方の版の仕組み（生活と目標・人間関係・婚姻・養子・密書・天候・治安）が動くこと（2026-11-13）。
[Trait("Tier","Heavy")]
public sealed class NationalSystemsTests(ITestOutputHelper o)
{
    [Fact]
    public void LifeAndSocialSystemsRunInTheNationalStartAndRestoreToTheSameFuture()
    {
        var sw=System.Diagnostics.Stopwatch.StartNew();
        var w=NationalStart.Create(NationalStartTests.Nobunaga());o.WriteLine($"作成 {sw.ElapsedMilliseconds}ms");
        Assert.True(PersonalLife.Enabled(w));Assert.True(CharacterRelations.Enabled(w));Assert.True(AdultMarriage.Enabled(w));Assert.True(AdultAdoption.Enabled(w));
        Assert.True(PrivateInformation.Enabled(w));Assert.True(RegionalWeather.Enabled(w));Assert.True(LocalSecurity.Enabled(w));Assert.True(HouseSuccession.Enabled(w));Assert.True(AbilityEffects.Enabled(w));
        Assert.True(ClinicalHealth.Enabled(w));Assert.True(HealthMortality.Enabled(w));Assert.True(TruceNegotiations.Enabled(w));Assert.True(TreatyNegotiations.Enabled(w));Assert.NotNull(w.Truces);
        var s=new WorldSession(w);
        var site=SocialWorld.Site(s.DiagnosticSnapshot(),s.PlayerControl)!.Value;
        var goal=s.ManageLife(new(new("request:national-goal"),LifeAction.SetGoal,site,Goal:LifeGoal.KeepHouse,Target:1),s.PlayerControl,s.Revision);
        Assert.True(goal.Code==ResultCode.Accepted,goal.VisibleReason);
        sw.Restart();NewGameWorldTests.At(s,s.Seconds+6*3600);o.WriteLine($"6時間 {sw.ElapsedMilliseconds}ms"); // 重さは PC が空いたときに別に測る
        var mid=s.DiagnosticSnapshot();
        Assert.Contains(PersonalLife.Goals(mid),g=>g.Goal==LifeGoal.KeepHouse);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(mid)));
        foreach(var x in new[]{s,restored})NewGameWorldTests.At(x,mid.ElapsedSeconds+3600);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
    // 全国の開始での和睦交渉の一巡：本人（織田信長）が今川家の当主へ不戦の条件を書状で送り、相手が開封して返書し、本人が開封して成立を知る。
    [Fact]
    public void ATruceLetterTravelsToANeighbouringHeadAndTheReplyFormsTheTruce()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());var s=new WorldSession(w);
        var own=new EntityId(NationalStart.FactionId("G-000131"));var imagawa=new EntityId(NationalStart.FactionId("G-000123"));
        var sent=s.NegotiateTruce(new(new("request:national-truce"),TruceAction.Propose,own,imagawa,null,20),s.PlayerControl,s.Revision);
        Assert.True(sent.Code==ResultCode.Accepted,sent.VisibleReason);
        var sw=System.Diagnostics.Stopwatch.StartNew();
        for(int n=0;n<60&&!TruceNegotiations.Known(s.DiagnosticSnapshot(),s.PlayerControl).Any();n++)
        {
            NewGameWorldTests.At(s,s.Seconds+6*3600);
            foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())s.Open(s.PlayerControl,m.Id,s.Revision);
        }
        var done=s.DiagnosticSnapshot();o.WriteLine($"{(done.ElapsedSeconds)/3600}時間 {sw.ElapsedMilliseconds}ms 段階={string.Join("、",s.TruceView().Select(c=>c.Stage))}");
        var known=Assert.Single(TruceNegotiations.Known(done,s.PlayerControl));
        Assert.Equal(new[]{own,imagawa}.OrderBy(x=>x.Value),new[]{known.Terms.A,known.Terms.B}.OrderBy(x=>x.Value));
        Assert.True(TruceNegotiations.WillNotInitiate(done,imagawa,own,done.ElapsedSeconds,done.CommitSequence)||!TruceNegotiations.Known(done,new(HouseSuccession.Head(done,imagawa).Value)).Any(),"相手の当主は成立を知れば出陣しない");
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(done)));
        foreach(var x in new[]{s,restored})NewGameWorldTests.At(x,done.ElapsedSeconds+3600);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
    // 全国の開始の歴史モード：新規ゲームの画面で選んだモードを持ち、地方の版には付けられない。
    [Fact]
    public void TheNationalStartKeepsTheChosenHistoryMode()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga(),history:HistoryMode.Fictional);
        Assert.Equal(HistoryMode.Fictional,w.NewGame!.History);
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(ContractJson.Read<ContractWorld>(ContractJson.Write(w))));
        Assert.Null(NationalStart.Create(NationalStartTests.Nobunaga()).NewGame!.History);
        var start=NewGameWorldTests.Setup([NewGameWorldTests.Officer()]);
        Assert.Throws<InvalidDataException>(()=>NewGameWorld.Create(start.World,start.Registered,"created:test",0,history:HistoryMode.Historical));
    }
}
