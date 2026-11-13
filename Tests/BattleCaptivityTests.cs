using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;
namespace HakutaiKagami.Tests;

// 合戦での捕縛と処遇（BD-44・C7）：崩れた敵の軍の大将を捕らえ、拘束中は動けず、当主が明示に解放・登用の誘い・処断を選ぶ。
[Trait("Tier","Heavy")]
public sealed class BattleCaptivityTests(ITestOutputHelper o)
{
    // 接触から、敵の軍が崩れるか捕縛が起きるまで1秒ずつ進める。
    static (WorldSession S,WorldClashStep? Capture) FightUntilRout(WorldSession s,int limit=900)
    {
        for(int n=0;n<limit;n++)
        {
            NewGameWorldTests.At(s,s.Seconds+1);
            var w=s.DiagnosticSnapshot();
            if(w.WorldClashes!.Steps.LastOrDefault(x=>x.Captures!=null) is {} hit)return(s,hit);
            if(w.NpcArmies!.Returns.Length>0)break;
        }
        return(s,null);
    }

    [Fact]
    public void ARoutedCommanderIsCapturedHeldAndTreatedOnlyByTheCaptorAndRestoresTheSameFuture()
    {
        var s=NpcArmyTests.AtActualContact(permitted:true,sparse:true,capture:true);
        Assert.True(BattleCaptivity.Enabled(s.DiagnosticSnapshot()));
        var (_,step)=FightUntilRout(s);
        var w=s.DiagnosticSnapshot();
        o.WriteLine($"steps={w.WorldClashes!.Steps.Length} at={w.ElapsedSeconds} capture={step!=null} musters={w.NpcArmies!.Musters.Length}");
        foreach(var m in w.NpcArmies.Musters)foreach(var u in NewGameMilitary.LedgerIdentity(m.Owner,m.Site).Units)o.WriteLine($"{m.Commander.Value} {u.Value} morale={100-WorldClashes.MoraleLost(w,u,w.ElapsedSeconds)}");
        Assert.NotNull(step);
        var c=Assert.Single(step.Captures!);
        Assert.Equal(s.PlayerControl,c.Holder);
        Assert.True(HostageCustody.HeldAt(w,c.Person,w.ElapsedSeconds,w.CommitSequence+1));
        var card=Assert.Single(s.Captives());Assert.True(card.Held);Assert.Equal(c.Site,card.Site);
        // 保存から戻して同じ。
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(restored.DiagnosticSnapshot()));
        // 捕縛の記録を書き換えた保存は読めない。
        var forged=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        forged.WorldClashes=forged.WorldClashes! with{Steps=forged.WorldClashes.Steps.Select(x=>x.Captures==null?x:x with{Captures=null}).ToArray()};
        Assert.Throws<InvalidDataException>(()=>new WorldSession(forged));
        // 留め置いている拠点にいなければ解放できない（本人の軍は戦場にいる）。
        var release=new CaptiveInput(new("request:captive-release"),CaptiveAction.Release,c.Person);
        Assert.Equal(ResultCode.NotAvailable,s.ManageCaptive(release,s.Revision).Code);
        Assert.True(HostageCustody.HeldAt(s.DiagnosticSnapshot(),c.Person,s.Seconds,s.Revision+1));
        // 本人が軍を戻して城に入れば、留め置いている城で処遇を選べる。
        void Send(NewMilitaryAction action){var r=s.DecideNewMilitary(new(new("request:captive-return/"+action),action),s.PlayerControl,s.Revision);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);}
        Send(NewMilitaryAction.Recall);var end=s.DiagnosticSnapshot().NewMilitary!.Orders.Last().Routes.SelectMany(r=>r).Select(l=>l.EndAt).DefaultIfEmpty(s.Seconds).Max();NewGameWorldTests.At(s,end);Send(NewMilitaryAction.Enter);
        Assert.True(Assert.Single(s.Captives()).CanAct);
        var home=ContractJson.Write(s.DiagnosticSnapshot());
        WorldSession Branch()=>new(ContractJson.Read<ContractWorld>(home));
        void SameFuture(WorldSession a)
        {
            var now=a.DiagnosticSnapshot();var back=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(now)));
            foreach(var x in new[]{a,back})NewGameWorldTests.At(x,now.ElapsedSeconds+3600);
            Assert.Equal(ContractJson.Write(a.DiagnosticSnapshot()),ContractJson.Write(back.DiagnosticSnapshot()));
        }
        // 解放：拘束が終わり、その城にいる。
        var result=s.ManageCaptive(release,s.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        Assert.Equal(ResultCode.AlreadyAccepted,s.ManageCaptive(release,s.Revision).Code);
        var freed=s.DiagnosticSnapshot();
        Assert.False(HostageCustody.HeldAt(freed,c.Person,freed.ElapsedSeconds,freed.CommitSequence+1));
        Assert.Equal(c.Site,PersonalCompanions.PhysicalSiteAt(freed,c.Person,freed.ElapsedSeconds,freed.CommitSequence+1));
        Assert.Equal(ResultCode.NotAvailable,s.ManageCaptive(new(new("request:captive-execute-after-release"),CaptiveAction.Execute,c.Person),s.Revision).Code);
        SameFuture(s);
        // 処断：当主の明示の判断で死亡し、死亡記録は処断の操作を指す。死者には何もできない。
        var x=Branch();
        result=x.ManageCaptive(new(new("request:captive-execute"),CaptiveAction.Execute,c.Person),x.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var dead=x.DiagnosticSnapshot();
        Assert.True(Assassination.DeadAt(dead,c.Person,dead.ElapsedSeconds,dead.CommitSequence));
        Assert.Equal(new EntityId("request:captive-execute"),Assert.Single(dead.NaturalDeaths!,d=>d.Person==c.Person).Execution);
        Assert.False(Assert.Single(x.Captives()).Held);
        Assert.Equal(ResultCode.NotAvailable,x.ManageCaptive(new(new("request:captive-release-dead"),CaptiveAction.Release,c.Person),x.Revision).Code);
        var forgedDeath=ContractJson.Read<ContractWorld>(ContractJson.Write(dead));
        forgedDeath.NaturalDeaths=forgedDeath.NaturalDeaths!.Select(d=>d.Person==c.Person?d with{Execution=null}:d).ToArray();
        Assert.Throws<InvalidDataException>(()=>new WorldSession(forgedDeath));
        SameFuture(x);
        // 登用の誘い：本人が判断する。応じれば所属と主従だけが捕らえた家へ移り、断れば同じ捕縛の間は誘い直せない。
        var y=Branch();var before=y.DiagnosticSnapshot();
        result=y.ManageCaptive(new(new("request:captive-recruit"),CaptiveAction.Recruit,c.Person),y.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var joined=y.DiagnosticSnapshot();bool accepted=joined.Captives!.Entries.Last().Accepted;o.WriteLine($"recruit {c.Person.Value} from={c.FromOwner.Value} accepted={accepted} reply={result.VisibleReason}");
        bool Member(ContractWorld w,EntityId house)=>w.Memberships.Any(m=>m.Subject==c.Person&&m.Target==house&&m.From<=w.ElapsedSeconds&&w.ElapsedSeconds<m.Until);
        Assert.True(Member(before,c.FromOwner));
        if(accepted)
        {
            Assert.True(Member(joined,c.CaptorOwner));Assert.False(Member(joined,c.FromOwner));
            Assert.False(HostageCustody.HeldAt(joined,c.Person,joined.ElapsedSeconds,joined.CommitSequence+1));
            Assert.Equal(before.LandRights,joined.LandRights);Assert.Equal(before.Offices,joined.Offices);
        }
        else
        {
            Assert.True(Member(joined,c.FromOwner));Assert.True(Assert.Single(y.Captives()).Declined);
            Assert.Equal(ResultCode.NotAvailable,y.ManageCaptive(new(new("request:captive-recruit-again"),CaptiveAction.Recruit,c.Person),y.Revision).Code);
        }
        var forgedAnswer=ContractJson.Read<ContractWorld>(ContractJson.Write(joined));
        forgedAnswer.Captives=forgedAnswer.Captives! with{Entries=forgedAnswer.Captives.Entries.Select(e=>e with{Accepted=!e.Accepted}).ToArray()};
        Assert.Throws<InvalidDataException>(()=>new WorldSession(forgedAnswer));
        SameFuture(y);
        // 応じた場合の付け替え：元の所属と主従をその時点で閉じ、捕らえた家と誘った当主へ移す。所領・役職は変えない。
        var z=ContractJson.Read<ContractWorld>(home);
        z.Captives=z.Captives! with{Entries=[new(new(new("request:captive-recruit-joined"),CaptiveAction.Recruit,c.Person),z.PlayerControl,c.Site,z.ElapsedSeconds,z.CommitSequence+1,true)]};
        var members=BattleCaptivity.Relations(z,z.Memberships,"membership");var bonds=BattleCaptivity.Relations(z,z.ServiceBonds,"service");
        bool Active(TimedRelation r)=>r.From<=z.ElapsedSeconds&&z.ElapsedSeconds<r.Until;
        Assert.Equal(c.CaptorOwner,Assert.Single(members,r=>r.Subject==c.Person&&Active(r)).Target);
        Assert.Equal(z.PlayerControl,Assert.Single(bonds,r=>r.Subject==c.Person&&Active(r)).Target);
        Assert.All(members.Where(r=>r.Subject!=c.Person),r=>Assert.Contains(r,z.Memberships));
    }
    // 家臣が大将の軍（当主は城に残り、権限と敵対意思は当主のもの）。捕らえた家臣は登用の誘いに本人の判断で答え、当主は家の権限を保つ。
    [Fact]
    public void ARetainerLedArmyLeavesTheHeadAtHomeAndItsCapturedLeaderAnswersRecruitmentHimself()
    {
        var s=NpcArmyTests.AtActualContact(permitted:true,sparse:true,capture:true,retainer:true);
        var m=Assert.Single(s.DiagnosticSnapshot().NpcArmies!.Musters);
        o.WriteLine($"commander={m.Commander.Value} lord={m.Lord?.Value}");
        Assert.Equal(new EntityId("person:nobunaga"),m.Lord);Assert.NotEqual(m.Lord,m.Commander);
        var (_,step)=FightUntilRout(s);Assert.NotNull(step);
        var c=Assert.Single(step.Captures!);Assert.Equal(m.Commander,c.Person);
        var w=s.DiagnosticSnapshot();
        Assert.NotNull(FactionStances.ResolveAuthority(w,m.Lord!.Value,m.Owner,w.ElapsedSeconds,w.CommitSequence));
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(restored.DiagnosticSnapshot()));
        void Send(NewMilitaryAction action){var r=s.DecideNewMilitary(new(new("request:retainer-return/"+action),action),s.PlayerControl,s.Revision);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);}
        Send(NewMilitaryAction.Recall);var end=s.DiagnosticSnapshot().NewMilitary!.Orders.Last().Routes.SelectMany(r=>r).Select(l=>l.EndAt).DefaultIfEmpty(s.Seconds).Max();NewGameWorldTests.At(s,end+1);Send(NewMilitaryAction.Enter);
        // 身代金：相手の当主（城に残った信長）へ書状で求め、相手は開封した時点で払うか決める。銭は返書と一緒に届き、開封で本人の私財に入る。捕虜は自動で解放しない。
        var beforeRansom=ContractJson.Write(s.DiagnosticSnapshot());
        var export=Environment.GetEnvironmentVariable("HAKUTAI_CAPTIVE_QA_DIR");if(!string.IsNullOrEmpty(export)){Directory.CreateDirectory(export);File.WriteAllBytes(Path.Combine(export,"ready.json"),beforeRansom);}
        void Await(WorldSession q){for(int n=0;n<300&&Assert.Single(q.Ransoms()).Stage=="返事待ち";n++){NewGameWorldTests.At(q,q.Seconds+600);foreach(var msg in q.GetActorView(q.PlayerControl).Messages.Where(x=>x.KnownBody==null).ToArray())q.Open(q.PlayerControl,msg.Id,q.Revision);}}
        // 上限を超える額は断られ、銭は動かない。
        var refused=new WorldSession(ContractJson.Read<ContractWorld>(beforeRansom));long refusedMine=NewGameWork.Balance(refused.DiagnosticSnapshot(),refused.PlayerControl);
        Assert.Equal(ResultCode.Accepted,refused.DemandRansom(new("request:ransom-high"),c.Person,2000,refused.Revision).Code);Await(refused);
        Assert.Equal("断り",Assert.Single(refused.Ransoms()).Stage);Assert.Equal(refusedMine,NewGameWork.Balance(refused.DiagnosticSnapshot(),refused.PlayerControl));
        var r=new WorldSession(ContractJson.Read<ContractWorld>(beforeRansom));
        var lord=m.Lord!.Value;long mine0=NewGameWork.Balance(r.DiagnosticSnapshot(),r.PlayerControl),theirs0=NewGameWork.Balance(r.DiagnosticSnapshot(),lord);
        var ask=r.DemandRansom(new("request:ransom"),c.Person,500,r.Revision);Assert.True(ask.Code==ResultCode.Accepted,ask.VisibleReason);
        Assert.Equal(ResultCode.NotAvailable,r.DemandRansom(new("request:ransom-again"),c.Person,400,r.Revision).Code);
        Assert.Equal("返事待ち",Assert.Single(r.Ransoms()).Stage);
        Await(r);
        var paid=r.DiagnosticSnapshot();o.WriteLine($"ransom stage={Assert.Single(r.Ransoms()).Stage} at={paid.ElapsedSeconds} mine={mine0}->{NewGameWork.Balance(paid,r.PlayerControl)} theirs={theirs0}->{NewGameWork.Balance(paid,lord)}");
        Assert.Equal("受け取り",Assert.Single(r.Ransoms()).Stage);
        Assert.Equal(theirs0-500,NewGameWork.Balance(paid,lord));Assert.Equal(mine0+500,NewGameWork.Balance(paid,r.PlayerControl));
        Assert.True(HostageCustody.HeldAt(paid,c.Person,paid.ElapsedSeconds,paid.CommitSequence+1));
        var forgedPay=ContractJson.Read<ContractWorld>(ContractJson.Write(paid));
        forgedPay.Captives=forgedPay.Captives! with{Papers=forgedPay.Captives.Papers.Select(p=>p.Action==RansomAction.Pay?p with{Amount=900}:p).ToArray()};
        Assert.Throws<InvalidDataException>(()=>new WorldSession(forgedPay));
        var rb=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(paid)));
        foreach(var x in new[]{r,rb})NewGameWorldTests.At(x,paid.ElapsedSeconds+3600);
        Assert.Equal(ContractJson.Write(r.DiagnosticSnapshot()),ContractJson.Write(rb.DiagnosticSnapshot()));
        var result=s.ManageCaptive(new(new("request:retainer-recruit"),CaptiveAction.Recruit,c.Person),s.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        var after=s.DiagnosticSnapshot();bool accepted=after.Captives!.Entries.Last().Accepted;o.WriteLine($"accepted={accepted}");
        bool Member(ContractWorld x,EntityId house)=>x.Memberships.Any(r=>r.Subject==c.Person&&r.Target==house&&r.From<=x.ElapsedSeconds&&x.ElapsedSeconds<r.Until);
        Assert.Equal(accepted,Member(after,c.CaptorOwner));Assert.Equal(!accepted,Member(after,c.FromOwner));
        Assert.Equal(accepted,!HostageCustody.HeldAt(after,c.Person,after.ElapsedSeconds,after.CommitSequence+1));
        var back=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(after)));
        foreach(var x in new[]{s,back})NewGameWorldTests.At(x,after.ElapsedSeconds+3600);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(back.DiagnosticSnapshot()));
    }
    // 本人の捕縛（BD-06）：退く途中で本隊が NPC の軍に打ち崩されると本人が捕らえられ、軍務の判断はできず、捕らえた家の当主が城にいれば銭を差し出して解放を願える。
    [Fact]
    public void ThePlayerRoutedWhileWithdrawingIsCapturedAndPleadsForRelease()
    {
        var s=NpcArmyTests.AtActualContact(permitted:true,sparse:true,capture:true,retainer:true,seed:20261026);
        NewGameWorldTests.At(s,s.Seconds+1);
        Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(new(new("request:withdraw"),NewMilitaryAction.Recall),s.PlayerControl,s.Revision).Code);
        BattleCapture? c=null;
        for(int n=0;n<900&&c==null;n++){NewGameWorldTests.At(s,s.Seconds+1);c=s.DiagnosticSnapshot().WorldClashes!.Steps.SelectMany(x=>x.Captures??[]).SingleOrDefault(x=>x.Person==s.PlayerControl);}
        o.WriteLine($"player={s.PlayerControl.Value}");
        Assert.NotNull(c);o.WriteLine($"captured at {c.At} by {c.CaptorOwner.Value} site {c.Site.Value}");
        var w=s.DiagnosticSnapshot();
        Assert.Equal(c,s.MyCaptivity());Assert.Equal(c.Site,PersonalCompanions.PhysicalSiteAt(w,s.PlayerControl,w.ElapsedSeconds,w.CommitSequence+1));
        Assert.Equal(ResultCode.NotAvailable,s.DecideNewMilitary(new(new("request:held-enter"),NewMilitaryAction.Enter),s.PlayerControl,s.Revision).Code);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(restored.DiagnosticSnapshot()));
        var export=Environment.GetEnvironmentVariable("HAKUTAI_CAPTIVE_QA_DIR");if(!string.IsNullOrEmpty(export)){Directory.CreateDirectory(export);File.WriteAllBytes(Path.Combine(export,"held.json"),ContractJson.Write(w));}
        // 差し出す銭が少なければ受け付けない。500文で願えば、当主（城に残った信長）が応じて解放され、銭が当主へ移る。
        Assert.Equal(ResultCode.NotAvailable,s.ManageCaptive(new(new("request:plead-low"),CaptiveAction.Plead,s.PlayerControl,100),s.Revision).Code);
        long mine=NewGameWork.Balance(w,s.PlayerControl),head=NewGameWork.Balance(w,c.Holder);
        var plead=s.ManageCaptive(new(new("request:plead"),CaptiveAction.Plead,s.PlayerControl,500),s.Revision);Assert.True(plead.Code==ResultCode.Accepted,plead.VisibleReason);o.WriteLine(plead.VisibleReason);
        var freed=s.DiagnosticSnapshot();
        Assert.Null(s.MyCaptivity());Assert.Equal(mine-500,NewGameWork.Balance(freed,s.PlayerControl));Assert.Equal(head+500,NewGameWork.Balance(freed,c.Holder));
        var forged=ContractJson.Read<ContractWorld>(ContractJson.Write(freed));
        forged.Captives=forged.Captives! with{Entries=forged.Captives.Entries.Select(e=>e with{Input=e.Input with{Amount=e.Input.Amount+100}}).ToArray()};
        Assert.Throws<InvalidDataException>(()=>new WorldSession(forged));
        var back=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(freed)));
        foreach(var x in new[]{s,back})NewGameWorldTests.At(x,freed.ElapsedSeconds+3600);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(back.DiagnosticSnapshot()));
    }
    // 捕虜の引き換え：本人と相手の家臣が互いに捕らわれたとき、本人は相手の家臣を放つことと引き換えに解放を願い、当主は自分の家の者が戻るなら応じる。二人はそれぞれ留め置かれていた城で放たれる。
    [Fact]
    public void ThePlayerAndAnEnemyRetainerCapturedInOneBattleAreExchanged()
    {
        var s=NpcArmyTests.AtActualContact(permitted:true,sparse:true,capture:true,retainer:true,seed:20261030);
        BattleCapture? Find(EntityId p)=>s.DiagnosticSnapshot().WorldClashes!.Steps.SelectMany(x=>x.Captures??[]).SingleOrDefault(x=>x.Person==p);
        var aide=s.DiagnosticSnapshot().NpcArmies!.Musters.Single().Commander;
        for(int n=0;n<900&&(Find(s.PlayerControl)==null||Find(aide)==null);n++)NewGameWorldTests.At(s,s.Seconds+1);
        var me=Find(s.PlayerControl);var them=Find(aide);o.WriteLine($"me={me?.At} them={them?.At}");
        Assert.NotNull(me);Assert.NotNull(them);
        Assert.True(s.MyCaptivity()!=null&&HostageCustody.HeldAt(s.DiagnosticSnapshot(),aide,s.Seconds,s.Revision+1));
        Assert.Equal(ResultCode.NotAvailable,s.ManageCaptive(new(new("request:exchange-self"),CaptiveAction.Plead,s.PlayerControl,0,s.PlayerControl),s.Revision).Code);
        var r=s.ManageCaptive(new(new("request:exchange"),CaptiveAction.Plead,s.PlayerControl,0,aide),s.Revision);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);o.WriteLine(r.VisibleReason);
        var w=s.DiagnosticSnapshot();
        Assert.Null(s.MyCaptivity());Assert.False(HostageCustody.HeldAt(w,aide,w.ElapsedSeconds,w.CommitSequence+1));
        Assert.Equal(me.Site,PersonalCompanions.PhysicalSiteAt(w,s.PlayerControl,w.ElapsedSeconds,w.CommitSequence+1));
        Assert.Equal(them.Site,PersonalCompanions.PhysicalSiteAt(w,aide,w.ElapsedSeconds,w.CommitSequence+1));
        var forged=ContractJson.Read<ContractWorld>(ContractJson.Write(w));
        forged.Captives=forged.Captives! with{Entries=forged.Captives.Entries.Select(e=>e with{Input=e.Input with{Exchange=null}}).ToArray()};
        Assert.Throws<InvalidDataException>(()=>new WorldSession(forged));
        var back=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
        foreach(var x in new[]{s,back})NewGameWorldTests.At(x,w.ElapsedSeconds+3600);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(back.DiagnosticSnapshot()));
    }
}
