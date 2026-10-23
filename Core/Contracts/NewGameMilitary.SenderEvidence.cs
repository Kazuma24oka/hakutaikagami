using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public static partial class NewGameMilitary
{
 public const string UnitSenderEvidenceContract="sender-physical-receipt-evidence-known-failure-bounded-copy-v1";
 static bool SenderEvidenceAction(NewMilitaryAction action)=>action==NewMilitaryAction.RemoteRecall||UnitInquiryAction(action)||UnitRetryAction(action)||CancellationAction(action)||DirectiveAction(action)||DirectiveRetryAction(action);
 static bool UsesSenderEvidence(ContractWorld w,TroopDispatch d){var m=w.Messaging.Messages.Single(m=>m.Id==d.Message);return (d.Role==UnitDirectiveRole?ContractJson.Read<UnitDirectiveNote>(System.Text.Encoding.UTF8.GetBytes(m.Body)).TransportPolicy:d.Role==ReceivingRole?ContractJson.Read<UnitPostOrder>(System.Text.Encoding.UTF8.GetBytes(m.Body)).TransportPolicy:d.Role==CancellationRole?CancellationNote(w,d).TransportPolicy:ContractJson.Read<UnitPostInquiry>(System.Text.Encoding.UTF8.GetBytes(m.Body)).TransportPolicy)==UnitSenderEvidenceContract;}
 static bool RetryUsesEvidence(ContractWorld w,TroopDispatch d,string? policy)=>policy==UnitSenderEvidenceContract||UsesSenderEvidence(w,d);
 static bool KnownFailedAttempt(MessageAttempt a,bool evidence)=>a.Status is AttemptStatus.Lost or AttemptStatus.Rejected||evidence&&a.Status==AttemptStatus.Delivered&&a.ArrivedLate&&a.FailureAt!=null;
 public static bool ReceiptConfirmedToSender(ContractWorld w,string original,string sender)
 {
  // A recorded local handover at the sender post is direct evidence; a distant global receipt is not.
  if(w.Messaging.Messages.Single(m=>m.Id==original).SenderId!=sender)return false;
  int senderIndex=Array.FindIndex(UnitIds,id=>id.Value==sender);if(senderIndex>=0)return (w.NewMilitary?.HeldDelivery?.Transfers??[]).Any(t=>t.Message==original&&w.NewMilitary!.HeldDelivery!.Arrivals.Any(a=>a.Id==t.Id&&a.Received&&DeployedAt(w,a.At)&&CustodyAt(w,a.At,a.Revision)&&StrengthAt(w,senderIndex,a.At)>0&&RegionalGround.Distance(Position(w,senderIndex,a.At),a.RecipientPosition)<=1000));
  if(sender!=w.PlayerControl.Value)return false;
  if(KnownDirectiveAck(w,original,sender)!=null)return true;
  if(CancellationReceiptEvidence(w,original,sender))return true;
  if((w.NewMilitary?.HeldDelivery?.Transfers??[]).Any(t=>t.Message==original&&w.NewMilitary!.HeldDelivery!.Arrivals.Any(a=>a.Id==t.Id&&a.Received&&RegionalGround.Distance(PhysicalPerson(w,a.At),a.RecipientPosition)<=1000)))return true;
  if(UnitReceipts(w).Any(r=>r.Delivered&&UnitDispatches(w).Single(d=>d.Id==r.Id).Message==original&&MessageFlow.Knows(w.Messaging,r.ReplyMessage,sender)))return true;
  if(w.NewMilitary?.CommandHolding?.Outcomes.Any(f=>f.Original==original&&f.ReplyMessage!=null&&MessageFlow.Knows(w.Messaging,f.ReplyMessage,sender))==true)return true;
  foreach(var r in w.NewMilitary?.UnitInquiry?.Receipts??[])if(r.Delivered&&MessageFlow.Knows(w.Messaging,r.ReplyMessage,sender)){var quote=ContractJson.Read<UnitPostInquiryAnswer>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==r.ReplyMessage).Body));if(quote.Originals.Any(o=>o.Original==original&&o.Received))return true;}
  return false;
 }
 static bool SenderHasNoReceipt(ContractWorld w,TroopDispatch d,string message,string sender)=>UsesSenderEvidence(w,d)?!ReceiptConfirmedToSender(w,message,sender):!w.Messaging.Receipts.Any(r=>r.MessageId==message);
}
