using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    static string TreatyClauses(TreatyClause clauses)=>string.Join("・",new[]{(TreatyClause.NonAggression,"不侵略"),(TreatyClause.Passage,"通行"),(TreatyClause.Supply,"補給協議"),(TreatyClause.Defense,"防衛援軍協議")}.Where(p=>(clauses&p.Item1)!=0).Select(p=>p.Item2));
    static string CharterText(CouncilCharter? charter)=>charter==null?"":charter.Rule switch{
        CouncilRule.Majority=>" ／ 契状の議決は過半数",CouncilRule.TwoThirds=>" ／ 契状の議決は三分の二以上",_=>" ／ 契状の議決は全会一致"};
    static string SubmissionText(ContractWorld world,TreatyTerms terms)
    {
        if(terms.Submission is not {} d)return "";
        string House(EntityId id)=>world.Definitions.Organizations.Single(o=>o.Id==id).Name;
        return $" ／ 主家 {House(terms.A)}・従属する家 {House(terms.B)}"+
            (d.GuaranteeLands?" ／ 本領安堵":"")+$" ／ 軍役 {d.MusterMen}人・献上米 {d.TributeRice}kg"+
            (d.RestrictDiplomacy?" ／ 外交制限あり":"")+(d.Hostage is {} person?$" ／ 人質候補 {world.NewGame!.Start.People.Single(p=>p.Id==person.Value).Name}":"")+(d.Precedent!=null?" ／ 先の従属合意を添付":"");
    }
    void ShowTreatyNegotiation()
    {
        var w=Snapshot();if(OwnPoliticalHouse(w) is not {} own||!TreatyNegotiations.Enabled(w))return;long shown=Session.Revision;var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var scroll=new ScrollContainer{CustomMinimumSize=new(560,440),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);var box=new VBoxContainer{CustomMinimumSize=new(530,0)};scroll.AddChild(box);
        void Text(string text)=>box.AddChild(new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string text,Action action,bool disabled=false){var b=new Button{Text=text,Disabled=disabled};box.AddChild(b);b.Pressed+=action;}
        void Run(TreatyOperationInput i){Submit(Session.OperateTreaty(i,Session.PlayerControl,shown));popup.Hide();Refresh();}
        Text("正式条約・独立条項");Text("同じ版の承諾が届いて成立します。相手は本人が知る代表者・本拠へ発送し、未通知の交代先は追尾しません。補給は受領返書の開封後に実物受取を手配し、本人が現地で受取り・納入します。援軍は受領返書の開封後に実出陣を手配し、出発・到着・帰還を返書で確認します。今回の援軍は常備100人の部隊です。通行軍対応は後続です。");
        var targets=w.Definitions.Organizations.Where(o=>o.Id!=own && RepresentativeKnowledge.Known(w,Session.PlayerControl,o.Id)!=null).OrderBy(o=>o.Name,StringComparer.Ordinal).ToArray();var target=new OptionButton();foreach(var t in targets)target.AddItem(t.Name);box.AddChild(target);
        var options=new[]{(TreatyClause.NonAggression,"互いに新たな戦を仕掛けない"),(TreatyClause.Passage,"通行の約束"),(TreatyClause.Supply,"補給を協議する条項"),(TreatyClause.Defense,"防衛援軍を協議する条項")}.Select(t=>(t.Item1,Box:new CheckBox{Text=t.Item2,ButtonPressed=t.Item1==TreatyClause.NonAggression})).ToArray();foreach(var item in options)box.AddChild(item.Box);
        var days=new SpinBox{MinValue=1,MaxValue=90,Value=30,Step=1,Suffix="日間"};box.AddChild(days);var notice=new SpinBox{MinValue=1,MaxValue=7,Value=1,Step=1,Suffix="日前に解除予告"};box.AddChild(notice);
        Button("この版の条件を発送",()=>{if(targets.Length==0)return;var clauses=options.Where(x=>x.Box.ButtonPressed).Aggregate(TreatyClause.None,(a,x)=>a|x.Item1);Submit(Session.NegotiateTreaty(new(new("request:treaty-ui/"+shown),TreatyAction.Propose,own,targets[target.Selected].Id,Days:(int)days.Value,Clauses:clauses,NoticeDays:(int)notice.Value){AttachHouseScale=true},Session.PlayerControl,shown));popup.Hide();Refresh();});
        var coalitionToggle=new CheckButton{Text="包囲網の書状"};box.AddChild(coalitionToggle);
        var coalition=new VBoxContainer{Visible=false};box.AddChild(coalition);coalitionToggle.Toggled+=open=>coalition.Visible=open;
        void CoalitionButton(string label,Action onPress){
            var button=new Button{Text=label};coalition.AddChild(button);button.Pressed+=onPress;
        }
        string HouseName(EntityId house)=>w.Definitions.Organizations.Single(o=>o.Id==house).Name;
        void ReturnToDiplomacy(){popup.Hide();Refresh();ShowTreatyNegotiation();}
        void CoalitionCommand(CourtInput input){Submit(Session.ManageCourt(input,shown));ReturnToDiplomacy();}
        foreach(var paper in CoalitionPacts.DeliveredPapers(w,Session.PlayerControl)){
            if(paper.Body!=null)coalition.AddChild(new Label{Text=paper.Body,AutowrapMode=TextServer.AutowrapMode.WordSmart});
            else CoalitionButton("包囲網の返書・成立通知を開く",()=>{Submit(Session.Open(Session.PlayerControl,new(paper.Id),shown));ReturnToDiplomacy();});
        }
        foreach(var pact in CoalitionPacts.Known(w,Session.PlayerControl))
            coalition.AddChild(new Label{Text=HouseName(pact.Target)+"を対象とする盟約　確認済みの署名："+string.Join("、",pact.Houses.Select(HouseName)),AutowrapMode=TextServer.AutowrapMode.WordSmart});
        foreach(var confirmation in CoalitionPacts.Confirmations(w,Session.PlayerControl))
            CoalitionButton("開封した加盟返書を取りまとめ、署名各家へ通知する",()=>CoalitionCommand(confirmation));
        foreach(var item in CoalitionPacts.Invitations(w,Session.PlayerControl)){
            if(item.Answer is not {} answer){
                CoalitionButton("包囲網への招請状を開く",()=>{Submit(Session.Open(Session.PlayerControl,new(item.Letter),shown));ReturnToDiplomacy();});
                continue;
            }
            var invitation=w.Court!.Events.Single(e=>e.Input.Id==answer.Reference);
            if(answer.Action==CourtAction.JoinCoalition)
                coalition.AddChild(new Label{Text=HouseName(invitation.Input.Owner)+"より　対象 "+HouseName(invitation.Input.Coalition!.Target)+" ／ "+CoalitionPacts.TermsLabel(invitation.Input.Coalition)});
            CoalitionButton(answer.Action switch{CourtAction.JoinCoalition=>"加盟を伝える",CourtAction.RefuseCoalition=>"参加を断る",_=>"返答を保留する"},()=>CoalitionCommand(answer));
        }
        var ownFaction=HouseSuccession.Factions(w).SingleOrDefault(f=>f.Id==own.Value);
        var known=RepresentativeKnowledge.Observations(w,Session.PlayerControl).Select(o=>o.Owner).Distinct().Where(h=>h!=own).ToArray();
        if(ownFaction?.Head==Session.PlayerControl.Value && SocialWorld.Site(w,Session.PlayerControl)?.Value==ownFaction.Home && known.Length>=3){
            coalition.AddChild(new Label{Text="包囲網の対象"});
            var enemy=new OptionButton();coalition.AddChild(enemy);foreach(var h in known)enemy.AddItem(HouseName(h));
            var purpose=new OptionButton();coalition.AddChild(purpose);
            purpose.AddItem("同時期の出兵");purpose.AddItem("単独で和睦しない");purpose.AddItem("同時期の出兵と単独和睦の禁止");
            var term=new SpinBox{MinValue=1,MaxValue=365,Value=30,Suffix="日後を期日とする"};coalition.AddChild(term);
            coalition.AddChild(new Label{Text="招請先（二家以上）"});
            var invitees=known.Select(h=>(House:h,Box:new CheckBox{Text=HouseName(h)})).ToArray();
            foreach(var candidate in invitees)coalition.AddChild(candidate.Box);
            void UpdateCandidates(){foreach(var c in invitees){c.Box.Visible=c.House!=known[enemy.Selected];if(!c.Box.Visible)c.Box.ButtonPressed=false;}}
            enemy.ItemSelected+=_=>UpdateCandidates();UpdateCandidates();
            CoalitionButton("選んだ各家に招請状を発送",()=>CoalitionCommand(new CourtInput(new("request:coalition-ui/"+shown),CourtAction.ProposeCoalition,
                Session.PlayerControl,own,new(ownFaction.Home)){Coalition=new(known[enemy.Selected],invitees.Where(x=>x.Box.ButtonPressed).Select(x=>x.House).ToArray(),
                (CoalitionPurpose)purpose.Selected,checked(w.ElapsedSeconds+(long)term.Value*TruceNegotiations.Day))}));
        }
        var leagueToggle=new CheckButton{Text="国人衆の契状を送る"};box.AddChild(leagueToggle);
        var league=new VBoxContainer{Visible=false};box.AddChild(league);leagueToggle.Toggled+=open=>league.Visible=open;
        var charterSelect=new OptionButton();league.AddChild(charterSelect);charterSelect.AddItem("新しい契状を起草");
        var existing=(w.Treaties?.Papers??[]).Where(p=>p.Author==Session.PlayerControl && p.Input.Owner==own &&
            p.Input.Charter?.Origin==p.Input.Id && Session.Seconds<p.Terms.Until).ToArray();
        foreach(var first in existing)charterSelect.AddItem($"既存の契状（{first.At}秒起草）");
        var rules=new OptionButton();rules.AddItem("過半数");rules.AddItem("三分の二以上");rules.AddItem("全会一致");league.AddChild(rules);
        charterSelect.ItemSelected+=index=>{
            rules.Disabled=index>0;
            if(index>0)rules.Select((int)existing[(int)index-1].Input.Charter!.Rule);
        };
        var invite=new Button{Text="選んだ家に署名を求める",Disabled=targets.Length==0};league.AddChild(invite);
        invite.Pressed+=()=>{
            var id=new EntityId("request:charter-ui/"+shown);
            var first=charterSelect.Selected>0?existing[charterSelect.Selected-1]:null;
            var charter=first?.Input.Charter??new CouncilCharter(id,(CouncilRule)rules.Selected);
            int duration=first==null?Math.Min(30,(int)days.Value):(int)((first.Terms.Until-Session.Seconds)/TreatyNegotiations.Day);
            Submit(Session.NegotiateTreaty(new(id,TreatyAction.Propose,own,targets[target.Selected].Id,Days:duration,NoticeDays:(int)notice.Value){Charter=charter},Session.PlayerControl,shown));
            popup.Hide();Refresh();
        };
        var expand=new CheckButton{Text="従属を勧告する"};box.AddChild(expand);
        var demand=new VBoxContainer{Visible=false};box.AddChild(demand);
        expand.Toggled+=open=>demand.Visible=open;
        var lands=new CheckBox{Text="本領を安堵する",ButtonPressed=true};demand.AddChild(lands);
        var men=new SpinBox{MinValue=0,MaxValue=100000,Step=1,Value=100,Prefix="軍役",Suffix="人"};demand.AddChild(men);
        var rice=new SpinBox{MinValue=0,MaxValue=1000000,Step=1,Prefix="献上米",Suffix="kg"};demand.AddChild(rice);
        var restrict=new CheckBox{Text="敵対する家との外交を制限する"};demand.AddChild(restrict);
        var hostages=new OptionButton();demand.AddChild(hostages);
        var precedents=new OptionButton();demand.AddChild(precedents);
        void RefreshCandidates(){
            hostages.Clear();hostages.AddItem("人質を求めない");
            precedents.Clear();precedents.AddItem("先例を添えない");
            if(target.Selected<0)return;
            var other=targets[target.Selected].Id;
            foreach(var person in w.NewGame!.Start.People.Where(p=>p.Faction==other.Value)){
                hostages.AddItem("人質候補　"+person.Name);
                hostages.SetItemMetadata(hostages.ItemCount-1,person.Id);
            }
            foreach(var f in TreatyNegotiations.Known(w,Session.PlayerControl).Where(f=>f.Terms.Submission!=null &&
                f.Terms.A==own && f.Terms.B!=other && Session.Seconds<f.Terms.Until &&
                !TreatyNegotiations.KnowsEnding(w,Session.PlayerControl,f.Acceptance,Session.Seconds,shown))){
                precedents.AddItem("成立済みの先例　"+w.Definitions.Organizations.Single(o=>o.Id==f.Terms.B).Name);
                precedents.SetItemMetadata(precedents.ItemCount-1,f.Acceptance.Value);
            }
        }
        RefreshCandidates();target.ItemSelected+=_=>RefreshCandidates();
        var send=new Button{Text="この条件で従属を勧告する",Disabled=targets.Length==0};demand.AddChild(send);
        send.Pressed+=()=>{
            var conditions=new SubmissionConditions(lands.ButtonPressed,(int)men.Value,(long)rice.Value,restrict.ButtonPressed,hostages.Selected>0?new EntityId(hostages.GetItemMetadata(hostages.Selected).AsString()):null){Precedent=precedents.Selected>0?new EntityId(precedents.GetItemMetadata(precedents.Selected).AsString()):null};
            var clauses=options.Where(x=>x.Box.ButtonPressed).Aggregate(TreatyClause.None,(a,x)=>a|x.Item1);
            Submit(Session.NegotiateTreaty(new(new("request:submission-ui/"+shown),TreatyAction.Propose,own,targets[target.Selected].Id,Days:(int)days.Value,Clauses:clauses,NoticeDays:(int)notice.Value){Submission=conditions,AttachHouseScale=true},Session.PlayerControl,shown));
            popup.Hide();Refresh();
        };
        foreach(var f in TreatyNegotiations.Known(w,Session.PlayerControl)){var other=f.Terms.A==own?f.Terms.B:f.Terms.A;Text("確認した条約 第"+f.Terms.Version+"版 ／ "+TreatyClauses(f.Terms.Clauses)+SubmissionText(w,f.Terms)+CharterText(f.Terms.Charter)+" ／ 期限 "+f.Terms.Until+"秒");
            if(f.Terms.Charter!=null && own==f.Terms.B && KokujinCouncil.Departure(w,f,Session.PlayerControl,Session.Seconds,shown) is {} leave)
                Button("個別の従属を明記して契状からの離脱を通知",()=>Run(leave),!TreatyNegotiations.CanOperate(w,leave,Session.PlayerControl));
            var end=new TreatyOperationInput(new("request:treaty-end-ui/"+shown+"/"+f.Acceptance.Value),TreatyOperation.Terminate,f.Acceptance,own,other,EffectiveAt:Session.Seconds+f.Terms.NoticeSeconds);Button("通知期間を守って解除を予告",()=>Run(end),!TreatyNegotiations.CanOperate(w,end,Session.PlayerControl));
            foreach(var kind in new[]{TreatyOperation.RequestDefense,TreatyOperation.RequestSupply}){var i=new TreatyOperationInput(new("request:treaty-request-ui/"+shown+"/"+kind+"/"+f.Acceptance.Value),kind,f.Acceptance,own,other,ReplyBy:Math.Min(f.Terms.Until,Session.Seconds+3*86400),Amount:kind==TreatyOperation.RequestDefense?100:20,Evidence:kind==TreatyOperation.RequestDefense?"防衛の援軍について協議したい":"補給米20kgについて協議したい");Button(kind==TreatyOperation.RequestDefense?"防衛援軍100人の相談を発送（受領確認まで）":"補給米20kgの相談を発送（受領確認まで）",()=>Run(i),!TreatyNegotiations.CanOperate(w,i,Session.PlayerControl));}}
        foreach(var request in w.Treaties?.Operations.Where(p=>p.Kind==TreatyOperation.RequestSupply&&p.Author==Session.PlayerControl)??Enumerable.Empty<TreatyOperationPaper>()){var arrange=new TreatySupplyInput(new("request:supply-arrange-ui/"+shown+"/"+request.Input.Id.Value),TreatySupplyAction.Arrange,request.Input.Id);if(TreatySupply.Can(w,arrange,Session.PlayerControl))Button("開封した受領返書に続けて、実物の受取を手配",()=>{Submit(Session.ManageTreatySupply(arrange,Session.PlayerControl,shown));popup.Hide();Refresh();});}if(TreatySupply.View(w).Length>0)Button("条約の預かり米・受取先を確認",()=>{popup.Hide();ShowTreatySupply();});
        foreach(var request in w.Treaties?.Operations.Where(p=>p.Kind==TreatyOperation.RequestDefense&&p.Author==Session.PlayerControl)??Enumerable.Empty<TreatyOperationPaper>()){var arrange=new TreatyDefenseInput(new("request:defense-arrange-ui/"+shown+"/"+request.Input.Id.Value),TreatyDefenseAction.Arrange,request.Input.Id,DutySeconds:3600);if(TreatyDefense.Can(w,arrange,Session.PlayerControl))Button("受領返書に続けて実出陣を手配（守備1時間）",()=>{Submit(Session.ManageTreatyDefense(arrange,Session.PlayerControl,shown));popup.Hide();Refresh();});}if(TreatyDefense.View(w).Length>0)Button("確認した防衛援軍の報告を見る",()=>{popup.Hide();ShowTreatyDefense();});
        foreach(var card in TreatyNegotiations.OperationView(w).TakeLast(4)){Text(card.Status);if(card.CanResend){var p=w.Treaties!.Operations.Single(p=>p.Input.Id==card.Id);var i=new TreatyOperationInput(new("request:treaty-retry-ui/"+shown+"/"+card.Id.Value),TreatyOperation.Resend,p.Input.Acceptance,p.Input.Owner,p.Input.Target,p.Input.Id);Button("本人が不達を知った同じ原本を再送",()=>Run(i));}}
        Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(600,500));
    }
    void CouncilChoices(ContractWorld w,TreatyPaper demand,EntityId own,long revision)
    {
        if(demand.Input.Action!=TreatyAction.Propose || demand.Terms.Submission is not {} terms)return;
        var prior=w.Treaties!.Papers.Where(p=>p.Input.Ballot?.Demand==demand.Input.Id && p.Author==Session.PlayerControl).ToArray();
        var charters=TreatyNegotiations.Known(w,Session.PlayerControl).Select(f=>f.Terms.Charter).OfType<CouncilCharter>().Distinct();
        foreach(var charter in charters){
            if(prior.Length>0 && prior[0].Input.Ballot!.Charter!=charter)continue;
            foreach(var house in KokujinCouncil.KnownMembers(w,Session.PlayerControl,charter,Session.Seconds,revision)
                .Where(h=>h!=own && h!=demand.Input.Owner && !prior.Any(p=>p.Input.Target==h))){
                var name=w.Definitions.Organizations.Single(o=>o.Id==house).Name;
                foreach(bool support in prior.Length==0?new[]{true,false}:new[]{prior[0].Input.Ballot!.ChairSupports}){
                    var input=new TreatyInput(new("request:council-consult/"+revision),TreatyAction.Propose,own,house,Days:7,NoticeDays:1)
                        {Ballot=new(charter,demand.Input.Id,demand.Input.Owner,terms,support)};
                    Choice("council-"+charter.Origin.Value+house.Value+support,$"{name}に賛否を問う（自家は{(support?"賛成":"反対")}）",()=>{
                        Submit(Session.NegotiateTreaty(input,Session.PlayerControl,revision));CloseBottomTalk();
                    });
                }
            }
        }
    }
    void ShowTreatyLetter(EntityId id)
    {
        var known=Branch(Session.PlayerControl).Knowledge.Messages.Single(m=>m.Id==id);if(known.KnownBody==null)return;var w=Snapshot();long shown=Session.Revision;
        if(w.Treaties!.Operations.SingleOrDefault(p=>p.Message==id.Value) is {} op){string line=op.Kind switch{TreatyOperation.Terminate=>"条約に定めた予告期間を置き、この原本の解除を通知する。発送だけで即時解除にはならない。",TreatyOperation.TerminationReceipt=>"解除通知を受領した。法的な受領資格と通知時点を記録して返す。",TreatyOperation.RequestDefense=>"防衛援軍について協議したい。受領返書と実際の出陣・到着は別に確認する。",TreatyOperation.RequestSupply=>"補給について協議したい。受領返書だけで米が届いたことにはしない。",TreatyOperation.Decline=>"この要請は辞退する。",_=>"要請を受領した。援軍・補給の実行が終わったという返答ではない。"};var origin=op.Kind is TreatyOperation.TerminationReceipt or TreatyOperation.Acknowledge or TreatyOperation.Decline?w.Treaties.Operations.Single(p=>p.Input.Id==op.Input.Original):op;var intent=origin.Intent;string timing=origin.Kind==TreatyOperation.Terminate?" ／ 解除予定 "+intent.EffectiveAt+"秒":" ／ 要請量 "+intent.Amount+(origin.Kind==TreatyOperation.RequestSupply?"kg":"人")+" ／ 返答期限 "+intent.ReplyBy+"秒";PresentWork("条約の通知・要請",op.Kind==TreatyOperation.Terminate&&op.Terms.Charter!=null&&op.Intent.Evidence!=null?"別の家への従属が成立したため、契状からの離脱を予告する。":line,TreatyClauses(op.Terms.Clauses)+timing+(op.ObservedAt is {} observed?" ／ 観測 "+observed+"秒":"")+" ／ 元の条約期限 "+op.Terms.Until+"秒",op.Author,op.Author,"本人開封した差出人","確認した書状");foreach(var copy in TreatyNegotiations.DepartureCopies(w,op,Session.PlayerControl,Session.Seconds,shown)){
            var name=w.Definitions.Organizations.Single(o=>o.Id==copy.Target).Name;
            Choice("forward-"+copy.Target.Value,name+"に離脱を知らせる",()=>{Submit(Session.NegotiateTreaty(copy,Session.PlayerControl,shown));CloseBottomTalk();});
        }
        var kind=op.Kind==TreatyOperation.Terminate?TreatyOperation.TerminationReceipt:TreatyOperation.Acknowledge;foreach(var action in new[]{kind,TreatyOperation.Decline}){var i=new TreatyOperationInput(new("request:treaty-letter-response/"+shown+"/"+action),action,op.Input.Acceptance,op.Input.Target,op.Input.Owner,op.Input.Id);if(TreatyNegotiations.CanOperate(w,i,Session.PlayerControl))Choice("treaty-"+action,action==TreatyOperation.Decline?"要請を辞退する":"受領したことを返書する",()=>{Submit(Session.OperateTreaty(i,Session.PlayerControl,shown));CloseBottomTalk();});}}
        else{var p=w.Treaties.Papers.Single(p=>p.Message==id.Value);var card=Session.TreatyView().SingleOrDefault(c=>c.Paper==p.Input.Id);string line=p.Input.Action switch{TreatyAction.Counter=>"同じ独立条項を保ち、30日以内の条件を対案として示す。",TreatyAction.Accept=>"この版の条件に承諾する。",TreatyAction.Confirm=>"承諾書が届き、この版の成立を確認した。",TreatyAction.Decline=>"この条件には承諾できない。",_=>"この独立条項と期間で条約を結びたい。"};PresentWork(p.Terms.DepartureCopy!=null?"加盟家からの離脱通知":p.Terms.Ballot!=null?"国人衆の投票書状":p.Terms.Charter!=null?"国人衆の契状":p.Terms.Submission==null?"正式条約の書状":"従属勧告の書状",p.Terms.DepartureCopy is {} copy?$"{w.Definitions.Organizations.Single(o=>o.Id==copy.DepartingHouse).Name}が別の家に従属し、契状からの離脱を届け出た。":p.Terms.Ballot is {} ballot&&p.Input.Action==TreatyAction.Propose?$"勧告を受けた。軍役{ballot.Conditions.MusterMen}人・献上米{ballot.Conditions.TributeRice}kgの条件について、貴家の賛否を伺いたい。":p.Terms.Charter!=null&&p.Input.Action==TreatyAction.Propose?"この議決規則で寄り合いたい。契状への署名を願う。":p.Terms.Submission!=null&&p.Input.Action==TreatyAction.Propose?"記した条件により、当家への従属を求める。":line,"第"+p.Terms.Version+"版 ／ "+TreatyClauses(p.Terms.Clauses)+SubmissionText(w,p.Terms)+CharterText(p.Terms.Charter)+" ／ 解除予告 "+p.Terms.NoticeSeconds/86400+"日前"+(p.Terms.DeclaredScale is {} rank?" ／ 差出時の家格 "+HouseScaleNotices.Name(rank):"")+" ／ 元期限 "+p.Terms.Until+"秒",p.Author,p.Author,"条約の差出人","開封した本文");if(OwnPoliticalHouse(w) is {} own){void Answer(TreatyAction action){Submit(Session.NegotiateTreaty(new(new("request:treaty-answer-ui/"+shown+"/"+action),action,own,p.Input.Owner,p.Input.Id),Session.PlayerControl,shown));CloseBottomTalk();}if(card?.CanAccept==true){CouncilChoices(w,p,own,shown);
            var count=KokujinCouncil.Tally(w,Session.PlayerControl,p.Input.Id,Session.Seconds,shown);
            if(count==null||count.Verdict==TreatyAction.Accept)Choice("treaty-accept","この版に承諾して返書する",()=>Answer(TreatyAction.Accept));if(count==null||count.Verdict==TreatyAction.Decline)Choice("treaty-decline","この版は辞退する",()=>Answer(TreatyAction.Decline));}if(p.Terms.Ballot==null&&p.Input.Action==TreatyAction.Accept&&!w.Treaties.Papers.Any(q=>q.Input.Source==p.Input.Id&&q.Author==Session.PlayerControl))Choice("treaty-confirm","承諾の到達と成立を確認して返書する",()=>Answer(TreatyAction.Confirm));}}
        Choice("treaty-map","地図へ戻る",CloseBottomTalk);LayoutBottomTalk();
    }
}
