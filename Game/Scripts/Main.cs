using Godot;
using HakutaiKagami.Core.World;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class Main : Control
{
    private WorldState world = null!;
    private Label details = null!;
    private Label status = null!;
    private Label castleDetails = null!;
    private string? selectedId;
    private readonly System.Collections.Generic.List<Button> buttons = new();

    public override void _Ready()
    {
        world = PrototypeScenario.Load(FileAccess.GetFileAsString("res://Data/prototype.json"));
        var theme = new Theme { DefaultFontSize = 21 };
        theme.DefaultFont = new SystemFont { FontNames = new[] { "Yu Gothic UI", "Meiryo", "sans-serif" } };
        Theme = theme;
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 36);
        var tabs = new TabContainer();
        tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(tabs);
        margin.Name = "武将・城";
        tabs.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 18);
        margin.AddChild(column);
        column.AddChild(new Label { Text = "百代鏡  /  MIRROR OF AGES", Modulate = new Color("e2c891") });
        column.AddChild(new Label { Text = "最小基盤の技術検証 — 仮データ", Modulate = new Color("a6b7c7") });
        column.AddChild(new HSeparator());
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 36);
        column.AddChild(row);
        var list = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
        list.AddThemeConstantOverride("separation", 12);
        row.AddChild(list);
        list.AddChild(new Label { Text = "武将一覧  /  3人" });
        foreach (var definition in world.Definition.Characters)
        {
            string id = definition.Id;
            var button = new Button { Text = definition.Name, CustomMinimumSize = new Vector2(300, 62), ToggleMode = true };
            button.Pressed += () => SelectCharacter(id);
            list.AddChild(button);
            buttons.Add(button);
        }
        details = new Label { Text = "武将を選択してください", CustomMinimumSize = new Vector2(560, 245) };
        row.AddChild(details);
        column.AddChild(new HSeparator());
        castleDetails = new Label();
        column.AddChild(castleDetails);
        status = new Label { Text = $"Seed：{world.Definition.Seed}   世界時刻：{world.Tick}（進行未接続）\n検証用の管理表示です。正式なNPC認識・権限UIは未実装。", Modulate = new Color("a6b7c7") };
        column.AddChild(status);
        RefreshCastle();
        SelectCharacter(world.Definition.Characters[0].Id);
        GD.Print("FOUNDATION_READY characters=3 castles=1 seed=" + world.Definition.Seed);
        var taxPanel = new TaxPanel { Name = "徴税・保存検証" };
        tabs.AddChild(taxPanel);
        taxPanel.Initialize(FileAccess.GetFileAsString("res://Data/prototype.json"));
        taxPanel.WorldChanged += updated =>
        {
            world = updated;
            RefreshCastle();
            SelectCharacter(selectedId ?? world.Definition.Characters[0].Id);
            status.Text = $"Seed：{world.Definition.Seed}   世界時刻：{world.Tick}（徴税検証と共通）\n検証用管理表示。正式な認識制限付きUIは未実装。";
        };
        if (OS.GetCmdlineUserArgs().Contains("--delivery-smoke-test")) { tabs.CurrentTab = 1; _ = taxPanel.DeliveryRecoverySmokeTest(); }
        else if (OS.GetCmdlineUserArgs().Contains("--tax-smoke-test")) { tabs.CurrentTab = 1; _ = taxPanel.SmokeTest(); }
        else if (OS.GetCmdlineUserArgs().Contains("--smoke-test")) _ = SmokeTest();
    }

    private void RefreshCastle()
    {
        var castle = world.Castles.Values.Single();
        string owner = world.Definition.Organizations[0].Id;
        var stock = world.Resources.Single(r => r.OwnerOrganizationId == owner);
        string organizationName = world.Definition.Organizations.Single(o => o.Id == castle.ControllerOrganizationId).Name;
        castleDetails.Text = $"城情報  /  {world.Definition.Castles.Single().Name}\n城ID：{castle.Id}    城主：{world.Definition.Characters.Single(c => c.Id == castle.LordId).Name}\n実効支配：{organizationName}\n鏡家の保管資源：米 {stock.Rice}俵 / 金 {stock.Gold}";
    }

    private void SelectCharacter(string id)
    {
        selectedId = id;
        var character = world.Characters[id];
        var definition = world.Definition.Characters.Single(c => c.Id == id);
        details.Text = $"武将詳細\n\n{definition.Name}\n所属：{world.Definition.Organizations.Single(o => o.Id == character.OrganizationId).Name}\n役職：{character.Role}\n所在：{world.Definition.Castles.Single(c => c.Id == character.CastleId).Name}\nID：{character.Id}";
        for (int i = 0; i < buttons.Count; i++) buttons[i].SetPressedNoSignal(world.Definition.Characters[i].Id == id);
        GD.Print($"CHARACTER_SELECTED id={id} role={character.Role}");
    }

    private async Task SmokeTest()
    {
        try
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].EmitSignal(BaseButton.SignalName.Pressed);
                if (selectedId != world.Definition.Characters[i].Id || !details.Text.Contains(world.Characters[selectedId].Role))
                    throw new InvalidOperationException("Selection/details mismatch.");
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                string path = ProjectSettings.GlobalizePath("res://../Docs/Proposals/Evidence/godot-foundation.png");
                DirAccess.MakeDirRecursiveAbsolute(System.IO.Path.GetDirectoryName(path)!);
                if (GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok)
                    throw new InvalidOperationException("Screenshot failed.");
            }
            GD.Print("SCENE_SMOKE_PASS selections=3 details=3 castle=1");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
