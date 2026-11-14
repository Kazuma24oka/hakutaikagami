using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// 実際の仕事の経験（WorkExperience、2026-11-15）：普請・検地を命じた人は、仕事が終わった時点で工期1日あたり1時間の実務の経験を、仕事の種類の技能で得る。
public sealed class WorkExperienceTests
{
    [Fact]
    public void TheOrdererGainsExperienceInTheWorksSkillWhenTheWorkEnds()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(NationalSystems.On(w,"WorkExperience"));
        var actor=w.PlayerControl;var site=new EntityId(w.NewGame!.Start.People.Single(p=>p.Id==actor.Value).Site);
        var order=new WorksOrder(new(new("request:test/works"),site,WorksKind.StoneWall),actor,new("org:test"),3600,1,3600+7*86400L,0,0,0,10,10,"test");
        var mine=new WorksOrder(new(new("request:test/mine"),site,WorksKind.Mine),actor,new("org:test"),3600,1,3600+2*86400L,0,0,0,10,10,"test");
        w.PublicWorks=new(1,PublicWorks.Policy,[order,mine],[]);
        w.ElapsedSeconds=3600+86400;CharacterDevelopment.Sync(w);
        Assert.DoesNotContain(w.Development!.Awards,a=>a.Task==order.Input.Id); // 終わる前は入らない
        w.ElapsedSeconds=3600+8*86400;CharacterDevelopment.Sync(w);
        var wall=Assert.Single(w.Development!.Awards,a=>a.Task==order.Input.Id);
        Assert.Equal(DevelopmentSkill.Construction,wall.Skill);Assert.Equal(7*3600,wall.Experience);
        Assert.Equal(DevelopmentSkill.Mining,Assert.Single(w.Development.Awards,a=>a.Task==mine.Input.Id).Skill);
        // 一覧を持たない世界は仕事の経験を入れない。
        w.NewGame=w.NewGame with{NationalSystems=w.NewGame.NationalSystems!.Where(x=>x!="WorkExperience").ToArray()};CharacterDevelopment.Sync(w);
        Assert.DoesNotContain(w.Development!.Awards,a=>a.Task==order.Input.Id);
    }
}
// 兵科の技能（TroopSkills、2026-11-15）：兵の種類に合う大将の技能が、統率を上限として損害を最大2割増やす。
public sealed class TroopSkillTests
{
    [Fact]
    public void TheCommandersMatchingWeaponSkillCappedByLeadershipRaisesDamage()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(NationalSystems.On(w,"TroopSkills"));
        var who=w.Characters.Select(c=>c.Id).First(p=>CharacterDevelopment.LevelAt(w,p,DevelopmentSkill.Spear,0)>0);
        int spear=CharacterDevelopment.LevelAt(w,who,DevelopmentSkill.Spear,0),lead=CharacterDevelopment.EffectiveAt(w,who,DevelopmentAbility.Leadership,0);
        var unit=new UnitState(new("unit:test"),new("army:test"),who,new("org:test"),100,0);
        Assert.Equal(Math.Min(spear,lead)/5,BattlefieldRules.TroopSkillBonus(w,unit,TroopKind.Spear));
        Assert.Equal(Math.Min(CharacterDevelopment.LevelAt(w,who,DevelopmentSkill.Gun,0),lead)/5,BattlefieldRules.TroopSkillBonus(w,unit,TroopKind.Gun));
        Assert.InRange(BattlefieldRules.TroopSkillBonus(w,unit,TroopKind.Spear),0,20);
        w.NewGame=w.NewGame! with{NationalSystems=w.NewGame.NationalSystems!.Where(x=>x!="TroopSkills").ToArray()};
        Assert.Equal(0,BattlefieldRules.TroopSkillBonus(w,unit,TroopKind.Spear));
    }
}
// 医者を頼む（NpcDoctor、2026-11-15）：同じ拠点にいる家の外の医術30以上の成人にも手当を頼める。病人は移らない。
public sealed class NpcDoctorTests
{
    [Fact]
    public void ADoctorOfAnotherHouseAtTheSameSiteCanTreat()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(NationalSystems.On(w,"NpcDoctor"));
        bool Adult(EntityId p)=>w.Characters.Single(c=>c.Id==p).Life==LifeState.Adult;
        HashSet<EntityId> Houses(EntityId p)=>w.Memberships.Where(m=>m.Subject==p&&m.From<=0&&0<m.Until).Select(m=>m.Target).ToHashSet();
        // 同じ城にいて家の違う成人の二人。
        var pair=w.Characters.Select(c=>c.Id).Where(p=>p!=w.PlayerControl&&Adult(p)).GroupBy(p=>PersonalCompanions.PhysicalSiteAt(w,p,0,0)).Where(g=>g.Key!=null)
            .SelectMany(g=>g.SelectMany(a=>g.Where(b=>b!=a&&!Houses(a).Overlaps(Houses(b))&&Houses(a).Count>0).Select(b=>(Site:g.Key!.Value,Patient:a,Doctor:b)))).First();
        long at=86400;w.ElapsedSeconds=at+60;
        w.Development=w.Development! with{Awards=[new(new("task:test/doctor"),pair.Doctor,DevelopmentSkill.Medicine,60,0,100,CharacterDevelopment.LevelXp(90))]};
        Assert.Equal(pair.Doctor,PersonalHealth.HouseCarer(w,pair.Patient,pair.Site,at));
        w.NewGame=w.NewGame! with{NationalSystems=w.NewGame.NationalSystems!.Where(x=>x!="NpcDoctor").ToArray()};
        Assert.NotEqual(pair.Doctor,PersonalHealth.HouseCarer(w,pair.Patient,pair.Site,at)); // 一覧を持たない世界は家の中だけ
    }
}
