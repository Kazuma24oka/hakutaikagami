using Godot;
using System.Linq;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;

public partial class NewGameRuntime
{
    void ShowFamilyNews()
    {
        if(IsSaving || IsClockControlling)return;
        var (panel,revision)=SocialWindow("家族の知らせ");
        var w=Snapshot();var actor=w.PlayerControl;
        bool visible=false;
        var letters=w.Messaging.Messages.Where(m=>m.RecipientId==actor.Value &&
            m.Kind is FamilyNotice.Kind or FamilyNotice.MaturityKind).ToArray();
        foreach(var letter in letters)
        {
            if(MessageFlow.Knows(w.Messaging,letter.Id,actor.Value))
            {
                SocialText(panel,letter.Body);visible=true;
            }
            else if(w.Messaging.Receipts.Any(r=>r.MessageId==letter.Id && r.ReceiverId==actor.Value))
            {
                var open=new Button{Text="届いた封書を開く"};panel.AddChild(open);visible=true;
                open.Pressed+=()=>{CancelAdvancePrefetch();var result=Session.Open(actor,new(letter.Id),revision);
                    message.Text=result.VisibleReason;Refresh();ShowFamilyNews();};
            }
        }
        foreach(var birth in w.Births??[])
        {
            if(birth.FirstParent!=actor && birth.SecondParent!=actor)continue;
            var date=WorldCalendar.At(w.EpochDate,birth.At);
            SocialText(panel,$"{date.Year}年{date.Month}月{date.Day}日、{NewGameWorld.SiteName(birth.Site.Value)}で子が生まれました。");
            visible=true;
        }
        foreach(var person in FamilyBirth.People(w))
        {
            if(FamilyNotice.WitnessedMaturity(w,person,actor))
            {SocialText(panel,person.Name+"の元服に立ち会いました。");visible=true;}
        }
        foreach(var death in w.NaturalDeaths??[])
        {
            if(!FamilyNotice.Knows(w,death,actor) ||
                letters.Any(m=>m.Kind==FamilyNotice.Kind && m.CorrelationId==death.Person.Value && MessageFlow.Knows(w.Messaging,m.Id,actor.Value)))continue;
            SocialText(panel,FamilyBirth.People(w).Single(p=>p.Id==death.Person.Value).Name+"殿のご逝去を見届けました。");visible=true;
        }
        if(!visible)SocialText(panel,"新しい知らせはありません。");
    }
}
