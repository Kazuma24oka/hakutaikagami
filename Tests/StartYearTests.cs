using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// S03（2026-11-17）：1467年・1600年の全国開始。地図と移動網は1560年の城の網を共通に使い、家・人物・城の持ち主はその年の版。
[Trait("Tier","Heavy")]
public sealed class StartYearTests(ITestOutputHelper o)
{
    static string Head(int year,string name)
    {
        var s=NationalStart.Scenario(NationalStart.RevenueEdition,NationalSystems.ForYear(year));
        return s.Factions.Select(f=>f.Head).First(h=>s.People.Single(p=>p.Id==h).Name.Replace(" ","").Contains(name));
    }

    [Theory]
    [InlineData(1467,"山名")]
    [InlineData(1600,"徳川家康")]
    public void AnotherStartYearBuildsItsOwnWorldAndRereadsTheSame(int year,string head)
    {
        var w=NationalStart.Create(Head(year,head),year:year);
        Assert.Equal(year+"-01-01",w.EpochDate);Assert.Equal(year,NationalSystems.StartYear(w));
        Assert.Equal(year,WorldCalendar.At(w.EpochDate,w.ElapsedSeconds).Year);
        o.WriteLine($"{year}：家 {w.NewGame!.Start.Factions.Length}・人物 {w.NewGame.Start.People.Length}");
        Assert.True(w.NewGame.Start.Factions.Length>=50);
        Assert.All(w.NewGame.Start.People.Where(p=>NationalStart.DbPerson(p.Id) is {Birth:int}),p=>Assert.True(NationalStart.DbPerson(p.Id)!.Birth<=year));
        var again=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        var s=new WorldSession(w);
        foreach(var x in new[]{s,again})NewGameWorldTests.At(x,3600);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(again.DiagnosticSnapshot()));
    }

    [Fact]
    public void The1560StartIsUnchanged()
    {
        Assert.Equal(1560,NationalSystems.StartYear(NationalSystems.Current));
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.Equal("1560-01-01",w.EpochDate);Assert.DoesNotContain(w.NewGame!.NationalSystems!,x=>x.StartsWith("StartYear",StringComparison.Ordinal));
    }
}
