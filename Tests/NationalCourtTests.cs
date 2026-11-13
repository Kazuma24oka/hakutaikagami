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
    public void TheShogunPetitionsTheCourtThroughYamashinaInKyoto()
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
        var owner=CentralMediation.ApplicantHouse(w0,new(Yoshiteru))!.Value;
        Assert.True(s.ManageSocial(new(new("request:visit-yamashina"),SocialWorldAction.Visit,broker),s.Revision).Code==ResultCode.Accepted);
        var gift=s.ManageSocial(new(new("request:gift-yamashina"),SocialWorldAction.DispatchGift,broker,Amount:100),s.Revision);
        Assert.True(gift.Code==ResultCode.Accepted,gift.VisibleReason);
        NewGameWorldTests.At(s,s.Seconds+3);
        var r=s.ManageCourt(new CourtInput(new("request:national-rank"),CourtAction.AskCourtIntroduction,broker,owner,site){Title="rank:14"},s.Revision);
        Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        // 取次は日課の仕事の最中なら、終えてから返答する。
        long start=s.Seconds;CourtEvent[] events;
        do{NewGameWorldTests.At(s,s.Seconds+6*3600);events=s.DiagnosticSnapshot().Court!.Events;}
        while(s.Seconds<start+3*86400&&!events.Any(e=>e.Input.Action==CourtAction.FileRankPetition));
        var filed=Assert.Single(events,e=>e.Input.Action==CourtAction.FileRankPetition);
        var w=s.DiagnosticSnapshot();
        Assert.Contains(w.Court!.Events,e=>e.Input.Action==CourtAction.AcceptCourtIntroduction&&e.Actor==broker);
        Assert.Equal(broker,filed.Actor);
        // 朝廷の審議（将軍のいる版では二か月）と返書は、地方開始の版の検査（CentralTitlesTests）で確かめる。
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
}
