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
  var section=new OptionButton();foreach(var label in new[]{"登用：条件と本人の引受","家中職：任命と執行の範囲","評定：届いた上申を審査","家中の様子","募兵"})section.AddItem(label);section.Select(courtCategory);box.AddChild(section);section.ItemSelected+=i=>{courtCategory=(int)i;ShowHouseholdCourt();};
  if(courtCategory==4){
   var control=w.LandRights.SingleOrDefault(r=>r.Kind=="control" && r.Target==site && r.From<=w.ElapsedSeconds && w.ElapsedSeconds<r.Until);
   foreach(var reservation in MusterNotices.Reservations(w,actor,w.ElapsedSeconds,rev+1)){
    var order=reservation.Input;
    SocialText(box,$"陣触れ　{NewGameWorld.SiteName(order.Site.Value)}　{WorldCalendar.Display(w.EpochDate,order.When)}集合　軍役{order.Amount}人");
    if(order.Site==site && w.ElapsedSeconds>=order.When){
     var present=Math.Min(order.Amount,PrivateRecruitment.Soldiers(w,actor,site.Value,w.ElapsedSeconds,rev+1));
     Button("attend/"+order.Id.Value,$"現地の{present}人で参集する",()=>Run(new(Nonce("attend/"+order.Id.Value),CourtAction.AttendMuster,actor,order.Owner,order.Site,Amount:present,Reference:order.Id)));
    }
   }
   if(control==null || !MilitaryEconomy.Enabled(w)){SocialText(box,"この拠点では募兵できません。");return;}
   SocialText(box,$"現地の自前の兵 {PrivateRecruitment.Soldiers(w,actor,site.Value,w.ElapsedSeconds,rev+1)}人　手持ちの扶持米 {HouseSalary.PrivateRice(w,actor)}kg　所持銭 {NewGameWork.Balance(w,actor)}文");
   SpinBox Count(string label,int max){
    var row=new HBoxContainer();box.AddChild(row);row.AddChild(new Label{Text=label});
    var value=new SpinBox{MinValue=1,MaxValue=max,Step=1,Value=1};row.AddChild(value);return value;
   }
   var men=Count("人数",100);var days=Count("従軍日数",30);
   void Recruit(CourtAction action)=>Run(new(Nonce(action.ToString()),action,actor,control.Subject,site.Value,
    Amount:(long)men.Value,When:checked(w.ElapsedSeconds+86400L*(long)days.Value)));
   if(PrivateRecruitment.CanLevy(w,actor,control.Subject,site.Value,w.ElapsedSeconds,rev+1))
    Button("recruit-villagers","村兵を集める（1人1日につき米1kg）",()=>Recruit(CourtAction.RaiseVillageSoldiers));
   if(MaterialMarkets.Enabled(w)&&MaterialMarkets.Sites(w).Contains(site.Value))
    Button("hire-market-soldiers",$"市で雇う（1人1日につき米1kg・{PrivateRecruitment.DailyWage}文）",()=>Recruit(CourtAction.HireMarketSoldiers));
   if(HouseSuccession.Head(w,control.Subject)==actor){
    foreach(var member in w.Memberships.Where(m=>m.Target==control.Subject && m.Subject!=actor && m.From<=w.ElapsedSeconds && w.ElapsedSeconds<m.Until)){
     var person=member.Subject;
     if(SocialWorld.Site(w,person)!=site)continue;
     Button("muster/"+person.Value,MeritRewards.DisplayName(w,actor,person)+"へ陣触れを渡す",()=>Run(new(Nonce("muster/"+person.Value),CourtAction.SendMusterNotice,person,control.Subject,site.Value,Amount:(long)men.Value,When:checked(w.ElapsedSeconds+86400L*(long)days.Value))));
    }
   }
   return;
  }
  if(courtCategory==3){
   var history=w.Court?.Events??[];
   foreach(var former in HouseRestoration.ObserveFormerSeat(w,actor)){
    SocialText(box,$"旧領の見聞：{NewGameWorld.SiteName(former.Site.Value)}で旧主の支配が変わったことを確認した。");
    foreach(var peer in w.Characters.Where(c=>c.Id!=actor && c.Life==LifeState.Adult && SocialWorld.Site(w,c.Id)==site)){
     if(history.Any(e=>e.Input.Action==CourtAction.SeekRestorationSupport && e.Actor==actor && e.Input.Person==peer.Id && e.Input.Owner==former.House && e.Place==site))continue;
     var key="restore/"+former.House.Value+"/"+peer.Id.Value;
     Button(key,MeritRewards.DisplayName(w,actor,peer.Id)+"に旧家再興の支持を求める",()=>Run(new(Nonce(key),CourtAction.SeekRestorationSupport,peer.Id,former.House,former.Site)));
    }
   }
   foreach(var known in history.Where(e=>e.Input.Action==CourtAction.RecordRecoveredSeat && e.Actor==actor))
    SocialText(box,$"{WorldCalendar.Display(w.EpochDate,known.At)}　{NewGameWorld.SiteName(known.Place.Value)}の本丸と旧家の守備兵を確認");
   foreach(var recoveringHouse in history.Where(e=>e.Input.Action==CourtAction.SeekRestorationSupport && e.Actor==actor && e.Place==site).Select(e=>e.Input.Owner).Distinct()){
    var successor=HouseRestoration.SuccessorOrganization(w,recoveringHouse,w.ElapsedSeconds,rev+1);
    if(successor!=null)SocialText(box,"旧家の家督と再興後の家の原本を照合済み");
    else foreach(var application in HouseRestoration.ContinuityApplications(w,actor).Where(i=>i.Owner==recoveringHouse))
     Button(application.Id.Value,"旧家の家督と現在の家の原本を照合する",()=>Run(application));
    if(HouseRestoration.SecuredKeep(w,actor,recoveringHouse,site.Value) is not {} capture ||
       history.Any(e=>e.Input.Action==CourtAction.RecordRecoveredSeat && e.Actor==actor && e.Input.Reference==capture))continue;
    var key="recovered/"+capture.Value;
    Button(key,"この本丸の回復を現地で記録する",()=>Run(new(Nonce(key),CourtAction.RecordRecoveredSeat,actor,recoveringHouse,site.Value,Reference:capture)));
   }
   foreach(var ask in history.Where(e=>e.Input.Action==CourtAction.SeekRestorationSupport && (e.Actor==actor || e.Input.Person==actor))){
    foreach(var claim in history.Where(e=>e.Input.Action==CourtAction.PresentRestorationArrears && e.Input.Person==ask.Actor && e.Actor==ask.Input.Person && e.Input.Owner==ask.Input.Owner))
    {
     SocialText(box,$"{WorldCalendar.Display(w.EpochDate,claim.At)}に提示した未払い扶持：{claim.Input.Amount}kg（提示時点）");
     if(HouseRestoration.SettlementForClaim(w,claim.Input.Id) is {} payment){
      var title=payment.Action==SalaryAction.Prepare?$"蔵の米{payment.Amount}kgをこの扶持に引き当てる":"引き当てた扶持米を現地で受け渡す";
      Button("settle/"+claim.Input.Id.Value,title,()=>{
       var result=Session.ManageSalary(payment,actor,rev);message.Text=result.VisibleReason;Refresh();ShowHouseholdCourt();
      });
     }
    }
    if(actor==ask.Input.Person && SocialWorld.Site(w,ask.Actor)==site){
     foreach(var term in (w.Salary?.Terms??[]).Where(t=>t.Employee==actor && t.Owner==ask.Input.Owner)){
      long owed=HouseRestoration.UnpaidSalary(term,w.ElapsedSeconds);
      if(owed<=0 || history.Any(e=>e.Input.Action==CourtAction.PresentRestorationArrears && e.Input.Reference==term.Id && e.Input.Person==ask.Actor && e.Input.Amount==owed))continue;
      var key="restoration-arrears/"+term.Id.Value+"/"+ask.Actor.Value;
      Button(key,$"未払い扶持{owed}kgの原本を示す",()=>Run(new(Nonce(key),CourtAction.PresentRestorationArrears,ask.Actor,term.Owner,site.Value,Amount:owed,Reference:term.Id)));
     }
    }
    var reply=history.SingleOrDefault(e=>e.Input.Reference==ask.Input.Id && e.Input.Action is CourtAction.SupportRestoration or CourtAction.ReserveRestoration);
    var peer=actor==ask.Actor?ask.Input.Person:ask.Actor;
    SocialText(box,MeritRewards.DisplayName(w,actor,peer)+"との再興相談："+(reply==null?"返答待ち":reply.Input.Action==CourtAction.SupportRestoration?"個人として支持":"判断を留保"));
    if(reply?.Input.Action==CourtAction.SupportRestoration){
     var escort=history.LastOrDefault(e=>e.Input.Action==CourtAction.OfferRestorationEscort && e.Input.Reference==reply.Input.Id);
     if(escort!=null){
      var response=history.SingleOrDefault(e=>e.Input.Reference==escort.Input.Id && e.Input.Action is CourtAction.AcceptRestorationEscort or CourtAction.DeclineRestorationEscort);
      var released=response!=null && history.Any(e=>e.Input.Action==CourtAction.ReleaseRestorationEscort && e.Input.Reference==response.Input.Id);
      if(!released && response?.Input.Action==CourtAction.AcceptRestorationEscort && actor==response.Actor && SocialWorld.Site(w,peer)==site && escort.Input.When>w.ElapsedSeconds){
       var key="release-escort/"+response.Input.Id.Value;
       Button(key,"指揮者に警護の終了を伝える",()=>Run(new(Nonce(key),CourtAction.ReleaseRestorationEscort,peer,escort.Input.Owner,site.Value,Reference:response.Input.Id,When:escort.Input.When)));
      }
      SocialText(box,"後援家の現地警護："+(released?"指揮者に終了を伝達済み":escort.Input.When<=w.ElapsedSeconds?"期限終了":response==null?"条件提示中":response.Input.Action==CourtAction.AcceptRestorationEscort?"合意済み":"辞退"));
      if(response?.Input.Action==CourtAction.AcceptRestorationEscort && RestorationEscort.WitnessedAtAssembly(w,actor,response) is >0 and var soldiers)
       SocialText(box,$"集合地で確認した後援家の兵：{soldiers}人");
      if(response==null && actor==escort.Input.Person && site==ask.Place && SocialWorld.Site(w,peer)==site && escort.Input.When>w.ElapsedSeconds)
       foreach(var answer in new[]{CourtAction.AcceptRestorationEscort,CourtAction.DeclineRestorationEscort}){
        var key=answer+"/"+escort.Input.Id.Value;
        Button(key,answer==CourtAction.AcceptRestorationEscort?"旧領での警護を依頼する":"警護を辞退する",()=>Run(new(Nonce(key),answer,peer,escort.Input.Owner,ask.Place,Reference:escort.Input.Id,When:escort.Input.When)));
       }
     }
     if(reply.Actor==actor && (escort==null || escort.Input.When<=w.ElapsedSeconds) && site==ask.Place && SocialWorld.Site(w,peer)==site){
      foreach(var sponsor in HouseSuccession.Factions(w).Select(f=>new EntityId(f.Id)).Where(h=>h!=ask.Input.Owner && FactionStances.ResolveAuthority(w,actor,h,w.ElapsedSeconds,rev)!=null)){
       var key="escort/"+reply.Input.Id.Value+"/"+sponsor.Value;
       Button(key,"家として七日間の現地警護を申し出る",()=>Run(new(Nonce(key),CourtAction.OfferRestorationEscort,peer,sponsor,ask.Place,Reference:reply.Input.Id,When:w.ElapsedSeconds+7*86400L)));
      }
     }
     var pledge=history.SingleOrDefault(e=>e.Input.Reference==reply.Input.Id && e.Input.Action==CourtAction.PledgeRestorationRice);
     var delivered=pledge!=null && history.Any(e=>e.Input.Reference==pledge.Input.Id && e.Input.Action==CourtAction.HandOverRestorationRice);
     if(pledge!=null)SocialText(box,$"支援米 {pledge.Input.Amount}kg："+(delivered?"受渡し済み":"持参待ち"));
     if(reply.Actor==actor && site==ask.Place && SocialWorld.Site(w,peer)==site && !delivered){
      var key="restoration-rice/"+reply.Input.Id.Value;
      if(pledge==null)Button(key,"私有米1kgの支援を約束する",()=>Run(new(Nonce(key),CourtAction.PledgeRestorationRice,peer,ask.Input.Owner,ask.Place,Amount:1,Reference:reply.Input.Id)));
      else if(HouseSalary.PrivateRice(w,actor)>=pledge.Input.Amount)Button(key,"約束した私有米を手渡す",()=>Run(new(Nonce(key),CourtAction.HandOverRestorationRice,peer,ask.Input.Owner,ask.Place,Amount:pledge.Input.Amount,Reference:pledge.Input.Id)));
     }
    }
    if(reply!=null || ask.Input.Person!=actor || site!=ask.Place || SocialWorld.Site(w,peer)!=site)continue;
    foreach(var choice in new[]{CourtAction.SupportRestoration,CourtAction.ReserveRestoration}){
     var key=choice+"/"+ask.Input.Id.Value;
     Button(key,choice==CourtAction.SupportRestoration?"個人として支持する":"判断を留保する",()=>Run(new(Nonce(key),choice,peer,ask.Input.Owner,ask.Place,Reference:ask.Input.Id)));
    }
   }
  }
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
  foreach(var proposal in w.Court?.Events.Where(e=>e.Input.Action==CourtAction.ConsultReform && e.Input.Person==actor)??[]){
   var input=proposal.Input;var letter=HouseReforms.Letter(input,HouseReforms.Proposal);
   if(!w.Messaging.Receipts.Any(r=>r.MessageId==letter && r.ReceiverId==actor.Value) ||
       w.Messaging.Messages.Any(m=>m.Id==HouseReforms.Letter(input,HouseReforms.Answer)))continue;
   if(!HakutaiKagami.Core.Messaging.MessageFlow.Knows(w.Messaging,letter,actor.Value)){
    Button("open-reform-"+input.Id,"届いた改革の相談状を開く",()=>{Session.Open(actor,new(letter),rev);Refresh();ShowHouseholdCourt();});
    continue;
   }
   SocialText(box,w.Messaging.Messages.Single(m=>m.Id==letter).Body);
   foreach(var choice in new[]{CourtAction.AssentReform,CourtAction.CounterReform,CourtAction.RejectReform})
    Button("reply-"+input.Id+choice,HouseReforms.ReplyText(choice),()=>Run(new(Nonce("reply/"+input.Id+choice),choice,
        proposal.Actor,input.Owner,input.Site,Reference:input.Id)));
  }
  foreach(var assent in w.Court?.Events.Where(e=>e.Actor==actor && e.Input.Action==CourtAction.AssentReform)??[]){
   var original=w.Court!.Events.Single(e=>e.Input.Id==assent.Input.Reference);
   if(original.Input.Reform!=ReformKind.DirectService ||
      w.Court.Events.Any(e=>e.Input.Reference==original.Input.Id && e.Input.Action==CourtAction.PresentServiceRelease))continue;
   var bond=w.ServiceBonds.SingleOrDefault(b=>b.Subject==actor && b.Target!=original.Actor && b.From<=w.ElapsedSeconds && w.ElapsedSeconds<b.Until);
   if(bond!=null && SocialWorld.Site(w,bond.Target)==site)
    Button("present-service-"+assent.Input.Id,"旧主に直臣化の相談状を示す",()=>Run(new CourtInput(Nonce("show-service"),CourtAction.PresentServiceRelease,bond.Target,original.Input.Owner,site.Value,Reference:original.Input.Id){FormerOffice=bond.Id}));
  }
  foreach(var shown in w.Court?.Events.Where(e=>e.Input.Action==CourtAction.PresentServiceRelease && e.Input.Person==actor && e.Place==site)??[]){
   if(SocialWorld.Site(w,shown.Actor)!=site || w.Court!.Events.Any(e=>e.Input.Reference==shown.Input.Id &&
      e.Input.Action is CourtAction.ConsentServiceRelease or CourtAction.RefuseServiceRelease))continue;
   foreach(var action in new[]{CourtAction.ConsentServiceRelease,CourtAction.RefuseServiceRelease})
    Button("service-answer-"+shown.Input.Id+action,action==CourtAction.ConsentServiceRelease?"旧主として奉公替えを認める":"奉公替えを断る",()=>Run(new CourtInput(Nonce("service-answer"),action,shown.Actor,shown.Input.Owner,shown.Place,Reference:shown.Input.Id){FormerOffice=shown.Input.FormerOffice}));
  }
  foreach(var consent in w.Court?.Events.Where(e=>e.Input.Action==CourtAction.ConsentServiceRelease && e.Input.Person==actor)??[]){
   var shown=w.Court!.Events.Single(e=>e.Input.Id==consent.Input.Reference);
   var proposal=w.Court.Events.Single(e=>e.Input.Id==shown.Input.Reference);
   var history=w.HouseService?.Entries.Where(e=>e.Input.ReformConsent==consent.Input.Id).ToArray()??[];
   if(history.Length>=2)continue;
   bool release=history.Length==0;
   Button("complete-direct-"+consent.Input.Id,release?"本拠で旧主との奉公を解く":"本拠で当主に直接奉公する",()=>{
    var terms=new HouseholdServiceInput(Nonce("direct-service"),release?HouseholdServiceAction.Release:HouseholdServiceAction.Pledge,consent.Input.Owner,
        release?consent.Actor:proposal.Actor,release?consent.Input.FormerOffice:null){ReformConsent=consent.Input.Id};
    var result=Session.ManageHouseholdService(terms,actor,rev);message.Text=result.VisibleReason;Refresh();ShowHouseholdCourt();
   });
  }
  foreach(var order in w.Court?.Events.Where(e=>e.Input.Action==CourtAction.IssueTransfer && e.Actor==actor)??[]){
   var terms=w.Court!.Events.Single(e=>e.Input.Id==order.Input.Reference).Input;
   if(terms.Reform!=ReformKind.AllocateProvince || (site!=terms.Site && site!=terms.NewEstate) ||
      SocialWorld.Site(w,terms.Person)!=site || w.Court.Events.Any(e=>e.Input.Reference==order.Input.Id && e.Place==site &&
          e.Input.Action is CourtAction.ReceiveProvinceShare or CourtAction.DeliverProvinceShare))continue;
   Button("deliver-province-"+order.Input.Id,"現地で"+PersonName(terms.Person)+"に徴収米の給付を引き渡す",()=>Run(new(Nonce("deliver-province/"+order.Input.Id),CourtAction.DeliverProvinceShare,terms.Person,terms.Owner,site.Value,Amount:terms.Amount,Reference:order.Input.Id)));
  }
  foreach(var order in w.Court?.Events.Where(e=>e.Input.Action==CourtAction.IssueTransfer && e.Input.Person==actor)??[]){
   var id=HouseReforms.Letter(order.Input,HouseReforms.TransferOrder);
   if(!w.Messaging.Receipts.Any(r=>r.MessageId==id && r.ReceiverId==actor.Value))continue;
   if(!HakutaiKagami.Core.Messaging.MessageFlow.Knows(w.Messaging,id,actor.Value)){
    Button("read-transfer-"+order.Input.Id,"届いた移封命令を開く",()=>{Session.Open(actor,new(id),rev);Refresh();ShowHouseholdCourt();});
    continue;
   }
   var terms=w.Court!.Events.Single(e=>e.Input.Id==order.Input.Reference).Input;
   var released=w.Court.Events.Any(e=>e.Input.Action==CourtAction.ReleaseCastleOffice && e.Input.Reference==order.Input.Id);
   var entered=w.Court.Events.Any(e=>e.Input.Action==CourtAction.EnterCastleOffice && e.Input.Reference==order.Input.Id);
   if(site==terms.NewEstate && Session.CastellanView() is {} administration){
    var custody=administration.Appointments.LastOrDefault(c=>c.Site==site && c.Person==actor && c.CanReceive);
    if(custody?.Receipt is {} receipt)
     SocialText(box,$"自分が最後に確認した蔵の数量（{WorldCalendar.Display(w.EpochDate,receipt.ObservedAt)}）：米{receipt.Rice}kg、木材{receipt.Wood}、武具{receipt.Arms}、家金{receipt.Cash}文");
    else SocialText(box,custody==null?"蔵の受領には、現地で当主と行政・保管の担当を取り決める必要があります。":"行政・保管の担当原本があります。現地の蔵を確認できます。");
    Button("transfer-store-"+order.Input.Id,"行政・保管の引継ぎを開く",ShowCastleAdministration);
   }
   if(HouseReforms.Baggage(w,order) is {} baggage && (!entered || baggage.HouseShipments.Length>0 || baggage.EntrustedLots.Length>0)){
    SocialText(box,$"赴任の携行品　本人が使える銭 {baggage.Purse}文");
    foreach(var item in baggage.PersonalItems)SocialText(box,$"私物　{item.Name} ×{item.Quantity}");
    foreach(var lot in baggage.EntrustedLots)SocialText(box,$"預かり荷　{lot.Quantity}{lot.Unit} ／ 所有者 {lot.Owner.Value}");
    if(baggage.EntrustedLots.Length>0)Button("transfer-cargo-"+order.Input.Id,"元の運搬依頼で受渡し先を確認",ShowTransportWork);
    foreach(var job in baggage.HouseShipments)SocialText(box,$"家の運搬　{MaterialMarkets.Name(job.Terms.Commodity)} {job.Cargo} ／ 預かり金 {job.Cash}文 ／ 納入先 {NewGameWorld.SiteName(job.Terms.Home.Value)}");
    if(baggage.HouseShipments.Length>0)Button("transfer-freight-"+order.Input.Id,"家の仕入れ・納入の原本を開く",ShowHouseFreight);
   }
   if(terms.Reform==ReformKind.AllocateProvince){
    foreach(var target in new[]{terms.Site,terms.NewEstate!.Value}){
     var received=w.Court.Events.Any(e=>(e.Input.Action is CourtAction.ReceiveProvinceShare or CourtAction.DeliverProvinceShare) && e.Input.Reference==order.Input.Id && e.Place==target);
     SocialText(box,NewGameWorld.SiteName(target.Value)+(received?"　受領済み":"　未受領"));
     if(!received && site==target)Button("receive-province-"+order.Input.Id+target,"この土地の徴収米の給付を引き取る",()=>Run(new(Nonce("province/"+target),CourtAction.ReceiveProvinceShare,order.Actor,terms.Owner,target,Amount:terms.Amount,Reference:order.Input.Id)));
    }
    continue;
   }
   if(terms.Reform==ReformKind.CastleResidence){
    SocialText(box,"現在の居所："+NewGameWorld.SiteName(HouseReforms.ResidenceAt(w,actor,w.ElapsedSeconds,rev+1).Value));
    var move=w.Court!.Events.FirstOrDefault(e=>e.Input.Action==CourtAction.SettleCastleResidence && e.Input.Reference==order.Input.Id);
    if(move!=null && site==move.Place && !w.Court.Events.Any(e=>e.Input.Action==CourtAction.ReportResidence && e.Input.Reference==move.Input.Id))
     Button("report-residence-"+order.Input.Id,"当主に新しい居所を知らせる",()=>Run(new(Nonce("report-residence/"+order.Input.Id),CourtAction.ReportResidence,order.Actor,terms.Owner,move.Place,Reference:move.Input.Id)));
    if(site==terms.NewEstate && HouseReforms.ResidenceAt(w,actor,w.ElapsedSeconds,rev+1)==terms.Site)
     Button("reside-"+order.Input.Id,"この城下に居所を移す",()=>Run(new(Nonce("reside/"+order.Input.Id),CourtAction.SettleCastleResidence,order.Actor,terms.Owner,site.Value,Reference:order.Input.Id)));
    continue;
   }
   if(terms.Reform==ReformKind.Confiscate && terms.LevyGrant is {} surrender){
    if(site==terms.Site && HouseReforms.LevyEnd(w,surrender)==long.MaxValue)
     Button("surrender-"+order.Input.Id,"当主に徴収米の給付原本を返上する",()=>Run(new CourtInput(Nonce("surrender/"+order.Input.Id),CourtAction.SurrenderLevyShare,order.Actor,terms.Owner,site.Value,Amount:terms.Amount,Reference:order.Input.Id){LevyGrant=surrender}));
    continue;
   }
   if(terms.LevyGrant is {} oldGrant){
    if(site==terms.NewEstate && HouseReforms.LevyEnd(w,oldGrant)==long.MaxValue)
     Button("exchange-"+order.Input.Id,"新領で徴収米の給付原本を切り替える",()=>Run(new CourtInput(Nonce("exchange/"+order.Input.Id),
         CourtAction.ExchangeLevyShare,order.Actor,terms.Owner,site.Value,Amount:terms.Amount,Reference:order.Input.Id){LevyGrant=oldGrant}));
    continue;
   }
   if(entered)continue;
   SocialText(box,w.Messaging.Messages.Single(m=>m.Id==id).Body);
   if(!released && site==terms.Site && w.Offices.FirstOrDefault(o=>o.Subject==actor && o.Target==site &&
       o.From<=w.ElapsedSeconds && w.ElapsedSeconds<o.Until && (o.Kind is "house-castellan" or "castellan")) is {} office)
    Button("release-"+order.Input.Id,"旧城で城主職を引き渡す",()=>Run(new(Nonce("release/"+order.Input.Id),CourtAction.ReleaseCastleOffice,
        order.Actor,terms.Owner,site.Value,Reference:order.Input.Id){FormerOffice=office.Id}));
   else if(released && site==terms.NewEstate && w.Offices.FirstOrDefault(o=>o.Subject!=actor && o.Target==site &&
       o.From<=w.ElapsedSeconds && w.ElapsedSeconds<o.Until && (o.Kind is "house-castellan" or "castellan")) is {} incumbent){
    if(w.Court.Events.Any(x=>x.Input.Action==CourtAction.PresentTransfer && x.Input.Reference==order.Input.Id && x.Input.FormerOffice==incumbent.Id))
     SocialText(box,"前任者に命令書を提示済み。城主職の引渡しを待っています。");
    else Button("present-"+order.Input.Id,"前任者に命令書を示して引渡しを求める",()=>Run(new CourtInput(Nonce("present/"+order.Input.Id),
        CourtAction.PresentTransfer,incumbent.Subject,terms.Owner,site.Value,Reference:order.Input.Id){FormerOffice=incumbent.Id}));
   }
   else if(released && site==terms.NewEstate)
    Button("enter-"+order.Input.Id,"新城で着任する",()=>Run(new(Nonce("enter/"+order.Input.Id),CourtAction.EnterCastleOffice,
        order.Actor,terms.Owner,site.Value,Reference:order.Input.Id)));
   else if(released)SocialText(box,"赴任先："+NewGameWorld.SiteName(terms.NewEstate!.Value.Value));
  }
  foreach(var shown in w.Court?.Events.Where(e=>e.Input.Action==CourtAction.PresentTransfer && e.Input.Person==actor && e.Place==site)??[]){
   if(w.Offices.Any(o=>o.Id==shown.Input.FormerOffice && o.Until>w.ElapsedSeconds))
    Button("yield-"+shown.Input.Id,"提示された命令書を認めて後任に職を渡す",()=>Run(shown.Input with{
        Id=Nonce("yield/"+shown.Input.Id),Action=CourtAction.YieldCastleOffice,Person=shown.Actor}));
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
   if(!head){
    var peers=roster.Where(p=>SocialWorld.Site(w,p)==site).ToArray();
    if(peers.Length>0){
     var peer=new OptionButton();foreach(var p in peers)peer.AddItem(PersonName(p));box.AddChild(peer);
     Button("start-fight","私闘を申し込む",()=>Run(Command("fight",CourtAction.ChallengeFight,peers[peer.Selected])));
    }
   }
   foreach(var act in w.Court?.Events.Where(e=>e.Place==site && e.Input.Owner==house)??[]){
    if(act.Input.Action==CourtAction.ChallengeFight && act.Input.Person==actor && SocialWorld.Site(w,act.Actor)==site &&
       !w.Court!.Events.Any(e=>e.Input.Reference==act.Input.Id && e.Input.Action is CourtAction.JoinFight or CourtAction.DeclineFight)){
     foreach(var choice in new[]{CourtAction.JoinFight,CourtAction.DeclineFight})Button("fight-"+act.Input.Id+choice,
         PersonName(act.Actor)+(choice==CourtAction.JoinFight?"と私闘する":"との私闘を断る"),()=>Run(Command("fight-reply",choice,act.Actor,reference:act.Input.Id)));
    }
    if(head && act.Input.Action==CourtAction.JoinFight && HouseReforms.EqualPenaltyAt(w,house,act.Place,act.At,act.Revision) &&
       HouseReforms.WitnessedFight(w,actor,act) && !w.Court!.Events.Any(e=>e.Input.Action==CourtAction.CensureBoth && e.Input.Reference==act.Input.Id))
     Button("censure-"+act.Input.Id,"私闘の双方を叱責する",()=>Run(Command("censure",CourtAction.CensureBoth,act.Actor,reference:act.Input.Id)));
   }
   foreach(var breach in HouseReforms.KnownLetterBreaches(w,actor))
    SocialText(box,WorldCalendar.Display(w.EpochDate,breach.At)+" ／ 他国への私信を発送："+PersonName(breach.Actor)+" → "+PersonName(breach.Input.Person));
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
   foreach(var grant in HouseReforms.LevyGrants(w).Where(e=>e.Place==site &&
       (e.Input.Person==actor || e.Actor==actor)))
   {
    long earned=HouseReforms.AccruedLevyRice(w,grant,w.ElapsedSeconds,rev+1);
    long received=HouseReforms.ReceivedLevyRice(w,grant.Input.Id,w.ElapsedSeconds,rev+1);
    SocialText(box,$"徴収米の給付　{PersonName(grant.Input.Person)}　{grant.Input.Amount}%　受領 {received}kg ／ 未受領 {earned-received}kg");
    if(actor!=grant.Input.Person || earned<=received)continue;
    long available=HouseholdCourt.AvailableLevyRice(w,grant,actor,w.ElapsedSeconds,rev+1);
    if(available==0)SocialText(box,"受渡し待ち。当主と現地で会い、蔵に引き渡せる米があると受領できます。");
    else{
     var take=new SpinBox{MinValue=1,MaxValue=available,Step=1,Value=available,Suffix="kg"};box.AddChild(take);
     Button("levy-receive-"+grant.Input.Id,"現地で米を受け取る",()=>Run(Command("receive/"+grant.Input.Id,CourtAction.ReceiveLevyShare,grant.Actor,(long)take.Value,grant.Input.Id)));
    }
   }
   var roads=HouseReforms.LocalRoads(w,site.Value);
   var roadChoice=new OptionButton();
   if(head && roads.Length>0){
    foreach(var road in roads){
     var neighbor=road.From==NewGameWorld.Node(site.Value.Value)?road.To:road.From;
     roadChoice.AddItem(NewGameWorld.SiteName(RegionalCampaign.Site(neighbor).Value)+(HouseReforms.BarrierAt(w,road.Id,w.ElapsedSeconds,rev+1)==null?"方面":"方面（関所あり）"));
    }
    box.AddChild(roadChoice);
    Button("establish-barrier","この道に関所を置く",()=>Run(Command("barrier/"+roads[roadChoice.Selected].Id,CourtAction.EstablishBarrier,actor) with{BarrierEdge=roads[roadChoice.Selected].Id}));
   }
   if(head&&roster.Length>0){
    var persons=new OptionButton();foreach(var person in roster)persons.AddItem(PersonName(person));box.AddChild(persons);
    var share=new SpinBox{MinValue=1,MaxValue=100,Step=1,Value=10,Suffix="%"};
    box.AddChild(share);
    Button("grant-levy-share","この家臣に現地の徴収米の取り分を定める",()=>Run(Command("levy-share",CourtAction.GrantLevyShare,roster[persons.Selected],(long)share.Value)));
    var kinds=Enum.GetValues<ReformKind>();var reform=new OptionButton();foreach(var k in kinds)reform.AddItem(HouseReforms.Name(k));box.AddChild(reform);
    var destinations=w.LandRights.Where(r=>r.Subject==house && r.Kind=="control" && r.Target!=site.Value && r.From<=w.ElapsedSeconds && w.ElapsedSeconds<r.Until).Select(r=>r.Target).Distinct().ToArray();
    var estate=new OptionButton();foreach(var dest in destinations)estate.AddItem("新領："+NewGameWorld.SiteName(dest.Value));
    if(destinations.Length==0)estate.AddItem("指定できる新領なし");
    box.AddChild(estate);
    void ShowEstate()=>estate.Visible=kinds[reform.Selected] is ReformKind.Relocate or ReformKind.ChangeFief or ReformKind.CastleResidence or ReformKind.AllocateProvince;
    reform.ItemSelected+=_=>ShowEstate();ShowEstate();
    CourtEvent? OfferedLevy()=>kinds[reform.Selected] is ReformKind.ChangeFief or ReformKind.Confiscate?HouseReforms.LevyGrants(w).SingleOrDefault(g=>
        g.Input.Person==roster[persons.Selected] && g.Place==site && g.Input.Owner==house && HouseReforms.LevyEnd(w,g.Input.Id)==long.MaxValue):null;
    Button("consult-reform","この改革案について意向を問う",()=>Run(Command("reform",CourtAction.ConsultReform,roster[persons.Selected]) with{Reform=kinds[reform.Selected],LevyGrant=OfferedLevy()?.Input.Id,Amount=kinds[reform.Selected]==ReformKind.AllocateProvince?(long)share.Value:OfferedLevy()?.Input.Amount??0,NewEstate=estate.Visible && destinations.Length>0?destinations[estate.Selected]:null,BarrierEdge=kinds[reform.Selected]==ReformKind.RemoveBarrier && roads.Length>0?roads[roadChoice.Selected].Id:null}));
   }
   foreach(var e in w.Court?.Events.Where(e=>e.Input.Action==CourtAction.ConsultReform&&e.Actor==actor)??[]){
    var id=HouseReforms.Letter(e.Input,HouseReforms.Answer);
    if(HouseReforms.AnswerKnown(w,e,actor)){
     SocialText(box,PersonName(e.Input.Person)+" ／ "+w.Messaging.Messages.Single(m=>m.Id==id).Body);
     if(w.Court!.Events.Any(d=>d.Input.Action==CourtAction.IssueTransfer&&d.Input.Reference==e.Input.Id))
      SocialText(box,"承諾済みの条件で移封の命令書を発送済み");
     else if(w.Court!.Events.Any(d=>d.Input.Action==CourtAction.AbolishBarrier&&d.Input.Reference==e.Input.Id))
      SocialText(box,"この道路の関所を撤廃済み");
     else if(w.Court!.Events.Any(d=>d.Input.Action==CourtAction.ReduceCastle&&d.Input.Reference==e.Input.Id))
      SocialText(box,"城割り済み。外堀と城門の防備を縮小しました");
     else if(w.Court!.Events.Any(d=>d.Input.Action==CourtAction.EnactSuccessionLaw&&d.Input.Reference==e.Input.Id))
      SocialText(box,"後継の既定順位を制定済み");
     else if(w.Court!.Events.Any(d=>d.Input.Action==CourtAction.EnactEqualPenalty && d.Input.Reference==e.Input.Id))SocialText(box,"喧嘩両成敗を制定済み");
     else if(w.Court!.Events.Any(d=>d.Input.Action==CourtAction.EnactMarriageLaw&&d.Input.Reference==e.Input.Id))
      SocialText(box,"この拠点では婚姻に許可願が必要です");
     else if(w.Court!.Events.Any(d=>d.Input.Action==CourtAction.EnactLetterLaw&&d.Input.Reference==e.Input.Id))
      SocialText(box,"この拠点で他国との私信を禁じる家法が発効済み");
     else if(w.Court!.Events.Any(d=>d.Input.Action==CourtAction.OpenLocalMarket&&d.Input.Reference==e.Input.Id))
      SocialText(box,"この市場で楽市を実施済み");
     else if(w.Court!.Events.Any(d=>d.Input.Action==CourtAction.DeferReform&&d.Input.Reference==e.Input.Id))
      SocialText(box,"この相談案は見送り済み");
     else if(head && site==e.Input.Site && house==e.Input.Owner){
      if((e.Input.NewEstate!=null || e.Input.Reform==ReformKind.Confiscate && e.Input.LevyGrant!=null) && w.Messaging.Messages.Single(m=>m.Id==id).Body==HouseReforms.Name(e.Input.Reform!.Value)+"への返答："+HouseReforms.ReplyText(CourtAction.AssentReform))
       Button("transfer-"+e.Input.Id,e.Input.Reform==ReformKind.Confiscate?"承諾された給付原本の返上を命じる":"承諾された条件で移転を発令する",()=>Run(Command("issue-transfer/"+e.Input.Id,CourtAction.IssueTransfer,e.Input.Person,reference:e.Input.Id)));
      if(e.Input.Reform==ReformKind.RemoveBarrier && e.Input.BarrierEdge is {} edge && HouseReforms.BarrierAt(w,edge,w.ElapsedSeconds,rev+1)!=null)
       Button("remove-barrier-"+e.Input.Id,"相談した道路の関所を撤廃する",()=>Run(Command("remove-barrier/"+e.Input.Id,CourtAction.AbolishBarrier,e.Input.Person,reference:e.Input.Id) with{BarrierEdge=edge}));
      if(e.Input.Reform==ReformKind.ReduceCastles && CastleWorks.Enabled(w) && MaterialMarkets.Sites(w).Contains(e.Input.Site) && HouseReforms.CastleReduction(w,e.Input.Site,w.ElapsedSeconds,rev+1)==null)
       Button("reduce-castle-"+e.Input.Id,"城割りを実施し、外堀と城門を縮小する",()=>Run(Command("reduce-castle/"+e.Input.Id,CourtAction.ReduceCastle,e.Input.Person,reference:e.Input.Id)));
      if(e.Input.Reform==ReformKind.SuccessionOrder && !HouseReforms.SuccessionLawAt(w,house,e.Input.Site,w.ElapsedSeconds,rev+1))
       Button("succession-law-"+e.Input.Id,"後継の既定順位を制定する",()=>Run(Command("succession-law/"+e.Input.Id,CourtAction.EnactSuccessionLaw,e.Input.Person,reference:e.Input.Id)));
      if(e.Input.Reform==ReformKind.EqualPenalty && !HouseReforms.EqualPenaltyAt(w,house,e.Input.Site,w.ElapsedSeconds,rev+1))
       Button("equal-"+e.Input.Id,"この拠点で喧嘩両成敗を制定する",()=>Run(Command("equal/"+e.Input.Id,CourtAction.EnactEqualPenalty,e.Input.Person,reference:e.Input.Id)));
      if(e.Input.Reform==ReformKind.MarriagePermission && !HouseReforms.MarriageLawAt(w,house,e.Input.Site,w.ElapsedSeconds,rev+1))
       Button("marriage-law-"+e.Input.Id,"この拠点で婚姻を許可制にする",()=>Run(Command("marriage-law/"+e.Input.Id,CourtAction.EnactMarriageLaw,e.Input.Person,reference:e.Input.Id)));
      if(e.Input.Reform==ReformKind.ForeignLetters && !HouseReforms.LetterLawAt(w,house,e.Input.Site,w.ElapsedSeconds,rev+1))
       Button("law-"+e.Input.Id,"この拠点で他国との私信を禁じる",()=>Run(Command("law/"+e.Input.Id,CourtAction.EnactLetterLaw,e.Input.Person,reference:e.Input.Id)));
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
