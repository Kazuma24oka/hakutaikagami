using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void BuildWorkRelations(VBoxContainer stack)=>Add(stack,"work-contact","仕事で縁のできた相手と相談",()=>ShowWorkContact());
    WorkContact? LocalWorkContact(EntityId? selected=null)
    {
        var contacts=WorkRelations.Known(Snapshot()).Where(c=>c.Local);if(selected is {} id)return contacts.SingleOrDefault(c=>c.Other==id);
        var lord=NewGameService.View(Snapshot()).Principal;return contacts.OrderByDescending(c=>c.Other==lord).ThenByDescending(c=>c.Proofs[0].LearnedAt).ThenBy(c=>c.Other.Value,StringComparer.Ordinal).FirstOrDefault();
    }
    void ShowWorkContact(EntityId? selected=null)
    {
        if(IsSaving||IsClockControlling)return;var c=LocalWorkContact(selected);if(c==null)throw new System.IO.InvalidDataException("確認した縁のある相手と現地で面会してください");
        var w=Snapshot();var service=NewGameService.View(w);var card=service.Cases.LastOrDefault(s=>s.Principal==c.Other);shownWork=null;
        string speech=card?.CanAccept==true?"仕官の条件は返書のとおりだ。引き受けるなら、改めて返答してほしい。":c.CanDiscussService?"これまでの働きは知っている。これからの身の振り方も相談してくれ。仕官を望むなら、条件を返書で示そう。":c.CanDiscussMilitary?"これまでの仕事のことは覚えている。今後の役目や軍務についても、相談してくれ。":"これまでの受渡しは覚えている。次の仕事についても相談してくれ。";
        var p=c.Proofs[0];PresentWork("仕事を通じた縁",speech,"確認したこと："+p.Description+"\n出来事：開始"+p.EventAt+"秒／本人の確認："+p.LearnedAt+"秒。",c.Other,c.Other,"現地での相談","今後の働き方");workCardHeading.Text=PersonName(c.Other);
        if(card?.CanAccept==true){ContactServiceChoice("work-contact-accept","返書の条件で仕えよう",CareerAction.Accept,c.Other,card.Case);ContactServiceChoice("work-contact-decline","今回は仕官を辞退しよう",CareerAction.Decline,c.Other,card.Case);}
        else {
            if(c.CanDiscussService)ContactServiceChoice("work-contact-apply","仕官について相談したい",CareerAction.Apply,c.Other,null);
            else if(c.CanDiscussMilitary){Choice("work-contact-service","今後の身の振り方を相談したい",()=>ShowServiceDirection(c.Other));if(WorkDuty.Candidate(w)==c.Other)Choice("work-contact-duty","今後の点検の役目を聞きたい",()=>ShowWorkDutyOffer(c.Other));else Choice("work-contact-military","軍務について相談したい",()=>ShowMilitaryWork());}
            var offer=TransportWork.OfferAt(w,new("job:contact-preview"));if(!c.CanDiscussMilitary&&offer?.Issuer==c.Other)Choice("work-contact-transport","運搬の仕事を続けたい",()=>{CloseBottomTalk();if(TransportWork.Jobs(Snapshot()).LastOrDefault() is {} last&&TransportHandover.Validate(last.Handover).Phase is HandoverPhase.Paid or HandoverPhase.Returned or HandoverPhase.Cancelled)Submit(Session.RequestTransport(new("job:contact/"+Session.Revision),Session.Revision));ShowTransportWork();});
        }
        Choice("work-contact-back","地図へ戻る",CloseBottomTalk);
    }
    void ContactServiceChoice(string id,string text,CareerAction action,EntityId principal,EntityId? caseId)
    {
        var expected=Session;long revision=Session.Revision;
        Choice(id,text,()=>{if(!ReferenceEquals(Session,expected)||Session.Revision!=revision)throw new InvalidOperationException("状況が変わりました。相手と条件を確認し直してください。");var requestId=new EntityId("request:work-contact/"+revision);Submit(Session.DecideService(new(requestId,action,caseId,action==CareerAction.Apply?principal:null),Session.PlayerControl,revision));if(action==CareerAction.Apply)FocusServiceCase(requestId);CloseBottomTalk();});
    }
}
