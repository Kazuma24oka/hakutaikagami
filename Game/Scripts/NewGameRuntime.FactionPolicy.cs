using Godot;
using System;
using System.Linq;
using System.IO;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    const long FactionPolicySeconds=30*86400;
    EntityId? OwnPoliticalHouse(ContractWorld w)
    {
        if(!WorldClashes.Enabled(w))return null;var f=w.NewGame!.Start.Factions.SingleOrDefault(f=>f.Head==w.PlayerControl.Value);if(f==null)return null;var owner=new EntityId(f.Id);return FactionStances.ResolveAuthority(w,w.PlayerControl,owner,w.ElapsedSeconds,w.CommitSequence)==null?null:owner;
    }
    static FactionStanceDecision? WithdrawablePolicy(ContractWorld w,EntityId target)=>w.FactionStances?.Decisions.LastOrDefault(d=>d.Authority.Issuer==w.PlayerControl&&d.Input.Action==FactionStanceAction.DeclareHostile&&d.Input.Target==target&&d.Input.Until>w.ElapsedSeconds&&!w.FactionStances.Decisions.Any(x=>x.Input.Action==FactionStanceAction.WithdrawOwnDeclaration&&x.Input.Original==d.Input.Id));
    void OpenFactionPolicy()
    {
        var w=Snapshot();if(OwnPoliticalHouse(w) is not {} owner)return;var targets=w.Definitions.Organizations.Where(o=>o.Id!=owner).OrderBy(o=>o.Name,StringComparer.Ordinal).ToArray();
        var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var box=new VBoxContainer{CustomMinimumSize=new(480,210)};panel.AddChild(box);
        box.AddChild(new Label{Text="自家の対外方針",HorizontalAlignment=HorizontalAlignment.Center});
        var target=new OptionButton();target.AddItem("対象勢力を選択");foreach(var t in targets)target.AddItem(t.Name);box.AddChild(target);
        var mode=new OptionButton();mode.AddItem("敵対意思を表明して通知する");mode.AddItem("自家の敵対意思を撤回して通知する");box.AddChild(mode);
        var detail=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(460,70)};box.AddChild(detail);var confirm=new Button{Text="この方針で通知を発送",Disabled=true};box.AddChild(confirm);
        void RefreshPolicy(){var current=Snapshot();int n=target.Selected-1;bool chosen=n>=0&&n<targets.Length;bool withdraw=mode.Selected==1;bool possible=chosen&&OwnPoliticalHouse(current)==owner;
            if(possible)possible=withdraw?WithdrawablePolicy(current,targets[n].Id)!=null:!FactionStances.KnownOwnDeclarations(current,w.PlayerControl).Any(c=>c.ToOwner==targets[n].Id&&c.EffectiveFrom<current.ElapsedSeconds+FactionPolicySeconds&&current.ElapsedSeconds<c.Until);
            confirm.Disabled=!possible;detail.Text=withdraw?"自家の方針を撤回し、相手の既知の本拠へ書状を送ります。\n相手が既に出発していれば、通知が届かない場合があります。":"30日間の敵対意思を記録し、相手の既知の本拠へ書状を送ります。\n相手が知り、どのように応じるかは、到達した後の判断です。";
            if(chosen&&!possible)detail.Text+="\n現在の記録では、この変更を行えません。";
        }
        target.ItemSelected+=_=>RefreshPolicy();mode.ItemSelected+=_=>RefreshPolicy();
        confirm.Pressed+=()=>{
            int n=target.Selected-1;if(n<0||n>=targets.Length)return;var current=Snapshot();if(OwnPoliticalHouse(current)!=owner)throw new InvalidDataException("自家の権限が変わりました");bool withdraw=mode.Selected==1;var original=withdraw?WithdrawablePolicy(current,targets[n].Id):null;if(withdraw&&original==null)throw new InvalidDataException("撤回する自家の方針がありません");
            var id=new EntityId("decision:faction-ui/"+Session.Revision);var decision=new FactionStanceInput(id,withdraw?FactionStanceAction.WithdrawOwnDeclaration:FactionStanceAction.DeclareHostile,owner,targets[n].Id,Session.Seconds,withdraw?0:checked(Session.Seconds+FactionPolicySeconds),withdraw?original!.Input.Id:null);
            var input=new FactionProclamationInput(decision,new(new("request:faction-ui/"+Session.Revision),withdraw?original!.Input.Id:id));popup.Hide();Submit(Session.ProclaimFactionStance(input,Session.PlayerControl,Session.Revision));
        };
        var back=new Button{Text="戻る"};back.Pressed+=()=>popup.Hide();box.AddChild(back);popup.PopupHide+=()=>popup.QueueFree();RefreshPolicy();popup.PopupCentered(new Vector2I(540,310));
    }
    void ShowFactionLetter(EntityId id)
    {
        var known=Branch(Session.PlayerControl).Knowledge.Messages.Single(m=>m.Id==id);if(known.KnownBody==null)throw new InvalidDataException("本人が開封した書状ではありません");var w=Snapshot();var m=w.Messaging.Messages.Single(m=>m.Id==id.Value);if(m.Kind!=FactionNotices.Kind)throw new InvalidDataException("対外方針の通知ではありません");
        var claim=ContractJson.Read<FactionStanceClaim>(System.Text.Encoding.UTF8.GetBytes(known.KnownBody));string from=w.Definitions.Organizations.Single(o=>o.Id==claim.FromOwner).Name,to=w.Definitions.Organizations.Single(o=>o.Id==claim.ToOwner).Name;long left=Math.Max(0,claim.Until-w.ElapsedSeconds);
        string speech=left==0?from+"の、"+to+"に対する敵対意思は、この通知に記載された期間を終えている。":from+"は"+to+"への敵対意思を表明する。記載された期限までは、あと約"+Math.Max(1,left/3600+(left%3600==0?0:1))+"時間。";
        PresentWork("対外方針の書状",speech,"開始から"+claim.ObservedAt+"秒時点の通知。現在の行動は別途確認する。",new(m.SenderId),new(m.SenderId),"開封した書状","確認した記録");Choice("work-faction-map","地図へ戻る",CloseBottomTalk);LayoutBottomTalk();
    }
}
