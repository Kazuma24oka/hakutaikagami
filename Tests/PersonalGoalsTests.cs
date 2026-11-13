using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// 本人の目標：家の存続・所領維持・主家支援・報復（BD-08、2026-11-13）。
public sealed class PersonalGoalsTests
{
    static readonly EntityId Site=new("site:kofu");
    static void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
    static CommandResult Goal(WorldSession s,LifeGoal goal,EntityId? subject=null)=>
        s.ManageLife(new(new($"request:goal/{goal}/{s.Revision}"),LifeAction.SetGoal,Site,Goal:goal,Target:1){Subject=subject},s.PlayerControl,s.Revision);

    [Fact]
    public void HoldingGoalsCountYearsWhileTheHouseKeepsItsCastles()
    {
        var s=CastleHandoverTests.Start();
        Good(Goal(s,LifeGoal.KeepHoldings));
        var w=s.DiagnosticSnapshot();
        Assert.Contains(PersonalLife.Goals(w),g=>g.Goal==LifeGoal.KeepHoldings&&g.Progress==0&&!g.Achieved);
        w.ElapsedSeconds+=366*86400L; // 一年後の日（城は開始時の持ち主のまま）
        var card=Assert.Single(PersonalLife.Goals(w),g=>g.Goal==LifeGoal.KeepHoldings);
        Assert.True(card.Achieved);Assert.Equal(1,card.Progress);
        Good(Goal(s,LifeGoal.SupportLord));
        Assert.Contains(PersonalLife.Goals(s.DiagnosticSnapshot()),g=>g.Goal==LifeGoal.SupportLord&&!g.Achieved);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(s.DiagnosticSnapshot())));
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }

    [Fact]
    public void RevengeNeedsAKnownLivingPersonAndOnlyItUsesASubject()
    {
        var s=CastleHandoverTests.Start();var w=s.DiagnosticSnapshot();
        var stranger=w.Characters.Select(c=>c.Id).First(id=>id!=s.PlayerControl);
        Assert.NotEqual(ResultCode.Accepted,Goal(s,LifeGoal.Revenge,new EntityId("person:nobody")).Code); // いない人は選べない
        Assert.NotEqual(ResultCode.Accepted,Goal(s,LifeGoal.Revenge,s.PlayerControl).Code);   // 自分は選べない
        Assert.NotEqual(ResultCode.Accepted,Goal(s,LifeGoal.KeepHouse,stranger).Code);        // 相手は報復だけ
        // 同じ拠点にいる人を相手にできる。
        var foe=PersonalLife.RevengeChoices(w,s.PlayerControl,w.ElapsedSeconds,w.CommitSequence).First();
        {
            Good(Goal(s,LifeGoal.Revenge,foe));
            var card=Assert.Single(PersonalLife.Goals(s.DiagnosticSnapshot()),g=>g.Goal==LifeGoal.Revenge);
            Assert.False(card.Achieved);
            s.SetPaused(false);s.AdvanceTo(s.Seconds+600,100000);Assert.True(s.Seconds>0); // 時間が進んでも検証が通る
            var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(s.DiagnosticSnapshot())));
            Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
        }
    }
}
