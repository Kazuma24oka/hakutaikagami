using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    Officer3DCustomActor? separateLead;
    MeshInstance3D? baggage,fieldCourier;
    Label? baggageLabel,courierLabel;
    object? WithdrawalPose(ContractWorld w)=>w.NewMilitary?.Withdrawal==null?null:new{State=w.NewMilitary.Withdrawal,Retries=w.NewMilitary.UnitRetry,Inquiries=w.NewMilitary.UnitInquiry,Person=NewGameMilitary.PersonPosition(w,w.ElapsedSeconds),Detached=NewGameMilitary.Detached(w),Carrier=w.ResourceLots.Single(l=>l.Id==NewGameMilitary.Carry).MobileHolder,Lead=separateLead==null?null:new{Position=V(separateLead.Position),Rotation=V(separateLead.Rotation),separateLead.Visible},Cargo=baggage==null?null:new{Position=V(baggage.Position),baggage.Visible},Courier=fieldCourier==null?null:new{Position=V(fieldCourier.Position),fieldCourier.Visible}};
    Vector3 WithdrawalPoint(FieldPoint p,FieldPoint center,float up=0)=>new((p.X-center.X)/1000f,ContinuousWorld.Height(p)/1000f+up,-(p.Y-center.Y)/1000f);
    void RenderWithdrawal(ContractWorld w,FieldPoint center)
    {
        if(separateLead!=null)separateLead.Visible=false;if(baggage!=null)baggage.Visible=baggageLabel!.Visible=false;if(fieldCourier!=null){fieldCourier.Visible=courierLabel!.Visible=false;fieldCourier.Position=Vector3.Zero;}
        if(w.NewMilitary?.Withdrawal is not {} x)return;
        if(separateLead==null){separateLead=new(){Scale=Vector3.One*2};stage.AddChild(separateLead);separateLead.Apply(Officer3DCustomActor.Preset("veteran"));baggage=new(){Mesh=new BoxMesh{Size=new(1.2f,.9f,.8f)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("957b42")}};stage.AddChild(baggage);baggageLabel=WorldLabel("40/baggage",14);baggageLabel.Text="本隊の兵糧";fieldCourier=new(){Mesh=new SphereMesh{Radius=.4f,Height=.8f},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("74d4db")}};stage.AddChild(fieldCourier);courierLabel=WorldLabel("50/unit-courier",14);}
        var p=NewGameMilitary.Position(w,0,w.ElapsedSeconds);var next=NewGameMilitary.Position(w,0,w.ElapsedSeconds+1);var current=new FieldPoint(p.X+(long)Math.Round((next.X-p.X)*fraction),p.Y+(long)Math.Round((next.Y-p.Y)*fraction));
        separateLead.Position=WithdrawalPoint(current,center);separateLead.Visible=NewGameMilitary.Detached(w)&&RegionalGround.Distance(current,center)<150000;
        var heading=new Vector2(next.X-p.X,-(next.Y-p.Y));separateLead.Rotation=new(0,heading==Vector2.Zero?0:Mathf.Atan2(heading.X,heading.Y),0);bool fighting=ExerciseFighting(w,0);string animation=fighting?"attack":next!=p?"walk":"idle";if(fighting)separateLead.Rotation=new(0,Mathf.Pi/2,0);var clip=separateLead.Player.GetAnimation(separateLead.Clips[animation]);double phase=next!=p?MilitaryDistance(w,0,w.ElapsedSeconds+fraction)/1000d/(separateLead.WalkStride*separateLead.Scale.Z)*clip.Length:w.ElapsedSeconds+fraction;separateLead.Freeze(animation,phase%clip.Length);
        baggage!.Position=WithdrawalPoint(current,center,.5f)+Vector3.Right*1.3f;baggage.Visible=baggageLabel!.Visible=NewGameMilitary.UnitCarries(w)&&RegionalGround.Distance(current,center)<150000;PlaceWorldLabel(baggageLabel,baggage.Position+Vector3.Up);
        var dispatch=NewGameMilitary.UnitDispatches(w).LastOrDefault();if(dispatch==null)return;var receipt=NewGameMilitary.UnitReceipts(w).LastOrDefault(r=>r.Id==dispatch.Id);FieldPoint? courier=null;
        if(receipt==null){courier=RegionalGround.At(dispatch.Route,dispatch.Origin,w.ElapsedSeconds);courierLabel!.Text=dispatch.Role==NewGameMilitary.UnitInquiryRole?"本隊への照会伝令":"撤退命令の伝令";}
        else if(!NewGameMilitary.UnitReplies(w).Any(r=>r.Id==dispatch.Id)){courier=RegionalGround.At(receipt.ReplyRoute,dispatch.Destination,w.ElapsedSeconds);courierLabel!.Text=dispatch.Role==NewGameMilitary.UnitInquiryRole?"本隊の照会返書":"部隊からの返書";}
        if(courier is {} cp){fieldCourier!.Position=WithdrawalPoint(cp,center,1);fieldCourier.Visible=courierLabel!.Visible=RegionalGround.Distance(cp,center)<150000;PlaceWorldLabel(courierLabel,fieldCourier.Position+Vector3.Up);}
    }
}
