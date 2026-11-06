using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class HouseConditionTests
{
    [Fact]
    public void BarrierReformNamesOneRoadAndCannotRemoveAnother()
    {
        var s=HouseholdCourtTests.Start();
        var roads=HouseReforms.LocalRoads(s.DiagnosticSnapshot(),HouseholdCourtTests.Site);
        var gate=new CourtInput(new("request:road-gate"),CourtAction.EstablishBarrier,s.PlayerControl,
            HouseholdCourtTests.Owner,HouseholdCourtTests.Site){BarrierEdge=roads[0].Id};
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(gate,s.Revision).Code);
        var consult=gate with{Id=new("request:gate-consult"),Action=CourtAction.ConsultReform,
            Person=HouseholdCourtTests.Retainer,Reform=ReformKind.RemoveBarrier};
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(consult,s.Revision).Code);
        NewGameWorldTests.At(s,120);
        Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(HouseReforms.Letter(consult,HouseReforms.Answer)),s.Revision).Code);
        var remove=consult with{Id=new("request:remove"),Action=CourtAction.AbolishBarrier,Reform=null,Reference=consult.Id};
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(remove with{BarrierEdge="another-road"},s.Revision).Code);
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(remove,s.Revision).Code);
        var w=new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot())).DiagnosticSnapshot();
        Assert.Null(HouseReforms.BarrierAt(w,roads[0].Id,w.ElapsedSeconds,w.CommitSequence+1));
        Assert.NotNull(HouseReforms.BarrierAt(w,roads[0].Id,w.ElapsedSeconds,w.CommitSequence));
    }

    [Fact]
    public void CrossHouseDispatchAfterLocalEnactmentLeavesEvidenceOnlyWithTheInformed()
    {
        var local=HouseholdCourtTests.Retainer;
        var foreign=new EntityId("created:partner");
        var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer(local.Value,StartTemplate.Retainer,lord:"person:nobunaga"),
            NewGameWorldTests.Officer(foreign.Value,StartTemplate.Retainer,"site:kofu","person:shingen")]);
        for(ulong seed=0;seed<32;seed++){
            var w=NewGameWorld.Create(setup.World,setup.Registered,"person:nobunaga",seed,
                inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,
                companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,
                socialPolicy:CharacterRelations.Policy,economyPolicy:HouseSuccession.Policy);
            var s=new WorldSession(w);
            var consult=new CourtInput(new("request:local-consult"),CourtAction.ConsultReform,local,
                HouseholdCourtTests.Owner,HouseholdCourtTests.Site){Reform=ReformKind.ForeignLetters};
            Assert.Equal(ResultCode.Accepted,s.ManageCourt(consult,s.Revision).Code);
            NewGameWorldTests.At(s,120);
            Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(HouseReforms.Letter(consult,HouseReforms.Answer)),s.Revision).Code);
            Assert.Equal(ResultCode.Accepted,s.ManageCourt(consult with{Id=new("request:local-law"),Action=CourtAction.EnactLetterLaw,
                Reference=consult.Id,Reform=null},s.Revision).Code);
            void Travel(string node){
                Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:visit-"+node,node),s.Revision).Code);
                NewGameWorldTests.At(s,s.Seconds+2);
                NewGameWorldTests.At(s,s.GetBranchView(s.PlayerControl).ArrivalAt!.Value+2);
            }
            Travel("kofu");
            EntityId pact=new("request:cross-house");
            Assert.Equal(ResultCode.Accepted,s.NegotiatePact(pact,foreign,PactPurpose.WithholdService,s.Revision).Code);
            if(!s.DiagnosticSnapshot().SecretPacts!.Single().Accepted)continue;
            Travel("castle");
            Assert.Equal(ResultCode.Accepted,s.SignalPact(pact,s.Revision).Code);
            var sent=s.DiagnosticSnapshot();
            Assert.Single(HouseReforms.KnownLetterBreaches(sent,s.PlayerControl));
            Assert.Empty(HouseReforms.KnownLetterBreaches(sent,foreign));
            Assert.Empty(HouseReforms.KnownLetterBreaches(sent,local));
            var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(sent)));
            var arrival=sent.Messaging.Attempts.Last().DueAt;
            NewGameWorldTests.At(restored,arrival+2);
            Assert.Single(HouseReforms.KnownLetterBreaches(restored.DiagnosticSnapshot(),foreign));
            Assert.Empty(HouseReforms.KnownLetterBreaches(restored.DiagnosticSnapshot(),local));
            return;
        }
        Assert.Fail("密約を承諾する相手を確認できませんでした");
    }

    [Fact]
    public void ForeignLetterLawRequiresAnOpenedAnswerAndDoesNotApplyOutsideItsJurisdiction()
    {
        var s=HouseholdCourtTests.Start();
        var input=new CourtInput(new("request:letter-law-consultation"),CourtAction.ConsultReform,
            HouseholdCourtTests.Retainer,HouseholdCourtTests.Owner,HouseholdCourtTests.Site){Reform=ReformKind.ForeignLetters};
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(input,s.Revision).Code);
        var law=input with{Action=CourtAction.EnactLetterLaw,Id=new("request:letter-law"),Reference=input.Id,Reform=null};
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(law,s.Revision).Code);
        NewGameWorldTests.At(s,120);
        Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(HouseReforms.Letter(input,HouseReforms.Answer)),s.Revision).Code);
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(law,s.Revision).Code);
        var w=s.DiagnosticSnapshot();
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.True(HouseReforms.LetterLawAt(restored.DiagnosticSnapshot(),law.Owner,law.Site,w.ElapsedSeconds,s.Revision+1));
        Assert.False(HouseReforms.LetterLawAt(w,law.Owner,new("site:kofu"),w.ElapsedSeconds,s.Revision+1));
        Assert.False(HouseReforms.LetterLawAt(w,law.Owner,law.Site,w.ElapsedSeconds,s.Revision));
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(law with{Action=CourtAction.LetterBreach,Id=new("request:forged-breach")},s.Revision).Code);
    }

    [Fact]
    public void LocalControlAndReadBooksAreDistinctFromTheExistenceOfASurvey()
    {
        var s=LandSurveyTests.Start();
        EntityId owner=new("org:oda"),site=new("site:castle");
        HouseConditionView Report()=>HouseCondition.Observe(s.DiagnosticSnapshot(),s.PlayerControl,owner)!;
        Assert.True(Report().ControlsLocalSite);
        Assert.Null(HouseCondition.Observe(s.DiagnosticSnapshot(),s.PlayerControl,new("org:takeda")));
        var order=new SurveyInput(new("request:house-survey"),SurveyAction.Measurement,owner,site);
        Assert.Equal(ResultCode.Accepted,s.ManageLandSurvey(order,s.Revision).Code);
        NewGameWorldTests.At(s,7*86400);
        Assert.Empty(Report().KnownSurveys);
        Assert.Equal(ResultCode.Accepted,s.ManageLandSurvey(order with{Id=new("request:read-survey"),Action=SurveyAction.ReadBook,Reference=order.Id},s.Revision).Code);
        var known=Assert.Single(Report().KnownSurveys);
        Assert.Equal(site,known.Site);Assert.True(known.Book.Measured);
        var restored=new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot()));
        Assert.Equal(ContractJson.Write(Report()),ContractJson.Write(HouseCondition.Observe(restored.DiagnosticSnapshot(),restored.PlayerControl,owner)!));
    }

    [Fact]
    public void ConsultationSurvivesTransitAndDoesNotConveyProperty()
    {
        var s=HouseholdCourtTests.Start();
        var w=s.DiagnosticSnapshot();
        var input=new CourtInput(new("request:market-consultation"),CourtAction.ConsultReform,
            HouseholdCourtTests.Retainer,HouseholdCourtTests.Owner,HouseholdCourtTests.Site){Reform=ReformKind.OpenMarket};
        var result=s.ManageCourt(input,s.Revision);
        Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        Assert.Equal(ResultCode.AlreadyAccepted,s.ManageCourt(input,s.Revision).Code);
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(input with{Id=new("request:another-consultation")},s.Revision).Code);
        s.SetPaused(true);
        var dir=Directory.CreateTempSubdirectory("hakutai-consultation-");
        try
        {
            var store=new GenerationSaveStore(dir.FullName,w.Definitions);
            Assert.True(store.Save(s,"sent",w.WorldId).Published);
            var host=new WorldSessionHost(new WorldSession(w));
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("sent"),host.Current,host.Current.Revision));
            NewGameWorldTests.At(host.Current,120);
            NewGameWorldTests.At(s,120);
            var after=host.Current.DiagnosticSnapshot();
            Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(after));
            Assert.Single(after.Messaging.Messages,m=>m.Id==HouseReforms.Letter(input,HouseReforms.Answer));
            Assert.False(HouseReforms.AnswerKnown(after,after.Court!.Events.Single(e=>e.Input.Id==input.Id),s.PlayerControl));
            Assert.Equal(ContractJson.Write(w.LandRights),ContractJson.Write(after.LandRights));
            var defer=input with{Action=CourtAction.DeferReform,Reform=null,Id=new("request:defer"),Reference=input.Id};
            Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(defer,s.Revision).Code);
            Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(HouseReforms.Letter(input,HouseReforms.Answer)),s.Revision).Code);
            var market=new WorldSession(s.DiagnosticSnapshot());
            var enact=defer with{Action=CourtAction.OpenLocalMarket,Id=new("request:enact-market")};
            var concession=new WorldSession(s.DiagnosticSnapshot());
            var representative=SocialWorld.Temple(w,enact.Site).Representative;
            var grant=new CourtInput(new("request:grant-seat"),CourtAction.GrantTempleSeat,representative,enact.Owner,enact.Site);
            Assert.Equal(ResultCode.NotAvailable,concession.ManageCourt(grant,concession.Revision).Code);
            var donation=concession.ManageSocial(new(new("request:temple-meeting"),SocialWorldAction.Donate,representative,Amount:20),concession.Revision);
            Assert.True(donation.Code==ResultCode.Accepted,donation.VisibleReason);
            var granted=concession.ManageCourt(grant,concession.Revision);
            Assert.True(granted.Code==ResultCode.Accepted,granted.VisibleReason);
            Assert.Equal(ResultCode.Accepted,concession.ManageCourt(enact,concession.Revision).Code);
            concession.SetPaused(true);
            Assert.True(store.Save(concession,"seat",w.WorldId).Published);
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("seat"),host.Current,host.Current.Revision));
            NewGameWorldTests.At(host.Current,32*86400);
            Assert.Equal(3,host.Current.DiagnosticSnapshot().Materials!.Months.First(m=>m.Site==enact.Site).WoodBought);
            var contested=host.Current;
            CourtInput Dispute(CourtAction act)=>grant with{Id=new("request:seat/"+act),Action=act,Reference=grant.Id};
            var challenge=Dispute(CourtAction.ChallengeSeat);
            Assert.Equal(ResultCode.Accepted,contested.ManageCourt(challenge,contested.Revision).Code);
            var insist=Dispute(CourtAction.InsistSeat);
            Assert.Equal(ResultCode.NotAvailable,contested.ManageCourt(insist,contested.Revision).Code);
            NewGameWorldTests.At(contested,32*86400+60);
            Assert.Equal(ResultCode.Accepted,contested.ManageCourt(insist,contested.Revision).Code);
            contested.SetPaused(true);
            Assert.True(store.Save(contested,"resistance",w.WorldId).Published);
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("resistance"),contested,contested.Revision));
            var restoredDispute=host.Current;
            Assert.Equal(2,HouseReforms.WoodPurchaseLimit(restoredDispute.DiagnosticSnapshot(),enact.Site,
                restoredDispute.DiagnosticSnapshot().ElapsedSeconds,restoredDispute.Revision+1));
            var settle=Dispute(CourtAction.SettleSeat);
            Assert.Equal(ResultCode.Accepted,restoredDispute.ManageCourt(settle,restoredDispute.Revision).Code);
            Assert.Equal(3,HouseReforms.WoodPurchaseLimit(restoredDispute.DiagnosticSnapshot(),enact.Site,
                restoredDispute.DiagnosticSnapshot().ElapsedSeconds,restoredDispute.Revision+1));
            var enacted=market.ManageCourt(enact,market.Revision);
            Assert.True(enacted.Code==ResultCode.Accepted,enacted.VisibleReason);
            var local=market.DiagnosticSnapshot();
            Assert.True(HouseReforms.OpenMarketAt(local,enact.Site,local.ElapsedSeconds,market.Revision+1));
            Assert.False(HouseReforms.OpenMarketAt(local,new("site:sakai"),local.ElapsedSeconds,market.Revision+1));
            Assert.Equal(ResultCode.NotAvailable,market.ManageCourt(defer,market.Revision).Code);
            market.SetPaused(true);
            Assert.True(store.Save(market,"market",w.WorldId).Published);
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("market"),host.Current,host.Current.Revision));
            Assert.Equal(ContractJson.Write(market.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
            NewGameWorldTests.At(host.Current,32*86400);
            var purchases=host.Current.DiagnosticSnapshot().Materials!.Months;
            Assert.Equal(4, purchases.First(m=>m.Site==enact.Site).WoodBought);
            Assert.All(purchases.Where(m=>m.Site!=enact.Site),m=>Assert.InRange(m.WoodBought,0L,2L));
            var decision=s.ManageCourt(defer,s.Revision);
            Assert.True(decision.Code==ResultCode.Accepted,decision.VisibleReason);
            Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(defer with{Id=new("request:duplicate-defer")},s.Revision).Code);
            s.SetPaused(true);
            Assert.True(store.Save(s,"deferred",w.WorldId).Published);
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("deferred"),host.Current,host.Current.Revision));
            Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        }
        finally{dir.Delete(true);}
    }
}
