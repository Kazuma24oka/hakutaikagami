using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 CastleSection selectedFort=CastleSection.Gate;
 void ShowCastleWorks(){if(IsSaving||IsClockControlling)return;var (box,rev)=SocialWindow("城の普請");worksReviewButtons.Clear();var w=Snapshot();if(!CastleWorks.Enabled(w)||SocialWorld.Site(w,w.PlayerControl) is not {} site||!MaterialMarkets.Sites(w).Contains(site)){SocialText(box,"城の現地で普請を相談してください。");return;}
 void Add(string key,string text,Action action,Container parent){var b=new Button{Text=text};parent.AddChild(b);worksReviewButtons[key]=b;b.Pressed+=action;}
 var tabs=new HBoxContainer();box.AddChild(tabs);foreach(var section in Enum.GetValues<CastleSection>())Add("district-"+section,CastleWorks.Name(section),()=>{selectedFort=section;ShowCastleWorks();},tabs);
 var defense=CastleWorks.Defense(w,site,selectedFort,w.ElapsedSeconds,rev+1);SocialText(box,$"{NewGameWorld.SiteName(site.Value)}・{CastleWorks.Name(selectedFort)}　防備 {defense.Strength}／{defense.Maximum}　損傷 {defense.Damage}");
 var current=PublicWorks.KnownProjects(w,w.PlayerControl).LastOrDefault(q=>q.Input.Site==site&&q.Due>w.ElapsedSeconds);if(current!=null){SocialText(box,$"{(current.Input.Section is {} s?CastleWorks.Name(s)+"の"+CastleWorks.Name(current.Input.Kind):WorksName(current.Input.Kind))}を普請中。完成は開始から{current.Due/86400}日目。人手は工事に出ています。");return;}
 SocialText(box,"村と相談し、働き手20人を七日間確保します。米100kg・木材5kg・銭50文を支出します。石垣には現地の石工と切石5個も必要です。");
 foreach(var kind in Enum.GetValues<WorksKind>().Where(k=>CastleWorks.Fits(selectedFort,k))){if(kind==WorksKind.RepairSection&&defense.Damage==0)continue;var target=selectedFort;Add("fort-"+kind,CastleWorks.Name(kind)+"の普請を相談する",()=>{CancelAdvancePrefetch();var result=Session.StartPublicWork(new(new("request:castle-works-ui/"+rev+"/"+target+"/"+kind),site,kind){Section=target},rev);message.Text=result.VisibleReason;Refresh();ShowCastleWorks();},box);}
 }
}
