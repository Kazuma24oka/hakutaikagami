using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S04（2026-11-17）：京の政治の効果（全国の守護・将軍の栄典と偏諱・守護の実権・名乗りと席次・噂・将軍の継承）と仲裁の後始末。
[Trait("Tier","Heavy")]
public sealed class CentralStandingTests
{
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.CentralStanding).ToArray()};
        return old;
    }
    static void Month(ContractWorld w,int year,int month){w.ElapsedSeconds=NationalStrategy.MonthStart(w,year,month);w.CommitSequence+=5;NationalStrategy.Apply(w);}
    static void Kill(ContractWorld w,EntityId p){w.NaturalDeaths=[..w.NaturalDeaths??[],new(p,w.ElapsedSeconds,w.CommitSequence)];w.Characters=w.Characters.Select(c=>c.Id==p?c with{Life=LifeState.Dead}:c).ToArray();}

    [Fact]
    public void TheShogunAppointsProvincialGovernorsWhoseNewsTravelsFromKyoto()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());var old=Without(w);
        Month(w,1560,2);Month(old,1560,2);
        Assert.Null(old.Strategy?.Shugo);
        var rows=w.Strategy!.Shugo!;Assert.NotEmpty(rows);
        Assert.All(rows,r=>Assert.True(CentralStanding.IsShugo(w,r.House.Value,r.Province,w.ElapsedSeconds)));
        // 尾張以外の守護を、信長は京から届く噂で遅れて知る。
        var far=rows.First(r=>r.Head!=w.PlayerControl);
        Assert.DoesNotContain(CentralStanding.KnownTitles(w,w.PlayerControl,far.Head),t=>t==far.Province+"守護");
        w.ElapsedSeconds+=120L*86400;
        Assert.Contains(far.Province+"守護",CentralStanding.KnownTitles(w,w.PlayerControl,far.Head));
        Assert.Contains(CentralStanding.Heard(w,w.PlayerControl,w.ElapsedSeconds),h=>h.Person==far.Head&&h.LearnedAt>far.At);
    }

    [Fact]
    public void AGovernorGetsSmallHousesOfTheProvinceOnItsSideAndTheyAreWeaker()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());Month(w,1560,2);
        var owners=NationalStrategy.Owners(w,w.Strategy);var power=NationalStrategy.Powers(w,owners);
        bool any=false;
        foreach(var r in w.Strategy!.Shugo!)
        {
            var target=owners.FirstOrDefault(x=>x.Value!=null&&x.Value!=r.House.Value&&CentralStanding.ProvinceOf(new(x.Key))==r.Province);
            if(target.Key==null)continue;
            var c=new StrategyCampaign(new("campaign:test"),r.House,new(target.Value!),new(target.Key),w.ElapsedSeconds,w.CommitSequence,"border");
            var join=CentralStanding.ShugoSupport(w,c,owners,h=>power.GetValueOrDefault(h,50),[]);
            Assert.All(join,j=>{Assert.Equal(r.Province,owners.Where(x=>x.Value==j.Value).Select(x=>CentralStanding.ProvinceOf(new(x.Key))).First(p=>p==r.Province));Assert.True(power.GetValueOrDefault(j.Value,50)*3<power.GetValueOrDefault(r.House.Value,50));});
            var notShugo=c with{Attacker=new(target.Value!),Defender=r.House};
            Assert.Empty(CentralStanding.ShugoSupport(w,notShugo,owners,h=>power.GetValueOrDefault(h,50),[]));
            any|=join.Length>0;
        }
        Assert.True(any,"守護の国の小さな家が加勢する例がある");
    }

    [Fact]
    public void TheShogunGrantsHonorsInKyotoAndANameCharacterThatShowInTitlesAndSeating()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var kyoto=CentralCourtCatalog.KyotoIn(w);var shogun=SupremeOffice.ShogunAt(w,0,0)!.Value;
        Assert.Equal(kyoto,SocialWorld.Site(w,shogun));
        // 六角の当主を京へ置き、位階を従五位下以上とみなせる人を探す（1560年の在職）。
        var guest=w.NewGame!.Start.Factions.Select(f=>new EntityId(f.Head)).First(h=>h!=w.PlayerControl&&h!=shogun&&CentralTitles.FormalRank(w,h,0,1)<=CentralTitles.Titles.Single(t=>t.Kind=="rank"&&t.Name=="従五位下").Order);
        w.Locations=w.Locations.Select(l=>l.Actor==guest?l with{Kind=LocationKind.AtSite,Site=kyoto,Route=null}:l).ToArray();
        Month(w,1560,2);
        var honors=w.Strategy!.Honors!;
        Assert.Contains(honors,h=>h.Person==guest&&h.Kind==CentralStanding.Companion);
        Assert.Contains(CentralStanding.Companion,CentralStanding.KnownTitles(w,guest,guest));
        var seat=CentralStanding.SeatAtSite(w,guest);Assert.NotNull(seat);Assert.True(seat.Value.Seat>=1&&seat.Value.Seat<=seat.Value.Of);
        var order=CentralStanding.Seating(w,[guest,w.PlayerControl]);
        Assert.True(CentralTitles.FormalRank(w,order[0],w.ElapsedSeconds,w.CommitSequence+1)<=CentralTitles.FormalRank(w,order[1],w.ElapsedSeconds,w.CommitSequence+1));
    }

    [Fact]
    public void WhenTheShogunDiesAnAdultOfTheShogunHouseSucceeds()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var shogun=SupremeOffice.ShogunAt(w,0,0)!.Value;
        w.ElapsedSeconds=NationalStrategy.MonthStart(w,1560,2)-86400;Kill(w,shogun);
        Month(w,1560,2);
        var last=w.Strategy!.Shoguns!.Last();
        Assert.Equal("succeeded",last.Kind);Assert.NotNull(last.Shogun);Assert.NotEqual(shogun,last.Shogun);
        Assert.Equal(last.Shogun,SupremeOffice.ShogunAt(w,w.ElapsedSeconds,w.CommitSequence));
        var old=NationalStart.Create(NationalStartTests.Nobunaga());old=Without(old);old.ElapsedSeconds=NationalStrategy.MonthStart(old,1560,2)-86400;Kill(old,shogun);Month(old,1560,2);
        Assert.DoesNotContain(old.Strategy?.Shoguns??[],x=>x.Kind=="succeeded");
    }

    [Fact]
    public void AnUnansweredMediationExpiresAfterSixtyDaysForTheRequesterAndTheBroker()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var broker=new EntityId(NationalStart.PersonId("P-010391"));var kyoto=CentralCourtCatalog.KyotoIn(w);var me=w.PlayerControl;
        var house=new EntityId(w.NewGame!.Start.People.Single(p=>p.Id==me.Value).Faction!);var other=new EntityId(w.NewGame.Start.Factions.First(f=>f.Id!=house.Value).Id);
        var ask=new CourtInput(new("court:test-ask"),CourtAction.AskPeaceMediation,broker,house,kyoto,Amount:30){OtherHouse=other};
        var send=new CourtInput(new("court:test-send"),CourtAction.SendPeaceProposal,me,house,kyoto,Amount:30,Reference:ask.Id){OtherHouse=other};
        w.Court=new(1,HouseholdCourt.Policy,[new(ask,me,kyoto,0,1,false),new(send,broker,kyoto,86400,2,false)]);
        w.ElapsedSeconds=86400+59*86400;
        Assert.Empty(PeaceBreach.Expired(w,me));
        w.ElapsedSeconds=86400+60*86400;
        Assert.Single(PeaceBreach.Expired(w,me));Assert.Single(PeaceBreach.Expired(w,broker));
        Assert.Empty(PeaceBreach.Expired(w,new(w.NewGame.Start.Factions.Last().Head)));
        Assert.Empty(PeaceBreach.Expired(Without(w),me));
    }

    [Fact]
    public void ABreachOfACertifiedPeaceCanBeAppealedAndTheBreakerGetsInfamy()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var broker=new EntityId(NationalStart.PersonId("P-010391"));var kyoto=CentralCourtCatalog.KyotoIn(w);var me=w.PlayerControl;
        var house=new EntityId(w.NewGame!.Start.People.Single(p=>p.Id==me.Value).Faction!);
        var owners=NationalStrategy.Owners(w,null);var mine=owners.First(x=>x.Value==house.Value).Key;
        var other=new EntityId(w.NewGame.Start.Factions.First(f=>f.Id!=house.Value&&f.Head!=me.Value).Id);
        var cert=new CourtInput(new("court:test-cert"),CourtAction.CertifyPeaceAgreement,me,house,kyoto,Amount:60){OtherHouse=other};
        w.Court=new(1,HouseholdCourt.Policy,[new(cert,broker,kyoto,0,1,false)]);
        var campaign=new StrategyCampaign(new("campaign:test-breach"),other,house,new(mine),10*86400,2,"border");
        w.Strategy=new StrategyState(1,NationalStrategy.Policy,[campaign],[]);w.ElapsedSeconds=11*86400;w.CommitSequence=3;
        var cases=PeaceBreach.Cases(w,me);Assert.Single(cases);
        var appeal=PeaceBreach.Decide(w,cases[0],me);w.Strategy=w.Strategy with{Appeals=[appeal]};
        PeaceBreach.Validate(w,w.Strategy,null);
        var after=w;
        Assert.True(after.Strategy!.Appeals!.Single().Upheld);
        var head=HouseSuccession.Head(after,other);
        Assert.Contains(SocialWorld.Renown(after,me),f=>f.Subject==head&&f.Kind==RenownKind.Infamy);
        Assert.Empty(PeaceBreach.Cases(after,me));
    }

    [Fact]
    public void ThePlayerCannotAppealWithoutAKnownBreach()
    {
        var s=new WorldSession(NationalStart.Create(NationalStartTests.Nobunaga()));
        Assert.Equal(ResultCode.NotAvailable,s.AppealPeaceBreach(new("campaign:none"),s.Revision).Code);
    }

    [Fact]
    public void TheShogunsTruceBindsOnlyAfterTheLettersArriveAndCanBeRefused()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var owners=NationalStrategy.Owners(w,null);var me=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var shogun=NationalStart.FactionId(NationalStrategy.Shogun);
        var pair=owners.Where(x=>x.Value!=null&&x.Value!=me&&x.Value!=shogun).SelectMany(x=>NationalStrategy.Neighbors(x.Key).Where(n=>owners.GetValueOrDefault(n) is {} o&&o!=x.Value&&o!=me&&o!=shogun).Select(n=>(Site:n,A:x.Value!,D:owners[n]!))).First();
        long t0=NationalStrategy.MonthStart(w,1560,1);
        var c1=new StrategyCampaign(new("campaign:t1"),new(pair.A),new(pair.D),new(pair.Site),t0,1,"border");var c2=c1 with{Id=new("campaign:t2")};
        w.Strategy=new StrategyState(1,NationalStrategy.Policy,[c1,c2],[new(c1.Id,t0,1,true),new(c2.Id,t0,1,true)]);
        Month(w,1560,2);
        var truce=(w.Strategy!.Pacts??[]).Single(p=>p.Kind=="truce");
        Assert.True(truce.From>w.ElapsedSeconds);
        Assert.Matches(":(accepted|refused)$",truce.Basis);
        Assert.Equal(truce.Basis.EndsWith(":refused"),truce.Broken!=null);
    }
}
