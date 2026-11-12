using System.Linq;
using Godot;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 void ShowSecurity(VBoxContainer box,long rev,ContractWorld w,EntityId site){if(!LocalSecurity.Enabled(w))return;
 void Add(string key,string text,SecurityAction action,EntityId? guard=null){var b=new Button{Text=text};box.AddChild(b);banditButtons[key]=b;b.Pressed+=()=>{CancelAdvancePrefetch();message.Text=Session.ManageSecurity(new(new("request:security-ui/"+rev+"/"+key.Replace(':','/')),action,site,guard),rev).VisibleReason;Refresh();ShowBandits();};}
 {}
 if(LocalSecurity.Known(w,site) is {} f)SocialText(box,"確認時点 "+f.At+"秒：賊"+f.Bandits+"人・警固"+f.Guards+"人。"+f.Reaction);else SocialText(box,"周辺の治安はまだ確認していません。");
 Add("security-see","村と商人に現地の様子を尋ねる",SecurityAction.Observe);
 foreach(var person in LocalSecurity.Candidates(w,site))Add("security-"+person.Value,w.NewGame!.Start.People.Single(p=>p.Id==person.Value).Name+"へ警固を頼む",SecurityAction.Assign,person);
 }
}
