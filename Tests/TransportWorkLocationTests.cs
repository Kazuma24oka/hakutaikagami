using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class TransportWorkLocationTests
{
    static readonly EntityId Job=new("job:local-purse");
    static void Do(WorldSession s,HandoverAction action)=>Assert.Equal(ResultCode.Accepted,s.HandoverTransport(Job,TransportWorkTests.Input(s,action),s.Revision).Code);
    static WorldSession Stopped()
    {
        var w=TransportWorkTests.New();w.Couriers=w.Couriers! with{Conditions=BranchRoutes.GraphEdges(w).Where(e=>e.From=="kofu"||e.To=="kofu").Select(e=>new RoadCondition(1,e.Id,true)).ToArray()};
        var s=new WorldSession(w);Assert.Equal(ResultCode.Accepted,s.RequestTransport(Job,s.Revision).Code);Do(s,HandoverAction.Accept);Do(s,HandoverAction.Collect);
        Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:blocked-purse","suwa"),s.Revision).Code);NewGameWorldTests.At(s,20);
        Assert.NotEmpty(s.DiagnosticSnapshot().TrafficStops!);Assert.Equal(LocationKind.AtSite,s.DiagnosticSnapshot().Locations.Single(l=>l.Actor==s.PlayerControl).Kind);return s;
    }
    [Fact]public void StoppedPurseCanActuallyBeReturnedBeforePlannedArrival(){var s=Stopped();Do(s,HandoverAction.Cancel);Do(s,HandoverAction.Return);Assert.Equal(1000,NewGameWork.Balance(s.DiagnosticSnapshot(),new("person:shingen")));}
    [Fact]public void ElapsedPlannedArrivalDoesNotPermitRemoteDelivery(){var s=Stopped();NewGameWorldTests.At(s,100000);var hash=ContractJson.CanonicalHash(s.DiagnosticSnapshot());Assert.Equal(ResultCode.NotAvailable,s.HandoverTransport(Job,TransportWorkTests.Input(s,HandoverAction.Deliver),s.Revision).Code);Assert.Equal(hash,ContractJson.CanonicalHash(s.DiagnosticSnapshot()));Do(s,HandoverAction.Cancel);Do(s,HandoverAction.Return);}
    [Fact]public void StoppedJourneyGenerationRestoreKeepsRealHandoverLocation(){var s=Stopped();s.SetPaused(true);var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"purse-halt-"+Guid.NewGuid().ToString("N")),w.Definitions);store.Save(s,"manual",w.WorldId,new(0,1));var host=new WorldSessionHost(new(TransportWorkTests.New()));Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("manual"),host.Current,host.Current.Revision));Do(s,HandoverAction.Cancel);Do(host.Current,HandoverAction.Cancel);Do(s,HandoverAction.Return);Do(host.Current,HandoverAction.Return);Assert.Equal(ContractJson.CanonicalHash(s.DiagnosticSnapshot()),ContractJson.CanonicalHash(host.Current.DiagnosticSnapshot()));}
}
