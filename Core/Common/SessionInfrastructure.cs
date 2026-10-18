using System.Text.Json;
using System.Text.Json.Serialization;
using HakutaiKagami.Core.Commands;

namespace HakutaiKagami.Core.Common;

public static class SnapshotCodec
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, Converters = { new JsonStringEnumConverter() } };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException("Empty snapshot.");
    public static T Copy<T>(T value) => Read<T>(Write(value));
    public static void SaveFile(string path, string json)
    {
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, json); File.Move(temp, full, overwrite: true);
    }
}
public static class SessionTransaction
{
    public static T Apply<T>(T current, Action<T> change, Action<T> validate, Action? beforeCommit = null)
    {
        var next = SnapshotCodec.Copy(current); change(next); validate(next); beforeCommit?.Invoke(); return next;
    }
}
public static class PermissionPolicy
{
    public static bool Allows(IEnumerable<Authority> grants, string actor, string target, string action, long tick) =>
        grants.Any(g => g.Allows(new("permission-check", actor, target, action, tick), tick));
}
public static class EffectLedger
{
    public static bool Mark(ref string[] applied, string id)
    {
        if (applied.Contains(id, StringComparer.Ordinal)) return false;
        applied = [.. applied, id]; return true;
    }
}
