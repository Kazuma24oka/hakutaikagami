using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 readonly Dictionary<string,Button> surveyReviewButtons=new();
 internal void ReviewSurveyPress(string key)=>surveyReviewButtons[key].EmitSignal(Button.SignalName.Pressed);
 void ShowLandSurvey(){if(IsSaving||IsClockControlling)return;var(box,rev)=SocialWindow("指出・検地と年貢");surveyReviewButtons.Clear();var w=Snapshot();var actor=Session.PlayerControl;if(SocialWorld.Site(w,actor) is not {} site){SocialText(box,"現地で相談してください。");return;}var castle=w.NewGame!.Start.Castles.SingleOrDefault(c=>c.Id==site.Value);if(castle?.Faction is not {} faction){SocialText(box,"この地点に管轄する家がありません。");return;}var owner=new EntityId(faction);
 void Button(string key,string text,SurveyAction action,EntityId? reference=null,LandTaxRate rate=LandTaxRate.Ordinary){var b=new Button{Text=text};box.AddChild(b);surveyReviewButtons[key]=b;b.Pressed+=()=>{if(IsSaving||IsClockControlling)return;CancelAdvancePrefetch();var result=Session.ManageLandSurvey(new(new("request:survey-ui/"+rev+"/"+key.Replace(':','/')),action,owner,site,reference,rate),rev);message.Text=result.VisibleReason;Refresh();ShowLandSurvey();};}
 SocialText(box,"村の申告と実測を分けて確認します。帳面は現地で読むか、届いた報告を開いて確認します。");Button("statement","村から指出を集める：5人・2日・米20kg・10文",SurveyAction.Statement);Button("measure","検地を行う：10人・7日・米100kg・30文",SurveyAction.Measurement);
 var known=LandSurvey.KnownBooks(w,actor);foreach(var job in LandSurvey.Planned(w,actor).Where(e=>e.Input.Site==site)){SocialText(box,(job.Input.Action==SurveyAction.Measurement?"検地":"指出")+"／完了予定："+job.Due/86400+"日目");if(job.Due<=w.ElapsedSeconds&&!known.Any(b=>b.Job==job.Input.Id))Button("read-"+job.Input.Id.Value,"現地の完成帳面を読む",SurveyAction.ReadBook,job.Input.Id);}
 foreach(var paper in LandSurvey.Inbox(w,actor)){var b=new Button{Text="届いた調査報告を開く"};box.AddChild(b);b.Pressed+=()=>{var result=Session.Open(actor,new(paper.Id),rev);message.Text=result.VisibleReason;Refresh();ShowLandSurvey();};}
 var local=known.Where(b=>w.Surveys!.Entries.Any(e=>e.Input.Id==b.Job&&e.Input.Site==site)).ToArray();foreach(var b in local)SocialText(box,(b.Measured?"実測":"申告見積り")+"：収穫基礎 "+b.Basis+"／確認時点 "+b.At/86400+"日目。"+b.Description);
 SocialText(box,"布告は7日後から将来の課税へ適用します。過去の課税額と蔵の米は変わりません。税率は試作値です。");foreach(var rate in Enum.GetValues<LandTaxRate>())Button("tax-"+rate,(rate==LandTaxRate.Gentle?"軽減":rate==LandTaxRate.Heavy?"重課":"平常")+"（"+(int)rate+"%）を布告する",SurveyAction.PostRate,local.LastOrDefault()?.Job,rate);
 var current=LandSurvey.EffectiveDecree(w,site,w.ElapsedSeconds);if(current!=null)SocialText(box,"現地の発効済み布告："+(int)current.Input.Rate+"%");
 }
}
