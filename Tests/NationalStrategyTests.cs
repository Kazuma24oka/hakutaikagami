using System.Globalization;
using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// 全国の家の戦略 V1（2026-11-12）：月の初めの出陣の判断・翌月の決着と城の支配・途中保存からの同じ未来。
[Trait("Tier","Heavy")]
public sealed class NationalStrategyTests(ITestOutputHelper o)
{
    static long Month(int year,int month)=>((long)new DateOnly(year,month,1).DayNumber-new DateOnly(1560,1,1).DayNumber)*86400;
    static string Takeda()=>NationalStart.Scenario().People.Single(p=>NationalStart.DbPerson(p.Id)?.Id=="P-005247").Id;

    [Fact]
    public void HousesDecideCampaignsMonthlyAndCapturesChangeControlWithTheSameFuture()
    {
        var w=NationalStart.Create(Takeda());
        w.ElapsedSeconds=Month(1560,5)-1;
        var session=new WorldSession(w);session.SetPaused(false);
        session.AdvanceTo(Month(1560,5)+1);
        var first=session.DiagnosticSnapshot();
        var campaigns=first.Strategy!.Campaigns;
        o.WriteLine($"出陣 {campaigns.Length}：{string.Join("、",campaigns.Take(8).Select(c=>c.Attacker.Value+"→"+c.Defender.Value+"("+c.Basis+")"))}");
        Assert.NotEmpty(campaigns);
        Assert.All(campaigns,c=>Assert.Equal(Month(1560,5),c.At));
        // 操作する人物（武田信玄）の家は攻めず、攻められない。
        var takeda=NationalStart.FactionId("G-000106");
        Assert.DoesNotContain(campaigns,c=>c.Attacker.Value==takeda||c.Defender.Value==takeda);
        // 桶狭間：準史実で前提（今川義元が当主・織田との境の城）がそろうので、不発ではなく、出陣したか当主が選ばなかったかのどちらか。
        {var imagawa=NationalStart.FactionId("G-000123");var oda=NationalStart.FactionId("G-000131");
         var own=first.NewGame!.Start.Castles.Where(c=>c.Faction==imagawa).Select(c=>c.Id).ToArray();
         var near=own.SelectMany(NationalStrategy.Neighbors).Distinct().Select(x=>first.NewGame.Start.Castles.SingleOrDefault(c=>c.Id==x)?.Faction).GroupBy(x=>x).Select(g=>g.Key+":"+g.Count());
         var head=HouseSuccession.Head(first,new(imagawa));
         o.WriteLine($"今川 城{own.Length} 隣 {string.Join(" ",near)} 当主 {head.Value} {NationalStart.DbPerson(head.Value)?.Id} 織田の城 {first.NewGame.Start.Castles.Count(c=>c.Faction==oda)}");}
        var okehazama=Assert.Single(first.Strategy.History!,h=>h.Event=="okehazama-1560");
        Assert.NotEqual("premise",okehazama.Result);
        Assert.Equal(okehazama.Result=="fired",campaigns.Any(c=>c.Basis=="history:okehazama-1560"));
        // 河内への出陣（三好→河内畠山）は10月なので、5月にはまだ記録がない。
        Assert.DoesNotContain(first.Strategy.History!,h=>h.Event=="takaya-1560");

        // 途中の保存から、翌月の決着まで同じ未来になる。
        var a=Jump(ContractJson.Copy(first));var b=Jump(ContractJson.Read<ContractWorld>(ContractJson.Write(first)));
        Assert.Equal(ContractJson.Write(a.Strategy),ContractJson.Write(b.Strategy));
        var outcomes=a.Strategy!.Outcomes;
        Assert.Equal(campaigns.Length,outcomes.Length);
        var taken=outcomes.Where(x=>x.Captured).Select(x=>a.Strategy.Campaigns.Single(c=>c.Id==x.Campaign)).ToArray();
        o.WriteLine($"落城 {taken.Length}/{outcomes.Length}");
        Assert.NotEmpty(taken);
        Assert.All(taken,c=>Assert.Equal(c.Attacker,NationalStrategy.Owner(a,c.Target,a.ElapsedSeconds,a.CommitSequence)));
        Assert.All(taken,c=>Assert.Equal(c.Defender,NationalStrategy.Owner(a,c.Target,Month(1560,5),long.MaxValue)));
        // 記録は書き換えられない（5月の世界で検査する）。
        ContractValidation.Validate(first);
        var tampered=ContractJson.Copy(first);tampered.Strategy=tampered.Strategy! with{Campaigns=[tampered.Strategy.Campaigns[0] with{Defender=tampered.Strategy.Campaigns[0].Attacker}]};
        Assert.Throws<InvalidDataException>(()=>ContractValidation.Validate(tampered));
    }
    // 次の月の初めの処理だけを直接当てる（全国の世界を1か月ふつうに進めると検査に30分かかるため）。
    static ContractWorld Jump(ContractWorld w)
    {
        w.ElapsedSeconds=Month(1560,6);w.CommitSequence++;NationalStrategy.Apply(w);return w;
    }
}
