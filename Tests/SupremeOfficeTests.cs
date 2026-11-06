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
    public void ImperialInvitationHonorsOpeningAndDeferralInHegemonFixture()
    {
        var home=CentralCourtCatalog.Kyoto;var broker=CentralCourtCatalog.CourtBroker;
        var leader=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:home.Value);
        var owner=new EntityId("org:hegemon");var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var request=new WorldSetupRequest([leader],[new(leader.Id,owner.Value,[home.Value],false)]);
        var scenario=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var w=NewGameWorld.Create(scenario,[leader],leader.Id,historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy);
        // 領国拡大後の断面を用意する。登録時の領土予算は通常の開始処理で検査される。
        var land=w.LandRights.Single(r=>r.Subject==owner && r.Target==home);
        var other=raw.Castles.Where(c=>c.Id!=home.Value).OrderByDescending(c=>HouseTerritoryScale.Province(new(c.Id))!=null).Take(14);
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
        w.ElapsedSeconds+=2;
        HakutaiKagami.Core.Messaging.MessageFlow.Advance(w.Messaging,w.ElapsedSeconds);
        if(!HakutaiKagami.Core.Messaging.MessageFlow.Knows(w.Messaging,letter.Id,leader.Id))
            w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(letter.Id,leader.Id,w.ElapsedSeconds,letter.Id)];
        w.CommitSequence++;
        HouseholdCourt.Execute(w,choice with{Id=new("request:defer"),Action=CourtAction.DeferInvitation},w.PlayerControl);
        var copy=ContractJson.Copy(w);
        foreach(var run in new[]{w,copy}){
            run.CommitSequence++;
            HouseholdCourt.Execute(run,choice,run.PlayerControl);
            Assert.Empty(CourtInvitation.Available(run,run.PlayerControl));
            Assert.Empty(SupremeInvestiture.Responses(run));
            Assert.Single(run.Messaging.Messages,m=>m.Kind=="imperial-reply" && m.RecipientId==broker.Value);
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
        Good(s.ManageCourt(petition,s.Revision));
        var before=s.DiagnosticSnapshot();var restored=new WorldSession(ContractJson.Copy(before));
        foreach(var session in new[]{s,restored}){
            NewGameWorldTests.At(session,session.Seconds+3);
            var w=session.DiagnosticSnapshot();
            Assert.Single(w.Court!.Events,e=>e.Input.Reference==petition.Id && e.Input.Action==CourtAction.GrantHighOffice);
            Assert.Single(w.Messaging.Messages,m=>m.Kind=="court-investiture" && m.RecipientId==child.Id);
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
        var request=new WorldSetupRequest([child],[new(child.Id,owner.Value,[site.Value],false)]);
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
        var restored=new WorldSession(ContractJson.Copy(game.DiagnosticSnapshot()));
        foreach(var run in new[]{game,restored}){
            NewGameWorldTests.At(run,run.Seconds+2);
            var final=run.DiagnosticSnapshot();
            Assert.Equal(CentralCourtCatalog.CourtBroker,Assert.Single(final.Court!.Events,e=>e.Input.Action==CourtAction.RecognizePedigree).Actor);
            Assert.True(CourtPedigree.Qualified(final,run.PlayerControl,run.Seconds,run.Revision));
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
            Assert.Equal(ResultCode.NotAvailable,run.ManageCourt(offer with{Id=new("request:duplicate-offer")},run.Revision).Code);
        }
        Assert.Equal(ContractJson.Write(game.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
        var expel=new CourtInput(new("request:expel"),CourtAction.ExpelShogun,CentralCourtCatalog.Shogun,house,TempleMilitary.Site);
        var ordered=game.ManageCourt(expel,game.Revision);Assert.True(ordered.Code==ResultCode.Accepted,ordered.VisibleReason);
        Assert.Equal(CentralCourtCatalog.Shogun,SupremeOffice.ShogunAt(game.DiagnosticSnapshot(),game.Seconds,game.Revision));
        NewGameWorldTests.At(game,game.Seconds+1);NewGameWorldTests.At(game,game.Seconds+2);
        var moving=game.DiagnosticSnapshot();
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
        var input=new CourtInput(new("request:resign-shogun"),CourtAction.ResignShogun,actor,SupremeOffice.Bakufu,CentralCourtCatalog.Kyoto);
        var result=game.ManageCourt(input,game.Revision);
        Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(game.DiagnosticSnapshot())));
        var state=restored.DiagnosticSnapshot();
        Assert.Equal(actor,SupremeOffice.ShogunAt(state,game.Seconds,before));
        Assert.Null(SupremeOffice.ShogunAt(state,game.Seconds,restored.Revision));
        Assert.Equal(ResultCode.AlreadyAccepted,restored.ManageCourt(input,restored.Revision).Code);
        Assert.Equal(ResultCode.NotAvailable,restored.ManageCourt(input with{Id=new("request:again")},restored.Revision).Code);
    }
}