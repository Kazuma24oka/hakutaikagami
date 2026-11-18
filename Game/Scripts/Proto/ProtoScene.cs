using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using HakutaiKagami.Core.Proto;

// 試作版の「話す場面」（2026-11-18 ユーザー「軍議とか話しているときは別画面を前に出してしゃべっている感じを」）。
// 地図の上に暗い幕を下ろし、話している人のちびコマを大きく並べ、名前と台詞を一つずつ送る。開いている間は時間が止まる。
// 最後に選ぶもの（軍議の陣・出立・合図など）があれば、台詞の後に下へ出す。
public partial class ProtoScene : CanvasLayer
{
    public bool Open => Visible;
    readonly ProtoGame game;
    Control root = null!, stage = null!, bottom = null!;
    Label name = null!, text = null!;
    List<(string Who, string Text)> lines = [];
    int index;
    Control? final;
    Action? done;
    readonly Dictionary<string, TextureRect> portraits = new();

    public ProtoScene(ProtoGame game) { this.game = game; Layer = 8; Visible = false; }

    public override void _Ready()
    {
        root = new Control { Theme = new Theme { DefaultFontSize = 22 } }; root.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(root);
        var shade = new ColorRect { Color = new Color(0.04f, 0.04f, 0.06f, 0.93f) }; shade.SetAnchorsPreset(Control.LayoutPreset.FullRect); root.AddChild(shade);
        shade.GuiInput += e => { if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left && final == null) Advance(); };
        stage = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        stage.SetAnchorsPreset(Control.LayoutPreset.FullRect); stage.OffsetBottom = -260; stage.OffsetTop = 40; stage.AddThemeConstantOverride("separation", 80); root.AddChild(stage);
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.09f, 0.09f, 0.11f), BorderColor = new Color(0.55f, 0.48f, 0.32f), BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2, ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 16, ContentMarginBottom = 16, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 }); panel.SetAnchorsPreset(Control.LayoutPreset.BottomWide); panel.OffsetTop = -250; panel.OffsetBottom = -16; panel.OffsetLeft = 40; panel.OffsetRight = -40; root.AddChild(panel);
        bottom = new VBoxContainer(); panel.AddChild(bottom);
        name = new Label { Modulate = new Color(1, 0.9f, 0.6f) }; bottom.AddChild(name);
        text = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; bottom.AddChild(text);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible || final != null) return;
        if (e is InputEventKey k && k.Pressed && !k.Echo && k.Keycode is Key.Enter or Key.Space or Key.KpEnter) { Advance(); GetViewport().SetInputAsHandled(); }
    }

    // lines：(話す人の id, 台詞)。people：場面に並べる人（話さない人も）。final：台詞の後に出す選ぶもの。
    public void Show(IEnumerable<(string Who, string Text)> talk, IEnumerable<string>? people = null, Control? choose = null, Action? onDone = null)
    {
        lines = talk.Where(l => l.Text != "").ToList(); index = 0; final = choose; done = onDone;
        foreach (var c in stage.GetChildren()) { stage.RemoveChild(c); c.QueueFree(); }
        portraits.Clear();
        var cast = (people ?? []).Concat(lines.Select(l => l.Who)).Distinct().ToList();
        if (cast.Remove(game.World.Player.Id)) cast.Insert(0, game.World.Player.Id); // 本人はいつも左
        foreach (var id in cast)
        {
            var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = Control.MouseFilterEnum.Ignore };
            var pic = new TextureRect { Texture = DotOfficerActor.Frame(ProtoGame.SpriteKey(game.World, game.World.Person(id))), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(300, 300), TextureFilter = CanvasItem.TextureFilterEnum.Nearest, FlipH = id != game.World.Player.Id, MouseFilter = Control.MouseFilterEnum.Ignore };
            col.AddChild(pic); col.AddChild(new Label { Text = game.World.Person(id).Name, HorizontalAlignment = HorizontalAlignment.Center });
            stage.AddChild(col); portraits[id] = pic;
        }
        Visible = true; ShowLine();
    }

    void ShowLine()
    {
        if (index < lines.Count)
        {
            var (who, said) = lines[index]; var p = game.World.Person(who);
            name.Text = p.Player ? p.Name : $"{p.Name}（{p.Role}）"; text.Text = said;
            foreach (var (id, pic) in portraits) pic.Modulate = id == who ? Colors.White : new Color(0.45f, 0.45f, 0.5f);
            return;
        }
        if (final != null && final.GetParent() == null)
        {
            name.Text = ""; text.Text = ""; text.Visible = false;
            foreach (var pic in portraits.Values) pic.Modulate = Colors.White;
            bottom.AddChild(final); return;
        }
        Close();
    }

    void Advance() { if (index < lines.Count) { index++; ShowLine(); } }
    public void Next() => Advance(); // 動画の台本から送る
    public bool Choosing => Visible && final != null && final.GetParent() != null;

    public void Close()
    {
        if (final != null && final.GetParent() == bottom) { bottom.RemoveChild(final); final.QueueFree(); }
        final = null; text.Visible = true; Visible = false; var d = done; done = null; d?.Invoke(); game.RefreshUi();
    }

    // 「名前（役）「台詞」」や「名前「台詞」」から、話す人と台詞を取り出す。取り出せなければ null。
    public static (string Who, string Text)? Parse(ProtoWorld w, string said)
    {
        int open = said.IndexOf('「'); if (open <= 0 || !said.EndsWith("」")) return null;
        var head = said[..open]; var paren = head.IndexOf('（'); if (paren > 0) head = head[..paren];
        var p = w.People.FirstOrDefault(x => x.Name == head); if (p == null) return null;
        return (p.Id, said[(open + 1)..^1]);
    }
}
