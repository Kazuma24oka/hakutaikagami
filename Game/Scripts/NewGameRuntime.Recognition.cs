using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 ContractWorld? recognitionSnapshot;MilitaryKnownUnit[] recognizedUnits=[];
 readonly Dictionary<EntityId,(MeshInstance3D Marker,Label Label)> recognitionGhosts=new();
 MilitaryKnownUnit[] RecognizedUnits(ContractWorld w){if(!ReferenceEquals(recognitionSnapshot,w)){recognitionSnapshot=w;recognizedUnits=NewGameMilitary.KnownMilitary(w);}return recognizedUnits;}
 MilitaryKnownUnit? RecognizedUnit(ContractWorld w,EntityId id)=>RecognizedUnits(w).FirstOrDefault(r=>r.Unit==id);
 static string KnownPhase(string phase)=>phase switch{"routed"=>"・敗走","rout-blocked"=>"・敗走経路待ち","regrouping"=>"・再集結待ち","regrouped"=>"・再集結済み","destroyed"=>"・壊滅",_=>""};
 static string DatedKnowledge(MilitaryKnownUnit row)=>row.Live?KnownPhase(row.Phase):"（開始"+row.ObservedAt+"秒・"+row.Source+"）";
 float KnownMilitarySpan(){var w=Snapshot();if(!NewGameMilitary.RecognitionEnabled(w))return 90;var own=FieldWorld.FromWorld(RegionalCampaign.ActorPointAt(w,w.PlayerControl,w.ElapsedSeconds));return Mathf.Clamp((float)RecognizedUnits(w).Select(r=>RegionalGround.Distance(own,r.Position)/1000d*2.4).DefaultIfEmpty(90).Max(),90,360);}
 void PlaceKnownLabel(Label label,Vector3 point){PlaceWorldLabel(label,point);var size=label.GetCombinedMinimumSize();label.Position=new(Mathf.Clamp(label.Position.X,8,Math.Max(8,view.Size.X-size.X-8)),Mathf.Clamp(label.Position.Y,8,Math.Max(8,view.Size.Y-size.Y-8)));}
 string MilitaryKnowledgeSummary(ContractWorld w){if(!NewGameMilitary.RecognitionEnabled(w))return w.NewMilitary==null?"":" / 旧版の技術表示・観測記録未開始";var rows=RecognizedUnits(w).Where(r=>r.Kind=="own").ToArray();return " / 現在確認："+rows.Count(r=>r.Live)+"隊 / 過去情報："+rows.Count(r=>!r.Live)+"隊";}
 object? RecognitionPose(ContractWorld w)=>!NewGameMilitary.RecognitionEnabled(w)?null:new{Known=RecognizedUnits(w),Couriers=NewGameMilitary.ProjectNearbyMilitaryCouriers(w).Select(c=>new{c.Token,c.Position,c.Next,Actual=courierActors.TryGetValue(new(c.Token),out var slot)?new{Position=V(slot.Marker.Position),slot.Marker.Visible,slot.Label.Text}:null}),PastExercise=recognitionGhosts.Where(p=>p.Value.Marker.Visible).OrderBy(p=>p.Key.Value).Select(p=>new{Unit=p.Key,Position=V(p.Value.Marker.Position),p.Value.Label.Text})};
 void RenderRecognizedMilitary(ContractWorld w,FieldPoint center)
 {
  RenderExercise(w,center);RenderWithdrawal(w,center);
  foreach(var actor in militaryModels)actor.Visible=false;
  for(int i=0;i<militaryMarkers.Count;i++){
   var pair=militaryMarkers[i];var row=RecognizedUnit(w,NewGameMilitary.UnitIds[i]);pair.Marker.Visible=pair.Label.Visible=false;
   if(row==null){pair.Marker.Position=Vector3.Zero;pair.Label.Text="";if(i>0){militaryModels[i-1].Position=Vector3.Zero;militaryModels[i-1].Rotation=Vector3.Zero;}continue;}
   var p=new FieldPoint(row.Position.X+(long)Math.Round((row.Next.X-row.Position.X)*fraction),row.Position.Y+(long)Math.Round((row.Next.Y-row.Position.Y)*fraction));pair.Marker.Position=WithdrawalPoint(p,center,.1f);pair.Marker.Visible=pair.Label.Visible=RegionalGround.Distance(p,center)<150000;
   pair.Label.Text="主家"+(i==0?"本隊":i==1?"左分隊":"右分隊")+row.Soldiers+DatedKnowledge(row);PlaceKnownLabel(pair.Label,pair.Marker.Position+Vector3.Up*(i==0?2.5f:1.5f));
   if(i>0){var actor=militaryModels[i-1];actor.Position=pair.Marker.Position-Vector3.Up*.1f;actor.Visible=pair.Marker.Visible&&row.Live&&row.Soldiers>0;var heading=new Vector2(row.Next.X-row.Position.X,-(row.Next.Y-row.Position.Y));actor.Rotation=new(0,heading==Vector2.Zero?Mathf.Pi:Mathf.Atan2(heading.X,heading.Y),0);bool fighting=row.Live&&ExerciseFighting(w,i);string animation=fighting?"attack":row.Next!=row.Position?"walk":"idle";if(fighting)actor.Rotation=new(0,Mathf.Pi/2,0);var clip=actor.Player.GetAnimation(actor.Clips[animation]);double phase=row.Live&&row.Next!=row.Position?MilitaryDistance(w,i,w.ElapsedSeconds+fraction)/1000d/(actor.WalkStride*actor.Scale.Z)*clip.Length:w.ElapsedSeconds+fraction;actor.Freeze(animation,phase%clip.Length);}
  }
  foreach(var ghost in recognitionGhosts.Values){ghost.Marker.Visible=ghost.Label.Visible=false;ghost.Marker.Position=Vector3.Zero;ghost.Label.Text="";}
  foreach(var row in RecognizedUnits(w).Where(r=>r.Kind=="exercise"&&!r.Live)){
   if(!recognitionGhosts.TryGetValue(row.Unit,out var ghost)){var marker=new MeshInstance3D{Mesh=new CylinderMesh{TopRadius=.6f,BottomRadius=.6f,Height=.04f},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("b3a36d")}};stage.AddChild(marker);ghost=(marker,WorldLabel("60/remembered/"+row.Unit.Value,14,new Color("d6c495")));recognitionGhosts[row.Unit]=ghost;}
   ghost.Marker.Position=WithdrawalPoint(row.Position,center,.07f);ghost.Marker.Visible=ghost.Label.Visible=RegionalGround.Distance(row.Position,center)<150000;ghost.Label.Text="演習相手"+row.Soldiers+DatedKnowledge(row);PlaceKnownLabel(ghost.Label,ghost.Marker.Position+Vector3.Up*2f);
  }
 }
 internal bool RecognitionModelsMatch(){var w=Snapshot();if(!NewGameMilitary.RecognitionEnabled(w)||!nationMode&&(terrain.Picks.Count==0||MilitaryPick((Vector2)view.Size/2)==null))return false;var center=FieldWorld.FromWorld(RenderPosition(Branch(Session.PlayerControl)));for(int i=0;i<militaryMarkers.Count;i++){var row=RecognizedUnit(w,NewGameMilitary.UnitIds[i]);var slot=militaryMarkers[i];if(row==null){if(slot.Marker.Visible||slot.Label.Visible||i>0&&militaryModels[i-1].Visible)return false;continue;}var p=new FieldPoint(row.Position.X+(long)Math.Round((row.Next.X-row.Position.X)*fraction),row.Position.Y+(long)Math.Round((row.Next.Y-row.Position.Y)*fraction));if(slot.Marker.Position.DistanceTo(WithdrawalPoint(p,center,.1f))>.001f)return false;if(i>0&&militaryModels[i-1].Visible!=(slot.Marker.Visible&&row.Live&&row.Soldiers>0))return false;if(!row.Live&&!slot.Label.Text.Contains("開始"+row.ObservedAt+"秒"))return false;}var main=RecognizedUnit(w,NewGameMilitary.UnitIds[0]);if(main?.Live!=true&&(separateLead?.Visible==true||baggage?.Visible==true))return false;var couriers=NewGameMilitary.ProjectNearbyMilitaryCouriers(w).ToDictionary(c=>new EntityId(c.Token));foreach(var slot in courierActors.Where(p=>p.Value.Marker.Visible)){if(!couriers.TryGetValue(slot.Key,out var c)||slot.Value.Label.Text!="目の前の伝令")return false;var point=new FieldPoint(c.Position.X+(long)Math.Round((c.Next.X-c.Position.X)*fraction),c.Position.Y+(long)Math.Round((c.Next.Y-c.Position.Y)*fraction));if(slot.Value.Marker.Position.DistanceTo(WithdrawalPoint(point,center,1))>.001f)return false;}return true;}
}
