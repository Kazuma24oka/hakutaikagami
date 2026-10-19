using Godot;
using System;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
public static class MilitaryGridGate
{
    static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(p))).ToLowerInvariant();
    public static void Require(bool reviewed)
    {
        string root=ProjectSettings.GlobalizePath("res://.."),dir=Path.Combine(root,"Art/Officer3D/MilitaryGridV1"),plan=Path.Combine(dir,"runtime-plan.json");
        using var data=JsonDocument.Parse(System.IO.File.ReadAllText(plan));foreach(var d in data.RootElement.GetProperty("dependencies").EnumerateArray())
            if(Hash(Path.Combine(root,d.GetProperty("path").GetString()!))!=d.GetProperty("sha256").GetString())throw new InvalidOperationException("軍令・認識・地図の入力が変更されています："+d.GetProperty("path").GetString());
        if(!reviewed)return;
        using var review=JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(dir,"visual-review.json")));
        if(review.RootElement.GetProperty("runtime_plan_sha256").GetString()!=Hash(plan))throw new InvalidOperationException("軍令・認識・地図の画像再検査が必要です。");
        foreach(var e in review.RootElement.GetProperty("evidence_sha256").EnumerateObject())if(Hash(Path.Combine(dir,"runtime-local",e.Name))!=e.Value.GetString())throw new InvalidOperationException("確認画像が変更されています。");
    }
}
