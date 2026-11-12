using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 AcceptDialog? socialDialog;readonly Dictionary<SocialWorldAction,Button> socialReviewButtons=new();
 internal void ReviewSocialClose()=>socialDialog?.Hide();
 internal void ReviewSocialAction(SocialWorldAction action){if(!socialReviewButtons.TryGetValue(action,out var b)||b.Disabled)throw new InvalidOperationException("表示されている用件を選べません");b.EmitSignal(Button.SignalName.Pressed);}
 (VBoxContainer Box,long Revision) SocialWindow(string title){socialDialog?.Hide();socialDialog?.QueueFree();socialReviewButtons.Clear();socialDialog=new(){Title=title,Exclusive=false};AddChild(socialDialog);var scroll=new ScrollContainer{CustomMinimumSize=new(620,400)};socialDialog.AddChild(scroll);var box=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(box);socialDialog.PopupCentered(new(660,470));return(box,Session.Revision);}
 static void SocialText(VBoxContainer box,string text)=>box.AddChild(new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(590,0)});
 void SocialButton(VBoxContainer box,string text,SocialWorldAction action,EntityId person,long rev,Func<long>? amount=null,Func<EntityId?>? carrier=null,EntityId? reference=null){var b=new Button{Text=text};box.AddChild(b);socialReviewButtons[action]=b;b.Pressed+=()=>{if(IsSaving||IsClockControlling)return;CancelAdvancePrefetch();var result=Session.ManageSocial(new(new("request:social-ui/"+Session.Revision+"/"+action),action,person,carrier?.Invoke(),amount?.Invoke()??0,reference),rev);GD.Print("SOCIAL_RESULT "+action+" "+result.Code+" "+result.VisibleReason);message.Text=result.VisibleReason;Refresh();if(result.Code is ResultCode.Accepted or ResultCode.AlreadyAccepted){if(action is SocialWorldAction.Donate or SocialWorldAction.Protect or SocialWorldAction.IssueBan or SocialWorldAction.BreakProtection)ShowSocialTemple();else ShowSocialPeople();}};}
 void ShowSocialPeople(){
  var (box,rev)=SocialWindow("人物と贈答：現地の相手から");var w=Snapshot();var player=Session.PlayerControl;
  var nearby=RegionalCampaign.Nearby(w,player).Where(p=>p.Actor!=player).Select(p=>p.Actor).ToArray();
  var known=w.Characters.Select(p=>p.Id).Where(p=>p!=player&&SocialWorld.Facts(w,player,p).Length>0).Union(nearby).ToArray();
  {}
  if(known.Length>0){var choice=new OptionButton();foreach(var id in known)choice.AddItem(w.Definitions.Characters.Single(c=>c.Id==id).Name+(nearby.Contains(id)?"（現地）":"（以前の面識）"));box.AddChild(choice);
   void Person(){var id=known[choice.Selected];ShowSocialPerson(id);}var b=new Button{Text="この相手との用件"};box.AddChild(b);b.Pressed+=Person;
   // 最初の現地相手を内部操作検査からも同じボタンで選ぶ。
   socialOpenPerson=()=>b.EmitSignal(Button.SignalName.Pressed);
  }else SocialText(box,"まだ現地の相手や面識のある人物がいません。人のいる拠点へ移動してください。");
  foreach(var g in SocialWorld.Gifts(w,player)){var parcel=w.SocialWorld!.Events.Single(e=>e.Input.Id==g.Id).Input.Asset;string contents=parcel is {} id?SocialAssets.Objects(w).Single(o=>o.Id==id).Name:$"銭{g.Amount}文";SocialText(box,$"贈り物：{contents} — {g.Phase}");if(g.Recipient==player&&g.Phase=="運搬・受領待ち"){SocialButton(box,"実物を受け取る",SocialWorldAction.ReceiveGift,g.Sender,rev,reference:g.Id);SocialButton(box,"受取を断る",SocialWorldAction.RefuseGift,g.Sender,rev,reference:g.Id);}if(g.Carrier==player&&g.Phase=="返却待ち")SocialButton(box,"贈り主へ実物を返す",SocialWorldAction.ReturnGift,g.Sender,rev,reference:g.Id);}
 }
 Action? socialOpenPerson;internal void ReviewSocialOpenPerson()=>socialOpenPerson?.Invoke();
 void ShowSocialPerson(EntityId person){var(box,rev)=SocialWindow("現地の用件とこれまでの経緯");var w=Snapshot();var player=Session.PlayerControl;SocialText(box,w.Definitions.Characters.Single(p=>p.Id==person).Name+"\n"+SocialWorld.Impression(w,player,person));foreach(var f in SocialWorld.Facts(w,player,person).TakeLast(8))SocialText(box,$"{WorldCalendar.Display(w.EpochDate,f.LearnedAt)}：{f.Description}");
  SocialButton(box,"現地で会って話す",SocialWorldAction.Visit,person,rev);
  var amount=new SpinBox{MinValue=1,MaxValue=1000,Value=20,Step=1};box.AddChild(amount);{}
  var carriers=SocialWorld.CarrierChoices(w,player);var courier=new OptionButton();foreach(var id in carriers)courier.AddItem(id==player?"本人が運ぶ":w.Definitions.Characters.Single(c=>c.Id==id).Name+"に運搬を任せる");box.AddChild(courier);
  SocialButton(box,"この銭を贈る・運搬を始める",SocialWorldAction.DispatchGift,person,rev,()=>checked((long)amount.Value),()=>carriers[courier.Selected]);{}
  foreach(var rumor in SocialWorld.Renown(w,player).TakeLast(3))SocialButton(box,"自分が知る評判をこの相手に話す",SocialWorldAction.TellRumor,person,rev,reference:new(rumor.Id));
 }
 void ShowSocialTemple(){var(box,rev)=SocialWindow("寺社代表との現地相談");var w=Snapshot();var site=SocialWorld.Site(w,Session.PlayerControl);if(site==null){{}return;}var temple=SocialWorld.Temple(w,site.Value);SocialText(box,temple.Name+"\n"+temple.Kind+"\n寄進・保護・禁制は別々の案件です。寄進で所属や兵・永続的な忠誠は得られません。");
  var amount=new SpinBox{MinValue=1,MaxValue=1000,Value=20,Step=1};box.AddChild(amount);SocialButton(box,"手持ちの銭を現地で寄進",SocialWorldAction.Donate,temple.Representative,rev,()=>checked((long)amount.Value));
  SocialButton(box,"この拠点の保護を取り交わす",SocialWorldAction.Protect,temple.Representative,rev);SocialButton(box,"自分の権限内で禁制を発する",SocialWorldAction.IssueBan,temple.Representative,rev);SocialButton(box,"保護の約束を破棄する（悪評の根拠になる）",SocialWorldAction.BreakProtection,temple.Representative,rev);
  {}foreach(var f in SocialWorld.Facts(w,Session.PlayerControl,temple.Representative))SocialText(box,f.Description+" — "+WorldCalendar.Display(w.EpochDate,f.LearnedAt));
 }
 void ShowSocialRenown(){var(box,rev)=SocialWindow("自分が聞き知った評判");var w=Snapshot();var facts=SocialWorld.Renown(w,Session.PlayerControl);{}if(facts.Length==0){}foreach(var f in facts){string kind=f.Kind switch{RenownKind.Valor=>"武名（修行・腕前の根拠は戦功と別）",RenownKind.Benevolence=>"仁徳",RenownKind.Infamy=>"悪名",_=>"文名"};SocialText(box,$"{kind}：{w.Definitions.Characters.FirstOrDefault(c=>c.Id==f.Subject)?.Name??"既知の相手"}\n{WorldCalendar.Display(w.EpochDate,f.OccurredAt)}の出来事を{WorldCalendar.Display(w.EpochDate,f.LearnedAt)}に認識\n聞いた地域：{w.Definitions.Sites.Single(s=>s.Id==f.Region).Name}\n出典：本人が得た現地記録・相手から聞いた伝聞\n{SocialWorld.RenownImpression(w,Session.PlayerControl,f.Subject,f.Kind,f.Region)}");}}
}
