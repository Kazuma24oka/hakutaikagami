using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;
namespace HakutaiKagami.Tests;

// 合戦での捕縛（BD-44、2026-11-13）：崩れた敵の軍の大将を捕らえ、拘束中は動けず、当主が明示に解放する。
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
    public void ARoutedCommanderIsCapturedHeldAndReleasedOnlyByTheCaptorAndRestoresTheSameFuture()
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
        // 本人が軍を戻して城に入れば、留め置いている城で解放できる。解放された人はその城にいる。
        void Send(NewMilitaryAction action){var r=s.DecideNewMilitary(new(new("request:captive-return/"+action),action),s.PlayerControl,s.Revision);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);}
        Send(NewMilitaryAction.Recall);var end=s.DiagnosticSnapshot().NewMilitary!.Orders.Last().Routes.SelectMany(r=>r).Select(l=>l.EndAt).DefaultIfEmpty(s.Seconds).Max();NewGameWorldTests.At(s,end);Send(NewMilitaryAction.Enter);
        var result=s.ManageCaptive(release,s.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);
        Assert.Equal(ResultCode.AlreadyAccepted,s.ManageCaptive(release,s.Revision).Code);
        var freed=s.DiagnosticSnapshot();
        Assert.False(HostageCustody.HeldAt(freed,c.Person,freed.ElapsedSeconds,freed.CommitSequence+1));
        Assert.Equal(c.Site,PersonalCompanions.PhysicalSiteAt(freed,c.Person,freed.ElapsedSeconds,freed.CommitSequence+1));
        var back=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(freed)));
        foreach(var x in new[]{s,back})NewGameWorldTests.At(x,freed.ElapsedSeconds+3600);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(back.DiagnosticSnapshot()));
    }
}
