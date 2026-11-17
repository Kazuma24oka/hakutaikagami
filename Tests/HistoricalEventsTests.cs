using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S03（2026-11-17）：史実の事件の第2版（1467年〜1615年の出陣と史実の同盟）と、その噂（遅れて届き、誤報がある）。
[Trait("Tier","Heavy")]
public sealed class HistoricalEventsTests
{
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalStrategy.Events2).ToArray()};
        return old;
    }
    static string Head(int year,string name)
    {
        var s=NationalStart.Scenario(NationalStart.RevenueEdition,NationalSystems.ForYear(year));
        return s.Factions.Select(f=>f.Head).First(h=>s.People.Single(p=>p.Id==h).Name.Replace(" ","").Contains(name));
    }
    static void Month(ContractWorld w,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;NationalStrategy.Apply(w);}

    [Fact]
    public void ThereAreAtLeastFiftyEventsFrom1467To1615WithSourcesAndKnownHouses()
    {
        Assert.True(NationalStrategy.EventCount>=50);
        var all=NationalStrategy.History.Concat(NationalStrategy.History2).ToArray();
        Assert.Equal(all.Length,all.Select(e=>e.Id).Distinct().Count());
        Assert.Contains(all,e=>e.Year==1467);Assert.Contains(all,e=>e.Year==1615);
        Assert.All(all,e=>Assert.StartsWith("通説：",e.Source));
        // 家はどれかの開始年の版にある。
        var houses=HakutaiDb.StartYears.SelectMany(y=>HakutaiDb.Era(y).HouseList).Select(h=>h.Id).ToHashSet();
        Assert.All(all,e=>{Assert.Contains(e.Attacker,houses);Assert.Contains(e.Defender,houses);});
        Assert.All(NationalStrategy.Alliances,a=>{Assert.Contains(a.A,houses);Assert.Contains(a.B,houses);});
    }

    [Fact]
    public void The1467WarIsJudgedHeardLaterAndOldListsKeepTheFirstEdition()
    {
        var w=NationalStart.Create(Head(1467,"畠山"),history:HistoryMode.Historical,year:1467);
        var old=Without(w);
        Month(w,1467,5);
        var rec=w.Strategy!.History!.Single(h=>h.Event=="kamigyo-1467");
        Assert.Contains(rec.Result,new[]{"fired","premise","declined"});
        Month(old,1467,5);
        Assert.DoesNotContain(old.Strategy?.History??[],h=>h.Event=="kamigyo-1467");
        if(rec.Result!="fired")return;
        Month(w,1467,6);
        var o=w.Strategy!.Outcomes.Single(x=>w.Strategy.Campaigns.Single(c=>c.Id==x.Campaign).Basis=="history:kamigyo-1467");
        var heard=HistoricalNews.Heard(w,w.PlayerControl,o.At+400L*86400).Single(r=>r.Event=="kamigyo-1467");
        Assert.True(heard.LearnedAt>=o.At+86400);
        Assert.DoesNotContain(HistoricalNews.Heard(w,w.PlayerControl,o.At),r=>r.Event=="kamigyo-1467");
    }

    [Fact]
    public void AHistoricalAllianceFormsOnlyWhenItsPremiseHoldsAndNeverInTheFictionalMode()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga(),history:HistoryMode.Historical);
        // 信長は操作する人物なので、清洲同盟は前提が崩れる（本人の家を自動で結ばない）。
        Month(w,1562,1);
        Assert.Equal("premise",w.Strategy!.History!.Single(h=>h.Event=="kiyosu-1562").Result);
        Assert.DoesNotContain(w.Strategy.Pacts??[],p=>p.Basis=="history:kiyosu-1562");
        var f=NationalStart.Create(Head(1560,"武田"),history:HistoryMode.Fictional);
        Month(f,1562,1);
        Assert.Equal("declined",f.Strategy!.History!.Single(h=>h.Event=="kiyosu-1562").Result);
        var h=NationalStart.Create(Head(1560,"武田"),history:HistoryMode.Historical);
        Month(h,1562,1);
        var r=h.Strategy!.History!.Single(x=>x.Event=="kiyosu-1562").Result;
        Assert.Equal(r=="fired",(h.Strategy.Pacts??[]).Any(p=>p.Basis=="history:kiyosu-1562"&&p.Kind=="alliance"));
        if(r=="fired")
        {
            var rumors=HistoricalNews.Heard(h,h.PlayerControl,h.ElapsedSeconds+200L*86400);
            Assert.Contains(rumors,x=>x.Event=="kiyosu-1562");
            h.ElapsedSeconds+=200L*86400;
            var rows=HistoricalNews.ChronicleRows(h,h.PlayerControl).ToArray();
            Assert.Contains(rows,c=>c.Kind=="噂"&&c.About(NationalStart.FactionId("G-000126"))&&c.About(NationalStart.FactionId("G-000131")));
        }
    }

    [Fact]
    public void RumorsAreMisreportedAtAFixedRateAndAreTheSameAfterRereading()
    {
        Assert.Equal(150,HistoricalNews.MisreportPerMille);
        var w=NationalStart.Create(Head(1560,"武田"),history:HistoryMode.Historical);
        Month(w,1562,1);
        var a=HistoricalNews.Heard(w,w.PlayerControl,w.ElapsedSeconds+300L*86400);
        var again=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        Assert.Equal(ContractJson.Write(a),ContractJson.Write(HistoricalNews.Heard(again,again.PlayerControl,again.ElapsedSeconds+300L*86400)));
    }
}
