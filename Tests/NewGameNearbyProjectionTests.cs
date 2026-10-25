using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class NewGameNearbyProjectionTests
{
    static NearbyActor[] Oracle(ContractWorld w,EntityId observer)
    {
        var session=new WorldSession(w);var own=session.GetBranchView(observer).Position;
        return w.Characters.Where(c=>c.Id!=observer).Select(c=>session.GetBranchView(c.Id)).Where(v=>Math.Abs(v.Position.EastMm-own.EastMm)<=80000&&Math.Abs(v.Position.NorthMm-own.NorthMm)<=80000).Select(v=>new NearbyActor(v.Knowledge.Observer,v.Position,v.Heading,v.DistanceMm,v.Walking,v.NextPosition,v.StepDistanceMm)).ToArray();
    }
    [Theory][InlineData(0)][InlineData(100)]public void StationaryNearFieldMatchesAuthoritativeActorViews(int count)
    {
        var records=Enumerable.Range(0,count).Select(i=>NewGameWorldTests.Officer("created:nearby-"+i,site:NationalRoutes.Sites[i%NationalRoutes.Sites.Length].Id.Value)).ToArray();var setup=NewGameWorldTests.Setup(records);var w=NewGameWorld.Create(setup.World,setup.Registered,"person:nobunaga",personalAuthorityLayout:NewGameWorld.CompactPersonalAuthority);
        foreach(var observer in new[]{new EntityId("person:nobunaga"),new("person:shingen")})Assert.Equal(ContractJson.Write(Oracle(w,observer)),ContractJson.Write(RegionalCampaign.Nearby(w,observer)));
    }
    [Fact]public void TravelerSamplingAndNearFieldPrivacyStayIdenticalAfterRestore()
    {
        var setup=NewGameWorldTests.Setup([NewGameWorldTests.Officer()]);var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,"person:nobunaga",personalAuthorityLayout:NewGameWorld.CompactPersonalAuthority));Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:nearby-walk","fork"),s.Revision).Code);NewGameWorldTests.At(s,5);var w=s.DiagnosticSnapshot();var bytes=ContractJson.Write(w);
        foreach(var observer in new[]{s.PlayerControl,new EntityId("created:test")})Assert.Equal(ContractJson.Write(Oracle(w,observer)),ContractJson.Write(RegionalCampaign.Nearby(w,observer)));
        var restored=ContractJson.Read<ContractWorld>(bytes);Assert.Equal(ContractJson.Write(RegionalCampaign.Nearby(w,new("created:test"))),ContractJson.Write(RegionalCampaign.Nearby(restored,new("created:test"))));Assert.Equal(bytes,ContractJson.Write(w));
    }
}
