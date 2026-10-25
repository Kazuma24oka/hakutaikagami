using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowServiceDirection(EntityId principal)
    {
        var w=Snapshot();var v=NewGameService.View(w);var card=v.Cases.LastOrDefault(c=>c.Principal==principal);if(card==null)return;shownWork=null;
        bool leaving=w.Service?.Requests.Any(q=>q.Case==card.Case&&q.Input.Action==CareerAction.RequestRelease)==true;
        bool ended=v.Principal!=principal&&card.Status.Contains("辞去成立を確認",StringComparison.Ordinal);
        bool waiting=card.Status.Contains("引継ぎの返報待ち",StringComparison.Ordinal);
        string speech=ended?"引継ぎと辞去の成立を確認した。これまでの働きは記録に残しておく。次の行先は自分で決めてくれ。":waiting?"辞去の願いは受け取った。預けた兵や兵糧、任務の返報を確認してから、引継ぎを終えよう。":leaving?"辞去願いの返答を待とう。正式な返答を確認するまでは、今の所属と預かり物をそのまま扱う。":"今の役目を続けるか、別の身の振り方を望むか。辞去を願うなら、預かり物と返報の引継ぎを確認しよう。";
        bool otherSpeaker=!leaving||waiting||ended;
        PresentWork("今後の身の振り方",speech,card.Status+"\n預り物と役目の引継ぎ、辞去の成立、本人による通知の確認は順に行います。",principal,otherSpeaker?principal:Session.PlayerControl,ended?"確認済みの辞去":waiting?"確認済みの返答":"本人の判断","どう働きますか");workCardHeading.Text=PersonName(principal);
        if(!leaving&&card.CanRelease){
            var expected=Session;long rev=Session.Revision;
            Choice("work-service-release","辞去を願い出たい",()=>{if(!ReferenceEquals(Session,expected)||Session.Revision!=rev)throw new InvalidOperationException("状況が変わりました。所属を確認し直してください。");Submit(Session.DecideService(new(new("request:service-talk/"+rev),CareerAction.RequestRelease,card.Case),Session.PlayerControl,rev));ShowServiceDirection(principal);});
            Choice("work-service-continue","今の主君のもとで働こう",()=>{CloseBottomTalk();ShowWorkContact(principal);});
        }
        else if(leaving&&!ended){
            var military=NewGameMilitary.View(w);if(military.Custody||military.Returning)Choice("work-service-handover","軍務の引継ぎを確認しよう",()=>ShowMilitaryWork());
            else if(KnownWork()!=null)Choice("work-service-report","任務の返報を確認しよう",ShowKnownWork);
        }
        Choice("work-service-map",ended?"次の行先を考えよう":"地図へ戻る",CloseBottomTalk);
    }
}
