using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 readonly Dictionary<string,Button> meritReviewButtons=new();
 internal void ReviewMeritPress(string key){if(!meritReviewButtons.TryGetValue(key,out var b))throw new InvalidOperationException("この論功は選べません");b.EmitSignal(Button.SignalName.Pressed);}
 void ShowMeritRewards()
 {
  if(IsSaving||IsClockControlling)return;CancelAdvancePrefetch();var w=Snapshot();var actor=Session.PlayerControl;
  if(w.Merits==null){var enabled=Session.ManageMerit(new(new("request:merit-enable/"+Session.Revision),MeritAction.Enable,actor,[]),Session.Revision);if(enabled.Code!=ResultCode.Accepted){message.Text=enabled.VisibleReason;return;}w=Snapshot();}
  var(box,rev)=SocialWindow("働きの評価と恩賞");meritReviewButtons.Clear();{}
  void Run(MeritInput i){if(IsSaving||IsClockControlling)return;CancelAdvancePrefetch();var r=Session.ManageMerit(i,rev);message.Text=r.VisibleReason;GD.Print("MERIT_RESULT "+i.Action+" "+r.Code+" "+r.VisibleReason);Refresh();ShowMeritRewards();}
  void Button(string key,string label,Action run){var b=new Button{Text=label};box.AddChild(b);meritReviewButtons[key]=b;b.Pressed+=run;}
  foreach(var report in w.Companions?.Reports??[]){if(!HakutaiKagami.Core.Messaging.MessageFlow.Knows(w.Messaging,report.Message,actor.Value))continue;var order=w.Companions!.Orders.Single(q=>q.Input.Id==report.Order);if(order.Task?.Action!=ActionKind.Inspect)continue;var task=w.Tasks.Single(t=>t.RequestId==order.Task.RequestId);if(w.Merits!.Events.Any(e=>e.Input.Action==MeritAction.ReviewCompanion&&e.Input.Merits.Contains(task.Id)))continue;Button("review-companion","開封した点検返報を評価し、本人へ評価通知を渡す",()=>Run(new(new("request:merit-review/"+rev),MeritAction.ReviewCompanion,order.Input.Person,[task.Id],Object:new(report.Message))));}
  var records=MeritRewards.View(w,actor);if(records.Length==0){}
  foreach(var group in records.GroupBy(r=>r.Principal)){SocialText(box,"評価者："+MeritRewards.DisplayName(w,actor,group.Key));if(group.Key!=actor)SocialText(box,MeritRewards.Face(w,actor,group.Key));foreach(var row in group){var person=w.Definitions.Characters.Single(c=>c.Id==row.Subject);SocialText(box,person.Name+"の働き — "+(row.Received?"恩賞を受領した記録あり":row.Issued?"沙汰済み・受領の確認待ち":"未沙汰")+"\n"+WorldCalendar.Display(w.EpochDate,row.KnownAt)+"に認識した評価（第"+row.Version+"版）");}
   if(group.Key==actor)foreach(var recipient in group.Where(r=>!r.Issued).GroupBy(r=>r.Subject)){var person=recipient.Key;var tasks=recipient.Select(r=>r.Task).ToArray();EntityId Nonce(string k)=>new("request:merit-ui/"+rev+"/"+k+"/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(person.Value))[..16]);
    Button("coins","この相手へ私財から20文の恩賞",()=>Run(new(Nonce("coins"),MeritAction.Grant,person,tasks,MeritRewardKind.Coins,20)));
    Button("commendation","働きを認める感状を申し渡す",()=>Run(new(Nonce("letter"),MeritAction.Grant,person,tasks)));
    Button("surname","家の姓を与える（本人の受領が必要）",()=>Run(new(Nonce("surname"),MeritAction.Grant,person,tasks,MeritRewardKind.Surname)));
    Button("name-character","自分の名の末字を与える（拝領字として記録）",()=>Run(new(Nonce("character"),MeritAction.Grant,person,tasks,MeritRewardKind.NameCharacter)));
    foreach(var obj in SocialAssets.Inventory(w,actor))Button("object","私有物「"+obj.Name+"」を恩賞として贈る",()=>Run(new(Nonce("object"),MeritAction.Grant,person,tasks,MeritRewardKind.Object,Object:obj.Id)));
    foreach(var term in w.Salary?.Terms.Where(t=>t.Employer==actor&&t.Employee==person)??[])Button("salary-link","成立済みの扶持契約をこの働きの恩賞に結び付ける",()=>Run(new(Nonce("salary"),MeritAction.LinkSalary,person,tasks,Object:term.Id)));
   }
  }
  foreach(var g in MeritRewards.Grievances(w,actor)){SocialText(box,g.Description);Button("petition","この主君へ現地で恩賞を上申する",()=>Run(new(new("request:merit-petition/"+rev),MeritAction.Petition,g.Principal,g.Tasks)));}
  Button("salary-open","扶持の相談へ（独立した合意と実支給が必要）",()=>{ReviewSocialClose();ShowSalary();});
  Button("office-open","役目の相談へ（既存の任命条件を確認）",()=>{ReviewSocialClose();ShowCastleAdministration();});
  {}
 }
}
