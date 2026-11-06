using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 readonly Dictionary<string,Button> schemeReviewButtons=new();
 internal void ReviewSchemePress(string key){if(!schemeReviewButtons.TryGetValue(key,out var b)||b.Disabled)throw new InvalidOperationException("この情報操作は選べません："+key);b.EmitSignal(Button.SignalName.Pressed);}
 void ShowInformationSchemes()
 {
  if(IsSaving||IsClockControlling)return;var(box,rev)=SocialWindow("情報・書状の照合・密偵");schemeReviewButtons.Clear();var w=Snapshot();var actor=Session.PlayerControl;var site=SocialWorld.Site(w,actor);if(site==null){if(CastleSiege.Enabled(w))Button("contact-menu","道中の人物・目撃記録",ShowPersonalContact);SocialText(box,"現地へ到着してから相談してください。");return;}
  void Button(string key,string label,Action action){var b=new Button{Text=label};box.AddChild(b);schemeReviewButtons[key]=b;b.Pressed+=action;}
  void Run(SchemeInput i){if(IsSaving||IsClockControlling)return;CancelAdvancePrefetch();var r=Session.ManageScheme(i,rev);message.Text=r.VisibleReason;GD.Print("SCHEME_RESULT "+i.Action+" "+r.Code+" "+r.VisibleReason);Refresh();ShowInformationSchemes();}
  EntityId Nonce(string key)=>new("request:scheme-ui/"+rev+"/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(key))[..24]);
  SocialText(box,"噂・書状の記載・本人の照合結果は別です。未確認情報で処分・離反・開戦を自動的に決めません。");
  foreach(var option in IndependenceGate.Available(w,actor)){
      string label=option.Action switch{
          CourtAction.RequestIndependentGate=>"旧主に城門の引渡しを申し入れる",
          CourtAction.AllowIndependentGate=>"城門の引渡しに同意する",
          CourtAction.RefuseIndependentGate=>"城門の引渡しを断る",
          CourtAction.ReceiveIndependentGate=>"立会いのもとで城門を受け取る",
          CourtAction.ReceiveIndependentKeep=>"守備の退去を確認し、本丸を受け取る",
          _=>throw new InvalidOperationException()
      };
      Button(option.Id.Value,label,()=>{var result=Session.ManageCourt(option,rev);message.Text=result.VisibleReason;Refresh();ShowInformationSchemes();});
  }
  if(JointIndependence.ObserveOwnCastle(w,actor) is {} guard)
  {
      var house=w.Definitions.Organizations.SingleOrDefault(h=>h.Id==guard.FormerHouse)?.Name??"旧主家";
      if(guard.FormerTroopsPresent)SocialText(box,house+"所属の兵がこの城にいます。");
      SocialText(box,guard.ControlsCastle?"この城の支配は自家に移りました。":"この城の支配は自家に移っていません。");
  }
  if(CharacterRelations.Enabled(w))
  {
      var partners=w.Characters.Where(c=>c.Id!=actor && c.Life==LifeState.Adult && SocialWorld.Site(w,c.Id)==site).ToArray();
      if(partners.Length>0)
      {
          var who=new OptionButton();foreach(var c in partners)who.AddItem(PersonName(c.Id));box.AddChild(who);
          var purposes=Enum.GetValues<PactPurpose>();var terms=new OptionButton();foreach(var p in purposes)terms.AddItem(DefectionPacts.Name(p));box.AddChild(terms);
          Button("consult-pact","選んだ条件で密かに相談",()=>{
              var r=Session.NegotiatePact(Nonce("pact"),partners[who.Selected].Id,purposes[terms.Selected],rev);
              message.Text=r.VisibleReason;Refresh();ShowInformationSchemes();
          });
      }
      foreach(var pact in DefectionPacts.Known(w,actor).Reverse().Take(5))
      {
          var evidence=PactDocuments.Evidence(w,actor).FirstOrDefault(e=>e.Pact==pact.Id);
          if(evidence!=null)SocialText(box,"押収した密書 ／ "+PersonName(pact.Proposer)+" と "+PersonName(pact.Partner)+" ／ "+evidence.At+" 秒時点");
          SocialText(box,PersonName(pact.Partner)+" ／ "+DefectionPacts.Name(pact.Purpose)+(pact.Accepted?"：合意":"：拒否"));
          string JointIndependenceLabel()=>w.Declarations?.Any(d=>d.Founder==actor && d.FormerHouse==pact.House)==true?"独立への参加を呼びかける":"独立を宣言し、参加を呼びかける";
          if(pact.Accepted && pact.Proposer==actor && !PactSignals.Sent(w,pact))
              Button("call-"+pact.Id,pact.Purpose==PactPurpose.RiseTogether?(JointIndependenceLabel()):"実行を求める密書を送る",()=>{var r=Session.SignalPact(pact.Id,rev);message.Text=r.VisibleReason;Refresh();ShowInformationSchemes();});
          var id=PactSignals.Letter(pact,PactSignals.Reply);
          if(HakutaiKagami.Core.Messaging.MessageFlow.Knows(w.Messaging,id,actor.Value))SocialText(box,w.Messaging.Messages.Single(m=>m.Id==id).Body);
          else if(w.Messaging.Receipts.Any(r=>r.MessageId==id && r.ReceiverId==actor.Value))
              Button("open-"+pact.Id,"調略の返書を開く",()=>{var r=Session.Open(actor,new(id),rev);message.Text=r.VisibleReason;Refresh();ShowInformationSchemes();});
      }
  }
  if(CastleSiege.Enabled(w)&&MaterialMarkets.Sites(w).Contains(site.Value))
  {
      var operations=Enum.GetValues<SchemeTopic>().Where(DestructiveSchemes.Operation).ToArray();
      var choice=new OptionButton();
      foreach(var op in operations)choice.AddItem(DestructiveSchemes.Name(op));
      box.AddChild(choice);
      SocialText(box,"現地での手応え："+DestructiveSchemes.Prospect(w,actor,site.Value));
      Button("local-operation","選んだ計略を現地で試みる",()=>Run(new(Nonce("operation"),SchemeAction.ExecuteOperation,actor,site.Value,operations[choice.Selected])));
      var delegates=SocialWorld.CarrierChoices(w,actor).Where(p=>p!=actor).ToArray();
      var targets=MaterialMarkets.Sites(w).Where(p=>p!=site).ToArray();
      if(delegates.Length>0&&targets.Length>0)
      {
          var who=new OptionButton();foreach(var p in delegates)who.AddItem(MeritRewards.DisplayName(w,actor,p));box.AddChild(who);
          var where=new OptionButton();foreach(var target in targets)where.AddItem(NewGameWorld.SiteName(target.Value));box.AddChild(where);
          Button("delegate-operation","選んだ者に現地の実行を任せる",()=>Run(new(Nonce("delegate"),SchemeAction.SendScout,delegates[who.Selected],actor,operations[choice.Selected],Destination:targets[where.Selected])));
      }
      var last=w.Schemes?.Events.LastOrDefault(e=>e.Actor==actor&&e.Input.Action==SchemeAction.ExecuteOperation);
      if(last!=null)SocialText(box,DestructiveSchemes.Outcome(w,last));
  }  if(CastleSiege.Enabled(w))Button("contact-menu","人物の所在・接触",ShowPersonalContact);
  var known=w.Characters.Select(c=>c.Id).Where(p=>p!=actor&&(SocialWorld.Site(w,p)==site||SocialWorld.Facts(w,actor,p).Length>0)).ToArray();
  if(known.Length>0){var receiver=new OptionButton();var subject=new OptionButton();foreach(var person in known){string name=MeritRewards.DisplayName(w,actor,person);receiver.AddItem("話す・渡す相手："+name);subject.AddItem("話題とする人物："+name);}box.AddChild(receiver);box.AddChild(subject);EntityId Person()=>known[receiver.Selected];EntityId Subject()=>known[subject.Selected];
   var topic=new OptionButton();foreach(var t in Enum.GetValues<SchemeTopic>().Where(t=>!DestructiveSchemes.Operation(t)&&t!=SchemeTopic.Assassination))topic.AddItem(InformationSchemes.Statement(t));box.AddChild(topic);SchemeInput Input(string key,SchemeAction action,EntityId? person=null,EntityId? reference=null)=>new(Nonce(key),action,person??Person(),Subject(),(SchemeTopic)topic.Selected,reference);
   Button("whisper","現地の相手に、選んだ噂を流す",()=>Run(Input("whisper",SchemeAction.Whisper)));
   Button("write","自分の名義でこの内容の書状を書く",()=>Run(Input("write",SchemeAction.WriteLetter,actor)));
   Button("forge","選んだ相手の名義を装った書状を作る",()=>Run(Input("forge",SchemeAction.ForgeLetter)));
   foreach(var paper in SocialAssets.Inventory(w,actor).Where(o=>o.Kind=="scheme-letter")){
    Button("give-paper","この書状の実物を、選んだ相手に渡す",()=>{if(IsSaving||IsClockControlling)return;CancelAdvancePrefetch();var r=Session.ManageSocial(new(Nonce("paper/"+paper.Id.Value),SocialWorldAction.DispatchGift,Person()){Asset=paper.Id},rev);message.Text=r.VisibleReason;GD.Print("SCHEME_PAPER "+r.Code);Refresh();ShowInformationSchemes();});
    var source=w.Schemes!.Events.Single(e=>e.Input.Id==paper.Id);Button("verify","この実物を持って、名義人に現地で照合を頼む",()=>Run(Input("verify/"+paper.Id.Value,SchemeAction.VerifyLetter,source.Input.Person,paper.Id)));
   }
   foreach(var claim in InformationSchemes.Claims(w,actor).TakeLast(4)){SocialText(box,claim.Statement+"\n"+claim.Standing+"\n認識："+WorldCalendar.Display(w.EpochDate,claim.LearnedAt));Button("forward","この聞き知った情報を現地の相手へ伝える",()=>Run(Input("forward/"+claim.Id.Value,SchemeAction.Forward,reference:claim.Id)));if(w.Schemes!.Events.Any(e=>e.Input.Id==claim.Id&&e.Input.Action==SchemeAction.VerifyLetter&&e.Actor==actor))Button("denial","この本人照合の結果を現地の相手へ伝える",()=>Run(Input("denial/"+claim.Id.Value,SchemeAction.ShowDenial,reference:claim.Id)));}
  }
  var temple=SocialWorld.Temple(w,site.Value);Button("temple-info","現地の寺社代表が知る寄進・保護の経緯を聞く",()=>Run(new(Nonce("temple"),SchemeAction.QueryTemple,temple.Representative,actor)));
  var agents=SocialWorld.CarrierChoices(w,actor).Where(p=>p!=actor).ToArray();if(agents.Length>0){var choice=new OptionButton();foreach(var a in agents)choice.AddItem("同意した密偵："+MeritRewards.DisplayName(w,actor,a));box.AddChild(choice);var destinations=NationalRoutes.Nodes.Where(n=>RegionalCampaign.Site(n.Id)!=site).ToArray();var destination=new OptionButton();foreach(var n in destinations)destination.AddItem("現地の陸路を調べる："+NewGameWorld.SiteName(RegionalCampaign.Site(n.Id).Value));box.AddChild(destination);Button("send-scout","密偵を実際に派遣する（海路は使わない）",()=>Run(new(Nonce("scout"),SchemeAction.SendScout,agents[choice.Selected],actor,Destination:RegionalCampaign.Site(destinations[destination.Selected].Id))));}
  foreach(var mission in w.Schemes?.Events.Where(e=>e.Actor==actor&&e.Input.Action==SchemeAction.SendScout)??[]){string id=InformationSchemes.ReportId(mission.Input.Id);var received=Session.GetActorView(actor).Messages.SingleOrDefault(m=>m.Id.Value==id);SocialText(box,received==null?"密偵へ依頼済み。帰還・返報はまだ確認していません。":received.KnownBody==null?"密偵から返報が届きました。中身は未開封です。":(DestructiveSchemes.Operation(mission.Input.Topic)||mission.Input.Topic==SchemeTopic.Assassination)?string.Join("、",w.Evidence.Where(e=>e.Observer==actor&&e.Message?.Value==id).Select(e=>e.Claim)):"密偵から道路の観測結果が届きました。");if(received?.KnownBody==null&&received!=null)Button("open-scout","届いた密偵の返報を開く",()=>{CancelAdvancePrefetch();var r=Session.Open(actor,new(id),rev);message.Text=r.VisibleReason;Refresh();ShowInformationSchemes();});}
  SocialText(box,"自分で調べる場合は、地図の現在地に隣接する道の確認を使います。密偵も同じ現地の道路観測を使い、蔵の在庫や他人の内心は調べません。");
 }
}
