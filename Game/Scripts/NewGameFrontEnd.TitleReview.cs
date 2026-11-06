using System;
using System.IO;
using System.Threading.Tasks;
using Godot;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
 internal async Task<NewGameRuntime> ReviewTitleToPlay(string root){if(TestKey!=null)throw new InvalidDataException("通常入口の設定で検査してください");campaigns=new NewCampaignStore(root);Press("new");await Frame();Press("next");await Frame();Press("next");await Frame();if(buttons["next"].Disabled)throw new InvalidDataException("配置から先へ進めません："+previewText.Text);Press("next");await Frame();Press("confirm");await Frame();if(Directory.Exists(root)&&Directory.GetFileSystemEntries(root).Length!=0)throw new InvalidDataException("武将を選ぶ前に保存が作成されています");PickRow("person:nobunaga");Press("begin");await Frame();if(game==null||game.Session.PlayerControl.Value!="person:nobunaga")throw new InvalidDataException("選択した武将で開始できません："+notice.Text);await WaitSave();return game;}
}
