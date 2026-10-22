using Godot;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    readonly List<(Officer3DCustomActor Actor,Label Label)> exerciseModels=new();
    bool ExerciseFighting(ContractWorld w,int i){if(!NewGameMilitary.CombatActive(w))return false;var hits=NewGameMilitary.CombatLosses(w,w.ElapsedSeconds);return 100-hits.Where(l=>l.Unit==NewGameMilitary.UnitIds[i]).Sum(l=>l.Morale)>25&&100-hits.Where(l=>l.Unit==NewGameMilitary.ExerciseIds[i]).Sum(l=>l.Morale)>25;}
    object? ExercisePose(ContractWorld w)=>NewGameMilitary.CombatStarted(w)?new{Own=NewGameMilitary.OwnCombatReport(w,w.ElapsedSeconds),Losses=NewGameMilitary.CombatLosses(w,w.ElapsedSeconds),Models=exerciseModels.Select(m=>new{Position=V(m.Actor.Position),m.Actor.Visible,m.Label.Text})}:null;
    void RenderExercise(ContractWorld w,FieldPoint center)
    {
        bool started=NewGameMilitary.CombatStarted(w);
        if(started&&exerciseModels.Count==0)for(int i=0;i<3;i++){
            var actor=new Officer3DCustomActor{Scale=Vector3.One*2};stage.AddChild(actor);actor.Apply(Officer3DCustomActor.Preset("veteran"));actor.AddChild(new MeshInstance3D{Mesh=new CylinderMesh{TopRadius=.35f,BottomRadius=.4f,Height=.06f},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("b34c41")},Position=Vector3.Up*.04f});
            var label=WorldLabel("30/exercise/"+i,16,new Color("ff9c91"));exerciseModels.Add((actor,label));
        }
        var hits=NewGameMilitary.CombatLosses(w,w.ElapsedSeconds);
        for(int i=0;i<exerciseModels.Count;i++){
            var m=exerciseModels[i];m.Actor.Visible=m.Label.Visible=started&&NewGameMilitary.Deployed(w);if(!started)continue;
            var p=NewGameMilitary.ExercisePosition(w,i);int initial=i==0?40:30;int men=initial-hits.Where(l=>l.Unit==NewGameMilitary.ExerciseIds[i]).Sum(l=>l.Soldiers);int morale=100-hits.Where(l=>l.Unit==NewGameMilitary.ExerciseIds[i]).Sum(l=>l.Morale);
            m.Actor.Position=new((p.X-center.X)/1000f,ContinuousWorld.Height(p)/1000f,-(p.Y-center.Y)/1000f);m.Actor.Rotation=new(0,-Mathf.Pi/2,0);string clip=ExerciseFighting(w,i)?"attack":"idle";m.Actor.Freeze(clip,(w.ElapsedSeconds+fraction)%m.Actor.Player.GetAnimation(m.Actor.Clips[clip]).Length);m.Label.Text="演習相手 "+men;PlaceWorldLabel(m.Label,m.Actor.Position+Vector3.Up*6f);m.Actor.Visible=m.Label.Visible=m.Actor.Visible&&men>0;
        }
    }
}
