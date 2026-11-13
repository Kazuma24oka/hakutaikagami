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
