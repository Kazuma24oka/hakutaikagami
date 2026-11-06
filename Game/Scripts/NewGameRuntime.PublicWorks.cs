using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 readonly Dictionary<string,Button> worksReviewButtons=new();
 internal void ReviewWorksPress(string key)=>worksReviewButtons[key].EmitSignal(Button.SignalName.Pressed);
 static string WorksName(WorksKind k)=>CastleWorks.IsFort(k)?CastleWorks.Name(k):k switch{WorksKind.RepairFields=>"被災した田畑の復旧",WorksKind.RepairTown=>"被災した町の復旧",WorksKind.RepairCastle=>"被災した城の復旧",WorksKind.Agriculture=>"農業",WorksKind.Commerce=>"市・商業",WorksKind.Workshop=>"工房",WorksKind.Pasture=>"牧場",WorksKind.Mine=>"鉱山",WorksKind.Port=>"港の整備",WorksKind.TradeShip=>"交易船",_=>"軍船"};
 void ShowPublicWorks(){if(IsSaving||IsClockControlling)return;var(box,rev)=SocialWindow("地域の開発・造船");worksReviewButtons.Clear();var w=Snapshot();var actor=Session.PlayerControl;if(SocialWorld.Site(w,actor) is not {} site){SocialText(box,"現地へ到着してから確認してください。");return;}var land=PublicWorks.Land(w,site);SocialText(box,RegionalWeather.LocalSummary(w,actor));foreach(var disaster in RegionalWeather.LocalEvents(w,actor).TakeLast(3))SocialText(box,disaster.At/86400+"日目："+disaster.Kind);SocialText(box,NewGameWorld.SiteName(site.Value)+"\n土地の容量は試作値。人手・米・木材・銭は実在の台帳から確保します。\n通常普請：20人・7日・米100kg・木材5kg・銭50文。造船：14日・米200kg・木材10kg・銭100文。");
 void Button(string key,string text,Action act){var b=new Button{Text=text};box.AddChild(b);worksReviewButtons[key]=b;b.Pressed+=()=>{if(IsSaving||IsClockControlling)return;CancelAdvancePrefetch();act();Refresh();ShowPublicWorks();};}
 foreach(var q in PublicWorks.KnownProjects(w,actor).Where(q=>q.Input.Site==site)){SocialText(box,$"{WorksName(q.Input.Kind)}：{(q.Due<=w.ElapsedSeconds?"完成":"工事中")}／完了予定 {q.Due/86400}日目／村の協力あり");}
 if(CastleWorks.Enabled(w)){var b=new Button{Text="城の区画を選んで普請する"};box.AddChild(b);worksReviewButtons["districts"]=b;b.Pressed+=ShowCastleWorks;}
 foreach(var kind in Enum.GetValues<WorksKind>()){if(CastleWorks.IsFort(kind))continue;int cap=PublicWorks.Capacity(w,site,kind);if(cap==0)continue;int level=PublicWorks.Level(w,site,kind);Button("build-"+kind,WorksName(kind)+(kind is WorksKind.RepairFields or WorksKind.RepairTown or WorksKind.RepairCastle?"の普請を相談する":"（完成"+level+"／上限"+cap+"）の普請を相談する"),()=>{var result=Session.StartPublicWork(new(new("request:works-ui/"+rev+"/"+kind),site,kind),rev);message.Text=result.VisibleReason;});}
 foreach(var ship in PublicWorks.Ships(w).Where(s=>s.Port==site&&PublicWorks.KnownProjects(w,actor).Any(q=>q.Input.Site==site&&q.Owner==s.Owner))){SocialText(box,WorksName(ship.Kind)+"："+(ship.Maintained?"保守期間内":"保守が必要"));if(!ship.Maintained)Button("care-"+ship.Id.Value,"30日分の保守へ銭2文・米1kgを支出",()=>{var result=Session.MaintainPublicShip(new("request:ship-care-ui/"+rev),ship.Id,rev);message.Text=result.VisibleReason;});}
 }
}
