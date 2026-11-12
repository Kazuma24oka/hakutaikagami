using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 readonly Dictionary<string,Button> banditButtons=new();readonly List<Officer3DCustomActor> banditModels=new();
 internal void ReviewBanditPress(string key)=>banditButtons[key].EmitSignal(Button.SignalName.Pressed);
 void ShowBandits(){if(IsSaving||IsClockControlling)return;var(box,rev)=SocialWindow("道の安全・賊との接触");banditButtons.Clear();var w=Snapshot();var actor=Session.PlayerControl;if(SocialWorld.Site(w,actor) is not {} site){{}return;}
 void Button(string key,string label,BanditAction action){var b=new Button{Text=label};box.AddChild(b);banditButtons[key]=b;b.Pressed+=()=>{CancelAdvancePrefetch();var result=Session.ManageBandits(new(new("request:bandit-ui/"+rev+"/"+key),action,site),rev);message.Text=result.VisibleReason;Refresh();ShowBandits();};}
 ShowSecurity(box,rev,w,site);ShowGuardJobs(box,rev,w,site);var current=BanditEncounter.Current(w);if(current==null){SocialText(box,"本人の残る負傷："+BanditEncounter.Injury(w,actor)+"。周辺の賊の状態は現地で確認します。");if(BanditEncounter.Dangerous(w,site))Button("encounter","道の入口で賊に対処する",BanditAction.Encounter);else SocialText(box,"この周辺に対応中の賊はいません。");return;}
 SocialText(box,current.Phase+"／本人側"+current.People.Count(p=>p.Side==0)+"人・賊"+current.People.Count(p=>p.Side==1&&p.Health>0)+"人。戦うと同じ世界時間で進みます。");if(current.Phase=="選択待ち"){Button("fight","戦う",BanditAction.Fight);Button("pay","私財20文を渡して通る",BanditAction.Pay);}Button("withdraw","安全な拠点側へ退く",BanditAction.Withdraw);if(current.Phase=="交戦中"){}
 }
 void ShowGuardJobs(VBoxContainer box,long rev,ContractWorld w,EntityId site){
 void Add(string key,string label,GuardInput input){var b=new Button{Text=label};box.AddChild(b);banditButtons[key]=b;b.Pressed+=()=>{CancelAdvancePrefetch();message.Text=Session.ManageGuard(input,rev).VisibleReason;Refresh();ShowBandits();};}
 var job=GuardJobs.Jobs(w).LastOrDefault(j=>j.Terms.Worker==w.PlayerControl&&!GuardJobs.Closed(j));
 EntityId Id(string k)=>new("request:guard-ui/"+rev+"/"+k);
 if(job==null){var issuer=GuardJobs.Issuers(w).FirstOrDefault();if(issuer==default)return;{}var dest=GuardJobs.Destinations(w,site).FirstOrDefault();if(dest!=null)Add("escort","隣の拠点まで同行を引き受ける",new(Id("escort"),GuardAction.Accept,Issuer:issuer,Destination:dest));if(BanditEncounter.Dangerous(w,site))Add("suppression","現地の賊の討伐を引き受ける",new(Id("suppression"),GuardAction.Accept,Issuer:issuer,Kind:GuardKind.Suppression));}
 else{SocialText(box,(job.Terms.Kind==GuardKind.Escort?"道中の同行":"賊の討伐")+"："+job.Phase+"。報酬80文／期限30日");if(job.Terms.Kind==GuardKind.Escort&&job.Departure==null)Add("depart","護衛対象と一緒に出発する",new(Id("depart"),GuardAction.Depart,job.Terms.Id));Add("settle","現地の依頼主へ報告して精算する",new(Id("settle"),GuardAction.Settle,job.Terms.Id));Add("cancel-job","依頼主に取止めを伝える",new(Id("cancel"),GuardAction.Cancel,job.Terms.Id));}
 }
 void RenderBandits(ContractWorld w,FieldPoint center){foreach(var a in banditModels)a.Visible=false;var e=BanditEncounter.Current(w);if(e==null)return;var enemies=e.People.Where(p=>p.Side==1).ToArray();while(banditModels.Count<enemies.Length){var a=new Officer3DCustomActor{Scale=Vector3.One*2};stage.AddChild(a);a.Apply(Officer3DCustomActor.Preset("veteran"));banditModels.Add(a);}for(int i=0;i<enemies.Length;i++){var a=banditModels[i];var p=new FieldPoint(center.X+3500,center.Y+(i-1)*2500);WorldArmyProjection.Apply(a,p,center);a.Visible=enemies[i].Health>0;a.Rotation=new(0,-Mathf.Pi/2,0);string animation=e.Phase=="交戦中"?"attack":"idle";var clip=a.Player.GetAnimation(a.Clips[animation]);a.Freeze(animation,(w.ElapsedSeconds+fraction)%clip.Length);}}
}
