using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S03（2026-11-17）：新規開始のつまみの保存、個人の目標の追加（名声・一門の繁栄・城の奪取）、1650年の総括の歩み。
[Trait("Tier","Heavy")]
public sealed class LifeGoals2Tests
{
    static ContractWorld Goal(LifeGoal goal,long target,EntityId? subject=null,ContractWorld? world=null)
    {
        var s=new WorldSession(world??NationalStart.Create(NationalStartTests.Nobunaga()));
        var site=SocialWorld.Site(s.DiagnosticSnapshot(),s.PlayerControl)!.Value;
        var r=s.ManageLife(new(new("request:goal2/"+goal),LifeAction.SetGoal,site,Goal:goal,Target:target){Subject=subject},s.PlayerControl,s.Revision);
        Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
        return s.DiagnosticSnapshot();
    }

    [Fact]
    public void TheNewGameKnobsAreSavedInTheNationalStart()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga(),history:HistoryMode.Historical,knobs:new(NpcAttention.Standard,NpcChallenge.Strong,DecisionSupport.Independent));
        Assert.Equal(NpcChallenge.Strong,w.NewGame!.Judgment!.Challenge);Assert.Equal(DecisionSupport.Independent,w.NewGame.Judgment.Support);Assert.Equal(NpcAttention.Standard,w.NewGame.Judgment.Attention);
        _=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        var plain=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.Equal(NpcChallenge.Standard,plain.NewGame!.Judgment!.Challenge);Assert.Null(plain.NewGame.Judgment.Support);
    }

    [Fact]
    public void FamilyProsperityCountsLivingChildrenAndGrandchildrenYearly()
    {
        var sc=NationalStart.Scenario();var motonari=sc.People.First(p=>p.Name.Replace(" ","")=="毛利元就").Id;
        var w=Goal(LifeGoal.FamilyProsperity,2,world:NationalStart.Create(motonari));
        var kids=FamilyBirth.People(w).Count(p=>(p.BirthParents??[]).Contains(w.PlayerControl.Value));
        Assert.True(kids>=2,"元就の子が名簿に2人以上いる");
        w.ElapsedSeconds=366L*86400;
        var g=PersonalLife.Goals(w).Single(x=>x.Goal==LifeGoal.FamilyProsperity);
        Assert.True(g.Achieved);Assert.Equal(365L*86400,g.AchievedAt);Assert.True(g.Progress>=2);
    }

    [Fact]
    public void TakingACastleIsMetAtTheFirstMonthItBelongsToTheHouseAndFameStartsAtZero()
    {
        var w0=NationalStart.Create(NationalStartTests.Nobunaga());
        var own=w0.NewGame!.Start.Castles.First(c=>c.Faction==w0.NewGame.Start.People.Single(p=>p.Id==w0.PlayerControl.Value).Faction).Id;
        var w=Goal(LifeGoal.TakeCastle,1,new(own),w0);
        var d=WorldCalendar.At(w.EpochDate,0);w.ElapsedSeconds=NationalStrategy.MonthStart(w,d.Year,d.Month+1);
        var g=PersonalLife.Goals(w).Single(x=>x.Goal==LifeGoal.TakeCastle);
        Assert.True(g.Achieved);Assert.Equal(w.ElapsedSeconds,g.AchievedAt);
        var f=Goal(LifeGoal.Fame,3);
        Assert.False(PersonalLife.Goals(f).Single(x=>x.Goal==LifeGoal.Fame).Achieved);
    }

    [Fact]
    public void OldListsCannotChooseTheNewGoalsAndHaveNoJourney()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga(),timePolicy:WorldTimeControl.Policy);
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.LifeGoals2).ToArray()};
        // 新しい一覧の世界で決めた名声の目標の記録を、一覧の無い世界に持ち込むと検証で拒まれる。
        var withGoal=Goal(LifeGoal.Fame,3,world:ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        PersonalLife.Validate(withGoal);
        var moved=ContractJson.Read<ContractWorld>(ContractJson.Write(withGoal));moved.NewGame=old.NewGame;
        Assert.Throws<InvalidDataException>(()=>PersonalLife.Validate(moved));
        // 総括の歩み：総括の時点の家と城の数・年数を出す（一覧の無い世界は出さない）。
        w.TimeControl=w.TimeControl! with{SummaryAt=WorldCalendar.HistoricalEndSeconds(w.EpochDate),Summary=new(0,0)};
        var j=WorldTimeControl.Journey(w);
        Assert.NotNull(j);Assert.Equal(90,j.Years);Assert.True(j.CastlesAtStart>0);
        old.TimeControl=w.TimeControl;Assert.Null(WorldTimeControl.Journey(old));
    }
}
