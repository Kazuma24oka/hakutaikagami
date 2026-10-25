using Godot;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    readonly List<(Officer3DCustomActor Actor,Label Label)> exerciseModels=new();
    ContractWorld? exerciseSnapshot;ExerciseStateView? exerciseState;
    ExerciseStateView CachedExercise(ContractWorld w){if(!ReferenceEquals(exerciseSnapshot,w)){exerciseSnapshot=w;exerciseState=NewGameMilitary.ExerciseState(w,w.ElapsedSeconds);}return exerciseState!;}
    bool ExerciseFighting(ContractWorld w,int i)=>WorldClashes.Enabled(w)?NewGameMilitary.Deployed(w)&&WorldClashes.AttacksNow(w,NewGameMilitary.UnitIdsFor(w)[i]):NewGameMilitary.CombatActive(w)&&CachedExercise(w).Own[i].Morale>25&&CachedExercise(w).Opponents[i].Morale>25;
    object? ExercisePose(ContractWorld w)=>NewGameMilitary.CombatStarted(w)?new{Episode=CachedExercise(w).Episode,Own=CachedExercise(w).Own,Opponents=CachedExercise(w).Opponents,Losses=CachedExercise(w).Losses,Models=exerciseModels.Select((m,i)=>new{Unit=CachedExercise(w).Opponents[i].Unit,Position=V(m.Actor.Position),m.Actor.Visible,m.Label.Text})}:null;
    void RenderExercise(ContractWorld w,FieldPoint center)
    {
        bool started=NewGameMilitary.ExerciseVisible(w);
        if(started&&exerciseModels.Count==0)for(int i=0;i<3;i++){
            var actor=new Officer3DCustomActor{Scale=Vector3.One*2};stage.AddChild(actor);actor.Apply(Officer3DCustomActor.Preset("veteran"));actor.AddChild(new MeshInstance3D{Mesh=new CylinderMesh{TopRadius=.35f,BottomRadius=.4f,Height=.06f},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("b34c41")},Position=Vector3.Up*.04f});
            var label=WorldLabel("30/exercise/"+i,16,new Color("ff9c91"));exerciseModels.Add((actor,label));
        }
        var combat=CachedExercise(w);
        for(int i=0;i<exerciseModels.Count;i++){
            var m=exerciseModels[i];m.Actor.Visible=m.Label.Visible=started&&NewGameMilitary.Deployed(w);if(!started){m.Actor.Position=Vector3.Zero;m.Actor.Rotation=Vector3.Zero;m.Label.Text="";continue;}
            bool observed=NewGameMilitary.RecognitionEnabled(w);var row=observed?RecognizedUnit(w,combat.Opponents[i].Unit):null;
            if(observed&&(row==null||!row.Live)){m.Actor.Visible=m.Label.Visible=false;m.Actor.Position=Vector3.Zero;m.Actor.Rotation=Vector3.Zero;m.Label.Text="";continue;}
            var p=row?.Position??NewGameMilitary.ExercisePosition(w,i);int men=row?.Soldiers??combat.Opponents[i].Soldiers;int morale=row?.Morale??combat.Opponents[i].Morale;
            m.Actor.Position=new((p.X-center.X)/1000f,ContinuousWorld.Height(p)/1000f,-(p.Y-center.Y)/1000f);m.Actor.Rotation=new(0,-Mathf.Pi/2,0);string clip=ExerciseFighting(w,i)?"attack":"idle";m.Actor.Freeze(clip,(w.ElapsedSeconds+fraction)%m.Actor.Player.GetAnimation(m.Actor.Clips[clip]).Length);m.Label.Text="演習相手"+(NewGameMilitary.ExerciseNumber(w)>1?"（"+NewGameMilitary.ExerciseNumber(w)+"回目）":"")+" "+men;PlaceWorldLabel(m.Label,m.Actor.Position+Vector3.Up*6f);m.Actor.Visible=m.Label.Visible=m.Actor.Visible&&men>0;
        }
    }
}
