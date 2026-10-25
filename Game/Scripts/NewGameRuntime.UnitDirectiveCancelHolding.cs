using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 void BuildDirectiveCancelHoldingUI(){Add(directiveCancelBar,"unit-directive-cancel-retain","原本を保持して取消を発送",()=>SendSelectedDirectiveCancellation(true));buttons["unit-directive-cancel-retain"].TooltipText="届かず伝令が戻った場合に、この取消書状の実物を保持します。以前の取消に紙を追加する操作ではありません。";Add(directiveCancelRetryBar,"unit-directive-cancel-handoff","取消原本の実物を届ける",HandoffSelectedCancellation);buttons["unit-directive-cancel-handoff"].TooltipText="本人の近くに実在する取消原本を、同じ隊の確認済み固定地点へ届けます。受取人がいなければ効力は生じません。";}
 void RefreshDirectiveCancelHolding(){if(!buttons.ContainsKey("unit-directive-cancel-retain"))return;buttons["unit-directive-cancel-retain"].Disabled=buttons["unit-directive-cancel"].Disabled;var w=Snapshot();buttons["unit-directive-cancel-handoff"].Disabled=selectedCancellationOriginal==null||!NewGameMilitary.DirectiveCancellationHandoffAllowed(w,NewGameMilitary.SelectDirectiveCancellationPaper(w,new(selectedCancellationOriginal)));}
 void HandoffSelectedCancellation(){if(selectedCancellationOriginal==null)return;var p=NewGameMilitary.SelectDirectiveCancellationPaper(Snapshot(),new(selectedCancellationOriginal));if(p==null)return;Submit(Session.DecideNewMilitary(new NewMilitaryInput(new("request:unit-directive-cancel-handoff-ui/"+Session.Revision),NewMilitaryAction.DeliverHeldUnitDirectiveCancellation){DirectiveCancelHandoff=p},Session.PlayerControl,Session.Revision));}
 object? DirectiveCancelHoldingPose()=>buttons.TryGetValue("unit-directive-cancel-handoff",out var hand)?new{RetainEnabled=!buttons["unit-directive-cancel-retain"].Disabled,HandoffEnabled=!hand.Disabled}:null;
 internal void ExerciseRetainedDirectiveCancelAt(long at){directiveCancelTime.Value=at;Press("unit-directive-cancel-retain");directiveCancelTime.Value=0;}
}
