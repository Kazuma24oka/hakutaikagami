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
        var sw=System.Diagnostics.Stopwatch.StartNew();var w=NationalStartTests.BeforeRevenue(Takeda());o.WriteLine($"作成 {sw.ElapsedMilliseconds}ms");sw.Restart();
        w.ElapsedSeconds=Month(1560,5)-1;
        var session=new WorldSession(w);session.SetPaused(false);
        session.AdvanceTo(Month(1560,5)+1);
        var first=session.DiagnosticSnapshot();o.WriteLine($"5月の境 {sw.ElapsedMilliseconds}ms");sw.Restart();
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
        var a=Jump(ContractJson.Copy(first));o.WriteLine($"6月の処理 {sw.ElapsedMilliseconds}ms");sw.Restart();var b=Jump(ContractJson.Read<ContractWorld>(ContractJson.Write(first)));sw.Restart();
        Assert.Equal(ContractJson.Write(a.Strategy),ContractJson.Write(b.Strategy));
        var outcomes=a.Strategy!.Outcomes;
        Assert.Equal(campaigns.Length,outcomes.Length);
        var taken=outcomes.Where(x=>x.Captured).Select(x=>a.Strategy.Campaigns.Single(c=>c.Id==x.Campaign)).ToArray();
        o.WriteLine($"落城 {taken.Length}/{outcomes.Length}");
        // 遠方の合戦の計算を持たない世界（旧保存と同じ）は、強さの比のくじで決める。
        Assert.False(StrategyBattles.Enabled(a));Assert.All(outcomes,x=>Assert.Null(x.Battle));
        Assert.NotEmpty(taken);
        Assert.All(taken,c=>Assert.Equal(c.Attacker,NationalStrategy.Owner(a,c.Target,a.ElapsedSeconds,a.CommitSequence)));
        Assert.All(taken,c=>Assert.Equal(c.Defender,NationalStrategy.Owner(a,c.Target,Month(1560,5),long.MaxValue)));
        // 包囲網：同じ強い家を相手にした同盟が三つ以上重なると記録され、加わる家は三つ以上。
        foreach(var x in a.Strategy.Coalitions??[]){Assert.True(x.Members.Length>=3);Assert.DoesNotContain(x.Target,x.Members);}
        o.WriteLine($"包囲網 {(a.Strategy.Coalitions??[]).Length}");
        // 京の主：山城の城の過半を押さえる家が記録される（1560年は開始時の持ち主）。
        var capital=Assert.Single(first.Strategy.Capitals!);o.WriteLine($"京の主 {capital.House.Value}");
        Assert.True(first.NewGame!.Start.Castles.Count(c=>NationalStrategy.InKyoto(c.Id)&&c.Faction==capital.House.Value)*2>first.NewGame.Start.Castles.Count(c=>NationalStrategy.InKyoto(c.Id)));
        // 同盟・和睦の相手には、破約でない限り出陣しない。破約した約束には破られた時刻が残る。
        var june=a.Strategy.Campaigns.Where(c=>c.At==Month(1560,6)).ToArray();var pacts=a.Strategy.Pacts??[];
        o.WriteLine($"6月 出陣 {june.Length} 約束 {pacts.Length}（同盟 {pacts.Count(p=>p.Kind=="alliance")}・和睦 {pacts.Count(p=>p.Kind=="truce")}・破約 {pacts.Count(p=>p.Broken!=null)}）");
        Assert.NotEmpty(pacts);
        foreach(var c in june)
        {
            var between=pacts.Where(p=>p.A==c.Attacker&&p.B==c.Defender||p.A==c.Defender&&p.B==c.Attacker).ToArray();
            if(c.Basis=="betrayal")Assert.Contains(between,p=>p.Broken==Month(1560,6));else Assert.DoesNotContain(between,p=>p.Broken==null&&p.From<=c.At&&c.At<p.Until);
        }
        // 記録は書き換えられない（5月の世界で検査する）。
        ContractValidation.Validate(first);o.WriteLine($"検査 {sw.ElapsedMilliseconds}ms");
        var tampered=ContractJson.Copy(first);tampered.Strategy=tampered.Strategy! with{Campaigns=[tampered.Strategy.Campaigns[0] with{Defender=tampered.Strategy.Campaigns[0].Attacker}]};
        Assert.Throws<InvalidDataException>(()=>ContractValidation.Validate(tampered));
    }
    // 将軍家が城をすべて失うと翌月に空位、その翌月に京の主が将軍家の成人を擁立して山城の城を一つ渡す。
    [Fact]
    public void ShogunVacancyLeadsTheCapitalHolderToInstallAShogun()
    {
        var w=NationalStartTests.BeforeRevenue(Takeda());var shogun=NationalStart.FactionId(NationalStrategy.Shogun);
        var castles=w.NewGame!.Start.Castles.Where(c=>c.Faction==shogun).Select(c=>new EntityId(c.Id)).ToArray();
        Assert.NotEmpty(castles);
        w.ElapsedSeconds=Month(1560,5);w.CommitSequence++;NationalStrategy.Apply(w);
        Assert.Equal(shogun,NationalStrategy.Capital(w)!.Value.Value); // 1560年の京の主は将軍家
        var capital=new EntityId(NationalStart.FactionId("G-000199")); // 三好が将軍家の城をすべて落とした（記録を直接置く）
        var s=w.Strategy!;var campaigns=castles.Select((c,n)=>new StrategyCampaign(new($"campaign:test/{n}"),capital,new(shogun),c,w.ElapsedSeconds,w.CommitSequence,NationalStrategy.Border)).ToArray();
        w.ElapsedSeconds=Month(1560,6);w.CommitSequence++;
        w.Strategy=s with{Campaigns=[..s.Campaigns,..campaigns],Outcomes=[..s.Outcomes,..campaigns.Select(c=>new StrategyOutcome(c.Id,w.ElapsedSeconds,w.CommitSequence,true))]};
        w.CommitSequence++;NationalStrategy.Apply(w);
        Assert.Equal("vacant",w.Strategy!.Shoguns!.Last().Kind);Assert.NotEqual(shogun,NationalStrategy.Capital(w)!.Value.Value);
        Assert.Null(SupremeOffice.ShogunAt(w,w.ElapsedSeconds,w.CommitSequence)); // 京の将軍職も空く
        w.ElapsedSeconds=Month(1560,7);w.CommitSequence++;NationalStrategy.Apply(w);
        var installed=w.Strategy!.Shoguns!.Last();
        Assert.Equal("installed",installed.Kind);Assert.Equal(NationalStrategy.Capital(w),installed.Patron);
        Assert.Equal(installed.Shogun,SupremeOffice.ShogunAt(w,w.ElapsedSeconds,w.CommitSequence));
        Assert.True(NationalStrategy.InKyoto(installed.Site!.Value.Value));
        Assert.Equal(shogun,NationalStrategy.Owner(w,installed.Site.Value,w.ElapsedSeconds,long.MaxValue)!.Value.Value);
    }
    // 包囲網ができると、包囲する相手と境を接する操作する人物の家（当主）へ誘いが届き、開いた後に一度だけ答えられる。
    [Fact]
    public void ThePlayerHouseIsInvitedToACoalitionAndAnswersOnceAfterReading()
    {
        var w=NationalStartTests.BeforeRevenue(Takeda());var takeda=NationalStart.FactionId("G-000106");var imagawa=NationalStart.FactionId("G-000123");
        w.ElapsedSeconds=Month(1560,5);w.CommitSequence++;NationalStrategy.Apply(w);
        var others=w.NewGame!.Start.Factions.Select(f=>f.Id).Where(f=>f!=takeda&&f!=imagawa&&f!=NationalStart.FactionId(NationalStrategy.Shogun)).OrderBy(f=>f,StringComparer.Ordinal).Take(3).ToArray();
        long now=w.ElapsedSeconds;var s=w.Strategy!;
        var pacts=new[]{(others[0],others[1]),(others[1],others[2]),(others[0],others[2])}.Select((x,n)=>new StrategyPact(new($"pact:test/{n}"),new(x.Item1),new(x.Item2),"alliance",now,now+2*365*86400L,w.CommitSequence,"threat:"+imagawa)).ToArray();
        w.Strategy=s with{Pacts=[..s.Pacts??[],..pacts]};
        w.ElapsedSeconds=Month(1560,6);w.CommitSequence++;NationalStrategy.Apply(w);
        var coalition=Assert.Single(w.Strategy!.Coalitions!,c=>c.Target.Value==imagawa);
        var invite=Assert.Single(coalition.Invites!);
        Assert.Equal(takeda,invite.House.Value);Assert.Null(invite.Joined);
        var letter=w.Messaging.Messages.Single(m=>m.Id==invite.Letter.Value);
        Assert.Equal(NationalStrategy.InviteKind,letter.Kind);Assert.Equal(w.PlayerControl.Value,letter.RecipientId);
        Assert.Empty(NationalStrategy.OpenInvites(w)); // 開く前は答えられない
        Assert.Throws<InvalidDataException>(()=>NationalStrategy.Answer(ContractJson.Copy(w),invite.Letter,true));
        w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(letter.Id,w.PlayerControl.Value,now,letter.Id)]; // 開いたことにする
        Assert.Single(NationalStrategy.OpenInvites(w));
        int before=w.Strategy!.Pacts!.Length;w.CommitSequence++;NationalStrategy.Answer(w,invite.Letter,true);
        var joined=w.Strategy!.Coalitions!.Single(c=>c.Id==coalition.Id);
        Assert.Contains(new EntityId(takeda),joined.Members);Assert.True(joined.Invites!.Single().Joined);
        Assert.Equal(before+coalition.Members.Length,w.Strategy.Pacts!.Length);Assert.All(w.Strategy.Pacts.Skip(before),p=>{Assert.Equal(takeda,p.B.Value);Assert.Equal("alliance",p.Kind);});
        var reply=w.Messaging.Messages.Single(m=>m.Kind==NationalStrategy.InviteReplyKind);Assert.Equal(invite.Sender.Value,reply.RecipientId);
        Assert.Empty(NationalStrategy.OpenInvites(w)); // 答えは一度だけ
        Assert.Throws<InvalidDataException>(()=>NationalStrategy.Answer(w,invite.Letter,false));
        Assert.Equal(ContractJson.Write(w.Strategy),ContractJson.Write(ContractJson.Read<StrategyState>(ContractJson.Write(w.Strategy)))); // 保存して戻しても同じ
    }
    // 次の月の初めの処理だけを直接当てる（全国の世界を1か月ふつうに進めると検査に30分かかるため）。
    // 遠方の合戦（W4）：決着を実際の合戦と同じ接触の規則で計算して記録し、落城は守る側が崩れたときだけ。保存から戻して同じ。
    [Fact]
    public void DistantBattlesUseTheContactRulesAndRestoreIdentically()
    {
        var w=NationalStartTests.BeforeRevenue(Takeda(),["StrategyBattle"]);w.ElapsedSeconds=Month(1560,5)-1;
        var session=new WorldSession(w);session.SetPaused(false);session.AdvanceTo(Month(1560,5)+1);
        var first=session.DiagnosticSnapshot();Assert.True(StrategyBattles.Enabled(first));Assert.NotEmpty(first.Strategy!.Campaigns);
        var a=Jump(ContractJson.Copy(first));var b=Jump(ContractJson.Read<ContractWorld>(ContractJson.Write(first)));
        Assert.Equal(ContractJson.Write(a.Strategy),ContractJson.Write(b.Strategy));
        var outcomes=a.Strategy!.Outcomes;Assert.Equal(first.Strategy.Campaigns.Length,outcomes.Length);
        Assert.All(outcomes,x=>{Assert.NotNull(x.Battle);if(x.Captured)Assert.True(StrategyBattles.Falls(x.Battle!));});
        foreach(var x in outcomes)o.WriteLine($"合戦 {x.Battle!.AttackerSoldiers}対{x.Battle.DefenderSoldiers} 士気{x.Battle.AttackerMorale}/{x.Battle.DefenderMorale} 損害{x.Battle.AttackerLost}/{x.Battle.DefenderLost} {x.Battle.Seconds}秒 落城={x.Captured}");
        // 月を直接進めた世界なので、戦略の記録の検証だけを行う（保存から戻して同じことは上の比較で確かめた）。
        NationalStrategy.Validate(ContractJson.Read<ContractWorld>(ContractJson.Write(a)),null);
        var forged=ContractJson.Read<ContractWorld>(ContractJson.Write(a));
        forged.Strategy=forged.Strategy! with{Outcomes=forged.Strategy.Outcomes.Select(x=>x with{Battle=x.Battle! with{DefenderLost=x.Battle.DefenderLost+1}}).ToArray()};
        Assert.Throws<InvalidDataException>(()=>NationalStrategy.Validate(forged,null));
    }
    // 家の滅亡と再興：最後の城を落とされた家を記録し、旧城の家の支配が揺らぐと滅んだ家の当主が旧臣で挙兵する。
    [Fact]
    public void AFallenHouseIsRecordedAndItsLivingHeadRisesWhenTheHolderIsShaken()
    {
        var w=NationalStartTests.BeforeRevenue(Takeda(),["StrategyBattle","StrategyRestoration"]);
        var takeda=NationalStart.FactionId("G-000106");var shogun=NationalStart.FactionId(NationalStrategy.Shogun);
        var castles=w.NewGame!.Start.Castles;var counts=castles.Where(c=>c.Faction!=null).GroupBy(c=>c.Faction!).ToDictionary(g=>g.Key,g=>g.Count());
        bool Adult(string f)=>w.Characters.SingleOrDefault(c=>c.Id==HouseSuccession.Head(w,new(f)))?.Life==LifeState.Adult;
        var pick=castles.Where(c=>c.Faction is {} f&&counts[f]==1&&f!=takeda&&f!=shogun&&Adult(f)).OrderBy(c=>c.Id,StringComparer.Ordinal)
            .SelectMany(c=>NationalStrategy.Neighbors(c.Id).Select(n=>(Castle:c,Holder:castles.SingleOrDefault(x=>x.Id==n)?.Faction)))
            .First(x=>x.Holder is {} h&&h!=x.Castle.Faction&&h!=takeda&&h!=shogun&&NationalStrategy.Cap(x.Castle.Faction!)>0&&NationalStrategy.Cap(h)>=4*NationalStrategy.Cap(x.Castle.Faction!)&&Adult(h));
        string fallen=pick.Castle.Faction!,holder=pick.Holder!;var site=new EntityId(pick.Castle.Id);var takedaCastle=new EntityId(castles.First(c=>c.Faction==takeda).Id);
        o.WriteLine($"滅ぶ家 {fallen}（兵{NationalStrategy.Cap(fallen)}）城 {site.Value} 攻める家 {holder}（兵{NationalStrategy.Cap(holder)}）");
        // 5月の初めに出陣を置き、6月の初めの決着で落城・滅亡。
        w.ElapsedSeconds=Month(1560,5);w.CommitSequence++;
        var s=w.Strategy??new StrategyState(1,NationalStrategy.Policy,[],[]);
        var attack=new StrategyCampaign(new("campaign:test/fall"),new(holder),new(fallen),site,w.ElapsedSeconds,w.CommitSequence,NationalStrategy.Border);
        w.Strategy=s with{Campaigns=[..s.Campaigns,attack]};
        Advance(w,1560,6);
        var fall=Assert.Single(w.Strategy!.Falls!,f=>f.House.Value==fallen);
        Assert.Equal(site,fall.Site);Assert.Equal(holder,fall.By.Value);
        Assert.Equal(holder,NationalStrategy.Owner(w,site,w.ElapsedSeconds,w.CommitSequence)!.Value.Value);
        // 支配が揺らがなければ挙兵しない。揺らいだ後は月に30%で挙兵する（2年まで見る）。
        Assert.DoesNotContain(w.Strategy.Campaigns,c=>c.Basis==NationalStrategy.Restoration);
        {var st=w.Strategy;var lost=new StrategyCampaign(new("campaign:test/holder-defeat"),new(holder),new(takeda),takedaCastle,Month(1560,5),w.CommitSequence,NationalStrategy.Border);
         w.Strategy=st with{Campaigns=[..st.Campaigns,lost],Outcomes=[..st.Outcomes,new(lost.Id,Month(1560,6),w.CommitSequence,false)]};}
        StrategyCampaign? rise=null;
        long previous=Month(1560,6);
        for(int m=7;m<=30&&rise==null;m++){Advance(w,1560+(m-1)/12,(m-1)%12+1);rise=w.Strategy!.Campaigns.SingleOrDefault(c=>c.Basis==NationalStrategy.Restoration);
            if(rise==null){var st=w.Strategy!;var again=new StrategyCampaign(new("campaign:test/holder-defeat/"+m),new(holder),new(takeda),takedaCastle,previous,w.CommitSequence,NationalStrategy.Border);w.Strategy=st with{Campaigns=[..st.Campaigns,again],Outcomes=[..st.Outcomes,new(again.Id,w.ElapsedSeconds,w.CommitSequence,false)]};}previous=w.ElapsedSeconds;}
        Assert.NotNull(rise);o.WriteLine($"挙兵 {rise.At}");
        Assert.Equal(fallen,rise.Attacker.Value);Assert.Equal(holder,rise.Defender.Value);Assert.Equal(site,rise.Target);
        var date=WorldCalendar.At(w.EpochDate,w.ElapsedSeconds);Advance(w,date.Month==12?date.Year+1:date.Year,date.Month%12+1);
        var outcome=Assert.Single(w.Strategy!.Outcomes,x=>x.Campaign==rise.Id);
        Assert.Equal(StrategyRestoration.Strength(fallen),outcome.Battle!.AttackerSoldiers);
        o.WriteLine($"再興の合戦 {outcome.Battle.AttackerSoldiers}対{outcome.Battle.DefenderSoldiers} 落城={outcome.Captured}");
        // 記録の検証（月を直接進めた世界なので戦略の記録だけ）。滅んでいない家の再興の挙兵は読まない。
        NationalStrategy.Validate(ContractJson.Read<ContractWorld>(ContractJson.Write(w)),null);
        var forged=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        forged.Strategy=forged.Strategy! with{Falls=[]};
        Assert.Throws<InvalidDataException>(()=>NationalStrategy.Validate(forged,null));
    }
    // 従属の離反：主家の支配が揺らぐと、主家の半分以上の強さの従う家は月に10%で離れる。揺らがなければ離れない。
    [Fact]
    public void AStrongVassalLeavesAShakenOverlordAndOnlyThen()
    {
        var w=NationalStartTests.BeforeRevenue(Takeda(),["StrategyDefection"]);
        var takeda=NationalStart.FactionId("G-000106");var factions=w.NewGame!.Start.Factions.Select(f=>f.Id).ToHashSet();
        var castles=w.NewGame.Start.Castles;var takedaCastle=new EntityId(castles.First(c=>c.Faction==takeda).Id);
        bool Adult(string f)=>w.Characters.SingleOrDefault(c=>c.Id==HouseSuccession.Head(w,new(f)))?.Life==LifeState.Adult;
        var pair=factions.OrderBy(f=>f,StringComparer.Ordinal).Select(h=>(House:h,Overlord:NationalStrategy.DbOverlord(h)))
            .First(x=>x.Overlord is {} o&&factions.Contains(o)&&x.House!=takeda&&o!=takeda&&Adult(x.House)&&castles.Any(c=>c.Faction==x.House)&&castles.Any(c=>c.Faction==o)&&
                NationalStrategy.Cap(o)>0&&NationalStrategy.Cap(x.House)*2>=NationalStrategy.Cap(o));
        string house=pair.House,overlord=pair.Overlord!;o.WriteLine($"従う家 {house}（兵{NationalStrategy.Cap(house)}） 主家 {overlord}（兵{NationalStrategy.Cap(overlord)}）");
        // 揺らがない間は離れない。
        for(int m=5;m<=8;m++)Advance(w,1560,m);
        Assert.Empty(w.Strategy?.Defections??[]);
        // 主家が毎月負けを重ねる（3か月以内の負け）と、月に10%で離れる。
        long previous=Month(1560,7);StrategyDefection? left=null;
        for(int m=9;m<=60&&left==null;m++)
        {
            var st=w.Strategy!;var loss=new StrategyCampaign(new("campaign:test/overlord-defeat/"+m),new(overlord),new(takeda),takedaCastle,previous,w.CommitSequence,NationalStrategy.Border);
            w.Strategy=st with{Campaigns=[..st.Campaigns,loss],Outcomes=[..st.Outcomes,new(loss.Id,w.ElapsedSeconds,w.CommitSequence,false)]};
            previous=w.ElapsedSeconds;Advance(w,1560+(m-1)/12,(m-1)%12+1);
            left=(w.Strategy!.Defections??[]).SingleOrDefault(d=>d.House.Value==house);
        }
        Assert.NotNull(left);Assert.Equal(overlord,left.From.Value);o.WriteLine($"離反 {left.At}");
        NationalStrategy.Validate(ContractJson.Read<ContractWorld>(ContractJson.Write(w)),null);
        var forged=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        forged.Strategy=forged.Strategy! with{Defections=[..forged.Strategy.Defections!,new(new(overlord),new(house),left.At,left.Revision)]};
        Assert.Throws<InvalidDataException>(()=>NationalStrategy.Validate(forged,null));
    }
    static void Advance(ContractWorld w,int year,int month){w.ElapsedSeconds=Month(year,month);w.CommitSequence++;NationalStrategy.Apply(w);}
    static ContractWorld Jump(ContractWorld w)
    {
        w.ElapsedSeconds=Month(1560,6);w.CommitSequence++;NationalStrategy.Apply(w);return w;
    }
}
