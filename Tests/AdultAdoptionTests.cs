using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class AdultAdoptionTests
{
    static readonly EntityId Player=new("created:test"),Other=new("created:other");
    static WorldSession Start(ulong seed=42,bool on=true,bool lord=false,bool minor=false,string site="site:castle",bool remoteHouse=false,bool distantSigner=false,bool law=false){
        var a=NewGameWorldTests.Officer();var b=NewGameWorldTests.Officer(Other.Value,remoteHouse?StartTemplate.Retainer:StartTemplate.Ronin,site:site,lord:remoteHouse?"person:shingen":null) with{BirthYear=minor?1550:1520};
        var setup=NewGameWorldTests.Setup(distantSigner||law?[a,b,NewGameWorldTests.Officer("created:signer",StartTemplate.Retainer,law?"site:castle":"site:kyoto","person:nobunaga"),NewGameWorldTests.Officer("created:younger") with{BirthYear=1550,BirthParents=["person:nobunaga"]}]:[a,b]);return new(NewGameWorld.Create(setup.World,setup.Registered,lord?"person:nobunaga":Player.Value,seed,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,socialPolicy:CharacterRelations.Policy,adoptionPolicy:on?AdultAdoption.Policy:null,economyPolicy:law?HouseSuccession.Policy:null));
    }
    [Fact]
    public void HouseLawRanksKnownChildrenWithoutOverridingNomination(){
        var s=Start(Yes.Value,lord:true,law:true);
        var adoption=Propose(s);At(s,1);
        var house=Assert.Single(s.DiagnosticSnapshot().Adoption!.Entries.First().Terms!.Houses).Owner;
        void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        Good(Act(s,AdoptionAction.ApproveHouse,adoption,house));Good(Act(s,AdoptionAction.Implement,adoption));
        Assert.Equal(Other,HeirDesignation.KnownChildren(s.DiagnosticSnapshot(),house)[0].Person);
        EntityId member=new("created:signer"),site=new("site:castle");
        var proposal=new CourtInput(new("request:succession-consult"),CourtAction.ConsultReform,member,house,site){Reform=ReformKind.SuccessionOrder};
        Good(s.ManageCourt(proposal,s.Revision));At(s,241);
        Good(s.Open(s.PlayerControl,new(HouseReforms.Letter(proposal,HouseReforms.Answer)),s.Revision));
        Good(s.ManageCourt(new(new("request:succession-law"),CourtAction.EnactSuccessionLaw,member,house,site,Reference:proposal.Id),s.Revision));
        s=new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot()));
        var candidates=HeirDesignation.KnownChildren(s.DiagnosticSnapshot(),house);
        Assert.Equal(new EntityId("created:younger"),candidates[0].Person);
        Assert.Equal(Other,candidates[1].Person);
        Good(s.DesignateHeir(house,Other,s.Revision));
        var paper=Assert.Single(s.DiagnosticSnapshot().HeirNominations!);
        Assert.Equal(Other,paper.Heir);
        Good(s.RequestHeirPledge(paper,member,s.Revision));
        var sent=s.DiagnosticSnapshot();
        Assert.Contains("家法の第一候補と異なる",Assert.Single(sent.Messaging.Messages,m=>m.Kind==HeirPledges.RequestKind).Body);
        Assert.DoesNotContain(sent.Messaging.Messages,m=>m.Kind==HeirPledges.ReplyKind);
        s=new WorldSession(ContractJson.Copy(sent));
        At(s,s.Seconds+240);
        var reply=Assert.Single(s.DiagnosticSnapshot().Messaging.Messages,m=>m.Kind==HeirPledges.ReplyKind);
        Assert.Contains("第一候補を外す",reply.Body);
        var score=CharacterRelations.DiagnosticScores(sent,member,Other);
        var support=sent.Characters.Single(c=>c.Id==member).WillObey &&
            CharacterRelations.DiagnosticTrait(sent,member,SocialTrait.Duty)+CharacterRelations.DiagnosticTrait(sent,member,SocialTrait.Affection)+score.Trust-score.Hostility>=100;
        Assert.Equal(support,reply.Body.Contains("支持すると誓います"));
    }
    static readonly Lazy<ulong> Yes=new(()=>Find(true)),No=new(()=>Find(false));
    static ulong Find(bool yes){for(ulong n=1;n<100;n++){var w=Start(n).DiagnosticSnapshot();var score=CharacterRelations.DiagnosticScores(w,Other,Player);if((CharacterRelations.DiagnosticTrait(w,Other,SocialTrait.Duty)+CharacterRelations.DiagnosticTrait(w,Other,SocialTrait.Affection)+score.Trust-score.Hostility>=80)==yes)return n;}throw new Exception("No branch seed");}
    static CommandResult Act(WorldSession s,AdoptionAction action,EntityId? original=null,EntityId? house=null,bool parent=true,string? id=null)=>s.DecideAdoption(new(new(id??"request:adoption/"+s.Revision),action,action==AdoptionAction.Propose?Other:null,parent,original,house),s.PlayerControl,s.Revision);
    static EntityId Propose(WorldSession s,bool parent=true){var r=Act(s,AdoptionAction.Propose,parent:parent);Assert.Equal(ResultCode.Accepted,r.Code);return s.DiagnosticSnapshot().Adoption!.Entries.Last().Input.Id;}
    static void At(WorldSession s,long at)=>NewGameWorldTests.At(s,at);
    static WorldSession Agreed(bool lord=false){var s=Start(Yes.Value,lord:lord);Propose(s);At(s,1);Assert.Contains(s.DiagnosticSnapshot().Adoption!.Entries,e=>e.Input.Action==AdoptionAction.PersonalYes);return s;}
    [Fact]public void OldPolicyStaysAbsent(){var s=Start(on:false);Assert.Null(s.DiagnosticSnapshot().Adoption);Assert.DoesNotContain("AdoptionPolicy",System.Text.Encoding.UTF8.GetString(ContractJson.Write(s.DiagnosticSnapshot())));Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.Propose).Code);}
    [Fact]public void ProposalIsNotFamilyAndDoesNotImpersonateTheOtherAdult(){var s=Start(Yes.Value);Propose(s);Assert.Single(s.DiagnosticSnapshot().Adoption!.Entries);Assert.False(AdultAdoption.View(s.DiagnosticSnapshot()).Single().CanImplement);Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.PersonalYes).Code);}
    [Fact]public void PersonalAgreementRequiresSeparateImplementationWithoutChangingRights(){var s=Agreed();var before=s.DiagnosticSnapshot();var card=Assert.Single(AdultAdoption.View(before));Assert.True(card.CanImplement);Assert.Equal(ResultCode.Accepted,Act(s,AdoptionAction.Implement,card.Id).Code);var after=s.DiagnosticSnapshot();Assert.Single(after.Adoption!.Entries,e=>e.Input.Action==AdoptionAction.Implement);Assert.Equal(ContractJson.Write(before.Memberships),ContractJson.Write(after.Memberships));Assert.Equal(ContractJson.Write(before.Offices),ContractJson.Write(after.Offices));Assert.Equal(ContractJson.Write(before.LandRights),ContractJson.Write(after.LandRights));Assert.Equal(NewGameWork.Balance(before,Player),NewGameWork.Balance(after,Player));Assert.False(AdultAdoption.View(after).Single().CanImplement);}
    [Fact]public void PlayerHouseConsentIsNotImpliedByPersonalProposal(){var s=Agreed(true);var w=s.DiagnosticSnapshot();var t=w.Adoption!.Entries.First().Terms!;Assert.Single(t.Houses);Assert.DoesNotContain(w.Adoption.Entries,e=>e.Input.Action==AdoptionAction.ApproveHouse);Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.Implement,t.Id).Code);Assert.Equal(ResultCode.Accepted,Act(s,AdoptionAction.ApproveHouse,t.Id,t.Houses[0].Owner).Code);Assert.Equal(ResultCode.Accepted,Act(s,AdoptionAction.Implement,t.Id).Code);}
    [Fact]
    public void AdoptedHeirIsVisibleForOwnHouseAfterImplementationAndLoad()
    {
        var session=Agreed(true);
        var w=session.DiagnosticSnapshot();
        var terms=w.Adoption!.Entries.First().Terms!;
        var house=Assert.Single(terms.Houses).Owner;
        Assert.Empty(HeirDesignation.KnownChildren(w,house));
        Assert.Equal(ResultCode.Accepted,Act(session,AdoptionAction.ApproveHouse,terms.Id,house).Code);
        Assert.Empty(HeirDesignation.KnownChildren(session.DiagnosticSnapshot(),house));
        Assert.Equal(ResultCode.Accepted,Act(session,AdoptionAction.Implement,terms.Id).Code);
        w=session.DiagnosticSnapshot();
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w))).DiagnosticSnapshot();
        foreach(var state in new[]{w,restored})
        {
            var heir=Assert.Single(HeirDesignation.KnownChildren(state,house));
            Assert.True(heir.Adopted);
            Assert.Equal(Other,heir.Person);
            Assert.Empty(HeirDesignation.KnownChildren(state,new("org:takeda")));
            var running=new WorldSession(state);
            var revision=running.Revision;
            Assert.Equal(ResultCode.Accepted,running.DesignateHeir(house,Other,revision).Code);
            Assert.Equal(ResultCode.AlreadyAccepted,running.DesignateHeir(house,Other,revision).Code);
            var after=running.DiagnosticSnapshot();
            Assert.Equal(Other,Assert.Single(after.HeirNominations!).Heir);
            Assert.Equal(ContractJson.Write(state.Offices),ContractJson.Write(after.Offices));
            Assert.Equal(ContractJson.Write(after),ContractJson.Write(new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(after))).DiagnosticSnapshot()));
        }
    }
    [Fact]
    public void DistantSignerRepliesToTheOriginalNominationAfterPhysicalDelivery()
    {
        var s=Start(Yes.Value,lord:true,distantSigner:true);
        var root=Propose(s);At(s,1);
        var house=s.DiagnosticSnapshot().Adoption!.Entries.First().Terms!.Houses.Single().Owner;
        Assert.Equal(ResultCode.Accepted,Act(s,AdoptionAction.ApproveHouse,root,house).Code);
        Assert.Equal(ResultCode.Accepted,Act(s,AdoptionAction.Implement,root).Code);
        Assert.Equal(ResultCode.Accepted,s.DesignateHeir(house,Other,s.Revision).Code);
        var paper=s.DiagnosticSnapshot().HeirNominations!.Single();
        EntityId signer=new("created:signer");
        Assert.Equal(ResultCode.Accepted,s.RequestHeirPledge(paper,signer,s.Revision).Code);
        s.SetPaused(true);
        var sent=s.DiagnosticSnapshot();
        Assert.DoesNotContain(sent.Messaging.Messages,m=>m.Kind==HeirPledges.ReplyKind);
        var directory=Directory.CreateTempSubdirectory("hakutai-heir-");
        WorldSession restored;
        try
        {
            var saves=new GenerationSaveStore(directory.FullName,sent.Definitions);
            Assert.True(saves.Save(s,"pledge",sent.WorldId).Published);
            var host=new WorldSessionHost(new WorldSession(sent));
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(saves.Prepare("pledge"),host.Current,host.Current.Revision));
            restored=host.Current;
        }
        finally{directory.Delete(true);}
        foreach(var branch in new[]{s,restored})
        {
            Assert.Equal(ResultCode.Accepted,branch.DesignateHeir(house,new("created:younger"),branch.Revision).Code);
            Assert.Equal(ResultCode.NotAvailable,branch.RequestHeirPledge(paper,signer,branch.Revision).Code);
            var outbound=sent.Messaging.Messages.Single(m=>m.Kind==HeirPledges.RequestKind);
            At(branch,sent.Messaging.Attempts.Single(a=>a.MessageId==outbound.Id).DueAt);
            var interim=branch.DiagnosticSnapshot();
            var reply=interim.Messaging.Messages.Single(m=>m.Kind==HeirPledges.ReplyKind);
            Assert.Equal(HeirPledges.Version(paper),reply.CorrelationId);
            Assert.NotEqual(HeirPledges.Version(interim.HeirNominations!.Last()),reply.CorrelationId);
            Assert.Equal(signer.Value,reply.SenderId);
            Assert.NotEqual(ResultCode.Accepted,branch.Open(branch.PlayerControl,new(reply.Id),branch.Revision).Code);
            At(branch,interim.Messaging.Attempts.Single(a=>a.MessageId==reply.Id).DueAt);
            Assert.Equal(ResultCode.Accepted,branch.Open(branch.PlayerControl,new(reply.Id),branch.Revision).Code);
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
        var forged=s.DiagnosticSnapshot();
        forged.Messaging.Messages=forged.Messaging.Messages.Select(m=>m.Kind==HeirPledges.ReplyKind
            ?m with{CorrelationId=HeirPledges.Version(forged.HeirNominations!.Last())}:m).ToArray();
        Assert.Throws<InvalidDataException>(()=>new WorldSession(forged));
    }
    [Fact]
    public void AdultAdoptedFromAnotherHouseRemainsInThatHouseAfterDesignation()
    {
        var parent=NewGameWorldTests.Officer(role:StartTemplate.Daimyo);
        var child=NewGameWorldTests.Officer(Other.Value,StartTemplate.Retainer,"site:kofu","person:shingen");
        EntityId house=new("org:created-test");
        var start=NewGameWorldTests.Setup([parent,child],[new(parent.Id,house.Value,[parent.Site],false)]);
        var w=NewGameWorld.Create(start.World,start.Registered,parent.Id,Yes.Value,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,
            companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,
            socialPolicy:CharacterRelations.Policy,adoptionPolicy:AdultAdoption.Policy,comingOfAge:15);
        var s=new WorldSession(w);
        Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(w,"request:visit-adoptive-family","kofu"),s.Revision).Code);
        At(s,2);At(s,s.GetBranchView(s.PlayerControl).ArrivalAt!.Value+2);
        var proposal=Propose(s);At(s,s.Seconds+1);
        Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.Implement,proposal).Code);
        Assert.Equal(ResultCode.Accepted,Act(s,AdoptionAction.ApproveHouse,proposal,house).Code);
        var before=s.DiagnosticSnapshot();
        Assert.Equal(2,before.Adoption!.Entries.Count(e=>e.Input.Action==AdoptionAction.ApproveHouse));
        Assert.Equal(ResultCode.Accepted,Act(s,AdoptionAction.Implement,proposal).Code);
        Assert.Equal(ResultCode.Accepted,s.DesignateHeir(house,Other,s.Revision).Code);
        s.SetPaused(true);
        var after=s.DiagnosticSnapshot();
        foreach(var pair in new[]{(ContractJson.Write(before.Memberships),ContractJson.Write(after.Memberships)),
            (ContractJson.Write(before.LandRights),ContractJson.Write(after.LandRights)),
            (ContractJson.Write(before.Units),ContractJson.Write(after.Units))})Assert.Equal(pair.Item1,pair.Item2);
        var directory=Directory.CreateTempSubdirectory("hakutai-cross-house-");
        try
        {
            var store=new GenerationSaveStore(directory.FullName,w.Definitions);
            Assert.True(store.Save(s,"adoption",after.WorldId).Published);
            var host=new WorldSessionHost(new WorldSession(w));
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("adoption"),host.Current,host.Current.Revision));
            Assert.Equal(ContractJson.Write(after),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        }
        finally{directory.Delete(true);}
    }
    [Fact]public void HouseApprovalCannotPrecedePersonalAnswer(){var s=Start(Yes.Value,lord:true);var root=Propose(s);var t=s.DiagnosticSnapshot().Adoption!.Entries.First().Terms!;Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.ApproveHouse,root,t.Houses[0].Owner).Code);}
    [Fact]public void RefusalIsStableAndCannotBeImplemented(){var s=Start(No.Value);var root=Propose(s);At(s,1);Assert.Contains(s.DiagnosticSnapshot().Adoption!.Entries,e=>e.Input.Action==AdoptionAction.PersonalNo);Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.Implement,root).Code);At(s,5);Assert.Single(s.DiagnosticSnapshot().Adoption!.Entries,e=>e.Input.Action==AdoptionAction.PersonalNo);}
    [Fact]public void WithdrawalBeforeReplyPreventsNpcAnswer(){var s=Start(Yes.Value);var root=Propose(s);Assert.Equal(ResultCode.Accepted,Act(s,AdoptionAction.Withdraw,root).Code);At(s,2);Assert.Equal(2,s.DiagnosticSnapshot().Adoption!.Entries.Length);Assert.False(AdultAdoption.View(s.DiagnosticSnapshot()).Single().CanWithdraw);}
    [Fact]public void ExpiryBlocksImplementation(){var s=Agreed();var root=s.DiagnosticSnapshot().Adoption!.Entries.First().Input.Id;At(s,7*86400+1);Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.Implement,root).Code);Assert.Contains("期限切れ",AdultAdoption.View(s.DiagnosticSnapshot()).Single().Status);}
    [Fact]public void DuplicateOriginalIsIdempotentAndPayloadConflictIsRejected(){var s=Start(Yes.Value);var root=Propose(s);Assert.Equal(ResultCode.AlreadyAccepted,Act(s,AdoptionAction.Propose,id:root.Value).Code);Assert.Equal(ResultCode.IdPayloadConflict,Act(s,AdoptionAction.Propose,parent:false,id:root.Value).Code);Assert.Single(s.DiagnosticSnapshot().Adoption!.Entries);}
    [Fact]public void AChildCannotHaveTwoPendingProposals(){var s=Start(Yes.Value);Propose(s);Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.Propose).Code);}
    [Fact]public void ExistingRelationshipCannotBeDuplicatedOrCycled(){var s=Agreed();var root=s.DiagnosticSnapshot().Adoption!.Entries.First().Input.Id;Assert.Equal(ResultCode.Accepted,Act(s,AdoptionAction.Implement,root).Code);Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.Propose).Code);Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.Propose,parent:false).Code);}
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BirthParentCannotBeDuplicatedOrBecomeOwnDescendant(bool asParent)
    {
        var parent=NewGameWorldTests.Officer() with{BirthYear=1490};
        var child=NewGameWorldTests.Officer(Other.Value) with{BirthParents=[parent.Id]};
        var setup=NewGameWorldTests.Setup([parent,child]);
        var session=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,parent.Id,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,
            companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,
            socialPolicy:CharacterRelations.Policy,adoptionPolicy:AdultAdoption.Policy));
        Assert.Equal(ResultCode.NotAvailable,Act(session,AdoptionAction.Propose,parent:asParent).Code);
        Assert.Empty(session.DiagnosticSnapshot().Adoption!.Entries);
    }
    [Theory][InlineData(true,"site:castle")][InlineData(false,"site:kofu")]
    public void MinorOrAbsentAdultCannotBeProposed(bool minor,string site){var s=Start(Yes.Value,minor:minor,site:site);Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.Propose).Code);Assert.DoesNotContain(Other,AdultAdoption.Candidates(s.DiagnosticSnapshot()));}
    [Fact]public void CompletedRelationRetainsItsRolesAfterTimePasses(){var s=Agreed();var root=s.DiagnosticSnapshot().Adoption!.Entries.First().Input.Id;Act(s,AdoptionAction.Implement,root);At(s,20);var t=s.DiagnosticSnapshot().Adoption!.Entries.First().Terms!;Assert.Equal(Player,t.Parent);Assert.Equal(Other,t.Child);new WorldSession(s.DiagnosticSnapshot());}
    [Fact]public void ForgedImplementationWithoutPersonalConsentIsRejected(){var s=Start(Yes.Value);var root=Propose(s);var w=s.DiagnosticSnapshot();w.Adoption=w.Adoption! with{Entries=[..w.Adoption.Entries,new(new(new("request:forged"),AdoptionAction.Implement,Original:root),Player,0,w.CommitSequence,null)]};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));}
    [Fact]public void WrongActorAndStaleRevisionCannotCommit(){var s=Start(Yes.Value);var i=new AdoptionInput(new("request:stale"),AdoptionAction.Propose,Other);Assert.Equal(ResultCode.NotAvailable,s.DecideAdoption(i,Other,s.Revision).Code);Assert.Equal(ResultCode.StateChanged,s.DecideAdoption(i,Player,s.Revision+1).Code);Assert.Empty(s.DiagnosticSnapshot().Adoption!.Entries);}
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void ActualGenerationRecoveryPreservesCaseAndFuture(int phase){var s=Start(phase==4?No.Value:Yes.Value,lord:phase is 2 or 3);var root=Propose(s);if(phase>0)At(s,1);if(phase==3){var t=s.DiagnosticSnapshot().Adoption!.Entries.First().Terms!;Assert.Equal(ResultCode.Accepted,Act(s,AdoptionAction.ApproveHouse,root,t.Houses[0].Owner).Code);Assert.Equal(ResultCode.Accepted,Act(s,AdoptionAction.Implement,root).Code);}if(phase==5)Assert.Equal(ResultCode.Accepted,Act(s,AdoptionAction.Withdraw,root).Code);s.SetPaused(true);var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"adoption-"+Guid.NewGuid().ToString("N")),w.Definitions);Assert.True(store.Save(s,"adoption",w.WorldId).Published);var host=new WorldSessionHost(Start());Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("adoption"),host.Current,host.Current.Revision));Assert.Equal(ContractJson.Write(w),ContractJson.Write(host.Current.DiagnosticSnapshot()));Assert.Equal(ContractJson.Write(AdultAdoption.View(w)),ContractJson.Write(AdultAdoption.View(host.Current.DiagnosticSnapshot())));var dir=Environment.GetEnvironmentVariable("HAKUTAI_WORLD_CLASH_QA_DIR");if(!string.IsNullOrEmpty(dir)){Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"adult-adoption-"+phase+".before.json"),ContractJson.Write(w));File.WriteAllBytes(Path.Combine(dir,"adult-adoption-"+phase+".restored.json"),ContractJson.Write(host.Current.DiagnosticSnapshot()));}foreach(var x in new[]{s,host.Current})At(x,x.Seconds+10);Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));}

    [Fact]public void MissingHouseRepresentativeRemainsPendingWithoutRetarget(){var s=Start(Yes.Value,remoteHouse:true);var root=Propose(s);At(s,1);var w=s.DiagnosticSnapshot();Assert.Contains(w.Adoption!.Entries,e=>e.Input.Action==AdoptionAction.PersonalYes);Assert.DoesNotContain(w.Adoption.Entries,e=>e.Input.Action==AdoptionAction.ApproveHouse);Assert.Equal(new EntityId("person:shingen"),w.Adoption.Entries.First().Terms!.Houses.Single().Representative);Assert.False(AdultAdoption.View(w).Single().CanImplement);Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.Implement,root).Code);}
    [Fact]public void NpcHouseAndPersonalConsentAreRecordedSeparately(){WorldSession? chosen=null;for(ulong n=1;n<100;n++){var s=Start(n);var r=s.DecideAdoption(new(new("request:npc-parent"),AdoptionAction.Propose,new("person:nobunaga"),false),s.PlayerControl,s.Revision);Assert.Equal(ResultCode.Accepted,r.Code);At(s,1);if(s.DiagnosticSnapshot().Adoption!.Entries.Any(e=>e.Input.Action==AdoptionAction.PersonalYes)){chosen=s;break;}}Assert.NotNull(chosen);var w=chosen.DiagnosticSnapshot();Assert.Single(w.Adoption!.Entries,e=>e.Input.Action==AdoptionAction.PersonalYes);Assert.Single(w.Adoption.Entries,e=>e.Input.Action==AdoptionAction.ApproveHouse);Assert.DoesNotContain(w.Adoption.Entries,e=>e.Input.Action==AdoptionAction.Implement);Assert.Equal(ResultCode.Accepted,Act(chosen,AdoptionAction.Implement,w.Adoption.Entries.First().Input.Id).Code);Assert.Equal(new EntityId("person:nobunaga"),AdultAdoption.View(chosen.DiagnosticSnapshot()).Single().Parent);}
    [Fact]public void DepartingAfterConsentBlocksImplementationAndPreservesPastConsent(){var s=Agreed();var root=s.DiagnosticSnapshot().Adoption!.Entries.First().Input.Id;Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:leave-adoption","fork"),s.Revision).Code);Assert.Equal(ResultCode.NotAvailable,Act(s,AdoptionAction.Implement,root).Code);At(s,10);new WorldSession(s.DiagnosticSnapshot());Assert.False(AdultAdoption.View(s.DiagnosticSnapshot()).Single().CanImplement);}
    [Fact]public void RequestIdsCannotBeReusedByOtherModules(){var s=Start(Yes.Value);var root=Propose(s);Assert.NotEqual(ResultCode.Accepted,s.Submit(NewGameWorld.Inspect(s.DiagnosticSnapshot(),root.Value),s.Revision).Code);}
}
