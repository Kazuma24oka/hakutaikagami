using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class HouseholdCourtTests
{
 internal static readonly EntityId Owner=new("org:oda"),Site=new("site:castle"),Ronin=new("created:court-ronin"),Retainer=new("created:court-retainer");
 internal static WorldSession Start(string player="person:nobunaga") {var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer(Ronin.Value),NewGameWorldTests.Officer(Retainer.Value,StartTemplate.Retainer,lord:"person:nobunaga")]);return new(NewGameWorld.Create(setup.World,setup.Registered,player,42,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy));}
 static void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
 static CourtInput Input(WorldSession s,CourtAction action,EntityId? person=null,long amount=0,EntityId? reference=null,CourtRole role=CourtRole.Steward,long when=0)=>new(new("request:court-test/"+s.Revision),action,person??Ronin,Owner,Site,role,amount,reference,when);
 [Theory]
 [InlineData(NpcAspiration.ProsperDomain, CourtCaseKind.RelieveVillage)]
 [InlineData(NpcAspiration.PreserveHouse, CourtCaseKind.BuyWeapons)]
 public void RegisteredLordOrdersOnlyReceivedCases(NpcAspiration aspiration, CourtCaseKind first)
 {
     var lord = NewGameWorldTests.Officer(role:StartTemplate.Daimyo) with { Aspiration = aspiration };
     var member = NewGameWorldTests.Officer(Retainer.Value, StartTemplate.Retainer, lord:lord.Id);
     var house = new EntityId("org:agenda");
     var setup = NewGameWorldTests.Setup([lord,member],[new(lord.Id,house.Value,[lord.Site],false)]);
     var session = new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,member.Id,42,
         inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy));
     var world = session.DiagnosticSnapshot();
     foreach (var kind in new[] { CourtCaseKind.BuyWeapons, CourtCaseKind.RelieveVillage })
     {
         var proposal = new CourtInput(new("request:agenda/"+kind),CourtAction.SubmitCase,new(lord.Id),house,
             new(lord.Site),Amount:1,Case:kind);
         world.CommitSequence++;
         HouseholdCourt.Execute(world, proposal, session.PlayerControl);
     }
     Assert.Empty(HouseholdCourt.CouncilAgenda(world));
     world.CommitSequence++;
     SocialWorld.Tick(world);
     var saved = ContractJson.Write(world);
     var restored = new WorldSession(ContractJson.Read<ContractWorld>(saved)).DiagnosticSnapshot();
     var agenda = HouseholdCourt.CouncilAgenda(restored);
     Assert.Equal(2,agenda.Length);
     Assert.Equal(first,agenda[0].Input.Case);
     Assert.Equal(saved,ContractJson.Write(restored));
 }
 [Theory]
 [InlineData(5,20,true)]
 [InlineData(1,5,false)]
 public void ReceivedMusterMovesTheRetainerToAnotherCastleBeforeAttendance(int days,int rice,bool contractsSurvive)
 {
     var lord=NewGameWorldTests.Officer(role:StartTemplate.Daimyo);
     var member=NewGameWorldTests.Officer(Retainer.Value,StartTemplate.Retainer,lord:lord.Id);
     var setup=NewGameWorldTests.Setup([lord,member],[new(lord.Id,"org:muster",[lord.Site,"site:kofu"],false)]);
     var initial=NewGameWorld.Create(setup.World,setup.Registered,lord.Id,42,inspectionPolicy:LocalInspectionFindings.Policy,
         workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy,economyPolicy:MilitaryEconomy.Policy);
     var session=new WorldSession(initial);
     void Pay(SalaryAction action,EntityId? contract=null,long amount=0,EntityId? prep=null)
     {
         Good(session.ManageSalary(new(new("request:march-pay/"+session.Revision),action,Retainer,Site,contract,amount,prep),session.PlayerControl,session.Revision));
     }
     Pay(SalaryAction.Propose,amount:20);
     var quote=session.DiagnosticSnapshot().Salary!.Quotes.Single().Id;
     Pay(SalaryAction.Confirm,quote);
     NewGameWorldTests.At(session,session.DiagnosticSnapshot().Salary!.Terms.Single().DueAt[0]);
     Pay(SalaryAction.Prepare,quote,rice);
     Pay(SalaryAction.Receive,quote,prep:session.DiagnosticSnapshot().Salary!.Entries.Last().Input.Id);
     var order=new CourtInput(new("request:assemble-kofu"),CourtAction.SendMusterNotice,Retainer,new("org:muster"),new("site:kofu"),Amount:6,When:session.Seconds+days*86400);
     Good(session.ManageCourt(order,session.Revision));
     NewGameWorldTests.At(session,session.Seconds+5);
     var moving=session.DiagnosticSnapshot();
     var trip=Assert.Single(moving.Requests,r=>r.Intent.RoutePolicy==MusterNotices.AssemblyRoute);
     Assert.Equal(Retainer,trip.Intent.Actor);
     Assert.Equal(order.Site,trip.Intent.DestinationSite);
     Assert.Equal(0,PrivateRecruitment.Soldiers(moving,Retainer,Site,session.Seconds,session.Revision+1));
     Assert.Equal(0,PrivateRecruitment.Soldiers(moving,Retainer,order.Site,session.Seconds,session.Revision+1));
     var mustered=moving.Court!.Events.Where(e=>e.Input.Action==CourtAction.HireMarketSoldiers && e.Actor==Retainer).ToArray();
     Assert.NotEmpty(mustered);
     Assert.Equal((long)rice,mustered.Sum(e=>e.Input.Amount*((e.Input.When-e.At)/86400))+HouseSalary.PrivateRice(moving,Retainer));
     session.SetPaused(true);
     var directory=Directory.CreateTempSubdirectory("hakutai-accompany-");
     try
     {
     var store=new GenerationSaveStore(directory.FullName,initial.Definitions);
     Assert.True(store.Save(session,"on-road",initial.WorldId).Published);
     var host=new WorldSessionHost(new WorldSession(initial));
     Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("on-road"),host.Current,host.Current.Revision));
     var restored=host.Current;
     long end=Math.Max(order.When,trip.Intent.EffectiveAt+trip.Intent.DeliverySeconds+trip.Intent.WorkSeconds)+2;
     foreach(var run in new[]{session,restored})
     {
         NewGameWorldTests.At(run,end);
         var after=run.DiagnosticSnapshot();
         var arrived=Assert.Single(after.Court!.Events,e=>e.Input.Action==CourtAction.AttendMuster);
         Assert.Equal(contractsSurvive,arrived.Input.Amount>0);
         var localHires=after.Court.Events.Where(e=>e.Input.Action==CourtAction.HireMarketSoldiers && e.Actor==Retainer && e.Place==order.Site && e.Input.When>arrived.At && e.Revision<arrived.Revision);
         Assert.Equal(mustered.Where(e=>e.Input.When>arrived.At).Sum(e=>e.Input.Amount)+localHires.Sum(e=>e.Input.Amount),arrived.Input.Amount);
         Assert.Equal(0,PrivateRecruitment.Soldiers(after,Retainer,Site,run.Seconds,run.Revision+1));
         Assert.Equal(order.Site,arrived.Place);
         Assert.Equal(order.Site,SocialWorld.Site(after,Retainer));
         Assert.Equal(Site,SocialWorld.Site(after,run.PlayerControl));
     }
     Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
     }
     finally {directory.Delete(true);}
 }

 [Fact]
 public void DistantMusterPaperCreatesReservationOnlyAfterCourierArrives()
 {
     var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer(Ronin.Value),
         NewGameWorldTests.Officer(Retainer.Value,StartTemplate.Retainer,lord:"person:nobunaga")]);
     var initial=NewGameWorld.Create(setup.World,setup.Registered,"person:nobunaga",42,
         inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy);
     var session=new WorldSession(initial);
     Good(session.ManageCompanion(new(new("request:muster-carrier"),Ronin,CompanionAction.Agree,20),session.PlayerControl,session.Revision));
     Good(session.ManageSocial(new(new("request:muster-at-home"),SocialWorldAction.Visit,Retainer),session.Revision));
     Good(session.ManageCompanion(new(new("request:muster-depart"),Ronin,CompanionAction.TravelTogether,Destination:"fork"),session.PlayerControl,session.Revision));
     NewGameWorldTests.At(session,2);
     NewGameWorldTests.At(session,session.DiagnosticSnapshot().Branches!.Journeys.Max(j=>j.Legs[^1].EndAt)+2);
     var order=Input(session,CourtAction.SendMusterNotice,Retainer,amount:3,when:session.Seconds+86400) with {Carrier=Ronin};
     Good(session.ManageCourt(order,session.Revision));
     var sent=session.DiagnosticSnapshot();
     var dispatch=sent.SocialWorld!.Events.Single(e=>e.Input.Asset==order.Id);
     var move=dispatch.GiftTerms!.Movement!;
     Assert.Equal(Ronin,move.Actor);
     Assert.Empty(MusterNotices.Reservations(sent,Retainer,session.Seconds,session.Revision+1));
     session.SetPaused(true);
     var dir=Directory.CreateTempSubdirectory("hakutai-muster-mail-");
     try
     {
         var store=new GenerationSaveStore(dir.FullName,initial.Definitions);
         Assert.True(store.Save(session,"road",initial.WorldId).Published);
         var host=new WorldSessionHost(new WorldSession(initial));
         Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("road"),host.Current,host.Current.Revision));
         foreach(var run in new[]{session,host.Current})
         {
             NewGameWorldTests.At(run,move.EffectiveAt+move.DeliverySeconds+move.WorkSeconds);
             var arrived=run.DiagnosticSnapshot();
             Assert.Equal(order.Id,Assert.Single(MusterNotices.Reservations(arrived,Retainer,run.Seconds,run.Revision+1)).Input.Id);
             Assert.Equal(RegionalCampaign.Site("fork"),arrived.Locations.Single(l=>l.Actor==run.PlayerControl).Site);
         }
         Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
     }
     finally {dir.Delete(true);}
 }

 [Fact]
 public void NpcAttendsAtOrderedTimeAcrossDiskSaveAndLongAdvance()
 {
     var session = Start();
     var initial = session.DiagnosticSnapshot();
     var order = Input(session,CourtAction.SendMusterNotice,Retainer,amount:5,when:120);
     Good(session.ManageCourt(order,session.Revision));
     Assert.Single(MusterNotices.Reservations(session.DiagnosticSnapshot(),Retainer,session.Seconds,session.Revision+1));
     session.SetPaused(true);
     var dir = Directory.CreateTempSubdirectory("hakutai-muster-");
     try
     {
         var store = new GenerationSaveStore(dir.FullName,initial.Definitions);
         Assert.True(store.Save(session,"before-muster",initial.WorldId).Published);
         var host = new WorldSessionHost(new WorldSession(initial));
         Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("before-muster"),host.Current,host.Current.Revision));
         foreach(var run in new[]{session,host.Current})
         {
             NewGameWorldTests.At(run,600);
             var arrived = Assert.Single(run.DiagnosticSnapshot().Court!.Events,e=>e.Input.Action==CourtAction.AttendMuster);
             Assert.Equal((120L,0L,Retainer),(arrived.At,arrived.Input.Amount,arrived.Actor));
         }
         Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
     }
     finally {dir.Delete(true);}
 }

 [Fact]
 public void MusterReservationStartsAtHandoverAndRecordsActualAttendanceOnlyOnce()
 {
     var session = Start(Retainer.Value);
     var order = Input(session, CourtAction.SendMusterNotice, Retainer, amount: 6, when: 120);
     session = Npc(session, order, new("person:nobunaga"));
     CourtEvent[] Reservations() => MusterNotices.Reservations(session.DiagnosticSnapshot(), Retainer, session.Seconds, session.Revision+1);
     Assert.Empty(Reservations());
     session = new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(session.DiagnosticSnapshot())));
     Receive(session, order.Id);
     Assert.Equal(order.Id, Assert.Single(Reservations()).Input.Id);
     var attend = Input(session, CourtAction.AttendMuster, Retainer, reference:order.Id);
     Assert.Equal(ResultCode.NotAvailable,session.ManageCourt(attend,session.Revision).Code);
     NewGameWorldTests.At(session, 130);
     Assert.Equal(ResultCode.NotAvailable,session.ManageCourt(attend with {Amount=6},session.Revision).Code);
     Good(session.ManageCourt(attend,session.Revision));
     Assert.Empty(Reservations());
     Assert.Equal(ResultCode.AlreadyAccepted,session.ManageCourt(attend,session.Revision).Code);
     var after = session.DiagnosticSnapshot();
     Assert.Equal(ContractJson.Write(after),ContractJson.Write(new WorldSession(ContractJson.Copy(after)).DiagnosticSnapshot()));
 }

 [Fact]public void RecruitmentMovesRealSigningMoneyAndOnlyAgreedMembershipAndBond(){var s=Start();var before=s.DiagnosticSnapshot();var i=Input(s,CourtAction.RecruitOffer,amount:20);Good(s.ManageCourt(i,s.Revision));var w=s.DiagnosticSnapshot();Assert.Equal(980,NewGameWork.Balance(w,s.PlayerControl));Assert.Equal(1020,NewGameWork.Balance(w,Ronin));Assert.Equal(0,HouseholdCourt.SigningEscrow(w));Assert.Contains(w.Memberships,m=>m.Subject==Ronin&&m.Target==Owner);Assert.Contains(w.ServiceBonds,b=>b.Subject==Ronin&&b.Target==s.PlayerControl);Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(w.LandRights));Assert.Equal(ContractJson.Write(before.Units),ContractJson.Write(w.Units));Assert.Equal(ResultCode.AlreadyAccepted,s.ManageCourt(i,s.Revision).Code);}
 [Fact]public void NonHeadCannotInventAnEmploymentOffer(){var s=Start(Ronin.Value);var raw=ContractJson.Write(s.DiagnosticSnapshot());Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(Input(s,CourtAction.RecruitOffer,Retainer,20),s.Revision).Code);Assert.Equal(raw,ContractJson.Write(s.DiagnosticSnapshot()));}
 [Fact]public void AStandingStewardStartsOnlyAfterTheActualDocumentAndConsent(){var s=Start();var before=s.DiagnosticSnapshot();var i=Input(s,CourtAction.Nominate,Retainer);Good(s.ManageCourt(i,s.Revision));var w=s.DiagnosticSnapshot();Assert.Contains(w.Offices,o=>o.Subject==Retainer&&o.Target==Site&&o.Kind=="house-steward"&&o.Until==long.MaxValue);Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(w.LandRights));Assert.Equal(ContractJson.Write(before.Grants),ContractJson.Write(w.Grants));Assert.Equal(ContractJson.Write(w),ContractJson.Write(new WorldSession(ContractJson.Copy(w)).DiagnosticSnapshot()));}
 [Fact]public void NewcomerCannotBeMadeElderWithoutKnownWork(){var s=Start();Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(Input(s,CourtAction.Nominate,Retainer,role:CourtRole.Elder),s.Revision).Code);}
 [Fact]public void NoScaleOrMilitaryRightsComeFromAnOfficeName(){var s=Start();Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(Input(s,CourtAction.Nominate,Retainer,role:CourtRole.Commander),s.Revision).Code);var i=Input(s,CourtAction.DeclareScale) with{Scale=CourtScale.Hegemon};Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(i,s.Revision).Code);}
 [Fact]public void LegacyWorldStaysWithoutCourtState(){var s=Start();var raw=ContractJson.Write(s.DiagnosticSnapshot());Assert.DoesNotContain("\"Court\"",System.Text.Encoding.UTF8.GetString(raw));Assert.Equal(raw,ContractJson.Write(new WorldSession(ContractJson.Read<ContractWorld>(raw)).DiagnosticSnapshot()));}
 static WorldSession Npc(WorldSession s,CourtInput input,EntityId actor){var w=s.DiagnosticSnapshot();w.CommitSequence++;HouseholdCourt.Execute(w,input,actor);return new(w);}
 static void Receive(WorldSession s,EntityId document){var w=s.DiagnosticSnapshot();var parcel=w.SocialWorld!.Events.Single(e=>e.Input.Action==SocialWorldAction.DispatchGift&&e.Input.Asset==document);Good(s.ManageSocial(new(new("request:court-receipt/"+s.Revision),SocialWorldAction.ReceiveGift,parcel.Actor,Reference:parcel.Input.Id),s.Revision));}
 [Fact]public void PlayerReadsAndAcceptsTheTermsBeforeServiceStatusChanges(){var s=Start(Ronin.Value);var offer=new CourtInput(new("request:player-recruit"),CourtAction.RecruitOffer,Ronin,Owner,Site,Amount:20);s=Npc(s,offer,new("person:nobunaga"));Assert.Null(NewGameService.View(s.DiagnosticSnapshot()).House);Assert.Equal(20,HouseholdCourt.SigningEscrow(s.DiagnosticSnapshot()));Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(Input(s,CourtAction.RecruitAccept,new("person:nobunaga"),reference:offer.Id),s.Revision).Code);Receive(s,offer.Id);Good(s.ManageCourt(Input(s,CourtAction.RecruitAccept,new("person:nobunaga"),reference:offer.Id),s.Revision));Assert.Equal(Owner,NewGameService.View(s.DiagnosticSnapshot()).House);Assert.Equal(new EntityId("person:nobunaga"),NewGameService.View(s.DiagnosticSnapshot()).Principal);Assert.Equal(1020,NewGameWork.Balance(s.DiagnosticSnapshot(),s.PlayerControl));}
 [Fact]public void RefusalDoesNotRefundUntilTheActualMoneyIsReturned(){var s=Start(Ronin.Value);var offer=new CourtInput(new("request:refuse-recruit"),CourtAction.RecruitOffer,Ronin,Owner,Site,Amount:20);s=Npc(s,offer,new("person:nobunaga"));Receive(s,offer.Id);Good(s.ManageCourt(Input(s,CourtAction.RecruitDecline,new("person:nobunaga"),reference:offer.Id),s.Revision));Assert.Equal(20,HouseholdCourt.SigningEscrow(s.DiagnosticSnapshot()));Good(s.ManageCourt(Input(s,CourtAction.ReturnSigning,new("person:nobunaga"),reference:offer.Id),s.Revision));Assert.Equal(0,HouseholdCourt.SigningEscrow(s.DiagnosticSnapshot()));Assert.Equal(1000,NewGameWork.Balance(s.DiagnosticSnapshot(),new("person:nobunaga")));Assert.Equal(1000,NewGameWork.Balance(s.DiagnosticSnapshot(),s.PlayerControl));Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(Input(s,CourtAction.ReturnSigning,new("person:nobunaga"),reference:offer.Id),s.Revision).Code);}
 static WorldSession PlayerSteward(){var s=Start(Retainer.Value);var offer=new CourtInput(new("request:player-steward"),CourtAction.Nominate,Retainer,Owner,Site);s=Npc(s,offer,new("person:nobunaga"));Receive(s,offer.Id);NewGameWorldTests.At(s,1);Good(s.ManageCourt(Input(s,CourtAction.AcceptOffice,new("person:nobunaga"),reference:offer.Id),s.Revision));return s;}
 [Fact]
 public void ReceivingAdministrativeDismissalEndsOnlyTheReferencedOffice()
 {
     var lord=new EntityId("person:nobunaga");
     var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer(Retainer.Value,StartTemplate.Castellan,lord:lord.Value)]);
     var session=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,Retainer.Value,42,
         inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy));
     var offer=Input(session,CourtAction.Nominate,Retainer);
     session=Npc(session,offer,lord);
     Receive(session,offer.Id);
     NewGameWorldTests.At(session,1);
     Good(session.ManageCourt(Input(session,CourtAction.AcceptOffice,lord,reference:offer.Id),session.Revision));
     NewGameWorldTests.At(session,2);
     var revoke=Input(session,CourtAction.RevokeOffice,Retainer,reference:offer.Id);
     Assert.Throws<InvalidDataException>(()=>Npc(session,revoke with{Site=new("site:kofu")},lord));
     session=Npc(session,revoke,lord);
     var pending=session.DiagnosticSnapshot();
     Assert.Contains(pending.Offices,o=>o.Subject==Retainer && o.Kind=="house-steward" && o.Until==long.MaxValue);
     var directory=Directory.CreateTempSubdirectory("castle-office-dismissal-");
     try{
         session.SetPaused(true);
         var store=new GenerationSaveStore(directory.FullName,pending.Definitions);
         Assert.True(store.Save(session,"pending",pending.WorldId).Published);
         var host=new WorldSessionHost(new WorldSession(ContractJson.Copy(pending)));
         Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("pending"),host.Current,host.Current.Revision));
         Receive(host.Current,revoke.Id);
         Receive(session,revoke.Id);
         Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
     }finally{directory.Delete(true);}
     var received=new WorldSession(ContractJson.Copy(session.DiagnosticSnapshot())).DiagnosticSnapshot();
     Assert.Contains(received.Offices,o=>o.Subject==Retainer && o.Kind=="house-steward" && o.Until==received.ElapsedSeconds);
     Assert.True(JointIndependence.CommandedCastle(received,Retainer,Site,received.ElapsedSeconds,received.CommitSequence+1));
     Assert.Equal(ContractJson.Write(pending.LandRights),ContractJson.Write(received.LandRights));
 }
 [Fact]public void StewardActuallyOperatesWithinMonthlyScopeAndLosesPowerAfterReceiptOfRevoke(){var s=PlayerSteward();long money=NewGameWork.Balance(s.DiagnosticSnapshot(),s.PlayerControl);Good(s.ManageCommunity(new(new("request:steward-relief"),Site,EconomyAction.RelieveVillage,100),s.PlayerControl,s.Revision));Assert.Equal(100,s.CommunityEconomyView()!.Accounts.Relief);Assert.Equal(money,NewGameWork.Balance(s.DiagnosticSnapshot(),s.PlayerControl));Assert.Equal(ResultCode.NotAvailable,s.ManageCommunity(new(new("request:steward-over-budget"),Site,EconomyAction.RelieveVillage,101),s.PlayerControl,s.Revision).Code);NewGameWorldTests.At(s,2);var revoke=new CourtInput(new("request:revoke-steward"),CourtAction.RevokeOffice,Retainer,Owner,Site,Reference:new("request:player-steward"));s=Npc(s,revoke,new("person:nobunaga"));Assert.Contains(s.DiagnosticSnapshot().Offices,o=>o.Subject==Retainer&&o.Until==long.MaxValue);Receive(s,revoke.Id);Assert.Equal(ResultCode.NotAvailable,s.ManageCommunity(new(new("request:steward-after-revoke"),Site,EconomyAction.RelieveVillage,1),s.PlayerControl,s.Revision).Code);ContractValidation.Validate(s.DiagnosticSnapshot());}
 internal static (WorldSession Session,EntityId Proposal,EntityId Meeting) Council(){var s=Start();var proposal=new CourtInput(new("request:local-relief-proposal"),CourtAction.SubmitCase,s.PlayerControl,Owner,Site,Amount:100);s=Npc(s,proposal,Retainer);Assert.Empty(HouseholdCourt.KnownCases(s.DiagnosticSnapshot(),s.PlayerControl));Receive(s,proposal.Id);var meeting=Input(s,CourtAction.Convene,s.PlayerControl,when:3600) with{People=[Retainer]};Good(s.ManageCourt(meeting,s.Revision));var invitation=HouseholdCourt.DocumentRows(s.DiagnosticSnapshot()).Single(d=>d.Source.Input.Id==meeting.Id);Good(s.ManageCourt(Input(s,CourtAction.DeliverDocument,Retainer,reference:invitation.Id),s.Revision));NewGameWorldTests.At(s,3600);Good(s.ManageCourt(Input(s,CourtAction.HoldCouncil,s.PlayerControl,reference:meeting.Id),s.Revision));return(s,proposal.Id,meeting.Id);}
 [Fact]public void CouncilApprovalUsesRealRiceAndCannotBeExecutedTwice(){var t=Council();var s=t.Session;long rice=s.CommunityEconomyView()!.Accounts.HouseRice;var input=Input(s,CourtAction.ApproveCase,Retainer,reference:t.Proposal);Good(s.ManageCourt(input,s.Revision));Assert.Equal(rice-100,s.CommunityEconomyView()!.Accounts.HouseRice);Assert.Equal(100,s.CommunityEconomyView()!.Accounts.Relief);Assert.Equal(ResultCode.AlreadyAccepted,s.ManageCourt(input,s.Revision).Code);Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(Input(s,CourtAction.ApproveCase,Retainer,reference:t.Proposal),s.Revision).Code);}
 [Fact]public void ActualSaveBeforeCouncilDecisionHasSameResourceAndMembershipFuture(){var t=Council();var s=t.Session;s.SetPaused(true);var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"court-council-"+Guid.NewGuid().ToString("N")),w.Definitions);Assert.True(store.Save(s,"meeting",w.WorldId).Published);var host=new WorldSessionHost(Start());Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("meeting"),host.Current,host.Current.Revision));foreach(var x in new[]{s,host.Current})Good(x.ManageCourt(Input(x,CourtAction.ApproveCase,Retainer,reference:t.Proposal),x.Revision));Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));}
 [Fact]public void AnUnreadProposalCannotBeApprovedOutsideARealCouncil(){var s=Start();var p=new CourtInput(new("request:unread-case"),CourtAction.SubmitCase,s.PlayerControl,Owner,Site,Amount:100);s=Npc(s,p,Retainer);var raw=ContractJson.Write(s.DiagnosticSnapshot());Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(Input(s,CourtAction.ApproveCase,Retainer,reference:p.Id),s.Revision).Code);Assert.Equal(raw,ContractJson.Write(s.DiagnosticSnapshot()));}

 [Fact]public void NpcDenialAfterFailedResourceCandidateLeavesNoPartialApproval(){var s=Start(Retainer.Value);var proposal=Input(s,CourtAction.SubmitCase,new("person:nobunaga"),200) with{Case=CourtCaseKind.BuyWeapons};Good(s.ManageCourt(proposal,s.Revision));var w=s.DiagnosticSnapshot();w.CommitSequence++;SocialWorld.Tick(w);s=new(w);var meeting=new CourtInput(new("request:npc-council"),CourtAction.Convene,new("person:nobunaga"),Owner,Site,When:3600){People=[Retainer]};s=Npc(s,meeting,new("person:nobunaga"));var doc=HouseholdCourt.DocumentRows(s.DiagnosticSnapshot()).Single(d=>d.Source.Input.Id==meeting.Id);s=Npc(s,new(new("request:npc-council-invitation"),CourtAction.DeliverDocument,Retainer,Owner,Site,Reference:doc.Id),new("person:nobunaga"));Receive(s,doc.Id);Good(s.ManageCourt(Input(s,CourtAction.AcceptInvitation,new("person:nobunaga"),reference:doc.Id),s.Revision));long money=s.DiagnosticSnapshot().Domains!.Profiles.Single(p=>p.Site==Site).InitialCash;NewGameWorldTests.At(s,3600);w=s.DiagnosticSnapshot();Assert.DoesNotContain(w.Court!.Events,e=>e.Input.Action==CourtAction.ApproveCase&&e.Input.Reference==proposal.Id);var denied=Assert.Single(w.Court.Events,e=>e.Input.Action==CourtAction.DenyCase&&e.Input.Reference==proposal.Id);Assert.DoesNotContain(w.Domains!.Entries,e=>e.Actor==new EntityId("person:nobunaga")&&e.Input.Action==TreasuryAction.Buy);Assert.Null(HouseholdCourt.KnownDecision(w,s.PlayerControl,proposal.Id));Receive(s,denied.Input.Id);Assert.Equal(CourtAction.DenyCase,HouseholdCourt.KnownDecision(s.DiagnosticSnapshot(),s.PlayerControl,proposal.Id)!.Input.Action);}
 [Fact]public void HeadChangeEndsOldStandingOfficeWithoutTransferringLandOrPrivateMoney(){var s=Start();Good(s.ManageCourt(Input(s,CourtAction.Nominate,Retainer),s.Revision));var w=s.DiagnosticSnapshot();var lands=ContractJson.Write(w.LandRights);var cash=ContractJson.Write(w.ResourceLots);var id=new EntityId("request:court-head-change");Good(s.ManageHouseSuccession(new(id,HouseSuccessionAction.Nominate,Owner,Retainer),s.PlayerControl,s.Revision));Good(s.ManageHouseSuccession(new(new("request:court-head-confirm"),HouseSuccessionAction.Confirm,Owner,Original:id),s.PlayerControl,s.Revision));NewGameWorldTests.At(s,1);w=s.DiagnosticSnapshot();Assert.Contains(w.Offices,o=>o.Subject==Retainer&&o.Kind=="house-steward"&&o.Until==1);Assert.Equal(lands,ContractJson.Write(w.LandRights));Assert.Equal(cash,ContractJson.Write(w.ResourceLots));Assert.Equal(Retainer,HouseSuccession.Head(w,Owner));ContractValidation.Validate(w);}
 [Fact]public void ForgedDoubleAcceptanceCannotDuplicateSigningCash(){var s=Start();Good(s.ManageCourt(Input(s,CourtAction.RecruitOffer,amount:20),s.Revision));var w=s.DiagnosticSnapshot();var accepted=w.Court!.Events.Single(e=>e.Input.Action==CourtAction.RecruitAccept);w.Court=w.Court with{Events=[..w.Court.Events,accepted with{Input=accepted.Input with{Id=new("request:duplicate-court-accept")}}]};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));}

 [Fact]public void KnownWorkCanQualifyAnElderWhoCanConveneButNotReplaceTheHead(){var s=MeritRewardTests.Worked();var person=MeritRewardTests.Retainer;for(int n=0;n<2;n++){Good(s.ManageCompanion(new(new("request:elder-inspect/"+n),person,CompanionAction.Inspect),s.PlayerControl,s.Revision));NewGameWorldTests.At(s,s.Seconds+20);var w=s.DiagnosticSnapshot();var report=w.Companions!.Reports.Last();Good(s.Open(s.PlayerControl,new(report.Message),s.Revision));var task=w.Tasks.Single(t=>t.RequestId==w.Companions.Orders.Last().Task!.RequestId);Good(s.ManageMerit(new(new("request:elder-review/"+n),MeritAction.ReviewCompanion,person,[task.Id],Object:new(report.Message)),s.Revision));}var nomination=new CourtInput(new("request:qualified-elder"),CourtAction.Nominate,person,Owner,Site,CourtRole.Elder);Good(s.ManageCourt(nomination,s.Revision));Assert.True(HouseholdCourt.CanConvene(s.DiagnosticSnapshot(),person,Owner,Site));Assert.Null(FactionStances.ResolveAuthority(s.DiagnosticSnapshot(),person,Owner,s.Seconds,s.Revision));var meeting=new CourtInput(new("request:elder-meeting"),CourtAction.Convene,s.PlayerControl,Owner,Site,When:s.Seconds+3600){People=[s.PlayerControl]};s=Npc(s,meeting,person);var doc=HouseholdCourt.DocumentRows(s.DiagnosticSnapshot()).Single(d=>d.Source.Input.Id==meeting.Id);s=Npc(s,new(new("request:elder-invitation"),CourtAction.DeliverDocument,s.PlayerControl,Owner,Site,Reference:doc.Id),person);Receive(s,doc.Id);Good(s.ManageCourt(Input(s,CourtAction.AcceptInvitation,person,reference:doc.Id),s.Revision));NewGameWorldTests.At(s,meeting.When);Good(s.ManageCourt(Input(s,CourtAction.HoldCouncil,s.PlayerControl,reference:meeting.Id),s.Revision));Assert.Contains(s.DiagnosticSnapshot().Court!.Events,e=>e.Input.Action==CourtAction.HoldCouncil&&e.Input.People!.Contains(person));}

 [Theory][InlineData("court")][InlineData("scheme")][InlineData("merit")][InlineData("asset")]
 public void EmptyNewModulesCannotBeInjectedIntoLegacyWorlds(string module){var w=ContractFixture.Create();if(module=="court")w.Court=new(1,HouseholdCourt.Policy,[]);else if(module=="scheme")w.Schemes=new(1,InformationSchemes.Policy,[]);else if(module=="merit")w.Merits=new(1,MeritRewards.Policy,[]);else w.SocialAssets=new(1,SocialAssets.Policy,[]);Assert.Throws<InvalidDataException>(()=>new WorldSession(w));}

}
