using Godot;
using System;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
public static class OfficerWorldGate
{
    static string Root=>ProjectSettings.GlobalizePath("res://..");
    static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(p))).ToLowerInvariant();
    public static void Require(bool reviewed)
    {
        string dir=Path.Combine(Root,"Art/Officer3D/WorldIntegrationV1");string plan=Path.Combine(dir,"runtime-plan.json");
        using var data=JsonDocument.Parse(System.IO.File.ReadAllText(plan));
        foreach(var item in data.RootElement.GetProperty("dependencies").EnumerateArray())
            if(Hash(Path.Combine(Root,item.GetProperty("path").GetString()!))!=item.GetProperty("sha256").GetString())throw new InvalidOperationException("武将・合戦の検査入力が変更されています："+item.GetProperty("path").GetString());
        if(!reviewed)return;
        using var review=JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(dir,"visual-review.json")));
        if(review.RootElement.GetProperty("runtime_plan_sha256").GetString()!=Hash(plan))throw new InvalidOperationException("武将・合戦の画像再検査が必要です。");
        foreach(var item in review.RootElement.GetProperty("evidence_sha256").EnumerateObject())if(Hash(Path.Combine(dir,"runtime-local",item.Name))!=item.Value.GetString())throw new InvalidOperationException("武将・合戦の確認画像が変わっています。");
    }
}
