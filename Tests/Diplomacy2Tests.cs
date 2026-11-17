using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// S06（2026-11-17）：DBの配偶者・養親の事実、婚姻による同盟と離縁、破約への制裁と悪名、約束の引き継ぎの知らせ、情報の索引。
[Trait("Tier","Heavy")]
public sealed class Diplomacy2Tests
{
    static ContractWorld Without(ContractWorld w)
    {
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.Diplomacy2).ToArray()};
        return old;
    }

    [Fact]
    public void RecordedSpousesAndAdoptiveParentsBecomeFactsAndTieTheirHouses()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var people=w.NewGame!.Start.People;
        var married=people.Select(p=>new EntityId(p.Id)).First(p=>Diplomacy2.Spouses(w,p,0).Length>0);
        var spouse=Diplomacy2.Spouses(w,married,0)[0];
        Assert.Contains(married,Diplomacy2.Spouses(w,spouse,0));
        Assert.Empty(Diplomacy2.Spouses(Without(w),married,0));
        var parent=people.Select(p=>new EntityId(p.Id)).FirstOrDefault(p=>Diplomacy2.AdoptedChildren(w,p).Length>0);
        Assert.NotEqual(default,parent);
        var cross=people.Select(p=>new EntityId(p.Id)).SelectMany(p=>Diplomacy2.Spouses(w,p,0).Select(s=>(P:p,S:s))).First(x=>people.Single(q=>q.Id==x.P.Value).Faction is {} a&&people.Single(q=>q.Id==x.S.Value).Faction is {} b&&a!=b);
        string fa=people.Single(q=>q.Id==cross.P.Value).Faction!,fb=people.Single(q=>q.Id==cross.S.Value).Faction!;
        Assert.True(Diplomacy2.Tied(w,fa,fb,0));Assert.Equal(400,Diplomacy2.AllianceChance(w,fa,fb,0));Assert.Equal(10,Diplomacy2.BetrayChance(w,fa,fb,0));
        Assert.False(Diplomacy2.Tied(Without(w),fa,fb,0));
    }

    [Fact]
    public void AnAllianceIsSealedByAMarriageThatEndsInDivorceWhenTheAllianceIsBroken()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());var me=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        var houses=w.NewGame.Start.Factions.Where(f=>f.Id!=me).Select(f=>new EntityId(f.Id)).ToArray();
        StrategyMarriage? m=null;StrategyPact? pact=null;
        for(int i=0;i<houses.Length&&m==null;i++)for(int j=i+1;j<Math.Min(houses.Length,i+12)&&m==null;j++)
        {pact=new StrategyPact(new($"pact:test/{i}/{j}"),houses[i],houses[j],"alliance",0,2*365*86400L,1,"threat:x");m=Diplomacy2.MarryFor(w,pact);}
        Assert.NotNull(m);
        w.Strategy=new StrategyState(1,NationalStrategy.Policy,[],[]){Pacts=[pact!],Marriages=[m]};
        Assert.Contains(m.Woman,Diplomacy2.Spouses(w,m.Man,10));
        w.Strategy=w.Strategy with{Pacts=[pact! with{Broken=100}]};
        Assert.DoesNotContain(m.Woman,Diplomacy2.Spouses(w,m.Man,200));Assert.Single(Diplomacy2.Divorced(w,200));
    }

    [Fact]
    public void AlliesOfTheBetrayedHouseDropTheBetrayerAndItsHeadGetsInfamy()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());var f=w.NewGame!.Start.Factions.Where(x=>x.Head!=w.PlayerControl.Value).Take(3).Select(x=>new EntityId(x.Id)).ToArray();
        EntityId betrayer=f[0],victim=f[1],friend=f[2];
        var pacts=new List<StrategyPact>{new(new("pact:t/1"),victim,friend,"alliance",0,999999999,1,"threat:x"),new(new("pact:t/2"),betrayer,friend,"alliance",0,999999999,1,"threat:x"),new(new("pact:t/3"),betrayer,victim,"alliance",0,999999999,1,"threat:x")};
        Diplomacy2.Sanctions(w,pacts,betrayer.Value,victim.Value,5000);
        Assert.Equal(5000,pacts.Single(p=>p.Id.Value=="pact:t/2").Broken);Assert.Null(pacts.Single(p=>p.Id.Value=="pact:t/1").Broken);
        Assert.Empty(new List<StrategyPact>(pacts).Where(p=>p.Id.Value=="pact:t/2"&&p.Broken==null));
        var site=new EntityId(w.NewGame.Start.Factions.Single(x=>x.Id==victim.Value).Home);
        w.Strategy=new StrategyState(1,NationalStrategy.Policy,[new(new("campaign:t/b"),betrayer,victim,site,5000,2,"betrayal")],[]){Pacts=pacts.ToArray()};
        w.ElapsedSeconds=6000;w.CommitSequence=3;
        var head=HouseSuccession.Head(w,betrayer);
        Assert.Contains(SocialWorld.Renown(w,HouseSuccession.Head(w,victim)),r=>r.Subject==head&&r.Kind==RenownKind.Infamy);
        Assert.Contains(SocialWorld.Renown(w,HouseSuccession.Head(w,friend)),r=>r.Subject==head&&r.Kind==RenownKind.Infamy);
        Assert.DoesNotContain(SocialWorld.Renown(Without(w),HouseSuccession.Head(w,victim)),r=>r.Subject==head&&r.Kind==RenownKind.Infamy);
    }

    [Fact]
    public void ANewHeadLearnsTheHouseCommitmentsItInheritsAndTheIndexGroupsWhatIsKnown()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());var f=w.NewGame!.Start.Factions.Where(x=>x.Head!=w.PlayerControl.Value).Take(2).ToArray();
        var heir=new EntityId(w.NewGame.Start.People.First(p=>p.Faction==f[0].Id&&p.Lord!=null).Id);
        w.Strategy=new StrategyState(1,NationalStrategy.Policy,[],[]){Pacts=[new(new("pact:t/i"),new(f[0].Id),new(f[1].Id),"alliance",0,999999999,1,"threat:x")],Successions=[new(new(f[0].Id),new(f[0].Head),heir,1000,2)]};
        w.ElapsedSeconds=2000;w.CommitSequence=3;
        Assert.Single(Diplomacy2.Commitments(w,new(f[0].Id),1000));
        var row=Diplomacy2.ChronicleRows(w,heir).Single(r=>r.Kind=="引き継ぎ");
        Assert.Contains(w.Definitions.Organizations.Single(o=>o.Id.Value==f[1].Id).Name,row.Text);
        var index=Diplomacy2.Index(w,[row,new("x","噂","t2",null,3000,"噂",f[1].Id)]);
        Assert.Equal(2,index.Length);Assert.All(index,r=>Assert.Equal(1,r.Count));
        Assert.Empty(Diplomacy2.ChronicleRows(Without(w),heir));
        // 操作の引き継ぎで当主を継いだ本人にも、引き継ぎの知らせが出る。
        var me=w.PlayerControl;var house=w.NewGame.Start.People.Single(p=>p.Id==me.Value).Faction!;
        w.Strategy=w.Strategy with{Pacts=[..w.Strategy.Pacts!,new(new("pact:t/me"),new(house),new(f[1].Id),"truce",0,999999999,1,"mediation:x")]};
        w.ControlHandovers=[new(new(f[1].Head),me,1500,2,ControlSuccession.Death)];
        Assert.Contains(Diplomacy2.ChronicleRows(w,me),r=>r.Kind=="引き継ぎ"&&r.Text.Contains("和睦"));
    }
}
