using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

public class DefectionPactTests
{
    static readonly EntityId Actor=new("created:test"),Partner=new("created:partner");
    static ContractWorld Start(ulong seed,bool foreign=false,bool castellan=false,RegisteredOfficer? other=null,string actorSite="site:castle")
    {
        var actor=NewGameWorldTests.Officer(site:actorSite,role:castellan?StartTemplate.Castellan:StartTemplate.Retainer,lord:"person:nobunaga");
        var partner=NewGameWorldTests.Officer(Partner.Value,StartTemplate.Retainer,foreign?"site:kofu":actor.Site,
            foreign?"person:shingen":"person:nobunaga");
        var setup=NewGameWorldTests.Setup(other==null?[actor,partner]:[actor,partner,other]);
        return NewGameWorld.Create(setup.World,setup.Registered,actor.Id,seed,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,
            socialPolicy:CharacterRelations.Policy,comingOfAge:15);
    }

    [Fact]
    public void DisplacedLordCanRestoreHisHouseWithTheLocalCastellanAfterLoading()
    {
        var replacement = NewGameWorldTests.Officer(role: StartTemplate.Daimyo);
        var partner = NewGameWorldTests.Officer(Partner.Value, StartTemplate.Castellan, lord: replacement.Id);
        var setup = NewGameWorldTests.Setup([replacement, partner], [new(replacement.Id, "org:replacement", [replacement.Site], true)]);
        EntityId lord = new("person:nobunaga");
        for (ulong seed = 0; seed < 32; seed++)
        {
            var start = NewGameWorld.Create(setup.World, setup.Registered, lord.Value, seed, inspectionPolicy: LocalInspectionFindings.Policy,
                workPolicy: WorkFollowup.Policy, companionPolicy: PersonalCompanions.Policy, developmentPolicy: CharacterDevelopment.Policy,
                socialPolicy: CharacterRelations.Policy, comingOfAge: 15);
            var session = new WorldSession(start);
            var seen = Assert.Single(HouseRestoration.ObserveFormerSeat(start, lord));
            var inquiry = new CourtInput(new("request:former-seat-inquiry"), CourtAction.SeekRestorationSupport, Partner, seen.House, seen.Site);
            var result = session.ManageCourt(inquiry, session.Revision);
            Assert.True(result.Code == ResultCode.Accepted, result.VisibleReason);
            EntityId pact = new("request:former-seat-pact");
            result = session.NegotiatePact(pact, Partner, PactPurpose.RiseTogether, session.Revision);
            Assert.True(result.Code == ResultCode.Accepted, result.VisibleReason);
            if (!session.DiagnosticSnapshot().SecretPacts!.Single().Accepted) continue;
            Assert.Equal(ResultCode.Accepted, session.SignalPact(pact, session.Revision).Code);
            session.SetPaused(true);
            var directory = Directory.CreateTempSubdirectory("hakutai-former-seat-");
            try
            {
                var store = new GenerationSaveStore(directory.FullName, start.Definitions);
                Assert.True(store.Save(session, "rebellion", start.WorldId).Published);
                var host = new WorldSessionHost(new WorldSession(start));
                Assert.Equal(ResultCode.Accepted, host.CommitLoad(store.Prepare("rebellion"), host.Current, host.Current.Revision));
                foreach (var run in new[] {session, host.Current})
                {
                    NewGameWorldTests.At(run, 120);
                    var world = run.DiagnosticSnapshot();
                    var application = Assert.Single(HouseRestoration.ContinuityApplications(world, lord));
                    var filed = run.ManageCourt(application, run.Revision);
                    Assert.True(filed.Code == ResultCode.Accepted, filed.VisibleReason);
                    world = run.DiagnosticSnapshot();
                    Assert.Equal(world.Declarations!.Single().House, HouseRestoration.SuccessorOrganization(world, seen.House, 120, world.CommitSequence + 1));
                }
                Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()), ContractJson.Write(host.Current.DiagnosticSnapshot()));
                return;
            }
            finally { directory.Delete(true); }
        }
        Assert.Fail("旧大名と城主の共同挙兵に至る開始条件がありません");
    }

    [Fact]
    public void FormerLordAsNpcCanSendHisOwnCallAfterWinningLocalSupport()
    {
        var daimyo = NewGameWorldTests.Officer(role: StartTemplate.Daimyo);
        var partner = NewGameWorldTests.Officer(Partner.Value, StartTemplate.Castellan, lord: daimyo.Id);
        var setup = NewGameWorldTests.Setup([daimyo, partner], [new(daimyo.Id, "org:replacement", [daimyo.Site], true)]);
        EntityId lord = new("person:nobunaga");
        for (ulong seed = 0; seed < 256; seed++)
        {
            var world = NewGameWorld.Create(setup.World, setup.Registered, daimyo.Id, seed, npcRoutinePolicy: NewGameNpcRoutine.Policy,
                inspectionPolicy: LocalInspectionFindings.Policy, workPolicy: WorkFollowup.Policy, companionPolicy: PersonalCompanions.Policy,
                developmentPolicy: CharacterDevelopment.Policy, socialPolicy: CharacterRelations.Policy, comingOfAge: 15);
            var opinion = CharacterRelations.DiagnosticScores(world, lord, Partner);
            var reverse = CharacterRelations.DiagnosticScores(world, Partner, lord);
            var loyalty = CharacterRelations.DiagnosticScores(world, Partner, new(daimyo.Id));
            bool Open(EntityId person) => CharacterRelations.DiagnosticTrait(world, person, SocialTrait.Ambition) > CharacterRelations.DiagnosticTrait(world, person, SocialTrait.Caution) && CharacterRelations.DiagnosticTrait(world, person, SocialTrait.Affection) > CharacterRelations.DiagnosticTrait(world, person, SocialTrait.Duty);
            if (!Open(lord) || !Open(Partner) ||
                CharacterRelations.DiagnosticTrait(world, Partner, SocialTrait.Ambition) + reverse.Trust + loyalty.Hostility <
                CharacterRelations.DiagnosticTrait(world, Partner, SocialTrait.Duty) + loyalty.Trust + reverse.Hostility) continue;
            var session = new WorldSession(world);
            NewGameWorldTests.At(session, 86400 * 5 + 120);
            var after = session.DiagnosticSnapshot();
            var pact = Assert.Single(after.SecretPacts ?? [], p => p.Proposer == lord && p.Partner == Partner);
            Assert.True(PactSignals.Sent(after, pact));
            Assert.Contains(after.Declarations!, d => d.Pact == pact.Id);
            Assert.Contains(after.PactResponses!, r => r.Pact == pact.Id && r.Acted);
            Assert.NotNull(HouseRestoration.SuccessorOrganization(after, new("org:oda"), after.ElapsedSeconds, after.CommitSequence+1));
            return;
        }
        Assert.Fail("互いに支持し共同挙兵を選ぶ条件がありません");
    }

    [Theory]
    [InlineData(PactPurpose.WithholdService)]
    [InlineData(PactPurpose.LeaveHouse)]
    [InlineData(PactPurpose.RiseTogether)]
    public void SignalIsDeliveredOnceAfterReload(PactPurpose purpose)
    {
        EntityId id=new("request:dispatch-test");
        for(ulong seed=0;seed<32;seed++)
        {
            var w=Start(seed);
            var session=new WorldSession(w);
            Assert.Equal(ResultCode.Accepted,session.NegotiatePact(id,Partner,purpose,session.Revision).Code);
            var pact=Assert.Single(session.DiagnosticSnapshot().SecretPacts!);
            if(!pact.Accepted)continue;
            Assert.Equal(ResultCode.Accepted,session.SignalPact(id,session.Revision).Code);
            var dispatched=session.DiagnosticSnapshot();
            Assert.Empty(dispatched.PactResponses??[]);
            if(purpose==PactPurpose.RiseTogether)
            {
                var declaration=Assert.Single(dispatched.Declarations!);
                Assert.Contains(dispatched.Memberships,m=>m.Subject==Actor && m.Target==declaration.House && m.Until==long.MaxValue);
                Assert.Contains(dispatched.Memberships,m=>m.Subject==Partner && m.Target==pact.House && m.Until==long.MaxValue);
                Assert.Equal(ContractJson.Write(w.Units),ContractJson.Write(dispatched.Units));
            }
            session.SetPaused(true);
            var directory=Directory.CreateTempSubdirectory("hakutai-signal-");
            try
            {
                var store=new GenerationSaveStore(directory.FullName,w.Definitions);
                Assert.True(store.Save(session,"signal",w.WorldId).Published);
                var host=new WorldSessionHost(new WorldSession(w));
                Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("signal"),host.Current,host.Current.Revision));
                var restored=host.Current;
                Assert.Equal(ResultCode.AlreadyAccepted,restored.SignalPact(id,restored.Revision).Code);
                NewGameWorldTests.At(restored,120);
                NewGameWorldTests.At(session,120);
                var after=restored.DiagnosticSnapshot();
                Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(after));
                var response=Assert.Single(after.PactResponses!);
                Assert.Equal(id,response.Pact);
                Assert.True(response.Acted);
                Assert.True(response.At>pact.At);
                if(purpose==PactPurpose.RiseTogether)
                {
                    var house=Assert.Single(after.Declarations!).House;
                    Assert.Equal(Actor,HouseSuccession.Head(after,house));
                    Assert.Contains(after.Memberships,m=>m.Subject==Partner && m.Target==house && m.Until>120);
                    Assert.Contains(after.Memberships,m=>m.Subject==Actor && m.Target==house && m.Until>120);
                    Assert.Equal(ContractJson.Write(w.LandRights),ContractJson.Write(after.LandRights));
                }
                Assert.Equal(purpose==PactPurpose.WithholdService,PactSignals.Withholds(after,Partner,120,after.CommitSequence));
                Assert.Single(after.Messaging.Messages,m=>m.Id==PactSignals.Letter(pact,PactSignals.Reply));
                return;
            }
            finally{directory.Delete(true);}
        }
        Assert.Fail("承諾に至る開始条件がありません");
    }

    [Fact]
    public void OtherRetainersRemainUntilTheirOwnDecision()
    {
        EntityId second=new("created:second"),bystander=new("created:bystander");
        var setup=NewGameWorldTests.Setup(new[]{Actor,Partner,second,bystander}.Select(id=>
            NewGameWorldTests.Officer(id.Value,StartTemplate.Retainer,lord:"person:nobunaga")).ToArray());
        for(ulong seed=0;seed<160;seed++)
        {
            var w=NewGameWorld.Create(setup.World,setup.Registered,Actor.Value,seed,inspectionPolicy:LocalInspectionFindings.Policy,
                workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,
                socialPolicy:CharacterRelations.Policy,comingOfAge:15);
            var s=new WorldSession(w);
            foreach(var person in new[]{Partner,second})
            {
                var input=new EntityId("request:rise/"+person.Value.Split(':')[1]);
                Assert.Equal(ResultCode.Accepted,s.NegotiatePact(input,person,PactPurpose.RiseTogether,s.Revision).Code);
            }
            var pacts=s.DiagnosticSnapshot().SecretPacts!;
            if(pacts.Any(p=>!p.Accepted))continue;
            Assert.Equal(ResultCode.Accepted,s.SignalPact(pacts[0].Id,s.Revision).Code);
            NewGameWorldTests.At(s,30);
            var after=s.DiagnosticSnapshot();var house=Assert.Single(after.Declarations!).House;
            Assert.Contains(after.Memberships,r=>r.Subject==second && r.Target==pacts[0].House && r.Until>30);
            Assert.Equal(ResultCode.Accepted,s.SignalPact(pacts[1].Id,s.Revision).Code);
            NewGameWorldTests.At(s,60);after=s.DiagnosticSnapshot();
            Assert.Single(after.Declarations!);
            Assert.Equal(2,after.PactResponses!.Length);
            foreach(var response in after.PactResponses)
            {
                var pact=pacts.Single(p=>p.Id==response.Pact);
                Assert.Contains(after.Memberships,r=>r.Subject==pact.Partner && r.Target==(response.Acted?house:pact.House) && r.Until>60);
            }
            Assert.Contains(after.Memberships,r=>r.Subject==bystander && r.Target==pacts[0].House && r.Until>60);
            return;
        }
        Assert.Fail("二人が密約を受ける開始条件がありません");
    }

    [Fact]
    public void IndependencePreservesTheFormerHousesActualMusterAndSupply()
    {
        for(ulong seed=0;seed<32;seed++)
        {
            var w=Start(seed,castellan:true);var s=new WorldSession(w);
            EntityId id=new("request:field-pact");
            Assert.Equal(ResultCode.Accepted,s.NegotiatePact(id,Partner,PactPurpose.RiseTogether,s.Revision).Code);
            if(!s.DiagnosticSnapshot().SecretPacts!.Single().Accepted)continue;
            void Travel(string to){
                var trip=TravelWorld.Move(s.DiagnosticSnapshot(),"request:gate-fee-trip/"+to,to);
                Assert.Equal(ResultCode.Accepted,s.Submit(trip,s.Revision).Code);
                NewGameWorldTests.At(s,s.Seconds+2);
                NewGameWorldTests.At(s,s.GetBranchView(Actor).ArrivalAt!.Value+1);
            }
            // 他家の拠点で受けた民間点検の報酬を未払いのまま残す。
            Travel("kofu");
            NewGameWorkTests.Work(s,id:"request:independence-outstanding-fee");
            NewGameWorldTests.At(s,s.Seconds+8);
            var debt=Assert.Single(s.DiagnosticSnapshot().Debts,d=>d.Outstanding>0);
            Assert.Equal(Actor,debt.Creditor);
            Travel("castle");
            void Send(NewMilitaryAction a,long amount=0)
            {
                var input=new NewMilitaryInput(new("request:field-pact/"+a),a,amount);
                var result=s.DecideNewMilitary(input,Actor,s.Revision);
                Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
            }
            void Receive()
            {
                NewGameWorldTests.At(s,s.Seconds+2);
                foreach(var m in s.GetActorView(Actor).Messages.Where(m=>m.KnownBody==null).ToArray())
                    Assert.Equal(ResultCode.Accepted,s.Open(Actor,m.Id,s.Revision).Code);
            }
            Send(NewMilitaryAction.Request);Receive();Send(NewMilitaryAction.Accept);Receive();
            Send(NewMilitaryAction.Pickup);Send(NewMilitaryAction.Load,100000);
            var before=s.DiagnosticSnapshot();
            Assert.Contains(before.Units,u=>u.Commander==Actor && u.Soldiers>0);
            Assert.Equal(ResultCode.Accepted,s.SignalPact(id,s.Revision).Code);
            var after=s.DiagnosticSnapshot();
            var declaration=Assert.Single(after.Declarations!);
            Assert.Equal(true,declaration.CheckGarrison);
            Assert.True(CastleHandover.Forces(after,declaration.FormerHouse,declaration.Site,declaration.At,declaration.Revision)>0);
            Assert.DoesNotContain(after.LandRights,r=>r.Subject==declaration.House && r.Target==declaration.Site);
            var observation=JointIndependence.ObserveOwnCastle(after,Actor);
            Assert.NotNull(observation);Assert.True(observation.FormerTroopsPresent);Assert.False(observation.ControlsCastle);
            Assert.Null(JointIndependence.ObserveOwnCastle(after,new("person:shingen")));
            Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(after.LandRights));
            Assert.Equal(ContractJson.Write(before.Units),ContractJson.Write(after.Units));
            Assert.Equal(ContractJson.Write(before.ResourceLots),ContractJson.Write(after.ResourceLots));
            Assert.DoesNotContain(after.Units,u=>u.Organization==after.Declarations!.Single().House);
            var storeDirectory=Directory.CreateTempSubdirectory("hakutai-contested-castle-");
            try
            {
                var store=new GenerationSaveStore(storeDirectory.FullName,w.Definitions);
                s.SetPaused(true);
                Assert.True(store.Save(s,"contested",after.WorldId).Published);
                var host=new WorldSessionHost(new WorldSession(w));
                Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("contested"),host.Current,host.Current.Revision));
                var recovered=host.Current.DiagnosticSnapshot();
                Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(recovered));
                Assert.Contains(debt,recovered.Debts);
                Assert.Equal(observation,JointIndependence.ObserveOwnCastle(recovered,Actor));
                Assert.Null(JointIndependence.ObserveOwnCastle(recovered,new("person:shingen")));
                var play=host.Current;
                var request=Assert.Single(IndependenceGate.Available(recovered,Actor));
                Assert.Equal(CourtAction.RequestIndependentGate,request.Action);
                var reply=play.ManageCourt(request,play.Revision);
                Assert.True(reply.Code==ResultCode.Accepted,reply.VisibleReason);
                Assert.Empty(IndependenceGate.Available(play.DiagnosticSnapshot(),new("person:shingen")));
                var proposed=ContractJson.Copy(recovered);
                proposed.CommitSequence++;
                HouseholdCourt.Execute(proposed,request,Actor);
                foreach(var decision in new[]{CourtAction.AllowIndependentGate,CourtAction.RefuseIndependentGate}){
                    var branch=ContractJson.Copy(proposed);
                    var negotiator=new EntityId("person:nobunaga");
                    var chosen=IndependenceGate.Available(branch,negotiator).Single(i=>i.Action==decision);
                    branch.CommitSequence++;
                    HouseholdCourt.Execute(branch,chosen,negotiator);
                    var negotiations=new WorldSession(branch);
                    var options=IndependenceGate.Available(negotiations.DiagnosticSnapshot(),Actor).ToArray();
                    if(decision==CourtAction.RefuseIndependentGate){Assert.Empty(options);continue;}
                    var allowedGate=Assert.Single(options);
                    Assert.Equal(CourtAction.ReceiveIndependentGate,allowedGate.Action);
                    Assert.Equal(ResultCode.Accepted,negotiations.ManageCourt(allowedGate,negotiations.Revision).Code);
                    Assert.Equal(ResultCode.AlreadyAccepted,negotiations.ManageCourt(allowedGate,negotiations.Revision).Code);
                    Assert.Empty(IndependenceGate.Available(negotiations.DiagnosticSnapshot(),Actor));
                    Assert.DoesNotContain(negotiations.DiagnosticSnapshot().LandRights,r=>r.Subject==declaration.House);
                    void MoveGuard(NewMilitaryAction action,FieldPoint? a=null,FieldPoint? b=null){
                        var input=new NewMilitaryInput(new("request:gate-movement/"+action),action,A:a,B:b);
                        var result=negotiations.DecideNewMilitary(input,Actor,negotiations.Revision);
                        Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
                    }
                    MoveGuard(NewMilitaryAction.Deploy);
                    var anchor=NewGameMilitary.Anchor(negotiations.DiagnosticSnapshot());
                    MoveGuard(NewMilitaryAction.Form,new(anchor.X+55000,anchor.Y-8000),new(anchor.X+55000,anchor.Y+8000));
                    NewGameWorldTests.At(negotiations,negotiations.Seconds+100);
                    Assert.Empty(IndependenceGate.Available(negotiations.DiagnosticSnapshot(),Actor));
                    MoveGuard(NewMilitaryAction.Handoff);MoveGuard(NewMilitaryAction.Withdraw);
                    var returning=negotiations.DiagnosticSnapshot();
                    var arrival=returning.NewMilitary!.Withdrawal!.Journeys.Last().Route.Last().EndAt;
                    Assert.Empty(IndependenceGate.Available(returning,Actor));
                    NewGameWorldTests.At(negotiations,arrival+1);
                    var keep=Assert.Single(IndependenceGate.Available(negotiations.DiagnosticSnapshot(),Actor));
                    Assert.Equal(CourtAction.ReceiveIndependentKeep,keep.Action);
                    var ready=negotiations.DiagnosticSnapshot();
                    var fixture=Environment.GetEnvironmentVariable("HAKUTAI_INDEPENDENCE_UI_FIXTURE");
                    if(!string.IsNullOrWhiteSpace(fixture))File.WriteAllBytes(fixture,ContractJson.Write(ready));
                    var taken=negotiations.ManageCourt(keep,negotiations.Revision);
                    Assert.True(taken.Code==ResultCode.Accepted,taken.VisibleReason);
                    var transferred=negotiations.DiagnosticSnapshot();
                    Assert.True(JointIndependence.ObserveOwnCastle(transferred,Actor)!.ControlsCastle);
                    Assert.Contains(debt,transferred.Debts);
                    Assert.All(transferred.Units,u=>Assert.Equal(declaration.FormerHouse,u.Organization));
                }
                NewGameWorldTests.At(play,play.Seconds+2);
                var record=play.DiagnosticSnapshot();
                var answer=Assert.Single(record.Court!.Events,e=>e.Input.Reference==declaration.Pact &&
                    e.Input.Action is CourtAction.AllowIndependentGate or CourtAction.RefuseIndependentGate);
                Assert.Equal(new EntityId("person:nobunaga"),answer.Actor);
                if(answer.Input.Action==CourtAction.AllowIndependentGate)
                {
                    var gate=Assert.Single(IndependenceGate.Available(record,Actor));
                    Assert.Equal(CourtAction.ReceiveIndependentGate,gate.Action);
                    Assert.Equal(ResultCode.Accepted,play.ManageCourt(gate,play.Revision).Code);
                }
                var contested=play.DiagnosticSnapshot();
                Assert.Empty(IndependenceGate.Available(contested,Actor));
                Assert.Equal(ContractJson.Write(after.LandRights),ContractJson.Write(contested.LandRights));
                Assert.Equal(observation,JointIndependence.ObserveOwnCastle(contested,Actor));
                play.SetPaused(true);
                Assert.True(store.Save(play,"after-gate",after.WorldId).Published);
                var resumed=new WorldSessionHost(new WorldSession(w));
                Assert.Equal(ResultCode.Accepted,resumed.CommitLoad(store.Prepare("after-gate"),resumed.Current,resumed.Current.Revision));
                Assert.Equal(ContractJson.Write(play.DiagnosticSnapshot()),ContractJson.Write(resumed.Current.DiagnosticSnapshot()));
            }
            finally{storeDirectory.Delete(true);}
            return;
        }
        Assert.Fail("実兵を引き受けた武将が独立できる開始条件がありません");
    }

    [Fact]
    public void ActualLossOfCastleCanLeadToLocalRestorationInquiryAcrossDiskLoad()
    {
        for (ulong seed = 0; seed < 32; seed++)
        {
            var start = Start(seed, castellan: true);
            var session = new WorldSession(start);
            NewGameWorldTests.At(session, 5);
            EntityId pact = new("request:loss-before-restoration");
            Assert.Equal(ResultCode.Accepted, session.NegotiatePact(pact, Partner, PactPurpose.RiseTogether, session.Revision).Code);
            if (!session.DiagnosticSnapshot().SecretPacts!.Single().Accepted) continue;
            Assert.Equal(ResultCode.Accepted, session.SignalPact(pact, session.Revision).Code);
            var changed = session.DiagnosticSnapshot();
            var change = Assert.Single(changed.Declarations!);
            var observation = Assert.Single(HouseRestoration.ObserveFormerSeat(changed, Actor));
            Assert.Equal((change.FormerHouse, change.Site), (observation.House, observation.Site));
            var ask = new CourtInput(new("request:seek-local-support"), CourtAction.SeekRestorationSupport,
                Partner, change.FormerHouse, change.Site);
            var result = session.ManageCourt(ask, session.Revision);
            Assert.True(result.Code == ResultCode.Accepted, result.VisibleReason);
            session.SetPaused(true);
            var directory = Directory.CreateTempSubdirectory("hakutai-restore-inquiry-");
            try
            {
                var store = new GenerationSaveStore(directory.FullName, start.Definitions);
                Assert.True(store.Save(session, "inquiry", start.WorldId).Published);
                var host = new WorldSessionHost(new WorldSession(start));
                Assert.Equal(ResultCode.Accepted, host.CommitLoad(store.Prepare("inquiry"), host.Current, host.Current.Revision));
                NewGameWorldTests.At(session, 8);
                NewGameWorldTests.At(host.Current, 8);
                var after = host.Current.DiagnosticSnapshot();
                Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()), ContractJson.Write(after));
                Assert.Single(after.Court!.Events, e => e.Input.Reference == ask.Id &&
                    e.Input.Action is CourtAction.SupportRestoration or CourtAction.ReserveRestoration);
                Assert.Null(HouseRestoration.SuccessorOrganization(after, change.FormerHouse, 8, after.CommitSequence + 1));
                Assert.Empty(HouseRestoration.ContinuityApplications(after, Actor));
                var attempt = new CourtInput(new("request:claim-former-house"), CourtAction.RecognizeHouseContinuity,
                    Actor, change.FormerHouse, change.Site, Reference: change.Pact);
                var refused = host.Current.ManageCourt(attempt, host.Current.Revision);
                Assert.Equal(ResultCode.NotAvailable, refused.Code);
                Assert.Equal(ContractJson.Write(after), ContractJson.Write(host.Current.DiagnosticSnapshot()));

            }
            finally { directory.Delete(true); }
            return;
        }
        Assert.Fail("独立に合意する開始条件がありません");
    }

    [Theory]
    [InlineData(StartTemplate.Retainer,false)]
    [InlineData(StartTemplate.Castellan,false)]
    [InlineData(StartTemplate.Castellan,true)]
    public void RemoteRetainerAnswersOnlyAfterMeetingAndReading(StartTemplate role,bool oldLordPresent)
    {
        var recipientNode=oldLordPresent?"castle":"fork";
        var founderNode=oldLordPresent?"fork":"castle";
        var contact=NewGameWorldTests.Officer("created:castle-contact",role,"site:"+recipientNode,"person:nobunaga");
        var recipient=new EntityId(contact.Id);
        for(ulong seed=0;seed<32;seed++){
            var w=Start(seed,castellan:true,other:contact,actorSite:"site:"+founderNode);var session=new WorldSession(w);
            EntityId pact=new("request:original-private-pact");
            Assert.Equal(ResultCode.Accepted,session.NegotiatePact(pact,Partner,PactPurpose.RiseTogether,session.Revision).Code);
            if(!session.DiagnosticSnapshot().SecretPacts!.Single().Accepted)continue;
            Assert.Equal(ResultCode.Accepted,session.SignalPact(pact,session.Revision).Code);
            Assert.DoesNotContain(IndependenceLetters.Choices(session.DiagnosticSnapshot(),Actor),i=>i.Person==recipient);
            void Travel(string node){
                var trip=TravelWorld.Move(session.DiagnosticSnapshot(),"request:contact-trip/"+node,node);
                Assert.Equal(ResultCode.Accepted,session.Submit(trip,session.Revision).Code);
                NewGameWorldTests.At(session,session.Seconds+2);NewGameWorldTests.At(session,session.GetBranchView(Actor).ArrivalAt!.Value+1);
            }
            EntityId oldLord=new("person:nobunaga");
            var appointment=new CourtInput(new("request:parallel-steward"),CourtAction.Nominate,recipient,
                new(w.NewGame!.Start.Factions.Single(f=>f.Head==oldLord.Value).Id),new("site:"+recipientNode));
            WorldSession IssueAsLord(WorldSession source,CourtInput input){
                var state=source.DiagnosticSnapshot();state.CommitSequence++;
                HouseholdCourt.Execute(state,input,oldLord);
                return new WorldSession(state);
            }
            Travel(recipientNode);
            if(oldLordPresent){
                session=IssueAsLord(session,appointment);
                NewGameWorldTests.At(session,session.Seconds+2);
                Assert.Contains(session.DiagnosticSnapshot().Offices,o=>o.Subject==recipient && o.Kind=="house-steward" && o.Until==long.MaxValue);
            }
            for(int day=0;day<2;day++){
                var visit=session.ManageSocial(new(new("request:visit-contact/"+day),SocialWorldAction.Visit,recipient),session.Revision);
                Assert.True(visit.Code==ResultCode.Accepted,visit.VisibleReason);
                if(day==0)NewGameWorldTests.At(session,session.Seconds+86400);
            }
            Travel(founderNode);var before=session.DiagnosticSnapshot();
            if(role==StartTemplate.Retainer && Environment.GetEnvironmentVariable("HAKUTAI_LETTER_FIXTURE") is {Length:>0} file)
                File.WriteAllBytes(file,ContractJson.Write(before));
            if(oldLordPresent && Environment.GetEnvironmentVariable("HAKUTAI_ALLIED_GATE_FIXTURE") is {Length:>0} alliedFile)
                File.WriteAllBytes(alliedFile,ContractJson.Write(before));
            var call=IndependenceLetters.Choices(before,Actor).Single(i=>i.Person==recipient && i.Action==CourtAction.SeekDeclaredSupport);
            var issued=session.ManageCourt(call,session.Revision);Assert.True(issued.Code==ResultCode.Accepted,issued.VisibleReason);
            var sent=session.DiagnosticSnapshot();var letter=sent.Messaging.Messages.Single(m=>m.Kind==IndependenceLetters.Kind);
            var courier=sent.Couriers!.Missions.Single(m=>sent.Messaging.Attempts.Any(a=>a.MessageId==letter.Id && a.Id==m.Attempt));
            Assert.Equal(recipientNode,courier.Destination);
            NewGameWorldTests.At(session,courier.ReadyAt+1);
            var after=session.DiagnosticSnapshot();var joined=Assert.Single(after.Court!.Events,e=>e.Input.Action==CourtAction.JoinDeclaredHouse);
            Assert.Equal(recipient,joined.Actor);
            Assert.Contains(after.Memberships,r=>r.Subject==recipient && r.Target==call.Owner && r.Until==long.MaxValue);
            Assert.Contains(after.ServiceBonds,r=>r.Subject==recipient && r.Target==Actor && r.Until==long.MaxValue);
            Assert.Equal(ContractJson.Write(before.Units),ContractJson.Write(after.Units));
            Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(after.LandRights));
            var observation=JointIndependence.ObserveOwnCastle(after,recipient);
            if(role==StartTemplate.Castellan){
                Assert.NotNull(observation);
                Assert.Equal(new EntityId("site:"+recipientNode),observation.Castle);
                Assert.False(observation.ControlsCastle);
            }else Assert.Null(observation);
            if(oldLordPresent){
                for(int step=0;step<5 && !session.DiagnosticSnapshot().Court!.Events.Any(e=>
                    e.Input.Reference==joined.Input.Id && e.Input.Action==CourtAction.ReceiveIndependentGate);step++)
                    NewGameWorldTests.At(session,session.Seconds+1);
                var gateway=session.DiagnosticSnapshot();
                Assert.Contains(gateway.Court!.Events,e=>e.Input.Reference==joined.Input.Id && e.Input.Action==CourtAction.ReceiveIndependentGate);
                Assert.False(JointIndependence.ObserveOwnCastle(gateway,recipient)!.ControlsCastle);
                var dismissal=appointment with{Id=new("request:parallel-dismissal"),Action=CourtAction.RevokeOffice,Reference=appointment.Id};
                var competing=IssueAsLord(session,dismissal);
                NewGameWorldTests.At(competing,competing.Seconds+1);
                var resolved=competing.DiagnosticSnapshot();
                Assert.Contains(resolved.Offices,o=>o.Subject==recipient && o.Kind=="house-steward" && o.Until<=resolved.ElapsedSeconds);
                Assert.True(JointIndependence.ObserveOwnCastle(resolved,recipient)!.ControlsCastle);
                Assert.Single(resolved.Messaging.Messages,m=>m.Kind==IndependenceLetters.KeepReport);
                // 現地に双方がいる同一の交渉記録に対し、任期の境界だけを変えた判定試験。
                var beforeExpiry=ContractJson.Copy(gateway);
                var assignment=Assert.Single(beforeExpiry.Offices,o=>o.Subject==recipient && o.Kind=="castellan" && o.Target==joined.Place);
                beforeExpiry.Offices=beforeExpiry.Offices.Select(o=>o==assignment?o with{Until=beforeExpiry.ElapsedSeconds+1}:o).ToArray();
                Assert.Contains(IndependenceGate.Available(beforeExpiry,recipient),i=>i.Action==CourtAction.ReceiveIndependentKeep);
                beforeExpiry.ElapsedSeconds++;
                Assert.DoesNotContain(IndependenceGate.Available(beforeExpiry,recipient),i=>i.Action==CourtAction.ReceiveIndependentKeep);
                session.SetPaused(true);
                var directory=Directory.CreateTempSubdirectory("allied-gate-");
                try{
                    var saves=new GenerationSaveStore(directory.FullName,gateway.Definitions);
                    Assert.True(saves.Save(session,"gate",gateway.WorldId).Published);
                    var host=new WorldSessionHost(new WorldSession(w));
                    Assert.Equal(ResultCode.Accepted,host.CommitLoad(saves.Prepare("gate"),host.Current,host.Current.Revision));
                    NewGameWorldTests.At(host.Current,session.Seconds+1);
                    NewGameWorldTests.At(session,session.Seconds+1);
                    Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
                }finally{directory.Delete(true);}
                var handed=session.DiagnosticSnapshot();
                var history=handed.Court!.Events.Where(e=>e.Input.Reference==joined.Input.Id).Select(e=>e.Input.Action).ToArray();
                Assert.Contains(CourtAction.RequestIndependentGate,history);
                Assert.Contains(CourtAction.ReceiveIndependentKeep,history);
                Assert.True(JointIndependence.ObserveOwnCastle(handed,recipient)!.ControlsCastle);
                var report=Assert.Single(handed.Messaging.Messages,m=>m.Kind==IndependenceLetters.KeepReport);
                Assert.Equal(recipient.Value,report.SenderId);
                Assert.Equal(Actor.Value,report.RecipientId);
                Assert.Contains("本丸を受領しました",report.Body);
                Assert.Empty(IndependenceLetters.KeepReports(handed,recipient));
                Assert.DoesNotContain(IndependenceLetters.Delivered(handed,Actor),l=>l.Id==report.Id);
                var messenger=handed.Couriers!.Missions.Single(m=>handed.Messaging.Attempts.Any(a=>a.MessageId==report.Id && a.Id==m.Attempt));
                Assert.Equal(founderNode,messenger.Destination);
                NewGameWorldTests.At(session,messenger.ReadyAt+1);
                Assert.Null(IndependenceLetters.Delivered(session.DiagnosticSnapshot(),Actor).Single(l=>l.Id==report.Id).Body);
                Assert.Equal(ResultCode.Accepted,session.Open(Actor,new(report.Id),session.Revision).Code);
                Assert.Equal(report.Body,IndependenceLetters.Delivered(session.DiagnosticSnapshot(),Actor).Single(l=>l.Id==report.Id).Body);
            }
            var replay=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(after)));
            Assert.Equal(ContractJson.Write(after.Memberships),ContractJson.Write(replay.DiagnosticSnapshot().Memberships));
            return;
        }
        Assert.Fail("独立原本を作成できませんでした");
    }

    [Fact]
    public void PublicDeclarationUsesKnownCastleAndCarriesNoSecretPact()
    {
        for(ulong seed=0;seed<32;seed++){
            var initial=Start(seed,castellan:true);var session=new WorldSession(initial);
            EntityId pact=new("request:private-signatures");
            Assert.Equal(ResultCode.Accepted,session.NegotiatePact(pact,Partner,PactPurpose.RiseTogether,session.Revision).Code);
            if(!session.DiagnosticSnapshot().SecretPacts!.Single().Accepted)continue;
            Assert.Equal(ResultCode.Accepted,session.SignalPact(pact,session.Revision).Code);
            var declared=session.DiagnosticSnapshot();
            var choices=IndependenceLetters.Choices(declared,Actor).Where(i=>i.Person.Value=="person:shingen").ToArray();
            Assert.Equal(2,choices.Length);
            foreach(var input in choices){
                var sender=new WorldSession(ContractJson.Copy(declared));
                Assert.Equal(ResultCode.NotAvailable,sender.ManageCourt(input with{Site=new("site:odawara")},sender.Revision).Code);
                var sent=sender.ManageCourt(input,sender.Revision);Assert.True(sent.Code==ResultCode.Accepted,sent.VisibleReason);
                Assert.Equal(ResultCode.AlreadyAccepted,sender.ManageCourt(input,sender.Revision).Code);
                var world=sender.DiagnosticSnapshot();var letter=Assert.Single(world.Messaging.Messages,m=>m.Kind==IndependenceLetters.Kind);
                Assert.Equal("site:kofu",letter.DeliverySite);Assert.Equal(input.Id.Value,letter.CorrelationId);
                Assert.DoesNotContain(pact.Value,letter.Body);Assert.DoesNotContain(Partner.Value,letter.Body);
                Assert.Equal(input.Action==CourtAction.SeekDeclaredSupport,letter.Body.Contains("支持を求めます"));
                Assert.DoesNotContain(world.Messaging.Knowledge,k=>k.MessageId==letter.Id && k.CharacterId==input.Person.Value);
                var mission=Assert.Single(world.Couriers!.Missions,m=>world.Messaging.Attempts.Any(a=>a.MessageId==letter.Id && a.Id==m.Attempt));
                Assert.Equal("kofu",mission.Destination);
                var resumed=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(world)));
                NewGameWorldTests.At(resumed,mission.ReadyAt+1);
                Assert.Contains(resumed.DiagnosticSnapshot().Messaging.Receipts,r=>r.MessageId==letter.Id && r.ReceiverId==input.Person.Value);
                var reached=resumed.DiagnosticSnapshot();
                Assert.Empty(DefectionPacts.Known(reached,input.Person));
                var replies=reached.Messaging.Messages.Where(m=>m.Kind==IndependenceLetters.Reply).ToArray();
                if(input.Action==CourtAction.AnnounceIndependence){Assert.Empty(replies);continue;}
                var answer=Assert.Single(replies);
                Assert.Equal(input.Person.Value,answer.SenderId);Assert.Equal(Actor.Value,answer.RecipientId);
                var courier=Assert.Single(reached.Couriers!.Missions,m=>reached.Messaging.Attempts.Any(a=>a.MessageId==answer.Id && a.Id==m.Attempt));
                Assert.Equal("castle",courier.Destination);
                var disk=Directory.CreateTempSubdirectory("independence-reply-");
                try{
                    resumed.SetPaused(true);var store=new GenerationSaveStore(disk.FullName,initial.Definitions);
                    Assert.True(store.Save(resumed,"reply",initial.WorldId).Published);
                    var host=new WorldSessionHost(new WorldSession(initial));
                    Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("reply"),host.Current,host.Current.Revision));
                    NewGameWorldTests.At(host.Current,courier.ReadyAt+1);
                    var unopened=Assert.Single(IndependenceLetters.Delivered(host.Current.DiagnosticSnapshot(),Actor));
                    Assert.Null(unopened.Body);
                    Assert.Equal(ResultCode.Accepted,host.Current.Open(Actor,new(answer.Id),host.Current.Revision).Code);
                    Assert.Equal(answer.Body,Assert.Single(IndependenceLetters.Delivered(host.Current.DiagnosticSnapshot(),Actor)).Body);
                }finally{disk.Delete(true);}
            }
            return;
        }
        Assert.Fail("独立に合意する開始条件がありません");
    }

    [Fact]
    public void LocalCastellanBringsOnlyTheCastleInHisCharge()
    {
        EntityId id=new("request:castle-rise");
        for(ulong seed=0;seed<32;seed++)
        {
            var before=Start(seed,castellan:true);
            var s=new WorldSession(before);
            Assert.Equal(ResultCode.Accepted,s.NegotiatePact(id,Partner,PactPurpose.RiseTogether,s.Revision).Code);
            if(!s.DiagnosticSnapshot().SecretPacts!.Single().Accepted)continue;
            Assert.Equal(ResultCode.Accepted,s.SignalPact(id,s.Revision).Code);
            var after=s.DiagnosticSnapshot();
            var declaration=Assert.Single(after.Declarations!);
            Assert.True(HouseRestoration.RepresentsFoundation(after, declaration, declaration.Founder, after.ElapsedSeconds, after.CommitSequence));
            Assert.False(HouseRestoration.RepresentsFoundation(after, declaration, Partner, after.ElapsedSeconds, after.CommitSequence));
            Assert.Null(HouseRestoration.SuccessorOrganization(after, declaration.FormerHouse, after.ElapsedSeconds, after.CommitSequence + 1));
            var control=Assert.Single(after.LandRights,r=>r.Subject==declaration.House);
            Assert.Equal(declaration.Site,control.Target);
            Assert.Equal("control",control.Kind);
            Assert.NotNull(FactionStances.ResolveAuthority(after,Actor,declaration.House,after.ElapsedSeconds,after.CommitSequence));
            Assert.Null(FactionStances.ResolveAuthority(after,Actor,declaration.House,declaration.At,declaration.Revision-1));
            Assert.DoesNotContain(after.LandRights,r=>r.Subject==declaration.FormerHouse && r.Target==control.Target && r.Until>s.Seconds);
            Assert.Equal(ContractJson.Write(before.Units),ContractJson.Write(after.Units));
            var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(after)));
            Assert.Equal(ContractJson.Write(after.LandRights),ContractJson.Write(restored.DiagnosticSnapshot().LandRights));
            return;
        }
        Assert.Fail("城主が合意できる開始条件がありません");
    }

    [Fact]
    public void CaptorFindsOnlyTheSignedCopyCarriedByThePrisoner()
    {
        EntityId target=new("created:contact"), pactId=new("request:private-agreement");
        const string site="site:kyoto";
        var actors=new[]{NewGameWorldTests.Officer(site:site,role:StartTemplate.Retainer,lord:"person:nobunaga"),
            NewGameWorldTests.Officer(Partner.Value,StartTemplate.Retainer,site,"person:nobunaga"),
            NewGameWorldTests.Officer(target.Value,site:site)};
        var setup=NewGameWorldTests.Setup(actors);
        for(ulong seed=0;seed<128;seed++)
        {
            var w=NewGameWorld.Create(setup.World,setup.Registered,Actor.Value,seed,inspectionPolicy:LocalInspectionFindings.Policy,
                workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy,territoryPolicy:CastleSiege.Policy,companionPolicy:PersonalCompanions.Policy,
                developmentPolicy:CharacterDevelopment.Policy,socialPolicy:CharacterRelations.Policy);
            var session=new WorldSession(w);
            Assert.Equal(ResultCode.Accepted,session.NegotiatePact(pactId,Partner,PactPurpose.WithholdService,session.Revision).Code);
            var signed=session.DiagnosticSnapshot();
            if(!signed.SecretPacts!.Single().Accepted)continue;
            Assert.Empty(DefectionPacts.Known(signed,target));
            var sight=new SchemeInput(new("request:sight"),SchemeAction.ObservePerson,Actor,target);
            var contact=sight with{Id=new("request:contact"),Action=SchemeAction.SeekContact,Reference=sight.Id};
            var attack=contact with{Id=new("request:attack"),Action=SchemeAction.AttemptAssassination,Reference=contact.Id};
            foreach(var input in new[]{sight,contact,attack})Assert.Equal(ResultCode.Accepted,session.ManageScheme(input,session.Revision).Code);
            session.SetPaused(true);
            var after=session.DiagnosticSnapshot();
            if(!after.Schemes!.Events.Any(e=>e.Input.Action==SchemeAction.CaughtInEncounter))continue;
            var directory=Directory.CreateTempSubdirectory("hakutai-seizure-");
            try
            {
                var store=new GenerationSaveStore(directory.FullName,w.Definitions);
                Assert.True(store.Save(session,"discovery",w.WorldId).Published);
                var host=new WorldSessionHost(new WorldSession(w));
                Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("discovery"),host.Current,host.Current.Revision));
                foreach(var world in new[]{after,host.Current.DiagnosticSnapshot()})
                {
                    var evidence=Assert.Single(PactDocuments.Evidence(world,target));
                    Assert.Equal(pactId,Assert.Single(DefectionPacts.Known(world,target)).Id);
                    Assert.Empty(DefectionPacts.Known(world,new("person:nobunaga")));
                    var copies=SocialAssets.Objects(world).Where(o=>o.Kind=="secret-pact").ToArray();
                    Assert.Equal(2,copies.Length);
                    Assert.Contains(copies,p=>p.Id==evidence.Document && p.Owner==Actor && p.Holder==target);
                    Assert.Contains(copies,p=>p.Owner==Partner && p.Holder==Partner);
                }
                return;
            }
            finally{directory.Delete(true);}
        }
        Assert.Fail("承諾後に捕縛される開始条件がありません");
    }

    [Fact]
    public void BothAnswersArePrivateAndAPromiseAloneDoesNotChangeMembership()
    {
        var outcomes=new HashSet<bool>();
        for(ulong seed=0;seed<32 && outcomes.Count<2;seed++)
        {
            var w=Start(seed);var s=new WorldSession(w);EntityId id=new("request:joint-independence");
            Assert.Equal(ResultCode.Accepted,s.NegotiatePact(id,Partner,PactPurpose.RiseTogether,s.Revision).Code);
            var after=s.DiagnosticSnapshot();
            var paper=Assert.Single(DefectionPacts.Known(after,Actor));
            outcomes.Add(paper.Accepted);
            Assert.Equal(paper,Assert.Single(DefectionPacts.Known(after,Partner)));
            Assert.Empty(DefectionPacts.Known(after,new("person:nobunaga")));
            Assert.Equal(ContractJson.Write(w.Memberships),ContractJson.Write(after.Memberships));
            Assert.Equal(ContractJson.Write(w.Units),ContractJson.Write(after.Units));
            Assert.Equal(ResultCode.AlreadyAccepted,s.NegotiatePact(id,Partner,PactPurpose.RiseTogether,s.Revision).Code);
            Assert.Equal(ResultCode.IdPayloadConflict,s.NegotiatePact(id,Partner,PactPurpose.LeaveHouse,s.Revision).Code);
            s.SetPaused(true);after=s.DiagnosticSnapshot();
            var directory=Directory.CreateTempSubdirectory("hakutai-pact-");
            try
            {
                var store=new GenerationSaveStore(directory.FullName,w.Definitions);
                Assert.True(store.Save(s,"pact",after.WorldId).Published);
                var host=new WorldSessionHost(new WorldSession(w));
                Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("pact"),host.Current,host.Current.Revision));
                Assert.Equal(ContractJson.Write(after),ContractJson.Write(host.Current.DiagnosticSnapshot()));
            }
            finally{directory.Delete(true);}
        }
        Assert.Equal(2,outcomes.Count);
    }

    [Fact]
    public void ForeignRetainerMustBeMetAndCanOnlyMakeAnApplicablePromise()
    {
        var w=Start(0,true);var s=new WorldSession(w);
        EntityId id=new("request:limited-service");
        Assert.Equal(ResultCode.NotAvailable,s.NegotiatePact(id,Partner,PactPurpose.WithholdService,s.Revision).Code);
        Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(w,"request:visit-foreign","kofu"),s.Revision).Code);
        NewGameWorldTests.At(s,2);NewGameWorldTests.At(s,s.GetBranchView(Actor).ArrivalAt!.Value+2);
        Assert.Equal(ResultCode.NotAvailable,s.NegotiatePact(id,Partner,PactPurpose.RiseTogether,s.Revision).Code);
        Assert.Equal(ResultCode.Accepted,s.NegotiatePact(id,Partner,PactPurpose.WithholdService,s.Revision).Code);
        Assert.Equal(new EntityId("site:kofu"),Assert.Single(s.DiagnosticSnapshot().SecretPacts!).Site);
    }
}
