using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
using Xunit;

namespace HakutaiKagami.Tests;

// NPC の当主が捕らえた者の処遇（NpcCaptors、2026-11-14）：捕縛の1日後に捕虜の家の当主へ身代金500文を書状で求め、払われれば城で放つ。
// 書状は実際に運ばれる（ここでは配送の時刻まで時計を進めて届ける）。
public sealed class NpcCaptorTests
{
    // 書状の検査では捕虜を城で捕らえた形にするので、護送（CaptiveEscort）は外す。護送は CaptiveEscortTests で確かめる。
    internal static ContractWorld WithoutEscort(ContractWorld w){w.NewGame=w.NewGame! with{NationalSystems=w.NewGame.NationalSystems!.Where(x=>x!="CaptiveEscort").ToArray()};return w;}
    [Fact]
    public void AnNpcCaptorDemandsRansomByLetterAndReleasesTheCaptiveWhenPaid()
    {
        var w=NpcCaptorTests.WithoutEscort(NationalStart.Create(NationalStartTests.Nobunaga()));
        Assert.True(NationalSystems.On(w,"NpcCaptors"));
        var player=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        bool Adult(EntityId p)=>w.Characters.Single(c=>c.Id==p).Life==LifeState.Adult;
        // 捕らえた家 D（当主が本拠にいる）と、捕虜 P の家 A（P は当主でない成人）。
        var houses=w.NewGame.Start.Factions.Where(f=>f.Id!=player&&Adult(new(f.Head))&&PersonalCompanions.PhysicalSiteAt(w,new(f.Head),0,0)==new EntityId(f.Home)).OrderBy(f=>f.Id,StringComparer.Ordinal).ToArray();
        var d=houses[0];
        var (a,prisoner)=houses.Skip(1).Select(f=>(f,P:w.Memberships.Where(m=>m.Target.Value==f.Id&&m.Subject.Value!=f.Head).Select(m=>m.Subject).FirstOrDefault(p=>p.Value!=null&&Adult(p)))).First(x=>x.P.Value!=null);
        var campaign=new StrategyCampaign(new("campaign:national/test/captor"),new(a.Id),new(d.Id),new(d.Home),0,0,"border"){Real=true};
        var capture=new BattleCapture(prisoner,new(a.Id),new(d.Id),new(d.Head),new(d.Home),0);
        w.Strategy=new(1,NationalStrategy.Policy,[campaign],[new(campaign.Id,0,0,false){Capture=capture}]);
        Assert.True(BattleCaptivity.HeldAt(w,prisoner,1,1));
        void Step(long at){w.ElapsedSeconds=at;w.CommitSequence++;MessageFlow.Advance(w.Messaging,at);BattleCaptivity.Update(w);}
        long Due(string kind){var m=w.Messaging.Messages.Last(m=>m.Kind==BattleCaptivity.RansomKind&&m.Body.Contains(kind));return w.Messaging.Attempts.Where(x=>x.MessageId==m.Id).Max(x=>x.DueAt);}
        // 1日経つ前は求めない。1日後に求める。
        Step(3600);Assert.DoesNotContain(w.Messaging.Messages,m=>m.Kind==BattleCaptivity.RansomKind);
        Step(BattleCaptivity.NpcWaitSeconds+3600);
        var demand=Assert.Single(w.Messaging.Messages,m=>m.Kind==BattleCaptivity.RansomKind);
        Assert.Equal(d.Head,demand.SenderId);Assert.Equal(a.Head,demand.RecipientId);Assert.Contains("500文を求めます",demand.Body);
        // 書状が届くと、相手の当主は開封して払う（私財1000文で、500文は上限以内）。
        Step(Due("求めます"));
        var reply=Assert.Single(w.Messaging.Messages,m=>m.Kind==BattleCaptivity.RansomKind&&m.SenderId==a.Head);
        Assert.Contains("送ります",reply.Body);
        Assert.True(BattleCaptivity.HeldAt(w,prisoner,w.ElapsedSeconds,w.CommitSequence+1)); // 払われただけでは放たない
        // 返書が届くと、捕らえた当主が開封して受け取り、城で放つ。
        Step(Due("送ります"));
        Assert.False(BattleCaptivity.HeldAt(w,prisoner,w.ElapsedSeconds,w.CommitSequence+1));
        NewGameWork.Sync(w); // 私財の行を記録から求め直す（ふだんは変更のたびに行う）
        Assert.Equal(NewGameWork.Balance(NationalStart.Create(NationalStartTests.Nobunaga()),new(d.Head))+BattleCaptivity.NpcRansom,NewGameWork.Balance(w,new(d.Head)));
        Assert.Equal(NewGameWork.Balance(NationalStart.Create(NationalStartTests.Nobunaga()),new(a.Head))-BattleCaptivity.NpcRansom,NewGameWork.Balance(w,new(a.Head)));
    }
}
// NPC どうしの捕虜交換（NpcCaptors、2026-11-14）：互いに相手の家の者を捕らえていれば、家の ID の小さい側の当主が書状で引き換えを申し入れ、相手は応じて放ち、受諾が届くと申し入れた側も放つ。
public sealed class NpcCaptiveExchangeTests
{
    [Fact]
    public void TwoHousesHoldingEachOthersMenExchangeThemByLetter()
    {
        var w=NpcCaptorTests.WithoutEscort(NationalStart.Create(NationalStartTests.Nobunaga()));
        var player=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        bool Adult(EntityId p)=>w.Characters.Single(c=>c.Id==p).Life==LifeState.Adult;
        EntityId? Member(SetupFaction f)=>w.Memberships.Where(m=>m.Target.Value==f.Id&&m.Subject.Value!=f.Head).Select(m=>m.Subject).FirstOrDefault(p=>p.Value!=null&&Adult(p)&&p!=w.PlayerControl);
        var houses=w.NewGame.Start.Factions.Where(f=>f.Id!=player&&Adult(new(f.Head))&&PersonalCompanions.PhysicalSiteAt(w,new(f.Head),0,0)==new EntityId(f.Home)&&Member(f)!=null).OrderBy(f=>f.Id,StringComparer.Ordinal).Take(2).ToArray();
        var (small,large)=(houses[0],houses[1]);var p=Member(large)!.Value;var q=Member(small)!.Value; // small が p（large の者）を、large が q（small の者）を捕らえている
        var c1=new StrategyCampaign(new("campaign:national/test/x1"),new(large.Id),new(small.Id),new(small.Home),0,0,"border"){Real=true};
        var c2=new StrategyCampaign(new("campaign:national/test/x2"),new(small.Id),new(large.Id),new(large.Home),0,0,"border"){Real=true};
        w.Strategy=new(1,NationalStrategy.Policy,[c1,c2],[new(c1.Id,0,0,false){Capture=new(p,new(large.Id),new(small.Id),new(small.Head),new(small.Home),0)},new(c2.Id,0,0,false){Capture=new(q,new(small.Id),new(large.Id),new(large.Head),new(large.Home),0)}]);
        void Step(long at){w.ElapsedSeconds=at;w.CommitSequence++;MessageFlow.Advance(w.Messaging,at);BattleCaptivity.Update(w);}
        long Due(string text){var m=w.Messaging.Messages.Last(m=>m.Kind==BattleCaptivity.RansomKind&&m.Body.Contains(text));return w.Messaging.Attempts.Where(x=>x.MessageId==m.Id).Max(x=>x.DueAt);}
        Step(BattleCaptivity.NpcWaitSeconds+3600);
        var offer=Assert.Single(w.Messaging.Messages,m=>m.Kind==BattleCaptivity.RansomKind);
        Assert.Equal(small.Head,offer.SenderId);Assert.Contains("引き換え",offer.Body); // 身代金ではなく引き換えを申し入れる
        Step(Due("放ちたく"));
        Assert.Contains(w.Messaging.Messages,m=>m.Kind==BattleCaptivity.RansomKind&&m.SenderId==large.Head&&m.Body.Contains("応じます"));
        Assert.False(BattleCaptivity.HeldAt(w,q,w.ElapsedSeconds,w.CommitSequence+1)); // 応じた側が先に放つ
        Assert.True(BattleCaptivity.HeldAt(w,p,w.ElapsedSeconds,w.CommitSequence+1));
        Step(Due("応じます"));
        Assert.False(BattleCaptivity.HeldAt(w,p,w.ElapsedSeconds,w.CommitSequence+1)); // 受諾が届いて申し入れた側も放つ
    }
}
// 本人と NPC の家の捕虜の書状（PlayerCaptiveLetters、2026-11-15）：NPC は本人の家とも引き換えを申し入れ、本人は開封して応じる。
// 身代金の求めには本人が返書を出し、書状で知った家臣を自分が捕らえた者と引き換えに申し入れられる。
public sealed class PlayerCaptiveLetterTests
{
    static (ContractWorld W,SetupFaction Npc,EntityId Retainer,EntityId Theirs) Setup(bool playerHolds)
    {
        var w=NpcCaptorTests.WithoutEscort(NationalStart.Create(NationalStartTests.Nobunaga()));
        Assert.True(BattleCaptivity.PlayerLetters(w));
        var mine=w.NewGame!.Start.Factions.Single(f=>f.Id==w.NewGame.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction);
        Assert.Equal(w.PlayerControl,HouseSuccession.Head(w,new(mine.Id)));
        bool Adult(EntityId p)=>w.Characters.Single(c=>c.Id==p).Life==LifeState.Adult;
        EntityId? Member(SetupFaction f)=>w.Memberships.Where(m=>m.Target.Value==f.Id&&m.Subject.Value!=f.Head).Select(m=>m.Subject).FirstOrDefault(p=>p.Value!=null&&Adult(p)&&p!=w.PlayerControl);
        var npc=w.NewGame.Start.Factions.Where(f=>f.Id!=mine.Id&&Adult(new(f.Head))&&PersonalCompanions.PhysicalSiteAt(w,new(f.Head),0,0)==new EntityId(f.Home)&&Member(f)!=null).OrderBy(f=>f.Id,StringComparer.Ordinal).First();
        var retainer=Member(mine)!.Value;var theirs=Member(npc)!.Value;
        var c1=new StrategyCampaign(new("campaign:national/test/p1"),new(mine.Id),new(npc.Id),new(npc.Home),0,0,"border"){Real=true};
        var c2=new StrategyCampaign(new("campaign:national/test/p2"),new(npc.Id),new(mine.Id),new(mine.Home),0,0,"border"){Real=true};
        var outcomes=new List<StrategyOutcome>{new(c1.Id,0,0,false){Capture=new(retainer,new(mine.Id),new(npc.Id),new(npc.Head),new(npc.Home),0)}};
        if(playerHolds)outcomes.Add(new(c2.Id,0,0,false){Capture=new(theirs,new(npc.Id),new(mine.Id),w.PlayerControl,new(mine.Home),0)});
        w.Strategy=new(1,NationalStrategy.Policy,[c1,c2],[..outcomes]);
        return (w,npc,retainer,theirs);
    }
    static void Step(ContractWorld w,long at){w.ElapsedSeconds=at;w.CommitSequence++;MessageFlow.Advance(w.Messaging,at);BattleCaptivity.Update(w);}
    static long Due(ContractWorld w,string text){var m=w.Messaging.Messages.Last(m=>m.Kind==BattleCaptivity.RansomKind&&m.Body.Contains(text));return w.Messaging.Attempts.Where(x=>x.MessageId==m.Id).Max(x=>x.DueAt);}
    static void Open(ContractWorld w,string text){var m=w.Messaging.Messages.Last(m=>m.Kind==BattleCaptivity.RansomKind&&m.Body.Contains(text));w.CommitSequence++;if(!MessageFlow.Knows(w.Messaging,m.Id,w.PlayerControl.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(m.Id,w.PlayerControl.Value,w.ElapsedSeconds,m.Id)];BattleCaptivity.RecordOpening(w,w.PlayerControl,m.Id);}

    [Fact]
    public void TheNpcProposesAnExchangeToThePlayerWhoAcceptsByLetter()
    {
        var (w,npc,retainer,theirs)=Setup(true);
        Step(w,BattleCaptivity.NpcWaitSeconds+3600);
        var offer=Assert.Single(w.Messaging.Messages,m=>m.Kind==BattleCaptivity.RansomKind);
        Assert.Equal(npc.Head,offer.SenderId);Assert.Equal(w.PlayerControl.Value,offer.RecipientId);Assert.Contains("引き換え",offer.Body);
        Step(w,Due(w,"放ちたく"));Assert.Empty(BattleCaptivity.Letters(w,w.PlayerControl)); // 開封するまで知らない
        Open(w,"放ちたく");
        var card=Assert.Single(BattleCaptivity.Letters(w,w.PlayerControl));Assert.Equal(RansomAction.Exchange,card.Action);Assert.Equal(theirs,card.Other);
        w.CommitSequence++;BattleCaptivity.AcceptExchange(w,card.Id);
        Assert.True(Assert.Single(BattleCaptivity.Letters(w,w.PlayerControl)).Answered);
        Step(w,Due(w,"応じます"));
        Assert.False(BattleCaptivity.HeldAt(w,retainer,w.ElapsedSeconds,w.CommitSequence+1)); // 受諾が届いて NPC が放つ
        BattleCaptivity.Validate(w);
    }

    [Fact]
    public void ThePlayerRefusesARansomAndLaterProposesAnExchange()
    {
        var (w,npc,retainer,theirs)=Setup(false);
        Step(w,BattleCaptivity.NpcWaitSeconds+3600);
        Assert.Contains("求めます",Assert.Single(w.Messaging.Messages,m=>m.Kind==BattleCaptivity.RansomKind).Body);
        Step(w,Due(w,"求めます"));Open(w,"求めます");
        var demand=Assert.Single(BattleCaptivity.Letters(w,w.PlayerControl));Assert.Equal(RansomAction.Demand,demand.Action);
        w.CommitSequence++;BattleCaptivity.AnswerDemand(w,demand.Id,false);
        Assert.Contains(w.Messaging.Messages,m=>m.Kind==BattleCaptivity.RansomKind&&m.SenderId==w.PlayerControl.Value&&m.Body.Contains("応じません"));
        // 本人の家がその家の者を捕らえると、書状で知った家臣と引き換えを申し入れられる。
        var c2=new StrategyCampaign(new("campaign:national/test/p3"),new(npc.Id),new(w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction!),new(npc.Home),w.ElapsedSeconds,0,"border"){Real=true};
        var mine=w.NewGame.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction!;
        w.CommitSequence++;
        w.Strategy=w.Strategy! with{Campaigns=[..w.Strategy.Campaigns,c2],Outcomes=[..w.Strategy.Outcomes,new(c2.Id,w.ElapsedSeconds,w.CommitSequence,false){Capture=new(theirs,new(npc.Id),new(mine),w.PlayerControl,new(w.NewGame.Start.Factions.Single(f=>f.Id==mine).Home),w.ElapsedSeconds)}]};
        var option=Assert.Single(BattleCaptivity.ExchangeOptions(w,w.PlayerControl));Assert.Equal((theirs,retainer),option);
        w.CommitSequence++;BattleCaptivity.ProposeExchange(w,new("request:test/propose"),theirs,retainer);
        Assert.Empty(BattleCaptivity.ExchangeOptions(w,w.PlayerControl));
        Step(w,Due(w,"放ちたく"));
        Assert.False(BattleCaptivity.HeldAt(w,retainer,w.ElapsedSeconds,w.CommitSequence+1)); // 相手は応じて家臣を放つ
        Assert.Contains(w.Messaging.Messages,m=>m.Kind==BattleCaptivity.RansomKind&&m.RecipientId==w.PlayerControl.Value&&m.Body.Contains("応じます"));
        BattleCaptivity.Validate(w);
    }
}
// 捕虜の護送（CaptiveEscort、2026-11-15）：捕らえた場所から城まで人の歩みで運ばれ、着くまでは野にいて処遇できない。
public sealed class CaptiveEscortTests
{
    [Fact]
    public void ACaptiveIsInTheFieldUntilEscortedToTheCastle()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(NationalSystems.On(w,"CaptiveEscort"));
        var player=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value).Faction;
        bool Adult(EntityId p)=>w.Characters.Single(c=>c.Id==p).Life==LifeState.Adult;
        var houses=w.NewGame.Start.Factions.Where(f=>f.Id!=player&&Adult(new(f.Head))).OrderBy(f=>f.Id,StringComparer.Ordinal).ToArray();
        var d=houses[0];
        var (a,prisoner)=houses.Skip(1).Select(f=>(f,P:w.Memberships.Where(m=>m.Target.Value==f.Id&&m.Subject.Value!=f.Head).Select(m=>m.Subject).FirstOrDefault(p=>p.Value!=null&&Adult(p)))).First(x=>x.P.Value!=null&&PersonalCompanions.PhysicalSiteAt(w,x.P,0,1)!=new EntityId(d.Home));
        var campaign=new StrategyCampaign(new("campaign:national/test/escort"),new(a.Id),new(d.Id),new(d.Home),0,0,"border"){Real=true};
        var capture=new BattleCapture(prisoner,new(a.Id),new(d.Id),new(d.Head),new(d.Home),0);
        w.Strategy=new(1,NationalStrategy.Policy,[campaign],[new(campaign.Id,0,0,false){Capture=capture}]);
        long arrive=BattleCaptivity.EscortArrival(w,capture);Assert.True(arrive>0);
        w.ElapsedSeconds=arrive-1;
        Assert.True(BattleCaptivity.HeldAt(w,prisoner,w.ElapsedSeconds,1));
        Assert.Null(PersonalCompanions.PhysicalSiteAt(w,prisoner,w.ElapsedSeconds,1)); // 護送の途中
        w.ElapsedSeconds=arrive;
        Assert.Equal(new EntityId(d.Home),PersonalCompanions.PhysicalSiteAt(w,prisoner,w.ElapsedSeconds,1)); // 城に着いた
    }
}
