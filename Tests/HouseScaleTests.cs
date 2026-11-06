using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class HouseScaleTests
{
    [Fact]
    public void ARegionalLordCanProposeEqualDefenseAndReceiveShorterTermsFromALargerHouse()
    {
        var proposer=NewGameWorldTests.Officer(role:StartTemplate.Daimyo,site:"site:fork");
        var senior=NewGameWorldTests.Officer("created:senior",StartTemplate.Daimyo) with{Attributes=new(30,30,30,30)};
        EntityId own=new("org:negotiator"),other=new("org:senior");
        var castles=NewGameWorld.Scenario().Castles.Where(c=>c.Id!=proposer.Site && c.Id!=senior.Site)
            .Take(7).Select(c=>c.Id).Prepend(senior.Site).ToArray();
        var setup=NewGameWorldTests.Setup([proposer,senior],[new(proposer.Id,own.Value,[proposer.Site],false),new(senior.Id,other.Value,castles,false)]);
        var w=NewGameWorld.Create(setup.World,setup.Registered,proposer.Id,42,worldArmyPolicy:NpcArmies.CompactCombatPolicy,
            trucePolicy:TruceNegotiations.Policy,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy);
        w.CommitSequence++;
        HouseholdCourt.Execute(w,new(new("request:senior-scale"),CourtAction.DeclareScale,new(senior.Id),other,new(senior.Site),Scale:CourtScale.Great),new(senior.Id));
        var session=new WorldSession(w);
        var offer=new TreatyInput(new("request:defense"),TreatyAction.Propose,own,other,Days:30,Clauses:TreatyClause.Defense,NoticeDays:1){AttachHouseScale=true};
        var result=session.NegotiateTreaty(offer,session.PlayerControl,session.Revision);
        Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var sent=session.DiagnosticSnapshot();
        var paper=Assert.Single(sent.Treaties!.Papers);
        Assert.Equal(CourtScale.Local,paper.Terms.DeclaredScale);
        var arrival=sent.Couriers!.Missions.Single(c=>sent.Messaging.Attempts.Any(a=>a.Id==c.Attempt && a.MessageId==paper.Message)).ReadyAt;
        TruceNegotiationTests.Advance(session,arrival+1);
        var received=session.DiagnosticSnapshot();
        var counter=Assert.Single(received.Treaties!.Papers,p=>p.Input.Action==TreatyAction.Counter);
        Assert.Equal(TreatyClause.Defense,counter.Terms.Clauses);
        Assert.Equal(16*86400,counter.Terms.Until-counter.Terms.From);
        Assert.Empty(received.Treaties.Formations);
        Assert.Equal(ContractJson.Write(received),ContractJson.Write(new WorldSession(ContractJson.Copy(received)).DiagnosticSnapshot()));
    }

    [Fact]
    public void ThreeCastlesEnableDeclarationAndSecondSeatButDoNotNotifyInstantly()
    {
        var head=NewGameWorldTests.Officer(role:StartTemplate.Daimyo);
        var first=NewGameWorldTests.Officer("created:first",StartTemplate.Retainer,lord:head.Id);
        var second=first with{Id="created:second"};
        EntityId owner=new("org:scale-test"),site=new(head.Site);
        var setup=NewGameWorldTests.Setup([head,first,second],[new(head.Id,owner.Value,[head.Site,"site:kofu","site:odawara"],false)]);
        var w=NewGameWorld.Create(setup.World,setup.Registered,head.Id,42,inspectionPolicy:LocalInspectionFindings.Policy,
            workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy);
        var s=new WorldSession(w);
        CommandResult Nominate(string id,string person)=>s.ManageCourt(new(new(id),CourtAction.Nominate,new(person),owner,site),s.Revision);
        Assert.Equal(ResultCode.Accepted,Nominate("request:first",first.Id).Code);
        Assert.Equal(ResultCode.NotAvailable,Nominate("request:early",second.Id).Code);
        var declaration=new CourtInput(new("request:declare"),CourtAction.DeclareScale,s.PlayerControl,owner,site,Scale:CourtScale.Daimyo);
        Assert.Equal(ResultCode.Accepted,s.ManageCourt(declaration,s.Revision).Code);
        var sent=s.DiagnosticSnapshot();
        Assert.Equal(CourtScale.Local,HouseScaleNotices.Known(sent,new(first.Id),owner));
        Assert.Equal(CourtScale.Daimyo,HouseScaleNotices.Known(sent,s.PlayerControl,owner));
        Assert.Equal(ResultCode.Accepted,Nominate("request:second",second.Id).Code);
        Assert.Equal(2,s.DiagnosticSnapshot().Offices.Count(o=>o.Kind=="house-steward" && o.Until==long.MaxValue));
        s.SetPaused(true);
        var dir=Directory.CreateTempSubdirectory("hakutai-scale-");
        try
        {
            var store=new GenerationSaveStore(dir.FullName,w.Definitions);
            Assert.True(store.Save(s,"declared",w.WorldId).Published);
            var host=new WorldSessionHost(new WorldSession(w));
            Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("declared"),host.Current,host.Current.Revision));
            NewGameWorldTests.At(host.Current,120);NewGameWorldTests.At(s,120);
            var after=host.Current.DiagnosticSnapshot();
            Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(after));
            Assert.Equal(CourtScale.Daimyo,HouseScaleNotices.Known(after,new(first.Id),owner));
            Assert.Equal(CourtScale.Local,HouseScaleNotices.Known(after,new("person:nobunaga"),owner));
        }
        finally{dir.Delete(true);}
    }
}
