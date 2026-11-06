using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;

public partial class NewGameRuntime
{
    void ShowPersonalContact()=>RenderPersonalContact("");
    void RenderPersonalContact(string response)
    {
        if(IsSaving||IsClockControlling)return;
        var (panel,revision)=SocialWindow("人物の所在・接触");
        var world=Snapshot();
        var actor=world.PlayerControl;
        schemeReviewButtons.Clear();
        if(response.Length>0)SocialText(panel,response);
        if(Assassination.CaptivityAt(world,actor,world.ElapsedSeconds,revision+1)!=null)
        {
            SocialText(panel,"捕縛されています。"); return;
        }
        var site=SocialWorld.Site(world,actor);
        if(site==null){SocialText(panel,"現地へ到着してから行動してください。");return;}
        void Offer(string key,string caption,SchemeAction action,EntityId target,EntityId? source=null)
        {
            var button=new Button{Text=caption}; panel.AddChild(button); schemeReviewButtons[key]=button;
            button.Pressed+=()=>
            {
                CancelAdvancePrefetch();
                var input=new SchemeInput(new("request:contact-ui/"+revision+"/"+key),action,actor,target,Reference:source);
                var result=Session.ManageScheme(input,revision);
                Refresh();
                RenderPersonalContact(result.Code==ResultCode.Accepted?"":result.VisibleReason);
            };
        }
        var present=world.Characters.Where(c=>c.Id!=actor && c.Life!=LifeState.Dead && SocialWorld.Site(world,c.Id)==site).ToArray();
        foreach(var character in present)
        {
            var person=character.Id;
            var key=person.Value.Replace(':','-');
            var name=MeritRewards.DisplayName(world,actor,person);
            var sight=Assassination.Sightings(world,actor).LastOrDefault(q=>q.Person==person && q.Site==site);
            if(sight==null){Offer("see-"+key,name+"の所在を確かめる",SchemeAction.ObservePerson,person);continue;}
            var previous=world.Schemes!.Events.LastOrDefault(e=>e.Actor==actor && e.Input.Subject==person && e.Input.Action==SchemeAction.SeekContact);
            if(previous==null||previous.Input.Reference!=sight.Original)
                Offer("seek-"+key,name+"に近づく",SchemeAction.SeekContact,person,sight.Original);
            else Offer("strike-"+key,name+"に暗殺を仕掛ける",SchemeAction.AttemptAssassination,person,previous.Input.Id);
        }
        var history=world.Schemes?.Events??[];
        foreach(var row in history.Where(e=>e.Actor==actor && Assassination.IsConclusion(e.Input.Action)).TakeLast(1))
            SocialText(panel,row.Input.Action switch{SchemeAction.FatalEncounter=>"相手の死亡を確認しました。",SchemeAction.EscapedEncounter=>"護衛との接触後、現場を離れました。",_=>"現地で捕縛されました。"});
        foreach(var prisoner in history.Where(e=>e.Input.Action==SchemeAction.CaughtInEncounter && e.Input.Subject==actor &&
            e.Site==site && Assassination.CaptivityAt(world,e.Actor,world.ElapsedSeconds,revision+1)?.Input.Id==e.Input.Id))
            Offer("release-"+prisoner.Actor.Value.Replace(':','-'),MeritRewards.DisplayName(world,actor,prisoner.Actor)+"を解放する",SchemeAction.ReleaseCaptive,prisoner.Actor,prisoner.Input.Id);
        foreach(var last in Assassination.Sightings(world,actor).GroupBy(q=>q.Person).Select(g=>g.Last()))
            SocialText(panel,MeritRewards.DisplayName(world,actor,last.Person)+"の最終目撃　"+NewGameWorld.SiteName(last.Site.Value)+"・開始から"+last.ObservedAt/86400+"日目");
    }
}