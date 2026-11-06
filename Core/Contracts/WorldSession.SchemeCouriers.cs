using System.Text;
namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
 bool drivingSchemes;
 void DriveSchemeCouriers()
 {
  if(drivingSchemes||state.Schemes==null)return;drivingSchemes=true;
  try{foreach(var mission in InformationSchemes.Events(state).Where(e=>e.Input.Action==SchemeAction.SendScout).ToArray()){
   var move=mission.Movement!;
   if(!state.Requests.Any(r=>r.Id==move.RequestId)&&!InformationSchemes.Events(state).Any(e=>e.Input.Reference==mission.Input.Id&&e.Input.Action==SchemeAction.ScoutBlocked)){
    var result=Submit(move,state.CommitSequence);if(result.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted))Change(w=>InformationSchemes.Add(w,new(InformationSchemes.Id("blocked",mission.Input.Id.Value),SchemeAction.ScoutBlocked,mission.Input.Person,mission.Input.Person,Reference:mission.Input.Id),mission.Input.Person,mission.Site));
   }
   var capture=InformationSchemes.Events(state).SingleOrDefault(e=>e.Input.Reference==mission.Input.Id&&e.Input.Action==SchemeAction.CaptureScout);if(capture==null||Assassination.CaptivityAt(state,mission.Input.Person,state.ElapsedSeconds,state.CommitSequence+1)!=null||SocialWorld.Site(state,mission.Input.Person) is not {} site||site==mission.Site)continue;
   var back=InformationSchemes.Events(state).SingleOrDefault(e=>e.Input.Reference==mission.Input.Id&&e.Input.Action==SchemeAction.PlanReturn);
   if(back==null){try{var returning=TravelWorld.ActorMove(state,InformationSchemes.Id("return-trip",mission.Input.Id.Value).Value,mission.Input.Person,NewGameWorld.Node(mission.Site.Value),true,InformationSchemes.LandRoute);Change(w=>InformationSchemes.Add(w,new(InformationSchemes.Id("return",mission.Input.Id.Value),SchemeAction.PlanReturn,mission.Input.Person,mission.Input.Person,Reference:mission.Input.Id),mission.Input.Person,site,returning));back=InformationSchemes.Events(state).Single(e=>e.Input.Reference==mission.Input.Id&&e.Input.Action==SchemeAction.PlanReturn);}catch(InvalidDataException){continue;}}
   if(!state.Requests.Any(r=>r.Id==back.Movement!.RequestId)){var result=Submit(back.Movement!,state.CommitSequence);if(result.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted))continue;}
  }}finally{drivingSchemes=false;}
 }
}
