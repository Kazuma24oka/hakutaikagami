using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 int courtCategory;readonly Dictionary<string,Button> courtReviewButtons=new();
 internal void ReviewCourtCategory(int category){courtCategory=category;ShowHouseholdCourt();}
 internal void ReviewCourtPress(string key){if(!courtReviewButtons.TryGetValue(key,out var b)||b.Disabled)throw new InvalidOperationException("今はこの家中の用件を選べません："+key);b.EmitSignal(Button.SignalName.Pressed);}
 void ShowHouseholdCourt()
 {
  if(IsSaving||IsClockControlling)return;var(box,rev)=SocialWindow("家中：登用・常設職・評定");courtReviewButtons.Clear();var w=Snapshot();var actor=Session.PlayerControl;var site=SocialWorld.Site(w,actor);if(site==null){SocialText(box,"現地へ到着してから相談してください。");return;}
  void Button(string key,string text,Action action){var b=new Button{Text=text};box.AddChild(b);courtReviewButtons[key]=b;b.Pressed+=action;}
  void Run(CourtInput input){if(IsSaving||IsClockControlling)return;CancelAdvancePrefetch();var r=Session.ManageCourt(input,rev);GD.Print("COURT_RESULT "+input.Action+" "+r.Code+" "+r.VisibleReason);message.Text=r.VisibleReason;Refresh();ShowHouseholdCourt();}
  EntityId Nonce(string key)=>new("request:court-ui/"+rev+"/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(key))[..24]);
  var section=new OptionButton();foreach(var label in new[]{"登用：条件と本人の引受","家中職：任命と執行の範囲","評定：届いた上申を審査","家中の様子"})section.AddItem(label);section.Select(courtCategory);box.AddChild(section);section.ItemSelected+=i=>{courtCategory=(int)i;ShowHouseholdCourt();};
  foreach(var parcel in SocialWorld.Gifts(w,actor).Where(g=>g.Recipient==actor&&g.Phase=="運搬・受領待ち")){
   var asset=w.SocialWorld!.Events.Single(e=>e.Input.Id==parcel.Id).Input.Asset;if(asset==null)continue;var doc=HouseholdCourt.DocumentRows(w).SingleOrDefault(d=>d.Id==asset);if(doc==null)continue;
   Button("read-document",doc.Name+"を開いて、実物を確認する",()=>{CancelAdvancePrefetch();var r=Session.ManageSocial(new(Nonce("read/"+doc.Id.Value),SocialWorldAction.ReceiveGift,parcel.Sender,Reference:parcel.Id),rev);message.Text=r.VisibleReason;Refresh();ShowHouseholdCourt();});
  }
  foreach(var doc in HouseholdCourt.PendingDocuments(w,actor)){
   var src=doc.Source;CourtInput Input(string key,CourtAction action,EntityId? reference=null)=>new(Nonce(key+"/"+doc.Id.Value),action,doc.Sender,src.Input.Owner,src.Input.Site,src.Input.Role,Reference:reference??src.Input.Id);
   if(src.Input.Action==CourtAction.RecruitOffer&&!w.Court!.Events.Any(e=>e.Input.Reference==src.Input.Id&&e.Input.Action is CourtAction.RecruitAccept or CourtAction.RecruitDecline)){
    SocialText(box,$"差出人：{MeritRewards.DisplayName(w,actor,src.Actor)}\n支度金{src.Input.Amount}文。家への所属と直属を引き受ける条件です。兵・城・毎月の扶持は別の約束です。");Button("accept-recruit","この条件で引き受ける",()=>Run(Input("recruit-yes",CourtAction.RecruitAccept)));Button("decline-recruit","条件を断る（実物の返還は別）",()=>Run(Input("recruit-no",CourtAction.RecruitDecline)));
   }
   if(src.Input.Action==CourtAction.Nominate&&!w.Court!.Events.Any(e=>e.Input.Reference==src.Input.Id&&e.Input.Action is CourtAction.AcceptOffice or CourtAction.DeclineOffice)){
    SocialText(box,"任命状："+HouseholdCourt.RoleName(src.Input.Role,HouseScaleNotices.Known(w,actor,src.Input.Owner))+"\n管轄："+NewGameWorld.SiteName(src.Input.Site.Value)+"。当主交代時は新しい任命状で再確認します。");Button("accept-office","この常設の役目を引き受ける",()=>Run(Input("office-yes",CourtAction.AcceptOffice)));Button("decline-office","役目を辞退する",()=>Run(Input("office-no",CourtAction.DeclineOffice)));
   }
   if(src.Input.Action==CourtAction.Convene&&!w.Court!.Events.Any(e=>e.Actor==actor&&e.Input.Reference==doc.Id&&e.Input.Action is CourtAction.AcceptInvitation or CourtAction.DeclineInvitation)){
    SocialText(box,"評定の招待："+WorldCalendar.Display(w.EpochDate,src.Input.When)+" / "+NewGameWorld.SiteName(src.Input.Site.Value));Button("accept-invitation","出席を承諾する（移動は別）",()=>Run(Input("meeting-yes",CourtAction.AcceptInvitation,doc.Id)));Button("decline-invitation","出席を断る",()=>Run(Input("meeting-no",CourtAction.DeclineInvitation,doc.Id)));
   }
  }
  var owner=HouseSuccession.Factions(w,w.ElapsedSeconds,w.CommitSequence+1).FirstOrDefault(f=>f.Head==actor.Value&&w.LandRights.Any(l=>l.Subject.Value==f.Id&&l.Target==site&&l.Kind=="control"&&l.From<=w.ElapsedSeconds&&w.ElapsedSeconds<l.Until))?.Id??ServiceDisplay().House?.Value;
  if(owner==null){SocialText(box,"まだ家中に所属していません。届いた条件書を確認するか、現地で仕官を相談できます。");return;}var house=new EntityId(owner);bool head=HouseSuccession.Head(w,house)==actor;
  CourtInput Command(string key,CourtAction action,EntityId person,long amount=0,EntityId? reference=null,CourtRole role=CourtRole.Steward,long when=0,CourtCaseKind kind=CourtCaseKind.RelieveVillage)=>new(Nonce(key),action,person,house,site.Value,role,amount,reference,when,kind);
  var roster=HouseholdCourt.Roster(w,actor,house).Where(p=>p!=actor).ToArray();
  if(courtCategory==3){
   var report=HouseCondition.Observe(w,actor,house);
   if(report==null){SocialText(box,"この家の現地記録を確認できません。");return;}
   SocialText(box,NewGameWorld.SiteName(report.Site.Value)+" ／ "+WorldCalendar.Display(w.EpochDate,report.ObservedAt));
   SocialText(box,report.ControlsLocalSite?"主家がこの拠点を支配":"この拠点で主家の支配を確認できず");
   foreach(var person in report.LocalLandholders)SocialText(box,"在地の所領："+PersonName(person));
   foreach(var person in report.LocalCastellans)SocialText(box,"現地の城主："+PersonName(person));
   if(report.KnownSurveys.Length==0)SocialText(box,"本人が確認した指出・検地の帳面はありません。");
   foreach(var entry in report.KnownSurveys)SocialText(box,NewGameWorld.SiteName(entry.Site.Value)+" ／ "+
       (entry.Book.Measured?"検地":"指出")+" ／ "+WorldCalendar.Display(w.EpochDate,entry.Book.At));
   var seat=HouseReforms.TempleMarketSeat(w,site.Value,w.ElapsedSeconds,rev+1);
   if(seat!=null)SocialText(box,"当市の木材商いに寺社の座があります。楽市でも一部の取引枠を留保します。");
   else if(head && report.ControlsLocalSite && !HouseReforms.OpenMarketAt(w,site.Value,w.ElapsedSeconds,rev+1))
    Button("grant-market-seat","面会した寺社代表へ木材商いの座を安堵する",()=>Run(Command("grant-seat",CourtAction.GrantTempleSeat,SocialWorld.Temple(w,site.Value).Representative)));
   if(seat!=null && head && report.ControlsLocalSite && HouseReforms.OpenMarketAt(w,site.Value,w.ElapsedSeconds,rev+1)){
    var dispute=HouseReforms.SeatDispute(w,site.Value,w.ElapsedSeconds,rev+1);
    void SeatButton(CourtAction action,string label)=>Button("seat-"+action,label,
        ()=>Run(Command("seat/"+action,action,seat.Input.Person,reference:seat.Input.Id)));
    if(dispute==null)SeatButton(CourtAction.ChallengeSeat,"代表者を呼び、座の撤廃を申し入れる");
    else if(dispute.Input.Action==CourtAction.SettleSeat)SocialText(box,"座の取引枠を留保して和解済み");
    else{
     SocialText(box,dispute.Input.Action==CourtAction.InsistSeat?"座の商人が新規買付への協力を停止中":"寺社代表が安堵状を示し、取引枠の維持を現地で嘆願しました。");
     if(dispute.Input.Action==CourtAction.ChallengeSeat && dispute.At<w.ElapsedSeconds)
      SeatButton(CourtAction.InsistSeat,"嘆願を退け、撤廃を求める");
     SeatButton(CourtAction.SettleSeat,"取引枠を認めて和解する");
    }
   }
   if(head&&roster.Length>0){
    var persons=new OptionButton();foreach(var person in roster)persons.AddItem(PersonName(person));box.AddChild(persons);
    var kinds=Enum.GetValues<ReformKind>();var reform=new OptionButton();foreach(var k in kinds)reform.AddItem(HouseReforms.Name(k));box.AddChild(reform);
    Button("consult-reform","この改革案について意向を問う",()=>Run(Command("reform",CourtAction.ConsultReform,roster[persons.Selected]) with{Reform=kinds[reform.Selected]}));
   }
   foreach(var e in w.Court?.Events.Where(e=>e.Input.Action==CourtAction.ConsultReform&&e.Actor==actor)??[]){
    var id=HouseReforms.Letter(e.Input,HouseReforms.Answer);
    if(HouseReforms.AnswerKnown(w,e,actor)){
     SocialText(box,PersonName(e.Input.Person)+" ／ "+w.Messaging.Messages.Single(m=>m.Id==id).Body);
     if(w.Court!.Events.Any(d=>d.Input.Action==CourtAction.OpenLocalMarket&&d.Input.Reference==e.Input.Id))
      SocialText(box,"この市場で楽市を実施済み");
     else if(w.Court!.Events.Any(d=>d.Input.Action==CourtAction.DeferReform&&d.Input.Reference==e.Input.Id))
      SocialText(box,"この相談案は見送り済み");
     else if(head && site==e.Input.Site && house==e.Input.Owner){
      if(e.Input.Reform==ReformKind.OpenMarket && !HouseReforms.OpenMarketAt(w,e.Input.Site,w.ElapsedSeconds,rev+1))
       Button("market-"+e.Input.Id,seat==null?"現地で楽市を実施する":"寺社の取引枠を残して楽市を実施する",()=>Run(Command("market/"+e.Input.Id,CourtAction.OpenLocalMarket,e.Input.Person,reference:e.Input.Id)));
      Button("defer-"+e.Input.Id,"この相談案を見送る",()=>Run(Command("defer/"+e.Input.Id,CourtAction.DeferReform,e.Input.Person,reference:e.Input.Id)));
     }
    }
    else if(w.Messaging.Receipts.Any(r=>r.MessageId==id&&r.ReceiverId==actor.Value))
     Button("reform-answer-"+e.Input.Id,"改革への返書を開く",()=>{Session.Open(actor,new(id),rev);Refresh();ShowHouseholdCourt();});
    else SocialText(box,PersonName(e.Input.Person)+" ／ "+HouseReforms.Name(e.Input.Reform!.Value)+"：返書待ち");
   }
   return;
  }
  if(courtCategory==0){
   SocialText(box,head?"条件を提示し、本人が実物を受け取って引き受けて初めて登用が成立します。家と個人の約束、兵や知行は分けます。":"自分が引き受けた登用の合意を確認できます。");
   if(head){var candidates=w.Characters.Where(c=>c.Id!=actor&&c.Life==LifeState.Adult&&SocialWorld.Site(w,c.Id)==site&&!roster.Contains(c.Id)).Select(c=>c.Id).ToArray();if(candidates.Length>0){var people=new OptionButton();foreach(var p in candidates)people.AddItem(MeritRewards.DisplayName(w,actor,p));box.AddChild(people);var money=new SpinBox{MinValue=20,MaxValue=500,Value=20,Step=10};box.AddChild(money);Button("recruit","支度金を条件書と一緒に預け、登用を打診する",()=>Run(Command("recruit",CourtAction.RecruitOffer,candidates[people.Selected],(long)money.Value)));}}
   foreach(var offer in w.Court?.Events.Where(e=>e.Input.Action==CourtAction.RecruitOffer&&e.Actor==actor)??[]){var answer=w.Court!.Events.FirstOrDefault(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action is CourtAction.RecruitAccept or CourtAction.RecruitDecline);SocialText(box,MeritRewards.DisplayName(w,actor,offer.Input.Person)+"："+(answer!=null&&HouseholdCourt.KnowsReply(w,answer,actor)?answer.Input.Action==CourtAction.RecruitAccept?"本人の引受を確認":"本人の辞退を確認":"条件を渡した。返答はまだ未確認"));}
   foreach(var joined in w.Court?.Events.Where(e=>e.Actor==actor&&e.Input.Action==CourtAction.RecruitAccept)??[])Button("resign-service","登用原本を持参して所属の終了を確認（元の個人奉公は別）",()=>Run(Command("resign-service",CourtAction.ResignService,actor,reference:joined.Input.Reference)));
   foreach(var decline in w.Court?.Events.Where(e=>e.Actor==actor&&e.Input.Action==CourtAction.RecruitDecline&&!w.Court.Events.Any(r=>r.Input.Reference==e.Input.Reference&&r.Input.Action==CourtAction.ReturnSigning))??[])Button("return-signing","辞退した条件書と支度金を現地で返す",()=>Run(Command("return-signing",CourtAction.ReturnSigning,actor,reference:decline.Input.Reference)));
  }else if(courtCategory==1){
   SocialText(box,"役職は無期限の引受原本です。行政は管轄の現地でだけ行い、代官・城主の処分枠は月ごとに制限されます。兵と指揮は別に預かります。");
   if(head&&roster.Length>0){var people=new OptionButton();foreach(var p in roster)people.AddItem(MeritRewards.DisplayName(w,actor,p)+" / "+HouseholdCourt.Seat(w,actor,p));box.AddChild(people);var roles=Enum.GetValues<CourtRole>().Where(r=>HouseholdCourt.SeatLimit(HouseScaleNotices.Known(w,actor,house),r)>0).ToArray();var role=new OptionButton();foreach(var r in roles)role.AddItem(HouseholdCourt.RoleName(r,HouseScaleNotices.Known(w,actor,house)));box.AddChild(role);Button("nominate","この相手に常設職の任命状を渡す",()=>Run(Command("nominate",CourtAction.Nominate,roster[people.Selected],role:roles[role.Selected])));}
   foreach(var offer in w.Court?.Events.Where(e=>e.Input.Action==CourtAction.Nominate&&(e.Actor==actor||e.Input.Person==actor))??[]){var answer=w.Court!.Events.FirstOrDefault(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action is CourtAction.AcceptOffice or CourtAction.DeclineOffice);bool known=answer!=null&&HouseholdCourt.KnowsReply(w,answer,actor);SocialText(box,HouseholdCourt.RoleName(offer.Input.Role,HouseScaleNotices.Known(w,actor,house))+" / "+MeritRewards.DisplayName(w,actor,offer.Input.Person)+"\n"+(known?answer!.Input.Action==CourtAction.AcceptOffice?"引受原本あり。現在の執行は現地で確認します。":"辞退の返答を確認しました。":"返答はまだ未確認です。"));if(known&&answer!.Input.Action==CourtAction.AcceptOffice){if(offer.Actor==actor)Button("revoke","解任の通知を渡す（受領後に効力）",()=>Run(Command("revoke",CourtAction.RevokeOffice,offer.Input.Person,reference:offer.Input.Id)));if(offer.Input.Person==actor)Button("resign-office","当主に現地で常設職の辞任を申し出る",()=>Run(Command("resign-office",CourtAction.ResignOffice,offer.Actor,reference:offer.Input.Id)));}}
   if(head){var territory=HouseTerritoryScale.Measure(w,house,w.ElapsedSeconds,rev+1);SocialText(box,$"宣言済み：{HouseScaleNotices.Name(HouseScaleNotices.Known(w,actor,house))} ／ 実効支配 {territory.Sites}拠点 ／ 所在確認済み {territory.Provinces}国");var scale=new OptionButton();foreach(var label in new[]{"国人衆の規模","大名の規模（支配3拠点以上）","大大名の規模（8拠点・2国以上）","覇者の規模（15拠点・4国以上）"})scale.AddItem(label);box.AddChild(scale);Button("scale","支配の実態を確かめ、この規模を宣言する",()=>Run(Command("scale",CourtAction.DeclareScale,actor) with{Scale=(CourtScale)scale.Selected}));}
   Button("treasury","現地の家の調達と予算へ",()=>{ReviewSocialClose();ShowHouseTreasury();});
  }else{
   SocialText(box,"本人に届いた上申だけを評定で裁きます。承認した救済・買付は実在の米や銭を使います。返答は相手が実物を確認してから伝わります。");
   if(head||HouseholdCourt.CanConvene(w,actor,house,site.Value)){var invited=roster;if(invited.Length>0)Button("convene","1時間後に現地の評定を招く（招待状を作成）",()=>Run(Command("convene",CourtAction.Convene,actor,when:w.ElapsedSeconds+3600) with{People=invited}));}
   foreach(var doc in HouseholdCourt.DocumentRows(w).Where(d=>d.Sender==actor&&d.Source.Input.Action==CourtAction.Convene&&SocialAssets.Inventory(w,actor).Any(o=>o.Id==d.Id)))Button("deliver-invitation","この評定招待状を本人へ渡す",()=>Run(Command("deliver-invitation/"+doc.Id.Value,CourtAction.DeliverDocument,doc.Recipient,reference:doc.Id)));
   foreach(var meeting in w.Court?.Events.Where(e=>e.Input.Action==CourtAction.Convene&&e.Input.Owner==house&&(e.Actor==actor||HouseholdCourt.PendingDocuments(w,actor).Any(d=>d.Source.Input.Id==e.Input.Id)))??[])if(!w.Court!.Events.Any(e=>e.Input.Action==CourtAction.HoldCouncil&&e.Input.Reference==meeting.Input.Id))Button("hold-council","この予定の評定を現地で開く",()=>Run(Command("hold-council",CourtAction.HoldCouncil,actor,reference:meeting.Input.Id)));
   if(!head){var principal=ServiceDisplay().Principal;if(principal!=null){var kind=new OptionButton();foreach(var label in new[]{"村への救済米","木材の買付","武具の買付","家の調達予算"})kind.AddItem(label);box.AddChild(kind);var amount=new SpinBox{MinValue=1,MaxValue=200,Value=5,Step=1};box.AddChild(amount);Button("submit-case","この用件の上申書を主君へ渡す",()=>Run(Command("submit",CourtAction.SubmitCase,principal.Value,(long)amount.Value,kind:(CourtCaseKind)kind.Selected)));}}
   foreach(var proposal in HouseholdCourt.KnownCases(w,actor)){var decision=HouseholdCourt.KnownDecision(w,actor,proposal.Input.Id);SocialText(box,$"上申：{proposal.Input.Case switch{CourtCaseKind.RelieveVillage=>"救済米",CourtCaseKind.BuyWood=>"木材の買付",CourtCaseKind.BuyWeapons=>"武具の買付",_=>"調達予算"}} {proposal.Input.Amount}\n"+(decision==null?"回答はまだ確認していません。":decision.Input.Action==CourtAction.ApproveCase?"承認。実際の処分を記録しました。":decision.Input.Action==CourtAction.DenyCase?"今回は退けられました。":"条件の見直しを求められました。"));if(head&&decision==null){Button("approve-case","この上申を認めて、現地で実行する",()=>Run(Command("approve/"+proposal.Input.Id.Value,CourtAction.ApproveCase,proposal.Actor,reference:proposal.Input.Id)));Button("deny-case","この上申を退ける",()=>Run(Command("deny/"+proposal.Input.Id.Value,CourtAction.DenyCase,proposal.Actor,reference:proposal.Input.Id)));Button("revise-case","条件を見直して再度上申してもらう",()=>Run(Command("revise/"+proposal.Input.Id.Value,CourtAction.ReviseCase,proposal.Actor,reference:proposal.Input.Id)));}}
  }
 }
}
