using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    readonly Dictionary<EntityId,Node3D> houseSuccessionMarkers=new();
    string SuccessionPerson(ContractWorld w,EntityId id)=>w.Definitions.Characters.Single(p=>p.Id==id).Name;
    void RefreshHouseSuccessionMarkers(ContractWorld w,FieldPoint center)
    {
        foreach(var m in houseSuccessionMarkers.Values)m.Visible=false;
        if(HouseSuccession.ViewForProjection(w) is not {} v)return;
        foreach(var c in v.Changes.Where(c=>c.Local)){
            if(!houseSuccessionMarkers.TryGetValue(c.Id,out var marker)){marker=new Node3D();stage.AddChild(marker);marker.AddChild(new Label3D{Position=new Vector3(0,8,0),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=24,PixelSize=.02f,Modulate=new Color("e7d8a1"),OutlineSize=5});houseSuccessionMarkers[c.Id]=marker;}
            marker.Visible=true;var point=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==c.Home).Position);WorldArmyProjection.Apply(marker,point,center);((Label3D)marker.GetChild(0)).Text=$"当主職：{SuccessionPerson(w,c.Incumbent)} → {SuccessionPerson(w,c.Heir)}\n{c.Status}";
        }
    }
    void ShowHouseSuccession()
    {
        if(Session.HouseSuccessionView() is not {} v)return;var w=Snapshot();long shown=Session.Revision;var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var scroll=new ScrollContainer{CustomMinimumSize=new(560,420),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);var box=new VBoxContainer{CustomMinimumSize=new(540,0)};box.AddThemeConstantOverride("separation",10);scroll.AddChild(box);
        void Text(string s)=>box.AddChild(new Label{Text=s,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string s,Action action,bool disabled=false){var b=new Button{Text=s,Disabled=disabled,CustomMinimumSize=new(0,36)};box.AddChild(b);b.Pressed+=action;}
        Text("家督・引継ぎ");Text("本拠で集合中の預かり軍は保管したまま交代を相談できます。軍務の発令者は別の指揮原本を確認するまで変わりません。");Text("生前の合意による当主職の交代です。所属・奉公・兵・私財は一括で移しません。家の未払いと預かり物は、原本ごとに現地で引き継ぎます。");var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Run(HouseSuccessionInput i){var r=Session.ManageHouseSuccession(i,Session.PlayerControl,shown);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowHouseSuccession();}
        foreach(var dispute in Guardianship.Known(w,w.PlayerControl))
        {
            Text(CastleOwnerName(w,dispute.Opening.House)+"の当主が死去："+SuccessionPerson(w,dispute.Opening.Deceased));
            var claims=Guardianship.WitnessedClaims(w,dispute.Opening,w.PlayerControl).ToArray();
            foreach(var claim in claims)
            {
                Text("現地での家督申立："+SuccessionPerson(w,claim.Claimant));
                var opinions=Guardianship.HeardOpinions(w,claim,w.PlayerControl).ToArray();
                foreach(var opinion in opinions)Text(PersonName(opinion.Speaker)+(opinion.Supports?"：申立を支持":"：申立に異議"));
                if(claim.Claimant!=w.PlayerControl || SocialWorld.Site(w,w.PlayerControl)!=dispute.Opening.Home)continue;
                foreach(var member in w.Memberships.Where(m=>m.Target==claim.House && m.From<=w.ElapsedSeconds && w.ElapsedSeconds<m.Until))
                {
                    var speaker=member.Subject;
                    if(speaker==claim.Claimant || speaker==claim.Deceased || opinions.Any(o=>o.Speaker==speaker) ||
                        SocialWorld.Site(w,speaker)!=dispute.Opening.Home)continue;
                    Button(PersonName(speaker)+"に意向を尋ねる",()=>{
                        var r=Session.ConsultHouseMember(claim.Revision,speaker,shown);
                        if(r.Code!=ResultCode.Accepted){feedback.Text=r.VisibleReason;return;}
                        popup.Hide();Refresh();ShowHouseSuccession();
                    });
                }
            }
            if(dispute.OwnClaim && !claims.Any(c=>c.Claimant==w.PlayerControl))
                Button("本拠で家督を申し立てる",()=>{
                    var r=Session.ClaimSuccession(dispute.Opening.House,dispute.Opening.Deceased,shown);
                    if(r.Code!=ResultCode.Accepted){feedback.Text=r.VisibleReason;return;}
                    popup.Hide();Refresh();ShowHouseSuccession();
                });
            if(dispute.KnownNomination is {} known)Text("あなたが確認した指名状の候補："+SuccessionPerson(w,known.Heir));
        }
        foreach(var house in w.NewGame!.Start.Factions)
        {
            var children=HeirDesignation.KnownChildren(w,new(house.Id));
            if(children.Length==0)continue;
            Text(house.Name+"の後継候補（生年順）");
            var owner=new EntityId(house.Id);
            var current=w.HeirNominations?.LastOrDefault(p=>p.House==owner && p.Head==Session.PlayerControl);
            if(current!=null)
            {
                foreach(var contact in HeirPledges.Contacts(w,current))
                {
                    if(w.Characters.Single(c=>c.Id==current.Heir).Life==LifeState.Minor &&
                        !(w.GuardianMandates??[]).Any(m=>m.Nomination==current.Revision) &&
                        SocialWorld.Site(w,new(contact.Id))==SocialWorld.Site(w,current.Head))
                        Button(contact.Name+"に元服までの帳簿保管を相談",()=>{
                            var r=Session.AgreeGuardian(current,new(contact.Id),shown);
                            if(r.Code!=ResultCode.Accepted){feedback.Text=r.VisibleReason;return;}
                            popup.Hide();Refresh();ShowHouseSuccession();
                        });
                    if(!HeirPledges.Requested(w,current,contact.Id))
                        Button(contact.Name+"に誓紙を求める",()=>{
                            var r=Session.RequestHeirPledge(current,new(contact.Id),shown);
                            if(r.Code!=ResultCode.Accepted){feedback.Text=r.VisibleReason;return;}
                            popup.Hide();Refresh();ShowHouseSuccession();
                        });
                    else Text(contact.Name+"へ指名状を送付済み");
                }
            }
            foreach(var child in children)
            {
                var name=SuccessionPerson(w,child.Person);
                Text(name+" ／ "+(child.Adopted?"養子":"実子")+(current?.Heir==child.Person?" ／ 指名済み":""));
                if(current?.Heir==child.Person)continue;
                Button(name+"を後継に指名",()=>{
                    var result=Session.DesignateHeir(owner,child.Person,shown);
                    if(result.Code!=ResultCode.Accepted){feedback.Text=result.VisibleReason;return;}
                    popup.Hide();Refresh();ShowHouseSuccession();
                });
            }
        }
        foreach(var reply in w.Messaging.Messages.Where(m=>m.Kind==HeirPledges.ReplyKind && m.RecipientId==w.PlayerControl.Value))
        {
            if(!w.Messaging.Receipts.Any(r=>r.MessageId==reply.Id && r.ReceiverId==w.PlayerControl.Value))continue;
            if(HakutaiKagami.Core.Messaging.MessageFlow.Knows(w.Messaging,reply.Id,w.PlayerControl.Value)){
                var original=w.HeirNominations!.Single(p=>HeirPledges.Version(p)==reply.CorrelationId);
                bool prior=w.HeirNominations!.Last(p=>p.House==original.House)!=original;
                Text((prior?"旧指名への返答：":"指名への返答：")+PersonName(new(reply.SenderId))+" ／ "+reply.Body);
            }else Button("届いた返書を開く",()=>{
                var r=Session.Open(w.PlayerControl,new(reply.Id),shown);
                if(r.Code!=ResultCode.Accepted){feedback.Text=r.VisibleReason;return;}
                popup.Hide();Refresh();ShowHouseSuccession();
            });
        }
        if(v.Candidates.Length>0){var list=new OptionButton();foreach(var c in v.Candidates)list.AddItem(CastleOwnerName(w,c.Owner)+" ／ "+SuccessionPerson(w,c.Person));box.AddChild(list);Button("当事者に生前の当主交代を相談",()=>{var c=v.Candidates[list.Selected];Run(new(new("request:house-nominate-ui/"+shown),HouseSuccessionAction.Nominate,c.Owner,c.Person){MilitaryPolicy=NewGameMilitary.CanRetainActiveForSuccession(w,Session.PlayerControl,c.Owner,c.Home,Session.Seconds,shown)?NewGameMilitary.ActiveSuccessionContract:null});});}
        foreach(var c in v.Changes.Reverse().Take(3)){Text($"{CastleOwnerName(w,c.Owner)}：{SuccessionPerson(w,c.Incumbent)} → {SuccessionPerson(w,c.Heir)}\n{c.Status}\n合意 {c.ProposedAt}秒"+(c.EffectiveAt is {} at?$" ／ 引渡し {at}秒":""));Button("同じ条件を確認し、次の時刻の交代を予定",()=>Run(new(new("request:house-confirm-ui/"+shown),HouseSuccessionAction.Confirm,c.Owner,Original:c.Id)),!c.CanConfirm);Button("交代前に取りやめ",()=>Run(new(new("request:house-cancel-ui/"+shown),HouseSuccessionAction.Cancel,c.Owner,Original:c.Id)),!c.CanCancel);}
        foreach(var o in v.Obligations.Take(5)){Text((o.Kind=="salary"?"家の給付原本":"家の運搬原本")+" ／ "+CastleOwnerName(w,o.Owner)+"\n元の当事者："+SuccessionPerson(w,o.OriginalPrincipal)+" ／ 相手："+SuccessionPerson(w,o.Counterparty)+"\n"+(o.Amount is {} amount?(o.Kind=="salary"?$"未払い {amount}kg":$"現地で確認した荷 {amount}"):"現況は未確認")+" ／ "+(o.Acknowledged?"現当主が原本を確認済み":"原本ごとの確認待ち"));Button("現地でこの原本の引継ぎを確認",()=>Run(new(new("request:house-ack-ui/"+shown),HouseSuccessionAction.Acknowledge,o.Owner,Original:o.Id)),!o.CanAcknowledge);}
        if(Session.HouseholdServiceView()!=null)Button("個人の奉公を整理する（所属は保持）",()=>{popup.Hide();ShowHouseholdService();});Button("行政委任の原本を確認",()=>{popup.Hide();ShowStewardship();});
        foreach(var t in Session.TruceSuccessionView()){Text("成立済み不戦原本："+SuccessionPerson(w,t.Previous)+" → "+SuccessionPerson(w,t.Successor)+(t.Until is {} until?$" ／ 元期限 {until}秒":" ／ 本文は現地確認後"));Button("旧代表者が原本を示し、新当主が確認",()=>{var r=Session.ReadInheritedTruce(new(new("request:truce-inheritance-ui/"+shown),t.Owner,t.Acceptance),Session.PlayerControl,shown);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowHouseSuccession();},!t.CanRead);}
        foreach(var target in NewGameMilitary.StoredSuccessionCandidates(w))Button("返納済み駐屯軍を現当主と再確認（"+NewGameWorld.SiteName(target.Site.Value)+"）",()=>{var result=Session.DecideNewMilitary(NewGameMilitary.StoredSuccessionRequest(w,new("request:stored-military-ui/"+shown+"/"+target.Ledger.Value),target.Ledger),Session.PlayerControl,shown);if(result.Code!=ResultCode.Accepted){feedback.Text=result.VisibleReason;return;}popup.Hide();Refresh();});
        foreach(var e in w.NewMilitary?.ActiveSuccession?.Entries.TakeLast(3)??Enumerable.Empty<ActiveMilitaryAgreement>())Text("活動軍の指揮原本："+SuccessionPerson(w,e.Previous)+" → "+SuccessionPerson(w,e.Principal)+" ／ 現地確認 "+e.At+"秒。兵・兵糧は同じ家の同じ台帳に保持。");
        if(Session.CanAgreeActiveMilitary())Button("現当主と、預かっている同じ軍の指揮原本を確認",()=>{var root=NewGameMilitary.Cases(w).Last().Id;var result=Session.AgreeActiveMilitary(new(new("request:active-military-ui/"+shown),root),Session.PlayerControl,shown);if(result.Code!=ResultCode.Accepted){feedback.Text=result.VisibleReason;return;}popup.Hide();Refresh();ShowHouseSuccession();});
        AppendRemoteMilitary(box,popup,feedback,shown);
        AppendTruceConfirmations(box,popup,feedback,shown);
        Text("交代後の支払いは「家の支給・待遇」、荷と余金の受渡しは「家の仕入れ・運搬」で行います。予定後も世界は通常の進行操作で進みます。");Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(610,540));
    }
}
