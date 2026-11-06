using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

public class DefectionPactTests
{
    static readonly EntityId Actor=new("created:test"),Partner=new("created:partner");
    static ContractWorld Start(ulong seed,bool foreign=false)
    {
        var actor=NewGameWorldTests.Officer(role:StartTemplate.Retainer,lord:"person:nobunaga");
        var partner=NewGameWorldTests.Officer(Partner.Value,StartTemplate.Retainer,foreign?"site:kofu":actor.Site,
            foreign?"person:shingen":"person:nobunaga");
        var setup=NewGameWorldTests.Setup([actor,partner]);
        return NewGameWorld.Create(setup.World,setup.Registered,actor.Id,seed,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,
            socialPolicy:CharacterRelations.Policy,comingOfAge:15);
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
