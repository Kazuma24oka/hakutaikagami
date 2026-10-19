using Godot;
using System;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Linq;
public static class Officer3DV3Gate
{
    public static string Root=>ProjectSettings.GlobalizePath("res://..");
    static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(p))).ToLowerInvariant();
    public static void Require(bool reviewed)
    {
        string runtime="";
        foreach(var stage in new[]{"export","runtime"})
        {
            string file=Path.Combine(Root,"Art/Officer3D/TrialV3/"+stage+"-plan.json");using var plan=JsonDocument.Parse(System.IO.File.ReadAllText(file));
            foreach(var item in plan.RootElement.GetProperty("dependencies").EnumerateArray())
            {string p=item.GetProperty("path").GetString()!;p=Path.IsPathRooted(p)?p:Path.Combine(Root,p);if(Hash(p)!=item.GetProperty("sha256").GetString())throw new InvalidOperationException("3D検証の入力が変更されています: "+p);}
            if(stage=="runtime")runtime=Hash(file);
        }
        if(reviewed)
        {
            using var visual=JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(Root,"Art/Officer3D/TrialV3/visual-review.json")));
            if(visual.RootElement.GetProperty("runtime_plan_sha256").GetString()!=runtime)throw new InvalidOperationException("3D画像の再検査が必要です");
            foreach(var item in visual.RootElement.GetProperty("evidence_sha256").EnumerateObject())if(Hash(Path.Combine(Root,"Art/Officer3D/TrialV3/runtime-local",item.Name))!=item.Value.GetString())throw new InvalidOperationException("3D確認画像が変更されています");
        }
    }
}
