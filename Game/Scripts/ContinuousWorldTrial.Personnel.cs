using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class ContinuousWorldTrial
{
    AcceptDialog? personnelDialog;
    OptionButton personnelSite=null!;
    Label personnelBody=null!;
    void OpenPersonnelCatalog()
    {
        if(personnelDialog==null){
            personnelDialog=new AcceptDialog{Title="1560年シナリオ / 人物・勢力の収録台帳",Size=new(900,500),MinSize=new(740,440)};
            AddChild(personnelDialog);personnelDialog.GetOkButton().Text="閉じる";
            var col=new VBoxContainer();personnelDialog.AddChild(col);
            col.AddChild(new Label{Text="収録管理用：主人公の観測や現在地を示す画面ではありません",Modulate=new Color("e6c785")});
            col.AddChild(new Label{Text="108拠点 / 本拠候補3人・3勢力 / 人物未収録105拠点・領域未収録"});
            personnelSite=new OptionButton();col.AddChild(personnelSite);
            foreach(var site in NationalPersonnel.Coverage()){personnelSite.AddItem(site.Name+" / "+site.Status);personnelSite.SetItemMetadata(personnelSite.ItemCount-1,site.Node);}
            var scroll=new ScrollContainer{SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(850,315)};col.AddChild(scroll);
            personnelBody=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(personnelBody);
            personnelSite.ItemSelected+=_=>RefreshPersonnelCatalog();
        }
        RefreshPersonnelCatalog();personnelDialog.PopupCentered();
    }
    void RefreshPersonnelCatalog()
    {
        var c=NationalPersonnel.Catalog;string node=personnelSite.GetItemMetadata(personnelSite.Selected).AsString();
        var coverage=NationalPersonnel.Coverage().Single(s=>s.Node==node);
        string people=string.Join("\n\n",c.People.Where(p=>p.HomeCandidate==node).Select(p=>{
            var faction=c.Factions.Single(f=>f.Id==p.Faction);
            return p.Name+" / "+faction.Name+" / 本拠候補："+coverage.Name+"\n"+p.Certainty+"。現在地・役職・主従・命令権は別です。\n"+
                string.Join("\n",p.SourceIds.Select(id=>{var source=c.Sources.Single(s=>s.Id==id);return source.Title+"（確認 "+source.CheckedOn+"）\n"+source.Fact+"\n"+source.Url;}));
        }));
        personnelBody.Text=coverage.Name+"："+coverage.Status+"\n\n"+(people.Length==0?"この拠点の人物・勢力は未収録。領主や仕事を推測で補完しません。":people)+
            "\n\n仕事は試作主君/書役の在所点検だけです。史実の人物へ架空の命令権を付け足しません。\n"+c.Policy;
    }
    async Task VerifyPersonnelCatalog()
    {
        string before=ContractJson.CanonicalHash(S.DiagnosticSnapshot());Click("personnel-catalog");
        foreach(string node in new[]{"castle","kofu","odawara","shuri"}){
            int index=Enumerable.Range(0,personnelSite.ItemCount).Single(i=>personnelSite.GetItemMetadata(i).AsString()==node);
            personnelSite.Select(index);personnelSite.EmitSignal(OptionButton.SignalName.ItemSelected,index);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Check(personnelDialog!.Visible&&personnelSite.ItemCount==108,"actual catalog dialog and all national sites "+node);
            Check(personnelBody.Text.Contains(node=="shuri"?"未収録":node=="castle"?"織田信長":node=="kofu"?"武田信玄":"北条氏康"),"selected site displays correct provenance/coverage "+node);
            Check(personnelDialog.Position.X>=0&&personnelDialog.Position.Y>=0&&personnelDialog.Position.X+personnelDialog.Size.X<=GetViewportRect().Size.X&&personnelDialog.Position.Y+personnelDialog.Size.Y<=GetViewportRect().Size.Y,"catalog fits actual rendered window "+node);
            GetViewport().GetTexture().GetImage().SavePng(Output+"/"+Case+"-personnel-"+node+".png");
        }
        personnelDialog!.GetOkButton().EmitSignal(Button.SignalName.Pressed);personnelDialog.Hide();
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        Check(!personnelDialog.Visible&&ContractJson.CanonicalHash(S.DiagnosticSnapshot())==before,"catalog open/select/close preserves clock, knowledge, tasks and world");
    }
}
