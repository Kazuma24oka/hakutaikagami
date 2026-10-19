using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
public static class FormationWorldGate
{
    public static void Require(bool reviewed)
    {
        var root=ProjectSettings.GlobalizePath("res://..");var dir=Path.Combine(root,"Art/Officer3D/FormationWorldV1");var plan=Path.Combine(dir,"runtime-plan.json");
        string Hash(string p)=>Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(p))).ToLowerInvariant();
        using var p=JsonDocument.Parse(System.IO.File.ReadAllText(plan));foreach(var d in p.RootElement.GetProperty("dependencies").EnumerateArray())if(Hash(Path.Combine(root,d.GetProperty("path").GetString()!))!=d.GetProperty("sha256").GetString())throw new InvalidOperationException("布陣・連続地図の実動作を再検査してください。");
        if(!reviewed)return;
        using var v=JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(dir,"visual-review.json")));if(v.RootElement.GetProperty("runtime_plan_sha256").GetString()!=Hash(plan))throw new InvalidOperationException("布陣画面の画像再確認が必要です。");
        foreach(var e in v.RootElement.GetProperty("evidence_sha256").EnumerateObject())if(Hash(Path.Combine(dir,"runtime-local",e.Name))!=e.Value.GetString())throw new InvalidOperationException("布陣画面の確認画像が変わっています。");
    }
}
