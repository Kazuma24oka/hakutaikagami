using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class CentralMediationTests
{
    [Fact]
    public void ShogunAndHisAttendantAreSeparatePeopleWithNoAutomaticKyotoTerritory()
    {
        var edition=HistoricalStartCatalog.Current;
        var scenario=NewGameWorld.Scenario(edition);
        var prior=NewGameWorld.Scenario(CentralCourtCatalog.Id);
        Assert.Equal(prior.Castles,scenario.Castles);
        Assert.DoesNotContain(prior.People,p=>p.Id==CentralCourtCatalog.Shogun.Value);
        var w=NewGameWorld.Create(scenario,[],CentralCourtCatalog.ShogunBroker.Value,historicalCatalog:edition);
        Assert.Contains(w.ServiceBonds,b=>b.Subject==CentralCourtCatalog.ShogunBroker && b.Target==CentralCourtCatalog.Shogun);
        Assert.DoesNotContain(w.LandRights,r=>r.Subject==CentralCourtCatalog.Shogun);
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(new WorldSession(w).DiagnosticSnapshot()));
    }

    [Fact]
    public void KyotoCourtierCanEnterTheWorldWithoutOwningKyotoOrAnArmy()
    {
        var prior=NewGameWorld.Scenario(HistoricalHouseholdCatalog.Id);
        var scenario=NewGameWorld.Scenario(CentralCourtCatalog.Id);
        var id=CentralCourtCatalog.CourtBroker;
        Assert.DoesNotContain(prior.People,p=>p.Id==id.Value);
        var person=Assert.Single(scenario.People,p=>p.Id==id.Value);
        Assert.Equal(1507,person.BirthYear);Assert.Equal(CentralCourtCatalog.Kyoto.Value,person.Site);
        Assert.Equal(prior.Castles,scenario.Castles);Assert.Equal(prior.Factions,scenario.Factions);
        var state=NewGameWorld.Create(scenario,[],id.Value,historicalCatalog:CentralCourtCatalog.Id);
        Assert.DoesNotContain(state.LandRights,right=>right.Subject==id);
        Assert.DoesNotContain(state.Units,u=>u.Commander==id);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(state)));
        Assert.Equal(id,restored.PlayerControl);
        Assert.Equal(ContractJson.Write(state),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
    [Fact]
    public void ShogunMustReadRecommendationAndApplicantMustOpenSeparateAppointment()
    {
        var broker=CentralCourtCatalog.ShogunBroker;var site=CentralCourtCatalog.Kyoto;var kofu=new EntityId("site:kofu");
        var owner=new EntityId("org:shugo-test");var applicant=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);
        var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var request=new WorldSetupRequest([applicant],[new(applicant.Id,owner.Value,[site.Value,kofu.Value],false)]);
        var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var s=new WorldSession(NewGameWorld.Create(setup,[applicant],applicant.Id,historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy));
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        Good(s.ManageSocial(new(new("request:meet-fujitaka"),SocialWorldAction.Visit,broker),s.Revision));
        Good(s.ManageSocial(new(new("request:fujitaka-gift"),SocialWorldAction.DispatchGift,broker,Amount:100),s.Revision));
        NewGameWorldTests.At(s,s.Seconds+3);
        var input=new CourtInput(new("request:kai-shugo"),CourtAction.AskShugo,broker,owner,kofu);
        Good(s.ManageCourt(input,s.Revision));NewGameWorldTests.At(s,s.Seconds+1);
        var w=s.DiagnosticSnapshot();
        Assert.Single(w.Messaging.Messages,m=>m.Kind==ShogunateAppointments.Petition && m.RecipientId==CentralCourtCatalog.Shogun.Value);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        foreach(var run in new[]{s,restored}){
            NewGameWorldTests.At(run,run.Seconds+6);var end=run.DiagnosticSnapshot();
            var record=Assert.Single(end.Court!.Events,e=>e.Input.Action==CourtAction.AppointShugo);
            Assert.Equal(CentralCourtCatalog.Shogun,record.Actor);
            Assert.Empty(ShogunateAppointments.KnownOffices(end,run.PlayerControl));
            var letter=Assert.Single(end.Messaging.Messages,m=>m.Kind==ShogunateAppointments.Reply);
            Good(run.Open(run.PlayerControl,new(letter.Id),run.Revision));
            Assert.Equal("甲斐守護",Assert.Single(ShogunateAppointments.KnownOffices(run.DiagnosticSnapshot(),run.PlayerControl)));
            Assert.Null(CentralRank.KnownRank(run.DiagnosticSnapshot(),run.PlayerControl));
            Assert.Equal(ContractJson.Write(w.LandRights),ContractJson.Write(run.DiagnosticSnapshot().LandRights));
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }

    [Fact]
    public void RetainerMustMeetCurrentLordBeforePayingCourtier()
    {
        var site=CentralCourtCatalog.Kyoto;
        var lord=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);
        var retainer=NewGameWorldTests.Officer("created:retainer",StartTemplate.Retainer,site.Value,lord.Id);
        var house=new EntityId("org:rank-leave");
        var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var setupRequest=new WorldSetupRequest([lord,retainer],[new(lord.Id,house.Value,[site.Value],false)]);
        var setup=NewGameSetup.Confirm(raw,setupRequest,NewGameSetup.Preview(raw,setupRequest));
        var session=new WorldSession(NewGameWorld.Create(setup,[lord,retainer],retainer.Id,historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy));
        var broker=CentralCourtCatalog.CourtBroker;
        Assert.Equal(ResultCode.Accepted,session.ManageSocial(new(new("request:court-visit"),SocialWorldAction.Visit,broker),session.Revision).Code);
        var petition=new CourtInput(new("request:rank-after-leave"),CourtAction.AskCourtIntroduction,broker,house,site);
        Assert.Equal(ResultCode.NotAvailable,session.ManageCourt(petition,session.Revision).Code);
        var leave=new CourtInput(new("request:leave"),CourtAction.AskRankLeave,new(lord.Id),house,site);
        var sent=session.ManageCourt(leave,session.Revision);
        Assert.True(sent.Code==ResultCode.Accepted,sent.VisibleReason);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(session.DiagnosticSnapshot())));
        foreach(var run in new[]{session,restored}){
            NewGameWorldTests.At(run,run.Seconds+2);
            Assert.Single(run.DiagnosticSnapshot().Court!.Events,e=>e.Input.Action==CourtAction.GrantRankLeave && e.Actor.Value==lord.Id);
            var result=run.ManageCourt(petition,run.Revision);
            Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        }
        Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void BrokerSendsSamePeaceTermsToTwoDistinctHousesAfterRestore(bool agree)
    {
        var site=CentralCourtCatalog.Kyoto;
        var broker=CentralCourtCatalog.CourtBroker;
        var owner=new EntityId("org:peace-test");
        var lord=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);
        var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var target=new EntityId(raw.Factions.First().Id);
        var request=new WorldSetupRequest([lord],[new(lord.Id,owner.Value,[site.Value],false)]);
        var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var session=new WorldSession(NewGameWorld.Create(setup,[lord],lord.Id,historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,worldArmyPolicy:NpcArmies.CompactCombatPolicy,trucePolicy:TruceNegotiations.Policy));
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        Good(session.ManageSocial(new(new("request:meet-peace-broker"),SocialWorldAction.Visit,broker),session.Revision));
        Good(session.ManageSocial(new(new("request:peace-gift"),SocialWorldAction.DispatchGift,broker,Amount:100),session.Revision));
        NewGameWorldTests.At(session,session.Seconds+3);
        var ask=new CourtInput(new("request:ask-peace"),CourtAction.AskPeaceMediation,broker,owner,site,Amount:30){OtherHouse=target};
        Good(session.ManageCourt(ask,session.Revision));
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(session.DiagnosticSnapshot())));
        foreach(var run in new[]{session,restored}){
            NewGameWorldTests.At(run,run.Seconds+1);
            var state=run.DiagnosticSnapshot();var letters=state.Messaging.Messages.Where(m=>m.Kind==PeaceMediation.Proposal).ToArray();
            Assert.Equal(2,letters.Length);Assert.Equal(2,letters.Select(m=>m.RecipientId).Distinct().Count());
            Assert.Single(letters.Select(m=>m.Body).Distinct());
            Assert.Empty(state.Truces?.Formations??[]);
            Assert.Empty(PeaceMediation.Votes(state,run.PlayerControl));
            NewGameWorldTests.At(run,run.Seconds+4);
            var original=letters.Single(m=>m.RecipientId==run.PlayerControl.Value);
            Good(run.Open(run.PlayerControl,new(original.Id),run.Revision));
            var answer=Assert.Single(PeaceMediation.Votes(run.DiagnosticSnapshot(),run.PlayerControl),i=>i.Action==(agree?CourtAction.AcceptPeaceProposal:CourtAction.RejectPeaceProposal));
            Good(run.ManageCourt(answer,run.Revision));
            Assert.Equal(ResultCode.AlreadyAccepted,run.ManageCourt(answer,run.Revision).Code);
            var answered=run.DiagnosticSnapshot();
            Assert.Single(answered.Messaging.Messages,m=>m.Kind==PeaceMediation.Response && m.SenderId==run.PlayerControl.Value);
            Assert.Empty(answered.Truces!.Formations);
            _=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(answered)));
            long Arrival(string id){
                var snapshot=run.DiagnosticSnapshot();
                return snapshot.Couriers!.Missions.Single(c=>snapshot.Messaging.Attempts.Any(a=>a.Id==c.Attempt && a.MessageId==id)).ReadyAt;
            }
            NewGameWorldTests.At(run,Math.Max(run.Seconds,Arrival(letters.Single(m=>m.RecipientId!=run.PlayerControl.Value).Id))+2);
            var answers=run.DiagnosticSnapshot().Messaging.Messages.Where(m=>m.Kind==PeaceMediation.Response).ToArray();
            Assert.Equal(2,answers.Length);
            NewGameWorldTests.At(run,Math.Max(run.Seconds,answers.Max(m=>Arrival(m.Id)))+3);
            var settled=run.DiagnosticSnapshot();
            Assert.Equal(agree?1:0,settled.Court!.Events.Count(e=>e.Input.Action==CourtAction.CertifyPeaceAgreement));
            Assert.False(TruceNegotiations.WillNotInitiate(settled,owner,target,run.Seconds,run.Revision));
            if(!agree){
                var notice=Assert.Single(settled.Messaging.Messages,m=>m.Kind==PeaceMediation.RefusalNotice);
                var recipient=new EntityId(notice.RecipientId);
                var arrival=Arrival(notice.Id);
                Assert.False(PeaceMediation.KnowsRefusal(settled,recipient,owner,target,arrival-1,run.Revision));
                NewGameWorldTests.At(run,Math.Max(run.Seconds,Arrival(notice.Id))+2);
                var known=run.DiagnosticSnapshot();
                Assert.True(PeaceMediation.KnowsRefusal(known,recipient,owner,target,run.Seconds,run.Revision));
                Assert.False(PeaceMediation.KnowsRefusal(known,CentralCourtCatalog.Shogun,owner,target,run.Seconds,run.Revision));
                var restoredNotice=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(known))).DiagnosticSnapshot();
                var report=Assert.Single(PeaceMediation.KnownRefusals(restoredNotice,recipient,owner,target,run.Seconds,run.Revision));
                var declaration=new FactionStanceInput(new("decision:cite-refusal"),FactionStanceAction.DeclareHostile,target,owner,
                    run.Seconds,run.Seconds+TruceNegotiations.Day){RefusalReport=report};
                Assert.Throws<InvalidDataException>(()=>FactionStances.Apply(restoredNotice,declaration with{RefusalReport=new("request:unrelated")},recipient));
                FactionStances.Apply(restoredNotice,declaration,recipient);
                Assert.Equal(report,FactionStances.ClaimAt(restoredNotice,target,owner,run.Seconds,run.Revision)!.RefusalReport);
                Assert.Equal(report,Assert.Single(FactionStances.KnownOwnDeclarations(restoredNotice,recipient),c=>c.Declaration==declaration.Id).RefusalReport);
            }
            if(agree){
                var certificate=settled.Messaging.Messages.Single(m=>m.Kind==PeaceMediation.Certificate && m.RecipientId==run.PlayerControl.Value);
                NewGameWorldTests.At(run,Math.Max(run.Seconds,Arrival(certificate.Id))+1);
                Good(run.Open(run.PlayerControl,new(certificate.Id),run.Revision));
                var final=run.DiagnosticSnapshot();
                Assert.True(TruceNegotiations.WillNotInitiate(final,owner,target,run.Seconds,run.Revision));
                var loaded=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(final)));
                Assert.True(TruceNegotiations.WillNotInitiate(loaded.DiagnosticSnapshot(),owner,target,loaded.Seconds,loaded.Revision));
            }
        }
        Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(true,true)][InlineData(false,true)]
    public void PetitionRequiresMeetingAndBrokerAnswersOnceAfterRestore(bool gift,bool currentEdition)
    {
        var broker=CentralCourtCatalog.CourtBroker;var site=CentralCourtCatalog.Kyoto;var owner=new EntityId("org:court-test");
        var applicant=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);
        var edition=currentEdition?HistoricalStartCatalog.Current:CentralCourtCatalog.Id;
        var raw=NewGameWorld.Scenario(edition);
        var request=new WorldSetupRequest([applicant],[new(applicant.Id,owner.Value,[site.Value],false)]);
        var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var s=new WorldSession(NewGameWorld.Create(setup,[applicant],applicant.Id,historicalCatalog:edition,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy));
        var ask=new CourtInput(new("request:petition"),CourtAction.AskCourtIntroduction,broker,owner,site);
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(ask,s.Revision).Code);
        var visit=s.ManageSocial(new(new("request:visit-courtier"),SocialWorldAction.Visit,broker),s.Revision);
        Assert.True(visit.Code==ResultCode.Accepted,visit.VisibleReason);
        if(gift){
            var offered=s.ManageSocial(new(new("request:court-gift"),SocialWorldAction.DispatchGift,broker,Amount:100),s.Revision);
            Assert.True(offered.Code==ResultCode.Accepted,offered.VisibleReason);NewGameWorldTests.At(s,s.Seconds+3);
        }
        var expectedBalance=NewGameWork.Balance(s.DiagnosticSnapshot(),s.PlayerControl)-(currentEdition?CentralRank.Offering:0);
        var petition=s.ManageCourt(ask,s.Revision);Assert.True(petition.Code==ResultCode.Accepted,petition.VisibleReason);
        Assert.Equal(expectedBalance,NewGameWork.Balance(s.DiagnosticSnapshot(),s.PlayerControl));
        var saved=s.DiagnosticSnapshot();Assert.DoesNotContain(saved.Court!.Events,e=>e.Input.Reference==ask.Id);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(saved)));
        foreach(var run in new[]{s,restored}){
            NewGameWorldTests.At(run,run.Seconds+2);var w=run.DiagnosticSnapshot();
            var reply=Assert.Single(w.Court!.Events,e=>e.Input.Reference==ask.Id);
            Assert.Equal(gift?CourtAction.AcceptCourtIntroduction:CourtAction.DeclineCourtIntroduction,reply.Input.Action);
            Assert.Equal(broker,reply.Actor);Assert.Equal(run.PlayerControl,reply.Input.Person);
            Assert.Equal(ResultCode.AlreadyAccepted,run.ManageCourt(ask,run.Revision).Code);
            Assert.Equal(expectedBalance,NewGameWork.Balance(run.DiagnosticSnapshot(),run.PlayerControl));
        }
        if(gift){
            foreach(var run in new[]{s,restored}){
                NewGameWorldTests.At(run,run.Seconds+2);
                var before=run.DiagnosticSnapshot();
                var filed=Assert.Single(before.Court!.Events,e=>e.Input.Action==CourtAction.FileRankPetition);
                var date=WorldCalendar.At(before.EpochDate,filed.At);
                var delay=currentEdition?(long)(date.AddMonths(2)-date).TotalSeconds:86400;
                NewGameWorldTests.At(run,filed.At+delay-1);
                Assert.DoesNotContain(run.DiagnosticSnapshot().Court!.Events,e=>e.Input.Action==CourtAction.IssueCourtRank);
                NewGameWorldTests.At(run,filed.At+delay);
                var issued=run.DiagnosticSnapshot();
                Assert.Single(issued.Court!.Events,e=>e.Input.Action==CourtAction.IssueCourtRank);
                Assert.Null(CentralRank.KnownRank(issued,run.PlayerControl));
                var letter=Assert.Single(issued.Messaging.Messages,m=>m.Kind==CentralRank.ReplyKind);
                NewGameWorldTests.At(run,run.Seconds+3);
                Assert.Null(CentralRank.KnownRank(run.DiagnosticSnapshot(),run.PlayerControl));
                var read=run.Open(run.PlayerControl,new(letter.Id),run.Revision);
                Assert.True(read.Code==ResultCode.Accepted,read.VisibleReason);
                Assert.Equal(CentralRank.RankName,CentralRank.KnownRank(run.DiagnosticSnapshot(),run.PlayerControl));
                Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(run.DiagnosticSnapshot().LandRights));
            }
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
}
