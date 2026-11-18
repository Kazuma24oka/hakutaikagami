using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// 全国1560年の開始 v2：京の朝廷の人々を置き、京の取次・将軍家との往来を全国で使う（BD-53〜55、2026-11-12）。
[Trait("Tier","Heavy")]
public sealed class NationalCourtTests
{
    static string Yoshiteru=>NationalStart.PersonId("P-005253");

    [Fact]
    public void CourtPeopleStandInKyotoWithoutAHouseAndOldNationalStartsStayTheSame()
    {
        var v2=NationalStart.Scenario();var v1=NationalStart.Scenario(NationalStart.Edition);
        var court=v2.People.Where(p=>NationalStart.DbPerson(p.Id)?.House==NationalStart.CourtHouse).ToArray();
        Assert.Contains(court,p=>p.Name=="山科言継");
        Assert.All(court,p=>{Assert.Equal(NationalStart.KyotoSite,p.Site);Assert.Null(p.Faction);});
        Assert.DoesNotContain(v1.People,p=>NationalStart.DbPerson(p.Id)?.House==NationalStart.CourtHouse);
        Assert.Equal(v1.People.Length+court.Length,v2.People.Length);
        Assert.False(CentralCourtCatalog.HasCourt(NationalStart.Edition));
        Assert.True(CentralCourtCatalog.HasShogunate(NationalStart.CourtEdition));
    }

    [Fact]
    public void TheNationalCourtNamesYamashinaAsBrokerAndTheShogunInKyoto()
    {
        var s=new WorldSession(NationalStart.Create(Yoshiteru));
        var w0=s.DiagnosticSnapshot();
        var broker=CentralCourtCatalog.CourtBrokerIn(w0);var site=CentralCourtCatalog.KyotoIn(w0);
        Assert.Equal("山科言継",w0.Definitions.Characters.Single(c=>c.Id==broker).Name);
        Assert.Equal(new EntityId(Yoshiteru),CentralCourtCatalog.ShogunIn(w0));
        Assert.Equal(new EntityId(Yoshiteru),SupremeOffice.ShogunAt(w0,w0.ElapsedSeconds,w0.CommitSequence));
        var shogunBroker=CentralCourtCatalog.ShogunBrokerIn(w0);
        Assert.Equal(site,SocialWorld.Site(w0,shogunBroker));
        Assert.Equal(site,SocialWorld.Site(w0,broker));
        Assert.True(s.ManageSocial(new(new("request:visit-yamashina"),SocialWorldAction.Visit,broker),s.Revision).Code==ResultCode.Accepted);
        var gift=s.ManageSocial(new(new("request:gift-yamashina"),SocialWorldAction.DispatchGift,broker,Amount:100),s.Revision);
        Assert.True(gift.Code==ResultCode.Accepted,gift.VisibleReason);
        NewGameWorldTests.At(s,s.Seconds+3);
        // 公家はそれぞれ取次になれる。天皇と将軍家の人は取次ではない。
        Assert.True(CentralCourtCatalog.IsCourtBroker(w0,new(NationalStart.PersonId("P-011328"))));Assert.True(CentralCourtCatalog.IsCourtBroker(w0,broker));
        Assert.False(CentralCourtCatalog.IsCourtBroker(w0,new(NationalStart.PersonId("P-010184"))));Assert.False(CentralCourtCatalog.IsCourtBroker(w0,new(Yoshiteru)));
        Assert.Equal(broker,CentralCourtCatalog.BrokerFor(s.DiagnosticSnapshot(),new(Yoshiteru))); // 最後に会った公家へ奏請する
        Assert.NotNull(CentralMediation.ApplicantHouse(w0,new(Yoshiteru)));
        // 取次の依頼から奏請・審議・返書までの流れは、地方開始の版の検査（CentralTitlesTests・CentralMediationTests）で確かめる。
        // 全国で3日回していた流れは同じことの繰り返しだったので外した（2026-11-18、検査の見直し）。
        var w=s.DiagnosticSnapshot();
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
}
