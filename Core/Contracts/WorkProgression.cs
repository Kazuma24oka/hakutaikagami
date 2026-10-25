namespace HakutaiKagami.Core.Contracts;
// Reuses the existing physical assessment original and its opening receipt.
// This is issuer-specific fulfillment recognition, not a global reputation or promotion.
public static class WorkProgression
{
    public const string Policy="local-work-recognized-fulfillment-next-consignment-v2";
    public const string Achievement="点検の履行を認める";
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.WorkPolicy is Policy or WorkFollowup.Policy;
    public static bool Completed(string text)=>text is "点検完了の申告を受領" or Achievement;
    public static int Recognized(ContractWorld w,EntityId issuer,long at=long.MaxValue,long revision=long.MaxValue)
    {
        if(!Enabled(w)||w.WorkCycle==null)return 0;
        var notes=w.WorkCycle.Openings.Where(o=>o.At<at||o.At==at&&o.Revision<revision).Select(o=>w.Messaging.Messages.Single(m=>m.Id==o.Message)).Where(m=>m.Kind==AssessmentNotice.Kind&&m.SenderId==issuer.Value&&m.RecipientId==w.PlayerControl.Value).Select(m=>AssessmentNotice.Read(m.Body));
        return notes.GroupBy(p=>p.Task).Count(g=>g.OrderByDescending(p=>p.Version).First().Review.Achievement==Achievement);
    }
    public static long Amount(ContractWorld w,EntityId issuer,long at=long.MaxValue,long revision=long.MaxValue)=>WorkFollowup.Enabled(w)?WorkFollowup.Terms(w,issuer,at,revision).Amount:Recognized(w,issuer,at,revision)>0?200:TransportWork.Amount;
    public static long Fee(ContractWorld w,EntityId issuer,long at=long.MaxValue,long revision=long.MaxValue)=>WorkFollowup.Enabled(w)?WorkFollowup.Terms(w,issuer,at,revision).Fee:Recognized(w,issuer,at,revision)>0?30:TransportWork.Fee;
}
