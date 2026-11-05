using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 readonly Dictionary<string,Button> brokerReviewButtons=new();
 internal void ReviewBrokerPress(string key){var b=brokerReviewButtons[key];if(b.Disabled)throw new InvalidOperationException(key);b.EmitSignal(Button.SignalName.Pressed);}
 void ShowBrokerWork(){if(IsSaving||IsClockControlling)return;var(box,rev)=SocialWindow("口入れ：書状と買付");brokerReviewButtons.Clear();var w=Snapshot();var actor=Session.PlayerControl;var site=SocialWorld.Site(w,actor);if(site==null){SocialText(box,"現地へ到着してから相談してください。");return;}
 foreach(var detail in AbilityEffects.KnownInspection(Session,actor))SocialText(box,detail);
 EntityId Nonce(string key)=>new("request:broker-ui/"+rev+"/"+key);
 void Button(string key,string text,BrokerInput i){var b=new Button{Text=text};box.AddChild(b);brokerReviewButtons[key]=b;b.Pressed+=()=>{if(IsSaving||IsClockControlling)return;CancelAdvancePrefetch();var result=Session.ManageBroker(i,rev);message.Text=result.VisibleReason;Refresh();ShowBrokerWork();};}
 var jobs=BrokerWork.Jobs(w).Where(j=>j.Terms.Worker==actor&&!BrokerWork.Closed(j)).ToArray();
 foreach(var j in jobs){SocialText(box,AbilityEffects.Description(j.Terms.Abilities));if(j.Terms.Abilities is {} abilities&&j.Terms.Kind==BrokerKind.Purchase&&w.ElapsedSeconds<abilities.ReadyAt)SocialText(box,"買付の準備完了まで "+(abilities.ReadyAt-w.ElapsedSeconds)+"秒。時間を進めてから市場で受け取れます。");SocialText(box,$"引受：{(j.Terms.Kind==BrokerKind.Letter?"封書の使い（本文非公開）":"木材の買付納品")}\n宛先：{NewGameWorld.SiteName(j.Terms.Destination.Value)}／{MeritRewards.DisplayName(w,actor,j.Terms.Recipient)}\n状態：{j.Phase}　期限：{j.Terms.Until/86400}日目\n預り金：{j.Cash}文・預り木材：{j.Cargo}kg\n期限内の納品と受領証の持帰りで報酬{j.Terms.Fee}文。私財とは別です。");
 void Act(string key,string title,BrokerAction a)=>Button(key,title,new(Nonce(key),a,j.Terms.Id));
 if(j.Terms.Kind==BrokerKind.Purchase&&j.Phase=="引受中")Act("buy","現地の市場で預り金を使って買う",BrokerAction.Buy);
 if(j.DeliveredAt==null)Act("deliver","指定の相手へ現物を手渡して受領証を受け取る",BrokerAction.Deliver);
 else Act("settle","依頼主へ受領証と残金を渡して精算する",BrokerAction.Settle);
 if(j.DeliveredAt==null&&j.Cargo==0)Act("return","引受を取りやめ、封書・預り金を現地で返す",BrokerAction.Return);
 }
 if(jobs.Length==0){SocialText(box,"面識がなくても、小口の仕事を相談できます。掲示は現地の依頼主が用意したものです。");foreach(bool personal in new[]{false,true})foreach(var kind in new[]{BrokerKind.Letter,BrokerKind.Purchase}){var id=Nonce((personal?"personal-":"small-")+kind);foreach(var offer in BrokerWork.Offers(w,actor,id,kind,personal)){string key=(personal?"personal-":"small-")+kind+"-"+offer.Issuer.Value.Replace(':','-');Button(key,$"{(personal?"個別相談":"小口")}：{MeritRewards.DisplayName(w,actor,offer.Issuer)}／{(kind==BrokerKind.Letter?"封書の使い":"木材"+offer.Quantity+"kgの買付")}／報酬{offer.Fee}文",new(id,BrokerAction.Accept,Issuer:offer.Issuer,Kind:kind,Personal:personal));}}}
 }
}
