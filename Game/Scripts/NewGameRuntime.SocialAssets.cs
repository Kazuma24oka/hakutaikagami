using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 readonly Dictionary<string,Button> objectReviewButtons=new();
 internal void ReviewObjectPress(string key){if(!objectReviewButtons.TryGetValue(key,out var b)||b.Disabled)throw new InvalidOperationException("この用件は選べません："+key);b.EmitSignal(Button.SignalName.Pressed);}
 void ObjectButton(VBoxContainer box,string key,string label,Action action){var b=new Button{Text=label};box.AddChild(b);objectReviewButtons[key]=b;b.Pressed+=action;}
 void AssetCommand(SocialAssetInput input,long revision){if(IsSaving||IsClockControlling)return;CancelAdvancePrefetch();var result=Session.ManageSocialAsset(input,revision);message.Text=result.VisibleReason;GD.Print("ASSET_RESULT "+input.Action+" "+result.Code+" "+result.VisibleReason);Refresh();ShowPersonalObjects();}
 void ShowPersonalObjects()
 {
  objectReviewButtons.Clear();var(box,rev)=SocialWindow("私有物・紹介状・茶会");var w=Snapshot();var actor=Session.PlayerControl;var site=SocialWorld.Site(w,actor);
  if(site==null){{}return;}
  {}
  var known=w.Characters.Select(c=>c.Id).Where(c=>c!=actor&&(SocialWorld.Facts(w,actor,c).Length>0||SocialWorld.Site(w,c)==site)).ToArray();var people=new OptionButton();foreach(var p in known)people.AddItem(w.Definitions.Characters.Single(c=>c.Id==p).Name);box.AddChild(people);
  EntityId Person()=>known[people.Selected];EntityId Nonce(string kind)=>new("request:asset-ui/"+rev+"/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(kind))[..24]);
  if(known.Length>0){
   ObjectButton(box,"tea-plan","選んだ相手へ1時間後の茶会の招待状を書く",()=>AssetCommand(new(Nonce("plan"),SocialAssetAction.PlanTea,Person(),When:w.ElapsedSeconds+3600),rev));
   var writers=known.Where(p=>SocialWorld.Site(w,p)==site&&SocialWorld.Facts(w,p,actor).Length>0).ToArray();if(writers.Length>0){var writer=new OptionButton();foreach(var p in writers)writer.AddItem(w.Definitions.Characters.Single(c=>c.Id==p).Name+"に紹介を頼む");box.AddChild(writer);ObjectButton(box,"intro-draft","選んだ相手宛ての紹介状を頼む",()=>AssetCommand(new(Nonce("intro"),SocialAssetAction.DraftIntroduction,writers[writer.Selected],Person()),rev));}
  }
  SocialText(box,"手元の私有物");var goods=SocialAssets.Inventory(w,actor);
  foreach(var obj in goods){SocialText(box,obj.Name+" ×"+obj.Quantity+"\n"+string.Join(" → ",obj.History.TakeLast(3)));
   if(known.Length>0)ObjectButton(box,"give-"+obj.Kind,"選んだ相手へこの実物を贈る・封書を渡す",()=>{if(IsSaving||IsClockControlling)return;CancelAdvancePrefetch();var r=Session.ManageSocial(new(Nonce("give/"+obj.Id.Value),SocialWorldAction.DispatchGift,Person()){Asset=obj.Id},rev);message.Text=r.VisibleReason;GD.Print("ASSET_GIFT "+r.Code+" "+r.VisibleReason);Refresh();ShowPersonalObjects();});
   if(obj.Kind=="introduction"&&known.Length>0)ObjectButton(box,"intro-present","選んだ宛先本人にこの紹介状を提示",()=>AssetCommand(new(Nonce("present/"+obj.Id.Value),SocialAssetAction.PresentIntroduction,Person(),obj.Id),rev));
   if(obj.Kind=="invitation"){var p=w.SocialAssets!.Events.Single(e=>e.Input.Id==obj.Id);if(p.Input.Person==actor){ObjectButton(box,"tea-accept","この招待を承諾する（現地への移動は別）",()=>AssetCommand(new(Nonce("accept/"+obj.Id.Value),SocialAssetAction.AcceptTea,p.Actor,obj.Id),rev));ObjectButton(box,"tea-decline","招待を断る",()=>AssetCommand(new(Nonce("decline/"+obj.Id.Value),SocialAssetAction.DeclineTea,p.Actor,obj.Id),rev));}}
  }
  if(w.Trade!=null)foreach(var deal in w.Trade.Deals.Where(d=>d.Phase==TradePhase.Carrying)){SocialText(box,"私有の買付荷："+MaterialMarkets.Name(deal.Commodity)+" ×"+deal.Amount);if(known.Length>0)ObjectButton(box,"give-goods","この私有荷を丸ごと贈る",()=>{var r=Session.ManageSocial(new(Nonce("goods/"+deal.Id.Value),SocialWorldAction.DispatchGift,Person()){Asset=deal.Id},rev);message.Text=r.VisibleReason;Refresh();ShowPersonalObjects();});}
  SocialText(box,"現地の名物商");foreach(var item in SocialAssets.Catalog(w,site.Value).Where(a=>!SocialAssets.Objects(w).Any(o=>o.Id==a.Id)))ObjectButton(box,"buy-"+item.Kind,item.Name+"を購入："+item.Price+"文",()=>AssetCommand(new(Nonce("buy/"+item.Id.Value),SocialAssetAction.Buy,actor,item.Id),rev));
  foreach(var plan in w.SocialAssets?.Events.Where(e=>e.Actor==actor&&e.Input.Action==SocialAssetAction.PlanTea)??[]){SocialText(box,"茶会の予定："+WorldCalendar.Display(w.EpochDate,plan.Input.When)+"");if(w.SocialAssets!.Events.Any(e=>e.Input.Action==SocialAssetAction.HoldTea&&e.Input.Reference==plan.Input.Id)){{}continue;}ObjectButton(box,"tea-hold","この予定の茶会を現地で開催（茶代を支払う）",()=>AssetCommand(new(Nonce("hold/"+plan.Input.Id.Value),SocialAssetAction.HoldTea,actor,plan.Input.Id),rev));}
 }
}
