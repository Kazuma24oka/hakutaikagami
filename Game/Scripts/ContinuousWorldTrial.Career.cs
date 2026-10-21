using Godot;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    CareerCard? CareerCard=>Career.View(S,S.PlayerControl).Cases.LastOrDefault();
    void CareerDecision(CareerAction action){var card=CareerCard;Require(S.DecideCareer(new(new("request:career-ui/"+S.Revision),action,action==CareerAction.Apply?null:card?.Case),S.PlayerControl,S.Revision));}
    void BuildCareerUI(HFlowContainer row)
    {
        Add(row,"career-apply","仕官を申し出る",()=>CareerDecision(CareerAction.Apply));
        Add(row,"career-open","身分の封書を開く",()=>{var card=Career.View(S,S.PlayerControl).Cases.First(c=>c.Unopened!=null);Require(S.Open(S.PlayerControl,card.Unopened!.Value,S.Revision));});
        Add(row,"career-accept","条件に同意して回答",()=>CareerDecision(CareerAction.Accept));
        Add(row,"career-decline","仕官を辞退して回答",()=>CareerDecision(CareerAction.Decline));
        Add(row,"career-release","辞去を願い出る",()=>CareerDecision(CareerAction.RequestRelease));
        Add(row,"career-status","身分を照会する",()=>CareerDecision(CareerAction.Inquire));
    }
    void RefreshCareerUI(ContractWorld w)
    {
        var view=Career.View(S,S.PlayerControl);var card=view.Cases.LastOrDefault();
        buttons["career-apply"].Visible=view.CanApply;buttons["career-apply"].Disabled=!w.Locations.Any(l=>l.Actor==S.PlayerControl&&l.Kind==LocationKind.AtSite&&l.Site==ContractFixture.Castle)||!RegionalWork.NearbyDispatchers(S).Contains(RetainerStart.Lord);
        buttons["career-open"].Visible=view.Cases.Any(c=>c.Unopened!=null);buttons["career-accept"].Visible=card?.CanAccept==true;buttons["career-decline"].Visible=card?.CanDecline==true;buttons["career-release"].Visible=card?.CanRelease==true;buttons["career-status"].Visible=card!=null&&!view.CanApply&&!view.Cases.Any(c=>c.Unopened!=null);
        var inbox=RegionalWork.Inbox(S);buttons["work-open"].Visible=inbox.Any(c=>c.CanOpen);buttons["work-accept"].Visible=buttons["work-decline"].Visible=inbox.Any(c=>c.CanDecide);buttons["retainer-interrupt"].Visible=KnownRetainerTask()!=null;buttons["retainer-explain"].Visible=OwnInterruptedTask()!=null;
        workRole.Text=w.Definitions.Characters.Single(c=>c.Id==S.PlayerControl).Name+" / "+view.Role;
        workText.Text="身分："+(card?.Status??"まだ申し出はありません")+" / 所属・直属と軍や財産は別";
        if(inbox.Length>0)workText.Text+="\n"+MissionText(inbox.Last().Text);
        details.Text="同じ人物・時計・世界で継続 / 合意の効力と本人の通知認識を区別 / 失職しても人生は続きます";
    }
}
