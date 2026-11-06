using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class HouseConditionTests
{
    [Fact]
    public void EqualPenaltyRequiresBothPartiesToFightAfterEnactment(){
        var head=NewGameWorldTests.Officer(role:StartTemplate.Daimyo);
        var a=NewGameWorldTests.Officer("created:first",StartTemplate.Retainer,lord:head.Id);
        var b=NewGameWorldTests.Officer("created:second",StartTemplate.Retainer,lord:head.Id);
        EntityId house=new("org:fight-test"),site=new(head.Site),first=new(a.Id),second=new(b.Id),lord=new(head.Id);
        var setup=NewGameWorldTests.Setup([head,a,b],[new(head.Id,house.Value,[head.Site],false)]);
        var w=NewGameWorld.Create(setup.World,setup.Registered,head.Id,42,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy);
        var s=new WorldSession(w);
        var consult=new CourtInput(new("request:law"),CourtAction.ConsultReform,first,house,site){Reform=ReformKind.EqualPenalty};
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(consult,s.Revision).Code);
        NewGameWorldTests.At(s,240);
        Assert.Equal(ResultCode.Accepted,s.Open(lord,new(HouseReforms.Letter(consult,HouseReforms.Answer)),s.Revision).Code);
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(new(new("request:enact"),CourtAction.EnactEqualPenalty,first,house,site,Reference:consult.Id),s.Revision).Code);
        w=s.DiagnosticSnapshot();
        var challenge=new CourtInput(new("request:fight"),CourtAction.ChallengeFight,second,house,site);
        w.CommitSequence++;HouseholdCourt.Execute(w,challenge,first);
        var peaceful=new WorldSession(ContractJson.Copy(w));
        NewGameWorldTests.At(peaceful,peaceful.Seconds+2);
        Assert.Contains(peaceful.DiagnosticSnapshot().Court!.Events,e=>e.Input.Action==CourtAction.DeclineFight);
        Assert.Empty(HouseholdCourt.Facts(peaceful.DiagnosticSnapshot(),lord,first));
        var joined=new CourtInput(new("request:joined"),CourtAction.JoinFight,first,house,site,Reference:challenge.Id);
        w.CommitSequence++;HouseholdCourt.Execute(w,joined,second);
        s=new WorldSession(ContractJson.Copy(w));
        var ruling=new CourtInput(new("request:censure"),CourtAction.CensureBoth,second,house,site,Reference:joined.Id);
        var result=s.ManageCourt(ruling,s.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var restored=new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot())).DiagnosticSnapshot();
        foreach(var p in new[]{first,second})Assert.Equal(-2,Assert.Single(HouseholdCourt.Facts(restored,lord,p)).Respect);
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(ruling with{Id=new("request:again")},s.Revision).Code);
    }
    [Theory]
    [InlineData(CourtAction.ConsentServiceRelease)]
    [InlineData(CourtAction.RefuseServiceRelease)]
    public void SubordinatePresentsOwnBondToItsActualLord(CourtAction decision){
        var head=NewGameWorldTests.Officer(role:StartTemplate.Daimyo);
        var master=NewGameWorldTests.Officer("created:middle-lord",StartTemplate.Retainer,lord:head.Id);
        var subject=NewGameWorldTests.Officer("created:subordinate",StartTemplate.Retainer,lord:master.Id);
        EntityId owner=new("org:service-reform"),site=new(head.Site),person=new(subject.Id),oldLord=new(master.Id),lord=new(head.Id);
        var setup=NewGameWorldTests.Setup([head,master,subject],[new(head.Id,owner.Value,[site.Value],false)]);
        var w=NewGameWorld.Create(setup.World,setup.Registered,subject.Id,42,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy);
        var proposal=new CourtInput(new("request:direct"),CourtAction.ConsultReform,person,owner,site){Reform=ReformKind.DirectService};
        w.CommitSequence++;HouseholdCourt.Execute(w,proposal,lord);
        var s=new WorldSession(w);NewGameWorldTests.At(s,120);
        Assert.Equal(ResultCode.Accepted,s.Open(person,new(HouseReforms.Letter(proposal,HouseReforms.Proposal)),s.Revision).Code);
        var bond=w.ServiceBonds.Single(b=>b.Subject==person && b.Target==oldLord);
        var show=new CourtInput(new("request:show-original"),CourtAction.PresentServiceRelease,oldLord,owner,site,Reference:proposal.Id){FormerOffice=bond.Id};
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(show,s.Revision).Code);
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(new(new("request:agree-direct"),CourtAction.AssentReform,lord,owner,site,Reference:proposal.Id),s.Revision).Code);
        var shown=s.ManageCourt(show,s.Revision);Assert.True(shown.Code==ResultCode.Accepted,shown.VisibleReason);
        var autonomous=new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot()));
        NewGameWorldTests.At(autonomous,autonomous.Seconds+2);
        var actual=Assert.Single(autonomous.DiagnosticSnapshot().Court!.Events,e=>e.Input.Reference==show.Id);
        Assert.Equal(oldLord,actual.Actor);
        Assert.Equal(CourtAction.ConsentServiceRelease,actual.Input.Action);
        var after=s.DiagnosticSnapshot();after.CommitSequence++;
        var reply=new CourtInput(new("request:old-lord-reply"),decision,person,owner,site,Reference:show.Id){FormerOffice=bond.Id};
        HouseholdCourt.Execute(after,reply,oldLord);
        var saved=new WorldSession(ContractJson.Copy(after)).DiagnosticSnapshot();
        Assert.Contains(saved.ServiceBonds,b=>b.Id==bond.Id && b.Until==long.MaxValue);
        var running=new WorldSession(ContractJson.Copy(saved));
        var release=new HouseholdServiceInput(new("request:leave-old-lord"),HouseholdServiceAction.Release,owner,oldLord,bond.Id){ReformConsent=reply.Id};
        var pledge=new HouseholdServiceInput(new("request:serve-daimyo"),HouseholdServiceAction.Pledge,owner,lord){ReformConsent=reply.Id};
        Assert.Equal(ResultCode.NotAvailable,running.ManageHouseholdService(pledge,person,running.Revision).Code);
        var freed=running.ManageHouseholdService(release,person,running.Revision);
        if(decision==CourtAction.ConsentServiceRelease){
            Assert.True(freed.Code==ResultCode.Accepted,freed.VisibleReason);
            running=new WorldSession(ContractJson.Copy(running.DiagnosticSnapshot()));
            var joined=running.ManageHouseholdService(pledge,person,running.Revision);
            Assert.True(joined.Code==ResultCode.Accepted,joined.VisibleReason);
            var state=new WorldSession(ContractJson.Copy(running.DiagnosticSnapshot())).DiagnosticSnapshot();
            Assert.Equal(lord,Assert.Single(state.ServiceBonds,b=>b.Subject==person && b.Until==long.MaxValue).Target);
            Assert.Equal(ContractJson.Write(saved.Memberships),ContractJson.Write(state.Memberships));
        }else Assert.Equal(ResultCode.NotAvailable,freed.Code);
        saved.CommitSequence++;
        Assert.Throws<InvalidDataException>(()=>HouseholdCourt.Execute(saved,reply with{Id=new("request:answer-again")},oldLord));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewEstatePaysOnlyCollectionsAfterExchange(bool province)
    {
        var head=NewGameWorldTests.Officer(role:StartTemplate.Daimyo);
        var retainer=NewGameWorldTests.Officer("created:travelling-retainer",StartTemplate.Retainer,lord:head.Id);
        EntityId owner=new("org:exchange"),person=new(retainer.Id),lord=new(head.Id),home=new(head.Site),destination=new("site:kofu");
        var setup=NewGameWorldTests.Setup([head,retainer],[new(head.Id,owner.Value,[home.Value,destination.Value],false)]);
        ContractWorld Start(ulong seed)=>NewGameWorld.Create(setup.World,setup.Registered,head.Id,seed,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy,companionPolicy:PersonalCompanions.Policy,socialPolicy:CharacterRelations.Policy);
        var w=Enumerable.Range(1,100).Select(i=>Start((ulong)i)).First(world=>CharacterRelations.AcceptsCompanion(world,person,20,0,1));
        var s=new WorldSession(w);
        NewGameWorldTests.At(s,RegionalEconomy.NextHarvest(w,0));
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        void Act(CourtInput input,EntityId actor){
            if(actor==lord){Good(s.ManageCourt(input,s.Revision));return;}
            var world=s.DiagnosticSnapshot();world.CommitSequence++;HouseholdCourt.Execute(world,input,actor);
            s=new WorldSession(ContractJson.Copy(world));
        }
        void Read(CourtInput original,string kind,EntityId reader){
            NewGameWorldTests.At(s,s.Seconds+120);
            var opened=s.Open(reader,new(HouseReforms.Letter(original,kind)),s.Revision);Assert.True(opened.Code is ResultCode.Accepted or ResultCode.AlreadyAccepted,opened.VisibleReason);
        }
        Good(s.ManageCompanion(new(new("request:travel-agreement"),person,CompanionAction.Agree,20),lord,s.Revision));
        for(int day=1;day<=2;day++){
            NewGameWorldTests.At(s,s.Seconds+86400);
            Good(s.ManageCompanion(new(new("request:pay/"+day),person,CompanionAction.Pay,20),lord,s.Revision));
        }
        var grant=new CourtInput(new("request:original-right"),CourtAction.GrantLevyShare,person,owner,home,Amount:25);
        if(!province)Act(grant,lord);
        var proposal=grant with{Id=new("request:swap"),Action=CourtAction.ConsultReform,Reform=ReformKind.ChangeFief,NewEstate=destination,LevyGrant=grant.Id};
        if(province)proposal=proposal with{Reform=ReformKind.AllocateProvince,LevyGrant=null};
        Act(proposal,lord);Read(proposal,HouseReforms.Proposal,person);
        Read(proposal,HouseReforms.Answer,lord);
        var order=new CourtInput(new("request:move-right"),CourtAction.IssueTransfer,person,owner,home,Reference:proposal.Id);
        Act(order,lord);NewGameWorldTests.At(s,s.Seconds+120);
        Assert.True(HakutaiKagami.Core.Messaging.MessageFlow.Knows(s.DiagnosticSnapshot().Messaging,HouseReforms.Letter(order,HouseReforms.TransferOrder),person.Value));
        if(province){
            var first=new CourtInput(new("request:first-estate"),CourtAction.ReceiveProvinceShare,lord,owner,home,Amount:25,Reference:order.Id);
            Act(first with{Action=CourtAction.DeliverProvinceShare,Person=person},lord);
            var partial=s.DiagnosticSnapshot();
            Assert.Equal(home,Assert.Single(HouseReforms.LevyGrants(partial)).Place);
            partial.CommitSequence++;
            Assert.Throws<InvalidDataException>(()=>HouseholdCourt.Execute(partial,first with{Id=new("request:second-too-soon"),Site=destination},person));
            Assert.Throws<InvalidDataException>(()=>HouseholdCourt.Execute(partial,first with{Id=new("request:duplicate-estate")},person));
            var dir=Directory.CreateTempSubdirectory("hakutai-province-");
            try{
                var store=new GenerationSaveStore(dir.FullName,w.Definitions);
                Assert.True(store.Save(s,"partial",w.WorldId).Published);
                var host=new WorldSessionHost(new WorldSession(w));
                Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("partial"),host.Current,host.Current.Revision));
                s=host.Current;
            }finally{dir.Delete(true);}
        }
        Good(s.ManageCompanion(new(new("request:travel-to-estate"),person,CompanionAction.TravelTogether,Destination:"kofu"),lord,s.Revision));
        NewGameWorldTests.At(s,s.Seconds+2);
        NewGameWorldTests.At(s,s.GetBranchView(lord).ArrivalAt!.Value+20);
        void Collect(string id,long amount)=>Good(s.ManageCommunity(new(new(id),destination,EconomyAction.CollectLevy,amount),lord,s.Revision));
        Collect("request:before-transfer",80);
        var exchange=new CourtInput(new("request:received-right"),CourtAction.ExchangeLevyShare,lord,owner,destination,Amount:25,Reference:order.Id){LevyGrant=grant.Id};
        if(province)exchange=exchange with{Action=CourtAction.DeliverProvinceShare,Person=person,LevyGrant=null};
        Act(exchange,province?lord:person);
        if(province)Assert.Equal(2,HouseReforms.LevyGrants(s.DiagnosticSnapshot()).Count());
        Collect("request:after-transfer",40);
        var receipt=new CourtInput(new("request:new-receipt"),CourtAction.ReceiveLevyShare,lord,owner,destination,Amount:10,Reference:exchange.Id);
        Act(receipt,person);
        Assert.Equal(10,HouseSalary.PrivateRice(s.DiagnosticSnapshot(),person));
        var after=s.DiagnosticSnapshot();after.CommitSequence++;
        Assert.Throws<InvalidDataException>(()=>HouseholdCourt.Execute(after,receipt with{Id=new("request:not-earned"),Amount=1},person));
    }

    [Fact]
    public void LocalRevenueEntitlementBeginsWithActualCollections()
    {
        var s=HouseholdCourtTests.Start();
        var grant=new CourtInput(new("request:share-of-levy"),CourtAction.GrantLevyShare,HouseholdCourtTests.Retainer,
            HouseholdCourtTests.Owner,HouseholdCourtTests.Site,Amount:25);
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(grant,s.Revision).Code);
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(grant with{Id=new("request:second-share")},s.Revision).Code);
        long Accrued(WorldSession session){
            var w=session.DiagnosticSnapshot();
            return HouseReforms.AccruedLevyRice(w,w.Court!.Events.Single(e=>e.Input.Id==grant.Id),w.ElapsedSeconds,w.CommitSequence+1);
        }
        var start=DateOnly.Parse(s.DiagnosticSnapshot().EpochDate);
        NewGameWorldTests.At(s,(new DateOnly(start.Year,10,1).DayNumber-start.DayNumber)*86400L);
        Assert.Equal(0,Accrued(s));
        var collect=new CommunityInput(new("request:actual-levy"),grant.Site,EconomyAction.CollectLevy,79);
        Assert.Equal(ResultCode.Accepted,s.ManageCommunity(collect,s.PlayerControl,s.Revision).Code);
        Assert.Equal(19,Accrued(s));
        Assert.Equal(ResultCode.Accepted,s.ManageCommunity(collect with{Id=new("request:levy-remainder"),Amount=1},s.PlayerControl,s.Revision).Code);
        var restored=new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot()));
        Assert.Equal(20,Accrued(restored));
        Assert.Equal(0,HouseSalary.PrivateRice(restored.DiagnosticSnapshot(),grant.Person));
        var before=restored.DiagnosticSnapshot();
        long Stock(ContractWorld world)=>world.Communities!.Accounts.Single(a=>a.Site==grant.Site).HouseRice;
        var receipt=new CourtInput(new("request:collect-part"),CourtAction.ReceiveLevyShare,s.PlayerControl,
            grant.Owner,grant.Site,Amount:12,Reference:grant.Id);
        var after=restored.DiagnosticSnapshot();after.CommitSequence++;
        HouseholdCourt.Execute(after,receipt,grant.Person);
        var resumed=new WorldSession(ContractJson.Copy(after)).DiagnosticSnapshot();
        Assert.Equal(Stock(before)-12,Stock(resumed));
        Assert.Equal(12,HouseSalary.PrivateRice(resumed,grant.Person));
        Assert.Equal(12,HouseReforms.ReceivedLevyRice(resumed,grant.Id,resumed.ElapsedSeconds,resumed.CommitSequence+1));
        resumed.CommitSequence++;
        Assert.Throws<InvalidDataException>(()=>HouseholdCourt.Execute(resumed,receipt with{Id=new("request:overdraw"),Amount=9},grant.Person));
    }

    [Theory]
    [InlineData(CourtAction.AssentReform)]
    [InlineData(CourtAction.CounterReform)]
    [InlineData(CourtAction.RejectReform)]
    public void AddressedOfficerChoosesOneReplyOnlyAfterReading(CourtAction choice)
    {
        var officer=HouseholdCourtTests.Retainer;
        var lord=new EntityId("person:nobunaga");
        var w=HouseholdCourtTests.Start(officer.Value).DiagnosticSnapshot();
        var proposal=new CourtInput(new("request:personal-consult"),CourtAction.ConsultReform,officer,
            HouseholdCourtTests.Owner,HouseholdCourtTests.Site){Reform=ReformKind.OpenMarket};
        w.CommitSequence++;HouseholdCourt.Execute(w,proposal,lord);
        var s=new WorldSession(w);
        var reply=new CourtInput(new("request:my-response"),choice,lord,proposal.Owner,proposal.Site,Reference:proposal.Id);
        NewGameWorldTests.At(s,120);
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(reply,s.Revision).Code);
        Assert.Equal(ResultCode.Accepted,s.Open(officer,new(HouseReforms.Letter(proposal,HouseReforms.Proposal)),s.Revision).Code);
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(reply,s.Revision).Code);
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(reply with{Id=new("request:changed-answer")},s.Revision).Code);
        var restored=new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot()));
        var id=new EntityId(HouseReforms.Letter(proposal,HouseReforms.Answer));
        Assert.Equal(ResultCode.NotAvailable,restored.Open(lord,id,restored.Revision).Code);
        NewGameWorldTests.At(restored,240);
        Assert.Equal(ResultCode.Accepted,restored.Open(lord,id,restored.Revision).Code);
        Assert.EndsWith(HouseReforms.ReplyText(choice),restored.DiagnosticSnapshot().Messaging.Messages.Single(m=>m.Id==id.Value).Body);
    }

    [Theory]
    [InlineData(false,false,false,false)]
    [InlineData(false,true,false,false)]
    [InlineData(true,false,false,false)]
    [InlineData(false,false,true,false)]
    [InlineData(false,true,false,true)]
    public void AgreedTransferStillRequiresAnOrderThatActuallyReachesTheOfficer(bool occupied,bool exchange,bool residence,bool surrender)
    {
        var head=NewGameWorldTests.Officer(role:StartTemplate.Daimyo);
        var officer=NewGameWorldTests.Officer("created:transfer-officer",StartTemplate.Castellan,lord:head.Id);
        var owner=new EntityId("org:transfer-test");
        var incumbent=NewGameWorldTests.Officer("created:prior-castellan",StartTemplate.Castellan,"site:kofu",head.Id);
        var setup=NewGameWorldTests.Setup(occupied?[head,officer,incumbent]:[head,officer],[new(head.Id,owner.Value,[head.Site,"site:kofu"],false)]);
        var w=NewGameWorld.Create(setup.World,setup.Registered,officer.Id,42,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy);
        EntityId lord=new(head.Id),person=new(officer.Id);
        var proposal=new CourtInput(new("request:actual-transfer"),CourtAction.ConsultReform,person,owner,new(head.Site)){
            Reform=residence?ReformKind.CastleResidence:ReformKind.Relocate,NewEstate=new("site:kofu")};
        var levy=new CourtInput(new("request:old-levy"),CourtAction.GrantLevyShare,person,owner,proposal.Site,Amount:25);
        long offset=0;
        if(exchange){
            var autumn=new WorldSession(w);
            NewGameWorldTests.At(autumn,RegionalEconomy.NextHarvest(w,0));
            offset=autumn.Seconds;
            w=autumn.DiagnosticSnapshot();
            w.CommitSequence++;HouseholdCourt.Execute(w,levy,lord);
            var steward=new WorldSession(w);
            var agreement=new StewardInput(new("request:transfer-local-steward"),StewardAction.Assign,proposal.Site,lord,ReturnAddress:proposal.Site);
            var assigned=steward.ManageStewardship(agreement,person,steward.Revision);
            Assert.True(assigned.Code==ResultCode.Accepted,assigned.VisibleReason);
            var collected=steward.ManageCommunity(new(new("request:pre-exchange-levy"),proposal.Site,EconomyAction.CollectLevy,80),person,steward.Revision);
            Assert.True(collected.Code==ResultCode.Accepted,collected.VisibleReason);
            w=steward.DiagnosticSnapshot();
            proposal=proposal with{Reform=ReformKind.ChangeFief,Amount=25,LevyGrant=levy.Id};
        }
        if(surrender)proposal=proposal with{Reform=ReformKind.Confiscate,NewEstate=null};
        w.CommitSequence++;HouseholdCourt.Execute(w,proposal,lord);
        var s=new WorldSession(w);
        NewGameWorldTests.At(s,offset+120);
        Assert.Equal(ResultCode.Accepted,s.Open(person,new(HouseReforms.Letter(proposal,HouseReforms.Proposal)),s.Revision).Code);
        var reply=new CourtInput(new("request:transfer-yes"),CourtAction.AssentReform,lord,owner,proposal.Site,Reference:proposal.Id);
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(reply,s.Revision).Code);
        var order=new CourtInput(new("request:transfer-order"),CourtAction.IssueTransfer,person,owner,proposal.Site,Reference:proposal.Id);
        var early=s.DiagnosticSnapshot();early.CommitSequence++;
        Assert.Throws<InvalidDataException>(()=>HouseholdCourt.Execute(early,order,lord));
        NewGameWorldTests.At(s,offset+240);
        Assert.Equal(ResultCode.Accepted,s.Open(lord,new(HouseReforms.Letter(proposal,HouseReforms.Answer)),s.Revision).Code);
        w=s.DiagnosticSnapshot();w.CommitSequence++;HouseholdCourt.Execute(w,order,lord);
        s=new WorldSession(ContractJson.Copy(w));
        EntityId document=new(HouseReforms.Letter(order,HouseReforms.TransferOrder));
        Assert.Equal(ResultCode.NotAvailable,s.Open(person,document,s.Revision).Code);
        NewGameWorldTests.At(s,offset+360);
        Assert.Equal(ResultCode.Accepted,s.Open(person,document,s.Revision).Code);
        Assert.Equal(proposal.Site,SocialWorld.Site(s.DiagnosticSnapshot(),person));
        if(surrender){
            var handover=new CourtInput(new("request:surrender-original"),CourtAction.SurrenderLevyShare,lord,owner,proposal.Site,Amount:25,Reference:order.Id){LevyGrant=levy.Id};
            var handedBack=s.ManageCourt(handover,s.Revision);
            Assert.True(handedBack.Code==ResultCode.Accepted,handedBack.VisibleReason);
            s=new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot()));
            Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(handover with{Id=new("request:duplicate-surrender")},s.Revision).Code);
            var more=s.ManageCommunity(new(new("request:after-forfeiture"),proposal.Site,EconomyAction.CollectLevy,40),person,s.Revision);
            Assert.True(more.Code==ResultCode.Accepted,more.VisibleReason);
            var state=s.DiagnosticSnapshot();
            Assert.Equal(20,HouseReforms.AccruedLevyRice(state,HouseReforms.LevyGrants(state).Single(),s.Seconds,s.Revision+1));
            var pay=s.ManageCourt(new(new("request:collect-arrears"),CourtAction.ReceiveLevyShare,lord,owner,proposal.Site,Amount:20,Reference:levy.Id),s.Revision);
            Assert.True(pay.Code==ResultCode.Accepted,pay.VisibleReason);
            Assert.Contains(state.Offices,o=>o.Subject==person && o.Kind=="castellan" && o.Until==long.MaxValue);
            return;
        }
        var heirloom=SocialAssets.Catalog(s.DiagnosticSnapshot(),proposal.Site).Single(a=>a.Kind=="tea");
        var purchase=s.ManageSocialAsset(new(new("request:transfer-tea"),SocialAssetAction.Buy,person,heirloom.Id),s.Revision);
        Assert.True(purchase.Code==ResultCode.Accepted,purchase.VisibleReason);
        EntityId freight=new("request:transfer-freight");
        CommandResult Freight(HouseFreightAction action)=>s.ManageHouseFreight(new(new("request:estate-freight/"+action+"/"+s.Revision),
            action,freight),person,s.Revision);
        var dispatch=s.ManageHouseFreight(new(freight,HouseFreightAction.Dispatch,Home:proposal.Site,Source:proposal.NewEstate,
            Commodity:"wood",Quantity:5,Advance:100),person,s.Revision);
        Assert.True(dispatch.Code==ResultCode.Accepted,dispatch.VisibleReason);
        var before=s.DiagnosticSnapshot();
        var baggage=HouseReforms.Baggage(before,before.Court!.Events.Single(e=>e.Input.Id==order.Id))!;
        Assert.Contains(baggage.PersonalItems,o=>o.Id==heirloom.Id && o.Owner==person);
        var old=before.Offices.Single(o=>o.Subject==person && o.Kind=="castellan" && o.Target==proposal.Site);
        var release=new CourtInput(new("request:give-up-office"),CourtAction.ReleaseCastleOffice,lord,owner,
            proposal.Site,Reference:order.Id){FormerOffice=old.Id};
        var replace=release with{Action=CourtAction.ExchangeLevyShare,Id=new("request:exchange-revenue"),FormerOffice=null,
            LevyGrant=levy.Id,Amount=25,Site=proposal.NewEstate!.Value};
        if(exchange)Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(replace,s.Revision).Code);
        var handed=s.ManageCourt(release,s.Revision);
        Assert.True(handed.Code==(exchange||residence?ResultCode.NotAvailable:ResultCode.Accepted),handed.VisibleReason);
        var enter=new CourtInput(new("request:enter-office"),CourtAction.EnterCastleOffice,lord,owner,
            proposal.NewEstate!.Value,Reference:order.Id);
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(enter,s.Revision).Code);
        Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:move-to-estate","kofu"),s.Revision).Code);
        NewGameWorldTests.At(s,s.Seconds+2);
        s.SetPaused(true);
        var dir=Directory.CreateTempSubdirectory("hakutai-transfer-");
        try{
            var store=new GenerationSaveStore(dir.FullName,before.Definitions);
            Assert.True(store.Save(s,"journey",before.WorldId).Published);
            var host=new WorldSessionHost(new WorldSession(before));
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("journey"),host.Current,host.Current.Revision));
            s=host.Current;
            NewGameWorldTests.At(s,s.GetBranchView(person).ArrivalAt!.Value+2);
            if(residence){
                var arrival=s.DiagnosticSnapshot();
                Assert.Equal(proposal.Site,HouseReforms.ResidenceAt(arrival,person,s.Seconds,s.Revision+1));
                var settle=new CourtInput(new("request:settle-at-kofu"),CourtAction.SettleCastleResidence,lord,owner,enter.Site,Reference:order.Id);
                var result=s.ManageCourt(settle,s.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
                Assert.True(store.Save(s,"residence",before.WorldId).Published);
                Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("residence"),host.Current,host.Current.Revision));
                var loaded=host.Current.DiagnosticSnapshot();
                Assert.Equal(enter.Site,HouseReforms.ResidenceAt(loaded,person,loaded.ElapsedSeconds,loaded.CommitSequence+1));
                var notice=settle with{Action=CourtAction.ReportResidence,Id=new("request:settled-notice"),Reference=settle.Id};
                var run=host.Current;
                Assert.Equal(ResultCode.Accepted,run.ManageCourt(notice,run.Revision).Code);
                Assert.Equal(proposal.Site.Value,HouseReforms.KnownAddress(run.DiagnosticSnapshot(),lord,person));
                var letter=new EntityId(HouseReforms.Letter(notice,HouseReforms.ResidenceNotice));
                NewGameWorldTests.At(run,run.DiagnosticSnapshot().Messaging.Attempts.Single(a=>a.MessageId==letter.Value).DueAt+1);
                Assert.Equal(ResultCode.Accepted,run.Open(lord,letter,run.Revision).Code);
                Assert.Equal(enter.Site.Value,HouseReforms.KnownAddress(run.DiagnosticSnapshot(),lord,person));
                var follow=proposal with{Id=new("request:follow-up-after-move"),Reform=ReformKind.ForeignLetters,NewEstate=null};
                var updated=run.DiagnosticSnapshot();updated.CommitSequence++;HouseholdCourt.Execute(updated,follow,lord);
                var dispatched=updated.Messaging.Messages.Single(m=>m.Id==HouseReforms.Letter(follow,HouseReforms.Proposal));
                Assert.Equal(enter.Site.Value,dispatched.Scope);
                var attempt=updated.Messaging.Attempts.Single(a=>a.MessageId==dispatched.Id);
                Assert.Equal("kofu",updated.Couriers!.Missions.Single(m=>m.Attempt==attempt.Id).Destination);
                var resumed=new WorldSession(ContractJson.Copy(updated));
                NewGameWorldTests.At(resumed,attempt.DueAt+1);
                Assert.Contains(resumed.DiagnosticSnapshot().Messaging.Receipts,r=>r.MessageId==dispatched.Id && r.ReceiverId==person.Value);
                var homeTrip=resumed.Submit(TravelWorld.Move(resumed.DiagnosticSnapshot(),"request:visit-lord","castle"),resumed.Revision);
                Assert.True(homeTrip.Code==ResultCode.Accepted,homeTrip.VisibleReason);
                NewGameWorldTests.At(resumed,resumed.Seconds+2);
                NewGameWorldTests.At(resumed,resumed.GetBranchView(person).ArrivalAt!.Value+2);
                var request=new NewMilitaryInput(new("request:service-after-move"),NewMilitaryAction.Request){LedgerPolicy=NewGameMilitary.LedgerIdentityContract,AuthorityPolicy=NewGameMilitary.AuthorityContract};
                var asked=resumed.DecideNewMilitary(request,person,resumed.Revision);
                Assert.True(asked.Code==ResultCode.Accepted,asked.VisibleReason);
                NewGameWorldTests.At(resumed,resumed.Seconds+3);
                var army=resumed.DiagnosticSnapshot();
                var militaryNotice=Assert.Single(army.Messaging.Messages,m=>m.Kind=="new-military-notice");
                Assert.Equal(proposal.Site,army.NewMilitary!.Terms.Site);
                Assert.Equal(proposal.Site.Value,militaryNotice.Scope);
                Assert.Equal(enter.Site.Value,militaryNotice.DeliverySite);
                var delivery=army.Messaging.Attempts.Single(a=>a.MessageId==militaryNotice.Id);
                Assert.Equal("kofu",army.Couriers!.Missions.Single(m=>m.Attempt==delivery.Id).Destination);
                var travel=resumed.Submit(TravelWorld.Move(army,"request:return-for-notice","kofu"),resumed.Revision);
                Assert.True(travel.Code==ResultCode.Accepted,travel.VisibleReason);
                NewGameWorldTests.At(resumed,resumed.Seconds+2);
                Assert.True(store.Save(resumed,"military-courier",before.WorldId).Published);
                Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("military-courier"),host.Current,host.Current.Revision));
                var meeting=host.Current;
                NewGameWorldTests.At(meeting,Math.Max(delivery.DueAt,meeting.GetBranchView(person).ArrivalAt!.Value)+2);
                Assert.Equal(ResultCode.NotAvailable,meeting.Open(person,new(militaryNotice.Id),meeting.Revision).Code);
                var failure=Assert.Single(meeting.DiagnosticSnapshot().Messaging.Messages,m=>m.Kind=="delivery-failure" && m.Body==delivery.Id);
                var failureDelivery=meeting.DiagnosticSnapshot().Messaging.Attempts.Single(a=>a.MessageId==failure.Id);
                NewGameWorldTests.At(meeting,Math.Max(meeting.Seconds,failureDelivery.DueAt)+1);
                var learned=meeting.Open(lord,new(failure.Id),meeting.Revision);
                Assert.True(learned.Code is ResultCode.Accepted or ResultCode.AlreadyAccepted,learned.VisibleReason);
                var retry=meeting.RetryCourier(lord,militaryNotice.Id,"kofu",meeting.Seconds+1,meeting.Revision);
                Assert.True(retry.Code==ResultCode.Accepted,retry.VisibleReason);
                var retransmission=meeting.DiagnosticSnapshot().Messaging.Attempts.Single(a=>a.MessageId==militaryNotice.Id && a.Number==2);
                NewGameWorldTests.At(meeting,retransmission.DueAt+1);
                var arrivedNotice=meeting.Open(person,new(militaryNotice.Id),meeting.Revision);
                Assert.True(arrivedNotice.Code==ResultCode.Accepted,arrivedNotice.VisibleReason);
                Assert.Equal(enter.Site,SocialWorld.Site(meeting.DiagnosticSnapshot(),person));
                Assert.Equal(proposal.Site,meeting.DiagnosticSnapshot().NewMilitary!.Terms.Site);
                Assert.False(NewGameMilitary.View(meeting.DiagnosticSnapshot()).CanPickup);

                Assert.Equal(ContractJson.Write(arrival.Offices),ContractJson.Write(loaded.Offices));
                Assert.Equal(ContractJson.Write(arrival.LandRights),ContractJson.Write(loaded.LandRights));
                Assert.Equal(ResultCode.NotAvailable,host.Current.ManageCourt(settle with{Id=new("request:repeat-residence")},host.Current.Revision).Code);
                return;
            }
            if(occupied){
                Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(enter,s.Revision).Code);
                var post=s.DiagnosticSnapshot().Offices.Single(o=>o.Subject.Value==incumbent.Id && o.Kind=="castellan");
                var present=enter with{Id=new("request:present-to-incumbent"),Action=CourtAction.PresentTransfer,Person=post.Subject,FormerOffice=post.Id};
                var shown=s.ManageCourt(present,s.Revision);
                Assert.True(shown.Code==ResultCode.Accepted,shown.VisibleReason);
                w=s.DiagnosticSnapshot();w.CommitSequence++;
                HouseholdCourt.Execute(w,present with{Id=new("request:yield-to-successor"),Action=CourtAction.YieldCastleOffice,Person=person},post.Subject);
                s=new WorldSession(ContractJson.Copy(w));
                Assert.True(s.DiagnosticSnapshot().Offices.Single(o=>o.Id==post.Id).Until<=s.Seconds);
            }
            var bought=Freight(HouseFreightAction.Buy);
            Assert.True(bought.Code==ResultCode.Accepted,bought.VisibleReason);
            Assert.Equal(ResultCode.NotAvailable,Freight(HouseFreightAction.Receive).Code);
            if(exchange){
                var completed=s.ManageCourt(replace,s.Revision);
                Assert.True(completed.Code==ResultCode.Accepted,completed.VisibleReason);
                var restored=new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot()));
                var rights=HouseReforms.LevyGrants(restored.DiagnosticSnapshot()).ToArray();
                Assert.Equal(2,rights.Length);
                var ledger=restored.DiagnosticSnapshot();
                var oldGrant=rights.Single(g=>g.Input.Id==levy.Id);
                var newGrant=rights.Single(g=>g.Input.Id==replace.Id);
                Assert.Equal(20,HouseReforms.AccruedLevyRice(ledger,oldGrant,restored.Seconds,restored.Revision+1));
                Assert.Equal(0,HouseReforms.AccruedLevyRice(ledger,newGrant,restored.Seconds,restored.Revision+1));
                Assert.Equal(0,HouseholdCourt.AvailableLevyRice(ledger,oldGrant,person,restored.Seconds,restored.Revision+1));
                Assert.Equal(25,rights.Single(g=>g.Input.Id==replace.Id).Input.Amount);
                Assert.Equal(restored.Revision,HouseReforms.LevyEnd(restored.DiagnosticSnapshot(),levy.Id));
                Assert.Equal(ResultCode.NotAvailable,restored.ManageCourt(replace with{Id=new("request:duplicate-exchange")},restored.Revision).Code);
                Assert.Contains(restored.DiagnosticSnapshot().Offices,o=>o.Id==old.Id && o.Until==long.MaxValue);
                Assert.True(store.Save(restored,"exchanged",before.WorldId).Published);
                Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("exchanged"),host.Current,host.Current.Revision));
                s=host.Current;
                var returnTrip=s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:return-for-arrears","castle"),s.Revision);
                Assert.True(returnTrip.Code==ResultCode.Accepted,returnTrip.VisibleReason);
                NewGameWorldTests.At(s,s.Seconds+2);
                NewGameWorldTests.At(s,s.GetBranchView(person).ArrivalAt!.Value+2);
                var later=s.ManageCommunity(new(new("request:post-exchange-old-land"),proposal.Site,EconomyAction.CollectLevy,40),person,s.Revision);
                Assert.True(later.Code==ResultCode.Accepted,later.VisibleReason);
                var receipt=new CourtInput(new("request:settle-old-land"),CourtAction.ReceiveLevyShare,lord,owner,proposal.Site,Amount:20,Reference:levy.Id);
                var settled=s.ManageCourt(receipt,s.Revision);
                Assert.True(settled.Code==ResultCode.Accepted,settled.VisibleReason);
                Assert.Equal(20,HouseSalary.PrivateRice(s.DiagnosticSnapshot(),person));
                Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(receipt with{Id=new("request:duplicate-arrears"),Amount=1},s.Revision).Code);
                return;
            }
            var beforeEntry=s.DiagnosticSnapshot();
            var delivered=HouseReforms.Baggage(beforeEntry,beforeEntry.Court!.Events.Single(e=>e.Input.Id==order.Id))!;
            Assert.Equal(enter.Site,delivered.Site);
            Assert.Equal(baggage.Purse,delivered.Purse);
            var shipment=Assert.Single(delivered.HouseShipments);
            Assert.Equal(proposal.Site,shipment.Terms.Home);
            Assert.Equal(owner,shipment.Terms.Owner);
            Assert.Equal(5,shipment.Cargo);Assert.Equal(100-shipment.Spent,shipment.Cash);
            Assert.Contains(delivered.PersonalItems,o=>o.Id==heirloom.Id && o.Owner==person && o.Holder==person);
            var arrived=s.ManageCourt(enter,s.Revision);
            Assert.True(arrived.Code==ResultCode.Accepted,arrived.VisibleReason);
            var after=s.DiagnosticSnapshot();
            Assert.Equal(ContractJson.Write(beforeEntry.Castellan!),ContractJson.Write(after.Castellan!));
            Assert.Equal(ContractJson.Write(beforeEntry.Materials!),ContractJson.Write(after.Materials!));
            Assert.Equal(ContractJson.Write(beforeEntry.Communities!),ContractJson.Write(after.Communities!));
            var unauthorized=s.ManageCastellan(new(new("request:store-with-office-alone"),CastellanAction.ReceiveStore,
                enter.Site,Appointment:enter.Id),person,s.Revision);
            Assert.Equal(ResultCode.NotAvailable,unauthorized.Code);
            Assert.Equal(enter.Site,after.Offices.Single(o=>o.Kind=="castellan" && o.Subject==person && o.Until==long.MaxValue).Target);
            Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(after.LandRights));
            Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(enter with{Id=new("request:repeat-entry")},s.Revision).Code);
            Assert.Equal(ResultCode.NotAvailable,Freight(HouseFreightAction.Receive).Code);
            s.SetPaused(true);
            Assert.True(store.Save(s,"arrived-with-house-cargo",before.WorldId).Published);
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("arrived-with-house-cargo"),host.Current,host.Current.Revision));
            s=host.Current;
            Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:return-with-cargo","castle"),s.Revision).Code);
            NewGameWorldTests.At(s,s.Seconds+2);
            NewGameWorldTests.At(s,s.GetBranchView(person).ArrivalAt!.Value+2);
            var paid=Freight(HouseFreightAction.Receive);
            Assert.True(paid.Code==ResultCode.Accepted,paid.VisibleReason);
            Assert.Equal(HouseFreightPhase.Completed,s.HouseFreightView()!.Jobs.Single().Phase);
            Assert.Equal(baggage.Purse,NewGameWork.Balance(s.DiagnosticSnapshot(),person));
        }finally{dir.Delete(true);}
    }

    [Fact]
    public void RelocationLetterNamesTheTwoEstatesWithoutMovingTheCastellan()
    {
        var head=NewGameWorldTests.Officer(role:StartTemplate.Daimyo);
        var follower=NewGameWorldTests.Officer("created:estate-officer",StartTemplate.Castellan,lord:head.Id);
        var owner=new EntityId("org:estate-test");
        var setup=NewGameWorldTests.Setup([head,follower],[new(head.Id,owner.Value,[head.Site,"site:fork"],false)]);
        var w=NewGameWorld.Create(setup.World,setup.Registered,head.Id,42,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy);
        var s=new WorldSession(w);
        EntityId person=new(follower.Id),site=new(head.Site);
        var offer=new CourtInput(new("request:estate-consult"),CourtAction.ConsultReform,person,owner,site){
            Reform=ReformKind.Relocate,NewEstate=new("site:fork")};
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(offer with{NewEstate=new("site:kofu")},s.Revision).Code);
        var before=s.DiagnosticSnapshot();
        var result=s.ManageCourt(offer,s.Revision);
        Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var sent=new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot())).DiagnosticSnapshot();
        var letter=sent.Messaging.Messages.Single(m=>m.Id==HouseReforms.Letter(offer,HouseReforms.Proposal));
        Assert.Contains(NewGameWorld.SiteName(offer.NewEstate!.Value.Value),letter.Body);
        Assert.Equal(ContractJson.Write(before.Locations),ContractJson.Write(sent.Locations));
        Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(sent.LandRights));
    }

    [Fact]
    public void ResidentLordAbolishesBarrierWhileAnotherPersonIsAlreadyTravelling()
    {
        var traveller=HouseholdCourtTests.Ronin;
        var lord=new EntityId("person:nobunaga");
        var site=HouseholdCourtTests.Site;
        var s=HouseholdCourtTests.Start(traveller.Value);
        void ActAsLord(CourtInput input){
            var world=s.DiagnosticSnapshot();
            world.CommitSequence++;
            HouseholdCourt.Execute(world,input,lord);
            s=new WorldSession(world);
        }
        var start=s.DiagnosticSnapshot();
        var from=NewGameWorld.Node(site.Value);
        var road=HouseReforms.LocalRoads(start,site).First(e=>BranchRoutes.Plan(from,e.From==from?e.To:e.From,[],w:start).SequenceEqual([e.Id]));
        var to=road.From==from?road.To:road.From;
        var gate=new CourtInput(new("request:concurrent-gate"),CourtAction.EstablishBarrier,lord,HouseholdCourtTests.Owner,site){BarrierEdge=road.Id};
        ActAsLord(gate);
        var consult=gate with{Action=CourtAction.ConsultReform,Person=HouseholdCourtTests.Retainer,Reform=ReformKind.RemoveBarrier,Id=new("request:concurrent-consult")};
        ActAsLord(consult);
        NewGameWorldTests.At(s,120);
        Assert.Equal(ResultCode.Accepted,s.Open(lord,new(HouseReforms.Letter(consult,HouseReforms.Answer)),s.Revision).Code);
        Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:leave-gate",to),s.Revision).Code);
        NewGameWorldTests.At(s,125);
        var original=s.DiagnosticSnapshot().Branches!.Journeys.Single(j=>j.Actor==traveller).Legs.Single();
        Assert.Equal(gate.Id,original.BarrierOrder);
        ActAsLord(consult with{Action=CourtAction.AbolishBarrier,Reform=null,Reference=consult.Id,Id=new("request:concurrent-removal")});
        Assert.Equal(original,s.DiagnosticSnapshot().Branches!.Journeys.Single(j=>j.Actor==traveller).Legs.Single());
        s.SetPaused(true);
        var dir=Directory.CreateTempSubdirectory("hakutai-concurrent-barrier-");
        try{
            var store=new GenerationSaveStore(dir.FullName,start.Definitions);
            Assert.True(store.Save(s,"abolished",start.WorldId).Published);
            var host=new WorldSessionHost(new WorldSession(start));
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("abolished"),host.Current,host.Current.Revision));
            s=host.Current;
            NewGameWorldTests.At(s,original.EndAt+2);
            Assert.Equal(BranchRoutes.GraphNodes(start).Single(n=>n.Id==to).Position,s.GetBranchView(traveller).Position);
            Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:return-no-gate",from),s.Revision).Code);
            NewGameWorldTests.At(s,s.Seconds+2);
            var back=s.DiagnosticSnapshot().Branches!.Journeys.Last(j=>j.Actor==traveller).Legs.Single();
            Assert.Null(back.BarrierOrder);
            Assert.Equal(TravelWorld.InspectionSeconds,(original.EndAt-original.StartAt)-(back.EndAt-back.StartAt));
        }finally{dir.Delete(true);}
    }

    [Fact]
    public void OnFootInspectionSurvivesSaveAndKeepsTheTravellerAtTheRoadEntrance()
    {
        var s=HouseholdCourtTests.Start();
        var start=s.DiagnosticSnapshot();
        var site=HouseholdCourtTests.Site;
        var from=NewGameWorld.Node(site.Value);
        var edge=HouseReforms.LocalRoads(start,site).First(e=>BranchRoutes.Plan(from,e.From==from?e.To:e.From,[],w:start).SequenceEqual([e.Id]));
        var to=edge.From==from?edge.To:edge.From;
        var gate=new CourtInput(new("request:traveller-gate"),CourtAction.EstablishBarrier,s.PlayerControl,HouseholdCourtTests.Owner,site){BarrierEdge=edge.Id};
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(gate,s.Revision).Code);
        Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:inspection",to),s.Revision).Code);
        NewGameWorldTests.At(s,5);
        var w=s.DiagnosticSnapshot();
        var leg=w.Branches!.Journeys.Last().Legs.First();
        Assert.Equal(gate.Id,leg.BarrierOrder);
        Assert.Equal(TravelWorld.End(start,edge,leg.StartAt)+TravelWorld.InspectionSeconds,leg.EndAt);
        Assert.Equal(0,TravelWorld.Progress(w,leg,s.Seconds));
        Assert.Equal(BranchRoutes.GraphNodes(w).Single(n=>n.Id==from).Position,s.GetBranchView(s.PlayerControl).Position);
        var redirect=TravelControl.Request(w,"request:return-through-gate",from);
        var changed=s.ChangeTravel(redirect,s.Revision);
        Assert.True(changed.Code==ResultCode.Accepted,changed.VisibleReason);
        var decision=s.DiagnosticSnapshot().TravelDecisions!.Single();
        Assert.Equal(leg,decision.After.First());
        Assert.Equal(gate.Id,decision.After.Last().BarrierOrder);
        Assert.Equal(leg.EndAt,decision.EffectiveAt);
        s.SetPaused(true);
        var dir=Directory.CreateTempSubdirectory("hakutai-barrier-");
        try{
            var store=new GenerationSaveStore(dir.FullName,w.Definitions);
            Assert.True(store.Save(s,"inspection",w.WorldId).Published);
            var host=new WorldSessionHost(new WorldSession(start));
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("inspection"),host.Current,host.Current.Revision));
            NewGameWorldTests.At(s,leg.StartAt+TravelWorld.InspectionSeconds+1);
            NewGameWorldTests.At(host.Current,s.Seconds);
            Assert.Equal(edge.FootSpeed,TravelWorld.Progress(host.Current.DiagnosticSnapshot(),leg,s.Seconds));
            Assert.Equal(s.GetBranchView(s.PlayerControl).Position,host.Current.GetBranchView(s.PlayerControl).Position);
            NewGameWorldTests.At(host.Current,decision.EffectiveAt+2);
            Assert.Equal("redirected",host.Current.DiagnosticSnapshot().TravelDecisions!.Single().Result);
            Assert.Equal(BranchRoutes.GraphNodes(w).Single(n=>n.Id==to).Position,host.Current.GetBranchView(s.PlayerControl).Position);
            NewGameWorldTests.At(host.Current,decision.After.Last().EndAt+1);
            Assert.Equal(BranchRoutes.GraphNodes(w).Single(n=>n.Id==from).Position,host.Current.GetBranchView(s.PlayerControl).Position);
        }finally{dir.Delete(true);}
    }

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
