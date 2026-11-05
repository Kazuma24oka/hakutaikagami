namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
 bool drivingSocialCouriers;
 void DriveSocialCouriers()
 {
  if(drivingSocialCouriers||state.SocialWorld==null)return;
  drivingSocialCouriers=true;
  try{
   foreach(var original in SocialWorld.Ledger(state).Where(e=>e.Input.Action==SocialWorldAction.DispatchGift&&e.GiftTerms?.Movement!=null).ToArray()){
    try{
    var carrier=original.Input.Carrier??original.Actor;if(carrier==state.PlayerControl)continue;
    var outward=original.GiftTerms!.Movement!;
    var request=state.Requests.SingleOrDefault(r=>r.Id==outward.RequestId);
    if(request==null&&!SocialWorld.Ledger(state).Any(e=>e.Input.Reference==original.Input.Id&&e.Input.Action==SocialWorldAction.CourierBlocked)){
     var result=Submit(outward,state.CommitSequence);if(result.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){CourierFailure(original,false);}
    }
    var task=state.Tasks.SingleOrDefault(t=>t.RequestId==outward.RequestId);
    var gift=SocialWorld.Gifts(state,carrier).Single(g=>g.Id==original.Input.Id);
    if(task?.Execution is ExecutionPhase.Failed or ExecutionPhase.Stopped&&gift.Phase=="運搬・受領待ち")CourierFailure(original,false);
    gift=SocialWorld.Gifts(state,carrier).Single(g=>g.Id==original.Input.Id);
    if(gift.Phase=="運搬・受領待ち"||SocialWorld.Site(state,carrier) is not {} site||site==original.Site)continue;
    var back=SocialWorld.Ledger(state).SingleOrDefault(e=>e.Input.Reference==original.Input.Id&&e.Input.Action==SocialWorldAction.StartCourierReturn);
    if(back==null){
     try{var intent=TravelWorld.ActorMove(state,SocialWorld.CourierRequest(original.Input.Id,true).Value,carrier,NewGameWorld.Node(original.Site.Value),true,SocialWorld.LandRoute);var terms=new SocialGiftTerms(original.Site,intent,original.GiftTerms.SenderRank,original.GiftTerms.CourierRank);Change(w=>SocialWorld.Add(w,new(new("social:courier-return-plan/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(original.Input.Id.Value))[..24]),SocialWorldAction.StartCourierReturn,original.Actor,Reference:original.Input.Id),carrier,site,terms));back=SocialWorld.Ledger(state).Single(e=>e.Input.Reference==original.Input.Id&&e.Input.Action==SocialWorldAction.StartCourierReturn);}
     catch(InvalidDataException){CourierFailure(original,true);continue;}
    }
    if(back.GiftTerms?.Movement is {} returning&&!state.Requests.Any(r=>r.Id==returning.RequestId)&&!SocialWorld.Ledger(state).Any(e=>e.Input.Reference==original.Input.Id&&e.Input.Action==SocialWorldAction.CourierReturnBlocked)){
     var result=Submit(returning,state.CommitSequence);if(result.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted))CourierFailure(original,true);
    }
    }catch(InvalidDataException){var current=SocialWorld.Gifts(state,original.Input.Carrier??original.Actor).Single(g=>g.Id==original.Input.Id);CourierFailure(original,current.Phase!="運搬・受領待ち");}
   }
  }finally{drivingSocialCouriers=false;}
 }
 void CourierFailure(SocialWorldEvent original,bool returning)
 {
  var kind=returning?SocialWorldAction.CourierReturnBlocked:SocialWorldAction.CourierBlocked;
  if(SocialWorld.Ledger(state).Any(e=>e.Input.Reference==original.Input.Id&&e.Input.Action==kind))return;
  var actor=original.Input.Carrier??original.Actor;var site=SocialWorld.Site(state,actor)??original.Site;
  var id=new EntityId("social:courier-failure/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(original.Input.Id.Value+"/"+returning))[..24]);
  Change(w=>SocialWorld.Add(w,new(id,kind,original.Actor,Reference:original.Input.Id),actor,site));
 }
}
