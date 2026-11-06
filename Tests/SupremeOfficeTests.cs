using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class SupremeOfficeTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public void KonoeAnswersInPersonWithoutChangingBirthFamilyOrDomain(bool gift)
    {
        var site=CentralCourtCatalog.Kyoto;var patron=CentralCourtCatalog.Konoe;
        var child=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);var owner=new EntityId("org:regent-test");
        var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        Assert.DoesNotContain(NewGameWorld.Scenario(CentralCourtCatalog.TempleEdition).People,p=>p.Id==patron.Value);
        var request=new WorldSetupRequest([child],[new(child.Id,owner.Value,[site.Value],false)]);
        var scenario=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var s=new WorldSession(NewGameWorld.Create(scenario,[child],child.Id,historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy));
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        Good(s.ManageSocial(new(new("request:meet-konoe"),SocialWorldAction.Visit,patron),s.Revision));
        if(gift){Good(s.ManageSocial(new(new("request:konoe-gift"),SocialWorldAction.DispatchGift,patron,Amount:100),s.Revision));NewGameWorldTests.At(s,s.Seconds+3);}
        Good(s.ManageCourt(new(new("request:konoe-kin"),CourtAction.RequestRegentKin,patron,owner,site),s.Revision));
        var before=s.DiagnosticSnapshot();Assert.False(RegentKin.Qualified(before,s.PlayerControl,s.Seconds,s.Revision));
        var restored=new WorldSession(ContractJson.Copy(before));
        foreach(var run in new[]{s,restored}){
            NewGameWorldTests.At(run,run.Seconds+2);var w=run.DiagnosticSnapshot();
            Assert.Equal(gift,RegentKin.Qualified(w,run.PlayerControl,run.Seconds,run.Revision));
            Assert.Single(w.Court!.Events,e=>e.Actor==patron&&e.Input.Action==(gift?CourtAction.AcceptRegentKin:CourtAction.DeclineRegentKin));
            Assert.False(CourtPedigree.Qualified(w,run.PlayerControl,run.Seconds,run.Revision));
            Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(w.LandRights));
            Assert.Equal(ContractJson.Write(before.NewGame!.Start.People),ContractJson.Write(w.NewGame!.Start.People));
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
    [Fact]
    public void ShogunAwardsHouseStandingAndPersonalPrivilegeOnlyOnce()
    {
        var shogun=CentralCourtCatalog.Shogun;var kyoto=CentralCourtCatalog.Kyoto;
        var lord=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:kyoto.Value);
        var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var setup=new WorldSetupRequest([lord],[new(lord.Id,"org:honoree",[kyoto.Value],false)]);
        var scenario=NewGameSetup.Confirm(raw,setup,NewGameSetup.Preview(raw,setup));
        var s=new WorldSession(NewGameWorld.Create(scenario,[lord],shogun.Value,historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy));
        var before=s.DiagnosticSnapshot();
        var offers=ShogunateHonors.Available(before,shogun).Where(i=>i.Person.Value==lord.Id).ToArray();
        Assert.Equal(2,offers.Length);
        foreach(var honor in offers){
            var result=s.ManageCourt(honor,s.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
            Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(honor with{Id=HouseholdCourtTestsId(honor.Action)},s.Revision).Code);
        }
        var path=Path.GetTempFileName();
        try{
            File.WriteAllBytes(path,ContractJson.Write(s.DiagnosticSnapshot()));
            var loaded=new WorldSession(ContractJson.Read<ContractWorld>(File.ReadAllBytes(path))).DiagnosticSnapshot();
            Assert.Equal(2,ShogunateHonors.Witnessed(loaded,new(lord.Id)).Count());
            Assert.DoesNotContain(ShogunateHonors.Available(loaded,shogun),i=>i.Person.Value==lord.Id);
            Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(loaded.LandRights));
            Assert.Equal(ContractJson.Write(before.Grants),ContractJson.Write(loaded.Grants));
        }finally{File.Delete(path);}
        static EntityId HouseholdCourtTestsId(CourtAction action)=>new("request:repeat-"+action.ToString().ToLowerInvariant());
    }
    [Fact]
    public void ImperialInvitationHonorsOpeningAndDeferralInHegemonFixture()
    {
        var home=TempleMilitary.Site;var broker=CentralCourtCatalog.CourtBroker;
        var leader=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:home.Value);
        var owner=new EntityId("org:hegemon");var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var request=new WorldSetupRequest([leader],[new(leader.Id,owner.Value,[home.Value],false)]);
        var scenario=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var w=NewGameWorld.Create(scenario,[leader],leader.Id,historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy);
        // 領国拡大後の断面を用意する。登録時の領土予算は通常の開始処理で検査される。
        var land=w.LandRights.Single(r=>r.Subject==owner && r.Target==home);
        var other=raw.Castles.Where(c=>c.Id!=home.Value).OrderByDescending(c=>c.Id==CentralCourtCatalog.Kyoto.Value).ThenByDescending(c=>HouseTerritoryScale.Province(new(c.Id))!=null).Take(14);
        foreach(var castle in other){
            w.LandRights=[..w.LandRights.Where(r=>r.Target.Value!=castle.Id),land with{Id=new("land:test/"+castle.Id[5..]),Target=new(castle.Id)}];
        }
        w.CommitSequence++;
        HouseholdCourt.Execute(w,new(new("request:hegemon"),CourtAction.DeclareScale,w.PlayerControl,owner,home,Scale:CourtScale.Hegemon),w.PlayerControl);
        var offer=Assert.Single(CourtInvitation.Offers(w));w.CommitSequence++;
        HouseholdCourt.Execute(w,offer,broker);
        var letter=Assert.Single(w.Messaging.Messages,m=>m.Kind=="imperial-invitation");
        var choice=new CourtInput(new("request:choose-office"),CourtAction.PetitionChancellor,broker,owner,home,Reference:offer.Id);
        Assert.Throws<InvalidDataException>(()=>HouseholdCourt.Execute(w,choice,w.PlayerControl));
        void Deliver(ContractWorld state,string message,EntityId destination){
            var attempt=state.Messaging.Attempts.Single(a=>a.MessageId==message);
            var mission=state.Couriers!.Missions.Single(m=>m.Attempt==attempt.Id);
            Assert.Equal(NewGameWorld.Node(destination.Value),mission.Destination);
            Assert.True(mission.ReadyAt>attempt.SentAt);
            state.ElapsedSeconds=mission.ReadyAt;RegionalCampaign.Arrivals(state);
            HakutaiKagami.Core.Messaging.MessageFlow.Advance(state.Messaging,state.ElapsedSeconds);
            Assert.Contains(state.Messaging.Receipts,r=>r.MessageId==message);
        }
        Deliver(w,letter.Id,home);

        Assert.True(HakutaiKagami.Core.Messaging.MessageFlow.Knows(w.Messaging,letter.Id,leader.Id));

        w.CommitSequence++;
        HouseholdCourt.Execute(w,choice with{Id=new("request:defer"),Action=CourtAction.DeferInvitation},w.PlayerControl);
        var copy=ContractJson.Copy(w);
        foreach(var run in new[]{w,copy}){
            run.CommitSequence++;
            HouseholdCourt.Execute(run,choice,run.PlayerControl);
            Assert.Empty(CourtInvitation.Available(run,run.PlayerControl));
            Assert.Empty(SupremeInvestiture.Responses(run));
            var reply=Assert.Single(run.Messaging.Messages,m=>m.Kind=="imperial-reply" && m.RecipientId==broker.Value);
            Deliver(run,reply.Id,CentralCourtCatalog.Kyoto);
            var grant=Assert.Single(SupremeInvestiture.Responses(run));run.CommitSequence++;
            HouseholdCourt.Execute(run,grant,broker);
            var notice=Assert.Single(run.Messaging.Messages,m=>m.Kind=="court-investiture");
            Deliver(run,notice.Id,home);
            Assert.Contains(SupremeInvestiture.Notices(run,run.PlayerControl),n=>n.Known=="太政大臣への任官を裁可する。");
        }
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(copy));
    }
    [Fact]
    public void RegentChangesAtDecisionAndPetitionSurvivesRestore()
    {
        var site=CentralCourtCatalog.Kyoto;var broker=CentralCourtCatalog.CourtBroker;
        var child=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);
        var house=new EntityId("org:regency");
        var setup=new WorldSetupRequest([child],[new(child.Id,house.Value,[site.Value],false)]);
        var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var scenario=NewGameSetup.Confirm(raw,setup,NewGameSetup.Preview(raw,setup));
        var s=new WorldSession(NewGameWorld.Create(scenario,[child],child.Id,historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy));
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        var petition=new CourtInput(new("request:petition-regent"),CourtAction.PetitionRegent,broker,house,site);
        Assert.NotEqual(ResultCode.Accepted,s.ManageCourt(petition,s.Revision).Code);
        var patron=CentralCourtCatalog.Konoe;
        Good(s.ManageSocial(new(new("request:meet-konoe"),SocialWorldAction.Visit,patron),s.Revision));
        Good(s.ManageSocial(new(new("request:konoe-gift"),SocialWorldAction.DispatchGift,patron,Amount:100),s.Revision));
        NewGameWorldTests.At(s,s.Seconds+3);
        Good(s.ManageCourt(new(new("request:konoe-kin"),CourtAction.RequestRegentKin,patron,house,site),s.Revision));
        NewGameWorldTests.At(s,s.Seconds+2);
        Good(s.ManageCourt(petition with{Id=new("request:first-petition")},s.Revision));
        NewGameWorldTests.At(s,s.Seconds+2);
        Assert.Single(s.DiagnosticSnapshot().Court!.Events,e=>e.Input.Action==CourtAction.RefuseHighOffice);
        Good(s.ManageSocial(new(new("request:visit-regent-broker"),SocialWorldAction.Visit,broker),s.Revision));
        Good(s.ManageSocial(new(new("request:gift-regent-broker"),SocialWorldAction.DispatchGift,broker,Amount:100),s.Revision));
        NewGameWorldTests.At(s,s.Seconds+3);
        Good(s.ManageCourt(petition,s.Revision));
        var before=s.DiagnosticSnapshot();var restored=new WorldSession(ContractJson.Copy(before));
        foreach(var session in new[]{s,restored}){
            NewGameWorldTests.At(session,session.Seconds+3);
            var w=session.DiagnosticSnapshot();
            Assert.Single(w.Court!.Events,e=>e.Input.Reference==petition.Id && e.Input.Action==CourtAction.GrantHighOffice);
            Assert.Single(w.Messaging.Messages,m=>m.Kind=="court-investiture" && m.RecipientId==child.Id && m.Body.Contains("裁可する"));
            Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(w.LandRights));
            Assert.NotEqual(ResultCode.Accepted,session.ManageCourt(petition with{Id=new("request:again")},session.Revision).Code);
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
    [Fact]
    public void AdoptedChildQualifiesOnlyAfterCourtCertifiesFiledReport()
    {
        var site=CentralCourtCatalog.Kyoto;
        var child=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);
        var owner=new EntityId("org:pedigree");
        var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var request=new WorldSetupRequest([child],[new(child.Id,owner.Value,[site.Value,"site:kofu"],false)]);
        var scenario=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var parent=CentralCourtCatalog.Shogun;
        var game=new WorldSession(NewGameWorld.Create(scenario,[child],child.Id,42,historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy,
            developmentPolicy:CharacterDevelopment.Policy,socialPolicy:CharacterRelations.Policy,adoptionPolicy:AdultAdoption.Policy));
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        var adoption=new EntityId("request:genji-adoption");
        Good(game.DecideAdoption(new(adoption,AdoptionAction.Propose,parent,false),game.PlayerControl,game.Revision));
        NewGameWorldTests.At(game,game.Seconds+2);
        Good(game.DecideAdoption(new(new("request:house-assent"),AdoptionAction.ApproveHouse,Original:adoption,House:owner),game.PlayerControl,game.Revision));
        Good(game.DecideAdoption(new(new("request:implement"),AdoptionAction.Implement,Original:adoption),game.PlayerControl,game.Revision));
        Assert.False(CourtPedigree.Qualified(game.DiagnosticSnapshot(),game.PlayerControl,game.Seconds,game.Revision));
        Good(game.ManageCourt(new(new("request:research"),CourtAction.AskPedigree,CentralCourtCatalog.CourtBroker,owner,site),game.Revision));
        NewGameWorldTests.At(game,game.Seconds+2);
        var report=Assert.Single(game.DiagnosticSnapshot().Court!.Events,e=>e.Input.Action==CourtAction.AttestPedigree);
        Good(game.ManageCourt(new(new("request:file"),CourtAction.FilePedigree,CentralCourtCatalog.CourtBroker,owner,site,Reference:report.Input.Id),game.Revision));
        Assert.False(CourtPedigree.Qualified(game.DiagnosticSnapshot(),game.PlayerControl,game.Seconds,game.Revision));
        Good(game.ManageSocial(new(new("request:visit-court-broker"),SocialWorldAction.Visit,CentralCourtCatalog.CourtBroker),game.Revision));
        Good(game.ManageSocial(new(new("request:gift-court-broker"),SocialWorldAction.DispatchGift,CentralCourtCatalog.CourtBroker,Amount:100),game.Revision));
        NewGameWorldTests.At(game,game.Seconds+3);
        var goBetween=CentralCourtCatalog.ShogunBroker;var kai=new EntityId("site:kofu");
        Good(game.ManageSocial(new(new("request:visit-go-between"),SocialWorldAction.Visit,goBetween),game.Revision));
        Good(game.ManageSocial(new(new("request:gift-go-between"),SocialWorldAction.DispatchGift,goBetween,Amount:100),game.Revision));
        NewGameWorldTests.At(game,game.Seconds+3);
        Good(game.ManageCourt(new(new("request:old-shugo"),CourtAction.AskShugo,goBetween,owner,kai),game.Revision));
        NewGameWorldTests.At(game,game.Seconds+1);
        var waiting=game.DiagnosticSnapshot();
        var oldPetition=Assert.Single(waiting.Court!.Events,e=>e.Input.Action==CourtAction.RecommendShugo);
        Assert.Equal(parent.Value,Assert.Single(waiting.Messaging.Messages,m=>m.Kind==ShogunateAppointments.Petition).RecipientId);
        Assert.DoesNotContain(waiting.Court.Events,e=>e.Input.Action==CourtAction.AppointShugo);
        var restored=new WorldSession(ContractJson.Copy(waiting));
        foreach(var run in new[]{game,restored}){
            var final=run.DiagnosticSnapshot();
            Assert.Equal(CentralCourtCatalog.CourtBroker,Assert.Single(final.Court!.Events,e=>e.Input.Action==CourtAction.RecognizePedigree).Actor);
            Assert.True(CourtPedigree.Qualified(final,run.PlayerControl,run.Seconds,run.Revision));
            var petition=new CourtInput(new("request:successor"),CourtAction.PetitionShogun,CentralCourtCatalog.CourtBroker,owner,site);
            Assert.Equal(ResultCode.NotAvailable,run.ManageCourt(petition,run.Revision).Code);
            Good(run.ManageCourt(new(new("request:remove-incumbent"),CourtAction.ExpelShogun,parent,owner,TempleMilitary.Site),run.Revision));
            NewGameWorldTests.At(run,run.Seconds+1);NewGameWorldTests.At(run,run.Seconds+2);
            Assert.Null(SupremeOffice.ShogunAt(run.DiagnosticSnapshot(),run.Seconds,run.Revision));
            Good(run.ManageCourt(petition,run.Revision));
            NewGameWorldTests.At(run,run.Seconds+2);
            var enthroned=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(run.DiagnosticSnapshot())));
            var state=enthroned.DiagnosticSnapshot();
            Assert.Equal(run.PlayerControl,SupremeOffice.ShogunAt(state,run.Seconds,run.Revision));
            Assert.Contains(run.PlayerControl,PeaceMediation.Brokers(state));
            Assert.DoesNotContain(parent,PeaceMediation.Brokers(state));
            var usurp=new CourtInput(new("request:rule-on-old-petition"),CourtAction.AppointShugo,run.PlayerControl,owner,kai,Reference:oldPetition.Input.Id);
            Assert.Equal(ResultCode.NotAvailable,enthroned.ManageCourt(usurp,enthroned.Revision).Code);
            Assert.DoesNotContain(ShogunateAppointments.AvailableAnswers(state,run.PlayerControl),i=>i.Reference==oldPetition.Input.Id);
            Assert.Equal(ContractJson.Write(final.LandRights),ContractJson.Write(state.LandRights));
        }
        Assert.Equal(ContractJson.Write(game.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
    [Fact]
    public void CandidateAnswersTheSponsorAfterRestoreWithoutTakingIncumbentsOffice()
    {
        var site=CentralCourtCatalog.Kyoto;
        var sponsor=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:site.Value);
        var house=new EntityId("org:kyoto-sponsor");
        var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var request=new WorldSetupRequest([sponsor],[new(sponsor.Id,house.Value,[site.Value],false)]);
        var scenario=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var game=new WorldSession(NewGameWorld.Create(scenario,[sponsor],sponsor.Id,historicalCatalog:HistoricalStartCatalog.Current,
            workPolicy:WorkFollowup.Policy,inspectionPolicy:LocalInspectionFindings.Policy));
        var genealogy=new CourtInput(new("request:pedigree"),CourtAction.AskPedigree,CentralCourtCatalog.CourtBroker,house,site);
        var started=game.ManageCourt(genealogy,game.Revision);Assert.True(started.Code==ResultCode.Accepted,started.VisibleReason);
        NewGameWorldTests.At(game,game.Seconds+1);
        var researched=game.DiagnosticSnapshot();
        Assert.Single(researched.Court!.Events,e=>e.Input.Action==CourtAction.ReportMissingPedigree && e.Input.Reference==genealogy.Id);
        Assert.False(CourtPedigree.Qualified(researched,game.PlayerControl,game.Seconds,game.Revision));
        Assert.True(CourtPedigree.Qualified(researched,CentralCourtCatalog.Shogun,game.Seconds,game.Revision));
        var candidate=CentralCourtCatalog.ShogunBroker;
        var offer=new CourtInput(new("request:sponsor"),CourtAction.OfferShogunSupport,candidate,house,site);
        var result=game.ManageCourt(offer,game.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(game.DiagnosticSnapshot())));
        foreach(var run in new[]{game,restored}){
            NewGameWorldTests.At(run,run.Seconds+2);
            var w=run.DiagnosticSnapshot();
            Assert.Single(w.Court!.Events,e=>e.Input.Action==CourtAction.AcceptShogunSupport && e.Actor==candidate && e.Input.Reference==offer.Id);
            Assert.Equal(CentralCourtCatalog.Shogun,SupremeOffice.ShogunAt(w,run.Seconds,run.Revision));
            Assert.Contains(house,SupremeOffice.Sponsors(w,candidate,run.Seconds,run.Revision));
            Assert.Equal(ResultCode.NotAvailable,run.ManageCourt(offer with{Id=new("request:duplicate-offer")},run.Revision).Code);
        }
        Assert.Equal(ContractJson.Write(game.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
        var expel=new CourtInput(new("request:expel"),CourtAction.ExpelShogun,CentralCourtCatalog.Shogun,house,TempleMilitary.Site);
        var ordered=game.ManageCourt(expel,game.Revision);Assert.True(ordered.Code==ResultCode.Accepted,ordered.VisibleReason);
        Assert.Equal(CentralCourtCatalog.Shogun,SupremeOffice.ShogunAt(game.DiagnosticSnapshot(),game.Seconds,game.Revision));
        NewGameWorldTests.At(game,game.Seconds+1);NewGameWorldTests.At(game,game.Seconds+2);
        var moving=game.DiagnosticSnapshot();
        Assert.Contains(SocialWorld.Renown(moving,CentralCourtCatalog.Shogun),f=>f.Id==expel.Id.Value && f.Kind==RenownKind.Infamy && f.Major);
        Assert.DoesNotContain(SocialWorld.Renown(moving,CentralCourtCatalog.CourtBroker),f=>f.Id==expel.Id.Value);
        var trip=Assert.Single(moving.Branches!.Journeys,j=>j.Actor==CentralCourtCatalog.Shogun);
        Assert.Null(SupremeOffice.ShogunAt(moving,game.Seconds,game.Revision));
        var travellingSave=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(moving)));
        foreach(var run in new[]{game,travellingSave}){
            NewGameWorldTests.At(run,trip.Legs[^1].EndAt+1);
            Assert.Equal(TempleMilitary.Site,SocialWorld.Site(run.DiagnosticSnapshot(),CentralCourtCatalog.Shogun));
            NewGameWorldTests.At(run,run.Seconds+2);
            var sent=run.DiagnosticSnapshot();
            var letter=Assert.Single(sent.Messaging.Messages,m=>m.Kind==ExileLetters.Kind);
            var mission=sent.Couriers!.Missions.Single(m=>sent.Messaging.Attempts.Any(a=>a.Id==m.Attempt && a.MessageId==letter.Id));
            Assert.Equal("kyoto",mission.Destination);
            Assert.Equal(ResultCode.NotAvailable,run.Open(run.PlayerControl,new(letter.Id),run.Revision).Code);
            NewGameWorldTests.At(run,mission.ReadyAt+1);
            Assert.Equal(ResultCode.Accepted,run.Open(run.PlayerControl,new(letter.Id),run.Revision).Code);
            var receipt=run.DiagnosticSnapshot();
            Assert.Null(SupremeOffice.ShogunAt(receipt,run.Seconds,run.Revision));
            Assert.Contains(run.GetActorView(run.PlayerControl).Messages,m=>m.Id.Value==letter.Id && m.KnownBody==letter.Body);
            _=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(receipt)));
        }
        Assert.Equal(ContractJson.Write(game.DiagnosticSnapshot()),ContractJson.Write(travellingSave.DiagnosticSnapshot()));
    }
    [Fact]
    public void IncumbentCanResignOnceAndVacancySurvivesSave()
    {
        var actor=CentralCourtCatalog.Shogun;
        var scenario=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var game=new WorldSession(NewGameWorld.Create(scenario,[],actor.Value,historicalCatalog:HistoricalStartCatalog.Current,
            workPolicy:WorkFollowup.Policy,inspectionPolicy:LocalInspectionFindings.Policy));
        var before=game.Revision;
        Assert.Contains(actor,PeaceMediation.Brokers(game.DiagnosticSnapshot()));
        var input=new CourtInput(new("request:resign-shogun"),CourtAction.ResignShogun,actor,SupremeOffice.Bakufu,CentralCourtCatalog.Kyoto);
        var result=game.ManageCourt(input,game.Revision);
        Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(game.DiagnosticSnapshot())));
        var state=restored.DiagnosticSnapshot();
        Assert.Equal(actor,SupremeOffice.ShogunAt(state,game.Seconds,before));
        Assert.Null(SupremeOffice.ShogunAt(state,game.Seconds,restored.Revision));
        Assert.DoesNotContain(actor,PeaceMediation.Brokers(state));
        Assert.Contains(CentralCourtCatalog.ShogunBroker,PeaceMediation.Brokers(state));
        Assert.Equal(ResultCode.AlreadyAccepted,restored.ManageCourt(input,restored.Revision).Code);
        Assert.Equal(ResultCode.NotAvailable,restored.ManageCourt(input with{Id=new("request:again")},restored.Revision).Code);
    }
}