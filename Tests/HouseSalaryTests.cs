using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class HouseSalaryTests
{
    static readonly EntityId Worker=HistoricalHouseholdCatalog.Edition.People.Single().Id;
    internal static WorldSession Start(bool employee=false)=>new(NewGameWorld.Create(NewGameWorld.Scenario(HistoricalHouseholdCatalog.Id),[],employee?Worker.Value:"person:shingen",42,historicalCatalog:HistoricalHouseholdCatalog.Id,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,economyPolicy:HouseSalary.Policy));
    static EntityId Other(WorldSession s)=>s.PlayerControl==Worker?new("person:shingen"):Worker;
    static CommandResult Act(WorldSession s,SalaryAction action,EntityId? contract=null,long amount=0,EntityId? prep=null,string? id=null)=>s.ManageSalary(new(new(id??"request:salary/"+s.Revision),action,Other(s),new("site:kofu"),contract,amount,prep),s.PlayerControl,s.Revision);
    static EntityId Agree(WorldSession s,long amount=20){var r=Act(s,SalaryAction.Propose,amount:amount);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);var q=s.DiagnosticSnapshot().Salary!.Quotes.Last();Assert.True(q.Agreed,q.Reply);r=Act(s,SalaryAction.Confirm,q.Id);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);return q.Id;}
    static void Advance(WorldSession s,long at){while(s.Seconds<at){long old=s.Seconds;s.SetPaused(false);s.AdvanceTo(at,100000);Assert.True(s.Seconds>old);}}
    [Fact]
    public void VillagePetitionRecordsCalledWorkersAndReachesLordSealed()
    {
        var s=new WorldSession(NewGameWorld.Create(NewGameWorld.Scenario(HistoricalHouseholdCatalog.Id),[],"person:shingen",42,
            historicalCatalog:HistoricalHouseholdCatalog.Id,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,economyPolicy:MilitaryEconomy.Policy));
        var salary=Agree(s);Advance(s,First(s));var portion=Prepare(s,salary,20);
        Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.Receive,salary,prep:portion).Code);
        var w=s.DiagnosticSnapshot();var term=w.Salary!.Terms.Single();
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(new(new("request:levy-share"),CourtAction.GrantLevyShare,Worker,term.Owner,term.Site,Amount:10),s.Revision).Code);
        w=s.DiagnosticSnapshot();w.CommitSequence++;
        HouseholdCourt.Execute(w,new(new("request:village-call"),CourtAction.RaiseVillageSoldiers,Worker,term.Owner,term.Site,
            Amount:10,When:w.ElapsedSeconds+86400),Worker);
        var notice=Assert.Single(w.Communities!.Notices,n=>n.Petition!=null);
        Assert.Equal(10,notice.Petition!.Workers);Assert.Equal(s.PlayerControl,notice.Recipient);
        RegionalCampaign.Bind(w);s=new WorldSession(w);
        Advance(s,s.Seconds+3);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(s.DiagnosticSnapshot())));
        foreach(var session in new[]{s,restored}){
            var message=Assert.Single(session.GetActorView(session.PlayerControl).Messages,m=>m.Id.Value==notice.Message);
            Assert.Null(message.KnownBody);
            Assert.Equal(ResultCode.Accepted,session.Open(session.PlayerControl,message.Id,session.Revision).Code);
            var body=session.GetActorView(session.PlayerControl).Messages.Single(m=>m.Id==message.Id).KnownBody;
            Assert.Equal(notice.Petition,ContractJson.Read<CommunityReport>(System.Text.Encoding.UTF8.GetBytes(body!)).Petition);
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }

    [Fact]
    public void PaidRetainerHiresOnlyAffordableSoldiersForReceivedMuster()
    {
        var session = new WorldSession(NewGameWorld.Create(NewGameWorld.Scenario(HistoricalHouseholdCatalog.Id), [], "person:shingen", 42,
            historicalCatalog: HistoricalHouseholdCatalog.Id, inspectionPolicy: LocalInspectionFindings.Policy,
            workPolicy: WorkFollowup.Policy, economyPolicy: MilitaryEconomy.Policy));
        var contract = Agree(session);
        Advance(session, First(session));
        var prep = Prepare(session,contract,5);
        Assert.Equal(ResultCode.Accepted,Act(session,SalaryAction.Receive,contract,prep:prep).Code);
        var before=session.DiagnosticSnapshot();
        var term=Assert.Single(before.Salary!.Terms);
        var order=new CourtInput(new("request:funded-muster"),CourtAction.SendMusterNotice,Worker,term.Owner,term.Site,
            Amount:8,When:session.Seconds+86400);
        var result=session.ManageCourt(order,session.Revision);
        Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var prepared=session.DiagnosticSnapshot();
        Assert.Equal(2,PrivateRecruitment.Soldiers(prepared,Worker,term.Site,order.When,session.Revision+1));
        Assert.Equal(1,HouseSalary.PrivateRice(prepared,Worker));
        Assert.Equal(NewGameWork.Balance(before,Worker)-16,NewGameWork.Balance(prepared,Worker));
        Advance(session,order.When);
        Assert.Equal(2,Assert.Single(session.DiagnosticSnapshot().Court!.Events,e=>e.Input.Action==CourtAction.AttendMuster).Input.Amount);
    }

    [Theory]
    [InlineData(CourtAction.RaiseVillageSoldiers)]
    [InlineData(CourtAction.HireMarketSoldiers)]
    public void VillageAndMarketSoldiersUsePrivateResourcesUntilTerm(CourtAction action)
    {
        var session = new WorldSession(NewGameWorld.Create(NewGameWorld.Scenario(HistoricalHouseholdCatalog.Id), [], Worker.Value, 42,
            historicalCatalog: HistoricalHouseholdCatalog.Id, inspectionPolicy: LocalInspectionFindings.Policy,
            workPolicy: WorkFollowup.Policy, economyPolicy: MilitaryEconomy.Policy));
        var contract = Agree(session);
        Advance(session, First(session));
        Assert.Equal(ResultCode.Accepted, Act(session, SalaryAction.Prepare, contract, 5).Code);
        var prepared = session.DiagnosticSnapshot().Salary!.Entries.Last().Input.Id;
        Assert.Equal(ResultCode.Accepted, Act(session, SalaryAction.Receive, contract, prep: prepared).Code);
        var before = session.DiagnosticSnapshot();
        var term = Assert.Single(before.Salary!.Terms);
        var levy = new CourtInput(new("request:private-soldiers"), action, Worker, term.Owner, term.Site,
            Amount: 2, When: session.Seconds + 2 * 86400);
        int Workers(ContractWorld w) => w.Communities!.Accounts.Single(a => a.Site == term.Site).Workforce;
        if (action == CourtAction.RaiseVillageSoldiers)
        {
            Assert.Equal(ResultCode.NotAvailable, session.ManageCourt(levy,session.Revision).Code);
            var grant = new CourtInput(new("request:grant-recruit-estate"), CourtAction.GrantLevyShare,
                Worker, term.Owner, term.Site, Amount: 10);
            before.CommitSequence++;
            HouseholdCourt.Execute(before, grant, term.Employer);
            session = new WorldSession(before);
        }
        var result = session.ManageCourt(levy, session.Revision);
        Assert.True(result.Code == ResultCode.Accepted, result.VisibleReason);
        var during = session.DiagnosticSnapshot();
        Assert.Equal(1, HouseSalary.PrivateRice(during, Worker));
        Assert.Equal(Workers(before) - (action == CourtAction.RaiseVillageSoldiers ? 2 : 0), Workers(during));
        Assert.Equal(NewGameWork.Balance(before, Worker) - (action == CourtAction.HireMarketSoldiers ? 16 : 0), NewGameWork.Balance(during, Worker));
        Assert.Equal(2, PrivateRecruitment.Soldiers(during, Worker, term.Site, session.Seconds, session.Revision + 1));
        var refused = session.ManageCourt(levy with {Id=new("request:unfunded-recruits")}, session.Revision);
        Assert.NotEqual(ResultCode.Accepted, refused.Code);
        Assert.Equal(ContractJson.Write(during), ContractJson.Write(session.DiagnosticSnapshot()));
        var restored = new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(during)));
        Advance(restored, levy.When);
        var returned = restored.DiagnosticSnapshot();
        Assert.Equal(0, PrivateRecruitment.Soldiers(returned, Worker, term.Site, levy.When, restored.Revision+1));
        Assert.Equal(Workers(before), Workers(returned));
        Assert.Equal(1, HouseSalary.PrivateRice(returned, Worker));
    }

    [Fact]
    public void RestorationClaimUsesUnpaidBalanceOfSignedSalaryAfterActualReceipt()
    {
        var session = Start(employee: true);
        var original = Agree(session);
        Advance(session, First(session));
        var prepare = Act(session, SalaryAction.Prepare, original, 5);
        Assert.Equal(ResultCode.Accepted, prepare.Code);
        var preparation = session.DiagnosticSnapshot().Salary!.Entries.Last().Input.Id;
        Assert.Equal(ResultCode.Accepted, Act(session, SalaryAction.Receive, original, prep: preparation).Code);
        var w = session.DiagnosticSnapshot();
        var term = Assert.Single(w.Salary!.Terms);
        var ask = new CourtInput(new("request:local-ask"), CourtAction.SeekRestorationSupport, Worker, term.Owner, term.Site);
        w.CommitSequence++;
        w.Court = new(1, HouseholdCourt.Policy, [new(ask, term.Employer, term.Site, w.ElapsedSeconds, w.CommitSequence, false)]);
        var claim = new CourtInput(new("request:arrears"), CourtAction.PresentRestorationArrears,
            term.Employer, term.Owner, term.Site, Amount: 15, Reference: original);
        w.CommitSequence++;
        Assert.Throws<InvalidDataException>(() => HouseholdCourt.Execute(w, claim with { Amount = 20 }, Worker));
        HouseholdCourt.Execute(w, claim, Worker);
        w = ContractJson.Copy(w);
        Assert.Equal(5, HouseSalary.PrivateRice(w, Worker));
        Assert.Equal(15, HouseRestoration.UnpaidSalary(Assert.Single(w.Salary!.Terms), w.ElapsedSeconds));
        w.CommitSequence++;
        Assert.Throws<InvalidDataException>(() => HouseholdCourt.Execute(w, claim with { Id = new("request:repeat") }, Worker));
        // 請求は別の原本。実支払は既存の給付セッションで行い、その受領記録を照合する。
        // 単一世界と同じ時間順になるよう、請求提示の後に支払の時刻を置く。
        Advance(session, session.Seconds + 1);
        var prepareBalance = Assert.IsType<SalaryInput>(HouseRestoration.SettlementForClaim(w, claim.Id));
        Assert.Equal(15, prepareBalance.Amount);
        Assert.Equal(ResultCode.Accepted, session.ManageSalary(prepareBalance, Worker, session.Revision).Code);
        void RefreshSalary()
        {
            var actual = session.DiagnosticSnapshot();
            w.Salary = actual.Salary;
            w.ElapsedSeconds = actual.ElapsedSeconds;
            w.CommitSequence = Math.Max(w.CommitSequence + 1, actual.CommitSequence);
        }
        RefreshSalary();
        var handover = Assert.IsType<SalaryInput>(HouseRestoration.SettlementForClaim(w, claim.Id));
        Assert.Equal(SalaryAction.Receive, handover.Action);
        Assert.Equal(ResultCode.Accepted, session.ManageSalary(handover, Worker, session.Revision).Code);
        RefreshSalary();
        Assert.Equal(20, HouseSalary.PrivateRice(w, Worker));
        Assert.Null(HouseRestoration.SettlementForClaim(w, claim.Id));
        Advance(session, term.DueAt[1]);
        RefreshSalary();
        Assert.True(HouseRestoration.UnpaidSalary(w.Salary!.Terms.Single(), w.ElapsedSeconds) > 0);
        Assert.Null(HouseRestoration.SettlementForClaim(w, claim.Id));

    }
    [Fact]
    public void OwnerCanBringPaidCompanyOnlyUnderCurrentPickup()
    {
        var s=new WorldSession(NewGameWorld.Create(NewGameWorld.Scenario(HistoricalHouseholdCatalog.Id),[],Worker.Value,42,
            historicalCatalog:HistoricalHouseholdCatalog.Id,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,
            economyPolicy:MilitaryEconomy.Policy,worldArmyPolicy:NpcArmies.CompactCombatPolicy,battlefieldPolicy:BattlefieldRules.Policy));
        var pay=Agree(s);Advance(s,First(s));var prepared=Prepare(s,pay,5);
        BattlefieldRulesTests.Good(Act(s,SalaryAction.Receive,pay,prep:prepared));
        var salary=s.DiagnosticSnapshot().Salary!.Terms.Single();
        BattlefieldRulesTests.Good(s.ManageCourt(new(new("request:paid-company"),CourtAction.HireMarketSoldiers,
            Worker,salary.Owner,salary.Site,Amount:2,When:s.Seconds+86400),s.Revision));
        HouseholdMilitaryTests.Permit(s);
        var w=s.DiagnosticSnapshot();var wing=NewGameMilitary.UnitIdsFor(w)[0];var pickup=w.NewMilitary!.Requests.Last();
        var input=new CourtInput(new("request:own-company"),CourtAction.BringOwnCompany,Worker,salary.Owner,salary.Site,Reference:pickup.Input.Id,Carrier:wing);
        BattlefieldRulesTests.Good(s.ManageCourt(input,s.Revision));
        Assert.Equal(ResultCode.NotAvailable,s.ManageCourt(input with{Id=new("request:bring-again")},s.Revision).Code);
        BattlefieldRulesTests.Good(s.ManageMilitaryRice(new(new("request:own-rice"),NewGameMilitary.DepotFor(w),1),Worker,s.Revision));
        BattlefieldRulesTests.Military(s,NewMilitaryAction.Load,1000);BattlefieldRulesTests.Military(s,NewMilitaryAction.Deploy);
        w=s.DiagnosticSnapshot();
        var company=Assert.Single(PrivateRecruitment.CompanyReports(w,Worker,s.Seconds,s.Revision+1));
        var own=Assert.Single(WorldEngagement.Snapshot(w).Units,u=>u.Unit.Id==company.Unit);
        Assert.Equal(2,own.Unit.Soldiers);Assert.Equal(Worker,own.Unit.Commander);
        Assert.Equal(NewGameMilitary.Position(w,0,s.Seconds),own.Position);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.Equal(own,WorldEngagement.Snapshot(restored.DiagnosticSnapshot()).Units.Single(u=>u.Unit.Id==company.Unit));
    }

    [Fact]
    public void StandingCompanyPaysOnlyForSurvivorsAndDisbandsAfterRiceRunsOut()
    {
        var s=new WorldSession(NewGameWorld.Create(NewGameWorld.Scenario(HistoricalHouseholdCatalog.Id),[],Worker.Value,42,
            historicalCatalog:HistoricalHouseholdCatalog.Id,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,economyPolicy:MilitaryEconomy.Policy));
        var pay=Agree(s);var salary=s.DiagnosticSnapshot().Salary!.Terms.Single();
        foreach(var due in salary.DueAt.Take(2)){
            Advance(s,due);var receipt=Prepare(s,pay,20);
            BattlefieldRulesTests.Good(Act(s,SalaryAction.Receive,pay,prep:receipt));
        }
        var recruit=new CourtInput(new("request:standing-recruit"),CourtAction.HireMarketSoldiers,Worker,salary.Owner,salary.Site,
            Amount:3,When:s.Seconds+86400);
        BattlefieldRulesTests.Good(s.ManageCourt(recruit,s.Revision));
        BattlefieldRulesTests.Good(s.ManageCourt(new(new("request:standing-order"),CourtAction.KeepPrivateCompany,
            Worker,salary.Owner,salary.Site,Reference:recruit.Id),s.Revision));
        Advance(s,recruit.When-1);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(s.DiagnosticSnapshot())));
        foreach(var run in new[]{s,restored}){
            long coins=NewGameWork.Balance(run.DiagnosticSnapshot(),Worker);
            Advance(run,recruit.When);var paid=run.DiagnosticSnapshot();
            var row=Assert.Single(paid.Court!.Events,e=>e.Input.Action==CourtAction.PrivateCompanyPayment);
            Assert.Equal(2,row.Input.Amount);Assert.Equal(1,PrivateRecruitment.Soldiers(paid,Worker,salary.Site,run.Seconds,run.Revision+1));
            Assert.Equal(7,HouseSalary.PrivateRice(paid,Worker));Assert.Equal(coins-120,NewGameWork.Balance(paid,Worker));
            Advance(run,row.Input.When);var end=run.DiagnosticSnapshot();
            Assert.Equal(0,PrivateRecruitment.Soldiers(end,Worker,salary.Site,run.Seconds,run.Revision+1));
            Assert.Equal(7,HouseSalary.PrivateRice(end,Worker));
            Assert.Equal(2,end.Court!.Events.Count(e=>e.Input.Action==CourtAction.PrivateCompanyPayment));
        }
        var missing=s.DiagnosticSnapshot();
        missing.Court=missing.Court! with{Events=missing.Court.Events[..^1]};
        Assert.Throws<InvalidDataException>(()=>new WorldSession(missing));
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }

    static long First(WorldSession s)=>s.DiagnosticSnapshot().Salary!.Terms.Single().DueAt[0];
    static EntityId Prepare(WorldSession s,EntityId term,long amount=20){var r=Act(s,SalaryAction.Prepare,term,amount);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);return s.DiagnosticSnapshot().Salary!.Entries.Last().Input.Id;}
    [Theory][InlineData(false)][InlineData(true)]public void BothHeadAndEmployeeCanConfirmLocalTermsWithoutTransferringRoles(bool employee){var s=Start(employee);var before=s.DiagnosticSnapshot();var id=Agree(s);Assert.Equal(0,s.SalaryView()!.Contracts.Single().Owed);Assert.Equal(ContractJson.Write(before.ServiceBonds),ContractJson.Write(s.DiagnosticSnapshot().ServiceBonds));Assert.Equal(ContractJson.Write(before.Units),ContractJson.Write(s.DiagnosticSnapshot().Units));Assert.Equal(0,s.SalaryView()!.PrivateRice);Assert.Equal(1000,NewGameWork.Balance(s.DiagnosticSnapshot(),s.PlayerControl));}
    [Theory][InlineData(false,10)][InlineData(true,50)]public void CounterpartyCanRefuseTermsAndRefusalCannotBeConfirmed(bool employee,long amount){var s=Start(employee);Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.Propose,amount:amount).Code);var q=s.DiagnosticSnapshot().Salary!.Quotes.Single();Assert.False(q.Agreed);Assert.Equal(ResultCode.NotAvailable,Act(s,SalaryAction.Confirm,q.Id).Code);Assert.Empty(s.DiagnosticSnapshot().Salary!.Terms);}
    [Fact]public void MonthlyDebtAndProtectedPreparationAreNotPayment(){var s=Start();var id=Agree(s);Assert.Equal(ResultCode.NotAvailable,Act(s,SalaryAction.Prepare,id,20).Code);Advance(s,First(s));Assert.Equal(20,s.SalaryView()!.Contracts.Single().Owed);var prep=Prepare(s,id);Assert.Equal(3000,s.CommunityEconomyView()!.Accounts.HouseRice);var before=ContractJson.Write(s.DiagnosticSnapshot());Assert.Equal(ResultCode.NotAvailable,s.ManageCommunity(new(new("request:payroll-conflict"),new("site:kofu"),EconomyAction.RelieveVillage,3000),s.PlayerControl,s.Revision).Code);Assert.Equal(before,ContractJson.Write(s.DiagnosticSnapshot()));Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.Receive,id,prep:prep).Code);Assert.Equal(2980,s.CommunityEconomyView()!.Accounts.HouseRice);Assert.Equal(0,s.SalaryView()!.Contracts.Single().Owed);Assert.Equal(20,HouseSalary.PrivateRice(s.DiagnosticSnapshot(),Worker));}
    [Fact]public void PreparationExpiresAndPastDebtSurvivesEnd(){var s=Start();var id=Agree(s);Advance(s,First(s));var p=Prepare(s,id);Advance(s,s.Seconds+86401);Assert.Equal(0,s.SalaryView()!.Contracts.Single().Prepared);Assert.Equal(ResultCode.NotAvailable,Act(s,SalaryAction.Receive,id,prep:p).Code);Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.End,id).Code);Advance(s,s.Seconds+100*86400);Assert.Equal(20,s.SalaryView()!.Contracts.Single().Owed);var again=Prepare(s,id);Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.Receive,id,prep:again).Code);}
    [Fact]public void PaidRiceIsPrivatePropertyAndCanBeSoldOnceThroughExistingMerchant(){var s=Start(true);var id=Agree(s);Advance(s,First(s));var prep=Prepare(s,id);Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.Receive,id,prep:prep).Code);Assert.Equal(20,s.SalaryView()!.PrivateRice);var trade=new TradeInput(new("request:salary-sale"),TradeAction.OfferOwnRice,new("site:kofu"),Destination:new("site:kofu"),Amount:20);Assert.Equal(ResultCode.Accepted,s.TradeRice(trade,s.PlayerControl,s.Revision).Code);Assert.Equal(0,s.SalaryView()!.PrivateRice);Assert.Equal(ResultCode.IdPayloadConflict,s.TradeRice(trade with{Amount=19},s.PlayerControl,s.Revision).Code);foreach(var a in new[]{TradeAction.OfferSale,TradeAction.Deliver,TradeAction.Settle})Assert.Equal(ResultCode.Accepted,s.TradeRice(new(new("request:sale/"+s.Revision),a,new("site:kofu"),trade.Id),s.PlayerControl,s.Revision).Code);Assert.Equal(1060,NewGameWork.Balance(s.DiagnosticSnapshot(),s.PlayerControl));Assert.Equal(0,s.SalaryView()!.PrivateRice);Assert.Equal(2020,s.DiagnosticSnapshot().Trade!.Markets.Single(m=>m.Site.Value=="site:kofu").Rice);}
    [Fact]public void RepeatedLatePaymentChangesNextLocalCounterpartyDecision(){var s=Start();var id=Agree(s);Advance(s,s.DiagnosticSnapshot().Salary!.Terms.Single().DueAt[^1]+2*86400);Assert.Equal(60,s.SalaryView()!.Contracts.Single().Owed);var p=Prepare(s,id,60);Act(s,SalaryAction.Receive,id,prep:p);Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.Propose,amount:20).Code);var q=s.DiagnosticSnapshot().Salary!.Quotes.Last();Assert.False(q.Agreed);Assert.Contains("遅れ",q.Reply);Assert.Equal(0,s.SalaryView()!.Contracts.Single().Owed);}
    [Fact]public void RepeatedIdsAndStaleRevisionCannotDoublePay(){var s=Start();var id=Agree(s);Advance(s,First(s));var p=Prepare(s,id);var input=new SalaryInput(new("request:salary-receipt"),SalaryAction.Receive,Other(s),new("site:kofu"),id,Preparation:p);Assert.Equal(ResultCode.StateChanged,s.ManageSalary(input,s.PlayerControl,s.Revision+10).Code);Assert.Equal(ResultCode.Accepted,s.ManageSalary(input,s.PlayerControl,s.Revision).Code);Assert.Equal(ResultCode.AlreadyAccepted,s.ManageSalary(input,s.PlayerControl,s.Revision).Code);Assert.Equal(ResultCode.IdPayloadConflict,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),input.Id.Value,"castle"),s.Revision).Code);Assert.Equal(2980,s.CommunityEconomyView()!.Accounts.HouseRice);}
    [Fact]public void OldV3WorldStaysByteCompatibleAndRejectsInjectedSalary(){var s=RegionalTradeTests.Start();var bytes=ContractJson.Write(s.DiagnosticSnapshot());Assert.DoesNotContain("Salary",System.Text.Encoding.UTF8.GetString(bytes));Assert.Equal(bytes,ContractJson.Write(new WorldSession(ContractJson.Read<ContractWorld>(bytes)).DiagnosticSnapshot()));var w=s.DiagnosticSnapshot();w.Salary=Start().DiagnosticSnapshot().Salary;Assert.Throws<InvalidDataException>(()=>new WorldSession(w));}
    [Theory][InlineData(0,false)][InlineData(1,false)][InlineData(2,false)][InlineData(2,true)]public void ActualGenerationRecoveryOfDebtPreparedAndPaidPreservesFuture(int phase,bool employee){var a=Start(employee);var id=Agree(a);Advance(a,First(a));EntityId? prep=null;if(phase>0)prep=Prepare(a,id);if(phase==2)Act(a,SalaryAction.Receive,id,prep:prep);a.SetPaused(true);var w=a.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"house-salary-"+Guid.NewGuid().ToString("N")),w.Definitions);Assert.True(store.Save(a,"salary",w.WorldId).Published);var host=new WorldSessionHost(Start(employee));Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("salary"),host.Current,host.Current.Revision));var b=host.Current;Assert.Equal(ContractJson.Write(a.SalaryView()),ContractJson.Write(b.SalaryView()));var dir=Environment.GetEnvironmentVariable("HAKUTAI_WORLD_CLASH_QA_DIR");if(!string.IsNullOrEmpty(dir)){Directory.CreateDirectory(dir);var name=new[]{"salary-owed","salary-prepared","salary-paid"}[phase]+(employee?"-employee":"");File.WriteAllBytes(Path.Combine(dir,name+".before.json"),ContractJson.Write(a.DiagnosticSnapshot()));File.WriteAllBytes(Path.Combine(dir,name+".restored.json"),ContractJson.Write(b.DiagnosticSnapshot()));}foreach(var s in new[]{a,b}){if(phase==0)Prepare(s,id);if(phase<2){var receipt=s.DiagnosticSnapshot().Salary!.Entries.Last(e=>e.Input.Action==SalaryAction.Prepare).Input.Id;Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.Receive,id,prep:receipt).Code);}if(employee&&phase==2){var cargo=new TradeInput(new("request:recovered-salary-sale"),TradeAction.OfferOwnRice,new("site:kofu"),Destination:new("site:kofu"),Amount:20);Assert.Equal(ResultCode.Accepted,s.TradeRice(cargo,s.PlayerControl,s.Revision).Code);foreach(var action in new[]{TradeAction.OfferSale,TradeAction.Deliver,TradeAction.Settle})Assert.Equal(ResultCode.Accepted,s.TradeRice(new(new("request:recovered-sale/"+s.Revision),action,new("site:kofu"),cargo.Id),s.PlayerControl,s.Revision).Code);}Advance(s,s.Seconds+35*86400);}Assert.Equal(ContractJson.Write(a.DiagnosticSnapshot()),ContractJson.Write(b.DiagnosticSnapshot()));}

    [Fact]public void InsufficientHouseRiceKeepsDebtAndAllowsOnlyActualPartialPayment(){var s=Start();var id=Agree(s);s.ManageCommunity(new(new("request:empty-house"),new("site:kofu"),EconomyAction.RelieveVillage,2990),s.PlayerControl,s.Revision);Advance(s,First(s));Assert.Equal(ResultCode.NotAvailable,Act(s,SalaryAction.Prepare,id,20).Code);var p=Prepare(s,id,10);Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.Receive,id,prep:p).Code);Assert.Equal(10,s.SalaryView()!.Contracts.Single().Owed);Assert.Equal(0,s.CommunityEconomyView()!.Accounts.HouseRice);}
    [Theory][InlineData(false)][InlineData(true)]public void RemoteCurrentAttitudeAndHouseStockAreNotDisclosedToEmployee(bool employee){var s=Start(employee);Agree(s);if(employee)Assert.Null(s.CommunityEconomyView());s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:salary-away","castle"),s.Revision);Advance(s,s.Seconds+1);Advance(s,s.DiagnosticSnapshot().Branches!.Journeys.Last().Legs[^1].EndAt);Assert.Empty(s.SalaryView()!.People);Assert.Null(s.CommunityEconomyView());if(!employee)Assert.Null(s.SalaryView()!.Contracts.Single().LocalResponse);Assert.DoesNotContain("HouseRice",System.Text.Encoding.UTF8.GetString(ContractJson.Write(s.SalaryView())));}
    [Fact]public void UnknownOrOtherHouseAndWrongActorCannotCreateSalary(){var s=Start();var i=new SalaryInput(new("request:salary-other"),SalaryAction.Propose,new("person:nobunaga"),new("site:kofu"),Amount:20);Assert.Equal(ResultCode.NotAvailable,s.ManageSalary(i,s.PlayerControl,s.Revision).Code);Assert.Equal(ResultCode.NotAvailable,s.ManageSalary(i,Worker,s.Revision).Code);Assert.Empty(s.DiagnosticSnapshot().Salary!.Quotes);}
    [Fact]public void PreparationCanBeCancelledWithoutForgivingDebt(){var s=Start();var id=Agree(s);Advance(s,First(s));var p=Prepare(s,id);Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.CancelPrepared,id,prep:p).Code);Assert.Equal(0,s.SalaryView()!.Contracts.Single().Prepared);Assert.Equal(20,s.SalaryView()!.Contracts.Single().Owed);Assert.Equal(3000,s.CommunityEconomyView()!.Accounts.HouseRice);}
    [Fact]public void TamperedReceiptAndAmountsCannotBeLoaded(){var s=Start();var id=Agree(s);Advance(s,First(s));var p=Prepare(s,id);Act(s,SalaryAction.Receive,id,prep:p);var w=s.DiagnosticSnapshot();w.Salary=w.Salary! with{Terms=[w.Salary.Terms.Single() with{Paid=999}]};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));w=s.DiagnosticSnapshot();w.Salary=w.Salary! with{Entries=w.Salary.Entries.Select(e=>e.Input.Id==p?e with{Input=e.Input with{Amount=100}}:e).ToArray()};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));}

    [Fact]public void TwoQuotedOffersCannotBecomeOverlappingConfirmedPromises(){var s=Start();Act(s,SalaryAction.Propose,amount:20);var first=s.DiagnosticSnapshot().Salary!.Quotes.Last().Id;Act(s,SalaryAction.Propose,amount:30);var second=s.DiagnosticSnapshot().Salary!.Quotes.Last().Id;Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.Confirm,first).Code);var before=ContractJson.Write(s.DiagnosticSnapshot());Assert.Equal(ResultCode.NotAvailable,Act(s,SalaryAction.Confirm,second).Code);Assert.Equal(before,ContractJson.Write(s.DiagnosticSnapshot()));Assert.Single(s.SalaryView()!.Contracts);}
    [Fact]public void NormalEntryProfileBuildsSalaryCommunityAndMerchantTogether(){var store=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"salary-profile-"+Guid.NewGuid().ToString("N")));var p=store.New(NewGameWorld.Scenario(HistoricalHouseholdCatalog.Id),[],"person:shingen",42,NewGameWorld.CompactPersonalAuthority,HistoricalHouseholdCatalog.Id,NewGameNpcRoutine.Policy,LocalInspectionFindings.Policy,WorkFollowup.Policy,null,VoyageExpense.Standard,NpcArmies.CompactCombatPolicy,HouseSalary.Policy,PersonalCompanions.RetainerReleasePolicy,TruceNegotiations.Policy,PrivateInformation.Policy,PrivateStanddown.Policy);Assert.Equal(HouseSalary.Policy,store.Read(p.Instance).EconomyPolicy);var s=new WorldSession(NewCampaignStore.Build(p));Assert.NotNull(s.SalaryView());Assert.NotNull(s.CommunityEconomyView());Assert.NotNull(s.RegionalTradeView());Assert.Contains(Worker,s.SalaryView()!.People);}

    [Fact]public void ActualServiceReleaseKeepsOldHouseDebtAndAllowsLaterLocalSettlement(){var s=Start(true);var id=Agree(s);Advance(s,First(s));var service=NewGameService.View(s.DiagnosticSnapshot()).Cases.Single().Case;Assert.Equal(ResultCode.Accepted,s.DecideService(new(new("request:salary-service-release"),CareerAction.RequestRelease,service),s.PlayerControl,s.Revision).Code);Advance(s,s.Seconds+3);foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())s.Open(s.PlayerControl,m.Id,s.Revision);Assert.Null(NewGameService.View(s.DiagnosticSnapshot()).House);Assert.Equal(20,s.SalaryView()!.Contracts.Single().Owed);var p=Prepare(s,id);Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.Receive,id,prep:p).Code);Assert.Equal(20,s.SalaryView()!.PrivateRice);Assert.Equal(0,s.SalaryView()!.Contracts.Single().Owed);}
    [Fact]public void PlayerEmployeeCanChooseToRenewAfterLateHistoryWithoutForcedNpcEmotions(){var s=Start(true);var id=Agree(s);Advance(s,s.DiagnosticSnapshot().Salary!.Terms.Single().DueAt[^1]+2*86400);var p=Prepare(s,id,60);Act(s,SalaryAction.Receive,id,prep:p);Assert.Equal(ResultCode.Accepted,Act(s,SalaryAction.Propose,amount:20).Code);Assert.True(s.DiagnosticSnapshot().Salary!.Quotes.Last().Agreed);Assert.Contains("本人が判断",s.SalaryView()!.Contracts.Single().LocalResponse!);}
}
