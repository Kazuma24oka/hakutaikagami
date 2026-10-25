namespace HakutaiKagami.Core.Contracts;
public sealed record UnitInquiryTarget(string Contract,KnownMilitaryTarget Known);
public static partial class NewGameMilitary
{
 public const string UnitRecipientContract="known-own-unit-fixed-post-query-recipient-owned-answer-v1";
 public static string UnitRecipientName(ContractWorld w,EntityId unit)=>Array.IndexOf(UnitIdsFor(w),unit) switch{0=>"本隊",1=>"左分隊",2=>"右分隊",_=>"部隊"};
 static void ValidateInquiryTarget(ContractWorld w,NewMilitaryInput input)
 {
  if(input.InquiryTarget is not {} target)return;
  Need(input.Action==NewMilitaryAction.InquireUnitKnown&&target.Contract==UnitRecipientContract&&RecognitionEnabled(w)&&input.Amount==0&&input.A==null&&input.B==null,"選択した自隊への照会だけに既知宛先を指定します");
  Need(target.Known!=null&&UnitIdsFor(w).Contains(target.Known.Unit)&&SelectKnownTarget(w,target.Known.Unit)==target.Known,"照会対象・記録地点・確認時刻・根拠が一致しません");
 }
 static int ReceivingUnitIndex(ContractWorld w,TroopDispatch dispatch)=>Array.IndexOf(UnitIdsFor(w),dispatch.Recipient);
}
