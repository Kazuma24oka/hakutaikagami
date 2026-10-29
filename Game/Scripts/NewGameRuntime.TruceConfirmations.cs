using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void AppendTruceConfirmations(VBoxContainer box,PopupPanel popup,Label feedback,long shown)
    {
        var w=Snapshot();var v=Session.TruceConfirmationView();void Text(string t)=>box.AddChild(new Label{Text=t,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Run(TruceConfirmationInput i){var r=Session.SendTruceConfirmation(i,Session.PlayerControl,shown);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowHouseSuccession();}
        void Button(string text,TruceConfirmationInput i){var b=new Button{Text=text};box.AddChild(b);b.Pressed+=()=>Run(i);}
        foreach(var c in Session.TruceConfirmationCandidates()){Text("不戦原本の元期限："+c.Until+"秒。署名者は現当主「"+SuccessionPerson(w,c.Signer)+"」。受領返報はこの依頼者へ戻します。");Button("現当主の同意で、相手へ確認書を発送",new(new("request:truce-confirm-send-ui/"+shown),TruceConfirmationAction.Send,c.Owner,c.Acceptance));}
        foreach(var c in v.Sent.TakeLast(4)){Text("署名者："+SuccessionPerson(w,c.Sender)+" ／ 宛先："+SuccessionPerson(w,c.Recipient)+" ／ "+c.Status);foreach(var p in c.Retryable)Button("既知の不達原本を同じ宛先へ再送",new(new("request:truce-confirm-retry-ui/"+shown),TruceConfirmationAction.Resend,c.Owner,c.Acceptance,p));}
        foreach(var id in v.Acknowledgeable){var p=w.TruceConfirmations!.Papers.Single(p=>p.Id==id);Button("開封した確認書の受領返報を発送",new(new("request:truce-confirm-receipt-ui/"+shown),TruceConfirmationAction.Acknowledge,p.Terms.A==p.Owner?p.Terms.B:p.Terms.A,p.Acceptance,p.Id));}
        if(v.Sent.Length>0)Text("相手の現在地や現在の受領状況は表示しません。届いた返報は受信箱で開封してください。");
    }
    void ShowTruceConfirmationLetter(EntityId id)
    {
        var known=Branch(Session.PlayerControl).Knowledge.Messages.Single(m=>m.Id==id);if(known.KnownBody==null)throw new InvalidOperationException("未開封の確認書です");var p=ContractJson.Read<TruceConfirmationPaper>(System.Text.Encoding.UTF8.GetBytes(known.KnownBody));var view=Session.TruceConfirmationView();long shown=Session.Revision;
        string line=p.Kind switch{TruceConfirmationKind.Confirmation=>"新しい当主として、先に成立した同じ条件の不戦合意を確認した。元の期限までの約束として扱う。",TruceConfirmationKind.Receipt=>"確認書を受け取り、内容を開封した。先に成立した条件を変える返答ではない。",_=>"確認書を届けられなかったとの返報を受け取った。その後に原本が到達したかどうかは、この報告だけでは分からない。"};
        string detail="第"+p.Terms.Version+"版 ／ 元の不戦期限 "+p.Terms.Until+"秒"+(p.ObservedAt is {} observed?" ／ 観測 "+observed+"秒":"")+"。到達と本人の認識を分けた記録です。";
        PresentWork("不戦確認書と返報",line,detail,p.Sender,p.Sender,"届いた書状の差出人","開封した原本");
        void Run(TruceConfirmationInput input){Submit(Session.SendTruceConfirmation(input,Session.PlayerControl,shown));CloseBottomTalk();}
        var card=view.Sent.SingleOrDefault(c=>c.Root==p.Root);if(card!=null)foreach(var failed in card.Retryable)Choice("truce-confirm-retry","同じ宛先へ元の確認書を再送する",()=>Run(new(new("request:truce-confirm-letter-retry/"+shown),TruceConfirmationAction.Resend,card.Owner,card.Acceptance,failed)));
        if(view.Acknowledgeable.Contains(p.Id))Choice("truce-confirm-receipt","受領したことを返書する",()=>Run(new(new("request:truce-confirm-letter-receipt/"+shown),TruceConfirmationAction.Acknowledge,p.Terms.A==p.Owner?p.Terms.B:p.Terms.A,p.Acceptance,p.Id)));
        Choice("truce-confirm-map","地図へ戻る",CloseBottomTalk);LayoutBottomTalk();
    }
}
