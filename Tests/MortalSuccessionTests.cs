using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S02（2026-11-17）：危篤・意思表示不能、家督争いの決着と兄弟姉妹の家督、遺産の相続、本人の手当の医術と後遺症、操作の引き継ぎの兄弟姉妹。
// 一覧（NationalSystems）に MortalSuccession のない世界では、どれも前のまま。
[Trait("Tier","Heavy")]
public sealed class MortalSuccessionTests
{
    static string Person(string name)=>NationalStart.Scenario().People.First(p=>p.Name.Replace(" ","")==name).Id;
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=MortalSuccession.Name).ToArray()};
        return old;
    }
    static void Kill(ContractWorld w,EntityId person,long at)
    {
        w.NaturalDeaths=[..w.NaturalDeaths??[],new(person,at,w.CommitSequence)];
        w.Characters=w.Characters.Select(c=>c.Id==person?c with{Life=LifeState.Dead}:c).ToArray();
    }

    [Fact]
    public void GravelyIllPeopleAreCriticalAndCannotDecideUntilTheCrisis()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        HealthCondition? grave=null;
        foreach(var p in FamilyBirth.People(w).Select(p=>new EntityId(p.Id)).Take(3000))
        {
            for(long k=0;k<12&&grave==null;k++)if(PersonalHealth.Illness(w,p,k) is {Severity:>=3} c)grave=c;
            if(grave!=null)break;
        }
        Assert.NotNull(grave);
        long crisis=HealthMortality.CrisisAt(grave);
        Assert.True(MortalSuccession.Critical(w,grave.Person,grave.OnsetAt+3600));
        Assert.True(MortalSuccession.Incapacitated(w,grave.Person,grave.OnsetAt+3600));
        Assert.False(MortalSuccession.Critical(w,grave.Person,crisis));
        Assert.False(MortalSuccession.Critical(w,grave.Person,grave.OnsetAt-1));
    }

    [Fact]
    public void ANpcHouseWithoutAdultChildrenPassesToAnAdultSiblingFromTheRecords()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var head=new EntityId(Person("斎藤義龍"));var faction=w.NewGame!.Start.Factions.Single(f=>f.Head==head.Value);
        Kill(w,head,w.ElapsedSeconds);
        var pick=MortalSuccession.Successor(w,new(faction.Id),head,faction.Id);
        Assert.NotNull(pick);Assert.Null(pick.Value.Dispute);
        Assert.Contains(pick.Value.Heir,MortalSuccession.Siblings(w,head));
        Assert.Equal(faction.Id,w.NewGame.Start.People.Single(p=>p.Id==pick.Value.Heir.Value).Faction);
    }

    [Fact]
    public void TwoClaimsAreSettledByTheRetainersAndTheLoserStaysInTheHouse()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var head=new EntityId(Person("龍造寺隆信"));var faction=w.NewGame!.Start.Factions.Single(f=>f.Head==head.Value);var house=new EntityId(faction.Id);
        var kin=MortalSuccession.Siblings(w,head).Where(s=>w.Characters.Single(c=>c.Id==s).Life==LifeState.Adult&&w.NewGame.Start.People.Single(p=>p.Id==s.Value).Faction==faction.Id).Take(2).ToArray();
        Assert.Equal(2,kin.Length);
        Kill(w,head,w.ElapsedSeconds);
        w.HouseClaims=[new(house,head,kin[0],w.ElapsedSeconds,1001),new(house,head,kin[1],w.ElapsedSeconds,1002)];
        var retainers=w.NewGame.Start.People.Where(p=>p.Faction==faction.Id&&!kin.Any(k=>k.Value==p.Id)&&p.Id!=head.Value).Take(3).Select(p=>new EntityId(p.Id)).ToArray();
        w.ClaimOpinions=[new(1002,retainers[0],true,w.ElapsedSeconds,1003),new(1002,retainers[1],true,w.ElapsedSeconds,1004),new(1001,retainers[2],true,w.ElapsedSeconds,1005)];
        var pick=MortalSuccession.Successor(w,house,head,faction.Id)!.Value;
        Assert.Equal(kin[1],pick.Heir);
        Assert.Equal("家臣の意向",pick.Dispute!.Basis);
        Assert.Equal(kin.OrderBy(k=>k.Value,StringComparer.Ordinal).ToArray(),pick.Dispute.Claimants);
        Assert.Equal(faction.Id,w.NewGame.Start.People.Single(p=>p.Id==kin[0].Value).Faction);
    }

    [Fact]
    public void TheDeadPersonsPurseGoesToTheEldestAdultChildOnly()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var dead=new EntityId(Person("武田晴信"));
        var heir=MortalSuccession.EstateHeir(w,dead,w.ElapsedSeconds,w.CommitSequence+1);
        Assert.NotNull(heir);Assert.Equal("成人の実子",heir.Value.Basis);
        Assert.Equal(new EntityId(Person("武田義信")),heir.Value.Heir);
        long at=w.ElapsedSeconds+60;long purse=TransportWork.WalletBefore(w,dead,at,w.CommitSequence+1);long before=TransportWork.WalletBefore(w,heir.Value.Heir,at+1,w.CommitSequence+1);
        Assert.True(purse>0);
        w.ElapsedSeconds=at;Kill(w,dead,at);
        Assert.Equal(before+purse,TransportWork.WalletBefore(w,heir.Value.Heir,at+1,w.CommitSequence+1));
        Assert.Equal(0,TransportWork.WalletBefore(w,dead,at+1,w.CommitSequence+1));
        Assert.Contains(MortalSuccession.Estates(w,heir.Value.Heir),e=>e.Deceased==dead&&e.Amount==purse);
        // 一覧に MortalSuccession のない世界では移らない。
        var old=Without(w);
        Assert.Equal(before,TransportWork.WalletBefore(old,heir.Value.Heir,at+1,old.CommitSequence+1));
    }

    [Fact]
    public void APlayerWithoutChildrenContinuesAsAnAdultSiblingAndOldListsEndTheChronicle()
    {
        var w=NationalStart.Create(Person("宇喜多直家"));
        Kill(w,w.PlayerControl,w.ElapsedSeconds);
        Assert.True(ControlSuccession.Awaiting(w));
        var c=ControlSuccession.Candidates(w);
        Assert.NotEmpty(c);Assert.All(c,x=>Assert.Contains(x,MortalSuccession.Siblings(w,w.PlayerControl)));
        var old=Without(w);
        Assert.Empty(ControlSuccession.Candidates(old));Assert.True(ControlSuccession.Ended(old));
        Assert.Null(MortalSuccession.End(old));
    }

    [Fact]
    public void TheChronicleEndSummarizesTheDeathAndTheHouse()
    {
        var w=NationalStart.Create(Person("宇喜多直家"));
        foreach(var s in MortalSuccession.Siblings(w,w.PlayerControl))Kill(w,s,w.ElapsedSeconds);
        Kill(w,w.PlayerControl,w.ElapsedSeconds);
        var e=MortalSuccession.End(w);
        Assert.NotNull(e);Assert.Equal(w.ElapsedSeconds,e.DiedAt);Assert.NotNull(e.House);Assert.True(e.Castles>0);
    }

    [Fact]
    public void SevereWoundsLeaveALongerAftereffectOnlyInTheNewList()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(MortalSuccession.On(w));Assert.False(MortalSuccession.On(Without(w)));
        // 重傷が治った後：7日までは +15、その後180日までは新しい一覧の世界だけ +10。
        long r=1000L*86400;
        Assert.Equal(15,ClinicalHealth.AftereffectOf(true,r,r+86400));Assert.Equal(15,ClinicalHealth.AftereffectOf(false,r,r+86400));
        Assert.Equal(10,ClinicalHealth.AftereffectOf(true,r,r+8*86400));Assert.Equal(0,ClinicalHealth.AftereffectOf(false,r,r+8*86400));
        Assert.Equal(0,ClinicalHealth.AftereffectOf(true,r,r+181*86400));Assert.Equal(0,ClinicalHealth.AftereffectOf(true,r,r-1));
    }

    // 月の初めの家の戦略の手番が、兄弟姉妹の家督と家督争いの決着を記録し、写しから読み直しても同じになる。
    [Fact]
    public void TheMonthlyTurnRecordsTheSuccessionAndTheDisputeAndRereadsTheSame()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var d=WorldCalendar.At(w.EpochDate,w.ElapsedSeconds);long month=NationalStrategy.MonthStart(w,d.Month==12?d.Year+1:d.Year,d.Month==12?1:d.Month+1);
        var saito=new EntityId(Person("斎藤義龍"));var ryuzoji=new EntityId(Person("龍造寺隆信"));
        var fs=w.NewGame!.Start.Factions.Single(f=>f.Head==saito.Value);var fr=w.NewGame.Start.Factions.Single(f=>f.Head==ryuzoji.Value);
        var kin=MortalSuccession.Siblings(w,ryuzoji).Where(s=>w.NewGame.Start.People.Single(p=>p.Id==s.Value).Faction==fr.Id&&w.Characters.Single(c=>c.Id==s).Life==LifeState.Adult).Take(2).ToArray();
        w.ElapsedSeconds=month-86400;w.CommitSequence+=10;
        Kill(w,saito,w.ElapsedSeconds);Kill(w,ryuzoji,w.ElapsedSeconds);
        w.ElapsedSeconds=month;
        var claimsAt=w.CommitSequence;
        w.HouseClaims=[new(new(fr.Id),ryuzoji,kin[0],month-86400,claimsAt-2),new(new(fr.Id),ryuzoji,kin[1],month-86400,claimsAt-1)];
        var prior=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        NationalStrategy.Apply(w);
        var rows=w.Strategy!.Successions!;
        Assert.Contains(rows,x=>x.House.Value==fs.Id&&x.Previous==saito&&MortalSuccession.Siblings(w,saito).Contains(x.Heir));
        Assert.Contains(w.Strategy.Disputes!,x=>x.House.Value==fr.Id&&x.Claimants.Length==2&&rows.Any(r=>r.House==x.House&&r.Heir==x.Winner));
        NationalStrategy.Validate(w,prior);
        var again=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(again));
        // 一覧に MortalSuccession のない世界では、家督争いを記録しない。
        var old=Without(prior);NationalStrategy.Apply(old);
        Assert.Null(old.Strategy?.Disputes);
    }
}
