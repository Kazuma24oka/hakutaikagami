using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

public class DefectionPactTests
{
    static readonly EntityId Actor=new("created:test"),Partner=new("created:partner");
    static ContractWorld Start(ulong seed,bool foreign=false,bool castellan=false)
    {
        var actor=NewGameWorldTests.Officer(role:castellan?StartTemplate.Castellan:StartTemplate.Retainer,lord:"person:nobunaga");
        var partner=NewGameWorldTests.Officer(Partner.Value,StartTemplate.Retainer,foreign?"site:kofu":actor.Site,
            foreign?"person:shingen":"person:nobunaga");
        var setup=NewGameWorldTests.Setup([actor,partner]);
        return NewGameWorld.Create(setup.World,setup.Registered,actor.Id,seed,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,
            socialPolicy:CharacterRelations.Policy,comingOfAge:15);
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
            var w=Start(seed);var s=new WorldSession(w);
            EntityId id=new("request:field-pact");
            Assert.Equal(ResultCode.Accepted,s.NegotiatePact(id,Partner,PactPurpose.RiseTogether,s.Revision).Code);
            if(!s.DiagnosticSnapshot().SecretPacts!.Single().Accepted)continue;
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
            Assert.Equal(ContractJson.Write(before.Units),ContractJson.Write(after.Units));
            Assert.Equal(ContractJson.Write(before.ResourceLots),ContractJson.Write(after.ResourceLots));
            Assert.DoesNotContain(after.Units,u=>u.Organization==after.Declarations!.Single().House);
            var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(after)));
            Assert.Equal(ContractJson.Write(after.Units),ContractJson.Write(restored.DiagnosticSnapshot().Units));
            return;
        }
        Assert.Fail("実兵を引き受けた武将が独立できる開始条件がありません");
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
