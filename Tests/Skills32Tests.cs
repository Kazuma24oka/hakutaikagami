using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// 32技能の版（2026-11-14、Docs/Decisions/2026-11-06_CHARACTER_ABILITIES_SKILLS_GROWTH.md）。新規の全国開始から。
public sealed class Skills32Tests
{
    [Fact]
    public void TheNationalStartUsesThirtyTwoSkillsWithACurveAndHalfCreditForSelfPractice()
    {
        bool guard=WorldSession.GuardTurnSnapshots;WorldSession.GuardTurnSnapshots=false;
        try{
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(CharacterDevelopment.Skills32(w));
        Assert.Equal(32,CharacterDevelopment.Skills(w).Length);
        // 経験の曲線：レベル10は396,000、50は9,180,000、100は36,360,000。
        Assert.Equal(396_000,CharacterDevelopment.LevelXp(10));Assert.Equal(9_180_000,CharacterDevelopment.LevelXp(50));Assert.Equal(36_360_000,CharacterDevelopment.LevelXp(100));
        Assert.Equal(49,CharacterDevelopment.LevelOf(9_179_999));Assert.Equal(50,CharacterDevelopment.LevelOf(9_180_000));Assert.Equal(100,CharacterDevelopment.LevelOf(long.MaxValue/4));
        var me=w.PlayerControl;
        var card=Assert.IsType<DevelopmentCard>(CharacterDevelopment.View(w,me,me));
        Assert.Equal(32,card.Skills.Length);
        // 開始時の熟練は4つだけ（武器1つを含む）。ほかは0。史料のない値は試作の初期値と示す。
        var started=card.Skills.Where(s=>s.Value>0).ToArray();
        Assert.Equal(4,started.Length);
        Assert.Contains(started,s=>s.Name is "剣術" or "槍" or "弓" or "騎馬");
        Assert.All(started,s=>{Assert.InRange(s.Value!.Value,5,40);Assert.Contains("未採録",s.Source);Assert.Equal(CharacterDevelopment.LevelXp(s.Value.Value),s.Experience);});
        // 医術を30分独習すると、実働1800秒の半分の900経験。
        var s=new WorldSession(w);
        var medicine=card.Skills.Single(x=>x.Name=="医術");
        Assert.Equal(ResultCode.Accepted,s.Submit(CharacterDevelopment.Practice(s.DiagnosticSnapshot(),"request:practice-medicine",DevelopmentSkill.Medicine),s.Revision).Code);
        NewGameWorldTests.At(s,s.Seconds+CharacterDevelopment.PracticeSeconds+120);
        var after=CharacterDevelopment.View(s.DiagnosticSnapshot(),me,me)!.Skills.Single(x=>x.Name=="医術");
        Assert.Equal(medicine.Experience+CharacterDevelopment.PracticeSeconds/2,after.Experience);
        // 保存から戻して同じ。
        var snap=s.DiagnosticSnapshot();var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(snap)));
        Assert.Equal(ContractJson.Write(snap),ContractJson.Write(restored.DiagnosticSnapshot()));
        }finally{WorldSession.GuardTurnSnapshots=guard;}
    }
}
// 家人に頼む手当（NpcHouseCare、2026-11-14）：NPC の手当は同じ拠点の同じ家で医術の最も高い人がし、医術が高いほど早く治る。
public sealed class NpcHouseCareTests
{
    [Fact]
    public void TheBestHealerOfTheHouseAtTheSameSiteTreatsAndShortensRecovery()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(NationalSystems.On(w,"NpcHouseCare"));
        // 同じ家・同じ城にいる成人の二人（操作する人物でない）。
        var pair=w.Memberships.GroupBy(m=>m.Target).SelectMany(g=>g.Select(m=>m.Subject).Distinct().Where(p=>p!=w.PlayerControl&&w.Characters.Single(c=>c.Id==p).Life==LifeState.Adult)
            .GroupBy(p=>PersonalCompanions.PhysicalSiteAt(w,p,0,0)).Where(x=>x.Key!=null&&x.Count()>=2).Select(x=>(Site:x.Key!.Value,A:x.ElementAt(0),B:x.ElementAt(1)))).First();
        var c=new HealthCondition(new("health:test/fever"),pair.A,HealthKind.Illness,HealthPart.Body,2,3600,10*86400);
        long at=c.OnsetAt+PersonalHealth.NpcCareDelay;w.ElapsedSeconds=at+60;
        // 医術の誰もいなければ、本人が手当をし、残りの経過は 3/5（今までと同じ）。
        Assert.Equal(pair.A,PersonalHealth.HouseCarer(w,pair.A,pair.Site,at));
        long untreated=c.OnsetAt+c.BaseSeconds;
        Assert.Equal(at+(untreated-at)*3/5,PersonalHealth.RecoverAt(w,c));
        // 家人が医術50なら、その人が手当をし、残りの経過は 1/2。
        w.Development=w.Development! with{Awards=[new(new("task:test/medicine"),pair.B,DevelopmentSkill.Medicine,60,0,100,CharacterDevelopment.LevelXp(50))]};
        Assert.Equal(pair.B,PersonalHealth.HouseCarer(w,pair.A,pair.Site,at));
        var w2=ContractJson.Copy(w); // 手当の答えは開始の定義ごとに覚えるので、新しい定義で求め直す
        w2.NewGame=w2.NewGame! with{Seed=w2.NewGame.Seed};
        Assert.Equal(at+(untreated-at)*(300-50)/500,PersonalHealth.RecoverAt(w2,c));
    }
}
