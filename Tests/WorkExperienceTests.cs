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
