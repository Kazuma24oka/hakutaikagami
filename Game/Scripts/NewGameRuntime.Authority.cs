using Godot;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 Label militaryAuthorityStatus=null!;
 string MilitaryActionNameForActor(ContractWorld w,NewMilitaryAction a){if(a==NewMilitaryAction.Request&&NewGameMilitary.CanAssumeReturnedGarrison(w))return "新当主と軍務を再確認";bool own=NewGameMilitary.CanRequestOwnAuthority(w)||w.NewMilitary!=null&&NewGameMilitary.CasePrincipal(w)==Session.PlayerControl&&HouseSuccession.Head(w,w.NewMilitary.Terms.Owner)==Session.PlayerControl;if(!own)return MilitaryActionName(a);return a switch{NewMilitaryAction.Request=>"自家の軍務を準備",NewMilitaryAction.Accept=>"軍務の準備を確定",NewMilitaryAction.Pickup=>"自家兵の指揮を現地で引き受ける",NewMilitaryAction.Report=>"自隊の状況を記録・返報",NewMilitaryAction.Return=>"兵の指揮を返還して軍務を終える",_=>MilitaryActionName(a)};}

 void BuildAuthorityUI(VBoxContainer root){militaryAuthorityStatus=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};root.AddChild(militaryAuthorityStatus);}
 void RefreshAuthorityUI(){if(militaryAuthorityStatus==null)return;militaryAuthorityStatus.Text=NewGameMilitary.AuthorityKnownStatus(Snapshot());militaryAuthorityStatus.Visible=militaryAuthorityStatus.Text.Length>0;}
 internal string AuthorityText=>militaryAuthorityStatus?.Text??"";
 object? AuthorityPose()=>militaryAuthorityStatus==null?null:new{militaryAuthorityStatus.Visible,militaryAuthorityStatus.Text};
}
