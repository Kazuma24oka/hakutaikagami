using Godot;
using HakutaiKagami.Core.Contracts;
using System.IO;
using System;
using System.Linq;

public partial class NationalFactionRelationsCheck:Control
{
    public override async void _Ready()
    {
        try
        {
            var start=NationalStart.Scenario();
            var relation=NationalFactionRelations.Initial(start).Relations.First(r=>r.SubjectFaction!=null&&r.OverlordFaction!=null);
            var person=start.Factions.Single(f=>f.Id==relation.SubjectFaction).Head;
            var store=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"national-relations-ui-"+System.Guid.NewGuid().ToString("N")));
            var profile=NationalStart.New(store,start,person,20261112,null,false);
            var runtime=new NewGameRuntime();runtime.Configure(store,profile,false);AddChild(runtime);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(!runtime.InfoText.Contains("主家・",StringComparison.Ordinal))throw new InvalidDataException("主家の関係がプレイヤー情報へ表示されません");
            var output=RecordPath.Globalize("res://../Docs/Design/NationalFactionRelationsV1/runtime-local/ui-check.json");Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Actor=person,Relation=runtime.InfoText.Split(" / ").LastOrDefault(x=>x.StartsWith("主家・",StringComparison.Ordinal)),Scope="当事者の家の関係を通常のプレイ画面へ表示",ComputerUse="未使用"},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            GD.Print("NATIONAL_FACTION_RELATIONS_UI_PASS");GetTree().Quit();
        }
        catch(System.Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
