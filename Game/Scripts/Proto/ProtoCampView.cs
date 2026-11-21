using Godot;
using System;
using HakutaiKagami.Core.Proto;

// 試作版の本陣の見え方（決まりは Core/Proto/ProtoCamp.cs）。2026-11-21 ユーザー「分かりにくいし、小さすぎるし、見た目がださい」で作り直した。
// ・本陣：隊の凸より大きい陣（幅120m）。家の色の帯が入った白い陣幕を三方に張り、前（南）は楯を並べて開ける。四隅と入口に背の高い幟、真ん中に大将旗（馬印）、
//   幕の内に床几と陣太鼓、左右に兵糧の俵と荷、入口に篝火。軍議で決めた所に、行軍の間から出す（着くまでは薄い名前だけ濃くしない）。取られたら幕も幟も黒くなる。
// ・遠くからでも分かるように、本陣の上に大きな名前と、家の色の旗の印を出す。
// ・指示と兵糧の届く範囲：本陣を中心にした輪（1,000m）。地形に隠れないように、いつも見える線で描く。
public partial class ProtoGame
{
    Node3D? campNode; MeshInstance3D? campRing; Label3D? campLabel; Sprite3D? campIcon; StandardMaterial3D? campCloth, campBand, campFlag, campRingMat;

    void BuildCamp()
    {
        campNode = new Node3D { Visible = false }; AddChild(campNode);
        var house = HouseColor("takano");
        campCloth = new StandardMaterial3D { AlbedoColor = new Color(0.96f, 0.95f, 0.91f) }; campBand = new StandardMaterial3D { AlbedoColor = house }; campFlag = new StandardMaterial3D { AlbedoColor = house };
        var wood = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.28f, 0.18f) }; var dark = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.14f, 0.13f) };
        var straw = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.68f, 0.38f) }; var white = new StandardMaterial3D { AlbedoColor = new Color(0.97f, 0.96f, 0.93f) };
        var fire = new StandardMaterial3D { AlbedoColor = new Color(1, 0.6f, 0.15f), EmissionEnabled = true, Emission = new Color(1, 0.5f, 0.1f), EmissionEnergyMultiplier = 2 };
        var ground = new StandardMaterial3D { AlbedoColor = new Color(0.74f, 0.68f, 0.54f) };
        MeshInstance3D Box(Vector3 size, Vector3 at, Material m) { var n = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = m, Position = at }; campNode.AddChild(n); return n; }
        const float W = 60, D = 48, H = 11; // 陣の半分の幅・半分の奥行き・幕の高さ
        // 踏み固めた地面。
        Box(new Vector3(W * 2 + 14, 0.6f, D * 2 + 14), new Vector3(0, 0.3f, 0), ground);
        // 陣幕：白い幕の上に家の色の帯、下に黒い裾。北・東・西の三方。
        void Curtain(Vector3 at, bool alongX, float len)
        {
            Vector3 S(float h, float t) => alongX ? new Vector3(len, h, t) : new Vector3(t, h, len);
            Box(S(H, 1), at + new Vector3(0, H / 2 + 1, 0), campCloth!); Box(S(2.4f, 1.3f), at + new Vector3(0, H - 1.5f, 0), campBand!); Box(S(1, 1.3f), at + new Vector3(0, 1.5f, 0), dark);
            for (float t = -len / 2; t <= len / 2 + 0.1f; t += len / 6) Box(new Vector3(1.6f, H + 4, 1.6f), at + (alongX ? new Vector3(t, 0, 0) : new Vector3(0, 0, t)) + new Vector3(0, (H + 4) / 2, 0), wood);
        }
        Curtain(new Vector3(0, 0, -D), true, W * 2); Curtain(new Vector3(-W, 0, 0), false, D * 2); Curtain(new Vector3(W, 0, 0), false, D * 2);
        // 前（南）は、入口をあけて左右に短い幕と、楯の並び。
        Curtain(new Vector3(-W + 16, 0, D), true, 32); Curtain(new Vector3(W - 16, 0, D), true, 32);
        for (float x = -22; x <= 22; x += 6.2f) if (Math.Abs(x) > 8) Box(new Vector3(5, 7, 0.8f), new Vector3(x, 3.5f, D + 6), wood);
        // 幟：四隅と入口の左右。背の高い竿に、家の色の長い旗（白い筋入り）。
        void Banner(float x, float z, float h)
        {
            Box(new Vector3(1.2f, h, 1.2f), new Vector3(x, h / 2, z), wood); Box(new Vector3(8, 1, 1), new Vector3(x + 3.4f, h - 1, z), wood);
            Box(new Vector3(7, h * 0.62f, 0.6f), new Vector3(x + 3.8f, h - 1.5f - h * 0.31f, z), campFlag!); Box(new Vector3(7.2f, 2.2f, 0.8f), new Vector3(x + 3.8f, h - 6, z), white);
        }
        foreach (var (x, z) in new[] { (-W, -D), (W - 8, -D), (-W, D), (W - 8, D), (-14f, D + 2), (10f, D + 2) }) Banner(x, z, 44);
        // 大将旗（馬印）：真ん中に、いちばん高い竿と大きな四角い旗。
        Box(new Vector3(1.8f, 62, 1.8f), new Vector3(0, 31, -12), wood); Box(new Vector3(20, 16, 0.8f), new Vector3(10.5f, 52, -12), campFlag!); Box(new Vector3(8, 8, 1), new Vector3(10.5f, 52, -12), white);
        // 幕の内：大将の床几と陣太鼓、軍議の盾机。
        Box(new Vector3(9, 3, 6), new Vector3(0, 1.8f, -26), wood); Box(new Vector3(16, 2.2f, 10), new Vector3(0, 1.5f, -6), wood);
        campNode.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 4, BottomRadius = 4, Height = 5 }, MaterialOverride = dark, Position = new Vector3(-22, 6, -28), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        Box(new Vector3(2, 5, 2), new Vector3(-22, 2.5f, -28), wood);
        // 兵糧：俵の山と荷駄（左右）。
        for (int i = 0; i < 3; i++) for (int j = 0; j < 4 - i; j++)
            campNode.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 2.6f, BottomRadius = 2.6f, Height = 9 }, MaterialOverride = straw, Position = new Vector3(-46 + j * 5.4f + i * 2.7f, 3 + i * 4.6f, 14), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        for (int i = 0; i < 2; i++) for (int j = 0; j < 3 - i; j++)
            campNode.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 2.6f, BottomRadius = 2.6f, Height = 9 }, MaterialOverride = straw, Position = new Vector3(-46 + j * 5.4f + i * 2.7f, 3 + i * 4.6f, 26), Rotation = new Vector3(Mathf.Pi / 2, 0, 0) });
        Box(new Vector3(12, 6, 8), new Vector3(42, 3.4f, 14), wood); Box(new Vector3(12, 6, 8), new Vector3(42, 3.4f, 26), wood); Box(new Vector3(10, 5, 7), new Vector3(42, 9, 20), wood);
        // 篝火：入口の左右。
        foreach (float x in new[] { -9f, 9f })
        {
            Box(new Vector3(1, 8, 1), new Vector3(x, 4, D - 6), dark); Box(new Vector3(4, 1.4f, 4), new Vector3(x, 8.5f, D - 6), dark);
            campNode.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 2, Height = 5, RadialSegments = 6 }, MaterialOverride = fire, Position = new Vector3(x, 11.5f, D - 6) });
        }
        campRingMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(1, 0.95f, 0.7f, 0.5f), NoDepthTest = true, RenderPriority = 12 };
        campRing = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.99f, OuterRadius = 1f, Rings = 128, RingSegments = 4 }, MaterialOverride = campRingMat, Visible = false }; AddChild(campRing);
        campLabel = new Label3D { Text = "本陣", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0016f, FontSize = 30, OutlineSize = 12, NoDepthTest = true, RenderPriority = 23, Modulate = new Color(1, 0.96f, 0.85f), Visible = false }; AddChild(campLabel);
        // 遠くからの印：家の色の旗（白い四角入り）。
        var img = Image.CreateEmpty(24, 28, false, Image.Format.Rgba8); img.Fill(new Color(0, 0, 0, 0));
        img.FillRect(new Rect2I(2, 0, 3, 28), new Color(0.2f, 0.15f, 0.1f)); img.FillRect(new Rect2I(5, 1, 18, 15), new Color(0.05f, 0.05f, 0.05f)); img.FillRect(new Rect2I(6, 2, 16, 13), house); img.FillRect(new Rect2I(11, 5, 6, 6), new Color(1, 1, 1));
        campIcon = new Sprite3D { Texture = ImageTexture.CreateFromImage(img), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.0022f, NoDepthTest = true, Shaded = false, TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, RenderPriority = 23, Visible = false }; AddChild(campIcon);
    }

    void UpdateCampView()
    {
        if (campNode == null) BuildCamp();
        bool war = World.War.Phase is "行軍" or "合戦" || World.Returning && World.CampAt("takano") != null;
        var plan = World.CampPlan; bool show = war && plan != null;
        campNode!.Visible = campRing!.Visible = campLabel!.Visible = campIcon!.Visible = show; if (!show) return;
        var at = ToScene(plan!.Value.X, plan.Value.Y); bool set = World.CampAt("takano") != null, lost = World.CampLost("takano");
        campNode.Position = at;
        float lift = 70 + distance * 0.035f; campLabel.Position = at + new Vector3(0, lift, 0);
        campIcon.Position = campLabel.Position + (Camera?.GlobalBasis.Y ?? Vector3.Up) * (distance * 0.05f); campIcon.Visible = distance > 500;
        var burnt = new Color(0.2f, 0.18f, 0.17f); var house = HouseColor("takano");
        campCloth!.AlbedoColor = lost ? burnt : new Color(0.96f, 0.95f, 0.91f); campBand!.AlbedoColor = lost ? burnt : house; campFlag!.AlbedoColor = lost ? burnt : house;
        campLabel.Text = lost ? "本陣（奪われた）" : "本陣";
        campLabel.Modulate = lost ? new Color(0.85f, 0.55f, 0.5f) : set ? new Color(1, 0.96f, 0.85f) : new Color(1, 0.96f, 0.85f, 0.6f);
        float r = ProtoWorld.CampRange / 100f; campRing.Position = at + new Vector3(0, 2, 0); campRing.Scale = new Vector3(r, Math.Max(1, distance / 60f), r);
        campRing.Visible = World.War.Result == "" && !lost;
        campRingMat!.AlbedoColor = new Color(1, 0.95f, 0.7f, set ? 0.6f : 0.3f);
    }
}
