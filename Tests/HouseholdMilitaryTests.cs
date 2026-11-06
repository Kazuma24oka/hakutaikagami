using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class HouseholdMilitaryTests
{
    static WorldSession Start()=>new(MilitaryDebriefTests.New());
    static void At(WorldSession s,long at)=>NewGameWorldTests.At(s,at);
    static void Open(WorldSession s){foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,m.Id,s.Revision).Code);}
    static NewMilitaryInput Input(WorldSession s,NewMilitaryAction action,long amount=0,bool scoped=true)=>new(new("request:house-military/"+s.Revision+"/"+action),action,amount){ObservationPolicy=action==NewMilitaryAction.Request?NewGameMilitary.ObservationContract:null,AuthorityPolicy=action==NewMilitaryAction.Request?NewGameMilitary.AuthorityContract:null,FromLedger=action==NewMilitaryAction.Request?NewGameMilitary.SourceForCurrentHouse(s.DiagnosticSnapshot()):null,LedgerPolicy=action==NewMilitaryAction.Request&&scoped?NewGameMilitary.LedgerIdentityContract:null,CycleContract=action==NewMilitaryAction.Request?NewGameMilitary.CycleForCurrentHouse(s.DiagnosticSnapshot()):null};
    static void Send(WorldSession s,NewMilitaryAction a,long amount=0,bool scoped=true){var result=s.DecideNewMilitary(Input(s,a,amount,scoped),s.PlayerControl,s.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);}
    static void Permit(WorldSession s,bool scoped=true){Send(s,NewMilitaryAction.Request,scoped:scoped);At(s,s.Seconds+2);Open(s);Send(s,NewMilitaryAction.Accept);At(s,s.Seconds+2);Open(s);Send(s,NewMilitaryAction.Pickup);}
    static (WorldSession Session,CourtInput Offer) AppointCaptain()
    {
        var setup=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var s=new WorldSession(NewGameWorld.Create(setup,[],"person:shingen",historicalCatalog:HistoricalStartCatalog.Current,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy));
        var person=new EntityId(setup.People.First(p=>p.Lord=="person:shingen").Id);
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        Good(s.ManageCompanion(new(new("request:follow-captain"),person,CompanionAction.Agree,20),s.PlayerControl,s.Revision));
        Permit(s);
        var w=s.DiagnosticSnapshot();var terms=w.NewMilitary!.Terms;var unit=NewGameMilitary.UnitIdsFor(w)[1];
        var pickup=w.NewMilitary.Requests.Last(q=>q.Input.Action==NewMilitaryAction.Pickup);
        var offer=new CourtInput(new("request:assign-captain"),CourtAction.OfferDetachment,person,terms.Owner,terms.Site,Reference:pickup.Input.Id,Carrier:unit);
        Good(s.ManageCourt(offer,s.Revision));At(s,s.Seconds+2);
        var after=s.DiagnosticSnapshot();var accepted=HouseholdDetachment.Appointment(after,unit,s.Seconds,s.Revision+1);
        Assert.Equal(person,accepted?.Actor);Assert.Equal(w.Units.Sum(u=>u.Soldiers),after.Units.Sum(u=>u.Soldiers));
        Assert.Equal(terms.Owner,after.NewMilitary!.Terms.Owner);
        return (s,offer);
    }

    [Fact]
    public void DeployedCaptainSignsOnlyAfterTheDirectiveReachesHisWing()
    {
        var (s,offer)=AppointCaptain();
        var wing=offer.Carrier!.Value;
        Send(s,NewMilitaryAction.Load,100000);
        Send(s,NewMilitaryAction.Deploy);
        Form(s,90000);
        Send(s,NewMilitaryAction.Handoff);
        Send(s,NewMilitaryAction.Withdraw);
        At(s,s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Journeys.Last().Route.Max(l=>l.EndAt)+1);
        var target=NewGameMilitary.SelectKnownTarget(s.DiagnosticSnapshot(),wing)!;
        var order=Input(s,NewMilitaryAction.SendUnitDirective) with{
            Directive=new(NewGameMilitary.UnitDirectiveContract,target,UnitDirectiveKind.Hold,null),
            DeliverBy=s.Seconds+600,PostPolicy=NewGameMilitary.UnitReplyRecoveryContract,
            TransportPolicy=NewGameMilitary.UnitSenderEvidenceContract};
        var issued=s.DecideNewMilitary(order,s.PlayerControl,s.Revision);
        Assert.True(issued.Code==ResultCode.Accepted,issued.VisibleReason);
        var dispatch=s.DiagnosticSnapshot().NewMilitary!.UnitDirective!.Dispatches.Last();
        At(s,dispatch.DueAt-1);
        var transit=s.DiagnosticSnapshot();
        Assert.Empty(transit.NewMilitary!.UnitDirective!.Receipts);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(transit)));
        foreach(var session in new[]{s,restored}){
            At(session,dispatch.DueAt);
            var world=session.DiagnosticSnapshot();
            var receipt=Assert.Single(world.NewMilitary!.UnitDirective!.Receipts);
            Assert.True(receipt.Applied);
            var message=world.Messaging.Messages.Single(m=>m.Id==receipt.ReplyMessage);
            var answer=ContractJson.Read<UnitDirectiveAnswer>(System.Text.Encoding.UTF8.GetBytes(message.Body));
            Assert.Equal(offer.Person,answer.Captain);
            Assert.Equal(wing,answer.Unit);
            At(session,receipt.ReplyDueAt);
            Open(session);
        }
        Assert.Equal(ContractJson.CanonicalHash(s.DiagnosticSnapshot()),ContractJson.CanonicalHash(restored.DiagnosticSnapshot()));
    }

    [Fact]
    public void ConsentingDirectFollowerCommandsOneBorrowedUnitUntilReturn()
    {
        var (s,offer)=AppointCaptain();
        var person=offer.Person;var unit=offer.Carrier!.Value;var w=s.DiagnosticSnapshot();
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        var next=offer with{Id=new("request:double-command"),Carrier=NewGameMilitary.UnitIdsFor(w)[2]};
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(next,s.Revision).Code);
        s=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        var inspection=new CompanionInput(new("request:detachment-inspect"),person,CompanionAction.Inspect);
        Assert.True(s.CompanionView().Single(c=>c.Person==person).Busy);
        Assert.Equal(ResultCode.NotAvailable,s.ManageCompanion(inspection,s.PlayerControl,s.Revision).Code);
        Finish(s);Assert.Null(HouseholdDetachment.Appointment(s.DiagnosticSnapshot(),unit,s.Seconds,s.Revision+1));
        Assert.False(s.CompanionView().Single(c=>c.Person==person).Busy);
        Good(s.ManageCompanion(inspection,s.PlayerControl,s.Revision));

    }
    [Fact]
    public void PaidPrivateCompanyMarchesBesideBorrowedWingWithSeparateFoodAndStrength()
    {
        var follower=NewGameWorldTests.Officer("created:company-leader",StartTemplate.Retainer,lord:"person:nobunaga");
        var enemy=NewGameWorldTests.Officer("created:company-enemy",StartTemplate.Daimyo,site:"site:fork");
        var setup=NewGameWorldTests.Setup([follower,enemy],[new(enemy.Id,"org:company-enemy",[enemy.Site],false)]);
        var initial=NewGameWorld.Create(setup.World,setup.Registered,"person:nobunaga",
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,
            companionPolicy:PersonalCompanions.Policy,economyPolicy:MilitaryEconomy.Policy,
            worldArmyPolicy:NpcArmies.CompactCombatPolicy,battlefieldPolicy:BattlefieldRules.Policy);
        var s=new WorldSession(initial);var person=new EntityId(follower.Id);var site=new EntityId(follower.Site);
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        void Pay(SalaryAction action,EntityId? contract=null,long amount=0,EntityId? preparation=null)=>
            Good(s.ManageSalary(new(new("request:company-salary/"+s.Revision),action,person,site,contract,amount,preparation),s.PlayerControl,s.Revision));
        Pay(SalaryAction.Propose,amount:20);
        var quote=s.DiagnosticSnapshot().Salary!.Quotes.Single().Id;
        Pay(SalaryAction.Confirm,quote);At(s,s.DiagnosticSnapshot().Salary!.Terms.Single().DueAt[0]);
        Pay(SalaryAction.Prepare,quote,10);
        Pay(SalaryAction.Receive,quote,preparation:s.DiagnosticSnapshot().Salary!.Entries.Last().Input.Id);
        Good(s.ManageCourt(new(new("request:company-muster"),CourtAction.SendMusterNotice,person,new("org:oda"),site,Amount:4,When:s.Seconds+30),s.Revision));
        At(s,s.Seconds+32);
        var recruited=s.DiagnosticSnapshot();
        Assert.Equal(4,PrivateRecruitment.Soldiers(recruited,person,site,s.Seconds,s.Revision+1));
        Good(s.ManageCompanion(new(new("request:company-follow"),person,CompanionAction.Agree,20),s.PlayerControl,s.Revision));
        Permit(s);
        var w=s.DiagnosticSnapshot();var wing=NewGameMilitary.UnitIdsFor(w)[1];
        var offer=HouseholdDetachment.Offer(w,person,wing,new("request:company-command"));
        Assert.NotNull(offer);Good(s.ManageCourt(offer!,s.Revision));At(s,s.Seconds+2);
        Good(s.ManageMilitaryRice(new(new("request:company-house-stock"),s.MilitaryEconomyView()!.Depot,2),s.PlayerControl,s.Revision));
        Send(s,NewMilitaryAction.Load,1000);Send(s,NewMilitaryAction.Deploy);
        w=s.DiagnosticSnapshot();
        var privateUnit=Assert.Single(WorldEngagement.Snapshot(w).Units,u=>u.Unit.Commander==person);
        Assert.Equal(4,privateUnit.Unit.Soldiers);
        Assert.Equal(100,BattlefieldRules.RationStrength(w,s.Seconds));
        Assert.Equal(NewGameMilitary.Position(w,1,s.Seconds),privateUnit.Position);
        Assert.DoesNotContain(w.Units,u=>u.Id==privateUnit.Unit.Id);
        var field=WorldEngagement.Snapshot(w);
        var raider=privateUnit with{Unit=new(new("unit:company-raider"),new("army:company-enemy"),new(enemy.Id),new("org:company-enemy"),40,0),
            Intent=EngagementIntent.DeliberateUnauthorizedAttack,Target=privateUnit.Unit.Id};
        var contact=BattlefieldRules.Resolve(w,field with{Units=[..field.Units,raider]});
        var loss=Assert.Single(contact.Losses,l=>l.Unit==privateUnit.Unit.Id);
        var casualties=Assert.Single(contact.Casualties!,c=>c.Unit==privateUnit.Unit.Id);
        Assert.Equal(loss.Soldiers,casualties.Wounded+casualties.Dead+casualties.Deserted);
        Assert.DoesNotContain(contact.Losses,l=>l.Unit==wing);
        Assert.Contains(contact.Strikes,strike=>strike.Attacker==privateUnit.Unit.Id && strike.Basis==EngagementBasis.ImmediateDefense);
        var dir=Directory.CreateTempSubdirectory("hakutai-private-company-");
        try
        {
            s.SetPaused(true);
            var store=new GenerationSaveStore(dir.FullName,initial.Definitions);
            Assert.True(store.Save(s,"deployed",initial.WorldId).Published);
            var host=new WorldSessionHost(new WorldSession(initial));
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("deployed"),host.Current,host.Current.Revision));
            foreach(var run in new[]{s,host.Current}){
                At(run,run.Seconds+2);
                Send(run,NewMilitaryAction.Enter);
                Finish(run);
                var home=run.DiagnosticSnapshot();
                Assert.Equal(4,PrivateRecruitment.Soldiers(home,person,site,run.Seconds,run.Revision+1));
                Assert.DoesNotContain(WorldEngagement.Snapshot(home).Units,u=>u.Unit.Id==privateUnit.Unit.Id);
            }
            Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        }
        finally{dir.Delete(true);}
    }

    static void Finish(WorldSession s){long carry=NewGameMilitary.View(s.DiagnosticSnapshot()).Carry;if(carry>0)Send(s,NewMilitaryAction.Unload,carry);Send(s,NewMilitaryAction.Report);Send(s,NewMilitaryAction.Return);At(s,s.Seconds+3);Open(s);}
    static void ChangeHouse(WorldSession s,string node,string lord){if(NewGameService.View(s.DiagnosticSnapshot()).Principal!=null){var current=NewGameService.View(s.DiagnosticSnapshot()).Cases.Last(c=>c.Principal==NewGameService.View(s.DiagnosticSnapshot()).Principal);Assert.Equal(ResultCode.Accepted,s.DecideService(new(new("request:house-leave/"+s.Revision),CareerAction.RequestRelease,current.Case),s.PlayerControl,s.Revision).Code);At(s,s.Seconds+2);Open(s);}Assert.Null(NewGameService.View(s.DiagnosticSnapshot()).Principal);Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:house-travel/"+s.Revision,node),s.Revision).Code);At(s,s.Seconds+2);At(s,s.GetBranchView(s.PlayerControl).ArrivalAt!.Value+2);var id=new EntityId("request:house-apply/"+s.Revision);Assert.Equal(ResultCode.Accepted,s.DecideService(new(id,CareerAction.Apply,Principal:new(lord)),s.PlayerControl,s.Revision).Code);At(s,s.Seconds+2);Open(s);Assert.Equal(ResultCode.Accepted,s.DecideService(new(new("request:house-accept/"+s.Revision),CareerAction.Accept,id),s.PlayerControl,s.Revision).Code);At(s,s.Seconds+2);Open(s);}
    static WorldSession AtSecondHouse(bool legacy=false){var s=Start();Permit(s,!legacy);Send(s,NewMilitaryAction.Load,100000);Finish(s);ChangeHouse(s,"odawara","person:ujiyasu");return s;}
    [Theory][InlineData(false)][InlineData(true)] public void SameOfficerTakesAnotherHouseArmyWithoutReplacingOldArmy(bool legacy)
    {
        var s=AtSecondHouse(legacy);var original=s.DiagnosticSnapshot();var oldUnits=ContractJson.Write(original.Units);var oldLotIds=original.ResourceLots.Select(l=>l.Id).ToHashSet();var oldLots=ContractJson.Write(original.ResourceLots.OrderBy(l=>l.Id.Value).ToArray());var oldState=ContractJson.Write(original.NewMilitary!);
        Assert.True(NewGameMilitary.ViewWithAuthority(original).CanRequest);Permit(s);var w=s.DiagnosticSnapshot();Assert.Equal(2,NewGameMilitary.LedgerStates(w).Length);Assert.Equal(6,w.Units.Length);
        Assert.Equal(oldState,ContractJson.Write(w.OtherMilitaryLedgers!.Single()));Assert.Contains(WorkRelations.Known(w),c=>c.Other==new EntityId("person:shingen")&&!c.Local);Assert.Equal(oldUnits,ContractJson.Write(w.Units.Where(u=>u.Organization==new EntityId("org:takeda")).ToArray()));
        Assert.Equal(oldLots,ContractJson.Write(w.ResourceLots.Where(l=>oldLotIds.Contains(l.Id)).OrderBy(l=>l.Id.Value).ToArray()));
        Assert.All(w.Units.Where(u=>u.Organization==new EntityId("org:hojo")),u=>Assert.Equal(s.PlayerControl,u.Commander));Assert.All(w.Units.Where(u=>u.Organization==new EntityId("org:takeda")),u=>Assert.Equal(new EntityId("person:shingen"),u.Commander));
        Send(s,NewMilitaryAction.Load,100000);Send(s,NewMilitaryAction.Deploy);At(s,s.Seconds+2);Send(s,NewMilitaryAction.Enter);Finish(s);ContractValidation.Validate(s.DiagnosticSnapshot());
    }
    [Fact] public void SaveRestoreAndSameFutureKeepBothHouseholdsExact()
    {
        var s=AtSecondHouse();Permit(s);Send(s,NewMilitaryAction.Load,100000);var copy=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(s.DiagnosticSnapshot())));foreach(var x in new[]{s,copy}){Send(x,NewMilitaryAction.Deploy);At(x,x.Seconds+2);Send(x,NewMilitaryAction.Enter);Finish(x);}Assert.Equal(ContractJson.CanonicalHash(s.DiagnosticSnapshot()),ContractJson.CanonicalHash(copy.DiagnosticSnapshot()));
    }
    [Fact] public void ReturnToFormerHouseReusesItsActualArmyAndInventory()
    {
        var s=AtSecondHouse();var first=ContractJson.Copy(s.DiagnosticSnapshot().NewMilitary!);Permit(s);Finish(s);ChangeHouse(s,"kofu","person:shingen");Permit(s);var w=s.DiagnosticSnapshot();Assert.Equal(2,NewGameMilitary.LedgerStates(w).Length);Assert.Equal(first.Terms.Army,w.NewMilitary!.Terms.Army);Assert.Equal(ContractJson.CanonicalHash(first.Binding!),ContractJson.CanonicalHash(w.NewMilitary.Binding!));Assert.Equal(2,NewGameMilitary.Cases(w).Length);Assert.Equal(6,w.Units.Length);Assert.Equal(1,NewGameMilitary.LedgerStates(w).Count(x=>x.Terms.Owner==new EntityId("org:takeda")));
    }
    [Fact] public void OldRequestReplayCannotSwitchActiveHouseOrDuplicateExecution()
    {
        var s=AtSecondHouse();var old=s.DiagnosticSnapshot().NewMilitary!.Requests.First().Input;Permit(s);string before=ContractJson.CanonicalHash(s.DiagnosticSnapshot());Assert.Equal(ResultCode.AlreadyAccepted,s.DecideNewMilitary(old,s.PlayerControl,0).Code);Assert.Equal(ResultCode.IdPayloadConflict,s.DecideNewMilitary(old with{Action=NewMilitaryAction.Accept},s.PlayerControl,s.Revision).Code);Assert.Equal(before,ContractJson.CanonicalHash(s.DiagnosticSnapshot()));
    }
    [Theory][InlineData("binding")][InlineData("archive")][InlineData("selection")][InlineData("stock")][InlineData("unopened")][InlineData("erased-history")]
    public void ForgedCrossHouseLedgerIsRejected(string part)
    {
        var s=AtSecondHouse();Permit(s);var w=s.DiagnosticSnapshot();if(part=="binding")w.NewMilitary=w.NewMilitary! with{Binding=w.OtherMilitaryLedgers!.Single().Binding};if(part=="archive")w.OtherMilitaryLedgers=[];if(part=="selection"){var old=w.NewMilitary!;w.NewMilitary=w.OtherMilitaryLedgers!.Single();w.OtherMilitaryLedgers=[old];}if(part=="unopened")w.OtherMilitaryLedgers=w.OtherMilitaryLedgers!.Select(x=>x with{Openings=[]}).ToArray();if(part=="erased-history"){var old=w.OtherMilitaryLedgers!.Single();var ids=old.Requests.Select(q=>q.Input.Id.Value).ToHashSet();var messages=w.Messaging.Messages.Where(m=>ids.Contains(m.CorrelationId)).Select(m=>m.Id).ToHashSet();w.OtherMilitaryLedgers=null;w.Units=w.Units.Where(u=>u.Organization!=old.Terms.Owner).ToArray();w.ResourceLots=w.ResourceLots.Where(l=>l.Owner!=old.Terms.Owner).ToArray();w.Messaging.Messages=w.Messaging.Messages.Where(m=>!messages.Contains(m.Id)).ToArray();w.Messaging.Attempts=w.Messaging.Attempts.Where(a=>!messages.Contains(a.MessageId)).ToArray();w.Messaging.Receipts=w.Messaging.Receipts.Where(a=>!messages.Contains(a.MessageId)).ToArray();w.Messaging.Knowledge=w.Messaging.Knowledge.Where(a=>!messages.Contains(a.MessageId)).ToArray();w.SealedMessages=w.SealedMessages.Where(a=>!messages.Contains(a.Value)).ToArray();}if(part=="stock")w.ResourceLots=w.ResourceLots.Select(l=>l.Owner==new EntityId("org:takeda")?l with{Quantity=l.Quantity+1}:l).ToArray();Assert.Throws<InvalidDataException>(()=>ContractValidation.Validate(w));
    }
    [Fact] public void ActualWaitingForMilitaryHandoverRemainsValidAfterChangingHouse()
    {
        var s=Start();Permit(s);Send(s,NewMilitaryAction.Load,100000);var card=NewGameService.View(s.DiagnosticSnapshot()).Cases.Single();
        Assert.Equal(ResultCode.Accepted,s.DecideService(new(new("request:wait-before-return"),CareerAction.RequestRelease,card.Case),s.PlayerControl,s.Revision).Code);At(s,s.Seconds+2);Open(s);Assert.Contains(s.DiagnosticSnapshot().Service!.Events,e=>e.Kind==CareerEventKind.ReleaseWaiting);
        Finish(s);ChangeHouse(s,"odawara","person:ujiyasu");Permit(s);ContractValidation.Validate(s.DiagnosticSnapshot());new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot()));
    }
    [Fact] public void LateInquiryReplyBelongsToItsOriginalHouseEvenAfterNewPickup()
    {
        var s=AtSecondHouse();Send(s,NewMilitaryAction.Inquire);var old=s.DiagnosticSnapshot().NewMilitary!;string inquiry=old.Requests.Last().Message!;Permit(s);
        for(int n=0;n<12&&!s.DiagnosticSnapshot().OtherMilitaryLedgers!.Single().Events.Any(e=>e.Effect==MilitaryEffect.StatusSent&&e.Source==inquiry);n++){
            var w=s.DiagnosticSnapshot();var attempt=w.Messaging.Attempts.Where(a=>a.Status==HakutaiKagami.Core.Messaging.AttemptStatus.InTransit).OrderBy(a=>a.DueAt).First();At(s,Math.Max(s.Seconds+1,attempt.DueAt));
        }
        var after=s.DiagnosticSnapshot();var archive=after.OtherMilitaryLedgers!.Single();Assert.Contains(archive.Events,e=>e.Effect==MilitaryEffect.StatusSent&&e.Source==inquiry);Assert.DoesNotContain(after.NewMilitary!.Events,e=>e.Source==inquiry);Assert.Equal(new EntityId("org:hojo"),after.NewMilitary.Terms.Owner);Assert.True(NewGameMilitary.View(after).Custody);new WorldSession(ContractJson.Copy(after));
    }

    static void Form(WorldSession s,int offset)
    {
        var a=NewGameMilitary.Anchor(s.DiagnosticSnapshot());Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(new(new("request:history-line/"+s.Revision),NewMilitaryAction.Form,A:new(a.X+offset,a.Y-12000),B:new(a.X+offset,a.Y+12000)),s.PlayerControl,s.Revision).Code);
        At(s,s.DiagnosticSnapshot().NewMilitary!.Orders.Last().Routes.SelectMany(x=>x).Max(x=>x.EndAt)+1);
    }
    [Fact] public void HistoricalPhysicalPositionUsesTheArmyActiveAtThatInstant()
    {
        var s=Start();Permit(s);Send(s,NewMilitaryAction.Load,100000);Send(s,NewMilitaryAction.Deploy);Form(s,20000);long oldAt=s.Seconds;var old=RegionalCampaign.ActorPointAt(s.DiagnosticSnapshot(),s.PlayerControl,oldAt);
        Send(s,NewMilitaryAction.Recall);At(s,s.DiagnosticSnapshot().NewMilitary!.Orders.Last().Routes.SelectMany(x=>x).Max(x=>x.EndAt)+1);Send(s,NewMilitaryAction.Enter);Finish(s);ChangeHouse(s,"odawara","person:ujiyasu");Permit(s);Send(s,NewMilitaryAction.Load,100000);Send(s,NewMilitaryAction.Deploy);Form(s,30000);
        var w=s.DiagnosticSnapshot();Assert.Equal(old,RegionalCampaign.ActorPointAt(w,s.PlayerControl,oldAt));var copy=new WorldSession(ContractJson.Copy(w));Assert.Equal(old,RegionalCampaign.ActorPointAt(copy.DiagnosticSnapshot(),s.PlayerControl,oldAt));Assert.Equal(FieldWorld.ToWorld(NewGameMilitary.PersonPosition(w,s.Seconds)),RegionalCampaign.ActorPointAt(w,s.PlayerControl,s.Seconds));
    }

}
