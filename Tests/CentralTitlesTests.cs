using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// 位階・官職の表と、望む位階・官職を添えた奏請（BD-54、2026-11-12）。
public sealed class CentralTitlesTests
{
    [Fact]
    public void CatalogKeepsTheListedTitlesAndDbNamesAreSeparateFromCourtTitles()
    {
        Assert.Equal(15,CentralTitles.Titles.Count(t=>t.Kind=="rank"));
        Assert.Equal(118,CentralTitles.Titles.Count(t=>t.Kind=="kokushi"));
        Assert.Equal(50,CentralTitles.Titles.Count(t=>t.Kind=="hyakkan"));
        Assert.Equal("従五位下",CentralTitles.ByName("尾張守")!.Rank);
        Assert.Null(CentralTitles.ByName("上総守")); // 親王任国の守は名乗りにしない
        Assert.Equal("正六位",CentralTitles.ByName("上総介")!.Rank);
    }

    [Fact]
    public void TitledPetitionsAreReviewedByRankStepsAndRecordedOncePerOffice()
    {
        var broker=CentralCourtCatalog.CourtBroker;var site=CentralCourtCatalog.Kyoto;var owner=new EntityId("org:court-test");
        var applicant=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);
        var edition=CentralCourtCatalog.Id;var raw=NewGameWorld.Scenario(edition);
        var request=new WorldSetupRequest([applicant],[new(applicant.Id,owner.Value,[site.Value],false)]);
        var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var s=new WorldSession(NewGameWorld.Create(setup,[applicant],applicant.Id,historicalCatalog:edition,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy));
        Assert.True(s.ManageSocial(new(new("request:visit-courtier"),SocialWorldAction.Visit,broker),s.Revision).Code==ResultCode.Accepted);
        Assert.True(s.ManageSocial(new(new("request:court-gift"),SocialWorldAction.DispatchGift,broker,Amount:100),s.Revision).Code==ResultCode.Accepted);
        NewGameWorldTests.At(s,s.Seconds+3);
        int filedBefore=0;
        string Petition(string id,string title)
        {
            var r=s.ManageCourt(new CourtInput(new("request:"+id),CourtAction.AskCourtIntroduction,broker,owner,site){Title=title},s.Revision);
            Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
            CourtEvent[] events;long start=s.Seconds;
            do{NewGameWorldTests.At(s,s.Seconds+1);events=s.DiagnosticSnapshot().Court!.Events;}while(s.Seconds<start+60&&events.Count(e=>e.Input.Action==CourtAction.FileRankPetition)<filedBefore+1);
            var filed=events.Last(e=>e.Input.Action==CourtAction.FileRankPetition);
            NewGameWorldTests.At(s,filed.At+86400);
            var w=s.DiagnosticSnapshot();var decision=w.Court!.Events.Last(e=>e.Input.Action is CourtAction.IssueCourtRank or CourtAction.RejectCourtRank);
            var letter=w.Messaging.Messages.Single(m=>m.Id==CentralRank.ReplyId(decision));
            NewGameWorldTests.At(s,s.Seconds+3);
            Assert.True(s.Open(s.PlayerControl,new(letter.Id),s.Revision).Code==ResultCode.Accepted);
            filedBefore++;
            return decision.Input.Action==CourtAction.IssueCourtRank?"issued":"rejected";
        }
        Assert.Equal("issued",Petition("rank-14","rank:14"));                                    // 従五位下
        Assert.Equal("issued",Petition("owari",CentralTitles.ByName("尾張守")!.Id));              // 相当位階 従五位下
        Assert.Equal("rejected",Petition("rank-12","rank:12"));                                   // 二段上はまだ
        var w=s.DiagnosticSnapshot();
        Assert.Equal(new[]{"従五位下","尾張守"},CentralTitles.Held(w,s.PlayerControl).Select(h=>h.Name));
        Assert.All(CentralTitles.Held(w,s.PlayerControl),h=>Assert.Equal(CentralTitles.CourtBasis,h.Basis));
        Assert.Contains(CentralTitles.Requestable(w,s.PlayerControl,owner),t=>t.Id=="rank:13");
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(restored.DiagnosticSnapshot()));
    }

    [Fact]
    public void NationalPeopleCarryTheirDbNamesAsUnofficialTitles()
    {
        var people=NationalStart.Scenario().People;
        var nanbu=people.First(p=>NationalStart.DbPerson(p.Id)?.Kanto=="大膳大夫");
        var w=new WorldSession(NationalStart.Create(NationalStartTests.Nobunaga())).DiagnosticSnapshot();
        var held=Assert.Single(CentralTitles.Held(w,new(nanbu.Id)));
        Assert.Equal(CentralTitles.DbBasis,held.Basis);
        Assert.Equal(CentralTitles.ByName("大膳大夫")!.Id,held.Title);
    }
}
