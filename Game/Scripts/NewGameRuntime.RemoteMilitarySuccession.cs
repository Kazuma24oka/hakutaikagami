using System;
using System.Linq;
using Godot;
using HakutaiKagami.Core.Contracts;

public partial class NewGameRuntime
{
    void AppendRemoteMilitary(VBoxContainer box,PopupPanel popup,Label feedback,long shown)
    {
        var w=Snapshot();if(w.NewMilitary==null)return;var root=NewGameMilitary.Cases(w).Last().Id;
        void Run(RemoteMilitaryInput i){var result=Session.SendRemoteMilitary(i,Session.PlayerControl,shown);if(result.Code!=ResultCode.Accepted){feedback.Text=result.VisibleReason;return;}popup.Hide();Refresh();ShowHouseSuccession();}
        if(Session.CanAgreeActiveMilitary()){
            box.AddChild(new Label{Text="遠隔で受け取る指揮書状を現当主と手配。指定拠点で待ち合わせます。開封後も、承諾するまで旧指揮のままです。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
            var sites=BranchRoutes.GraphNodes(w).ToArray();var list=new OptionButton();foreach(var site in sites)list.AddItem(NewGameWorld.SiteName(RegionalCampaign.Site(site.Id).Value));box.AddChild(list);
            var hours=new SpinBox{MinValue=1,MaxValue=168,Value=24,Step=1,Suffix="時間後"};box.AddChild(hours);var button=new Button{Text="この拠点・待合時刻へ署名書状を発送"};box.AddChild(button);button.Pressed+=()=>Run(new(new("request:remote-military-ui/"+shown),RemoteMilitaryAction.Issue,root,RegionalCampaign.Site(sites[list.Selected].Id),Session.Seconds+(long)hours.Value*3600));
        }
        foreach(var paper in w.NewMilitary.RemoteSuccession?.Papers??[]){var i=new RemoteMilitaryInput(new("request:remote-retry-ui/"+shown),RemoteMilitaryAction.Resend,root,Original:paper.Id);if(!NewGameMilitary.CanSendRemote(w,i))continue;var b=new Button{Text="本人が不達を知った軍務書状を同じ宛先へ再送"};box.AddChild(b);b.Pressed+=()=>Run(i);}
    }
    void ShowRemoteMilitaryLetter(EntityId id)
    {
        var known=Branch(Session.PlayerControl).Knowledge.Messages.Single(m=>m.Id==id);if(known.KnownBody==null)return;var p=ContractJson.Read<RemoteMilitaryPaper>(System.Text.Encoding.UTF8.GetBytes(known.KnownBody));long shown=Session.Revision;
        string line=p.Kind switch{RemoteMilitaryKind.Offer=>"同じ預かり軍について、新当主を発令者とする指揮原本を確認してほしい。兵や兵糧の再貸与ではない。承諾した時点から切り替わる。",RemoteMilitaryKind.Receipt=>"引継ぎ書状を開封し、同じ軍の指揮原本を承諾した。",_=>"指揮書状が届かなかったとの返報を受け取った。その後の到達は、この報告だけでは分からない。"};
        PresentWork("軍務の指揮引継ぎ",line,"署名 "+p.Signature.At+"秒 ／ 待合 "+NewGameWorld.SiteName(p.Address.Value)+"・"+p.MeetAt+"秒 ／ 承諾期限 "+p.Until+"秒"+(p.ObservedAt is {} at?" ／ 観測 "+at+"秒":""),p.Sender,p.Sender,"書状の差出人","本人が開封した原本");
        var input=new RemoteMilitaryInput(new("request:remote-letter-ui/"+shown),RemoteMilitaryAction.Accept,p.Case,Original:p.Id);if(NewGameMilitary.CanSendRemote(Snapshot(),input))Choice("remote-accept","同じ軍の指揮引継ぎを承諾する",()=>{Submit(Session.SendRemoteMilitary(input,Session.PlayerControl,shown));CloseBottomTalk();});
        Choice("remote-map","地図へ戻る",CloseBottomTalk);LayoutBottomTalk();
    }
}
