using Godot;
using System;
using HakutaiKagami.Core.Proto;

// 試作版の本陣の見え方（決まりは Core/Proto/ProtoCamp.cs）。
// ・本陣：陣幕（白い幕を三方に張ったもの）と幟。軍議で決めた所に、行軍の間から出す（着くまでは薄く）。取られたら幕が黒くなる。
// ・指示と兵糧の届く範囲：本陣を中心にした輪（800m）。地形に隠れないように、いつも見える線で描く。
public partial class ProtoGame
{
    Node3D? campNode; MeshInstance3D? campRing; Label3D? campLabel; StandardMaterial3D? campCloth, campRingMat;

    void UpdateCampView()
    {
        if (campNode == null)
        {
            campNode = new Node3D { Visible = false }; AddChild(campNode);
            campCloth = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.94f, 0.9f) };
            var pole = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.25f, 0.18f) }; var flag = new StandardMaterial3D { AlbedoColor = HouseColor("takano") };
            void Box(Vector3 size, Vector3 at, Material m) => campNode.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = m, Position = at });
            // 陣幕：北・東・西の三方。南が開いている。
            Box(new Vector3(34, 5, 0.8f), new Vector3(0, 3.5f, -14), campCloth); Box(new Vector3(0.8f, 5, 28), new Vector3(-17, 3.5f, 0), campCloth); Box(new Vector3(0.8f, 5, 28), new Vector3(17, 3.5f, 0), campCloth);
            foreach (var (x, z) in new[] { (-17f, -14f), (17f, -14f), (-17f, 14f), (17f, 14f) }) Box(new Vector3(1, 7, 1), new Vector3(x, 3.5f, z), pole);
            // 幟と、大将の床几。
            Box(new Vector3(0.8f, 22, 0.8f), new Vector3(-10, 11, -10), pole); Box(new Vector3(0.5f, 12, 5), new Vector3(-10, 15, -7), flag);
            Box(new Vector3(0.8f, 22, 0.8f), new Vector3(10, 11, -10), pole); Box(new Vector3(0.5f, 12, 5), new Vector3(10, 15, -7), flag);
            Box(new Vector3(5, 1.6f, 3), new Vector3(0, 0.8f, -4), pole);
            campRingMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(1, 0.95f, 0.7f, 0.5f), NoDepthTest = true, RenderPriority = 12 };
            campRing = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.994f, OuterRadius = 1f, Rings = 96, RingSegments = 4 }, MaterialOverride = campRingMat, Visible = false }; AddChild(campRing);
            campLabel = new Label3D { Text = "本陣", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.001f, FontSize = 30, OutlineSize = 10, NoDepthTest = true, RenderPriority = 23, Modulate = new Color(1, 0.96f, 0.85f), Visible = false }; AddChild(campLabel);
        }
        bool war = World.War.Phase is "行軍" or "合戦" || World.Returning && World.CampAt("takano") != null;
        var plan = World.CampPlan; bool show = war && plan != null;
        campNode.Visible = campRing!.Visible = campLabel!.Visible = show; if (!show) return;
        var at = ToScene(plan!.Value.X, plan.Value.Y); bool set = World.CampAt("takano") != null, lost = World.CampLost("takano");
        campNode.Position = at; campLabel.Position = at + new Vector3(0, 30 + distance * 0.03f, 0);
        campCloth!.AlbedoColor = lost ? new Color(0.2f, 0.18f, 0.17f) : new Color(0.95f, 0.94f, 0.9f);
        campLabel.Modulate = lost ? new Color(0.7f, 0.5f, 0.5f) : set ? new Color(1, 0.96f, 0.85f) : new Color(1, 0.96f, 0.85f, 0.55f);
        float r = ProtoWorld.CampRange / 100f; campRing.Position = at + new Vector3(0, 2, 0); campRing.Scale = new Vector3(r, Math.Max(1, distance / 60f), r);
        campRing.Visible = World.War.Result == "" && !lost;
        campRingMat!.AlbedoColor = new Color(1, 0.95f, 0.7f, set ? 0.55f : 0.25f);
    }
}
