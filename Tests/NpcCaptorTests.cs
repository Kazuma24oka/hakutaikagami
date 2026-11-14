using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
using Xunit;

namespace HakutaiKagami.Tests;

// NPC の当主が捕らえた者の処遇（NpcCaptors、2026-11-14）：捕縛の1日後に捕虜の家の当主へ身代金500文を書状で求め、払われれば城で放つ。
// 書状は実際に運ばれる（ここでは配送の時刻まで時計を進めて届ける）。
public sealed class NpcCaptorTests
{
    [Fact]
    public void AnNpcCaptorDemandsRansomByLetterAndReleasesTheCaptiveWhenPaid()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
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
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
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
