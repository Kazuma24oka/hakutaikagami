using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowClinical()
    {
        if(Session.ClinicalView() is not {} view)return;
        var popup=new PopupPanel{Exclusive=true};AddChild(popup);lifePopup=popup;healthButtons.Clear();verbDialog?.Hide();
        var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);
        var box=new VBoxContainer();panel.AddChild(box);box.AddThemeConstantOverride("separation",10);
        void Text(string text)=>box.AddChild(new Label{Text=text});
        void Act(string label,string key,Func<CommandResult> run,bool disabled=false)
        {
            var button=new Button{Text=label,Disabled=disabled,CustomMinimumSize=new Vector2(380,34)};box.AddChild(button);healthButtons[key]=button;
            button.Pressed+=()=>{var r=run();if(r.Code is ResultCode.Accepted or ResultCode.AlreadyAccepted){popup.Hide();Refresh();ShowClinical();}else Text(r.VisibleReason);};
        }
        long revision=Session.Revision;EntityId Id(string key)=>new("request:clinical-ui/"+key+"/"+revision);
        Text(PersonName(Session.PlayerControl));Text($"疲労 {view.Fatigue}　医術 {view.MedicineExperience}　後遺症 {view.Aftereffect}");
        if(view.TreatmentUntil is {} until)Text($"診療 {Math.Max(0,until-Session.Seconds)/60}分");
        foreach(var c in Session.HealthView(Session.PlayerControl)!.Conditions)
        {
            Text($"{c.Part} {c.Severity}");if(c.Treated)continue;
            Act("手当","self:"+c.Id.Value,()=>Session.ManageHealth(new(Id("self"),Session.PlayerControl,c.Id,Session.PlayerControl),Session.PlayerControl,revision),view.TreatmentUntil!=null);
            foreach(var friend in Session.CompanionView().Where(f=>f.Active&&f.Nearby&&!f.Busy))
                Act(friend.Name+" 手当",friend.Person.Value,()=>Session.ManageHealth(new(Id("care"),Session.PlayerControl,c.Id,friend.Person),Session.PlayerControl,revision),view.TreatmentUntil!=null);
        }
        foreach(var recipient in Session.HealthReportRecipients())Act(PersonName(recipient)+" 病状報告",recipient.Value+"/report",()=>Session.ReportHealth(new(Id("report"),Session.PlayerControl,recipient),Session.PlayerControl,revision));
        var close=new Button{Text="閉じる"};box.AddChild(close);close.Pressed+=()=>popup.Hide();popup.PopupCentered();
    }
}
