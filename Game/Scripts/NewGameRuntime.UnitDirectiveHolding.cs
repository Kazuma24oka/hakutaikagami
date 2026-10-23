using Godot;
using System;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 Label directiveOriginalResult=null!;
 void BuildDirectiveHoldingUI(){Add(directiveRetryBar,"unit-directive-handoff","目の前の原本を届ける",HandoffSelectedDirective);buttons["unit-directive-handoff"].TooltipText="目の前の伝令が実際に持つ原本を、同じ隊の確認済み所在地へ運びます。内容と元期限は変えません。";directiveOriginalResult=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=SizeFlags.ExpandFill,CustomMinimumSize=new(300,0)};directiveRetryBar.AddChild(directiveOriginalResult);}
 void RefreshDirectiveHolding(){if(directiveOriginalResult==null)return;var w=Snapshot();var selected=selectedDirectiveOriginal==null?(EntityId?)null:new(selectedDirectiveOriginal);var paper=selected is {} id?NewGameMilitary.SelectDirectivePaper(w,id):null;buttons["unit-directive-handoff"].Disabled=paper==null;directiveOriginalResult.Text=selected is {} original?NewGameMilitary.UnitDirectiveOriginalStatus(w,original):"";}
 void HandoffSelectedDirective(){if(selectedDirectiveOriginal==null)return;var w=Snapshot();var paper=NewGameMilitary.SelectDirectivePaper(w,new(selectedDirectiveOriginal));if(paper==null){message.Text="この原本を持つ伝令が目の前にいないか、受渡し条件を満たしていません。";Refresh();return;}Submit(Session.DecideNewMilitary(new NewMilitaryInput(new("request:unit-directive-handoff-ui/"+Session.Revision),NewMilitaryAction.DeliverHeldUnitDirective){DirectiveHandoff=paper},Session.PlayerControl,Session.Revision));}
 object? DirectiveHoldingPose()=>directiveOriginalResult==null?null:new{Enabled=!buttons["unit-directive-handoff"].Disabled,Text=directiveOriginalResult.Text};
}
