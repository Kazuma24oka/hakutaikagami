using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class RegionalActorPointsTests
{
    [Fact]public void BatchedPositionsKeepLocalMotionAndHistoricalTimeOfEachActor()
    {
        var s=new WorldSession(RegionalBattle.Create());
        Assert.Equal(ResultCode.Accepted,s.IssueRegional(RegionalBattleTests.Order(),s.Revision).Code);
        Assert.Equal(ResultCode.Accepted,s.IssueRegional(RegionalBattleTests.Order("request:clerk-points",clerk:true),s.Revision).Code);
        s.AdvanceTo(7);var w=s.DiagnosticSnapshot();
        foreach(long at in new[]{0L,s.Seconds,s.Seconds+3})
        {
            var points=RegionalCampaign.ActorPointsAt(w,at);
            foreach(var c in w.Characters)Assert.Equal(RegionalCampaign.ActorPointAt(w,c.Id,at),points(c.Id));
        }
        Assert.NotEqual(FieldWorld.Initial(0),RegionalBattle.Position(w,w.PlayerControl));
    }
}
