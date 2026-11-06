using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 readonly Dictionary<string,Button> battlefieldButtons=new();Officer3DCustomActor? templeModel;
 internal void ReviewBattlefieldPress(string key)=>battlefieldButtons[key].EmitSignal(Button.SignalName.Pressed);
 static string TroopName(TroopKind k)=>k switch{TroopKind.Spear=>"槍",TroopKind.Bow=>"弓",TroopKind.Gun=>"鉄砲",TroopKind.Cavalry=>"騎馬",_=>"輸送"};
 void ShowBattlefield(){if(IsSaving||IsClockControlling)return;var(box,rev)=SocialWindow("軍の編成・陣形・療養");battlefieldButtons.Clear();var w=Snapshot();if(!BattlefieldRules.Enabled(w)){SocialText(box,"この保存は従来の戦闘規則です。");return;}if(w.NewMilitary is not {} military){SocialText(box,"軍務で兵を引き受けると、現地の装備を受け取れます。");return;}if(NewGameMilitary.Deployed(w)&&NewGameMilitary.RecognitionEnabled(w)&&!NewGameMilitary.AllOwnCurrentlySeen(w)){SocialText(box,"各隊を現地で確認してから編成状態を表示します。離れた隊の状態は受領した報告で確認してください。");return;}if(!NewGameMilitary.Deployed(w)&&SocialWorld.Site(w,w.PlayerControl)!=military.Terms.Site){SocialText(box,"装備と負傷兵の確認は軍の本拠で行います。");return;}var site=RegionalCampaign.Site(ContinuousWorld.Nearest(NewGameMilitary.PersonPosition(w,w.ElapsedSeconds)));string weather=BattlefieldRules.Weather(w,site,w.ElapsedSeconds) switch{BattleWeather.Rain=>"雨：移動と鉄砲に影響",BattleWeather.Fog=>"霧：見通し20m",BattleWeather.Snow=>"雪：移動に影響",_=>"晴れ"};SocialText(box,weather+"。米・弾薬・装備は実物の在庫から使います。");
 void Button(string key,string text,Action act){var b=new Button{Text=text};battlefieldButtons[key]=b;box.AddChild(b);b.Pressed+=()=>{CancelAdvancePrefetch();act();Refresh();ShowBattlefield();};}
 EntityId Id(string key)=>new("request:battlefield-ui/"+rev+"/"+key.Replace(':','/') );
 void Act(string key,string text,BattlefieldInput input)=>Button(key,text,()=>message.Text=Session.ManageBattlefield(input,rev).VisibleReason);
 var ids=NewGameMilitary.UnitIdsFor(w);for(int n=0;n<ids.Length;n++){var unit=ids[n];var appointment=HouseholdDetachment.Appointment(w,unit,w.ElapsedSeconds,rev+1);
 if(appointment!=null){
     var captain=FamilyBirth.People(w).Single(p=>p.Id==appointment.Actor.Value);
     SocialText(box,$"第{n+1}隊の現地指揮　{captain.Name}");
     foreach(var company in PrivateRecruitment.CompanyReports(w,appointment.Actor,w.ElapsedSeconds,rev+1)){
         string source=company.VillageLevy?"村からの徴募兵":"市の雇い兵";
         SocialText(box,$"{captain.Name}の{source}　健在{company.Fit}人／負傷{company.Wounded}・戦死{company.Dead}・逃亡{company.Deserted}");
     }
     if(!NewGameMilitary.Deployed(w)){
         var end=new CourtInput(Id("release-"+n),CourtAction.EndDetachment,appointment.Actor,military.Terms.Owner,military.Terms.Site,Reference:appointment.Input.Id,Carrier:unit);
         Button("release-"+n,"分隊指揮を返してもらう",()=>message.Text=Session.ManageCourt(end,rev).VisibleReason);
     }
 }else if(!NewGameMilitary.Deployed(w)){
     foreach(var follower in PersonalCompanions.View(w).Where(c=>c.Active&&c.Nearby&&!c.Busy)){
         string key="captain-"+n+"-"+follower.Person.Value;
         if(HouseholdDetachment.Offer(w,follower.Person,unit,Id(key)) is {} offer)
             Button(key,$"第{n+1}隊の指揮を{follower.Name}に頼む",()=>message.Text=Session.ManageCourt(offer,rev).VisibleReason);
     }
 }
 var gear=BattlefieldRules.Loadout(w,unit);var losses=BattlefieldRules.Casualties(w,unit,w.ElapsedSeconds,rev+1);SocialText(box,$"第{n+1}隊：{w.Units.Single(u=>u.Id==unit).Soldiers}人／{TroopName(gear.Kind)} {gear.Equipped}組　負傷{losses.Wounded}・戦死{losses.Dead}・逃亡{losses.Deserted}");if(!NewGameMilitary.Deployed(w)){if(gear.Equipped>0)Act("return-"+n,"装備を蔵へ戻す",new(Id("return-"+n),BattlefieldAction.ReturnGear,unit,gear.Kind,BattlefieldRules.Gear(gear.Kind),gear.Equipped));else foreach(var kind in Enum.GetValues<TroopKind>()){string key="equip-"+n+"-"+kind;int amount=(int)Math.Min(w.Units.Single(u=>u.Id==unit).Soldiers,BattlefieldRules.Depot(w,military.Terms.Owner,military.Terms.Site,BattlefieldRules.Gear(kind)));if(amount>0)Act(key,TroopName(kind)+"を"+amount+"組受け取る",new(Id(key),BattlefieldAction.Equip,unit,kind,BattlefieldRules.Gear(kind),amount));}if(losses.Wounded>0){string key="rest-"+n;Act(key,$"負傷{losses.Wounded}人を5日療養（米{losses.Wounded*3}kg）",new(Id(key),BattlefieldAction.Rest,unit,Amount:losses.Wounded));}}
 foreach(string good in new[]{"arrow","powder","bullet","fodder"}){long carried=BattlefieldRules.Carried(w,unit,good);if(carried>0)SocialText(box,good switch{"arrow"=>"矢："+carried,"powder"=>"火薬："+carried,"bullet"=>"玉："+carried,_=>"馬の飼料："+carried});if(!NewGameMilitary.Deployed(w)&&gear.Equipped>0&&((gear.Kind==TroopKind.Bow&&good=="arrow")||(gear.Kind==TroopKind.Gun&&good is "powder" or "bullet")||(gear.Kind==TroopKind.Cavalry&&good=="fodder"))){int amount=(int)Math.Min(60,Math.Min(1000-carried,BattlefieldRules.Depot(w,military.Terms.Owner,military.Terms.Site,good)));if(amount>0){string key="load-"+n+"-"+good;Act(key,(good=="arrow"?"矢":good=="powder"?"火薬":good=="bullet"?"玉":"飼料")+amount+"を積む",new(Id(key),BattlefieldAction.Load,unit,Good:good,Amount:amount));}}}}
 if(NewGameMilitary.Deployed(w)){var a=NewGameMilitary.Anchor(w);foreach(var kind in Enum.GetValues<FormationKind>()){string key="formation-"+kind;Button(key,kind switch{FormationKind.Line=>"横陣",FormationKind.Wedge=>"魚鱗",FormationKind.Wings=>"鶴翼",_=>"円陣"},()=>message.Text=Session.DecideNewMilitary(new(Id(key),NewMilitaryAction.Form){A=new(a.X-12000,a.Y),B=new(a.X+12000,a.Y),Formation=kind},Session.PlayerControl,rev).VisibleReason);}}
 if(site==TempleMilitary.Site){SocialText(box,"堺の武装寺院（試作）：兵は寺社の所属のまま、保護合意がある場合に一日だけ現地防衛へ協力します。米12kg。");foreach(var action in Enum.GetValues<TempleMilitaryAction>()){string key="temple-"+action;Button(key,action switch{TempleMilitaryAction.RequestDefense=>"寺社へ現地防衛を依頼",TempleMilitaryAction.EndDefense=>"防衛協力を終了",TempleMilitaryAction.Observe=>"現地で寺社の態度を確認",TempleMilitaryAction.Attack=>"武装敵対の寺社へ攻撃を指示",_=>"寺社への攻撃を取りやめる"},()=>message.Text=Session.ManageTempleMilitary(new(Id(key),action,site),rev).VisibleReason);}}
 }
 void RenderTempleMilitia(ContractWorld w,FieldPoint center){if(templeModel!=null)templeModel.Visible=false;if(!BattlefieldRules.Enabled(w)||RegionalGround.Distance(center,TempleMilitary.Position(w))>BattlefieldRules.SightRange(w,center,w.ElapsedSeconds)||TempleMilitary.Strength(w,w.ElapsedSeconds)<=0)return;if(templeModel==null){templeModel=new Officer3DCustomActor{Scale=Vector3.One*2};stage.AddChild(templeModel);templeModel.Apply(Officer3DCustomActor.Preset("veteran"));}templeModel.Visible=true;WorldArmyProjection.Apply(templeModel,TempleMilitary.Position(w),center);string animation=WorldClashes.AttacksNow(w,TempleMilitary.Unit)?"attack":"idle";templeModel.Freeze(animation,(w.ElapsedSeconds+fraction)%templeModel.Player.GetAnimation(templeModel.Clips[animation]).Length);}
}
