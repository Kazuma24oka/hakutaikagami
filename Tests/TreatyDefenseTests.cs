using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
[Trait("Tier","Heavy")]public sealed class TreatyDefenseTests
{
    static readonly EntityId Own=new("org:defense-recipient"),Other=new("org:oda"),Player=new("created:defense-head"),Supplier=new("person:nobunaga"),Home=new("site:village"),Source=new("site:castle");
    // A real, supported new-game registered daimyo near Kiyosu, not relocated historical heads or forged stock.
    static WorldSession Start(bool far=false,bool campaign=false){var home=campaign?new EntityId("site:sakai"):far?new EntityId("site:kofu"):Home;var scenario=NewGameWorld.Scenario(HistoricalStartCatalog.Current);var officer=NewGameWorldTests.Officer(Player.Value,StartTemplate.Daimyo,home.Value);var raider=NewGameWorldTests.Officer("created:ally-raider",StartTemplate.Daimyo,"site:ishiyama");var ally=NewGameWorldTests.Officer("created:ally-head",StartTemplate.Daimyo,"site:kyoto");RegisteredOfficer[] people=campaign?[officer,raider,ally]:[officer];var request=new WorldSetupRequest(people,campaign?[new(officer.Id,Own.Value,[home.Value],false),new(raider.Id,"org:ally-raider",[raider.Site],false),new(ally.Id,"org:ally",[ally.Site],false)]:[new(officer.Id,Own.Value,[home.Value],false)]);var preview=NewGameSetup.Preview(scenario,request);Assert.True(preview.IsEligible(),string.Join(",",preview.Errors));var setup=NewGameSetup.Confirm(scenario,request,preview);return new(NewGameWorld.Create(setup,people,Player.Value,42,historicalCatalog:HistoricalStartCatalog.Current,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,worldArmyPolicy:NpcArmies.CompactCombatPolicy,economyPolicy:HouseSuccession.Policy,trucePolicy:TruceNegotiations.Policy,companionPolicy:campaign?PersonalCompanions.Policy:null,developmentPolicy:campaign?CharacterDevelopment.Policy:null,socialPolicy:campaign?CharacterRelations.Policy:null));}
    static void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
    static void At(WorldSession s,long t){while(s.Seconds<t){var old=s.Seconds;s.SetPaused(false);s.AdvanceTo(t,100000);Assert.True(s.Seconds>old);}}
    static long Arrival(WorldSession s,string id)=>s.DiagnosticSnapshot().Couriers!.Missions.Single(c=>s.DiagnosticSnapshot().Messaging.Attempts.Any(a=>a.Id==c.Attempt&&a.MessageId==id)).ReadyAt;
    static void Open(WorldSession s,string id)=>Good(s.Open(Player,new(id),s.Revision));
    [Fact]
    public void OnlyOpenedMemberVoteCountsAndCannotBeResentAsSecondVote()
    {
        var s=Start(campaign:true);
        EntityId ally=new("org:ally"),raider=new("org:ally-raider"),id=new("request:league");
        var charter=new CouncilCharter(id,CouncilRule.Unanimous);
        Good(s.NegotiateTreaty(new(id,TreatyAction.Propose,Own,ally,Days:30,NoticeDays:1){Charter=charter},Player,s.Revision));
        TreatyPaper Last()=>s.DiagnosticSnapshot().Treaties!.Papers.Last();
        At(s,Arrival(s,Last().Message)+1);
        var signature=Last();At(s,Arrival(s,signature.Message));Open(s,signature.Message);
        var w=s.DiagnosticSnapshot();w.CommitSequence++;
        FactionStances.Apply(w,new(new("decision:league-raid"),FactionStanceAction.DeclareHostile,raider,Own,w.ElapsedSeconds,w.ElapsedSeconds+7*86400),new("created:ally-raider"));
        s=new WorldSession(w);At(s,s.Seconds+2);
        var demand=s.DiagnosticSnapshot().Treaties!.Papers.Single(p=>p.Input.Submission!=null);
        At(s,Arrival(s,demand.Message));Open(s,demand.Message);
        var ballot=new CouncilBallot(charter,demand.Input.Id,raider,demand.Terms.Submission!,true);
        var ask=new TreatyInput(new("request:league-vote"),TreatyAction.Propose,Own,ally,Days:7,NoticeDays:1){Ballot=ballot};
        Good(s.NegotiateTreaty(ask,Player,s.Revision));var letter=Last();
        Assert.Equal(ResultCode.NotAvailable,s.NegotiateTreaty(ask with{Id=new("request:duplicate-vote")},Player,s.Revision).Code);
        KokujinCouncil.Count Count()=>KokujinCouncil.Tally(s.DiagnosticSnapshot(),Player,demand.Input.Id,s.Seconds,s.Revision)!;
        Assert.Null(Count().Verdict);
        At(s,Arrival(s,letter.Message)+1);
        var vote=s.DiagnosticSnapshot().Treaties!.Papers.Single(p=>p.Input.Source==ask.Id);
        At(s,Arrival(s,vote.Message));Assert.Equal(1,Count().Yes);
        s=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(s.DiagnosticSnapshot())));
        Open(s,vote.Message);Assert.Equal(vote.Input.Action,Count().Verdict);
        Assert.DoesNotContain(s.DiagnosticSnapshot().Treaties!.Formations,f=>f.Acceptance==vote.Input.Id);
        Good(s.NegotiateTreaty(new(new("request:league-verdict"),Count().Verdict!.Value,Own,raider,demand.Input.Id),Player,s.Revision));
    }
    [Fact]
    public void MemberSubmissionIsLearnedFromDepartureLetterOnlyAndEndsAfterNotice()
    {
        var s=Start(campaign:true);
        EntityId member=new("org:ally"),raider=new("org:ally-raider"),id=new("request:departure-charter");
        var charter=new CouncilCharter(id,CouncilRule.Majority);
        TreatyPaper Paper(EntityId source)=>s.DiagnosticSnapshot().Treaties!.Papers.Single(p=>p.Input.Source==source);
        Good(s.NegotiateTreaty(new(id,TreatyAction.Propose,Own,member,Days:30,NoticeDays:1){Charter=charter},Player,s.Revision));
        var invitation=s.DiagnosticSnapshot().Treaties!.Papers.Single();
        At(s,Arrival(s,invitation.Message)+1);
        var consent=Paper(id);At(s,Arrival(s,consent.Message));Open(s,consent.Message);
        Good(s.NegotiateTreaty(new(new("request:charter-confirm"),TreatyAction.Confirm,Own,member,consent.Input.Id),Player,s.Revision));
        At(s,Arrival(s,Paper(consent.Input.Id).Message)+1);
        var second=new EntityId("request:second-signatory");
        Good(s.NegotiateTreaty(new(second,TreatyAction.Propose,Own,Other,Days:20,NoticeDays:1){Charter=charter},Player,s.Revision));
        var secondInvite=s.DiagnosticSnapshot().Treaties!.Papers.Single(p=>p.Input.Id==second);
        At(s,Arrival(s,secondInvite.Message)+1);
        var secondReply=Paper(second);At(s,Arrival(s,secondReply.Message));Open(s,secondReply.Message);
        var w=s.DiagnosticSnapshot();w.CommitSequence++;
        FactionStances.Apply(w,new(new("decision:member-demand"),FactionStanceAction.DeclareHostile,raider,member,w.ElapsedSeconds,w.ElapsedSeconds+60),new("created:ally-raider"));
        s=new WorldSession(w);At(s,s.Seconds+2);
        var demand=s.DiagnosticSnapshot().Treaties!.Papers.Single(p=>p.Input.Submission!=null);
        At(s,Arrival(s,demand.Message)+1);
        var answer=Paper(demand.Input.Id);Assert.Equal(TreatyAction.Accept,answer.Input.Action);
        At(s,Arrival(s,answer.Message)+1);
        var check=s.DiagnosticSnapshot();Assert.True(check.Treaties!.Papers.Any(p=>p.Input.Source==answer.Input.Id),string.Join(",",check.Messaging.Attempts.Where(a=>a.MessageId==answer.Message).Select(a=>a.Status.ToString()))+" 成立数 "+check.Treaties.Formations.Length+" 開封 "+check.Treaties.Openings.Any(o=>o.Message==answer.Message)+" 時刻 "+s.Seconds+" 期限 "+answer.Terms.ReplyBy);
        var confirmed=Paper(answer.Input.Id);
        At(s,Arrival(s,confirmed.Message)+1);
        var departure=s.DiagnosticSnapshot().Treaties!.Operations.Single(p=>p.Input.Evidence==answer.Input.Id.Value);
        bool Knows()=>KokujinCouncil.KnowsSeparateDeparture(s.DiagnosticSnapshot(),Player,member,s.Seconds,s.Revision);
        bool Includes()=>KokujinCouncil.KnownMembers(s.DiagnosticSnapshot(),Player,charter,s.Seconds,s.Revision).Contains(member);
        Assert.False(Knows());Assert.True(Includes());
        At(s,Arrival(s,departure.Message));Assert.False(Knows());
        s=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(s.DiagnosticSnapshot())));
        Open(s,departure.Message);Assert.True(Knows());
        var forward=Assert.Single(TreatyNegotiations.DepartureCopies(s.DiagnosticSnapshot(),departure,Player,s.Seconds,s.Revision));
        Good(s.NegotiateTreaty(forward,Player,s.Revision));
        var copy=s.DiagnosticSnapshot().Treaties!.Papers.Single(p=>p.Input.Id==forward.Id);
        Assert.Equal(Other,copy.Input.Target);
        bool OtherKnows()=>KokujinCouncil.KnowsSeparateDeparture(s.DiagnosticSnapshot(),copy.Recipient,member,s.Seconds,s.Revision);
        Assert.False(OtherKnows());At(s,Arrival(s,copy.Message)+1);Assert.True(OtherKnows());
        At(s,Math.Max(s.Seconds,departure.Intent.EffectiveAt));Assert.False(Includes());
    }
    static readonly Lazy<ContractWorld> Prepared=new(()=>BuildReady());
    static ContractWorld BuildReady(int amount=100,bool far=false,bool wide=false,bool campaign=false){var target=campaign?new EntityId("org:ally"):wide?new EntityId("org:hojo"):Other;var s=Start(far,campaign);Good(s.NegotiateTreaty(new(new("request:defense-treaty"),TreatyAction.Propose,Own,target,Days:30,Clauses:TreatyClause.Defense,NoticeDays:1),Player,s.Revision));var p=s.DiagnosticSnapshot().Treaties!.Papers.Single();At(s,Arrival(s,p.Message)+1);var a=s.DiagnosticSnapshot().Treaties!.Papers.Last();At(s,Arrival(s,a.Message));Open(s,a.Message);Good(s.NegotiateTreaty(new(new("request:defense-confirm"),TreatyAction.Confirm,Own,target,a.Input.Id),Player,s.Revision));var c=s.DiagnosticSnapshot().Treaties!.Papers.Last();At(s,Arrival(s,c.Message)+1);Good(s.OperateTreaty(new(new("request:actual-defense"),TreatyOperation.RequestDefense,a.Input.Id,Own,target,ReplyBy:s.Seconds+7*86400,Amount:amount,Evidence:"100人の防衛援軍を希望する"),Player,s.Revision));var r=s.DiagnosticSnapshot().Treaties!.Operations.Last();At(s,Arrival(s,r.Message)+1);var receipt=s.DiagnosticSnapshot().Treaties!.Operations.Last();At(s,Arrival(s,receipt.Message));Open(s,receipt.Message);s.SetPaused(true);return s.DiagnosticSnapshot();}
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VisitingTreatyCommanderObservesThirdHouseAttackOnHost(bool present)
    {
        var s=new WorldSession(BuildReady(campaign:true));
        if(present) ArrivalOfArmy(s,Dispatched(s,Arrange(s,86400)));
        var w=s.DiagnosticSnapshot();
        EntityId enemy=new("org:ally-raider");
        w.CommitSequence++;
        FactionStances.Apply(w,new(new("decision:raid-host"),FactionStanceAction.DeclareHostile,enemy,Own,w.ElapsedSeconds,w.ElapsedSeconds+7*86400),new("created:ally-raider"));
        s=new WorldSession(w);
        At(s,s.Seconds+3);
        var demand=Assert.Single(s.DiagnosticSnapshot().Treaties!.Papers,p=>p.Author.Value=="created:ally-raider" && p.Terms.Submission!=null);
        Assert.Equal(Own,demand.Input.Target);
        var march=Assert.Single(s.DiagnosticSnapshot().Branches!.Journeys,j=>j.Actor.Value=="created:ally-raider");
        At(s,march.Legs[^1].EndAt+1);
        void Army(NewMilitaryAction action,long amount=0)
        {
            var world=s.DiagnosticSnapshot();
            var input=new NewMilitaryInput(new("request:ally-host/"+action),action,amount);
            if(action==NewMilitaryAction.Request)input=input with {AuthorityPolicy=NewGameMilitary.AuthorityContract,LedgerPolicy=NewGameMilitary.LedgerIdentityContract,FromLedger=NewGameMilitary.SourceForCurrentHouse(world),CycleContract=NewGameMilitary.CycleForCurrentHouse(world)};
            Good(s.DecideNewMilitary(input,Player,s.Revision));
        }
        void ReadOrders(){At(s,s.Seconds+2);foreach(var m in s.GetActorView(Player).Messages.Where(m=>m.KnownBody==null))Open(s,m.Id.Value);}
        Army(NewMilitaryAction.Request);ReadOrders();Army(NewMilitaryAction.Accept);ReadOrders();Army(NewMilitaryAction.Pickup);
        Good(s.ManageMilitaryRice(new(new("request:ally-host-rice"),NewGameMilitary.DepotFor(s.DiagnosticSnapshot()),2),Player,s.Revision));
        Army(NewMilitaryAction.Load,100000);Army(NewMilitaryAction.Deploy);
        s.SetPaused(true);
        var dir=Directory.CreateTempSubdirectory("hakutai-three-houses-");
        try
        {
            var store=new GenerationSaveStore(dir.FullName,w.Definitions);
            Assert.True(store.Save(s,"contact",w.WorldId).Published);
            var host=new WorldSessionHost(new WorldSession(w));
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("contact"),host.Current,host.Current.Revision));
            foreach(var run in new[]{s,host.Current})
            {
                At(run,run.Seconds+1);
                var after=run.DiagnosticSnapshot();
                Assert.Contains(after.WorldClashes!.Steps[^1].Result.Strikes,h=>after.Units.Any(u=>u.Id==h.Target&&u.Organization==Own));
                Assert.Equal(present,after.FactionStances!.Decisions.Any(d=>d.Authority.Issuer.Value=="created:ally-head"&&d.Input.Target==enemy));
            }
            Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        }
        finally {dir.Delete(true);}
    }

    static WorldSession Ready()=>new(ContractJson.Copy(Prepared.Value));

    static TreatyDefenseInput Input(WorldSession s,TreatyDefenseAction a,EntityId request,int duty=0)=>new(new("request:defense-test/"+s.Revision+"/"+a),a,request,duty);
    static TreatyDefenseEntry Send(WorldSession s,TreatyDefenseInput i){Good(s.ManageTreatyDefense(i,Player,s.Revision));return s.DiagnosticSnapshot().Treaties!.Defense!.Entries.Last();}
    static EntityId Arrange(WorldSession s,int duty=3600){var p=s.DiagnosticSnapshot().Treaties!.Operations.Single(p=>p.Kind==TreatyOperation.RequestDefense);return Send(s,Input(s,TreatyDefenseAction.Arrange,p.Input.Id,duty)).Root;}
    static TreatyDefenseTerms Dispatched(WorldSession s,EntityId root){var ask=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single(p=>p.Root==root&&p.Kind==TreatyDefensePaperKind.Ask);At(s,Arrival(s,ask.Id)+1);var state=s.DiagnosticSnapshot().Treaties!.Defense!;Assert.DoesNotContain(state.Entries,e=>e.Root==root&&e.Input.Action==TreatyDefenseAction.Refuse);return state.Entries.Single(e=>e.Root==root&&e.Input.Action==TreatyDefenseAction.Dispatch).Terms!;}
    static TaskState Trip(WorldSession s,TreatyDefenseTerms t)=>s.DiagnosticSnapshot().Tasks.Single(q=>q.RequestId==s.DiagnosticSnapshot().NpcArmies!.Marches.Last(m=>m.Muster==t.Muster).Input.RequestId);
    static void ArrivalOfArmy(WorldSession s,TreatyDefenseTerms t){At(s,s.Seconds+2);for(int n=0;n<8&&!s.DiagnosticSnapshot().Treaties!.Defense!.Entries.Any(e=>e.Root==t.Root&&e.Input.Action==TreatyDefenseAction.Arrive);n++){var task=Trip(s,t);var journey=s.DiagnosticSnapshot().Branches!.Journeys.Single(j=>j.Task==task.Id);At(s,Math.Max(s.Seconds+1,journey.Legs.Last().EndAt+1));}Assert.Contains(s.DiagnosticSnapshot().Treaties!.Defense!.Entries,e=>e.Root==t.Root&&e.Input.Action==TreatyDefenseAction.Arrive);}
    static void ReturnOfArmy(WorldSession s,TreatyDefenseTerms t){var arrived=s.DiagnosticSnapshot().Treaties!.Defense!.Entries.Single(e=>e.Root==t.Root&&e.Input.Action==TreatyDefenseAction.Arrive);At(s,arrived.At+t.DutySeconds+3);var task=Trip(s,t);var j=s.DiagnosticSnapshot().Branches!.Journeys.Single(j=>j.Task==task.Id);At(s,j.Legs.Last().EndAt+2);Assert.Contains(s.DiagnosticSnapshot().NpcArmies!.Returns,r=>r.Muster==t.Muster);}
    [Fact]public void ArrangementAndAcknowledgmentDoNotSpawnAnArmy(){var s=Ready();Assert.Empty(NpcArmies.Musters(s.DiagnosticSnapshot()));Arrange(s);Assert.Empty(NpcArmies.Musters(s.DiagnosticSnapshot()));Assert.Null(Assert.Single(TreatyDefense.View(s.DiagnosticSnapshot())).KnownTerms);}
    [Fact]public void DispatchUsesSameOwnedSoldiersAndActualHouseRice(){var s=Ready();long before=s.DiagnosticSnapshot().Communities!.Accounts.Single(a=>a.Site==Source).HouseRice;var root=Arrange(s);var t=Dispatched(s,root);var w=s.DiagnosticSnapshot();var muster=Assert.Single(NpcArmies.Musters(w));Assert.Equal(t.Muster,muster.Id);Assert.Equal(Other,muster.Owner);Assert.Null(muster.Objective);Assert.Equal(100,w.Units.Sum(u=>u.Soldiers));long kg=100L*t.CarryDays*86400/MilitaryEconomy.PersonSecondsPerKg;Assert.InRange(t.CarryDays,2,7);Assert.Equal(before-kg,w.Communities!.Accounts.Single(a=>a.Site==Source).HouseRice);Assert.Equal(100L*t.CarryDays*86400-NpcArmies.Consumed(w,muster),w.ResourceLots.Single(l=>l.Id==NewGameMilitary.LedgerIdentity(Other,Source).Carry).Quantity);Assert.Null(Assert.Single(TreatyDefense.View(w)).KnownTerms);new WorldSession(w);}
    [Fact]public void ArmyActuallyArrivesHoldsAndReturnsWithoutChangingOwnerOrDuplicatingUnits(){var s=Ready();var t=Dispatched(s,Arrange(s));var ids=s.DiagnosticSnapshot().Units.Select(u=>u.Id).ToArray();ArrivalOfArmy(s,t);Assert.Equal(Home,s.DiagnosticSnapshot().Locations.Single(l=>l.Actor==Supplier).Site);ReturnOfArmy(s,t);var w=s.DiagnosticSnapshot();Assert.Equal(Source,w.Locations.Single(l=>l.Actor==Supplier).Site);Assert.Equal(ids,w.Units.Select(u=>u.Id));Assert.Equal(100,w.Units.Sum(u=>u.Soldiers));Assert.All(w.Units,u=>Assert.Equal(Other,u.Organization));Assert.Single(NpcArmies.Musters(w));Assert.Empty(FactionStances.Truth(w,s.Seconds));new WorldSession(w);At(s,s.Seconds+100);Assert.Single(NpcArmies.Musters(s.DiagnosticSnapshot()));}
    [Fact]public void UnsupportedRequestedQuantityProducesPhysicalRefusalWithoutFundingOrArmy(){var s=new WorldSession(BuildReady(20));var root=Arrange(s);var ask=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single();At(s,Arrival(s,ask.Id)+1);Assert.Empty(NpcArmies.Musters(s.DiagnosticSnapshot()));var reply=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single(p=>p.Kind==TreatyDefensePaperKind.Refused);Assert.DoesNotContain(TreatyDefense.View(s.DiagnosticSnapshot()),c=>c.Status.Contains("辞退"));At(s,Arrival(s,reply.Id));Open(s,reply.Id);Assert.Contains(TreatyDefense.View(s.DiagnosticSnapshot()),c=>c.Status.Contains("辞退"));Assert.Null(reply.Terms);}
    [Fact]public void OpeningADispatchReportGivesDatedTermsNotLiveRemoteLocation(){var s=Ready();var root=Arrange(s);var t=Dispatched(s,root);var report=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single(p=>p.Kind==TreatyDefensePaperKind.Dispatched);At(s,Arrival(s,report.Id));Open(s,report.Id);var v=Assert.Single(TreatyDefense.View(s.DiagnosticSnapshot()));Assert.Equal(t,v.KnownTerms);Assert.Equal(report.ObservedAt,v.ObservedAt);Assert.Contains("現在地は未確認",v.Status);}

    static void OpenDispatch(WorldSession s){var p=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single(p=>p.Kind==TreatyDefensePaperKind.Dispatched);At(s,Arrival(s,p.Id));Open(s,p.Id);}
    static WorldSession BlockAhead(WorldSession s,TreatyDefenseTerms t){var w=s.DiagnosticSnapshot();var task=Trip(s,t);var edge=w.Branches!.Journeys.Single(j=>j.Task==task.Id).Legs.First(l=>l.StartAt>w.ElapsedSeconds).Edge;w.Couriers=w.Couriers! with{Conditions=[..w.Couriers.Conditions,new(w.ElapsedSeconds+1,edge,true)]};return new(w);}
    static WorldSession Phase(int phase){var s=phase==11?new WorldSession(BuildReady(20)):phase==13?new WorldSession(BuildReady(wide:true)):Ready();if(phase==0)return s;var root=Arrange(s);if(phase==1)return s;
        if(phase==10){var ask=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single();s.DiagnosticTransport((m,t)=>HakutaiKagami.Core.Messaging.MessageFlow.Fail(m,ask.Id));At(s,Arrival(s,ask.Id)+1);var f=s.DiagnosticSnapshot().Messaging.Messages.Single(m=>m.Kind=="delivery-failure"&&s.DiagnosticSnapshot().Messaging.Attempts.Any(a=>a.Id==m.Body&&a.MessageId==ask.Id));At(s,Arrival(s,f.Id));var retry=Input(s,TreatyDefenseAction.Resend,root) with{Original=ask.Id};Assert.Equal(ResultCode.NotAvailable,s.ManageTreatyDefense(retry,Player,s.Revision).Code);Open(s,f.Id);Send(s,retry);var copy=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Last();Assert.Equal(ask.Deadline,copy.Deadline);Assert.Equal(ask.ObservedAt,copy.ObservedAt);At(s,Arrival(s,copy.Id)+1);Assert.Single(NpcArmies.Musters(s.DiagnosticSnapshot()));return s;}
        if(phase==11){var ask=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single();At(s,Arrival(s,ask.Id)+1);var p=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single(p=>p.Kind==TreatyDefensePaperKind.Refused);At(s,Arrival(s,p.Id));Open(s,p.Id);return s;}
        if(phase==13){var ask=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single();At(s,Arrival(s,ask.Id)+1);var t=s.DiagnosticSnapshot().Treaties!.Defense!.Entries.Single(e=>e.Input.Action==TreatyDefenseAction.Dispatch).Terms!;Assert.InRange(t.CarryDays,3,7);At(s,s.Seconds+10);return s;}
        var terms=Dispatched(s,root);if(phase==2)return s;if(phase==6){At(s,s.Seconds+10);return s;}if(phase==12){s=BlockAhead(s,terms);var blocked=s.DiagnosticSnapshot().Couriers!.Conditions.Last();var entry=s.DiagnosticSnapshot().Branches!.Journeys.Single(j=>j.Task==Trip(s,terms).Id).Legs.First(l=>l.Edge==blocked.Edge);At(s,entry.StartAt+2);Assert.NotEmpty(BranchRoutes.KnownBlocked(s.DiagnosticSnapshot(),Supplier));Assert.Empty(BranchRoutes.KnownBlocked(s.DiagnosticSnapshot(),Player));return s;}
        if(phase is 8 or 9)OpenDispatch(s);ArrivalOfArmy(s,terms);if(phase==3)return s;
        if(phase is 8 or 9){var e=Send(s,Input(s,TreatyDefenseAction.Cancel,root));var p=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single(p=>p.Id==e.Paper);Assert.Equal(Home,p.Address);Assert.DoesNotContain(s.DiagnosticSnapshot().Treaties!.Defense!.Entries,e=>e.Input.Action==TreatyDefenseAction.Recall);if(phase==9){At(s,Arrival(s,p.Id)+2);Assert.Contains(s.DiagnosticSnapshot().Treaties!.Defense!.Entries,e=>e.Input.Action==TreatyDefenseAction.Recall);}return s;}
        var arrived=s.DiagnosticSnapshot().Treaties!.Defense!.Entries.Single(e=>e.Input.Action==TreatyDefenseAction.Arrive);At(s,arrived.At+terms.DutySeconds+3);if(phase==7){At(s,s.Seconds+10);return s;}if(phase==5){var task=Trip(s,terms);At(s,s.DiagnosticSnapshot().Branches!.Journeys.Single(j=>j.Task==task.Id).Legs.Last().EndAt+2);}return s;
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)][InlineData(8)][InlineData(9)][InlineData(10)][InlineData(11)][InlineData(12)][InlineData(13)]public void GenerationRecoveryKeepsArmyFoodRouteAndFuture(int phase){var s=Phase(phase);s.SetPaused(true);var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"defense-"+Guid.NewGuid().ToString("N")),w.Definitions);Assert.True(store.Save(s,"defense",w.WorldId).Published);var host=new WorldSessionHost(Start());Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("defense"),host.Current,host.Current.Revision));Assert.Equal(ContractJson.Write(w),ContractJson.Write(host.Current.DiagnosticSnapshot()));var dir=Environment.GetEnvironmentVariable("HAKUTAI_WORLD_CLASH_QA_DIR");if(!string.IsNullOrEmpty(dir)){File.WriteAllBytes(Path.Combine(dir,"treaty-defense-"+phase+".before.json"),ContractJson.Write(w));File.WriteAllBytes(Path.Combine(dir,"treaty-defense-"+phase+".restored.json"),ContractJson.Write(host.Current.DiagnosticSnapshot()));}foreach(var x in new[]{s,host.Current})At(x,x.Seconds+10);Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));}
    [Fact]public void TooLongRealRoundTripIsRefusedInsteadOfInventingEndurance(){var s=new WorldSession(BuildReady(100,true));Arrange(s);var ask=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single();At(s,Arrival(s,ask.Id)+1);Assert.Empty(NpcArmies.Musters(s.DiagnosticSnapshot()));var p=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single(p=>p.Kind==TreatyDefensePaperKind.Refused);Assert.Contains("往復",p.Reason);new WorldSession(s.DiagnosticSnapshot());}
    [Fact]public void CancelBeforeOpeningDispatchUsesOnlyOriginalKnownSourceAddress(){var s=Ready();var root=Arrange(s);Dispatched(s,root);Assert.Null(Assert.Single(TreatyDefense.View(s.DiagnosticSnapshot())).KnownTerms);var e=Send(s,Input(s,TreatyDefenseAction.Cancel,root));var paper=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single(p=>p.Id==e.Paper);Assert.Equal(Source,paper.Address);Assert.Equal(Supplier,paper.Recipient);Assert.Null(paper.Terms);new WorldSession(s.DiagnosticSnapshot());}

    [Theory][InlineData("Policy")][InlineData("Entries")][InlineData("Papers")][InlineData("Openings")][InlineData("Failures")]public void DefenseExtensionFieldsCannotBeSilentlyDefaulted(string field){var s=Ready();Arrange(s);var n=System.Text.Json.Nodes.JsonNode.Parse(ContractJson.Write(s.DiagnosticSnapshot()))!;n["Treaties"]!["Defense"]!.AsObject().Remove(field);Assert.Throws<InvalidDataException>(()=>ContractJson.Read<ContractWorld>(System.Text.Encoding.UTF8.GetBytes(n.ToJsonString())));}
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]public void ForgedArmyBudgetOwnerDestinationOrExpiryCannotLoad(int variant){var s=Ready();var root=Arrange(s);Dispatched(s,root);var w=s.DiagnosticSnapshot();int n=Array.FindIndex(w.Treaties!.Defense!.Entries,e=>e.Terms!=null);var e=w.Treaties.Defense.Entries[n];var t=e.Terms!;w.Treaties.Defense.Entries[n]=e with{Terms=variant switch{0=>t with{CarryDays=8},1=>t with{Soldiers=99},2=>t with{Destination=Source},3=>t with{Owner=Own},4=>t with{Muster=new("decision:fake-army")},_=>t with{Until=t.Until+86400}}};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));}
    [Fact]public void RepeatedArrangeInputCannotMusterAgain(){var s=Ready();var p=s.DiagnosticSnapshot().Treaties!.Operations.Single(p=>p.Kind==TreatyOperation.RequestDefense);var i=Input(s,TreatyDefenseAction.Arrange,p.Input.Id,3600);Send(s,i);Dispatched(s,i.Id);var w=s.DiagnosticSnapshot();Assert.Equal(ResultCode.AlreadyAccepted,s.ManageTreatyDefense(i,Player,0).Code);Assert.Equal(ResultCode.IdPayloadConflict,s.ManageTreatyDefense(i with{DutySeconds=7200},Player,s.Revision).Code);Assert.Equal(ContractJson.Write(w),ContractJson.Write(s.DiagnosticSnapshot()));Assert.Single(NpcArmies.Musters(w));}
    [Fact]public void UnknownClosureIsOnlyLearnedAtPhysicalEntryAndTheSameArmyDetours(){var s=Ready();var t=Dispatched(s,Arrange(s));var before=s.DiagnosticSnapshot();var army=Assert.Single(NpcArmies.Armies(before));s=BlockAhead(s,t);Assert.Empty(BranchRoutes.KnownBlocked(s.DiagnosticSnapshot(),Supplier));ArrivalOfArmy(s,t);var w=s.DiagnosticSnapshot();Assert.NotEmpty(BranchRoutes.KnownBlocked(w,Supplier));Assert.Empty(BranchRoutes.KnownBlocked(w,Player));Assert.Single(NpcArmies.Musters(w));Assert.Equal(army,Assert.Single(NpcArmies.Armies(w)));Assert.Equal(before.MilitaryEconomy!.Entries.Count(e=>!e.Input.Returning),w.MilitaryEconomy!.Entries.Count(e=>!e.Input.Returning));ReturnOfArmy(s,t);new WorldSession(s.DiagnosticSnapshot());}
    [Fact]public void LongerSortieFundsSignedDaysFromTheActualWarehouseAndReturnsUnusedRice(){var s=new WorldSession(BuildReady(wide:true));var site=new EntityId("site:odawara");long before=s.DiagnosticSnapshot().Communities!.Accounts.Single(a=>a.Site==site).HouseRice;Arrange(s);var ask=s.DiagnosticSnapshot().Treaties!.Defense!.Papers.Single();At(s,Arrival(s,ask.Id)+1);var t=s.DiagnosticSnapshot().Treaties!.Defense!.Entries.Single(e=>e.Input.Action==TreatyDefenseAction.Dispatch).Terms!;Assert.InRange(t.CarryDays,3,7);var w=s.DiagnosticSnapshot();Assert.Equal(before-60L*t.CarryDays,w.Communities!.Accounts.Single(a=>a.Site==site).HouseRice);ArrivalOfArmy(s,t);ReturnOfArmy(s,t);w=s.DiagnosticSnapshot();var m=Assert.Single(NpcArmies.Musters(w));long used=NpcArmies.Consumed(w,m);Assert.Equal(before-(used+MilitaryEconomy.PersonSecondsPerKg-1)/MilitaryEconomy.PersonSecondsPerKg,w.Communities!.Accounts.Single(a=>a.Site==site).HouseRice);new WorldSession(w);}
}
