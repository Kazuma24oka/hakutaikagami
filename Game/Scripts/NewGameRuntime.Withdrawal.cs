using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    Officer3DCustomActor? separateLead;
    MeshInstance3D? baggage;
    readonly System.Collections.Generic.Dictionary<EntityId,(MeshInstance3D Marker,Label Label)> courierActors=new();
    Label? baggageLabel;
    object? WithdrawalPose(ContractWorld w)=>w.NewMilitary?.Withdrawal==null?null:new{State=w.NewMilitary.Withdrawal,Retries=w.NewMilitary.UnitRetry,Inquiries=w.NewMilitary.UnitInquiry,ReplyRecovery=w.NewMilitary.ReplyRecovery,Cancellation=w.NewMilitary.Cancellation,CommandHolding=w.NewMilitary.CommandHolding,HeldDelivery=w.NewMilitary.HeldDelivery,Person=NewGameMilitary.PersonPosition(w,w.ElapsedSeconds),Detached=NewGameMilitary.Detached(w),Carrier=w.ResourceLots.Single(l=>l.Id==NewGameMilitary.Carry).MobileHolder,Lead=separateLead==null?null:new{Position=V(separateLead.Position),Rotation=V(separateLead.Rotation),separateLead.Visible},Cargo=baggage==null?null:new{Position=V(baggage.Position),baggage.Visible},Couriers=NewGameMilitary.CourierFrames(w,w.ElapsedSeconds).Select(f=>new{f.Carrier,f.Position,f.Next,f.Purpose,f.Holding,Actual=courierActors.TryGetValue(f.Carrier,out var slot)?new{Position=V(slot.Marker.Position),slot.Marker.Visible,slot.Label.Text}:null})};
    Vector3 WithdrawalPoint(FieldPoint p,FieldPoint center,float up=0)=>new((p.X-center.X)/1000f,ContinuousWorld.Height(p)/1000f+up,-(p.Y-center.Y)/1000f);
    void RenderWithdrawal(ContractWorld w,FieldPoint center)
    {
        if(separateLead!=null)separateLead.Visible=false;if(baggage!=null)baggage.Visible=baggageLabel!.Visible=false;foreach(var slot in courierActors.Values){slot.Marker.Visible=slot.Label.Visible=false;slot.Marker.Position=Vector3.Zero;}
        if(w.NewMilitary?.Withdrawal is not {} x)return;
        if(separateLead==null){separateLead=new(){Scale=Vector3.One*2};stage.AddChild(separateLead);separateLead.Apply(Officer3DCustomActor.Preset("veteran"));baggage=new(){Mesh=new BoxMesh{Size=new(1.2f,.9f,.8f)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("957b42")}};stage.AddChild(baggage);baggageLabel=WorldLabel("40/baggage",14);baggageLabel.Text="本隊の兵糧";}
        bool observed=NewGameMilitary.RecognitionEnabled(w);var row=observed?RecognizedUnit(w,NewGameMilitary.UnitIds[0]):null;bool confirmed=!observed||row?.Live==true;
        var p=observed?row?.Position??center:NewGameMilitary.Position(w,0,w.ElapsedSeconds);var next=observed?row?.Next??p:NewGameMilitary.Position(w,0,w.ElapsedSeconds+1);var current=new FieldPoint(p.X+(long)Math.Round((next.X-p.X)*fraction),p.Y+(long)Math.Round((next.Y-p.Y)*fraction));
        separateLead.Position=WithdrawalPoint(current,center);separateLead.Visible=confirmed&&NewGameMilitary.Detached(w)&&RegionalGround.Distance(current,center)<150000;
        var heading=new Vector2(next.X-p.X,-(next.Y-p.Y));separateLead.Rotation=new(0,heading==Vector2.Zero?0:Mathf.Atan2(heading.X,heading.Y),0);bool fighting=confirmed&&ExerciseFighting(w,0);string animation=fighting?"attack":next!=p?"walk":"idle";if(fighting)separateLead.Rotation=new(0,Mathf.Pi/2,0);var clip=separateLead.Player.GetAnimation(separateLead.Clips[animation]);double phase=next!=p?MilitaryDistance(w,0,w.ElapsedSeconds+fraction)/1000d/(separateLead.WalkStride*separateLead.Scale.Z)*clip.Length:w.ElapsedSeconds+fraction;separateLead.Freeze(animation,phase%clip.Length);
        baggage!.Position=WithdrawalPoint(current,center,.5f)+Vector3.Right*1.3f;baggage.Visible=baggageLabel!.Visible=confirmed&&NewGameMilitary.UnitCarries(w)&&RegionalGround.Distance(current,center)<150000;PlaceWorldLabel(baggageLabel,baggage.Position+Vector3.Up);
        RenderPhysicalCouriers(w,center);
    }
    void RenderPhysicalCouriers(ContractWorld w,FieldPoint center)
    {
        var frames=NewGameMilitary.RecognitionEnabled(w)?NewGameMilitary.ProjectNearbyMilitaryCouriers(w).Select(c=>new UnitCourierFrame(new(c.Token),c.Position,c.Next,"目の前の伝令",false)):NewGameMilitary.CourierFrames(w,w.ElapsedSeconds);
        foreach(var frame in frames){
            if(!courierActors.TryGetValue(frame.Carrier,out var slot)){
                var marker=new MeshInstance3D{Mesh=new SphereMesh{Radius=.4f,Height=.8f},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("74d4db")}};stage.AddChild(marker);
                var label=WorldLabel("50/unit-courier/"+frame.Carrier.Value,14);slot=(marker,label);courierActors[frame.Carrier]=slot;
            }
            var p=new FieldPoint(frame.Position.X+(long)Math.Round((frame.Next.X-frame.Position.X)*fraction),frame.Position.Y+(long)Math.Round((frame.Next.Y-frame.Position.Y)*fraction));
            slot.Marker.Position=WithdrawalPoint(p,center,1);slot.Marker.Visible=slot.Label.Visible=RegionalGround.Distance(p,center)<150000;slot.Label.Text=frame.Purpose;PlaceWorldLabel(slot.Label,slot.Marker.Position+Vector3.Up);
        }
    }
}
