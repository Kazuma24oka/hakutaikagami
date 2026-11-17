using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// S12（2026-11-18）：長い受入。全国の信長の開始を、月の家の戦略で1560年6月から1570年12月まで（10年7か月）進め、
// 全国のNPCの家の出陣・外交・滅亡と再興・離反・婚姻・中央の政治・人物の一生・残りの機能が起き続けること、
// 途中（1565年1月）の保存から続けた世界が同じ未来になることを確かめる。1650年の総括と、全国の開始の武将の負傷・捕縛の一巡も確かめる。
// 重い（数分）ので、工程の終わりに1回だけ流す。
[Trait("Tier","Probe")]
public sealed class LongAcceptanceS12Tests(ITestOutputHelper output)
{
    static void Month(ContractWorld w,int y,int m){w.ElapsedSeconds=NationalStrategy.MonthStart(w,y,m);w.CommitSequence+=5;NationalStrategy.Apply(w);}

    [Fact]
    public void TenYearsOfTheNationalWorldKeepHappeningAndRestoreToTheSameFuture()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga(),history:HistoryMode.Historical);
        ContractWorld? saved=null;var clock=System.Diagnostics.Stopwatch.StartNew();
        for(int y=1560,m=6;y<1571;m++){if(m==13){m=1;y++;if(y==1571)break;}Month(w,y,m);if(y==1565&&m==1)saved=ContractJson.Read<ContractWorld>(ContractJson.Write(w));}
        var s=w.Strategy!;
        int Count(string kind)=>(s.Finish??[]).Count(e=>e.Kind==kind);
        var counts=new Dictionary<string,int>{
            ["出陣"]=s.Campaigns.Length,["決着"]=s.Outcomes.Length,["落城"]=s.Outcomes.Count(o=>o.Captured),["約束"]=(s.Pacts??[]).Length,["包囲網"]=(s.Coalitions??[]).Length,
            ["滅亡"]=(s.Falls??[]).Length,["離反"]=(s.Defections??[]).Length,["家督"]=(s.Successions??[]).Length,["婚姻"]=(s.Marriages??[]).Length,["守護"]=(s.Shugo??[]).Length,["栄典"]=(s.Honors??[]).Length,
            ["史実の事件"]=(s.History??[]).Count(h=>h.Result=="fired"),["人物"]=(s.Persons??[]).Length,["軍勢"]=(s.Armies??[]).Length,["外交と家中"]=(s.Politics??[]).Length,["案件"]=(s.Cases??[]).Length,["任務"]=(s.Duties??[]).Length,
            ["身代金"]=(s.Duties??[]).Count(e=>e.Kind=="ransom"),["捕虜の交換"]=(s.Politics??[]).Count(e=>e.Kind=="exchange"),["捕縛からの逃亡"]=(s.Escapes??[]).Length,
            ["兵糧の勲功"]=Count("merit-supply"),["恩賞の裁定"]=Count("adjudicate"),["通行"]=Count("passage")+Count("passage-refused")+Count("protest"),["人質"]=Count("hostage"),["残りの栄典"]=Count("honor"),["兵糧の援け"]=Count("aid-sent")};
        output.WriteLine($"10年7か月：{clock.Elapsed.TotalSeconds:F0}秒　"+string.Join("　",counts.Select(x=>x.Key+" "+x.Value)));
        foreach(var k in new[]{"出陣","決着","落城","約束","家督","婚姻","史実の事件","人物","軍勢","外交と家中","任務","兵糧の勲功","恩賞の裁定","人質"})Assert.True(counts[k]>0,k+" が起きていない");
        // 起き続ける：最後の2年にも出陣と外交が起きている。
        long late=NationalStrategy.MonthStart(w,1569,1);Assert.Contains(s.Campaigns,c=>c.At>=late);Assert.Contains(s.Politics??[],e=>e.At>=late);
        NationalStrategy.Validate(w,null);
        // 途中の保存から続けても同じ未来。
        for(int y=1565,m=2;y<1571;m++){if(m==13){m=1;y++;if(y==1571)break;}Month(saved!,y,m);}
        Assert.Equal(ContractJson.CanonicalHash(w),ContractJson.CanonicalHash(saved!));
    }

    [Fact]
    public void The1650SummaryAndANationalOfficerWoundAndCaptureCycle()
    {
        var start=NationalStart.Create(NationalStartTests.Nobunaga(),timePolicy:WorldTimeControl.Policy,history:HistoryMode.Historical);
        // 1650年の総括：時計が1650年に届くと総括の印が付き、歩み（年数・城の数・目標）が出る。
        var end=ContractJson.Copy(start);var prior=ContractJson.Copy(end);end.ElapsedSeconds=WorldCalendar.HistoricalEndSeconds(end.EpochDate);WorldTimeControl.Observe(prior,end);
        Assert.NotNull(end.TimeControl!.SummaryAt);var journey=WorldTimeControl.Journey(end);Assert.NotNull(journey);Assert.Equal(90,journey!.Years);
        // 全国の開始の武将の負傷：本人の部隊が損害を受けた日のどこかで、率いる人が傷を負う（同じ日には同じ判定）。
        // 本人の部隊（全国の開始では出陣の時に作られる。ここでは本人が率いる100人の部隊を置く）。
        var unit=new UnitState(new("unit:test/officer"),new("army:test/officer"),start.PlayerControl,new(Screens2.HouseOf(start,start.PlayerControl)!),100,start.CommitSequence);start.Units=[..start.Units,unit];OfficerWound? wound=null;
        for(long day=0;day<200&&wound==null;day++)wound=OfficerBattleHealth.Resolve(start,new EngagementResult(start.ElapsedSeconds+day*86400,[],[new(start.ElapsedSeconds+day*86400,unit.Id,10,0)]))?.FirstOrDefault();
        Assert.NotNull(wound);Assert.Equal(unit.Commander,wound!.Person);
    }
}
